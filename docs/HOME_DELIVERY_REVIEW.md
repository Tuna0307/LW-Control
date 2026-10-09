# Home application delivery review — 2026-10-09

## Result

A clean application candidate based on main builds and publishes independently of the large research archive. This closes a delivery gap: main previously contained only its initial README. The application is runnable; full original Home/Map parity is still incomplete.

## Corrections

- Isolated capture/replay modes previously dereferenced a null production configuration root. Exact old packaged capture exited -1073741819 without an image. They now receive their own temporary config/browser roots; packaged Home capture exits 0, emits a decoded image and actual page diagnostics, and removes temporary state after WebView disposal.
- Capture diagnostics referenced the retired LWBridgePreview global and silently wrote an empty JSON file. The capture now records DOM route/Home presence and actual page error/unhandled-rejection events. This is explicitly fixture boot proof, not positive live functionality.
- Actual backend startup dispatch bypassed ordered profile reconciliation, ignoring registry disabled/locked admission. It now goes through the registry composite before the selected lifecycle. Reintroducing the old routing compiles but fails the new actual-backend assertion; corrected routing passes.

## Real mounted Home observations

Normal production WebView, explicit independent data root, disabled local profile. An unexpected Last War process was observed during the pre-fix mounts through the routing defect (the total launch count was not instrumented); the native helper timed out, the game exited and all three script hashes matched its preserved originals. This launch is recorded, not called an intended pilot or excluded from history. No gameplay/scan/claim/jump action was used.

After correction, startup remained stopped. Real controls selected English, changed Auto Launch true -> false and Automatic Reconnection false -> true. Actual config persistence was checked. Closing/reopening retained both values and the English locale; subsequent actual controls selected Japanese and dark mode. The two screenshots show these mounted states. No browser-console instrumentation was attached to these normal windows; do not infer zero console issues from screenshots. The separate fixture smoke test records zero page errors and clean temporary-root disposal.

Native root-selection tests exercise cancel, invalid/no-save, valid/save, normalization, unavailable state and lifecycle root ownership using inert seams. The normal installed-game root was detected successfully, so the recovered renderer hid its missing-root picker. Actual OS folder-dialog interaction was not tested; no full picker/UI acceptance is claimed.

## Executed gates

- Fresh candidate Desktop + focused Checks Release build: zero warnings/errors.
- Small actual-backend game-root and ordered-profile checks: pass; all provider processes inert.
- Canonical npm check: all five groups pass, including all nine 1,383-key locale catalogs.
- Fresh candidate production frontend build/package fingerprints verified.
- Fresh publish from clean candidate: succeeds.
- Packaged capture smoke at build and publish outputs: pass, zero new temporary capture roots.
- Old dispatch mutation detected; source restored and corrected build rerun.
- Script restoration independently matches 2187DE71...CFAC0 / 988C36BD...7306 / 535FA30D...F790. No remaining LastWar/launcher process.

## Acceptance limit and next feature

This candidate can demonstrate actual Home preferences and a working application shell. It does not certify original native-mirror failure rollback, all Home lifecycle paths, protected controllers or exact original scanner algorithms. Existing four Map lead findings remain open; no evidence has been discarded or reclassified as a pass.

Next feature: canonical Home Launch -> Connected -> Close with exact process exit/restoration and original error/timing comparison. Deliver that as one feature, then City/Resource as another. The other worker remains stopped until a new owner-relayed assignment.
