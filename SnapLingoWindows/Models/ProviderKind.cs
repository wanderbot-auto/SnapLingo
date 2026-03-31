namespace SnapLingoWindows.Models;

public enum ProviderKind
{
    OpenAI,
    Anthropic,
    Gemini,
    ZhipuGLM,
    Kimi,
    MiniMax,
    AlibabaBailian,
    VolcengineArk,
}

public static class ProviderKindExtensions
{
    public static string DisplayName(this ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => "OpenAI",
        ProviderKind.Anthropic => "Anthropic",
        ProviderKind.Gemini => "Google Gemini",
        ProviderKind.ZhipuGLM => "Zhipu GLM",
        ProviderKind.Kimi => "Kimi",
        ProviderKind.MiniMax => "MiniMax",
        ProviderKind.AlibabaBailian => "Alibaba Bailian",
        ProviderKind.VolcengineArk => "Volcengine Ark",
        _ => kind.ToString(),
    };

    public static string ApiKeyPlaceholder(this ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => "OpenAI API key",
        ProviderKind.Anthropic => "Anthropic API key",
        ProviderKind.Gemini => "Gemini API key",
        ProviderKind.ZhipuGLM => "Zhipu GLM API key",
        ProviderKind.Kimi => "Kimi API key",
        ProviderKind.MiniMax => "MiniMax API key",
        ProviderKind.AlibabaBailian => "DashScope API key",
        ProviderKind.VolcengineArk => "ARK API key",
        _ => "API key",
    };

    public static string SecretFileName(this ProviderKind kind) => kind switch
    {
        ProviderKind.OpenAI => "openai-api-key.bin",
        ProviderKind.Anthropic => "anthropic-api-key.bin",
        ProviderKind.Gemini => "gemini-api-key.bin",
        ProviderKind.ZhipuGLM => "zhipu-glm-api-key.bin",
        ProviderKind.Kimi => "kimi-api-key.bin",
        ProviderKind.MiniMax => "minimax-api-key.bin",
        ProviderKind.AlibabaBailian => "alibaba-bailian-api-key.bin",
        ProviderKind.VolcengineArk => "volcengine-ark-api-key.bin",
        _ => $"{kind.ToString().ToLowerInvariant()}.bin",
    };
}
