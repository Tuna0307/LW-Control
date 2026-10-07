# Continuation — LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005

Checkpoint A is complete.

Continue B from the current installed v22 package/RDL hashes. Resolve modified
metadata/CIL operands only through validated read-only mappings. Trace
SendLuaMessage future allocation/association, response consumption, reset/reuse,
raw response conversion and Ghost handler. Retain unresolved operands explicitly.

B may remain partial/blocked without affecting C. Do not add a production
correlator or real Ghost transport.
