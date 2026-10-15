# MAP-014 clock correction — independent lead acceptance, 2026-10-15

Product reviewed: `e6cafd5257e1c900bbd64785c4432f58e2d69272` on existing
`codex/map-owner-fixes-014`, draft PR #10. Source, direct remote and PR head
matched at review start; no tracked changes were present.

**LEADMAP014-FINAL-01: ACCEPTED.** The Railway/moving-Monster table clock now
refreshes arrival eligibility. Independently rerun actual-source mounted tests
pass deadline disabling, active Follow Stop, unknown arrival, stale-click
rejection, tab/profile retirement and timer disposal. No additional product
defect was found in the two correction commits reviewed after `381318a8`.

## Verification performed by lead

- Fresh current-source Release build: zero warnings/errors. Full inert suite
  passes, including the new Truck witness classifier and Home/Map regressions;
  terminal `HOME_FEATURE_DELIVERY_CHECKS_OK`. Full `npm run check` passes,
  including `LEADMAP014_FINAL_01_MOUNTED_CLOCK_PASS` for both moving kinds.
  Logs: `artifacts/map014-lead-finish/lead-review-e6cafd52-*`.
- Review ZIP: 10,861,037 bytes, 81 entries, SHA-256
  `b7009eb7cd00e91e5c029670f9f8270519953f74fbdaa1898b4afe2324b58a04`.
  CRC, membership and every manifest hash pass; no detected private DB/profile/
  original-script paths. Extracted files match. EXE ProductVersion identifies
  e6cafd52; extracted production UI validator passes.
- Read-only expiry calculation over all 193 genuine retained Trucks reproduces
  38 eligible at the earlier `2026-10-14T16:17:54Z` successful proof, then zero
  at both later failed observations. No rows/timestamps were edited.
- Final extracted run `native-ui-20261015-011725` genuinely loads Other controls,
  Resource filters and Dispatch names, then exits 1/proof=false with
  `TRUCK_POSITIVE_WITNESS_UNAVAILABLE: no_unexpired_truck_rows`. It is NOT an
  eight-gate pass. The previous native-image positive remains dated historical
  evidence; no image renderer/native transport code changed in these commits.
- The observer correction classifies absent source without turning it into a
  pass; positive native icon sources still require decoded images. Exact native
  Stop/restoration receipts remain preserved. Fresh read-only installed hashes
  match all three v26 originals; no game/app was running at review start.

## Delivery decision and remaining scope

The correction is accepted and the source-pinned local RC is **READY FOR OWNER
TESTING of the available passive Map scope**, with the explicit current-Truck
witness limitation above. A new acquisition is needed for current Truck rows;
expired retained results must not be presented as missing native image assets.
No repetitive full-world scan is required merely to accept this presentation-
clock fix and unchanged image pipeline. Final available-scope evidence combines
the scoped historical genuine positives with the current tests and partial
extracted replay; it is not a fresh full-catalogue live certification.

Whole Map remains PARTIAL: authenticated Ghost execution and Treasure actual
claims/aggregate quotas are unavailable; rare/event/Train terminal positives
remain unwitnessed; Dispatch/Truck plunder and sharing are owner-only live tests.
Optimization beyond the selected tested routes is deferred under the owner's
15–20-second future wishlist, not a new current correction gate.

No live game launched, no account action, no product edit and no new ZIP in this
review. This documentation commit does not change the e6cafd52 package identity.
PR #10 remains unmerged; no main merge or publication is authorized/performed
by this acceptance. Future repository cleanup remains deferred until Map work
is closed under the owner's agreed scope.
