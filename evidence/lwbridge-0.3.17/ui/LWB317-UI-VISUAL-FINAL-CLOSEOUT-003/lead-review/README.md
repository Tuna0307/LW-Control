# Milestone E — final current-App integrated proof

Date: 2026-10-05

This directory is the fresh closeout location for the final integrated current-App proof required by `LWB317-UI-VISUAL-FINAL-CLOSEOUT-003`. It consumes the final frozen Milestone D host packet and the Milestone C Automation composition/config-store packets rather than repinning historical evidence.

## Intended proof

`run-final-current.mjs` derives from the accepted REMAINING-002 Milestone-5 current-App runner but writes only under this Milestone E directory. It is designed to exercise the actual current canonical App after earlier closeout milestones settle:

- all eight top-level routes, forward and return, with retained DOM identity;
- Automation's seven categories, Map's eight data tabs plus Scheduled, Map Manual/Auto, and Squads AFK/Equipment parent ownership;
- uncached/cached profile changes, App-owned tab/category retention, profile-owned Map child reset, and profile-scoped Automation child drafts that must not leak across profile replacement;
- populated Map count/page-2 retention;
- Automation and AFK dirty-draft hide/return behavior plus representative Milestone C conditional branches;
- native `dialog` and popover boundaries, focus/Escape cleanup, exit idle/busy behavior, live locale/theme changes, and settled captures with AFK Join, Cross-server, profile-note and exit overlays visibly open;
- hidden Activity ownership using observable City keydown and Mini Games interval-owner counts;
- LR-S1 recovery profile ownership, LR-S2 recurring-poll in-flight/profile cleanup, LR-S3 preview loading ownership, and obsolete Map request retirement through controlled deferred callbacks/harnesses;
- all nine locale catalogs (`en`, `id`, `ja`, `ko`, `pt`, `ru`, `vi`, `zh-CN`, `zh-TW`) against the complete recovered English key set, including dynamic assignments such as `hotkeys.title` / `miniGames.title` that a literal-only source scan misses. The one all-nine source-proven fallback, `common.loading`, is inherited by hash from Unit C and must remain the only assigned literal outside the catalogs;
- EN/light and JA/dark at both desktop and narrow widths, plus EN/dark desktop and JA/light narrow supplemental modes;
- settled full-page captures only after fonts load, profile/Suspense loading placeholders are absent, and two animation frames have completed. Each capture records text/icon/image counts, host geometry/computed style snapshots, PNG bytes, dimensions and a decoded-pixel SHA-256.

The runner hashes the complete App dependency closure before any browser work and again after all journeys, records both the per-file map and aggregate digest, and fails if either closure differs. Before the journeys it independently requests every closure member from the owned Vite endpoint and requires those served bytes to match the workspace closure, after stripping only the known Vite development injection from `index.html`. It freezes the Milestone D/C acceptance hashes, Chrome, the exact JavaScript entrypoints/binary used by Playwright/Vite/Babel/esbuild/React, and recursive implementation-tree digests for those packages.

## Recording and verification

The final producer is deliberately explicit. Once production and Milestones C/D are settled and an owned current Vite listener exists, run from the repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-e/run-final-current.mjs <owned-port> --record
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-e/validate-final-current.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-e/mutation-check.mjs
```

`--record` will create only fresh Milestone E artifacts: `browser-current/`, `final-current-results.json`, and `final-current-manifest.json`. It refuses to overwrite an existing result/manifest unless `--replace` is supplied for an intentional pre-review refresh. `validate-final-current.mjs` requires `--verify`, never writes, recomputes the served dependency closure, verifies evidence/support/tool hashes, verifies the frozen result and every screenshot, decodes every PNG in memory, and reruns the semantic mutation detector. Validation must fail on source, tool, result, screenshot, pixel, locale, or mutation-sensitivity drift rather than refreshing hashes.

`mutation-check.mjs` targets closeout semantics rather than the old M5 string-only ownership mutations. It executes mutated App callback bodies to prove detection of cross-profile recovery acceptance, overlapping recurring status polls, and abandoned-profile loading acknowledgement; checks the corrected Train missing-array default, AFK first-load target and native Join-dialog contracts; executes the Automation preview fixture module to detect lost retained Trade goods plus the retained category-Activity contract; and rejects removal of profile identity from Automation config-store scopes.

## Preservation and limits

Historical REMAINING-002, FINAL-CAMPAIGN-001, lead-review, and earlier closeout evidence remains immutable. The M5 current runner/results/manifest and M4 shell packet continue to describe their recorded checkpoints; they are inputs or historical authority only and are not repinned by Milestone E. Provider-backed actions remain fenced: the runner must not launch/control Last War, scan, jump, plunder, execute OS hotkeys, run the updater, or claim native success.

This packet can establish fresh **source/local current-App integration** after C/D settle. It cannot establish protected original-runtime complete-App pixel equality, loaded native game assets, or provider-positive native/gameplay behavior. Those remain separate dependencies even if every Milestone E source/local check passes. Human final review must still inspect the settled captures and challenge missing conditionals, unequal fixtures, adapter contamination, stale source hashes and regressions before worker delivery.
