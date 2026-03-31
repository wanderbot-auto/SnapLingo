using System.Text.Json;

namespace SnapLingoWindows.Services;

public sealed class JsonSettingsStore
{
    private readonly string settingsPath = Path.Combine(AppPaths.RootDirectory, "settings.json");

    public AppSettingsDocument Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettingsDocument();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettingsDocument>(json) ?? new AppSettingsDocument();
        }
        catch
        {
            return new AppSettingsDocument();
        }
    }

    public void Save(AppSettingsDocument document)
    {
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(settingsPath, json);
    }
}

public sealed class AppSettingsDocument
{
    public string SelectedProvider { get; set; } = ProviderKind.OpenAI.ToString();
    public string SelectedShortcutPreset { get; set; } = ShortcutPreset.ControlAltSpace.ToString();
    public Dictionary<string, string> SelectedModels { get; set; } = new(StringComparer.Ordinal);
}
