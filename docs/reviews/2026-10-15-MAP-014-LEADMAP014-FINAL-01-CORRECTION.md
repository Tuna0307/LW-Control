# MAP-014 lead review follow-up — LEADMAP014-FINAL-01

Review input: `docs/reviews/2026-10-15-MAP-014-19b818c5-LEAD-REVIEW.md`,
starting revision `381318a842aae0d3c7293212d22595f5d0cd5c6a`.
**Disposition: corrected for independent available-scope review, not whole-Map
acceptance and not merge approval.** The original lead reproduction and all
previous native receipts remain unmodified.

## Confirmed defect and narrowly scoped correction

`MapDataPage.jsx`'s mounted `MapTable` already refused new Follow on a known
native `arriveTs` for Truck, Railway and moving Monster, and the parent click
handler independently checked `Date.now()`. But the table's one-second visual
clock previously ran only for Truck/Dispatch/Ghost. Railway and moving Monster
therefore kept an enabled Follow after the stored deadline: a silent no-op
when clicked. The independent controlled reproduction is retained under
`artifacts/map014-independent-review/expiry-clock-gap*`.

The table clock now runs when its visible `kind` is Railway or Monster, in
addition to Truck, Dispatch and Ghost. It resets on entering a kind and clears
the interval on leaving/unmounting. There are **no changes** to scanner
discovery, native navigation, identity, backend, game scripts or action
execution; a stationary Monster continues to use coordinate Jump rather than
the moving Follow control.

`scripts/check-map014-arrival-clock.mjs` mounts actual bundled React
`MapDataPage` with inert native interfaces, separately for Railway and
moving Monster. At controlled time 1,800,000,000,000 and native arrival
1,800,000,005,000, it verifies:

- Each active moving-kind tab registers a one-second clock, then disables
  **new** Follow on crossing a known arrival deadline.
- The parent independently rejects an expired click before visual refresh:
  zero native requests even if an enabled stale element is clicked.
- An **already active** Follow retains its visible enabled **Stop** after
  the table clock crosses arrival; pressing Stop retires the three-second
  polling timer without issuing any new native request.
- A native **unknown arrival** remains Follow-eligible and relies on current
  native provider validation; a stationary Monster retains coordinate Jump.
- Tab retirement clears both one-second visual and three-second Follow clocks
  and fences a captured stale poll callback; profile replacement retires the
  old owner's Follow and time callbacks; unmount leaves **zero timers**.

The distinguishing mounted test is registered in the main frontend
`npm run check`, including the pre-existing EN/light desktop and JA/dark
mobile owner tests. It is a **controlled mounted UI regression**, not evidence
of a naturally spawned Railway or moving Doom Walker.

## Acceptance / packaging boundary

All existing genuine full-world native scan, retained 27,205-row publication,
Dispatch owner-query, City FOV20 identity, Truck Follow and real Stop receipts
are reused at their exact scope because this patch changes no acquisition
or game action source. The new extracted Windows candidate and its source SHA,
ZIP member hashes, WebView browse and process/script cleanup are separately
recorded in ignored `artifacts/` and the updated draft PR #10.

The standard-world City-only FOV20 route remains qualified narrowly to the
source-matched trials; all other kinds and mixed selections keep guarded
shared serial LOD0. Dedicated authenticated Ghost plunder and real Treasure
quota/claim/reward providers remain unavailable. Missing natural Party,
moving Doom/Train/Ghost positives, positive-staged live Stop and owner-only
Dispatch/Truck account-changing actions remain pending. Never merge or
publish on this correction.
