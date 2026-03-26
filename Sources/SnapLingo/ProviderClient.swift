import Foundation

struct ProviderOutput: Equatable {
    let text: String
}

struct ProviderPrompt {
    let systemInstruction: String
    let userText: String
}

@MainActor
protocol ProviderClient {
    func translate(_ text: String) async throws -> ProviderOutput
    func polish(_ text: String) async throws -> ProviderOutput
}

enum ProviderValidation {
    static func validate(_ output: ProviderOutput, input: String, mode: TranslationMode) throws -> ProviderOutput {
        let normalized = output.text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalized.isEmpty else { throw ProviderError.emptyOutput }

        if mode == .translate, normalized.caseInsensitiveCompare(input.trimmingCharacters(in: .whitespacesAndNewlines)) == .orderedSame {
            throw ProviderError.unusableOutput
        }

        return ProviderOutput(text: normalized)
    }
}

enum ProviderError: LocalizedError {
    case missingAPIKey
    case emptyOutput
    case unusableOutput
    case invalidResponse
    case httpError(Int)
    case unsupportedProvider(String)

    var errorDescription: String? {
        switch self {
        case .missingAPIKey:
            return "Add an API key in Settings to use translation."
        case .emptyOutput:
            return "The provider returned an empty result."
        case .unusableOutput:
            return "The provider returned a result SnapLingo cannot trust."
        case .invalidResponse:
            return "The provider response was malformed."
        case let .httpError(code):
            return "The provider request failed with HTTP \(code)."
        case let .unsupportedProvider(name):
            return "\(name) is not configured correctly."
        }
    }
}

private func loadAPIKey(from credentialStore: CredentialStore, key: CredentialKey) throws -> String {
    do {
        return try credentialStore.loadSecret(for: key)
    } catch CredentialStoreError.notFound {
        throw ProviderError.missingAPIKey
    }
}

private func joinTextParts(_ parts: [[String: Any]]) -> String {
    parts.compactMap { $0["text"] as? String }
        .joined(separator: "\n")
        .trimmingCharacters(in: .whitespacesAndNewlines)
}

@MainActor
struct OpenAICompatibleResponsesProvider: ProviderClient {
    private let preset: ProviderPreset
    private let credentialStore: CredentialStore
    private let session: URLSession

    init(preset: ProviderPreset, credentialStore: CredentialStore, session: URLSession = .shared) {
        self.preset = preset
        self.credentialStore = credentialStore
        self.session = session
    }

    func translate(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Translate the user's text into natural, professional English. Return only the final translated text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .translate)
    }

    func polish(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Rewrite the user's text into natural, professional English that is ready to send. Keep the meaning intact. Return only the final polished text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .polish)
    }

    private func request(prompt: ProviderPrompt) async throws -> ProviderOutput {
        let apiKey = try loadAPIKey(from: credentialStore, key: preset.kind.credentialKey)
        let url = URL(string: "\(preset.baseURL)/responses")!

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.timeoutInterval = 20

        let body: [String: Any] = [
            "model": preset.model,
            "instructions": prompt.systemInstruction,
            "input": prompt.userText,
        ]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ProviderError.invalidResponse
        }
        guard 200 ..< 300 ~= httpResponse.statusCode else {
            throw ProviderError.httpError(httpResponse.statusCode)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw ProviderError.invalidResponse
        }

        if let outputText = json["output_text"] as? String, !outputText.isEmpty {
            return ProviderOutput(text: outputText)
        }

        if let output = json["output"] as? [[String: Any]] {
            let chunks = output.flatMap { item -> [String] in
                guard let content = item["content"] as? [[String: Any]] else { return [] }
                return content.compactMap { contentItem in
                    if let text = contentItem["text"] as? String { return text }
                    return nil
                }
            }

            let combined = chunks.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
            if !combined.isEmpty {
                return ProviderOutput(text: combined)
            }
        }

        throw ProviderError.invalidResponse
    }
}

@MainActor
struct OpenAICompatibleChatProvider: ProviderClient {
    private let preset: ProviderPreset
    private let credentialStore: CredentialStore
    private let session: URLSession

    init(preset: ProviderPreset, credentialStore: CredentialStore, session: URLSession = .shared) {
        self.preset = preset
        self.credentialStore = credentialStore
        self.session = session
    }

    func translate(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Translate the user's text into natural, professional English. Return only the final translated text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .translate)
    }

    func polish(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Rewrite the user's text into natural, professional English that is ready to send. Keep the meaning intact. Return only the final polished text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .polish)
    }

