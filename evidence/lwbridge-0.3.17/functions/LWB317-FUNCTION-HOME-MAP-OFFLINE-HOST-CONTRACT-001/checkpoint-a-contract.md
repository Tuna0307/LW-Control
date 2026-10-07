# Checkpoint A — exact offline host/binding contract

Work item: `LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001`  
Baseline HEAD: `bc44275be9f6c198b237504df063bd307f4fdaf0`  
Method: static/read-only installed-client inspection plus repository source tracing. No game process, desktop control, owner-runtime mutation, updater action, or protected-service access.

## Evidence classes

- **CURRENT BUILD** means the exact current rebuild source and/or the exact installed client identities in `checkpoint-a-source-hashes.json`.
- **RECOVERED 0.3.17** means the exact original `lwbridge-0.3.17.exe` hash `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`, using the hash-gated `tools/lwbridge317/*` inspectors and accepted 0.3.17 recovery.
- **UNKNOWN** is deliberately not inferred from lupa, historical 0.3.1 RVAs, lexical presence, or current source intent.

## Home runtime reader and host binding

| Fact | Class | Exact locator / evidence |
| --- | --- | --- |
| Packaged adapter exposes `ReadRuntimeSnapshot` as `public static readonly Func<string,string>` bound to `ReadRuntimeSnapshotCore`. | CURRENT BUILD | `src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:18`; source hash in `checkpoint-a-source-hashes.json`. |
| Reader accepts only `control.txt` / `lease.txt`, strict UTF-8, max 4096 bytes. Success is `"ok\n"+payload`; Windows sharing violations 32/33 are `"busy\n"`; all other failures are `"unavailable\n"`. | CURRENT BUILD | `PipeClientAdapter.cs:34-50`. |
| Overview Lua loads the adapter with `Assembly.LoadFrom`, resolves `LWBridge.GamePipe.PipeClientAdapter`, reflects static `Connect` and `ReadRuntimeSnapshot`, and rejects a missing reader. | CURRENT BUILD | `tools/current_overview_bridge.lua:241-277`. |
| Shared metadata reads call a Lua-function reader directly or otherwise call `reader:Invoke(path)`; only a string `ok\n...` is accepted, exact `busy\n` is distinguished, exception/non-string/other prefixes become unavailable. | CURRENT BUILD | `current_overview_bridge.lua:392-411`. |
| Shared `control.txt` / `lease.txt` parsing rejects duplicate keys and malformed lines and retains the 4096-byte bound. | CURRENT BUILD | `current_overview_bridge.lua:413-433`. |
| Current xLua metadata includes `XLua.DelegateBridge.Func<TArg,TResult>(TArg)`, `DelegateBridgeBase.GetDelegateByType`, `ObjectTranslator.getDelegate`, `ObjectTranslator.GetDelegate<T>`, `delegateCreatorCache`, and reflection wrapping. | CURRENT BUILD static client | Exact installed `Assembly-CSharp.rdl` SHA-256 `bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`; `checkpoint-a-xlua-static.txt`. |
| The exact reflected external adapter `Func<string,string>` has not been observed crossing the real game xLua boundary and returning a value in this owner-held offline assignment. | UNKNOWN | A lupa replacement is not this seam. Static generic delegate/reflection support narrows the dependency but does not execute the actual game/xLua conversion. |

## Ownership, readiness, and lease refresh

