# Recovery packaged-desktop closeout

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001`
Implementation checkpoint: `eaa73ce571d0c419a0e2880e212c46151ee7fd1f`
Mode: isolated package-pinned WebView/native host with inert providers and temporary stores only.

## Accepted captures

The accepted recovery captures are:

| Variant | JSON | Screenshot | Export workbook | Result |
| --- | --- | --- | --- | --- |
| English / light | `recovery-v3-en-light.json` | `recovery-v3-en-light.png` | `recovery-v3-en-light-ui-export.xlsx` | PASS |
| Japanese / dark / narrow | `recovery-v3-ja-dark-narrow.json` | `recovery-v3-ja-dark-narrow.png` | `recovery-v3-ja-dark-narrow-ui-export.xlsx` | PASS |

Both JSON packets record `schemaVersion=3`, `state=proven`,
`mode=isolated-package-pinned-real-ui-controls`, `externalGameActions=0`, apphost
SHA-256 `a349e1698b47dd9f14b5f52d172a48ccf2643226792521461521e0197c932c69`,
managed Desktop DLL SHA-256
`4368114d17656362003cbf1eb5ef6fbf14adcd14ada09b39d87ab22e5cb90f9d`, and the
canonical ProductionUi identity
(`sourceFingerprint=4686126fe21d843506b4938371a5532cdf9a12b4c67d32f87b2e67f35833783c`,
`artifactFingerprint=d3f32d1de7108642278f841a274d9bd4dcf3f8ea3a65dec6b758eb45c1d10108`).
Both record zero captured browser issues.

The English packet measures `1120x720`, asserts `document.documentElement.lang=en`,
the language control value `en`, light theme state and the English rendered Map label.
The Japanese packet measures `900x720`, asserts `lang=ja`, control value `ja`, dark
theme state and Japanese rendered navigation; `scrollWidth == width`, so the claimed
narrow viewport has no page-level horizontal overflow. Both screenshots were opened
and visually inspected after settlement; they match those recorded states.

## Real-control/native-owner routes executed

The driver does not manufacture native invoke messages. `ExecuteScriptAsync` is used
only to operate or inspect rendered React controls. Native state is read separately as
an oracle. Through those controls the packet proves:

- native profile sidebar A -> B -> A -> B -> A replacement, with native owner
  generations `1 -> 2 -> 3 -> 4 -> 5`;
- all eight top-level routes are clicked and settled through the real sidebar, followed
  by an explicit return to Map; the packet records each active route and rendered label;
- first-A delayed Map Search cannot revive after B becomes current; returned A is a
  new generation, not the first-A owner;
- profile-local Home Auto Launch, Auto Scan config and Map stores remain isolated and
  reopen on profile return; a delayed first-A Auto Launch acknowledgement cannot
  overwrite B, and a rejected returned-A save rolls the UI back to committed false;
- actual Map navigation and every one of the eight Map tabs with one seeded row before
  destructive B-only Clear;
- actual Auto Scan tab controls, interval edits and target-server Add for A and B; the
  final runner enables the native scheduler and inert plunder workers, starts one real
  inert Auto scan from the checkbox, then cancels the exact owned run from that control;
- HA-03 fresh-pair invalidation: deferred status becomes Checking, forced paired read
  rejection becomes Unavailable, an injected disconnected pair becomes stopped, and a
  later fresh pair returns to connected;
- actual City Export button cancellation, real write failure and successful XLSX write;
- actual Clear control removes B's eight native summary counts while A's Map rows
  survive profile return;
- document replacement cancels the exact delayed B request owned by generation 4,
  reload reopens persisted profile B, and language/theme persist across that restart;
- final shutdown closes the request registry, leaves zero active requests and zero
  subscriptions, detaches profile-runtime event handlers, removes the isolated root,
  and records `cleanupFailures=[]`.
- named native events are captured with profile ID and runtime generation. Each packet
  contains 39 observations and explicitly requires both
  `bridge://local-map-auto-scan-changed` and `bridge://map-scan-status`.

The written recovery restart requirement is satisfied at its stated ownership layers:
the packaged host performs a real WebView document replacement/reload with persisted
profile/language/theme/request retirement, while focused native checks separately
construct fresh Home lifecycle/config, Auto service/state and plunder store/worker
owners and prove restart reconciliation. The recovery work item does not explicitly
require launching a second `LWBridge.Desktop.exe` process, so no whole-executable
relaunch is claimed as package evidence.

