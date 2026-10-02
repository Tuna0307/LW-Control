# LWB317-UI-MAP-FILTER-LIFECYCLE-001

Project-lead assignment, 2026-10-02. Status: **ASSIGNED; execution not confirmed**.
Owner: manually relayed worker AI; project lead owns final acceptance.

## Goal and starting point

Implement four remaining source-backed Map UI behaviors: option-refresh filter
validation, alliance sentinel separation, acknowledged clear-data resets, and
Treasure checkbox defaults/persistence/query projection. This is an implementation
assignment, not another audit-only delivery. Complete all milestones within this
scope; there is no fixed time limit or approval pause between milestones.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Assignment preparation baseline: `9d8fa4f982e3067c8359feb5f0a5e06b0efcd476`;
accepted product baseline: `5b76ae8111e160ad7673e1162e8a354d1c3cbd45`.
The assignment commit is newer. Inspect actual HEAD; do not reset to a baseline.

Read `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`, this work item,
`docs/PROJECT_LEAD.md`, `docs/lwbridge-ui.md`, the current handoff, and
`docs/reviews/2026-10-02-LWB317-PM-026-map-interactions-closeout.md` first.
INTERACTIONS-001 and corrected NAVIGATION-001 are accepted for focused source/local
UI scope. Do not restart A-D or wait for a separate worker approval. Overall UI
is still IMPLEMENTED_NOT_VALIDATED; original pixels/native functions remain open.

