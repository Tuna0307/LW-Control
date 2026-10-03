# LWB317-UI-REMAINING-PAGES-001 — large four-page UI closeout

Status: READY. Owner requested a larger assignment after the worker model update.
Work alone: no subagents, delegation or another worker chat. Complete sequential
milestones within this assignment without waiting for permission between them.
The project lead performs final acceptance through the owner's manual relay.

## Goal and starting state

Finish source-recoverable UI/UX residuals on exactly four pages: **City Layout,
Hotkeys, Mini Games and Settings**. The product goal remains one-for-one LWBridge
0.3.17 post-auth experience without login/account/licensing UI. This assignment
closes page presentation and local interaction gaps; it does not start functional
game integration or assert original protected-runtime pixel equality.

Repository: `C:/Users/chimw/OneDrive/Desktop/Github/LW-Control`.
Branch: `research/offline-controller`.
Current worker return: `32b290699035964ecefd62efe6f100178d7460dc` (documentation),
with Equipment implementation `2e4accef7b0709a903034293126cb63fe389b105`.
A lead acceptance/dispatch commit follows. Inspect HEAD/status, keep newer work,
and never reset to an older hash. Equipment parent/R1 are accepted for their bounded
source/local scope by the new lead R1 review. Home, Map, Automation and AFK already
have focused accepted proofs. Preserve them and the corrected Equipment lifecycle.

Before work read AGENTS.md, docs/AI_WORK_PROTOCOL.md, task.md,
docs/PROJECT_LEAD.md, docs/UI_FINISH_CHECKLIST.md, docs/implementation-handoff.md,
docs/lwbridge-parity-matrix.md, docs/lwbridge-feature-ledger.md and this work item.
Historical reviews guide research but do not prove current parity.

Exact reference: `C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Exact original assets: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`:

- CityLayoutPanel-DoNWkywK.js
- HotkeyPanel-XA8idRHB.js
- SettingsPanel-DqxIWv_E.js
- index-BVfnK1wp.js, index-rIL9Fpht.css, locale assets and imported helpers.

Trace the Mini Games route/renderer from the original index and its imports; do not
invent a separate asset name. Current page entry points and shared HotkeyCard are
in `src/LWBridge.UI-0.3.17/src/Pages.jsx`. Inspect actual related modules/styles.

## Milestone A — fixed inventory and immutable baseline

Recover each page's render branches, controls, defaults, disabling predicates,
local state lifetimes, effects, dialogs, keyboard/focus and save/error behavior.
Record source identity and exact UTF-8 byte locators. Include current disconnected/
loading, populated, busy, success, empty and error branches only when present in
the source; mark unavailable inputs/producers explicitly.

Create a finite four-page coverage matrix: behavior, original locator, current
implementation, actual proof method, baseline finding and remaining limit. Counts
refer to inventoried branches, not multiplied test scenarios. Preserve an immutable
dispatch baseline plus executable distinguishing cases for demonstrated defects.
Do not spend the whole campaign cataloguing: proceed to the four implementation
milestones once the finite inventory is saved. Commit this checkpoint.

## Milestone B — City Layout

Audit the recovered disconnected/load and populated workbench surfaces: header,
toolbars, grid geometry, buildings/obstacles/labels, selection and inspector,
available actions, warnings and source-backed validation. Recover exact local
move/drag/drop, placement rejection/no-op, selection cleanup, Escape, history and
Undo/Redo shortcuts, dirty/reset/save transitions and confirmation behavior where
they exist. Test boundaries, overlaps and invalid input against actual original
helpers/expressions, not guessed geometry or clone constants.

Fix demonstrated presentation/local interaction omissions. Synthetic city objects
must be explicit preview fixtures; never claim that they are actual native city
data. Distinguish physical HTML5/pointer proof from DOM-dispatch and actual React
handler proof. If physical input cannot be driven, record that limit and finish
source/handler proof. Test navigation away/return and effect/timer cleanup. Commit.

## Milestone C — Hotkeys

Audit every recovered shortcut card, binding, label/description/warning, ordering,
enabled state, conditional attack speedup settings and local configuration branch.
Recover initial/load/failure, edit, pending acknowledgement, success/failure and
Retry/Discard only as actually present. Test single-field edits preserve unrelated
fields, source-specific save timing, overlapping edits/older acknowledgement if
the original draft contract permits them, and navigation/profile state ownership.

Fix source-backed gaps in the existing canonical implementation. Exercise actual
callbacks and mounted keyboard/focus behavior locally. No OS/game hotkey injection,
global native listener registration, attack, healing or other gameplay action.
Supplied online predicates can be compared in an inert harness without enabling
native actions in the product. Preserve shared Mini Games card behavior. Commit.

## Milestone D — Mini Games

Recover all original cards, defaults and conditional presentations, including
Frontline, treasure-chest configuration, Land action result and Food House/Sheep
states where present. Recover source state precedence, progress/count/time
formatting, errors/results, start/stop labels and predicates, unknown/missing values,
configuration feedback and lifecycle. Do not treat a few named fixture strings as
complete coverage of the original state branches.

