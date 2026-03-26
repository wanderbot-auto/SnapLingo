import Foundation

@MainActor
final class WorkflowOrchestrator {
    private let store: PanelStateStore
    private let capture: SelectionCapture
    private let providerRegistry: ProviderRegistry
    private let panelController: FloatingPanelController
    private var activeTask: Task<Void, Never>?
    private var currentInput: String?

    init(
        store: PanelStateStore,
        capture: SelectionCapture,
        providerRegistry: ProviderRegistry,
        panelController: FloatingPanelController
    ) {
        self.store = store
        self.capture = capture
        self.providerRegistry = providerRegistry
        self.panelController = panelController
    }

    func handleHotkey() async {
        activeTask?.cancel()
        store.resetForNewSession()
        panelController.show()

        activeTask = Task { @MainActor [weak self] in
            guard let self else { return }
            let captureOutcome = await self.capture.captureSelection()
            guard !Task.isCancelled else { return }

            switch captureOutcome {
            case let .text(text):
                await self.process(text: text, sourceLabel: "Auto")
            case .requiresPermission:
                self.store.presentPermissionOnboarding()
            case let .requiresClipboardFallback(changeCount):
                self.store.presentClipboardFallback()
                if let text = await self.capture.waitForClipboardChange(after: changeCount), !Task.isCancelled {
                    await self.process(text: text, sourceLabel: "Clipboard")
                } else if !Task.isCancelled {
                    self.store.showError("SnapLingo never received copied text. Press the hotkey again to retry.")
                }
            }
        }
    }

    func retry() async {
        await handleHotkey()
    }

    func switchMode(to mode: TranslationMode) async {
        guard let currentInput else { return }
        store.selectedMode = mode
        await runProviderPipeline(text: currentInput, selectedMode: mode, modeSourceLabel: "Manual")
    }

    private func process(text: String, sourceLabel: String) async {
        currentInput = text
        let mode = ModeDetector.detect(for: text)
        store.beginProcessing(text: text, selectedMode: mode, modeSourceLabel: sourceLabel)
        await runProviderPipeline(text: text, selectedMode: mode, modeSourceLabel: sourceLabel)
    }

    private func runProviderPipeline(text: String, selectedMode: TranslationMode, modeSourceLabel: String) async {
        store.beginProcessing(text: text, selectedMode: selectedMode, modeSourceLabel: modeSourceLabel)
        let provider = providerRegistry.currentClient()

        do {
            switch selectedMode {
            case .translate:
                let translation = try await provider.translate(text)
                guard !Task.isCancelled else { return }
                store.showPartialTranslation(translation.text)

                let polished = try await provider.polish(translation.text)
                guard !Task.isCancelled else { return }
                store.showFinalResult(title: "Polished Version", text: polished.text)

            case .polish:
                let polished = try await provider.polish(text)
                guard !Task.isCancelled else { return }
                store.showFinalResult(title: "Polished Version", text: polished.text)
            }
        } catch {
            if Task.isCancelled { return }
            let message = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            store.showError(message)
        }
    }
}
