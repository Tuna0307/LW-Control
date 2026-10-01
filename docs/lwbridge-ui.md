# LWBridge 0.3.17 UI parity plan

This is the master UI workstream document.

## Goal

Copy the observable **in-scope post-auth LWBridge 0.3.17 UI** one-for-one before broad function implementation.

The original login/account/licensing experience is not part of the clone target.

If the reference opens into a login/locked state, capture only enough evidence to document the access boundary. Do not implement that screen in the clone and do not bypass it.

## Required inventory

For every accessible in-scope screen/state, capture or record:

- window size/minimum behavior;
- navigation hierarchy;
- tab order;
- labels and exact copy;
- icons/assets;
- colors;
- typography;
- spacing/padding;
- borders/radii/shadows;
- tables and columns;
- filters;
- buttons and disabled states;
- toggles/defaults;
- empty/loading/error states;
- dialogs/popovers/tooltips;
- theme/language behavior;
- responsive behavior;
- keyboard/focus/hover/selected states where observable.

## Auth-boundary rule

Login/account/licensing is not a page-reproduction target.

If encountered:

- record the visible boundary and what in-scope surfaces are inaccessible;
- use legitimate owner-provided/existing access only if available;
- inspect auth-related local state contracts if required by the assigned UI
  predicate, under `AGENTS.md` section 6; do not circumvent original access controls;
- static frontend evidence may still be used to inventory post-auth UI, but any runtime visual state not directly observed must remain `UNKNOWN` / not visually validated.

## Evidence layout

Future UI findings should use:

`docs/reviews/YYYY-MM-DD-LWB317-UI-###-short-title.md`

Durable screenshots/captures should go under:

`evidence/lwbridge-0.3.17/ui/`

Do not overwrite the original reference executable.

## Build policy

The UI implementation may reuse exact recovered frontend assets where lawful/technically practical.

Synthetic backend data is allowed only in an explicitly labeled preview/test harness. It must not be confused with recovered runtime behavior.

The new clone should enter the in-scope app experience without reconstructing LWBridge's original login/account/licensing system.

## Acceptance

An in-scope page is visually complete only after repeatable comparison against the 0.3.17 reference in the same observable state.

Pixel/geometry comparison should be preferred over subjective statements such as “looks close.”

If runtime visual comparison is blocked by auth, mark that validation gap explicitly rather than bypassing the boundary.

## Current work

`LWB317-UI-001A` established the 0.3.17 frontend package statically. The exact
reference contains a 24-record Brotli-compressed Tauri asset table in `.rdata`;
all 24 web assets were losslessly recovered and hashed under
`evidence/lwbridge-0.3.17/ui/frontend-package/`.

Detailed evidence:

`docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`

This is an `EXACT_BYTES` package baseline only. It does not claim visual parity,
runtime-state parity, or any recovered gameplay/backend behavior.

`LWB317-UI-001B` runtime observation on 2026-09-29 reached the original
authorization/login boundary before the post-auth shell. The boundary was
captured without submitting credentials or bypassing entitlement, and the
post-auth shell/navigation runtime baseline is therefore `BLOCKED`. The active
campaign may continue with its separately authorized static/offline UI stages;
static frontend evidence must not be described as runtime visual proof.

`LWB317-UI-002A` through `LWB317-UI-002H` have now inventoried Home,
Automation, Map Data, Squads / AFK, City Layout, Hotkeys, Mini Games and
Settings from exact recovered frontend bytes. `LWB317-UI-003` consolidates the
shared light/dark tokens, typography, shell geometry, navigation states,
controls, cards, tabs, tables and responsive rules. These are static contracts;
post-auth rendered visual comparison remains blocked by the same auth boundary.

`LWB317-UI-004` created the clean separate React/Vite project at
`src/LWBridge.UI-0.3.17/`. `LWB317-UI-005` reproduces the exact statically
recoverable shell/navigation contract and `LWB317-UI-006` implements the eight
effective visible routes with evidence-safe static/loading/empty/disconnected
states. The original login/account/licensing experience remains absent by
design, and gameplay/backend actions are not wired in this Phase 1 preview.

`LWB317-UI-007` completed the available exact-component/CSS comparison pass and
fixed concrete clone hierarchy/state deviations. Before/after clone evidence is
under `evidence/lwbridge-0.3.17/ui/visual-comparison/`. The stage remains
`PARTIAL` solely because repeatable direct comparison against the original
post-auth runtime is blocked by the auth boundary. No pixel-parity claim is made
without that reference state.

The static UI campaign was accepted and Map function recovery subsequently
opened. The canonical frontend is now the normal packaged Desktop UI. This
acceptance covers the static baseline, not every conditional state or original
runtime pixel parity.

