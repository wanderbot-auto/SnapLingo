# SnapLingo

SnapLingo is a macOS menu bar translation utility for fast selection-to-copy workflows.

## V1 scope

- Global hotkey with `Command + Option + Space`
- Accessibility-based selection capture
- Explicit clipboard fallback when selection capture fails
- Translate or polish mode with auto-detection
- Progressive reveal for Chinese input: quick translation first, polished version second
- Copy-first workflow
- Multi-provider adapter support for `OpenAI`, `Anthropic`, `Google Gemini`, `Zhipu GLM`, `Kimi`, `MiniMax`, `Alibaba Bailian`, and `Volcengine Ark`
- Provider-specific API keys stored in macOS Keychain

## Not in v1

- Replace selection
- Undo
- Context-aware modes
- History and memory
- Tone tweak controls
- Windows support

## Build

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" \
  CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" \
  swift build
```

## Test

```bash
mkdir -p .codex-home .cache/clang/ModuleCache
env HOME="$PWD/.codex-home" \
  CLANG_MODULE_CACHE_PATH="$PWD/.cache/clang/ModuleCache" \
  swift test
```

## Windows Client

For the WinUI client under `SnapLingoWindows`, use the launcher script at the repo root:

```powershell
.\run-windows-client.ps1
```

If the app is already running, the launcher stops the current instance before rebuilding and starting the updated executable.

Or double-click:

```text
run-windows-client.cmd
```

Useful options:

- `.\run-windows-client.ps1 -Configuration Release`
- `.\run-windows-client.ps1 -Platform ARM64`
- `.\run-windows-client.ps1 -NoBuild`
- `.\run-windows-client.ps1 -RequireBuild`

## Provider Notes

- `OpenAI` uses the Responses API.
- `Anthropic` uses the native Messages API.
- `Gemini` uses the native `generateContent` API.
- `Zhipu GLM`, `Kimi`, `MiniMax`, `Alibaba Bailian`, and `Volcengine Ark` use curated OpenAI-compatible chat presets.
- API keys are stored only in macOS Keychain and are never written to plain-text config files.
