# Treasure: LwTool "Dig Treasure" logic first (2026-10-26)

Branch `codex/lwtool-remaining-tabs` / draft PR #17. Owner direction 2026-10-26: Treasure means the
alliance **radar treasure digs** and **Test Flight Failure** treasures (both `TreasurePointInfo`,
pointType 21, told apart by `eventId`). Treasure is no longer on hold; the owner reports live
opportunities, which cannot be summoned on demand. "Implement LwTool logic first, then edit to our
own preference." World chat is not used.

## What LwTool does (recovered evidence)

LwTool's in-game Lua (`Lw_Core.lua`/`Lw_Api.lua`) is **not** in our recovered copy; its settings,
defaults, UI text, log tags and result codes are. Sources:
`artifacts/lwtool-remaining-tabs-20261025/current-lwtool-observation/recovered-ui.html`
(`makeToggleGroup(t('ft_dig'), 'digReward', …)`, `makeDigOrderPicker`) and `recovered-core.py`
(defaults ~15014-15052, `_kep_click_truoc` ~15272, `_notify_cfg` ~27089, `_treasure_hunt_loop`
~16518, result codes ~27940-27975).

| LwTool setting (key, default) | Meaning | Ours |
| --- | --- | --- |
| Dig Treasure (`digReward`, on) | Claim treasure rewards | Card switch |
| Treasure Hunt (`autoTreasure`, on) | Find digs from alliance location shares | `huntEnabled`: shared tile or within 3 tiles (radius is OWN_DESIGN; LwTool's is unrecovered) |
| Dig Dispatch (`digDispatch`, on) | Send a squad to help dig | `dispatchEnabled`, one squad per dig |
| Order (`digTeamOrder`, weak) | Weakest/strongest squad first | `dispatchOrder`, game's `GetFormationPowerByUuid` |
| Unshared Treasure (`unsharedTreasure`, off) | Scan 30x30 (H=15) around every point share for digs nobody shared | `unsharedEnabled`, camera-free 15-tile area request |
| Do not auto-open treasure chests in my radar (`skipAutoOpenRadarTreasure`, on) | Skip own radar treasure | `skipOwnRadar` (claim only) |
| Random (`randomClaimEnabled`, off; `randomClaimMax`=10) | Not on time | `randomClaimEnabled`: up to 10 s later (unit of 10 is unrecovered; OWN_DESIGN) |
| Click before (`digRewardInterval`, 0 ms, -5000..300) | Negative = later, positive = earlier | `clickBeforeMs`, claim at completion time minus it |
| Dig delay (`digDispatchDelay`, 300 ms, random ±20%) | Wait before dispatching | `dispatchDelayMs` |

Result codes used: `detect_dig_err_01` = claimed before the dig finished (retried every 300 ms for
up to 15 s), `radar_tips_4` = all reward slots taken (counted as `late`), `801348` already claimed,
`801354` other alliance. LwTool's claim is independent of dispatch: our squad need not have joined.

## Owner additions (OWN_DESIGN / OWNER_DIRECTION)

- **Redirect a gathering squad** (`redirectGathering`, on): if no checked squad is at home, a squad
  whose march target is `COLLECT` is redirected with the game's `MarchUtil.SendChangeMarchToServer`
  (the call `MarchUtil.StartMarch` makes at line 1945 whenever the chosen squad already has a march).
  Broken marches and marches inside black tiles are refused, as in the Q/W/E/R redirect. Squads
  doing anything else are never touched. LwTool's logs show `world.march.change` for treasure, but
  its exact rule is unrecovered.
- **Gathering resumes**: Treasure marks a redirected squad in `__lwbridgeTreasureBorrow`; the
  Gathering engine does not apply the manual-resume wait to it and searches again as soon as the
  squad is home (mark expires after 2 h). A redirected engine-owned march keeps its UUID, stays
  "ours" and is not recalled by Gathering Stop because its target is no longer the resource tile.
- Squad checkboxes stay as the allowed set (default all four); ties in power keep the saved order.
- Location shares are accepted as `Text_PointShare` (13) or `Text_PointShare_Alliance` (404), alliance
  chat only, same server; position from `pos` or `x`/`y`.
- The earlier time-of-day claim schedule and "claim only digs our squad joined" rule are replaced;
  their saved values stay readable but are no longer used for Treasure.

## Qualification

Inert, native-shaped: `tests/GameScripts.Checks/feature018_chat_engine_checks.py` (order by power, redirect arguments,
black-tile/redirect-off refusals, hunt 3 vs unshared 15, completion-time claim with Click before,
early-claim retry, late count, Random, own-radar skip, finish-announcement claim without joining),
`tests/GameScripts.Checks/feature018_gather_checks.py` (borrow handoff, no recall of a squad on a dig, stale borrow),
Desktop `ChatRewardConfigChecks` (LwTool defaults, older-save migration, ranges), full UI check.

**Not qualified live.** No live dig, redirect or claim has been observed. Open questions for the
first live opportunity: whether the server accepts a 4x4-block area request, whether a redirected
gathering squad keeps its collected resources, whether `completionTime` is populated while digging,
and real claim timing versus the 20 reward slots.
