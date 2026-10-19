# R20 Squads / AFK engines

R20 is on `codex/feature-recovery-018`, draft PR #14. Engine work is in progress.
This document separates recovered contracts, implementation choices and proof.

## Recovered contracts

- Original 0.3.17 `SquadPanel-HC3-DJei.js`, SHA-256
  `ed466e353aa896d8c3fc783ff1802930156fb9e9eb54327aef73f2ff86fa06c7`, retained on
  `origin/research/offline-controller`, establishes Shared AFK Profiles, master
  switch, target type/name, levels, distance, progressive levels, assignments,
  execution limit, no-return attacks, rally launching/joining, join timing,
  leader lists, skip-solo and skip-removed options. The original provider engine
  was not recovered by those UI/configuration investigations. R8-024 and R8-047
  on that branch explicitly distinguish persistence/catalogue plumbing from
  provider execution. Static REA evidence for the extracted original SquadPanel:
  `ev_fe4332dda5e7e1d21b0ef279c1c58a5ff73edfe37b149d519f05031143d711e8`
  (semantic relationships remain partially unknown; no original runtime run).
- Current v26 `UISearchCtrl.OnSearchClick` lines 42–68 and
  `FindMonsterMessage.OnCreate/HandleMessage` lines 10–55 establish the native
  monster search request and returned UUID/point. `OnSearchEnd` lines 70–78 uses
  `GoToUtil.MoveToWorldMarchAndOpen` to acquire the returned native object.
- `MarchUtil.OnAttackMonster` lines 430–539 supplies the original formation or
  existing march, typed monster, stamina and `MarchAutoBackType.NoBack/Back` to
  the original march route. `UIFormationSelectListNewCtrl.BestSelect` lines
  2123–2125 uses `formation:ConscriptSoldier()` for native troop staffing.
- New formations reuse the Q/W/E/R adapter's original create serializer and
  single-send observation. Confirmation requires the current owned native
  march's response-derived `clientCreateGuid`, target and formation.
- `MarchUtil.SendChangeMarchToServer` lines 2016–2065 sends an existing march
  change; `WorldMarchChangeMessage.HandleMessage` line 90 applies the native
  server response. Continuous retargeting adds a separate single-send fence
  and requires the same owned march's new target/type and changed native
  `startTime`. It does not claim a create GUID for a change request.
- `UIAllianceWarMainTableCtrl.OnJoinClick` lines 663–707 admits `ATTACK_BOSS`
  rallies, using the rally UUID and leader's `startId`. `GetWarItemData` lines
  310–318 resolves **`AllianceWarInfo.targetUid` as a monster template ID**;
  `targetUuid` is the monster's world UUID. These fields must not be confused.
  `AllianceWarDataManager.GetAllCanJoinAllianceWarData` supplies allied joinable
  rallies. The native formation controller owns rally time and battle checks.

Native source reports and SHA-256 identities are retained under
`artifacts/feature018-r20-source/`. The admitted original package has content
version 3/26 and original LuaEntry SHA-256
`50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`.

## OWN_DESIGN implementation

`feature018_afk_engine.lua` schedules copied, confirmed settings. A profile's
execution count is shared across its assigned squads; pending sends reserve a
count before another squad can send. Zero is unlimited. A completed attempt is
an acknowledged native march followed by battle/return/idle state; it does not
claim a battle win without a separate win observation. Progressive profiles
advance after completed attempts, bounded by the game's catalogue limit.

Profiles run in saved order and share a squad reservation. Disabling/deleting a
profile leaves its already sent march observable but suppresses new sends.
Stop immediately suppresses new sends and does not recall an in-flight march.
Account changes pause the captured owner; a fresh session lease loss stops it.
No send may transfer to another account or another session.

The native adapter searches at most once per ten seconds when no eligible
world target is available. A briefly retained target exclusion avoids repeated
stale UUIDs; it expires after 60 seconds. Runtime receipts retain the latest 20
attempts while counters continue. Native target/template identity is re-read
at the send boundary. Players and player rallies are always rejected.

Join timing retains one random position/delay per profile/rally. Leader lists,
departure-time limits, single-hero exclusion and observed removal exclusion
filter that native rally. Joining outside the camera AOI uses the game's
alliance-war data, rather than inventing a monster from coordinates. Native
controller/permission failures remain visible errors, never successful sends.

## Current proof

The first background packaged trial is retained at
`artifacts/map014-takeover/native-ui-20261019-052803`. Its exact approved account
admission preceded every mutation. One squad-1 profile completed three native
ordinary level-10 monster marches (monster ID 2023010) and stopped itself:
`r20-count3-final.json` contains three distinct march IDs, matching native
create GUIDs, observed returns, processed=3, running=false, state=completed.

A separate unlimited profile was stopped with a fourth march in flight. That
march returned; subsequent observations stayed idle with unchanged stamina and
no further sends. `r20-stop-command.json`, `r20-stop-after-*.json` and
`r20-stop-final-status.json` preserve the evidence. This trial predates later
rally/continuous/UI corrections; a final-source ordinary loop still needs its
own recheck.

The task-owned app/game exited normally. `owned-exit.json` confirms all three
script hashes restored exactly and no remaining recovery journal. No keyboard,
mouse, foreground/window management, player attack or Land Cell action was used.

Inert checks cover scheduler lifecycle/count reservations, level/distance/name
filters, stamina/account/squad pauses, Stop, native typed monster admission,
create GUID correlation, continuous change post-state, allied monster rally
admission and player-rally rejection. They are fixtures, not live rally proof.
The original 0.3.17 engine's internal scheduling algorithm remains unknown.

### Task 1 corrected-source qualification

Source `12b146dc` completed a fresh three-attack/auto-stop proof in
`artifacts/map014-takeover/native-ui-20261019-055515`, followed by a two-attempt
continuous proof using the same native march and a fresh Stop-in-flight proof.
See `r20-fixed-count3-final.json`, `r20-continuous-08.json` and
`r20-fixed-stop-final.json`. Continuous behavior reuses an existing returning
march before it reaches HQ; the server can briefly label it Returning. After
the finite limit the squad returned home. The source fix also reobserved native
Chinese target names and proper empty arrays. All task-owned processes exited,
all scripts were restored and temporary profiles were disabled/removed.

Native rally routes are built and covered by inert source-shaped checks. A
live rally/event positive has not yet been observed. Automatic stamina items
and the independent Drill engine are the next ordered R20 tasks.
