# FEATURE-RECOVERY-018 lead audit — 2026-10-17 (source 8d99f358, draft PR #14)

Verdict: **NOT ACCEPTED — PARTIAL, no live proof.** Inventory, City/Hotkey/Mini Game bridge code and offline checks
exist (lead re-ran Desktop.Checks incl. FEATURE018_* and UI checks: pass), but no feature was proven in the game.

## Live blocker resolved by lead
`%LOCALAPPDATA%\LWBridgeRebuild\overview-bridge\recovery.json` (profile `local-9370d887…`, game PID 31476, stage
`active_ready_deferred_restore`, 2026-10-16T17:49Z) came from the **owner's own app session**; the app was closed
without restoring. Installed lwScripts were still the bridge candidate (data sha ec8f2c38…). With no game/app
running, the lead restored the three files from the journal's backup
(`overview-bridge-backups/20261016-174910-1d5efc208d2a48db937cfde18eaec76c`), verified original hashes
(data 49f6249d…, metadata 52d5b30c…, version 5f9c4ab0…) and archived the journal as
`recovery.lead-restored-20261017.json`. Live admission is unblocked.

## R2 assignment — same branch/PR #14
0. LEAD018-01: find why the owner's app exit left the game on modified scripts (stage
   `active_ready_deferred_restore`); fix so closing the app (or the next app start) always restores originals,
   with a regression check.
1. Run the live sessions now (sub-account authority, record UID). For every feature in the inventory: make it
   work and prove it in the real packaged app with before/after game state — City snapshot/plan/apply moves
   (incl. cancel), all Hotkeys (Q/W/E/R attack + speedups, A/S/D/F recall, Space shield countdown, F6/F7/F8
   shields, Alt+1–4 equipment, F9/F10 relocation), Mini Games (G reinforcements, Black Market marker, Land Cell
   unlock, Food House solver). Items/gems/relocation/attacks may be used on this account.
2. Hotkey proof without stealing the owner's focus: deliver keys to the task-owned game window only (e.g. window
   messages to that HWND), or bring only the task-owned game window forward. Never touch owner windows.
3. A feature may end NOT_POSSIBLE only with a live-tested reason (not "no provider yet").
4. Final ZIP from the final commit, report update, status READY_FOR_LEAD_AUDIT or PARTIAL.

## R2 audit — source c12bc5d0 (lead)
Worker correctly refused all gameplay: the logged-in account was UID 3133546747002289 on server 2289, while the
lead's authority note wrongly named 2212. Owner confirmed 2289 is the sub-account and 2212 the main account. The
authority doc is corrected. No action was performed on any account; four live sessions restored original scripts.
LEAD018-01 (exit restore) fixed in 0982279e. Status remains PARTIAL — no live feature proof yet.

## R3 assignment — same branch/PR #14
Run the live proof on the sub-account (2289, UID 3133546747002289) now. Replace every server-2212 action gate with
the exact 2289 UID+server gate; keep 2212 read-only. For each inventoried feature: real before/after proof, or
NOT_POSSIBLE only with a live-tested reason. Resolve SOURCE_GAP items (Q/W/E/R cursor target, F6/F7/F8 shield
items, Alt+1-4 equipment, diamond speedups) by live experiments on the sub-account rather than static analysis.
Final ZIP, report, status READY_FOR_LEAD_AUDIT or PARTIAL.

## Lead status check — 2026-10-17 evening (source 81b4f13f, owner stopped the worker)
- Live-accepted so far (2289 sub-account only): City single-building move + restore (`...9726`), Land Cell 6 unlock,
  equipment preset apply + restore, 8-hour shield bought with in-game gold and used. Main 2212 untouched.
- Last live session: `native-ui-20261017-083453` (16:34 local) failed before game start with `WinError 5` renaming
  `recovery.json.tmp-*` inside the OneDrive-hosted artifacts folder. After that, rounds R5–R11 (62 pushes total)
  were offline hardening only; no new live proof.
