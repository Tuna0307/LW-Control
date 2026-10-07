# Owner clarification — A -> A behavioural parity

Date: 2026-10-07

Owner request: if the original behaves like A, the clone must behave like A.
Rewriting code must not turn that behaviour into B. This clarifies the existing
one-for-one product goal; it does not create a new feature campaign.

## Fixed reference and evolving game compatibility

LWBridge 0.3.17 remains the fixed behavioural reference. The compatibility target
is the verified current Last War client and must be revalidated as that client
changes. Later LWBridge releases do not automatically become the reference or
expand the clone's scope. Version numbers mentioned as examples by the owner are
not evidence of installed or published versions.

Internal request formats, bindings and data mappings may need to change to reach
the same observable behaviour on the current game. These adaptations must preserve
the recovered 0.3.17 workflow, defaults, ordering, timing and result meanings for
equivalent supported states. Compatibility is not permission to redesign them.
Record the actual game build and evidence for each supported mapping; do not
claim compatibility with untested future releases.

If a game update removes a required capability or prevents equivalent behaviour,
report the exact incompatibility for owner review. Keep that feature incomplete
until resolved or explicitly rescoped; do not introduce a substitute action and
label it parity. Existing login/account/licensing exclusions and the live hold
remain in force.

## Acceptance rule

Compare equivalent supported inputs, game/profile/server state and preceding
events. The clone must preserve the recovered original observable behaviour:
selection, ordering, defaults, validation, limits, timing, retries, cancellation,
failure results, persistence, UI states and success meanings. Proof must identify
the exact reference artifact/locator or authorized observation and the current
implementation exercised. Record stateful/concurrent sequences where relevant.

The clone can use newly written code and a current-client adapter. An internal
mechanism is an adaptation, not an exact-original fact. It is acceptable for final
parity only when its externally observable effect matches the recovered contract.
Missing private implementation details do not automatically prohibit a provably
equivalent implementation, and they do not authorize guessed behaviour.

Reference-versus-clone distinguishing tests, recovered-body execution and verified
source dataflow establish bounded evidence. Ordinary passing builds and tests
which only restate the clone's design do not establish original equivalence.
Never claim universal correctness merely from a finite test count. Identify
uncovered inputs, sequences and original behaviours as UNKNOWN.

If the original contract is unknown, continue source recovery within the assigned
scope or report the exact missing edge. Keep the feature incomplete/unavailable
where necessary. A fence prevents fabricated success but does not make that
feature equivalent to the working original.

A stricter check, different fallback, retry count, delay, filtering/order or
terminal-success rule is a behavioural difference when observable; do not label
it an improvement and count it as parity. Preserve evidence of demonstrated
differences and correct them against the source. The confirmed Ghost helper expiry
mismatch illustrates this rule; it is already assigned to EXPIRY-CORRELATION-005.

Explicit owner exclusions, including the original login/account/licensing product
UI, remain. The live/shared-desktop hold, session ownership and access boundaries
remain. Current source/local UIUX acceptance stays bounded by its reviewed evidence;
it does not accept unproven native/gameplay behaviour or invalidate historical
evidence without a concrete counterexample.

## Current worker relay

The current worker continues `LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005`.
Do not restart the campaign or broaden implementation to unknown Treasure/Ghost
controllers. Apply this rule to the assigned expiry correction and correlation
recovery. Review retained guards/assumptions against recovered behaviour; report
new out-of-scope discrepancies for lead assignment rather than silently changing
them. Preserve worker-owned code, checkpoint evidence and the existing live hold.
