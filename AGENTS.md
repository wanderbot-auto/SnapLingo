# Repository Guidelines

## Project Structure & Module Organization

`SnapLingo` is a Swift Package Manager macOS menu bar app. Core app code lives in [Sources/SnapLingo](/Users/wander/apps/SnapLingo/Sources/SnapLingo): UI in `Views.swift`, app/session state in `AppModel.swift`, `PanelStateStore.swift`, and `SettingsStore.swift`, workflow coordination in `WorkflowOrchestrator.swift`, and platform bridges such as `HotkeyManager.swift`, `SelectionCapture.swift`, `FloatingPanelController.swift`, and `CredentialStore.swift`. Provider integration is split between `ProviderRegistry.swift` and `ProviderClient.swift`. Tests live in [Tests/SnapLingoTests](/Users/wander/apps/SnapLingo/Tests/SnapLingoTests). Product and QA notes live in [docs](/Users/wander/apps/SnapLingo/docs).

## Build, Test, and Development Commands

Create local cache directories before running SwiftPM:

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift build
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift test
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift run
```

`swift build` compiles the app, `swift test` runs the XCTest suite, and `swift run` launches the menu bar app for smoke testing.

## Coding Style & Naming Conventions

Follow existing Swift style: 4-space indentation, one top-level type per concern, and small focused files where practical. Use `UpperCamelCase` for types, `lowerCamelCase` for properties and methods, and expressive enum case names such as `waitingForClipboard` or `anthropicMessages`. Prefer explicit state transitions over implicit side effects. Keep adapter logic thin and provider-specific behavior isolated in provider clients or presets.

## Testing Guidelines

Tests use `XCTest`. Add unit tests in `SnapLingoLogicTests.swift` or a new `*Tests.swift` file when a module grows. Name tests `test<Behavior>` and cover state transitions, provider preset mapping, and failure handling. Run `swift test` before opening a PR.

## Commit & Pull Request Guidelines

History is minimal (`first commit`), so use short imperative commit messages such as `Add Gemini native provider client` or `Fix settings main-actor crash`. Keep commits scoped to one concern. PRs should include: what changed, why it changed, test coverage, and screenshots or short notes for UI-visible changes.

At the end of each nightly or stage-based development session, run `git commit` before stopping work so partial progress is checkpointed in version control.

## Code searching

You run in an environment where `ast-grep` is available; whenever a search requires syntax-aware or structural matching, default to `ast-grep --lang <language> -p '<pattern>'` (or set `--lang` appropriately) and avoid falling back to text-only tools like `rg` or `grep` unless I explicitly request a plain-text search.

## Security & Configuration Tips

Do not store API keys in files. Use the Keychain-backed `CredentialStore` only. Keep machine-specific paths out of committed docs; use repo-relative examples like `$PWD/.codex-home`.
