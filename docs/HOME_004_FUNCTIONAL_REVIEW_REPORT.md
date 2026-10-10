# HOME-004 — functional review report (2026-10-11)

**Decision: PARTIAL.** The revised owner objective is a faithful, reliable
functional replacement of LWBridge 0.3.17 Home/sidebar. Exact original byte,
ticket and pixel parity is not a prerequisite. The lead decides acceptance,
main merge and publication; draft PR #6 remains unmerged.

## What works and what is still unverified

| Gate | Current functional evidence | Remaining operation |
| --- | --- | --- |
| F-01 folder | Root discovery/select/cancel/invalid/save; picker on valid root; staged owner safety proven in native and packaged inert WebView. | Select and operate independent second genuine supported root for concurrent profiles (F-07). |
| F-02 manual lifecycle | Prior genuine Start/Connected/Close; native exact session stop, pending recovery cancellation, per-owner concurrency and packaged controls pass. | No separate new genuine game launch performed in this continuation. |
| F-03 auto launch | Durable preference and native ordered startup reconciliation, OFF, enabled/locked admission and sidebar batch controls tested. | Two live independent startups depend on F-07. |
| F-04 automatic reconnection | Prior genuine exit/hung and Stop receipts; D-10 fixes ID-less pending recovery sidebar Stop/Stop All. D-12 additionally fixes the production monitor using fresh heartbeat alone when the authenticated pipe is absent. Controlled exact still-responsive/fresh-heartbeat + missing authenticated route: OFF preserves owner, ON triggers at 60s, restores old owner, launches connected successor and Stop cleans up. | Responsive real game whose authenticated transport alone is absent for 60s: observe OFF/ON replacement and Stop without breaking current installation. No protected-runtime or networking fault was induced. |
| F-05 adoption | Prior genuine same-build adoption; wrong-owner/stale/obsolete identity, restoration and reentry regressions pass. | No new blocker identified. |
| F-06 repair | Native exact journal restore/relaunch plus own-design no-journal helper path and authenticated success requirement. D-13 closes a no-op reporting bug: missing/false post-repair target plus zero restarted selected owners now yields a visible failure, including other-owner-only restarts. Production JSX callbacks executed against controlled outcomes. | A safely backed-up genuine supported bridge-damaged installation must be repaired via packaged Update-and-Launch and reach a fresh authenticated Connected state with exact restoration. |
| F-07 profile sidebar | Four local SQLite slots, Add/Delete, **local enable/disable** (D-11), note, reorder, select/focus, collapse, per-owner play/stop, Start/Stop All, status/error/busy and safe deletion. D-14 corrected missing Add/Delete command admission. **D-15 reproduced and fixed false `PROFILE_GENERATION_RETIRED` after committed SQLite Add and A→B switch.** Extracted EN/JA production WebView now passes actual Add/cancelled Delete/confirmed Delete plus independently correlated delayed Add and Delete acknowledgements while the display profile changes. Native/SQLite exact A/B roster, Stop and cleanup pass. | Two separately compatible real Last War installations and simultaneous independently authenticated game sessions are not available for final confirmation. These UI operations used inert profiles. |
| F-08 preferences/status | Native/local save and rollback, Home statuses/translation, profile independence, retirement and late replies pass. | Live feedback for the two F-04/F-06 genuine conditions awaits those tests. |
| F-09 package | Source-identified ZIP, extracted native/WebView Home EN/light and JA/dark, inert capture, clean shutdown and real isolated named-pipe RPC pass. | Final Home gate acceptance depends on F-04/F-06/F-07. |

## Current source, package and validation

- Functional source: `d09b8328ce1752cd6c305acae8f52acf827e6c87`.
- Windows review ZIP: `artifacts/release/home004-functional-d09b8328/LW-Control-HOME004-FUNCTIONAL-RC-d09b8328ce17.zip`.
  SHA-256: `0F871CDCADACBCF0C3685726608F7AA36D1B96744E42EFC8859801C16EA4FA43`.
  Extracted `SOURCE-COMMIT.txt` and executable ProductVersion match; EXE SHA-256
  `66AEE4C45FCA50156982827801712D341B89ACA3ED5EFEA6D318B48214CEDC39`.
- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: PASS.
  `npm.cmd run check`: PASS (five frontend groups, nine locale catalogs).
  Release Windows publish/canonical frontend manifest check: PASS.
  `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release`:
  PASS, real isolated Windows named pipes, 30-second idle retirement, fresh RPC,
  concurrent authenticated A/B routes, correct rejection and shutdown.
