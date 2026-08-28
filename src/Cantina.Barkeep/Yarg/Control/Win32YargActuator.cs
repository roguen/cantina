// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// The Windows implementation of the proven input primitives. Everything here was
/// measured before it was written: scan-code+virtual-key injection (D-014, D-017), the
/// pointer click the search field requires because it has no keyboard focus route
/// (D-017), and foreground verified by observation because SetForegroundWindow reports
/// success in cases where the window never came forward (D-014).
///
/// Key-up always follows key-down through a try/finally: the spikes' SendKeyPress lacked
/// one, and a process killed mid-hold leaves a key logically down and autorepeating into
/// a UI where hold is a first-class gesture (D-024's stuck-key hazard).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class Win32YargActuator(IOptions<YargCueOptions> options) : IYargActuator
{
    public int YargProcessCount() => Process.GetProcessesByName("YARG").Length;

    public bool TryFocusYarg()
    {
        var processes = Process.GetProcessesByName("YARG");

        if (processes.Length != 1)
        {
            return false;
        }

        var window = processes[0].MainWindowHandle;

        if (window == 0)
        {
            return false;
        }

        if (IsIconic(window))
        {
            _ = ShowWindow(window, ShowRestore);
            Thread.Sleep(250);
        }

        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var thisThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread
            && AttachThreadInput(thisThread, foregroundThread, true);

        try
        {
            _ = SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(thisThread, foregroundThread, false);
            }
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (GetForegroundWindow() == window)
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    public (bool IsYargForeground, string Owner) ForegroundState()
    {
        _ = GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
        var processes = Process.GetProcessesByName("YARG");

        if (processes.Length == 1 && foregroundPid == (uint)processes[0].Id)
        {
            return (true, "YARG");
        }

        try
        {
            return (false, Process.GetProcessById((int)foregroundPid).ProcessName);
        }
        catch (ArgumentException)
        {
            return (false, "unknown");
        }
    }

    public bool ClickSearchBox()
    {
        var settings = options.Value;
        _ = SetCursorPos(settings.SearchBoxX, settings.SearchBoxY);
        Thread.Sleep(300);
        MouseEvent(MouseLeftDown);
        Thread.Sleep(60);
        MouseEvent(MouseLeftUp);
        Thread.Sleep(600);
        return true;
    }

    public bool ClearSearch()
    {
        for (var i = 0; i < 40; i++)
        {
            if (SendKeyPress(scanCode: 0x0E, virtualKey: 0x08, holdMilliseconds: 12) < 2)
            {
                return false;
            }

            Thread.Sleep(15);
        }

        Thread.Sleep(400);
        return true;
    }

    public bool TypeQuery(string query)
    {
        // Resolve every character before sending anything: typing half a query and then
        // failing selects something nobody asked for.
        var keys = new (ushort Scan, ushort VirtualKey)[query.Length];

        for (var i = 0; i < query.Length; i++)
        {
            if (!KeyMap.TryResolve(query[i], out var scan, out var virtualKey))
            {
                return false;
            }

            keys[i] = (scan, virtualKey);
        }

        foreach (var (scan, virtualKey) in keys)
        {
            if (SendKeyPress(scan, virtualKey, holdMilliseconds: 25) < 2)
            {
                return false;
            }

            Thread.Sleep(35);
        }

        return true;
    }

    public bool PressEnter() => SendKeyPress(scanCode: 0x1C, virtualKey: 0x0D, holdMilliseconds: 60) == 2;

    public bool PressEscape() => SendKeyPress(scanCode: 0x01, virtualKey: 0x1B, holdMilliseconds: 60) == 2;

    private const int ShowRestore = 9;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;

    private static uint SendKeyPress(ushort scanCode, ushort virtualKey, int holdMilliseconds)
    {
        var accepted = SendKeyEvent(scanCode, virtualKey, keyUp: false);

        try
        {
            Thread.Sleep(holdMilliseconds);
        }
        finally
        {
            accepted += SendKeyEvent(scanCode, virtualKey, keyUp: true);
        }

        return accepted;
    }

    private static unsafe uint SendKeyEvent(ushort scanCode, ushort virtualKey, bool keyUp)
    {
        var input = new Input
        {
            Type = 1,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = keyUp ? 0x0002u : 0u,
                },
            },
        };

        return SendInput(1, &input, sizeof(Input));
    }

    private static unsafe void MouseEvent(uint flags)
    {
        var input = new Input
        {
            Type = 0,
            Union = new InputUnion { Mouse = new MouseInput { Flags = flags } },
        };

        _ = SendInput(1, &input, sizeof(Input));
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static unsafe partial uint SendInput(uint count, Input* inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attachTo, uint attachFrom, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }
}

/// <summary>
/// US-layout character map, virtual key + scan code — the shape a real keyboard produces,
/// which is what reaches a Unity text field (D-017). Promoted from the input spike.
/// </summary>
internal static class KeyMap
{
    public static bool TryResolve(char value, out ushort scanCode, out ushort virtualKey)
    {
        var lower = char.ToLowerInvariant(value);
        scanCode = lower switch
        {
            'a' => 0x1E,
            'b' => 0x30,
            'c' => 0x2E,
            'd' => 0x20,
            'e' => 0x12,
            'f' => 0x21,
            'g' => 0x22,
            'h' => 0x23,
            'i' => 0x17,
            'j' => 0x24,
            'k' => 0x25,
            'l' => 0x26,
            'm' => 0x32,
            'n' => 0x31,
            'o' => 0x18,
            'p' => 0x19,
            'q' => 0x10,
            'r' => 0x13,
            's' => 0x1F,
            't' => 0x14,
            'u' => 0x16,
            'v' => 0x2F,
            'w' => 0x11,
            'x' => 0x2D,
            'y' => 0x15,
            'z' => 0x2C,
            '1' => 0x02,
            '2' => 0x03,
            '3' => 0x04,
            '4' => 0x05,
            '5' => 0x06,
            '6' => 0x07,
            '7' => 0x08,
            '8' => 0x09,
            '9' => 0x0A,
            '0' => 0x0B,
            ' ' => 0x39,
            '-' => 0x0C,
            '\'' => 0x28,
            ',' => 0x33,
            '.' => 0x34,
            _ => 0,
        };

        virtualKey = lower switch
        {
            >= 'a' and <= 'z' => (ushort)char.ToUpperInvariant(lower),
            >= '0' and <= '9' => (ushort)lower,
            ' ' => 0x20,
            '-' => 0xBD,
            '\'' => 0xDE,
            ',' => 0xBC,
            '.' => 0xBE,
            _ => 0,
        };

        return scanCode != 0 && virtualKey != 0;
    }
}
