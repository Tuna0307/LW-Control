# MAP-015 lead audit — 2026-10-16 (source 26212916, draft PR #11)

Verdict: **Task A ACCEPTED for owner testing scope; Task B NOT STARTED live; overall PARTIAL.**

## Independently verified by lead
- Remote branch SHA 26212916 matches; working tree clean.
- ZIP `artifacts/release-map015-final/LW-Control-MAP015-RC-26212916bc20.zip` SHA-256 B789C282…E779 matches.
- `tests/LWBridge.Desktop.Checks` (not run by worker; lead ran it): all pass incl. new MAP015 drift checks,
  HOME_FEATURE_DELIVERY_CHECKS_OK. Python suites (map_014_lead_native 46 tests, map_015 package 4 tests) and
  Ghost reward Lua check pass.
- Report evidence paths exist; numbers are internally consistent.

## Findings
- LEADMAP015-01 (accepted): camera-free scan works in the packaged app, falls back to camera safely, Stop and
  camera/AOI restoration proven. Single kinds 17–27 s (was ~200 s); all eight non-Truck kinds 64–70 s vs 226 s
  camera in the same session. Same-session key sets match within normal world drift (City 1/4,176 each way).
- LEADMAP015-02 (open, speed): all-kinds and Zombie Boss time is dominated by serial Monster protection/detail
  hydration (~24–26 s) after the 18–26 s sweep.
- LEADMAP015-03 (open, Truck): no live attempt at the fast Truck path. Lead's measured data: pushes arrive one per
  request; the client keeps the latest march set until the next push is processed, so sampling every frame with
  request rate **below the frame rate** should not lose pushes. The worker stopped at a static argument.
- LEADMAP015-04 (open): Ghost plunder-share not witnessed live in the packaged UI; Railway one-record parity gap
  (sparse moving object, low priority).

## Next assignment (R2), same branch/PR #11
1. Truck: implement the 40×40-tile, one-area overlapped Truck sweep (findings doc) and read the march set every
   frame (`GetAllMarchesByCS` + `train`, also `_logMarchUuids1`). Measure 5/10/15/20 requests per second, each with a
   same-session camera Truck round; pick the fastest rate whose Truck keys are no worse than camera-vs-camera drift.
   Ship only if it qualifies; else keep camera Truck and document the measurements.
2. Hydration: make Monster/Zombie Boss protection-detail hydration concurrent/batched or overlapped with the sweep,
   without dropping fields. Targets: all non-Truck ≤ 40 s, Zombie Boss ≤ 30 s.
3. Packaged UI: Ghost plunder-share witness on a live Ghost row; Truck filter on fresh Trucks.
4. One final ZIP from the final commit, report update, status READY_FOR_LEAD_AUDIT or PARTIAL.

## R2 audit — source 7e93b92f (lead)
Verified: remote SHA 7e93b92f; ZIP `artifacts/release-map015-r2-7e93b92f/LW-Control-MAP015-RC-7e93b92f42d0.zip`
SHA-256 3ffc29da…e105d matches; lead re-ran Desktop.Checks, map_014_lead_native, map_015 package and Ghost reward
checks: all pass.
- LEADMAP015-02 ACCEPTED: Zombie Boss 51→34 s, all non-Truck 64→46 s with full fields; close enough to targets for
  owner testing.
- LEADMAP015-04 CLOSED: packaged Ghost plunder-share witness and fresh Truck filter witness passed.
- LEADMAP015-03 OPEN but de-risked: all 625 pushes captured at 5–20/s; 40–134 s. Native train set (98–108) vs
  camera (40–55); camera-only misses 16–26 are below camera↔camera drift (33–47). Missing: Truck vs Railway
  `train.type` classification and full production Truck fields.

## R3 assignment (final) — same branch/PR #11
1. Productionize the fast Truck sweep at 20/s (fall back to 15/s if pushes are lost): on every frame run the
   existing production Truck record builder (`train_march_aoi_records` path: rewards, owner, quality, power,
   arrival) on newly seen train marches; classify by `train.type` so only Trucks become Truck rows (Railway rows
   stay with Railway). Camera Truck scan stays as automatic fallback (lost pushes, errors, zero rows).
2. Qualification: same session, order fast → camera → fast. Pass when fast-vs-camera misses ≤ camera↔camera-style
   drift (fast-vs-fast pair as the drift control) and every fast Truck row has the same fields as camera rows.
3. Packaged app: Truck-only scan and all-nine-kinds scan, Truck filter, Stop mid-scan, restoration.
4. Final ZIP, report update, status READY_FOR_LEAD_AUDIT or PARTIAL.
