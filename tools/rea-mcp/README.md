# Project-local REA MCP

Installed 2026-10-12 from official npm `rea-agents` **6.4.0** and updated
2026-10-23 to the latest published release **6.7.0**;
upstream https://github.com/morluto/rea. Exact dependency versions and package
integrity are pinned in package-lock.json. No global npm installation is required.

From repository root, restore dependencies with:

```powershell
npm.cmd ci --prefix tools/rea-mcp --ignore-scripts --no-audit --no-fund
tools/rea-mcp/rea.cmd --version
tools/rea-mcp/rea.cmd mcp doctor --json
```

Codex registration is project-scoped in `.codex/config.toml`; existing user MCP
servers are preserved. `codex.cmd mcp get rea --json` verifies the effective entry.
Restart/reconnect the Codex project session to load the new tools. Project `.mcp.json`
also contains the stdio entry for clients supporting that format. These files have
absolute paths for this Windows checkout; update them if moving the checkout.

The owner-relayed worker can immediately use `tools/rea-mcp/rea.cmd` through its
terminal. Chat On Steroids' own MCP plugin list was not modified; project JSON is
not proof that another host has imported/connected the server. CLI inspection
remains available without restarting or controlling any application window.

The matching release workflow is in `.agents/skills/reverse-engineer-anything`
(skill version **42**). Its seven instruction files match the 6.7.0 npm bundle. They are REA third-party
instructions, subject to project/owner execution authority. Read them when using
REA for shipped artifacts; ordinary repository reads do not require REA.

Actual installed 6.7.0 production stdio verification passed initialize, tools/list
(207), prompts/list (6), binary_session and inspect_managed_artifact against the
built Map assembly. Repeat a bounded read-only integration check with:

```powershell
node tools/rea-mcp/verify.mjs src/LWBridge.Map-0.3.17/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Map_0_3_17.dll
```

The 2026-10-23 update preserves the existing project MCP registration and Ghidra/JDK
paths. Pre-update manifests, registration files and skill files are backed up under
ignored `artifacts/rea-update-20261023`. A chat that already loaded 6.4.0 must
reconnect/restart its MCP connection to load the 207-tool catalog. Fresh stdio
connections and the project CLI use 6.7.0 immediately. npm reported that the old
process still holds its native addon, so it left an ignored `.rea-agents-*`
cleanup directory; do not remove a loaded addon or stop unrelated sessions.

