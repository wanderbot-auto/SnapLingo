import Foundation

private struct ProviderManifestDocument: Decodable {
    let providers: [ProviderManifestEntry]
}

private struct ProviderManifestEntry: Decodable {
    let id: ProviderKind
    let style: ProviderProtocolStyle
    let baseURL: String
    let defaultModel: String
    let presetModels: [String]
}

private struct StringTable: Decodable {
    let values: [String: String]

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        values = try container.decode([String: String].self)
    }

    subscript(key: String) -> String? {
        values[key]
    }
}

private enum ResourceCatalogLoader {
    static func decodeJSON<T: Decodable>(subdirectory: String, resourceName: String, as type: T.Type) -> T {
        guard let url = Bundle.module.url(forResource: resourceName, withExtension: "json", subdirectory: subdirectory) else {
            fatalError("Missing bundled resource: \(subdirectory)/\(resourceName).json")
        }

        do {
            let data = try Data(contentsOf: url)
            return try JSONDecoder().decode(type, from: data)
        } catch {
            fatalError("Failed to load bundled resource \(subdirectory)/\(resourceName).json: \(error)")
        }
    }
}

enum MacStrings {
    static let shared = MacStrings()

    private let table = ResourceCatalogLoader.decodeJSON(
        subdirectory: "Localization/macos",
        resourceName: "en",
        as: StringTable.self
    )

    func string(_ key: String) -> String {
        table[key] ?? key
    }

    func format(_ key: String, _ arguments: CVarArg...) -> String {
        String(format: string(key), locale: Locale(identifier: "en_US_POSIX"), arguments: arguments)
    }
}

final class ProviderCatalog {
    static let shared = ProviderCatalog()

    private let entries: [ProviderKind: ProviderManifestEntry]

    private init() {
        let document = ResourceCatalogLoader.decodeJSON(
            subdirectory: "Providers",
            resourceName: "providers",
            as: ProviderManifestDocument.self
        )
        entries = Dictionary(uniqueKeysWithValues: document.providers.map { ($0.id, $0) })
    }

    func preset(for provider: ProviderKind) -> ProviderPreset {
        guard let entry = entries[provider] else {
            fatalError("Missing provider manifest entry for \(provider.rawValue)")
        }

        return ProviderPreset(
            kind: provider,
            style: entry.style,
            baseURL: entry.baseURL,
            model: entry.defaultModel
        )
    }

    func presetModels(for provider: ProviderKind) -> [String] {
        guard let entry = entries[provider] else {
            fatalError("Missing provider manifest entry for \(provider.rawValue)")
        }

        return entry.presetModels
    }
}
