# LWBridge 0.3.1 recovery backlog

Last updated: 2026-09-08

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Project rules

- [x] LWBridge is the only feature authority for this repository.
- [x] Active login/account/license UI is excluded from the independent rebuild.
- [x] Original recovered frontend assets are persisted with an integrity manifest.
- [x] Fixture/capture mode is isolated from live game actions.
- [ ] Keep RECOVERED, IMPLEMENTED/OFFLINE-TESTED, LIVE-PROVEN, and UNKNOWN/BLOCKED findings separate in every feature record.
- [ ] Document each newly confirmed reverse-engineering finding as it is recovered.

## Completed foundation

- [x] Recover and reproduce the original React/Vite feature UI in WebView2.
- [x] Preserve eight normal pages and the explicitly requested hidden Advanced view.
- [x] Preserve themes, icons, navigation, and nine languages.
- [x] Add real WebView2 JavaScript-to-C# RPC with request/session IDs, profile scoping, origin validation, structured errors, cancellation, and events.
- [x] Persist stable local profile/configuration state.
- [x] Detect and validate the current Last War installation and 64-bit game/xLua binaries.
- [x] Keep unmanaged game processes distinct from a verified LWBridge-owned instance.
- [x] Recover Map Scan selected-type allowlist and `normal=8` / `fast=20` request concurrency.
- [x] Add a read-only official-runtime inspector and machine-readable current-install evidence.
- [x] Keep launch and production scanning fail-closed until bridge readiness is proven.

## P0 — Overview lifecycle

- [ ] Recover the host-side producer for `LaunchEnvelope` (`descriptorJson`, `launchProof`, `gameLaunchTicket`).
- [ ] Recover exact descriptor/proof/ticket representation and validation rules.
- [ ] Recover xLua secure/plain ABI fingerprint selection exactly.
- [ ] Implement owned `profile_instance_start`, `profile_instance_status`, and `profile_instance_stop` lifecycle.
- [ ] Require matching instance identity, bridge handshake, and fresh heartbeat before reporting connected.
- [ ] Implement startup launch preference through the same lifecycle service without double-start races.
- [ ] Implement automatic reconnect/recovery with explicit eligibility, cancellation, and bounded retry behavior.
- [ ] Recover and implement repair/update/restart presentation and state transitions.
- [ ] Validate repeated cold start, restart, disconnect, and stop cycles against the current client.

## P0 — Map Data

- [ ] Connect production `map_scan_start` to the recovered bridge/native capture path.
- [ ] Recover exact block scheduler/tick behavior, block ordering, retry rules, and resume state.
- [ ] Recover typed schemas and stable identities for city, resource, monster, truck, railway, dispatch, ghost, and treasure records.
- [ ] Persist scan run/checkpoint state atomically and reject stale run/session/server results.
- [ ] Preserve dropped/pending/acknowledgement/failure semantics and never silently complete with unresolved work.
- [ ] Implement clear, search, filters, sorting, pagination, marked players, export, coordinate jump, and march follow.
- [ ] Implement Auto Scan as a single-owner durable scheduler with server travel and return-to-origin handling.
- [ ] Implement treasure state/claims and scheduled plunder workflows with authoritative outcome evidence.
- [ ] Implement alliance sharing payloads; live message delivery remains separately authorization-gated.
- [ ] Validate representative data for all eight record kinds and repeated full-scan completion.

## P1 — Remaining feature families

- [ ] Build the exact original command/event/config catalog from the verified application.
- [ ] Recover one feature family at a time using the same evidence labels and live-proof gates.
- [ ] Keep unsupported commands fail-closed until their contracts are recovered and implemented.

## Verification

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
./tools/capture_lwbridge_ui.ps1
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
```

See [docs/README.md](docs/README.md) for the durable evidence/documentation map and [task.md](task.md) for the full Overview + Map Data acceptance contract.
