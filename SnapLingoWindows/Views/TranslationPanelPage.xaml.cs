using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SnapLingoWindows.Views;

public sealed partial class TranslationPanelPage : Page
{
    private bool suppressModeEvents;

    public MainViewModel ViewModel { get; }

    public TranslationPanelPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.Workflow.PropertyChanged += OnWorkflowChanged;
        Unloaded += OnUnloaded;
        Render();
    }

    private void OnWorkflowChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Workflow.PropertyChanged -= OnWorkflowChanged;
        Unloaded -= OnUnloaded;
    }

    private void Render()
    {
        suppressModeEvents = true;

        try
        {
            PrimaryTitleTextBlock.Text = ViewModel.Workflow.PrimaryTitle;
            ModeSourceTextBlock.Text = ViewModel.Workflow.ModeSourceLabel;
            PrimaryTextTextBox.Text = ViewModel.Workflow.PrimaryText ?? string.Empty;
            SecondaryStatusTextBlock.Text = ViewModel.Workflow.SecondaryStatus ?? string.Empty;
            SecondaryStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.Workflow.SecondaryStatus)
                ? Visibility.Collapsed
                : Visibility.Visible;

            BusyProgressRing.IsActive = ViewModel.Workflow.IsBusy;
            BusyProgressRing.Visibility = ViewModel.Workflow.IsBusy ? Visibility.Visible : Visibility.Collapsed;

            ClipboardPromptBorder.Visibility = ViewModel.Workflow.Phase == WorkflowPhase.WaitingForClipboard
                ? Visibility.Visible
                : Visibility.Collapsed;

            OriginalPreviewBorder.Visibility = string.IsNullOrWhiteSpace(ViewModel.Workflow.OriginalPreview)
                ? Visibility.Collapsed
                : Visibility.Visible;
            OriginalPreviewTextBox.Text = ViewModel.Workflow.OriginalPreview ?? string.Empty;

            CopyButton.IsEnabled = ViewModel.Workflow.CanCopy;
            CopyButton.Content = ViewModel.Workflow.IsCopied ? "Copied" : "Copy";
            RetryButton.IsEnabled = ViewModel.Workflow.CanRetry;

            TranslateModeRadioButton.IsChecked = ViewModel.Workflow.SelectedMode == TranslationMode.Translate;
            PolishModeRadioButton.IsChecked = ViewModel.Workflow.SelectedMode == TranslationMode.Polish;
        }
        finally
        {
            suppressModeEvents = false;
        }
    }

    private async void OnTranslateModeChecked(object sender, RoutedEventArgs e)
    {
        if (!suppressModeEvents)
        {
            await ViewModel.SelectModeAsync(TranslationMode.Translate);
        }
    }

    private async void OnPolishModeChecked(object sender, RoutedEventArgs e)
    {
        if (!suppressModeEvents)
        {
            await ViewModel.SelectModeAsync(TranslationMode.Polish);
        }
    }

    private async void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.CopyPrimaryResultAsync();
    }

    private async void OnRetryClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryCurrentFlowAsync();
    }
}
