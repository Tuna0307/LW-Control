# Lead final Map qualification — 2026-10-25

**Map's required local, read-only qualification gates pass.** Source review,
implementation, genuine packaged Manual/Auto all-ten, Stop, Clear/reopen and
bounded Home integration smoke are complete. This accepts the tested Map scope;
it does not establish universal performance, reproduce every seasonal population
or resolve the separate intermittent controlled Home assertion described below.
No main merge or public release occurred. The retained runtime receipts are dated
2026-10-24; this closeout was refreshed on 2026-10-25.

The authoritative sanitized receipts are in
[LEAD_FINAL_QUALIFICATION_EVIDENCE.json](evidence/2026-10-25-map-final-qualification.json).
Earlier journal-blocked and incomplete-package statuses are historical.

## Final source and package

- Product source: `2b32d64f4a531ec8a3b8c61926da358d0d96f0c4`.
- Branch: `codex/lwtool-full-improvement`; draft PR #16.
- ZIP: `artifacts/map-lead-finish-20261024/cancel-final/package-final/LW-Control-lwtool-improvement-2b32d64f4a53.zip`.
- SHA-256: `ff2cb0189288638a21a00121d07d9ab6f7448cbd0e8a65fabf4f40c99cf8b763`.
- 10,377,389 bytes; 105 ZIP entries, including root integrity manifest;
  104 application files; 29 linked bridge identities verified.
- ZIP CRC, payload hashes/sizes, source pin, production UI fingerprint and
  privacy exclusions passed. Each Map case used a separate clean extraction.
  Documentation-only follow-ups do not change this product pin.

## Useful recovered lessons and actual changes

LwTool's local scanner uses a different request layout and owner-detail batching,
deduplication and provisional UI delivery. Our existing direct point source uses
fewer point requests and our typed mobile sweep covers more than its filtered
normal Truck route. We retained complete local acquisition and the qualified
20 requests/s, fence25 settings instead of copying constants or reducing coverage.
LwTool's remote scanner service is not integrated or required.

The worker's transactional batch staging and early seen-UUID March collector
guard remain. Independent finish testing also corrected native data and Stop issues:

1. **City main-base classification.** Native point type 6 includes buildings
   beyond the main bases returned by the game's canonical owner index. The
   cross-check now follows the native main-base item contract. Known non-main
   items are classified separately; missing metadata and unresolved actual main
   bases still fail closed. No missing City row is fabricated or silently unioned.
2. **Resource reply identity.** The native detail cache is keyed by point ID,
   which can collide across servers. Every required detail now gets a fresh
   request. A private observer matches the native `_id`, point, owner/run and
   connection configuration/proxy, preserves the original handler, freezes the
   matching response and refuses old cache values. The outgoing argument order
   follows the recovered v27 signature. Only missing replies receive bounded
   repairs; superseded request IDs are retired. Two eight-second repair windows
   follow the initial eight-second window; the host deadline is 28 seconds.
3. **Resource Stop acknowledgement.** The host cancels the exact detail request
   and waits for native confirmation that the private retry lane and observer
   have retired. Stop is checked before a due repair. A late cancellation after
   completion can acknowledge the exact already-retired request; a foreign run
   cannot cancel its successor. The adverse timing cases are actual-source inert
   tests; the genuine packaged Stop gate interrupts the typed Truck sweep.

The current-version official-settlement auditor was also completed. It selects
the exact trusted v27 settlement and verifies its original bytes and prior owned
restoration, rather than comparing against a hardcoded older v26 marker.

## Genuine final-package gates

All cases below ran from the final source-pinned ZIP and were independently
re-audited against the exact extraction used by that case.