    private func request(prompt: ProviderPrompt) async throws -> ProviderOutput {
        let apiKey = try loadAPIKey(from: credentialStore, key: preset.kind.credentialKey)
        var request = URLRequest(url: URL(string: "\(preset.baseURL)/chat/completions")!)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.timeoutInterval = 20

        let body: [String: Any] = [
            "model": preset.model,
            "messages": [
                [
                    "role": "system",
                    "content": prompt.systemInstruction,
                ],
                [
                    "role": "user",
                    "content": prompt.userText,
                ],
            ],
        ]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ProviderError.invalidResponse
        }
        guard 200 ..< 300 ~= httpResponse.statusCode else {
            throw ProviderError.httpError(httpResponse.statusCode)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let choices = json["choices"] as? [[String: Any]],
              let first = choices.first,
              let message = first["message"] as? [String: Any] else {
            throw ProviderError.invalidResponse
        }

        let combined: String
        if let text = message["content"] as? String {
            combined = text.trimmingCharacters(in: .whitespacesAndNewlines)
        } else if let parts = message["content"] as? [[String: Any]] {
            combined = joinTextParts(parts)
        } else {
            throw ProviderError.invalidResponse
        }

        guard !combined.isEmpty else { throw ProviderError.invalidResponse }
        return ProviderOutput(text: combined)
    }
}

@MainActor
struct AnthropicMessagesProvider: ProviderClient {
    private let preset: ProviderPreset
    private let credentialStore: CredentialStore
    private let session: URLSession

    init(preset: ProviderPreset, credentialStore: CredentialStore, session: URLSession = .shared) {
        self.preset = preset
        self.credentialStore = credentialStore
        self.session = session
    }

    func translate(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Translate the user's text into natural, professional English. Return only the final translated text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .translate)
    }

    func polish(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Rewrite the user's text into natural, professional English that is ready to send. Keep the meaning intact. Return only the final polished text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .polish)
    }

    private func request(prompt: ProviderPrompt) async throws -> ProviderOutput {
        let apiKey = try loadAPIKey(from: credentialStore, key: preset.kind.credentialKey)
        var request = URLRequest(url: URL(string: "\(preset.baseURL)/messages")!)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("2023-06-01", forHTTPHeaderField: "anthropic-version")
        request.timeoutInterval = 20

        let body: [String: Any] = [
            "model": preset.model,
            "max_tokens": 1024,
            "system": prompt.systemInstruction,
            "messages": [
                [
                    "role": "user",
                    "content": prompt.userText,
                ],
            ],
        ]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ProviderError.invalidResponse
        }
        guard 200 ..< 300 ~= httpResponse.statusCode else {
            throw ProviderError.httpError(httpResponse.statusCode)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let content = json["content"] as? [[String: Any]] else {
            throw ProviderError.invalidResponse
        }

        let combined = content
            .compactMap { item -> String? in
                guard (item["type"] as? String) == "text" else { return nil }
                return item["text"] as? String
            }
            .joined(separator: "\n")
            .trimmingCharacters(in: .whitespacesAndNewlines)

        guard !combined.isEmpty else { throw ProviderError.invalidResponse }
        return ProviderOutput(text: combined)
    }
}

@MainActor
struct GeminiGenerateContentProvider: ProviderClient {
    private let preset: ProviderPreset
    private let credentialStore: CredentialStore
    private let session: URLSession

    init(preset: ProviderPreset, credentialStore: CredentialStore, session: URLSession = .shared) {
        self.preset = preset
        self.credentialStore = credentialStore
        self.session = session
    }

    func translate(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Translate the user's text into natural, professional English. Return only the final translated text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .translate)
    }

    func polish(_ text: String) async throws -> ProviderOutput {
        let output = try await request(prompt: ProviderPrompt(
            systemInstruction: "Rewrite the user's text into natural, professional English that is ready to send. Keep the meaning intact. Return only the final polished text.",
            userText: text
        ))
        return try ProviderValidation.validate(output, input: text, mode: .polish)
    }

    private func request(prompt: ProviderPrompt) async throws -> ProviderOutput {
        let apiKey = try loadAPIKey(from: credentialStore, key: preset.kind.credentialKey)
        var request = URLRequest(url: URL(string: "\(preset.baseURL)/\(preset.model):generateContent")!)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(apiKey, forHTTPHeaderField: "x-goog-api-key")
        request.timeoutInterval = 20

        let body: [String: Any] = [
            "contents": [
                [
                    "role": "user",
                    "parts": [
                        [
                            "text": "\(prompt.systemInstruction)\n\nUser text:\n\(prompt.userText)",
                        ],
                    ],
                ],
            ],
        ]
        request.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await session.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ProviderError.invalidResponse
        }
        guard 200 ..< 300 ~= httpResponse.statusCode else {
            throw ProviderError.httpError(httpResponse.statusCode)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let candidates = json["candidates"] as? [[String: Any]],
              let first = candidates.first,
              let content = first["content"] as? [String: Any],
              let parts = content["parts"] as? [[String: Any]] else {
            throw ProviderError.invalidResponse
        }

        let combined = joinTextParts(parts)
        guard !combined.isEmpty else { throw ProviderError.invalidResponse }
        return ProviderOutput(text: combined)
    }
}
