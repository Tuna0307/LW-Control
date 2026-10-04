# Shared profile sidebar recovery and local component — 2026-10-04

Lead-assigned isolated work. Product file: `src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx`.
Original source: recovered `index-BVfnK1wp.js`, SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
The checker extracts and executes the actual original helpers, icon renderer,
dialog and whole `Yr` sidebar. `results.json` records exact UTF-8 offsets, lengths
and hashes and the submitted component SHA. No original service is invoked.

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/profiles/check-profiles.mjs`
runs read-only. `--record` writes only this packet's results. Requirements: existing
product React/esbuild and isolated jsdom 27 from the shell harness dependency directory;
an alternative jsdom package path can be supplied through `LWB317_JSDOM_PACKAGE`.

Current result: **98 case groups pass**, including 97 original/current case groups
(one of these contains 32 helper comparisons), plus the intentional unavailable-provider
fence. Test scenario counts are not UI completion percentages or total product coverage.
Two locales, nine connection states, compact/expanded and busy predicates have 72
actual original/current normalized DOM comparisons. Only the excluded Upgrade button
and an irrelevant SVG focusable attribute are removed before comparison. Icon paths,
labels, classes, control order and disabled predicates remain in the comparison.

## Finite branch inventory

All locators below are UTF-8 byte offsets in the exact main bundle. These are
`EXACT_BYTES` / `EXACT_CONTRACT`; local execution verifies this isolated component,
not native/profile-service persistence or original rendered pixels.

| Branch group | Original locator | Current implementation / proof |
|---|---|---|
| Optional profile column | `se` 193993, `Fn` 208687, parent `rt` 363306 | Root integration owns clone-local capacity; component does not read entitlement |
| Name/server/note fallbacks | `ce` 194447 | `profileDisplay`; exact helper comparisons and both-locale DOM |
| Locked/connection-state label and dot | `Jr` 338919, `Yr` 339285 | Supplied local runtime states; nine source states compared |
| Default collapsed list | `Yr` 339285, local `T=true` | Initial compact DOM compared |
| Compact selection and focus-game flag | `Yr` compact `r.select(t.id,e)` | Actual source/current callbacks compared |
| Expanded heading and local capacity/Add limit | `Yr` `te=D.length>=E.maxProfiles` | Exact structural/disabled checks; root capacity replaces commercial entitlement |
| Forced expansion on provider/launch/action/restart error | `Yr` `ae=T&&!r.error&&!ie&&!f&&!m` | External, ordered launch and action-error comparisons |
| Expanded selected/enabled/busy row | `Yr` profile map | All actual DOM comparisons |
| Reordering before target / invalid/no-op ids | `le` 194621, `Yr` drag callbacks | 16 helper pairs plus actual source/current rendered drag handlers |
| Note opening/raw text/maxLength/autofocus | `Yr` final `g&&In` expression | Original/current modal and submit comparison |
| Note deferred acknowledgement, success close, failure retain | `Yr` updateNote promise chain | Inert deferred real mounted callbacks with busy changes |
| Note busy Cancel gate | `In` 209017 + `Yr` note actions | Actual handlers compared under deferred busy |
| Shared modal Tab loop and conditional previous-focus lifetime | `In` 209017 | Actual source/current controls and focus states compared with jsdom visibility shim |
| Delete confirmation/cancel/error | `de` 194896 + `Yr` delete callback | Source/current true/false and synchronous-provider-failure cases |
| Per-profile start vs stop/failed-instance handling | `Yr`, `ue` 194751 | Inert source/current acknowledgements; production missing providers disabled |
| Batch start/stop eligibility and source order | `ue` 194751 + `Yr.oe` | Helpers and real actual source/current callback trace |
| Restart-required action message/button | `Yr` `LAUNCH_TICKET_RESTART_REQUIRED` | Inert failure and retry presentation/callback comparison |
| Instance poll initial/3000ms/overlap/failure | `Yr` opening effect | Both actual mounts, deferred replies and explicit timer callbacks |
| Poll disposal and obsolete reply fence | `Yr` opening cleanup | Source/current unmount-before-reply proof |
| Collapse action and aria/title | `Yr` final `profile-collapse` | Actual en/ja compact/expanded DOM/click comparison |

Commercial Upgrade/account-opening, subscriptions, entitlement expiry/downgrade,
activation and original authentication remain **OUT_OF_SCOPE**. They are not source
gaps to implement. Displaying supplied `locked`/`grace` connection states does not
assert a valid original entitlement or contact its service.

## Root integration boundary

Inputs are source-shaped `state.profiles`, `state.selectedProfileId`, `state.maxProfiles`,
`busy`, `error`, supplied instances/launch errors and optional providers. Original
profile entries consume `id`, `roleName`/`displayName`, `serverId`, `note`, `enabled`
and `lockedReason`; the sidebar never requires or invents `isPrimary`.

The production bootstrap currently supplies a single immutable native profile and
does not expose verified multi-profile CRUD/start/stop producers. Root may show the
sidebar for **clone-owned local profile capacity >1** and use explicit browser-preview
profile state/actions. Missing callbacks fence selection/create/delete/reorder/note/
game actions before dispatch. Start/Stop/Restart remain unavailable unless a real
separately assigned provider is passed; the isolated tests use controlled inert
bindings and are not product success mocks. Preview selected IDs must not become
native invocation profile IDs. Root owns that boundary and all App integration.

The source's profile bootstrap/cache/switch-loading flow (`Gi` around byte 368800,
`profile-switch-loading` 373064) is a separate parent contract. Sidebar reproduction
alone does not recover that producer or establish native cross-profile persistence.

## Proof limits

No browser session, original post-auth pixel comparison, physical HTML5 drag,
native confirmation, game launch/stop, original service or owner-session action
was performed by this isolated unit. `showModal`/`close` and visible client rects are
controlled jsdom shims. Focus comparison preserves what the original actually
does; it does not claim universal return to the triggering button. The test host
blurs/detaches its container before disposal after interaction observations.

No App, bridge, CSS, master status or protected WIP was edited by this subtask.
Lead must review/integrate the component and record browser/whole-shell evidence.
