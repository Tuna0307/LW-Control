# Owner test checklist — current

There is **no ordinary Home/Map retest currently required from the owner**.

The historical Player City visible-row check was completed under `LWB-PC-003` and should not be repeated unless a future regression specifically requires it.

Current owner-dependent tests are only:

1. Ghost positive-row proof when authentic population exists; R7-152 already sampled 2212/2175/2180/2185/2207 with clean zero-row scans, so no owner retest is needed until population changes.
2. Supplies positive-row proof when authentic Supplies population exists; R7-153 already sampled 2212/2175/2180/2185/2207/2213 with clean zero-row scans, so no owner retest is needed until population changes.
3. Simultaneous real multi-account UI population when several usable accounts/sessions are available.
4. Explicitly authorized state-changing Treasure/Truck/Dispatch/Alliance acceptance when suitable safe targets exist.

ChatGPT should prepare/operate the technical collection path and give only simple UI instructions when owner interaction is genuinely needed.

See `docs/live-test-handoff.md` for the current detailed live-test boundary.
