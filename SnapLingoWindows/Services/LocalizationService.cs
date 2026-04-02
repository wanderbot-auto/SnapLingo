namespace SnapLingoWindows.Services;

public sealed class LocalizationService
{
    private readonly LocalizationCatalog catalog;

    public LocalizationService(AppLanguage language, LocalizationCatalog? catalog = null)
    {
        this.catalog = catalog ?? LocalizationCatalog.Shared;
        CurrentLanguage = language;
    }

    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; }

    public void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        var table = catalog.GetTable(CurrentLanguage);
        return table.TryGetValue(key, out var value) ? value : key;
    }

    public string Format(string key, params object[] args) => string.Format(Get(key), args);
}
