> Archived research record. The owner has clarified that Home comparison means game-connection methods and efficiency. Read CONNECTION_COMPARISON.md and the independent lead corrections before using the recommendations. Local user-path prefixes have been generalized.

# LastWarPlugin Home investigation — 2026-10-23

## Verdict

**Result: PARTIAL for LastWarPlugin's actual post-launch Home, sufficient for bounded Home improvement decisions.**

The supplied `LastWarPlugin.exe` contains a byte-exact login page and a byte-exact **Launch Center** account/profile launcher. Those are recovered and can be analyzed confidently. The Launch Center is **not** the actual post-launch Home dashboard: its own code describes `ui.html` as a per-window WebView menu mode, and the production executable separately downloads encrypted `core.py.enc` and `ui.html.enc` after authentication. Neither encrypted payload is present in the supplied folder or current `%LOCALAPPDATA%\LwTool` installation, and the owner has never authenticated to LastWarPlugin.

The investigation did recover the production child bootstrap. At EXE file offset `0x21AFD3`, 456 bytes are XOR-decoded with `0x5A` and written to the spawned Python child (`WriteFileEx @ 0x1400213A6`). The decoded bootstrap proves that production `core.py.enc` is read from `LWAPPENC`, deleted from disk, and decrypted with AES-GCM using a **32-byte key read from the named mapping `Local\LwTool_MasterKey`**; the first 12 encrypted bytes are the AES-GCM nonce. The result is optionally zlib-decompressed and executed as `core.py`. Recovered bytes are retained as `artifacts/lastwarplugin-home-20261023/bootstrap-0x21afd3-xor5a.bin`, SHA-256 `6372938bcbc80b500816d10ac164b2f6ebf56aca9521065a32441dd27d0b00ee`.

That closes the native-wrapper uncertainty but does **not** recover the Home implementation: the post-auth `core.py` and encrypted UI remain the exact missing layer. There is therefore no evidence basis to replace LW-Control's lifecycle, recovery, profile isolation, or status ownership with LastWarPlugin's unknown backend methods.

The accessible competitor launcher does contain several useful UI/contract ideas. The strongest immediate improvement for LW-Control is to display the already-stored `LastLaunchedAt` value. A second useful direction is an all-profile native status event feed so sidebar transient states update immediately instead of relying on the existing 3-second poll. Window-index/multi-open UI should stay deferred until LW-Control actually supports simultaneous games; exposing those controls now would advertise a capability the released one-game owner policy intentionally does not provide.

## Evidence boundary

Evidence classes used below:

- **SHIPPED-ARTIFACT** — byte/string/machine-code evidence from the supplied EXE or byte-exact HTML/JS recovered from it.
- **SOURCE** — current LW-Control source code at `main` / `45e151c8`.
- **CONTROLLED-RUNTIME** — passive observation of a task-owned LastWarPlugin process; no login, input, or game action.
- **INFERENCE** — conclusion suggested by evidence but not established by the unavailable decrypted payload.

The recovered launcher files are byte-exact extractions, not reconstructed designs:

| Artifact | EXE file range | Evidence |
|---|---:|---|
| Login page | `0x20DE90..0x21387B` | `artifacts/lastwarplugin-home-20261023/manifest.json:6-11` |
| Launch Center | `0x213BC1..0x21AE55` | `artifacts/lastwarplugin-home-20261023/manifest.json:13-21` |

`embedded-02.html:123-140` identifies the recovered page as the profile/game launcher. `embedded-02.html:211-241` implements its menu-mode, refresh and launch IPC, and line 211 explicitly describes `ui.html` as the **per-window WebView** alternative to the in-game overlay. This is the key reason the Launch Center must not be presented as recovered post-launch Home.

## LastWarPlugin production loader findings

### Authenticated payload path — confirmed shipped-artifact evidence

`artifacts/lastwarplugin-static-20261023/inspection.json:48-100` records:

- `lwprotect-v1` — file offset `0x1B5238`.
- `Local\LwTool_MasterKey` — `0x1B52A8`.
- `core.py.enc` — `0x1B52FA`, plus copies at `0x1B5530` and `0x1B555C`.
- `LWAPPENC` — `0x1B531A`.
- `LWTOOL_PROD` followed by value `1` — string table begins `0x1B5322`.
- `LWUIEP` — `0x1B5348`; `/api/ui_zara` begins `0x1B534E`.
- `/api/download/core` — `0x1B58E8`.
- `ui.html.enc` — `0x1B58FA`.

The existing static xrefs in `inspection.json:103-204` place the master-key/core bootstrap work in `0x140020538..0x140021583`, including `CreateFileMappingW @ 0x140020A7F` and `MapViewOfFile @ 0x140020AA8`. The key length is checked against `0x20` at `0x140020A09`.

The production download table at `0x140025E14..0x140025E67` pairs:

- `/api/download/core` (`0x12` bytes) -> `core.py.enc` (`0x0B` bytes).
- `/api/ui_zara` (`0x0C` bytes) -> `ui.html.enc` (`0x0B` bytes).

The login-response string table around file offset `0x1B2448` includes `token`, `plan`, `expires_at`, `detail`, `_dk` (`0x1B2461`) and `_dk_nonce` (`0x1B2464`). `_dk` has code references at `0x14002244A` and `0x140027597`; `_dk_nonce` at `0x1400224D2` and `0x1400275A8`. The executable also contains the explicit error `login resp missing dk fields` (`inspection.json:93-95`). No credential or value from these fields was collected.

### Production Python bootstrap — confirmed shipped-artifact evidence

Machine code at `0x140021306` points into EXE file offset `0x21AFD3`; the loop `0x14002130D..0x140021323` copies `0x1C8` (456) bytes while XORing each byte with `0x5A`, then `WriteFileEx @ 0x1400213A6` writes the decoded bytes to the child process.

Offline decoding produced this behavior:

1. Open named mapping `Local\LwTool_MasterKey` and read exactly 32 bytes.
2. Read the path from environment variable `LWAPPENC`.
3. Read the encrypted core, then attempt to delete that encrypted file.
4. `AESGCM(key).decrypt(blob[:12], blob[12:], None)`.
5. Attempt `zlib.decompress` on the plaintext.
6. Compile/execute it as `core.py`, exposing the same key as `_LWBOOTKEY` to that module.

This production bootstrap is stronger evidence than the readable `runtime\Lib\site-packages\lwprotect` examples. The installed `lwprotect` package remains useful corroboration, but it uses its own `LWP\x01`/`LWPATH\x00\x01` formats and does not contain production strings such as `LWAPPENC`, `LWTOOL_PROD`, `LwTool_MasterKey`, or `ui.html.enc`; production cipher/container parity must not be inferred from that package alone.

### Exact remaining blocker

Recursive search found no `core.py.enc` or `ui.html.enc` in:

- `%LOCALAPPDATA%\LwTool`
- `%APPDATA%\LwTool`
- `%LOCALAPPDATA%\Temp`
- the supplied `LastWarPlugin` download folder

`lwps.set` is a 4,242-byte `LWSEv1`/Windows-DPAPI settings container, not an encrypted Home payload. Its values were kept redacted. No core/UI/session-key/nonce material was found in it.

Recovering the actual post-launch Home now requires one of the following owner-authorized artifacts from a **normal authenticated owner launch**:

1. transient `core.py.enc` and `ui.html.enc` bytes (initially only size/hash/magic are needed), or
2. a read-only Python-child memory snapshot after decryption.

Without that artifact/access, post-launch Home UI content, handlers, reconnect implementation, sandbox implementation, and failure paths remain unknown.

## Recovered Launch Center behavior

The following are confirmed renderer contracts or behavior, **not proof of the encrypted backend implementation**.

