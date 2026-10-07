# Checkpoint C — binding/acquisition dependency matrix

Work item: `LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001`
Date: 2026-10-07
State: offline work complete; lead review required.

This matrix distinguishes **established offline**, **established only as static/inert
evidence**, and **future live witness required**. A future witness description is a
plan only. The owner has not authorized live/shared-desktop execution in this work
item.

| Dependency | Offline state | Evidence / exact boundary | Remaining future witness |
| --- | --- | --- | --- |
| Original 0.3.17 executable identity | ESTABLISHED OFFLINE | Exact reference SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`; hash-gated `tools/lwbridge317/*` evidence in checkpoint A. | None for static identity. |
| Original 0.3.17 Map host command/service boundary | ESTABLISHED OFFLINE | Current 0.3.17 start/status/stop handlers and start/provider service are recovered from exact bytes; see `checkpoint-a-original-0317-native-handlers.json` and accepted RE-MAP-001/002. Historical 0.3.1 RVAs are not reused. | Positive runtime behavior remains separate from static host recovery. |
| Current installed client compatibility admission | ESTABLISHED OFFLINE / STATIC | Exact installed package/game/xLua/RDL hashes and current content v22 pass the existing compatibility and structural `WorldPointManager` / `WorldGetBlockMessage` checks. | At any future live session, rerun compatibility before interaction because installed binaries can change. |
| Packaged `ReadRuntimeSnapshot` .NET field and signature | ESTABLISHED OFFLINE ON ACTUAL PACKAGED DLL | `PipeClientAdapter.ReadRuntimeSnapshot` is an actual reflected/cast `Func<string,string>`; Release `--profile-runtime-owner-check` loads the packaged adapter and invokes the real delegate without calling `Connect`. | None for .NET type/signature itself. |
| Reader Windows sharing classification | ESTABLISHED OFFLINE ON ACTUAL PACKAGED DLL | Real `FileShare.None` collision returns exact `busy\n`; missing metadata returns exact `unavailable\n`. | None for Win32 classification. |
| Reader successful response grammar | SOURCE-BACKED + CONSUMER-TESTED | Adapter source returns `ok\n<payload>`; production Lua accepts only exact prefix and string type. | A future real xLua call should observe one successful control/lease payload through the external delegate. |
| Current game xLua conversion of reflected external `Func<string,string>` | **UNKNOWN / LIVE WITNESS REQUIRED** | Exact current `Assembly-CSharp.rdl` contains `XLua.DelegateBridge.Func<TArg,TResult>(TArg)`, translator delegate caches/conversion and reflection wrapping. No current assignment execution crosses the packaged external delegate through the actual game xLua runtime. | Under later owner authorization, correlate the exact packaged field object loaded by the production Lua module and observe `reader:Invoke(path)` return through the current game xLua runtime. A lupa or unrelated xLua runtime is insufficient. |
| Lua nil/error/non-string/busy/unavailable handling | ESTABLISHED INERT ON COMPLETE PRODUCTION MODULES | Six full-module lupa/Lua 5.4 tests pass; source locators in checkpoint A. These tests inject the reader and therefore prove consumer semantics, not marshalling. | Covered by the same real-xLua binding witness for end-to-end conversion/error propagation. |
| Same-owner lease publication contention on Windows | ESTABLISHED OFFLINE ON ACTUAL PACKAGED DLL + OWNERSHIP WRITER | Controlled exclusive publication blocks the real reader, then validates the complete lease; contention cannot extend the previously validated five-second horizon. | Future live witness only needs to establish that actual game xLua scheduling observes/resumes the same policy while the host refreshes the same owner. |
| Foreign/stale/malformed/missing lease rejection | ESTABLISHED OFFLINE | Actual packaged reader predicate plus Lua ownership tests reject all four classes; no cached-fresh fallback admits a retired owner. | No extra live witness required for policy; real-session correlation will revalidate owner identity. |
| Overview exact ready identity and lease refresh while awaiting ready | ESTABLISHED SOURCE / INERT HOST | `run_overview_bridge.py` requires exact profile/session/challenge/PID and fresh `ready=true`; same owner lease is refreshed about every 0.25 s while waiting. Launch-binding/lifecycle/host transport checks pass. | Observe a future authorized current-game session publish matching ready/heartbeat under the same exact identity. |
| Retired owner / replacement fencing | ESTABLISHED OFFLINE / INERT | Release native boundary/lifecycle checks plus full production Lua modules pass; invalid/replaced owners terminalize admitted lanes and do not consume new queued work. | No separate live mutation test is required; future live session should verify cleanup/retirement as part of normal stop/restart only. |
| Manual City/Resource command admission | ESTABLISHED SOURCE / INERT | Resource probe validates Overview ownership before consuming command; one verified identity is reused only within the synchronous pump. | Positive live acquisition still required. |
| Current rebuild Resource acquisition | STATIC/INERT CONTRACT ESTABLISHED; POSITIVE RUNTIME **UNKNOWN** | Current probe route uses fresh `WorldPointManager` response flags and restores them before publishing. Current installed structure is compatible. | Later authorized Manual Resource witness: one exact owned session, positive fresh response/result, correlation fields, restoration, Stop/cleanup. |
| Current rebuild City acquisition | STATIC/INERT CONTRACT ESTABLISHED; POSITIVE RUNTIME **UNKNOWN** | Same ownership preflight; City uses fresh view and a bounded same-server targeted view only when needed/allowed, restoring flags before publication. | Later authorized Manual City witness: positive row/result in one exact owned session; if targeted view naturally occurs, record it without forcing gameplay. Verify restoration and cleanup. |
| Exact original protected traversal/wire equivalence | **UNKNOWN / NOT CLAIMED** | Original 0.3.17 host/provider boundaries are recovered, but current rebuild command-file rendezvous and probe route are implementation policy. Historical 0.3.1 proxy findings are provenance only. | A functional live witness may validate supported behavior, but must not be relabeled exact original protected wire/traversal parity without separate source evidence. |
| Home global Auto Launch, Auto Scan semantic rebase, Stop, ownership/restoration protections | PRESERVED / PREVIOUSLY ACCEPTED OFFLINE | RECOVERY-003 R3-01 through R3-05 remain accepted. This work item made no product edits and introduced no fallback. | Any later live pilot must preserve these accepted contracts and stop on mismatch rather than weaken them. |
| Desktop-control capability for a future pilot | NOT A CURRENT BLOCKER; OWNER AUTHORIZATION IS THE GATE | Remote Desktop Commander is available now. Windows-MCP 0.8.7 is documented under `Windows-MCP`; actual desktop calls stayed on hold. | Before any later authorized desktop work, check both Remote Desktop Commander and Windows-MCP capability/schema, then perform the work item's fresh ownership preflight. |

## Minimal future live sequence — plan only

1. **Authorization/preflight.** Wait for explicit owner resumption. Re-read the current
   live-pilot work item, inventory processes without attaching to an owner session,
   verify current-client compatibility, and check both Remote Desktop Commander and
   Windows-MCP before reporting any capability limitation.
2. **Binding/readiness witness.** In one assistant-owned session authorized by the
   owner, correlate profile/session/challenge/PID from host control through
   `ready.json`/heartbeat. Confirm production Lua obtains the packaged
   `ReadRuntimeSnapshot` object and one real `Invoke(path)` returns through the
   current game xLua runtime. Observe same-owner refresh contention only if it occurs
   naturally in that owned flow; do not fabricate a fallback or extend the TTL.
3. **Manual Resource witness.** Perform exactly the live-pilot's bounded Manual
   Resource acquisition, record positive fresh-response/result correlation and exact
   restoration, then stop/clean the owned session.
4. **Manual City witness.** Perform exactly the bounded Manual City acquisition,
   recording targeted same-server view only if naturally required, followed by exact
   restoration and cleanup.
5. **Disposition.** Any identity, readiness, compatibility, restoration, Stop or
   provider mismatch is a truthful failure. Do not promote `LIVE_PROVEN` until the
   project lead accepts the resulting live evidence.

No step above was executed by this offline assignment.
