# Home/Map feature campaign progress

RECOVERY-002 worker delivery, 2026-10-07: **AWAITING_REVIEW**. The lead's RECOVERY-001 findings have been corrected through three sequential checkpoints: 7586c425 closes unsafe shared-runtime cleanup and active owner/picker isolation; 5d3ba448 closes latest-status/global Auto Launch and Auto hydration/error ownership; b2e6089f closes run-scoped Map capture/lease lifetime, missing native Auto/plunder/DTO cases and fresh whole-session packaged evidence. The isolated native execution gate is now closed; it is not a live-game permission.

Fresh current Release proofs are recovery-002-2026-10-06/package-en-light.json/.png and package-ja-dark-narrow.json/.png, plus their real isolated XLSX exports. Both report externalGameActions 0, zero unexpected browser issues across reload generations and complete isolated shutdown cleanup. The JA proof measures 900x720 and is actually Japanese/dark. Attempts 1-10 and all prior lead counterexamples remain preserved as negative history. See recovery-002-2026-10-06/README.md, checks.md and operation-matrix.md. Project-lead acceptance remains pending; no LIVE_PROVEN status or whole-clone/live parity claim is made.


Recovery worker delivery, 2026-10-06: **AWAITING_REVIEW**. The interrupted
Home/Map implementation campaign has been recovered through source-supported native,
UI and isolated packaged-desktop acceptance. Latest pushed implementation checkpoint:
`eaa73ce571d0c419a0e2880e212c46151ee7fd1f`. Final project-lead acceptance is
still pending; no live/protected-provider status is promoted.
Evidence/master reconciliation commit `4330d1baaf62ccf281f59082d606bc23c68cef24`
is pushed and directly verified equal to `origin/research/offline-controller`.

Historical lead dispatch: ASSIGNED. No feature implementation was claimed by that
dispatch. Accepted source/local UI delivery remains `75b7b215`.

Authoritative scope and all acceptance checks:
`docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001.md`.
The standalone readiness audit is folded into A, not a return boundary.
Worker starts A, creates source/contract/owner inventory, then advances ready units
automatically. Prioritize Home B–D and core Map E–F, advance independent Map/store/
contract work when a producer branch is blocked. Queue.json is the live execution
index; record every implementation/proof/commit and exact missing dependency.

No elapsed-time stop rule or artificial eight-hour minimum. No live game execution
is assigned. Source/local UI acceptance remains intact; function/current-client/live
evidence states are tracked separately. Do not convert test-provider replies into
native/gameplay LIVE_PROVEN claims. See coordination.md and continuation.md.

## 2026-10-06 Milestone A / first integration checkpoint

Milestone A is complete at production checkpoint
`c4509dac4d0b7fb1aa09b343c39fb9a1e00615bc`, pushed to
`origin/research/offline-controller`.

Readiness evidence:

- `home-lane/findings.md` recovers the exact original Home command/caller bytes,
  current bridge/native ownership, strict-vs-public root predicates, lifecycle,
  status/recovery, preference and profile-composition limits.
- `map-lane/findings.md` records the normal Map317 owner, all-eight producer
  matrix, manual scan/store/query/export/action ownership, Auto Scan ownership gap,
  and Treasure/Ghost/current-client limits.
- Startup inventory found an existing owner Last War process (PID 51980); it was
  treated as occupied and no live lifecycle, scan, movement, combat, claim, share,
  update or installation action was executed.

Checkpoint implementation:

- Home canonical callbacks now invoke the existing real lifecycle service using the
  recovered start/status/stop/update/reconcile contracts and original busy split.
- Native game-root picker persistence now rebinds the existing lifecycle under its
  ownership/recovery guard while preserving the weaker recovered public picker
  predicate. A strict launch fallback is resolved separately.
- The shared control-pipe host can retarget its expected client image only while
  idle; active routes/launch registrations/RPC calls fail closed with
  `BRIDGE_HOST_BUSY`.
- Canonical Map bridge now exposes the existing Treasure state, Dispatch share and
  Dispatch/Truck scheduled-job command families. The recovered frontend-owned
  Dispatch random-delay and Truck execute-at transforms are preserved.

Validated without touching the owner game:

- Desktop checks project build with `SkipCanonicalProductionUiBuild=true`: green,
  zero warnings/errors.
- `--overview-bridge-host-transport-check`: green.
- `--overview-bridge-normal-composition-check`: green.
- `--game-root-select-check`: green, including stopped A→B launch binding and
  active-session retarget refusal through synthetic lifecycle hooks.
