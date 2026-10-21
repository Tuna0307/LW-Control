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
- R14 continuation review (2026-10-18): retained the four inherited dirty files from the stopped Codex worker. `feature018_attack_actions.lua` now reads current nested `soldiers[slot].supply` / `totalSoldierNum` staffing and allows an explicitly unaffiliated owner while preserving player-target alliance blocking; `feature018_hotkey_actions.lua` observes the original `WorldMarchChangeMessage` reply without claiming unique nonce causality and requires independent return poststate. Scoped checks pass: `FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS` and `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS`. No new live march is claimed by this checkpoint. Next: diagnose `native-ui-20261017-175459` exact-owner loss, then obtain the first physical hover+Q monster positive on UID 1183884595002296/server 2296.
- R14 owner-loss diagnosis/fix (2026-10-18): `native-ui-20261017-175459` had authenticated UID 1183884595002296/server 2296 and continued native focus replies through 17:57:52 UTC, but cleanup at 17:58:15 found exact game PID 38272 already absent; the run's Player log ends without a managed/Lua crash trace and Windows Application Error/Windows Error Reporting produced no matching event in the bounded window. Isolated recovery was disabled (`autoReconnect=false`, `gameDesiredRunning=false`), so the recovery monitor did not terminate this process. The staged harness now includes exact PID/path/start-time ownership in every owner gate, emits `feature018-owner-loss.json`, immediately distinguishes an unavailable exact process from the existing 15-second authenticated-heartbeat/account grace, and never admits a command during either condition. Release build and the complete Desktop.Checks suite passed, ending `HOME_FEATURE_DELIVERY_CHECKS_OK`. Next: publish this candidate once and obtain the first real physical hover+Q monster march; no further guard work before that attempt.
- R14 first direct physical-Q attempt (2026-10-18): fresh isolated run `native-ui-20261017-181752` admitted exact PID 34244, UID 1183884595002296/server 2296. The ordinary World search centered a level-1 Iron Mine Zombie; a Windows computer-use `Q` was delivered while the physical cursor remained on that monster. This is a **negative**, not a march proof: the target popup stayed unchanged, no visible march appeared, and monster stamina remained exactly **170** (same as the pre-Q base screenshot), so no gameplay success is credited. The bridge remained healthy with exact session/account and `feature018_input_focus inputClear=true`. The staged queue's read-only `observedKeys` request file could not be written because the host tool blocked that filesystem operation twice, so direct-key diagnostics previously trapped only in the process-local queue were not retrievable. A proof-only sink now writes the existing exact-root `Feature018CapturedIntent` sequence plus the existing native action outcome/status/reason to `native-ui-map.json.hotkeys.json` after direct physical-key dispatch in staged proof mode; it changes no capture, admission, target, send, or confirmation logic. Release build, `git diff --check`, and the complete Desktop.Checks suite pass with `HOME_FEATURE_DELIVERY_CHECKS_OK`. Next: let this task-owned session restore normally, then rerun the physical monster Q with the durable outcome receipt and fix only the concrete live rejection it reports.
- R14 direct-Q rejection isolated (2026-10-18): evidence-enabled run `native-ui-20261017-183220` admitted exact PID 42756, UID 1183884595002296/server 2296. A fresh level-1 Iron Mine Zombie was centered with stamina still 170; the Windows cursor was visibly placed on the zombie model before one physical `Q`. Durable capture proves exact HWND 19269800, `AttackSquad` slot 1, capture sequence 1. Native outcome was **Failed before send**: `GAME_PRECONDITION_FAILED: Cursor has no native resource world point`. This establishes that `GetRaycastHitMarch` returned no WorldArmy ID and the resolver incorrectly fell through to the resource-ground branch; no march, stamina use or retry is claimed. Installed v26 IL proves `WorldInputManager.GetRaycastHitMarch(Vector3)` immediately passes its supplied screen point to `WorldScene.ScreenPointToRay` and raycasts the `WorldArmy` layer. The physical ground path already passes Unity's native `Input.mousePosition` Vector3 directly, but `native_hover_target` was rebuilding it through the Lua `Vector3.New` wrapper before the monster raycast. The hover path now preserves the native `Input.mousePosition` object directly; the inert inverse fixture requires object identity and zero physical-path constructor calls. `FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS`, `git diff --check`, and a full canonical Windows publish to `artifacts/feature018/r14-native-pointer/windows` pass. Next: cleanly end this owned session, rerun one physical monster Q with the corrected packaged Lua, and require server/poststate plus visible march before crediting the first positive.
- R14 native-pointer retest / selected-monster fallback (2026-10-18): `native-ui-20261017-183930` admitted UID 1183884595002296/server 2296, exact game PID 30368. The original search centered the same ordinary level-1 Iron Mine Zombie; after placing the real Windows cursor on its rendered model, one physical `Q` was captured on HWND 28904284. The native-pointer fix still returned no WorldArmy march ID, so the durable outcome again failed before send with `GAME_PRECONDITION_FAILED: Cursor has no native resource world point`; no march/poststate is credited. The task-owned run then exited 0 with `restorationAudit.state=proven`, exact v26 originals restored and no journal. Recovered v26 `UIUtil.OnClickWorldTroop` proves the game's own click path resolves the clicked `marchUuid` and opens/refreshes `UIWorldPoint` with that same UUID and native target tile; `UIWorldPointCtrl.InitData` stores `uuid`/`pointId`, and the ordinary monster button later calls `MarchUtil.OnClickStartMarch` with those exact fields. The adapter now prefers the direct raycast but, on a miss, may reuse an already-open native monster popup only when the same physical cursor freshly projects to the popup target tile and the live manager march, world march, troop UUID/type, ordinary template, owner/server, and fresh troop tile all agree twice before opening the formation chooser. Monster/resource targets no longer require alliance identity; player city/army/ruin targets retain the own-alliance fence, with source-proven no-alliance treating every player as non-alliance. Resources still require explicit zero gather occupancy. `FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS`, `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS`, and `git diff --check` pass. Next: publish this popup-target candidate and obtain the first real physical Q march.
- R14 first physical Q reached the original chooser (2026-10-18): popup-target candidate `native-ui-20261017-190333` admitted exact UID 1183884595002296/server 2296, game PID 39920. One physical Q on HWND 5377906 with the cursor visibly on the searched level-1 Iron Mine Zombie produced durable capture sequence 1 and opened the original V2 formation chooser. The live game UI visibly showed squad 1 with two heroes and **170/170 soldiers** (frame-82); no attack packet was sent because the adapter then rejected its own final preflight: `Selected native formation must contain soldiers and assigned heroes before attack`. Source recovery proves that rejection was stricter than v26: `BestSelect` calls `ArmyFormationInfo:ConscriptSoldier()` with no local-hero mode, `HasHero()` is simply `table.count(self.heroes)>0`, and the original `OnCreateClick` runs `CheckCanBattle` before its native sender rather than requiring the adapter's extra `canMarch`/`heroTotalSoldierCapacity` predicate. `formation_staffing` now treats validated positive nested `soldiers[heroSlot].supply` plus assigned `heroes` as dispatch staffing; `canMarch`, capacity, and local-editor hero count remain diagnostics only. Zero/malformed nested soldiers and no assigned heroes still fail. Scoped attack/hotkey suites and `git diff --check` pass. Next: republish this BestSelect staffing fix and repeat the same one-Q monster proof, requiring native send/reply/poststate plus visible march before credit.
- R14 Q produced the first real march effect but direct reply hook missed (2026-10-18): BestSelect candidate `native-ui-20261017-191247` admitted UID 1183884595002296/server 2296, exact game PID 16136. One physical Q on HWND 54527104 sent exactly one original `WorldMarchFormationNew` from formation `1392040810786650129` to monster `1427557225287186660`; the serializer captured request GUID `bf7aa523-05c9-4896-849c-cb4200afbaac`. Live stamina changed **170→160** and the game visibly showed squad 1 marching with a 22-second ETA; `r14-q-visible-march.png` preserves that screen. Native immediate poststate independently exposed the new owned march with the exact matching `clientCreateGuid` and target, but the Lua `HandleMessage` replacement never saw the response, leaving the hotkey result `Submitted`. v26 source proves `WorldMarch.clientCreateGuid` is populated by `WorldMarch.UpdateFromSFS` from response field `clientCreateUuid`, after `WorldMarchFormationNewMessage.HandleMessage` passes the response to `World.HandleFormationMarch`; it is not the locally generated serializer token field. Confirmation now accepts the fresh owned march's exact response-derived GUID + target/state transition as server correlation and records whether the direct diagnostic reply wrapper also fired. Synthetic tests no longer copy the outbound GUID into local march state before a simulated server response. Scoped attack/hotkey suites and `git diff --check` pass. This run is real gameplay evidence but Task 2 is not yet marked complete until a fresh packaged run returns the corrected confirmed receipt.
- R14 Task 2 **PROVEN — physical Q monster + physical W resource** (2026-10-18). Fresh package `native-ui-20261017-192001` admitted exact UID 1183884595002296/server 2296 and returned physical Q slot 1 as **Confirmed**: one `WorldMarchFormationNew`, response-derived `clientCreateGuid` matched the request, exact owner/formation/monster poststate matched, and `r14-q-confirmed-visible-march.png` shows the squad marching. The run exited 0 with exact v26 restoration and no journal. For W, source recovery pinned the original COLLECT tuple: V2 selector stores missing targetUuid as numeric 0, while `MarchUtil.SendCreateMarchToServer` carries the physical resource point in `path=startPoint;targetPoint`; server `WorldMarch.UpdateFromSFS` exposes that endpoint as `targetPos`. The adapter now requires targetUid 0 + exact path endpoint at sender and serializer, then confirms exact response-derived GUID + owned formation + COLLECT kind + `targetPos`. Fresh package/run `native-ui-20261017-200539` admitted exact UID/server and physical W slot 2 returned **Confirmed** for empty level-1 oil point 990604: one native create, path `985554;990604`, GUID `17f2b428-0a55-4d13-a73c-a99dca141d43`, server-correlated poststate, march UUID `1427557227937986565`; `r14-w-confirmed-visible-march.png` visibly shows squad 2 outbound. That run also captured physical S and visibly changed the same squad to `返回`, but the coordinator downgraded the native call to `SessionChanged: game_session_changed_after_native_call`; S is therefore **not yet credited**. The exact-owned run exited 0 with `restorationAudit.state=proven`, original v26 bytes restored and no journal. Scoped attack checks pass; next: fix the post-call recall admission race and obtain confirmed S/A, then speedups.
- R14 recall coordinator race fix (2026-10-18): the authenticated native mailbox already rechecks exact process/session and UID/server immediately before returning a response, but the outer hotkey broker performed one additional instantaneous heartbeat read. A heartbeat rewrite or >5 s freshness edge could make that reader transiently return `null` and erase a valid native result, which matches the live S failure above. The broker now retries only a `null` post-call sample for a bounded 450 ms; any concrete different profile/session/UID/server still fails immediately. A regression reproduces a successful native result followed by two unavailable account samples and requires the original result to survive; the existing explicit account-switch race still fails closed. Attack/hotkey Lua suites, full Desktop.Checks (`HOME_FEATURE_DELIVERY_CHECKS_OK`) and `git diff --check` pass. Next: publish once and obtain confirmed physical S/A on 2296.
- R14 S retest / returning-march lookup defect (2026-10-18): exact package `94eec7d2` run `native-ui-20261017-202629` admitted UID 1183884595002296/server 2296. A fresh physical W slot 2 was **Confirmed** to empty oil point 990604 (march `1427557228353222822`, formation `1427557221701056605`, GUID `1ce1f419-6cef-406f-bc9e-f3636d258b69`). One physical S then visibly changed that squad to `返回` with an 8-second return, and the broker no longer misclassified it as SessionChanged; however the follow-up returned `Submitted / GAME_METHOD_UNAVAILABLE: Native owner formation march identity is missing or mismatched`. This isolates a second defect: v26 removes a BACK_HOME march from `GetOwnerFormationMarch` immediately, while its exact WorldMarch UUID remains readable until home. Query-only recall now revalidates the retained pre-dispatch UUID with `GetMarch`, while still pinning the same Player, DataCenter, WorldMarchDataManager, current formation UUID, UID and server. New regression hides the formation lookup after dispatch and requires the original reply + exact UUID BACK_HOME poststate to confirm; changed manager/account/server remain hard failures. `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS` and `git diff --check` pass. The live run exited 0 with `restorationAudit.state=proven`, exact v26 originals restored and no journal. Next: republish this exact fix and repeat one physical S; no duplicate return is allowed.
- R14 S reply-object diagnosis (2026-10-18): exact `5e8a1b8e` run `native-ui-20261017-203502` again admitted UID 1183884595002296/server 2296. Fresh physical W slot 2 was Confirmed (march `1427557228462274682`, GUID `1bca9a13-ea1a-468a-b42f-34178ca053a5`), and one S visibly showed `返回` with 9 seconds remaining; screenshot `r14-s-return-visible.png`. The final outcome remained Submitted because no matching reply was recorded before the exact march disappeared. Read-only admitted-v26 bytecode and managed IL show why: `WorldMarchChangeMessage.HandleMessage` passes an Sfs2X `ISFSObject` to native `HandleFormationMarchChange`; `ParseFormationUpdate` / `WorldMarch.UpdateFromSFS` read `uuid`, `ownerUid`, `ownerFormationUuid`, `target` and server fields through SFS getters. The observer had treated that object as a Lua table. It now reads native replies with `GetLong/GetUtfString/GetInt` and retains table-field fallback only for inert fixtures. An SFS-getter-shaped regression must correlate the exact march/owner/formation/BACK_HOME reply, and the full hotkey Lua suite still passes. Next: publish the getter fix and require physical S Confirmed + visible return.
- R14 partial WorldMarchChange reply diagnosis (2026-10-18): exact `89a7a1fe` run `native-ui-20261017-204208` admitted the same 2296 owner. Fresh W slot 2 was Confirmed (march `1427557228567132172`, GUID `2d9bd5e6-9aa0-4d65-9007-506ffc65de33`); one physical S visibly changed the squad to `返回`, while the managed result stayed `Submitted / native_action_dispatched_confirmation_pending`. The SFS getter path therefore reached the reply object, but the server change response does not reliably duplicate every unchanged owner/formation/server field. Native `WorldMarchDataManager.ParseFormationUpdate` proves `uuid` is mandatory; the change target is the requested authoritative transition. The observer now requires exact response `uuid` + BACK_HOME, rejects any optional owner/formation/server field that is present but conflicting, and independently revalidates the retained native march UUID/owner/formation plus authenticated UID/server before attributing the reply. Missing duplicated identity fields are filled only from that validated pending march. Regression coverage includes each omitted optional field and the all-omitted combination, while wrong explicit owner/formation/server still fail. `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS` and `git diff --check` pass. Next: publish this protocol-correct matcher and repeat one physical S.
- R14 recall authoritative-poststate rule (2026-10-18): admitted v26 managed source proves `WorldMarch.GetMarchTargetType` reads the real march `target` field and the normal `MarchUtil.OnBackHome/StartMarch/SendChangeMarchToServer` send path does not write that field locally; real-march target writes are server update paths `UpdateFromSFS/UpdateFromProto`. Therefore the exact retained real march changing from a verified non-BACK_HOME prestate to BACK_HOME after exactly one owned recall send is a server-applied effect even when a proto/delta response omits the raw top-level target needed by the diagnostic hook. The query now confirms that transition with `observation=native_world_march_return_server_applied_poststate`, while keeping `exactRequestCausality=false` and `serverAcknowledgementCorrelated=false`; preexisting BACK_HOME, uncertain sends, account/server switches, changed manager/formation/march and foreign explicit reply fields still fail closed. Lua and full Desktop checks pass through `HOME_FEATURE_DELIVERY_CHECKS_OK`. Next: final live S acceptance, then A.
- R14 Task 3 **PROVEN — physical S and A recalls** (2026-10-18). Exact package `6c85a475` run `native-ui-20261017-205511` admitted UID `1183884595002296`/server `2296`: fresh physical W slot 2 first returned Confirmed for march `1427557228789430442`; one captured physical S (RecallSquad slot 2) then returned **Confirmed** with `native_world_march_return_server_applied_poststate`, exact same march/formation/owner/server, prior target `2`, `priorTargetWasBackHome=false`, `requestPreviouslyDispatched=true`, authoritative `WorldMarch.GetMarchTargetType_after_dispatch`, and final `BACK_HOME`. Earlier source-identical S run `native-ui-20261017-203502` preserves visible Chinese `返回` evidence in `r14-s-return-visible.png`; the final 205511 ledger is the durable managed/server-applied acceptance. For A, run `native-ui-20261017-213119` admitted the same UID/server, physical Q slot 1 first created and Confirmed march `1427557229519239210` for formation `1392040810786650129`; captured physical A (RecallSquad slot 1) then returned **Confirmed** on that exact same real march with the same authoritative non-BACK_HOME→BACK_HOME server-applied rule. The 213119 task-owned game process later became unavailable before the harness could capture an exact Stop receipt, so that run's `owned-exit.json` is exit 1; however it independently records all three original script files restored and no recovery journal, and the later clean `native-ui-20261017-214424` run exited 0 with `restorationAudit.state=proven` and the exact admitted v26 hashes restored. No duplicate A/S replay is needed. Next: prove item march speed-up, then implement/prove native gem speed-up.

## Lead audit of Codex R14 partial — 2026-10-18
Codex ran out of tokens. Pushed a936d95c (= local HEAD). Uncommitted: attack Lua/checks + recall reply observer in
`feature018_hotkey_actions.lua`/checks (599+/86-) — next worker reviews, tests and commits it.
- Task 1 ACCEPTED: harness pinned to 1183884595002296/2296, product any-account, alliance fence added.
- Task 2 NOT DONE: one physical Q was rejected by our own guard ("Own alliance identity unknown"), and the adapter
  read zero soldiers while the original chooser showed 170/170. No march sent yet.
- Later run `native-ui-20261017-175459` failed: "Staged proof lost its exact 2296 owner for 15 seconds". Lead checked:
  original scripts restored (hashes match), no journal; `restorationAuditError` only says the Stop proof was missing.
- In-game training/hero upgrade/quest claim by the worker were within owner authority.

Lead direction for the next worker:
1. Alliance rule: monsters and resource nodes need no alliance check. For player targets: if the sub-account's own
   alliance is definitely none (empty/0 alliance id read from the game), every player target counts as non-alliance;
   block only same-alliance or truly unreadable state.
2. Staffing: read soldiers the way the original does (`ArmyFormationInfo.ConscriptSoldier` nested
   `soldiers[slot].supply` / V2 chooser `BestSelect`), not the legacy scalar.
3. Diagnose and fix the 15 s owner loss in 175459 before the next long run.
4. Get the first real positive now: hover + Q on a monster → march visible + server reply. No more guard hardening
   until one march has been sent and recalled.

## Lead audit of CoS R14 run — 2026-10-18 (source fbf6b63a)
CoS stopped. 24 commits, all pushed (local = origin fbf6b63a), working tree clean, no game/app/python processes left.
Lead re-ran: attack, hotkey, city game, city live driver, relocation, diamond rapid, rapid integration and Mini
checks all PASS; full `tests/LWBridge.Desktop.Checks` PASS (`HOME_FEATURE_DELIVERY_CHECKS_OK`).
All 15 live runs since 21:17 restored the exact original game scripts with no recovery journal.

Per task:
- Task 2 Q/W: **ACCEPTED (live)**. Physical hover+Q monster `native-ui-20261017-192001` and hover+W empty oil
  `native-ui-20261017-200539`, both Confirmed by server-response GUID, screenshots `r14-q/w-confirmed-visible-march.png`.
  Repeated in later runs (214424 Q Confirmed).
- Task 3 A/S: **ACCEPTED (live)**. S `native-ui-20261017-205511`, A `native-ui-20261017-213119`, both Confirmed by
  server-applied BACK_HOME on the exact march (not a raw reply match; documented honestly).
- Task 4 speed-ups: **NOT PROVEN**. Code only (fbf6b63a gem rapid path, item path); no live run found.
- Task 5 F9/F10: **NOT PROVEN**. Code only (63d33498); no live run found.
- Task 6 City Layout: **PARTIAL live**. Snapshot of the real 2296 city (33 buildings) read; single moves and swaps
  were sent and server-applied (050340: 3 moves completed); in-flight Cancel proven in `native-ui-20261018-051405`
  (`task6CancelProven=true`). Lead compared that run's before/after snapshots with the original positions from
  220737: **0 of 33 buildings differ — the owner's city is back to its original layout.** Open defects: single-move
  completion often reported CITY_LAYOUT_RESULT_UNCERTAIN (no matching native reply), and 051122 restore preflight
  failed with GAME_ACCOUNT_CHANGED (same transient owner-loss family as 175459/220737).
- Task 7 ZIP: **NOT BUILT** (only packaging script prepared in cc777949).
- Worker did not update the R14 progress list after Task 3; this section is the current status.

Difficulties seen: (1) the bridge intermittently loses the "same account" heartbeat for a few seconds, which
aborts runs or marks results uncertain; (2) the game often applies a move/recall without sending a reply our code
can match, so the worker had to switch to "server-applied after-state" evidence; (3) the game process sometimes
vanished mid-run (211733/213119/213826 "exact owned game process is unavailable"); (4) time spent on extra proof
rigour rather than moving to Tasks 4/5/7.

Next (not yet assigned): speed-ups live, F9/F10 live, fix the transient owner loss, make City single-move report
Completed from server-applied state, owner-test ZIP.

## Lead-built R14 owner-test ZIP — 2026-10-18
Owner asked for a test build to try on their own accounts. Lead found and fixed two leftover 2288 account checks in
the Hotkeys UI (F9/F10 prerequisites panel, G battle status panel) — they would have failed on any other account —
and extended the package gate to scan UI JSX (7ca3d0cd). UI checks pass. ZIP built from pushed 7ca3d0cd:
`artifacts/feature018-r14-7ca3d0cd/LW-Control-FEATURE-018-R14-PARTIAL-7ca3d0cd39db.zip`, SHA-256
9c10c4947cbd66d921a9c1389aa0500b6d652c886eec42727685275af33d3a26, no test UID in any shipped file. README lists
working (G, Q/W/E/R monster+resource, A/S/D/F, Land Cell), partial (City Layout) and untested features.

## Owner test of R14 ZIP and R15 assignment — 2026-10-18
Owner tested ZIP 7ca3d0cd on their own account:
- Hover + Q on a resource: squad marched; A recalled it. **Then a second Q did nothing** (repeat use broken).
- A/S recall works on squads the owner dispatched manually.
- Q on a player city does not work. Owner rule: only guildless players may be attacked.
New test sub-account: **UID 4799432499002212 on server 2212** (see authority doc; lock on UID, not server).

R15 tasks (live on 4799432499002212, real key presses in the game window):
1. Move the proof harness pin to UID 4799432499002212 / server 2212 (harness only, product stays any-account).
   The harness must refuse any other UID on 2212.
2. **Repeat use bug:** Q → march → A recall → wait until home → Q again must send again. Find what stays stuck after
   the first send (one-shot latch, pending/outstanding state, cached chooser, hover pointer, focus admission) and fix
   it. Prove at least 3 cycles of Q→A in one session, plus Q then W (two squads) then A and S, without restarting.
   Also: pressing Q while that squad is already out must give a clear message, not silence.
3. **Player city attack:** hover over a guildless player's city on the world map + Q → squad attacks it (server
   reply + visible march). Use the Map scan to find guildless cities nearby if needed. Guild players must be
   refused with a clear message. Then recall with A.
4. Rebuild the owner-test ZIP with `tools/feature018_r14_package.py` (rename to R15 if you like) from the final
   pushed commit; update the README.
Handoff rule unchanged: after each task commit and update "R15 progress" below; push at task checkpoints.

### R15 progress (worker updates this)
- R15 Task 1 checkpoint (2026-10-18): harness, staged queue and City driver now require exact UID `4799432499002212` / server `2212`; the shipped product retains per-session any-account admission. Managed regression explicitly refuses another UID on 2212, null UID, wrong server and the retired 2296 account. Full Desktop checks PASS (`HOME_FEATURE_DELIVERY_CHECKS_OK`; `artifacts/feature018-r15-task1-checks.log`), current-owner hotkey and diamond Lua checks PASS. Live admission is next, followed by repeat Q/A proof; no R15 gameplay positive is claimed yet.

- R15 Task 1 live admission **PROVEN**: `artifacts/map014-takeover/native-ui-20261018-064255/feature018-account-admission.json` reports exact UID `4799432499002212`/2212, game PID 47412. Read-only native inventory returned the same UID; no march sent. Repeat defect recovered in current adapter: confirmed attack clears `attacks[slot]` but retains consumed `cursor_open_pending`, rejecting the next physical press. Candidate clears that consumed intent only after exact confirmation; three-cycle one-VM regression passes with late-query no replay. City physical ground path and guildless-only guard are implemented from original `UIWorldPointBtn` root/13 line 1678; source/Lua checks pass. Neither R15 gameplay task is credited yet. Next: live repeated physical keys with new input diagnostics, then guildless city proof.

- R15 input/chooser diagnosis: Windows-MCP Shortcut plain letters emit Unicode/VK_PACKET, not the VK_Q watched by the app. `tools/feature018_r15_windows_key.ps1`, invoked through Windows-MCP PowerShell, delivers a real held VK with exact UID/server/session/PID/foreground checks and unconditional release. Run `native-ui-20261018-065041` captured physical Q on HWND68094084; native chooser visibly showed1570/1570 soldiers, but its first follow-up read Ctrl before target initialization and returned Submitted/no send. A read-only later selectorTarget observed COLLECT/point163123/server2212. Candidate now waits for missing InitData fields while matching all populated fields against the retained hover; wrong concrete target still fails. Deferred-init regression passes. Both prior R15 runs exited0, exact v26 restored, no journal. Next: republish and get actual repeated sends/recalls; no gameplay success credited yet.

- R15 Task 2 **PROVEN live** (2026-10-18): `artifacts/map014-takeover/native-ui-20261018-065538/r15-task2-summary.json`, same UID `4799432499002212`/server `2212`/game PID `29336`/session `eafec977fe554cacacc78ffe39e5ca7e`. Three physical Q→A cycles Confirmed matched march pairs `1427557174758384870`, `1427557174775161898`, `1427557174779356304`; native home snapshots between sends. Physical Q and W then created distinct Confirmed marches `1427557174787744845` and `1427557174846465051` simultaneously; physical A/S confirmed exact server-applied returns, then all squads home. Each attack has one native create and exact response-derived GUID; recalls use the accepted server-applied BACK_HOME rule, not raw reply correlation. Held VKs delivered through Windows-MCP PowerShell, per-key receipts `r15-*-key.json`. `r15-two-squads-visible.png` shows2/3 collecting, `r15-cycle3-q-visible.png` shows third send. Q while out gave an occupied-squad refusal; visible app-message transport proven in `feature018-r2-stage-4.png` (later out-squad Q on empty cursor gives its clear cursor refusal). No app/game restart between cycles or paired test. Source31436196; subsequent UI-only fix positions feedback as an overlay. Next: guildless city physical Q/server-correlated visible march/A, guild-city refusal, final ZIP.

- R15 city preflight correction: physical Q in065538 on visible WhattaTops city was refused before any send by the older own-alliance-identity prerequisite. R15 requires target guildless, so the guard now gates solely on explicit target alliance0, keeping own membership diagnostic; unknown/positive target alliance still refused at hover, selector and sender. Regression includes unreadable owner alliance with known guildless target. Task2 run exited0 with proven exact restoration/no journal. Next: new city-only live run and final package.

- R15 city membership source recovery: run070654 refused WhattaTops with unknown target alliance before any send and exited0/restored. Installed BuildPointInfo ctor RID13427 copies protobuf string allianceId; protobuf BuildInfo ctor RID43782 defaults it to empty string, setter RID43798 rejects null. The adapter now reads that actual native string (narrow BuildPointInfo public-field reflection fallback if exposed as base PointInfo); only a successfully read typed-city empty string is normalized to no alliance. Nil/unreadable stays blocked. Exact target alliance raw value/reader are now retained in attack receipts. Source/Lua suite and final typed-empty receipt regression pass. Next: republish for actual city march/refusal/A and R15 ZIP.

