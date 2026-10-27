# Gathering recall and idle Drill join correction

Product source `44be59d892ad9faf0d09f8342f31a99b273d5c1a` on
`codex/lwtool-remaining-tabs`, draft PR #17. Source fixes and controlled
qualification are complete; patched live acceptance is still open. No merge
or release. The preceding Chat test ZIP remains unchanged and is superseded
by the new package for current product testing.

## Findings and changes

**Idle Drill join used the wrong native argument.** Original v27
`UIFormationSelectListNewCtrl.ChangeMarchByType`, source lines 1206–1212,
looks up an existing owned march and stores its UUID before passing that UUID
to `MarchUtil.OnJoinRally` at line 1352. `OnJoinRally` (890–895) forwards it
into `StartMarch`'s existing-march argument. Our idle-squad path passed a
formation UUID there. A valid native function call could therefore return
without creating the intended join march, eventually timing out.

Idle joins now use the native formation path:
`TryStartMarch(formation, JOIN_RALLY, stamina, true, rallyUuid, point, ...)`
→ `SendCreateMarchMessage` → the formation argument of `StartMarch`.
It retains native formation checks and uses the existing exact send,
serializer nonce, reply and march-target confirmation machinery. The final
wire guard rechecks owner, session, idle formation and eligible monster rally;
it refuses unexpected routes or a second send. An unrelated same-formation
march can no longer count as a confirmed join. Ambiguous attempts retain
their reservation and are not retried automatically.

**Gathering observers could reject their own tool's wrappers.** The old
`intact()` required each observer to remain the top function. Another bridge
feature could legitimately wrap it, leaving its callback active underneath,
yet recall would fail with the owner's reported combined account/observer
error. A paired controlled test with an unchanged account reproduces that
exact error on `a5253803`; the patched source sends exactly one recall instead.

Cooperating gathering/attack observers now register explicit object/key/
previous-function links. A bounded walk proves that the original observer
still exists in this known chain. Unknown replacement functions still refuse
sending. Gathering cleanup retires its own wrapper even when another module
holds it underneath, preserves the newer wrapper and makes the old wrapper
inert on subsequent enable/disable cycles. UID/server/lease, exact owned
formation/march/GUID, target, native return eligibility and no-replay checks
remain. Read-only gathering observations separately expose owner-match and
observer-chain readiness; refusal messages distinguish these two conditions.

The original screenshot's combined message does not identify which half of
the guard failed in that particular session. The observer collision is a
reproduced causal failure mode consistent with it, not a completed patched
live recall measurement.

**Failed Drill could keep a Joining presentation.** Native failure now sets
the failed step and clears stale display rows while keeping uncertain native
reservations. The UI prioritizes a failed state/error over stale joining rows.
Mounted tests verify that failure never renders as Joining/Leading.

## Live observation and boundary

The original owner-opened app/game were inspected without a second controller.
The owner explicitly allowed only tool-owned gathering recall and the current
Drill join on the main account; other main gameplay remained excluded.

The retained native snapshot shows an active event with **11 eligible rally
candidates**, approximately **405.948 seconds** remaining, and Drill already
`failed/running=false/step=joining`, with
`Rally join was not confirmed by a native march`. The UI still displayed
Joining. The event was therefore available; lack of rallies was not the
observed reason for the stopped engine.

One manual native UI Dispatch was attempted as a control after opening a
Drill formation chooser. It is **not** a patched-engine positive: no exact
native receipt was retained that independently confirms that control's target
and march. No second ambiguous join was sent, and no agent recall packet was
sent. The current old build was not patched in place or restarted, preserving
the owner's running session and its existing march ownership records.

A later direct game observation showed `活動已結束` (activity ended). Thus a
genuine patched auto-join positive could not be completed in that event.
No new gathering trip was created on main: that was outside the two-action
authorization. Patched live recall likewise remains unqualified. Existing
owner app/game processes were intentionally preserved; their active installed
scripts were not restored or replaced by this task.

## Checks and package

Passed: actual attack adapter, AFK native/engine, Drill scheduler, new idle-join,
gathering, Automation engine, Hotkeys and Chat/Gold/cross-room Lua suites;
full UI suite; full Desktop delivery suite including Home/Map regressions;
all 31 package-auditor tests. The distinguishing fixtures cover wrong
owner/formation/target/nonce, account drift at wire time, unexpected route,
ambiguous resend, registered observer cooperation, unknown replacement,
exact recall and re-enable without double counting.

Fresh canonical publish, source fingerprint, all **105** ZIP entries and
**29** bridge files passed. All **750** source inputs are pinned to the product
commit. The extracted package was independently compared with every archive
entry. ZIP SHA-256:
`5aaf4faf811a6c20630c4ca984f494bb77690ec2fd17a0d6175ced61d0a825b3`.

Local package:
`artifacts/owner-gather-drill-20261025/package-final/LW-Control-lwtool-improvement-44be59d892ad.zip`.
The running old package does not contain these fixes; testing requires the new
package and a fresh owned session. Earlier active gathering trips cannot be
silently adopted into a new session's ownership ledger.

Sanitized facts, original-bytecode hashes and retained receipt hashes:
[GATHER-DRILL-OWNER-FIX-2026-10-25.json](evidence/GATHER-DRILL-OWNER-FIX-2026-10-25.json).
Raw account/rally records, screenshots and native disassembly remain ignored
under `artifacts/owner-gather-drill-20261025/`.

The next live acceptance needs a newly engine-owned gathering trip with exact
recall-on-disable confirmation, and an active Drill rally with an exact
create GUID/owned march/target confirmation on this package. Use the approved
sub-account for setup/actions beyond the owner's specifically permitted main
tests. Independent review and owner acceptance remain separate from these
controlled passes.