- LEAD018-02: a one-off OS/filesystem error is **not** an owner or tool denial. Retry it; if it repeats, run the
  owned isolated test roots outside OneDrive (e.g. `%LOCALAPPDATA%\LWControl-runs\`). The lead authorizes that
  location change; it does not modify ACLs or bypass any security control. Use `npm.cmd`, not `npm.ps1`.
- LEAD018-03 (owner): commit locally as often as needed but **push only at checkpoints** (one finished feature or
  round), not every commit.
- LEAD018-04: stop adding further offline "fence/guard" layers without a live test. Next round is live-first: each
  remaining feature gets a real 2289 attempt before any more code hardening.

## R12 assignment — G key troops first (owner priority), same branch/PR #14
Owner wants the original "press G to add troops while clearing stages" working first so they can use it.
1. Behaviour: with the G switch on (Hotkeys/Mini Games page), each **physical G press in the game window** during a
   Frontline Breakthrough / 關卡 stage run (the owner's screenshot: 關卡 5 bridge runner) adds 5 soldiers, same as the
   original 0.3.17. No confirmation popup per press. Not limited to the Sunday challenge: any stage run where the
   game itself allows adding members. Outside a stage run, G does nothing.
2. Prove live on the 2288 sub-account in the packaged app: before/after unit count for several presses, stage run
   continues normally; screenshot.
3. Then stop and hand back (owner tests G before the rest). Push once at this checkpoint.
After owner OK: continue the live-first list from the "Lead status check" section (skip shields, Black Market,
Food House). Commit locally freely, push only at checkpoints. One-off OS errors are not denials (LEAD018-02).

## R12 result (lead) — 2026-10-18
Owner tested G in a normal 關卡 stage: WORKS. Lead added owner-configurable soldiers per G press (1–50, default 5) in
1dcb94c0; tests pass; owner-test ZIP `artifacts/feature018-g-1dcb94c0/`.

## R13 assignment — same branch/PR #14 (owner priorities)
1. **Any account (product rule).** The owner wants these features on whatever account they use. Remove the fixed
   UID/server gates (2553945035002288 / 2288, and any 2289/2212 constants) from product code: C# hotkey session
   admission, Feature018OwnedGameExchange, native providers, overview bridge heartbeat, and all feature018 Lua
   (`hotkey_actions`, `attack_actions`, `equipment_actions`, `mini_game`, City, relocation). Replace them with a
   **same-account fence**: record the logged-in UID/server when an action starts and abort if it changes before the
   action finishes. Keep the worker **test rule** separate: workers' live proof runs still act only on the 2288
   sub-account and never on 2212 — that check lives in the proof harness only, not in the shipped app.
2. **Q/W/E/R attack like the original.** Original: hover the mouse over a target on the world map (enemy city, ally
   city reinforcement, monster, resource node, city ruin; skip world bosses and rally monsters) and press Q/W/E/R to
   send squad 1/2/3/4 there. Implement the real cursor→world-tile mapping (game camera screen-to-world /
   touch-camera raycast for the mouse position in the game window) and the target lookup at that tile; then the
   original march send. If a target popup is already open from a click, using it is fine too. Prove live on 2288:
   hover + Q on a monster and on a resource node → march sent, then A recalls it.
3. Rebuild the owner-test ZIP (README must describe the configurable G count and any-account use). Push only at
   checkpoints. Report back for owner test before continuing other hotkeys.

## R13 implementation and native cursor evidence (in progress, 2026-10-17 UTC)

Checkpoint `2fc682c1f79772431074f2837ffb447bf4859cf0` was pushed on the existing draft PR #14 branch,
with matching `origin/codex/feature-recovery-018`. The shipped C# Hotkey/Exchange providers, Overview producer
admission and Feature018 bridge, native Hotkey/shield, equipment, relocation and Food House Lua now accept an
authenticated numeric UID and current agreeing native source/self/current server, pin the originating account
across native callbacks and server replies, and reject request UID drift. The C# exchange stamps the observed
authenticated UID into every native mailbox request, even City and Mini Game requests, avoiding an unbound
command path. Fixed 2288 eligibility remains in the dedicated worker proof harness ONLY. Standalone C# Desktop
checks, feature-specific Lua53 source and inverse checks, front-end production build/fingerprint and mounted UI
checks all passed. Remaining City, Mini and attack-source changes are being reviewed separately.

Installed v26 managed `Assembly-CSharp.rdl` SHA `bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`
exposes `WorldScene.GetRaycastHitMarch(Vector3)` token `0x06003ad1`, whose `WorldInputManager.GetRaycastHitMarch`
token `0x0600305f` raycasts a `WorldArmy` collider from the supplied screen point and decodes `March_<i64>`;
the xLua wrapper token is `0x06008d32`. Source `WorldTroop.GetMarchUUID` token `0x060027cf`,
`WorldTroop.IsMonsterTroop` `0x06002793`, `IsWorldBossTroop` `0x06002797`, and native manager
GetMarch/GetTroop independently resolve exact IDs. This is the first located current-screen world-troop hit
identity, beyond the historical `curTouchPoint` last-click field. Ground point uses original
`WorldScene.GetTouchPoint(Vector3)` and `WorldToTile`/`TilePosToIndex`. Original v26
`UIUtil.OnClickWorldTroop(marchUuid)` and `UIWorldPointBtn` `MarchUtil.OnClickStartMarch(ATTACK_MONSTER,...)`
are the normal monster path; resource `OnClickWorld` and `MarchTargetType.COLLECT` use
`World.GetResourcePointInfoByIndex`. These are method/IL/source observations, not yet live attack proof.

A genuine isolated v26 staged session at `artifacts/map014-takeover/native-ui-20261017-150524` authenticated
UID `2553945035002288`, server `2288`, and returned a **read-only** native `cursorPoint` receipt:
screen pixel `(600,400)` projected through `WorldScene.GetTouchPoint+WorldToTile+TilePosToIndex` to tile `5069`,
native world position `(136.509765625, 0, 100.52708435059)`, empty world-point classification,
`requestDispatched=false`. The single sampled point has **not** been visually compared with a displayed tile,
nor is it physical hover, target hit, squad send or server ACK. The session exited with
`TaskCanceledException` in staged-proof queue; `run_ui.py` exitCode `2` accurately reports a proof error,
while its independently audited `restorationAudit.state=proven` found all original game scripts identical,
no recovery journal, and zero surviving task-owned processes. The historical failed proof is preserved;
none of Q/W/E/R or A/S/D/F are yet classified as working live. The R13 owner-test ZIP must say so.

## Lead status check and R14 assignment — 2026-10-18
Worker R13 stopped mid-round (disconnected). Pushed: 2fc682c1 (any-account same-account fence in shipped code;
lead spot check: no fixed UID left in `src/`), 0cce49b1 (native hover-target research, one read-only cursor→tile
receipt). Uncommitted worker work remains in 8 files (attack Lua/checks, native providers, proof, live queue):
review it, keep what is correct, commit it — do not discard.

Owner update: sub-account is now **UID 1183884595002296 / server 2296** (see the authority doc 2026-10-18 section).
Mini Games accepted as done. Unlock Land Cell must never be tested again. Space, F6/F7/F8, Alt+1–4 set aside.

R14 tasks, in this order (live first, each proven on 2296 with a real key press in the game window):
1. Move the proof harness/test pins from 2288 to 2296 (harness only; shipped app stays any-account). Add a
   same-alliance check before any attack: never attack own-alliance targets.
2. **Q/W/E/R** hover attack: mouse over a target on the world map + key → squad 1–4 marches there
   (monster, resource node, non-alliance enemy city/troops; skip world bosses/rally monsters). Prove hover+Q on a
   monster and hover+W on a resource node with matching server reply and visible march.
3. **A/S/D/F** recall of those marches (live proof).
4. **March speed-ups** by item and by gem on a live march (exact item/gem change + shorter arrival).
5. **F9** random relocation and **F10** alliance relocation (live, before/after city position).
6. **City Layout:** read the real 2296 city into the grid, move one building and swap two buildings via Apply,
   Cancel mid-apply, then restore the original layout. Screenshots before/after.
7. Owner-test ZIP from the final commit with an honest README (what works / not yet).

Handoff rule (worker may run out of tokens): after every finished task, commit + update the "R14 progress" list
below with what is proven (artifact path) and what is next; push at task checkpoints, not every commit.

### R14 progress (worker updates this)
- Task 1 (2026-10-18): **PROVEN harness admission / OFFLINE guard checks**. Approved proof UID/server and active attack fixtures/City driver now pin `1183884595002296/2296`; product remains any-account. Reviewed and retained all eight inherited files (native error detail, independent moving-troop raycast, zero-soldier refusal, staged timeout). Native target alliance guard rejects same/unknown alliance at hover, selector and final dispatch; empty resources require explicit zero gather UUID. New same/unknown/changed-alliance inverses pass. Land requests are disabled in R14 proof tools. Actual authenticated admission: `artifacts/map014-takeover/native-ui-20261017-174201/feature018-account-admission.json`; read-only inventory operation `331c66a340c449c48ac88454bb29645d` reports squad 1 two heroes/zero soldiers, squad 2 zero heroes/zero soldiers and no unassigned soldiers. This is not a march positive. Next: staff squads through the original game, then physical hover+Q monster and hover+W empty resource with native reply/visible march.
- Task 2 in progress: first R14 live round `native-ui-20261017-174201` identified a **current-client staffing bug**, not lack of real troops. Original level-4 barracks showed 450 troops; normal training of 30 with six 5-minute items raised this to 480. Hero1 was upgraded from level1 to5 using existing experience and its completed quest claimed. Original monster search/chooser then displayed **170/170 troops** with two heroes (`r14-original-chooser-170-troops.png`), while the adapter's legacy scalar/ArmyManager reader still reported zero/unreadable. Current original `ArmyFormationInfo.ConscriptSoldier` stores nested `soldiers[heroSlot].supply`; V2 chooser `BestSelect` performs this original preview automatically. Correcting that preflight before further sends. A real computer-use Q on owned HWND45157928 was captured once, then correctly rejected `Own alliance identity unknown; attack suppressed` (operation `870e10ede218491e952c1d2cedd68b6d`); no march/ACK claimed. Session ended normally; `owned-exit.json` independently proves exact v26 script restoration and no journal. Next: current-client staffing and explicit unaffiliated-owner handling, then new genuine hover Q/W round. Tasks3–7 remain unproven.
