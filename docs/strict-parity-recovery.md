# LWBridge 0.3.17 parity directive

**Effective:** 2026-09-29

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Goal

Reproduce the observable product behavior of LWBridge 0.3.17 one-for-one, then make that recovered behavior work against the current Last War client.

## Authority order

1. verified 0.3.17 reference bytes/runtime observations;
2. exact assets extracted from that reference;
3. current official Last War artifacts for compatibility mapping;
4. 0.3.1 findings as hypotheses/history only;
5. implementation choices only when explicitly labeled and not confused with recovered behavior.

## Rules

- Do not redesign before recovering.
- Do not invent missing values.
- Do not silently substitute a 0.3.1 behavior for 0.3.17.
- Do not call a feature parity-complete from UI appearance alone.
- Preserve exact recovered bytes unchanged where practical.
- Record unknowns explicitly.
- Separate static recovery from live proof.
- Keep user-visible behavior as the parity target even when current-client compatibility requires different internals.

## Required trace for a function

Where evidence permits, recover:

`UI -> frontend/API -> host command -> provider/runtime -> state/storage -> visible result`

If the trace stops, document exactly where and why.

## Current order

UI parity comes first. Function recovery begins after the 0.3.17 UI baseline is established.
