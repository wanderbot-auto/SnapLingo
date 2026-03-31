namespace SnapLingoWindows.Models;

public enum ShortcutPreset
{
    ControlAltSpace,
    ControlShiftSpace,
    ControlShiftAltSpace,
    ControlAltK,
    ControlShiftAltK,
}

public static class ShortcutPresetExtensions
{
    public static ShortcutPreset DefaultPreset => ShortcutPreset.ControlAltSpace;

    public static string DisplayName(this ShortcutPreset preset) => preset switch
    {
        ShortcutPreset.ControlAltSpace => "Ctrl + Alt + Space",
        ShortcutPreset.ControlShiftSpace => "Ctrl + Shift + Space",
        ShortcutPreset.ControlShiftAltSpace => "Ctrl + Shift + Alt + Space",
        ShortcutPreset.ControlAltK => "Ctrl + Alt + K",
        ShortcutPreset.ControlShiftAltK => "Ctrl + Shift + Alt + K",
        _ => preset.ToString(),
    };

    public static string CompactLabel(this ShortcutPreset preset) => preset switch
    {
        ShortcutPreset.ControlAltSpace => "Ctrl+Alt+Space",
        ShortcutPreset.ControlShiftSpace => "Ctrl+Shift+Space",
        ShortcutPreset.ControlShiftAltSpace => "Ctrl+Shift+Alt+Space",
        ShortcutPreset.ControlAltK => "Ctrl+Alt+K",
        ShortcutPreset.ControlShiftAltK => "Ctrl+Shift+Alt+K",
        _ => preset.ToString(),
    };

    public static HotkeyRegistration ToHotkeyRegistration(this ShortcutPreset preset) => preset switch
    {
        ShortcutPreset.ControlAltSpace => new HotkeyRegistration(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            0x20
        ),
        ShortcutPreset.ControlShiftSpace => new HotkeyRegistration(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            0x20
        ),
        ShortcutPreset.ControlShiftAltSpace => new HotkeyRegistration(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            0x20
        ),
        ShortcutPreset.ControlAltK => new HotkeyRegistration(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            0x4B
        ),
        ShortcutPreset.ControlShiftAltK => new HotkeyRegistration(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            0x4B
        ),
        _ => throw new InvalidOperationException($"Unsupported shortcut preset: {preset}"),
    };
}

public readonly record struct HotkeyRegistration(uint Modifiers, uint VirtualKey);
