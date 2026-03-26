# SnapLingo Test Plan

Generated from the accepted v1 implementation plan.

## Fixed host matrix

- `TextEdit` or `Notes` — native macOS text controls
- `Safari` or `Chrome` — browser text areas
- `VS Code` — Electron editor
- `Slack` — real-world desktop messaging flow

## Critical system flows

- Chinese selection -> quick translation -> polished result -> copy
- English selection -> polish -> copy
- AX capture failure -> explicit clipboard fallback -> copy
- In-flight request -> second hotkey press -> old request canceled -> new request active

## State coverage

- Permission missing onboarding
- Clipboard waiting card
- Partial state with `Quick Translation`
- Final state with `Polished Version`
- Provider fail-closed error state
- Copy success feedback and auto-dismiss

## Edge cases

- Mixed-language text defaults to `Translate`
- Empty clipboard after fallback prompt
- Unsupported hotkey combinations rejected in settings
- Very long original text still keeps result as primary visual anchor
