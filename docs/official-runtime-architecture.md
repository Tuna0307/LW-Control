# Official Last War PC runtime architecture

Historical baseline: 2026-09-08. This document records read-only findings from the
then-installed official PC client. It deliberately separates official
client facts from recovered LWBridge behavior.

## Latest reviewed baseline — review 9, 2026-09-10

The user performed data deletion/reinstall; active scripts are version 14 and match the accepted update's metadata/CRC. All 14 sampled core hashes and main build 1078 remain unchanged. Table 39191 matches the failure/recovered snapshots but differs from the older September 8 table. See [the exact comparison and readiness requirements](lastwar-update-readiness.md). Preserve unchanged static binary evidence; revalidate script/table-dependent contracts. Historical `latestLuaState` in the architecture collector may still show version 12; prefer current files plus the later applied-update record for actual active script identity.

## Current observation — 2026-09-10

[LWB-PM8-004 recovery](lastwar-update-incident.md) supersedes the failed-update state for current live readiness. After the seven preserved `14 <- 12` CRC failures, the official launcher ran its cleanup/reinstall path, accepted Lua version 14 and started the ordinary game. All 14 sampled PE/container/protected-runtime anchors still match the pre-recovery build-1078 snapshot, including Assembly-CSharp/xLua; the current table hash is also unchanged from the failure snapshot. Active Lua is now version 14 with SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`. [Recovery comparison](../evidence/official-runtime/2026-09-10-pm8-004-runtime-comparison.json) identifies exactly what stayed stable and changed.

Historical evidence is reproducible with (choose a new dated output for a fresh observation):

```powershell
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
```

The inspector hashes and parses files and summarizes the existing launcher log.
It does not start the game, change installed files, inspect user identifiers, or
alter protected runtime components.

## Observed installed baseline — 2026-09-08 (not gameplay proof)

The official install is `%LOCALAPPDATA%\FunFly\Last War-Survival Game`.
Independent version anchors agree:

- `manifest.json`: app `1.0.361`, build/version code `1078`, launcher `0.1.2`,
  package `com.lastwar.pc`, platform `Windows`.
- `Game\AppVersion.info`: `1.0.361|1078`.
- `Game\LastWar_Data\AppManifest.json`: product `Last War-Survival Game`,
  version `1.0.361`, build target `StandaloneWindows64`.
- `Game\LastWar_Data\StreamingAssets\VERSION.txt`:
  `app_1.0.361=0|1.250.2036048`.

The current manifest inventories 214 packaged game files. It contains 56
`.rdl` files, 75 `.mdl` files, 14 DLLs, four EXEs, the xLua/script payloads,
asset bundles, data-table files, and 11 `AntiCheatExpert` entries.

Current SHA-256 anchors from installed bytes:

| File | SHA-256 |
|---|---|
| `LastWarLauncher.exe` | `b6e29176f64c11a6f203d414fb50ee1e02293be318efda9c33cf221c83d763bc` |
| `LastWarSync.exe` | `18fde4ebe98767de1c769d2bcc474b726ad400a0d2c67178d7ded01aedfda3e9` |
| `Game\LastWar.exe` | `df5abcf8618d48500befa9f587b509ed4f58373ff34932bb87ce217f0cf267d5` |
| `Game\GameAssembly.dll` | `496fbb32195086deaf39221668957d130a96a94aa90f87dd5adaab23bb800279` |
| `Game\LastWarBase.dll` | `10f3e8a462141d0aa6771902dd162c6550c32f12bc5d2ddf5ffc1bd85ca3c0d9` |
| `Game\LastWar_Data\Plugins\x86_64\xlua.dll` | `21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f` |
| `Game\AntiCheatExpert\ACE-Base64.dll` | `e464279f37d8b27c2289ac52d8446c94c5b09a29842490d25d04c749f1387041` |
| `Game\AntiCheatExpert\ACE-Service64.exe` | `dd6c302bada944b5246fb3b81a9e4244f57bceffcb4c309a370886d721da8114` |

The ACE entries are recorded only as protected-runtime integrity anchors. They
are not modified or used as a bypass target.

## LIVE-PROVEN PE architecture

All six inspected launcher/game/runtime binaries are x64 PE files.

- `Game\LastWar.exe` is a thin Unity bootstrap. Its only imported DLLs are
  `LastWarBase.dll`, `UnityPlayer.dll`, and `KERNEL32.dll`. It exports the AMD
  and NVIDIA high-performance-GPU preference flags.
- `Game\GameAssembly.dll` exposes 235 export functions. The export set includes
  the normal IL2CPP API surface and is therefore part of the managed/runtime
  loading boundary rather than the thin `LastWar.exe` bootstrap.
- `Game\LastWarBase.dll` has one ordinal export and no named exports. Its large
  native body is therefore reached through a very small public PE surface.
- `Game\LastWar_Data\Plugins\x86_64\xlua.dll` exposes 254 export functions,
  including xLua packing helpers.
- `LastWarLauncher.exe` and `LastWarSync.exe` are standalone x64 native
  executables with no exported API surface.

This confirms that process-level status must distinguish the thin Unity entry
process from the managed/runtime and xLua layers behind it.

## LIVE-PROVEN packaged RDL/MDL representation

The current gameplay assemblies are not ordinary PE/.NET assemblies on disk.
Four representative RDL files all start with the same `RG` wrapper prefix
`52 47 01 00`:

- `Assembly-CSharp.rdl`
- `BaseUtils.rdl`
- `SmartFox2X.rdl`
- `XLuaRuntime.rdl`

Representative MDL files are also transformed, but they do not share that RDL
header. `UnityEngine.CoreModule.mdl` and `mscorlib.mdl` are neither `MZ` nor
`RG` at byte zero. RDL and MDL must therefore remain separate format families
in recovery tooling.

The current RDL SHA-256 anchors match the previously recovered runtime-loader
work for this same build, so those earlier RG/RGMD findings remain relevant as
historical evidence. The current inspector independently proves only the file
identity and outer headers; deeper metadata decoding remains a separate
recovery layer.

## LIVE-PROVEN install baseline versus active hot-update state

There are two distinct script/data locations that must not be conflated:

1. The install baseline under `Game\LastWar_Data\StreamingAssets`.
2. The active per-user hot-update state under
   `%USERPROFILE%\AppData\LocalLow\FunFly\Last War-Survival Game`.

At this checkpoint the active LocalLow script set is:

- `lwScripts\version.txt`: content version `12`.
- `lwScripts\LWScripts.data`: 42,030,332 bytes, SHA-256
  `bc10cda45528ef82adb1439b0cac5b08265c7a11770430e7484a27a7031c485c`.
- `lwScripts\LWScripts.txt`: `42030332|3591022538`, SHA-256
  `bb2609a3c94feea15bad98f244bd517881d414637f6a2f509d54509cd7f756e8e`.
- Latest active table file: `table_39128_5d6bbf1725c5524bbfe9ae862865d7db.data`,
  19,316,988 bytes, SHA-256
  `8a4da3d8f4015f77188d6600ffe2144562014d3035b4d96d106489588618e18d`.

The packaged manifest still lists `StreamingAssets\lwScripts\LWScripts.bz2`
at 41,176,784 bytes. The launcher log independently reports the active Lua
state as version 12, file version 3, format `Lenc`, size 42,030,332. The
packaged archive is therefore a baseline artifact, not sufficient evidence for
the active runtime script body.

## LIVE-PROVEN official launcher lifecycle from existing log

The current `Launcher.log` contains 291 historical `Starting game at ...\Game\LastWar.exe`
records. In 146 records the current logging format contains the complete ordered
preflight sequence before game start:

1. compare local and remote manifest versions;
2. resolve/update the data table;
3. verify the current Lua hot-update state;
4. start asset-pack synchronization;
5. complete pack-index hash verification;
6. start `Game\LastWar.exe`.

The same log contains 36 `Prepared game relaunch` records. The latest observed
one reports `ExitedGracefully` before the subsequent launcher preflight and
game start. This proves the official launcher has an explicit relaunch path and
does not reduce to an unconditional `Start-Process LastWar.exe` operation.

The log does **not** currently prove the exact game command line, environment,
working directory, process-creation flags, registry/single-instance contract,
or protected-runtime initialization order. Those remain **UNKNOWN/BLOCKED**.

## RECOVERED launcher-manifest naming mismatch

`manifest.json` describes its launcher artifact as `Launcher.exe`, size
24,022,928 bytes. The installed top-level executable is instead
`LastWarLauncher.exe`, also 24,022,928 bytes; no top-level `Launcher.exe` exists.
The manifest's 32-hex `hash` value also does not equal the installed
`LastWarLauncher.exe` MD5. The exact meaning/target of that launcher metadata
field is therefore **UNKNOWN** and must not be used as an integrity verdict for
the installed launcher without recovering the updater's interpretation.

## Consequences for the LWBridge rebuild

- `game_root_status` can safely validate the current version/architecture and
  required binary locations with read-only checks.
- A running `LastWar.exe` proves only that the Unity process exists. It does not
  prove xLua, LWBridge session identity, bridge heartbeat, or Map Scan readiness.
- The reconstructed Overview launch path must account for the official
  launcher's manifest/table/Lua/pack reconciliation instead of assuming the
  packaged install tree is already the active runtime state.
- Any cache or evidence keyed only to `StreamingAssets\LWScripts.bz2` can become
  stale while the current active LocalLow Lua body has changed.
- Current official launcher lifecycle evidence and recovered LWBridge bootstrap
  evidence are separate layers. Neither layer by itself proves a working
  bridge-connected game session.

## UNKNOWN/BLOCKED next recovery items

- Exact official game command line, environment, working directory, and
  process-parenting contract.
- Exact official registry/single-instance behavior.
- Meaning of the manifest launcher `path/hash` fields relative to the installed
  `LastWarLauncher.exe`.
- Exact boundary between official launcher startup and protected-runtime
  initialization. Protected runtime is not modified during this recovery.
- A safe, independently implemented bridge-ready lifecycle remains blocked on
  the unresolved LWBridge launch-envelope/session proof contract.