`rea doctor --client codex` checks the user-level registration and reports it missing
on this machine; the effective project-scoped registration is independently confirmed
with `codex.cmd mcp get rea --json`. Do not add a global registration to satisfy
that diagnostic. With the project's existing environment values, `rea doctor
--provider ghidra --json` reports healthy, including the Windows native controls.

The 6.7.0 native smoke check also passed against the previously admitted
`xlua-proxy-plain.dll`: real Ghidra assembly, pseudocode, procedure body,
callers/callees and incoming references were returned, and `close_binary` succeeded.
Raw evidence is retained under ignored `artifacts/rea-toolchain/update-6.7.0-native`.
The compact update receipt is `docs/proof/REA-MCP-UPDATE-2026-10-23.json`.

## MAP-013 portable Ghidra native provider (2026-10-12)

**Now installed, configured, and independently exercised against original-associated
native PE bytes.** Official [Ghidra 12.1.4](https://github.com/NationalSecurityAgency/ghidra/releases/tag/Ghidra_12.1.4_build)
(`ghidra_12.1.4_PUBLIC_20260921.zip`, SHA-256
`ddac49f903da9d5bac833e5cc79395098b9c33cfd3279be5f31bd00387d2d4db`)
and official [Eclipse Temurin JDK 21](https://github.com/adoptium/temurin21-binaries/releases/tag/jdk-21.0.12.1%2B1)
(`OpenJDK21U-jdk_x64_windows_hotspot_21.0.12.1_1.zip`, SHA-256
`f9d6e191ab098c0d416e7d588a24420a8621cd2f4720dab2459b8b7b2d2d8b4e`)
are extracted into **ignored** `artifacts/rea-toolchain`. These copies are not
checked into Git or packaged in the LWBridge application. Original installation
files and their SHA-256 digests are retained privately.

Project-only `.codex/config.toml` and `.mcp.json` supply matching `GHIDRA_INSTALL_DIR`,
`JAVA_HOME` and `REA_ANALYSIS_PROVIDER=ghidra` to the REA stdio process. Config
pre-edit backups are in `artifacts/rea-toolchain/config-backups`. No global
`JAVA_HOME`, Java 17, Node, PATH or unrelated MCP registration was changed.
The project Codex tool timeout is increased to 480 seconds to allow REA's
bounded 330-second cold native import. A host must load/reconnect its project
registration; this does **not** claim that Chat On Steroids itself connected a
new external MCP server.

To verify a read-only native session, from the repository root in PowerShell:

```powershell
$root = Join-Path (Get-Location) 'artifacts/rea-toolchain'
$env:GHIDRA_INSTALL_DIR = Join-Path $root 'ghidra_12.1.4_PUBLIC'
$env:JAVA_HOME = Join-Path $root 'jdk-21.0.12.1+1'
$env:REA_ANALYSIS_PROVIDER = 'ghidra'
tools/rea-mcp/rea.cmd doctor --json
node tools/rea-mcp/native-catalog.mjs
node tools/rea-mcp/verify-native.mjs 'evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006/c-extracted/xlua-proxy-plain.dll' 'artifacts/rea-toolchain/recheck-native'
```

The original-associated 64-bit `xlua-proxy-plain.dll` SHA-256
`266129c6c92f3ae89001493d61ccfdf4f5dac1fe66cff4fbb4799e6c01a6c902`
produced **real Ghidra 12.1.4 assembly, decompiled entry pseudocode,
callers/callees and references**; its owned analysis session closed successfully.
Raw REA Evidence IDs/outputs are in ignored
`artifacts/rea-toolchain/xlua-plain-proof-v2`, summarized at
`docs/proof/MAP-013-REA-NATIVE-2026-10-12.json`.

For direct Ghidra `analyzeHeadless.bat` workflows (outside REA's bridge), set
`APPDATA` and `LOCALAPPDATA` to private **process-scoped** folders under
`artifacts/rea-toolchain` before starting Java. The MAP-013 direct run created
fresh Ghidra-only user cache/preferences paths; after confirming the owned
helper was closed, those directories were preserved in ignored
`artifacts/rea-toolchain/owned-user-cache-backups` and removed from the
initially absent user-profile locations. Do not indiscriminately remove
unrelated pre-existing Ghidra user settings.

The hash-admitted original `lwbridge-0.3.17.exe` (15,866,880-byte Rust native
PE) instead exceeded REA/Ghidra's full auto-analysis 330-second startup limit.
Its bounded selective `Map013Selective.java`/Capstone analysis recovered
native branch instructions and **five smaller original EXE function
pseudocode bodies** (including a Map-related state machine and three linked
callees), but **not** complete original Map scanner pseudocode, kind-dispatch
message schemas, or a decryption key. Use actual v24
managed IL/Lua decoders for custom RDL/RGMD and Lua bytecode; do not force
them into a PE parser. Refer to `docs/MAP_013_REA_NATIVE_RECOVERY.md` for limits.

**MAP-013 lead-corrected extractor:** `Map013Selective.java` now requires
`<ignored-report-path> <ignored-raw-directory> <budget-seconds 10..7200>
<hex-source-start:hex-source-end-exclusive> ...` as Ghidra postScript
arguments. Pass independently verified original PE `.pdata` function
bounds. It applies those exact bounds to Ghidra's disposable function body
(the old `-noanalysis` case created a misleading one-address stub), writes
the complete pseudocode to one ignored `.pseudo.c` file per function,
and records original/retained character and byte counts, SHA-256, full-body
and snippet-truncation flags, source ranges and bounded errors. Run the
read-only `python tools/rea-mcp/validate-selective.py <ignored-report>` to
independently verify SHA, unwinds, complete text and excerpt markers, then
`python -m unittest discover -s tools/rea-mcp -p 'test_*.py'` for its
three fail-closed inverse tests. The prior eight-second trials and old
10,000-character prefix are preserved untouched; the new 75-second original
function results are **bounded timeouts**, not proof of unrecoverability.

**Monitored continuation:** The 300-second cap was a helper policy, not
a Ghidra capability limit. `monitor_native_013.py` runs one hash-admitted
original function using an ignored saved Ghidra project and project-local
Python psutil installed under `artifacts/rea-toolchain/monitor-py`,
not any global Java or Python modification. Example first run:

```powershell
python tools/rea-mcp/monitor_native_013.py --function 140191253:140191aca --output artifacts/rea-toolchain/map013-monitored/control --budget-seconds 60 --interval-seconds 10
```

For subsequent functions, use a **different private output folder**, a
verified source range and `--reuse-project`. The Ghidra project persists
in `artifacts/rea-toolchain/map013-monitored/saved-project`; an
interrupted function is not claimed to resume internally. Raw
`samples.jsonl` observes the Java/child process tree every 30–60 seconds
while `native-child-fast-samples.jsonl` samples actual `decompile.exe`
PID, start identity, CPU, RSS and thread CPU independently at ~200ms.
`report.txt` is flushed before and after each Ghidra decompile,
with `DecompileResults` timeout/cancel/startup/valid flags.
`result.json` is the executable's raw classification; on the
2026-10-12 long run the resource stop is separately verified at
`long-13a8c1/resource-pressure-stop.json` and the consolidated
`result-reviewed.json`. The script now has a fail-closed memory
pressure safeguard for *verified native descendants only*. Do not
misclassify resource interruption as timeout; neither CPU activity
nor a heartbeat proves decompiler phase progress.

No game was launched, game scripts changed, or scanner behavior altered during
MAP-013 tool installation or offline analysis. Native decompilation does **not**
automatically decrypt protected commercial feature packages, authenticate
source responses or prove scanner speed/accuracy.

Raw installation smoke outputs stay under ignored `artifacts/rea-*`. The small
receipt is `docs/proof/REA-MCP-INSTALL-2026-10-12.json`. Source/skill attribution
and license are in this directory's `LICENSE-REA.txt`.
