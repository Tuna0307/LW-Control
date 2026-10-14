# MAP-014 independent scanner method audit — 2026-10-14

Scope: read-only inspection of the checked-in Map317 producer, selector, engine,
native Lua capture fields, qualification receipts and existing regression tests.
No game/app launch, shared release build, mutation of installed files, or new
native experiment. This is a source-supported per-selection decision for the
current product, **not** proof of a globally fastest method or complete server
census. The current worker handoff at
`docs/MAP_014_WORKER_RESUME_HANDOFF_2026-10-14.md` remains authoritative.

## Decision by selected kind

| Public selection | Active method | Qualification boundary |
| --- | --- | --- |
| **City alone** (standard 1000 x 1000, world 0) | **FOV20**, with measured native `_curViewIndex` AOI union and per-probe restoration | **Selected:** six exact-owned baseline/FOV20 rounds all found the same 4,114 City owner identities, 10,000 AOIs, zero missing/extra owners and a source-timestamped relocation. Median 227.912 s baseline to 86.179 s FOV20 (62.19% faster). Actual packaged City-only FOV20, UI and cancellation restoration were separately evidenced. No fixed worldwide count. |
| **Resource alone** | Conservative, camera-serial LOD0 full-world AOI discovery, native Resource identity reconciliation and hydrated detail | **Do not promote Resource FOV20:** six native paired rounds were faster (149.747 s to 73.370 s median) but candidate omitted 19/4/3 baseline IDs, had 25 candidate-only IDs in first pair, and lacks an independently contemporaneous per-ID creation/expiry witness. Sixteen first-pair baseline-only records were unoccupied and hydrated. Native AOI count equality cannot settle identity accuracy. |
| **Monster, Dispatch, Ghost, Treasure** (alone or combined) | The same guarded serial LOD0 AOI discovery, with source-specific enrichment and category validation | Neither the private bounded lens nor native group shortcut has source-authenticated same-session identity/field completeness for these kinds. Do not infer a live seasonal/Ghost positive from controlled fixtures. |
| **Truck and Railway** (alone or combined) | Guarded serial LOD0 AOI discovery; Railway additionally refreshes official `LWTrainDataManager.TryGetTrainList(true)` after AOI coverage and merges by UUID | **Do not promote FOV20/direct Train-list:** mobile March manager has distinct rectangle, response-handler, cache and queue lifecycle. The fast mixed trial lost 92/92/83 baseline Truck UUIDs, with 90/88/82 still future `arriveTs`; also 2/1/1 future-arrival Railway rows. A later sample of 60 untyped manager UUIDs had no native typed before/after processing or independent same-time source census. |
| **Other** (alone or combined with Treasure/other kinds) | Native request maps `other` to `treasure`, so one point discovery feeds both, followed by source-qualified subtype partitioning | Existing genuine Other-only Supplies and combined Treasure/Other receipts establish positive categorization, not any faster acquisition path. Unsupported native supplies types must not become guessed claimable Treasure. |
| **Any mixed or all-kinds selection** | **One shared** guarded full-world AOI discovery, then per-kind enrichment/projection over the same 2,500 logical blocks | No simultaneous independent per-kind camera scans are needed. FOV20 mixed is specifically rejected because the missing unexpired Truck identities have no qualified processing/expiry explanation. Multi-server work is sequential, owned server by owned server; no remote-without-context-switch native coverage claim. |

`Map014BoundedLensChecks` exhaustively asserts the production camera policy for
**all 1,023** nonempty subsets of the ten internal scan kinds: FOV20 is allowed
only for one City, including the `ObserveMarchFreshness` diagnostic hook case;
`ForceLegacyFullWorldLens` disables it. The separate
`ExperimentalBoundedCameraLens` opt-in is a private diagnostic exception and
must never be confused with public selection. See
`CurrentClientMapBlockSource.FastCity.cs:830-845` and
`tests/LWBridge.Desktop.Checks/Map014BoundedLensChecks.cs:51-64,101-119`.

## Actual scheduler versus strategy label

`MapScanStrategyPlanner` reports requested concurrency **8** for Normal and
**20** for Fast (`MapScanContract.cs:72-105`). Actual camera-acquisition work
is serialized in `MapScanEngine.cs:219-307` and the native full-world
`CaptureFullCityMapAsync` loop (`CurrentClientMapBlockSource.FastCity.cs:300-510`):
the engine awaits each progress batch and the producer awaits each native
camera move/probe. Fast changes adaptive camera placement, not the number of
simultaneously mutating cameras or concurrent game AOI requests. Neither
8 nor 20 is a proven camera request rate or parallel speed multiplier.

