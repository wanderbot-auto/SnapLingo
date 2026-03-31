namespace SnapLingoWindows.Models;

public sealed record PromptProfile(
    string Id,
    string Name,
    string TranslatePrompt,
    string PolishPrompt,
    bool IsBuiltIn = false)
{
    public const string DefaultId = "default";
    public const string DefaultName = "Default";

    public static PromptProfile CreateDefault() => new(
        DefaultId,
        DefaultName,
        "Translate the user's text into natural, professional English. Return only the final translated text.",
        "Rewrite the user's text into natural, professional English that is ready to send. Keep the meaning intact. Return only the final polished text.",
        true
    );
}
