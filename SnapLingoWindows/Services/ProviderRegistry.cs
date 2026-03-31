using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace SnapLingoWindows.Services;

public sealed class ProviderRegistry
{
    private readonly SecureSecretStore secretStore;
    private readonly HttpClient httpClient;
    private readonly Dictionary<ProviderKind, string> selectedModels = new();

    public ProviderRegistry(SecureSecretStore secretStore, HttpClient? httpClient = null)
    {
        this.secretStore = secretStore;
        this.httpClient = httpClient ?? new HttpClient();
    }

    public ProviderKind SelectedProvider { get; set; } = ProviderKind.OpenAI;

    public IProviderClient CurrentClient() => MakeClient(SelectedProvider);

    public IProviderClient MakeClient(ProviderKind provider)
    {
        var preset = GetResolvedPreset(provider);

        return preset.Style switch
        {
            ProviderProtocolStyle.OpenAIResponses => new OpenAIResponsesProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.OpenAIChatCompletions => new OpenAIChatProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.AnthropicMessages => new AnthropicMessagesProvider(preset, secretStore, httpClient),
            ProviderProtocolStyle.GeminiGenerateContent => new GeminiGenerateContentProvider(preset, secretStore, httpClient),
            _ => throw new InvalidOperationException($"Unsupported provider style: {preset.Style}"),
        };
    }

