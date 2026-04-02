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
                return MacStrings.shared.string("hotkey.commandOptionSpace.displayName")
            case .commandShiftSpace:
                return MacStrings.shared.string("hotkey.commandShiftSpace.displayName")
            case .commandShiftOptionSpace:
                return MacStrings.shared.string("hotkey.commandShiftOptionSpace.displayName")
            case .commandOptionK:
                return MacStrings.shared.string("hotkey.commandOptionK.displayName")
            case .commandShiftOptionK:
                return MacStrings.shared.string("hotkey.commandShiftOptionK.displayName")
            }
        }

        var compactLabel: String {
            switch self {
            case .commandOptionSpace:
                return MacStrings.shared.string("hotkey.commandOptionSpace.compactLabel")
            case .commandShiftSpace:
                return MacStrings.shared.string("hotkey.commandShiftSpace.compactLabel")
            case .commandShiftOptionSpace:
                return MacStrings.shared.string("hotkey.commandShiftOptionSpace.compactLabel")
            case .commandOptionK:
                return MacStrings.shared.string("hotkey.commandOptionK.compactLabel")
            case .commandShiftOptionK:
                return MacStrings.shared.string("hotkey.commandShiftOptionK.compactLabel")
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
