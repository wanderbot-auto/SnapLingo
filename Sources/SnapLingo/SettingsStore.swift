import Foundation

@MainActor
final class SettingsStore: ObservableObject {
    @Published var selectedProvider: ProviderKind
    @Published var apiKeyInput: String
    @Published var statusMessage: String?

    private let providerRegistry: ProviderRegistry

    init(providerRegistry: ProviderRegistry = ProviderRegistry(credentialStore: KeychainCredentialStore())) {
        self.providerRegistry = providerRegistry
        self.selectedProvider = providerRegistry.selectedProvider
        self.apiKeyInput = providerRegistry.loadKey(for: providerRegistry.selectedProvider)
    }

    func selectProvider(_ provider: ProviderKind) {
        selectedProvider = provider
        providerRegistry.selectedProvider = provider
        apiKeyInput = providerRegistry.loadKey(for: provider)
        statusMessage = "Switched to \(provider.displayName)."
    }

    func saveAPIKey() {
        let trimmed = apiKeyInput.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty {
            clearAPIKey()
            return
        }

        do {
            try providerRegistry.saveKey(trimmed, for: selectedProvider)
            statusMessage = "Saved \(selectedProvider.displayName) API key to Keychain."
        } catch {
            statusMessage = "Could not save API key: \(error.localizedDescription)"
        }
    }

    func clearAPIKey() {
        apiKeyInput = ""

        do {
            try providerRegistry.deleteKey(for: selectedProvider)
            statusMessage = "Removed \(selectedProvider.displayName) API key from Keychain."
        } catch {
            statusMessage = "Could not remove API key: \(error.localizedDescription)"
        }
    }
}