    public async Task<ProviderModelCatalog> FetchModelsAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var preset = GetPreset(provider);
        var apiKey = LoadKey(provider).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderException("Save an API key first, then SnapLingo can load models automatically.");
        }

        using var request = CreateModelsRequest(preset, apiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException($"Could not load models from {provider.DisplayName()}. HTTP {(int)response.StatusCode}.");
        }

        var root = JsonNode.Parse(body) ?? throw new ProviderException("The provider model list response was malformed.");

        var models = preset.Style switch
        {
            ProviderProtocolStyle.GeminiGenerateContent => ParseGeminiModels(root),
            ProviderProtocolStyle.AnthropicMessages => ParseAnthropicModels(root),
            ProviderProtocolStyle.OpenAIResponses or ProviderProtocolStyle.OpenAIChatCompletions => ParseOpenAICompatibleModels(root),
            _ => throw new ProviderException("SnapLingo does not know how to list models for this provider yet."),
        };

        if (models.Count == 0)
        {
            throw new ProviderException($"No usable text models were returned by {provider.DisplayName()}.");
        }

        var defaultModelId = ResolveDefaultModelId(models, preset.Model);
        return new ProviderModelCatalog(models, defaultModelId);
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

    public void SetSelectedModel(ProviderKind provider, string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            selectedModels.Remove(provider);
            return;
        }

        selectedModels[provider] = modelId.Trim();
    }

    public string ResolveModel(ProviderKind provider)
    {
        if (selectedModels.TryGetValue(provider, out var modelId) && !string.IsNullOrWhiteSpace(modelId))
        {
            return modelId;
        }

        return GetPreset(provider).Model;
    }

    private ProviderPreset GetResolvedPreset(ProviderKind provider)
    {
        var preset = GetPreset(provider);
        return preset with { Model = ResolveModel(provider) };
    }

    private static HttpRequestMessage CreateModelsRequest(ProviderPreset preset, string apiKey)
    {
        return preset.Style switch
        {
            ProviderProtocolStyle.GeminiGenerateContent => CreateGeminiModelsRequest(preset, apiKey),
            ProviderProtocolStyle.AnthropicMessages => CreateAnthropicModelsRequest(preset, apiKey),
            ProviderProtocolStyle.OpenAIResponses or ProviderProtocolStyle.OpenAIChatCompletions => CreateOpenAICompatibleModelsRequest(preset, apiKey),
            _ => throw new ProviderException("SnapLingo does not know how to list models for this provider yet."),
        };
    }

    private static HttpRequestMessage CreateOpenAICompatibleModelsRequest(ProviderPreset preset, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{preset.BaseUrl}/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static HttpRequestMessage CreateAnthropicModelsRequest(ProviderPreset preset, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{preset.BaseUrl}/models");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }

    private static HttpRequestMessage CreateGeminiModelsRequest(ProviderPreset preset, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, preset.BaseUrl);
        request.Headers.Add("x-goog-api-key", apiKey);
        return request;
    }

    private static IReadOnlyList<ProviderModelOption> ParseOpenAICompatibleModels(JsonNode root)
    {
        var models = (root["data"] as JsonArray ?? [])
            .Select(item =>
            {
                var id = item?["id"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(id) || !IsLikelyTextGenerationModel(id))
                {
                    return null;
                }

                return new ProviderModelOption(
                    id,
                    id,
                    ParseUnixSeconds(item?["created"])
                );
            })
            .Where(option => option is not null)
            .Cast<ProviderModelOption>()
            .OrderByDescending(option => option.CreatedAt)
            .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }

    private static IReadOnlyList<ProviderModelOption> ParseAnthropicModels(JsonNode root)
    {
        var models = (root["data"] as JsonArray ?? [])
            .Select(item =>
            {
                var id = item?["id"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    return null;
                }

                var displayName = item?["display_name"]?.GetValue<string>()?.Trim();
                var label = string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, id, StringComparison.OrdinalIgnoreCase)
                    ? id
                    : $"{displayName} ({id})";

                return new ProviderModelOption(
                    id,
                    label,
                    ParseDateTime(item?["created_at"])
                );
            })
            .Where(option => option is not null)
            .Cast<ProviderModelOption>()
            .OrderByDescending(option => option.CreatedAt)
            .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }

    private static IReadOnlyList<ProviderModelOption> ParseGeminiModels(JsonNode root)
    {
        var models = (root["models"] as JsonArray ?? [])
            .Select(item =>
            {
                var supportedMethods = item?["supportedGenerationMethods"] as JsonArray;
                var supportsGenerateContent = supportedMethods?.Any(method =>
                    string.Equals(method?.GetValue<string>(), "generateContent", StringComparison.OrdinalIgnoreCase)) ?? false;
                if (!supportsGenerateContent)
                {
                    return null;
                }

                var rawName = item?["name"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    return null;
                }

                var id = rawName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                    ? rawName["models/".Length..]
                    : rawName;

                if (!IsLikelyTextGenerationModel(id))
                {
                    return null;
                }

                var displayName = item?["displayName"]?.GetValue<string>()?.Trim();
                var label = string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, id, StringComparison.OrdinalIgnoreCase)
                    ? id
                    : $"{displayName} ({id})";

                return new ProviderModelOption(id, label);
            })
            .Where(option => option is not null)
            .Cast<ProviderModelOption>()
            .OrderByDescending(option => ExtractModelVersionScore(option.Id))
            .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }

    private static string ResolveDefaultModelId(IReadOnlyList<ProviderModelOption> models, string presetModelId)
    {
        if (models.Count == 0)
        {
            throw new ProviderException("No models were available.");
        }

        if (models.Any(model => model.CreatedAt.HasValue))
        {
            return models
                .OrderByDescending(model => model.CreatedAt)
                .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .First()
                .Id;
        }

        if (models.Any(model => string.Equals(model.Id, presetModelId, StringComparison.OrdinalIgnoreCase)))
        {
            return models.First(model => string.Equals(model.Id, presetModelId, StringComparison.OrdinalIgnoreCase)).Id;
        }

        return models
            .OrderByDescending(model => ExtractModelVersionScore(model.Id))
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .First()
            .Id;
    }

    private static DateTimeOffset? ParseUnixSeconds(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<long>(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return null;
    }

    private static DateTimeOffset? ParseDateTime(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var rawValue) &&
            DateTimeOffset.TryParse(rawValue, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return null;
    }

    private static bool IsLikelyTextGenerationModel(string id)
    {
        var normalized = id.Trim().ToLowerInvariant();
        string[] excludedFragments =
        {
            "embedding",
            "rerank",
            "moderation",
            "tts",
            "whisper",
            "transcribe",
            "transcription",
            "speech",
            "image",
            "vision-preview",
        };

        return excludedFragments.All(fragment => !normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static long ExtractModelVersionScore(string id)
    {
        var digits = id
            .Select(character => char.IsDigit(character) ? character : ' ')
            .ToArray();
        var segments = new string(digits).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        long score = 0;
        foreach (var segment in segments.Take(4))
        {
            if (!long.TryParse(segment, out var value))
            {
                continue;
            }

            score = checked((score * 100000L) + value);
        }

        return score;
    }
}
