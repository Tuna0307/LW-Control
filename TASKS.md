# LW-Control reconstruction master task list

Last updated: 2026-09-07

This is the main backlog for reconstructing the Last War control application. It is intentionally evidence-driven: a feature is **not complete merely because its UI exists, its command can be queued, or recovered code for it exists**.

## Evidence labels used in this file

- **CURRENT-PROVEN** — observed and verified against the currently installed Last War build with bounded evidence.
- **RECOVERED** — behavior/contract recovered from the supplied original LW Control artifacts, decompiled managed code, embedded Lua, UI resource, or the separate `lwbridge` artifact. It still requires current-build compatibility proof unless explicitly stated otherwise.
- **RECOVERED+WIRED** — the rebuilt desktop has a UI/host/bridge path for the recovered behavior, but gameplay semantics are not yet live-proven.
- **PARTIAL** — some sub-paths are proven or implemented, but the full recovered feature is not.
- **UNKNOWN** — evidence is incomplete. Do not guess values, modes, packet fields, defaults, eligibility, or success semantics.
- **BLOCKED-RUNTIME** — implementation exists but cannot be validated until the game/runtime/account state provides the required condition.

---

# 0. Current baseline — what is already done

These are not tasks to redo unless a later change breaks them.

## UI / desktop baseline

- [x] The rebuilt desktop hosts the exact recovered Build 189 WebView2 UI resource.
- [x] Recovered UI SHA-256: `d75e86d7cef945929cd3fc6af8d736f4cd3dee028f8d79d52b405edc95486e4a`.
- [x] Six recovered top-level destinations are present.
- [x] All 42 recovered feature descriptors are present.
- [x] English/Simplified Chinese UI and recovered themes/responsive breakpoints are present.
- [x] Latest WebView smoke contract reports `shell=true`, `tabCount=6`, `featureCount=42`, `availabilityCount=14`, `recoveredExecutionEnabled=35`, `pendingExecutionEnabled=0`, and `captureCount=127`.
- [x] Latest desktop build before this task-file expansion passed with 0 warnings / 0 errors.

**Important:** the smoke result proves UI/host wiring and fail-closed gating. It does **not** prove that those recovered execution paths perform the correct gameplay action on the current game build.

## Runtime / bridge baseline

- [x] Recovered local command transport is implemented in `RecoveredGameCommandBridge`.
- [x] Recovered queue shape is preserved: 64 pending slots, schema version 1, single-use command IDs, result correlation, heartbeat freshness checks, and atomic queue writes.
- [x] Generic recovered `run_feature` forwarding is wired from the recovered WebView into the desktop host.
- [x] Host-side scheduling/enable semantics exist for continuous recovered automation paths.
- [x] Equipment scheme persistence/capture/apply plumbing exists.
- [x] Gameplay/menu hotkey host plumbing exists.
- [x] Auto-reconnect host lifecycle plumbing exists.
- [x] Game-data snapshot plumbing exists.

## Stronger current-build features

- [x] **World Scan:** current persistent runtime is live-proven for the existing v9 implementation.
- [x] **Daily Task claim path:** current persistent Daily Task runtime has bounded live proof for explicit current Daily Task reward claims.
- [ ] **Daily Free Claims as a whole:** remains PARTIAL because only the Daily Task category is current-implemented/proven.

---

# 1. Definition of DONE for any gameplay feature

No gameplay feature should be marked complete until all applicable boxes below are satisfied.

## 1.1 Recover the original contract

- [ ] Recover exact feature ID and action/mode strings.
- [ ] Recover exact argument names, types, ranges, defaults, and optional/required behavior.
- [ ] Recover original preconditions/eligibility checks.
- [ ] Recover original scheduling/continuous-mode behavior where applicable.
- [ ] Recover original stop/pause/cancel behavior where applicable.
- [ ] Recover original timeout and retry policy.
- [ ] Recover original post-action verification/effect contract.
- [ ] Recover original error/result mapping that the UI expects.
- [ ] Recover host-only behavior from original `MainForm.cs` rather than reimplementing by guess.

## 1.2 Prove current-build compatibility

- [ ] Identify the current manager/API/message/call path corresponding to the recovered original path.
- [ ] Confirm current enum/field meanings from current bytecode/runtime evidence.
- [ ] Keep unsupported/unknown fields null/unknown rather than inventing mappings.
- [ ] Verify all destructive/consumable actions use explicit preconditions.
- [ ] Verify actions do not silently consume premium currency, tickets, speedups, paid items, or other unapproved resources.
- [ ] Verify ambiguous network commit does not cause unsafe blind retries.

