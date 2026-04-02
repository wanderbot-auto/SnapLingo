import XCTest
@testable import SnapLingo

final class SnapLingoLogicTests: XCTestCase {
    func testProviderKindMapsToUniqueCredentialKey() {
        XCTAssertEqual(ProviderKind.openAI.credentialKey, .openAI)
        XCTAssertEqual(ProviderKind.anthropic.credentialKey, .anthropic)
        XCTAssertEqual(ProviderKind.gemini.credentialKey, .gemini)
        XCTAssertEqual(ProviderKind.zhipuGLM.credentialKey, .zhipuGLM)
        XCTAssertEqual(ProviderKind.kimi.credentialKey, .kimi)
        XCTAssertEqual(ProviderKind.minimax.credentialKey, .minimax)
        XCTAssertEqual(ProviderKind.aliyunBailian.credentialKey, .aliyunBailian)
        XCTAssertEqual(ProviderKind.volcengineArk.credentialKey, .volcengineArk)
    }

    func testChineseTextDefaultsToTranslate() {
        XCTAssertEqual(ModeDetector.detect(for: "这个 API endpoint 返回 500"), .translate)
    }

    func testEnglishTextDefaultsToPolish() {
        XCTAssertEqual(ModeDetector.detect(for: "Please review this draft before I send it."), .polish)
    }

    @MainActor
    func testPartialTranslationUnlocksCopy() {
        let store = PanelStateStore()
        store.resetForNewSession()
        store.beginProcessing(text: "你好世界", selectedMode: .translate, modeSourceLabel: "Auto")
        store.showPartialTranslation("Hello world")

        XCTAssertEqual(store.phase, .partial)
        XCTAssertEqual(store.primaryTitle, "Quick Translation")
        XCTAssertEqual(store.primaryText, "Hello world")
        XCTAssertTrue(store.canCopy)
    }

    func testTranslateValidationRejectsIdenticalOutput() throws {
        XCTAssertThrowsError(try ProviderValidation.validate(.init(text: "你好"), input: "你好", mode: .translate))
    }

    func testPolishValidationRejectsEmptyOutput() throws {
        XCTAssertThrowsError(try ProviderValidation.validate(.init(text: "   "), input: "Draft", mode: .polish))
    }

    func testProviderPresetsUseExpectedProtocolStyles() {
        let registry = ProviderRegistry(credentialStore: TestCredentialStore())

        XCTAssertEqual(registry.providerPreset(for: .openAI).style, .openAIResponses)
        XCTAssertEqual(registry.providerPreset(for: .anthropic).style, .anthropicMessages)
        XCTAssertEqual(registry.providerPreset(for: .gemini).style, .geminiGenerateContent)
        XCTAssertEqual(registry.providerPreset(for: .zhipuGLM).style, .openAIChatCompletions)
        XCTAssertEqual(registry.providerPreset(for: .kimi).style, .openAIChatCompletions)
        XCTAssertEqual(registry.providerPreset(for: .minimax).style, .openAIChatCompletions)
        XCTAssertEqual(registry.providerPreset(for: .aliyunBailian).style, .openAIChatCompletions)
        XCTAssertEqual(registry.providerPreset(for: .volcengineArk).style, .openAIChatCompletions)
    }

    func testDomesticProviderPresetsUseCuratedDefaults() {
        let registry = ProviderRegistry(credentialStore: TestCredentialStore())

        XCTAssertEqual(registry.providerPreset(for: .zhipuGLM).baseURL, "https://api.z.ai/api/paas/v4")
        XCTAssertEqual(registry.providerPreset(for: .kimi).model, "kimi-k2-turbo-preview")
        XCTAssertEqual(registry.providerPreset(for: .minimax).baseURL, "https://api.minimaxi.com/v1")
        XCTAssertEqual(registry.providerPreset(for: .aliyunBailian).baseURL, "https://dashscope.aliyuncs.com/compatible-mode/v1")
        XCTAssertEqual(registry.providerPreset(for: .volcengineArk).baseURL, "https://ark.cn-beijing.volces.com/api/v3")
    }

    func testHotkeyPresetsStayCuratedAndStable() {
        XCTAssertEqual(HotkeyManager.ShortcutPreset.defaultPreset, .commandOptionSpace)
        XCTAssertEqual(HotkeyManager.ShortcutPreset.commandOptionSpace.compactLabel, "⌘⌥Space")
        XCTAssertEqual(HotkeyManager.ShortcutPreset.commandOptionK.displayName, "Command + Option + K")
        XCTAssertEqual(HotkeyManager.ShortcutPreset.allCases.count, 5)
    }

    func testSharedProviderManifestKeepsDefaultsInSync() {
        let catalog = ProviderCatalog.shared

        XCTAssertEqual(catalog.preset(for: .openAI).model, "gpt-4.1-mini")
        XCTAssertEqual(catalog.preset(for: .minimax).model, "MiniMax-M2.5-highspeed")
        XCTAssertEqual(catalog.presetModels(for: .minimax), ["MiniMax-M2.5-highspeed", "MiniMax-M2.7"])
    }

    func testMacLocalizedStringsLoadBundledHotkeyLabels() {
        XCTAssertEqual(MacStrings.shared.string("hotkey.commandShiftOptionK.compactLabel"), "⌘⌥⇧K")
        XCTAssertEqual(MacStrings.shared.string("panel.translate.title"), "Quick Translation")
    }
}

private struct TestCredentialStore: CredentialStore {
    func loadSecret(for key: CredentialKey) throws -> String { "" }
    func saveSecret(_ secret: String, for key: CredentialKey) throws {}
    func deleteSecret(for key: CredentialKey) throws {}
}
