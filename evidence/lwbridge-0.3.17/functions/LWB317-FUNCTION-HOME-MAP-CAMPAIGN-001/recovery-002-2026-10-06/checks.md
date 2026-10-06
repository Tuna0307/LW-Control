# RECOVERY-002 checks actually executed

All commands below were run on 2026-10-07 from the authorized LW-Control checkout.
No `--live-*` runner was invoked. Before native execution, the process inventory
showed no running LastWar or LWBridge process, and lifecycle cleanup/root isolation
had already been verified in source.

## Frontend and package

- `npm.cmd --prefix src\LWBridge.UI-0.3.17 run check` — PASS.
- `npm.cmd --prefix src\LWBridge.UI-0.3.17 run build` — PASS.
- `npm.cmd --prefix src\LWBridge.UI-0.3.17 run check:production-build` — PASS.
- `dotnet build src\LWBridge.Desktop\LWBridge.Desktop.csproj -c Release --no-restore --nologo`
  — PASS, 0 warnings / 0 errors, canonical UI included.
- Fresh EN/light `--home-map-campaign-proof` — exit 0.
- Fresh JA/dark `--home-map-campaign-narrow --home-map-campaign-proof` — exit 0.
- Both settled PNGs were decoded/visually inspected after generation.

## Focused isolated Desktop checks

The check project was compiled with the documented production-UI skip only for the
source/focused check binary:
`dotnet build tests\LWBridge.Desktop.Checks\LWBridge.Desktop.Checks.csproj -c Release --no-restore -p:SkipCanonicalProductionUiBuild=true --nologo`
— PASS, 0 warnings / 0 errors.

Executed and passed:

- `--profile-runtime-owner-check`
- `--home-campaign-lifecycle-check`
- `--map-auto-scan-campaign-check`
- `--map317-native-boundary-check`
- `--map317-plunder-worker-boundary-check`
- `--map317-dto-matrix-check`
- `--map-campaign-canonical-check`
- `--map317-restart-check`
- `--game-root-select-check`
- `--overview-bridge-lifecycle-launch-binding-check`
- `--overview-bridge-host-transport-check`
- `--overview-bridge-normal-composition-check`

The first six were run as one isolated chain after the cleanup safety gate; the
second regression chain also exited 0.

## Canonical Map checks

`dotnet run --project tests\LWBridge.Map-0.3.17.Checks\LWBridge.Map-0.3.17.Checks.csproj -c Release --no-restore --nologo`
— PASS (`LWB317_MAP_CHECKS_OK`).

## Integrity checks

- `git diff --check` — PASS (line-ending notices only, no whitespace error).
- Reference executable SHA-256 — matches
  `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`.
- Recovered Map panel SHA-256 — matches
  `ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`.
- DTO fixture SHA-256 — matches
  `700e465605275122a66888435a1814f496245bc3f84221f19d8325579a17b314`.

The lead's historical JS counterexample result files were intentionally not rewritten.
The corrected inverse behavior is exercised by the current frontend integration
checks and the schema-v4 packaged proof.