## 1.3 Live proof requirement

- [ ] Capture fresh pre-action state.
- [ ] Send exactly the intended bounded action.
- [ ] Correlate command/result/effect to the same command ID or authoritative state transition.
- [ ] Capture fresh post-action state.
- [ ] Confirm expected state changed and unrelated protected state did not.
- [ ] Record success/failure evidence.
- [ ] Confirm owned hooks/flags/runtime state are restored after the test.
- [ ] Confirm the game remains usable/running when that is the intended contract.

## 1.4 UI completion requirement

- [ ] Correct recovered control/action calls the correct host method.
- [ ] UI displays queued/running/succeeded/failed/blocked state accurately.
- [ ] Result details are adapted into the recovered UI format rather than dumped as raw JSON when the original had richer handling.
- [ ] Continuous actions show true runtime state after app refresh/restart.
- [ ] Stop/pause state is reconciled with the game runtime, not only local UI state.
- [ ] Feature remains fail-closed when runtime evidence is unavailable.

## 1.5 Regression requirement

- [ ] Desktop Release build: 0 errors, preferably 0 warnings.
- [ ] Core checks pass.
- [ ] Relevant Python/source/runtime tests pass.
- [ ] WebView smoke passes.
- [ ] `pendingExecutionEnabled = 0` remains true for unproven paths.
- [ ] `git diff --check` passes.
- [ ] Evidence/documentation is updated.

---

# 2. P0 — World Scan: make the full scan camera-free

## Goal

Pressing **World Scan** should scan the entire normal world without visibly moving the player's camera, while preserving complete player/resource/monster/alliance/other target coverage and all required detail fields.

## What is already proven

- [x] Normal map geometry is 100 x 100 logical AOI blocks = 10,000 blocks.
- [x] Recovered native batching can cover the full grid in 65 batches.
- [x] Recovered/current batch shape is 60 batches of 160 blocks + 5 batches of 80 blocks.
- [x] Serial capability probe + bounded concurrent native requests are proven.
- [x] Current main-grid transport can complete with zero camera moves.
- [x] The recovered original `LWC2MapScanner` defaults to `native_batch` and directly invokes `WorldPointManager.SendAoiRequest` / `WorldGetBlockMessage` instead of navigating the camera.
- [x] The current v9 full scan is live-proven, but it adds 500 camera views specifically for monster/march enrichment.
- [x] v9 restores the original camera after the 500 views.
- [x] v9 enriches player power and resource remaining/capacity through `WorldPointDetailManager`.

## Why the 500 camera views currently exist

The camera-free point sweep supplies strong persistent world-point coverage, but current-build direct point results did not provide complete monster/march coverage. The v9 scanner therefore loads 500 normal game views and captures monster/march push data as those regions become active.

## Reverse-engineering tasks

- [ ] Recover the exact **camera-free monster/march trigger** used by the original/reference implementation if present.
- [ ] Fully inspect recovered `LWC2MapScanner` around `SceneManager.World.GetMarchesBossInfo`, `WorldMarchDataManager`, `WorldPointManager`, `WorldGetBlockMessage`, native AOI hooks, and manager caches populated after native requests.
- [ ] Determine whether `GetMarchesBossInfo` can expose all off-screen monsters after native batch requests or only currently loaded data.
- [ ] Determine whether monster/boss data is present in native `WorldGetBlock` responses but currently discarded/misclassified before reaching our accumulator.
- [ ] Trace current `push.world.march.world.get.new` / march-push handling and determine whether direct AOI/block requests can cause those pushes without camera movement.
- [ ] Recover the exact `lwbridge-0.3.1.exe` map-scan trigger that causes off-screen records to enter hooked managers.
- [ ] Inspect `lwbridge` normal/fast scan service strings and IL2CPP/native call targets that precede publishing.
- [ ] Determine whether `lwbridge` calls an internal native view/AOI request directly, changes hidden world state, requests blocks through a different packet, or performs a non-rendering scene traversal.
- [ ] Compare original LW Control native-batch behavior and `lwbridge` behavior; do not assume they are the same architecture.

## Implementation tasks after no-camera monster source is recovered

- [ ] Add the recovered no-camera monster/march capture route to the persistent World Scan runtime.
- [ ] Keep the proven 65-batch static-world scan unchanged unless evidence requires a change.
- [ ] Preserve player-power detail enrichment.
- [ ] Preserve resource remaining/capacity enrichment.
- [ ] Merge records with stable identities so direct point + march data do not duplicate the same target.
- [ ] Preserve removal/update semantics if the source produces deltas while scanning.
- [ ] Keep the 30,000 accumulated-record fail-closed ceiling unless a live run proves it must change.
- [ ] Keep the 500-view path temporarily as a diagnostic/fallback until no-camera equivalence is proven.
- [ ] Once equivalence is proven, make normal product World Scan use `camera_move_count = 0`.