| Fact | Class | Exact locator / evidence |
| --- | --- | --- |
| Host writes `control.txt` with schema/version/profile/session/challenge/gamePid and optional control-pipe/adapter paths, then writes same-owner lease metadata. | CURRENT BUILD | `tools/run_overview_bridge.py:326-360`. |
| Host rejects a fresh foreign lease and clears stale runtime only through exact runtime-file identity ownership. | CURRENT BUILD | `run_overview_bridge.py:385-427`; accepted RECOVERY-003 R3-01/R3-02 remain authoritative. |
| `await_ready` accepts only exact same profile/session/challenge/gamePid, requires a fresh ready timestamp and `ready=true`, and refreshes the same owner's lease about every 0.25 s while waiting. | CURRENT BUILD | `run_overview_bridge.py:741-782`. |
| Lua validates control identity, lease schema/version/session/challenge and a 5-second freshness horizon with +5-second future tolerance. | CURRENT BUILD | `current_overview_bridge.lua:459-497`. |
| A busy shared read may be deferred only from previously verified same-owner metadata that is still inside the same 5-second horizon; busy never extends that horizon. | CURRENT BUILD | `current_overview_bridge.lua:499-508,3549-3585`; `PipeClientAdapter.cs:185-249`. |
| Owner replacement retires the active pipe and pending lanes; invalid/expired metadata clears verified lease and terminalizes owned work rather than consuming under stale ownership. | CURRENT BUILD | `current_overview_bridge.lua:3549-3585`; probe terminalization `current_live_resource_probe.lua:6771-6827`. |
| Overview writes `ready.json` only for the current validated session and continuously emits ownership/readiness heartbeat state. | CURRENT BUILD | `current_overview_bridge.lua:3452-3484,3601-3615`. |
| A real same-owner host refresh contending with the in-game xLua reader has not been witnessed on the exact current game runtime during this hold. | UNKNOWN | Inert Windows file-lock proof exists in checkpoint B; live game/xLua scheduling remains a future witness. |

## Manual City/Resource acquisition binding

| Fact | Class | Exact locator / evidence |
| --- | --- | --- |
| Resource probe uses `LWBridgeOverviewBridge.ReadSharedRuntimeMetadata` for Overview control/lease, so Map acquisition shares the same ownership/read-result policy instead of independently interpreting Windows sharing failures. | CURRENT BUILD | `tools/current_live_resource_probe.lua:549-571`. |
| Before request consumption the probe validates Overview control/lease identity and freshness; busy may defer only a still-fresh previously verified same owner. Invalid/retired ownership does not consume queued files. | CURRENT BUILD | `current_live_resource_probe.lua:1000-1049,6771-6827`. |
| A verified synchronous pump is the only path that consumes the City/Resource command. Command identity must match the active profile/session/gamePid before acquisition proceeds. | CURRENT BUILD | `current_live_resource_probe.lua:959-997,6608-6664` and per-lane identity checks. |
| City/Resource acquisition uses current `WorldPointManager` fresh-view response flags; City may issue the bounded same-server targeted view when allowed; response flags are restored before a proven result is published. | CURRENT BUILD | `current_live_resource_probe.lua:6666-6766`. |
| Current installed client passes the compatibility policy and structural `WorldPointManager` / `WorldGetBlockMessage` contract check. | CURRENT BUILD static client | `checkpoint-a-current-runtime-contract.json`, `checkpoint-a-current-map-compat.json`. |
| Original 0.3.17 host has current Map start/status/stop services and provider boundary `enterWorldMap` / `startMapScan`; current RVAs are not inherited from 0.3.1. | RECOVERED 0.3.17 | Accepted `docs/reviews/2026-09-30-LWB317-RE-MAP-001-frontend-host-contract.md` and `...MAP-002-scan-state-machine.md`; fresh `checkpoint-a-original-0317-native-handlers.json`. |
| Original 0.3.17 current host normalizer and progress arithmetic are reverified from exact bytes. | RECOVERED 0.3.17 | `checkpoint-a-original-0317-map-progress.json`; normalizer `0x14042612D-0x140426A05`. |
| The current rebuild's City/Resource `WorldPointManager` acquisition is not claimed to be the original 0.3.17 protected wire/traversal implementation. | UNKNOWN / explicit non-equivalence | `run_live_resource_probe.py` identifies its command-file rendezvous as rebuild policy; original reviews keep game traversal/native producer details separately bounded. |

## Hash-gate correction

The first attempt to apply historical `inspect_lwbridge_map_tick_cadence.py`,
`inspect_lwbridge_native_capture_hooks.py`, and
`inspect_lwbridge_native_capture_ack_constants.py` to the 0.3.17 reference
failed closed because those tools are pinned to an older 0.3.1 outer executable.
Their gates were **not** weakened. Current 0.3.17 facts instead use the
`tools/lwbridge317/*` inspectors pinned to `4E9C...6783`.

## Checkpoint A conclusion

No source-backed production defect is established by contract tracing alone.
The exact current source is internally consistent about reader result grammar,
same-owner freshness, retirement and acquisition preflight. The remaining
marshalling/readiness uncertainty is an execution dependency, not a license to
add a fallback or alter ownership semantics.
