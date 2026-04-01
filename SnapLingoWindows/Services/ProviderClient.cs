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
    public static ProviderOutput Validate(ProviderOutput output, string input, TranslationMode mode, LocalizationService localizer)
    {
        var normalized = output.Text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ProviderException(localizer.Get("error_provider_empty_result"));
        }

        if (mode == TranslationMode.Translate &&
            string.Equals(normalized, input.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ProviderException(localizer.Get("error_provider_untrusted_result"));
        }

        return new ProviderOutput(normalized);
    }
}

internal abstract class ProviderClientBase : IProviderClient
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(350),
        TimeSpan.FromMilliseconds(900),
    ];

    private readonly ProviderPreset preset;
    private readonly PromptProfile promptProfile;
    private readonly LocalizationService localizer;
    private readonly SecureSecretStore secretStore;
    private readonly HttpClient httpClient;

    protected ProviderClientBase(ProviderPreset preset, PromptProfile promptProfile, LocalizationService localizer, SecureSecretStore secretStore, HttpClient httpClient)
    {
        this.preset = preset;
        this.promptProfile = promptProfile;
        this.localizer = localizer;
        this.secretStore = secretStore;
        this.httpClient = httpClient;
    }

    public abstract Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken);
    public abstract Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken);

    protected ProviderPreset Preset => preset;
    protected PromptProfile PromptProfile => promptProfile;
    protected LocalizationService Localizer => localizer;

    protected string LoadApiKey()
    {
        var key = secretStore.LoadSecret(preset.Kind);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ProviderException(localizer.Get("error_add_api_key"));
        }

        return key.Trim();
    }

    protected async Task<JsonNode> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return JsonNode.Parse(body) ?? throw new ProviderException(localizer.Get("error_provider_malformed"));
            }

            var statusCode = (int)response.StatusCode;
            if (IsTransientStatus(statusCode) && attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken);
                continue;
            }

            if (statusCode == 529)
            {
                throw new ProviderException(localizer.Get("error_provider_busy"));
            }

            throw new ProviderException(localizer.Format("error_provider_http", statusCode));
        }
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

    private static bool IsTransientStatus(int statusCode)
    {
        return statusCode is 429 or 500 or 502 or 503 or 504 or 529;
    }
}

internal sealed class OpenAIResponsesProvider : ProviderClientBase
{
    public OpenAIResponsesProvider(ProviderPreset preset, PromptProfile promptProfile, LocalizationService localizer, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, localizer, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate, Localizer);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish, Localizer);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/responses")
            {
                Content = JsonContent(new
                {
                    model = Preset.Model,
                    instructions,
                    input = text,
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoadApiKey());
            return request;
        }

        var root = await SendAsync(CreateRequest, cancellationToken);

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

        throw new ProviderException(Localizer.Get("error_provider_malformed"));
    }
}

internal sealed class OpenAIChatProvider : ProviderClientBase
{
    public OpenAIChatProvider(ProviderPreset preset, PromptProfile promptProfile, LocalizationService localizer, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, localizer, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate, Localizer);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish, Localizer);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/chat/completions")
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
            return request;
        }

        var root = await SendAsync(CreateRequest, cancellationToken);
        var message = root["choices"]?[0]?["message"];
        if (message is null)
        {
            throw new ProviderException(Localizer.Get("error_provider_malformed"));
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

        throw new ProviderException(Localizer.Get("error_provider_malformed"));
    }
}

internal sealed class AnthropicMessagesProvider : ProviderClientBase
{
    public AnthropicMessagesProvider(ProviderPreset preset, PromptProfile promptProfile, LocalizationService localizer, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, localizer, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate, Localizer);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish, Localizer);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/messages")
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
            return request;
        }

        var root = await SendAsync(CreateRequest, cancellationToken);
        if (root["content"] is not JsonArray contentArray)
        {
            throw new ProviderException(Localizer.Get("error_provider_malformed"));
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
            throw new ProviderException(Localizer.Get("error_provider_malformed"));
        }

        return new ProviderOutput(combined);
    }
}

internal sealed class GeminiGenerateContentProvider : ProviderClientBase
{
    public GeminiGenerateContentProvider(ProviderPreset preset, PromptProfile promptProfile, LocalizationService localizer, SecureSecretStore secretStore, HttpClient httpClient)
        : base(preset, promptProfile, localizer, secretStore, httpClient)
    {
    }

    public override async Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.TranslatePrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Translate, Localizer);
    }

    public override async Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
    {
        var output = await RequestAsync(PromptProfile.PolishPrompt, text, cancellationToken);
        return ProviderValidation.Validate(output, text, TranslationMode.Polish, Localizer);
    }

    private async Task<ProviderOutput> RequestAsync(string instructions, string text, CancellationToken cancellationToken)
    {
        HttpRequestMessage CreateRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Preset.BaseUrl}/{Preset.Model}:generateContent")
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
            return request;
        }

        var root = await SendAsync(CreateRequest, cancellationToken);
        var parts = root["candidates"]?[0]?["content"]?["parts"] as JsonArray;
        var combined = JoinTextParts(parts);
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return new ProviderOutput(combined);
        }

        throw new ProviderException(Localizer.Get("error_provider_malformed"));
    }
}