## Required live acceptance for camera-free World Scan

- [ ] Full 10,000/10,000 logical block coverage.
- [ ] 65/65 native batches complete.
- [ ] `camera_move_count = 0`.
- [ ] No scan-time camera traversal.
- [ ] Player bases captured.
- [ ] Resource points captured.
- [ ] Monsters/bosses captured across the full map.
- [ ] Alliance targets captured.
- [ ] Other recovered map categories not regressed.
- [ ] Compare camera-free and v9 results under as-close-as-practical world state; category counts are dynamic, so do not require historical fixed counts, but investigate material unexplained drops.
- [ ] Player identity/name/level/alliance/server/coordinates remain valid.
- [ ] Player power enrichment remains bounded; unresolved values remain unknown.
- [ ] Resource type/level/gather state/remaining/capacity remain valid where authoritative data exists.
- [ ] Monster identity/name/level/recommended-power fields remain populated from authoritative current sources.
- [ ] No duplicate-identity explosion.
- [ ] All scan-owned hooks/flags are restored.
- [ ] Protected runtime/game files remain hash-stable.
- [ ] Game remains running and usable after scan.

## Remaining World Scan semantic gaps

- [ ] Recover exact current resource **gather-end time**. Do not misuse `expireTime` as gather completion.
- [ ] Improve small unresolved player-detail rows after one bounded retry if an authoritative route can be recovered.
- [ ] Improve unresolved resource-detail rows after one bounded retry if an authoritative route can be recovered.
- [ ] Finish season-specific/special point normalization without guessing enum meanings.
- [ ] Verify map live-delta/status handling in the rebuilt WebView after repeated scans.
- [ ] Verify `Locate in Game` moves the camera only when explicitly requested by the user, not during normal scan.
- [ ] Verify filter/search/details behavior with real mixed-category output.

## Completion gate

World Scan is complete only when a current-build full-world scan returns complete useful target categories with **zero visible scan-time camera movement**, keeps the strict restoration/evidence contract, and the desktop shows progress/results/Locate correctly.

---

# 3. P0 — Generic recovered command bridge: live end-to-end proof

The generic bridge is implemented, but transport wiring must not be confused with semantic proof.

## Housekeeping before new bridge testing

- [ ] Check whether Last War is running.
- [ ] Inspect `%LOCALAPPDATA%\LastWarControl` bridge health and heartbeat.
- [ ] If still present, remove **only** the known stale read-only test command `ui-readonly-20260907084901060-460d057db84a46e5959103a0cf106cc3` and only its own artifacts.
- [ ] Preserve unrelated queue/runtime state.

## Read-only bridge acceptance

- [ ] Use a recovered read-only command first, preferably `auto_join_rally` state/battle-squad view.
- [ ] Verify host creates a unique command ID.
- [ ] Verify one pending slot is written with correct schema/feature/arguments.
- [ ] Verify game runtime consumes exactly that slot.
- [ ] Verify correlated result is written for the same ID.
- [ ] Verify desktop receives/classifies the result.
- [ ] Verify pending/running/result UI state transitions correctly.
- [ ] Verify duplicate command IDs are rejected.
- [ ] Verify stale/missing heartbeat blocks enqueue.
- [ ] Verify queue-full behavior fails closed.

## Gameplay bridge acceptance

- [ ] Select one low-impact feature with a fully understood current contract.
- [ ] Prove exactly one action through WebView -> host -> file bridge -> game runtime -> correlated result -> WebView.
- [ ] Confirm no blind retry after ambiguous commit.
- [ ] Confirm result adapter uses authoritative effect evidence rather than only `transport succeeded`.

## Completion gate

The generic bridge is CURRENT-PROVEN only after at least one read-only and one bounded gameplay path complete end-to-end with correlated game-side evidence.

---

# 4. P1 — Recovered WebView host parity and result adapters

## Result handling

- [ ] Compare every special result-handling branch in original `MainForm.cs` against `ReferenceWebViewForm.cs`.
- [ ] Recover richer per-feature status/error mapping where the original did more than generic success/failure.
- [ ] Ensure recovered game errors map to useful user-facing states.
- [ ] Preserve queued/running/awaiting-evidence/succeeded/failed/blocked/cancelled/paused states where applicable.
- [ ] Do not display “Succeeded” merely because a command was accepted into the queue.

