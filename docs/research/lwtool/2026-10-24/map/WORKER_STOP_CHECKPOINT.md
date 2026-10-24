# Map worker stop checkpoint — 2026-10-24

> Historical checkpoint. Current Map disposition and final-source receipts: [LEAD_FINAL_MAP_QUALIFICATION.md](LEAD_FINAL_MAP_QUALIFICATION.md). Pending/journal statements below describe that earlier checkpoint.

Status: **implementation/research/package saved; genuine packaged live qualification blocked by a pre-existing unowned recovery journal**.
This is a lead progress/housekeeping check, not independent acceptance of every
production change or a new live scan. No merge or release occurred.

## Retained work

- Branch: `codex/lwtool-full-improvement`, draft PR #16.
- `6604092e`: scanner report, transactional batch staging, qualification harness
  and retained current-source native benchmarks.
- `9ddd0ed83afc771813cd60786698d32f85f5eeb7`: package checker compatibility with
  the programme integrity manifest, plus distinguishing tests. Local and remote
  branch tips matched this commit before the documentation-only checkpoint.
- Tracked worktree was clean; pre-existing untracked `.claude/` was preserved.
  Ignored evidence and both earlier/current package attempts were preserved.

## Findings and benefit boundaries

Recovered LwTool client code submits Quick Map jobs to a remote scanning service,
streams results and receives already-enriched player rows. Its client also has
a 30-second duplicate-job/result cache. The remote farm's actual traversal,
coverage and freshness policy remain unknown. Quick Map was not live timed:
the recovered route requires Premium and local cached entitlement was free.
The advertisement has not been independently verified under equivalent work.

Our matched inert 24,362-record storage benchmark reports median staging time
of 2.484 seconds before versus 0.673 seconds with batching, equal row counts and
canonical hashes. This is a storage improvement, not a 73% whole-scan speedup.
Current-source native all-ten direct/camera ABBA reports about 91.0 versus
142.6 seconds. This compares the direct scanner with the camera control, not
the pre-change direct scanner or LwTool; it cannot establish the incremental
live speed benefit of the new batching change. It also performs different work
from an individual-kind 20-second scan. See the benchmark JSON and method report.

## Package available for resumption

All paths are relative to the repository:

- ZIP: `artifacts/map-final-acceptance-20261024/package-9ddd0ed8/LW-Control-lwtool-improvement-9ddd0ed83afc.zip`
- Extracted application: `artifacts/map-final-acceptance-20261024/extracted-9ddd0ed8/LW-Control-UI`
- Receipt: `artifacts/map-final-acceptance-20261024/package-9ddd0ed8/package-receipt.json`
- SHA-256: `c23a0f2603f6790417c23662297385593b830355ae721755a753147e27ff339f`
- 10,363,558 bytes; 105 ZIP entries, including root integrity manifest;
  104 application files. Receipt remains `INDEPENDENT_REVIEW_REQUIRED`.

Fresh independent checks at this stop checkpoint:

- `python -m unittest discover -s tools -p test_map_015_package_ui.py`:
  **29 tests passed**.
- `tools/map_015_package_ui.py` with the paths above, exact 40-character source
  commit and `--mode all-ten`, **without `--execute`**: passed ZIP/extraction,
  per-file inventory/hash/size and source-manifest checks; `executesGame=false`.
- The same non-executing structural verification passed for `auto-all-ten`,
  `truck-stop` and `saved-clear-reopen` against the same extracted package.
- A genuine packaged `all-ten --execute` attempt stopped at admission before any
  new app/game launch because the global recovery journal already existed. Its
  exact owner could not be correlated to the retained current-task Home/Map
  receipts. The journal and recovery helper were left untouched.
- Read-only current-client compatibility inspection still reports an unchanged,
  supported installed game package. No current LastWar/launcher/LWBridge process
  was present after the blocked attempt.
- `git diff --check`: passed before documentation tidy-up.
- Native process inventory: no LastWar, launcher, LwTool, LWBridge, Ghidra or
  Java analysis process running. Remaining Python processes are MCP/tool
  services; dotnet processes are reusable build workers. No process was killed.
- Previous native evidence reports exact Stop/script restoration. No fresh
  installed-script hash comparison or live-game test was performed in this check.

## Required next work after independent recovery-owner resolution

1. Execute final packaged Manual all-ten, remote Auto all-ten with exact return,
   Truck Stop and saved-data Clear/reopen using the existing audited harness.
2. Complete bounded final-package Home same-game route recovery and
   Stop-during-reconnect smoke. Do not repeat the completed Home campaign.
3. Add only the resulting live restoration receipts and return for independent
   lead acceptance. Package helper/UI/privacy/source identity is already complete.

Use the full assignment and `QUALIFICATION_REMAINING_GAPS.md` for commands and
authority. The existing pinned package can be reused after documentation-only
changes. If product/package inputs change, rebuild and qualify the new exact
source. Do not relabel a verification-only invocation as a live test. Do not
repeat scanner-fence experiments or inaccessible Premium trials unchanged.