- Extracted application inert WebView receipts:
  `artifacts/home-004/functional-d09b8328-en-light.json` and
  `artifacts/home-004/functional-d09b8328-ja-dark.json`: both PASS including
  actual sidebar enabled toggle → native persisted value → denied B Start →
  re-enabled B, plus actual Add → 3/4 → cancelled Delete → confirmed Delete
  → exact saved A/B 2/4, with A selection preserved. Zero genuine
  game launches/Map scans, zero active requests/subscriptions, both profiles
  stopped, no cleanup failures. D-15 additionally checks exact native response
  IDs and durable Add/Delete acknowledged across A→B selection, native roster
  `2/4` and A restored; `nativeCrudAckAcrossSelection=true`. Extracted
  `check_packaged_home.ps1` screenshot
  `artifacts/home-004/functional-d09b8328-capture/home.png`: PASS; zero temp roots.
- D-15 distinguishing negative `artifacts/home-004/crud-switch-correlated-negative-en-light.json.error.txt`
  proves the old native dispatcher returned `PROFILE_GENERATION_RETIRED` after
  a committed Add and B selection. The original shared-response-slot test
  gave misleading output and was corrected before this negative. Corrected
  source-mounted `crud-switch-fixed-{en-light,ja-dark}.json` both PASS. No
  genuine LastWar operation was started to test this metadata race.
- Historical D-14 failure preserved:
  `artifacts/home-004/local-crud-proof-source-en-light.json.error.txt` and
  `local-crud-proof-diagnostic-en-light.json.error.txt` (backend command scope
  blocked Add before registry; UI/SQLite both remained 2/4). Corrected
  source-mounted `local-crud-proof-confirm-{en-light,ja-dark}.json` PASS.
- **Current genuine installed client:** `python tools/run_overview_bridge_current.py
  check-only` PASS, exact read-only v23 critical signatures and original
  `LWScripts.data` SHA-256
  `2187de71f426741eb61482f6e85639314526e6bdefc9445319b1ff52b31cfac0`,
  `installedFilesChanged:false`. This is **not a live game session**.
- D-12 native controlled adversity now covers a continuously fresh exact-session
  heartbeat while the independently authenticated route is absent, at the real
  60-second policy threshold; `gameLaunches=0`. D-13 executed production JSX
  tests ensure an empty/no-op repair or another owner's restart is an error.
- Historical negative: first `e3ab51d3` extracted EN mounted proof failed
  because the original positional Edit Note test clicked the new enable icon;
  this was a proof-selector regression, fixed in `74bcfa49`. The original
  `artifacts/home-004/functional-e3ab51d3-en-light.json.error.txt` is retained.
- Prior genuine current-client outcomes, original negative evidence, R4–R16,
  H-33 and former candidate remain intact. New controlled outcomes are not
  presented as genuine game results. Deliberate differences D-06 through D-15
  are in `HOME_004_BEHAVIOR_DIFFERENCES.md`.

## Live versus controlled acceptance

The **earlier** current-client genuine tests include real LastWar
Launch/authenticated Connected/Close, automatic startup, same-build adoption,
unexpected exit/hang, and pending recovery user Stop (including the original
negative and successful corrected follow-up). Those receipts predate the newest
transport-monitor, no-op repair and local CRUD corrections; they do not certify
them live. The **newest** Windows EXE/WebView/SQLite and real named-pipe tests
use inert profile/game effects, with `genuineGameLaunches=0`. The read-only
current-installation preflight verifies game-file compatibility, not a running
game connection. Never report F-04 still-responsive live pipe-only recovery,
F-06 actually damaged live bridge repair or F-07 two real concurrent game
installations as tested until genuine safe receipts exist.

## Precise unresolved dependencies

For F-04, a permissible way to observe a responsive current game losing *only*
its authenticated bridge transport is missing; existing hang/process-exit
cases and isolated named-pipe simulations were tested but cannot replace that
witness. For F-06, no approved disposable damaged game installation exists;
using a working owner installation or starting an official updater is outside
the established authorization, while exact journal/no-journal inert repair
alternatives have passed. For F-07, current available fixtures use two inert
profile owners; no second independently compatible actual client/installation
was supplied to test two genuine simultaneous games. These are required
functional verification dependencies, not unknown original-only details.

Keep the draft branch/PR for independent lead review. No merge, publication,
Map campaign, unauthorized networking, protected runtime bypass or updater action.