## Automation runtime-state reconciliation

- [ ] For every continuous feature, distinguish local configured `enabled` from actual game-side running state.
- [ ] Refresh actual runtime state when UI opens and after start/stop commands.
- [ ] Recover state after desktop restart while game remains running.
- [ ] Handle game restart/disconnect without leaving false “running” UI state.

## Map live/delta/status behavior

- [ ] Compare original `MapScanProgressReader`, `MapScanLiveDeltaReader`, and related host logic with current adapters.
- [ ] Verify sequence/delta gap detection and resync.
- [ ] Verify clear/stop semantics do not silently discard evidence.
- [ ] Verify repeated scans replace/merge UI data exactly as intended.

## Shield display / hotkey host behavior

- [ ] Verify shield-display result parsing and remaining-time clock source.
- [ ] Verify Space hold/release behavior.
- [ ] Verify overlay clears when target/state becomes invalid.
- [ ] Verify F6/F7/F8 item-use hotkeys remain disabled unless exact matching shield-item action is proven.

## Auto reconnect

- [ ] Compare original reconnect state machine with rebuilt timer behavior.
- [ ] Distinguish game closed, launcher running, game starting, runtime connected, and runtime stale.
- [ ] Verify no restart loop.
- [ ] Verify reconnect does not overwrite ambiguous protected runtime state.
- [ ] Verify successful reconnect refreshes runtime/UI state.

## Start Game

- [ ] Revalidate current host flow against recovered `LaunchGameAsync()` whenever startup code changes.
- [ ] Continue launching official `LastWarLauncher.exe`, not `LastWar.exe` directly.
- [ ] Require fresh runtime heartbeat before claiming ready.
- [ ] Preserve fail-closed behavior for partial/ambiguous runtime install state.

---

# 5. P1 — Recovered command modes, arguments, defaults, and configuration parity

`RecoveredFeatureCommandArguments.cs` maps several high-configuration features, but all mappings need an evidence audit.

## Global argument audit

- [ ] Build a machine-readable table: `feature_id -> UI action -> mode -> arguments -> default -> range -> source evidence`.
- [ ] Recover the original helper that converts WebView `qR(...)` actions into bridge command arguments for every feature.
- [ ] Compare current host-generated arguments string-for-string where possible.
- [ ] Remove guessed defaults when the original/current contract can be recovered.
- [ ] Confirm snake_case wire names and enum/string normalization.
- [ ] Confirm omitted fields remain omitted when original semantics distinguish missing from false/zero.

## High-configuration mappings already present and needing audit

- [ ] `auto_attack`
- [ ] `use_stamina_item`
- [ ] `auto_radar`
- [ ] `auto_join_rally`
- [ ] `daily_free_claims`
- [ ] `troop_promotion`
- [ ] `alliance_train`
- [ ] `hospital_heal`
- [ ] `apply_position`

## Safety/default audit

- [ ] Confirm retry counts and operation timeouts.
- [ ] Confirm start/stop `enabled` semantics.
- [ ] Confirm premium-currency/ticket/speedup/resource-purchase defaults remain false where recovered.
- [ ] Confirm stamina-item usage never becomes implicit where the recovered feature disables it.
- [ ] Confirm maximum-actions-per-run bounds.

---

# 6. P1 — Equipment schemes and team-swap parity

## Already wired

- [x] Local scheme store exists.
- [x] Save/capture/apply plumbing exists.
- [x] Active-team reconciliation code exists.

## Remaining work

- [ ] Recover exact original scheme slot/team assignment semantics.
- [ ] Verify hero/equipment identity fields against real current game output.
- [ ] Verify capture from real squad state and persistence across desktop restart.
- [ ] Verify Apply Scheme sends exact intended assignment only.
- [ ] Verify success requires authoritative post-state equality, not command acceptance.
- [ ] Verify partial/missing equipment is reported rather than silently substituted.
- [ ] Verify duplicate equipment/hero assignment constraints.
- [ ] Verify Alt+1..4 hotkeys use the same apply contract and foreground gate.
- [ ] Verify failure clears stale “active scheme” indicators.

---

# 7. P1 — Gameplay and menu hotkeys

Recovered groups: Q/W/E/R attack squads 1..4; A/S/D/F recall squads 1..4; Space shield countdown; F6/F7/F8 use 8h/12h/24h shield; Alt+1..4 equipment schemes; F9 random teleport.

