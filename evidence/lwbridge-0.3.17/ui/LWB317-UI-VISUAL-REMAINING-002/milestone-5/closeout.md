# Milestone 5 closeout — Map and complete-App integration

Date: 2026-10-05

Status: **READY FOR PROJECT-LEAD REVIEW**

Milestone 5 is built on pushed Milestone 4 baseline
`1a7d863b536f8da67c2abc1b7dfbec483ec36d81`. It closes the final recoverable
source/local Map and whole-App UIUX gates without changing production source after
Milestone 4.

## Final current-App packet

`run-full-app-current.mjs` executes the canonical App on a task-owned Vite listener
with the real retained route tree and records:

- **213 assertions**;
- **11 screenshots**, all decoded and hash-pinned by the validator;
- **0 console warnings/errors/page errors**;
- **64 route transition assertions** across all eight routes in four modes:
  EN/light 1280×900, JA/dark 1280×900, EN/dark 375×1000 and
  JA/light 375×1000;
- exact route DOM identity/retention checks, one visible routed surface, loaded
  fonts and zero narrow horizontal overflow;
- live retained locale/theme propagation;
- uncached profile loading and cached returns;
- App-owned Automation/Map/Squads selection retention across profile replacement;
- keyed child reset and representative local draft/state retention across ordinary
  route hiding;
- Cross-server, profile-note and exit idle/busy composition without invoking native
  success paths;
- current hidden-Activity owner checks with no duplicated City keydown owner.

The current browser packet hashes the **67-file served-App dependency closure**
generated recursively from the real Vite entry instead of a curated file list.

## Final Map authority

The final Map acceptance composes inherited finite proof with current integration
proof rather than repeating the entire historical campaign:

- Unit B remains immutable historical evidence: 10,482 Scheduled renders,
  51/51 mutations, 75 integrated renderer cases and 25 mounted cases.
- `replay-map-pixels-read-only.py` is task-local and write-free. It recomputes the
  15 unaffected historical browser pairs as **13 pixel-exact + 2 bounded Start Scan
  provider fences**, with `outsideAcceptedPixels: 0`.
- The superseded historical `city-error-en-dark` mask is not accepted by final M5.
  Unit A `capture-corrected-error.mjs --read-only` returns
  `CORRECTED_SEARCH_REJECTION_EXACT`, **3/3 unmasked exact pairs**, zero issues.
- The populated current-App Map journey proves 52 results, page 2, and route-return
  retention.
- The profile-boundary journey starts with a non-default App-owned Map tab plus
  keyword, Auto mode, SSR quality, plunderable-only and a 75-minute profile-scoped
  Auto interval. After profile replacement, the parent tab remains while child mode,
  keyword and filters reset and the replacement profile owns its default 60-minute
  interval.
- `replay-map-entry-current.mjs` executes the final App SHA
  `E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`
  through an inert task-local harness. It proves summary dispatch before preload and
  route transition, non-blocking summary rejection, inert active-Map re-click,
  unavailable-provider transport fencing, and retirement of an obsolete profile
  response in favor of the replacement profile owner.

## Manifest and read-only validation

`manifest.json` pins:

- the reference executable and recovered frontend assets;
- all 67 served-App source/style/asset dependencies;
- package.json, package-lock and Vite configuration;
- 207 evidence inputs consumed by the validator;
- all 11 current screenshots;
- exact material tool versions: Node 24.18.0, Vite 7.3.6, React/React DOM 19.3.0,
  Vite React plugin 5.2.0, Playwright 1.63.0, Chrome 154.0.8037.95,
  Python 3.12.10, Pillow 12.3.0 and Git 2.55.0.windows.3.

`validate-milestone-5.mjs` is read-only. It verifies source/evidence/package hashes,
recomputes PNG decoding, tool identities, current Map entry execution, current Map
pixel bounds, corrected City rejection, Unit B, E–H, M4 whole-shell and archive
inheritance. Current result:

`LWB317_REMAINING_M5_VALIDATION_OK` — 67 sources, 213 assertions, 11 screenshots,
11 decoded PNGs, 0 console issues, 64 route transitions, all inherited gates green.

`mutation-check.mjs` detects all 3/3 distinguishing regressions: removing the keyed
profile subtree, removing Map summary-before-transition dispatch, and removing the
App-owned Map tab.

## Review history

The first independent full-App/Map reviews correctly reported two evidence-contract
blockers: the old final validator called a parent pixel writer, and its source closure
was a curated 32-file list. A later Map audit also required final-current Map-entry
execution and a stronger profile-boundary ownership/reset journey. Those historical
NOT READY findings are preserved in `review/`.

The coordinator corrected the packet without changing production: local read-only
pixels, corrected City authority, 67-file generated closure/exact tool pins,
current-App Map-entry runtime replay, exact 375px narrow journeys and the expanded
profile-boundary proof. `review/fresh-map-audit.md` contains the independent post-fix
follow-up and reports **READY** after re-running the final read-only M5 validator,
mutation, Map-entry and pixel checks.

The dedicated fresh full-App worker failed to start and produced no file or edits.
The coordinator therefore independently inspected the final whole-App packet and ran
the complete final gate rather than waiting on a failed worker. Project-lead review
remains the acceptance boundary.

## Canonical checks and limits

Final coordinator gates are green:

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`;
- `node evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs`
  -> `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10`;
- `git diff --check`.

No native Last War action, gameplay action, server jump, scan start/stop, plunder
operation, updater action, OS/global hotkey action or protected original runtime was
executed by this milestone. Unavailable provider controls remain fenced. Loaded native
game assets, positive provider/native success and protected post-auth original-runtime
pixels remain separately recorded dependencies and are not converted into source/local
UI gaps.