| Feature | LastWarPlugin accessible implementation | Evidence |
|---|---|---|
| Profile cards | Renders profile name/description/status and `Last used`; first real profile is selected by default. | `embedded-02.html:253-295`; extracted JS `scripts/embedded-02-inline-01.js:112-154` |
| Last-used display | `Last used: <value>` is shown on every profile tile. | `embedded-02.html:291-292`; JS `:150-151` |
| Status ownership | Renderer deliberately does not invent `connecting`; comments state the launcher reads realtime mmap state based on live PID + `ACT`. | `embedded-02.html:224-233`; JS `:83-92` |
| States | Per-profile `off`, `ing`, `on`; `lwSetStatus(id, st, idx)` is the only renderer update entry point. | `embedded-02.html:177-208`; JS `:36-67` |
| Launch serialization | Launch is disabled while `_launching` or any profile is `ing`; current `on` profiles are snapshotted and the button is released when a newly launched profile reaches `on`. | `embedded-02.html:179-207`; JS `:38-67` |
| Launch fallback | `_launching` is cleared after 90,000 ms. Existing `ing` still keeps the button disabled. | `embedded-02.html:193-201`; JS `:52-60` |
| Window index | Native-provided `idx` is a first-free window slot, not fixed to the profile, and is shown as `Index: N`. | `embedded-02.html:202-205,288-290`; JS `:61-65,147-149` |
| Capacity counters | Displays registered/purchased and opened/registered counters. `mwOpen` counts only profiles in `on`. | `embedded-02.html:253-258`; JS `:113-117` |
| Refresh | Refresh disables itself visually for 1,200 ms and posts action `save`; comment says native side re-fetches `/api/profiles` and reinjects `lwInit`. | `embedded-02.html:216-220`; JS `:75-80` |
| Import/new session | `none` maps to `__new__`; blank/import path is intended to seed the machine's existing signed-in session; real tile sends its UID/profile ID. | `embedded-02.html:222-240`; JS `:81-100` |
| Double-click launch | Double-clicking a tile launches it. | `embedded-02.html:294-295`; JS `:153-154` |
| Delete safety | Delete is armed on first click and requires an intentional second click after >400 ms and within 3 s; this avoids double-click-to-launch accidentally deleting. | `embedded-02.html:296-305`; JS `:155-164` |
| Menu mode | Launch includes `menu_mode`, selectable between in-game overlay and WebView `ui.html`. | `embedded-02.html:211-215,235-240`; JS `:70-73,94-99` |

One concrete weakness is visible even at renderer level: the `Opened / registered` counter counts only `on`, not `ing`, so a spawned-but-connecting window is temporarily omitted (`embedded-02.html:256`). Refresh feedback is also time-based (1.2 s) rather than coupled to completion in this renderer. The encrypted native/core side may compensate, but that cannot be established without the payload.

## LW-Control Home implementation

The current released Home has a much stronger source-evidence chain than the accessible competitor layer.

### Lifecycle and exact owner routing

`src/LWBridge.UI-0.3.17/src/HomePage.jsx:84-110` derives root validity, game-running, repair, recovery and busy states and only enables Start/Close when the lifecycle is admissible. `HomePage.jsx:123-161` wires Start/Close/root selection plus Auto Launch and Auto Reconnect; `:162-174` surfaces recovery failures and Close confirmation.

`src/LWBridge.UI-0.3.17/src/App.jsx:242-279` assigns the selected profile a `{profileId, generation}` owner identity. `App.jsx:1163-1189` captures that owner before Start; `:1191-1228` captures it before the asynchronous Stop status read, then stops that same owner even if the UI selection changes. `src/LWBridge.UI-0.3.17/src/backendBridge.js:95-107` injects the captured profile ID and rejects late replies after its UI generation retires.

Native routing preserves the same identity. `src/LWBridge.Desktop/LWBridgeWindow.cs:598-604` states that selecting a profile changes only UI subscriptions/backend selection; it does not retire another profile's process, recovery loop, lease or route. `LWBridgeWindow.cs:749-786` routes explicit Home status/start/stop commands by payload `profileId` to that retained owner backend, and `:5727-5755` serializes mutations per profile. Stale selected-view responses are rejected at `:5795-5804` and `:5840-5871`.

