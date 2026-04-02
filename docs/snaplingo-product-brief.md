# SnapLingo Product Brief

Updated: 2026-04-02
Status: Active desktop implementation with Windows shell expansion

## Overview

SnapLingo is a desktop utility for turning selected text into usable translated or polished output without forcing the user to leave the current app. The core product loop is:

1. Select text in any desktop app.
2. Press the global hotkey.
3. SnapLingo captures the selection directly or falls back to the clipboard.
4. A compact panel shows the best current result.
5. The user copies the result and returns to work.

The repository now contains two desktop clients:

- `macOS`: SwiftPM + SwiftUI menu bar app in `Sources/SnapLingo`
- `Windows`: WinUI 3 desktop client in `SnapLingoWindows`

## Product Goal

Deliver the fastest selection-to-copy translation flow for short desktop text such as chat messages, emails, documentation snippets, browser paragraphs, subtitles, and UI copy.

The product is not a full translation workspace. It is a utility that stays out of the way and finishes the job in one small interruption.

## Current User Experience

- Global hotkey opens the workflow instead of requiring an app switch
- Direct selection capture is attempted first
- Clipboard fallback is explicit when direct capture fails
- `Translate` and `Polish` share the same compact result surface
- Chinese text defaults to `Translate`
- Non-CJK text defaults to `Polish`
- Translation mode can show a quick translation before a polished result
- Retry and copy are first-class actions
- Windows now includes a standalone translation panel, a small selection launcher near detected text selections, and a settings shell for provider, model, hotkey, prompt, and language controls
- Shared provider defaults and Windows localization strings are now bundled as repo resources so both desktop clients can stay aligned on presets and copy

## Supported Providers

SnapLingo currently supports these providers:

- `OpenAI`
- `Anthropic`
- `Google Gemini`
- `Zhipu GLM`
- `Kimi`
- `MiniMax`
- `Alibaba Bailian`
- `Volcengine Ark`

Provider integration is adapter-based. `OpenAI` uses the Responses API, `Anthropic` uses Messages, `Gemini` uses `generateContent`, and the remaining providers use curated OpenAI-compatible chat presets.

## Platform Notes

### macOS

- App model: menu bar utility
- Selection path: Accessibility APIs, then clipboard fallback
- Secret storage: Keychain via `CredentialStore`
- Provider defaults and localized strings now load from bundled JSON resources
- Build/test flow: `swift build`, `swift test`, `swift run`

### Windows

- App model: WinUI 3 settings shell with a standalone translation panel
- Selection path: UI Automation text/selection patterns, then clipboard fallback
- Auto-selection monitoring can surface a lightweight launcher near the recent selection before opening the translation panel
- Secret storage: DPAPI-encrypted files via `SecureSecretStore`
- Settings can switch provider, model, hotkey, prompt profile, and interface language
- Run flow: `.\run-windows-client.ps1`

## Current Non-Goals

- Replace selection in the source app
- Undo
- Context-aware translation modes
- History and memory
- Cloud sync for settings, secrets, or prompt profiles
- Rich tone controls, prompt marketplaces, or multi-step writing workspaces
- OCR, screenshot translation, or document translation
- Mobile, web, or Linux clients

## Quality Bar

- The user should be able to trigger the app from another desktop program and finish with a copy action in one short loop.
- Capture failures should degrade into a clear clipboard fallback instead of a silent failure.
- Provider failures should surface as explicit error states.
- API keys must stay in secure platform storage and must not be written to plain-text config files.

## Current Gaps

- Automated coverage now spans both `Tests/SnapLingoTests` and the lightweight Windows regression project in `SnapLingoWindows.Tests`, but WinUI shell behavior is still not UI-automated.
- Windows validation still needs manual smoke coverage for hotkeys, overlay windows, selection capture, and host-app integration even when logic tests pass.
- Feature parity should still be treated as a product goal, not an assumption; Windows currently ships extra settings capabilities such as prompt profiles and interface language switching.
