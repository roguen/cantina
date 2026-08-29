// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Cantina.Barkeep.Yarg.Control;

namespace Cantina.SelfTest;

/// <summary>
/// Reads the readiness signals of docs/failure-behavior.md and reports each. This is
/// the observation half of the D-024 gate, run standalone: nothing here acts, and a
/// signal that fails names itself the way a refused cue would name it to the iPad.
/// </summary>
internal static partial class ReadinessSuite
{
    public static SuiteResult Run(Transcript transcript)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SuiteResult("readiness", Verdict.Inconclusive, "Windows-only signals");
        }

        return RunWindows(transcript);
    }

    [SupportedOSPlatform("windows")]
    private static SuiteResult RunWindows(Transcript transcript)
    {
        var yargProcesses = Process.GetProcessesByName("YARG");
        var processAlive = yargProcesses.Length == 1;
        transcript.Case("readiness", "process-alive", processAlive,
            processAlive ? $"one YARG, pid {yargProcesses[0].Id}" : $"{yargProcesses.Length} YARG instances");

        if (!processAlive)
        {
            return new SuiteResult("readiness", Verdict.Inconclusive,
                yargProcesses.Length == 0
                    ? "YARG is not running - every other signal is moot"
                    : "multiple YARG instances - the oracle is ambiguous");
        }

        // Deliverability: whether this host would swallow injected input silently. Read
        // through the production actuator rather than reimplemented here, so the suite
        // reports what a cue would actually decide.
        var actuator = new Win32YargActuator(
            Microsoft.Extensions.Options.Options.Create(new YargCueOptions()));
        var blocked = actuator.InputBlockedReason();
        transcript.Case("readiness", "input-deliverable", blocked is null,
            blocked ?? "no integrity, session, or desktop barrier between Barkeep and YARG");

        _ = GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
        var foreground = foregroundPid == (uint)yargProcesses[0].Id;
        string foregroundOwner;

        try
        {
            foregroundOwner = Process.GetProcessById((int)foregroundPid).ProcessName;
        }
        catch (ArgumentException)
        {
            foregroundOwner = "unknown";
        }

        // Foreground is reported, not required, for a PASS here: this suite may be run
        // from a terminal, which then rightly holds the screen. The cue gate requires it;
        // the readiness report states it.
        transcript.Case("readiness", "foreground", pass: true,
            foreground
                ? "YARG holds the screen"
                : $"another application has the screen: {foregroundOwner} (pid {foregroundPid}) - a cue would fail closed here");

        return new SuiteResult("readiness", blocked is null ? Verdict.Pass : Verdict.Fail,
            $"signals read: process=1, input={(blocked ?? "deliverable")}, "
            + $"foreground={(foreground ? "YARG" : foregroundOwner)}; "
            + "stream signals are the live suite's verdict");
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
