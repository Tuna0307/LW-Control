# Current implementation handoff — strict parity phase

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R8-010`
**Date:** 2026-09-24

## Current directive

Stop designing our own LWBridge.

The verified `lwbridge-0.3.1.exe` is the product specification. Recover the original implementation and reproduce it one-for-one. Internal compatibility code may differ only when required for the current Last War client and only if the user-visible/output contract remains the same.

Read `docs/strict-parity-recovery.md` and `docs/lwbridge-parity-matrix.md` before touching production code.

## Reference

Path:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Reverified on 2026-09-24.

## What the previous phase accomplished

R1-R7 recovered the original frontend, substantial Rust/Tauri/launcher/proxy architecture, many Map contracts, SQL/query semantics, lifecycle behaviors, current-client Last War call surfaces, and a working Home/Map reconstruction.

That evidence is valuable.

However, the reconstruction also accumulated non-reference decisions: removed original features, added convenience behavior, current-game-specific scan strategies, and performance optimizations that were not first proven to be what LWBridge 0.3.1 did.

The old “0 ordinary partial rows” acceptance statement is therefore not one-to-one completion.

## R8-002 cleanup checkpoint

The pending R7-156 Map work was reconciled instead of left as 30+ GitHub Desktop changes. The known incomplete wide-FOV/68-request production shortcut and the rebuild-only Secret Task Quick Find surface are removed, the complete movement/AOI baseline is restored as a temporary correctness fallback, and the Clear resolver-gap fix is retained. The exploratory Dispatch/Ghost finder probes created during the abandoned redesign discussion were discarded.

This cleanup is not parity proof. The temporary scanner remains a reconstruction until the original LWBridge 0.3.1 Map implementation is recovered.

## R8-007 City Excel parity checkpoint

The original `map_city_export` surface has been restored end-to-end. The generator again preserves the original API wrapper, City-only UI/button/state and all nine locale labels; the desktop host owns the save dialog; backend export uses the recovered 200-row / 1000-page / 200,000-row contract; and the exact six-part XLSX writer/result envelope is regression-tested. See `docs/reviews/2026-09-24-r8-007-restore-city-excel-export.md`.

The secondary Map control-plane research is preserved at `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md` and should be the starting authority for Scheduled Plunder, Auto Scan and the remaining Map control-plane corrections.

## R8-008 Map Clear parity checkpoint

The original `map_scan_clear` boundary is restored. Clear now requires a positive requested server equal to the current scan-state server with exact `serverIdSource=live`; active scans preserve the recovered `SCAN_RUNNING` precedence and all other server-gate failures return `SERVER_UNAVAILABLE`. Only that admitted server's `scan_runs` and `map_records` are deleted; player marks and other server datasets survive.

The frontend is also corrected back to the immutable 0.3.1 behavior: one Manual-only Clear control calling the current server ID. The R7-147 Auto Clear / `serverId=0` clear-all override is removed. See `docs/reviews/2026-09-24-r8-008-map-scan-clear-strict-parity.md`.

## R8-009 server_jump result parity checkpoint

The recovered public `server_jump` success envelope is now complete: `{changed, previousServerId, serverId}`. The current-client source already proved the destination server internally; R8-009 carries that validated destination through the service response instead of dropping it. Existing invalid-ID, busy-operation and timeout behavior remains unchanged. See `docs/reviews/2026-09-24-r8-009-server-jump-result-parity.md`.

## R8-010 map_summary parity checkpoint

`map_summary` now follows the recovered original boundary: shared scan state first, exactly eight original count kinds, run-scoped `scan_records` only while the matching scan is active, otherwise published `map_records`, and exactly `{serverId, counts, scanState}` on success. Rebuild-only `savedServerIds`, `saved_profile_index`, saved-server selection, first-live/live-probe fallback and `zombie_boss` counts are removed from this command. See `docs/reviews/2026-09-24-r8-010-map-summary-strict-parity.md`.

Persisted multi-server browsing and its frontend transforms remain separate Auto/result-browsing parity work; R8-010 does not delete that saved data.

## P0

Recover the original protected `bridge-scripts.dat` implementation.

Already known:

- LWBP package version 2.
- `package-key.envelope` runtime path and bounded reader.
- Microsoft Software Key Storage Provider.
- persistent key `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`.
- ECK1/ECCPUBLICBLOB public-key format.
- 32-byte derived material.
- AES-GCM with 32-byte key, 12-byte nonce and 16-byte tag.
- an identified package decrypt/validation consumer.
- `LWBP2|`, package-integrity and package-build validation strings.

R8-003 closes the package binary layout/AES ownership. R8-004 closes the host-side `LWKE1` token framing and auth persistence. R8-005 closes the exact client P-256 public-key and challenge encodings. R8-006 proves caller-side output ownership: the opaque `package-key.envelope` consumer receives a zero-initialized vector as arg4; on success that exact vector becomes package arg3 and then the required 32-byte AES key. The independent helper lane additionally closes the exact `NCryptSecretAgreement` + `NCryptDeriveKey(L"TRUNCATE", NULL, ..., 32)` KDF contract, generic envelope AES-GCM argument sizes, `LWAT2` authorization-context ordering, raw-envelope equality guard, and manifest/output separation. Remaining evidence-limited unknowns are the exact `LWKE1` peer-key/nonce/ciphertext/tag field indexes/encodings, actual AES-GCM AAD and second-segment meaning. See `docs/reviews/2026-09-24-r8-lwke1-package-key-independent-recovery.md`.

Historical SB-79 records one exact operation rejected by a previous environment. Do not reroute that forbidden operation. Continue the underlying recovery through genuinely permitted methods.

## Map Data correction

Do not continue the LW Atlas-inspired redesign.

Do not further optimize the wide-FOV scanner.

Do not treat the current Train list, Dispatch finder or any other Last War-native source as the intended product design unless the original LWBridge evidence proves that mapping.

The old complete traversal may remain as a temporary safety/reference oracle, but production parity must ultimately follow the recovered original LWBridge behavior.

## Whole-program scope

The project is no longer limited to Home and Map Data. Automation, Squads/AFK, City Layout, Hotkeys, Mini-games, Settings, auth/account flows and every conditional/nested feature in the reference are part of the parity target.

Previously retired original features are parity gaps, not retired scope.

## Worktree state

R8-002 reconciled the old pending R7 Map work and removed the abandoned finder probes. Begin each new checkpoint by confirming `git status`; do not accumulate unrelated experiments in the production worktree.

## Delivery

Every coherent checkpoint must update the parity matrix, recovery finding, backlog and handoff; run applicable checks; commit only its own files; push to `origin/research/offline-controller`; and verify the remote revision.
