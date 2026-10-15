# MAP-014 — current native City ownership correction

2026-10-14 solo lead continuation. Preserve all negative raw receipts.

The first repeated baseline benchmark at
`artifacts/map014-lead-live/native-benchmark-city-20261014-010558` failed:
2,494/2,500 blocks completed, six failed. It is not a passing benchmark.
The runner eventually terminated correctly and exact owned cleanup passed.

Focused native source observations at (45,175) established the cause:

| Receipt under `artifacts/map-013-live/` | Observation |
| --- | --- |
| `native-causal-20261014-011639` | Fresh native cache had 91 City entries; main-base list had 90; one exact index was missing from the retained-list subset |
| `native-causal-20261014-012042` | Missing cache point is primary BuildPointInfo index 174054, mainIndex also 174054; not a secondary tile |
| `native-causal-20261014-012736` | An experimental full-field cache supplement produced a positive but resurrected the older location; **rejected**, not shipped/accepted |
| `native-causal-20261014-014618` | Actual native `GetBaseMainInfoByOwnerUid` resolved that same player to current index 199052; explicit superseded accounting worked, but a redundant outer raw-count check still failed |
| `native-causal-20261014-015503` | Corrected outer accounting passed; every raw City entry was accounted for by a canonical current identity or explicit superseded point; camera and scripts restored |

Current installed v24 metadata establishes public
`WorldPointManager.GetBaseMainInfoByOwnerUid(string) -> BuildPointInfo`,
`GetAllMainBaseList() -> List<BuildPointInfo>` and
`baseMainBuild: Dictionary<string, int64>`. The game-specific canonical lookup
is used rather than guessing which cached location is current.

The production producer still takes complete City rows from the main-base
lane. A raw cache point is classified as superseded only after the game's
resolver returns a different positive primary index for the same owner,
point type and current server. Unknown mappings, missing actual identities,
conflicting owners, invalid fields and unaccounted raw points remain failures.
No cached-row supplementation is enabled. The managed validator reconciles
raw = current + superseded counts and requires exact superseded point/index
references and current server context; it does not infer missing fields.

Controlled actual-Lua inverses preserve the older count-equal-substitution
failure, missing owner failure and duplicate-index failure, and add current
owner replacement, unknown canonical mapping and proven superseded-location
cases. Existing native, storage, Auto, Home and provider checks pass.

Repeated whole-world timings/accuracy qualification are separate and in
progress. The first two completed baseline/candidate pairs have identical
4,111 unique player identities and matching compared stable fields, but each
candidate retains one historical location for the same player across regional
observations. Final aggregation must use one latest observed primary location
per player before accuracy acceptance. Do not hide that duplicate or declare
the candidate complete based on speed alone.

All observations are current-client adaptation evidence, not original encrypted
scanner recovery or an external simultaneous server population census.
