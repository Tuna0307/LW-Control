# Isolated Home ownership lane checks — 2026-10-07

No game, host UI, native provider, owner runtime/config, launch/install/update or actual helper entry point was executed by this worker. Coordinator builds/checks are separately attributed below.

## Runtime installation

Read-only preflight: task venv path `C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua` did not exist; bundled Python `C:\Users\chimw\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe` reported Python 3.12.14, MSC v.1944 64-bit AMD64, and no installed lupa. Created that new task-owned venv; no repository dependency changes.

Source/command: `python -m pip install --index-url https://pypi.org/simple lupa`.

```
Collecting lupa
Downloading lupa-2.8-cp312-cp312-win_amd64.whl.metadata (62 kB)
Downloading lupa-2.8-cp312-cp312-win_amd64.whl (1.9 MB)
Installing collected packages: lupa
Successfully installed lupa-2.8
```

## Lua full-module execution

Command: task venv Python `tests/home_runtime_lease_lua_checks.py`. Default engine explicitly `lupa.lua54`. Tests substitute only in-memory I/O, clock/environment and CS reflection/delegates; root starts with unique `TemporaryDirectory(prefix="lwb317-lua-lease-")`. No real DLL loading, Connect, game calls or owner paths.

```
......
Ran 6 tests in 0.098s
OK
lupa=2.8 lua=Lua 5.4
```

Earlier same six tests with `lupa.lua55`: `Ran 6 tests in 0.098s`, `OK`, Lua 5.5. The first attempted full module parse failed because newly added locals exceeded Lua's 200-local limit; the correction stores this lane's cache/body in a runtime table and the complete production modules now parse/execute under both reported engines. Initial subsequent tests exposed the existing 0.75-second heartbeat throttle in the test clock; the harness now supplies a consistent os.clock/os.time and the unknown-error assertion advances time. Product throttle unchanged.

Additional engine `LWB317_TEST_LUA_ENGINE=lua53`: six tests PASS (`Ran 6 tests in 0.087s`, `OK`, `lupa=2.8 lua=Lua 5.3`). Historic source evidence references Lua 5.3's chunk-local limit at `docs/reviews/2026-09-30-LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001.md:474-478` and `docs/lwbridge-map-scan.md:1426`; no exact-current game engine/header fingerprint was recovered here.

Additional `LWB317_TEST_LUA_ENGINE=lua51` compatibility probe failed on complete probe-module parse: `function at line 5165 has more than 60 upvalues` near source line 6064. The bulk AOI function was not edited by this lane. This is a recorded limitation pending independent baseline/version classification, not current-game validation.

Coordinator independently compared baseline Git revision `138ea`: Lua 5.1 also rejects the baseline bulk AOI function with its greater-than-60-upvalue error. This establishes the unrelated compatibility limitation as historical rather than introduced by this lane. No broad Lua rewrite was undertaken. Exact current-host engine version remains UNKNOWN; Lua 5.3/5.4/5.5 execution is isolated source proof, not game-host proof.

## Coordinator-reported results and preserved failure

Coordinator reported first native compilation PASS with zero warnings; Python producer function-only tests `tests/home_runtime_file_ownership_checks.py` four tests PASS; native Map boundary PASS.

Coordinator's first profile owner rerun failed at `HomeRuntimeFileOwnershipChecks` line 38: `owned refresh succeeds under identity guard`. Source inspection identified test-only barrier handling: Windows replacement denial can surface UnauthorizedAccessException; the callback caught only IOException, and the native guard correctly converted that propagated access denial to false. Corrected only the test's expected replacement-denial handling to recognize both exceptions; ownership predicate/production exclusion were unchanged. Coordinator retains the detailed failed output in lane evidence. No success is inferred from the correction before rerun.

Final coordinator results received: rebuild zero warnings; corrected ProfileRuntimeOwnerChecks PASS, detailed output `profile-owner-corrected.txt` in this checkpoint folder; Python four producer checks rerun PASS; Lua six consumer checks independently rerun PASS under Lua 5.4; eleven other focused native flags PASS in coordinator-recorded outputs. These executions are coordinator-attributed and did not use owner runtime roots. The initial denial failure remains preserved as described above.

## Files changed in this lane

Production: `src/LWBridge.Desktop/OverviewLifecycleService.cs`, new `src/LWBridge.Desktop/OverviewRuntimeFileOwnership.cs`, `src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs`, `tools/run_overview_bridge.py`, `tools/current_overview_bridge.lua`, `tools/current_live_resource_probe.lua`.

Checks: `tests/LWBridge.Desktop.Checks/ProfileRuntimeOwnerChecks.cs`, new `tests/LWBridge.Desktop.Checks/HomeRuntimeFileOwnershipChecks.cs`, new `tests/home_runtime_file_ownership_checks.py`, new `tests/home_runtime_lease_lua_checks.py`.

Evidence: this `home-isolated-checks.md` and `home-ownership-design.md`. No Window/DTO/frontend/Map/master/Git edits were performed by this worker. Production and checks are frozen for coordinator checkpoint commit/review.