**Metadata mismatch worth retaining in review:** `MapScanStrategyPlanner.Plan`
labels any Truck/Railway-only selection `current_fast_train_list_v1`, but
`CaptureFullTrainListMapAsync` has **no call sites** in the checked-in desktop
sources (`CurrentClientMapBlockSource.FastCity.cs:157-216`). The actually
called full-world branch (`:65-86`) uses `CaptureFullCityMapAsync`. The real
Railway refresh in `:515-532` enriches an AOI scan; it does not turn that
selection into a standalone direct-list census. Treat `StrategyId` as an
inaccurate route name, not evidence the advertised direct route ran. Its
public label could be corrected by the integrator separately, preserving the
known-good acquisition method and explicit evidence limits.

Other/Treasure mixed sharing is concrete:
`MapOtherEventScanSource.NativeRequest` converts `other` to `treasure` and
deduplicates kinds (`MapOtherEventScanSource.cs:16-25`); its
`CaptureBatchAsync` calls the underlying batch source **once** before
partitioning (`:40-58`). `MapScanEngine` has a separate test-injected
`IMapScanSharedAcquisitionSource` interface, but the production provider's
per-run factory defaults to null (`CurrentClientMap317ScanProvider.cs:39-55,
169-215`); it must not be counted as a genuine alternate producer.

## Train's own end-of-life fields

The actual Lua March row reads `WorldMarch.endTime` separately from
`TrainData.arriveTs` (`tools/current_live_resource_probe.lua:3668-3671,
3729-3738`), while the official Train-list row reads arrival directly from
TrainData (`:6918-6925`). The managed parser retains `arriveTs` from the
native row, falls back to Railway TrainData JSON when necessary, and stores
`endTime` **as a separate field** (`CurrentClientMapBlockSource.FastCity.cs:
2471-2509,2533-2538,2582-2605`). Native `trainType` 1 maps to Truck and 2
to Railway. Query logic filters records with a **known, elapsed** `arriveTs`
but preserves a null arrival as unknown (`MapStore.Query.cs:283-287`).

Therefore a movement-leg `endTime` must never be substituted for final Train
arrival, and a fixed three-hour lifetime must not be invented for either
kind. The historical native Truck Follow→automatic Stop is genuine; a separate
Train expiry countdown and successful exact-UUID native expiry/Stop are still
unwitnessed. A future qualification should observe native time, own TrainData
arrival and same-UUID position/expiry across its final lifecycle, with the
typed March rectangle/request/reply/handler/queue barrier established.

## Considered alternatives and why they remain off

- **FOV20 for Resource/mobile/mixed:** real faster probe times cannot override
  specific unmatched identities or the missing March freshness witness.
- **Consecutive A→B→C with one final camera return:** genuine private test had
  119 diagnostic probes, all 10,000 AOI footprint indices and 49.203 s,
  but not 10,000 native individually owned reply proofs, 2,500 typed block
  outcomes, complete run/session cancellation ownership or a distinct final
  home-reply ACK. The checked-in Lua one-probe producer and status-only pipe
  do not implement a usable production native A/B/C group ingress. See
  `docs/MAP_014_NATIVE_GROUP_TRANSPORT_BOUNDARY.md`.
- **Direct Train-list alone or LOD2 coarse Monster/Zombie Boss:** direct
  Train-list body is currently uncalled, and generic Monster intentionally
  bypasses the LOD2 shortcut because moving Monster/Doom source rows may be
  absent. A non-public `zombie_boss`-only branch exists, but it is outside
  the public kind allowlist and does not justify changing the Monster route.

## Existing regressions and this audit's validation

The checked-in bounded-lens test already covers 1,023 combinations, explicit
ForceLegacy, diagnostic-only March opt-in and stale/duplicate/incorrect native
AOI footprints. `Map014ConsecutiveEngineCleanupChecks`,
`Map014SharedAcquisitionChecks`, `Map014ResourceNativeSourceChecks`,
`Map014ResourceHydrationReceiptChecks`, `Map014OtherEventChecks` and
`Map006PublishedAudit` cover controlled source/cleanup/category/expiry
inverses. Source-backed Python suites retain the passive March and native
shared-transport adversarial boundaries. No additional test reproducing the
same FOV20 selector cases is needed; the lead owns the eventual combined
.NET build/suite and genuine package validation.

Read-only offline tests executed for this audit (no native/game launch):

```text
python -m unittest discover -s tools/map_014_march_research -p test_audit_compare_ready_march.py -q
  29 tests, PASS
python -m unittest discover -s tools/map_014_shared_transport_audit -p test_audit_group_transport.py -q
  64 tests, PASS
python -m unittest discover -s tools/map_014_lead_native -p test_scanner_phase_evidence.py -q
  2 tests, PASS
```

Decision: **retain City-alone FOV20 and guarded serial LOD0 for everything
else, with shared mixed discovery**. This is the fastest *currently
accuracy-qualified* selection within the cited routes, not a proof of an
absolute optimum. The source evidence does not close global City census,
Resource lifecycle, typed March queue completion, Train lifetime or real
consecutive group home-restoration gates. Whole MAP-014 remains **PARTIAL**.
