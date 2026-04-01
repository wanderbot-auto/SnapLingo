# SnapLingo Test Plan

Updated: 2026-04-01

## Test Strategy

- Automated coverage currently lives in `Tests/SnapLingoTests` and focuses on Swift logic, mode detection, panel state transitions, provider preset mapping, and validation behavior.
- Windows changes currently require manual smoke testing through `.\run-windows-client.ps1` because there is no parallel WinUI test suite in the repo yet.
- Cross-platform validation should focus on workflow parity, not identical implementation details.

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

## Critical Workflow Checks

- Chinese selection -> quick translation -> polished result -> copy
- English selection -> polish -> copy
- direct capture success path
- direct capture failure -> explicit clipboard fallback -> copy
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

## Release Smoke Checklist

- launch the macOS client with `swift run`
- launch the Windows client with `.\run-windows-client.ps1`
- save and reload API keys for at least one provider on each platform
- verify copy action from both `Translate` and `Polish`
- verify at least one clipboard fallback flow on each platform
