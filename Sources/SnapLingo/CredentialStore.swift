import Foundation
import Security

enum CredentialKey: String {
    case openAI = "openai-api-key"
    case anthropic = "anthropic-api-key"
    case gemini = "gemini-api-key"
    case zhipuGLM = "zhipu-glm-api-key"
    case kimi = "kimi-api-key"
    case minimax = "minimax-api-key"
    case aliyunBailian = "aliyun-bailian-api-key"
    case volcengineArk = "volcengine-ark-api-key"
}

protocol CredentialStore {
    func loadSecret(for key: CredentialKey) throws -> String
    func saveSecret(_ secret: String, for key: CredentialKey) throws
    func deleteSecret(for key: CredentialKey) throws
}

struct KeychainCredentialStore: CredentialStore {
    private let service = "com.wanderbot.snaplingo"

    func loadSecret(for key: CredentialKey) throws -> String {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key.rawValue,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]

        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        guard status != errSecItemNotFound else { throw CredentialStoreError.notFound }
        guard status == errSecSuccess, let data = item as? Data, let value = String(data: data, encoding: .utf8) else {
            throw CredentialStoreError.unexpectedStatus(status)
        }

        return value
    }

    func saveSecret(_ secret: String, for key: CredentialKey) throws {
        let data = Data(secret.utf8)
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key.rawValue,
        ]

        let attributes: [String: Any] = [
            kSecValueData as String: data,
        ]

        let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if updateStatus == errSecSuccess { return }
        if updateStatus != errSecItemNotFound {
            throw CredentialStoreError.unexpectedStatus(updateStatus)
        }

        var addAttributes = query
        addAttributes[kSecValueData as String] = data
        let addStatus = SecItemAdd(addAttributes as CFDictionary, nil)
        guard addStatus == errSecSuccess else {
            throw CredentialStoreError.unexpectedStatus(addStatus)
        }
    }

    func deleteSecret(for key: CredentialKey) throws {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key.rawValue,
        ]

        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw CredentialStoreError.unexpectedStatus(status)
        }
    }
}

enum CredentialStoreError: LocalizedError {
    case notFound
    case unexpectedStatus(OSStatus)

    var errorDescription: String? {
        switch self {
        case .notFound:
            return "No API key is saved yet."
        case let .unexpectedStatus(status):
            return "Keychain returned status \(status)."
        }
    }
}
