namespace SnapLingoWindows.Services;

public sealed class ProviderCatalog
{
    private const string ResourceName = "Resources.Providers.providers.json";
    private static readonly Lazy<ProviderCatalog> SharedCatalog = new(Load);

    private readonly IReadOnlyDictionary<ProviderKind, ProviderCatalogEntry> entries;

    public ProviderCatalog(IReadOnlyDictionary<ProviderKind, ProviderCatalogEntry> entries)
    {
        this.entries = entries;
    }

    public static ProviderCatalog Shared => SharedCatalog.Value;

    public ProviderPreset GetPreset(ProviderKind provider)
    {
        var entry = GetEntry(provider);
        return new ProviderPreset(provider, entry.Style, entry.BaseUrl, entry.DefaultModel);
    }

    public IReadOnlyList<string> GetPresetModels(ProviderKind provider)
    {
        return GetEntry(provider).PresetModels;
    }

    private ProviderCatalogEntry GetEntry(ProviderKind provider)
    {
        return entries.TryGetValue(provider, out var entry)
            ? entry
            : throw new InvalidOperationException($"Missing provider manifest entry for {provider}.");
    }

    private static ProviderCatalog Load()
    {
        var document = EmbeddedJsonResourceLoader.Load<ProviderCatalogDocument>(ResourceName);
        var entries = document.Providers.ToDictionary(
            entry => ParseProviderKind(entry.Id),
            entry => new ProviderCatalogEntry(
                ParseProtocolStyle(entry.Style),
                entry.BaseURL,
                entry.DefaultModel,
                entry.PresetModels));

        return new ProviderCatalog(entries);
    }

    private static ProviderKind ParseProviderKind(string id) => id switch
    {
        "openAI" => ProviderKind.OpenAI,
        "anthropic" => ProviderKind.Anthropic,
        "gemini" => ProviderKind.Gemini,
        "zhipuGLM" => ProviderKind.ZhipuGLM,
        "kimi" => ProviderKind.Kimi,
        "minimax" => ProviderKind.MiniMax,
        "aliyunBailian" => ProviderKind.AlibabaBailian,
        "volcengineArk" => ProviderKind.VolcengineArk,
        _ => throw new InvalidOperationException($"Unsupported provider manifest id: {id}"),
    };

    private static ProviderProtocolStyle ParseProtocolStyle(string style) => style switch
    {
        "openAIResponses" => ProviderProtocolStyle.OpenAIResponses,
        "openAIChatCompletions" => ProviderProtocolStyle.OpenAIChatCompletions,
        "anthropicMessages" => ProviderProtocolStyle.AnthropicMessages,
        "geminiGenerateContent" => ProviderProtocolStyle.GeminiGenerateContent,
        _ => throw new InvalidOperationException($"Unsupported provider manifest style: {style}"),
    };

    public sealed record ProviderCatalogEntry(
        ProviderProtocolStyle Style,
        string BaseUrl,
        string DefaultModel,
        IReadOnlyList<string> PresetModels);

    private sealed record ProviderCatalogDocument(IReadOnlyList<ProviderCatalogManifestEntry> Providers);

    private sealed record ProviderCatalogManifestEntry(
        string Id,
        string Style,
        string BaseURL,
        string DefaultModel,
        IReadOnlyList<string> PresetModels);
}
