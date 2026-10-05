# Home/Map feature campaign progress

Lead dispatch, 2026-10-06: ASSIGNED. No feature implementation is claimed by this
dispatch. Current UI accepted source delivery is75b7b215; lead acceptance21bb5b3b.
Inspect actual current HEAD/status; later campaign assignment commits are expected.

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
