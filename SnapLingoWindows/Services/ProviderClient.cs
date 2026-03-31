using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SnapLingoWindows.Services;

public interface IProviderClient
{
    Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken);
    Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken);
}

public sealed class ProviderException : Exception
{
    public ProviderException(string message)
        : base(message)
    {
    }
}

public static class ProviderValidation
{
    public static ProviderOutput Validate(ProviderOutput output, string input, TranslationMode mode)
    {
        var normalized = output.Text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ProviderException("The provider returned an empty result.");
        }

        if (mode == TranslationMode.Translate &&
            string.Equals(normalized, input.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ProviderException("The provider returned a result SnapLingo cannot trust.");
        }

        return new ProviderOutput(normalized);
    }
}

internal abstract class ProviderClientBase : IProviderClient
{
    private readonly ProviderPreset preset;
    private readonly PromptProfile promptProfile;
    private readonly SecureSecretStore secretStore;
    private readonly HttpClient httpClient;

    protected ProviderClientBase(ProviderPreset preset, PromptProfile promptProfile, SecureSecretStore secretStore, HttpClient httpClient)
    {
        this.preset = preset;
        this.promptProfile = promptProfile;
        this.secretStore = secretStore;
        this.httpClient = httpClient;
    }

    public abstract Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken);
    public abstract Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken);

    protected ProviderPreset Preset => preset;
    protected PromptProfile PromptProfile => promptProfile;

    protected string LoadApiKey()
    {
        var key = secretStore.LoadSecret(preset.Kind);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ProviderException("Add an API key in Settings to use translation.");
        }

        return key.Trim();
    }

    protected async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException($"The provider request failed with HTTP {(int)response.StatusCode}.");
        }

        return JsonNode.Parse(body) ?? throw new ProviderException("The provider response was malformed.");
    }

    protected static StringContent JsonContent(object body)
    {
        return new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    }

    protected static string JoinTextParts(JsonArray? parts)
    {
        if (parts is null)
        {
            return string.Empty;
        }

        return string.Join(
            "\n",
            parts.Select(part => part?["text"]?.GetValue<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text))
        ).Trim();
    }
}

internal sealed class OpenAIResponsesProvider : ProviderClientBase
{
    public OpenAIResponsesProvider(ProviderPreset preset, PromptProfile promptProfile, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/responses")
        {
            Content = JsonContent(new
            {
                model = Preset.Model,
                instructions,
                input = text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoadApiKey());

        var root = await SendAsync(request, cancellationToken);

        if (root["output_text"]?.GetValue<string>() is string outputText &&
            !string.IsNullOrWhiteSpace(outputText))
        {
            return new ProviderOutput(outputText.Trim());
        }

        if (root["output"] is JsonArray outputArray)
        {
            var chunks = outputArray
                .SelectMany(item => item?["content"] as JsonArray ?? [])
                .Select(content => content?["text"]?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value));

            var combined = string.Join("\n", chunks).Trim();
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return new ProviderOutput(combined);
            }
        }

        throw new ProviderException("The provider response was malformed.");
    }
}

internal sealed class OpenAIChatProvider : ProviderClientBase
{
    public OpenAIChatProvider(ProviderPreset preset, PromptProfile promptProfile, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/chat/completions")
        {
            Content = JsonContent(new
            {
                model = Preset.Model,
                messages = new object[]
                {
                    new { role = "system", content = instructions },
                    new { role = "user", content = text },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoadApiKey());

        var root = await SendAsync(request, cancellationToken);
        var message = root["choices"]?[0]?["message"];
        if (message is null)
        {
            throw new ProviderException("The provider response was malformed.");
        }

        if (message["content"] is JsonValue value &&
            value.TryGetValue<string>(out var stringContent) &&
            !string.IsNullOrWhiteSpace(stringContent))
        {
            return new ProviderOutput(stringContent.Trim());
        }

        if (message["content"] is JsonArray parts)
        {
            var combined = JoinTextParts(parts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return new ProviderOutput(combined);
            }
        }

        throw new ProviderException("The provider response was malformed.");
    }
}

internal sealed class AnthropicMessagesProvider : ProviderClientBase
{
    public AnthropicMessagesProvider(ProviderPreset preset, PromptProfile promptProfile, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/messages")
        {
            Content = JsonContent(new
            {
                model = Preset.Model,
                max_tokens = 1024,
                system = instructions,
                messages = new object[]
                {
                    new { role = "user", content = text },
                },
            }),
        };
        request.Headers.Add("x-api-key", LoadApiKey());
        request.Headers.Add("anthropic-version", "2023-06-01");

        var root = await SendAsync(request, cancellationToken);
        if (root["content"] is not JsonArray contentArray)
        {
            throw new ProviderException("The provider response was malformed.");
        }

        var combined = string.Join(
            "\n",
            contentArray
                .Where(item => string.Equals(item?["type"]?.GetValue<string>(), "text", StringComparison.Ordinal))
                .Select(item => item?["text"]?.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
        ).Trim();

        if (string.IsNullOrWhiteSpace(combined))
        {
            throw new ProviderException("The provider response was malformed.");
        }

        return new ProviderOutput(combined);
    }
}

internal sealed class GeminiGenerateContentProvider : ProviderClientBase
{
    public GeminiGenerateContentProvider(ProviderPreset preset, PromptProfile promptProfile, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/{Preset.Model}:generateContent")
        {
            Content = JsonContent(new
            {
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = $"{instructions}\n\nUser text:\n{text}" },
                        },
                    },
                },
            }),
        };
        request.Headers.Add("x-goog-api-key", LoadApiKey());

        var root = await SendAsync(request, cancellationToken);
        var parts = root["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        var combined = JoinTextParts(parts);
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return new ProviderOutput(combined);
        }

        throw new ProviderException("The provider response was malformed.");
    }
}
