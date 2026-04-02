# SnapLingo

SnapLingo is a desktop translation utility for fast selection-to-copy workflows. This repository currently contains two desktop clients:

- `Sources/SnapLingo`: a macOS menu bar app built with SwiftPM and SwiftUI
- `SnapLingoWindows`: a Windows desktop client built with WinUI 3 and .NET

## Current Scope

- Global hotkey entry point on both desktop clients
- Direct selection capture with explicit clipboard fallback
- `Translate` and `Polish` modes with automatic default mode detection
- Progressive reveal for Chinese input: quick translation first, polished result second
- Copy-first workflow with retry support
- Windows standalone translation panel plus a lightweight selection launcher for drag-selected text
- Windows settings shell for provider, model, hotkey, prompt profile, and language selection
- Multi-provider adapter support for `OpenAI`, `Anthropic`, `Google Gemini`, `Zhipu GLM`, `Kimi`, `MiniMax`, `Alibaba Bailian`, and `Volcengine Ark`
- Secure per-provider API key storage on each platform
- Shared provider defaults and localized strings bundled in repo resources

## Current Non-Goals

- Replace selection
- Undo
- Context-aware modes
- History and memory
- Cloud sync for settings or secrets
- Rich tone-control workspaces or prompt marketplaces
- Mobile, web, or Linux clients

## Clipboard Fallback Policy

- Clipboard fallback must stay explicit and user-initiated.
- Do not add an automatic simulated `Ctrl+C` fallback on Windows.
- Automatic `Ctrl+C` has severe bugs in real host apps, including unintended copy side effects and unreliable selection behavior.
- If direct capture fails, SnapLingo should show a clear fallback prompt or let the user choose an existing clipboard action instead of synthesizing copy input on the user's behalf.

## Repository Layout

- `Sources/SnapLingo`: macOS app source
- `Tests/SnapLingoTests`: Swift/XCTest coverage for shared macOS logic and provider presets
- `SnapLingoWindows`: WinUI 3 Windows client
- `SnapLingoWindows.Tests`: lightweight Windows regression test harness for workflow, localization, and provider manifest behavior
- `docs`: product notes, QA plans, and UI exploration artifacts

## macOS Build

Create local cache directories before running SwiftPM:

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" \
  CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" \
  swift build
```

## macOS Test

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" \
  CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" \
  swift test
```

## macOS Run

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" \
  CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" \
  swift run
```

## Windows Client

For the WinUI client under `SnapLingoWindows`, use the launcher script at the repo root:

```powershell
.\run-windows-client.ps1
```

If the app is already running, the launcher stops the current instance before rebuilding and starting the updated executable.

For auto-rebuild during active development, run watch mode once and leave it open:

```powershell
.\run-windows-client.ps1 -Watch
```

Or double-click:

```text
run-windows-client-watch.cmd
```

Watch mode keeps listening under `SnapLingoWindows`, then automatically rebuilds and relaunches the app after code changes.

Or double-click:

```text
run-windows-client.cmd
```

Useful options:

- `.\run-windows-client.ps1 -Configuration Release`
- `.\run-windows-client.ps1 -Platform ARM64`
- `.\run-windows-client.ps1 -NoBuild`
- `.\run-windows-client.ps1 -RequireBuild`
- `.\run-windows-client.ps1 -Watch`
- `.\run-windows-client.ps1 -Watch -NoBuild`

The current Windows shell includes:

- a settings window for provider, model, hotkey, prompt profile, and interface language
- a standalone translation panel for the active workflow
- a lightweight launcher that can appear near detected text selection before opening the panel

## Windows Regression Test

Run the lightweight Windows regression project from the repo root:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet run --project .\SnapLingoWindows.Tests\SnapLingoWindows.Tests.csproj
```

This covers localization key parity, shared provider manifest defaults, workflow retry and cancellation behavior, and duplicate-selection suppression.

## Provider Notes

- `OpenAI` uses the Responses API.
- `Anthropic` uses the native Messages API.
- `Gemini` uses the native `generateContent` API.
- `Zhipu GLM`, `Kimi`, `MiniMax`, `Alibaba Bailian`, and `Volcengine Ark` use curated OpenAI-compatible chat presets.
- Provider defaults are loaded from shared bundled manifests so macOS and Windows stay aligned on base URLs and preset models.
- API keys are stored in platform-native secure storage:
  macOS uses Keychain via `CredentialStore`, and Windows uses DPAPI-encrypted files via `SecureSecretStore`.
