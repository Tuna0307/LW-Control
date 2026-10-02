# LWB317-UI-MAP-INTERACTIONS-001

Owner: worker delivery, project-lead verified continuation. Status:
**COMPLETE / ACCEPTED for focused source/local UI scope**, PM-026 at product
5b76ae8. All four milestones and final browser evidence are closed; native/full
UI/pixel parity remains open. See the PM-026 closeout review. Historical
project lead assignment 2026-10-02, after owner requests a large
task. One UI campaign with four required milestones; no fixed time limit.

## Goal and inputs

Recover and implement the remaining assigned Map search/selection interactions
and Scheduled Plunder UI presentation from LWBridge 0.3.17. Complete actual
production UI changes, not an audit-only or another review-only delivery.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Product baseline: `305240e22c963f64fe4f6522696792acbed2db91`; the lead assignment
documentation/evidence commit is newer. Inspect HEAD and incoming changes;
never reset the checkout to the baseline.

Read `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`, this work item,
`docs/PROJECT_LEAD.md`, `docs/lwbridge-ui.md`, `docs/lwbridge-map-scan.md`, the
latest handoff and PM-025 review before editing. Current owner priority remains
UI/UX before game-function integration. The lead owns the acceptance decision.

Reference: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Original assets are in
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.
Primary original `MapDataPanel-B4GXEND2.js` SHA-256:
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
Canonical frontend: `src/LWBridge.UI-0.3.17`.

## Milestone A — request ownership correction

NAVIGATION-001 remains CHANGES_REQUIRED after PM-025. Its tab-cache/navigation
correction is useful and must be retained. Lead reproduced two regressions:
pending success and pending rejection still mutate the component after
`backendAvailable` becomes false. The previous cancellation cleanup ignored both.
Read and rerun the lead independent case under `LWB317-PM-025` in the UI evidence
tree. Correct request disposal ownership while preserving generation fencing.

Cover backend loss, provider replacement, effect cleanup/unmount, re-entry and
stale success/rejection/finally, plus all delivered tab/server/page scenarios.
Retain the failing lead results as historical evidence; put corrected results
under this campaign rather than overwrite them. Commit this correction as a
coherent first milestone, then continue the already-assigned campaign.

## Milestone B — search and per-kind selection

Recover actual original keyword input/search submission behavior and timing.
Do not assume a debounce exists. Trace typing versus submitting, page resets,
navigation input reset, name-dropdown interaction, Resource/Monster option
selection, trimming/query projection where present, and loading/disabled/error
boundaries. Reproduce source-supported behavior only; keep unaffected accepted
filters and sorting unchanged. Scope excludes broader scan/options polling and
all-data clearing workflows.

Recover dispatch/ghost versus truck selection ownership from the actual row
callbacks and effects: source keys, stored row payload, membership, refresh/page/
tab behavior, invalid or duplicate identifiers and displayed selected counts.
Implement source-backed local state instead of assuming one shared Set and a
blanket reset matches all kinds. Preserve accepted rendered row keys and table
formatters unless a directly evidenced in-scope defect requires correction.

Recover local random-delay input, default/validation and related action busy/
disabled/label predicates. Separate original formatter predicates from canonical
runtime availability. Runtime scheduling/sharing remains fenced; an unavailable
provider must not become enabled merely to imitate an original connected screen.
Use explicit preview render inputs for connected/busy/error presentation without
performing or faking successful gameplay operations.

Starting UTF-8 byte locators in the primary original: cache helper 554; server
effect 33502; navigation selection effect body 34142; normal query effect 34677;
query builder `nr` 37497; search `rr` 38513; tab `ir` 39238; job list `Q` starts
near the `dispatchJobs` access at 39911; searchbar `map.searchLabel` context near
50746; selected-action labels near 55606. Revalidate and find complete functions,
state producers and renderers; proximity locators are not exact new facts.

## Milestone C — Scheduled Plunder presentation

The current tab shows a hardcoded zero count and generic empty table. Replace
that incomplete surface with the exact recovered Scheduled Plunder UI for
Dispatch/Ghost and Truck job data as supported by the original source.

