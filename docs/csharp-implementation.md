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

The application sends no game actions and has no live adapter, injection,
authentication service connection, background task runner, or automatic claim
execution. It is an application implementation milestone, not a working game bot.
Imported observations are untrusted preview data, not verified server evidence.

## Build and run on Windows

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet build src/LWControl.Desktop/LWControl.Desktop.csproj --configuration Release
dotnet run --project src/LWControl.Desktop/LWControl.Desktop.csproj
dotnet run --project tests/LWControl.Core.Checks/LWControl.Core.Checks.csproj --configuration Release
```

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

Local validation on Windows used the .NET 10.0.400 SDK. The core and desktop
projects compiled with zero warnings and zero errors. All 15 core checks passed,
including the recovered bridge-heartbeat contract. The bounded Windows Forms smoke
test also completed successfully and exercised sample planning plus settings
persistence. It is still not a substitute for manual visual QA or a live game
command test.

Windows project configuration follows the
[Microsoft Desktop SDK reference](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop).
