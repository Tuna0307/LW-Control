# Progress — LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005

Starting HEAD and origin: `e18d8f3fdbad0a23804c783e5497ad450d74d49c`; checkout clean.

## A — complete

LR-GHOST-EXPIRY-005 is corrected in the internal production helper. Fresh actual
packaged host/helper proof matches for positive-after, zero, negative, missing,
positive-equal and positive-before expiry. Accepted rows preserve raw JSON.
Frontend delay, identity, protection and counter guards remain. Numeric-string
and malformed-string expiry keep the existing Desktop reader's
InvalidOperationException behavior; numeric parsing was not broadened.

Public Ghost preparation stays unavailable with a corrected explanation separating
preparation-transform uncertainty from downstream terminal correlation.

## B — active

Recover actual managed FutureManager / SendLuaMessage / receive dataflow using
validated read-only token resolution. No production correlator or transport.