- R15 Task 3 **PROVEN live** (2026-10-18), source85c58362: `artifacts/map014-takeover/native-ui-20261018-071429/r15-task3-summary.json`, exact UID4799432499002212/2212/game24556/sessionc402f1e0f33e4a15a5983c3119a52f60. Physical hover+Q on WhattaTops city (targetUUID1349056560080921695, point156129) sent ATTACK_CITY11 once and Confirmed exact server-response GUID. Native receipt records `targetAllianceRaw=""`, normalized0, reader `native_member_allianceId`; `r15-guildless-city-after-q.png` visibly shows the outbound city march (16s ETA). Final rapid physical Q/A pair Confirmed same march1427557175043597470, createGUID060ed19e-d65e-4d85-848d-9f0aaaa6d8fe; A proved server-applied non-BACK_HOME→BACK_HOME. Earlier first A was late after battle/auto return; a second paired helper exited1 before issuing A; neither is credited as recall. Final pair keys in `r15-city-immediate-q-a-keys.jsonl`. Physical hover+Q on [A3VN]njopari was refused with no send, native target alliance string15f557d90bf9485594cc0ebb6fd89ebc read successfully; app refusal visible in `feature018-r2-stage-2.png`. Follow-up message-only classification now calls a nonempty string alliance membership rather than unknown, preserving dispatch eligibility; full Lua suite passes. All squads home before Stop. Next: final any-account R15 package from pushed source, updated README, extracted-package refusal/feedback smoke and final restoration receipt.

## Lead audit of Codex R15 — 2026-10-18 (source 9ad8ab71)
Codex ran out of tokens during its final packaged smoke run. All 9 commits pushed (remote = 9ad8ab71), tree clean.
Lead re-ran attack + hotkey Lua checks and full Desktop.Checks: PASS.
- Task 1 harness pin to 4799432499002212/2212: ACCEPTED.
- Task 2 repeat use: **ACCEPTED (live)** `native-ui-20261018-065538`: 3× Q→A cycles, then Q+W → A+S, one session.
  Root cause: a consumed hover intent (`cursor_open_pending`) was kept after a confirmed send and blocked the next
  press. Also found: Windows-MCP plain-letter shortcuts send VK_PACKET, not VK_Q; worker used a held-VK helper.
- Task 3 guildless city attack: **ACCEPTED (live)** `native-ui-20261018-071429`: Q on guildless city WhattaTops sent
  ATTACK_CITY and was server-confirmed, A recalled it; Q on an alliance player ([A3VN]) refused with no send.
- Task 4 ZIP: built by worker from 9ad8ab71,
  `artifacts/feature018-r15-9ad8ab71/LW-Control-FEATURE-018-R15-PARTIAL-9ad8ab715ade.zip`, SHA-256
  37c1eee042978a6bfcfe537702908a8775b27f88c46477cef6fc4539532fa834. Lead verified: no test UID in any file, CRC ok,
  honest README.
- Cleanup: the worker's packaged smoke run `native-ui-20261018-072738` was left open (game + app). Lead sent the
  harness `finish`: exit 0, original scripts restored, no journal, restoration proven. Smoke test itself unfinished.
All R15 runs exited 0 with exact restoration.

## Owner decision and R16 assignment — 2026-10-18
Owner: "i want to remove all this feature … i dont need it" — the Attack card's march speed-up options:
"Use speedup items from the inventory", "Buy speedups with diamonds when items run out" and their warning text.
Record as OWNER_DIRECTION removal in the behaviour-differences ledger.

R16 (for CoS, later):
1. Remove march speed-ups completely: both Attack-card checkboxes + warning, the separate "Current-march item
   speedup" panel in HotkeyPages.jsx, their locale keys (all 9 languages), the config fields
   `attackMarchSpeedupItem` / `attackMarchSpeedupDiamond` from save, the C# command/provider paths and the
   Lua item/diamond ("rapid") speed-up code and their checks. Q/W/E/R must keep sending marches exactly as now
   (never use items or diamonds). Old saved settings that still contain those fields must load without error
   (ignore them). Attack card description stays.
2. Prove nothing else broke: UI build/check, Desktop.Checks, attack/hotkey Lua checks; one live Q→A on
   4799432499002212 (attack still works, no item/diamond change).
3. Owner addendum: BUILD the two missing Q/W/E/R hover targets from the original: (a) hover an ALLY city (same
   alliance) + key = send that squad as reinforcement (the game's own reinforce/garrison march, not an attack);
   (b) hover a CITY RUIN + key = send that squad to the ruin with the game's own march type for ruins. Recover the
   original target classification and march type from the 0.3.17 original + current client (rea). Keep the
   guildless-only rule for attacking players; ally cities must never be attacked. Worker proves these offline
   (source/Lua checks, read-only hover classification if a target is visible) but does NOT send live
   reinforcement/ruin marches: the owner tests those personally.
4. New owner-test ZIP (README: speed-ups removed by owner decision; ally reinforcement and ruins built but
   untested — owner to test).
Handoff rule unchanged; record progress under "R16 progress".

### R16 progress (worker updates this)
- **Task 1 — march speed-ups removed (2026-10-18):** recorded as FEATURE-018-D01
  OWNER_DIRECTION in docs/FEATURE_018_BEHAVIOR_DIFFERENCES.md. The Attack card no
  longer renders the inventory/diamond speed-up choices or warning, the separate
  current-march speed-up panel is gone, and all nine canonical locale catalogs no
  longer define the retired copy. HotkeyConfig no longer exposes or saves
  attackMarchSpeedupItem / attackMarchSpeedupDiamond; an on-disk legacy
  runtime-config.json with both fields enabled loads successfully, projects neither
  field, and the next save removes them. The Desktop command schema/provider and game
  Lua now expose only normal attack behavior: feature018_rapid_action,
  speedupItem, speedupDiamond, WorldMarchRapid, item/diamond consent and the
  three rapid-only check files are retired. Q/W/E/R attack payloads contain no
  speed-up/item/diamond fields. The explicit legacy WebUi recovery bundle was also
  stripped of its old speed-up controls/copy so --legacy-ui cannot surface the
  retired feature. Focused evidence is PASS: FEATURE018_HOTKEY_CONFIG_PASS,
  FEATURE018_HOTKEY_CAPTURE_PASS, full Desktop delivery runner
  HOME_FEATURE_DELIVERY_CHECKS_OK, FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS,
  FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS,
  FEATURE018_HOTKEYS_MOUNTED_OFFLINE_PASS; canonical UI build/package identity
  b2bf57249bc2de7c9fd658a6a4d363a9f922e275c9b1b5cc218bdcc504da987d /
  908f94c9c8e2204632385f65b6cb3ae4a0e0343bcb99daac56782a74cf6091a1.
  Task 2 live Q→A and no-item/no-diamond regression remains to be run after this
  checkpoint.
- **Task 2 — attack regression proven live (2026-10-18):** from pushed Task 1
  checkpoint e870f3cd13cd6ce9258e2b90e51596473c89d6b4, the canonical UI build,
  production-build check, full UI check, full Desktop delivery runner, Hotkeys Lua
  suite and attack Lua suite all passed. Fresh isolated run
  artifacts/map014-takeover/native-ui-20261018-082907 admitted exact UID
  4799432499002212/server 2212/game PID 27148/session
  5f9dc1eb77f4484f91c0dbca75b7efc7. A native physical-cursor read classified
  Ranjitsinh as player city UUID 1321165592854001462/point 123154; the existing
  no-send city guard independently accepted it as guildless_player_city with
  requestDispatched=false/worldMarchSent=false. Exactly one guarded physical Q then
  A pair was delivered. Q is Confirmed with exactly one WorldMarchFormationNew,
  ATTACK_CITY target type 11, source-generated clientCreateUuid
  d91c5716-9a2e-42d3-9f87-054a7363913d, exact server-derived GUID poststate and
  march UUID 1427557175748240473; the target alliance native string was empty and
  normalized to guildless. A is Confirmed on the same march via authoritative
  non-BACK_HOME→BACK_HOME server-applied poststate. The R16 staged proof captured
  native GOODS_TYPE_108 inventory and LuaEntry.Player.gold immediately before and
  after the pair: inventory []→[] and gold/diamond balance 130243→130243. The
  temporary read-only staged observer was removed after capture, so no extra
  inventory/rapid command ships. Evidence:
  artifacts/map014-takeover/native-ui-20261018-082907/r16-task2-summary.json and
  r16-q-a-keys.jsonl. Task 3 ally-city reinforcement and city-ruin recovery remains
  offline-only for implementation/verification; no live reinforcement or ruin send
  is authorized for this worker.
- **Task 3 — ally-city reinforcement and city ruin built OFFLINE (2026-10-18):**
  original/current v26 source pins an ordinary allied player base to the
  `AssistanceCity` button, whose preferred native route is
  `WorldBattleUtil.TrySendAssistanceMarch` with `AssistanceType.MainCity`; that
  helper maps to symbolic `MarchTargetType.ASSISTANCE_CITY`. This remains distinct
  from `WORLD_ALLIANCE_CITY` / `ASSISTANCE_ALLIANCE_CITY`. Hover classification now
  preserves the existing guildless-only attack rule: a typed PlayerBuilding with
  explicit no-alliance still uses `ATTACK_CITY`, an exact same-alliance city uses
  the native MainCity assistance route, other/unknown alliance targets cannot
  dispatch, and an ally city can never enter `ATTACK_CITY`. Destroyed-city ruins
  are source-pinned WorldPointType 38 and require native `LW_CITY_RUIN` /
  `LW_CITY_RUIN_1`, `MasteryManager:IsShowWorldMasteryBtn()`, current alliance
  membership and non-`MasteryHome.Gather`; their original entrypoint is exactly
  `MarchUtil.OnClickStartMarch(ATTACK_PLAYER_RUIN_BUILDING, pointId, uuid, -1, 1)`.
  The existing one-create sender/GUID/poststate transaction fence now accepts both
  symbolic kinds and still rejects duplicate/unattributed sends. Boss, special
  monster and occupied/rally exclusions are unchanged. Source/Lua regression is
  PASS (`FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS` and
  `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS`), including same-alliance native
  assistance payload, ruin mastery/alliance/build negatives, exact ruin five-arg
  tuple, and fresh response-derived create-GUID confirmation for both new kinds.
  No game session was opened for Task 3 and no live reinforcement or ruin march was
  sent; the owner remains the first live tester for those two targets.
- **Task 4 / cleanup — R16 owner package checkpoint (2026-10-18):**
  `tools/feature018_r16_package.py` is the R16 copy of the accepted R15 packager,
  updated for an OWNER_TEST release. It refuses a dirty/unpushed branch, requires
  the Windows ProductVersion to end in the exact pushed HEAD, verifies every linked
  OverviewBridge source against that checkout, CRC/hash-checks every ZIP entry and
  scans every uncompressed entry for all historical/test account UIDs in both
  ASCII/UTF-8 and UTF-16LE. The fixed R16 proof UID/server were removed from the
  owner Desktop binary: isolated proof identity now comes only from proof-launch
  environment variables. README-R16 states that march speed-ups were removed by
  owner decision, that old speed-up settings are ignored, and that ally-city
  reinforcement plus destroyed-city ruin hover are built/source-verified but
  intentionally untested live for the owner to test personally. It also says not
  to press Unlock Land Cell during this R16 owner test. The final external ZIP and
  `r16-package-receipt.json` are generated only after this exact source checkpoint
  is pushed; their embedded `SOURCE-COMMIT.txt` / R16 integrity manifest are the
  authoritative commit/hash witnesses.
  Full Desktop checks pass (`HOME_FEATURE_DELIVERY_CHECKS_OK`), both Hotkey/attack
  Lua suites pass, `git diff --check` passes, and the packager preflight proves the
  UTF-16 test-UID detector. The earlier successful Task 2 live session later hit
  the outer 900-second harness timeout and therefore lacked its normal owned-exit
  file. Independent post-timeout cleanup evidence at
  `artifacts/map014-takeover/native-ui-20261018-082907/r16-post-timeout-restoration.json`
  proves all three installed v26 script hashes exactly equal that run's captured
  originals, no LastWar/LWBridge process remains, and no recovery journal exists.

## Lead audit of CoS R16 — 2026-10-18 (source 4f090efd): ACCEPTED for owner test
Verified by lead: local HEAD = remote 4f090efd, tree clean. Attack + hotkey Lua checks, full Desktop.Checks
(`HOME_FEATURE_DELIVERY_CHECKS_OK`) and UI check (`LWB317_UI_COMPLETE_CHECKS_OK`, 9 languages) PASS.
- Speed-up removal (e870f3cd): ACCEPTED. UI controls, item panel, config fields, rapid/diamond Lua and their checks
  removed (−2,361 lines); no speed-up config references left in UI/C#/Lua.
- Regression (28be4c6b): live Q→A on 4799432499002212 confirmed, items and diamonds unchanged — accepted per report.
- Ally reinforcement + city ruin (6360b1c4): built with offline checks only, as assigned. **Owner to test live.**
- Proof UID removed from the compiled owner build (4f090efd).
- ZIP `artifacts/feature018-r16-4f090efd/release/LW-Control-FEATURE-018-R16-OWNER-TEST-4f090efd1df3.zip`
  SHA-256 4e63284d…86d0f7: lead re-hashed, CRC ok, 92 entries, no test UID (ASCII or UTF-16), honest README.
- Cleanup: lead hashed the live game scripts in LocalLow/…/lwScripts — exact v26 originals; no active recovery
  journal; no game/app process.
Open: owner live test of ally reinforcement and ruins; F9/F10; City Layout "result uncertain"; shields/Alt+1–4
set aside by owner.

## Owner test of R16 — 2026-10-19
- Ally city reinforcement (hover + Q on same-alliance city): **WORKS** (owner confirmed).
- City ruin: not tested yet. Needs the profession skill 攻城精通 (Siege Mastery) level 1, which unlocks 拆除廢墟
  (demolish ruins); owner's account does not have it. Owner will test once learned.
- Owner question: the Mini Games panel "Frontline: original G adds five units / Inspect active original battle"
  is a worker test panel (reads the running battle's soldier count, then a separate click button adds five). It
  duplicates the G hotkey card and still says "five" although the count is now configurable. Lead recommends
  removing it, together with the other worker diagnostic panels in HotkeyPages.jsx (NativeAttackReadinessPanel,
  ShieldOwnedInventoryPanel, ShieldNativeActionPanel, EquipmentNativePreflightPanel,
  RelocationNativeReadinessPanel) so only the original feature cards remain. Pending owner decision; next round.

## R17 assignment — 2026-10-19
Owner decision: "remove the Frontline panel and all the other worker test panels … keep only the original
feature cards." Record as OWNER_DIRECTION in the behaviour-differences ledger.
1. Remove from the UI (HotkeyPages.jsx) the worker diagnostic panels: FrontlineNativeActionPanel ("Frontline:
   original G adds five units / Inspect active original battle"), NativeAttackReadinessPanel,
   ShieldOwnedInventoryPanel, ShieldNativeActionPanel, EquipmentNativePreflightPanel,
   RelocationNativeReadinessPanel, plus their unused locale text and UI checks that only test those panels.
   The Hotkeys and Mini Games tabs must show only the original 0.3.17 feature cards (with the G count setting).
   Do NOT remove the hotkey features themselves (G, Q/W/E/R incl. ally reinforcement and ruins, A/S/D/F, Space,
   F6/F7/F8, Alt+1–4, F9/F10) or the backend commands the hotkeys use. Remove a backend command only if nothing
   but a deleted panel calls it.
2. Checks: UI build + check, Desktop.Checks, attack/hotkey Lua checks. Screenshot of the Hotkeys and Mini Games
   tabs before/after. Live: one hover+Q→A and one G press in a stage on 4799432499002212 still work.
3. Owner-test ZIP (R17 package script), README updated.
Handoff rule unchanged; record progress under "R17 progress".

### R17 progress (worker updates this)
- Task 1 complete: removed all six worker diagnostic panels from `HotkeyPages.jsx` and their inline bilingual
  copy. Hotkeys now renders only the seven recovered 0.3.17 cards; Mini Games keeps the original G card and its
  configurable soldiers-per-press field, plus the original treasure/Land/Sheep cards. Reworked the two
  panel-specific mounted checks into absence/original-card regressions. Removing the Frontline panel exposed a
  stale positional Land fixture in `check-mini-r2.mjs`; its existing Land/Sheep test was retained and the unlocked
  reply fixture was updated with the already-required exact-cell causality fields.
- Backend caller audit for Task 1: no diagnostic endpoint command removed. The endpoint names still have
  non-panel Desktop/tooling/check callers, while physical hotkeys use the preserved `feature018_hotkey_action`
  provider path. This follows the R17 rule to remove a backend command only when nothing except a deleted panel
  calls it.
- Focused Task 1 checks pass: `check-feature018-hotkeys-mounted.mjs` reports seven original cards and diagnostics
  removed; `check-feature018-land-mounted.mjs` reports original G count preserved and the Frontline diagnostic
  removed. Before screenshots were captured under ignored `artifacts/feature018-r17-ui/`; after screenshots and
  the full R17 verification matrix are Task 2.
- **Task 2 — regressions proven (2026-10-19):** exact before/after Hotkeys and Mini Games screenshots are under
  `artifacts/feature018-r17-ui/`. The before set was rendered from parent `66de5525`; the after set is R17 source.
  After removal Hotkeys contains exactly the seven recovered cards, and Mini Games retains the original G card with
  its soldiers-per-press input plus Treasure Chest, Land and Sheep. Canonical UI build/check PASS
  (`LWB317_PRODUCTION_UI_BUILD_OK`, `LWB317_UI_COMPLETE_CHECKS_OK`, all nine locales), full
  `tests/LWBridge.Desktop.Checks` PASS (`HOME_FEATURE_DELIVERY_CHECKS_OK`), and both Hotkey/attack Lua suites PASS
  (`FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS`, `FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS`).
  The first Desktop run hit only an `esbuild.exe` EPERM while the screenshot Vite server still held that binary;
  after the evidence server closed, the unchanged suite passed normally.
- **Task 2 live — Q→A and G still work:** pushed source `b3bb37db` package
  `artifacts/feature018-r17-task2/windows/LWBridge.Desktop.exe` admitted only exact UID
  `4799432499002212` / server `2212` in
  `artifacts/map014-takeover/native-ui-20261018-183342` (game PID 8820, session
  `0e0a7752666447efab6400b392387b2d`). A no-send native hover check first classified the target as
  `guildless_player_city`, alliance `0`, UUID `1349057083047715926`, point `759192`. One physical Q then sent one
  `ATTACK_CITY` march and returned **Confirmed** with response-derived client GUID
  `25d631c8-9a2b-4e8b-8199-d056b2de7f96` / march `1427557180999509106`; one physical A returned that same march
  with the accepted server-applied `BACK_HOME` poststate. In a fresh Frontline Breakthrough stage, one physical G
  returned **Confirmed** and changed the native Parkour team from **4 → 9**, with five verified new unit identities
  and configured count 5. The helper now admits G so the required real VK path can be used (plain-letter desktop
  shortcuts are VK_PACKET). No Land Cell request was issued.
- **Task 2 cleanup:** the same run was explicitly finished. `owned-exit.json` reports exit 0, no error,
  `restorationAudit.state=proven`, all `LWScripts.data` / `LWScripts.txt` / `version.txt` hashes equal the captured
  originals, and no recovery journal. No task-owned LastWar or LWBridge process remains. R17 packaging is next.
- **Task 3 / package source prepared:** `tools/feature018_r17_package.py` carries forward the accepted R16 release
  gates and emits an R17 OWNER_TEST ZIP only from a clean pushed commit whose Windows ProductVersion embeds that
  exact SHA. It verifies the canonical production UI fingerprint, every Desktop-linked OverviewBridge source,
  per-file hashes/ZIP CRC, and scans every uncompressed ZIP entry for all historical/test account UIDs in both
  ASCII/UTF-8 and UTF-16LE. README-R17 records the owner-directed removal of worker diagnostic panels, preservation
  of all original hotkeys/cards and the G count setting, the fresh R17 G and guildless Q→A positives, the owner's
  successful R16 ally-city reinforcement test, and the still-unavailable ruin test prerequisite. It explicitly
  keeps Unlock Land Cell out of this owner test. The external ZIP/receipt will be generated only after this package
  source checkpoint is pushed, so its embedded `SOURCE-COMMIT.txt` remains the authoritative final source witness.

## Lead audit of R17 — 2026-10-19 (source 3939fb01): ACCEPTED for owner test
Verified by lead: HEAD = remote 3939fb01, tree clean. None of the six panel names remain in the UI source; the ZIP's
JS has no "Inspect active original battle" / "original G adds five" text. Attack, hotkey and Mini Lua checks,
Desktop.Checks (`HOME_FEATURE_DELIVERY_CHECKS_OK`) and UI check (9 languages) PASS. Live Q→A on a guildless city and
G 4→9 in a Frontline stage reported in `native-ui-20261018-183342` (exit 0, restoration proven).
ZIP `artifacts/feature018-r17-3939fb01/release/LW-Control-FEATURE-018-R17-OWNER-TEST-3939fb010d36.zip` SHA-256
5644b303…f4e0eb: re-hashed, CRC ok, 92 entries, same layout as R16, SOURCE-COMMIT = 3939fb01, no test UID.
Cleanup: live game scripts in LocalLow are the exact v26 originals, no active journal, no game/app process. Two
earlier runs (165523, 171602) left no exit receipt, but nothing is left running and the files are original. The
r17 City receipt (175655) is a read-only snapshot, no moves.

## Lead finding — Space shield countdown does not match the original (2026-10-19)
Original 0.3.17 text: "Hold Space to display remaining Shield time above protected cities" (all shielded cities in
view on the world map). Current build reads only the player's OWN shield (`StatusManager.GetShieldStatusIds`,
tools/feature018_hotkey_game.lua) and shows one remaining time. Next round: hold Space on the world map → show
each visible shielded city's remaining shield time above that city; release Space → hide. Read-only, no game
actions. Also pending owner direction: map scan data is not worth keeping between sessions — wipe map scans at app
start, keep settings/profiles.

## Owner will test personally (set aside for workers) — 2026-10-19
Owner: "put those that i will test it later aside as record it down, i will test it when i am free".
Workers do NOT live-test these; owner tests when free and reports back:
- F6/F7/F8 use 8h/12h/24h shield (needs alliance coins).
- Alt+1–4 equipment presets.
- F9 random relocation, F10 alliance relocation.
- City ruin hover (needs 攻城精通 skill).
- Space shield countdown — after it is fixed to show all protected cities (worker builds the fix; owner tests).
- Food House auto-solve and Black Market chest marker — when the events return.
Remaining worker items: Space countdown fix (build), wipe map scans at app start, City Layout reliability.

## Owner notes on the Squads / AFK tab — 2026-10-19 (not yet assigned)
Owner decision — REMOVE (OWNER_DIRECTION) from Squads / AFK → AFK Tasks:
- "Use Stamina items automatically" card (automation.autoUsePotion),
- Auto Alliance Drill,
- Auto Garrison,
- Zombie Bus Garrison.
Owner is interested in (explain/keep, status to be checked): Monster AFK (master switch + Shared AFK Profiles),
and the Equipment Schemes tab ("recover equipment schemes" — make it work like the original).
Note for the assignment: without automatic stamina items, Monster AFK pauses when stamina runs out (free daily
stamina on the Automation tab is separate). Lead to confirm with owner before removing stamina auto-use.
Owner confirmed (2026-10-19): KEEP the "Use Stamina items automatically" card. Remove only Auto Alliance Drill,
Auto Garrison and Zombie Bus Garrison.
Owner correction (2026-10-19, final): remove ONLY Auto Garrison and Zombie Bus Garrison. KEEP Auto Alliance Drill
and the "Use Stamina items automatically" card. This supersedes the two notes above.

## R18 assignment — BACKGROUND ONLY (2026-10-19)
Owner will be using the PC's keyboard and mouse. The worker may start its own task-owned game (logged in as
4799432499002212) and leave it in the background, but must NOT use the mouse or keyboard, Windows-MCP input,
the key helper (feature018_r15_windows_key.ps1), focus stealing, minimising/moving windows or anything that
touches what the owner is doing. Screenshots only of the worker's own windows if needed. All live checks go
through the app's own commands / staged proof queue (no physical keys).
Tasks, in order:
1. Remove Auto Garrison and Zombie Bus Garrison (owner decision): UI cards/settings (SquadsPage.jsx), locale keys
   (9 languages), C# config/command services (e.g. AllianceGarrisonConfigCommandService, "allianceGarrison" task
   name) and their Lua/automation code and checks. Old saved settings load safely and are ignored. KEEP Auto Alliance
   Drill, Use Stamina items automatically, Monster AFK, Shared AFK Profiles. OWNER_DIRECTION ledger entry.
2. Map scans wiped at app start (owner: data goes stale): on each app start clear saved map scan rows for all
   servers; keep profiles, settings, hotkey config, G count. Prove with a packaged-app start: old rows gone,
   settings still there.
3. Space shield countdown fix (build): holding Space on the world map shows, above each visible shielded city,
   its remaining shield time; releasing hides it. Read-only. Prove the data side in the background: a native
   read-only query lists visible cities with shield end times (compare with the Map City "Shield Ends" column).
   The physical Space key test is the owner's.
4. City Layout reliability: report a move/swap as Completed when the building's real position afterwards matches
   the plan (server-applied after-state), instead of "result uncertain"; fix the brief account/session dropout
   (GAME_ACCOUNT_CHANGED / owner lost for 15 s) so a running job does not abort. Prove on 4799432499002212 through
   the City Layout commands: one move, one swap, Cancel, restore — and the city ends exactly as it started.
5. Status check (no new features): run Monster AFK with one Shared AFK Profile on one squad against an ordinary
   monster, and apply then restore one Equipment Scheme, both through the app's commands. Report what works and
   what does not, with the exact reason.
6. Owner-test ZIP (R18 package script), README updated, no test UID inside.
Do not live-test: F6/F7/F8, Alt+1–4 keys, F9/F10, city ruins, Unlock Land Cell (owner tests those).
Handoff rule unchanged; record progress under "R18 progress".

### R18 progress (worker updates this)
- **Task 1 complete (2026-10-19), pushed 72277796:** removed Auto Garrison and Zombie Bus Garrison
  cards/settings, preview fixtures, all nine locale keys and locale regeneration entries. Retired the native
  AllianceGarrisonConfigCommandService, primary/retained registrations and automation task name. Legacy enabled
  settings/status entries are ignored and disappear on the next config write. Monster AFK / Shared AFK Profiles,
  Auto Alliance Drill, automatic stamina items and G count remain. OWNER_DIRECTION: FEATURE-018-D03. Current Lua
  has no executor for either removed feature. Canonical UI build/full check (nine locales), full Desktop.Checks
  (HOME_FEATURE_DELIVERY_CHECKS_OK), legacy setting/status migration and Hotkey/attack Lua suites PASS. Logs:
  artifacts/feature018-r18-task1-{ui-build,ui-check,desktop}.log. No game started or desktop input used.
  Task 2 next: all-profile/all-server startup Map scan clear with packaged preservation proof.

- **Task 2 complete (2026-10-19):** normal production startup now clears all canonical and legacy Map
  scan databases under every saved profile before opening any profile runtime. All servers' published rows,
  scan runs/blocks/staging and transient treasure states are cleared; settings, marks, action history, profile
  files, hotkeys and G count remain. A live-scan lease prevents deleting underneath another app's active scan.
  Packaged process proof uses the exact production startup boundary with --startup-state-proof and exits
  before creating a window/game. Controlled two-profile/two-server seed: 44,828 old rows per profile -> zero;
  a later-session inserted row also disappears on a second start. Six durable config/profile/controller files
  retained byte-identical SHA-256s, Map app_settings retained, G count 17 retained. Proof:
  artifacts/feature018-r18-task2/startup-proof/receipt.json; reproducible via tools/feature018_r18_startup_check.py.
  Fresh Release publish PASS. This is packaged controlled startup evidence, not a native scan/gameplay positive.