## Historical/superseded attempts retained

The following files are intentionally retained as history and are **not** acceptance:

- `isolated-desktop-en-light.json/.png` and
  `isolated-desktop-ja-dark-narrow.json/.png`: predecessor transport evidence whose
  claimed locale/theme names were disproved by lead review (both screenshots were
  Chinese/light). `isolated-desktop-en-light.json.error.txt` is the predecessor timeout.
- `recovery-isolated-desktop-en-light.json.error.txt`: first replacement-driver Clear
  assertion timed out; its generated workbook is retained.
- `recovery-final-isolated-desktop-en-light.json.error.txt`: shared WebView user-data
  contamination made the initial Home preference nondeterministic; the final driver
  now owns a fresh per-run user-data root.
- `recovery-v2-isolated-desktop-en-light.json.error.txt`: an over-constrained badge
  assertion timed out after Clear although native deletion later proved correct.
- `recovery-final-en-light.json.error.txt`: diagnostic run showed native all-zero Clear
  summary and empty table while transient parent badges displayed `-`; acceptance now
  asserts the destructive native result plus disappearance of the B row instead of a
  presentation glyph.
- `recovery-debug-clear-en-light.json/.png/.xlsx`: first end-to-end green diagnostic;
  superseded because its recovered-status/registry/export sampling was less strict.
- `recovery-accepted-en-light.json/.png/.xlsx`: first strict accepted-flow run;
  superseded only because shutdown exposed SQLite pooled file handles. The finalizer
  now releases library-managed pools after service disposal, and the v2 packet records
  successful isolated-root removal.
- `recovery-accepted-v2-en-light.json/.png/.xlsx` and
  `recovery-accepted-ja-dark-narrow.json/.png/.xlsx`: later green packets that fixed the
  locale/theme and cleanup defects, but were superseded after independent review found
  that their isolated composition still disabled positive Auto/plunder execution,
  visited only Home/Map top-level routes and recorded aggregate rather than named event
  proof. They also predate the final managed Desktop binary.
- `recovery-v3-attempt1-en-light.json.error.txt`: first v3 run exposed that the
  reduced invalid-root proxy-status test seam hardcoded `gameRunning=false`; the seam
  was corrected to honor its injected `GameRunning` hook before the final v3 reruns.

No historical file was overwritten or deleted.

## Validation around the accepted package

The canonical Release Desktop build completed with zero warnings/errors and emitted:

`LWB317_PRODUCTION_UI_BUILD_OK 4686126fe21d843506b4938371a5532cdf9a12b4c67d32f87b2e67f35833783c d3f32d1de7108642278f841a274d9bd4dcf3f8ea3a65dec6b758eb45c1d10108`

`LWB317_PRODUCTION_UI_PACKAGE_OK` also passed during the same package build. The first
attempt was blocked by two repo-local Vite/esbuild helpers holding the repo's esbuild
binary; only processes whose command line belonged to this LW-Control checkout were
stopped, then the canonical build was rerun successfully. No owner game/browser
process was touched.

Focused checks after the final implementation:

- Desktop checks build: PASS, 0 warnings / 0 errors.
- `--profile-runtime-owner-check`: PASS (includes registry acknowledgement/rollback and
  production owner A/B/A checks).
- `--home-campaign-lifecycle-check`: PASS.
- `--map-auto-scan-campaign-check`: PASS.
- `--map-campaign-canonical-check`: PASS.
- `--game-root-select-check`: PASS.
- `--overview-bridge-host-transport-check`: PASS.
- `--overview-bridge-normal-composition-check`: PASS.
- canonical `tests/LWBridge.Map-0.3.17.Checks`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS, including 470 referenced
  locale keys across nine 1,383-key catalogs.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`: PASS with the same production
  source/artifact fingerprints above.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`: PASS.
- Legacy WIP archive guard: PASS (`exactFiles=10`).
- Reference 0.3.17 executable hash guard: PASS,
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `python tools/check_current_client_compat.py`: PASS for policy
  `lwbridge-current-client-critical-anchors-3`, content version 22, no problems.

## Bounded external limits

This closeout does not promote protected/current-client state-changing actions to
live proof. Treasure claim/status and Ghost plunder preparation remain intentionally
unavailable at the production provider boundary. Positive current Railway/Ghost/
Treasure population remains live-state/population gated. Those are external/bounded
limits recorded by the recovery work item, not reasons to fabricate success or run an
owner session.
