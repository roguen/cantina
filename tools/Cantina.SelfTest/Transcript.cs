// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;

namespace Cantina.SelfTest;

internal enum Verdict
{
    Pass = 0,
    Fail = 1,
    Inconclusive = 2,
}

internal sealed record SuiteResult(string Suite, Verdict Verdict, string Claim);

/// <summary>House-style transcript: a monotonic clock on every line, verdicts named.</summary>
internal sealed class Transcript
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// The run header, and an attestation that is true.
    ///
    /// This line used to read <c>attest_no_input=true (links no SendInput/...)</c>, which
    /// was inherited from <c>Cantina.Spikes.YargSetlist</c> — where it is true, because that
    /// assembly links no input API at all — and was carried into this tool when the cue
    /// suite was added. It has been false ever since: <c>CueSuite</c> constructs
    /// <c>Win32YargActuator</c>, so this assembly links every one of those entry points.
    ///
    /// The project's rule is that innocence should be a property of the assembly rather
    /// than a claim in a log. This assembly cannot offer that, so it states the weaker
    /// truth instead of the stronger falsehood, and names where the strong guarantee lives.
    /// </summary>
    public void Banner(bool runSendsInput)
    {
        Log("RUN", $"wall_start={DateTimeOffset.Now:O}");
        Log("RUN", runSendsInput
            ? "attest_input=THIS RUN SENDS INPUT (the cue suite actuates and can start a song)"
            : "attest_input=none in this run (the assembly links SendInput for the cue suite; "
              + "for assembly-level innocence use Cantina.Spikes.YargSetlist)");
        Log("RUN", $"self_pid={Environment.ProcessId}");
    }

    public void Log(string tag, string message) =>
        Console.WriteLine($"[T+{_clock.Elapsed.TotalSeconds,8:0.000}] {tag,-10} {message}");

    public void Case(string suite, string name, bool pass, string detail) =>
        Log(pass ? "PASS" : "FAIL", $"{suite}/{name}: {detail}");

    public void Summary(IReadOnlyList<SuiteResult> results)
    {
        Log("SUMMARY", $"suites={results.Count}");

        foreach (var result in results)
        {
            Log("VERDICT", $"suite={result.Suite} result={result.Verdict} claim=\"{result.Claim}\"");
        }
    }
}
