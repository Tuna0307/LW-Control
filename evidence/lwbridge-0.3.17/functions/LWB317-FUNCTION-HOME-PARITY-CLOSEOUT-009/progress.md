# HOME 009 progress (durable; newest last)

Start: lead checkpoint b15c13e4f0c8191cd9e6f92814948b548801d124 on research/offline-controller. Solo, static/headless/inert.
No game launch, no shared-desktop capture/input/focus, no protected-service access.

- [START] Tooling: `tools/lwbridge317/home009_native.py` (hash-gated whole-image call/ref index, abs-pointer slot index). Derived index cache lives in the session scratchpad only (never evidence).
- [A-1] Original stop handler `0x199627` does NOT use a graceful close. Order recovered: instance-id compare (`INSTANCE_MISMATCH`), `0x24156e` state lookup, `0x41bbff` log, `0x41e543` terminate, then close-wait `0xe5725`. `0x41e543 -> 0x41d84b`: QueryFullProcessImageNameW verification (`PROCESS_QUERY_FAILED` / "Unable to verify the target process path."), OpenProcess(PROCESS_TERMINATE=1), TerminateProcess(h,1) ("open target process"/"terminate target process" error labels). `0x41e3cf` is a single-PID + image-path (`<root>\Game\LastWar.exe`, ASCII case-insensitive) presence predicate, not a name-count scan.
- [A] Stop = path-verified TerminateProcess + 100x100ms poll implemented (run_stop); comparator 21 scenarios 0 mismatches (baseline 19). Commit c987cb73.
- [C/D] Original monitor 0x41a8a0 / start 0x41b03a / run 0xe5884 decoded; production recovery engine ported (OverviewLifecycleRecovery.cs). Oracle-vs-production trace compare: 230 scenarios 0 mismatches (baseline before port: 112/142 differed). User Stop resets status idle + invalidates run (0x41bbff/0x41ad16).
- [IN PROGRESS] Old Program.cs game-recovery-status block (lines ~1685-2260) still encodes pre-port behaviour and FAILS; must be replaced by contract checks. overview-reconnect-policy, home-campaign-lifecycle, close-timing already updated.
- [CLOUD-1] Cloud (Linux, no dotnet) continuation from f5396258. Repo reference path replaced in home009_native/home008_*; hash gate passes. Static tools home008_close_contract/start_contract/frame_refs/valueflow and home009_contract.py run PASS against reference/lwbridge-0.3.17.exe. home009_recovery_compare re-run: 230 scenarios, 0 mismatches.
- [CLOUD-2] Stale Program.cs recovery block retired (scenario assertions removed; fixture, stop and failed-launch checks kept). New tools/lwbridge317/home009_recovery_contract_check.py asserts original contract points on oracle+production traces (PASS). C# edit NOT compiled: .NET verification PENDING on Windows.
- [CLOUD-3] E skeleton only (home009_handler_skeleton.py -> handler-skeletons.txt). B not started. Both INCOMPLETE; see semantic-obligations.json.
