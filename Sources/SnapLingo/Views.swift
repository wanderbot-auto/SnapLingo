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
    @State private var showsOriginalText = false

    private var store: PanelStateStore { model.store }
    private let panelCornerRadius: CGFloat = 18

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.980, green: 0.980, blue: 0.978),
                    Color(red: 0.954, green: 0.956, blue: 0.962),
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            Circle()
                .fill(.white.opacity(0.92))
                .frame(width: 320, height: 320)
                .blur(radius: 18)
                .offset(x: 105, y: 54)

            Circle()
                .fill(Color(red: 0.929, green: 0.945, blue: 1.0).opacity(0.58))
                .frame(width: 252, height: 252)
                .blur(radius: 32)
                .offset(x: -138, y: -120)

            panelShell
                .padding(26)
        }
        .frame(width: 438, height: 328)
    }

    private var panelShell: some View {
        VStack(spacing: 0) {
            headerBar
            previewSurface
            divider
            modeBar
            divider
            contentSection
            divider
            footerBar
        }
        .background(
            RoundedRectangle(cornerRadius: panelCornerRadius, style: .continuous)
                .fill(.white.opacity(0.84))
        )
        .overlay(
            RoundedRectangle(cornerRadius: panelCornerRadius, style: .continuous)
                .stroke(.white.opacity(0.82), lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: panelCornerRadius, style: .continuous))
        .shadow(color: .black.opacity(0.08), radius: 24, y: 16)
        .shadow(color: .white.opacity(0.7), radius: 8, y: -1)
    }

    private var headerBar: some View {
        HStack(spacing: 10) {
            Image(systemName: "square.grid.3x3.fill")
                .font(.system(size: 8, weight: .bold))
                .foregroundStyle(SnapTheme.tertiaryInk.opacity(0.58))

            Text("Auto-Detect")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(SnapTheme.subtleInk)

            Image(systemName: "arrow.right")
                .font(.system(size: 11, weight: .semibold))
                .foregroundStyle(SnapTheme.tertiaryInk.opacity(0.75))

            Text(resultLanguageLabel)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(resultAccentColor)
                .padding(.horizontal, 12)
                .padding(.vertical, 6)
                .background(
                    Capsule(style: .continuous)
                        .fill(resultAccentColor.opacity(0.13))
                )

            Spacer()

            iconButton(symbol: "pin", isEnabled: false) {}

            iconButton(symbol: "xmark") {
                model.dismissPanel()
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
    }

    private var previewSurface: some View {
        ZStack(alignment: .topLeading) {
            RoundedRectangle(cornerRadius: 0, style: .continuous)
                .fill(
                    LinearGradient(
                        colors: [
                            Color(red: 0.960, green: 0.964, blue: 0.971),
                            Color(red: 0.944, green: 0.947, blue: 0.955),
                        ],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                )

            RoundedRectangle(cornerRadius: 0, style: .continuous)
                .fill(.white.opacity(0.18))
                .padding(.horizontal, 1)
                .blur(radius: 12)

            VStack(alignment: .leading, spacing: 8) {
                if showsOriginalText || shouldShowContextHint {
                    Text(previewText)
                        .font(.system(size: 12.5, weight: .medium))
                        .foregroundStyle(SnapTheme.tertiaryInk.opacity(0.92))
                        .lineSpacing(3)
                        .lineLimit(4)
                        .multilineTextAlignment(.leading)
                        .textSelection(.enabled)
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)
        }
        .frame(height: 92)
        .clipped()
    }

    private var modeBar: some View {
        HStack(spacing: 10) {
            HStack(spacing: 6) {
                ForEach(TranslationMode.allCases) { mode in
                    Button {
                        model.selectMode(mode)
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: mode.symbolName)
                                .font(.system(size: 11, weight: .semibold))
                            Text(mode.chipTitle)
                                .font(.system(size: 13, weight: .semibold))
                        }
                        .foregroundStyle(store.selectedMode == mode ? resultAccentColor : SnapTheme.subtleInk)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 9)
                        .background(
                            RoundedRectangle(cornerRadius: 12, style: .continuous)
                                .fill(store.selectedMode == mode ? .white : .clear)
                        )
                        .overlay(
                            RoundedRectangle(cornerRadius: 12, style: .continuous)
                                .stroke(
                                    store.selectedMode == mode
                                        ? SnapTheme.hairlineStrong
                                        : Color.clear,
                                    lineWidth: 1
                                )
                        )
                        .shadow(
                            color: store.selectedMode == mode ? .black.opacity(0.06) : .clear,
                            radius: 9,
                            y: 5
                        )
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(4)
            .background(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .fill(Color(red: 0.974, green: 0.975, blue: 0.979))
            )

            Spacer()

            HStack(spacing: 14) {
                Image(systemName: statusSymbolName)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(statusSymbolColor)

                Button {
                    showsOriginalText.toggle()
                } label: {
                    Image(systemName: "text.alignleft")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(showsOriginalText ? SnapTheme.ink : SnapTheme.tertiaryInk)
                }
                .buttonStyle(.plain)
                .disabled(store.originalPreview == nil && !shouldShowContextHint)

                Image(systemName: store.selectedMode == .translate ? "sparkles" : "wand.and.stars")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(resultAccentColor.opacity(0.88))
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
    }

    private var contentSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            if let text = store.primaryText, !text.isEmpty {
                Text(text)
                    .font(.system(size: 15, weight: .medium))
                    .foregroundStyle(SnapTheme.ink)
                    .lineSpacing(4)
                    .textSelection(.enabled)
            } else {
                HStack(spacing: 10) {
                    ProgressView()
                        .controlSize(.small)
                    Text(primaryPlaceholder)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(SnapTheme.subtleInk)
                }
            }

            if let status = store.secondaryStatus {
                Text(status)
                    .font(.system(size: 11.5, weight: .medium))
                    .foregroundStyle(SnapTheme.tertiaryInk)
            }

            if case .permissionRequired = store.phase {
                inlineBanner(
                    title: "Accessibility access is required before SnapLingo can read selected text.",
                    actionTitle: "Open Settings",
                    action: model.openAccessibilitySettings
                )
            }

            if case .waitingForClipboard = store.phase {
                inlineBanner(
                    title: "Copy the current selection and SnapLingo will continue automatically.",
                    actionTitle: "Retry",
                    action: model.retryCurrentFlow
                )
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }

    private var footerBar: some View {
        HStack(spacing: 12) {
            HStack(spacing: 7) {
                Image(systemName: "sparkles")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 18, height: 18)
                    .background(
                        RoundedRectangle(cornerRadius: 6, style: .continuous)
                            .fill(resultAccentColor)
                    )

                Text(model.activeModelName)
                    .font(.system(size: 12.5, weight: .medium))
                    .foregroundStyle(SnapTheme.subtleInk)
                    .lineLimit(1)

                Image(systemName: "chevron.down")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(SnapTheme.tertiaryInk)
            }
            .help("\(model.activeProviderName) · \(model.activeModelName)")

            Spacer(minLength: 10)

            HStack(spacing: 14) {
                Image(systemName: "speaker.wave.2")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(SnapTheme.tertiaryInk.opacity(0.55))

                Button {
                    model.retryCurrentFlow()
                } label: {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(store.canRetry ? SnapTheme.subtleInk : SnapTheme.tertiaryInk.opacity(0.45))
                }
                .buttonStyle(.plain)
                .disabled(!store.canRetry)
                .keyboardShortcut("r")

                Rectangle()
                    .fill(SnapTheme.hairlineStrong)
                    .frame(width: 1, height: 18)

                Button(store.isCopied ? "Copied" : "Copy") {
                    model.copyPrimaryResult()
                }
                .buttonStyle(PanelCopyButtonStyle())
                .disabled(!store.canCopy)
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 11)
    }

    private var divider: some View {
        Rectangle()
            .fill(SnapTheme.hairlineStrong)
            .frame(height: 1)
    }

    private var previewText: String {
        if let originalPreview = store.originalPreview, !originalPreview.isEmpty {
            return originalPreview
        }

        switch store.phase {
        case .capturing:
            return "Reading the current selection and preparing the translation pipeline."
        case .waitingForClipboard:
            return "This app blocks direct selection access. Copy the text once and SnapLingo will continue from the clipboard."
        case .permissionRequired:
            return "Grant Accessibility permission so SnapLingo can read selected text from other apps."
        case let .error(message):
            return message
        case .loadingTranslation, .loadingPolish:
            return "The original text will appear here once the current session has content to compare."
        case .idle:
            return "Select text in any app and press the SnapLingo hotkey to open this panel."
        case .partial, .ready:
            return "The original text is hidden. Use the middle toggle if you want to compare it against the result."
        }
    }

    private var shouldShowContextHint: Bool {
        switch store.phase {
        case .capturing, .waitingForClipboard, .permissionRequired, .error(_), .idle:
            return true
        default:
            return false
        }
    }

    private var primaryPlaceholder: String {
        switch store.phase {
        case .capturing:
            return "Capturing your selection..."
        case .loadingTranslation:
            return "Generating a natural English translation..."
        case .loadingPolish:
            return "Refining the English copy for send-ready tone..."
        case .waitingForClipboard:
            return "Waiting for clipboard content..."
        case .permissionRequired:
            return "SnapLingo cannot proceed until Accessibility is enabled."
        case .idle:
            return "Ready for the next selection."
        case let .error(message):
            return message
        case .partial:
            return "Preparing the final pass..."
        case .ready:
            return "Result ready."
        }
    }

    private var resultLanguageLabel: String {
        "English"
    }

    private var resultAccentColor: Color {
        store.selectedMode == .translate ? SnapTheme.translationAccent : SnapTheme.polishAccent
    }

    private var statusSymbolName: String {
        switch store.phase {
        case .ready:
            return "checkmark"
        case .partial:
            return "ellipsis"
        case .error(_):
            return "exclamationmark"
        default:
            return "checkmark"
        }
    }

    private var statusSymbolColor: Color {
        switch store.phase {
        case .error(_):
            return SnapTheme.accent
        case .ready:
            return resultAccentColor
        default:
            return SnapTheme.tertiaryInk
        }
    }

    private func iconButton(symbol: String, isEnabled: Bool = true, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(isEnabled ? SnapTheme.tertiaryInk : SnapTheme.tertiaryInk.opacity(0.45))
                .frame(width: 18, height: 18)
        }
        .buttonStyle(.plain)
        .disabled(!isEnabled)
    }

    private func inlineBanner(title: String, actionTitle: String, action: @escaping () -> Void) -> some View {
        HStack(spacing: 10) {
            Text(title)
                .font(.system(size: 11.5, weight: .medium))
                .foregroundStyle(SnapTheme.subtleInk)
                .fixedSize(horizontal: false, vertical: true)

            Spacer(minLength: 6)

            Button(actionTitle, action: action)
                .buttonStyle(PanelMiniActionButtonStyle())
        }
    }
}

private struct PanelCopyButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12.5, weight: .semibold))
            .foregroundStyle(SnapTheme.ink)
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 11, style: .continuous)
                    .fill(configuration.isPressed ? Color.white.opacity(0.84) : .white.opacity(0.96))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 11, style: .continuous)
                    .stroke(SnapTheme.hairlineStrong, lineWidth: 1)
            )
            .shadow(color: .black.opacity(configuration.isPressed ? 0.03 : 0.05), radius: 8, y: 4)
            .scaleEffect(configuration.isPressed ? 0.985 : 1)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

private struct PanelMiniActionButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 11.5, weight: .semibold))
            .foregroundStyle(SnapTheme.subtleInk)
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(
                Capsule(style: .continuous)
                    .fill(configuration.isPressed ? Color.white.opacity(0.72) : Color.white.opacity(0.92))
            )
            .overlay(
                Capsule(style: .continuous)
                    .stroke(SnapTheme.hairlineStrong, lineWidth: 1)
            )
    }
}

private extension TranslationMode {
    var chipTitle: String {
        switch self {
        case .translate:
            return "Translate"
        case .polish:
            return "Polish"
        }
    }

    var symbolName: String {
        switch self {
        case .translate:
            return "globe"
        case .polish:
            return "sparkles"
        }
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
    static let hairlineStrong = Color.black.opacity(0.08)
    static let translationAccent = Color(red: 0.144, green: 0.443, blue: 0.965)
    static let polishAccent = Color(red: 0.455, green: 0.353, blue: 0.941)
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
