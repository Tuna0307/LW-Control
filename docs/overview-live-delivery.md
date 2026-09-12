# Active delivery: Overview launch, injected bridge message, and close

Owner-directed priority change, 2026-09-11. This is the only active user-visible feature. It supersedes resource-first and monster-next instructions elsewhere. The full 47-case specification in task.md remains future scope; this milestone does not claim all Overview settings or both pages are complete.

## The result the owner must receive

1. In the normal login-free rebuilt Overview page, the owner clicks **Launch Game / 启动游戏**.
2. The actual installed Last War client opens through the supported rebuilt launch path. The bridge is successfully loaded/injected and establishes a verified session with that running game.
3. The exact text **LWbridge is running** appears at the **top centre inside the game view**. Preserve the owner's spelling and capitalization. It must indicate current bridge readiness, not merely that a process exists.
4. In Overview the owner clicks **Close Game / 关闭游戏**. The correct launched game session closes, its bridge session ends, and Overview returns to stopped state with cleanup/restoration accounted for.
5. The owner personally verifies the result and explicitly permits moving on. PM review, a worker completion message, CI success or a research checkpoint cannot select the next feature on the owner's behalf.

The message is an explicit new owner requirement. Any rebuild-specific rendering, lifecycle or scheduling decisions must be documented as IMPLEMENTATION POLICY, not falsely attributed to the original executable. Do not satisfy it with an always-on Windows overlay, browser banner, edited screenshot, mock window or text drawn before a real game bridge is ready. Prefer a supported game-side rendering path backed by current-runtime evidence. If the exact requested placement/rendering cannot yet be implemented, report it as incomplete rather than quietly substituting another location.

## Scope and current starting state

Owner: Web for research, implementation and technical validation. PM plans/audits only. The user can perform clearly guided permitted UI steps, describe results and supply screenshots; Web automatically captures technical evidence. No separate Sol role. No Daybreak assignment by default.

OVL-00 is delivered as `cc4bc7788c2502681546cede187a4daee6c0c26a`; its GitHub Actions run `34590097938` passed. The prior PM15 evidence/handoff cleanup is closed without reopening resource work.

The exact original protected launch/bootstrap/session contract is still incomplete: launch-proof/ticket production plus final `hello.ack`/request-result readiness grammar remain UNKNOWN/BLOCKED. OVL-01 therefore selected an explicit independent rebuild policy instead of inventing those fields. OVL-02/03/04 are now **TECHNICAL LIVE-PROVEN** on that independent current-v14 LuaEntry route: two fresh helper-driven current-client sessions reached correlated game-side readiness/message evidence and exact owned Close/restoration. The owner-visible normal Overview button sequence has now passed as `LWB-OVL-003`; this still does not claim recovery of the original protected protocol.

Current extension, 2026-09-12: the owner selected O04/O05 next. `LWB-OVL-004` technically live-proves launch-at-app-startup through the same accepted lifecycle. `LWB-OVR-005` through `LWB-OVR-009` now recover and offline-test the automatic-reconnection gates, process/disconnect/event/update classifiers, retry/stability tables, confirmed-event adapter and 15-minute stalled-updater transition. O05 is IMPLEMENTED/OFFLINE-TESTED but still requires the bounded real-game recovery run and combined owner-visible O04/O05 verification. After that acceptance, continue directly to Player City and the remaining Map Data types in the owner-defined order recorded in `task.md`.

### Current accepted checkpoint - `LWB-OVL-003`

Implementation commit `dc82bc9bebd9e1b56c56621edd18e905a7e3ee23` is pushed with GitHub Actions run `34603661402` passing. The exact rebuilt owner-test package is EXE SHA-256 `0435223634a7804a442c5f499db80fb2df6552075263ee738b03cc848accb473` and DLL SHA-256 `ecb43f8084a145f8839185c7b6b7f0398b1bb1481df222c62d087f533958e872`. `Start Overview Verification.cmd --self-test` passes, and its deployed Overview helper/Lua hashes match the source files.

The initial live attempt exposed a UTF-8 BOM embedded into the generated Lua wrapper; the game therefore could not load `DataCenter.Global.LuaEntry`. The repaired source is BOM-free and registration is lazy across the preserved LuaEntry lifecycle. A later live attempt reached exact game-side readiness but proved Windows locks `LWScripts.data` while the game remains open. The implementation now keeps the verified candidate installed only for the exact owned ready session, journals the exact originals, and restores them only after exact-PID normal Close releases the lock. An abnormal game exit remains `recovering` until that journal is restored rather than being reported clean.

