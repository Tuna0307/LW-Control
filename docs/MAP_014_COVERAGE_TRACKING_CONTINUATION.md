# MAP-014 — City/Truck/acquisition follow-up, 2026-10-13

This continues the **PARTIAL** MAP-014 assignment after the independent lead
review at checkpoint `18c9a614`. The earlier verified remote Auto/Jump/Clear,
genuine images, filters, City/Stop camera restoration and historical negative
receipts are preserved. The task is **not** marked complete. Work stays on
`codex/map-owner-fixes-014` and draft PR #10, with no merge/publication.

## 1. Current City evidence and missing identities

New independent *read-only* audit:
`python tools/map_014_lead_native/audit_city_records.py`.
It reads the original genuine native SQLite snapshots without moving or
modifying their records. Identifiers are used internally for set intersection;
no player IDs or names are printed or committed.

| Genuine source / capture | Unique 2212 City owners | AOIs containing Cities | Invalid UID/coordinates | Duplicate UID rows |
| --- | ---: | ---: | ---: | ---: |
| Oct 13 City-only `native-city-20261013-180338` | 4,029 | 2,201 | 0 | 0 |
| Oct 13 mixed `native-mixed-20261013-180909` | 4,030 | 2,201 | 0 | 0 |
| Oct 13 saved genuine packaged UI source `native-20261013-104453` | 4,032 | 2,202 | 0 | 0 |
| Oct 12 `trial-scan008r5_20261012` | 7,065 UIDs (7,066 rows) | 3,901 | 0 | 1 |
| Oct 11 `trial-scan008qual_20261011` | 7,062 | 3,907 | 0 | 0 |

The City-only and mixed scans share **all 4,029** owner identities at
unchanged coordinates, and mixed adds one owner in one AOI. The later genuine
saved dataset contains those 4,030 plus two more owners; across City-only to
saved there are **three additional identities** and **seven same-owner
coordinate changes**. The authenticated home tile (498,63) is present in
each Oct 13 snapshot.

In contrast, among 4,003 shared Oct 12 versus Oct 13 City-only owners,
**all 4,003 positions differ**. The historical 7,060-range census is
therefore neither a current-position reference nor a safe minimum target.
The difference is not evidence that today's 3,000+ other historical owners
were rejected or silently dropped. The reason for the complete coordinate
layout change was not established and is not guessed.

Native `city_aoi_records` uses `WorldPointManager.GetAllMainBaseList`,
filters to the exact accepted native AOI and rejects missing point ID,
coordinates, server, UID or name. The managed receiver rejects inconsistent
snapshot counts, foreign server and out-of-footprint points; these gates do
not establish that every current-world City was sent by the server. All
available retained City rows are well-formed. A **current, independently
known missing owner/position** is still needed to quantify true recall
loss or to justify a targeted acquisition repair. Do not patch a quota or
merge old coordinates into the current world.

## 2. Moving Truck — sustained positive live witness

**New genuine packaged UI native receipt:**
`artifacts/map014-takeover/native-ui-20261013-135941`.
It reused the genuine saved Truck scan; **no new full world scan** was
performed. After a native Follow on one March UUID, a bounded 24-second
proof observed **five distinct, exact same-UUID/PID/session/challenge
request IDs** from the real `WorldMarchDataManager.GetMarch/GetMarchCurPos`.
Five game-reported world positions changed along the march and the native
camera reported four distinct tiles:

| Native observation | World X | World Z | Camera tile |
| --- | ---: | ---: | --- |
| 1 | 901.508 | 1858.090 | (450,929) |
| 2 | 902.995 | 1856.822 | (451,928) |
| 3 | 904.137 | 1855.849 | (452,927) |
| 4 | 905.291 | 1854.865 | (452,927) |
| 5 | 906.427 | 1853.897 | (453,926) |

The real WebView's **Stop** button was then clicked and retired Follow.
The owned app exited 0, original `LWScripts.data`, `LWScripts.txt` and
`version.txt` hashes all matched, and no recovery journal remained.
The optional proof records source, coordinates and camera tile while leaving
the existing game's passive follow implementation unchanged. Its previous
single-Follow success is now superseded **for sustained same-UUID motion**.

**Natural expiry is still not proven live.** It requires an admissible
same-UUID march that reaches a source-backed `arriveTs` during a bounded
observation. The targeted EN/light and JA/dark mounted expiry inverse now
starts actual controlled Follow, advances only its test clock beyond
`arriveTs`, invokes the scheduled poll, proves no new native command and
proves the Follow interval is removed. Other controlled server, profile,
tab, scan and owner retirement inverses also pass. These are not a
natural-expiry native witness.
Do not simulate an expiry and call it a live one.

