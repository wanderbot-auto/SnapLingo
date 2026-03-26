import SwiftUI

struct MenuBarAppView: View {
    @ObservedObject var model: AppModel
    @ObservedObject var settingsStore: SettingsStore

    var body: some View {
        ZStack(alignment: .topLeading) {
            SnapBackdrop()

            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 12) {
                    heroCard
                    quickActionsCard
                    SettingsView(model: model, store: settingsStore)
                    footer
                }
                .padding(14)
            }
        }
        .frame(width: 396, height: 516)
    }

    private var heroCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .center, spacing: 12) {
                ZStack {
                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .fill(
                            LinearGradient(
                                colors: [SnapTheme.accentStrong, SnapTheme.accent],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 52, height: 52)

                    Image(systemName: "text.word.spacing")
                        .font(.system(size: 22, weight: .bold))
                        .foregroundStyle(.white)
                }

                VStack(alignment: .leading, spacing: 5) {
                    Text("SnapLingo")
                        .font(.system(size: 22, weight: .semibold, design: .rounded))
                        .foregroundStyle(SnapTheme.ink)

                    Text("A quiet menu bar tool for fast translation and one-click English polishing.")
                        .font(.system(size: 11.5, weight: .medium))
                        .foregroundStyle(SnapTheme.subtleInk)
                        .fixedSize(horizontal: false, vertical: true)
                }

                Spacer()

                HStack(spacing: 6) {
                    compactBadge("Copy First")
                    compactBadge(model.hotkeyDisplayText)
                }
            }
        }
        .snapSurface(tint: SnapTheme.accent.opacity(0.12))
    }

    private var quickActionsCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            sectionHeader(title: "Quick Actions", detail: "Run once from here or jump to permissions.")

            HStack(spacing: 10) {
                Button {
                    Task { @MainActor in
                        await model.handleHotkey()
                    }
                } label: {
                    Label("Translate Selection", systemImage: "sparkles.rectangle.stack")
                }
                .snapPrimaryButton()

                Button {
                    model.openAccessibilitySettings()
                } label: {
                    Label("Accessibility", systemImage: "hand.raised")
                }
                .snapSecondaryButton()
            }

            HStack(spacing: 8) {
                utilityPill(symbol: "lock.fill", text: "Keys in Keychain")
                utilityPill(symbol: "bolt.fill", text: "Two-step pipeline")
            }
        }
        .snapSurface()
    }

    private var footer: some View {
        HStack {
            Text("Local-first settings. API keys never land in config files.")
                .font(.system(size: 10.5, weight: .medium))
                .foregroundStyle(SnapTheme.tertiaryInk)

            Spacer()

            Button("Quit") {
                NSApplication.shared.terminate(nil)
            }
            .snapQuietButton()
        }
        .padding(.horizontal, 4)
    }

    private func statusBadge(title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.system(size: 9, weight: .bold))
                .foregroundStyle(SnapTheme.tertiaryInk)
            Text(value)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(SnapTheme.ink)
        }
        .padding(.horizontal, 11)
        .padding(.vertical, 9)
        .background(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .fill(.white.opacity(0.76))
        )
    }

    private func sectionHeader(title: String, detail: String) -> some View {
        HStack(alignment: .firstTextBaseline) {
            Text(title)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(SnapTheme.ink)
            Spacer(minLength: 8)
            Text(detail)
                .font(.system(size: 11))
                .foregroundStyle(SnapTheme.subtleInk)
                .multilineTextAlignment(.trailing)
        }
    }

    private func utilityPill(symbol: String, text: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: symbol)
                .font(.system(size: 10, weight: .bold))
            Text(text)
                .font(.system(size: 11, weight: .semibold))
        }
        .foregroundStyle(SnapTheme.subtleInk)
        .padding(.horizontal, 9)
        .padding(.vertical, 7)
        .background(
            Capsule(style: .continuous)
                .fill(SnapTheme.cream)
        )
    }

    private func compactBadge(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 11, weight: .semibold))
            .foregroundStyle(SnapTheme.subtleInk)
            .padding(.horizontal, 9)
            .padding(.vertical, 7)
            .background(
                Capsule(style: .continuous)
                    .fill(.white.opacity(0.76))
            )
    }
}

