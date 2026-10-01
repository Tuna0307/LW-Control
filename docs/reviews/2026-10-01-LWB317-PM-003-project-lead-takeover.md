# LWB317-PM-003 — project-lead takeover audit

Date: 2026-10-01 (Asia/Singapore)

## Baseline and review limits

The incoming pasted handoff ended at `13b25f6`. The actual clean checkout and
`git ls-remote origin refs/heads/research/offline-controller` both returned
`389df373ba3a25af3b61d1a3fc75f2e741eb23bc` during this audit. The intervening
checkpoints include production frontend migration (`086757e`), restart/reopen
(`5092959`), server jump/ownership (`75515a5`), categories/actions (`4ccb987`),
Auto Scan (`b5e786d`), and the worker closeout (`389df37`).

The exact reference executable was rehashed: SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
This is a source/document/evidence audit with fresh offline checks. No new
original-reference visual comparison or Last War live test was performed.
The Map Goal remains `AWAITING_REVIEW`; reading stored proof does not constitute
new live proof or full independent acceptance of every campaign checkpoint.

## Confirmed current implementation

| Area | Finding | Source / locator | Limit |
|---|---|---|---|
| Production frontend | Normal host selection requires packaged `ProductionUi` from `src/LWBridge.UI-0.3.17`; explicit legacy recovery is preserved | `src/LWBridge.Desktop/DesktopUiContentRoot.cs`, `Select`; `LWBridge.Desktop.csproj`, `BuildCanonicalProductionUi` | Normal-launch evidence is stored, not rerun here |
| Eight primary pages | Home, Automation, Map Data, Squads / AFK, City Layout, Hotkeys, Mini Games, Settings are present | `src/LWBridge.UI-0.3.17/src/routes.js`; `Pages.jsx`, `PageForRoute` | Route presence/smoke rendering does not close populated-state UX or visual parity |
| Home | Only unresolved `Checking game setup…` and two disabled switches are implemented; no launch/folder/close/repair control wiring | `src/LWBridge.UI-0.3.17/src/Pages.jsx:41`, `HomePage`; `PageForRoute` does not pass Home runtime props | `IMPLEMENTED_NOT_VALIDATED` for the limited static state; complete Home UX/function parity remains open |
| Original Home contract | Static bytes establish folder selection, launch/close, repair/restart, busy/status/recovery variants | `docs/reviews/2026-09-29-LWB317-UI-002A-home-inventory.md`; recovered `index-BVfnK1wp.js`, overview render at `0x5B222`; English locale/CSS locators in that review | Runtime defaults and original post-auth pixels remain `BLOCKED` |
| Existing lifecycle backend | Retained host has a lifecycle service; asynchronous dispatch checks it before fallback stubs | `LWBridgeBackend.cs`, `InvokeAsync`; `OverviewLifecycleService.cs`, `CanHandle` and `StartAsync` | Do not infer either missing backend or exact 0.3.17 Home parity from the fallback `profile_instance_start` error alone; revalidation is a separate future task |
| Language selector | Selection changes local React state; current page labels remain hardcoded English | `src/LWBridge.UI-0.3.17/src/App.jsx:43`, language selector; `Pages.jsx` | Language UX parity is incomplete; this audit does not repair it |
| Original visual parity | Static source/CSS recovery accepted; direct original post-auth comparison was blocked by legitimate access | `docs/reviews/2026-09-29-LWB317-UI-007-visual-comparison.md`, Remaining legitimate blocker | No claim of finished one-for-one UI/UX |

## Stored Map evidence inspected

`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/normal-launch-live-acceptance.json`
records empty application arguments, canonical project/native bridge identity,
eight active rendered routes, Resource scan/query/pagination/filter/Clear
(7994 rows, 2500/2500 read, zero failed/unread in that recorded snapshot),
clean runtime diagnostics and successful owned-session cleanup. `cleanup.json`
records restoration to the official v22 package and zero task-owned processes.
Home's recorded navigation text is still only the unresolved static state.

`LWB317-MAP-CATEGORIES-V22-001/live-categories-proof.json` records a completed
2500-block scan with zero failed blocks, City 6850, Monster 9568, Truck 61 and
Dispatch 18, and same-session health. Railway/Ghost/Treasure counts were zero.
These are stored native acquisition/query snapshots, not new direct canonical
WebView positive-row acceptance.

`LWB317-MAP-AUTO-SCAN-001/normal-ui-auto-scan.json` records a bounded
current-server Dispatch Auto cycle, 2500/2500 read and zero failed/unread,
followed by disable/Clear/healthy cleanup. Multi-server Auto is not proven by it.

The campaign closeout explicitly retains non-Resource direct table rendering,
live marks/export and restart termination/reopen validation gaps. Treasure
claim/status and Ghost preparation remain `BLOCKED`; complete Resource universe
and original private traversal equivalence remain `UNKNOWN`.

## Checkout defect and repair

Fresh `npm.cmd run check --prefix src/LWBridge.UI-0.3.17` initially failed with
`Recovered asset hash mismatch: src/reference.css` on the clean checkout.
`git ls-files --eol` showed index LF / worktree CRLF for this exact asset.
The worktree had one added CR byte (126218 versus 126217 bytes); removing only
that conversion reproduced the locked SHA-256
`3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`.

Added a `-text` attribute for the canonical `src/reference.css` and restored it
from the byte-locked recovered CSS. The expected hash/checker is unchanged.
The first standalone frontend build also found missing local Vite dependencies;
`npm ci` from the existing lockfile supplies the normal build prerequisites.

Fresh checks all passed after repair/prerequisite installation:

- canonical static checks and `LWB317_MAP_UI_INTEGRATION_CHECKS_OK`;
- standalone Map317 deterministic checks: `LWB317_MAP_CHECKS_OK`;
- frontend build: `LWB317_PRODUCTION_UI_BUILD_OK`;
- package verification: `LWB317_PRODUCTION_UI_PACKAGE_OK`;
- `git diff --check`.

Node v24.18.0 and lockfile-resolved Vite 7.3.6 were used. The built source
fingerprint is `ac2604e1e4551ce53ab5cd9f221ca6af62d8b8a430efba3b6de48ab8403b98ad`;
artifact fingerprint is `1bd92dc8adfe360b073d6045798a77f7cf2e59cdd384ba5a79a96d24dca89840`.
No Desktop or game process was launched by this audit. No full Desktop checker,
fresh live Map acceptance or original pixel comparison was run.

## Leadership decision and continuation

The user's priority is Home and Map UI/UX. Preserve the useful Map implementation
and historical evidence. Do not repeat productionization or delete old material.
Use `docs/PROJECT_STRUCTURE.md` to distinguish active code from historical inputs.

Next bounded worker: `LWB317-UI-HOME-STATES-001`, documented in
`docs/work-items/LWB317-UI-HOME-STATES-001.md`. Complete source-backed Home
rendering states with an isolated preview QA harness before opening Home native
lifecycle integration. This is UI work only; no game launch/close/repair,
auth work, new Map campaign or other backend family is authorized.

Then independently review that worker, finish the outstanding Map acceptance
review, and assign narrowly scoped Home backend contract revalidation/integration.
Direct original visual closure requires legitimate reference access. A remaining
blocker must stay visible rather than allowing all useful offline work to stall.
