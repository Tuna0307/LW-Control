Lead review, 2026-10-08: PIPE-CONNECT-003 accepted at e51ab590 for current-client direct userdata Connect repair, actual authenticated pipe/getStatus RPC and restoration. Fresh 4 delegate + 5 file ownership + 6 Lua lease tests and three-attempt audit pass. Full Home/Map remains PARTIAL. BW004-01: runner stopped on city state before exercising existing StartAsync -> EnterWorldMapAsync -> SceneUtils.ChangeToWorld path; desktop-input requirement is not established. Next solo relay: BACKGROUND-WORLD-RESOURCE-004, correct proof gate and exercise existing native world entry/Resource/Stop. Background game/native ordinary same-server scene transition permitted; OS desktop capture/input/focus remains restricted.

Lead review, 2026-10-08: BACKGROUND-WITNESS-002 accepted for bounded runner/two negative game connection witnesses and restoration at 35d92a24. Fresh 7 inert inverses and read-only two-root audit pass; installed originals match and no game/clone process remains. The game adapter never connected to the waiting host; no Resource start/capture is proven. Next solo relay: BACKGROUND-PIPE-CONNECT-003, distinguish native Connect invocation/root/worker/pipe-open failure, repair and background retest. Background game launch remains permitted; desktop capture/input/focus restricted. Home/Map remains PARTIAL.

Lead review, 2026-10-08: BACKGROUND-CONTINUATION-001 accepted for bounded archived diagnosis/input recheck at 79164b82. No original decryption or new feature implementation was completed. Resource cancellation predates attempt 5; 5 classifier checks rerun and 7 saved hashes preserved. Next solo owner relay: LWB317-FUNCTION-HOME-MAP-BACKGROUND-WITNESS-002, actual-provider background runner/host acknowledgement and fresh bounded Resource witness. Game launch permitted; shared-desktop capture/input/focus restricted. Native-only proof does not complete canonical UI pilot; Home/Map remains PARTIAL.

## 2026-10-08 Home/Map background witness 002 — fresh negative native proof

Solo assigned A/B checkpoints completed and marked **AWAITING_REVIEW**
(`docs/work-items/LWB317-FUNCTION-HOME-MAP-BACKGROUND-WITNESS-002.md`).
An explicit checks-only runner now creates a fresh isolated actual production
Home/Map provider composition with native pipe listener, no Auto/fixture/UI
automation; normal app option gates are unchanged. Checkpoint A pushed as
`2ca93838d1de2a4755a8ebe34d515dbf4b8b19aa`. Both real game launches
were authorised and performed headlessly; both reached session-owned Home
ready, then failed to establish an authenticated host pipe connection.
Twenty-second attempt-2 host trace: server instance 1, pending native connect
(error 997 = overlapped pending), authenticated 0, routes 0, rejected 0;
in-game Lua `hello_sent`/`clientConnected=false`. No Resource request/run
or row was acquired; do not reuse older City/Resource records as this witness.

Owned Home Stop on both, exactly restored original scripts, no journal and no
remaining game or clone process. Independent read-only reopen of actual isolated
SQLite: zero new runs, blocks and Resource rows. All focused checks passed.
Details and all failed attempts:
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-witness-002/checkpoint-B.md`;
repeatable post-run archive verification
`tools/lwbridge317/audit_background_witness_002.py`.
Next permitted engineering investigation: same-session game adapter's native
pipe connect/hello ACK, then actual current-server Resource scan/persist/Stop.
Original Lua plaintext still input-blocked; native tests do not replace
canonical UI Home/Map click/visual/manual Stop/reopen acceptance, which is
pending separately authorised interaction. Parent pilot remains **PARTIAL**.

## 2026-10-08 — BACKGROUND-PIPE-CONNECT-003 (AWAITING_REVIEW)

Corrected a demonstrated current-client integration failure:
in actual Last War xLua the `Action<string,string,string>`
`PipeClientAdapter.Connect` value is `userdata`. The previous
`MethodInfo.Invoke` path reported Lua `pcall` success but no native
entry/state and no host authentication. A direct **single** delegate call
(with no void/nil retry) resolves the gap. Three fresh isolated live
attempts include before/after negative and positive packet evidence,
session-bound native worker/open/hello-write receipts, host authenticated
ACK and a genuine production `getStatus` RPC. Read
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-pipe-connect-003/checkpoint-A.md`,
`checkpoint-B.md` and `read-only-audit.json`.

Both repaired live runs prove current server 2212, but the real game
is **not in the world scene** (`isInWorld=false`). No Resource scan,
capture/Stop run, publication/export or canonical visual test is
claimed. Those depend on legitimately entering the world view; desktop
input/capture/focus remains prohibited until owner resumption.
All three owned sessions were stopped through the production exact
identity boundary, original scripts restored and journals cleared;
final process inventory zero. All affected Lua/helper/.NET checks,
canonical frontend/Release build and publish package pass. Existing
City-only exporter and original encrypted controller restrictions
unchanged. Parent LIVE-PILOT-001 remains PARTIAL. No new delegated task
or owner desktop interaction was initiated.
## 2026-10-08 — BACKGROUND-WORLD-RESOURCE-004 A/B (AWAITING_REVIEW)

