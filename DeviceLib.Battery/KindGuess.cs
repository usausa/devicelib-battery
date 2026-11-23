namespace DeviceLib.Battery;

internal static class KindGuess
{
    private static readonly string[] MouseWords = ["mouse", "trackball", "trackpad", "touchpad"];

    private static readonly string[] KeyboardWords = ["keyboard", "keypad"];

    private static readonly string[] GamepadWords = ["controller", "gamepad", "joystick"];

    private static readonly string[] HeadsetWords = ["headset", "headphone", "earphone", "earbud", "buds"];

    public static DeviceKind FromName(string name)
    {
        if (ContainsAny(name, MouseWords))
        {
            return DeviceKind.Mouse;
        }

        if (ContainsAny(name, KeyboardWords))
        {
            return DeviceKind.Keyboard;
        }

        if (ContainsAny(name, GamepadWords))
        {
            return DeviceKind.Gamepad;
        }

        return ContainsAny(name, HeadsetWords) ? DeviceKind.Headset : DeviceKind.Unknown;
    }

    private static bool ContainsAny(string text, ReadOnlySpan<string> words)
    {
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