- [ ] Verify hotkey persistence and foreground game-window gate.
- [ ] Verify key-down/key-up debouncing.
- [ ] Verify holding a key cannot enqueue repeated consumable actions.
- [ ] Verify attack requires a valid target contract.
- [ ] Verify recall requires an active march for selected squad.
- [ ] Verify Space is display-only.
- [ ] Verify shield-use hotkeys require matching item/current contract.
- [ ] Verify equipment hotkeys require valid saved scheme.
- [ ] Verify F9 remains guarded because it consumes a teleport item.
- [ ] Verify hotkeys do not fire while editing desktop text/config fields.

---

# 8. P1 — Daily Free Claims: complete all seven recovered adapters

Current status: **PARTIAL**. Daily Task is the only current-build implemented/proven category.

## Recovered categories

1. Daily Task chests/tasks
2. Weekly Task chests
3. Store free packs
4. VIP rewards
5. Login rewards
6. Tavern free recruit
7. Idle rewards

## Global contract

- [x] Recovered policy is free-only.
- [x] Premium currency, advertisements, tickets, and background mode are disabled by recovered config.
- [ ] Recover exact adapter order/priority.
- [ ] Recover exact `enabled_adapter_ids` / `blocked_adapter_ids` behavior.
- [ ] Recover `prefer_expiring_rewards` / `prefer_task_chests` semantics.
- [ ] Prove `stop_on_unknown_cost` for every adapter.

## Daily Task

- [x] Current snapshot contract recovered.
- [x] Current explicit task claim live-proven.
- [x] Current chest claim path has current proof.
- [x] Persistent Daily Task runtime exists.
- [ ] Finish diagnostic response/push attribution for multi-task changes after one claim.
- [ ] Integrate any missing recovered UI status/details beyond `Daily Task only`.

## Weekly Task chests

- [ ] Recover current manager/state and claimable predicate.
- [ ] Recover exact claim call and authoritative post-state.
- [ ] Implement one bounded target and live-prove no-cost claim.

## Store free packs

- [ ] Recover current store product identity and authoritative zero-cost representation.
- [ ] Reject unknown/missing cost.
- [ ] Recover exact claim/purchase call and verify reward/store post-state.
- [ ] Live-prove one explicitly zero-cost pack.

## VIP rewards

- [ ] Recover current VIP manager/state/free predicate.
- [ ] Recover exact claim call and post-state.
- [ ] Live-prove one bounded free claim.

## Login rewards

- [ ] Recover current login-calendar/event manager.
- [ ] Separate normal free reward from paid/event upsells.
- [ ] Recover exact claim call and claimed-day transition.
- [ ] Live-prove one bounded free claim.

## Tavern free recruit

- [ ] Recover current tavern/recruit manager.
- [ ] Distinguish free cooldown recruit from ticket/premium recruit.
- [ ] Recover exact free predicate and recruit call.
- [ ] Verify no paid currency/ticket loss and live-prove one free attempt.

## Idle rewards

- [ ] Recover current idle/campaign reward manager.
- [ ] Distinguish free base claim from any enhanced/paid claim.
- [ ] Recover exact claim call and verify reset/advance state.
- [ ] Live-prove one bounded free claim.

## Completion gate

- [ ] All seven adapters have current read state + cost/eligibility proof + bounded action + post-state verification.
- [ ] Full recovered `run_once` can be safely enabled.

---

# 9. P1/P2 — 42-feature completion matrix

Default status below is **RECOVERED+WIRED / live proof still required** unless a stronger status is stated.

## Map & Data

### 1. `map_scan` — World Scan — CURRENT-PROVEN, enhancement open
- [x] Full v9 live scan proven.
- [ ] Replace 500 camera views with equivalent no-camera monster/march capture.
- [ ] Close remaining semantic/detail unknowns in Section 2.

## Squads & AFK

### 2. `continuous_gathering`
- [ ] Recover/verify resource selection/cycling and reserved-idle-squad policy.
- [ ] Prove Status and one bounded Gather Once dispatch.
- [ ] Prove Start/Stop scheduling/restart reconciliation and no duplicate dispatch.

### 3. `quick_attack`
- [ ] Prove read-only target/squad state.
- [ ] Recover current target-under-pointer/current-target identity path.
- [ ] Prove Q/W/E/R attack and A/S/D/F recall mappings with foreground/debounce gates.

### 4. `secret_mobile_squad`
- [ ] Recover current dispatch-task manager and eligibility.
- [ ] Prove Open Dispatch Tasks, one dispatch, one completed claim.
- [ ] Reject unknown hero/slot requirements.

