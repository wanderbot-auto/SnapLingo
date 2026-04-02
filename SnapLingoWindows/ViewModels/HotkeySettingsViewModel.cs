using Microsoft.UI.Xaml;
using SnapLingoWindows.Infrastructure;

namespace SnapLingoWindows.ViewModels;

public sealed class HotkeySettingsViewModel : BindableBase
{
    private readonly AppSettingsDocument settingsDocument;
    private readonly Action saveSettings;
    private readonly IReadOnlyList<ShortcutChoice> shortcutChoices;
    private ShortcutChoice? selectedShortcutChoice;
    private string? hotkeyStatusMessage;

    public HotkeySettingsViewModel(
        AppSettingsDocument settingsDocument,
        Action saveSettings,
        ShortcutPreset initialPreset)
    {
        this.settingsDocument = settingsDocument;
        this.saveSettings = saveSettings;
        shortcutChoices = Enum.GetValues<ShortcutPreset>()
            .Select(preset => new ShortcutChoice(preset, preset.DisplayName()))
            .ToList();
        selectedShortcutChoice = shortcutChoices.FirstOrDefault(choice => choice.Preset == initialPreset)
            ?? shortcutChoices.First();
        settingsDocument.SelectedShortcutPreset = selectedShortcutChoice.Preset.ToString();
    }

    public IReadOnlyList<ShortcutChoice> ShortcutChoices => shortcutChoices;

    public ShortcutChoice? SelectedShortcutChoice
    {
        get => selectedShortcutChoice;
        set
        {
            if (!SetProperty(ref selectedShortcutChoice, value) || value is null)
            {
                return;
            }

            settingsDocument.SelectedShortcutPreset = value.Preset.ToString();
            OnPropertyChanged(nameof(SelectedShortcutPreset));
            OnPropertyChanged(nameof(SelectedShortcutDisplayName));
            OnPropertyChanged(nameof(SelectedShortcutCompactLabel));
            saveSettings();
        }
    }

    public ShortcutPreset SelectedShortcutPreset => selectedShortcutChoice?.Preset ?? ShortcutPresetExtensions.DefaultPreset;

    public string SelectedShortcutDisplayName => SelectedShortcutPreset.DisplayName();

    public string SelectedShortcutCompactLabel => SelectedShortcutPreset.CompactLabel();

    public string? HotkeyStatusMessage
    {
        get => hotkeyStatusMessage;
        private set
        {
            if (SetProperty(ref hotkeyStatusMessage, value))
            {
                OnPropertyChanged(nameof(HotkeyStatusVisibility));
            }
        }
    }

    public Visibility HotkeyStatusVisibility =>
        string.IsNullOrWhiteSpace(HotkeyStatusMessage) ? Visibility.Collapsed : Visibility.Visible;

    public void SetHotkeyStatusMessage(string? message)
    {
        HotkeyStatusMessage = message;
    }
}

public sealed record ShortcutChoice(ShortcutPreset Preset, string Label);