### Readiness and status source

`src/LWBridge.Desktop/OverviewLifecycleService.cs:810-864` validates the selected root, exact lifecycle admission and foreign ownership before publishing a new random session/challenge. `:956-1035` validates the helper result, exact process identity and authenticated route before accepting the start; a missing fresh bridge response becomes `BRIDGE_START_TIMEOUT` and the owned start is cleaned up.

`OverviewLifecycleService.cs:1861-1899` defines readiness as all of:

- exact process PID + executable path + process start time,
- authenticated current route when required,
- heartbeat matching profile, session, random challenge and game PID.

`src/LWBridge.Desktop/OverviewLifecycleProfileInstanceStatus.cs:57-120` projects profile connection state from the exact route plus heartbeat freshness (<15,001 ms), and `:123-155` verifies the heartbeat identity fields before accepting its timestamp.

This is materially stronger *evidence* than LastWarPlugin's accessible mmap comments. It is also a stronger stale-PID defense on its face because PID/path/start/session/challenge are all bound. The unavailable LastWarPlugin core could implement equivalent or stronger checks, so the correct verdict is **LW-Control is better proven and currently safer to retain**, not “the competitor is definitively weaker.”

### Refresh and responsiveness

`src/LWBridge.UI-0.3.17/src/App.jsx:798-842` reads runtime status, proxy status and local config concurrently with `Promise.allSettled`; owner and revision checks prevent older responses from overwriting newer state. `App.jsx:933-964` combines selected-owner events with a non-overlapping 5-second fallback poll.

The sidebar concurrently polls every profile every 3 seconds and protects Start/Stop results from stale poll responses with per-profile revisions: `src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx:156-185`.

Compared with the recovered launcher, LastWarPlugin's native-to-renderer `lwSetStatus` push is attractive for all-profile transient responsiveness, while LW-Control has the stronger stale-response fencing. The best improvement is to keep the fencing and add all-profile event publication, retaining the poll as fallback.

### Recovery and failure handling

LW-Control's reconnect path is source-proven. `src/LWBridge.Desktop/OverviewLifecycleRecovery.cs:155-167` requires both `AutoReconnect` and `GameDesiredRunning`. `:336-428` combines exact process identity, bridge/heartbeat state, missing-process observations, hang detection, update detection and disconnect thresholds. `:434-473` publishes the waiting state before the asynchronous recovery run begins.

Stop is likewise exact-owner. `OverviewLifecycleService.cs:1280-1400` validates an optional instance ID, can cancel a recovery before a replacement PID exists, invalidates recovery on user Stop, calls the helper with the captured instance/challenge/PID/path/start time, cleans exact session state after success, and leaves an explicit lifecycle error on failure.

No comparable LastWarPlugin reconnect/recovery backend is available outside the encrypted core. There is therefore **no supported reason to replace LW-Control's recovery method**.

### Profile/session isolation

LW-Control retains a runtime owner per profile (`LWBridgeWindow.cs:728-786`) and isolates lifecycle command ownership from which profile is currently displayed. This is source-proven isolation of controller state and exact process ownership.

The current release intentionally does **not** claim simultaneous independent games. `LWBridgeWindow.cs:798-817` documents that the mutable Lua package is shared per Windows user; `OverviewLifecycleService.cs:843-845` rejects a foreign-owned installation/shared package with `BRIDGE_HOST_BUSY`. This is the released one-game policy, not a hidden concurrency feature.

The LastWarPlugin launcher visibly exposes multi-open capacity, window indices and profile sandbox/import concepts, so its product contract appears more advanced for simultaneous windows. The encrypted core is required to prove whether those windows are truly isolated, how account state is sandboxed, and how cleanup/recovery works. Treating the launcher contract as proof of superior multi-session isolation would be unsupported.

## Feature-by-feature decision

