# Progress — LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004

Starting HEAD: `8ee3b83f4812d017f813b6ac5fe6a4f12aec7332`.

## A — complete

BT-01 is PARTIAL. Exact frontend/native locators prove the status call is selected-profile scoped even though it carries no Treasure records. Idle Treasure entry consumes playerUid/allianceId/states before any claim; after a claim returns queued>0, status polls optional provider-owned batch state until batch.state leaves running. Therefore no-UUID does not disprove per-target status. The genuine missing edge is the high-level provider/controller storage, transitions, reset/error/profile lifecycle and full batch result vocabulary.

Checkpoint B is active. No provider/action was enabled.
