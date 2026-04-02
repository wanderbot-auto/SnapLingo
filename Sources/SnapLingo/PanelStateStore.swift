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
    @Published var primaryTitle: String = MacStrings.shared.string("panel.ready.title")
    @Published var primaryText: String?
    @Published var originalPreview: String?
    @Published var secondaryStatus: String?
    @Published var settingsMessage: String?
    @Published var canCopy = false
    @Published var isCopied = false
    @Published var canRetry = false

    func resetForNewSession() {
        phase = .capturing
        primaryTitle = MacStrings.shared.string("panel.capturing.title")
        primaryText = nil
        originalPreview = nil
        secondaryStatus = MacStrings.shared.string("panel.capturing.status")
        canCopy = false
        canRetry = false
        isCopied = false
        settingsMessage = nil
    }

    func resetForIdle() {
        phase = .idle
        primaryTitle = MacStrings.shared.string("panel.ready.title")
        primaryText = nil
        originalPreview = nil
        secondaryStatus = nil
        canCopy = false
        canRetry = false
        isCopied = false
    }

    func presentPermissionOnboarding() {
        phase = .permissionRequired
        primaryTitle = MacStrings.shared.string("panel.permission.title")
        primaryText = MacStrings.shared.string("panel.permission.primary")
        secondaryStatus = MacStrings.shared.string("panel.permission.status")
        canCopy = false
        canRetry = false
    }

    func presentClipboardFallback() {
        phase = .waitingForClipboard
        primaryTitle = MacStrings.shared.string("panel.clipboard.title")
        primaryText = MacStrings.shared.string("panel.clipboard.primary")
        secondaryStatus = MacStrings.shared.string("panel.clipboard.status")
        canCopy = false
        canRetry = true
    }

    func beginProcessing(text: String, selectedMode: TranslationMode, modeSourceLabel: String) {
        self.selectedMode = selectedMode
        self.modeSourceLabel = modeSourceLabel
        originalPreview = text.replacingOccurrences(of: "\n", with: " ")
        canRetry = true

        switch selectedMode {
        case .translate:
            phase = .loadingTranslation
            primaryTitle = MacStrings.shared.string("panel.translate.title")
            primaryText = nil
            secondaryStatus = MacStrings.shared.string("panel.translate.status")
            canCopy = false
        case .polish:
            phase = .loadingPolish
            primaryTitle = MacStrings.shared.string("panel.polish.title")
            primaryText = nil
            secondaryStatus = MacStrings.shared.string("panel.polish.status")
            canCopy = false
        }
    }

    func showPartialTranslation(_ text: String) {
        phase = .partial
        primaryTitle = MacStrings.shared.string("panel.translate.title")
        primaryText = text
        secondaryStatus = MacStrings.shared.string("panel.partial.status")
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
        primaryTitle = MacStrings.shared.string("panel.error.title")
        primaryText = message
        secondaryStatus = MacStrings.shared.string("panel.error.status")
        canCopy = false
        canRetry = true
    }

    func showCopiedFeedback() {
        isCopied = true
        secondaryStatus = MacStrings.shared.string("panel.copied.status")
    }

    func setSettingsMessage(_ message: String) {
        settingsMessage = message
    }
}
