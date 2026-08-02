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
    public static unsafe uint SendKeyPress(ushort scanCode, bool extended, int holdMilliseconds)
    {
        var flags = KeyEventScanCode | (extended ? KeyEventExtendedKey : 0);

        var down = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput { ScanCode = scanCode, Flags = flags },
            },
        };

        var up = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput { ScanCode = scanCode, Flags = flags | KeyEventKeyUp },
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
}
