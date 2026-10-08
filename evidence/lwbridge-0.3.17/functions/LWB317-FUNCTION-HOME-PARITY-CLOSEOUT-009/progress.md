# HOME 009 progress (durable; newest last)

Start: lead checkpoint b15c13e4f0c8191cd9e6f92814948b548801d124 on research/offline-controller. Solo, static/headless/inert.
No game launch, no shared-desktop capture/input/focus, no protected-service access.

- [START] Tooling: `tools/lwbridge317/home009_native.py` (hash-gated whole-image call/ref index, abs-pointer slot index). Derived index cache lives in the session scratchpad only (never evidence).
- [A-1] Original stop handler `0x199627` does NOT use a graceful close. Order recovered: instance-id compare (`INSTANCE_MISMATCH`), `0x24156e` state lookup, `0x41bbff` log, `0x41e543` terminate, then close-wait `0xe5725`. `0x41e543 -> 0x41d84b`: QueryFullProcessImageNameW verification (`PROCESS_QUERY_FAILED` / "Unable to verify the target process path."), OpenProcess(PROCESS_TERMINATE=1), TerminateProcess(h,1) ("open target process"/"terminate target process" error labels). `0x41e3cf` is a single-PID + image-path (`<root>\Game\LastWar.exe`, ASCII case-insensitive) presence predicate, not a name-count scan.