| Area | LastWarPlugin accessible method | LW-Control method | Decision |
|---|---|---|---|
| Start/Close ownership | Backend unavailable; launcher sends profile ID and waits for native status. | Exact `{profileId,generation}` capture plus retained native owner, per-profile mutation gate and exact process/session identity. | **Keep LW-Control.** Competitor backend superiority unknown. |
| Connecting/connected status | Push callback from launcher/native contract; renderer trusts mmap-derived `pid + ACT` and does not fabricate connection state. | Selected-owner events + 5 s poll; sidebar 3 s poll; connected requires authenticated exact-session heartbeat. | **Keep source of truth; improve all-profile push UX.** |
| Stale replies | No comparable renderer revision/generation mechanism recovered. | UI generation/revision fences plus native selected-backend retirement checks. | **LW-Control advantage.** |
| Refresh | Manual `save`/profile refetch; 1.2 s visual timer; status also arrives by callback. | Parallel status/proxy/config read, `Promise.allSettled`, revision fencing, events + fallback poll. | **LW-Control advantage.** |
| Recovery/reconnect | Actual implementation encrypted/unavailable. | Desired-running + AutoReconnect, exact process/heartbeat monitoring, typed recovery states and exact cleanup. | **Keep LW-Control; competitor unknown.** |
| Failure handling | Launcher has 90 s launch flag fallback; native/core errors unknown. | Typed native errors, lifecycle-owned cancellation, exact cleanup; no arbitrary UI timeout for lifecycle. | **LW-Control better proven.** |
| Profile isolation | Launcher contract suggests sandboxed import/new/restored profiles and multi-open slots. | Retained per-profile controller/runtime; simultaneous game blocked by shared Lua package. | **Competitor product contract is broader; implementation quality unknown. Defer F-07 claims.** |
| Last-used history | Visible on every tile. | `last_launched_at` is read/stored (`ProfileRegistryStore.cs:412-434`) but not rendered in `ProfileSidebar.jsx:260-261`. | **Competitor UX advantage; adopt.** |
| Window index | Visible first-free `Index: N`. | Not relevant under released one-game policy. | **Defer until real multi-game support.** |
| Capacity counters | Registered/purchased and opened/registered; connecting windows omitted from opened count. | Sidebar already shows local profile count/max (`ProfileSidebar.jsx:238-240`). | **No immediate gap for one-game Home.** |
| Double-click launch | Supported. | Explicit Start control. | **Do not prioritize.** Explicit action is safer under one-game lifecycle. |
| Delete confirmation | Inline two-step arm avoids accidental double-click deletion. | `window.confirm()` in `ProfileSidebar.jsx:233-236`. | **Optional UX improvement.** |
| Overlay/WebView menu mode | Launcher sends mode on launch; actual per-window UI remains encrypted. | Outside current Home lifecycle scope. | **Do not copy from incomplete evidence.** |

## Prioritized LW-Control improvements

### 1. Display `LastLaunchedAt` — Priority P0, effort **Low**

The data already exists. `ProfileRegistryStore.cs:412-434` selects and maps `last_launched_at`, while `ProfileSidebar.jsx:260-261` currently renders name/server/state/note without it. Add a concise `Last used`/`Last launched` row (relative local time with exact timestamp tooltip). This is the clearest competitor UX improvement with almost no lifecycle risk.

### 2. Publish all-profile lifecycle/status events — Priority P1, effort **Medium**

Keep the current exact-owner state and revision fences. Add native profile-status events for starting/awaiting-login/reconnecting/connected/recovering/offline/error so the sidebar can update immediately, while retaining the 3-second poll as loss/reconnect fallback. This takes the useful part of LastWarPlugin's `lwSetStatus` push design without adopting its unknown mmap/backend implementation.

Expected benefit: selected Home is already event-assisted, but non-selected sidebar rows can currently lag by up to the 3-second polling interval. Event delivery removes that normal-case latency and reduces repeated all-profile reads while preserving a deterministic fallback.

### 3. Make transient state more explicit in profile rows — Priority P1, effort **Low to Medium**

