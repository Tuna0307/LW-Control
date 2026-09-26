# R7-145 — documentation, evidence, and Home/Map self-audit

Date: 2026-09-22
Parent revision: `eb6f36babdd57a6236f0b96d42d647a07c769f4c`
Branch: `research/offline-controller`

## Purpose

The owner requested a repository cleanup suitable for handoff to another AI: organize current documentation/evidence by tab, remove stale/repeated current-status prose, verify that all current material agrees, and independently re-audit Home/Map completeness and scan performance.

This checkpoint does not manufacture missing live populations or perform consuming game actions. Historical evidence is preserved rather than deleted.

## Documentation/evidence cleanup

Canonical current entry points are now intentionally small:

- `docs/tabs/home.md`
- `docs/tabs/map-data.md`
- `docs/tabs/shared-release.md`
- `docs/implementation-handoff.md`
- `docs/lwbridge-project-status.md`
- `docs/lwbridge-feature-ledger.md`
- `docs/external-audit-guide.md`
- `BACKLOG.md`
- `evidence/lwbridge-implementation/README.md`

Older delivery/status filenames that remain link targets are now explicit historical redirects or carry a prominent cumulative-ledger warning. Detailed dated evidence stays in `docs/reviews/`, `docs/lwbridge-map-scan.md`, `docs/lwbridge-overview-recovery.md`, `docs/lwbridge-injection.md`, the evidence directory, and Git history.

Structural audit after cleanup:

- broken relative Markdown links: **0**;
- invalid JSON files under `evidence/lwbridge-implementation`: **0**;
- exact duplicate files in that evidence directory: **0 groups**;
- the two old review files that used `file.cs:line` as Markdown paths now use valid `file.cs#Lline` anchors.

## Scan-performance self-audit

Current production strategy selection in `MapScanStrategyPlanner` was re-read from source. Standard `worldId=0`, `1000x1000` geometry selects `current_fast_full_world_v2` at concurrency 20 for supported selections; Zombie Boss-only selects `current_fast_zombie_boss_lod2_v1` at concurrency 20. Nonstandard geometry permits only the proven single City/Resource LOD0 path at concurrency 8 and otherwise fails closed.

R7-130 remains the current live speed authority: Truck improved 135.165346 s -> 74.7213523 s; Monster 137.0355255 s -> 77.7809171 s; all-eight completed in 77.8903951 s with 2,500/2,500 blocks and zero failed/unread. Two-server all-eight completed in 82.6769287 s on 2212 and 78.1995665 s on 2213.

Conclusion: **no additional evidence-backed safe speed optimization is currently known.** This is deliberately not a claim that future software can never be faster. Any future optimization must preserve exact coverage, identity, publication, failure, and restoration contracts.

## Failure-first verifier finding

The fresh zero-argument normal-user restart walkthrough initially ended `ok:false` because the owner's real config currently has `autoLaunchGame=true`. The application correctly honored that setting and started Last War, while the verifier incorrectly assumed the final LastWar process count must always be zero.

`tools/check_normal_user_restart_walkthrough.ps1` now snapshots the exact user config/backup bytes, temporarily suppresses auto-launch **outside** the acceptance app, launches the Release app with zero arguments for both acceptance runs, and restores the exact original bytes in `finally`. An intermediate rerun missed one external Win32 mouse click; cleanup/restoration succeeded and the unchanged rerun passed, so that event is retained as input-delivery flakiness rather than a product defect.

Final fresh walkthrough:

- `ok=true`;
- two distinct zero-argument Release processes;
- Overview -> Map Data -> Overview active-nav proof in both runs;
- user config and config backup hashes restored exactly;
- v20 game package identity unchanged;
- final LWBridge / LastWar / launcher / helper process counts all zero;
- no scan, Follow, attack/plunder, claim/collect, or messaging action.

Durable evidence: `evidence/lwbridge-implementation/2026-09-22-r7-normal-user-restart-walkthrough-autolaunch.json`.

## Fresh validation

- frontend generator `--check`: PASS;
- Release desktop build: PASS, 0 warnings / 0 errors;
- checks project build: PASS, 0 warnings / 0 errors;
- deterministic executable: PASS, all six groups true, `failures=[]`;
- browser source parity: PASS, 36 checks;
- City export removal browser check: PASS;
- automatic scan-strategy UI browser check: PASS;
- R7-131 Map persistence/saved-server/Stop/Clear browser check: PASS;
- R7-132 Auto navigation/Refresh/reconnect browser check: PASS;
- R7-136 Auto restart browser check: PASS;
- normal production Overview/Map window smoke: PASS;
- hardened normal-user restart walkthrough: PASS.

Build-artifact identity is recorded by scope rather than treated as universal: the detached-worktree Release build was `B8150836C619E9B3A00F26A0CAD7DCA9D72463D50F01DD64A89CAD6879047D8B`, while the exported staged-snapshot Release build was `8A228D9AD619BB9406855A8CEBAECA3E10E397102B5153569CA43E50DE24A489`. The source/tool checkpoint is the same; the audit does not infer a cause for the differing build bytes. Final checkpoint acceptance uses the staged-snapshot artifact hash.

The acceptance matrix remains 47 cases with the exact R7-144 status counts and **0 ordinary `partial` rows**. No acceptance row was promoted by documentation cleanup.

## Remaining gates

Ghost positive-row proof remains owner-deferred until 2026-09-24. Supplies remains population-pending after the R7-144 2212/2213 zero-Supplies recheck. Treasure E02 remains behind preserved SB-79. Live Truck/Dispatch plunder and Alliance-share require suitable explicitly authorized targets/actions. Simultaneous real multi-account population remains unavailable. Final integrated release acceptance remains open.

Machine-readable audit: `evidence/lwbridge-implementation/2026-09-22-r7-doc-evidence-self-audit.json`.