Trace original row schemas, ordering, tables/columns, combined counts, rewards,
timestamps/countdowns, statuses, conditional actions and local inputs/dialogs
where they actually exist. Reproduce empty/populated and conditional states;
do not invent a state enum, modal, generic error panel or loading skeleton.
Unknown/invalid/missing values must follow source behavior.

Local original read-only job and action contracts may be inspected to recover
display dependencies. Do not add desktop commands, wire native providers or
schedule/share/cancel/claim jobs. Build canonical presentation components and
strictly isolated offline preview inputs. Runtime unconnected producers stay
explicitly documented as UNKNOWN/BLOCKED or IMPLEMENTED_NOT_VALIDATED as
appropriate; preview fixtures do not establish native feature completion.

Create deterministic explicit fixtures for representative recovered states.
Keep `online:false`, native action rejection and native/native-unavailable
isolation. Preview fixtures must never arm durable jobs or fake mutation success.
Exercise harmless local selection/input/navigation; verify action fences and
render busy/error/result states via disclosed controlled inputs.

## Milestone D — integration and evidence

Integrate all three milestones and verify cross-effects: cache restoration with
search inputs and selection; filter/name resets; rapid navigation while requests
are deferred; normal/scheduled transitions and counts; locale/theme layout.
Use actual production callbacks/effects and independently recovered reference
expressions. Preserve immutable baseline failures before fixing each defect.
Do not count tests that simply repeat new implementation logic as parity proof.

Exercise real browser controls with populated fixture verification. Use
`?previewPage=map-data&previewState=map-truck&previewLanguage=en&previewTheme=light`
and explicit new fixture names. Confirm actual provider marker, server and rows
before claiming success. Inspect saved screenshots, capture console errors and
disclose DOM-dispatched interactions. Cover English/Japanese, light/dark and a
narrow viewport across the combined surfaces; do not require a full Cartesian
product or arbitrary screenshot/test totals. Test timestamp boundaries with a
controlled clock instead of long waits. Browser proof and synthetic proof must
be distinguished in the coverage matrix.

Required regression checks: navigation; filter projection/ownership; Treasure
Checking/isolation; accepted row/table behavior; Home checks through canonical
gates. Historical harnesses may need a NEW read-only replay adapter for changed
state setters. Keep old scripts/results/manifests immutable; do not rename a
failure as success or skip current table verification because an old harness is
stale. Explain each adapter and ensure it still evaluates actual production.

Run `npm.cmd run check`, `npm.cmd run build`, `npm.cmd run check:production-build`,
focused evidence validators, protected-WIP checks and staged/full diff checks.
Write per-milestone evidence, a consolidated dated review, granular coverage
matrix, current master/matrix/ledger/handoff updates and an exact continuation.
Conservative status only: this task cannot establish full Map, overall UI,
original pixel, native persistence or gameplay parity.

## Subagents, constraints and delivery

The worker is authorized to use up to two subagents for this campaign. Suggested
division: main worker owns MapDataPage, request/search/selection integration and
masters; subagent 1 owns NEW Scheduled Plunder renderer/presentation files and
their focused tests; subagent 2 independently inspects original contracts,
negative cases and final regressions in exclusively owned evidence files.
Give each a self-contained scope and explicit file ownership before it edits.
Do not let multiple agents edit MapDataPage, preview providers or master docs.
Main worker reviews all contributions and runs integrated checks. Subagent
review is advisory; project lead still makes final acceptance.

Preserve accepted Home, Map filter/table/Checking and other UI work. No new
Automation/AFK campaign, native Treasure connection, scan/gameplay operations,
native asset retrieval, authentication bypass, login/licensing UI, redesign or
legacy fallback. Do not manufacture live states or native success.

Protected pre-existing WIP must remain byte-for-byte unchanged and unstaged:
`src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`.
Pin and compare the manifest under `LWB317-UI-LEAD-TABLES-001/protected-wip.json`.

Commit/push coherent completed milestones, verify direct remote SHA, and continue
through A-D without asking the owner to relay another prompt for each milestone.
If a concrete dependency blocks a part, preserve it with exact evidence and
continue independent assigned parts; do not invent missing behavior. At campaign
completion return AWAITING_REVIEW with per-milestone outcomes, final SHA, checks,
evidence, remaining limits and continuation. Never force-push or discard WIP.