| Gate | Retained run | Result |
|---|---|---|
| Manual all-ten | `native-ui-20261024-121316` | 2500/2500, zero failed blocks; direct 63 replies/10,000 AOIs; complete publication and browse |
| Auto all-ten | `native-ui-20261024-121607` | Actual Run Now, 2212→2211→2212; complete remote publication and saved-data browse |
| Mid-flight Truck Stop | `native-ui-20261024-121908` | Native cancellation, durable cancelled run, no staged/publication leakage |
| Resource saved-data Clear/reopen | `native-ui-20261024-122019` | Fresh home acquisition, remote 2213 scan, exact return, Clear and both views reopened; 8,008 home rows preserved |
| Home integration | `cancel-final/home-smoke-final-2b32d64f` | Two route recoveries in the same exact game; Stop during reconnect; no successor for 15 seconds |

Final Resource cohorts received 7,945/7,945 home details, 7,963/7,963 on the Auto
all-ten target and 7,972/7,972 on the saved-data remote target. The last cohort
ignored **67** existing native cache entries and obtained fresh correlated
replies instead. Missing-only repair counts were 4, 2 and 4 respectively.
All final cases established exact owned exit and original-script restoration.
The final Home controller also closed its isolated host.

Some selected kinds were genuinely empty. Final all-ten selections had no
Railway, Other, Ghost or Zombie Boss rows at that availability point. The earlier
dd6bf881 home Railway positive and other accepted populated receipts remain
historical evidence, never relabelled as final-source positives.

## Checks and performance boundaries

- Full Release Desktop checks passed through `HOME_FEATURE_DELIVERY_CHECKS_OK`
  on final source; 22 native Resource source, 10 hydration and 15
  per-row provenance rejection cases passed.
- Eleven actual-source inert Lua correlation/cancellation tests passed, including wrong/missing
  reply ID, wrong point/server, connection reset, retired run, hook replacement,
  frozen results, superseded repairs, bounded exhaustion, Stop before a due repair,
  early Stop, foreign Stop and the completion/cancellation race. Nine managed
  cancellation identity/retirement inverses also passed.
- Two City source/syntax checks and 31 package-auditor tests passed.
- All 49 inert Lua suites passed on the final `2b32d64f` Lua source.
- Canonical production UI build/fingerprint and package integrity checks passed.

The retained 24,362-record storage benchmark is **2.484→0.673 seconds median**
with identical canonical hashes. Collector-only gains are inert slice evidence.
Neither establishes a causal whole-scan speedup. Historical all-ten direct versus
camera ABBA remains historical; it is not a new before/after comparison of these
correctness fixes. LwTool's claimed few-second fresh full-world scan remains
unverified and is not an acceptance target for this local implementation.

## Retained failures and follow-up

- Initial native-ID candidate received 7,934/7,936 details and refused to publish;
  bounded repair was then implemented and qualified.
- One Auto run saw native `server_changed`, refused partial publication and
  restored the exact originals. The later instrumented run and final-source Auto
  run passed. The trigger for the earlier external context drift is not established.
- One full controlled Desktop run failed the existing HomeR2 late-helper Stop
  assertion while publishing/build work was concurrent. **The cause is unresolved.**
  The same source passed the isolated full rerun, and both genuine bounded Home
  smoke runs passed. Retain `final-desktop-checks.log` and the passing
  `final-desktop-checks-r2.log` under the ignored finish workspace. This is a
  separate follow-up; do not claim that every Home race or every random disconnect
  is fixed from the Map evidence.
- The first cancellation draft exceeded the native 65,535-byte encoded-module
  limit and was rejected by the existing package guard. Equivalent compact
  control flow and moving a preserved inline comment to its own line brought
  the final module to 65,493 bytes. The guard and package round-trip stay enforced.
- Test setup negatives (unsupported internal Home view, generated bytecode in a
  warmed extraction, and re-auditing against another case's extraction path) were
  corrected without weakening package or restoration checks. Earlier evidence
  and failed setups are retained.

Private recovered source, account rows and raw runtime receipts remain ignored.
The pre-existing `.claude/` directory was preserved. Map has no remaining required
gate in this finite scope; the Home assertion above remains an explicit follow-up.