Two fresh current-client cycles then passed with distinct sessions/PIDs. Both produced exact same-session readiness with `messageVisible=true`, text **LWbridge is running**, `registrationMethod=UpdateManager.AddUpdate`, and render path `GameFramework/UI/UIContainer/LWBridgeOverviewReady/Message`. Both Close operations used `Process.CloseMainWindow()` on the exact owned PID, proved process exit, restored the v14 package to SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`, and left no recovery journal. The second cycle used the deployed Release helper and wrote a `COMPLETE` automatic attempt bundle. Durable evidence is [`2026-09-11-ovl-live-lifecycle.json`](../evidence/lwbridge-implementation/2026-09-11-ovl-live-lifecycle.json).

This is **OWNER ACCEPTED for the selected Overview Launch/message/Close milestone**. In the normal Overview run, session `61732f78f03e4643bb5329b40012c89a` produced correlated automatic start/ready/Close/restore evidence, and the owner supplied a 1920x1080 screenshot visibly showing the exact **LWbridge is running** text near the top centre, then reported "yup all working". Durable owner-acceptance evidence is [`2026-09-11-ovl-owner-acceptance.json`](../evidence/lwbridge-implementation/2026-09-11-ovl-owner-acceptance.json). No Resource/Monster operation was started; **stop and wait for the owner to choose the next feature**.

## Ordered tasks for Web — continue without a PM stop after each checkpoint

### OVL-00 — finish the interrupted handoff, then switch focus

Inspect actual branch/worktree and the pending PM15 evidence/docs. Finish the reported small documentation cleanup and commit/push/verify that coherent prior checkpoint, preserving the new Overview priority banners. Do not stage another task's work, claim a previously reported remote hash still matches without checking, or amend/rewrite the pushed code commit. Keep code/evidence scope and owner acceptance distinct. CI should be checked against the actual delivered revision. Do not repeat successful expensive suites without a relevant change/failure, request an owner recorder rerun, or reopen resource research merely to finalize documentation.

### OVL-01 — establish the exact launch-to-ready dependency chain

Read AGENTS.md, task.md, `docs/lwbridge-injection.md`, `docs/official-runtime-architecture.md`, the relevant ledger entries O02/O03/S01, current Overview command handlers and saved bootstrap/session evidence. Verify the reference and current-client identities before new recovery. Trace the actual normal Launch button -> handler -> launch/session ownership -> injected/loaded bridge -> authoritative ready response -> game-side display -> close/cleanup path.

Reuse R5-001 through R5-007 findings where applicable. Record only the precise missing facts blocking this chain, the source/method that can answer each, and the implementation it unlocks. ESC-005 is relevant again as a source index for session acknowledgement/readiness/request-result gaps; relevance is not specialist assignment or approval to repeat restricted operations. Do not assume the original protected proxy can operate without its unresolved prerequisites or invent its envelopes/credentials. An independent rebuild design must be explicit policy and compatible with evidenced current game interfaces; it must not be called recovered original protocol.

Deliver an implementation route and a short dependency table with known/unknown facts. Proceed directly with supported dependencies. Do not use research breadth or new tools as the milestone: every investigation must unblock the specified launch/readiness/message/close result.

### OVL-02 — wire the normal Overview launch and real bridge session

Implement the evidenced supported launch/injection path behind the actual Overview command. Discover/validate the game installation and handle launcher/update state accurately. Bind the selected profile, launch attempt, actual game path/PID and bridge session. Guard duplicate Launch and interrupted/failed starts. Existing-game behavior must follow the recovered contract or an explicitly documented rebuild policy; do not silently treat an unrelated running instance as owned.

Keep stopped, starting, bridge-not-ready, ready, stopping and failed outcomes distinguishable using recovered UI states where available. A spawned process, loaded DLL/module or successful injection API return alone is insufficient for ready. Establish same-session game-side execution and a current authoritative response using a source-supported harmless readiness mechanism. Preserve exact request/session correlation and make stale/disconnected responses unable to turn the current session green.

Do not enable the UI with fabricated readiness, stubbed responses or arbitrary protocol fields. If a required contract is still unknown, keep that transition explicitly unavailable and continue only direct permitted dependencies.

### OVL-03 — render the requested message from actual readiness

Once the bridge session is genuinely ready, display **LWbridge is running** inside the game, horizontally centred near the top of its current view. Use the actual game viewport and an evidenced compatible rendering path; document any layout policy instead of inventing original pixel coordinates. Ensure the message is visible in the owner's normal game mode/resolution without covering required controls, and does not duplicate across starts.

Tie the displayed state to the same game PID/bridge session that Overview reports. Do not display it during a failed/partial injection. If the bridge becomes unavailable while the game continues, remove/change the running indication and record loss of readiness; an independent game-side expiry/watch mechanism may be needed and must be justified/documented rather than guessed as original behavior. A screenshot confirms visible placement; automatically captured runtime/session evidence must establish why it appeared.

### OVL-04 — wire normal Close Game and cleanup

Close the correctly owned game from the Overview button. Prefer the supported normal close path; do not broadly terminate by process name or affect other workers/unrelated applications. Record the actual exit and end pending bridge work so stale events cannot restore ready status. Track cleanup/restoration and preserve failures/partial-state journals instead of deleting evidence to make the result appear clean.

Cover a failed start, bridge disconnect, game already exited, repeated Close and a new Launch after Close. The complete happy path should leave no attempt-owned game/helper/bridge session running. If modified game files require restoration, verify the supported restore contract and exact file identity; do not promote corrupt temporary data or relax integrity gates.

### OVL-05 — technical evidence and beginner-friendly test package

Web owns all technical recording. Reuse the repaired session isolation and observation-health components where useful; do not require the owner to operate tools, capture terminal output, find logs/databases, compare hashes or debug cleanup. Deliver an actual tested collector or automatically collecting app mode, plus a simple entry point if local initiation is necessary.

The old passive resource `--owner-evidence` mode intentionally blocks Launch/Close. It is not the Overview test mode. If a separate Overview verification mode is needed, scope its permitted lifecycle actions and passive instrumentation explicitly and test the boundary; do not silently remove the old read-only protection or use the new mode to replay a denied scan/observation. No screenshot/click workaround is implied by shell access.

Save one durable evidence bundle per attempt with actual build/client/profile/session identity, launch/injection/readiness events, game-side message evidence, normal Close request/exit, integrity/cleanup outcome, errors and partial/interrupted state. Correlate the owner screenshot to the same session; never accept any visible banner as proof by itself. Record failed observations as unknown/incomplete, not clean success. Exclude secrets and unrelated personal data.

Use isolated meaningful tests for state transitions, duplicate/stale sessions, message-not-shown-before-ready, disconnect/loss of readiness, and exact ownership/cleanup. Keep synthetic data confined to tests. Prepare clear Chinese/English button labels, expected screens, when to take screenshots and when to stop. Do not publish instructions referring to a script/shortcut that does not yet exist.

### OVL-06 — actual current-game demonstration and owner verification

When implementation, capture and permitted execution conditions are satisfied, give the owner the short guide in `docs/user-test-checklist.md`. The visible sequence is normal Overview Launch -> actual game with **LWbridge is running** top-centre -> Overview Close -> actual game closes. Perform a further start/close cycle where permitted to detect stale ownership/reused readiness; this is validation policy, not a claim about original behavior.

Web reads the automatic evidence and owner descriptions/screenshots, fixes the first failed step, and continues this same feature. PM may audit periodically, but there is no mandatory return after every subtask or commit. Stop to request necessary owner observations, present a real external blocker, or seek a reviewed bounded specialist assignment; do not generate filler while no direct permitted work remains.

When the owner says it works, record the exact tested build/client, evidence and owner acceptance. Then **wait for the owner's next feature instruction**. Do not automatically resume maps or monsters.

## Restriction handling

Existing operation-specific SB restrictions remain recorded; a priority change does not clear them. At each investigation distinguish unknown contracts, missing tools, missing test targets and actual denials. Do not assume every new Overview analysis is prohibited because a previous resource operation was rejected; assess the exact action. Conversely do not reconstruct a denied operation through another model/tool/owner-run script. Document the specific unresolved dependency and independently permitted next step or necessary external condition.

User authorization covers ordinary project tools, installation and game testing as stated in AGENTS.md, subject to higher-priority rules. Daybreak receives only a concrete reviewed ESC when Web has exhausted relevant permitted methods; no broad injection assignment is created merely because the topic is difficult. Never promise the external restriction can be removed by PM approval.

## Exit checklist — all required for this delivery

- [x] OVL-00: prior pending evidence delivered without restarting deferred work (`cc4bc77`, CI passed).
- [x] Normal Overview Launch opened the identified current game and established fresh same-session bridge session `61732f78f03e4643bb5329b40012c89a`.
- [x] Readiness is supported by current game-side execution/response, not process presence or an overlay alone.
- [x] Exact **LWbridge is running** was visibly confirmed by the owner near the top centre inside the real game, correlated to the same automatic ready session.
- [x] Normal Overview Close ended the exact owned game session and automatic evidence proved byte-exact restoration/cleanup.
- [x] Failed/duplicate/stale/disconnected lifecycle paths are truthful; a repeat start/close does not reuse old success.
- [x] Automatic technical capture and the verified beginner entry point/guide are delivered; the owner-visible run populated the correlated normal-app session bundle.
- [x] Web's applicable checks, durable evidence and GitHub delivery are verified; owner verification is complete for this selected milestone.
- [x] **Owner explicitly confirmed "yup all working". Selected Overview milestone accepted. STOP and wait for owner selection of the next feature.**

For progress reports use: which of Launch / bridge readiness / in-game text / Close works live, exact remaining broken step, next action, and whether owner input is required. Research, builds and script work are supporting progress, not additional delivered live features.
