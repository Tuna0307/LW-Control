# LWB317-UI-MAP-NAVIGATION-001

Owner: returning worker through manual owner relay. Status: CHANGES_REQUIRED.
Lead PM-025 reviewed delivery 305240e on 2026-10-02. Useful navigation correction
is retained; pending search success/rejection must be ignored after backend
unavailability/effect disposal. Independent lead evidence preserves two failures.
Correction is assigned as milestone A of LWB317-UI-MAP-INTERACTIONS-001.

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

## Worker delivery — 2026-10-02

Implemented the recovered component-local normal-tab view cache in
MapDataPage.jsx. Normal tabs now store and restore page/rows/total, a same-tab
click is a no-op, uncached entry starts empty at page 1, cached entry restores
the prior view and still refreshes, and Scheduled Plunder acts only as a
navigation boundary.

Search callbacks now use a generation fence matching the recovered rr behavior:
obsolete success, failure and finally paths cannot replace another tab's rows,
error state or loading state. The current request still reports failures and
page-count shrink still clamps/requeries. Server changes and server loss
invalidate the mounted cache; no browser persistence was added.

Exact recovered UTF-8 byte anchors are recorded under the work-item evidence:
ye 554, server transition 33502, normal-query effect 34677, rr 38513 and ir
39238. The immutable pre-edit MapDataPage baseline reproduces six distinguishing
navigation failures; the corrected component passes the same persistent
hook/deferred-promise campaign with zero failures.

Browser preview at the required map-truck URL confirms server 321, Truck total
55 and populated rows. The observed flow reaches Truck page 2 / Truck Owner 51,
enters Train at page 1, returns to Truck page 2, and crosses Scheduled Plunder
without normal pagination before returning to Truck page 2. Captured console
errors are zero. Loading and deterministic query-error screenshots are also
recorded. Preview remains offline and no native/gameplay action ran.

Focused accepted filter, Treasure Checking and row-action regressions pass.
Canonical npm check/build/production-package verification passes. Historical
LEAD-TABLES/transport executable scripts still contain their pre-FILTERS
changeItemFilter harness and fail on that stale harness; historical manifests
were preserved. The FILTERS evidence validator likewise pins the prior
MapDataPage hash by design. This work item provides its own passing validator.

Remaining outside this assignment: keyword/debounce recovery, exact selection
and plunder lifecycle, native Treasure context/refresh wiring, Scheduled Plunder
scheduling/claims, native images/text integration and original post-auth pixels.