- **Task 3 complete for worker scope (2026-10-19):** Space now reads visible native cities through
  WorldPointManager.GetMainByScreen / BuildPointInfo.protectEndTime, projects each city through the native
  world camera, and displays separate read-only countdown labels above protected cities while Space is held.
  Release, text-input admission loss or loss of the exact foreground game closes the non-activating,
  click-through display; positions/remaining times refresh while held. The original own-shield status path
  remains only for shield-item validation. Inert multi-city/seconds/ms/expired/missing/scene checks PASS;
  hold/release/owner-race checks and full Desktop.Checks PASS (HOME_FEATURE_DELIVERY_CHECKS_OK).
  Native background proof: artifacts/map014-takeover/native-ui-20261019-004200 on exact approved UID/2212,
  session 55e8a11ec5074f8a99b9d19eb5354028/game33392. Passive City scan completed 2500/2500, 4288 rows; two
  simultaneously visible active shielded cities 1349057123795379308 / 1349057347985122434 had end times
  1792628428 / 1792372406, exactly matching fresh Map City Shield Ends data; countdowns 257696820 / 1674820 ms.
  Receipts r18-active-visible-shields.json and r18-shield-comparison.json. Physical Space remains OWNER TEST.
  Background harness disallows key/show/window operations, skips key capture and starts its launcher without
  activation. The owner's open R17 app stays untouched. First private app attempt failed on the occupied
  default pipe before game start; second exposed a hardcoded helper pipe path and failed authentication,
  then exited/restored exactly. Fixed isolated proof pipe propagation; successful run exit0, restoration
  proven, exact v26 originals and no recovery journal. REA cannot parse the custom .rdl format; current
  source pins use the existing hash-gated RGMD reader (artifacts/feature018-r18-{world-fields,shield-cil}.txt).
  No gameplay action, input injection, shield use or anti-cheat change was performed.

- **Task 4 complete (2026-10-19):** native BuildingDate destination + full-city expected positions now
  complete a move even when no raw reply/event callback is observable. Source checks prove the original
  visual/send paths do not update BuildingDate.pointId; the server AddBuilding/UpdateInfo path does. The
  one-send/foreign-send/identity/footprint guards remain. A missing native account pauses rather than poisons
  the City job; the wrapper keeps it while the exact session lease remains fresh. City exchange waits for
  fresh identity (30s bound), retains an already-run result across a transient missing account without
  rerunning its handler, and still rejects a concrete UID/server/session change. Staged commands pin the
  admitted UID/server, wait through transient observation gaps and refuse explicit account mismatch.
  Inert City/source/swap/cancel/restoration, bridge replay, owner-transition and live-driver checks PASS;
  full Desktop.Checks PASS, including a simulated 16s identity gap with one request and no replay.
  Live: native-ui-20261019-005144, exact UID4799432499002212/2212/game15588/session
  a4ad77a9d995421dbee610755b305349. Single 1/1 Completed, swap 3/3 Completed; Cancel consumed while a
  3-move job was in flight, settled Cancelled with exactly 1 dispatched/completed move. Raw reply counts
  were zero; authoritative server-applied after-state counts account for every completed move. Each run
  restored through the native immutable-original Restore command. Final full snapshot equals all **109**
  original building positions exactly (r18-city-summary.json and feature018-city-driver-20261019-005257.json).
  The same background-only session remains open solely for Task 5; final exit/restoration will be recorded
  below. One earlier incremental publish omitted ProductionUi and exited before game start; corrected with
  the verified unchanged canonical UI. No desktop input, forbidden key or Land Cell request used.

- **Task 5 status check complete (2026-10-19), no new features:** same admitted background session
  native-ui-20261019-005144; reproducible one-shot queue driver tools/feature018_r18_status_check.py.
  Equipment Scheme **PROVEN LIVE**: saved a native baseline, applied a scheme removing one gear UUID
  1376833201183958087 from squad1/hero1397117333692523610, then applied the original scheme to restore it.
  Apply and restore each dispatched/completed exactly one native HeroEquip step; equipmentApplied=true,
  nativeSlotPoststateVerified=true. Per-slot reply correlation remains explicitly false. All 15 heroes'
  equipment-slot maps and every inventory item's wearer exactly match the baseline afterwards.
  Monster AFK **DOES NOT RUN**: saved one enabled Shared AFK Profile configured for an ordinary-monster
  farm on squad1, executionLimit1, then invoked the app's automation_start taskName=monsterSweep command.
  It returned INVALID_REQUEST: "FEATURE018 only supports the original sheepGame task." automation_status
  reports monsterSweep enabled=true / running=false / step=not_loaded; the checkout has config persistence
  but no Monster AFK runtime producer, and its production AFK presentation remains disabled. No ordinary
  monster march was sent or claimed as passed. The temporary profile was disabled/removed afterwards;
  final status enabled=false/running=false/not_loaded. Evidence: r18-task5-status.json in that session.
  Equipment works through commands; physical Alt+1-4 remains the owner's test. No new AFK feature was added.
- **Task 4/5 final cleanup:** re-read the native city after the status tests; all 109 positions still exactly
  equal the original. Sent staged finish: own host39768 and game15588 exited0, no error, restorationAudit=proven,
  all three installed script hashes exactly equal admitted v26 originals, no recovery journal. The owner's
  R17 app PID11544 remains untouched; no task-owned game/app or launcher remains. Receipt owned-exit.json.

- **Task 6 package source prepared (2026-10-19):** tools/feature018_r18_package.py carries forward the
  clean-pushed-commit, exact Windows ProductVersion, production UI fingerprint, linked bridge source hashes,
  ZIP CRC/per-entry manifest gates. It scans all uncompressed entries for current/historical test UIDs in
  ASCII, UTF-16LE and UTF-16BE; explicit inert positive-detection witnesses pass for all three encodings.
  README-R18 records garrison removals, session-only scans, multi-city Space data proof/owner visual test,
  live City Layout and Equipment Scheme restore, and the exact NOT WORKING Monster AFK dependency. Earlier
  G/Q/A positives are labelled historical; reserved owner key/event tests remain pending. Packaging review
  found the opt-in legacy recovery bundle still displayed Auto Garrison: removed that legacy component/call
  and its nine locale catalogs too. All ten modified legacy JS files parse and have no retired card keys;
  the canonical retained controls are unchanged. Final publish/ZIP and extracted startup proof follow this
  pushed source checkpoint; their exact SOURCE-COMMIT/integrity receipt are authoritative.

- **Task 5 UI qualification (source check):** the successful Equipment Scheme test is specifically the
  native command path. Canonical SquadsPage.EquipmentContent still reads previewEquipmentFixture and an
  in-memory preview config adapter; normal production previewState is empty, so that tab is offline/empty
  and does not call the native snapshot/apply services. UI wiring remains unfinished. This was not added
  under the R18 status-only task. README-R18 explicitly records the gap; do not treat native command proof
  as an end-to-end Equipment Schemes tab pass.
- **Task 6 verification / final-source handoff (2026-10-19):** fresh Release publish and R18 packaging gates
  passed on verification checkpoint faefbfa2: exact pushed ProductVersion, production UI fingerprint,
  16 linked bridge source matches, 114 ZIP entries, CRC/per-file hashes and all current/historical test UIDs
  absent in ASCII, UTF-16LE and UTF-16BE. Hotkey/attack Lua suites and bridge/visible-city-shield checks PASS;
  full Desktop.Checks passed at the final runtime-code checkpoint. The README now also qualifies the
  canonical Equipment Schemes UI gap above. After this final audit/source checkpoint is pushed, the
  owner ZIP is regenerated from that exact HEAD (not the older verification ZIP), then extracted for the
  two-start stale-row/settings smoke. The authoritative final filename, SOURCE-COMMIT, SHA-256 and UID-scan
  result are in artifacts/feature018-r18-<HEAD8>/release/r18-package-receipt.json. This source-pinned external
  receipt is the final artifact handback; no later code/doc commit is needed to identify it. All live work
  is finished with exact restoration; owner-only physical key/event checks remain pending as recorded.

## Lead audit of R18 — 2026-10-19 (source 99d98b1b): ACCEPTED for owner test, with two found gaps
Verified by lead: HEAD = remote 99d98b1b, tree clean. Attack, hotkey, City game and City live-driver checks,
Desktop.Checks (`HOME_FEATURE_DELIVERY_CHECKS_OK`) and UI check (9 languages) PASS. No garrison/zombie-bus UI or
service left (only the legacy-settings cleanup line). ZIP
`artifacts/feature018-r18-99d98b1b/release/LW-Control-FEATURE-018-R18-OWNER-TEST-99d98b1b0472.zip` SHA-256
10359077…06734e: re-hashed, CRC ok, 114 entries, SOURCE-COMMIT = 99d98b1b, no test UID (ASCII/UTF-16LE/BE).
Live session 005144 exit 0, restoration proven; live game scripts are exact v26 originals; no journal; only the
owner's own R17 app is running. City summary: 109 buildings back in their original positions.
- Task 1 remove Auto Garrison + Zombie Bus: ACCEPTED.
- Task 2 wipe map scans at start, settings kept: ACCEPTED (controlled packaged startup proof).
- Task 3 Space countdown for all visible shielded cities: data side ACCEPTED (2 cities matched Map Shield Ends);
  physical Space test is the owner's.
- Task 4 City Layout: ACCEPTED live — move Completed, swap 3/3 Completed, Cancel mid-job, restore, 109/109 exact.
- Task 5 FOUND GAPS (not fixed, status task only):
  * **Monster AFK does not exist at runtime**: settings save, but `automation_start monsterSweep` is rejected
    ("FEATURE018 only supports the original sheepGame task"); no runtime producer. Needs to be built.
  * **Equipment Schemes**: the native apply/restore commands work live (exact before/after match), but the
    Equipment Schemes TAB still shows preview fixture data and is not wired to the real game. Needs wiring.

## Lead UI layout review at different window sizes — 2026-10-19
Owner: "when i drag the size of the ui, some card look awkward". Lead checked the UI preview at 900, 1100 and 1600 px.
Problems found (worst first):
1. Squads / AFK → AFK Tasks: below ~1100 px the page spills past the right edge (33 px horizontal scroll at 900);
   the three top toggle cards cut their titles ("Monster AFK mas…", "Use Stamina items…", "Launch priority: 1 · Jo…");
   the Shared AFK Profiles column gets so narrow profile names shrink to one letter; the AFK Target dropdown text is cut.
2. Squads / AFK → Equipment Schemes: preset names cut, squad position cards crammed (hero names, "Lv.3…", "Em…"),
   the third position card runs off the right side.
3. Automation: two-column card grid pairs short and tall cards, leaving large empty gaps (e.g. Auto Training next
   to Automatic Construction); at 900 px the category tab bar is cut off ("Pro…") with no scroll hint.
4. Mini Games: an odd number of cards leaves half a row empty and cards have uneven heights.
5. Top bar: at ≤1100 px it wraps into two rows (Preview / Pending tasks drop under the logo).
6. Map Data table: at narrow widths columns squeeze (status text "Fully plunde…" cut, "Marke d" header wraps).
7. Hotkeys, Home, City Layout: acceptable; City Layout's right properties panel is tight at 1100.
Next: fix responsive layout (min widths, wrapping, grid that packs cards, no horizontal page scroll at 900 px+),
before/after screenshots at 900/1100/1400/1600, UI checks pass.

## R19 assignment — responsive UI layout (2026-10-19)
Fix the 7 problems in "Lead UI layout review at different window sizes" above. UI/CSS only; no game needed, no
desktop input (owner uses the PC). Preview: `npm.cmd --prefix src/LWBridge.UI-0.3.17 run dev` →
http://127.0.0.1:4317/?view=<page>&previewLanguage=en&previewState=<state> (e.g. march+squads-profile,
march+squads-equipment, automation+automation-runtime-running, mini-games+hotkeys-connected,
map-data+map-actions-ready, city-layout+city-layout-populated-moved, overview+home-connected, hotkeys+hotkeys-connected).
Acceptance at 900, 1100, 1400 and 1600 px wide, in English AND Chinese (zh-CN), light and dark:
- no horizontal page scroll; nothing runs off the right edge;
- card titles/labels wrap instead of being cut to "…" (names may ellipsize only with a tooltip showing the full text);
- Squads AFK Tasks: toggle cards wrap to new rows; profile list and editor stack vertically when narrow;
- Equipment Schemes: preset list and squad positions reflow (positions wrap to fewer per row), no clipped cards;
- Automation and Mini Games: cards pack without big empty gaps (e.g. CSS grid with auto-fill/minmax and
  align-items:start, or a masonry-style column layout); tab bars wrap or scroll with a visible hint;
- top bar stays one tidy row down to 1100 px, wraps cleanly below;
- Map Data table: sensible minimum column widths and horizontal scroll INSIDE the table only;
- keep the familiar 0.3.17 look; no feature or text changes.
Deliver before/after screenshots for every page at each width, UI build + check PASS, Desktop.Checks PASS, an
owner-test ZIP (R19 package script, no test UID) and progress under "R19 progress".

### R19 progress (worker updates this)
- **Baseline/diagnosis complete (2026-10-19):** headless Chrome preview only; no desktop input or game/session used. Added reproducible `scripts/r19-responsive-evidence.mjs` and captured all nine assigned pages at 900/1100/1400/1600 under `artifacts/feature018-r19-ui/before/` (36 screenshots plus `before-metrics.json`). At 900 px Squads/AFK has 23 px page overflow, 36 px main-view overflow, six viewport offenders and eight clipped text fields; AFK still has eight clipped fields at 1100. Equipment Schemes has 34 clipped preset/hero/equipment labels at both 900 and 1100. The Map table currently reports `overflow-x: visible` and is squeezed to 662 px at 900 rather than scrolling inside its table container. The top bar is 90 px tall at both 900 and 1100, confirming the early two-row wrap. Visual baseline also confirms Automation's paired-card vertical gaps, Mini Games' half-empty final row and tight City Layout inspector. Next: CSS-only responsive corrections, then the full en/zh-CN × light/dark × 900/1100/1400/1600 acceptance matrix.
- **Responsive CSS + viewport acceptance complete (2026-10-19):** production runtime change is CSS-only in `styles.css`; no wording or feature behavior changed. AFK toolbar cards now auto-wrap and titles/descriptions wrap; profile list/editor stack at narrower widths. Equipment presets wrap, the rail/main stack at 900-class widths, squad positions use auto-fit columns and narrow position equipment uses a readable 2×2 grid. Automation tabs wrap and the card list uses balanced CSS columns so short cards pack beneath short cards. Mini Games uses auto-fit cards with the odd final card spanning the narrow two-column row. The top bar is a single tidy row at 1100+ and a deliberate two-row layout below. Map tables restore their recovered minimum widths and scroll horizontally only inside `.map-table-scroll`; City Layout reserves a 250–280 px inspector at 1100-class widths and stacks below 1000. Headless acceptance covered **144 combinations** (9 assigned pages × 4 widths × en/zh-CN × light/dark) and reports zero page overflow, zero layout overflow, zero viewport offenders and zero untitled clipped-text cases. Top bar height is 52 px at 1100 and 84 px at 900. `map-actions-ready` at 900 is a 1480 px table inside a 652 px `overflow-x:auto` container. After evidence: `artifacts/feature018-r19-ui/after/` (36 screenshots) plus `after-metrics.json`; every page now has matched before/after evidence at 900/1100/1400/1600. Next: canonical UI build/check, full Desktop.Checks, then final R19 package/UID scan.
- **Build/check checkpoint complete (2026-10-19):** `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build` PASS (`LWB317_PRODUCTION_UI_BUILD_OK`, source fingerprint `631065160fc8d070f41892746376c4fe693146d7eb0b34328f77459f0f7f47b3`, artifact fingerprint `1bb97ad3411bd600461b43427dcafa2ce402299c99d91a41989b562aa75e9037`); `check:production-build` PASS with the same pair; full `npm ... run check` PASS including complete UI keys for all nine locale catalogs. The first `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release` build attempt stopped before tests because its internal `npm ci` could not unlink `node_modules/@esbuild/win32-x64/esbuild.exe` while this worker's headless Vite preview still owned that file (`EPERM -4048`). After stopping only that task-owned preview server, the identical Desktop.Checks command PASSed through `HOME_FEATURE_DELIVERY_CHECKS_OK`; its native checks report no game launches. No source correction was needed for the lock. Next: prepare/push the R19 source-pinned package script, fresh Windows publish, final ZIP CRC/hash and ASCII/UTF-16 UID scan.
- **R19 package source prepared (2026-10-19):** `tools/feature018_r19_package.py` carries forward the accepted R18 clean-pushed-source identity, exact Windows ProductVersion, production-UI fingerprint, linked OverviewBridge source hashes, ZIP CRC, per-entry SHA-256 and embedded release-integrity manifest gates. R19 additionally requires the responsive CSS/evidence source contract and records the 9-page × 4-width × 2-language × 2-theme qualification without claiming gameplay. README-R19 states that R19 is UI/CSS-only and preserves the known R18 Monster AFK runtime and canonical Equipment Schemes wiring gaps. Package cleanliness is based on tracked source/index state so the owner's pre-existing untracked `.claude/` workspace metadata remains untouched and is never packaged. The package scanner checks every uncompressed publish/ZIP/manifest entry for all current/historical test UIDs in ASCII, UTF-16LE and UTF-16BE; inert positive-detection witnesses PASS for all three encodings. Python compilation, responsive-source preflight, retirement preflight and account-agnostic source preflight PASS. After this package-source/audit checkpoint is pushed, create a fresh Windows publish and owner ZIP from that exact remote HEAD; the external `r19-package-receipt.json` is authoritative so no later source/doc commit will invalidate the source pin.

## Lead audit of R19 — 2026-10-19 (source 6548ede5): ACCEPTED for owner test
HEAD = remote 6548ede5, runtime change is styles.css only (+101 lines) plus an evidence script. UI check and
Desktop.Checks PASS (lead re-run). Lead re-checked in the preview: at 900 px Squads/AFK, Equipment Schemes and Map
Data have no horizontal page scroll and no cut text; Squads toggle cards and profiles reflow; Equipment presets wrap;
Automation cards pack in two columns and tabs wrap; Mini Games (zh-CN, 1100 px) shows three even cards; Map table
scrolls inside itself. Minor: at 900 px the Automation tab "Resource gathering" wraps onto two lines (acceptable).
ZIP `artifacts/feature018-r19-6548ede5/release/LW-Control-FEATURE-018-R19-OWNER-TEST-6548ede55aba.zip` SHA-256
657e11ad…99cb76: re-hashed, CRC ok, 92 entries, SOURCE-COMMIT = 6548ede5, no test UID.

## Lead status of the Squads / AFK tab — 2026-10-19
Source check: the only native `automation_start` handler is the Food House `sheepGame` solver
(tools/feature018_sheep_solver.lua). There is no runtime for Monster AFK (`monsterSweep`), automatic stamina items
(`staminaPotion`) or Auto Alliance Drill — their settings save but nothing runs. Equipment Schemes: native
apply/restore commands work live (R18), but the tab shows preview data and is not wired. The same source check
suggests the Automation tab tasks may also lack runtimes; to be verified before any owner claim.
Owner plans (2026-10-19): Monster AFK — main purpose is attacking level-10 zombies; owner tests next week.
Auto Alliance Drill — owner tests when the alliance drill event starts. Stamina items — owner tests when free.
Lead clarified to owner: these are NOT built yet (no runtime); they must be built before any test.

## R20 assignment — build Monster AFK, stamina items, Auto Alliance Drill (2026-10-19, due before next week)
These three have UI and saved settings but NO runtime. Build real engines that follow the existing Squads / AFK
settings exactly (recover original 0.3.17 behaviour/contract from research/offline-controller evidence where
available; label OWN_DESIGN where unknown). Background only: the owner may be using the PC — no mouse/keyboard,
no Windows-MCP input, no focus stealing; drive the game only through the app's own commands/native bridge.
Account rules unchanged (sub 4799432499002212 on 2212, lock on UID; monsters only, never players).
1. **Monster AFK** (owner priority: attacking level-10 zombies):
   - master switch on/off; each Shared AFK Profile's target type/name, level and distance limits, "attack level by
     level", squad assignments, execution count (0 = forever), continuous attack (no return);
   - find targets with the game's own monster search / world data, send the assigned idle squad with the game's own
     march path (reuse the proven Q/W/E/R native send in tools/feature018_attack_actions.lua), wait for the battle
     result / return, repeat; skip world bosses and rally-only monsters unless the profile says "Launch rally";
   - "Launch rally" and "Join Alliance rallies" options: build them too, using the game's own rally routes;
   - pause when stamina is too low (unless stamina items are on), when a squad is busy, or when the account
     changes; Stop always stops new sends; status shown in the app (running / waiting / current target / count).
   Live proof on 4799432499002212: one profile on squad 1, level-10 ordinary zombie monster, execution count 3 →
   three confirmed attacks then it stops by itself; then Stop mid-run works.
2. **Use Stamina items automatically**: when stamina is below the set limit, use the cheapest owned stamina item
   (never buy with gems unless the setting says so), confirm stamina went up. Live proof on 4799432499002212
   (stamina below limit → one item used → stamina higher, item count −1).
3. **Auto Alliance Drill**: when an alliance drill is running, the first free squad in the priority order launches
   a rally on the drill target, others join; stops when the drill ends. Build + offline checks; live proof only if a
   drill is running (otherwise the owner tests when the event starts).
4. Owner-test ZIP (R20 package script, no test UID), README with how to use each feature and what was proven.
Checks: new Lua checks for each engine (start/stop/limits/stamina pause/account change/no player targets), UI
check,
Desktop.Checks. Handoff rule unchanged; record progress under "R20 progress".

### R20 progress (worker updates this)
- **Task 1 in progress, first real ordinary-monster loop proven (2026-10-19):** added an OWN_DESIGN Lua scheduler and current v26 adapter, trusted persisted-profile command admission, background runtime pumping and initial native AFK screen wiring. Recovery read `origin/research/offline-controller` R8-024/R8-047 and the exact 0.3.17 SquadPanel (SHA ed466e35…06c7): these recover configuration/UI contracts, not the original provider engine. Current native search/march routes are separately recovered from admitted v26 bytecode. New inert scheduler checks pass (shared execution reservations, Stop, busy/stamina/account pauses, target/level/distance/name limits, never player targets); existing attack source/Lua checks and Windows build passed before the live loop. In task-owned packaged session `artifacts/map014-takeover/native-ui-20261019-052803`, exact approved UID/server admission preceded every staged command. One profile on squad 1 attacked ordinary monster ID 2023010 at level 10 three times, with distinct native marches 1427557186842174531 / 1427557186846368960 / 1427557186850563166, exact server-derived create GUIDs and observed native return state. `r20-count3-final.json` reports processed=3, state=completed, running=false. A separate unlimited run was stopped while march 1427557186863146110 was in flight; it returned, then remained idle across nine subsequent observations, stamina fixed at 145, running=false, processed=0 (`r20-stop-command.json`, `r20-stop-after-*.json`, `r20-stop-final-status.json`). One read-only status poll hit a transient OS “Access to the path is denied”; a subsequent status read succeeded, with no mutation replay. Own app/game exited normally and all three installed script hashes exactly restored, journal absent (`owned-exit.json`). Owner R19 app PID 49572 was left untouched. **Task 1 is not finished:** rally/continuous routes and full UI/config/status qualification are still being completed; no stamina/drill/package completion is claimed.

- **Task 1 qualification correction (2026-10-19):** pushed scheduler/adapter checkpoint `2d796b1b` and source/correlation corrections `3778c332`. Full Desktop.Checks PASS through `HOME_FEATURE_DELIVERY_CHECKS_OK`, including the new trusted-settings/UID command-boundary check; UI check, new native-shaped mounted AFK screen check, scheduler/native Lua checks, existing attack, bridge, sheep and owner-transition checks PASS. The next background packaged session `native-ui-20261019-055023` exposed two real defects: empty arrays were being tagged through the Overview owner instead of the feature serializer, and the second search reused the prior result's `moved` latch. The ordinary loop was stopped at one completed attack while waiting for the next target; this is preserved as a failed final qualification, not a three-attack pass. Fixes now tag through the actual feature transport and reset acquisition state for each new search. Distinguishing inert checks exercise two successive native search replies/AOI acquisitions and empty-array tagging without an Overview Array method. A further fresh packaged rerun is required. No desktop input was used; the stopped trial exited and restored scripts.

- **Task 1 complete for assigned implementation/proof scope (2026-10-19):** corrected source `12b146dc` passed a fresh background packaged run in `native-ui-20261019-055515`: exact admitted test UID/server, native Chinese catalogue names, real array-shaped options/empty monsterIds, unlocked squads [1,2,3]. One squad-1 profile completed **three ordinary level-10 zombie attacks** and stopped itself: distinct native marches 1427557187148358881 / 1427557187152553193 / 1427557187160941651; `r20-fixed-count3-final.json` has count=3, completed, running=false, three matching response-derived create GUIDs and native returns. A further two-attempt continuous profile reused the **same march** 1427557187169330337, with the second send confirmed by its server-applied target/type/startTime change; there was no idle/home reset between sends (`r20-continuous-08.json`). The server briefly labels these marches Returning before retarget; this proves continuous reuse before reaching HQ, not a permanently stationary native state. Both attempts ended, and the squad was already home; an unnecessary cleanup recall was safely rejected before dispatch because no owned march remained (`r20-continuous-cleanup-recall.json`). A new unlimited ordinary profile was stopped in flight; ten later observations remained idle with stamina=75 and running=false/count=0 (`r20-fixed-stop-*.json`). Settings were disabled/temporary profiles cleared. Own app/game exited 0; all script hashes restored exactly and journal absent. Native rally launch/join routes, timing/leader filters, removal exclusion and player-rally rejection are built and inert-qualified; live rally/event positives remain unobserved. A mounted native-shaped UI check proves real catalogue/squad loading, profile saves, master Start/Stop and unique IDs across remounts. A transient Windows file-replacement error in the proof queue is handled with bounded atomic publication of the **same operation ID**, never a gameplay replay. Task 2 (automatic stamina item consumption) is next; no stamina/drill runtime completion is claimed yet.

- **Task 2 implementation/check checkpoint (2026-10-19):** added an owner/session-bound stamina scheduler and the recovered `ItemData.GetItemList(GOODS_TYPE_3, GOODS_TYPE2.StaminaItem)` / `ItemUse {uuid,num=1}` route. It uses owned stock only and has no purchase route. Owned items all cost zero gems; the existing explicit Prefer 50-point item option breaks that cost tie, otherwise the smallest recovery item is selected. A use is counted only after the native item's count falls by exactly one AND native stamina rises; unresolved outcomes retain a one-shot fence. Stop suppresses further uses, account changes pause the captured owner, and pending item confirmation pauses new AFK sends. The existing stamina controls now read/save real profile config and start/stop the native task. Runtime wait/confirmation/item-count feedback is localized in all nine app languages. Also separated trusted AFK Configure from explicit Start, preserving completed execution counts across ordinary profile edits. New inert stamina/native item tests and mounted UI/config tests PASS; Desktop.Checks and full UI check PASS at this checkpoint. Live item consumption still needs the next packaged run.

- **Task 2 inventory-reader correction, not an owner resource gap (2026-10-19):** two background sessions (`native-ui-20261019-061555` and `061948`) returned zero rows from the legacy `UIFormationAddStamina` category. Both exited normally with exact script restoration. The owner then supplied a backpack screenshot showing 258 × 50-point and 333 × 10-point stamina items; the worker's request for the owner to add an item was based on the wrong reader and is withdrawn. Current v26 `FormationStaminaSlider.OnAddClick` calls `LWResourceLackUtil:GotoSpecialResLack(ResLackContextType.Energy)`, not that legacy screen. The corrected reader follows `LWResourceLackManager:GetSpecialResWay(Energy)` → UseItem-only `para1` → `ItemData:GetItemById`, with current `GoodsUtil.GetGoodsReturnItemCount` recovery and `LWResourceLackCell.OnMultUseClick` ItemUse payload. It also uses ItemInfo.itemId (not template.id). An inert regression leaves the legacy list empty while current Energy routes expose owned stock; purchase routes and duplicate item routes are excluded. A new packaged native observation and one-item proof remain required.

