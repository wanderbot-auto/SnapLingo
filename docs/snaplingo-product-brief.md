# SnapLingo Product Brief

Updated: 2026-04-01
Status: Active desktop implementation

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
- Build/test flow: `swift build`, `swift test`, `swift run`

### Windows

- App model: WinUI 3 desktop application
- Selection path: UI Automation text/selection patterns, then clipboard fallback
- Secret storage: DPAPI-encrypted files via `SecureSecretStore`
- Run flow: `.\run-windows-client.ps1`

## Current Non-Goals

- Replace selection in the source app
- Undo
- Context-aware translation modes
- History and memory
- Tone controls or prompt-tuning UI for end users
- OCR, screenshot translation, or document translation
- Mobile, web, or Linux clients

## Quality Bar

- The user should be able to trigger the app from another desktop program and finish with a copy action in one short loop.
- Capture failures should degrade into a clear clipboard fallback instead of a silent failure.
- Provider failures should surface as explicit error states.
- API keys must stay in secure platform storage and must not be written to plain-text config files.

## Current Gaps

- Automated test coverage is currently centered on the Swift/XCTest suite.
- Windows validation is still mostly manual smoke testing through the launcher script.
- Feature parity should be treated as a product goal, not an assumption; both clients share the same workflow concept, but implementation details remain platform-specific.
