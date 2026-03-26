import AppKit
import ApplicationServices
import Foundation

enum SelectionCaptureOutcome: Equatable {
    case text(String)
    case requiresPermission
    case requiresClipboardFallback(changeCount: Int)
}

@MainActor
protocol SelectionCapture {
    func captureSelection() async -> SelectionCaptureOutcome
    func waitForClipboardChange(after changeCount: Int) async -> String?
}

@MainActor
struct AccessibilitySelectionCapture: SelectionCapture {
    func captureSelection() async -> SelectionCaptureOutcome {
        guard AXIsProcessTrusted() else {
            return .requiresPermission
        }

        let systemWide = AXUIElementCreateSystemWide()
        var focusedObject: CFTypeRef?
        let focusStatus = AXUIElementCopyAttributeValue(systemWide, kAXFocusedUIElementAttribute as CFString, &focusedObject)
        guard focusStatus == .success, let focused = focusedObject else {
            return .requiresClipboardFallback(changeCount: NSPasteboard.general.changeCount)
        }

        let focusedElement = focused as! AXUIElement
        var selectedTextObject: CFTypeRef?
        let selectionStatus = AXUIElementCopyAttributeValue(focusedElement, kAXSelectedTextAttribute as CFString, &selectedTextObject)

        if selectionStatus == .success,
           let text = selectedTextObject as? String,
           !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return .text(text)
        }

        return .requiresClipboardFallback(changeCount: NSPasteboard.general.changeCount)
    }

    func waitForClipboardChange(after changeCount: Int) async -> String? {
        let pasteboard = NSPasteboard.general
        for _ in 0 ..< 75 {
            try? await Task.sleep(for: .milliseconds(200))
            if Task.isCancelled { return nil }
            guard pasteboard.changeCount != changeCount else { continue }
            if let text = pasteboard.string(forType: .string)?.trimmingCharacters(in: .whitespacesAndNewlines),
               !text.isEmpty {
                return text
            }
        }

        return nil
    }
}
