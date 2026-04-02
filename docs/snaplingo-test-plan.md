# SnapLingo Test Plan

Updated: 2026-04-02

## Test Strategy

- Automated coverage now lives in both `Tests/SnapLingoTests` and `SnapLingoWindows.Tests`.
- Swift/XCTest still covers the macOS-side shared logic, while `SnapLingoWindows.Tests` covers Windows workflow and manifest regressions through a lightweight executable test harness.
- Windows shell behavior still requires manual smoke testing through `.\run-windows-client.ps1`; there is not yet a full WinUI UI-automation suite.
- Cross-platform validation should focus on workflow parity, not identical implementation details, because the Windows shell now includes extra configuration and launcher surfaces.

## Automated Coverage

Run:

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift test
```

Current automated checks cover:

- provider kind to credential key mapping
- default mode detection for Chinese and English input
- partial translation state unlocking copy
- validation failures for empty or unchanged provider output
- curated provider preset protocol selection
- curated domestic provider base URLs and models
- hotkey preset stability
- shared provider manifest defaults for Windows
- Windows localization key parity between English and Simplified Chinese
- Windows retry flow after clipboard fallback
- Windows in-flight cancellation when a new selection arrives
- Windows duplicate-selection suppression for auto-selection activation

Run the Windows regression project with:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet run --project .\SnapLingoWindows.Tests\SnapLingoWindows.Tests.csproj
```

## Manual Host Matrix

### macOS

- `TextEdit` or `Notes` for native text controls
- `Safari` or `Chrome` for browser text areas
- `VS Code` for Electron/editor behavior

### Windows

- `Notepad` for native text controls
- `Edge` or `Chrome` for browser text areas
- `VS Code` for Electron/editor behavior
- one additional Win32 or Office-style app when available to verify UI Automation fallback behavior
- verify the lightweight selection launcher appears near drag-selected text when direct capture succeeds

## Critical Workflow Checks

- Chinese selection -> quick translation -> polished result -> copy
- English selection -> polish -> copy
- direct capture success path
- direct capture failure -> explicit clipboard fallback -> copy
- drag selection on Windows -> launcher appears -> translation panel opens -> copy
- in-flight request -> second hotkey press -> old request canceled -> new request active
- retry from an error or stale result state
- manual mode switch after capture
- use current clipboard action when clipboard already contains text

## State Coverage

- macOS permission missing onboarding
- clipboard waiting state
- partial state with `Quick Translation`
- final state with polished output
- provider fail-closed error state
- copy success feedback and auto-dismiss behavior
- Windows launcher dismissed state and duplicate-selection suppression window

## Provider Coverage

At minimum, smoke test one provider from each protocol family:

- `OpenAI` for Responses API
- `Anthropic` for Messages API
- `Gemini` for `generateContent`
- one OpenAI-compatible preset such as `Zhipu GLM` or `Alibaba Bailian`

## Edge Cases

- mixed-language text defaults to `Translate`
- empty clipboard after fallback prompt
- unsupported or conflicting hotkey registration
- very long original text still keeps the result as the primary visual anchor
- invalid, missing, or corrupted stored secrets fail safely
- provider request canceled while a previous response is still in flight
- Windows language switch updates labels without restart
- Windows prompt profile create/save/delete flow persists safely

## Release Smoke Checklist

- launch the macOS client with `swift run`
- run the Windows regression project with `dotnet run --project .\SnapLingoWindows.Tests\SnapLingoWindows.Tests.csproj`
- launch the Windows client with `.\run-windows-client.ps1`
- save and reload API keys for at least one provider on each platform
- verify copy action from both `Translate` and `Polish`
- verify at least one clipboard fallback flow on each platform
- on Windows, verify prompt profile persistence and interface language switching
- on Windows, verify the selection launcher and standalone panel can reopen repeatedly without becoming stuck