struct ResultPanelView: View {
    @ObservedObject var model: AppModel

    private var store: PanelStateStore { model.store }

    var body: some View {
        ZStack(alignment: .topLeading) {
            SnapBackdrop()

            VStack(alignment: .leading, spacing: 14) {
                Picker("Mode", selection: Binding(
                    get: { store.selectedMode },
                    set: { model.selectMode($0) }
                )) {
                    ForEach(TranslationMode.allCases) { mode in
                        Text(mode.rawValue).tag(mode)
                    }
                }
                .pickerStyle(.segmented)

                VStack(alignment: .leading, spacing: 12) {
                    HStack(alignment: .firstTextBaseline) {
                        Text(store.primaryTitle)
                            .font(.system(size: 20, weight: .semibold, design: .rounded))
                            .foregroundStyle(SnapTheme.ink)

                        Spacer()

                        Text(store.modeSourceLabel)
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(SnapTheme.tertiaryInk)
                            .padding(.horizontal, 9)
                            .padding(.vertical, 6)
                            .background(
                                Capsule(style: .continuous)
                                    .fill(SnapTheme.cream)
                            )
                    }

                    if let text = store.primaryText {
                        Text(text)
                            .font(.system(size: 15, weight: .medium))
                            .foregroundStyle(SnapTheme.ink)
                            .lineSpacing(3)
                            .textSelection(.enabled)
                    } else {
                        ProgressView()
                            .controlSize(.small)
                    }

                    if let status = store.secondaryStatus {
                        Text(status)
                            .font(.system(size: 12, weight: .medium))
                            .foregroundStyle(SnapTheme.subtleInk)
                    }
                }
                .snapSurface()

                if let originalPreview = store.originalPreview {
                    DisclosureGroup("Original text") {
                        Text(originalPreview)
                            .font(.system(size: 12.5, weight: .medium))
                            .foregroundStyle(SnapTheme.subtleInk)
                            .lineLimit(3)
                            .textSelection(.enabled)
                            .padding(.top, 6)
                    }
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(SnapTheme.subtleInk)
                }

                HStack(spacing: 10) {
                    Button(store.isCopied ? "Copied" : "Copy") {
                        model.copyPrimaryResult()
                    }
                    .snapPrimaryButton()
                    .disabled(!store.canCopy)
                    .keyboardShortcut(.defaultAction)

                    Button("Retry") {
                        model.retryCurrentFlow()
                    }
                    .snapSecondaryButton()
                    .disabled(!store.canRetry)
                    .keyboardShortcut("r")
                }

                if case .permissionRequired = store.phase {
                    permissionPrompt
                }

                if case .waitingForClipboard = store.phase {
                    clipboardPrompt
                }
            }
            .padding(16)
        }
        .frame(width: 420)
    }

    private var permissionPrompt: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("SnapLingo needs Accessibility access to read selected text from other apps.")
                .font(.system(size: 12))
                .foregroundStyle(SnapTheme.subtleInk)

            Button("Open Accessibility Settings") {
                model.openAccessibilitySettings()
            }
            .snapSecondaryButton()
        }
        .snapSurface()
    }

    private var clipboardPrompt: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Press Copy in the current app to continue.")
                .font(.system(size: 12))
                .foregroundStyle(SnapTheme.subtleInk)
            Text("SnapLingo will continue automatically as soon as the clipboard changes.")
                .font(.system(size: 11))
                .foregroundStyle(SnapTheme.tertiaryInk)
        }
        .snapSurface()
    }
}

