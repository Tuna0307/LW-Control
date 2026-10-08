# HOME009-R3 partial-work independent lead audit

Date: 2026-10-09 (Asia/Singapore). Reviewed saved checkpoint
`98b9d4f78825970590ccb57ef8e146f2d78f30ea` on research/offline-controller; tree
was clean. This is a review of interrupted work, not final worker acceptance.
Decision: **BOUNDED WORK CREDITED / CHANGES_REQUIRED; full Home/Map PARTIAL**.

## Credited completed work

The old R2 registration failure inverse now returns PIPE_REGISTRATION_INVALID for
both fulfilled and rejected helpers. The lead reran the actual production private
helper/host/registry comparison against freshly built R3 binaries; 2/2 match. Its
inherited JSON checkpoint label names the original R2 negative, not the new DLL;
the execution context in this packet records the actual reviewed R3 checkpoint.

Fresh controlled checks: adoption 19/19, Python report handoff 8/8, mounted actual
App/Home localized reconcile errors 6/6, Windows process-local pipe generations
24/24. The two static original contract inspectors pass, including the newly closed
launch-failure code-string projection. These are source/controlled proofs, not
execution of original protected runtime. The original error code-vs-message branch
must not be re-listed as completely undecoded. Local finalizer drop/state facts are
credited; nested response/error semantics remain bounded, not globally recovered.

Fresh integrated R2-compatible sweep: **38/38 exit 0**, Release build 0 warnings /
0 errors, 230 recovery comparisons with unchanged normalization limits, 20 mounted
App cases, native/Home/profile/Map checks, frontend/package and scratch publish.
General passing suites do not cover the new inverse below.

## LEAD009R3-01 — retired adoption disables successor lease timer (HIGH)

Actual production sequence, with controlled helper/process/file seams:

1. Reconcile begins adoption of the old owned game and is held waiting for heartbeat.
2. Explicit Stop completes for that exact game/session.
3. Start successfully establishes a new game/session and lease timer.
4. The old adoption wait resumes, detects that its session is retired, and enters
   `RetireIncompleteAdoption(oldSession, oldNonce)`.

That method calls `StopLeaseTimer` before the ownership check. The timer stop is
unconditional for the service: it disposes the successor timer even though the
file deletion predicate and later state mutation target the old session. The new
game remains running/ready initially, but its lease renewal has been disabled.
This is not closed by the worker's Close-only or sequential A→B→A tests.

New independent `adoption-stop-start/Program.cs` executes actual public production
reconcile/Stop/Start handlers. Control order (old completion before new Start)
keeps the new timer; late order (after new Start) removes it. Two cases, one
mismatch, zero real game launches/OS terminations. `adoption-stop-start-negative.json`
is immutable. Fix session/attempt/generation ownership of all retirement side
effects, not just file/state predicates. Protect successor timer, route, lease,
record, recovery owner and publication against late success/rejection/cancellation.

## Historical live proof and open scope

Worker records four real game launches, including a default-root isolation negative,
an earlier same-PID reconnect witness, a missing-lease failure and a fresh post-fix
same-PID reconnect/Stop/restoration witness. The lead did NOT repeat those launches.
Saved native receipts, closeout and task-root records were inspected. The saved
post-fix screenshot shows Connected/running in Chinese/light. `home-ja-dark.png`
shows Japanese/light, so it is correctly excluded from a JA/dark live pass.
Saved screenshots are not a protected-original pixel/runtime oracle.

Fresh passive process inventory found no game/clone. Read-only installed script
hashes match the worker's post-fix before/after record:
data `2187de71f426741eb61482f6e85639314526e6bdefc9445319b1ff52b31cfac0`,
metadata `988c36bd7d3758404193f9cb380ef5b7f6484621061443fd95a90c8fc1d97306`,
version `535fa30d7e25dd8a49f1536779734ec8286108d115da5045d77f3b4185d8f790`.
This validates current matching files; it does not undo the provenance of the
default-root negative or prove every prior owner config field stayed unchanged.
Continue that preservation accounting where a baseline exists; never delete or
rewrite owner records merely to make a clean status.

Still open: new timer/retirement inverse, further original finalizer response/error
semantics, genuine current launcher-restart producer, repeated reconnect/adversity
and canonical JA/dark desktop evidence. Original encrypted Map controller inputs,
traversal/extractor equality and Treasure/Ghost controller/result semantics remain
separate gaps. Working Resource acquisition does not establish exact original
coverage/order/timing/retry/cancel parity.

The next owner-relayed assignment consolidates ready work as
**LWB317-FUNCTION-HOME-MAP-COMPLETION-010**, preserving R3 progress rather than
restarting it. Live Home permission and the bounded current-server City/Resource
pilot remain; protected/gameplay/recurring Auto scopes are not silently expanded.
No product source changes, game launch/Stop or desktop capture/input by this lead
audit. Historical evidence and negative records remain unchanged.
