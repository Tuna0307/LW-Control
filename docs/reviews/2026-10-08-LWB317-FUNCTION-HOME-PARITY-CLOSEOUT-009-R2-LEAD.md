# HOME009-R2 independent project-lead review

Decision: **PARTIALLY_ACCEPTED / CHANGES_REQUIRED**. Reviewed worker checkpoint
`79617a18d493fbb722f4cc2179279bff954472be`. Home/Map original A→A remains **PARTIAL**.

Accepted bounded work: removal of the extra persisted native AutoLaunchGame
admission gate; the Python post-game-report 90,000 ms wall-clock/final-lookup
arithmetic; separation of acquisition and readiness budgets; closed-owner
cancellation fencing during unmanaged close; repaired inert start-failure tests;
source-located launcher/report/restartRequired facts within their stated limits.
This is not acceptance of full end-to-end readiness, retry equivalence, startup
adoption, original-runtime behavior or live parity.

## LEAD009R2-01 — refresh failure suppressed by helper fulfillment (HIGH)

Actual `OverviewLifecycleService.RunHelperAsync` returns a fulfilled helper result
without checking the report observer's already-completed error. Its catch filter
only propagates that error when the helper rejects. The finally block awaits the
observer but discards its returned error and clears the cancellation marker.
Thus an already-observed registry refresh failure can cross this production
boundary as success. The helper reads cancellation cooperatively; a successful
reply racing the marker is not proof the refresh succeeded.

Independent `refresh-probe/Program.cs` executes this actual private production
boundary with the actual host and registry, a controlled matching report, an
already-claimed binding, and an inert helper. Both cases wait until the observer
writes cancellation for `PIPE_REGISTRATION_INVALID`. Rejected helper → that error;
fulfilled helper → **SUCCESS**, contrary to the required refresh-error precedence.
Two cases, one mismatch. This is a controlled native-boundary proof, not a live
launch/public Start or protected-original execution witness.

Original authority: SHA-matched EXE; `0x1DD152` calls refresh_pending,
`0x1DD163` branches on success to the wait, and failure reaches game termination
`0x1DD198` before rollback. Existing `home009_launch_contract.py` asserts these
edges. New `original-refresh-error.json` preserves the fresh instruction slice.
Do not change the original oracle or relax the registry's claimed-binding rule.

Also audit ordering: Python writes connect-capable control before game-reported;
the C# observer sleeps before its first read, and successful helper completion
cancels it. These are coupled, not independently proven equivalent by tests that
sleep until the observer runs. Establish production handoff completion/error
ownership with distinguishing fast/slow/failed/retired response cases. The lead
does not claim every fast connection is an original mismatch without that proof.

## LEAD009R2-02 — assigned local static recovery still incomplete (MEDIUM)

R2 A/B explicitly assigned the finalizer and recovery/report branches. The
worker's own remaining list still includes finalizer async frames
`0x1DDF66–0x1DE357`, recovery-record schema beyond PID, launch-failure error value
`0x2054CB`, and `RECOVERY_PROCESS_MISMATCH` condition. Their enclosing protected
service does not make available local consuming/control-flow branches external
blockers. Preserve unknowns and continue them; do not say all ready A–F is complete.

## LEAD009R2-03 — same-build re-adoption/restart remains an implementation gap

Recovered original startup can retain/re-adopt a same-build running game and marks
an outdated build restartRequired; the clone classifies previous sessions as
repair-required and does not re-adopt. Leaving automatic termination disabled is
safer than closing games original would keep, and that protection is accepted.
The missing clone capability itself is not an external dependency or A→A pass.
Recover the remaining local record/identity contract, audit current adapter
reconnect capability, and implement only source-supported primary-profile
adoption/restart transitions through isolated production boundaries. Identify any
actual missing artifact/contract precisely. Protected lease capacity/multi-profile
responses and live event mapping remain separate, genuinely unproved dependencies.

## Independent execution and preservation

Fresh `home009_r2_verify.py` in the lead packet: **38/38 exit 0**, Release build
0 warnings/errors, 230 normalized recovery comparisons with unchanged limits,
20 mounted actual-App EN/light/JA/dark cases, native/profile/Map/ownership,
original-byte checks, frontend/package validation and scratch Release publish.
Separate actual reconcile probe: **4/4 original admission comparisons match**.
The new refresh inverse is additional evidence; passing those 38 commands did not
detect it. All proof types remain bounded and are not original/live execution.

Packet: `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/r2/lead-review-2026-10-08/`.
`record-integrity.py` verifies the reference hash, all prior committed 009 evidence
content (with explicit Windows CRLF handling), successful check count, independent
inverse and removal of verification scratch. No product changes, new game launch,
desktop capture/input/focus, protected-service access or owner-state mutation by
this review. The existing owner game was not controlled or closed.

Continue solo **HOME-PARITY-CLOSEOUT-009-R3**, one combined assignment through
sequential durable checkpoints. Preserve all accepted fixes and historical negative
records. No global/UI/live status promotion. Final acceptance stays with the lead.