The existing direct xLua delegate/pipe authentication fix is preserved.
Source-backed checks-only correction removes the false
`isInWorld=false` pre-Start failure: actual production Scan Start
uses `MapScanStateMachine.StartAsync -> EnterWorldMapAsync` and a
fresh identity-matched `world-ready-result.json`. First live request
was `already_world_scene`, proven worldId 0/server 2212,
1000x1000 dimensions. Production current-client Resource capture
**completed 2500/2500 blocks**, zero failed, publishing **8008**
Resource rows under new exact run
`b439bfa57cd54405a14e803d21964c9d`.
Independent production `MapStore` reopened the exact database,
confirmed completed status, 50-row pages 1/2 nonoverlapping,
and zero impossible-keyword matches. The initial runner
`map_search` request was malformed AFTER durable completion; it
was corrected. Do not claim the positive completed-run query
passed at the higher command layer when only reopened store queries did.

The second isolated session, explicitly launched by this chat
after the first concurrent worker's completed game had exited and
restored all originals, proved exact active Map Stop:
run b9fc31a14f6f4c3d819c0e17efaddbe8 had one
inflight block, then production Stop committed idle/zero inflight;
durable status cancelled, zero published/staged rows. Corrected
production map_search returned zero. Both owned Home sessions stopped,
original installed triplet restored, recovery journals cleared,
and no game/clone processes remain. The shared concurrent checkout
activity was not delegation: this chat authored the heartbeat fix
df4c9551 and directly launched the active Stop; the other worker
committed first-run runner/completion evidence as d59a4aaa.
Six production state-machine boundary inverses and all applicable
headless/Release/build/package checks passed.
Review evidence in
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-world-resource-004/`
(`checkpoint-A.md`, `checkpoint-B.md`,
`completed-reopen-proof-001.json`, `read-only-audit.json`).

The full parent pilot remains **PARTIAL**: the particular live
world-ready result used `already_world_scene`, not an observed
`ChangeToWorld` callback. Canonical UI visual/click/manual
Stop/reopen A-to-A, original controller Lua semantics, and
unsupported Resource export remain unproven. No foreground
input/capture/focus was used.
# Current live-test handoff — strict parity phase

## Current 2026-10-08 background-only checkpoint

Home/Map pilot remains PARTIAL. Follow `docs/owner-directions/2026-10-08-BACKGROUND-ONLY-PILOT.md`: no desktop capture, input or focus control. The solo `LWB317-FUNCTION-HOME-MAP-BACKGROUND-CONTINUATION-001` diagnosis attributes the archived zero-block cancelled Resource run to a time before attempt-5 game launch, and separates game heartbeat readiness from authenticated pipe handshake. See its work item and `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-continuation-001/diagnosis.md`. No production fault or fresh same-session Resource proof is established. Foreground-dependent canonical Manual Resource/Stop/reopen proof awaits owner resumption. Original Lua plaintext remains input-blocked after an explicit supplied-artifact recheck; the encrypted package alone is insufficient.

---

**Current through:** `LWB-R8-097`, 2026-09-27.

Live testing is no longer driven by the old “close remaining Home/Map acceptance rows” matrix. The primary task is now original-reference recovery and parity implementation.

## Current testing rule

A live test of an equivalent/reconstructed path may be recorded as evidence, but it cannot produce a WORKING status. Home/Map acceptance requires the recovered original path itself to be the path under test; no fallback may be enabled.

Do not ask the owner to repeatedly test reconstructed behavior that has not first been tied back to LWBridge 0.3.1.

For each parity feature:

1. recover the original reference contract/bytes;
2. implement or map it to the current Last War client;
3. run automated reference-vs-rebuild checks where possible;
4. run automated current-client technical checks;
5. ask the owner only for the minimal visible interaction that cannot be captured automatically.

## Owner interaction

The owner is not expected to run commands, inspect JSON, calculate hashes, locate databases, or diagnose technical state.

ChatGPT should operate the technical collection path directly whenever permitted and request only simple UI observations/screenshots when necessary.

## Historical R7 live evidence

Ghost/Supplies population checks, Railway population checks, multi-account availability, lifecycle stress, Map scan proofs and other R7 results remain useful current-client evidence. They are not current parity gates by themselves.

Previously retired product features such as Scheduled Plunder are no longer out of scope merely because R7 removed them. R8-016 restores the offline control plane and original UI, while the protected runtime remains intentionally absent; do not treat control-plane parity as evidence that the protected action path is ready for live testing.

## Safety / action boundary

Do not perform irreversible or state-changing actions merely to make a matrix row green. Follow current tool/environment rules and preserve explicit authorization requirements for messaging/spending/consuming actions.

## Current technical sources

- `docs/strict-parity-recovery.md`
- `docs/lwbridge-parity-matrix.md`
- `docs/implementation-handoff.md`
- `BACKLOG.md`
- `evidence/lwbridge-implementation/2026-09-24-r8-current-evidence-index.json`