- canonical frontend `npm run check`: green, including the strengthened Map action
  adapter assertions.

The ordinary Desktop build's first attempt was blocked before C# compilation because
its build target runs `npm ci` and Windows reported the existing
`node_modules/@esbuild/win32-x64/esbuild.exe` as locked. The owner/tool process was
not killed. The canonical UI check/build itself remained usable, and native
compilation was validated with the documented skip property.

## 2026-10-06 Recovery closeout

The recovery was checkpointed rather than accumulated into one unreviewed batch:

- `44487b10` — exact fresh-host selected-root ownership; lifecycle no longer falls
  back from a weak saved selection to another detected installation.
- `602a6f99` — run-token Auto ownership, atomic Manual/Auto transition admission,
  native deadline/persistence authority and fresh destructive Clear authorization.
- `103c091b` — ordered frontend Auto configuration/Run Now coordination and
  owner-retirement fencing.
- `1dad611f` — reusable profile-owned native runtime composition plus direct
  A/B/A/service/store acceptance.
- `a33b02d7` — normal Window/UI generation ownership, HA-03 paired status
  invalidation, actual native profile selection and genuine package-pinned
  real-control desktop integration.
- `3ada9f4c` — closes final independent-review gaps: profile-scoped Home Auto Launch
  acknowledgement/rollback, profile-swap/shutdown serialization, a real Map317
  Auto-A -> Manual-M ownership regression, and v3 packaged proof with enabled inert
  Auto/plunder workers, all eight top-level routes, named events and current managed
  package identity.
- `eaa73ce5` — strengthens the final packaged HA-03 assertion so the disconnected
  capture cannot accept the transient localized Checking state. The regenerated EN
  and JA packets both now prove a settled stopped/disconnected state before recovery.

Executed recovery verification on the final implementation checkpoint:

- Desktop checks build with `SkipCanonicalProductionUiBuild=true`: PASS, 0 warnings,
  0 errors.
- `--profile-runtime-owner-check`: PASS.
- `--home-campaign-lifecycle-check`: PASS.
- `--map-auto-scan-campaign-check`: PASS.
- `--map-campaign-canonical-check`: PASS.
- `--game-root-select-check`: PASS.
- `--overview-bridge-host-transport-check`: PASS.
- `--overview-bridge-normal-composition-check`: PASS.
- canonical `LWBridge.Map-0.3.17.Checks`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`: PASS.
- canonical Release Desktop build/package: PASS after releasing only repo-local
  Vite/esbuild helper locks; no owner game/browser process was touched.
- legacy WIP archive integrity: PASS (`exactFiles=10`).
- reference executable SHA-256: PASS,
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `python tools/check_current_client_compat.py`: PASS for pinned current-client v22.

The final isolated desktop runs are
`integration/recovery-v3-en-light.json/.png` and
`integration/recovery-v3-ja-dark-narrow.json/.png`, with their real exported XLSX
files. They are pinned to source `eaa73ce5`, packaged apphost SHA-256
`a349e1698b47dd9f14b5f52d172a48ccf2643226792521461521e0197c932c69`, managed
DLL SHA-256 `4368114d17656362003cbf1eb5ef6fbf14adcd14ada09b39d87ab22e5cb90f9d`, UI source
fingerprint `4686126fe21d843506b4938371a5532cdf9a12b4c67d32f87b2e67f35833783c`
and artifact fingerprint `d3f32d1de7108642278f841a274d9bd4dcf3f8ea3a65dec6b758eb45c1d10108`.
Each drives all eight top-level routes plus a Map return, every Map child tab, real
inert Auto start/cancel, delayed and rejected Home Auto Launch ownership, native A/B/A,
HA-03 connected -> deferred -> rejected -> disconnected -> connected, export
cancel/failure/success, B-only Clear, named generation-scoped events and final cleanup.
Both record zero browser issues, zero active requests/subscriptions and zero shutdown
cleanup failures. The predecessor Chinese/light captures, v2 packets and every
intermediate recovery timeout/diagnostic remain preserved as historical evidence and
are not counted as current acceptance; see `integration/recovery-closeout.md`.

Remaining limits are external/bounded, not invented successes: Treasure claim/status
and Ghost plunder preparation remain unavailable protected-provider actions; positive
current Railway/Ghost/Treasure population remains live-state gated; no fresh live
game/updater/protected-service action was authorized or executed.