Reference EXE: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Canonical frontend: `src/LWBridge.UI-0.3.17`.
Original asset: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`.
SHA-256: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

## Milestone A — refreshed options and alliance identity

Recover and implement validation of the selected City alliance, Secret Task
level, and Treasure type when a current options reply replaces the lists.
Keep valid values and recover exact behavior for missing values, empty lists,
no-alliance counts and invalid selected values. Preserve accepted Resource/
Monster name validation and exact query/page ownership. Include deferred options
responses around server changes/clear/unmount so obsolete data cannot restore
an invalid selection or another server's options. Recover existing source
ownership before changing it; this does not authorize a polling redesign.

Represent real alliance options separately from `all` and `none` sentinels.
Original options use `name:${encodeURIComponent(e.name)}`; decoder `tt` returns
an empty string for a non-prefixed value or decoding failure. Both normal search
and existing export query construction must decode the actual alliance name.
Test names `none`, `all`, Unicode and URI-significant characters, plus malformed
encoded selections and disappearing options. Do not execute native export or
broaden into its feedback behavior.

Source starting anchors, UTF-8 bytes in the exact primary asset: decoder `tt`
9486; options effect 34871-35650; query `nr` 37497-38513; encoded option expression
51738. Revalidate complete expressions/locators; the independent INTERACTIONS
review's C02/C09 are starting hypotheses, not authority for new acceptance.

## Milestone B — successful clear-data UI reset

Trace every state reset and retained value in original `tr` (37087-37497).
On acknowledged success it resets alliance/name/level/item/Treasure filters,
options/counts/scan progress, both selection maps, cache/page/rows/total/loading
and revisions. Keyword and quality are retained. Determine the exact treatment
of every other filter, sort, message and option generation from the source;
do not implement a blanket reset. Keep already-correct state/error translation.
Verify failed/deferred clear preserves data/filter state as specified by the
source and that a stale options/search acknowledgement cannot repopulate cleared
data incorrectly. Inspect the subsequent options/query effects, not just setters.

This milestone changes frontend response handling only. A controlled test
harness may return a disclosed synthetic successful acknowledgement to exercise
the actual callback/effects. Production provider/native operations and browser
preview still reject unavailable clear actions; do not make the preview report
successful deletion. Real native clearing, deletion schema and scan engine are
outside scope. Browser evidence must distinguish local controls from harness-only
clear-success proof.

## Milestone C — Treasure preferences

Recover exact lazy state initialization and persistence effects. In the original:

- `lwbridge.mapIncludeForeignRadarTreasures` initializes checked only for the
  stored string `true` (state at 31702; persistence expression at 32076).
- `lwbridge.mapLuckyTreasurePriority` initializes checked unless the stored
  string is `false` (state at 31764; persistence expression at 32140).
- Persistence writes `String(boolean)`; both booleans are included for Treasure
  queries even when false, and omitted on unrelated tabs (query `nr`).

Revalidate identifiers/keys/effect timing directly. Cover missing/true/false/
unexpected storage strings, mounted toggles, remount/reload, tab changes and
false-valued query fields. Preserve source storage scope; do not invent profile
scoping, migrations, normalized values or extra fallback behavior. This assignment
does not connect native Treasure viewer/context/refresh/claim producers. Keep the
accepted explicit Checking fixture isolated from other fixtures and native modes.

## Milestone D — integration and reviewable evidence

Create immutable pre-change baseline evidence and new original-vs-production
tests using actual production callbacks/effects, not duplicate implementation
logic. Reuse the existing exact original component runner under
`LWB317-UI-MAP-INTERACTIONS-001/original/` where suitable. C02/C03/C08/C09/C28
are starting cases: expand to distinguish retention, failure and race behavior.
Separate genuine source differences from harness stub artifacts, especially the
previous C04 synthetic-name and C14 summary-count diagnostics.

Use real browser controls with verified populated fixtures for option selection,
alliance sentinel labels/queries and Treasure toggle/reload behavior. Add explicit
fixture states only where needed. Record English/Japanese and light/dark plus one
narrow layout across the changed surfaces, without requiring a full Cartesian
product. Inspect screenshots, capture console errors, record test-only inputs
and restore task-owned browser preference/storage changes. Do not disrupt owner
sessions or stop a preview process you did not start.

Preserve accepted navigation/cache/request disposal, manual Search with no typing
debounce, name interactions, per-kind filters/sorts, Dispatch/Ghost reset versus
Truck persistence, row/table rendering, Scheduled Plunder presentation and action
fences. Run focused assertions for those touched dependencies and Checking
isolation. The unchanged exhaustive 10,482-render scheduled suite does not need
another full replay solely for adjacent filter edits; broaden verification if
Scheduled components/semantics change or a focused regression fails.

Run canonical `npm.cmd run check`, `npm.cmd run build`, and
`npm.cmd run check:production-build` from the frontend, new evidence validation,
and `git diff --check`. Historical evidence/scripts/manifests remain immutable.
Old identity validators may correctly pin older product hashes; disclose that
limit and use a new read-only assertion replay adapter when necessary, rather
than rewriting historical acceptance or silently skipping behavioral checks.

## File ownership, boundaries and delivery

You may use up to two subagents: one independently recovers original contracts
and counterexamples; the other builds independent tests/reviews the final diff.
Give each exclusive new evidence/test files. Main worker alone edits canonical
production and master docs and integrates/verifies their results. Subagent
recommendations do not replace project-lead acceptance.

Protected unrelated WIP must remain unchanged and unstaged:
`src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`.
Use the existing `LWB317-UI-LEAD-TABLES-001/protected-wip.json` for byte checks.
Do not force-push, discard work or stage the whole repository.

Explicit non-goals: scan polling/header timing/row refresh; export/start feedback;
native player-mark predicates; native Treasure/job/lifecycle providers; gameplay;
Automation/AFK changes; auth bypass; full original runtime pixel proof. Necessary
frontend filter dependencies are in scope, broader feature/backend work is not.

Write evidence under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/` and a dated
review in `docs/reviews/`. Update the current UI master, relevant parity matrix/
ledger and handoff conservatively, with proof types and remaining limits.
Review diffs, commit coherent milestones, push `origin/research/offline-controller`
and verify direct remote SHA. Continue through A-D without intermediate permission
requests; stop at this campaign's completion or a concrete blocker.

Return **AWAITING_REVIEW** to the project lead with commit/remote SHA, changed
behavior, baseline versus corrected results, check commands/results, browser
proof versus synthetic proof, subagent findings and your independent assessment,
protected-WIP verification, remaining unknowns, and exact continuation if blocked.
Do not claim full UI/native/pixel parity or assign the next campaign yourself.
