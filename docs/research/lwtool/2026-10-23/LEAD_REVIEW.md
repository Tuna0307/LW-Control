# Independent lead review — 2026-10-23

Reviewed worker commit: 1b3e8b6c83a3997e2ca6cb4c2223ca78f6f47f04.
LW-Control baseline: main@45e151c8.

Status: accepted for the scoped static connection-method comparison, with the
precision corrections below applied by the lead. This does not accept a
performance winner, runtime reliability ranking or production redesign.

## Supported conclusions

The recovered LwTool backend/UI and native DLL remove the previous missing-core
blocker for this target. Its normal Home path hard-selects Route-C; the older
libzstd path is not credited as active production behavior.

The native evidence supports suspended official-launcher creation, APC bridge
loading, launcher CreateProcess interception and process/thread/Job Object
checks when selecting the child. Retained native decompilation is tied to the
verified DLL SHA-256. Native runtime success was not observed in this review.

LwTool's ordinary controller/game command path uses shared mappings and avoids
the disk mailbox boundary present between LW-Control's managed adapter and
Lua. This is a useful structural difference, not proof of faster execution or
lower whole-product resource use.

The normal Home Stop isolation difference is concrete. LwTool py_stop_game
passes image-wide taskkill arguments through to subprocess.run; the wrapper
does not convert them into an exact-child kill. LW-Control's helper verifies
the selected PID/path/start identity and withholds restoration until exact
exit and installation-survivor checks pass. This conclusion concerns normal
single Home; the competitor's separate multi-instance path has different
ownership machinery.

## Precision corrections

1. Reply identity is permissive when absent. GameClient.send uses
   response.get('callbackId', pendingId), so a reply without callbackId is
   accepted. The report previously said only matching callback IDs were
   accepted without this qualification. See recovered-core.py:2169-2204.
2. The normal success delay is per invocation. Its sleep is outside the client
   lock, so another caller can start during that sleep. About 80 ms including
   response wait is not a global command interval, throughput ceiling or
   measured processing latency. See recovered-core.py:2169-2215.
3. Mapping capacity is per distinct instance channel set. Normal single
   GameClient objects reuse global mappings. About 15 MiB is not necessarily
   allocated again per object and is not measured resident/private memory.
   See recovered-core.py:2134-2154.

The comparison report and structured evidence were corrected together.

## Independent checks

- Recomputed backend, UI and native DLL hashes; all match the research record.
- Matched all eight recorded Route-C export RVAs against the actual PE exports.
- Independently decoded eleven recorded native call sites and resolved their
  IAT targets: CreateProcessW; VirtualAllocEx twice; WriteProcessMemory twice;
  VirtualProtectEx; FlushInstructionCache; QueueUserAPC;
  GetProcessIdOfThread; IsProcessInJob; ReadProcessMemory. All match.
- Read the retained child-selection, bind and parent-initialization pseudocode;
  checked its target SHA and source conditions rather than relying on API
  imports alone. Evidence IDs:
  ev_5f0e3f3a1b05624ffe36e7d0fbb5800a327bdf79b7a75c0dc7b5b9c71c631c99;
  ev_cf69bc76759d1c4adda3080d4b8ef72aa56a0deb47971d9436c553e9d17c8c4c;
  ev_847e978326b02fac8b71c86c3a61597e592d636938bafb4c66c37fbf47dfd5f1.
- Checked 97 explicit path:line source references matched by the independent
  report/JSON scanner; all files and line ranges resolve. Existence alone does
  not establish the truth of every associated assertion.
- Directly checked active shared-memory send/pacing, single Stop/wrapper,
  late-connect readiness, LW-Control adapter file exchange and exact Stop/
  restoration predicates.
- Verified the worker commit on the remote research branch and no src/ or
  tools/ differences against main@45e151c8. Only the pre-existing .claude/
  item was untracked before review.

All lead checks were offline. No game/application launch, DLL load, account
action, production edit, merge or release occurred during this review.

## Next decision

Measure equivalent connected workloads before selecting a transport rewrite.
First establish a supported automation-disabled idle configuration for the
competitor: its connect/recovery paths can start AFK/chat engines. A read-only
benchmark command alone does not guarantee an inert game session.

Preserve exact task ownership, use one live controller, and keep original game
files/restoration receipts. If an inert competitor session cannot be proven,
return that specific blocker and complete the offline measurement design.
Do not substitute a launcher-only sample for connected-idle performance.
