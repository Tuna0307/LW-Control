# First C# implementation

The Windows application is now implemented in `src/LWControl.Desktop`, with an
independent planning library in `src/LWControl.Core`. It targets .NET 10 and uses
Windows Forms. There are no third-party NuGet package dependencies.

## Implemented behavior

- Choose daily-reward categories and a per-run selection limit.
- Save and restore settings under `%LOCALAPPDATA%\LWControlRebuild\settings.json`.
- Import local JSON observations or load clearly labeled synthetic sample data.
- Preview eligible rewards and the reason each other reward was skipped.
- Prefer expiring rewards, optionally followed by daily/weekly task chests.
- Export a preview plan and invalidate it when settings or observations change.
- Reject unknown costs, paid rewards, stale/future observations, expired rewards,
  missing availability, and duplicate source identities.

This is newly written policy. Type responsibilities and some field concepts were
informed by inspected metadata. Original method instructions have not yet been
decompiled into verified behavior, so neither exact default values nor feature
parity is claimed. The daily-claim observation JSON is our own format; it is not
compatible with an original bridge merely because fields have similar names.

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
enum. Free eligibility requires `freeConfirmed: true`, `currencyCost: 0`, a positive
`remainingFreeClaims`, and `claimButtonVisible: true`. Missing evidence prevents
selection. `expiresAt` is optional. Timestamps use ISO 8601 with a timezone offset.

## Verification

The core-check executable contains 13 cases for eligibility, settings, freshness,
priority, deduplication, selection limits, JSON handling, and persistence. The
Windows workflow builds the desktop program, runs those checks, and opens the
form with a bounded smoke check exercising sample planning and settings saving.
Workflow success must be checked before calling the build verified. The smoke
check does not replace manual visual QA or live-game testing.

Local validation used the .NET 10.0.400 SDK and Microsoft Windows desktop
reference pack. The core and desktop projects compiled with zero warnings and
zero errors. All 13 core checks passed. Desktop compilation was performed on
Linux with Windows targeting enabled; the form has not been run or visually
validated locally. The Windows workflow is a separate runtime check.

Windows project configuration follows the
[Microsoft Desktop SDK reference](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop).
