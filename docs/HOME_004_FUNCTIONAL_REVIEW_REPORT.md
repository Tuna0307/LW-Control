# HOME-004 — functional review report (2026-10-10)

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
| F-04 automatic reconnection | Prior genuine exit/hung and Stop receipts; controlled precise transport-only offline threshold and recovery; D-10 fixes ID-less pending recovery sidebar Stop/Stop All. | Responsive real game whose authenticated transport alone is absent for 60s: observe OFF/ON replacement and Stop without breaking current installation. |
| F-05 adoption | Prior genuine same-build adoption; wrong-owner/stale/obsolete identity, restoration and reentry regressions pass. | No new blocker identified. |
| F-06 repair | Native exact journal restore/relaunch plus own-design no-journal helper path and authenticated success requirement, tested controlled through the actual Home button. | A safely backed-up genuine supported bridge-damaged installation must be repaired via packaged Update-and-Launch and reach a fresh authenticated Connected state with exact restoration. |
| F-07 profile sidebar | Four local SQLite slots, Add/Delete, **local enable/disable** (D-11), note, reorder, select/focus, collapse, per-owner play/stop, Start/Stop All, status/error/busy and safe deletion pass native and packaged EN/JA inert tests. Disabled profiles are not admitted to Start; the real packaged UI toggle persists through native SQLite and can re-enable B without changing selected A. | Two separately compatible real Last War installations and simultaneous independently authenticated game sessions are not available for final confirmation. |
| F-08 preferences/status | Native/local save and rollback, Home statuses/translation, profile independence, retirement and late replies pass. | Live feedback for the two F-04/F-06 genuine conditions awaits those tests. |
| F-09 package | Source-identified ZIP, extracted native/WebView Home EN/light and JA/dark, inert capture, clean shutdown and real isolated named-pipe RPC pass. | Final Home gate acceptance depends on F-04/F-06/F-07. |

## Current source, package and validation

- Functional source: `74bcfa49779182c40690cceedcc23ae20d4812a1`.
- Windows review ZIP: `artifacts/release/home004-functional-74bcfa49/LW-Control-HOME004-FUNCTIONAL-RC-74bcfa497791.zip`.
  SHA-256: `3D83D14778860AAA3E568AD236428C6C8B5D4B1A69139AB1224A8DA188DAF945`.
  Extracted `SOURCE-COMMIT.txt` and executable ProductVersion match; EXE SHA-256
  `CC74D99AA2637113C9388FE770595EFE7E363B19DC22A9F4C5118F22D1369260`.
- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: PASS.
  `npm.cmd run check`: PASS (five frontend groups, nine locale catalogs).
  Release Windows publish/canonical frontend manifest check: PASS.
  `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release`:
  PASS, real isolated Windows named pipes, 30-second idle retirement, fresh RPC,
  concurrent authenticated A/B routes, correct rejection and shutdown.
- Extracted application inert WebView receipts:
  `artifacts/home-004/functional-74bcfa49-en-light.json` and
  `artifacts/home-004/functional-74bcfa49-ja-dark.json`: both PASS including
  actual sidebar enabled toggle → native persisted value → denied B Start →
  re-enabled B, with A selection preserved. Zero genuine
  game launches/Map scans, zero active requests/subscriptions, both profiles
  stopped, no cleanup failures. Extracted `check_packaged_home.ps1` screenshot
  `artifacts/home-004/functional-74bcfa49-capture/home.png`: PASS; zero temp roots.
- Historical negative: first `e3ab51d3` extracted EN mounted proof failed
  because the original positional Edit Note test clicked the new enable icon;
  this was a proof-selector regression, fixed in `74bcfa49`. The original
  `artifacts/home-004/functional-e3ab51d3-en-light.json.error.txt` is retained.
- Prior genuine current-client outcomes, original negative evidence, R4–R16,
  H-33 and former candidate remain intact. New controlled outcomes are not
  presented as genuine game results. Deliberate differences D-06–D-10 are in
  `HOME_004_BEHAVIOR_DIFFERENCES.md` (D-06 through D-11).

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