- **Task 3 implementation/check checkpoint (2026-10-19):** built the independent OWN_DESIGN Drill scheduler, recovered native AttackStage/deadline/target reader and source-native JumpToDrill acquisition for an armed active event only. Priority order chooses the first free squad to lead when Active Rally is enabled, then eligible allied monster rallies on the exact Drill monster are joined under saved restrictions. The native CheckJoinAllianceWarByWarData explicitly rejects the account joining its own leader rally; the engine preserves that game permission, so other squads wait for eligible allied leaders instead of forcing an own-rally join. Event end, changed target and Stop cancel unsent choosers before dispatch; already sent marches are only observed. Shared reservations prevent the one native formation chooser from being overwritten. Drill and Monster master controls are independent; native members/status replace preview data in the production Drill controls. automation_status now overlays real native state for these three engines. Pure Drill/native adapter checks cover lifecycle, event end/target drift, priority, stamina/account pauses, Stop, purchase exclusion and never player targets. Also fixed disabled/deleted AFK profiles dispatching a pending unsent chooser. Full UI check and Desktop.Checks PASS. Live event availability still needs a background read-only observation; no Drill live positive claimed.

- **Task 2 complete with genuine one-item proof (2026-10-19), source 8281420b:** fresh task-owned background packaged session `artifacts/map014-takeover/native-ui-20261019-105043` admitted exactly the approved UID/server. The corrected current Energy catalog matched the owner's screenshot exactly: item 400401 recovery 50/count 258 and item 400402 recovery 10/count 333 (`r20-energy-corrected-before.json`). One ordinary level-10 zombie attack reduced stamina 120→110 and self-stopped at execution count 1, native march 1427557189996291304 with matching create GUID and observed return. Threshold 111 / Prefer 50 off then selected the cheapest owned 10-point item. `r20-energy-item-after-01.json` proves native count **333→332**, native stamina **110→120**, used=1, confirmed=true, pending=false; 50-point stock stayed 258. No gems/purchase route, no further item use. Production automation_status independently reflected processed=1 / item used=1 and the live Drill wait state. All three tasks stopped; temporary settings were disabled/cleared. Own app/game exited 0 and `owned-exit.json` confirms all three script hashes exactly restored and no recovery journal. The earlier “empty inventory” claim is corrected; no owner resource action was needed.

- **Task 3 complete for implementation/available-event scope (2026-10-19):** in the same restored source-8281420b packaged session, read-only native Drill status reported stage=1 / active=false (`r20-drill-event-before.json`). Enabling the independent Drill task with priority [2,1] produced running=true/state=waiting/step=waiting_event and confirmedSends=0 (`r20-drill-armed.json`); production automation_status showed this same native wait state alongside the finite Monster result and item confirmation. Stop returned running=false/state=stopped/confirmedSends=0. The worker did not start/sign/donate/claim an event. No active-event rally positive is claimed; the owner tests it when the event starts, as assigned. Additional inert integration now drives the actual native Drill adapter and proven attack adapter together: a real-shaped Drill chooser opens without a send, event end cancels before send, an active fixture dispatches the native rally route, and Stop suppresses another send. Core status rows now clear their running flag after finite completion/Stop. All required Lua checks pass; final full UI/Desktop checks are running. Task-owned game is closed and exact script restoration is proven.

- **Final checks/package-source checkpoint (2026-10-19):** full UI check PASS including nine-language key completeness and mounted AFK/config/Drill adapter checks; full Desktop.Checks PASS through HOME_FEATURE_DELIVERY_CHECKS_OK (`ui-final-checks.log`, `desktop-final-checks.log`). All three engine suites, combined native adapter, existing attack, game bridge, owner transition and 37 sheep checks PASS. The native rally dispatch error path now retains an uncertain reservation across Stop/configure; a distinguishing fixture proves it cannot be dropped as an “unsent chooser” and replayed. `tools/feature018_r20_package.py` requires clean pushed tracked source, exact Windows ProductVersion/SOURCE-COMMIT, current production UI fingerprint, exact hashes for all linked bridge sources, ZIP CRC/per-entry integrity and every-entry ASCII/UTF-16LE/UTF-16BE scans for current/historical test UIDs. Its new README explains real AFK/stamina/Drill use and the exact proven/conditional boundaries. Source/retirement/engine preflights and positive UID-detection witnesses pass. Next after this checkpoint is pushed: fresh final Windows publish and ZIP; the external `r20-package-receipt.json` is the authoritative final artifact identity, avoiding a later doc-only commit that would stale the executable pin. Game remains closed and all scripts restored.

- **Task 4 complete, final source-pin handoff (2026-10-19):** the first fresh R20 Windows ZIP from pushed source 37d5bfd5 passed the packager's executable/ProductVersion/source match, production UI fingerprints, all 20 linked bridge-source hashes, CRC, all-entry manifest SHA checks and ASCII/UTF-16LE/UTF-16BE scans for every current/historical test UID. It has 96 entries; `artifacts/feature018-r20-37d5bfd5/release/r20-package-receipt.json` records SHA-256 f3a9ac1c3ccc147a15950a6180e5788a73c5557cbaff9b31ac52185522cd48dc. This audit-only completion checkpoint is committed/pushed, then the identical runtime is freshly rebuilt/repackaged from that final HEAD into `artifacts/feature018-r20-final/`. Its external `release/r20-package-receipt.json` is the authoritative delivered ZIP path/hash/source identity. README-R20 describes how each engine is used, the actual three-attack/continuous/Stop and one-item proofs, native own-leader rally restrictions, and the inactive-event/owner active-Drill test boundary. R20 is ready for independent lead review/owner test, not merged or whole-feature accepted. No own game/app remains running; all scripts exactly restored, no journal. Owner `.claude/` metadata remains untouched/untracked.

## Lead audit of R20 — 2026-10-19 (source bc3e32e6): ACCEPTED for owner test
HEAD = remote bc3e32e6, tree clean. New AFK engine, AFK native, Drill engine and Stamina engine checks, attack
checks, Desktop.Checks and UI check (9 languages) PASS (lead re-run). All R20 live sessions exited 0 with
restoration proven; live game scripts are exact v26 originals; no journal; no game/app process running.
- Monster AFK: **live** — 3 attacks on level-10 ordinary zombies then self-stop (055515, strategyProcessed=3);
  continuous attack reused the same march; Stop mid-run left the squad idle. Rally launch/join: built and
  offline-checked only (no live rally). Note: the 055515 status row still said running=true at "completed";
  worker reports this flag was fixed later in source.
- Stamina items: **live** — 10-point item used once (333→332), stamina 110→120, no gems (105043).
- Auto Alliance Drill: built + offline checks; live only showed "waiting for event" (no drill running). Owner tests
  when the event starts. Game rule: a player cannot join their own leader rally, so other squads wait for allies.
ZIP `artifacts/feature018-r20-final/release/LW-Control-FEATURE-018-R20-OWNER-TEST-bc3e32e6cfcf.zip` SHA-256
9477d6d8…55fbbdc: re-hashed, CRC ok, 96 entries, SOURCE-COMMIT = bc3e32e6, no test UID.

## Owner test of R20 and R21 assignment — 2026-10-19
Owner results:
- F9 random relocation: **WORKS** (owner). F10 alliance relocation: **does not work** (no details yet).
- City Layout: buildings show only item numbers (#10113000) and tiny "Level N" text — no building names.
- Alt+1: banner "ApplyEquipmentPreset: No confirmed owner equipment preset is saved." Cause (lead): Alt+1–4 need
  presets saved in Squads → Equipment Schemes, but that tab still shows sample data and cannot save real presets
  (R18 gap). Also the hotkey error banner (HotkeyFeedback.jsx) never disappears, even after changing tabs.
- Q/W/E/R: a squad that is already out (marching, attacking, or recalled and returning) cannot be sent to a new
  target — it must reach home first. Owner wants to send it again straight away, as the game allows.
R21 tasks (background only unless the owner says the PC is free; owner tests physical keys):
1. Q/W/E/R re-target: if the squad for that key is out (marching, returning after battle, or recalled), send it
   to the hovered target directly with the game's own change-target/redirect route (the Monster AFK continuous
   path already reuses a returning march). Remove the "squad busy, wait for home" refusal for these cases. Keep the
   guildless-only and never-own-alliance rules. Prove via native commands on 4799432499002212: attack → retarget
   while marching; attack → A recall → retarget while returning.
2. Hotkey error banner: auto-hide after ~6 s, add a close (×) button, clear on tab change; successes stay silent.
3. Equipment Schemes tab: wire it to the real game (read current gear, save presets 1–4, apply, restore) so Alt+1–4
   have real presets; replace preview data in production. Prove save + apply + restore through the app.
4. City Layout: show each building's real Chinese/English name from game text (same localization source as Map
   names) instead of #itemId; make level text readable at 100% zoom; tooltip with name + level.
5. F10 alliance relocation: diagnose why it fails for the owner (read-only checks of the native preconditions:
   alliance membership, alliance territory/target, item, cooldown) and fix; report the exact reason. Do not
   relocate the owner's main; live relocation only on 4799432499002212 if the precondition is available.
6. Owner-test ZIP (R21 package), README updated. Checks: UI, Desktop.Checks, relevant Lua suites.

### R21 progress (worker updates this)
- **Task 1 implementation checkpoint (2026-10-19):** traced admitted v26 UIFormationSelectListV2Ctrl.OnAtkClick/ChangeMarchByType and WorldMarchChange serializer. Native hover admission now accepts a verified owned existing march; the selected-controller path invokes OnAtkClick with the formation UUID and retains the same owned march UUID. Change confirmation uses the R20 one-send fence plus changed target/type/startTime server poststate, without inventing a create GUID. Typed guildless/never-own-alliance target checks remain at the final send boundary. Managed hotkey receipts distinguish native change transactions from create transactions; the physical cursor proof remains the owner's. Attack source/Lua suite, AFK native regression and Windows build pass. Background native proof of outbound and recalled-returning redirects is next.

- **Task 1 live qualification correction:** an early launch raced its publish and selected an old local app; that app rejected its obsolete 2289 account policy before gameplay and exited with exact restoration (`native-ui-20261019-113911`). The verified d3d00a76 background session (`114018`) admitted the exact current UID. A level-1 candidate's initial create send remained unconfirmed (no owned march/debit observed); it was never replayed. A separate squad-2 level-4 ordinary-monster create was confirmed with native GUID/march 1427557190671574168. Its native MOVING state exposed the second old guard: an out squad's saved formation has zero soldiers, so the idle-staffing predicate blocked redirect. The corrected path applies that staffing predicate only to idle formation creation; existing marches require fresh exact UID/formation/march identity and the game's native broken/redirect checks. The failed redirect was pre-dispatch; no change request was sent. The session exited/restored exactly. A new source-shaped test uses zero saved-formation soldiers for an existing march, and a managed positive/unchanged-startTime negative covers physical-hotkey change receipts. Fresh native rerun is next.

- **Task 1 complete (source ad5ffcfd, background native session 114524):** squad 2 created march 1427557190696739933, then redirected while native status was MOVING (oldKind=1), with one WorldMarchChange and a fresh target/startTime on that same UUID (`r21-retarget-proof.json` movingRedirect). The immediate recall/redirect attempt reached HQ too quickly and remains a preserved unconfirmed negative; it was not replayed. A separate one-shot squad-3 trial waited six seconds outbound before recall, observed BACK_HOME (oldKind=3), then redirected successfully on the same march 1427557190700934279 (`r21-returning-squad3-proof.json`, state=proven). Both successful changes were native commands, with no physical-key claim. Cleanup recall succeeded, final inventory has no owned marches, and the task-owned session was finished for exact restoration. Guildless/player and exact UID guards remain in the production path; no players were targeted in these proofs. Tests distinguish missing create GUID from native change poststate, zero stored soldiers while out, stale startTime, duplicate/unattributed sends and foreign-owner marches. Next: transient banner UI.

- **Task 2 complete:** HotkeyFeedback expires each failure after 6 seconds, exposes a localized Close button, clears on top-level route/profile change and on an actual subtab change, and keeps Confirmed outcomes silent. A mounted inert check covers timer expiry/replacement cleanup, close, route/subtab clearing, silent success and stale-owner events. Task 1's native session 114524 exited 0 with all three scripts restored exactly and no journal (`owned-exit.json`). Next: real Equipment Schemes tab.

- **Task 3 implementation checkpoint:** production Equipment Schemes now reads the native owner gear/inventory, saves ordinal presets 1–4, edits owned UUID slots, applies with one-shot native transactions and resumes pending confirmation. Initial native equipment is saved with UID/server; the new restore command reads that trusted baseline without overwriting presets. Native hero/equipment name keys use the installed game-text reader in the app language. Only the selected preset's full loadout is transmitted, retaining ordinal identities and the native wire bound. Mounted UI, native equipment (30 checks), and full Desktop.Checks pass, including trusted restore, no caller payload substitution, baseline owner fence and exact preset preservation. Background native apply/restore proof is next.

- **Task 3 complete (31a77179 equipment source, native session 115806):** exact UID admission passed. Through the app commands, saved all four ordinal schemes, removed gear UUID 1376833201183958087 from one native slot (one completed uninstall), then used the new trusted initial Restore endpoint (one completed install). Every native hero/slot map and every inventory wearer matches the starting snapshot; saved schemes and initial baseline are unchanged after Restore. Native metadata includes real hero key 900549 and equipment key 431073/configId 410100/level 40. Evidence: r21-equipment-proof.json state=proven. No physical keys or desktop input. The test-owned config is restored by staged cleanup. F10 was not invoked in this session (the build contained an uncommitted offline F10 candidate); Task 5 gets a fresh qualified build. Finished the owned session for exact script restoration. Next: City Layout native names/readable levels.

## Lead audit of R21 partial — 2026-10-19 (pushed 6ab1220a; worker stopped mid-round)
Pushed = remote 6ab1220a. No game/app running; live game scripts are exact v26 originals; no journal.
Lead re-ran attack, hotkey, AFK native, City, relocation and equipment Lua checks, the new R21 City-names UI check,
Desktop.Checks and UI check: all PASS (including the uncommitted work below).
- Task 1 Q/W/E/R re-target: ACCEPTED (native commands, session 114524): redirect while MOVING on the same march;
  redirect after recall while returning (squad 3). Physical keys = owner test.
- Task 2 error banner: ACCEPTED (6 s auto-hide, Close button, clears on tab change).
- Task 3 Equipment Schemes tab: ACCEPTED (session 115806): real gear read, presets 1–4 saved, apply + restore,
  every slot back to the start. Alt+1–4 physical = owner test.
- Task 4 City names: CODED, NOT COMMITTED (CityLayoutPage.jsx, styles.css, feature018_city_game.lua + checks,
  scripts/check-r21-city-names.mjs); offline checks pass; no live proof yet.
- Task 5 F10: CODED, NOT COMMITTED (feature018_relocation_actions.lua + checks, large change); no diagnosis report
  or live proof yet.
- Task 6 ZIP: NOT BUILT.
Next worker: review and commit the uncommitted Task 4/5 work (do not discard), live-prove City names in the
background, report the F10 root cause, then build the R21 ZIP.

### R21 progress — Task 4/5 recovery completion attempt (2026-10-19)
- **Task 4 City names implementation committed (54e08664):** the native City snapshot now carries the current
  building-level game-text key; the production City Layout resolves those keys through the installed game-text
  reader in the app-selected language. At 100% zoom the grid uses compact numeric levels, and each building button
  retains the full localized name plus level in its tooltip/accessibility label. The R21 City-names mounted check
  is part of the normal UI check. Offline City and UI checks pass.
- **Task 4 live proof is blocked by an owner-owned active session, so no screenshot is claimed.** The required
  background proof launcher refused to start because the global recovery journal was already active. Independent
  ownership review tied that journal to the owner's currently running R20 owner-test app: the app started at
  20:19:21 local, its recorded game session started 15 seconds later, the journal/profile heartbeat is the approved
  test UID on server 2212, gameDesiredRunning=true, and the installed scripts are still that session's candidate
  bytes rather than the journal's captured originals. I did not clear the journal, restore scripts, stop the app,
  or attach my proof driver to it. Therefore the required English/Chinese live City screenshots remain pending.
- **Task 5 F10 root cause, in plain words:** the previous recovery read the original v26 Lua equality/jump branch
  backwards. When the player's source server equals the self/home server, the original game sends AllianceMoveCity
  with cost type 4. The old recovered adapter treated that valid equal-server branch as the cross-server path and
  suppressed it, so F10 could reach prechecks yet never send the normal same-home alliance relocation. Executed
  original-bytecode tests now prove equal server -> AllianceMoveCity; unequal server -> cross-server helper.
- **Task 5 implementation committed and hardened (4c79bcc0, ab08e487):** same-home F10 uses the recovered original
  sender and keeps exact UID/server/alliance/rally/item/warning/cooldown evidence. The chooser and direct paths
  revalidate the exact AllianceMoveCity(4,false,nil) route at the network edge, fail closed if the send observer
  cannot attach, keep a one-click/one-send tombstone through ACK/Dispose, block owner/route TOCTOU and foreign-server
  selections, and confirm only from one attributed send + reason-240 reply + independent position and item-debit
  poststate. The relocation suite is 79/79 PASS. Independent adversarial review re-ran hook-install denial, owner
  change, MoveCrossServer substitution, foreign double-selection and successful immediate double-click cases with
  zero unobserved retries.
- **Task 5 live preconditions/relocation were not run because the same owner-owned R20 recovery session still owns
  the global game bridge state.** No F10 relocation request was sent by this worker. This means alliance membership,
  current native rally/territory target, relocation-item stock and cooldown still need the requested fresh read-only
  observation in a task-owned exact-UID session before a live F10 attempt; if any one is missing, owner test should
  report that blocker instead of relocating.
- **Task 6 packaging implementation:** tools/feature018_r21_package.py verifies the final pushed source identity,
  canonical production UI and linked bridge sources, checks R21 re-target/banner/Equipment/City/F10 source markers,
  embeds an R21 README covering all five owner-facing changes, and scans every ZIP entry for known test-account UIDs
  in ASCII, UTF-16LE and UTF-16BE. The final ZIP is produced only after the tracked tree is committed/pushed and the
  complete R21 verification set passes.
- **Task 6 verification checkpoint (b271c4f9):** fresh Release publish + R21 packaging gates pass: exact pushed
  ProductVersion/source identity, production UI fingerprint, 20 linked bridge source hashes, 118 ZIP entries,
  CRC/per-entry hashes, and all known test UIDs absent from every entry in ASCII, UTF-16LE and UTF-16BE. The
  verification ZIP SHA-256 is 8e220d4513420c7ef5aad90a8b59af8ec77544f6d4f2f0f9fa25302aa6e00da6.
  After this audit checkpoint is pushed, the authoritative R21 owner-test ZIP is regenerated from that exact final
  HEAD and identified by artifacts/feature018-r21-<HEAD8>/release/r21-package-receipt.json; no later tracked change
  is needed to identify it.

## Lead audit of R21 finish — 2026-10-19 (source bf59ccc9)
HEAD = remote bf59ccc9, nothing uncommitted. Attack, hotkey, City, relocation and equipment Lua checks, R21
City-names UI check, Desktop.Checks and UI check PASS (lead re-run).
- Task 4 City names: code ACCEPTED offline; live screenshot NOT done.
- Task 5 F10: root cause found — the recovered code read the game's "same server" branch backwards, so a normal
  same-home alliance relocation was blocked as if it were cross-server. Fix committed; 79 relocation checks pass.
  NOT live-proven.
- Both live proofs were blocked correctly: the owner's own R20 app + game were running and held the shared game
  bridge (recovery journal). The worker did not touch the owner's session.
- Task 6 ZIP: `artifacts/feature018-r21-bf59ccc9/release/LW-Control-FEATURE-018-R21-OWNER-TEST-bf59ccc9ad63.zip`
  SHA-256 e9db5764…6d69c0: re-hashed, CRC ok, 118 entries, SOURCE-COMMIT = bf59ccc9, no test UID.
- Found at audit time (22:40): no game/app running, but the owner's R20 session left the game scripts in the bridge
  (candidate) state with journal stage `active_ready_deferred_restore` (game PID 50508, started 20:19). An exact
  backup of the originals exists (`overview-bridge-backups/20261019-121928-…`, hashes match). Restoration pending
  owner OK. Lesson for owner prompts: close the owner's own LW-Control app/game before a worker live test.
Owner approved restore (2026-10-19 22:4x): with no game/app running, lead copied the originals back from
`overview-bridge-backups/20261019-121928-…`; installed hashes now 49f6249d / 52d5b30c / 5f9c4ab0 (exact v26), and the
stale journal was archived as `recovery.lead-restored-20261019.json`. Worker live tests are unblocked.

## Owner test of R21 and R22 assignment — 2026-10-19
Owner results (R21 ZIP):
- City Layout names now show (e.g. Headquarters, Drill Ground, Golden Marshal Statue). But no building images: the
  grid and the Building properties box show an empty grey square. Lead: the UI already renders
  `GameAssetImage assetPath={building.iconPath}`; the native City snapshot never fills `iconPath`.
- Spamming Q on different targets → "Q: Native hover chooser was already consumed once"
  (tools/feature018_attack_actions.lua ~2214): after one send, the consumed hover latch blocks every new Q press
  until that send is confirmed.
- Equipment Schemes tab in the real app: "Equipment snapshot accepts an empty authenticated request only.", no
  squads, Refresh does nothing. Lead: equipmentRuntime.js calls `invokeProfileScoped("feature018_equipment_snapshot")`,
  which adds `profileId`; Feature018GameCommandService.cs (~line 290) rejects any non-empty payload. R21's proof used
  raw commands, not the real tab, so this was missed.
R22 tasks (background only; owner tests keys):
1. Equipment tab: accept the owner-routing `profileId` (only that) for feature018_equipment_snapshot, like restore
   does. Prove through the REAL tab in the packaged app (WebView harness, no desktop input): open tab → squads and
   gear load → save preset 1 → apply → restore, all from the tab's buttons. Add a mounted check that uses the real
   bridge payload shape.
2. Q/W/E/R spam: every new key press is a new intent. A consumed earlier chooser must not block a new press: if the
   squad is out use the R21 redirect, if home create a new march; still never replay an uncertain send and keep the
   guildless/own-alliance rules. Prove three rapid Q presses on different targets via native commands, each one
   accepted or giving a clear specific reason (e.g. "squad already heading to this target").
3. City Layout images: fill `iconPath` in the City snapshot from the game's own building icon (same asset source as
   Map images) so the grid and properties box show the building picture. Prove with a screenshot of the worker's own
   app window.
4. Owner-test ZIP (R22 package), README. Checks: UI, Desktop.Checks, attack/hotkey/City/equipment Lua suites.
Owner reminder: close your own LW-Control app and game before the worker starts live tests.

### R22 progress (worker updates this)
- **Task 1 Equipment Schemes complete (2026-10-19), source `61b2a895`:**
  `feature018_equipment_snapshot` now accepts only the profile-scoped UI routing `profileId` (validated non-empty
  string, max 128) in addition to the legacy empty request, discards it before the game transport, and still rejects
  every other caller field. Desktop exchange coverage includes the routed real-UI shape plus forbidden owner/slot
  fields. The mounted Equipment check now creates the production `createBackendBridge` and proves the real WebView
  wire payload is exactly `{profileId:"A"}`. Fresh exact-commit Windows publish ProductVersion
  `1.0.0+61b2a89547335fd31e73f75359e8f1b9279c955c` passed the dedicated background packaged-tab proof in
  `artifacts/map014-takeover/native-ui-20261019-151531`: authenticated UID `4799432499002212`/server `2212`, 15
  native hero positions and 60 gear selectors loaded, page Refresh succeeded, preset 1 was captured with the tab's
  own button, the tab removed equipped UUID `1376833201183958087` from `1:1:1` and Apply produced the corresponding
  native slot change, then the tab's Restore returned both every slot assignment and every inventory wearer exactly
  to the initial snapshot. `r22-equipment-tab-loaded.png` and `r22-equipment-tab-restored.png` are the WebView
  captures. The task-owned app/game exited 0; `owned-exit.json` proves exact v26 script restoration for all three
  files and no recovery journal. Focused UI/equipment tests and Release Desktop build pass; no desktop input or Land
  Cell action was used. Task 2 rapid Q/W/E/R intent recovery is next.
- **Task 2 rapid Q/W/E/R complete (2026-10-19), production source `e7f8e1ec`:** the consumed physical-hover chooser
  now belongs only to the key edge that created it. A fresh Q/W/E/R order first observes any prior transaction
  read-only; if it is confirmed, the fresh order continues through current native state (existing owned march uses
  the R21 `WorldMarchChange` redirect, no march uses the create path). If the prior send is still uncertain, no second
  packet is emitted and the caller gets the specific reason `Previous native attack is still awaiting confirmation;
  fresh order not sent`. The same settle-then-continue rule now applies to the source-native selected-target path used
  for background qualification, so a new command cannot be silently consumed merely by finishing the older request.
  Attack regressions prove an immediate second physical-hover press no longer returns the old `consumed once` error
  and does not replay the unresolved send; selected-target coverage proves the same one-send fence. Attack and hotkey
  Lua suites pass. Fresh Windows publish ProductVersion
  `1.0.0+e7f8e1ec25a23ef86a3e61dbb86370eacd9b5131` was admitted in background staged session
  `artifacts/map014-takeover/native-ui-20261019-152939` on exact UID `4799432499002212`/server `2212`. The final
  `r22-rapid-attack-proof-attempt3.json` submitted three distinct ordinary-monster target orders to squad 2 in
  1734 ms (UUIDs `1427557187009946785`, `1427557184287843444`, `1427557184296232109`). The squad's pre-existing
  march was currently source-reported broken, so each fresh order received the clear pre-dispatch reason `Existing
  squad is broken and cannot redirect`; zero attack/change requests were dispatched, and the pre-existing march was
  intentionally left untouched. Earlier attempt 1 only performed map/read-only inventory and found too few monsters
  under an overly narrow level filter; attempt 2 opened one native target but the proof driver was too strict about
  accepting the broken-march reason, again with no attack request dispatched. Staged finish exited 0, restored all
  three v26 script hashes exactly and left no recovery journal. No player target, own-alliance target, desktop input,
  or Land Cell action was used. Task 3 City Layout building images is next.
- **Task 3 City Layout building images complete (2026-10-20), production source `75b55f47`, visual-proof source
  `40feb183`:** the native City snapshot now resolves every building picture through the current game's own
  `BuildManager.GetBuildIconPath(buildId, level)`, recovered from the same `UIBuildDetailsView.ShowPanel` path that
  feeds the game's `LoadSpriteAuto`; `iconPath` is returned with the existing native building record and consumed by
  the already-shipped `GameAssetImage` reader. The City Lua suite verifies the original v26 source call plus the exact
  snapshot path, and the City candidate/swap/grid-audit and mounted City checks plus Release Desktop build pass.
  Fresh Windows ProductVersion `1.0.0+40feb183a76120e128e508be7f3ac583058ebf73` passed the final background
  packaged-WebView proof in `artifacts/map014-takeover/native-ui-20261019-161052` on exact UID
  `4799432499002212`/server `2212`. The real City tab loaded 109 buildings, decoded seven grid images, selected a
  decoded Farm (`农田`) and scrolled it into the visible grid, and decoded the matching Building properties image.
  `r22-city-layout-building-images.png` visibly contains building artwork in both the grid and properties panel;
  `native-ui-map.json` records `visibleSelectedGridImages=1`, `loadedPropertiesImages=1`, and `readOnly=true`.
  The task-owned app/game exited 0; `owned-exit.json` proves all three v26 scripts restored exactly and no recovery
  journal remained. No desktop input, layout move/apply, player target, or Land Cell action was used. Task 4 final
  R22 qualification/package is next.
- **Task 4 final qualification/package-source checkpoint (2026-10-20):** the complete production UI check passes,
  including the real-bridge Equipment `profileId` payload regression and mounted City checks; full
  `tests/LWBridge.Desktop.Checks` passes through `HOME_FEATURE_DELIVERY_CHECKS_OK`. The attack-actions,
  hotkey-actions, hotkey-game and game-bridge Lua suites pass. City game/candidate/swap/grid-audit/live-driver all
  pass. Equipment actions pass 30/30 and the equipment integration/native-preflight bridge suite passes.
  `tools/feature018_r22_package.py` carries forward the clean-pushed-source, exact Windows ProductVersion,
  production-UI fingerprint, linked OverviewBridge source hashes, ZIP CRC and per-entry SHA gates, and adds source
  contracts for R22's routed Equipment snapshot, fresh rapid-order settlement fence and native
  `GetBuildIconPath`/`iconPath` City snapshot. `README-R22.txt` explains the real-tab Equipment fix, new-order Q/W/E/R
  behavior, City grid/properties images and the bounded live proof. Every publish/ZIP/manifest entry is scanned for
  all current/historical test UIDs in ASCII, UTF-16LE and UTF-16BE; positive detection witnesses pass for all three
  encodings. After this checkpoint is pushed, create the authoritative R22 owner ZIP from that exact final HEAD in
  `artifacts/feature018-r22-<HEAD8>/release/`; its external `r22-package-receipt.json` is the final artifact identity,
  so no later tracked source/doc commit is needed. Game remains closed, original scripts restored, no journal.

