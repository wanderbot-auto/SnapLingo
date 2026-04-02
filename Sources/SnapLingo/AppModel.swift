import AppKit
import Foundation
import SwiftUI

@MainActor
final class AppModel: ObservableObject {
    let store = PanelStateStore()
    @Published private(set) var selectedShortcutPreset: HotkeyManager.ShortcutPreset
    @Published private(set) var hotkeyStatusMessage: String?

    private let credentials = KeychainCredentialStore()
    private let providerRegistry: ProviderRegistry
    private let defaults: UserDefaults
    private let shortcutDefaultsKey = "selected-hotkey-preset"
    private lazy var capture = AccessibilitySelectionCapture()
    private lazy var panelController = FloatingPanelController(rootView: ResultPanelView(model: self))
    private lazy var orchestrator = WorkflowOrchestrator(
        store: store,
        capture: capture,
        providerRegistry: providerRegistry,
        panelController: panelController
    )

    private var hotkeyManager: HotkeyManager?

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        providerRegistry = ProviderRegistry(credentialStore: credentials)
        selectedShortcutPreset = HotkeyManager.ShortcutPreset(
            rawValue: defaults.string(forKey: shortcutDefaultsKey) ?? ""
        ) ?? .defaultPreset
        registerHotkey(preset: selectedShortcutPreset)
    }

    var hotkeyDisplayText: String {
        selectedShortcutPreset.compactLabel
    }

    var activeProviderName: String {
        providerRegistry.selectedProvider.displayName
    }

    var activeModelName: String {
        providerRegistry.providerPreset(for: providerRegistry.selectedProvider).model
    }

    func updateShortcutPreset(_ preset: HotkeyManager.ShortcutPreset) {
        selectedShortcutPreset = preset
        defaults.set(preset.rawValue, forKey: shortcutDefaultsKey)
        registerHotkey(preset: preset)
    }

    private func registerHotkey(preset: HotkeyManager.ShortcutPreset) {
        hotkeyManager = try? HotkeyManager(shortcut: preset.shortcut) { [weak self] in
            Task { @MainActor in
                await self?.handleHotkey()
            }
        }

        if hotkeyManager == nil {
            hotkeyStatusMessage = MacStrings.shared.format("app.hotkey.register.failed", preset.displayName)
        } else {
            hotkeyStatusMessage = MacStrings.shared.format("app.hotkey.register.success", preset.displayName)
        }
    }

    func handleHotkey() async {
        panelController.show()
        await orchestrator.handleHotkey()
    }

    func copyPrimaryResult() {
        guard let text = store.primaryText, !text.isEmpty else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
        store.showCopiedFeedback()
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .milliseconds(350))
            self?.panelController.hide()
            self?.store.resetForIdle()
        }
    }

    func retryCurrentFlow() {
        Task { @MainActor in
            await orchestrator.retry()
        }
    }

    func selectMode(_ mode: TranslationMode) {
        Task { @MainActor in
            await orchestrator.switchMode(to: mode)
        }
    }

    func openAccessibilitySettings() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") else { return }
        NSWorkspace.shared.open(url)
    }

    func dismissPanel() {
        panelController.hide()
        store.resetForIdle()
    }
}

enum TranslationMode: String, CaseIterable, Identifiable {
    case translate = "Translate"
    case polish = "Polish"

    var id: String { rawValue }
}

enum ModeDetector {
    static func detect(for text: String) -> TranslationMode {
        if containsCJK(text) { return .translate }
        return .polish
    }

    private static func containsCJK(_ text: String) -> Bool {
        text.unicodeScalars.contains { scalar in
            switch scalar.value {
            case 0x3400 ... 0x9FFF, 0xF900 ... 0xFAFF:
                return true
            default:
                return false
            }
        }
    }
}
