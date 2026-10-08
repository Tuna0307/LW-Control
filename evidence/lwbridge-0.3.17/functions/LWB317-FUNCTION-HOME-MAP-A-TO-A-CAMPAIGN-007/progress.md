# Campaign 007 â€“ live progress

Starting HEAD `839f0bcd162eed560a52c17ccf61eeb6a8731af0` was clean. Exact original executable SHA-256 verified `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`. No Last War/LWBridge process found at initial inventory. Existing 004 completed Resource SQLite retained on disk, to be opened read-only for E; accepted run evidence remains untouched. Source/historical parity is bounded; protected original provider remains dependent on absent matched envelope/device input.

Next: read-only copy + production dispatcher E; execute A/B/C/D/F in independent checkpoints. No game launch assumed.
## Checkpoint 007-E — 2026-10-08

**Current-client command replay proved, NOT original extractor parity.** Original
004 SQLite retained untouched. Read-only sqlite3 backup to uniquely owned
007 temp root; source SHA before/after
09cf51c0a7a6242385e42605578062107599d2c93ad803883a8c353468b45993;
snapshot SHA 641f70f6ce7c69c6d2b8af0d3afa9fe8ad50a0bbcabd0308667719509afc78a8;
8008 Resource rows, exact completed 2500/2500, zero failed/staged,
scheduled jobs absent and integrity check ok.

**Actual Map317CommandService.InvokeAsync**: external game/actions inert;
actual dispatcher/MapControlPlane/MapStore operate on an owned copied database.
Malformed old query rejected INVALID_MAP_QUERY; correct {kind,query:{...}}
returned 8008; two 50-row pages nonoverlap and match read-only comparator.
ResourceNameKey=129027, derived from real result, returned 2608;
impossible key returned zero. Actual data_options count 8008, 4 resource
name options. Offline summary correctly serverId=0 (no game context).
A -> empty B -> A retains A 8008, B 0 and exact rows after reopen.
No scan or job worker, game or destructive command occurred.
Owned temp tree cleanup succeeded after SQLite pools cleared.

**Actual frontend adapter**: production mapBackend.js/createMapApi consumed
recorded actual native command responses. Correct 50/50 page envelope,
positive/negative filter, options, A/B/A, stale event rejection.
This is JS adapter replay, not a mounted DOM or real original WebView.
No claim of original scan row membership, timing or order parity.
Sources: RE-MAP-001/002/003; Map317CommandService.cs;
MapStore.Query.cs; mapBackend.js. Evidence: safe-real-db-snapshot.json,
command-actual-resource-proof.json/.payload.json,
frontend-real-resource-adapter-proof.json.
