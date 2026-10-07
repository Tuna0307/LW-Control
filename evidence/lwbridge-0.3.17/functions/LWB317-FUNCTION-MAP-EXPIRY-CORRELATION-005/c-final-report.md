# Final report — LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005

State: **AWAITING_REVIEW**

## Checkpoints

- A: `9da977db8ad83c716c207a92515cc1db058e190c` — LR-GHOST-EXPIRY-005 corrected.
- B: `70348efb38ec107b57b698c2166787c5b8c4c82e` — managed fuid correlation recovered to the observable static boundary.
- C: verification/documentation/delivery checkpoint pending final commit at the time this report was written.

Owner/lead documentation commits that landed between A and B were preserved without reset or discard, through `fe0f5d2735d06a83f52c2033bec02de4300b26d0`.

## A result

The current helper now matches the exact host conditional expiry semantics for the six required cases:

- positive expiry after plunder — accept;
- zero — accept;
- negative — accept;
- missing — accept;
- positive equal to plunder — reject;
- positive before plunder — reject.

Accepted helper rows preserve raw JSON. Frontend random delay, identity, protection timing and counter guards remain. Numeric-string/malformed string handling is unchanged and remains rejected by the established Desktop reader; numeric parsing was not broadened.

Public Ghost preparation remains unavailable and its explanation now separates incomplete protected preparation transformations from separately unresolved execution correlation.

## B result

Managed fuid is source-proven as a keyed pending-request identity **when a response contains the future field**:

- SendLuaMessage allocates fuid and associates fuid + msgId;
- FutureManager stores pending state by fuid;
- MessageFactory conditionally reads the same future field and calls onServerMsgCome;
- onServerMsgCome accounts/removes the keyed pending entry;
- the same SFS response object is converted entry-by-entry to Lua;
- Connect resets FutureManager, zeroing the counter and clearing pending entries.

Therefore fuid is request-specific within a connection/pending lifetime, but reusable across connections and not a globally durable Ghost job identifier.

Exact remaining edge: static source does not prove the direct `ghost.recon.steal` response producer/schema emits or echoes the future field. No real Ghost correlator, request queue, transport or provider capability was added.

## C result

Passed:

- corrected packaged expiry replay;
- Desktop default/module-initializer checks;
- Map317 native boundary;
- Map317 plunder worker boundary;
- canonical Map campaign;
- profile runtime ownership;
- overview normal composition;
- standalone Map checks;
- decoded current-v22 body oracles 4/4;
- blocker body checks 4/4;
- production-Lua safety 3/3;
- managed parser/correlation proof;
- current-client runtime contract;
- current Map compatibility;
- final 005 validator;
- Release Desktop build, 0 warnings / 0 errors;
- Release Desktop.Checks build, 0 warnings / 0 errors;
- canonical frontend check;
- production frontend build;
- production package verification.

Canonical plunder checks remained inert with `externalProviderCalls=0`.

## Acceptance boundary

Home/Map remains PARTIAL. Treasure/Ghost public methods remain unavailable. Ghost scheduled execution remains fenced before the shared Dispatch transport. No LIVE_PROVEN status is upgraded. Live/shared-desktop work remains ON_HOLD_BY_OWNER. Final acceptance remains with the project lead.
