// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The setlist surface end to end through the real pipeline: journaled command in,
/// projected state out, and the idempotent replay a client relies on after a retry.
/// </summary>
public sealed class SetlistEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SetlistEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Yarg:Enabled", "false")
                .UseSetting("Setlist:DataDirectory",
                    Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName()))).CreateClient();
    }

    private static StringContent Command(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task AddsThenReadsBackAndReplaysIdempotently()
    {
        const string add =
            """{"commandId":"cmd-1","kind":"Add","entry":{"hash":"h1","title":"Detonation","artist":"Trivium"}}""";

        using var first = await _client.PostAsync("/api/setlist/commands", Command(add));
        first.EnsureSuccessStatusCode();
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal("Done", firstBody.RootElement.GetProperty("outcome").GetString());
        Assert.False(firstBody.RootElement.GetProperty("replayed").GetBoolean());

        // A client retry after a lost response: same command id, answered from the
        // journal, applied exactly once (D-023).
        using var second = await _client.PostAsync("/api/setlist/commands", Command(add));
        using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(secondBody.RootElement.GetProperty("replayed").GetBoolean());

        using var view = JsonDocument.Parse(await _client.GetStringAsync("/api/setlist"));
        var entries = view.RootElement.GetProperty("state").GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("Detonation", entries[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task AMissingCommandIdIsRejected()
    {
        using var response = await _client.PostAsync(
            "/api/setlist/commands",
            Command("""{"commandId":"","kind":"Clear"}"""));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
