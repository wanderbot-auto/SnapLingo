import Carbon
import Foundation

final class HotkeyManager {
    struct Shortcut {
        let keyCode: UInt32
        let modifiers: UInt32
    }

    enum ShortcutPreset: String, CaseIterable, Identifiable {
        case commandOptionSpace
        case commandShiftSpace
        case commandShiftOptionSpace
        case commandOptionK
        case commandShiftOptionK

        var id: String { rawValue }

        var displayName: String {
            switch self {
            case .commandOptionSpace:
                return "Command + Option + Space"
            case .commandShiftSpace:
                return "Command + Shift + Space"
            case .commandShiftOptionSpace:
                return "Command + Shift + Option + Space"
            case .commandOptionK:
                return "Command + Option + K"
            case .commandShiftOptionK:
                return "Command + Shift + Option + K"
            }
        }

        var compactLabel: String {
            switch self {
            case .commandOptionSpace:
                return "⌘⌥Space"
            case .commandShiftSpace:
                return "⌘⇧Space"
            case .commandShiftOptionSpace:
                return "⌘⌥⇧Space"
            case .commandOptionK:
                return "⌘⌥K"
            case .commandShiftOptionK:
                return "⌘⌥⇧K"
            }
        }

        var shortcut: Shortcut {
            switch self {
            case .commandOptionSpace:
                return Shortcut(keyCode: UInt32(kVK_Space), modifiers: UInt32(cmdKey | optionKey))
            case .commandShiftSpace:
                return Shortcut(keyCode: UInt32(kVK_Space), modifiers: UInt32(cmdKey | shiftKey))
            case .commandShiftOptionSpace:
                return Shortcut(keyCode: UInt32(kVK_Space), modifiers: UInt32(cmdKey | shiftKey | optionKey))
            case .commandOptionK:
                return Shortcut(keyCode: UInt32(kVK_ANSI_K), modifiers: UInt32(cmdKey | optionKey))
            case .commandShiftOptionK:
                return Shortcut(keyCode: UInt32(kVK_ANSI_K), modifiers: UInt32(cmdKey | shiftKey | optionKey))
            }
        }

        static let defaultPreset: ShortcutPreset = .commandOptionSpace
    }

    private var hotKeyRef: EventHotKeyRef?
    private let callback: @Sendable () -> Void

    init(shortcut: Shortcut, callback: @escaping @Sendable () -> Void) throws {
        self.callback = callback

        let hotKeyID = EventHotKeyID(signature: OSType(0x534E4150), id: 1)
        let status = RegisterEventHotKey(
            shortcut.keyCode,
            shortcut.modifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &hotKeyRef
        )

        guard status == noErr else {
            throw HotkeyError.registrationFailed(status)
        }

        let eventSpec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(
            GetApplicationEventTarget(),
            { _, event, userData in
                guard let userData else { return noErr }
                let manager = Unmanaged<HotkeyManager>.fromOpaque(userData).takeUnretainedValue()
                manager.callback()
                return noErr
            },
            1,
            [eventSpec],
            UnsafeMutableRawPointer(Unmanaged.passUnretained(self).toOpaque()),
            nil
        )
    }

    deinit {
        if let hotKeyRef {
            UnregisterEventHotKey(hotKeyRef)
        }
    }
}

enum HotkeyError: LocalizedError {
    case registrationFailed(OSStatus)

    var errorDescription: String? {
        switch self {
        case let .registrationFailed(status):
            return "RegisterEventHotKey failed with status \(status)."
        }
    }
}