### 5. `zombie_gold`
- [ ] Recover zombie target discovery/type mapping.
- [ ] Prove Zombie attack, Gold Once, Status, and Start/Stop Auto Gold.

### 6. `auto_join_rally` — current read-state research advanced; join unproven
- [x] Current Alliance War/formation manager read paths recovered.
- [x] Rally refresh/list snapshot live-observed.
- [x] Boss Rally identity/display/occupancy semantics substantially recovered.
- [ ] Recover authoritative target taxonomy for unknown types.
- [ ] Recover selected-formation `MarchSeconds` source.
- [ ] Implement/recover `110204` warning handling.
- [ ] Live-prove exact join transport + authoritative membership/formation post-state.
- [ ] Prove multi-Rally ordering and Start/Status/Stop behavior.

### 7. `region_jump`
- [ ] Prove current server refresh, target validation, one bounded Jump, Return Home, and server confirmation/error handling.

### 8. `team_swap_outfit`
- [ ] Complete Section 6; prove Swap Gear, Save Scheme, Apply Scheme.

### 9. `random_teleport`
- [ ] Recover random-teleport item eligibility/current item semantics.
- [ ] Prove F9 consumes exactly one intended item and position changes.
- [ ] Recover/prove Alliance Teleport separately.

### 10. `hospital_heal`
- [ ] Verify wounded/queue state and `max_soldiers` mapping.
- [ ] Prove one bounded heal and queue/wounded/cost post-state.

### 11. `auto_reconnect`
- [ ] Complete reconnect host parity.
- [ ] Prove controlled closed/disconnected -> launcher -> game -> fresh-runtime recovery.
- [ ] Verify Stop prevents restart.

## Automation / Daily

### 12. `auto_mail_claim`
- [ ] Recover current mail manager, unread/read/attachment eligibility.
- [ ] Prove read + one valid attachment claim and reward post-state.

### 13. `auto_radar`
- [ ] Audit config arguments/defaults.
- [ ] Recover task type/rarity/expiry/distance/stamina semantics.
- [ ] Prove read-only probe and bounded Run Once.
- [ ] Separately prove claim/dispatch/combat task subtypes and Pause behavior.

### 14. `auto_truck`
- [ ] Recover truck/cargo/guard managers, eligibility, and selection priority.
- [ ] Prove one departure; verify no implicit premium reroll/purchase.

### 15. `camp_armored_reward`
- [ ] Recover camp and armored reward states.
- [ ] Prove each claim subtype and already-claimed behavior.

### 16. `alliance_train`
- [ ] Audit interval/reward-priority config.
- [ ] Recover queue/board/observe/claim model.
- [ ] Prove Queue, Board, Observe, Claim and reward post-state.

### 17. `auto_train`
- [ ] Recover training queue and next-unit selection policy.
- [ ] Prove completion claim/next training start.
- [ ] Verify resource bounds and no implicit speedup/premium use.

### 18. `troop_promotion`
- [ ] Audit barracks/tier/reserve/limit arguments.
- [ ] Recover authoritative promotion cost/eligibility.
- [ ] Prove direct vs one-tier-step and one bounded promotion.
- [ ] Verify troop/resource post-state and Pause behavior.

### 19. `apply_position`
- [ ] Audit `position_id` mapping and current availability state.
- [ ] Prove one application and appointment/application post-state.

### 20. `alliance_gift_claim`
- [ ] Recover gift/chest managers.
- [ ] Prove one normal gift and one chest claim separately.

### 21. `use_stamina_item`
- [ ] Audit threshold/prefer-50/recovery arguments and current item IDs.
- [ ] Prove exact inventory decrement + stamina increment and threshold no-repeat behavior.

### 22. `auto_reward_collect`
- [ ] Enumerate included reward sources and avoid overlap with Daily Free Claims.
- [ ] Prove each subtype or restrict to proven subtypes.
- [ ] Verify Collect All is bounded and free of paid actions.

### 23. `daily_free_claims` — PARTIAL
- [ ] Complete Section 8.

### 24. `auto_attack`
- [ ] Audit search/team/event config.
- [ ] Recover target discovery/eligibility.
- [ ] Prove Attack Once and Start/Stop repeated behavior with no duplicate march.

### 25. `auto_rally`
- [ ] Recover rally-creation contract distinct from join.
- [ ] Recover formation/duration/defaults.
- [ ] Prove Create Once and Start/Stop; verify Rally exists in authoritative state.

### 26. `auto_chat`
- [ ] Recover channel/send contract and limits.
- [ ] Prove Send Once with exact text/channel and no duplicate retry.

