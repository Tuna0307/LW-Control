# RECOVERY-003 checkpoint A — R3-03 committed Map Stop lifetime

Date: 2026-10-07. Assigned implementation: R3-03 only. Coordinator owns compilation,
execution, integration, current master updates and Git delivery. This file records
the bounded source change and distinguishing test; it does not assert lead acceptance.

## Source authority and preserved behavior

Reference target: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`,
SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
No new executable recovery or hash verification was performed in this subtask.

`EXACT_CONTRACT` (previously recovered source authority):
`docs/reviews/2026-09-30-LWB317-RE-MAP-002-scan-state-machine.md:180-202`
locates original cancellation transaction `0x3C4964-0x3C50FE`, updating a
running/paused run to `cancelled` and removing that run's staged records, then
constructing non-reading/idle state with `resumeAvailable=false`. Provider
`stopMapScan` is behind the available-provider boundary. This does not establish
that local idle means successful game-side termination. The original provider's
game-side transport/termination details remain unproven here.

`MapScanStateMachine.cs:355-411` retains cancelable gate admission, the caller token
for `localSink.CancelScanAsync`, fail-closed local cancellation, best-effort provider
error handling, and the original local idle/resume boundary. The only behavioral
change is the provider Stop token after successful local cancellation:
`CancellationToken.None` at line 381. This independent terminal wait is a necessary
clone/current-client lifetime adaptation; it is not promoted as an original timing
or provider-success contract.

The actual local sink is `MapControlPlane.cs:195-199`; it checks caller cancellation
before calling the synchronous durable `MapStore.CancelScan` transaction
(`MapStore.cs:345-370`). The current-client provider has a cancellation-sensitive
entry at `CurrentClientMap317ScanProvider.cs:174-176`, then cancels its accepted
capture source and awaits the active task independently at lines 177-195. Changing
the state-machine handoff prevents caller retirement in between the durable commit
and that entry from abandoning capture. The current-client provider and native
command service needed no source edits.

## Preserved negative witness

The baseline counterexample remains unchanged in
`lead-review-2026-10-07/native-counterexamples/` and the lead finding at
`docs/reviews/2026-10-07-LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-002-LEAD.md:82-99`.
It established local idle with an uncanceled inert capture after caller cancellation
at the committed local boundary; repeated idle Stop did not retry the provider.
Its reflection setup is historical negative evidence, not this new test seam.

## New inverse proof design

`IMPLEMENTED_NOT_VALIDATED`: the existing Desktop check entry invokes
`CommittedStopRetiresCallerWithoutAbandoningCaptureAsync` at
`tests/LWBridge.Desktop.Checks/Map317NativeBoundaryChecks.cs:24,474-588`.
It uses the actual `Map317CommandService`, actual `MapControlPlane`, actual SQLite
`MapStore`, actual run-scoped process lease, and a controlled run provider. The root
is an explicitly generated temporary directory; plunder workers are disabled and
the action provider is unavailable. No game, helper, current-client capture, owner
storage, browser, reflection, or manual lease-release repair is used.

The barrier lives only in the test's private `PublishingProvider`:
`BeforeCaptureStop` at lines 620,657-662. It runs after actual native local commit,
before that provider checks the passed token or signals its capture. A second
`MapStore` reads the real `cancelled` run, then the barrier cancels the original
request CTS and requires the provider token to be uncancelable. With the baseline
handoff this requirement fails and the provider never reaches its held capture
Stop; with the new handoff its own capture token is canceled and its terminal
callback is deliberately held. There is no new production hook or fallback.

Assertions distinguish:

- A canceled Stop before admission preserves persisted `running`, reading state,
  and zero provider Stop calls.
- Caller retirement after local commit still signals capture for exact run A.
- Capture cancellation does not fabricate termination: Stop is incomplete, the
  run provider still owns A, and the exact process lease cannot be reacquired.
- A concurrent native Start waits behind Stop; canceling that queued Start leaves
  A owned and cannot install another run.
- Releasing the test barrier emits exactly A's terminal callback, allows the idle
  Stop response, and only then makes the lease reusable.
- Repeated idle Stop has no second provider Stop or terminal callback.
- New Start after terminalization owns distinct run B, and its Stop signals and
  terminalizes only B once.

The test always releases and awaits the inert Stop barrier in `finally`
(lines 553-559), including assertion failures, before service disposal. The
controlled provider owns/disposes its capture CTS and emits exact run events
(lines 664-684,695-718,721-733); native lease release remains the existing
run-ID-fenced callback (`Map317CommandService.cs:625-638`).

## Validation handoff and limits

No build or test was executed by this subagent while other assigned lanes edited
the shared checkout. Parent should compile Desktop checks, run the existing
`--map317-native-boundary-check` entry and affected Map checks, then preserve new
command/output evidence beside this design. Existing Map tests already exercise
best-effort provider Stop failure and fail-closed local cancellation
(`tests/LWBridge.Map-0.3.17.Checks/ScanStateChecks.cs:163-181,217-235`); those were
source-inspected, not rerun here.

This inverse test proves the native orchestration and controlled provider lifetime
offline when executed. It does not prove live current-client acquisition, native
capture responsiveness, game-side Stop parity, or the original provider's timing.