One earlier `track` attempt was stopped by the genuine intermittent
Dispatch-tab busy state **before** Follow; its error receipt remains in
`native-ui-20261013-135453`. A tracking-specific rerun skipped that
unrelated tab to reach the genuine moving-target gate; previous successful
Dispatch names remain separately documented.

## 3. Source-backed scanner efficiency investigation

**Current selected production:** serial camera-assisted LOD0 shared spatial
discovery with fresh target reply, cache/geometry validation and per-region
exact home-camera restoration; Normal/Fast controls unchanged.

- **Consecutive A→B:** Post-camera-correction second region
  `native-causal-20261013-194843/sequence-region-2.json` had native
  current-set count **9**, its own observed target but
  `map013NativeReplyState=waiting`, and
  `map013_native_reply_or_cache_timeout` with restoration retry. A
  shifted visible camera alone did not produce a source-matched next native
  `world.get.block` reply. No shortcut enabled.
- **ZoomMax:** `native-causal-20261013-210353/zoom-pair-02-zoom.json`
  proves the Zoom changed the game to **LOD2**, block size **1000**, block
  count **1**, with a single coarse whole-world native view. The prior
  ordinary expanded-coverage control returned **30** actual Resource rows.
  ZoomMax yielded a waiting unmatched native reply/zero qualified records,
  so coarse zoom geometry cannot substitute for the LOD0 source/field
  contract or prove a faster scan. No shortcut enabled.
- **Supported FOV/aspect route:** The native source already admits
  `coverageAspect=4` or `6` under explicit `coverage` mode. An opt-in
  three-alternating-pair method was added to the owned observer with native
  identity, camera restoration and shared-AOI matching gates; it never
  changes the production scanner. The 2026-10-13 live aspect trial started,
  but retrieving its active terminal result was **blocked before execution**
  by the tool safety check. That retrieval was not retried or rerouted;
  **no aspect candidate or speedup is accepted on unavailable evidence**.
  The subsequent independent process/journal cleanup check showed no
  task app/game process and no recovery journal.

This investigation explains the *observed* freshness failures more precisely,
but does not prove every possible camera/remote acquisition method impossible.
At least three successful alternating paired runs, comparable native identities
and fields, complete footprint/edge checks, source freshness, retries,
restoration, staging/publishing and missing/extra/duplicate comparisons
would be required before a per-kind, mixed or cross-server speed claim.
The failed/blocked candidates do not qualify for that benchmark. No claimed
median, spread or universal optimum has been invented.

## 4. Event availability — actual file-backed bounds

Read-only audit:
`python tools/map_014_lead_native/audit_event_availability.py`.
The Oct 13 genuine six-kind mixed scan contains **9,078 ordinary
`WorldMarch` Monster rows** and no source-labelled Doom Walker/BossVO
March UUID witness. Its selection did not include Treasure, so the empty
Treasure count cannot prove no Treasure existed on the server.

The retained Oct 12 eight-kind scan has **53 Treasure rows**, all ordinary
`pointType=21` and `suppliesType=0`; none is a positive type-27,
`LWIceSupplies.suppliesType=1` Scattered Supplies event.
The saved Oct 13 Truck datasets still contained rows with future
`arriveTs` at audit time, but cached timestamps alone do not establish
that any particular march remains in the current native manager at the
time of a scheduled expiry observation.
Source-backed Treasure template `eventId` resolution exists, but the
source does **not** establish that any such `eventId` corresponds to the
owner's "Celebration Party" label. The required native point class,
exact event/config ID and an actual event-spawned row remain unknown.
Candidates such as ActivityPartyMonsterTemplate or a festival bubble
are **not** assumed to be the requested map object.

Future positive qualification is conditional on a current event-spawned
`WorldSuppliesPoint` whose `suppliesType=1` is verified, the actual
Celebration Party point/config/name identity, or a genuine moving Doom
Walker/BossVO March whose type/UUID and moving coordinates can be proven
under the same owner. No claims, plunder, sharing, marches, spending or
relocation were performed.

**Overall assignment status: PARTIAL.** The verified earlier MAP-014 owner
fixes and newly sustained Truck Follow are independently reviewable.
True current City completeness, accelerated accurate scanner qualification,
natural expiry and event-dependent positive rows remain unresolved.
