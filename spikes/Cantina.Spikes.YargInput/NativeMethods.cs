// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Cantina.Spikes.YargInput;

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HardwareInput
{
    public uint Message;
    public ushort ParamL;
    public ushort ParamH;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)]
    public MouseInput Mouse;

    [FieldOffset(0)]
    public KeyboardInput Keyboard;

    [FieldOffset(0)]
    public HardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint Type;
    public InputUnion Union;
}

/// <summary>
/// The minimum Win32 surface needed to prove whether stock YARG accepts synthetic input.
///
/// Scan codes are used rather than virtual keys because Unity's Input System reads raw
/// input, where the scan code is what identifies the key.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    public const uint InputKeyboard = 1;
    public const uint KeyEventScanCode = 0x0008;
    public const uint KeyEventExtendedKey = 0x0001;
    public const uint KeyEventKeyUp = 0x0002;

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static unsafe partial uint SendInput(uint count, Input* inputs, int size);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    /// <summary>
    /// Sends one key press and release. Returns the number of events Windows accepted,
    /// which is 2 on success. A value below 2 means the injection itself was refused,
    /// which is a different failure from YARG ignoring a delivered key.
    /// </summary>
    public static unsafe uint SendKeyPress(
        ushort scanCode,
        bool extended,
        int holdMilliseconds,
        ushort virtualKey = 0)
    {
        // Two injection shapes, because they are not equivalent to every consumer.
        //
        // Scan-code-only is what raw input reads, and it demonstrably drives YARG's menu
        // actions: Escape pauses, Enter confirms. But a text field is fed by the character
        // that Windows derives during message translation, and a capture on 2026-08-03
        // showed 17 typed characters never reaching YARG's search box while Enter still
        // worked. Supplying the virtual key as well is the shape a real keyboard produces.
        var useVirtualKey = virtualKey != 0;

        var flags = extended ? KeyEventExtendedKey : 0;

        if (!useVirtualKey)
        {
            flags |= KeyEventScanCode;
        }

        var down = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = flags,
                },
            },
        };

        var up = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = flags | KeyEventKeyUp,
                },
            },
        };

        var size = sizeof(Input);
        uint sent;

        sent = SendInput(1, &down, size);

        if (holdMilliseconds > 0)
        {
            Thread.Sleep(holdMilliseconds);
        }

        sent += SendInput(1, &up, size);
        return sent;
    }
}

/// <summary>Scan codes for the keys a menu-driving control path would need.</summary>
internal static class ScanCodes
{
    public static bool TryResolve(string name, out ushort scanCode, out bool extended)
    {
        extended = false;

        switch (name.ToLowerInvariant())
        {
            case "escape" or "esc":
                scanCode = 0x01;
                return true;
            case "enter" or "return":
                scanCode = 0x1C;
                return true;
            case "space":
                scanCode = 0x39;
                return true;
            case "backspace":
                scanCode = 0x0E;
                return true;
            case "up":
                scanCode = 0x48;
                extended = true;
                return true;
            case "down":
                scanCode = 0x50;
                extended = true;
                return true;
            case "left":
                scanCode = 0x4B;
                extended = true;
                return true;
            case "right":
                scanCode = 0x4D;
                extended = true;
                return true;
            default:
                scanCode = 0;
                return false;
        }
    }

    public static string Known => "escape, enter, space, backspace, up, down, left, right";

    // US layout, scan code set 1. Search in YARG's song list is driven by typing directly
    // into the list, so a selection spike needs printable characters as well as named keys.
    private static readonly Dictionary<char, ushort> Printable = new()
    {
        ['a'] = 0x1E,
        ['b'] = 0x30,
        ['c'] = 0x2E,
        ['d'] = 0x20,
        ['e'] = 0x12,
        ['f'] = 0x21,
        ['g'] = 0x22,
        ['h'] = 0x23,
        ['i'] = 0x17,
        ['j'] = 0x24,
        ['k'] = 0x25,
        ['l'] = 0x26,
        ['m'] = 0x32,
        ['n'] = 0x31,
        ['o'] = 0x18,
        ['p'] = 0x19,
        ['q'] = 0x10,
        ['r'] = 0x13,
        ['s'] = 0x1F,
        ['t'] = 0x14,
        ['u'] = 0x16,
        ['v'] = 0x2F,
        ['w'] = 0x11,
        ['x'] = 0x2D,
        ['y'] = 0x15,
        ['z'] = 0x2C,
        ['1'] = 0x02,
        ['2'] = 0x03,
        ['3'] = 0x04,
        ['4'] = 0x05,
        ['5'] = 0x06,
        ['6'] = 0x07,
        ['7'] = 0x08,
        ['8'] = 0x09,
        ['9'] = 0x0A,
        ['0'] = 0x0B,
        [' '] = 0x39,
        ['-'] = 0x0C,
        ['\''] = 0x28,
        [','] = 0x33,
        ['.'] = 0x34,
    };

    /// <summary>
    /// Resolves a printable character. Only characters this map covers can be typed; anything
    /// else is reported rather than silently dropped, because a query that types differently
    /// from what was requested would invalidate the whole selection result.
    /// </summary>
    public static bool TryResolveChar(char value, out ushort scanCode) =>
        Printable.TryGetValue(char.ToLowerInvariant(value), out scanCode);

    /// <summary>
    /// Virtual-key code for a printable character, US layout. Letters and digits map to
    /// their ASCII uppercase value; the rest are the documented OEM constants.
    /// </summary>
    public static bool TryResolveCharVirtualKey(char value, out ushort virtualKey)
    {
        var lower = char.ToLowerInvariant(value);

        if (lower is >= 'a' and <= 'z')
        {
            virtualKey = (ushort)char.ToUpperInvariant(lower);
            return true;
        }

        if (lower is >= '0' and <= '9')
        {
            virtualKey = lower;
            return true;
        }

        virtualKey = lower switch
        {
            ' ' => 0x20,
            '-' => 0xBD,
            '\'' => 0xDE,
            ',' => 0xBC,
            '.' => 0xBE,
            _ => 0,
        };

        return virtualKey != 0;
    }

    public const ushort VirtualKeyBackspace = 0x08;
    public const ushort VirtualKeyEnter = 0x0D;
}
