# Launch recovery after an official Lua update

## Observed failure

On 2026-10-29, a launch reused a genuine cached settlement for the original v27
Lua package. The official launcher had a v29 delta waiting. After LWBridge installed its
candidate, the launcher applied that delta to the modified input and rejected the output CRC.
The helper reported `official_lua_update_failed` and restored the original files; no game
was started by that attempt. These are local launcher/helper observations, not gameplay evidence.

The existing repair covered only the manual Launch command. Startup reconciliation,
reconnect and update/restart called the shared start transaction directly and skipped it.
A local package hash cannot reveal a newly published server-side delta, so the settlement
cache alone cannot prevent this race.

## Change

Handle the structured official Lua failure inside the shared, already admitted start
transaction. Recognize only a start helper's `OverviewBridgeError` with the exact
`official_lua_update_failed:` prefix. Unrelated errors and incomplete-cleanup wrappers do
not qualify. Invalidate the cached settlement even when the retry has already been used.

Before retrying, run the existing restore-only preflight and untouched official launcher
settlement. The original package must be restored and no selected game may be running.
After settlement passes compatibility validation, refresh the launch binding and give the
bridge a fresh bounded readiness budget. Permit at most two helper attempts total, shared
with the existing distinct `OFFICIAL_LAUNCHER_RESTARTED` retry policy.

Keep the same session, admitted phase and active-start completion barrier through recovery.
Stop/Close cancels the ongoing transaction and waits for its owned cleanup; it cannot admit
a second independent start between failure and repair. No gameplay requests are replayed.

## Compatibility boundary

The existing policy accepts changed Lua package/content identities only while the pinned
game runtime and four critical Lua entries remain exact. This change does not alter that
policy or authorize unknown native interfaces. A future runtime or critical-entry update
still needs artifact inspection, appropriate tests and live Home qualification.

## Validation

Controlled regression and genuine isolated Home qualification results are recorded in
STATUS.md. Raw logs, installed-file hashes and process receipts stay in ignored `artifacts/`.
The isolated live run keeps all gameplay tasks disabled. Restoration must match the exact
post-update backup, because an official update legitimately changes the pre-run originals.
