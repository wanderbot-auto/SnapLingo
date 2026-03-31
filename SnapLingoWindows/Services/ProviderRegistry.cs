namespace SnapLingoWindows.Services;

public sealed class ProviderRegistry
{
    private readonly SecureSecretStore secretStore;
    private readonly HttpClient httpClient;

    public ProviderRegistry(SecureSecretStore secretStore, HttpClient? httpClient = null)
    {
        this.secretStore = secretStore;
        this.httpClient = httpClient ?? new HttpClient();
    }

    public ProviderKind SelectedProvider { get; set; } = ProviderKind.OpenAI;

    public IProviderClient CurrentClient() => MakeClient(SelectedProvider);

    public IProviderClient MakeClient(ProviderKind provider)
    {
        var preset = GetPreset(provider);

        return preset.Style switch
        {
            ProviderProtocolStyle.OpenAIResponses => new OpenAIResponsesProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.OpenAIChatCompletions => new OpenAIChatProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.AnthropicMessages => new AnthropicMessagesProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.GeminiGenerateContent => new GeminiGenerateContentProvider(preset, secretStore, httpClient),
            _ => throw new InvalidOperationException($"Unsupported provider style: {preset.Style}"),
        };
    }

    public ProviderPreset GetPreset(ProviderKind provider) => provider switch
    {
        ProviderKind.OpenAI => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIResponses, "https://api.openai.com/v1", "gpt-4.1-mini"),
        ProviderKind.Anthropic => new ProviderPreset(provider, ProviderProtocolStyle.AnthropicMessages, "https://api.anthropic.com/v1", "claude-sonnet-4-20250514"),
        ProviderKind.Gemini => new ProviderPreset(provider, ProviderProtocolStyle.GeminiGenerateContent, "https://generativelanguage.googleapis.com/v1beta/models", "gemini-2.5-flash"),
        ProviderKind.ZhipuGLM => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIChatCompletions, "https://api.z.ai/api/paas/v4", "glm-4.5-air"),
        ProviderKind.Kimi => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIChatCompletions, "https://api.moonshot.cn/v1", "kimi-k2-turbo-preview"),
        ProviderKind.MiniMax => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIChatCompletions, "https://api.minimaxi.com/v1", "MiniMax-M2.5-highspeed"),
        ProviderKind.AlibabaBailian => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIChatCompletions, "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.5-flash"),
        ProviderKind.VolcengineArk => new ProviderPreset(provider, ProviderProtocolStyle.OpenAIChatCompletions, "https://ark.cn-beijing.volces.com/api/v3", "doubao-seed-1-6-250615"),
        _ => throw new InvalidOperationException($"Unsupported provider: {provider}"),
    };

    public string LoadKey(ProviderKind provider) => secretStore.LoadSecret(provider);

    public void SaveKey(string secret, ProviderKind provider) => secretStore.SaveSecret(secret, provider);

    public void DeleteKey(ProviderKind provider) => secretStore.DeleteSecret(provider);
}
