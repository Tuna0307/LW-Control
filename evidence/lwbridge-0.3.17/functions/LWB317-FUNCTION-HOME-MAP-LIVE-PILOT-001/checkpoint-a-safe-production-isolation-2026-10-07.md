# LWB317 live pilot — checkpoint A: safe production isolation

Date: 2026-10-07
Branch: `research/offline-controller`
Starting HEAD: `c61944b5414c97125cecc2766e9cb4d51fead1ad`

## Result

Checkpoint A is ready for commit. Normal launch still defaults to
`%LOCALAPPDATA%\LWBridgeRebuild`. A new normal-production-only
`--isolated-root <absolute-path>` option routes the production composition under
one explicit root without changing the existing proof/replay modes.

The routed production paths are controller config/backup/lock, profile registry,
per-profile config/profile database/runtime, Map317 database, Overview runtime,
Overview evidence, Overview backup, live-resource probe runtime, locale cache and
WebView2 user-data. The real `CurrentClientMapBlockSource` now receives the
selected lifecycle runtime roots instead of independently falling back to owner
LocalAppData.

Explicit isolated roots fail closed if they are the normal root, are inside it, or
contain it. Existing no-option launch behavior remains the normal LocalAppData
root.

## Deterministic production-composition proof

`--production-root-isolation-check` exercised a disposable production root with
the real current-client Map provider composition and profile sequence
`live-pilot-isolation-A -> live-pilot-isolation-B -> live-pilot-isolation-A`.
The check proved isolated global config/registry, profile databases, Map
databases, Overview runtime/evidence/backups, live-resource runtime, profile
asset cache and WebView path resolution, then deleted the disposable test root.
It deliberately started neither bridge transport nor the Auto scheduler.

## Owner-root non-interference witness

Before the real-host isolated-root smoke, the normal owner root
`C:\Users\chimw\AppData\Local\LWBridgeRebuild` had:

- 10,018 files
- 42,958,415,731 bytes
- metadata SHA-256
  `cfe79dee154505b21387e27a6f40083c939f3e51b99e3f037ce022c39e6b4946`

After the isolated-root smoke the same file count, byte count, root last-write
time, top-level config/controller metadata and metadata SHA-256 were identical.
See `checkpoint-a-owner-root-before.json` and
`checkpoint-a-owner-root-after.json`.

The isolated pilot root
`C:\Users\chimw\AppData\Local\Temp\LWB317-LIVE-PILOT-001-root`
received `config.json`, `controller.db`, per-profile `profile.db`, Map
storage and `Presentation\EBWebView`. No LastWar process was started by this
checkpoint.

## Executed checks

- `dotnet build src\LWBridge.Desktop\LWBridge.Desktop.csproj -c Release`:
  PASS, 0 warnings / 0 errors. Canonical production UI build/package identity:
  source `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685`,
  artifact `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`.
- Release `--production-root-isolation-check`: PASS.
- Full Release `LWBridge.Desktop.Checks`: PASS; installed diagnostic valid and
  no game/launcher process observed.
- `python tools\check_current_client_compat.py`: PASS for installed content 22.
- `python tools\check_current_client_runtime_contract.py`: PASS.
- `python tests\home_runtime_file_ownership_checks.py`: PASS, 4/4.
- `python tests\home_runtime_lease_lua_checks.py`: not executed because the
  default Python lacks optional module `lupa` (`ModuleNotFoundError`). No
  package was installed or substituted.
- canonical frontend `npm ... run check`, `build` and
  `check:production-build`: PASS with the hashes above.
- Windows-MCP schema discovery: 21 tools; `ControlStatus` returned
  `state=ready`, generation 1, wait_seconds 0 before desktop use.

## Preserved failed attempts / issues

1. A CRLF-preservation helper opened `CurrentClientMapBlockSource.cs` before
   rejecting an invalid newline argument and truncated that file. The repository
   was clean before this work and the file had no unrelated changes; it was
   recovered byte-for-source from the recorded HEAD and only the intended
   isolation patch was reapplied. The subsequent build and full native checks
   passed.
2. The first owner-root manifest attempt used a .NET API unavailable in Windows
   PowerShell and emitted errors. It was replaced with a compatible implementation;
   the authoritative before/after witness is the identical digest above.
3. Two short isolated-host smoke launches created the expected isolated production
   storage but did not remain open long enough for sustained UI control. No
   LastWar process was launched and no matching Windows Application crash record
   was found. Sustained process/session ownership is therefore explicitly carried
   into checkpoint B rather than being claimed here.

No protected-original service access, authentication automation, updater action,
gameplay action, cross-server movement, recurring Auto scan, spending, claim,
share, march, combat or plunder occurred in checkpoint A.
