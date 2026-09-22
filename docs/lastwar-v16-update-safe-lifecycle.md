# Last War v16 update-safe Overview lifecycle — 2026-09-14

> **Historical version-specific lifecycle evidence.** The installed game-side package later advanced through v19 to v20. Keep this document as the v16 source/history record; do not use it as current package/status authority.

## LWB-V16-001 — modified-package launcher patch failure is causally isolated

**Date / scope:** 2026-09-14, Overview lifecycle update safety. **Status:** LIVE-PROVEN diagnosis.

**Source identity:** installed `LastWarLauncher.exe` and `Launcher.log`; official LocalLow `lwScripts` package; rebuilt `tools/run_overview_bridge.py`. Main game build remains 1078. The launcher-update target is Lua content version 16 from version 14.

**Exact locators:** `Launcher.log` lines 47756-47757 and 48147-48148 record two failed `LWScripts_16_14_u440.patch` applications while the temporary Overview candidate was present. Lines 48274-48275 record the same patch succeeding from untouched official v14 bytes; line 48280 records the subsequent normal game start.

**Result:** the patch advertises size `429388`, patch CRC `3398512614`, and expected output CRC `3454076078`. The two modified-base attempts produced output CRCs `1420218095` and `1720494302`, both rejected. The untouched-base attempt produced exact expected CRC `3454076078` and was accepted as Lua version 16.

**Interpretation:** the current `run_start()` order (`verify -> backup -> install candidate -> launcher`) is not update-safe. The launcher must be allowed to settle/update untouched official files before any temporary candidate is installed. This is evidence for lifecycle ordering, not permission to fake or bypass launcher integrity checks.
## LWB-V16-002 — current official v16 identity and compatibility anchors

**Status:** LIVE-OBSERVED package identity plus RECOVERED/static applicability comparison.

After the successful untouched update, official `LWScripts.data` is SHA-256 `943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4`, size `41278785`, CRC-32 `3454076078`, file version `3`, content version `16`, entry count `18734`. `LWScripts.txt` is `41278785|3454076078`; `version.txt` is `16`.

The following anchors remain unchanged from the accepted v14 build:

- `LastWar.exe`: `df5abcf8618d48500befa9f587b509ed4f58373ff34932bb87ce217f0cf267d5`.
- `xlua.dll`: `21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f`.
- `Assembly-CSharp.rdl`: `871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd`.
- official `DataCenter/Global/LuaEntry.luac`: `50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`.
- `Global/ConstDefine.luac`: `95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd`.
- `Util/CSharpCallLuaInterface.luac`: `af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e`.
- `UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac`: `3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b`.

Therefore the recovered AOI `10/20/1000` table and exact `SceneUtils.ChangeToWorld(callback)` bytecode route remain current-v16 applicable. A temporary v16 Overview candidate was built and verified in a temporary directory without installation; its serialization/LENC round-trip succeeded.
## Validation and implementation impact

Final deterministic verification on 2026-09-15 passes with `ok=true` across profile routing, persistence, request lifetime, map persistence, map contract and bridge-control-pipe contract, with `failures=[]`. The build completes with 0 warnings / 0 errors, and the post-proof diagnostic reports no Last War game or launcher process running.

The original `tools/run_live_resource_probe.py` source remains pinned to its historical v14 constants and was not mutated after that exact edit was rejected. Current production integration instead uses the verified current-client wrappers (`run_live_resource_probe_current.py` and `run_overview_bridge_current.py`), while `OverviewLifecycleService.cs` and `LiveResourceProbeCommandService.cs` now require the supported v16 package SHA-256. This preserves fail-closed behavior without replaying the rejected source mutation.

**Required implementation policy:** normal Overview Start should first run the official launcher against untouched files, wait for the helper-owned selected game to prove launcher settlement, normally close that exact preflight game and any still-owned launcher, then verify the resulting supported official fingerprint. Only after that verification may the helper arm recovery, build/install the temporary Overview candidate, and launch the current client for the real owned session.

