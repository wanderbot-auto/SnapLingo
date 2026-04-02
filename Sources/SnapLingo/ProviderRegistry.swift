import Foundation

enum ProviderKind: String, CaseIterable, Identifiable, Decodable {
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

enum ProviderProtocolStyle: String, Decodable {
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
    private let providerCatalog: ProviderCatalog
    private let credentialStore: CredentialStore
    private let session: URLSession
    private let defaults: UserDefaults
    private let providerDefaultsKey = "selected-provider"

    var selectedProvider: ProviderKind {
        get { ProviderKind(rawValue: defaults.string(forKey: providerDefaultsKey) ?? "") ?? .openAI }
        set { defaults.set(newValue.rawValue, forKey: providerDefaultsKey) }
    }

    init(
        providerCatalog: ProviderCatalog = .shared,
        credentialStore: CredentialStore,
        session: URLSession = .shared,
        defaults: UserDefaults = .standard
    ) {
        self.providerCatalog = providerCatalog
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
        providerCatalog.preset(for: provider)
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
