import SwiftUI

@main
struct SnapLingoApp: App {
    @StateObject private var model = AppModel()
    @StateObject private var settingsStore = SettingsStore()

    var body: some Scene {
        MenuBarExtra("SnapLingo", systemImage: "text.word.spacing") {
            MenuBarAppView(model: model, settingsStore: settingsStore)
        }
        .menuBarExtraStyle(.window)
    }
}
