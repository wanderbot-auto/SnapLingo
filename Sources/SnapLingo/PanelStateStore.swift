import Foundation

@MainActor
final class PanelStateStore: ObservableObject {
    enum Phase: Equatable {
        case idle
        case capturing
        case waitingForClipboard
        case loadingTranslation
        case partial
        case loadingPolish
        case ready
        case permissionRequired
        case error(message: String)
    }

    @Published var phase: Phase = .idle
    @Published var selectedMode: TranslationMode = .translate
    @Published var modeSourceLabel: String = "Auto"
    @Published var primaryTitle: String = "Ready"
    @Published var primaryText: String?
    @Published var originalPreview: String?
    @Published var secondaryStatus: String?
    @Published var settingsMessage: String?
    @Published var canCopy = false
    @Published var isCopied = false
    @Published var canRetry = false

    func resetForNewSession() {
        phase = .capturing
        primaryTitle = "Capturing Selection"
        primaryText = nil
        originalPreview = nil
        secondaryStatus = "Reading the selected text…"
        canCopy = false
        canRetry = false
        isCopied = false
        settingsMessage = nil
    }

    func resetForIdle() {
        phase = .idle
        primaryTitle = "Ready"
        primaryText = nil
        originalPreview = nil
        secondaryStatus = nil
        canCopy = false
        canRetry = false
        isCopied = false
    }

    func presentPermissionOnboarding() {
        phase = .permissionRequired
        primaryTitle = "Allow Accessibility Access"
        primaryText = "SnapLingo needs Accessibility permission to read selected text in other apps."
        secondaryStatus = "Once you enable it, return here and press the hotkey again."
        canCopy = false
        canRetry = false
    }

    func presentClipboardFallback() {
        phase = .waitingForClipboard
        primaryTitle = "Press Copy To Continue"
        primaryText = "This app does not expose selected text directly. Press Copy and SnapLingo will continue automatically."
        secondaryStatus = "Clipboard content stays in memory only for this session."
        canCopy = false
        canRetry = true
    }

    func beginProcessing(text: String, selectedMode: TranslationMode, modeSourceLabel: String) {
        self.selectedMode = selectedMode
        self.modeSourceLabel = modeSourceLabel
        self.originalPreview = text.replacingOccurrences(of: "\n", with: " ")
        self.canRetry = true

        switch selectedMode {
        case .translate:
            phase = .loadingTranslation
            primaryTitle = "Quick Translation"
            primaryText = nil
            secondaryStatus = "Generating a send-ready translation…"
            canCopy = false
        case .polish:
            phase = .loadingPolish
            primaryTitle = "Polished Version"
            primaryText = nil
            secondaryStatus = "Polishing your English…"
            canCopy = false
        }
    }

    func showPartialTranslation(_ text: String) {
        phase = .partial
        primaryTitle = "Quick Translation"
        primaryText = text
        secondaryStatus = "Optimizing…"
        canCopy = true
    }

    func showFinalResult(title: String, text: String) {
        phase = .ready
        primaryTitle = title
        primaryText = text
        secondaryStatus = nil
        canCopy = true
    }

    func showError(_ message: String) {
        phase = .error(message: message)
        primaryTitle = "Could Not Finish"
        primaryText = message
        secondaryStatus = "Try again or switch modes."
        canCopy = false
        canRetry = true
    }

    func showCopiedFeedback() {
        isCopied = true
        secondaryStatus = "Copied"
    }

    func setSettingsMessage(_ message: String) {
        settingsMessage = message
    }
}
