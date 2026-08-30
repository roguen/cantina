// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Acquisition;
using Cantina.Barkeep.Library;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// Regressions for the defects the pre-PR adversarial review confirmed (D-030's review
/// pass). Each test names the failure it pins shut.
/// </summary>
public sealed class AcquisitionReviewRegressionTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task ATrailingSeparatorOnTheWatchDirectoryDoesNotRejectEverything()
    {
        // Path.GetFullPath preserves a trailing separator, and the containment check
        // compared against it un-trimmed — so a watch directory pasted as "C:\songs\"
        // rejected every legitimate arrival as escaping the root, with acquisition
        // entirely dead and a misleading reason.
        var root = TempDirectory();
        File.WriteAllBytes(
            Path.Combine(root, "fine.sng"),
            SngDocumentTests.Build([("name", "Song"), ("artist", "Artist")]));

        var port = new FileArrivalPort(Options.Create(new AcquisitionOptions
        {
            WatchDirectory = root + Path.DirectorySeparatorChar,
            StabilityProbeMilliseconds = 1,
        }));
        var candidate = new SongArrivalCandidate("test", "fine.sng", "f");

        var first = await port.ProbeAsync(candidate, CancellationToken.None);
        var second = await port.ProbeAsync(candidate, CancellationToken.None);

        Assert.NotEqual(SongArrivalProbeState.Rejected, first.State);
        Assert.Equal(SongArrivalProbeState.Ready, second.State);
    }

    [Fact]
    public void AHostileLengthNearIntMaxIsRefusedByNameNotThrown()
    {
        // offset + length overflows int and wraps negative, sailing past a naive bounds
        // check into an exception — which would have faulted the startup library scan and
        // stopped the host.
        var bytes = SngDocumentTests.Build([("name", "X")], claimedPairCount: 2);
        var stubLength = "file-table-and-payloads"u8.Length;
        var index = bytes.Length - stubLength;

        // Overwrite the stub's first four bytes with a crafted key length of int.MaxValue.
        bytes[index] = 0xFF;
        bytes[index + 1] = 0xFF;
        bytes[index + 2] = 0xFF;
        bytes[index + 3] = 0x7F;

        // The claimed metadata length must cover the crafted pair for the guard to be the
        // thing under test, so extend it to the whole remainder.
        var metadataLength = (ulong)(bytes.Length - 42);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(26, 8), metadataLength);

        using var stream = new MemoryStream(bytes);

        Assert.False(SngDocument.TryRead(stream, out _, out var reason));
        Assert.StartsWith("sng-", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownReceiptOutcomeFromAFutureBinaryDoesNotPreventStartup()
    {
        // The journal outlives binaries. A rolled-back Barkeep reading a newer outcome
        // name must skip the line and treat the key as claimable, not refuse to boot.
        var directory = TempDirectory();
        var lease = "{\"kind\":\"lease\",\"key\":\"k1\",\"fingerprint\":\"fp-1\",\"leaseId\":\"l1\",\"outcome\":null,\"failureCode\":null}";
        var receipt = "{\"kind\":\"receipt\",\"key\":\"k1\",\"fingerprint\":\"fp-1\",\"leaseId\":\"l1\",\"outcome\":\"OutcomeFromTheFuture\",\"failureCode\":null}";
        File.WriteAllText(
            Path.Combine(directory, "acquisition-journal.jsonl"),
            lease + "\n" + receipt + "\n");

        using var journal = AcquisitionJournal.Open(directory);
        var claim = await journal.ClaimAsync(
            "k1", new SongArrivalCandidate("test", "a.sng", "fp-1"), CancellationToken.None);

        Assert.Equal(ImportPlayNextClaimState.Acquired, claim.State);
    }

    [Fact]
    public async Task ATornTailDoesNotSwallowTheNextLineWrittenAfterRestart()
    {
        // A kill mid-append leaves a fragment with no newline. Without repair, the first
        // line appended after restart concatenates onto it and BOTH records vanish on the
        // replay after that.
        var directory = TempDirectory();
        var path = Path.Combine(directory, "acquisition-journal.jsonl");
        var candidate = new SongArrivalCandidate("test", "a.sng", "fp-1");

        File.WriteAllText(path, "{\"kind\":\"lease\",\"key\":\"torn\",\"finge");

        using (var journal = AcquisitionJournal.Open(directory))
        {
            var claim = await journal.ClaimAsync("k1", candidate, CancellationToken.None);
            await journal.FinalizeAsync("k1", candidate, claim.LeaseId!,
                new ImportPlayNextTerminalReceipt(ImportPlayNextOutcome.Completed, null),
                CancellationToken.None);
        }

        using var reopened = AcquisitionJournal.Open(directory);
        var replay = await reopened.ClaimAsync("k1", candidate, CancellationToken.None);

        Assert.Equal(ImportPlayNextClaimState.Terminal, replay.State);
        Assert.Equal(ImportPlayNextOutcome.Completed, replay.TerminalReceipt!.Outcome);
    }
}
