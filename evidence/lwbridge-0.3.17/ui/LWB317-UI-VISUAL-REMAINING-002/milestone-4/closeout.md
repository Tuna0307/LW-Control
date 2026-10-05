# Milestone 4 closeout — shared shell and Home

Date: 2026-10-05

Status: **READY**

Integrated baseline for this milestone is Milestone 3,
`52dd0e353a9525abbf9e04f022cf5ffbf3371eed`. This closeout records the
task-local source/local recovery and evidence to be checkpointed as Milestone 4.

## Recovered source authority

- Reference executable SHA-256:
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Main recovered JavaScript SHA-256:
  `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
- Recovered stylesheet / current `reference.css` SHA-256:
  `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`.
- Exact shell/Home source slices are pinned by the task-local validator:
  `Fn`, `In`, `qr`, `Yr`, `Qr`, `si`, `Gi`, and `Ji`.
  In particular, complete shell `Gi` is UTF-8 byte `361306`, length
  `14461`, SHA-256
  `AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5`.

## Demonstrated source-local corrections

The immutable pre-fix baseline under `baseline/` records the demonstrated
composition/lifetime gaps. Current production restores:

1. exact `Fn` multi-profile ancestry,
   `aside.profile-sidebar > ProfileSidebar`;
2. exit prompt/dialog composition as siblings after `main.app-shell`, matching
   recovered root `Gi` + `qi`;
3. the four profile-scoped shell flag stores in recovered order — Weekend
   Shield, Attack Shield, Automatic Reconnection, Auto Close Popup — including
   the recovered Weekend/Attack legacy `auto_shield` fallback;
4. the local preview-only uncached/cached profile loading/cache transition,
   without claiming a native multi-profile provider;
5. Auto Launch preference edits no longer clear the parent-owned lifecycle
   action error;
6. recurring status refresh no longer owns recovery state; the selected-profile
   effect now owns both initial `game_recovery_status` and
   `bridge://game-recovery`, with the same profile/closed guard and cleanup.

Unavailable native/profile/game/updater actions remain provider-fenced. No
control was enabled merely to force visual equality.

## Home source/render proof

`home-source-render/` contains fresh task-local original/current proof:

- 42 EN/JA source-render cases;
- 34 structurally exact cases;
- 8 cases whose only structural difference is the current-side `disabled`
  attribute for unavailable-provider lifecycle controls;
- 40 browser pixel pairs;
- 24/24 exact-required pairs pixel-identical;
- 16 disclosed provider-fence pairs;
- matching Home root geometry in every pair;
- zero browser console/page issues.

The provider-fence cases are deliberate availability differences, not parity
failures.

## Complete shell source/render proof

`shell-source-render/` closes the final independent-review blocker by
executing the recovered whole `Gi` composition and the canonical current App
composition from source-derived roots. It also executes/pins the recovered
`Fn`, `si`, `Yr`, `Qr`, `ti`, `In`, `Ji`, `qi`, and `Oe`
dependencies with inert disclosed providers/hooks.

The 16-case whole-shell matrix covers base EN/light and JA/dark, connected and
update/status presentation, compact/expanded/error-forced-open profiles, note
modal, drag/over, uncached loading and cached return, four ordered save errors,
Cross-server closed/open, exit idle/busy, and a 720px JA/dark expanded
multi-profile shell. Original pages use the recovered stylesheet; current pages
use `reference.css + styles.css`.

The raw evidence is deliberately unmasked: 16 pairs, zero pixel-identical whole
shell pairs, and 1,659,712 changed pixels. Representative inspection and saved
measurements classify those deltas as the already-declared scope/availability
differences: recovered Account/authorization-upgrade UI is outside clone scope,
while current updater/Refresh Status/profile-run controls remain disabled when
providers are unavailable. Common shell ancestry/order/state is aligned.

The packet detects both motivating ancestry regressions: removing
`aside.profile-sidebar` and moving exit inside `main.app-shell`.
`validate-read-only.mjs` reports 16 browser pairs, zero console issues and two
mutation detections without repinning.

## Current mounted App proof

`run-browser-current.mjs` / `browser-current-results.json` are regenerated
against current `App.jsx` SHA-256
`E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`.
The mounted real-App packet has 124 assertions, 18 screenshots and zero
console/page issues. It covers combined Home/shell ancestry, EN/JA, light/dark,
narrow layout, ordered save errors, profile compact/expanded/note/loading/cache,
Cross-server, exit idle/busy, live locale/theme, updater presentation and
provider fences.

## Independent review

- `review/home-source-audit.md`: final follow-up **READY**, no remaining Home
  source/local blocker.
- `review/shell-source-audit.md`: final follow-up **READY** after independent
  replay of the whole-`Gi` packet; no remaining shell source/local/evidence
  blocker outside the declared auth/account and unavailable-provider fences.

The original reviewer family became inaccessible after conversation compaction.
The coordinator first attempted to recover it, then used the fresh bounded
review family only after the runtime confirmed the original workers were not
available in this resumed chat.

## Validation and preservation

Current closeout gates:

- `validate-milestone-4.mjs`:
  `LWB317_REMAINING_M4_VALIDATION_OK`;
- whole-shell `validate-read-only.mjs`:
  `SHELL_SOURCE_RENDER_VALIDATE_OK` with 16 cases / 16 browser pairs /
  0 console issues / 2 mutation detections;
- canonical package `npm run check`: green;
- production build: green;
- `check:production-build`: green;
- legacy WIP archive guard:
  `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10`;
- `git diff --check`: green.

The older AFK-editor/startup read-only manifest intentionally remains a
historical checkpoint. Later accepted Milestones 2–4 changed several of its
frozen inputs, so its present hash mismatch is a checkpoint boundary rather
than a current product failure. It is not repinned or rewritten here.

No native/gameplay/network/updater action was invoked by Milestone-4 evidence.
Task-owned browser work used isolated port 4391; owner listeners 4319 and 4347
were not controlled.
