# Repository Guidelines

## Project Structure & Module Organization

`SnapLingo` is a desktop translation project with two client implementations in the same repo.

- `Sources/SnapLingo`: Swift Package Manager macOS menu bar app
- `SnapLingoWindows`: WinUI 3 Windows desktop client
- `Tests/SnapLingoTests`: Swift/XCTest coverage for macOS logic and shared workflow rules
- `docs`: product notes, QA plans, and design artifacts

In the macOS client, UI lives in `Views.swift`, app/session state in `AppModel.swift`, `PanelStateStore.swift`, and `SettingsStore.swift`, workflow coordination in `WorkflowOrchestrator.swift`, and platform bridges such as `HotkeyManager.swift`, `SelectionCapture.swift`, `FloatingPanelController.swift`, and `CredentialStore.swift`. Provider integration is split between `ProviderRegistry.swift` and `ProviderClient.swift`.

In the Windows client, views live under `Views`, models under `Models`, state under `Stores`, and platform/services code under `Services`.

## Build, Test, and Development Commands

Create local cache directories before running SwiftPM:

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift build
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift test
env HOME="$PWD/.codex-home" CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" swift run
```

`swift build` compiles the macOS app, `swift test` runs the current automated test suite, and `swift run` launches the macOS menu bar app for smoke testing.

For the Windows client, use the launcher script from the repo root:

```powershell
.\run-windows-client.ps1
.\run-windows-client.ps1 -Watch
```

## Coding Style & Naming Conventions

Follow existing Swift and C# style: 4-space indentation, one top-level type per concern, and small focused files where practical. Use `UpperCamelCase` for types, `lowerCamelCase` for properties and methods, and expressive enum case names such as `waitingForClipboard` or `anthropicMessages`. Prefer explicit state transitions over implicit side effects. Keep adapter logic thin and provider-specific behavior isolated in provider clients or presets.

## Testing Guidelines

Tests use `XCTest`. Add unit tests in `SnapLingoLogicTests.swift` or a new `*Tests.swift` file when a module grows. Name tests `test<Behavior>` and cover state transitions, provider preset mapping, and failure handling. Run `swift test` before opening a PR.

The repo does not currently contain a parallel automated Windows test suite, so Windows changes should also receive manual smoke coverage through `.\run-windows-client.ps1`.

## Commit & Pull Request Guidelines

History is minimal (`first commit`), so use short imperative commit messages such as `Add Gemini native provider client` or `Fix settings main-actor crash`. Keep commits scoped to one concern. PRs should include: what changed, why it changed, test coverage, and screenshots or short notes for UI-visible changes.

At the end of each nightly or stage-based development session, run `git commit` before stopping work so partial progress is checkpointed in version control.

## Code Searching

You run in an environment where `ast-grep` is available; whenever a search requires syntax-aware or structural matching, default to `ast-grep --lang <language> -p '<pattern>'` (or set `--lang` appropriately) and avoid falling back to text-only tools like `rg` or `grep` unless I explicitly request a plain-text search.

## Security & Configuration Tips

Do not store API keys in files checked into the repo. On macOS, use the Keychain-backed `CredentialStore`. On Windows, use `SecureSecretStore`, which writes DPAPI-encrypted secrets under the app's local data directory. Keep machine-specific paths out of committed docs; use repo-relative examples like `$PWD/.codex-home`.