struct SettingsView: View {
    @ObservedObject var model: AppModel
    @ObservedObject var store: SettingsStore

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            providerCard
            hotkeyCard
        }
    }

    private var providerCard: some View {
        VStack(alignment: .leading, spacing: 12) {
            header(
                title: "Provider",
                detail: "Choose the model backend and manage its API key."
            )

            HStack(alignment: .center, spacing: 10) {
                VStack(alignment: .leading, spacing: 6) {
                    fieldLabel("Backend")

                    Picker("Provider", selection: Binding(
                        get: { store.selectedProvider },
                        set: { store.selectProvider($0) }
                    )) {
                        ForEach(ProviderKind.allCases) { provider in
                            Text(provider.displayName).tag(provider)
                        }
                    }
                    .labelsHidden()
                    .pickerStyle(.menu)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .snapControlSurface()
                }

                VStack(alignment: .leading, spacing: 6) {
                    fieldLabel("Actions")

                    HStack(spacing: 8) {
                        Button("Save") {
                            store.saveAPIKey()
                        }
                        .snapPrimaryButton()

                        Button("Clear") {
                            store.clearAPIKey()
                        }
                        .snapSecondaryButton()
                    }
                }
                .fixedSize()
            }

            VStack(alignment: .leading, spacing: 8) {
                fieldLabel("API Key")

                SecureField(store.selectedProvider.apiKeyPlaceholder, text: $store.apiKeyInput)
                    .textFieldStyle(.plain)
                    .font(.system(size: 13, weight: .medium, design: .monospaced))
                    .foregroundStyle(SnapTheme.ink)
                    .snapControlSurface(vertical: 11)
            }

            Text("Saved only in macOS Keychain. Nothing is written to plain-text config files.")
                .font(.system(size: 12))
                .foregroundStyle(SnapTheme.subtleInk)
                .fixedSize(horizontal: false, vertical: true)

            if let statusMessage = store.statusMessage {
                Text(statusMessage)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(SnapTheme.subtleInk)
            }
        }
        .snapSurface()
    }

    private var hotkeyCard: some View {
        VStack(alignment: .leading, spacing: 12) {
            header(
                title: "Hotkey",
                detail: "Pick one of the stable shortcuts supported by the current global hotkey path."
            )

            HStack(alignment: .center, spacing: 10) {
                VStack(alignment: .leading, spacing: 6) {
                    fieldLabel("Preset")

                    Picker("Hotkey", selection: Binding(
                        get: { model.selectedShortcutPreset },
                        set: { model.updateShortcutPreset($0) }
                    )) {
                        ForEach(HotkeyManager.ShortcutPreset.allCases) { preset in
                            Text(preset.displayName).tag(preset)
                        }
                    }
                    .labelsHidden()
                    .pickerStyle(.menu)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .snapControlSurface()
                }

                Text(model.selectedShortcutPreset.compactLabel)
                    .font(.system(size: 12, weight: .semibold, design: .rounded))
                    .foregroundStyle(SnapTheme.ink)
                    .snapControlSurface(
                        tint: SnapTheme.accentWash,
                        stroke: SnapTheme.accent.opacity(0.22)
                    )
                    .fixedSize()
            }

            if let hotkeyStatusMessage = model.hotkeyStatusMessage {
                Text(hotkeyStatusMessage)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(SnapTheme.subtleInk)
            }
        }
        .snapSurface()
    }

    private func header(title: String, detail: String) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(title)
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(SnapTheme.ink)
            Text(detail)
                .font(.system(size: 12))
                .foregroundStyle(SnapTheme.subtleInk)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func fieldLabel(_ title: String) -> some View {
        Text(title)
            .font(.system(size: 11, weight: .bold))
            .foregroundStyle(SnapTheme.tertiaryInk)
    }

}

private struct SnapBackdrop: View {
    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.996, green: 0.983, blue: 0.962),
                    Color(red: 0.988, green: 0.971, blue: 0.940),
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            Circle()
                .fill(SnapTheme.accent.opacity(0.16))
                .frame(width: 260, height: 260)
                .blur(radius: 22)
                .offset(x: 160, y: -130)

            Circle()
                .fill(SnapTheme.sky.opacity(0.14))
                .frame(width: 220, height: 220)
                .blur(radius: 26)
                .offset(x: -110, y: 240)
        }
        .ignoresSafeArea()
    }
}

