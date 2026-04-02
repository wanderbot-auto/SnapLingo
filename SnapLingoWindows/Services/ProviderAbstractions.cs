namespace SnapLingoWindows.Services;

public interface IProviderClient
{
    Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken);
    Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken);
    Task<ProviderOutput> ContinueAsync(string text, CancellationToken cancellationToken);
}

public class ProviderException : Exception
{
    public ProviderException(string message)
        : base(message)
    {
    }
}

public sealed class ProviderHttpException : ProviderException
{
    public ProviderHttpException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
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
