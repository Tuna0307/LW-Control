# Interrupted live pilot: lead checkpoint, 2026-10-08

Disposition: **COHERENT PARTIAL CHECKPOINT / BACKGROUND-ONLY CONTINUATION**.
Product checkpoint reviewed: `b1f75a25a8ede943fa0503265b0a946f90f68a20`.
This is not full pilot acceptance or promotion of Home/Map to LIVE_PROVEN.

## Saved worker progress

Preserved all seven previously untracked `attempt-5-fresh-ready` files verbatim.
Scoped binary Git attributes retain their original BOM/CRLF bytes and recorded
hashes; the immutable captures are reviewed through the JSON/text readers rather
than line-normalized into different artifacts.
They associate game PID 66036 with profile `lwb317-live-pilot-001` and session
`1ba5a00e5c8646dd81341f7beb736d24`; later heartbeat says connected/ready on server
2212. The pipe transport record independently reports `adapterReadMode=direct`,
`adapterActive=true`, `state=hello_sent`, `clientConnected=false`. These captures
are historical, not a current connection witness or proof of completed host pipe
handshake. Do not infer handshake completion from the heartbeat alone.

The saved pre-scan database inventory contains an older completed City run with
7,000 rows and a cancelled Resource run with zero completed blocks and zero rows.
The City record predates attempt 5. It must not be relabeled as acquisition during
attempt 5 or as independently observed original-equivalent coverage. Earlier local
City pagination/export proof remains useful. Fresh same-session Resource, bounded
Stop and full pilot UI/persistence/restoration sequence remain incomplete.

## Completed recovery checkpoint

Fresh lead process inventory found no LastWar/LWBridge process. Installed scripts
still matched the attempt-5 candidate and its active recovery journal. Verified
the exact backup triplet against original hashes, then executed:

```powershell
$env:LWBRIDGE_REBUILD_DATA_ROOT='C:\Users\chimw\AppData\Local\Temp\LWB317-LIVE-PILOT-001-root'
& 'C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe' tools/recover_overview_pending_current.py --game-root 'C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game'
```

The existing production helper returned `ok=true`, `recovered.restored=true`,
`compatibilityOk=true`. A separate read-only check confirms all installed and
backup hashes match the exact originals, manifest stage `restored`, no pending
recovery journal and no game/clone processes. Backups, isolated profile/DB, earlier
failed attempts and owner data were retained. No desktop capture/input or game
launch was performed by this lead checkpoint.

## Review and checks

Inspected the 980eabf/b1f75a product/test diff. Direct delegate invocation preserves
the result-string parsing boundary; reflection is a marshalling compatibility path,
not product-success substitution. Saved actual-game evidence identifies the direct
path. Reflection-path equivalence is not newly live-proven. Abandoned metadata
cleanup is limited to four runtime files after no-game/no-fresh-lease checks and
uses file-handle deletion inside the serialized helper path; cancellation remains.

Fresh file ownership suite passes 5/5; production Lua lease suite passes 6/6 under
lupa 2.8/Lua 5.4. Read-only current-client runtime structural check passes, including
binary admission and current v22 anchors. First structural invocation used the Lua
environment without dncil; its failure is retained as `initial-environment-failure.json`.
Rerun used the existing system Python containing dncil, with no install or gate change.
See `lead-checkpoint-2026-10-08/checkpoint-results.json` for exact commands, outputs,
source checkpoint, process inventory, original/backup hashes and attempt-5 file hashes.

No production source changed in this checkpoint; no fresh frontend/Release build
was warranted by these evidence/policy changes. This bounded review does not accept
all isolation/native integration changes or replace the remaining live tests.

## Continuation

The owner's newest direction is `2026-10-08-BACKGROUND-ONLY-PILOT.md`. Shared-desktop
capture/input/focus is now restricted; background Last War remains permitted.
Next solo medium task: `LWB317-FUNCTION-HOME-MAP-BACKGROUND-CONTINUATION-001`.
Diagnose Resource request/transport retirement headlessly and close only demonstrated
in-scope defects. Actual UI-dependent live witnesses await desktop resumption.
