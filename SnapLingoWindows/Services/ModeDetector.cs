namespace SnapLingoWindows.Services;

public static class ModeDetector
{
    public static TranslationMode Detect(string text)
    {
        return text.Any(IsCjk) ? TranslationMode.Translate : TranslationMode.Polish;
    }

    private static bool IsCjk(char value)
    {
        return value is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF';
    }
}
