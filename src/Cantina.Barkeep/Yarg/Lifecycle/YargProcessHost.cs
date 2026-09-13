// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;

namespace Cantina.Barkeep.Yarg.Lifecycle;

/// <summary>The operating-system seam under the lifecycle service, so it tests without YARG.</summary>
public interface IYargProcessHost
{
    int Count();

    /// <summary>Starts the executable. Throws with the operating system's reason on failure.</summary>
    void Start(string executablePath);

    /// <summary>Asks YARG to close its window and waits. True when every instance exited.</summary>
    bool RequestClose(TimeSpan timeout);

    /// <summary>Terminates YARG and waits. True when every instance exited.</summary>
    bool Kill(TimeSpan timeout);
}

/// <summary>
/// The real process host. Starts YARG through the shell so it gets an ordinary window and
/// inherits none of Barkeep's handles: Barkeep itself runs hidden with its output
/// redirected to a log, and a child that inherited those handles would keep the log open.
/// </summary>
public sealed class Win32YargProcessHost : IYargProcessHost
{
    private const string ProcessName = "YARG";

    public int Count()
    {
        var processes = Process.GetProcessesByName(ProcessName);

        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length;
    }

    public void Start(string executablePath)
    {
        using var started = Process.Start(new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = true,
        });
    }

    public bool RequestClose(TimeSpan timeout) => Stop(timeout, process => process.CloseMainWindow());

    public bool Kill(TimeSpan timeout) => Stop(timeout, process =>
    {
        process.Kill(entireProcessTree: true);
        return true;
    });

    private static bool Stop(TimeSpan timeout, Func<Process, bool> ask)
    {
        var processes = Process.GetProcessesByName(ProcessName);
        var deadline = DateTime.UtcNow + timeout;

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    ask(process);
                }
                catch (InvalidOperationException)
                {
                    // Already gone between the listing and the request.
                }
            }

            foreach (var process in processes)
            {
                var remaining = deadline - DateTime.UtcNow;

                if (remaining > TimeSpan.Zero)
                {
                    process.WaitForExit(remaining);
                }
            }

            return processes.All(process =>
            {
                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            });
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
