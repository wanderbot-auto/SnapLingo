namespace SnapLingoWindows.Services;

public sealed record LocalizationCatalog(
    IReadOnlyDictionary<string, string> English,
    IReadOnlyDictionary<string, string> Chinese)
{
    private const string EnglishResourceName = "Resources.Localization.windows.en.json";
    private const string ChineseResourceName = "Resources.Localization.windows.zh-Hans.json";

    private static readonly Lazy<LocalizationCatalog> SharedCatalog = new(Load);

    public static LocalizationCatalog Shared => SharedCatalog.Value;

    public IReadOnlyDictionary<string, string> GetTable(AppLanguage language)
    {
        return language == AppLanguage.Chinese ? Chinese : English;
    }

    private static LocalizationCatalog Load()
    {
        var english = EmbeddedJsonResourceLoader.Load<Dictionary<string, string>>(EnglishResourceName);
        var chinese = EmbeddedJsonResourceLoader.Load<Dictionary<string, string>>(ChineseResourceName);
        return new LocalizationCatalog(english, chinese);
    }
}
