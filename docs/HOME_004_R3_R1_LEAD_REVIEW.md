# HOME-004 R3-R1 independent lead review — 2026-10-10

Reviewed worker HEAD: `48fe84354b47733fca48d9a15e9d9cdc3f997b5e`.
Disposition: ACCEPT for the bounded listener correction and saved current-client
hang-recovery witness. Whole Home remains PARTIAL; draft PR #6 is not approved
for whole-Home merge or publication.

## Independent checks

- Inspected the production listener/session exception boundary, transport task
  health reporting and opt-in native recovery trace. Host cancellation remains
  separate from session failure; exact registry generation retirement is retained.
- Executed `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj
  -c Release`: actual isolated Windows pipes, admission rejection, malformed
  authenticated frame, unchanged idle timeout, higher generation, successor
  hello/ACK and framed RPC, explicit host shutdown all passed. No game launched.
- Executed `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`:
  Home launch/Close, actual repair producer, recovery monitor/held old cleanup,
  ordered reconciliation and affected Map contracts passed; game launches=0.
- Frontend `check`, `check:production-build` and `git diff --check` passed.
- Independently read saved recovery events and stability/cleanup JSON, verified
  the EN/light and JA/dark Connected screenshot hashes and visually inspected
  both images. Events establish native hang detection, fresh authenticated route
  and succeeded recovery; stability contains eight observations spanning
  15.438 seconds with maximum heartbeat age 1.654 seconds. This is sampled
  stability, not a continuously instrumented guarantee.
- Direct origin branch matched reviewed HEAD. Saved finish receipt reports
  exact original script restoration, no journal and no remaining task processes;
  this lead also observed no current LastWar/Desktop processes.

Initial lead builds were accidentally run concurrently against shared build
folders and collided in npm installation and DLL compilation. Their failed logs
are preserved in ignored `artifacts/lead-home004/`; sequential reruns above
passed. Those failures are lead orchestration errors, not product findings.
No new live fault, desktop interaction or installation mutation was performed
by this lead review. Native live credit refers to the inspected worker witness.

## Acceptance limits and continuation

LEADHOME004R3-01 is closed for its reproduced current-client listener defect.
The prior failed live attempt and original inverse remain historical negatives.
The 30-second adapter reader timeout still derives from historical 0.3.1;
it is not independently decoded 0.3.17 authority.

Launch/Connected/Close, automatic startup, same-build host adoption,
unexpected-process-exit recovery and now hung-process recovery have bounded
saved native positives. They do not close every original Home obligation.

Remaining local implementation work includes H-38 independent simultaneous
profile owners, H-39 active-profile selection, H-41 active-root behavior and
some launcher/error mappings. Repair, pending Stop, offline-only and prolonged
retry/maintenance require their own applicable native/original evidence.
Protected lease/finalizer and encrypted forwarded controllers are distinct
unrecovered inputs, not excuses to skip local implementation.

Next owner-relayed medium unit: `HOME_004_R4_RUNNING_ROOT_CONTINUATION.md`,
H-41 only. Preserve all accepted fixes. No Map work, new branch/worktree,
main merge or publication in that worker assignment.