The native status already distinguishes `starting`, `awaitingLogin`, `reconnecting`, `connected`, `recovering` and `error` (`OverviewLifecycleProfileInstanceStatus.cs:73-120`). Ensure these states are visibly distinct and that Start remains gated by native lifecycle state. This provides the clarity of LastWarPlugin's Connecting/Connected tiles without creating renderer-owned status.

### 4. Replace browser `confirm()` deletion with an inline armed action — Priority P2, effort **Low**

LastWarPlugin's first-click arm / intentional second-click delete is a sensible embedded-WebView pattern and specifically avoids a launch double-click becoming a delete. LW-Control currently calls `window.confirm()` (`ProfileSidebar.jsx:233-236`). An accessible inline confirmation could reduce modal dependence. Preserve keyboard/screen-reader support and do not reuse the competitor's timing values blindly.

### 5. Defer window index / multi-open counters / launch-next-after-connected — **Deferred until F-07**

These are useful only when simultaneous games are genuinely supported. If F-07 is later implemented, the competitor launcher's admission rule is a useful design input: do not admit another window while any prior window is still connecting; release admission only when the newly created owner reaches connected or definitively fails. LW-Control should implement that in the native lifecycle/admission layer, not with a 90-second renderer fallback.

## Controlled runtime observation

Evidence: `artifacts/lastwarplugin-home-20261023/unauth-runtime-observation.json`.

A hidden, task-owned, unauthenticated LastWarPlugin session was observed for **8 seconds** with no mouse/keyboard input, no login, no license entry and no game action.

Observed (`unauth-runtime-observation.json:2-95`):

- EXE SHA-256 matched the supplied target.
- Root PID was `17892`.
- Process tree contained `LastWarPlugin.exe` plus six `msedgewebview2.exe` children only.
- No `LwTool.exe`, Python child or game process appeared before authentication.
- No `core.py.enc` or `ui.html.enc` appeared.
- `lwps.set` remained byte-identical and timestamp-identical before/after.
- New files were confined to the WebView cache; outside WebView, `app.ico` metadata changed.
- Exact cleanup left **0** LastWarPlugin/LwTool/WebView task processes.

This is genuine runtime evidence for the unauthenticated boundary only. It does not measure post-login Home, launch latency, reconnect behavior or game integration.

## REA/Ghidra probe and cleanup

The live REA identity was verified before analysis:

- `rea` / `rea-agents` **6.7.0**
- expected reverse-engineer-anything skill **42**
- **207** MCP tools
- project toolchain: Ghidra **12.1.4**, JDK **21.0.12.1** (`docs/proof/REA-MCP-UPDATE-2026-10-23.json:25-59`)

A targeted `rea search ... ui.html.enc --provider ghidra` was attempted with the project-local Ghidra/JDK environment and a retained snapshot path. Despite the focused query, REA/Ghidra performed whole-program auto-analysis and spawned **10 decompiler workers**. At the observed snapshot the ten decompilers used **5.732 GiB** combined working set and Java used **0.783 GiB**, **6.516 GiB combined** before other process overhead. This reproduced the resource-heavy pattern the repository explicitly says not to repeat. The task-owned session was cancelled rather than allowed to continue, and post-cancel process checks found no matching Ghidra task or `decompile.exe` processes.

REA's Rizin fallback was also checked and returned `ERR_REA_WINDOWS_NATIVE` / capability unavailable for this Windows target. No unchanged full-Ghidra retry was made.

This negative result does not establish that the binary is undecompilable. It establishes that the available REA/Ghidra route expands this PE query into an unsuitable whole-program analysis under the current owner-use/resource constraints.

## Reproduction commands

Run from the repository root. These commands are read-only except where noted for artifacts under ignored `artifacts/`.

Verify target identity:

```powershell
Get-FileHash -Algorithm SHA256 '%USERPROFILE%\Downloads\Compressed\LastWarPlugin\LastWarPlugin.exe'
```

Locate the important shipped strings by original file offset:

