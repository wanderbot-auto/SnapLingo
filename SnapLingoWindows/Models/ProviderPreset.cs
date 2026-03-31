namespace SnapLingoWindows.Models;

public enum ProviderProtocolStyle
{
    OpenAIResponses,
    OpenAIChatCompletions,
    AnthropicMessages,
    GeminiGenerateContent,
}

public sealed record ProviderPreset(
    ProviderKind Kind,
    ProviderProtocolStyle Style,
    string BaseUrl,
    string Model
);
