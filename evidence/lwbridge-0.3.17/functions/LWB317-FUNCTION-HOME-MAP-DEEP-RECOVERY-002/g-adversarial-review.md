# Milestone G — headless integration and adversarial review

G ran only command-line/headless checks. All LastWar/LWBridge process inventories
before/after the pass were empty. No `--live-*` entry point was used.

## Broad integration

Canonical Map317 checks were run from the project with Release configuration and
`--no-restore` because no prebuilt Map check DLL was present. Result:
`LWB317_MAP_CHECKS_OK`.

The existing Release Desktop check binary was then run through its default
non-live path. Result: `ok=true` with deterministic profile routing,
persistence, request lifetime, Map persistence/contract, control-pipe contract
and production-UI selection all true. Installed diagnostic was valid and the
reported process state was `gameRunning=false`, `launcherRunning=false`.

Focused inverse reruns all passed:

- `--map317-native-boundary-check`;
- `--map317-dto-matrix-check`;
- `--map317-plunder-worker-boundary-check`;
- `--profile-runtime-owner-check`.

## Preserved negative attempt

The first canonical Map invocation guessed a nonexistent
`tests/LWBridge.Map-0.3.17.Checks/bin/Release/net10.0/...` DLL. It failed before
running tests. The actual project has no prebuilt Release DLL at that path, so G
reran the canonical project with `dotnet run --configuration Release --no-restore`.
This is recorded in `g-checks.txt`.

## Adversarial review

A full path review from starting lead checkpoint
`6bc72c0493ac0dffa11432713840f0de96800647` through the end of F found all
campaign changes under this campaign evidence directory only. No product source,
test source, accepted historical evidence, compatibility/hash gate, UI source or
runtime configuration was changed.

The review specifically re-tested the high-risk interpretations:

- protected per-kind original producer details were not inferred from generic
  0.3.17 host storage;
- v22 field existence was not promoted to positive runtime/population proof;
- `resourceTypeId` was not invented into a public Map query field;
- Dispatch `expiredTime` was not reinterpreted as exact host
  `taskExpireTime` without v22 eligibility evidence;
- lower-level Treasure/Ghost action messages were not promoted into missing
  aggregate/preparation providers;
- the read-only Treasure-state lane was not converted into an action lane;
- R3 runtime ownership, Stop, Auto Launch and Auto Scan contracts were not
  reopened;
- Truck synthetic reward keys were not relabeled as exact original normalization.

## G result

All independent offline branches are exhausted and green except the exact D
protected-provider blockers and the already-known owner-held live witnesses.
There is no demonstrated source-backed product discrepancy to correct in this
campaign, so no product build/package regression requirement was triggered.