Use exact original renderers/helpers and controlled supplied runtime state. Implement
omitted local UI/configuration branches. Local toggles may use acknowledged preview
drafts; start/stop/result producers remain inert and unavailable natively. Do not
implement puzzle solvers, execute games, send game input or create native jobs.
Check changed shared HotkeyCard regressions and navigation/effect cleanup. Commit.

## Milestone E — Settings

Recover visual FPS/ping preferences, any profile-interaction/focus section,
conditional visibility, defaults, loading/errors, edits/save acknowledgement and
retention as actually present. Recover diagnostic export UI progress, completion,
failure/size/path presentation and updater idle/checking/available/downloading/
complete/error branches, version/percentage formatting and action predicates from
the original source. Enumerate additional states if they are present; do not guess.

Match the source's labels/defaults/conditions/timing and local state. Controlled
preview acknowledgements and events may exercise UI transitions; no original
service calls, diagnostic submission, archive export of private owner data, updater
download/install/restart or native preference integration. Keep native operations
fenced. Original commercial/account/licensing features remain excluded. Commit.

## Milestone F — four-page verification and closeout

Review the complete production diff for regressions and unintended changes.
Verify all four page inventories are reconciled, every correction has actual
original/submitted/current proof where executable, and unknowns are honestly
listed. Check route leave/return, keyboard listener ownership, focus/dialog/timers,
theme/locale transitions, layout and preview/native separation for these pages.
This is affected integration, not a new full Home/Map/shell campaign.

For each changed page exercise real browser controls in English/light and
Japanese/dark, plus an actual recorded narrow viewport appropriate to the recovered
layout. Cover meaningful pending/error/empty/conditional branches and negative
interactions. Inspect saved settled screenshots; capture fresh console warnings/
errors. Native actions must stay unavailable/reject, never simulate native success.
Audit all nine locale catalogs for new keys/recovered copy; do not invent translations
or fill gaps with English when an exact recovered catalog entry exists.

Use actual production callbacks/effects and original renderers/functions. Marker
checks and fixtures validating their own constants are insufficient. Source/handler,
mounted browser, physical input, reference pixels and native proof are different
claims. If a legitimate evidence/tool limitation remains, record it with the exact
continuation; don't relabel it as a pass or add a fallback.

Run meaningful focused checks after each milestone; after final production changes
run `npm.cmd run check`, `build`, `check:production-build` from the frontend folder,
affected Equipment/AFK/shared draft regressions, the new evidence validator, protected
WIP guard, and diff/staged checks. Reuse current proof adapters. Don't repeatedly
run expensive unrelated Map suites; rerun broader checks only for an actual shared
change/risk. Preserve historical pinned manifests when product hashes become stale;
create a current validator instead of rewriting history to manufacture a pass.

## Scope and preservation

Allowed: these four page components, their directly used local helpers/fixtures,
locale/style corrections required by recovered source, focused evidence/checkers,
and task/current master documentation. Shared files require minimum scoped edits
and directly affected regressions. Do not rewrite Pages.jsx wholesale or reorganize
the messy research repository as part of this campaign.

Not assigned: Home/Map/Automation/Equipment redesign or broader reinvestigation,
shell profile/navigation campaign, native/backend/gameplay/service reconstruction,
auth bypass, login/account/licensing UI, live Last War operations, legacy retirement
or a legacy/product fallback. Local UI dependency tracing follows AGENTS section 6.
Don't delete earlier evidence or convert native controls into working game actions.

Preserve all seven protected paths in
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/protected-wip-before.json`.
Run its `check-protected-wip.mjs` before/after. Keep Map result WIP,
previewAfkFixtures.js, .scratch-lwb317 and CORRECT-003 screenshots unchanged/unstaged.
No reset, stash, clean, broad staging, force-push or unrelated process/tab cleanup.
Do not open native window.confirm: execute confirmation accept/cancel inertly.
Own/clean only your temporary browser tabs, test storage and preview helpers.

## Durable delivery and interruption recovery

Write `evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/` with per-milestone
subdirectories, inventory/coverage matrix, original/production source manifest,
immutable baselines, actual results, browser/console/image records, complete diff
review and executable integrity validator. The validator must check actual EXE,
asset/slice/current product hashes, evidence JSON/screenshot hashes and protected WIP.
Write a dated campaign review and a short `continuation.md` after every milestone.

Continue A → F autonomously. Each coherent milestone: review diff, run applicable
checks, commit, push origin/research/offline-controller, verify full remote SHA,
then update the next exact step. No fixed 20-minute cutoff and no permission wait
between pages. If interrupted, preserve all work, state exact next file/case/command
and commit a coherent checkpoint when possible. Resume the first unfinished
milestone rather than restarting or rerunning completed unaffected investigations.

Final return: AWAITING_REVIEW with A–F statuses, per-page source/local implemented
and validated branches, exact unresolved UI/asset/proof gaps, checks, evidence/review
paths, implementation/final commits, direct remote SHA and protected status. Update
this work item and current handoff/matrix/ledger conservatively; the lead accepts
individual pages and global UI separately. Stop at these four pages. A large
assignment does not authorize cutting corners or declaring the whole clone done.
