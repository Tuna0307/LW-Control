# 009-R1 continuation (AWAITING_REVIEW; Home/Map original A→A parity remains PARTIAL)

Resume point if anything is re-opened: every checkpoint A–F is committed and pushed. Remaining ready work is **not** unfinished code:
it is listed per edge in `semantic-obligations-r1.json` (`undecodedStatic`, `missingEdges`, `protectedInputs`).

Exact next static actions, in order:
1. `python tools/lwbridge317/home009_native.py calls 0x1ddde3` — decode the rollback finalizer body (instance close/restore) called by `0x1D420D`.
2. Map the launcher stage `0x1DC2BC`–`0x1DCDA0` line by line (retry count at `0x1DC35C`/`0x1DC3D0`, `LAUNCH_TASK_FAILED`, `LAUNCH_TICKET_INVALID`).
3. Decode the reconcile recovery-record sub-path `0x203B30`–`0x2043C8` and the status `reason` producer `0x2A0CB7` (command `0x21C5B4`).
4. Post-connect report `0x1E71D6`/`0x23C1E4`/`0x23FAC5`.

External / live dependencies (not unfinished static work): protected `/api/multi/leases` response and entitlement `max_profiles`
(blocks any multi-profile launch parity and `PROFILE_LIMIT_REACHED`), a live witness for the current-client spawn→ready interval
(needed before touching the 120 s readiness window against the original 90 s post-spawn window), a live `closeUnmanaged` witness, and
the lead decision on the native `AutoLaunchGame` AND-gate (ADAPTATION, see `profile-intent-recovery.md`).
