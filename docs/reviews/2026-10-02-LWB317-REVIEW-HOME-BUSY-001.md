# LWB317-REVIEW-HOME-BUSY-001 — independent Home busy presentation review

Date: 2026-10-02

Reviewed branch: `research/offline-controller`

Reviewed checkpoint: `6277295755ae37f99d5ef5cbf7a9a6650ba656ec`

Verdict: **ACCEPT** for the focused recovered-source/current-local Home busy presentation scope.

No product code was changed by this review. The existing AFK fixture, scratch work and
parent `LWB317-UI-CORRECT-003` screenshots were left untouched and unstaged.

## Source identity and independent anchors

The assigned original executable is
`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`, SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The recovered frontend source used for this review is
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

`check-review.mjs` parses that bundle independently and verifies these UTF-8 byte
anchors before executing any case:

| Source item | UTF-8 byte | Relevant recovered expression |
| --- | ---: | --- |
| `Kr` predicates | 336469 | `showRootPicker:e&&!t`, `canStart:e&&t&&!n&&!i&&!a&&!o`, `canStop:e&&t&&(n||i)&&!a&&!o`, `repairOnClose:e&&t&&n&&r&&!i` |
| `qr` Home renderer | 336694 | missing root → launching → proxy processing → recovery → repair → running online/offline → stopped; launch label prefers launching over proxy; close processing requires proxy busy plus running |
| Home caller | 373291 | call of `qr` with root/proxy/recovery/busy inputs |
| proxy busy state | 361529 | `[p,m]=(0,b.useState)(!1)` |
| folder busy state | 361581 | `[_,v]=(0,b.useState)(!1)` |
| derived launch busy | 248345 | `gameLaunchBusy:u>0` |

The review harness also verifies every source locator retained in the historical
HOME-BUSY manifest directly against the unchanged recovered bundle.

## Current presentation and predicate comparison

Current `HomePage` in `src/LWBridge.UI-0.3.17/src/Pages.jsx:123-183` preserves the
recovered precedence and keeps the busy inputs separate:

- folder selection busy is `state.busy === "gameRoot"` and affects only the root
  picker label/disabled state;
- proxy busy is `state.proxyBusy === true`;
- launch busy is `state.gameLaunchBusy === true`;
- preference busy uses the distinct `autoLaunchGame` and `autoReconnect` values in
  `state.busy` and disables only the corresponding switch;
- the header uses missing root first, then launch, proxy processing, recovery,
  repair, running online/offline and stopped, matching recovered `qr`;
- the launch button label uses launch before proxy processing; the close button
  changes to processing only when proxy busy and the game is running;
- repair replaces the launch button with the update-and-launch close action only
  when the game is running, repair is required and recovery is inactive.

The current local lifecycle fence is still `lifecycleProviderAvailable = false`.
Therefore actual current start/stop controls remain disabled for every previewed
state. For predicate comparison only, the review executes the current extracted
start/stop expressions with that single availability term set to true and confirms
the remaining root/running/recovery/proxy/launch conditions equal recovered `Kr`.
It then executes the actual false fence and verifies both lifecycle controls fail
closed. This is presentation/predicate evidence only; it does not assert a live
lifecycle provider.

The independent harness executes 17 explicit distinguishing cases against the
actual extracted original renderer/predicates and current renderer/predicates:

| Case | Expected source behavior | Current result |
| --- | --- | --- |
| unresolved root | `Checking`, no picker/game controls | match |
| invalid root idle | `Game root not detected`, enabled source picker with `Select Game Folder` | match; picker enabled when production/root selection is available |
| invalid root + folder busy | missing-root header, picker `Processing` and disabled | match |
| valid root + folder busy | `Game stopped`; `Launch Game` / `Close game`, folder busy does not relabel lifecycle controls | match; lifecycle controls fenced disabled locally |
| stopped + proxy busy | header/launch `Processing`; close remains `Close game`; source start/stop both disabled | match |
| running offline + proxy busy | header, launch and close all `Processing`; source start/stop disabled | match |
| running online + proxy busy | same processing labels; connected status styling remains tied to running+online | match |
| stopped + launch busy | header/launch `Launching game…`; close remains `Close game`; source start/stop disabled | match |
| launch + proxy busy + running | launch wins for header/launch; close is `Processing`; source start/stop disabled | match |
| running + repair | `Repair required`; only `Update and Launch`, source stop enabled, repair hint shown | match; local lifecycle fence disables action |
| running + repair + proxy busy | header/close `Processing`; repair action retained; source stop disabled | match |
| active recovery | recovery status; ordinary launch/close labels; source stop enabled | match; local lifecycle fence disables action |
| repair + active recovery | recovery status suppresses repair-on-close | match |
| running offline | `Bridge disconnected`; source stop enabled | match; local lifecycle fence disables action |
| running online | `Game running` with `status-ok`; source stop enabled | match; local lifecycle fence disables action |
| `autoLaunchGame` busy | stopped header/labels unchanged; only auto-launch preference disabled | match |
| `autoReconnect` busy | stopped header/labels unchanged; only auto-reconnect preference disabled | match |

