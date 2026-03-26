import AppKit
import SwiftUI

@MainActor
final class FloatingPanelController {
    private final class Panel: NSPanel {
        override var canBecomeKey: Bool { true }
        override var canBecomeMain: Bool { false }
    }

    private let panel: Panel

    init<Content: View>(rootView: Content) {
        panel = Panel(
            contentRect: NSRect(x: 0, y: 0, width: 420, height: 300),
            styleMask: [.nonactivatingPanel, .titled, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        panel.isReleasedWhenClosed = false
        panel.titleVisibility = .hidden
        panel.titlebarAppearsTransparent = true
        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .moveToActiveSpace]
        panel.standardWindowButton(.closeButton)?.isHidden = true
        panel.standardWindowButton(.miniaturizeButton)?.isHidden = true
        panel.standardWindowButton(.zoomButton)?.isHidden = true
        panel.contentView = NSHostingView(rootView: rootView)
    }

    func show() {
        positionNearMouse()
        panel.orderFrontRegardless()
        panel.makeKey()
    }

    func hide() {
        panel.orderOut(nil)
    }

    private func positionNearMouse() {
        let mouseLocation = NSEvent.mouseLocation
        let panelSize = panel.frame.size
        guard let screen = NSScreen.screens.first(where: { NSMouseInRect(mouseLocation, $0.frame, false) }) ?? NSScreen.main else {
            return
        }

        let padding: CGFloat = 20
        let minX = screen.visibleFrame.minX + padding
        let maxX = screen.visibleFrame.maxX - panelSize.width - padding
        let minY = screen.visibleFrame.minY + padding
        let maxY = screen.visibleFrame.maxY - panelSize.height - padding

        let origin = NSPoint(
            x: min(max(mouseLocation.x - panelSize.width / 2, minX), maxX),
            y: min(max(mouseLocation.y - panelSize.height / 2, minY), maxY)
        )
        panel.setFrameOrigin(origin)
    }
}
