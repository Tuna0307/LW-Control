# LWB317-UI-MAP-NAVIGATION-001

Owner: returning worker through manual owner relay. Status: ASSIGNED; prompt
prepared, worker execution not yet confirmed. Assigned by project lead 2026-10-02.

## Goal

Correct Map tab navigation and page/row restoration against recovered LWBridge
0.3.17. This is one medium UI implementation assignment, not another review-only
campaign and not native/function integration.

## Inputs and baseline

- Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
- Branch: `research/offline-controller`.
- Product baseline: `f21b4acc74dc51a44d2734d86022de5b2b843f7d`; the assignment
  documentation commit may be newer. Inspect HEAD and incoming changes before work.
- Read `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`, this work item,
  `docs/lwbridge-ui.md`, current Map masters and the latest handoff first.
- Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
  SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Original asset: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`.
  SHA-256: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- Canonical implementation: `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`.

Lead-verified starting locators in the original asset, all UTF-8 bytes:
cache helper `ye` 554; data-server transition effect 33502; tab selection effect
body 34142; search `rr` 38513; tab handler `ir` 39238. Revalidate and recover
surrounding state/effects rather than treating these excerpts as the full contract.

## Observed defect and allowed scope

Current MapDataPage resets page/rows/total on every `[tab, dataServerId]` change.
Original `ir` stores the outgoing normal tab's `{page, rows, total}` in a
component-local Map and restores the incoming tab's cached view, or starts an
uncached tab at page 1 with empty rows. Clicking the current tab is a no-op.
The query effect still refreshes after switching: caching is not a substitute
for querying and must not mask real search failure.

Implement and validate:

1. Per-kind page/row/total restoration across normal Map tabs, with exact
   source-derived cached/uncached loading transitions and current-tab no-op.
2. Search generation/cancellation behavior across rapid switching, pagination,
   delayed success, delayed failure, and page-count clamping. An obsolete reply
   must not replace the active view, clear its loading, or contaminate its cache.
3. Source-derived data-server transition invalidation, including positive to
   another positive server and loss of the data server. Trace the actual current
   producer; test with controlled local inputs, without starting a scan.
4. Navigation into/out of Scheduled Plunder only: preserve normal-tab cache,
   loading and search boundaries. Do not build or execute scheduling operations.

Cache lifetime is the mounted Map component; do not add browser persistence.
Trace navigation-only input reset behavior if relevant, but do not expand this
assignment into keyword/debounce recovery. Selection lifecycle is a separate
scope: record source/current differences, preserve existing action fences and
avoid claiming full selection parity. Do not broaden into selection/plunder UI.

## Intentional behavior and exclusions

Preserve accepted FILTERS-001 per-kind filters, exact Secret Task level, applicable
query fields, active-item sort clearing, existing table formatting/row identity,
locale catalogs, native availability and summary/status generation fencing.
Preserve strictly isolated preview Treasure Checking and existing known/blocking
precedence. See PM-024 acceptance and the accepted filter work item.

No login/licensing UI, authentication bypass, Last War launch/control, native
scan/clear/jump/mark/export/claim/plunder execution, native Treasure connection,
protocol recovery, broader Automation/AFK work, redesign or legacy fallback.
Delayed preview data may be added only to an explicit browser-preview fixture;
keep `online:false` and native/native-unavailable modes fenced.

Leave these unrelated changes byte-for-byte unchanged and unstaged:
`src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`.
Pin their baseline hashes before editing; existing protected-WIP manifest is
under `LWB317-UI-LEAD-TABLES-001/protected-wip.json` in the UI evidence tree.

## Acceptance and required outputs

- Exact reference/asset identity and source locators, immutable baseline,
  distinguishing baseline failures, and one canonical production correction.
- Tests exercising actual production callbacks AND effects with persistent
  state, render/effect ordering and deferred promises. Pure helper tests or
  the old filter harness with unrelated effects suppressed are insufficient.
- Cover Truck page 2 -> Train -> Truck; independent pages on two tabs; uncached
  entry; cached entry while refreshing; same-tab click; rapid A/B/A switches;
  obsolete success/rejection/finally; current failure; shrunken page count;
  server switch/loss; and Scheduled Plunder entry/exit without a normal search
  while scheduled. Preserve accepted per-kind filter and Checking isolation cases.
- Real browser preview navigation and page restoration, plus loading/error
  evidence where the fixture can safely exercise it. Verify a populated fixture
  first (`data-preview-fixture`, server 321 and actual rows). Use
  `?previewPage=map-data&previewState=map-truck&previewLanguage=en&previewTheme=light`;
  generic `page`/`state` URL parameters are incorrect. Inspect saved screenshots
  and capture console errors. Distinguish browser observations from synthetic tests.
- Run `npm.cmd run check`, `npm.cmd run build`, `npm.cmd run check:production-build`
  in the canonical UI directory, focused/regression checks, evidence validation,
  protected-WIP comparison and `git diff --check`.
- Preserve historical evidence/hash manifests. New production hashes belong to
  this work item's evidence; do not rewrite old snapshots to manufacture a pass.
- Write a dated finding/review and evidence under this ID; update current UI/Map
  masters, parity matrix/ledger and handoff conservatively. No full UI/pixel/native
  acceptance claim. Use project evidence states and name unresolved dependencies.
- Review/stage only owned changes, commit, push without force, and verify the
  direct remote SHA. Return `AWAITING_REVIEW` with exact commit, checks, evidence,
  remaining limits and continuation. Project lead makes the acceptance decision.

Complete this bounded unit or document a concrete evidence/provider blocker.
There is no fixed time limit and no automatic broader follow-on assignment.