private enum SnapTheme {
    static let ink = Color(red: 0.145, green: 0.129, blue: 0.106)
    static let subtleInk = Color(red: 0.353, green: 0.318, blue: 0.271)
    static let tertiaryInk = Color(red: 0.518, green: 0.467, blue: 0.400)
    static let accent = Color(red: 0.925, green: 0.522, blue: 0.247)
    static let accentStrong = Color(red: 0.839, green: 0.384, blue: 0.137)
    static let accentWash = Color(red: 0.998, green: 0.935, blue: 0.883)
    static let cream = Color(red: 0.984, green: 0.949, blue: 0.901)
    static let sky = Color(red: 0.482, green: 0.694, blue: 0.910)
    static let hairline = Color.black.opacity(0.06)
}

private extension View {
    func snapSurface(tint: Color = .white.opacity(0.55)) -> some View {
        self
            .padding(14)
            .background(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .fill(
                        LinearGradient(
                            colors: [Color.white.opacity(0.90), tint],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
            )
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(SnapTheme.hairline, lineWidth: 1)
            )
    }

    func snapPrimaryButton() -> some View {
        self
            .buttonStyle(SnapButtonStyle(kind: .primary))
    }

    func snapSecondaryButton() -> some View {
        self
            .buttonStyle(SnapButtonStyle(kind: .secondary))
    }

    func snapQuietButton() -> some View {
        self
            .buttonStyle(SnapButtonStyle(kind: .quiet))
    }

    func snapControlSurface(
        horizontal: CGFloat = 12,
        vertical: CGFloat = 10,
        tint: Color = .white.opacity(0.82),
        stroke: Color = SnapTheme.hairline
    ) -> some View {
        self
            .padding(.horizontal, horizontal)
            .padding(.vertical, vertical)
            .background(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(tint)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .stroke(stroke, lineWidth: 0.75)
            )
    }
}

private struct SnapButtonStyle: ButtonStyle {
    enum Kind {
        case primary
        case secondary
        case quiet
    }

    let kind: Kind

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12, weight: .semibold))
            .foregroundStyle(foregroundColor)
            .padding(.horizontal, horizontalPadding)
            .padding(.vertical, verticalPadding)
            .frame(minHeight: kind == .quiet ? nil : 42)
            .background(background(configuration.isPressed))
            .overlay(border(configuration.isPressed))
            .scaleEffect(configuration.isPressed ? 0.985 : 1)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }

    private var foregroundColor: Color {
        switch kind {
        case .primary:
            return .white
        case .secondary:
            return SnapTheme.ink
        case .quiet:
            return SnapTheme.subtleInk
        }
    }

    private var horizontalPadding: CGFloat {
        switch kind {
        case .primary, .secondary:
            return 12
        case .quiet:
            return 8
        }
    }

    private var verticalPadding: CGFloat {
        switch kind {
        case .primary, .secondary:
            return 8
        case .quiet:
            return 6
        }
    }

    @ViewBuilder
    private func background(_ pressed: Bool) -> some View {
        switch kind {
        case .primary:
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(
                    LinearGradient(
                        colors: pressed
                            ? [SnapTheme.accentStrong.opacity(0.96), SnapTheme.accentStrong.opacity(0.96)]
                            : [SnapTheme.accentStrong.opacity(0.94), SnapTheme.accent.opacity(0.92)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                )
        case .secondary:
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(pressed ? SnapTheme.cream.opacity(0.92) : .white.opacity(0.82))
        case .quiet:
            Capsule(style: .continuous)
                .fill(pressed ? SnapTheme.cream.opacity(0.95) : .clear)
        }
    }

    @ViewBuilder
    private func border(_ pressed: Bool) -> some View {
        switch kind {
        case .primary:
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .stroke(.white.opacity(0.08), lineWidth: 0.65)
        case .secondary:
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .stroke(pressed ? SnapTheme.accent.opacity(0.14) : SnapTheme.hairline, lineWidth: 0.7)
        case .quiet:
            Capsule(style: .continuous)
                .stroke(.clear, lineWidth: 0)
        }
    }
}
