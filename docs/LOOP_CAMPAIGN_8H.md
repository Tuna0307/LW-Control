# LWB317-UI-CAMPAIGN-8H — pre-authorized UI parity campaign

**State:** ACTIVE  
**Project-lead authorization:** 2026-09-29  
**Maximum unattended duration:** 8 hours  
**Stop-opening-new-work threshold:** 7 hours 45 minutes after recorded start

This campaign is designed for Chat On Steroids Loop mode.

It pre-authorizes the worker to progress through the UI-only stages below **in order without waiting for project-lead review between stages**.

It does **not** authorize gameplay/backend function reverse engineering.

## Worker must fill at start

- Local campaign start time: `2026-09-29 21:20:56 +08:00`
- Starting branch: `research/offline-controller`
- Starting commit: `a2e470818b000b10285a346da0306b57fa315561`
- Reference hash verified: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` — exact match
- Desktop tooling available: yes — Chat On Steroids Desktop responded successfully at campaign start

Obtain the actual machine time; do not guess it.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Expected SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Critical scope boundary

The project is **not recreating LWBridge's original login/account/licensing system**.

During this campaign:

- do not implement login/account/licensing UI;
- do not reverse engineer credentials, tokens, subscriptions, purchases,
  activation or license validation;
- do not bypass login/auth/entitlement;
- if runtime access is blocked by login, document that boundary and continue
  only with independently possible static/offline UI work;
- do not treat unobserved post-auth runtime states as visually proven.

The clone target for this campaign is the in-scope **post-auth product UI**.

## Global permissions

The worker may:

- use Chat On Steroids Core;
- use Chat On Steroids Desktop;
- launch/close the exact LWBridge 0.3.17 reference as required for UI
  observation;
- navigate through legitimately accessible top-level UI;
- capture screenshots;
- inspect the exact frontend assets already recovered by UI-001A;
- create a **new separate 0.3.17 UI reconstruction project** after the
  inventory stages permit it;
- create static/mock preview data only when clearly labeled as preview/test
  data and never represented as recovered backend behavior.

## Global prohibitions

Throughout the whole campaign:

- do not launch or control Last War;
- do not gameplay-test;
- do not reverse engineer gameplay/backend function semantics;
- do not investigate IPC/provider requests beyond what is needed to identify
  static UI structure;
- do not bypass auth/licensing;
- do not implement auth/licensing;
- do not modify the legacy `src/LWBridge.Desktop` reconstruction as the new
  0.3.17 clone;
- do not invent UI that cannot be established from direct runtime observation
  or exact recovered frontend assets;
- do not start Phase 2 function recovery.

## Evidence discipline

For every stage distinguish:

- **runtime-observed**;
- **EXACT_BYTES/static frontend evidence**;
- **preview implementation**;
- **UNKNOWN/BLOCKED**.

Never describe a static inference as runtime visual proof.

Use durable evidence paths under:

`evidence/lwbridge-0.3.17/ui/`

Use review files under:

`docs/reviews/`

## Stage status table

The worker should update this table as stages progress.

| Stage | Status | Commit | Notes |
|---|---|---|---|
| LWB317-UI-001B shell/navigation runtime baseline | BLOCKED | `bb7ec42` | Exact reference reaches out-of-scope auth boundary; boundary captured, post-auth runtime shell not bypassed; whitespace cleanup `491fd90` |
| LWB317-UI-002A Home inventory | COMPLETE | `81cc933` | Static `EXACT_BYTES` inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002B Automation inventory | COMPLETE | `51adf01` | Static `EXACT_BYTES` tab/card/string/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002C Map Data inventory | COMPLETE | `17bc084` | Static `EXACT_BYTES` scan/tab/filter/table/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002D Squads / AFK inventory | COMPLETE | `31a9d57` | Static `EXACT_BYTES` AFK/equipment/string/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002E City Layout inventory | COMPLETE | `aeb22f8` | Static `EXACT_BYTES` grid/inspector/string/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002F Hotkeys inventory | COMPLETE | `273a55a` | Static `EXACT_BYTES` shortcut/card/string/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002G Mini Games inventory | COMPLETE | `3224ec8` | Static `EXACT_BYTES` helper/card/status/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-002H Settings inventory | COMPLETE | `eb8e9b3` | Static `EXACT_BYTES` settings/feedback/update/CSS inventory; runtime visual observation blocked by auth boundary |
| LWB317-UI-003 common visual system | COMPLETE | | Exact static tokens/shell/control/card/tab/table/responsive system consolidated; runtime post-auth validation blocked |
| LWB317-UI-004 clean 0.3.17 UI project scaffold | PENDING | | |
| LWB317-UI-005 shell/navigation reproduction | PENDING | | |
| LWB317-UI-006 accessible page reproduction | PENDING | | |
| LWB317-UI-007 visual comparison/fix pass | PENDING | | |
| Campaign handoff/cleanup | PENDING | | |

Allowed campaign stage states:

- `READY`
- `IN_PROGRESS`
- `COMPLETE`
- `PARTIAL`
- `BLOCKED`
- `SKIPPED_TIME_LIMIT`

## Stage 1 — LWB317-UI-001B shell/navigation runtime baseline

Follow:

`docs/work-items/LWB317-UI-001B-shell-navigation-visual-baseline.md`

Checkpoint and push its evidence before continuing.

## Stages 2–9 — page UI inventories

For each page below, the goal is **UI inventory only**, not function discovery.

### LWB317-UI-002A — Home

### LWB317-UI-002B — Automation

### LWB317-UI-002C — Map Data

### LWB317-UI-002D — Squads / AFK

### LWB317-UI-002E — City Layout

### LWB317-UI-002F — Hotkeys

### LWB317-UI-002G — Mini Games

### LWB317-UI-002H — Settings

For each page:

1. verify whether it is legitimately runtime-accessible;
2. if accessible, capture the default page state before pressing feature/action
   controls;
3. record exact visible:
   - title/subtitle;
   - tabs/sub-tabs;
   - cards/sections;
   - labels;
   - button text;
   - toggles/default state;
   - fields/placeholders/default values;
   - table headers;
   - filters;
   - icons/images;
   - colors/typography/spacing;
   - disabled/selected/hover state where safely observable;
   - empty/loading/error text already present without triggering gameplay;
4. compare against exact recovered frontend assets/locale strings;
5. create:
   `docs/reviews/2026-09-29-LWB317-UI-002X-<page>-inventory.md`;
6. store screenshots under:
   `evidence/lwbridge-0.3.17/ui/pages/<page>/`.

Do **not** click page actions to discover backend behavior.

If a page cannot be runtime-accessed without auth/game state:

- mark runtime observation `BLOCKED`;
- do not bypass anything;
- perform a static `EXACT_BYTES` inventory from recovered frontend assets
  where possible;
- clearly separate static inventory from runtime visual evidence;
- continue to the next independently accessible page.

After each page inventory, run `git diff --check`, commit, push, and update the stage table.

## Stage 10 — LWB317-UI-003 common visual system

Consolidate only evidence already established by the earlier stages.

Create:

`docs/reviews/2026-09-29-LWB317-UI-003-common-visual-system.md`

Record evidence-backed:

- app/window geometry;
- sidebar/navigation dimensions;
- content paddings/gaps;
- typography family/size/weight hierarchy;
- exact/derived colors where measurable;
- borders/radii/shadows;
- button variants;
- input variants;
- card/panel variants;
- table/grid treatment;
- tab treatment;
- icon sizing/alignment;
- hover/focus/selected/disabled states;
- reusable image/font assets.

Do not infer unseen variants.

## Stage 11 — LWB317-UI-004 clean 0.3.17 UI project scaffold

Only begin this stage after the inventory stages have reached a coherent checkpoint.

Create a **new clearly named source project/path** for 0.3.17.

Requirements:

- do not overwrite legacy `src/LWBridge.Desktop`;
- choose technology based on the exact recovered reference and project
  maintainability, but preserve observable parity;
- wire only static navigation/shell scaffolding initially;
- no gameplay backend;
- no auth/login implementation;
- no fake “working” function results;
- recovered assets may be reused where appropriate;
- preview/mock data must be clearly isolated and labeled.

Create a short architecture/scaffold report under `docs/reviews/`.

Build/test the scaffold before checkpointing.

## Stage 12 — LWB317-UI-005 shell/navigation reproduction

Reproduce the in-scope app shell/navigation using the evidence gathered.

Target:

- geometry;
- labels/order;
- icons;
- colors;
- fonts;
- spacing;
- selected/hover/focus states;
- content frame behavior.

Do not add the excluded login system.

Capture clone screenshots under:

`evidence/lwbridge-0.3.17/ui/clone/shell-navigation/`

If reference runtime shell evidence is blocked, implement only what exact static
evidence supports and mark visual-validation gaps.

## Stage 13 — LWB317-UI-006 accessible page reproduction

Reproduce the static visual structure of pages inventoried in stages 2–9.

Rules:

- no gameplay/backend wiring;
- no auth/login implementation;
- no fabricated dynamic success state;
- static empty/default state only unless a reference state was directly
  observed;
- preserve exact visible copy/defaults/assets from evidence.

Commit logical page groups rather than one giant unsafe change.

## Stage 14 — LWB317-UI-007 visual comparison/fix pass

Perform repeatable comparison between the in-scope reference UI and clone for
states legitimately observable in both.

Prefer measurable differences:

- window/client bounds;
- component position/size;
- spacing;
- typography;
- color;
- borders/radii;
- icon placement;
- table/card dimensions.

Fix evidence-backed differences only.

Create:

`docs/reviews/2026-09-29-LWB317-UI-007-visual-comparison.md`

Store comparisons under:

`evidence/lwbridge-0.3.17/ui/visual-comparison/`

Do not “improve” the reference design.

## Time management

At the start of **every Loop continuation**:

1. read the recorded campaign start time;
2. obtain current machine time;
3. calculate elapsed time;
4. inspect this stage table and resume the first incomplete authorized stage.

Once elapsed time reaches **7 hours 45 minutes**:

- do not begin a new stage;
- finish only the current coherent checkpoint;
- update evidence/status;
- run applicable validation and `git diff --check`;
- commit/push;
- close LWBridge if the worker launched it;
- perform final campaign handoff.

Do not work past the 8-hour campaign boundary merely because Loop asks for another continuation.

## Early-stop conditions

Stop the campaign early if:

- reference SHA-256 does not match;
- repository contains unexplained conflicting changes that cannot be safely
  reconciled;
- remaining safe progress would require auth/entitlement bypass;
- remaining work would require Last War/gameplay access;
- desktop tooling is unavailable and no authorized static/offline stage can
  progress;
- all authorized stages are complete.

A blocked runtime page does **not** automatically end the campaign if later
static/offline UI stages remain independently possible.

## Per-stage Git/checkpoint rule

After each coherent completed stage:

1. finish its review/evidence;
2. update this campaign table;
3. run applicable build/test checks;
4. run `git diff --check`;
5. review `git status --short`;
6. create one coherent commit;
7. push to `origin/research/offline-controller`;
8. continue to the next authorized stage.

Do not force-push.

## Final campaign handoff

Before stopping:

- change `LWB317-UI-CAMPAIGN-8H` in `docs/LOOP_QUEUE.md` from
  `ACTIVE` to `AWAITING_REVIEW`;
- ensure the working tree is clean or document exactly why not;
- close LWBridge if the campaign launched it;
- do not start Phase 2.

Return:

```text
Campaign: LWB317 UI parity 8H
Elapsed:
Last completed stage:
Current commit:
Completed stages:
Partial/blocked stages:
Evidence created:
UI implementation state:
Runtime visual validation gaps:
Checks:
Processes left running:
Recommended project-lead review point:
WAITING_FOR_PROJECT_LEAD
```

After that, make no more project changes until the project lead reviews the campaign.
