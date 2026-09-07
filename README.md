# LW-Control

Independent Last War controller research, a C# Windows planning application,
and the earlier Python offline prototype.

## Run the C# Windows application

Requires the .NET 10 SDK on Windows.

```powershell
dotnet run --project src/LWControl.Desktop/LWControl.Desktop.csproj
dotnet run --project tests/LWControl.Core.Checks/LWControl.Core.Checks.csproj
```

The desktop app saves settings, imports observations, previews daily-claim
decisions, exports plans, and can inspect the existing bridge read-only. The
**Daily Task Claim** path now also has a clean-room current-game runtime and a
bilingual **Claim daily tasks / 领取每日任务奖励** action. Other reward categories
remain preview-only.
The desktop also exposes **World Scan / 世界扫描**. Its 100x100/65-batch direct
block transport is live-proven. The rebuilt monster phase follows the recovered
500-view camera path and the current-build explicit march-message contract; the
importer refuses to show a completed scan unless that march path, monster capture,
and cleanup are also proven by the live result. The current checkpoint has passed
static validation but still requires that final guarded live monster proof.
Current generated `WorldPointInfo` payloads are preferred over historical field
aliases; unsupported optional fields stay unknown. See
[World Scan rich-record recovery](docs/current-world-map-record-recovery.md).
See [implementation details](docs/csharp-implementation.md).
The daily-claim preview policy is now based on statically recovered behavior from
the supplied executable; see [Daily Free Claims recovery notes](docs/daily-free-claims-recovery.md).
The current game's `BaseUtils.rdl` loader metadata can also be inspected read-only;
see [BaseUtils.rdl loader recovery](docs/baseutils-rdl-recovery.md).
Native `GameAssembly.dll` tracing is documented in
[GameAssembly RGMD runtime recovery](docs/gameassembly-rgmd-runtime.md).
Closed-game loader experiments also identified the current `LENC` encrypted Lua
entry format. The rejected probe path and its fail-closed status are documented in
[Version-aware loader probe research](docs/loader-probe-installation.md). A
read-only native decoder now reproduces the installed LWLF-v3 loader, including
the xLua-derived key/nonce, its eight-round no-feed-forward ChaCha-family transform,
zlib inflate, and the resulting Lua bytecode. See
[Assembly-CSharp / xLua LENC runtime recovery](docs/assembly-csharp-lwlua-lenc-runtime.md).
Native RG-loader analysis has also recovered the full 32-bit metadata-token
transform. The symmetric LENC encoder now builds and re-verifies a separate
encrypted candidate package without changing installed files. A separate
`--verify-dir` path now proves that candidate satisfies the same encrypted-payload,
official-entry, metadata, and BaseUtils preflight contract expected after
installation. A bounded current-game launch on 2026-09-06 loaded that encrypted
candidate and produced a fresh loader heartbeat; the game was then closed and the
original package/BaseUtils were restored with exact hash equality. The
current daily-task Lua protocol is
also recovered down to wire command names, request fields, and response-state
handlers. The current task-point and five-box goal-state algorithm is now mirrored
symbolically in the C# core without assuming the game's numeric `TaskState` enum;
see the current-game section of the Daily Free Claims notes. A strict version-1
read-only snapshot model now defines the Lua-to-C# state boundary and
re-derives current points and all five chest states before accepting a capture; see
[Current daily-task read-only snapshot contract](docs/current-daily-task-snapshot.md).
That snapshot payload has now also executed successfully in the current game. One
bounded `DailyQuestLs` refresh populated 23 task records and five box thresholds,
the Lua probe emitted a valid 240-point snapshot, and the same JSON passed the C#
validator before the exact original game files were restored. See
[Current daily-task live capture](docs/current-daily-task-live-capture.md). The
repeatable guarded runner is `tools/run_daily_task_snapshot_probe.py`; it permits
at most one list refresh and contains no reward-claim action path.

