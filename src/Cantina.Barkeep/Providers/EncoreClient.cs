// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Providers;

/// <summary>One chart as the iPad needs to see it: enough to choose, and the md5 to act.</summary>
public sealed record EncoreChart(
    string Md5,
    string Name,
    string Artist,
    string? Album,
    string? Charter,
    string? Year,
    long SongLengthMilliseconds,
    bool HasVideoBackground,
    Cantina.Barkeep.Library.SongInstruments Instruments);

public sealed record EncoreSearchResult(int Found, IReadOnlyList<EncoreChart> Charts, string? Refusal = null);

/// <summary>
/// The wire client for Chorus Encore, speaking the same two endpoints its own desktop
/// client (Geomitron Bridge, GPL-3.0) speaks: <c>POST /search</c> on the API host and
/// <c>GET /{md5}.sng</c> on the files host. Unknown response fields are ignored — the
/// schema is Encore's to grow — and every request carries Cantina's own User-Agent so
/// the operator of a donation-funded service can see who is calling and reach out or
/// block by name if Cantina ever misbehaves.
/// </summary>
public sealed class EncoreClient(IHttpClientFactory httpFactory, IOptions<EncoreOptions> options) : IDisposable
{
    public const string HttpClientName = "encore";

    public void Dispose() => _searchGate.Dispose();

    private readonly SemaphoreSlim _searchGate = new(1, 1);
    private long _lastSearchTicks;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class SearchResponse
    {
        public int Found { get; set; }

        public List<SearchRecord> Data { get; set; } = [];
    }

    private sealed class SearchRecord
    {
        public string? Md5 { get; set; }

        public string? Name { get; set; }

        public string? Artist { get; set; }

        public string? Album { get; set; }

        public string? Charter { get; set; }

        public string? Year { get; set; }

        [JsonPropertyName("song_length")]
        public long SongLength { get; set; }

        public bool HasVideoBackground { get; set; }

        [JsonPropertyName("diff_guitar")]
        public int DiffGuitar { get; set; } = -1;

        [JsonPropertyName("diff_bass")]
        public int DiffBass { get; set; } = -1;

        [JsonPropertyName("diff_drums")]
        public int DiffDrums { get; set; } = -1;

        [JsonPropertyName("diff_keys")]
        public int DiffKeys { get; set; } = -1;

        [JsonPropertyName("diff_vocals")]
        public int DiffVocals { get; set; } = -1;
    }

    public async Task<EncoreSearchResult> SearchAsync(string query, CancellationToken cancellation)
    {
        // A walking pace, enforced where the requests leave the building: concurrent
        // searches queue here and each waits out the cooldown before going to the wire.
        await _searchGate.WaitAsync(cancellation).ConfigureAwait(false);

        try
        {
            var since = TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSearchTicks)));
            var cooldown = TimeSpan.FromMilliseconds(options.Value.SearchCooldownMilliseconds);

            if (since < cooldown)
            {
                await Task.Delay(cooldown - since, cancellation).ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _lastSearchTicks, DateTime.UtcNow.Ticks);
        }
        finally
        {
            _searchGate.Release();
        }

        var client = httpFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            $"{options.Value.ApiBaseUrl}/search",
            new { search = query, page = 1, per_page = options.Value.PerPage },
            cancellation).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var parsed = await response.Content
            .ReadFromJsonAsync<SearchResponse>(Json, cancellation).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Encore answered with an empty body");

        var charts = parsed.Data
            .Where(record => record.Md5 is { Length: 32 } && record.Name is { Length: > 0 })
            .Select(record => new EncoreChart(
                record.Md5!,
                record.Name!,
                record.Artist ?? "Unknown artist",
                record.Album,
                record.Charter,
                record.Year,
                record.SongLength,
                record.HasVideoBackground,
                new Cantina.Barkeep.Library.SongInstruments(
                    record.DiffGuitar, record.DiffBass, record.DiffDrums, record.DiffKeys, record.DiffVocals)))
            .ToList();

        return new EncoreSearchResult(parsed.Found, charts);
    }

    /// <summary>
    /// Streams one chart to <paramref name="destination"/>. The caller owns validation
    /// and the final move — a downloaded byte stream proves nothing about being a chart.
    /// </summary>
    public async Task DownloadAsync(string md5, string destination, CancellationToken cancellation)
    {
        var client = httpFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync(
            $"{options.Value.FilesBaseUrl}/{md5}.sng",
            HttpCompletionOption.ResponseHeadersRead,
            cancellation).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var file = File.Create(destination);
        await response.Content.CopyToAsync(file, cancellation).ConfigureAwait(false);
    }
}
