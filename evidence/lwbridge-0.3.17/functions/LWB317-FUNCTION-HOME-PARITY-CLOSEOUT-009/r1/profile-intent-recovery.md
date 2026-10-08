# 009-R1 D — profile selection, launch order and preference scope (LEAD009-04)

Reference: `reference/lwbridge-0.3.17.exe`, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Static only; byte assertions: `tools/lwbridge317/home009_profile_contract.py` (output `profile-contract-run.json`).

## 1. Recovered original contract (EXACT_CONTRACT)

**Profile store.** `0x325ABE` reads `controller.db`:
`SELECT id, display_name, role_name, server_id, game_uid, note, display_order, enabled, locked_reason, is_primary, created_at,
updated_at, last_launched_at FROM profiles ORDER BY display_order, created_at, id` (literal `0xD4E5CA`). The 0xD8-byte record keeps
`enabled` at `+0xD0` and `is_primary` at `+0xD1` (serializer `0x328924`, keys `enabled`/`isPrimary`); the id string is at `+0x18/+0x20`.
Record tag `+0` = 2 marks a skipped record in the status builder `0x23F466`.

**Reconcile (`profile_instances_reconcile`, `0x203021`).**
1. `autoLaunchAll` — payload object key (13 bytes, `0x83CF58`); missing or non-boolean ⇒ **true** (`0x20354A`); stored at `[state+0x68D1]`.
2. Pre-step `0x1DDBB2`, then `0x23F685` (profile list prepare; its last argument is the second result of the pre-step and is compared with the list length — role inferred, not decoded), then
   `0x325ABE` (records, order above), then `0x23F466` (per-profile runtime status list; items are 0x78 bytes and carry an
   optional **reason** string at `+0x38/+0x40`; each item also records whether its process is present through `0x41E3CF`).
3. `0x39E6E1` → `0x392EB9` maps every record, in order, to an entry `{id, enabled, reason}` where `reason` is taken from the status
   item with the same id.
4. `0x39E153` keeps entries with `enabled == true` and applies the closure `0x2EA391`:
   * `reason == "restartRequired"` (15 bytes compared as two qwords) ⇒ kind 0 — selected **regardless of `autoLaunchAll`**;
   * no reason ⇒ selected (kind 1) **only if `autoLaunchAll` is true**;
   * any other reason ⇒ dropped.
   `0x39E77D` then derives the ids of the kind-0 (restartRequired) subset.
5. `0x1E70F7` is a read-lock guard over the *authorization state*; a poisoned lock yields `STATE_UNAVAILABLE`
   ("authorization state is unavailable"). It is **not** a launch preference.
6. `GAME_ROOT_NOT_FOUND` (`0x203844`) when the root is unresolved. The per-profile part contains stale-game handling
   (`0x203DCC` path-verified terminate, `0x204593` close-wait, instance removal `0x23E158`), recovery-record errors
   (`RECOVERY_RECORD_NOT_FOUND`, `RECOVERY_PROCESS_MISMATCH`, "validated recovery record has a pid"), and exactly one launch call
   `0x2053FA → 0x1D5009` with `closeUnmanaged = false`. Failures are pushed as `{profileId, error}` and the loop continues; the
   result is `{errors:[…]}`.

**Corrects the earlier 009/008 reading.** `[cfg+0x140]` (`0x203776`) is the poison flag of the authorization-state `RwLock`, not a
global auto-launch gate. The original native code reads **no** persisted launch preference: there is no `autoLaunchGame` literal in
the image; the only gate is the command's `autoLaunchAll`. In the original UI that value is `startupAutoLaunchGameRef.current` —
the global local-storage preference `lwbridge.autoLaunchGame` (getter `!== "false"`, 001A review) captured when the App mounted.
So the original preference is **global and UI-side**; profile participation is decided by the profile's own `enabled` column and
the runtime status reason.

## 2. Current implementation compared

| aspect | original | current | status |
|---|---|---|---|
| order of profiles | `display_order, created_at, id` | `ProfileRegistryStore` uses the same ORDER BY | EQUIVALENT (list), launch uses one selected profile |
| selection | all `enabled` profiles, `restartRequired` or `autoLaunchAll` | each profile has its own lifecycle service; the selected profile only | DIFFERENT SCOPE — multi-profile launch needs `/api/multi/leases` (protected) for any profile beyond the primary; BLOCKED, not guessed |
| `enabled` | filters | stored; the UI cannot create a disabled profile (`enabled:true` only); reconcile ignores it | no observable difference reachable from the supported UI |
| global preference | UI local storage → payload only | UI local storage → payload, **plus** `LocalConfigStore.AutoLaunchGame` of the profile (`OverviewLifecycleService.ReconcileStartupAsync`) | **ADAPTATION, not parity** (see below) |
| `restartRequired` | profiles needing restart relaunch automatically, ignoring `autoLaunchAll` | a correlated repair snapshot returns early; relaunch is the update-and-restart command | UNKNOWN — the source of the status reason (`0x2A0CB7`, command `0x21C5B4`) is not decoded |
| `closeUnmanaged` for reconcile/restart | false | StartAsync default false | EQUIVALENT |
| error list | `{profileId, error}` entries | `OverviewStartupError(profileId, code, message)` | shape equivalent; `error` value (code vs message) in the original not decoded |

**The native `AutoLaunchGame` AND-gate.** With payload `autoLaunchAll = true` and the profile's persisted native value `false`, the
clone does not launch where the original would. It is not original behaviour. It is retained because the isolated pilot hosts and the
mounted App suites seed `AutoLaunchGame = false` in their configs (BackgroundHomeMapWitness002, Home009RecoveryTraceChecks,
OverviewReconnectPolicyChecks, Program.cs "Overview startup OFF") to guarantee that a mounted UI with the default `true` local-storage
value can never launch a real game. Removing it would silently turn those safety seeds into live launches. **Decision recorded for
the lead** (no change made): either keep as an explicit non-parity safety fence, or replace it with a host/pilot-only admission flag
that production never sets. Current native/App preference ownership (A→B→A, deferred, rejected saves) is the accepted R3 adaptation
and is unchanged; its re-run evidence is in `r1/final-checks.json`.

## 3. A→B→A, overlapping/rejected preference writes, Close during Map scan

These are current-client ownership behaviours with no original native counterpart (the original has no native preference write). They
were re-executed, not re-derived: mounted App EN/light and JA/dark (20 cases), `--home-campaign-lifecycle-check`,
`--profile-runtime-owner-check`, `--map-campaign-canonical-check`, `--map317-native-boundary-check`; results in `r1/final-checks.json`.
Additional held-async overlaps added by R1 (E) cover intentional Stop during recovery and Close while a recovery effect is parked.

## 4. Missing edges (D)

* `0x23F685` argument semantics and the pre-step `0x1DDBB2` result (capacity/entitlement-derived — protected input).
* Source of the status `reason` (`restartRequired`) — `0x2A0CB7` (called from `0x21E6E6`) and `0x241836` bodies.
* The recovery-record sub-path of reconcile (`0x203B30`–`0x2043C8`) is catalogued by its literals and calls only.
* `error` field content in the `{profileId,error}` entries (`0x203C5F`/`0x203CA8`, value built by `0x31EF22` from literal `0x83D040`).