The full input/output record is
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-BUSY-001/review-results.json`.

## App producers versus preview-only inputs

Current `App.jsx:166-211` produces `homeBusy` values `gameRoot`,
`autoLaunchGame`, `autoReconnect` and the clearing empty value. The `homeState`
mapping at `App.jsx:400-415` passes that value as `busy` together with root,
proxy status, online/recovery, preferences and errors.

`App` does **not** pass production `proxyBusy` or `gameLaunchBusy` fields into
`homeState`. Those inputs exist in `HomePage` and its explicit preview fixtures so
the recovered formatter and precedence can be checked, but the current App has no
producer for them. This is a missing lifecycle-input producer, not a formatter
defect in this focused review. Native Home lifecycle implementation remains a
separate work unit.

## Browser recheck

The task-owned preview at `127.0.0.1:4319` was used only for synthetic browser
fixtures. No native picker or gameplay control was pressed. DOM inspection covered
the assignment's bounded cases:

| Preview state | Observed header | Observed lifecycle labels | Disabled |
| --- | --- | --- | --- |
| `home-root-busy-valid` | `Game stopped` | `Launch Game` / `Close game` | both |
| `home-proxy-busy-stopped` | `Processing` | `Processing` / `Close game` | both |
| `home-proxy-busy-running` | `Processing` | `Processing` / `Processing` | both |
| `home-busy-overlap` | `Launching game…` | `Launching game…` / `Processing` | both |
| `home-launching`, Japanese | `ゲームを起動中…` | `ゲームを起動中…` / `ゲームを閉じる` | both |

The browser console error list was empty. The saved and visually inspected
`home-launching-ja.jpg` is 774×415 and has SHA-256
`1F190D6484F2777D78E93D461C70434626A8D3D2737DA0DCD68FD854F6B0101B`; it clearly
shows the Home header, both lifecycle labels and their disabled appearance. The
visible language was restored to English, the task-owned tab was closed and the
task-owned Vite preview was stopped.

## Historical HOME-BUSY evidence

The historical checker was rerun exactly as assigned, without `--record` or
`--verify-record`, and passed with `13824` render comparisons, `384` predicate
cases, `27` preference checks and `63` preview-fixture checks.

Its historical validator is intentionally no longer a current-App validator. It
stops at the saved App normalized-LF hash `136C6361B8C8871ECB33ADFB249A6334BD874F94C604E63C85EABC8C0D46CBE7`
versus current accepted App hash
`BA4A300D525A271C61A8061406CA81DE2414AACC15DF10A728B0E1A8641B2701`. That hash
changed in the separately accepted root-acknowledgement correction. I did not
rewrite the historical report. The new review harness separately validates its
unchanged recovered-source locators and both historical screenshot hashes against
the saved manifest.

## Verification

All required current checks passed:

| Check | Result |
| --- | --- |
| independent review harness `--verify-record` | PASS — 17 distinguishing cases, 6 assigned anchors |
| historical `check-home-busy.mjs` live rerun | PASS |
| historical recovered-source/image manifest checks | PASS in new review harness |
| browser preview + saved screenshot + console | PASS |
| R1 root acknowledgement checker `--verify-record` | PASS — 14 scenarios |
| R1 root acknowledgement validator | PASS |
| Home translation checker `--verify-record` | PASS — 60 synthetic, 4230 locale checks |
| Home translation validator | PASS |
| switch localization checker `--verify-record` | PASS — 9 languages, 360 render comparisons |
| switch localization validator | PASS |
| `npm.cmd run check` | PASS |
| `npm.cmd run check:production-build` | PASS — `1280d8a4…`, `b2a90317…` |
| new review `validate-evidence.mjs` | PASS |
| `git diff --check` on owned review/evidence paths | PASS |

Reproduce the focused review evidence from repository root with:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-BUSY-001/check-review.mjs --verify-record
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-BUSY-001/validate-evidence.mjs
```

The ACCEPT verdict is limited to recovered-source/current-local Home busy-state
presentation and predicates. It does not claim native lifecycle reachability,
original protected runtime behavior or original pixel parity. The absent production
`proxyBusy`/`gameLaunchBusy` producers remain explicitly outside this formatter
acceptance.
