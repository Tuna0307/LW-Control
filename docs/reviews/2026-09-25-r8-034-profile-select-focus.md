# R8-034 — restore profile selection and best-effort game-window focus

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the host-local `profile_select` registry mutation plus the original optional/best-effort focus behavior without adding launcher ownership or synthetic instance state.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- public `profile_select` handler at VA `0x14016D01A`;
- registry selection helper at VA `0x1403DA8A1`;
- shared profile-ID validator at VA `0x1403DC23E`;
- `focusGame` parser at VA `0x1403ABF8E`;
- focus/path precheck helper at VA `0x14033C3A7`;
- visible-window focus helper at VA `0x140337A21`;
- EnumWindows callback at VA `0x14033C8DF`;
- retained frontend API wrapper `profile_select({profileId, focusGame})`, whose second argument defaults true.

No frontend asset changed.
## Registry selection contract

The public command requires a string `profileId`, then applies the shared native profile-ID rules.

The registry helper queries:

```sql
SELECT enabled = 1 AND locked_reason IS NULL
FROM profiles
WHERE id = ?
```

Observable outcomes:

- missing row -> `PROFILE_NOT_FOUND`;
- existing row that is disabled or has a non-null `locked_reason` -> `PROFILE_LOCKED`;
- selectable row -> update only `controller_state.selected_profile_id`.

The recovered update is equivalent to:

```sql
UPDATE controller_state
SET value = ?
WHERE key = 'selected_profile_id'
```

Selecting a profile does not modify that profile's `updated_at` or other registry metadata.

Success returns the refreshed normal profile-list state.
## focusGame semantics

The native payload parser treats `focusGame` as optional:

- boolean true -> focus attempt;
- boolean false -> no focus attempt;
- missing or non-boolean -> defaults to true.

The retained frontend wrapper also defaults the argument to true, while the Settings option "Focus the corresponding game when selecting an account" passes the user's stored preference into profile selection.

Focus is best effort. A failed or unavailable focus operation does not fail the already-completed profile selection.
## Native focus sequence

When focus is requested, native 0.3.1 looks up the running instance for the selected profile. Before focusing, it verifies the process belongs to the selected game root's `Game\\LastWar.exe`.

For the validated PID it then:

1. calls `EnumWindows`;
2. selects a top-level window whose PID matches and for which `IsWindowVisible` is true;
3. calls `ShowWindow(hwnd, 9)`;
4. calls `SetForegroundWindow(hwnd)`.

If no matching running instance or visible window exists, selection still succeeds.

R8-034 reuses the rebuild's existing `GameInstallationService` process/path evidence. It does not create a second launcher or PID registry.
## Implementation

R8-034 adds:

- `ProfileRegistryStore.SelectProfile`;
- `profile_select` routing in `ProfileRegistryCommandService`;
- native-compatible `focusGame` defaulting;
- `ProfileWindowFocusService` using the recovered Win32 sequence;
- production wiring through the existing validated game-root/process service;
- global-command admission for `profile_select`.

The production focus helper is deliberately best effort and swallows focus-side failures after registry selection, matching the recovered command behavior.
## Regression coverage

The deterministic checks prove:

- selection persists `selected_profile_id`;
- selection does not alter `updated_at`;
- `focusGame=false` suppresses focus;
- missing `focusGame` defaults true;
- wrong-type `focusGame` also defaults true;
- `focusGame=true` invokes the focus callback;
- focus callback failure does not fail selection;
- disabled and locked profiles return `PROFILE_LOCKED`;
- a missing profile returns `PROFILE_NOT_FOUND`;
- an invalid ID returns `INVALID_PROFILE_ID`;
- missing `profileId` returns `INVALID_REQUEST`;
- backend routing reaches `profile_select`.

Tests inject a focus callback, so deterministic checks never steal desktop focus.
## Validation

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.

## Still intentionally missing

R8-034 does not restore `profile_enable_set`, `profile_create`, `profile_delete`, launcher ownership, multi-instance ownership, or instance start/stop lifecycle.

During target selection, `chat_automation_configure` and `chat_automation_run_pending` were re-audited and confirmed provider-backed through `configureChatAutomation` / `runChatAutomationPending`, each with a 5,000 ms timeout; no fake local implementation was added.
