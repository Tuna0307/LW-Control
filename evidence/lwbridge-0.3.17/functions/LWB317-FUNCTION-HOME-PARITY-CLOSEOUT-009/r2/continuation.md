# 009-R2 continuation (AWAITING_REVIEW; Home/Map original A→A parity remains PARTIAL)

All R2 checkpoints A–F are committed. Remaining items are individual and listed in `semantic-obligations-r2.json`:

Static, still undecoded (INCOMPLETE, next action first):
1. finalizer `0x1DDDE3` async frame states `0x1DDF66–0x1DE357` (callees `0x1DE6F0/0x1DFEE6/0x1E0BBD/0x1E0C27`);
2. per-instance recovery record schema beyond the PID (the `0xA8`-byte structure read by `0x2DBEBA`, committed by `0x2DB7CC`);
3. launch-failure `error` value shape in reconcile (`0x2054CB`), and the exact condition of `RECOVERY_PROCESS_MISMATCH` in `0x203D4D–0x203DCC`.

Protected / capability / live (not static work): lease release/activation bodies and entitlement capacity (multi-profile launch parity, `PROFILE_LIMIT_REACHED`);
session adoption of a running game across host restarts (needed before `restartRequired` can be mapped to an automatic restart); the launcher
self-restart trigger (`OFFICIAL_LAUNCHER_RESTARTED`) in the live current launcher; live proof that ready.json arrival equals the original registry
`pid_of` event and of `closeUnmanaged` against a real game.
