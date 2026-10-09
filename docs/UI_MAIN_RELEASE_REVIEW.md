# LWB317-UI-MAIN-DELIVERY-001 — release candidate review
Date: 2026-10-09. Scope: accepted recovered-source/local post-auth UI; no game/native-positive test.

## Authority and release gap (A)
- Approved UI source/local checkpoint: research commit `75b7b215679a8ea7986af72bdbdd7039c5278b70`, reviewed in `docs/reviews/2026-10-06-LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2-LEAD.md` in research checkout. The review accepted 282 assertions, 16 decoded complete-App screenshots, original source/contract and nine locale catalogs, **not** full protected-runtime pixel parity.
- Candidate was clean at `15a90ae94959ff5b924b1ee25f43dec9fd703dd0`, PR #3 draft against main; imported production source from research snapshot `c8acbf38fa387bc3b8236d0615a6cc832eb4478a`.
- `python tools/audit_ui_delivery.py`: 82 frontend files at accepted checkpoint, 88 at imported snapshot; 16 post-checkpoint changed/added UI and integration files. 86 imported frontend paths (including image assets) match the release candidate (LF-normalized text). The only 2 deliberate import differences are `package.json` and `package-lock.json`, adding a **dev-only** Playwright verification gate; no original production UI file or binary asset is missing/divergent. The added edits focus on native Home status and profile ownership, startup, Map auto status, root/status wiring, theme, and focused assertions. The untouched page source remains accepted by the earlier reviewed source/local contract; later native state producers are not thereby certified.
- Relevant original-byte locators remain on the research branch; for example original `index-BVfnK1wp.js` AutoLaunch reads/writes at UTF-8 bytes 246750/246758, 247632, 248381, 338639; theme at 364550; Equipment shared Save original `fd.P` at byte 180274. Do not replace source authority with fixture screenshots.
- Missing *delivery* dependencies closed: independent application source/build targets, packaged frontend, Win-x64 native WebView2, runtime libraries, bundled Python/Lua helpers, checked launcher and extracted release ZIP. No frontend runtime requires the untracked research checkout.
- **No demonstrated new presentation/local-interaction mismatch** against reviewed source and bounded replay was found. Existing capture isolation/cleanup and ordered profile startup registry dispatch were retained. The one transient Desktop build EPERM was a test Vite port holding esbuild; after closing that owned listener, a clean build passed. This was not a product UI regression.
- Unavailable native producers/assets, original protected-runtime visual states, true connected/game launch lifecycle, exact Map scanning, updater/native actions and remaining source-unknown error paths stay separate; preview fixtures do not upgrade these.

## Page/state exercise (B)
Actual Vite **production build** served to installed Microsoft Edge Chromium via `npm --prefix src/LWBridge.UI-0.3.17 run check:release-ui`. The script uses no game/native providers. Captures stored *outside Git* in `%TEMP%\lwbridge-ui-release-proof\` (26 screenshots plus `coverage.json`); representative decoded images manually examined. All rows: nav selected, nonempty main view, correct locale/theme, no whole-document horizontal overflow.

| Page/shell | EN/light 1365 x 900 | JA/dark 640 x 840 actual narrow viewport | Representative conditional state (fixture) | Limit |
|---|---|---|---|---|
| Home | Pass | Pass | connected | No native lifecycle-positive proof |
| Map | Pass | Pass | city results and error; tab/scan shell | Scans not executed; data synthetic |
| Automation | Pass | Pass | save error | No live automation actions |
| Squads / Equipment | Pass | Pass | rename modal | Fixture hero/equipment; original explicit draft/save semantics from accepted packet |
| City Layout | Pass | Pass | populated layout | Fixture grid, no native publication |
| Hotkeys | Pass | Pass | save error | Native keybinding/mapping not certified |
| Mini Games | Pass | Pass | conflict | No gameplay/execution |
| Settings | Pass | Pass | updater error | No updater or protected access |
| Shared shell | Pass | Pass (stacked layout) | exit busy modal | Locale/theme switch + reload, route return, server-jump popover open/close; no server jump performed |

Playwright complete session reports 0 `pageerror`, console `error` or `requestfailed`; 16 route screenshots and 10 conditional-state screenshots. Dialog remained modal in busy state, correctly intercepting navigation. One test was corrected to assert the modal rather than clicking behind it; no product change. Theme and locale persistence reloaded in both configurations. These are *real mounted browser production App* states, not native producer-positive proof.

## Native and package gates (C)
- Canonical npm check: five groups pass including all nine 1,383-key language catalogs.
- Fresh `npm run build` and `check:production-build` pass; frontend build/package fingerprints match.
- `dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release` and focused `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release`: 0 warnings, 0 errors.
- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: `HOME_FEATURE_DELIVERY_CHECKS_OK`; actual-backend ordered profile reconcile checked, game launches 0; inert providers.
- `pwsh -NoProfile -File tools/check_packaged_home.ps1`: actual packaged exe captures a decoded fixture Home PNG + diagnostics, exit 0, zero browser-page errors and zero residual temporary capture roots.
- `dotnet publish ... -c Release -o artifacts/application` PASS with independent runtime assets.
- **Normal published app**: `python tools/verify_normal_release.py start artifacts/application/LWBridge.Desktop.exe` using an independently created isolated config + SQLite controller.db: installed native app window titled `lwbridge` exists and stayed running, its selected profile had `enabled=0`, `locked_reason=RELEASE_UI_SMOKE`, `gameDesiredRunning=false` while shipped `autoLaunchGame=true` remained unchanged. Observed game processes before/after both empty; no game launch. WM_CLOSE of **owned** PID completed; registry stayed disabled, zero new game processes. Receipt in `%TEMP%\lwb317-normal-ui-disabled-sxgfk1xy\normal-smoke*.json`.
- Native offscreen Win32 PrintWindow/ImageGrab returned a blank compositor surface, so **not** proof of pixels. A Windows-MCP `App switch` request encountered active-user control and UIA transient errors; did not override desktop gate. Browser screenshots and actual exe fixture capture are independently valid but not a new normal-WebView2 GUI screenshot. Original runtime native asset/content visuals remain pending.
- Production npm runtime dependencies audit (`npm audit --omit=dev`) reports 0 vulnerabilities at this check. Playwright is a dev-only verification dependency (installed Edge required for this optional Windows browser gate), not shipped in release ZIP.

## Non-goals and lead decision
This is a **usable UI baseline** in the existing release branch, not a fully functioning A-to-A 0.3.17 clone. Original login/licensing UI deliberately excluded. Do not claim full protected original pixels, game/client compatibility, Home lifecycle, Map producer parity, updater/protected-service access or working native features merely because a fixture renders. The lead reviews and merges; no worker merge or GitHub Release publication.