The bounded claim contract is also live-proven for both explicit target types. A
task-101 request produced a later authoritative `CanReceive -> Received`
transition, and one explicit `DailyQuestReward(1)` produced a later stage-1
`Received` state. The direct chest response had an empty `stageArr`, proving that
fresh authoritative list state, rather than response echo data, must verify the
effect. See
[Current daily-task bounded claim proof contract](docs/current-daily-task-claim-proof.md).

The persistent Daily Task runtime uses the current game's proven
`UpdateManager.AddUpdate` scheduler and a single-use local `run_once` command. It
refreshes `DailyQuestLs`, selects only an explicit symbolic `CanReceive` target,
sends that exact task/stage claim, refreshes state, and accepts the claim only when
the same target is `Received`. It re-detects from fresh state before every next
target and never uses the special unclassified stage `-1` path. The current
content-version-12 runtime is installed with an exact reversible backup. To inspect
or rebuild it:

```powershell
python tools/install_daily_task_runtime.py --status --json
python tools/prepare_daily_task_runtime.py --prepare-dir .codex-live/daily-runtime-new --json
python tools/install_daily_task_runtime.py --install .codex-live/daily-runtime-new --json
dotnet run --project src/LWControl.DailyTaskCli/LWControl.DailyTaskCli.csproj -- inspect
```

Live runtime validation on 2026-09-06 proved recurring registration through
`UpdateManager.AddUpdate`, then exercised the same C# client used by the desktop.
With the fresh account state containing no `CanReceive` target, `run_once` issued
one read-only list refresh and returned `no_eligible_target` with **0 reward
sends**. Uninstall restored every official protected hash exactly, and reinstall
reproduced the verified runtime hashes.

## Run the earlier Python prototype

Requires Python 3.10 or newer; no third-party dependencies.

```sh
python lwcontrol.py --scenario examples/demo.json
python -m unittest discover -s tests -v
```

The example uses invented daily-claim and resource-batch state. It prints JSON
activity events while demonstrating settings, cooldowns, health checks,
request/result correlation, effect checks, and stop/resume behavior.

This earlier Python prototype is still only a mock. The current Daily Task live
implementation is in the C#/Lua runtime paths described above.

## Contents

- `lwcontrol.py`: independent scheduler, mock adapter, and scenario CLI.
- `examples/demo.json`: synthetic scenario, not a game protocol.
- `tests/test_controller.py`: controller decisions and failure-handling tests.

All source code here is newly written. The Python prototype is synchronous and uses
process-local state. The C# application has a Windows Forms interface. Daily Task
Claim now has bounded live I/O, persistent command IDs, authoritative result
verification, and Windows live testing; the remaining reward categories do not
yet have live adapters.

## Reconstruction direction

C# is the chosen language for the planned Windows application because LWControl
retains named .NET assemblies and managed method bodies. The current Python code
remains an offline prototype. The first C# desktop shell and independent
daily-claim planning library are now in `src/`.

The first feature selected for detailed study is daily free claims. Its bundle,
managed feature logic, embedded game runtime, and local file-command transport have
now been statically recovered far enough to replace the first synthetic planner
rules with a clean-room runtime-policy preview.

## Research

- [Initial architecture assessment](docs/architecture.md)
- [Artifact hashes and marker evidence](docs/evidence.json)
- [Managed-code recovery assessment](docs/recovery-assessment.md)
- [Daily Free Claims runtime and bridge recovery](docs/daily-free-claims-recovery.md)
- [Daily Free Claims recovery evidence](docs/daily-free-claims-evidence.json)
- [Current daily-task read-only snapshot contract](docs/current-daily-task-snapshot.md)
- [Current daily-task snapshot JSON schema](docs/current-daily-task-snapshot.schema.json)
- [BaseUtils.rdl loader recovery](docs/baseutils-rdl-recovery.md)
- [BaseUtils.rdl recovery evidence](docs/baseutils-rdl-evidence.json)
- [GameAssembly RGMD runtime recovery](docs/gameassembly-rgmd-runtime.md)
- [Assembly-CSharp / xLua LENC runtime recovery](docs/assembly-csharp-lwlua-lenc-runtime.md)
- [Version-aware loader probe research](docs/loader-probe-installation.md)
