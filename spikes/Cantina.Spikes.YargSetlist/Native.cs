// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Cantina.Spikes.YargSetlist;

[StructLayout(LayoutKind.Sequential)]
internal struct LastInputInfo
{
    public uint Size;
    public uint Time;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XInputGamepad
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short ThumbLX;
    public short ThumbLY;
    public short ThumbRX;
    public short ThumbRY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XInputState
{
    public uint PacketNumber;
    public XInputGamepad Gamepad;
}

/// <summary>
/// Read-only Win32 surface. Every import here observes; none of them act.
///
/// This is the load-bearing property of the whole harness. The measurement is "did YARG
/// advance without anything pressing anything", and the datagram carries no input
/// provenance whatsoever: a score screen dismissed by a controller button produces exactly
/// the same bytes as one YARG advanced by itself. The harness therefore has to prove its
/// own innocence, and the strongest available proof is that it never links the functions
/// that could be guilty.
///
/// There is deliberately no SendInput, no keybd_event, no mouse_event and no
/// SetForegroundWindow in this assembly. The last one matters as much as the others:
/// PauseOnFocusLoss is true, so taking foreground would resume a paused game and
/// manufacture a state change with no key behind it.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Native
{
    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    internal static partial uint XInputGetState(uint userIndex, out XInputState state);

    /// <summary>
    /// The keys that could dismiss a score screen, plus the modifiers that could be stuck
    /// down from an earlier injection. SendKeyPress has no try/finally around its hold, so
    /// a staging process killed mid-hold leaves a key logically down and autorepeating into
    /// a UI where "hold" is a first-class gesture.
    /// </summary>
    internal static readonly (int Vk, string Name)[] WatchedKeys =
    [
        (0x0D, "ENTER"), (0x1B, "ESCAPE"), (0x20, "SPACE"), (0x08, "BACKSPACE"), (0x09, "TAB"),
        (0x26, "UP"), (0x28, "DOWN"), (0x25, "LEFT"), (0x27, "RIGHT"),
        (0x10, "SHIFT"), (0x11, "CONTROL"), (0x12, "ALT"), (0x5B, "LWIN"), (0x5C, "RWIN"),
    ];

    internal static bool TryGetStuckKeys(out string stuck)
    {
        var names = new List<string>();

        foreach (var (vk, name) in WatchedKeys)
        {
            // High bit set means the key is physically down right now.
            if ((GetAsyncKeyState(vk) & 0x8000) != 0)
            {
                names.Add(name);
            }
        }

        stuck = string.Join(",", names);
        return names.Count > 0;
    }

    internal static uint LastInputTicks()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? info.Time : 0;
    }
}
