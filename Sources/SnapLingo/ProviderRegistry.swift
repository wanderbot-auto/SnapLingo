import Foundation

enum ProviderKind: String, CaseIterable, Identifiable {
    case openAI
    case anthropic
    case gemini
    case zhipuGLM
    case kimi
    case minimax
    case aliyunBailian
    case volcengineArk

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .openAI: return "OpenAI"
        case .anthropic: return "Anthropic"
        case .gemini: return "Google Gemini"
        case .zhipuGLM: return "Zhipu GLM"
        case .kimi: return "Kimi"
        case .minimax: return "MiniMax"
        case .aliyunBailian: return "Alibaba Bailian"
        case .volcengineArk: return "Volcengine Ark"
        }
    }

    var credentialKey: CredentialKey {
        switch self {
        case .openAI: return .openAI
        case .anthropic: return .anthropic
        case .gemini: return .gemini
        case .zhipuGLM: return .zhipuGLM
        case .kimi: return .kimi
        case .minimax: return .minimax
        case .aliyunBailian: return .aliyunBailian
        case .volcengineArk: return .volcengineArk
        }
    }

    var apiKeyPlaceholder: String {
        switch self {
        case .openAI: return "OpenAI API key"
        case .anthropic: return "Anthropic API key"
        case .gemini: return "Gemini API key"
        case .zhipuGLM: return "Zhipu GLM API key"
        case .kimi: return "Kimi API key"
        case .minimax: return "MiniMax API key"
        case .aliyunBailian: return "DashScope API key"
        case .volcengineArk: return "ARK API key"
        }
    }
}

enum ProviderProtocolStyle {
    case openAIResponses
    case openAIChatCompletions
    case anthropicMessages
    case geminiGenerateContent
}

struct ProviderPreset {
    let kind: ProviderKind
    let style: ProviderProtocolStyle
    let baseURL: String
    let model: String
}

final class ProviderRegistry {
    private let credentialStore: CredentialStore
    private let session: URLSession
    private let defaults: UserDefaults
    private let providerDefaultsKey = "selected-provider"

    var selectedProvider: ProviderKind {
        get { ProviderKind(rawValue: defaults.string(forKey: providerDefaultsKey) ?? "") ?? .openAI }
        set { defaults.set(newValue.rawValue, forKey: providerDefaultsKey) }
    }

    init(
        credentialStore: CredentialStore,
        session: URLSession = .shared,
        defaults: UserDefaults = .standard
    ) {
        self.credentialStore = credentialStore
        self.session = session
        self.defaults = defaults
    }

    @MainActor
    func currentClient() -> any ProviderClient {
        makeClient(for: selectedProvider)
    }

    @MainActor
    func makeClient(for provider: ProviderKind) -> any ProviderClient {
        let preset = providerPreset(for: provider)

        switch preset.style {
        case .openAIResponses:
            return OpenAICompatibleResponsesProvider(
                preset: preset,
                credentialStore: credentialStore,
                session: session
            )
        case .openAIChatCompletions:
            return OpenAICompatibleChatProvider(
                preset: preset,
                credentialStore: credentialStore,
                session: session
            )
        case .anthropicMessages:
            return AnthropicMessagesProvider(
                preset: preset,
                credentialStore: credentialStore,
                session: session
            )
        case .geminiGenerateContent:
            return GeminiGenerateContentProvider(
                preset: preset,
                credentialStore: credentialStore,
                session: session
            )
        }
    }

    func providerPreset(for provider: ProviderKind) -> ProviderPreset {
        switch provider {
        case .openAI:
            ProviderPreset(kind: .openAI, style: .openAIResponses, baseURL: "https://api.openai.com/v1", model: "gpt-4.1-mini")
        case .anthropic:
            ProviderPreset(kind: .anthropic, style: .anthropicMessages, baseURL: "https://api.anthropic.com/v1", model: "claude-sonnet-4-20250514")
        case .gemini:
            ProviderPreset(kind: .gemini, style: .geminiGenerateContent, baseURL: "https://generativelanguage.googleapis.com/v1beta/models", model: "gemini-2.5-flash")
        case .zhipuGLM:
            ProviderPreset(kind: .zhipuGLM, style: .openAIChatCompletions, baseURL: "https://api.z.ai/api/paas/v4", model: "glm-4.5-air")
        case .kimi:
            ProviderPreset(kind: .kimi, style: .openAIChatCompletions, baseURL: "https://api.moonshot.cn/v1", model: "kimi-k2-turbo-preview")
        case .minimax:
            ProviderPreset(kind: .minimax, style: .openAIChatCompletions, baseURL: "https://api.minimaxi.com/v1", model: "MiniMax-M2.5-highspeed")
        case .aliyunBailian:
            ProviderPreset(kind: .aliyunBailian, style: .openAIChatCompletions, baseURL: "https://dashscope.aliyuncs.com/compatible-mode/v1", model: "qwen3.5-flash")
        case .volcengineArk:
            ProviderPreset(kind: .volcengineArk, style: .openAIChatCompletions, baseURL: "https://ark.cn-beijing.volces.com/api/v3", model: "doubao-seed-1-6-250615")
        }
    }

    func loadKey(for provider: ProviderKind) -> String {
        (try? credentialStore.loadSecret(for: provider.credentialKey)) ?? ""
    }

    func saveKey(_ secret: String, for provider: ProviderKind) throws {
        try credentialStore.saveSecret(secret, for: provider.credentialKey)
    }

    func deleteKey(for provider: ProviderKind) throws {
        try credentialStore.deleteSecret(for: provider.credentialKey)
    }
}
