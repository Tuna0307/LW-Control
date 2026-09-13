# Team workflow — shared Map Data scan engine first

Latest owner direction, 2026-09-13: stop treating City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure as eight separate scanners. The active implementation priority is the **shared Manual Scan engine** shown on the Map Data page. Player City remains the first acceptance category for that common engine because its current-client acquisition/storage path is already proven.

Player City `LWB-PC-001/002/003` remains valid historical/live evidence for fresh city acquisition, persistence, reopen/search and owner-visible saved-row rendering. It is a foundation, not proof that the normal Start button performs a complete world scan. Initial Map Data load may show a persisted City row before Search; that is saved-data browsing, not a new scan.

## Active implementation order

1. Recover and implement the common Manual Scan lifecycle: current server/world readiness, real map geometry/traversal, run identity, block scheduling, Start, Stop, cancellation, retry/failure ownership, native capture, acknowledgements, removals, staging and final publication.
2. Make **Normal** and **Fast** use that same engine. Recovered parity currently proves Normal concurrency `8` and Fast concurrency `20`; do not invent other pacing/timeout/retry differences without evidence.
3. Make the progress/status bar truthful from real scan state: total/completed/read/failed/unread/inflight work, scan rate, progress, capture readiness/pending/dropped state, last error and resume state where recovered. `100%` alone is not completion.
4. Finish **Clear Map Data** as part of the scan lifecycle: server-scoped clear, active-run conflict/cancel handling, generation protection against late-result resurrection, progress/query reset, and preservation of marks where the recovered contract says they survive.
5. Route the eight **Scan Content** selections through the same engine. Prove Player City first, then Resource, then Monster/Truck/Railway/Dispatch/Ghost/Treasure, then mixed selections and finally all eight together.
6. After acquisition is reliable, finish per-kind normalization/storage plus Search/filter/sort/options/export/navigation. Scan-content selection controls acquisition; result filters operate on already acquired persisted data and must remain separate concepts.
7. Build Auto Scan only on top of the proven Manual Scan engine. Do not create a second scanner; Auto Scan owns scheduling/server sequencing and calls the same scan lifecycle.

## Current production boundary

The present `LiveResourceProbeCommandService` city/resource route is a bounded proof adapter, not the finished world scanner. It currently supports exactly one `city` or `resource` selection and leaves full-scan metrics such as block counts/rate/progress unknown. Do not present that one-view acquisition as Normal/Fast full-world scanning.

Existing operation-specific restrictions remain in force. In particular, do not recreate SB-97 through another executor, owner clicks, helper-direct execution or repackaged automation. Continue all permitted static recovery, implementation and offline verification around the shared engine instead of skipping to unrelated categories.

Web remains the single implementation/research/technical-verification worker. The owner supplies only simple UI observations/screenshots when needed. Commit/push/verify each coherent checkpoint under `AGENTS.md`; do not request another Player City saved-row retest unless a concrete regression requires it.