## Automation / Event

### 27. `fireworks`
- [ ] Recover item/event eligibility.
- [ ] Prove one use and inventory/event/alliance-point effect.

### 28. `red_packet`
- [ ] Recover packet list/eligibility.
- [ ] Prove one claim and chat/server reward effect.

### 29. `golden_egg`
- [ ] Recover egg queue/claimability.
- [ ] Prove one open and queue/reward effect.

### 30. `treasure_hunt`
- [ ] Recover dig-site discovery/target types and spend-limit semantics.
- [ ] Prove Run One Cycle, Start/Stop Auto Dig, Refresh Status, Claim Dig Reward, Fragment Dig.
- [ ] Verify site/reward post-state and fail closed on unknown cost.

### 31. `plane_mission`
- [ ] Recover/verify Business Center navigation and state probe.
- [ ] Recover takeoff eligibility.
- [ ] Prove Take Off Once and Start/Refresh/Stop Auto Takeoff.
- [ ] Keep unsupported independent claim/dispatch/reward-priority behavior disabled.

### 32. `double_reward_tracker`
- [ ] Recover multiplier-window state and activity mappings.
- [ ] Prove Check Multiplier/read-only and Start/Stop tracker.
- [ ] Never infer unsupported activities.

### 33. `arms_race_alliance_duel`
- [ ] Recover event tasks/scores/windows.
- [ ] Prove Check Events/read-only.
- [ ] Verify exact training/promotion mappings only.
- [ ] Prove Run Smart Once + score/progress growth and Start/Stop Smart Mode.

### 34. `mining_dispatch`
- [ ] Recover mining target/formation state.
- [ ] Prove one Dispatch and one Recall with march post-state.

### 35. `ghost_scout`
- [ ] Recover personal ghost task state.
- [ ] Prove Start Task and Claim Reward with task/reward post-state.

## Automation / Alliance

### 36. `alliance_help`
- [ ] Recover building/technology/healing help request types.
- [ ] Prove Help Once and Start/Stop; verify request removal/effect.

### 37. `alliance_tech_donate`
- [ ] Recover eligible technology, donation cost, remaining attempts.
- [ ] Prove one bounded donation and contribution/resource post-state.
- [ ] Prove Start/Stop without overspending.

### 38. `resource_grab`
- [ ] Recover transport/resource target discovery and attack eligibility.
- [ ] Prove Grab Once, camera/target evidence where required, and resulting march state.

### 39. `shield_display`
- [ ] Complete shield result/overlay host behavior.
- [ ] Recover authoritative shield remaining-time source.
- [ ] Prove Show/Refresh on shielded and unshielded states when available.

### 40. `performance_overlay`
- [ ] Recover original in-game FPS and PING sources.
- [ ] Prove Toggle FPS/PING on/off and restart cleanup.

### 41. `alliance_ghost_scout`
- [ ] Recover alliance ghost recommendations/eligibility.
- [ ] Prove Assist Once and assistance/task transition.

### 42. `secret_task`
- [ ] Recover secret-task manager and free-refresh state.
- [ ] Prove Free Refresh only when authoritatively free.
- [ ] Prove Dispatch One and Claim One with task/hero/reward post-state.

---

# 10. P1 — Data snapshot / state-view parity

- [ ] Audit `GameDataSnapshotBuilder` against recovered state-view adapters.
- [ ] Verify every exposed data view has current authoritative source.
- [ ] Return explicit unavailable/unknown when missing-manager state differs from a genuine empty result.
- [ ] Validate real snapshots for squads/formations, Rally/battle squads, map/world, equipment, alliance/event state.
- [ ] Keep snapshots bounded in size and versioned where needed.

---

# 11. P1 — Persistence and restart behavior

- [ ] Verify feature config, language/theme, gameplay hotkeys, and equipment schemes persist correctly.
- [ ] Reconcile automation enabled state with game runtime rather than blindly restoring local state.
- [ ] Verify stale command/result files cannot resurrect completed actions.
- [ ] Verify desktop restart while game remains open does not duplicate active automation.
- [ ] Verify game restart while desktop remains open refreshes bridge/runtime state.

---

# 12. P1 — Error handling and fail-closed behavior

- [ ] Inventory original game-side error strings per feature and map known current errors to useful UI status.
- [ ] Unknown errors remain errors; never reinterpret them as success.
- [ ] Timeout after a possibly committed action is ambiguous/needs-state-refresh, not an automatic retry.
- [ ] Consumable/item/currency actions require explicit pre-state and post-state proof.
- [ ] Continuous automation pauses/stops safely when manager/runtime state disappears.
- [ ] Queue corruption, duplicate IDs, ledger conflicts, stale heartbeat, and version mismatch fail closed.

