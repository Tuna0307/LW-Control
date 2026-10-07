# LWB317 live pilot — checkpoint 1 recovery and integration

Date: 2026-10-07
Starting continuation HEAD: `4e2d2cb3bb3ce5d41ddc15cb7c83aa47e7cd6095`

## Recovery result

Fresh process inventory returned no LastWar, LastWarLauncher or LWBridge.Desktop
process. The isolated recovery journal was still
`active_ready_deferred_restore` and referenced backup
`overview-bridge-backups/20261007-142602-46861c0ac9f04f69a1e83301cb0c781c`.

The backup bytes were verified against the journal before restoration. Production
`recover_overview_pending_current.py` was then run with
`LWBRIDGE_REBUILD_DATA_ROOT` set to the isolated pilot root and the verified
current game root. It required zero selected game processes, restored all three
files, rechecked the current-client identity, marked the backup manifest
`restored`, and removed `overview-bridge/recovery.json`.

Exact restored hashes:

- LWScripts.data:
  `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`
- LWScripts.txt:
  `d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a`
- version.txt:
  `785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09`

The restored current client is content/version 22 and passes compatibility policy
`lwbridge-current-client-critical-anchors-3`. See
`checkpoint-1-recovery-state.json`.

## Pending live-integration WIP review

Preserved live attempts 1–3 already established that the game-side wrapper reached
authenticated current server 2212, but failed successively at control bootstrap,
the reflected reader invocation boundary, and delegate invocation exposure.
Descendant commits `d2750f21`, `32d6fa2a` and `4fbe0c10` retain those fixes
and evidence.

The preserved untracked `checkpoint-b-attempt-001` then showed the next concrete
failure after the MethodInfo-based reader change:

`pipe_adapter_read_non_string:nil`

with `adapterLoaded=true`. That makes the pending
`tools/current_overview_bridge.lua` change justified as a bounded marshalling
correction: prefer the xLua callable-delegate boundary `reader(path)`, while
retaining reflected MethodInfo invocation as a compatibility fallback and recording
`adapterReadMode`. It does not change the protocol, lease identity, game action
scope, or fallback success semantics.

The paired `tests/home_runtime_lease_lua_checks.py` changes update the inert CS
seam and assertions for the typed reader outcomes without weakening lease/run
fences.

## Checks before the next live launch

- full production Lua lease/ownership tests under lupa 2.8:
  - Lua 5.3: 6/6 PASS
  - Lua 5.4: 6/6 PASS
  - Lua 5.5: 6/6 PASS
- Python compile of Overview helpers: PASS
- `tools/test_overview_bridge_lifecycle.py`: PASS
- current-client compatibility: PASS, v22
- current-client runtime contract: PASS
- runtime-file ownership checks: 4/4 PASS
- current helper `check-only`: PASS, installed files unchanged
- Release Desktop build: PASS, 0 warnings / 0 errors
- production-root isolation check: PASS
- full Release native check suite: PASS
- frontend static/Home/Map/UI-draft checks: PASS
- canonical frontend build: PASS
- canonical production package identity:
  source `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685`,
  artifact `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`

Checkpoint 1 is ready for live retest. No game process is active and no recovery
journal remains.
