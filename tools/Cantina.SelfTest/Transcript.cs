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

    public void Banner()
    {
        Log("RUN", $"wall_start={DateTimeOffset.Now:O}");
        Log("RUN", "attest_no_input=true (links no SendInput/keybd_event/mouse_event/SetForegroundWindow)");
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
