# Current implementation handoff — strict parity phase

**Project:** Last War Bot / LW-Control
**Branch:** `research/offline-controller`
**Current checkpoint:** `LWB-R8-003`
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

R8-003 now closes the package binary layout and package AES argument ownership: the original package is `LWBP/version/buildId/12-byte nonce/u32 ciphertext length/ciphertext/16-byte tag`, AAD is exactly `LWBP2|<buildId>`, and the package function receives the 32-byte key as its original third argument. Still missing is the independent `package-key.envelope` grammar/agreement linkage that produces that key, followed by post-decrypt container/script extraction.

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
