# First C# implementation

The Windows application is now implemented in `src/LWControl.Desktop`, with an
independent planning library in `src/LWControl.Core`. It targets .NET 10 and uses
Windows Forms. There are no third-party NuGet package dependencies.

## Implemented behavior

- Choose daily-reward categories and a per-run selection limit.
- Save and restore settings under `%LOCALAPPDATA%\LWControlRebuild\settings.json`.
- Import local JSON observations or load clearly labeled synthetic sample data.
- Preview eligible rewards and the reason each other reward was skipped.
- Follow the recovered runtime's task-chest and fixed-adapter priority ordering.
- Export a preview plan and invalidate it when settings or observations change.
- Inspect the existing LastWarControl bridge read-only without queueing a command.
- Inspect and command the clean-room Daily Task runtime through a fresh-heartbeat
  gated, single-use local command contract.
- Run **Claim daily tasks / 领取每日任务奖励** from the Windows UI when the current
  Daily Task runtime is installed and loaded by Last War.
- Reject unknown costs, paid rewards, stale/future observations, expired rewards,
  unconfirmed source state, and duplicate source identities.

The planner has now been revised using static recovery of the supplied
`LastWarControl.Core.dll` and embedded `LWC2DailyFreeClaims` runtime. The recovered
runtime defaults to a maximum of 20 claims, all seven Daily Free Claims categories
enabled behind a disabled master switch, fixed adapter priorities, and a strict
zero-cost/free-only gate. See [Daily Free Claims recovery notes](daily-free-claims-recovery.md)
for the recovered contract and the managed-core/runtime discrepancy around expiry
priority.

The code in this repository remains a clean-room implementation. Daily-claim
defaults, adapter priorities, free-condition rules, and command/result correlation
were recovered from the supplied managed assemblies and embedded game module, but
the daily-claim observation JSON remains our own import format. It is not the
original bridge schema and must not be sent to the game.

The bridge inspector is also read-only. It reads the existing Last War process,
heartbeat timestamp/version, and pending queue occupancy. It does not create a
pending command, install scripts, patch `BaseUtils.rdl`, or restart Last War.
The current `BaseUtils.rdl` metadata and `CommonUtils.IsDebug` location have also
been recovered independently with `tools/inspect_baseutils_rdl.py`; see
[BaseUtils.rdl loader recovery](baseutils-rdl-recovery.md).

Daily Task Claim is now the first live adapter. Its C# client writes one atomic
`run_once` command only when the runtime heartbeat is fresh, waits for a correlated
result, validates all counters/target identities, and validates the final
authoritative `CurrentDailyTaskSnapshot`. A target reported as claimed must still
be `Received` in that final snapshot. The other six Daily Free Claims categories
remain preview-only. Imported observation files remain untrusted preview data and
are never used as live Daily Task claim evidence.

The game-side Daily Task runtime is newly written Lua packaged into the current
encrypted LWLF-v3 script container. It preserves the official `LuaEntry`, registers
through the current-build-proven `UpdateManager.AddUpdate`, and uses only recovered
current commands/handlers: `DailyQuestLs`, `DailyTaskReward`,
`DailyQuestReward`, the three manager handlers, and optional `PushDailyQuest`
observation. It never uses `DailyQuestReward(-1)`.

## Build and run on Windows

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet build src/LWControl.Desktop/LWControl.Desktop.csproj --configuration Release
dotnet run --project src/LWControl.Desktop/LWControl.Desktop.csproj
dotnet run --project tests/LWControl.Core.Checks/LWControl.Core.Checks.csproj --configuration Release
dotnet run --project src/LWControl.DailyTaskCli/LWControl.DailyTaskCli.csproj -- inspect
```

The runtime installer is separate from the UI so it can fail closed while Last War
is closed and keep an exact rollback record:

```powershell
python tools/prepare_daily_task_runtime.py --prepare-dir .codex-live/daily-runtime-new --json
python tools/install_daily_task_runtime.py --install .codex-live/daily-runtime-new --json
python tools/install_daily_task_runtime.py --status --json
```

After installation, start Last War normally. Once the city/client is loaded, open
the desktop app, enable daily claims and the Daily Task Chest category, then click
**Claim daily tasks**. The Simplified Chinese UI exposes the same action as
**领取每日任务奖励**; protocol matching is language-independent.

The application starts with planning disabled. To inspect the screen's behavior,
click **Load sample**, enable planning, select **VipDailyReward**, then click
**Build plan**. This should select the free sample and skip the unknown-cost one.
Sample observations become stale after 60 seconds; reload them when needed.

Observation files contain a JSON array. Required fields are `sourceKey`,
`rewardName`, `kind`, and `capturedAt`. The category names match the `ClaimKind`
enum. Runtime-policy eligibility requires `status: "Claimable"`,
`currencyCost: 0`, and `hasCurrencyCost: false`. A positive
`remainingFreeClaims` is sufficient free evidence; otherwise `freeConfirmed: true`
must be paired with `claimButtonSemantic: "free"` or `"claim"`. `expiresAt` is
optional. Timestamps use ISO 8601 with a timezone offset. Imported observations are
still subject to local freshness, identity, duplicate, and expiry checks.

## Verification

The core-check executable covers eligibility, recovered defaults and priorities,
runtime ordering, settings, freshness, deduplication, selection limits, JSON
handling, and persistence. The
Windows workflow builds the desktop program, runs those checks, and opens the
form with a bounded smoke check exercising sample planning and settings saving.
Workflow success must be checked before calling the build verified. The smoke
check does not replace manual visual QA or live-game testing.

Local validation on Windows used the .NET 10.0.400 SDK. The core, desktop, and
Daily Task CLI projects compiled with zero warnings and zero errors. The
2026-09-06 completion rerun passed **23/23** focused Daily Task, startup,
installer, and LENC Python tests, and the bounded Windows Forms smoke test also
passes. Eight consecutive claim-free cold starts also produced a fresh
`UpdateManager.AddUpdate` heartbeat without changing the installed game hashes.

Live validation went further: runtime candidate `a42` (content version 12,
SHA-256 `afc145b9614cc81697e7079723a688ee26ccd9c6a3aa569ecf31e341bc60c8f6`)
registered through `UpdateManager.AddUpdate`. The C# client first completed a real
zero-target `run_once` with no reward send and a validated final snapshot. After a
later naturally claimable state appeared, the persistently installed runtime
completed two independent `maximumClaims=1` positive runs: activity chest stage
`2` and daily task `105`. Each command sent exactly one reward request, performed
two list refreshes, reported one confirmed claim, and returned a fresh final
snapshot proving the exact target `Received`. The task run also raised
`currentPoint` from `80` to `90`. A full uninstall restored the exact official
hashes and reinstall reproduced the exact runtime hashes.

Windows project configuration follows the
[Microsoft Desktop SDK reference](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop).