```powershell
rg -a -b -o -e 'core\.py\.enc' -e 'ui\.html\.enc' -e '/api/download/core' `
  -e 'LWAPPENC' -e 'LWUIEP' -e 'Local\\\\LwTool_MasterKey' `
  '%USERPROFILE%\Downloads\Compressed\LastWarPlugin\LastWarPlugin.exe'
```

Decode the bounded 456-byte production bootstrap:

```powershell
python -c "from pathlib import Path; import hashlib; b=Path(r'%USERPROFILE%\Downloads\Compressed\LastWarPlugin\LastWarPlugin.exe').read_bytes(); d=bytes(x^0x5A for x in b[0x21AFD3:0x21AFD3+0x1C8]); p=Path(r'artifacts\lastwarplugin-home-20261023\bootstrap-0x21afd3-xor5a.bin'); p.write_bytes(d); print(len(d), hashlib.sha256(d).hexdigest(), repr(d))"
```

Verify that no encrypted payload is currently cached:

```powershell
$roots = @(
  "$env:LOCALAPPDATA\LwTool",
  "$env:APPDATA\LwTool",
  "$env:LOCALAPPDATA\Temp",
  '%USERPROFILE%\Downloads\Compressed\LastWarPlugin'
)
foreach ($root in $roots) {
  if (Test-Path -LiteralPath $root) {
    Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -in @('core.py.enc','ui.html.enc') }
  }
}
```

Controlled unauthenticated runtime observation was performed by starting the exact EXE with `Start-Process -WindowStyle Hidden`, waiting 8 seconds, enumerating only the root PID and descendants, recording file/process state into `unauth-runtime-observation.json`, then force-stopping only those captured PIDs. Final process checks returned zero LastWarPlugin/LwTool task processes. No input or account action is required or permitted for this reproduction.

The Ghidra command that was **cancelled and should not be repeated unchanged** was equivalent to:

```powershell
$root = (Resolve-Path '.\artifacts\rea-toolchain').Path
$env:GHIDRA_INSTALL_DIR = Join-Path $root 'ghidra_12.1.4_PUBLIC'
$env:JAVA_HOME = Join-Path $root 'jdk-21.0.12.1+1'
$env:REA_ANALYSIS_PROVIDER = 'ghidra'
.\tools\rea-mcp\rea.cmd search `
  '%USERPROFILE%\Downloads\Compressed\LastWarPlugin\LastWarPlugin.exe' `
  'ui.html.enc' --provider ghidra `
  --snapshot 'artifacts\lastwarplugin-home-20261023\lastwarplugin-native.snapshot' `
  --format json
```

## Remaining unknowns and recommended next evidence

The following cannot be answered from the supplied unauthenticated installation:

- actual post-launch Home/dashboard layout and features;
- actual `ui.html.enc` plaintext and whether it is the full Home, only the per-window WebView menu, or both through generated content;
- native/core implementation of profile sandboxing and multi-open isolation;
- competitor reconnect/recovery rules and exact failure cleanup;
- exact production UI payload cipher/container and how `_LWBOOTKEY`, token and UI endpoint are consumed after `core.py` starts;
- genuine authenticated launch/connect/recovery timing.

The highest-value future evidence is a passive capture from an owner-authenticated normal launch: record the transient `core.py.enc` and `ui.html.enc` **ciphertext** with size/hash/magic, then (only if needed) inspect the task-owned Python child after decryption. Do not record license/token/account values. That would allow a real post-launch Home comparison without conflating the launcher with Home.

## Research handback

No LW-Control production source was changed. No merge, publish or product behavior change was performed. Repository status remained `main` at `45e151c8` with the pre-existing unrelated untracked `.claude/` directory untouched. All new research material is under ignored `artifacts/lastwarplugin-home-20261023/`.

The supported product decision today is: **retain LW-Control's lifecycle/status/recovery architecture; adopt the low-risk history/status UX improvements; defer multi-open UI until simultaneous-game support is independently implemented and qualified; do not claim the competitor's actual Home has been recovered.**