## Lead audit of R22 — 2026-10-20 (source 1f45410f)
HEAD = remote 1f45410f, clean. Attack, hotkey, City and equipment Lua checks, Desktop.Checks and UI check PASS
(lead re-run). No game/app running; live game scripts are exact v26 originals; no active journal.
ZIP `artifacts/feature018-r22-1f45410f/release/LW-Control-FEATURE-018-R22-OWNER-TEST-1f45410f6d4e.zip` SHA-256
c41b3223…e3cc98: re-hashed, CRC ok, 96 entries, SOURCE-COMMIT = 1f45410f, no test UID.
- Task 1 Equipment tab: ACCEPTED. Lead viewed `r22-equipment-tab-loaded.png`: real heroes (Lv.100) and gear names
  load in the real tab; preset save/apply/restore done from the tab's buttons (session 151531).
- Task 2 Q/W/E/R spam: logic ACCEPTED offline (no more "consumed once"; uncertain sends never replayed), but the
  live proof sent nothing — all three orders were refused because the squad's existing march was "broken". Owner to
  test with a healthy squad.
- Task 3 City images: PARTIAL. Lead viewed `r22-city-layout-building-images.png`: in a block of identical level-19
  Farms only the left column shows the picture; the other Farms in the same rows are blank ("decoded seven grid
  images"). Likely image loading stops after a few requests or only for visible-first cells. Needs a follow-up fix.
R22 published as pre-release hotkeys-test-r22-2026-10-20 (owner approved). Next round (owner decision): fix City
Layout pictures so EVERY building in the grid and properties box shows its game picture (find why only ~7 decode —
request limit, de-duplication by path, visible-first loading, or cache misses), prove with screenshots of a full city
view at 100% and zoomed out.

## Owner test of R22 and R23 assignment — 2026-10-20
Owner results (R22 ZIP, own account) and lead diagnosis:
1. Q: "Native continuous-march transaction lost its single-send fence" — tools/feature018_attack_actions.lua ~2457
   (`AutomationObserve`): a previous re-target record for that squad (automationRetarget/hotkeyRetarget) whose
   sendCount is not exactly 1 (e.g. a re-target that was refused before sending, sendCount 0) is never cleared and
   blocks every later press. Fix: a record that never sent is discarded; only a truly uncertain send (sent but not
   confirmed) may block, with a clear message, and it must clear once the game state settles.
2. F10: "Native owner alliance UID not verifiable" — tools/feature018_relocation_actions.lua `alliance_rally_snapshot`
   normalises the alliance id with `uid()`, which only accepts decimal digits. Current v26 alliance ids are hex
   strings (R15 receipt: "15f557d90bf9485594cc0ebb6fd89ebc"), so F10 fails for every player in an alliance. Fix:
   read the alliance id as an opaque non-empty string (as the R15 attack guard does); keep all other F10 gates.
3. App disconnected mid-session ("Reconnecting"); the app then relaunched the game (00:54 → new game PID at 00:58,
   now connected, heartbeat OK). Cause not yet known (Player-prev.log shows no crash line). Investigate: app logs /
   recovery reason (game exit, server disconnect, heartbeat timeout, bridge pipe), and whether any F018 command or the
   Q error path triggered it.
4. Equipment Schemes tab stayed empty with "There is no authenticated owned game session" while disconnected. It must
   reload by itself when the game reconnects (and Refresh must retry), not stay empty.
5. City Layout pictures (queued from R22): every building must show its picture.
R23 tasks: fix 1, 2, 4, 5; diagnose 3 and fix if it is ours. Background only; owner tests keys. Live-prove F10 only on
4799432499002212 if it is in an alliance (else offline + owner test). Owner-test ZIP (R23 package).
Owner reminder: close your own LW-Control app and game before the worker starts live tests.

### R23 progress (worker updates this)
- Task 1 source/offline checkpoint: zero-send re-target records are retired without a replay; uncertain sends retain a clear fence, and independently verified target/startTime or absent-march + idle-formation poststate releases it. Attack source/Lua53 suite passes, including refused re-target -> next explicit order and controller throw after send -> confirmation. Native R23 proof still pending.
- Task 2 source/offline checkpoint: F10 reads owner and rally alliance IDs as exact opaque non-empty strings (hex supported), retaining owner/type/server/point and item/send gates. Relocation suite: 80 checks passed. Live eligibility not yet assessed.
- Session preparation: no current owner LWBridge/LastWar/launcher process found. The stale owner-session journal was reviewed against its exact session/PID/start/path and authenticated backup. Supported `run_overview_bridge.py stop` proved PID absent and restored the exact original v26 triplet; it cleared the stale journal without touching a live owner process. Historical disconnect trigger remains UNKNOWN; controlled restart is PROVEN (see forthcoming R23 disconnect report).

### R23 addendum (owner, 2026-10-20) — part of R23
6. Equipment Schemes dropdowns cannot be clicked. Lead: NativeEquipmentContent.jsx disables each slot <select> when
   the preset has no saved entry for that position (`!saved`), so a fresh preset shows "Empty" everywhere and nothing
   is editable until the user finds "Save current squads". Fix: dropdowns editable on an empty preset too; one-line
   hint ("Press Save current squads to copy your current gear into this preset"); show gear and hero pictures
   (iconPath). Meaning: the dropdown picks which owned item goes in that slot for this preset; the text under it is
   what the hero wears now.
7. Automation → Chat (Red Packet, Fireworks / Egg, Treasure): cards show "Game disconnected. Actions are disabled" /
   "Disconnected" while the header says Connected; nothing can be clicked. Recover the original 0.3.17 behaviour:
   listen to chat and automatically claim red packets, fireworks/eggs and treasure, with each card's settings and
   "Run now". Wire the Automation tab to the real connection state. Real engines with the game's own claim routes;
   never spend gems or real money. Live-prove on 4799432499002212 if a claimable item appears in chat; otherwise prove
   the listener sees chat messages and report; owner tests the rest. Also report which other Automation cards have a
   real engine and which do not (status list only; build only Chat now).

### R23 progress checkpoint — 2026-10-20
- Task 1 native session `native-ui-20261019-172027` admitted the exact approved UID. A squad-2 create was server-correlated by its native GUID; a subsequent change remained uncertain, blocked the next explicit order without replay, then cleared only after the old march was absent and the owned formation idle. A NEW explicit order then received a matching native server-derived GUID. This proves uncertain-send settlement/next-order behavior. The zero-send refusal path is PROVEN offline; that exact live zero-send reproduction remains pending. The earlier three-order script's `proven` label means three intents/refusals only, not three confirmed attacks. Preserve its raw receipt.
- Task 2 native preflight read hex alliance `3785645e55a040a9a1df48e3cca7b06c` and valid home rally/7 items. The direct trial exposed another decimal-only comparison at the final selection edge and refused with zero sends/debits. That last comparison is corrected with a full dispatch-edge hex regression; relocation suite now passes 81 tests. Fresh native corrected trial pending.
- Task 3 complete as a bounded diagnosis: [disconnect report](../FEATURE018_R23_DISCONNECT_DIAGNOSIS_2026-10-20.md). The app deliberately terminated/relaunched the game; initial trigger UNKNOWN because R22 omitted production recovery history. Added bounded persistent per-session trigger/state, heartbeat/route diagnostics without changing restart decisions. Release Desktop checks pass.
- Tasks 4/5 source and mounted checks pass: Equipment reconnect/Refresh retries; empty preset dropdowns; localized copy-current hint; native hero/gear iconPath. Native fresh-edit/Save/Apply/Restore proof pending.
- Task 6 native snapshot had 109 buildings/109 game iconPaths (67 distinct). Eager City images reached the all-buildings-decoded predicate in the real WebView. The initial visual harness failed afterward because its selected-building visibility test ran after zooming without resetting scroll. Retained 100%/zoom captures are partial; strengthened full-view recapture pending. Image-reader identity now refreshes on connection transitions so old failed-path cache entries do not survive reconnect indefinitely.
- First R23 task-owned game session closed; independent audit proves exact v26 script triplet restoration, no game process or recovery journal left. No owner process was controlled.

## Lead tidy-up after Codex R23 (2026-10-20) — handover to CoS
Codex ran out of tokens. Pushed = remote bcaa4685 (tasks 1–2 fixes, equipment reload + recovery-trigger logging).
Codex's last test session `native-ui-20261019-172924` (app from artifacts/feature018-r23-working2 + game) was still
running; lead sent the harness `finish`: exit 0, restoration proven, scripts exact v26, no journal, nothing running.
UNCOMMITTED Codex work (Chat engines + R23 tooling), left in the tree for CoS to review and commit, NOT discarded:
  modified: AutomationStatusCommandService.cs, Feature018GameCommandService.cs, LWBridge.Desktop.csproj,
  LWBridgeWindow.Feature018StagedProof.cs, LWBridgeWindow.cs, ProfileRuntimeOwner.cs, AutomationPage.jsx,
  tools/run_overview_bridge.py; new: ChatAutomationConfigCommandService.cs, LWBridgeWindow.Feature018R23Proof.cs,
  NativeChatAutomationCard.jsx, chatAutomationRuntime.js, scripts/check-r23-chat.mjs, tools/feature018_chat_engine.lua
  (+_checks.py), tools/feature018_chat_treasure.lua, tools/feature018_r23_package.py (+_checks.py),
  tools/feature018_r23_rapid_attack_proof.py.
Lead re-ran with that work in place: attack, hotkey, City, relocation (81), equipment, new Chat engine checks,
R23 Chat mounted check, Desktop.Checks and UI check — all PASS.
Status per task: 1 Q fence — accepted (native uncertain-send settle proven; zero-send path offline). 2 F10 — hex fix
complete incl. final-edge fix; fresh native trial pending. 3 disconnect — diagnosed: the app deliberately
restarted the game; first trigger unknown, logging added (docs/FEATURE018_R23_DISCONNECT_DIAGNOSIS_2026-10-20.md).
4/5 equipment reload + dropdowns — code + checks pass; native tab proof pending. 6 City pictures — 109/109 icon
paths, all decoded in WebView; full-view screenshot pending. 7 Chat — built, uncommitted, no live proof yet; the
Automation-card status list not written yet. 8 ZIP — not built.
Note: the "R23 progress checkpoint" block above appears twice (worker paste duplicate); content is identical.

### R23 progress continuation - CoS recovery
- Recovered the lead-preserved Codex handoff without discarding any listed R23 file. Reviewed the native Chat config/status path, packaged R23 UI proof harness, bridge injection, Chat claim engine and package gates; removed only the accidental extra project-file EOF blank line. Focused `feature018_chat_engine_checks.py`, `feature018_r23_package_checks.py`, mounted `check-r23-chat.mjs`, and `git diff --check` PASS. The owner's pre-existing untracked `.claude/launch.json` remains untouched and excluded from this checkpoint. Fresh corrected F10 native proof is next.
- **F10 fresh native trial complete for server effect (session `native-ui-20261019-174251`):** exact UID/server/session admission passed. Initial same-home F10 readiness saw the recovered hex alliance `3785645e55a040a9a1df48e3cca7b06c`, home rally point `962037`, item count **7**, and city point `572199`; the startup city scene initially blocked the direct route (`Native same-home F10 requires world scene and source-server login`). A same-server map jump to the already observed `(199,572)` entered the world scene without moving the city, after which readiness became `actionable=true`. Exactly one `AllianceMoveCityToSourceRallyPoint` / `AllianceMoveCity` send was issued (`allianceMoveSendCount=1`, cost type 4). Read-only follow-up proves city point **572199 -> 966030** and the exact owned item stack **7 -> 6**, stable on a second observation. The direct reason-240 reply wrapper did not fire (`worldMvReason240Observed=false`, `allianceMoveReplyObserved=false`), so the conservative client correctly retains this one-shot transaction as `relocationConfirmed=false` / awaiting confirmation and it was **not retried**. This trial proves the corrected hex-alliance direct route has a real native server effect and item debit; it does not manufacture the missing reply correlation. Equipment real-tab proof is next in the same exact-owned background session.
- **Equipment real-tab proof complete (same session):** `r23-equipment-ui.json` is `state=proven` from the actual packaged Equipment Schemes WebView. The live tab loaded 15 native positions and 60 gear selectors, its real Refresh button succeeded, a fresh preset was edited through dropdown `1:1:1`, saved, applied to produce a genuine native loadout change, then Restore returned every equipment slot and every inventory wearer exactly to the starting snapshot. Screenshots: `r23-equipment-tab-loaded.png`, `r23-equipment-fresh-preset-saved.png`, `r23-equipment-tab-restored.png`. Reconnect behavior cannot be safely forced inside this exact-PID staged session without retiring the owner it is required to preserve; the current mounted real-bridge regression was re-run and PASSes the disconnected read, Refresh retry, offline->online automatic reload, and zero-mutation-on-retry contract (`FEATURE018_R21_EQUIPMENT_UI_OK`). Live native refresh + real Save/Apply/Restore and mounted reconnect/retry evidence are kept distinct rather than simulating a native reconnect.
- **City pictures full-view proof complete (same session):** `r23-city-ui.json` is `state=proven`, read-only, with **109/109** native buildings decoded through their game-owned image paths. The strengthened harness reset scroll before visibility checks and produced `r23-city-100-percent.png` plus `r23-city-zoomed-out.png`, then selected the native HQ (`总部`) and proved both its visible grid image and properties image in `r23-city-layout-building-images.png`. Receipt counts are `loadedGridImages=109`, `visibleSelectedGridImages=1`, `loadedPropertiesImages=1`, `allBuildingImagesDecoded=true`; no City drag/apply or land action was issued.
- **Chat live listener + first claim attempt (same session):** production `feature018_chat_status` proved the native listener real and owner-bound: `ready=true`, 4 native event listeners, 25 rooms, **254 observed messages** (**33 live event callbacks + 221 retained messages**). Red Packet and Fireworks / Egg each had no claimable target. Treasure found one retained native target and issued exactly one no-spend `DetectEventClaimTreasure` submission (`submitted=1`, `pendingCount=1`); repeated read-only status after the confirmation bound still showed `claimed=0` / `waiting_ack`, so the target was **not replayed and is not counted as a successful claim**. The task-owned app/game then exited cleanly with exact original v26 script hashes restored and no recovery journal.
- **Treasure terminal recovery after that live gap:** archived original current-v26 `Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac` SHA-256 `eb7ac9ba1e7b69779155138fbe87df176f0d65e3987b131839f8706f7d0b9b8c` proves request serialization is `uuid` + `targetServer` (+ optional `type`), while `HandleMessage` uses `errorCode` as failure terminal and on success processes `reward` through `RewardManager` then calls `ActDetectTreasureDataManager:OnGetDigTimesMsg(reply)`. The inherited engine only polled world-point `IsHaveGetReward`, which can be unavailable for a retained/cross-scene target and explains the unresolved live send. Recovery now installs an owner-fenced wrapper around that exact native response handler, serializes Treasure claims to one in-flight request because the response has no source-proven request nonce, restores the original handler on lease cleanup, and advertises Treasure claim capability only while the terminal observer is intact. The wrapper always runs the original handler and never upgrades a send alone to success. The inert Chat suite now exercises the real terminal wrapper and restoration and PASSes. A packaged real Automation-tab proof surface was also added; fresh live UI proof is next and will deliberately **not** replay the earlier unresolved Treasure target.
- **Treasure correlation follow-up / fail-closed correction:** deeper current-v26 source review found the response handler does **not** read or expose a source-proven request UUID/nonce, while multiple native UI paths independently send the same `DetectEventClaimTreasure` route. `ActDetectTreasureDataManager:OnGetDigTimesMsg` updates only global/activity dig counters and daily limits, not an exact Treasure UUID. Therefore a same-owner one-in-flight wrapper cannot uniquely attribute a native reply to this automation. The temporary wrapper was removed before final delivery. Treasure remains visible to the real Chat listener, but automatic Treasure claim now fails closed **before send**, reports `capabilities.claims=false`, and its real UI switch/Run-now controls become unavailable with an explicit warning. Red Packet and Fireworks / Egg retain their source-qualified claim engines. The Chat Lua regression now asserts no Treasure network send under a detected claimable Treasure.
- **First real Automation-tab proof attempt exposed and contained a UI restore race:** exact UID/server admission passed and the real Red Packet switch successfully persisted/native-applied its changed state, but the immediate restore click raced the component's save/busy re-render. No Red Packet claim was submitted (`submitted=0`). The task app/game was closed at once; all v26 scripts restored exactly and no recovery journal remained. The isolated runtime config was then restored byte-for-byte in substance to the prior task-owned baseline (no Chat task entry). The proof harness now waits for the real switch to leave save/busy state before restoring and snapshots the full initial Red Packet config; on any proof failure it writes that exact snapshot back through `ProfileRuntimeConfigStore` and reconfigures native Chat before returning partial. Fresh live Automation-tab proof remains pending.
- **Chat real Automation-tab proof complete (session `native-ui-20261019-182816`, source `8b40bef2`):** exact UID/server/session admission passed. The real packaged Automation -> Chat page rendered all three native cards while connected. The Red Packet switch changed the persisted/native setting `false -> true`, waited through the actual save/busy cycle, then restored `true -> false` exactly; `settingsRestoredExactly=true`. The native listener was ready with 4 installed event listeners, 25 rooms and **227 observed messages** (**6 live event callbacks + 221 retained**). Screenshots: `r23-chat-tab-connected.png`, `r23-chat-tab-listening.png`; receipt: `r23-chat-ui.json state=proven`. The UI harness deliberately did not issue another claim after the earlier unresolved Treasure send. In the earlier live listener session Red Packet and Fireworks / Egg had no claimable target; no successful claim is therefore claimed. Treasure's uncorrelated native reply route remains fail-closed before send as recorded above. The task-owned app/game exited 0; restoration audit proves all three installed v26 script hashes exact and no recovery journal.

### R23 Automation card engine status (list only)
| Category | Card | Real engine status |
| --- | --- | --- |
| Daily Tasks | Auto Training | No - preview/local UI only in production. |
| Daily Tasks | Automatic Construction | No - preview/local UI only in production. |
| Daily Tasks | Free Stamina | No - preview/local UI only in production. |
| Daily Tasks | Automatic Treatment | No - preview/local UI only in production. |
| Daily Tasks | Trucks | No - preview/local UI only in production. |
| Daily Tasks | Secret Task | No - preview/local UI only in production. |
| Daily Tasks | Ghost Ops | No - preview/local UI only in production. |
| Alliance | Alliance Tech Donations | No - preview/local UI only in production. |
| Alliance | Automatic Official Application | No - preview/local UI only in production. |
| Alliance | Automatic Alliance Train Boarding | No - preview/local UI only in production. |
| Alliance | Alliance Help | No - preview/local UI only in production. |
| Alliance | Alliance Gifts | No - preview/local UI only in production. |
| Alliance | Excavation Stronghold Resources | No - preview/local UI only in production. |
| Alliance | Alliance Center Resources | No - preview/local UI only in production. |
| Alliance | Alliance Gathering Dispatch | No - preview/local UI only in production. |
| Resource gathering | Resource gathering | No - preview/local UI only in production. |
| Resource Claims | Building Resource Collection | No - preview/local UI only in production. |
| Resource Claims | Armed Truck | No - preview/local UI only in production. |
| Chat | Red Packet | **Yes** - real owner-bound chat listener, persisted settings, Run now, native qualified claim route and native poststate/ACK observation; no spend route. |
| Chat | Fireworks / Egg | **Yes** - real owner-bound chat listener, persisted settings, Run now, native qualified claim route and native poststate/ACK observation; no spend route. |
| Chat | Treasure | **Partial / claim disabled** - real listener, settings and status; automatic claim is fail-closed because the current native reply has no source-proven request UUID/nonce for safe attribution. |
| Trade Station | Trade Station | No - preview/local UI only in production. |
| Protection & System | Weekend Shield | No - preview/local UI only in production. |
| Protection & System | Attack Shield | No - preview/local UI only in production. |

- **Final R23 regression gate complete:** attack actions, hotkey actions/game, City game/candidate/swap/grid-audit/live-driver, relocation (**81/81**), equipment actions (**32/32**) + integration, and Chat engine suites all PASS. Full UI `npm ... run check` PASSes through `FEATURE018_R21_CITY_NAMES_UI_OK`; R23 package-boundary checks PASS; full `tests/LWBridge.Desktop.Checks` PASSes through `HOME_FEATURE_DELIVERY_CHECKS_OK`. No game was launched by the regression suites. Final source-pinned Windows publish/package is next.
- **Final delivery source checkpoint:** a verification R23 Windows publish/ZIP from the preceding pushed checkpoint passed ProductVersion/source identity, canonical ProductionUi fingerprint, **21** linked bridge-source hashes, ZIP CRC/per-entry SHA checks and every-entry ASCII/UTF-16LE/UTF-16BE scans for every retained test UID; its generated README/FULL assessment accurately records Q/F10/Equipment/City proof, UNKNOWN historical reconnect cause, Red Packet/Fireworks live-listener boundary and fail-closed Treasure limitation. This audit-only checkpoint is pushed next, then the owner ZIP is regenerated once from that exact final HEAD; the external `r23-package-receipt.json` beside that final ZIP is the authoritative delivered artifact identity. No task-owned game/app remains running; installed scripts are exact v26 originals and no recovery journal exists.

## Lead audit of R23 (CoS) — 2026-10-20 (source 66f1dbe8): ACCEPTED for owner test
HEAD = remote 66f1dbe8, nothing uncommitted (Codex's Chat work reviewed and committed in 5f567f9e). No game/app
running; live game scripts exact v26; no journal. Attack, hotkey, City, relocation, equipment and Chat Lua checks,
Desktop.Checks and UI check PASS (lead re-run). Lead viewed `r23-city-100-percent.png`: building pictures and names
show (HQ, Tank Center, Hospital…).
- F10: hex-alliance fix works live (174251): one send, city 572199 → 966030, item 7 → 6. The game's reply was not
  caught, so the app reports "awaiting confirmation" rather than "Confirmed" — the move itself happened.
- Equipment tab: real-tab fresh preset → dropdown → save → apply → restore, exact start state (proven).
  Reconnect reload proven by mounted check only.
- City pictures: 109/109 decoded, full-view screenshots.
- Chat: Red Packet and Fireworks/Egg engines real (listener saw 227–254 messages); no claimable item appeared, so no
  live claim. Treasure: automatic claim DISABLED on purpose (the game's reply cannot be matched to our request);
  one earlier Treasure claim was sent and left unconfirmed, not retried.
- Automation status list written: every card except Chat has NO real engine (preview only).
- Disconnect: app restarts the game on purpose; first trigger unknown; logging added for next time.
ZIP `artifacts/feature018-r23-final-66f1dbe8/release/LW-Control-FEATURE-018-R23-OWNER-TEST-66f1dbe834ce.zip`
SHA-256 aef365c1…2d93fa1: re-hashed, CRC ok, 120 entries, SOURCE-COMMIT = 66f1dbe8, no test UID.

## Owner test of R23 and R24 assignment — 2026-10-20
Owner results (R23 ZIP, own account):
1. Q: "Previous native attack is still awaiting confirmation; fresh order not sent". Owner: the first Q was sent
   while the squad was inside BLACK TILES (the game does not allow changing a march's target there). After the squad
   left the black tiles, every new Q kept failing with this message. So a redirect the game silently refused leaves
   our "uncertain" fence up forever.
   Fix: (a) before sending a redirect, use the game's own check (can this march change target now — black tile /
   blocked state) and refuse with a clear message WITHOUT sending; (b) an uncertain redirect must settle within a few
   seconds by reading the march: target/startTime unchanged ⇒ game refused it ⇒ clear the fence and say so; the next
   press must work. Never replay automatically.
2. F10: relocation SUCCEEDED, but the app showed the error "AllianceRelocate:
   game_message_sent_without_independent_state_confirmation" (Feature018HotkeyNativeProviders.cs ~239). R23 live proof
   already showed the reliable evidence (city position changed + one relocation item used). Fix: confirm F10 (and F9)
   from that after-state and show success; never show raw internal codes to the owner — use a plain message.
3. Equipment Schemes:
   - error "Current equipped item disagrees with native inventory: wearer mismatch" when changing a dropdown
     (tools/feature018_equipment_actions.lua); the tab then shows Preset 1 with only 1 item. Find which data
     disagree (snapshot cache vs live inventory after a change; item moved between heroes) and fix — refresh the
     snapshot instead of failing, and only block when the game really disagrees after a fresh read.
   - drag and drop does not work: the original 0.3.17 tab lets you drag a single item or a full set between
     positions/squads (texts "Drag squad equipment", "Drag equipment set"). The native tab (NativeEquipmentContent.jsx)
     has no drag handlers. Recover drag-and-drop with the original rules (same slot only; hero stays fixed).
   - "Save current squads" should fill all 4 slots for all positions; the hint should disappear after saving.
4. REMOVE the Space shield countdown feature (owner decision, OWNER_DIRECTION): Hotkeys card, Space key capture,
   overlay and native visible-city shield reader, locale text, checks. Keep F6/F7/F8 shield items.
R24: fix 1–4, owner-test ZIP (R24 package). Background only; owner tests keys. Prove 1 via native commands (a
blocked-redirect case if a black-tile march can be arranged; otherwise offline + timing proof), 2 via a background
F10 on 4799432499002212 if an item and rally remain (else offline), 3 through the real tab (change → save → apply →
restore; drag one item; drag a full set). Owner reminder: close your own LW-Control app and game first.

### R24 progress (worker updates this)
- **R24 task 1 — Q redirect black-tile/refusal recovery complete (worker, 2026-10-20):** redirects now re-read the exact owned march at the final send edge, convert its current world position with the source-v26 `ForceChangeScene.World` argument, and call the game's own `SceneUtils.IsInBlackRange(pointIndex, serverId)` predicate. A black-tile march (or the existing native broken-state guard) is refused before `WorldMarchChange`; no transaction/send fence is created. For a one-send redirect whose outcome remains uncertain, the transaction now retains the pre-send target + target position + `startTime`. Read-only observation keeps the fence for the first 3 seconds; at >=3 seconds the exact same owner/formation/march with unchanged target/type/startTime is treated as a game refusal, the fence is cleared, a plain refusal is returned, and there is **no automatic replay**. The following explicit hotkey press is free to send once. Managed hotkey polling recognizes these authenticated refusal details and returns the plain game-facing message instead of an internal code. `python tools/feature018_attack_actions_checks.py` PASSes (`FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS`) with black-tile zero-send, <3s retained fence, >=3s unchanged-target/startTime retirement, and next-press one-send coverage. Full `tests/LWBridge.Desktop.Checks` PASSes through `HOME_FEATURE_DELIVERY_CHECKS_OK`, including managed query-only refusal handling; checks launched no game. A live black-tile march was not arranged because the current task has no deterministic native-only command that safely places an owned march inside black land; this checkpoint therefore uses the requested offline + timing proof rather than fabricating a live terrain state.
- **R24 task 2 — F9/F10 after-state confirmation and plain feedback complete (worker, 2026-10-20):** the relocation transaction keeps the existing exact owner/session/item identity and one-send fences, but reply-hook fields are now diagnostics instead of a success prerequisite. F9 confirms after a verified send when the owned city point changed and the exact relocation stack is one lower. F10 confirms after one source-classified `AllianceMoveCity` send when the selected native rally remains bound, the city point changed, and that exact alliance-relocation stack is one lower; the direct same-home F10 receipt is now admitted to the same read-only polling path as the chooser path. This exactly covers R23 session `native-ui-20261019-174251`, whose reliable evidence was city **572199 -> 966030**, item **7 -> 6**, one send, and no reason-240/reply-hook callback. The final receipt observations were renamed to explicit post-state labels and carry `itemDebitedVerified=true`; the managed provider no longer requires reply/ACK diagnostic fields. `HotkeyFeedback` keeps ordinary confirmed hotkeys silent but visibly reports `F9/F10: Relocation confirmed.`; pending/internal relocation tokens such as `game_message_sent_without_independent_state_confirmation` are translated to plain owner-facing guidance. `python tools/feature018_relocation_actions_checks.py` PASSes **82/82**, including F9 and F10 city+item proof with reply hooks absent; mounted feedback checks PASS and explicitly reject raw-token rendering; full Desktop checks PASS through `HOME_FEATURE_DELIVERY_CHECKS_OK`, including direct-F10 one-send + query-only confirmation. A fresh live F10 was attempted only through the background staged launcher, but it stopped **before any app/game launch or action** because a pre-existing external recovery journal exists at `%LOCALAPPDATA%/LWBridgeRebuild/overview-bridge/recovery.json` (profile `local-9370d887d93e4475a8d4fee049b50632`, session `e9428d22923b49a29e96406d6084d3b1`, stage `active_ready_deferred_restore`). No LWBridge/LastWar process is presently running, but the journal is not owned by this worker and was left untouched. Therefore this checkpoint uses the assignment's offline proof path rather than risking another session against foreign recovery state.
- **R24 task 3 — Equipment Schemes stale-state recovery, drag/drop and full capture complete (worker, 2026-10-20):** native equipment preflight now treats an initial wearer mismatch as potentially stale UI/snapshot state, re-reads the exact owner/server/hero/slot snapshot once, and blocks only if the fresh native inventory still disagrees. `Save current squads` explicitly walks all four native equipment slot types across every live position and the native tab hides its capture hint once that full position set is saved. The real tab now recovers the original drag rules: a single item can move/swap only with the same slot type, a whole hero loadout can move between positions, and the hero identity never moves; squad-level equipment dragging swaps loadouts only across matching live positions. Fresh packaged background proof `artifacts/map014-takeover/native-ui-20261020-055529` admitted exact UID/server and completed through the actual Equipment Schemes WebView with **15 positions / 60 selectors**: Refresh; Save current squads; single item drag `1:5:3 -> 1:1:3` and exact reverse; full-set drag `1:5 -> 1:1` and exact reverse with heroes fixed; dropdown edit + Save + native Apply; then Restore. Independent native snapshots prove Apply changed the loadout and Restore returned both equipment slots and every inventory wearer **exactly** to the starting state. Receipt `r23-equipment-ui.json` is `state=proven`; screenshots are `r24-equipment-tab-loaded.png`, `r24-equipment-current-squads-saved.png`, `r24-equipment-single-item-drag.png`, `r24-equipment-full-set-drag.png`, `r24-equipment-dropdown-saved.png`, `r24-equipment-applied.png`, and `r24-equipment-tab-restored.png`. Three preceding proof-driver-only failures were all before native Apply and each task-owned session exited with exact v26 script restoration/no recovery journal; the final proof also exited 0 with restoration `state=proven`. `feature018_equipment_actions_checks.py` PASSes **35/35**, integration and full UI checks PASS, `git diff --check` PASSes, and full Desktop checks PASS through `HOME_FEATURE_DELIVERY_CHECKS_OK`; no game/app remains running after proof.
- **R24 task 4 — Space shield countdown removed by OWNER_DIRECTION (worker, 2026-10-20):** added the owner decision to `OWNER_DIRECTION_SUB_ACCOUNT_ACTIONS_2026-10-17.md` and behavior-difference ledger D06. The Space card and `shieldOverlay` preference are removed from the canonical Hotkeys UI and all nine locale catalogs; legacy saved configs may still contain the old property but current projection/save drops it. `Keys.Space`, task-owned Space injection, `ShieldRemaining`, the read-only provider/action, desktop overlay window, `feature018_visible_city_shields`, and Lua visible-city screen projection are removed. The selectable legacy recovery WebUi bundle was patched in place as well, so it cannot resurrect the retired Space card/text. The F6/F7/F8 `shieldUse` card, bindings, native inventory/shop, item-use transaction fences and shared `ShieldStatusSnapshot`/`MAIN_SHIELD` after-state reader remain intact. `feature018_hotkey_game_checks.py`, `feature018_hotkey_actions_checks.py`, the dedicated mounted Hotkeys check (`cards=r24-six space=removed`), canonical UI check, Release Desktop build and full Desktop checks PASS through `HOME_FEATURE_DELIVERY_CHECKS_OK`; removal sweeps find no production `ShieldRemaining`, `shieldOverlay`, overlay class, visible-city endpoint, Space binding, or `hotkeys.shield.title/description` references outside deliberate legacy-migration/negative assertions. No game was launched for this removal.
- **R24 final delivery source gate complete (worker, 2026-10-20):** added `feature018_r24_package.py` plus inert release-boundary checks. The R24 generator pins the remote branch HEAD, Windows ProductVersion, canonical ProductionUi identity, every Desktop-linked bridge source hash, tracked `src/`+`tools/` digest, R24 source contracts for black-tile/timed redirect recovery, relocation city+item after-state, fresh equipment wearer reconciliation/drag rules, and complete Space-countdown retirement while retaining F6/F7/F8. It also scans every publish/ZIP payload for every retained proof UID as ASCII, UTF-16LE and UTF-16BE and refuses retired Space UI markers in the published JS/HTML. Final source checks all PASS: attack actions; hotkey actions; hotkey game; relocation **82/82**; equipment actions **35/35** + integration; R24 package boundary **3/3**; full canonical UI check; and full `tests/LWBridge.Desktop.Checks` through `HOME_FEATURE_DELIVERY_CHECKS_OK`. This audit/package-source checkpoint is pushed next; the owner ZIP will then be produced from that exact pushed HEAD with an external `r24-package-receipt.json` as the authoritative final artifact identity. No additional tracked edit is made after packaging so the ZIP remains source-pinned.
- **R24 release-boundary verification complete; final audit-only checkpoint (worker, 2026-10-20):** a fresh Windows publish/package from pushed source `319d5d10` passed ProductVersion/source identity, canonical ProductionUi source+artifact fingerprints, **21** Desktop-linked bridge hashes, ZIP CRC and per-entry SHA verification, and every-entry proof-UID scans in ASCII/UTF-16LE/UTF-16BE. The verification ZIP contained **120** entries and its external receipt recorded SHA-256 `521fb35066ac47990930520bb4a403c36d64443349d0351f8c7a1c6feeef4ddc`. This line is the only tracked change after that verification, so it is pushed as an audit-only checkpoint; the delivered R24 owner ZIP is regenerated once from the resulting final remote HEAD. The regenerated external `r24-package-receipt.json` is authoritative for the delivered commit/hash; no tracked edit follows it.

## Lead audit of R24 — 2026-10-20 (source 52ca82f4): ACCEPTED for owner test
HEAD = remote 52ca82f4, clean. Attack, both hotkey suites, relocation, equipment Lua checks, Desktop.Checks and UI
check PASS (lead re-run). No game/app running; live scripts exact v26; no journal.
- Q black-tile: game's own "can change target" check before send + refused/unchanged redirect clears after ≥3 s
  with a "press again" message. Offline + timing proof only (no black-tile march arranged) — owner to test.
- F9/F10: success confirmed from city-moved + item-used after-state; no raw codes. Live basis = R23 F10 move.
- Equipment: fresh-read recovery for wearer mismatch, drag one item and full set (same slot, hero fixed), Save
  current squads fills all slots; real-tab proof 055529 ended exactly at the start state. Lead viewed
  `r24-equipment-current-squads-saved.png`: drag buttons visible; the test account had only 2 equipped items at
  that moment, so the saved preset shows 2 items (expected for that account).
- Space shield countdown removed (no "Shield countdown" text in the ZIP's UI).
ZIP `artifacts/feature018-r24-final-52ca82f4/release/LW-Control-FEATURE-018-R24-OWNER-TEST-52ca82f49e5a.zip`
SHA-256 6481bf6f…31affe0: re-hashed, CRC ok, 120 entries, SOURCE-COMMIT = 52ca82f4, no test UID.

## Owner test of R24 Equipment Schemes — 2026-10-20 (owner confused; redesign needed)
Owner: dragging "still has issues", unsure what drag does, whether it syncs with the game, can't drag gear to another
hero, "not even sure what to test". Screens: Preset 1 "15 positions · 0 items" with every dropdown Empty while
"Wearing now" shows real gear; one save gave "Access to the path is denied."
Lead findings:
- The tab edits a PLAN (preset), not the game. Drag/dropdown only change the plan; the game changes only on
  "Save and apply to squads" or Alt+1–4. Nothing on screen says this, and a new preset starts EMPTY, so there is
  nothing to drag (you drag the plan's item, not the "Wearing now" item).
- "Access to the path is denied": HotkeyConfigCommandService.cs ~348-351 saves with File.WriteAllText + File.Move
  (overwrite) and no retry — a brief file lock (antivirus/indexer/another reader) fails the save. Needs bounded retry
  (or File.Replace) and a plain message.
Pending owner decision on the design (see chat): plan-then-apply (original) vs. live-sync.

## R25 assignment — Equipment Schemes redesign (owner decision 2026-10-20)
Owner: "Keep plan-then-apply like the original, but start each preset as a copy of my current gear, let me drag gear
between heroes, and make it clear when changes are not applied yet."
1. A preset with no saved content starts as a copy of the CURRENT game gear for every squad/position/slot
   (auto-filled when the tab loads; "Save current squads" stays to re-copy on demand). Never show a fresh preset as
   all "Empty" when the heroes wear gear.
2. Drag gear between heroes: drag one item onto the same slot of another hero (any squad/position) → the two heroes'
   items for that slot swap in the plan (empty target = move). Drag a full set onto another hero → swap all 4 slots.
   Same slot type only; heroes stay in their positions. Dropdowns still work. Visible drop highlight; invalid targets
   refused clearly.
3. Make plan vs game obvious:
   - a banner at the top: "Plan only — not in the game yet. Press Save and apply to squads (or Alt+N) to change the
     game." while the plan differs from what is worn; it disappears when they match;
   - each changed slot highlighted, with "Plan: X" vs "Wearing now: Y";
   - a count "N changes not applied"; "Save configuration" saves the plan without changing the game;
     "Save and apply to squads" changes the game, then re-reads and shows "Applied" when Wearing now = Plan.
4. Fix "Access to the path is denied" on save: HotkeyConfigCommandService.cs ~348-351 (File.WriteAllText +
   File.Move overwrite) — add bounded retry with short backoff (or File.Replace) for transient locks; plain message
   if it still fails; never lose the plan.
5. Prove through the REAL tab in the packaged app on 4799432499002212 (background, WebView harness): tab opens with
   preset auto-filled from current gear; drag one item to another hero (swap) and a full set; banner + change count
   appear; Save configuration (game unchanged); Save and apply (game changes, banner clears); Restore initial
   equipment (exact start state). Screenshots of each step. Also a check that simulates a locked config file.
6. Owner-test ZIP (R25 package), README with a short "How to use Equipment Schemes" (5 steps).

### R25 progress (worker updates this)
- **Task 1 complete (2026-10-20):** fresh and legacy content-free presets auto-fill all native squads/positions/four slots from the current authenticated snapshot on tab load. Auto-fill is local only; saved nonempty plans remain intact, and an initialization marker preserves deliberately emptied plans on later reads. Save current squads still explicitly re-copies. Mounted production Equipment check PASSes fresh worn-slot visibility without Capture, zero auto-fill save/mutation calls and initialized-empty preservation. No game session started.
- **Task 2 complete (2026-10-20):** native drag handles initialize browser DataTransfer; valid item/set/squad targets have an explicit high-contrast outline. Same-slot item drops swap across heroes/squads (empty target moves); full sets exchange all saved slots with hero identities fixed. Wrong-slot drops are accepted only to explain refusal, with no plan mutation. Mounted checks PASS visible drop highlight, invalid-drop message/no-op and item/set moves; pure checks PASS cross-squad item/set swaps and fixed heroes. Packaged live proof remains task 5.
- **Task 3 complete (2026-10-20):** selected-plan UUIDs are compared with all current native slots. Differences show a top plan-only/Alt+N banner, exact unapplied slot count, highlighted slots and Plan/Wearing now names. Save configuration reports plan-only persistence; Apply re-reads native gear and reports Applied only when all position identities and slot UUIDs match. All seven new messages have explicit translations in all nine app languages. Mounted checks PASS matching/changed/applied banner transitions, count/highlights, Save leaving game unchanged and fresh Apply confirmation. Genuine packaged proof remains task 5.
- **Task 4 complete (2026-10-20):** config reads and atomic temporary-file replacement retry IO/access failures six times with 50/100/150/200/250 ms backoff (750 ms total). Exhaustion returns CONFIG_FILE_BUSY with a plain localized Equipment message; the React plan remains intact and Apply cannot send after a failed save. Actual Windows file-handle checks PASS brief replacement lock, persistent lock preserving the old file/cleaning the owned temporary, and exclusive read-lock recovery. Mounted check PASSes save failure retaining the edited UUID and unapplied banner followed by successful retry. Full Desktop suite running; FEATURE018_HOTKEY_CONFIG_PASS already observed.
- **Task 5 preparation checkpoint (2026-10-20):** full UI check, full Desktop.Checks through HOME_FEATURE_DELIVERY_CHECKS_OK, equipment actions 35/35 and native-preflight integration PASS. Added an opt-in R25 real-tab proof path requiring all four presets auto-filled, equipped-to-equipped item swap, cross-squad full-set swap, visible highlights/banner/count, plan-only Save preserving every native slot/wearer, native Apply matching every planned UUID, and exact native Restore. Captures use CoreWebView2.CapturePreviewAsync only. No live R25 outcome is claimed yet.
- **Task 5 first native trial / correction (2026-10-20):** session native-ui-20261020-073745 passed all-four auto-fill, exact equipped-item swap, cross-squad full-set swap, fixed heroes, seven changed-slot highlights/count and Save leaving every native slot/wearer unchanged. Apply hit GAME_CONNECTION_UNAVAILABLE (temporary authenticated heartbeat gap) after native progress; proof remains PARTIAL. No Apply resend occurred. Two later query-only observations completed the same 8/8 transaction; one Restore plus query-only confirmation returned every original slot and inventory wearer exactly (r25-recovery-exact-restoration.json). Owned exit 0, exact v26 scripts restored, no recovery journal. Runtime now continues only query-only confirmation through bounded transient connection gaps under the same profile-generation fence; mounted regression proves one Apply send. Proof uses a language-independent Applied marker and records actual app language (the first live tab was zh-CN). Fresh full proof follows.
- **Task 5 complete (2026-10-20), live source 18ce1021:** packaged background session native-ui-20261020-074330 verified exact approved UID/server and PASSed the real Equipment Schemes tab: all four presets auto-filled 15 positions / 60 selectors / 15 worn items; exact equipped-to-equipped same-slot swap; full-set swap across squads with all four slots exchanged and heroes fixed; seven changed slots/count/banner; Save configuration preserving every native slot and inventory wearer; Save and apply matching every planned UUID after fresh native read and clearing the banner; Restore matching every starting slot and every inventory wearer exactly. r23-equipment-ui.json has state=proven and actual appLanguage=zh-CN. Nine r25-equipment PNGs include each step and both drop highlights. Owned exit 0; all three v26 scripts restored exactly, no recovery journal. Screenshot review found Applied was below the long list, so notices now render beside the top controls; mounted check passes. Final package proof will capture that presentation improvement.
- **Task 6 package-source checkpoint (2026-10-20):** added R25 source-pinned package generator and release-boundary checks, retaining exact remote HEAD/ProductVersion, canonical UI/source fingerprints, all linked bridge-source hashes, per-entry SHA/CRC and all-entry ASCII/UTF-16LE/UTF-16BE scans for every current/historical proof UID. Added R25 auto-fill/drag/plan/count/highlight/config-lock-retry source contracts and nine-language contracts. README contains the requested five-step Equipment Schemes guide and explicitly distinguishes Save plan from Save and apply. Inert package boundary 3/3 and source/account/retirement preflights PASS. Final Desktop suite and top-notice package verification are in progress; final ZIP/receipt will be pinned to the last pushed checkpoint.
- **Task 6 verified / final delivery checkpoint (2026-10-20):** fresh R25 verification publish/ZIP from pushed 0f198b7c PASSed exact Windows ProductVersion/source commit, ProductionUi source/artifact fingerprints, all 21 linked bridge hashes, CRC/per-entry SHA, 120-entry ASCII/UTF-16LE/UTF-16BE scans for every retained proof UID, and the five-step README. Verification ZIP SHA-256 d6d980f2718df32d413c2a666e2b95f4b5a7cd81dd1196311897693804a30a03. The app extracted from that ZIP then PASSed the full real-tab proof in native-ui-20261020-074818: all four auto-filled presets, equipped-item swap, cross-squad four-slot set swap, fixed heroes, seven count/highlights/banner, Save with unchanged native slots/wearers, Apply matching every planned UUID and clearing the banner, visible top Applied notice (zh-CN), and exact initial slots plus all inventory wearers restored. Nine step/highlight PNGs and r23-equipment-ui.json state=proven are retained there. Owned exit 0, restorationAudit=proven, all three v26 installed scripts exact, no recovery journal or game/app process remains. Final full UI check and full Desktop.Checks through HOME_FEATURE_DELIVERY_CHECKS_OK PASS; equipment Lua 35/35 + integration and package boundary 3/3 PASS. This is the final audit-only checkpoint: push it, rebuild/package once from that exact HEAD, with no later tracked edit. The external r25-package-receipt.json beside artifacts/feature018-r25-final-<HEAD>/release/ is the authoritative final ZIP identity; src/tools source hashes and UI fingerprints must match this verified product. All R25 worker tasks are complete; READY_FOR_LEAD_REVIEW, independent acceptance remains separate. The initial partial trial and its exact recovery remain preserved. Pre-existing owner .claude/ metadata is untouched and excluded.


## R26 owner decision — live Equipment (2026-10-20)
The owner rejected plan mode after testing R25. Every equip, remove and item drag must immediately change the game; no Save current squads, plan, Save configuration, Apply or Restore controls remain in the production Equipment tab. Dragging moves the item: the source is unequipped, the destination equips it, and any displaced destination item returns to inventory. The owner reports all gear is currently unequipped and authorizes testing. This supersedes the R25 plan-mode decision. Exact approved test UID/server, background-only/no input/no focus, no owner-session control, no Land Cell, no real money, original script restoration and draft-PR-only boundaries remain.

### R26 progress
- **Implementation checkpoint:** production Equipment now displays native worn slots and unequipped inventory, submits immediate one-slot native edits, and polls current game state every 1.5 seconds only while its tab is active. No preset config read/write or plan/save/apply/restore action is used by this tab. Native requests validate the fresh hero, item slot, current target item and source wearer, then reuse the source-proven install/uninstall sequencer with owner/server/focus and uncertain-send fences; query-only confirmation retains the original operation identity. Moving to an occupied slot returns displaced gear to inventory rather than swapping. New operation and completed-ID fences reject overlap/replays. UI blocks overlapping edits, shows native confirmation state, rejects wrong-slot drag and discards a poll started before an edit. Seven live-editor messages are translated in nine languages. Mounted production/real-bridge checks PASS direct equip/remove/move, returned inventory, no plan controls/config calls, periodic game-side refresh and pending fences. Native equipment suite PASSes 39/39 (including four new live tests), Hotkey actions and equipment integration PASS. Full Desktop checks and packaged empty-start proof follow.
- **R26 verification/package preparation:** full canonical UI checks PASS including the mounted live editor; full Desktop.Checks PASS through HOME_FEATURE_DELIVERY_CHECKS_OK, including strict single-edit/query schemas and forged formation/authority rejection. Equipment Lua 39/39, equipment integration and Hotkey actions PASS. Sync read recovery now clears transient read errors after the next successful read, while keeping action errors visible. R26 package generator and 3/3 boundary checks retain source/version/UI/bridge/hash/CRC/UID scans and replace the old plan guide with a five-step immediate equip/remove/move guide. Source/account/retirement preflights PASS. The opt-in R26 real-tab proof expects the fully unequipped initial account, tests immediate armor equip/remove, drag between heroes, automatic reflection of a native game-side edit and exact initial inventory-wearer restoration. No R26 live outcome is claimed yet.
- **R26 genuine live proof complete (2026-10-20), source 2f08e775:** exact approved UID/server was admitted in owned background session native-ui-20261020-085800. First proof attempt met a transient OS file-access error before any Equipment action or screenshot; its receipt is preserved, and the same session was retried only after confirming no proof/action had started. The real packaged tab then PASSed from the owner-reported fully unequipped state: armor selection equipped immediately in the game; Empty immediately removed it; drag moved that armor from hero 1 to hero 2 (source empty, destination equipped); removing it returned all gear to inventory. A separate supported native edit appeared automatically in the tab without Refresh. Every checkpoint compared rendered UUIDs with an independent native snapshot. Final removal through the live tab restored EVERY starting slot and inventory wearer exactly. Eight r26-equipment PNGs and r23-equipment-ui.json state=proven are retained. Owned exit 0, restorationAudit=proven, all three v26 scripts exact, no journal/game/app left. Source UI review confirms no preset rail or Save/Capture/Apply/Restore controls. A further mounted recovery check proves an unpublished operation is only queried and its fence releases on explicit native no-transaction rejection, without replay. Final full UI, full Desktop.Checks, equipment Lua 39/39, integration, Hotkey Lua and package boundary 3/3 PASS. This final source checkpoint is pushed next; build the R26 owner ZIP from that exact HEAD with the adjacent external r26-package-receipt.json as authoritative identity. Independent lead acceptance remains separate; old partial/negative receipts remain preserved. Pre-existing owner .claude/ is untouched.


## R27 owner correction — keep presets, restore levels, one Refresh (2026-10-20)
The owner clarified that removing plan mode did NOT authorize removing presets: retain saved presets 1–4 with Alt+1–4 while ordinary equip/remove/drag remains immediate and synchronized with the game. R26 also omitted native gear levels and accidentally rendered two Refresh controls. This correction supersedes the R26 removal of preset controls, while retaining its live editing semantics and all existing account/background/restoration boundaries.

### R27 progress
- **Implementation checkpoint:** restored four saved preset selectors with fixed Alt+1–4 ordinal labels, preset names, Save current gear to preset and Equip preset. Saving reads current native gear and changes only the selected persisted ordinal; other presets and existing config are preserved, and no game mutation is sent. Equipping uses the same saved ordinal and native command as the existing Alt+1–4 provider, with read-only preflight and the live editor shared pending-confirmation fence. Normal live edits never overwrite presets. Native gear levels are shown in worn slots, dropdown choices and unequipped inventory (including level zero; unavailable values are marked). Removed the inner Refresh; the page-level control remains. Ten new labels/help/status strings are translated in nine languages. Mounted production/real-bridge checks PASS retained existing presets, all four save/equip ordinals, unchanged game state on Save, preserving other presets, direct equip/remove/drag/polling, level labels and no inner Refresh. R27 package source preflight and release-boundary 3/3 PASS. Full UI check passes; full Desktop.Checks and real packaged proof follow. No R27 live result is claimed yet.
- **R27 first native trial / retained negative:** owned session native-ui-20261020-092128 admitted the exact approved UID/server and verified actual native gear levels, four preset controls and exactly one Refresh. Immediate equip plus saving all four presets PASSed with every native slot/wearer unchanged by each Save. The first Equip preset did not settle; no positive apply claim is made. A read-only follow-up preflight succeeded. Independent native diagnostic snapshot proves EVERY slot and inventory wearer already matched the original start (the proof had removed its temporary gear before Equip), then owned exit 0 restored all three v26 scripts exactly with no journal. Proof now retains actual tab error text on a failure; read-only preflight retries bounded transient connection gaps only, without sending or replaying an apply. Fresh trial follows.
- **R27 causal correction:** source inspection distinguishes the failed real WebView preflight from the successful direct diagnostic: createBackendBridge appends profileId, but ReadFields for equipment preflight still required exactly one slot field. It now accepts only slot plus validated profile routing, rejects duplicates/forged authority, and forwards only the ordinal to native Lua. This is the same real-routing class previously fixed for equipment snapshot in R22. Added managed routed-payload and forged/empty-routing checks. No account-action gate is relaxed; the first failed Equip had no native mutation. Rebuilding and rerunning the full packaged proof.
- **R27 routed regression:** mounted checks now click Equip preset through the production createBackendBridge (not only the direct fake adapter) and assert the exact slot + profileId preflight wire payload before the matching hotkey-ordinal request. A refused read-only preflight also leaves presets editable and emits zero Apply requests. Managed routed/forged-field checks are retained; the build name-scope collision in the new validation local is corrected.
- **R27 final native acceptance candidate (2026-10-20), live source c02a70dd:** genuine packaged session native-ui-20261020-092913 admitted exact approved UID/server and PASSed all requested corrections through the real Equipment tab: four preset selectors with Alt+1–4 labels, actual native level in inventory/equipped slots (zero retained, upgraded levels visible), dropdown level labels and exactly one page Refresh. Every preset 1–4 captured current native gear with every slot/inventory wearer unchanged by Save; each Equip preset then restored the exact saved native loadout via the same ordinal command used by Alt+1–4. Hardware keys were not injected in this background-only session; physical capture remains the previously accepted provider path. Final direct edit restored every original slot and every inventory wearer exactly. Twelve r27-equipment PNGs and r23-equipment-ui.json state=proven are retained. Owned exit 0; restorationAudit=proven, all three v26 scripts restored, no journal/game/app left. Full canonical UI and final Desktop.Checks through HOME_FEATURE_DELIVERY_CHECKS_OK PASS, including routed preflight/forged authority regressions; equipment Lua 39/39 + integration and R27 package boundary 3/3 PASS. Saving a preset now uses the generic processing status rather than claiming a game change; mounted checks re-pass. Earlier failed preflight and exact original-state receipts remain retained. R27 README restores the five-step gear/preset/Alt guide and package contracts include the corrected real-route handler. Push this final source checkpoint and generate the owner ZIP from its exact HEAD; adjacent r27-package-receipt.json is authoritative for the final source/version/UI/bridge/hash/CRC/ASCII-and-UTF16 UID checks. READY_FOR_LEAD_REVIEW, not independent lead acceptance. Existing owner .claude/ metadata remains untouched.


## R28 owner defect — file access error leaves live equipment disabled (2026-10-20)
The owner reports `Access to the path is denied.` followed by a permanent `Changing equipment in the game...` status and disabled drag/equip controls. Preserve R27 gear levels, four presets/Alt+1–4, immediate live edits and one Refresh. No plan mode is reintroduced.

### R28 progress
- **Implementation / offline checkpoint:** the native request mailbox previously used an unretried atomic File.Move and its response reader did not handle access-denied reads. Publication now retries the SAME owned envelope/request ID at most six attempts with 50/100/150/200/250ms backoff, rechecking captured account/session before each attempt and the final move. A persistent write/replacement failure returns GAME_COMMAND_NOT_PUBLISHED and requestDispatched=false; old mailbox contents remain intact. After publication, IO/access-denied response reads poll within the existing deadline without resending. Owned temporary cleanup cannot mask dispatch state. Live runtime releases its fence for a definite no-publication error; ambiguous generic host errors and response timeouts query only the original operation. A confirmed transaction releases its fence before the final snapshot, so a later read failure can recover through ordinary polling. An unresolved idle transaction has a distinct waiting message and Resume confirmation button directly below it, rather than falsely claiming an active change forever. Both new messages are translated in all nine languages. Actual Windows FileStream request/response locks plus a real access-denied directory replacement PASS brief recovery with one request ID, persistent six-attempt no-send with old file preserved, cleanup and owner retirement. Mounted tab checks PASS no-send releasing selects/draggable inventory, visible idle recovery, authenticated no-transaction release, query-only ambiguous error recovery and confirmed-then-failed-read fence release. Full UI checks, full Desktop.Checks through HOME_FEATURE_DELIVERY_CHECKS_OK, equipment Lua 39/39, integration and Hotkey Lua PASS. R28 package boundary 3/3 PASS. Added an opt-in packaged proof for a task-owned mailbox lock, real DOM drag between heroes, levels/presets/one Refresh and exact native restoration; its Release compile PASSes. No new live outcome is claimed at this checkpoint. Existing .claude/ remains untouched.

- **First packaged trial / causal follow-up (source 2b4d1e22):** exact-owned background session native-ui-20261020-094916 proved a held private request-file lock recovered through the actual tab, then a real item drag moved gear from hero 1 to hero 2 and back with visible drop highlights. Gear levels/one Refresh, all four plan-free preset saves and first three exact preset recalls passed. The fourth recall stopped on a distinct native wearer mismatch after its uninstall: HeroInfo and EquipDataManager receive separate pushes, and two immediate Lua reads in one frame cannot wait for the second push. This receipt remains PARTIAL; no full-pass claim. Query-only continuation of the SAME preset completed its original two steps without replay, followed by one Restore plus confirmation. Independent r28-recovery-exact-restoration.json proves EVERY original slot and inventory wearer restored. Owned exit 0, all three original v26 scripts exact, no journal/process left. Native Preflight now classifies this specific slot/wearer disagreement as EQUIPMENT_STATE_PENDING; an existing exact owner/preset transaction returns an accepted waiting receipt with nextStepNotDispatched=true until a later frame agrees. Structural identity/slot/type errors remain refusals. A failed initial live snapshot is explicitly rejected before dispatch so it cannot create a spurious UI fence. The read-only UI preflight retries the transient classification. New offline Lua regression holds the disagreement across three polls, proves no replay/next send, then confirms exactly the original four steps after the remaining pushes; equipment suite now PASSes 40/40. Full UI checks re-PASS including transient read-only preflight and all original R27 controls. Fresh packaged proof follows; older negative and exact cleanup receipts are retained.

- **Second packaged trial / observer-only correction (source 9f507ba4):** native-ui-20261020-095607 again passed the real held-mailbox equip and forward/reverse drag. After two preset saves, the proof's separate read-only native Snapshot hit GAME_ACCOUNT_NOT_AUTHORIZED during an interrupted heartbeat read; no full-pass claim. One exact original-baseline Restore and query-only confirmation returned EVERY initial slot/wearer (r28-recovery-exact-restoration.json). Owned exit 0 restored all original scripts with no journal. The proof observer now waits at most 20 x 100ms for fresh authenticated authority around read-only snapshots; it never retries mutations and refuses a changed/non-matching UID, server, PID or session. Product gameplay admission is unchanged. This addresses the actual observer race rather than rerunning an unchanged driver; both negative receipts remain retained.

- **Final native acceptance candidate (source 8cb501b3):** genuine background session native-ui-20261020-095955 admitted exact approved UID/server and PASSed the real production Equipment tab. Holding only its private mailbox file during an equip recovered automatically; the game wore the selected gear. Real WebView drag/drop with visible highlights moved that item to another hero and back, with independent native snapshots matching every rendered slot. Native levels and exactly one Refresh remain. All four presets saved current gear without changing any native slot/wearer; every saved ordinal 1–4 equipped the exact saved loadout through the same command used by Alt+1–4. The final direct edit restored EVERY initial slot and EVERY inventory wearer exactly (exactRestore=true). r23-equipment-ui.json is state=proven; 16 r28-equipment PNGs include held-lock equip, drag highlights/results, each save/recall and exact restoration. No physical input was used. Owned exit 0; restorationAudit=proven, all three original v26 scripts exact, no journal/game/app left. The first two partial/negative receipts and their independent exact-recovery evidence remain retained.
- **Final delivery gate:** full canonical UI checks PASS; final full Desktop.Checks PASS through HOME_FEATURE_DELIVERY_CHECKS_OK including FEATURE018_MAILBOX_LOCK_RECOVERY_PASS; equipment Lua 40/40, integration, Hotkey Lua and R28 package boundary 3/3 PASS. Account-agnostic product/retirement/retained-feature contracts and git diff --check PASS. R28 README retains the five-step live gear/preset/Alt guide and explains bounded file-lock recovery plus visible Resume confirmation. Push this final audit-only source checkpoint, then build the owner ZIP from its exact pushed HEAD. The adjacent external r28-package-receipt.json is authoritative for the final commit, ProductVersion/UI/21 bridge-source identities, SHA/CRC and every-entry ASCII/UTF-16LE/UTF-16BE proof-UID scans. No tracked edit follows packaging. READY_FOR_LEAD_REVIEW; independent lead acceptance remains separate. Pre-existing .claude/ metadata is untouched; all worker changes are committed.

## Owner test after R28 — 2026-10-20
Owner: hotkeys work, including Alt+1–4 equipment switching; Squads → Equipment Schemes works. Request: preset
switching is slow — gear is removed and equipped one item at a time. Next: investigate a faster apply (the game's own
one-click/batch equip route if it exists, or sending all slot changes back-to-back and confirming once at the end;
never replay uncertain sends). Note: R26–R28 were owner-directed directly to the worker (live equipment, keep presets,
file-lock fix); lead has not independently audited R26–R28 yet.

## Owner decisions — 2026-10-20 (status review)
- DROP (OWNER_DIRECTION): running two games at once (F-07) — removed from the backlog; one game at a time.
- DROP: scanning another server without switching servers — not possible; switch-scan-return stays as is.
- Owner CONFIRMED working: City Layout building moves; Q/W/E/R black-tile behaviour (R24).
- Treasure auto-claim: ON HOLD until Season 4 (owner is in Season 2).
- Settings FPS / ping: keep (nice to have); status to be checked.
- Account side bar: owner asks if it can be hidden completely; it is the profile switcher (separate saved settings
  per account, one game running at a time). Next round: allow fully hiding the side bar (and re-opening it).
- Wipe map scans at app start: built and in the test builds since R18; reaches the main release on merge.

## R29 assignment — speed, hideable account bar, then full bug sweep of every tab except Automation (2026-10-20)
Goal (owner): finish everything outside Automation so it can be closed and merged; then focus on Automation.
Part A — fixes:
1. Faster equipment preset switching (Alt+1–4 and "Equip preset"): today gear is removed and equipped one item at a
   time, each waiting for confirmation. Find the game's own one-click / whole-set equip route (e.g. what the game's
   own equipment-plan / quick-equip UI sends) and use it; if none exists, send the needed per-slot changes
   back-to-back without waiting between them and confirm once at the end from a fresh read. Never replay an uncertain
   send. Measure before/after time for a full 4-squad preset switch and report both numbers.
2. Account side bar: allow fully hiding it (no strip left), with a small, always-visible button to bring it back;
   remember the choice. It stays the profile switcher (one game at a time).
3. Settings → Show FPS / Show ping: check they really work in the game; fix if not.
Part B — bug sweep of EVERY tab except Automation: Home, Map Data, Squads / AFK (AFK Tasks + Equipment Schemes),
City Layout, Hotkeys, Mini Games, Settings, plus the top bar and account side bar.
For each tab, in the packaged app on 4799432499002212 (background, WebView harness, no desktop input):
   - open it connected AND disconnected, English AND Chinese, at 900 and 1600 px;
   - press every button / switch / dropdown that is safe to press (see exclusions), check the result in the game
     where it changes something, and check error messages are plain and disappear;
   - check nothing is stuck, nothing shows raw internal codes, nothing shows sample/preview data in production;
   - fix every bug found, with a regression check.
   Exclusions (do not press live): Unlock Land Cell; F6/F7/F8 shield use; Treasure claim (on hold until Season 4);
   Food House / Black Market (events off); Alliance Drill rally (event off) — check their screens only.
   Anything that moves gear/buildings/marches must end with the account exactly as it started.
Deliver: a "R29 tab sweep" table in the audit doc (tab → what was checked → bugs found → fixed? → evidence),
screenshots per tab, all checks (UI, Desktop.Checks, all feature Lua suites) PASS, owner-test ZIP (R29 package).
Removed/dropped items (do not restore): Space countdown, march speed-ups, Auto Garrison, Zombie Bus Garrison,
worker test panels, two-games-at-once, remote no-switch scanning.

### R29 progress (worker updates this)
- **Part A complete / checkpoint candidate (2026-10-20):** archived admitted v26 source has no hero-equipment
  one-click plan message: the similarly named `UITCQuickEquipPanelV2` is Tactical Card equipment. The native
  `HeroEquipInstall` serializer does accept a per-hero slot map and `HeroEquipUninstall` accepts a slot array, but
  their replies still have no slot/item/request nonce. The retained owner-preset route therefore keeps the existing
  exact UID/server/focus/preset/hero/item gates, freezes the whole change set, sends every required original
  uninstall/install step back-to-back without waiting for individual pushes, and confirms only once from a fresh
  complete native preflight. Any throw/false return or owner/server change tombstones the uncertain send and stops the
  remainder; query-only continuation never transmits. Offline equipment coverage is now 42/42 including burst-before-
  push, partial-push observation, third-send failure, final-state mismatch and no-replay negatives.
- **Part A real preset timing:** the unchanged `7747ac82` sequential baseline in exact-owned packaged session
  `artifacts/map014-takeover/native-ui-20261020-115927/` restored a complete current-account preset in **8964.1016 ms**.
  The R29 burst sender in `native-ui-20261020-120439/` restored the same **20 changed slots** in **806.5011 ms**:
  **8157.6005 ms / 91.003% less elapsed time, 11.115x faster**. Both runs independently matched every starting native
  slot and inventory wearer and restored the original persisted equipment config; both task-owned sessions exited 0,
  restored all three original v26 script files exactly and left no recovery journal. The approved account currently
  exposes **3 populated squads / 15 positions**, so a literal four-populated-squad live timing is unavailable on this
  account; both receipts explicitly record `requestedSquadCount=4`, `fullFourSquadSwitch=false` and
  `timingScope=all-populated-native-squads` rather than inventing a fourth squad.
- **Part A account sidebar:** the profile switcher can now disappear completely (no residual strip/aside/grid column),
  with a small restore control outside the hidden subtree; `lwbridge.profileSidebarHidden` remembers the choice.
  Genuine current-tree WebView proof `native-ui-20261020-121028/r29-part-a-ui.json` captured visible, fully hidden after
  a real WebView document reload, and restored states. Hidden proof has no `.profile-sidebar`, no compact profile item,
  an external restore button and the single-profile content grid; the same exact game owner/session stayed alive.
- **Part A Settings FPS/ping:** production Settings previously never loaded/applied these switches to the game. It now
  loads the persisted profile values, applies online changes through exact-owner `feature018_visual_metrics_configure`,
  persists only after native success, rolls the live game back if persistence fails, and reapplies saved values on
  reconnect. The game overlay is session-bound and samples FPS from `UnityEngine.Time.smoothDeltaTime` (fallback
  `unscaledDeltaTime`) and ping only from the verified `GameKit.Base.NetRawProxy` `GetPing`/`_ping` millisecond path.
  `native-ui-20261020-121028` confirmed render path
  `GameFramework/UI/UIContainer/LWBridgeOverviewReady/VisualMetrics`, **108.038 FPS** and **222 ms** ping, captured
  Settings **EN at 900 px** and **zh-CN at 1600 px** with both visual switches on, then restored persisted and live game
  settings exactly to the original off/off values. It also exposed and fixed a Lua precedence bug that had returned
  boolean `true` instead of the render-path string. Two earlier proof-harness negatives are retained: `120439` caught a
  canceled superseded WebView navigation event while all settings still restored; `120745` caught the render-path
  contract bug with real numeric FPS/ping before the fix. All three owned sessions exited cleanly with exact script
  restoration/no journal.
- **Part A validation:** canonical `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check` PASS (including R29 sidebar and
  visual-metrics mounted checks); full Release `tests/LWBridge.Desktop.Checks` PASS through
  `HOME_FEATURE_DELIVERY_CHECKS_OK`; all current FEATURE018 offline/Lua suites PASS, including equipment 42/42,
  relocation 82/82, Mini 55/55 and the native bridge/AFK/Chat/City/Sheep/Shield/Stamina suites. The owner-transition
  fixture was updated to include the already-packaged Chat engine dependency so that it again exercises the current
  periodic owner gate instead of crashing on a missing test global. `git diff --check` PASS. Part B/C remain open.

### R29 addendum — Part C: performance and smoothness (owner, 2026-10-20)
Owner: the program must not slow the game down, and the app must feel smooth, not clunky.
Measure on the packaged app + game (4799432499002212, background), idle and while busy (Map scan, Monster AFK,
Chat listener, Equipment page polling), and compare with the game running WITHOUT the app:
- game FPS and frame-time spikes, game CPU/RAM; the app's CPU/RAM/disk writes; how often the app polls the game
  (heartbeat, equipment 1.5 s poll, chat, status pollers) and the size/frequency of mailbox file writes;
- in-game stutter caused by our Lua (per-frame pumps/hooks): time spent per frame in our handlers;
- app responsiveness: time to open each tab, first data shown, button-press-to-feedback, scroll smoothness on long
  lists (Map table, City grid at 109 buildings, Equipment 15 positions), memory growth over 30 min.
Fix what is wasteful: stop polling on tabs that are not open, slow down idle pollers, batch/skip redundant reads and
writes, avoid per-frame work when nothing is running, lazy-load heavy pages/images, debounce saves, remove leaks.
Target: game FPS with the app idle within ~5% of no-app; no visible hitch when pressing hotkeys; tabs open in under
~1 s; no steady memory growth. Report a before/after table with real numbers.

## Lead tidy-up of R29 (worker stopped halfway) — 2026-10-20
Pushed = remote 165a267d ("complete feature018 r29 part a"). No game/app running; live game scripts exact v26; no
active journal. (Node processes running are the owner's CoS/Codex/REA tool servers, not leftovers.)
- Part A: DONE and pushed. Preset switch 8964 ms → 807 ms (11× faster, burst send + one final check; the game has
  no one-click hero-equipment route). Account bar can be fully hidden with a restore button (remembered). Settings
  FPS/ping now really works in the game (108 FPS / 222 ms read live), restored to off afterwards.
- Part B (tab bug sweep): IN PROGRESS, UNCOMMITTED. 36 files changed (App, Home, Map, City, Hotkeys, HotkeyFeedback,
  Equipment, ProfileSidebar, Settings, Squads, AFK/stamina engines, locales, mounted checks) plus new
  LWBridgeWindow.Feature018R29SweepProof.cs and checks check-r29-home-shell / -hotkeys-mini-responsive /
  -map-city-errors. Lead re-ran with this work in place: all feature Lua suites, Desktop.Checks, full UI check and
  the three new R29 checks PASS. No "R29 tab sweep" table, no live sweep evidence yet.
- Part C (performance): STARTED only — tools/r29_part_c_sampler.py (+ selftest PASS) and
  artifacts/feature018-r29-perf-research; no before/after measurements or fixes yet.
- ZIP: not built.
Next worker: review + commit the Part B work (do not discard), finish the live sweep and its table, do Part C with
real numbers, then the R29 ZIP.

### R29 progress - resumed worker checkpoint (2026-10-20)
- Reviewed and retained all inherited Part B source, locale, mounted-check and Lua recovery fixes. Added the three new R29 checks to the canonical UI command. The unfinished packaged sweep harness and passive Part C sampler are preserved; live sweep and performance acceptance remain OPEN. The owner-created `.claude/launch.json` is unrelated and remains untouched/untracked.
- Current verification: full canonical UI check and the three R29 checks PASS; Release Desktop.Checks PASS through HOME_FEATURE_DELIVERY_CHECKS_OK; all FEATURE018 Lua/offline suites PASS; original-package roundtrip PASS with the installed admitted v26 originals unchanged; passive sampler selftest PASS. Logs: `artifacts/r29-resume-{ui,desktop,lua}-check.log` and `artifacts/r29-resume-original-package-check.log`. The initial concurrent UI check raced the desktop build's npm-ci removal of node_modules; rerunning after that build passed. No live game started at this checkpoint.
- Next: correct sweep-harness asynchronous click/state observations, run the genuine packaged connected/disconnected matrix, then collect and fix measured Part C costs. No completion, merge or ZIP claim yet.

### R29 tab sweep - first packaged matrix (2026-10-20; control closeout remains open)

Genuine isolated session `artifacts/map014-takeover/native-ui-20261020-131404` authenticated the exact approved UID/server, ran the disconnected matrix before its own Home launch, then the connected matrix. All eight surfaces have EN/zh-CN captures at 900/1600 px in BOTH states (64 PNGs), copied with receipts to `artifacts/feature018-r29-sweep/first-matrix-20261020/`. No preview-fixture nodes or raw-code alerts were observed. All recorded route/data-ready timings were below one second. Home Close stopped the exact task-owned game; host exited 0, all admitted v26 originals were restored exactly, no journal remained. This is matrix evidence and the explicit actions below, **not yet every-control acceptance**.

| Tab/surface | What was checked | Bugs found | Fixed? | Evidence |
|---|---|---|---|---|
| Home | Eight state/language/width combinations; genuine Launch and confirmed Close | Immediate destructive Close and missing localized confirmation in inherited code | Yes; mounted regression + native Close | first-matrix receipt/PNG prefix `r29-sweep-*-home-*` |
| Map Data | Eight combinations; result/mode controls observed; manual native Start/Stop | Raw native errors; narrow result labels wrap awkwardly; initial rows empty | Error presentation fixed; further populated control/scroll checks open | `r29-sweep-*-map-data-*` |
| Squads / AFK | Eight combinations; config hydration/status; settings cards | Offline mutations/raw errors/transient failure recovery fixed; screenshot captured Add before catalogue returned | Inherited fixes pass; catalogue/data-ready capture needs improvement (not evidence of missing catalogue) | `r29-sweep-*-squads-afk-*` |
| Equipment Schemes | Eight combinations; native 15 positions; Refresh/preset selection | Preset read failure/retry and uncertain transaction lost on remount | Yes; pending fence retained; gear mutation exact-restore evidence remains separate R24/R29 Part A | `r29-sweep-*-squads-equipment-*` |
| City Layout | Eight combinations; 111 current native buildings; Refresh/zoom/fit controls | Initial large-grid viewport opens on blank land; renders thousands of offscreen cells | New centering/viewport rendering candidate implemented, retest open; inherited errors fixed | `r29-sweep-*-city-layout-*` |
| Hotkeys | Eight combinations; configuration switches paired with completed saves | Raw feedback/internal action names, untranslated messages | Yes; native config paired toggles + mounted expiry/owner tests | `r29-sweep-*-hotkeys-*` |
| Mini Games | Eight combinations; G configuration switch paired save | G number editing saved each keystroke; raw errors; disconnected Food House state | Yes; blur/Enter commit and localized feedback; excluded live actions unpressed | `r29-sweep-*-mini-games-*` |
| Settings | Eight combinations; focus setting observed; FPS/ping controls in Part A | Fake/unwired update/export surfaces; error language/retry | Inherited fixes retained; paired focus-click proof needs sequential-state correction | `r29-sweep-*-settings-*` |
| Top/account bars | All matrix captures; theme, chooser, hide/restore | One-game policy/UI error localization fixed | Hide/restore and theme exercised; full metadata/dropdown closeout open | matrix receipts + shell mounted checks |

Excluded live actions remain Unlock Land Cell, F6/F7/F8 item use, Treasure claim, Food House/Black Market, Alliance Drill rally. Native file/folder/export dialogs cannot be driven under the background-only/no-focus boundary; screen/disabled/error contracts will be reported separately. Earlier equipment/City mutation proofs are explicitly inherited, not new matrix mutations.

### R29 performance - measurement underway (2026-10-20)
- Quiet instrumented before run `native-ui-20261020-131920`, 60-second sample: 3,110 observed game frames / 60.0483 frame-seconds = **51.79 FPS**, **8.21 ms of our combined Lua handlers per observed frame**, 129 frames over 25 ms. Exact owned cleanup/restoration passed.
- Separate original-script game WITHOUT app: 60-second passive sample, **126.06% of one logical CPU**, mean working set **1172.66 MiB**, zero measured game write bytes; owned launcher/game closed and original hashes unchanged (`artifacts/feature018-r29-perf-research/{game-without-app,no-app-exit}.json`). FPS is UNKNOWN for this control. Intel-signed PresentMon was attempted without injection; Windows denied the ETW trace for missing performance-log privileges. No elevation/group change was attempted. CPU is not an FPS substitute.
- Candidate limits duplicate Overview/FEATURE018 lifecycle work to at most 20 Hz across Update/LateUpdate/callbacks; each actual action still takes fresh native exact-owner authorization. Exact consumed mailbox envelopes skip redundant native account/lease observations; new envelopes retain every gate. Opt-in counters measure only our Lua and write a bounded report once per five seconds. Regression checks cover competing callbacks and clock rollback; existing owner-transition/game-bridge suites pass.
- First candidate idle sample: **48.94 FPS** and **4.61 ms of our Lua per observed frame**. This supports less own-handler time; it does **not** prove an FPS improvement or the 5% no-app target. App tree and game sampling, busy phases and a genuine 30-minute memory run are now underway in `native-ui-20261020-132638`; do not claim complete/performance accepted.

### R29 progress - candidate source/check checkpoint (2026-10-20)
- Expanded the packaged sweep driver to require the visible active route, sequential React commits, scoped Hotkeys/Mini controls, native catalogue readiness, visible City icon decoding, and explicit restoration/disabled-control receipts. Added safe chooser validation/recovery, Home preferences, note-dialog cancel, Map mode/result/filter/keyword controls, City Refresh/zoom/fit, equipment preset selection, AFK settings/Add menu, G count edit/restore, and Settings focus preference. No excluded action or blanket button-click loop was added. Live proof of this stronger driver is next.
- Found and fixed a real stale server-ID validation message: editing the input or reopening its chooser clears the prior error without a native jump. Mounted English/Chinese recovery checks pass. Corrected Map result labels split at the last letter at 900 px by fitting complete labels to available width; eight EN/zh-CN light/dark width cases pass.
- City now renders a bounded viewport/overscan of cells while all native movement/selection lookups retain the full map, lazily requests offscreen icons (decoding eagerly once visible), and centers on displayed buildings, including saved draft placements. Native live capture and corrected actual-container scrolling remain to be rerun.
- Read-only subagent review found no owner-fence/dedupe regression. It identified the retained hidden React Activity DOM and wrong scroll-container proof gaps, both corrected in the stronger drivers. Frame counters deduplicate actual Unity frameCount; timing is **Unity cadence and timed pump-body cost**, not presented frames or the complete cost of gating/diagnostic observers. Lifetime max counters are not phase-specific maxima.
- Added bounded immutable reflection-member metadata caching: at most 16 actual CLR types x64 members. Every value read still calls GetValue on the CURRENT object; native UID/server double reads, lease/control reads, connection state and authorization are uncached. Six actual-helper Lua53 tests verify changing values/instances/types, nil semantics, transient resolver retry, property fallback and bounded FIFO eviction. Fresh native timing of this additional candidate remains open.
- R29 package helper/boundary suite added (7/7 PASS), preserving source/pushed-commit/frontend/bridge gates, ASCII and UTF-16 account-byte scans, private-profile exclusions, explicit per-tab/performance evidence labels and limitations. No ZIP built yet.
- Final-source checks at this checkpoint: canonical UI PASS including new Home, chooser, viewport, label/responsive and error regressions; full Release Desktop.Checks PASS; every FEATURE018 offline/Lua suite PASS; budget and reflection-cache suites PASS; offline package roundtrip from the exact captured v26 ORIGINAL backup PASS without modifying originals. Logs `artifacts/r29-final-{ui,desktop,lua}-check.log`, `r29-final-original-package-check.log`.
- A newly added mounted test assertion initially compared a React/jsdom DOM element to null after input events had not initialized correctly. Node failure formatting consumed excessive external memory. Only exact owned test PID 4192 was stopped; comparisons now use text/booleans and react-dom initializes after DOM globals; the regression passes in about two seconds. Measurement conditions retain this negative/pressure interval; exclude it from FPS interpretation and disclose its effect on working sets. No owner process was stopped.
- The genuine 30-minute memory phase is still in progress on the previous budget/dedupe candidate. It precedes the additional bounded metadata cache and final UI refinements; do not mislabel it as a completed exact-final-build run.

### R29 progress - final native closeout preparation (2026-10-20)
- Added an isolated memory-only proof surface and sufficient opt-in staged lifetime for a genuine final-candidate 30-minute run followed by the strengthened safe-control sweep. Extended explicit retained-native DB reuse to tab proof only after the same complete 2500/2500/zero-failure read-only admission used by browse proof; this is real saved native data, not a preview fixture or current census claim.
- `tools/r29_run_checkpoint.py` orchestrates only its newly created app/root: exact admitted UID/server, Part-A setting restoration, actual-container scrolling of retained Map/City/equipment data, quiet final-cache sampling, optional bounded ordinary-monster AFK search with one initially idle staffed squad, Stop/config restoration and owned march return, final 30-minute measurement, then the exact Home Close sweep. Account identity is an operator argument; no fixed proof UID is shipped. Failures consume a one-shot operation and end through owned lifecycle cleanup; no excluded actions or OS input are used. AFK proof is limited to ordinary native level-1 targets within distance 1, at most one execution, with all item use/alliance rally settings disabled. Any new native result must be reviewed, not inferred from this driver.
- Full UI, Release Desktop, every FEATURE018 offline/Lua suite, R29 budget/cache/package tests and original-v26 backup roundtrip all pass. Candidate `29eb18d4` was published locally; final checkpoint driver will be committed before its source-matched use.

## R29 finished by lead (Claude) after workers ran out — 2026-10-21
Owner asked the lead to continue the leftover R29 work. Lead fixes (all pushed):
- 39670b18 proof harness: brief heartbeat gaps no longer abort runs (15 s grace for an UNAVAILABLE read only; a
  different account / lost process still fails at once). Product action gates unchanged. New Desktop check.
- 90f08395 perf-only per-part Lua timers (owner check, City, AFK, Chat, Sheep, mailbox); empty Map table (scans
  cleared at app start) is recorded as "no rows", not a scroll failure.
- 3cc9b0a3 performance fixes from those timers, plus the sweep preset selector bug (unquoted `[data-equipment-preset=1]`).

### R29 performance (final, real numbers)
| Measure | Before (90f08395) | After (3cc9b0a3) |
|---|---|---|
| Our in-game Lua, idle, per second | 223 ms | **75 ms (−66%)** |
| Our Lua per frame, idle | 2.18 ms | **0.58 ms** |
| Chat engine per tick (all Chat off) | 8.3 ms (full room rescan every 1 s, up to 185 ms/pass) | **0.08 ms** (no rescan when idle; 15 s when on) |
| Overview host tick | 20 Hz, 3.6 ms/call | **10 Hz** (heartbeat still 0.75 s; hotkey tick stays 20 Hz) |
| Frames slower than 50 ms (60 s idle sample) | 78 | **23** |
| Game CPU (one-core %, idle sample) | 396% | 272% |
| Unity frame cadence (idle sample) | 102.7 FPS | 130.3 FPS (sessions differ; indicative) |
| 30-min run: our Lua per second / FPS | 168 ms/s / 49 FPS (pre-fix run) | **86 ms/s / 131.7 FPS** |
| 30-min memory: app tree / game | — | 700→661 MiB / 1392→1397 MiB (**flat, no leak**) |
| Tab open time (all tabs, 64 cases) | — | **≤ 613 ms** (target < 1 s) |
| Preset switch (20 slots) | 8964 ms | **807 ms** (Part A) |
Limitations: FPS with NO app is unknown (Windows denied passive frame capture without perf-log privilege); FPS here
is Unity frame cadence, not presented frames.

### R29 tab sweep (final)
Final packaged runs `native-ui-20261020-160903` and `native-ui-20261020-161509` (exact test UID, exit 0, scripts
restored, no journal): 64 cases (8 surfaces × connected/disconnected × EN/zh-CN × 900/1600 px), **0 raw-code alerts,
0 preview-fixture nodes**, 69 safe controls exercised and restored.
| Tab | What was checked | Bugs found (whole R29) | Fixed? | Evidence |
|---|---|---|---|---|
| Home | 8 cases; auto-launch/reconnect prefs; Close Game | Close without confirmation; raw errors | Yes | r29-sweep-*-home-* |
| Map Data | 8 cases; mode, result tabs/search, filters, item filter, Start/Stop, Clear | Raw errors; labels split at 900 px; stale server-ID error | Yes | r29-sweep-*-map-data-* |
| Squads / AFK | 8 cases; settings card, Add menu | Offline edits/raw errors/failed-load recovery | Yes | r29-sweep-*-squads-afk-* |
| Equipment Schemes | 8 cases; Refresh, preset select + restore | Preset read retry; pending fence lost on remount; sweep selector bug | Yes | r29-sweep-*-squads-equipment-* |
| City Layout | 8 cases; zoom in/out/fit/restore | Opened on blank land; rendered thousands of offscreen cells | Yes (viewport render, centering, lazy icons) | r29-sweep-*-city-layout-* |
| Hotkeys | 8 cases; 6 config switches | Raw feedback/internal names; untranslated | Yes | r29-sweep-*-hotkeys-* |
| Mini Games | 8 cases; G switch, G count edit/restore | G count saved every keystroke; raw errors | Yes (save on blur/Enter) | r29-sweep-*-mini-games-* |
| Settings | 8 cases; focus pref; FPS/ping (Part A) | FPS/ping never applied; fake update/export surfaces | Yes | r29-sweep-*-settings-*, r29-part-a-ui.json |
| Top bar / account bar | all cases; theme, chooser, hide/restore, note edit | One-game/localization errors | Yes | sweep receipts |
Not pressed live (screen only): Unlock Land Cell, F6/F7/F8, Treasure claim, Food House, Black Market, Alliance Drill
rally. Bounded Monster AFK sample skipped (no idle staffed squad that moment; AFK proven live in R20).
Status: R29 READY for owner test.

## Owner decisions — Automation tab (2026-10-21)
The owner has not tested R29 yet ("no time to test the rest for now"); merging waits for that. Automation starts now.
- **Remove (cards and their settings/engines/strings/checks):** the whole Protection & System tab (Weekend Shield,
  Attack Shield); the whole Resource Claims tab (Building Resource Collection, Armed Truck); Automatic Alliance Train
  Boarding; from Daily Tasks: Auto Training, Automatic Construction, Free Stamina, Trucks.
- **Recover with a real working engine:** Resource gathering; Automatic Official Application; Alliance Tech
  Donations; Excavation Stronghold Resources; Alliance Gifts; Alliance Help; Automatic Treatment; Secret Task
  (incl. its Dispatch Assist settings); Ghost Ops.
- **Try to recover (owner does not know what they do — explain in plain words):** Alliance Center Resources,
  Alliance Gathering Dispatch.
- **Chat:** the settings collapse (show/hide settings button) is missing on the Chat cards — add it like the other
  cards. Treasure auto-claim stays on hold until Season 4.
- **Trade Station (owner update 2026-10-21):** try to recover it too. The owner is not sure whether it is a seasonal
  feature and will look into it later; report in plain words what it does and whether the game has it now.
- The owner is now using this laptop: background only, **no mouse/keyboard input** until the owner says ready.

## R30 assignment — Automation tab (2026-10-21)
Branch `codex/feature-recovery-018` (same draft PR). Worker: Codex first; lead audits; CoS continues if needed.
Today every Automation card except Chat is a UI shell (preview fixtures only, `data-preview-fixture=
"runtime-config-unobserved"` in production). R30 turns the kept cards into real features.

Part 0 — removals and Chat collapse (do first, one commit):
remove the cards/tabs listed above everywhere (UI, i18n in all languages, preview fixtures/contracts, Desktop
commands, checks); add the settings collapse to the three Chat cards (default collapsed, remembered like the
others). No preview/fixture data may show in production.

Part 1 — engines, in this order (commit + push after each one that passes):
1. Alliance Help, Alliance Gifts (normal + advanced), Alliance Tech Donations, Automatic Official Application.
2. Automatic Treatment (treat wounded, ask Alliance help, collect finished — never spend Gold/gems to speed up).
3. Secret Task (claim finished, refresh to the chosen weekly quality, dispatch; Dispatch Assist) and Ghost Ops
   (start + share own, join eligible alliance missions, claim on time).
4. Resource gathering (per squad: resource type + level, scan radius, manual-pause resume delay, recall on disable).
5. Excavation Stronghold Resources, Alliance Center Resources, Alliance Gathering Dispatch. First find out from the
   original 0.3.17 artifacts / current game what each really does and write it in plain words in the audit doc.
6. Trade Station (auto-buy the selected items with the selected currencies, optional cross-server). First find out
   whether it is seasonal and whether the sub-account has it now; if not available, build + inert-check it and mark
   "waiting for owner". In-game currency only, never real money, at most one test purchase.
For every feature:
- Recover the original 0.3.17 purpose/defaults/settings (exact SquadPanel/automation UI contracts, research branch
  `origin/research/offline-controller` via `git show`, and the current v26 game routes). Label OWN_DESIGN where the
  original engine is unknown. Follow the pattern of `tools/feature018_chat_engine.lua` / `feature018_afk_engine.lua`.
- Real game route only; result shown from a fresh game read (submission is not success); never replay an uncertain
  send; switch off = stops at once, nothing queued keeps running; settings saved per profile; plain-language status
  and errors; Run now where the card has one; works on any account (UID lock is proof-harness only).
- **Performance (R29 lesson):** zero work when the feature is off; when on, check on a slow timer or on game events,
  never a heavy scan every tick. Add each engine to the R29 perf counters and show idle cost stays ≈ R29 (≤ ~80 ms
  of our Lua per second with all Automation off; report the number with everything on).
- Inert Lua checks (like `feature018_chat_engine_checks.py`): happy path, off/stop, uncertain send, wrong account,
  nothing to do, game error.
- Live proof on the sub-account in the packaged app (background WebView harness): at least one real successful
  action per feature where the game has something to do; record what was done and the before/after game state.
  If the game has nothing available (no wounded, no gift, no stronghold occupied, no Ghost mission, etc.), record
  that honestly and mark "waiting for owner/event" — do not fake it.
Safety for live tests (in addition to the standing rules):
- Exact sub UID 4799432499002212 only; any other UID (incl. owner main on 2212) read-only.
- Resource gathering / Gathering Dispatch: send only to EMPTY tiles or alliance points — never to a tile occupied
  by another player (that would be an attack); recall test squads at the end.
- Treatment: no gold/gem speed-ups. Secret Task refresh: free refreshes first; gem refreshes only if the original
  did so, at most a few during testing. Tech Donations: standard resources only (as the card says).
- Official Application: one application during the test; record the position applied for.
  **Owner rule 2026-10-21:** the feature must support all six positions, but NEVER test-apply for Vice President
  (10002), Secretary of Strategy (10003) or Secretary of Defense (10004). Live tests may use only Construction
  (10005), Science (10006) or Internal Affairs (10007).
- Never press Unlock Land Cell; no shields; no Treasure claims; no Train Boarding (removed).
- Background only: no mouse/keyboard/Windows-MCP input, no focus stealing, never touch the owner's own LW-Control
  app, game window, or node tools (CoS, Codex, REA). Stop only your own session; restore scripts after exit.
Deliver: an "R30 Automation" table in this doc (feature → what it does in plain words → route → inert checks →
live result → status), perf numbers, all checks PASS (UI, Desktop.Checks, every feature Lua suite incl. new ones),
and an owner-test ZIP from `tools/feature018_r30_package.py` (copy the R29 script; same CRC/test-UID gates).
Workers never merge, publish, force-push or reset.

### R30 progress (worker updates this)
- 2026-10-21 Part 0: removed the two retired tabs and nine retired cards, their preview fields/defaults/runtime fixtures, dedicated Resource Claims Desktop service/wiring, task status entries and legacy profile task settings. Retired feature catalog keys are removed in all nine languages and fenced against regeneration. Shared Gathering shield-status text is retained.
- Chat: all three native cards have default-collapsed Settings with expansion remembered per profile/card. Mounted inert check exercises collapse/reopen/remount for each card; Treasure remains unavailable and no claims were tested.
- Verification: Release Desktop build PASS (0 warnings/errors), Desktop.Checks PASS (`HOME_FEATURE_DELIVERY_CHECKS_OK`); removal/localization/mounted Chat checks PASS. Full UI suite PASS, including the mounted removals/Chat check. No game session or installed script was touched for Part 0.
- Recovery underway: read original AutomationPanel from `origin/research/offline-controller` with `git show`, and v26 package admission passes (3/26, original entry `50f3ae90…137`). REA static artifact evidence `ev_38e82f66d52d6e316c62c0aaf162aba1f904242e80ac3f009cfa39f5b725c671` inventories the recovered Automation JavaScript; static analysis is not engine/live proof. Original engines remain UNKNOWN; native adaptation/scheduler will be labelled OWN_DESIGN.
- Part 0 committed/pushed `0e76cd8d`.
- Alliance Help native/package proof PASS: `artifacts/map014-takeover/native-ui-20261021-004150/r30-allianceHelp-ui.json`. Real WebView switch enabled the per-profile engine; one native request (`al.help.all`) changed Help availability **1 → 0**, independently read back before confirmation; switch/explicit cleanup stopped it. `owned-exit.json` confirms exact own-session exit and all three original script hashes restored. No OS input or owner-window control.
- Shared scheduler is OWN_DESIGN: constant off gate, slow 1 s active poll / Help check each minute, authenticated UID/server/launch-lease fences, no uncertain-send replay after off/on, explicit Stop barrier survives a failed config save, per-service R29 counters. Browser supplies only service kind; settings and game UID come from trusted native profile/session.
- Help checks: actual Lua adapter/scheduler PASS (happy/off/Stop/uncertain/wrong UID/wrong lease/empty/game error/Run now); mounted native card PASS (save/enable, stop-before-save, disk failure), managed checks PASS (profile independence, schema rejection, no forged UID/config), Desktop.Checks PASS; all 30 feature Lua suites PASS. Original-package roundtrip and unchanged-original-bytes checks PASS.
- Performance correction in progress: initial 20.36 s idle sample **97.60 ms/s total** (Overview 49.68 + FEATURE018 47.92), Automation 0. This is above target, not an accepted perf pass. Found host Chat status polling still called full `scan()` even off; removed that path (explicit Run now and 15 s enabled backup retained), added repeated-status inert regression. A fresh 40 s packaged idle measurement is running. R29 counter labels now accept the internal per-service Automation names.
- Help closeout: the corrected fresh **40.70 s** sample is **47.55 ms/s total Lua**, with **0 Automation work while off**, satisfying the bounded idle target (`artifacts/feature018-r30/help-live-perf/idle-perf.json`). The same run confirmed another real Help 1 → 0 but its English-only UI witness predicate failed against actual Chinese clone UI; preserve this negative at `native-ui-20261021-004752` (not a gameplay failure). Language-independent packaged DOM witness now passes at `native-ui-20261021-005226`: actual card reports `nothing_available`, zero gameplay requests, then stopped, exact script restoration PASS. Original positive Help proof remains `native-ui-20261021-004150`; the no-work observation does not replace it.
- Full UI suite PASS after Help integration (`help-ui-pass-final.log`); Desktop.Checks PASS (`help-desktop-check.log`); all 30 feature Lua suites PASS (`help-lua-check.log`). Original-package candidate roundtrip PASS. No final all-engines-on performance or owner ZIP is claimed yet.
- Help committed/pushed `e37c7fce`.
- Alliance Gifts recovered: original UI default **off, 120-minute interval**, range 1–1440. OWN_DESIGN engine first sends the source-native `AllianceGiftList(0,1000)` inspection and waits for `RefreshAllianceGift`; unloaded cache is never an empty positive. It then uses `AllianceReceiveAllGift(category)` for normal (1) and advanced (2), with per-UUID `receiveState=1` plus fresh native `receiveTime` confirmation. Reads do not count as claims. Both categories are processed in one bounded cycle; off removes its event listener and cancels pending read-only work.
- Gifts packaged proof PASS at `artifacts/map014-takeover/native-ui-20261021-005938/r30-allianceGift-ui.json`: one real list query, **2 normal + 12 advanced gifts** claimed via **2** `alliance.reward.allreceive` requests; all **14** exact UUIDs confirmed. Native card's language-independent DOM witness, switch off, own game exit and exact script restoration all PASS. No currency purchase or OS input.
- Gifts checks PASS: actual Lua fresh query/two categories/exact post-state/120-min interval/zero off/Stop/partial response/uncertain no-replay/wrong account/empty/game error; mounted interval invalid/save/Stop-even-with-invalid-draft; managed schema/default/isolation checks and full Desktop.Checks. Full UI suite PASS (`artifacts/feature018-r30/gifts-ui-check.log`).
- Gifts committed/pushed `3806ac7f`.
- Donations IN PROGRESS. RECOVERED UI: off, threshold 15 (1–30). Native ordinary route is `AlScienceDonate(scienceId,1)`; premium `AlScienceGoldDonate` / option 2 are excluded. OWN_DESIGN cadence: at most one ordinary donation per minute while remaining attempts meet the selected threshold; prefer recommended eligible research, otherwise ascending eligible research ID. Native UI `GetScienceDataById` supplies dependencies/level/research/progress checks; ordinary server `lowRes`/`lowNum` supplies cost, and premium currency enum values are refused.
- Preserve Donation negatives `native-ui-20261021-010738` and `native-ui-20261021-011104`: both made **zero** donation sends and restored exactly. They are **NOT** accepted "nothing available" witnesses. The second diagnostic found **7** native-donatable research entries but their `res`/`resNum` were unhydrated zeros. `AllScienceRefresh` only fetches the list; recovered `AlScienceFresh(scienceId)` fetches `lowRes`/`lowNum`. Engine now waits for that separate detail response before eligibility/spending. Actual Lua regression covers unhydrated zero cost; a corrected packaged trial is being prepared.
- Donation corrected packaged proof PASS at `artifacts/map014-takeover/native-ui-20261021-011511/r30-allianceDonate-ui.json`: **2 read-only queries + 1 ordinary donation**, `al.science.donate`, research **10011200**; native attempt count **29 → 28**, research progress **201100 → 201350**, ordinary resource type **2** balance **1226704747 → 1226704347** (exact **400** cost). No premium donation route/currency. Native card result, Stop, exact owned exit and script restoration PASS. Actual Lua suites cover both ordinary resource types, forbidden premium type, balance/threshold/eligibility, separate detail hydration, error/partial/uncertain/no replay, account/off/Stop. Full UI suite and Desktop.Checks PASS (`donations-ui-check.log`, `donations-desktop-check.log`).
- Donations committed/pushed `016eaf7c`.
- Official Application IN PROGRESS: recovered off/position None and six allowed position choices. Inert checks cover native string IDs, exact position + UID in freshly fetched lists, no optimistic local-flag credit, eligibility/already queued/held/refusal, off/Stop/uncertain no-replay. `native-ui-20261021-011956` sent only a failed list inspection (numeric/native-string mismatch; corrected); **no application** there. `native-ui-20261021-012307` sent **ONE** application for **10005, Construction Minister**; fresh ordinary application list stayed empty, so the result is **UNCONFIRMED**, not success. **The test application budget is exhausted: DO NOT send another application.** Both sessions stopped/restored exactly.
- Official source reveals a separate auto-accepted appointment queue (`KingdomPositionAppointmentList`, `OfficialAppointmentListRefresh`). Added checks for that exact-position UID state, plus an explicitly requested read-only observation route that never enables the engine or sends applications. Next trial is **read-only reconciliation only**, `observe-officialPosition`; preserve the original unconfirmed send. Also corrected Run now's transport acknowledgment to `accepted` (not unsupported `submitted`); queued work still cannot be reported as game success.

- **Lead tidy-up 2026-10-21 (Codex out of tokens):** no game/app left running, installed scripts are the exact originals, no recovery journal. Committed Codex's uncommitted Official Application work as a checkpoint. Found and fixed one real defect from the Help commit: the wrapper called `automation.CleanupAfterLeaseLoss()` and `automation.IsActive()` without `pcall`, so an Automation error on account change would have skipped Sheep/City cleanup; both are now protected like the other modules, and `feature018_owner_transition_checks.py` (which had been failing since `e37c7fce`) now stubs an Automation module whose cleanup throws and asserts the others still retire. Checks PASS: Desktop.Checks, full UI suite, all feature Lua suites incl. package roundtrip/original bytes. Owner rule added: never test-apply for positions 10002/10003/10004. Official Application stays IN PROGRESS: the one 10005 application is unconfirmed; next step is the read-only reconciliation only.

#### R30 Automation

- **Worker continuation 2026-10-21 — Official reconciliation:** fresh packaged read-only queries at `artifacts/map014-takeover/native-ui-20261021-053913/r30-observe-officialPosition-ui.json` report Construction `10005`: application queue 0, appointment queue 19, exact test UID in neither queue, own application 0 and current office 0. The engine stayed off (`submitted=0`); no replacement application was sent. These later empty/missing own records do **not** establish whether the earlier application was lost, rejected, or processed in the intervening time. Preserve the original ONE unconfirmed send; live success stays UNKNOWN / owner test pending. All six positions remain supported; restricted positions were never test-applied.
- Reconciliation cleanup PASS: exact owned session exited 0; original v26 script triplet restored and no recovery journal. Full UI and Desktop.Checks PASS; all **42** currently present inert feature suites PASS via `tools/feature018_all_lua_checks.py`, including owner-transition throwing-cleanup regression, original-package roundtrip/unchanged bytes, and the offline Treatment candidate. Logs: `artifacts/feature018-r30/{reconcile-ui-check-2.log,reconcile-desktop-check.log,official-final-all-checks.log}`. Treatment's native module is prepared offline but not yet integrated or live-proven.

| Feature | What it does | Native v26 route | Inert checks | Live result | Status |
|---|---|---|---|---|---|
| Alliance Help | Complete pending requests to help alliance members | `AlHelpAll` → `al.help.all`; `AllianceHelpDataManager.GetOtherHelpList` | PASS | Packaged switch; 1 request → 0; 1 confirmed send; separate real empty/UI observation; stopped and restored | PROVEN; owner test pending |
| Alliance Gifts (normal + advanced) | Collect both categories of alliance gifts at a saved interval | `AllianceGiftList` + `AllianceReceiveAllGift`; native `RefreshAllianceGift` / per-UUID receive state | PASS | 2 normal + 12 advanced; 14 UUIDs confirmed; packaged DOM/Stop/restoration PASS | PROVEN; owner test pending |
| Alliance Tech Donations | Donate ordinary resources to eligible alliance research when saved attempt threshold is met | `AllScienceRefresh` + `AlScienceFresh` + `AlScienceDonate(id,1)` | PASS, incl. unloaded-cost regression | 1 donation; attempts 29→28; progress +250; ordinary resource −400; DOM/Stop/restoration PASS | PROVEN; owner test pending |
| Automatic Official Application | Apply for one saved position; skip if already applied/appointed | `KingdomPositionApplyList` / `KingdomPositionAppointmentList` / `KingdomPositionApply` (string IDs) | PASS | ONE Construction application remains unconfirmed; later read-only queues 0 applicants / 19 appointments, own UID absent, no office; no replacement send | PARTIAL; waiting for owner test |
| Automatic Treatment | Treat wounded, ask for help, collect finished treatment | Not recovered yet | Not run | Not run | NOT STARTED |
| Secret Task + Dispatch Assist | Collect/refresh/dispatch own tasks and help eligible alliance tasks | Not recovered yet | Not run | Not run | NOT STARTED |
| Ghost Ops | Start/share own missions, join eligible allies, collect finished rewards | Not recovered yet | Not run | Not run | NOT STARTED |
| Resource gathering | Send idle configured squads to safe public resource tiles | Not recovered yet | Not run | Not run | NOT STARTED |
| Excavation Stronghold Resources | Original purpose/route still to be verified | Not recovered yet | Not run | Not run | NOT STARTED |
| Alliance Center Resources | Original purpose/route still to be verified | Not recovered yet | Not run | Not run | NOT STARTED |
| Alliance Gathering Dispatch | Original purpose/route still to be verified | Not recovered yet | Not run | Not run | NOT STARTED |
| Trade Station | Availability/seasonality and purchase route still to be verified | Not recovered yet | Not run | Not run | NOT STARTED |

### R30 lead continuation (Claude) — 2026-10-21 (Codex out of tokens again)
- **Owner direction:** Official Application must keep trying while the chosen list is full ("spam until applied");
  test on Science (10006), currently full. Implemented: while full (apply-list length >= `auto_wonder_config.k3`
  or `GetApplyListFull`), re-read only the apply list every 3 s and apply the moment a place opens; a pass-through
  `KingdomPositionApplyMessage.HandleMessage` observer reads the server reply, so an explicit refusal (errorCode) is
  retried after 5 s instead of becoming a permanent "unconfirmed"; a server OK with no list entry is recorded as
  accepted and never resent; a personal re-apply cooldown waits for `GetOwnApplyCD`. No reply within 15 s still stays
  unconfirmed (no replay). The live-test harness refuses positions 10002/10003/10004.
- Treatment was proven live by Codex (`native-ui-20261021-054735`): collected 5580 healed soldiers, started healing 1,
  asked help 1, `gold=0`. That receipt is marked failed only because that build lacked the UI confirm attribute.
- Wired in Codex's offline modules: Secret Task + Dispatch Assist + Ghost Ops (`feature018_missions_native.lua`),
  Resource gathering (`feature018_gather_native.lua`), Excavation Stronghold + Alliance Center
  (`feature018_alliance_resources_native.lua`), Trade Station (`feature018_trade_native.lua`): engine routing, package
  entries, Desktop settings validation for every kind, native UI cards (the Secret Task card holds Dispatch Assist; new
  native Gathering and Trade cards). The engine gained adapter-owned sends (`execute`; refused before the wire is not
  uncertain) and one-shot verified cleanup (gathering recall on switch-off). Commit `e060a217`; all checks PASS.
- **What the unknown cards are (plain words):** Excavation Stronghold Resources = collect the resources from strongholds
  your alliance has captured (seasonal). Alliance Center Resources = collect what the alliance Military Center
  buildings produce (seasonal). Alliance Gathering Dispatch = send your chosen idle squads, in priority order, to gather
  at your alliance's Alliance Mine (the game allows one of your squads per mine); game route
  `MarchUtil.StartMarch(COLLECT_ALLIANCE_BUILD_RESOURCE, minePoint, mineUuid, ...)`. Module in progress.
- **BLOCKER — official game update today (manifest 1093 -> 1101, Lua content 26 -> 27):** the first lead live run
  `native-ui-20261021-081726` never reached the game. The official launcher tried to patch the scripts while the bridge's
  modified package was installed and failed (`LWLua decode failed: crc mismatch`), so the helper aborted
  (`official_lua_update_failed`). Zero game requests; originals restored and verified; no journal. The launcher left its
  own partial `LWScripts.data.tmp` and `LWScripts_27_26_u440.patch` in lwScripts (not ours; left untouched). Core files
  (LastWar.exe, Assembly-CSharp.rdl) still match the admitted hashes. The update must finish through the official
  launcher with original scripts before any live test; owner asked. The shipped app hits the same failure on every game
  update until the game is opened normally once (product follow-up: settle and retry once on this error).

### R30 handoff to Codex (lead, 2026-10-21, after commit ceaae70e)
Game is now official content **27** (manifest 1101); harness admits it (`tools/map_014_lead_native/run_ui.py`
`EXPECTED_27`, settle proof `native-ui-20261021-083513`). Live runner: `tools/feature018_r30_live.py --keep-going
--kind <k>...` (env `LWBRIDGE_R30_WATCH_SECONDS`, `LWBRIDGE_R30_OFFICIAL_POSITION` 10005/10006/10007 only).
Live results on v27 (sub UID 4799432499002212):
| Feature | Live result | Status |
|---|---|---|
| Alliance Help / Gifts / Tech Donations | proven on v26 (Codex) | re-check once on v27 |
| Official Application | Science 10006 applied, own UID in fresh apply list (48->49), `native-ui-20261021-083513` | PROVEN; full-list retry inert-only |
| Treatment | cure 1 + help 1, UI confirmed, `native-ui-20261021-083513` | PROVEN |
| Secret Task | 4 claims + 6 dispatches confirmed, `native-ui-20261021-085332` | PROVEN |
| Dispatch Assist | ran; 1 alliance task not yet complete | nothing available |
| Ghost Ops | outside dispatch window | waiting for event |
| Excavation Stronghold | stronghold 157 collected 17 -> 0 (confirmation fixed after); later run nothing ready | PROVEN by state; re-run when resources ready |
| Alliance Center | season feature unavailable | waiting for season |
| Alliance Gathering Dispatch | no alliance mine with room | nothing available (new engine, inert PASS) |
| Resource gathering | squad 1 -> empty L1 food tile 978032, march 1430091807089796199 confirmed, `native-ui-20261021-091512` | PROVEN; **recall on switch-off sent but NOT confirmed** |
| Trade Station | season unavailable (read-only observe) | waiting for season |
Open items: (1) gathering recall confirmation (`gather_native` recall Verify needs WorldMarchChange reply; check v27
field shapes like the empty-text owner fix); (2) `tools/feature018_attack_actions_checks.py` FAILS since v27 (likely a
pinned v26 assumption) - diagnose; (3) full gate (UI, Desktop.Checks, all Lua); (4) perf idle all-off and all-on;
(5) owner-test ZIP `tools/feature018_r30_package.py` (copy R29 script; same gates); (6) update the R30 table above.

### R30 worker continuation — Codex, 2026-10-21 (temporary account)
- Owner temporarily changed the proof account to **UID 1183884595002296 / server 2296**, freshly admitted in `native-ui-20261021-111448`. Test only unlocked/available features. All other account, background and spending restrictions remain; the product remains any-account. Proof launcher/queue now accept this explicit pair without using server-only authorization.
- **Item 1 PASS — gathering + switch-off recall:** `artifacts/map014-takeover/native-ui-20261021-111448/r30-resourceGather-ui.json`: squad 1, empty public L1 food tile **982563**, exact native march **1430091806997543100** and creation GUID; **submitted 1 / confirmed 1**, **cleanupSubmitted 1 / cleanupConfirmed 1**, actual saved WebView switch, UI witness and `success=true`. Owned session exited 0; all three original script hashes restored, no journal.
- Recall defects recovered/fixed: the native formation lookup can omit returning marches, so confirmation reads the captured exact UUID; native `WorldMarch.UpdateFromSFS` (0x060031e6) overwrites `clientCreateGuid` from `clientCreateUuid`, which is empty on the observed v27 return update; UI Stop then disabled-save/Configure previously detached the in-flight recall observer. Pre-send still requires exact UID/server/lease/formation/UUID/creation GUID and COLLECT target; post-send requires that same owned march become BACK_HOME after one guarded send. A different nonempty GUID is refused. Direct reply was **not observed**, and the receipt explicitly retains `replyObserved=false` / `exactRequestCausality=false`; authoritative poststate is not a uniquely attributed server ACK.
- Inert actual adapter regression PASS: SFS getter-shaped reply, empty-GUID return, returning march absent from formation lookup, repeated Stop/save without resend or detached confirmation, wrong owner/different GUID rejection, no invented direct reply. Scheduler regression PASS. Packaged harness now fails a gather proof if cleanup is absent or unconfirmed.
- Preserve failed receipts `native-ui-20261021-105054`, `105351`, `105612`: fresh gathers were confirmed but recalls were not confirmed by those builds. All exited/restored exactly; no failed send was replayed. The first temporary-account preflight refused only a changed official numeric version marker before starting any session. Package/data metadata hashes are unchanged; the exact marker-27 triplet is now admitted, with fresh normal official settlement in `111448` rather than a forged/copied incompatible receipt.
- **Item 2 PASS — attack checks on v27:** `tools/feature018_attack_actions_checks.py` rejected container `(3,27)` before any safety regression ran. All **17** pinned game modules are byte-identical to their admitted v26 copies (`HASH_DRIFT []`); only that container-version admission now explicitly accepts 26 or 27. Original entry/module SHA-256 pins, managed binary SHA/IL contracts, exact native argument tuples, ownership/alliance restrictions, pre-send revalidation and no-replay negatives are unchanged. Complete check PASS: `artifacts/feature018-r30/codex-attack-v27-check.log`. No live attack or speed-up was performed.
- **Item 3 PASS — v27 re-check of Help/Gifts/Donations:** `native-ui-20261021-111900/r30-{allianceHelp,allianceGift,allianceDonate}-ui.json`, exact temporary UID/server, real packaged controls. Help: **3 pending → 0**, **one confirmed** `al.help.all`. Gifts: fresh list query, normal **0** / advanced **0**, **zero claims**. Donations: separate list/detail queries, hydrated eligible research **10011300**, ordinary cost **200**, balance **2490699**, but **0 remaining attempts**; **zero donations**. All three UI/Stop checks pass, own exit 0 and exact original restoration pass. The v26 positive gift/donation receipts remain historical; fresh v27 empty/attempt-exhausted observations are not positive claims/donations and wait for owner availability.
