# LWB317-FUNCTION-HOME-MAP-BACKGROUND-WORLD-RESOURCE-004

Status: **AWAITING_REVIEW** — production Resource completed and active Stop separately verified; original/UI parity pending.
Reviewed delivery: `e51ab590f1c2866136c2f57e6f936add2945dd11`.

Goal: exercise existing automatic production world entry and prove a fresh bounded
current-server Resource native pipeline. Connection repair is accepted; preserve it.
`isInWorld=false` is not by itself a desktop-input blocker: see BW004-01 in the 003
lead review. Existing Scan Start enters world through the real provider.

Read AGENTS, AI_WORK_PROTOCOL, parent LIVE-PILOT-001, background-only owner direction,
003 lead review and this item's full 004-PROMPT.txt. Worker stays solo.

Checkpoint A: fix test-runner admission so authenticated owned current-server game
can exercise production map_scan_start/world entry. Add distinguishing actual-
boundary inverse cases for initially-city success, world-entry failure/stale reply/
cancellation and unchanged already-world path. Do not alter product to force success.
Checkpoint B: fresh isolated background run, correlated world transition/start,
actual capture/staging/completed publication/query/reopen, plus meaningful active
Stop/terminalization and exact restoration. A second bounded same-server Manual
Resource run is allowed if needed to prove active Stop separately from completion.

No OS screenshots/input/focus. Owner permits game launch/background operation and
the existing pilot's ordinary city-to-world transition through existing native
providers. No unrelated movement, claims/plunder, spending, cross-server, recurring
Auto, updater or protected service. Legitimate UI-only login/maintenance requirements
remain dependencies; prove their actual code/result instead of inferring from city.

Original A-to-A contracts, admission/backup/root/session/run leases and Stop remain.
Partial canceled staging must not be treated as completed publication. No historical
rows, fixture positive proof, fake world state, Resource export invention, product
fallback or guessed protocol. Return AWAITING_REVIEW after applicable checks,
coherent commits/push/direct SHA, updated handoff and honest outcomes/cleanup.

## 2026-10-08 solo delivery — checkpoints A/B complete

Starting clean `80e4f4d746333ca610b2f9180eb41a582e747e2f`.
The existing accepted Lua userdata pipe/host authentication fix was preserved.
No desktop screenshots, keyboard, mouse, focus or foreground UI automation.
The source-specific bug was a checks-only premature city-state return, not a
production world-entry failure. Fixed the runner to invoke real
`map_scan_start` and the existing StartAsync world-ready protocol; six
production state-machine/adapter inert inverses passed.

**Actual completed Resource run:** server 2212, worldId 0,
verified world dimensions 1000x1000; world-ready session/PID match and
state `proven` via `already_world_scene`.
Fresh run `b439bfa57cd54405a14e803d21964c9d`
completed **2500/2500**, zero failures, published **8008** Resource
rows. Reopened the exact isolated database through production
`MapStore.ReadScanRun/Search` and proved page1/page2 each 50 unique,
nonoverlapping records and impossible-name filter zero. The checks-only
runner initially used an incorrectly shaped `map_search` payload
AFTER completed publication; this negative `INVALID_MAP_QUERY`
is preserved and fixed to `{kind,query:{...}}`. The prior
completed publication remained durable, not undone by that harness
request. Higher-level command querying the completed-run data
remains a separate unproven layer, not invented as PASS.

**Actual active Stop run:** a separately started authorised production
test runner appeared while working (the worker neither delegated nor
launched it), with new exact process/profile/run identity.
Its evidence was independently audited. Run
`b9fc31a14f6f4c3d819c0e17efaddbe8` was stopped during
`scanning`, one block in flight. `map_scan_stop` returned idle,
inflight=0; durable status **cancelled**, 0 staged/published Resource
rows. The corrected production command `map_search` returned zero.
This proves meaningful Stop *separately* from the completed positive
run; it does not call cancelled partial staging a completed publication.

Both runs completed exact owned Home Stop, matching original LocalLow
script SHA256, helper manifests restored, journals absent, no final
game/clone process. Independent read-only audit
`tools/lwbridge317/audit_world_resource_004.py` strictly asserts
completed 8008 vs cancelled zero. Underlying source remains standard
production Map/CurrentClient, without fixtures or owner-default-root writes.
All relevant Lua/heartbeat/ownership, native Map/host/world,
Release/frontend and package checks in `verification-004.txt`.

A simultaneous unrelated fast-forward `df4c9551` added current Lua
heartbeat publication. It was **not authored by this 004 worker**,
was not overwritten, and was verified by applicable checks;
no branch reset or force-push occurred. Full evidence and exact
limits: `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001/background-world-resource-004/checkpoint-A.md`
and `checkpoint-B.md`.

**Unproven:** in this completed run, the world-ready result took the
`already_world_scene` route, not an observed
`SceneUtils.ChangeToWorld` callback. Canonical UI visual/click/manual
Stop/reopen, unavailable original Lua and 0.3.17 A-to-A acceptance
remain separate; Resource export is unsupported (City-only dialog).
Parent pilot stays PARTIAL for these claims. Ready for independent
lead review.