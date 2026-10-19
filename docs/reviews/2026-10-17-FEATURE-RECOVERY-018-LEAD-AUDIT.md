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
