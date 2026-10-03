# Cross-server popover — worker delivery, 2026-10-04

Status: **AWAITING_REVIEW** for `LWB317-UI-SHELL-CROSSSERVER-001`.
Project-lead acceptance remains pending. Global UI/native/original-pixel acceptance is unchanged.

## Recovered authority and implementation

The target EXE is `lwbridge-0.3.17.exe` at SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The exact original shell asset is `frontend-package/web/assets/index-BVfnK1wp.js`
at SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

`recovery/source-locators.json` pins the exact recovered source byte ranges. The
original popover function begins at UTF-8 byte 350656 (3155 bytes,
SHA-256 `04B9D1EE…FD05`); its legacy-history-helper block begins at 350405.
The parent server-jump callback begins at 365565 (229 bytes,
SHA-256 `653AEDF4…6284`). Exact `server_jump`, history set/import and `map_summary`
API locators are included alongside the header `onJumpServer:Mt` binding.

Milestone A saved the immutable Git blob
`55c9ca118d31a9fcdbc0ec0cf86930474303930e:src/LWBridge.UI-0.3.17/src/App.jsx`.
Its raw SHA-256 is `F201E28E…6CDF`, and before correction its normalized contents
equaled the dispatch App. `recovery/run-differential.mjs` executes the exact
original popover/parent bytes with inert bindings and mounts that baseline/current
App. It records nine passing original cases and twelve demonstrated current
mismatches. `recovery/branch-matrix.md` is the source-backed branch inventory.

Canonical `src/LWBridge.UI-0.3.17/src/App.jsx` now restores only those demonstrated
contracts: document pointer-down click-away; profile-keyed history/error reset and
legacy import; event-time localized validation/action errors; offline/scan Enter
fencing; summary-before-history/close acknowledgement; swallowed summary-refresh
failure at the parent jump boundary; and removal of the extra action-path status
refresh. No Escape shortcut, autofocus, focus trap, invented message, limit,
timing or persistence was added because the recovered source has none.

Checkpoint A is `a74aba9b3a5abaf7a12979c7938e8bcc2ca465c7` and checkpoint B is
`5318467c43598993b8c9cad499643e037fbc7dde`; both were pushed and their direct
remote branch SHA was verified when delivered.

## Executed proof

`milestone-b/run-focused.mjs` mounts the actual corrected App body with controlled
inert bridge/map APIs and passes 12 cases: history normalization/list presentation,
trigger/outside/Escape, locale validation, offline, active scan, profile reset/import,
changed and unchanged deferred summary, summary failure, jump failure, history-write
failure and busy-trigger behavior. Successful/deferred/error action proof exists here;
no browser or native game action is spoofed as a real server jump.

`milestone-c/run-regressions.mjs` passes three affected boundaries without rewriting
historical evidence: accepted `RetainedPages` Activity/profile bytes remain equal to
dispatch, current parent Map summary/status/scan ownership still observes cadence/
offline/reconnect/completion/cleanup rules, and Home auto-reconnect still acknowledges
only after the controlled real `backendBridge` WebView response.

`milestone-c/run-current-checks.mjs` records seven current checks in
`current-checks.json`: focused mount, affected regressions, canonical `npm run check`,
build, production-package check, seven-file protected-WIP guard and repository
`git diff --check`. The Vite large-chunk advisory is non-failing.

## Real browser QA

`milestone-c/browser/observations.json` records true offline Preview QA at
`?previewPage=overview` with no `previewLanguage` query parameter. EN/light showed
the open popover with one `Game disconnected` message. Typing `0` and pressing Enter
while offline added no validation/action duplicate; Escape left the popover open.
Clicking Map Data closed it through outside pointer-down; returning Home and reopening
preserved the unsent `0`. Interactive locale/theme changes produced the JA/dark open
state with one `ゲーム未接続` message.

The main CSS viewport was measured at 1920x855. The browser connector exported
1600x713 JPEG captures at scale 0.833333; those image dimensions are kept separate
from CSS viewport size. One separate narrow window measured exactly 800x543 CSS px
(visual viewport also 800x543, DPR 1; outer bounds 816x673). Its popover rect was
390..680 x 59..178, fully inside the measured viewport. Its capture is a native
800x543 JPEG. All three saved JPEGs were visually inspected and have verified
FF-D8-FF signatures/hashes. Fresh consoles in both windows contain only Vite debug
connection lines and the React DevTools information notice: zero warnings/errors.

## Reproduction and limits

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-b/run-focused.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/run-regressions.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/run-current-checks.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/validate-current.mjs
```

No Last War process, real server jump, real map scan, updater, diagnostics export,
native provider or original protected service was launched or controlled. Positive,
unchanged, deferred and failure branches use actual production handlers/mounts with
controlled inert responses. Direct original post-auth geometry/pixels remain
unavailable behind the original auth boundary; the task establishes exact recovered
source/local behavior, not whole-product pixel/native acceptance. Existing accepted
shell retention, Home/Map ownership and all seven protected WIP hashes are preserved.