`LWB317-UI-COMPLETE-001` remains historically `CHANGES_REQUIRED` after PM-006.
The follow-on `LWB317-UI-CORRECT-001` is also `CHANGES_REQUIRED` after PM-007
review of `543b6eb`. The Home reconnect scope fix is accepted at the actual
callback/transport boundary; useful Equipment dialog/drag/result/progress and
locale corrections are retained. Those reviewed defects motivated
`LWB317-UI-CORRECT-002`, now `AWAITING_REVIEW` after the owner requested recovery
of the interrupted worker's uncommitted changes at lead baseline `a19905d`.
Construction/Training/Gather/Train local controls, per-ID AFK drafts and applied
Map fixture queries now have targeted contract and browser evidence. The config
store handles concurrent edits, failed saves, retry and discard in local memory.
This is a focused correction checkpoint, not complete all-page UI acceptance.
Additional Automation runtime/Trade/Assist branches, weekly quality draft wiring
and AFK target/member variants remain source-recoverable implementation gaps.
See `reviews/2026-10-01-LWB317-UI-CORRECT-002.md` and the complete affected
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-002/coverage-matrix.md`.

CORRECT-003 subsequently pushed partial implementation `5204f67` for many recorded
gaps. Its Stage B browser/coverage/delivery evidence is unfinished; PM-008 does
not accept full UI parity. At the owner's request the broad task is split, with
initial CORRECT-003A limited to weekly quality controls and save-state verification.
Earlier gap statements describe the reviewed CORRECT-002 baseline; validate the
new implementation before changing their completion disposition.

PM-009 returned CORRECT-003A `2f439ce` for its missing immediate write. R1 at
`d065d08` fixes both actual callbacks; PM-010 accepts the focused local weekly unit
after default, saving editability, actual deferred-callback/recovery and source
anchor checks. Native persistence/original pixels/full UI remain unaccepted.
PM-012 accepts focused local Trade selection CORRECT-003B/R1 at `bfc652f`.
PM-011's first-offer metadata and ID-based goods-label defects are fixed; the
unchanged actual-expression and selection/save/recovery checks pass with canonical
build/package and evidence validation. Worker browser evidence is retained.
PM-013 accepts CORRECT-003C `1a25a9c` for source/local Purchased-items presentation:
actual helper, grouping/totals/names/row fields, empty state and package/evidence
checks pass; worker populated/empty/Portuguese browser QA is retained. Native
images remain declared placeholders. PM-014 accepts local/source cross-server
setting CORRECT-003D at `bd808994` after actual-callback/regression/package/
evidence checks. CORRECT-003E is AWAITING_REVIEW after project-lead implementation
takeover: exact-shaped status defaults, supplied fetch-error text and independent
goods/purchases are checked by differential render fixtures and local browser QA.
See `reviews/2026-10-02-LWB317-UI-CORRECT-003E.md`; independent review pending.
Other panels/native providers/purchasing
and full UI/native/pixel parity remain unaccepted.

Correction evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-PM-015/` for the additional Home source/state
audit: 960 synthetic comparisons identify remaining error translation/channels,
busy presentation and localized switch-description gaps; no product/native change.
Finding: `reviews/2026-10-02-LWB317-PM-015-home-audit.md`.
HOME-ERROR-001 implements the translation-only correction and is AWAITING_REVIEW:
actual helper/render/nine-locale/browser checks pass. HOME-ERROR-002 separate
channels correction is also AWAITING_REVIEW after 9 callback scenarios, 4 original
picker cases, 72 render comparisons and 5 browser observations. See
`reviews/2026-10-02-LWB317-UI-HOME-ERROR-002.md`. HOME-BUSY-001 is also
AWAITING_REVIEW after nine-locale render/predicate and local browser checks;
independent proxy/launch inputs have no production lifecycle producers. See
`reviews/2026-10-02-LWB317-UI-HOME-BUSY-001.md`. Localized switch descriptions
are assigned to the returning worker under LWB317-UI-SWITCH-LOCALE-001. See
`reviews/2026-10-02-LWB317-UI-HOME-ERROR-001.md`; original/native parity unproved.
Earlier correction evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/`; worker report:
`reviews/2026-10-01-LWB317-UI-CORRECT-001.md`. Previous lead disposition:
`reviews/2026-10-01-LWB317-PM-007-ui-correction-lead-review.md`. PM-006 and
previous worker findings remain preserved.
Direct post-auth original visual
comparison remains `BLOCKED`, so these rows stay `IMPLEMENTED_NOT_VALIDATED`
unless they already have separately scoped live proof. No product-wide parity
claim is made. The Map Goal remains awaiting independent project-lead closeout.