---

# 13. P2 — Reverse-engineering completeness audit

- [ ] Diff current `ReferenceWebViewForm` responsibilities against recovered original `MainForm.cs` method-by-method.
- [ ] Inventory all original feature-specific result adapters, timers/schedulers, keyboard hooks, settings fields, bridge health fields, equipment behavior, map progress/delta/focus behavior, reconnect/startup behavior, and command helpers.
- [ ] Inspect original embedded Lua modules for every 42-feature action mode.
- [ ] Where current game differs, inspect current content-v12 bytecode before adapting.
- [ ] Keep a source/evidence pointer for every recovered fact so future game updates can be revalidated.

---

# 14. P2 — Rare live-state testing strategy

Some features cannot be proven on demand because the account/world must contain a specific Rally, reward, item, wounded troop, event, etc.

- [ ] Build read-only probes first so the app can detect when testable state exists.
- [ ] Save bounded snapshots/evidence when rare state appears.
- [ ] Perform only the minimum action required to prove the contract.
- [ ] Mark unavailable-state tests **BLOCKED-RUNTIME**, not failed implementation.
- [ ] Retain exact pre/post snapshots for offline regression.

Examples: joinable Rally; alliance train/gift/red packet/golden egg; wounded troops; free store/VIP/login/tavern reward; active shield; eligible position; ghost/secret task state.

---

# 15. P2 — Build, smoke, regression, and evidence after implementation batches

## Required release gate

- [ ] `dotnet build src/LWControl.Desktop/LWControl.Desktop.csproj -c Release`
- [ ] Core checks.
- [ ] Relevant Python unit/source tests.
- [ ] Legacy smoke if shared host/catalog changed.
- [ ] WebView smoke.
- [ ] Confirm 42 unique feature descriptors.
- [ ] Confirm recovered HTML hash unchanged unless intentionally updating preserved artifact.
- [ ] Confirm `pendingExecutionEnabled = 0`.
- [ ] Review enabled recovered controls against actual proof status.
- [ ] `git diff --check`.
- [ ] Update evidence docs.

## Evidence record for each completed live feature

- [ ] Date/time + current game/content version.
- [ ] Feature/action/mode and arguments.
- [ ] Precondition snapshot.
- [ ] Command/result ID.
- [ ] Game-side effect evidence.
- [ ] Post-state snapshot.
- [ ] Retry count and timeout/error behavior.
- [ ] Restoration/cleanup evidence.
- [ ] Final classification: CURRENT-PROVEN / PARTIAL / BLOCKED-RUNTIME / UNKNOWN.

---

# 16. Recommended execution order

1. [ ] **Camera-free World Scan research** — recover the no-camera full monster/march route.
2. [ ] **Bridge housekeeping + read-only E2E validation** with a running game.
3. [ ] **Generic bridge/result-state correctness** — prove one bounded gameplay path.
4. [ ] **Original MainForm host-parity audit** and fill missing adapters.
5. [ ] **Command mode/argument/default audit** for all 42 features.
6. [ ] **Hotkey/equipment/shield/reconnect host features**.
7. [ ] **Complete Daily Free Claims seven adapters**.
8. [ ] **Live-prove features by category**, starting with read-only/low-impact states.
9. [ ] **Capture rare-state proofs opportunistically** when live game presents them.
10. [ ] **Final 42-feature status matrix** with no “wired = complete” entries.
11. [ ] **Full release regression + documentation**.

---

# 17. Final project completion criteria

- [ ] All 42 recovered features/actions are present in the UI.
- [ ] Every action is CURRENT-PROVEN or explicitly disabled with a documented current-build blocker/unknown.
- [ ] No button is enabled solely because recovered transport exists.
- [ ] World Scan completes full useful coverage without visible scan-time camera jumping.
- [ ] Daily Free Claims covers all seven recovered free-reward adapters safely.
- [ ] Continuous automations correctly survive/reconcile desktop/game restarts.
- [ ] Hotkeys, equipment schemes, shield display, reconnect, map status/deltas, and result adapters match recovered host behavior.
- [ ] All consumable/resource actions have strict cost/eligibility/effect proof.
- [ ] Unknown current-game semantics remain unknown rather than guessed.
- [ ] Final Release build and full smoke/regression suite pass.
- [ ] Final 42-feature evidence matrix documents exactly what is CURRENT-PROVEN and the evidence used for each feature.

