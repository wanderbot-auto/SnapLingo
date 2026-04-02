using System.Reflection;
using System.Text.Json;

namespace SnapLingoWindows.Services;

public static class EmbeddedJsonResourceLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T Load<T>(string resourceSuffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Missing embedded resource ending with: {resourceSuffix}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource stream: {resourceName}");

        var result = JsonSerializer.Deserialize<T>(stream, Options);
        return result ?? throw new InvalidOperationException($"Embedded resource {resourceName} did not deserialize to {typeof(T).Name}.");
    }
}