This design intentionally prefers a bounded two-phase launcher lifecycle over an unproven direct `LastWar.exe` shortcut. Launcher binary inspection exposed no supported update-only mode, and invoking `--help` while the game was running merely focused the existing game window. The running game command line contains no launch arguments; any launcher ticket/validation is therefore out-of-band and direct launch remains unproven.

**Current implementation status (2026-09-15):** the v16-safe lifecycle has now been integrated through a distinct permitted path. Production start performs pending-candidate recovery before untouched official-launcher settlement, then verifies the supported v16 package before the temporary candidate phase. The exact previously rejected mutations were not replayed or disguised.

## LWB-V16-003 — full update-safe live lifecycle proof

**Status:** LIVE-PROVEN on the installed v16 client.

The untouched official launcher was started first. It launched the selected real game (`LastWar.exe` PID `4872`) at `2026-09-15 00:28:32` local time from the expected installation path. The exact game accepted `Process.CloseMainWindow()` and exited normally. After official settlement, every v16 prerequisite check still passed with package SHA-256 `943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4`, size `41278785`, and CRC-32 `3454076078`.

A fresh temporary Overview candidate was then installed and launched through the official launcher. Session `livev16proof20260915a` reached a genuine same-session game-side READY response on real game PID `39688`: `ready=true`, `messageVisible=true`, `messageText="LWbridge is running"`, registration method `UpdateManager.AddUpdate`. The active candidate identity matched the previously verified v16 candidate SHA-256 `d13bdf539109173c468bf6db85d6929577edeecb930111942fa2ca65eb155eca`.

Stop used exact PID/path/process-start identity, `Process.CloseMainWindow()` returned `accepted=true`, the owned game exited, and exact restoration completed. The helper reported restored official package SHA-256 `943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4` with `installedFilesChanged=false`. A separate post-restore verifier then passed every package/metadata/version/critical-entry check, and no Last War game or launcher remained running.

Durable machine evidence is under `evidence/official-runtime/2026-09-15-v16-live-lifecycle/` (`helper-start.json`, `helper-stop.json`, `attempt-summary.json`, `post-restore-prereqs.json`). This closes the v16 update-safety live-proof blocker; Map Data work can resume after checkpoint review/tests.

## LWB-OVR-018 — v19 authoritative package identity with lagging LocalLow marker

**Status: LIVE-PROVEN, 2026-09-18.** The untouched official launcher applied Lua `19 <- 18`, producing LWLF content version 19 with exact size `41296373` and CRC `1913170558`. A second untouched launch reported that local LWLuaFile version 19 was current against remote version 19. In both cases LocalLow `lwScripts/version.txt` remained `18`; therefore exact marker/header equality is no longer an official-client invariant.

Compatibility policy `lwbridge-current-client-critical-anchors-2` keeps the fail-closed boundary on the LWLF header, exact `LWScripts.txt` size/CRC, recovered LastWar/xLua/managed identities and all four critical Lua hashes. `version.txt` remains required, positive and numeric; it may lag the header, but a marker newer than the header is rejected. The dynamic verifier performs a second full authoritative inspection to preserve change-during-verification protection without calling the historical verifier that required exact marker parity.

The current v19 package SHA-256 is `e7c5742a44d5f5862e4eb6c94944b4150969b6c4bd0a1c1cb9a337cfd1141fa2`. The marker-policy regression, Release build and all six deterministic groups pass. Real manual and auto-start Overview cycles both reached Connected and then closed/restored cleanly; post-restore compatibility remained `ok=true`, with no game/launcher/LWBridge process or recovery journal. Aggregate privacy-safe evidence is [`2026-09-18-v19-version-marker-compat.json`](../evidence/lwbridge-implementation/2026-09-18-v19-version-marker-compat.json).
