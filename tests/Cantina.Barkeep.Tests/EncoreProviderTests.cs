// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using Cantina.Barkeep.Acquisition;
using Cantina.Barkeep.Library;
using Cantina.Barkeep.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The Chorus Encore integration (D-032) against a scripted wire: the response parse, the
/// polite refusals by name, and the handoff that publishes a validated chart into the
/// acquisition watch directory — whole, under its final name, or not at all.
/// </summary>
public sealed class EncoreProviderTests
{
    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(answer(request));
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static EncoreChart Chart(string md5 = "0123456789abcdef0123456789abcdef") =>
        new(md5, "Everlong", "Foo Fighters", false, "The Colour and the Shape", "Hoph2o", "1997", 252673, false, SongInstruments.Unknown);

    private static EncoreOptions Politeness(string staging) => new()
    {
        SearchCooldownMilliseconds = 0,
        StagingDirectory = staging,
    };

    // ── The parse ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ASearchResponseMapsTheFieldsTheIpadNeeds()
    {
        const string body = """
            {"found":2,"out_of":2,"page":1,"data":[
              {"md5":"0123456789abcdef0123456789abcdef","name":"Everlong","artist":"Foo Fighters",
               "album":"The Colour and the Shape","charter":"Hoph2o","year":"1997",
               "song_length":252673,"hasVideoBackground":false,"someFutureField":42,
               "diff_guitar":3,"diff_drums":5,"diff_vocals":-1},
              {"md5":"not-a-hash","name":"Broken row is skipped"}
            ]}
            """;
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        });
        using var client = new EncoreClient(
            new SingleClientFactory(handler), Options.Create(Politeness(TempDirectory())));

        var result = await client.SearchAsync("everlong", CancellationToken.None);

        Assert.Equal(2, result.Found);
        var chart = Assert.Single(result.Charts);
        Assert.Equal("Everlong", chart.Name);
        Assert.Equal("Foo Fighters", chart.Artist);
        Assert.Equal("Hoph2o", chart.Charter);
        Assert.Equal("1997", chart.Year);
        Assert.Equal(252673, chart.SongLengthMilliseconds);

        // The instrument picture, in the same vocabulary the local library speaks:
        // charted guitar and drums, no vocals chart, bass and keys absent from the row.
        Assert.Equal(new Cantina.Barkeep.Library.SongInstruments(3, -1, 5, -1, -1), chart.Instruments);
        Assert.Contains("POST", Assert.Single(handler.Requests), StringComparison.Ordinal);
    }

    // ── The file name ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TheFileNameDropsWhatTheFilesystemRefuses()
    {
        var chart = new EncoreChart(
            "0123456789abcdef0123456789abcdef", "T.N.T.", "AC/DC", false, null, "some:charter", null, 1, false, SongInstruments.Unknown);

        Assert.Equal("ACDC - T.N.T. (somecharter).sng", EncoreDownloadCoordinator.FileNameFor(chart));
    }

    // ── The refusals ──────────────────────────────────────────────────────────────────

    [Fact]
    public void RefusesWhenThereIsNowhereToDeliver()
    {
        using var client = new EncoreClient(
            new SingleClientFactory(new ScriptedHandler(_ => new(HttpStatusCode.OK))),
            Options.Create(Politeness(TempDirectory())));
        using var coordinator = new EncoreDownloadCoordinator(
            client,
            Options.Create(Politeness(TempDirectory())),
            Options.Create(new AcquisitionOptions()),
            TimeProvider.System,
            NullLogger<EncoreDownloadCoordinator>.Instance);

        var download = coordinator.Request(Chart());

        Assert.Equal("refused", download.State);
        Assert.Contains("nowhere to deliver", download.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAChartAlreadyInTheWatchDirectory()
    {
        var watch = TempDirectory();
        File.WriteAllText(Path.Combine(watch, "Foo Fighters - Everlong (Hoph2o).sng"), "existing");
        using var client = new EncoreClient(
            new SingleClientFactory(new ScriptedHandler(_ => new(HttpStatusCode.OK))),
            Options.Create(Politeness(TempDirectory())));
        using var coordinator = new EncoreDownloadCoordinator(
            client,
            Options.Create(Politeness(TempDirectory())),
            Options.Create(new AcquisitionOptions { WatchDirectory = watch }),
            TimeProvider.System,
            NullLogger<EncoreDownloadCoordinator>.Instance);

        var download = coordinator.Request(Chart());

        Assert.Equal("refused", download.State);
        Assert.Contains("already in the watch directory", download.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePoliteHourlyCeilingRefusesByName()
    {
        var options = Politeness(TempDirectory());
        options.DownloadsPerHour = 1;
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(SngDocumentTests.Build([("name", "Song"), ("artist", "Artist")])),
        });
        using var client = new EncoreClient(new SingleClientFactory(handler), Options.Create(options));
        using var coordinator = new EncoreDownloadCoordinator(
            client,
            Options.Create(options),
            Options.Create(new AcquisitionOptions { WatchDirectory = TempDirectory() }),
            TimeProvider.System,
            NullLogger<EncoreDownloadCoordinator>.Instance);

        Assert.Equal("downloading", coordinator.Request(Chart("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")).State);

        var second = coordinator.Request(Chart("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));

        Assert.Equal("refused", second.State);
        Assert.Contains("polite ceiling", second.Detail, StringComparison.Ordinal);
    }

    // ── The handoff ───────────────────────────────────────────────────────────────────

    private static async Task<ProviderDownload> Terminal(EncoreDownloadCoordinator coordinator, string md5)
    {
        for (var waited = 0; waited < 100; waited++)
        {
            var current = coordinator.Recent.Single(download => download.Md5 == md5);

            if (current.State is not "downloading")
            {
                return current;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("the download never reached a terminal state");
    }

    [Fact]
    public async Task AValidChartIsDeliveredWholeUnderItsFinalName()
    {
        var watch = TempDirectory();
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(SngDocumentTests.Build([("name", "Everlong"), ("artist", "Foo Fighters")])),
        });
        using var client = new EncoreClient(new SingleClientFactory(handler), Options.Create(Politeness(TempDirectory())));
        using var coordinator = new EncoreDownloadCoordinator(
            client,
            Options.Create(Politeness(TempDirectory())),
            Options.Create(new AcquisitionOptions { WatchDirectory = watch }),
            TimeProvider.System,
            NullLogger<EncoreDownloadCoordinator>.Instance);

        var chart = Chart();
        Assert.Equal("downloading", coordinator.Request(chart).State);

        var final = await Terminal(coordinator, chart.Md5);

        Assert.Equal("delivered", final.State);
        Assert.True(File.Exists(Path.Combine(watch, "Foo Fighters - Everlong (Hoph2o).sng")));
        Assert.Contains($"GET https://files.enchor.us/{chart.Md5}.sng", handler.Requests);
    }

    [Fact]
    public async Task ADownloadThatIsNotAChartFailsByNameAndDeliversNothing()
    {
        var watch = TempDirectory();
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("this is an error page, not a chart"u8.ToArray()),
        });
        using var client = new EncoreClient(new SingleClientFactory(handler), Options.Create(Politeness(TempDirectory())));
        using var coordinator = new EncoreDownloadCoordinator(
            client,
            Options.Create(Politeness(TempDirectory())),
            Options.Create(new AcquisitionOptions { WatchDirectory = watch }),
            TimeProvider.System,
            NullLogger<EncoreDownloadCoordinator>.Instance);

        var chart = Chart();
        coordinator.Request(chart);

        var final = await Terminal(coordinator, chart.Md5);

        Assert.Equal("failed", final.State);
        Assert.Contains("not a valid chart", final.Detail, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(watch));
    }
}
