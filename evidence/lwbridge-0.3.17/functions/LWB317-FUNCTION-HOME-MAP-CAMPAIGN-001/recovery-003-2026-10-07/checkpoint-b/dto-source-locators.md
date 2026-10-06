# DTO formula/default source locators — RECOVERY-003 checkpoint B

Independent bounded read-only recovery, 2026-10-07. No fixture, production, tests or historical evidence was changed. Source is the exact original `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`, SHA-256 independently recomputed as `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`, 15,866,880 bytes. Read-only tool: PowerShell/.NET file bytes, Latin-1 one-byte indexing for ASCII literal search, PE section headers for raw→RVA conversion. No executable was launched or native code executed.

Ranges are half-open raw file byte ranges `[start,end)`. Every listed range was read directly from that hash-pinned executable and matched the exact ASCII literal. All are in PE `.rdata`; inspected section mapping gives RVA = raw + `0x1200`. Reported RVA is the first byte, not a machine-code instruction/xref. These locators supplement the fixture's broad `0x3E10E7–0x3E6952` search-service range and public field ownership; they do not invent narrower control-flow xrefs.

## Independently verified literals — EXACT_BYTES

| ID / fixture rule | Raw byte range | RVA start | Literal / exact implication |
| --- | --- | --- | --- |
| D01 City no alliance | `0xD5E7CB–0xD5E7F8` | `0xD5F9CB` | `(alliance_name IS NULL OR alliance_name = '')` includes SQL NULL/empty. Missing/null JSON→column materialization is a separate ingestion fact. |
| D02 Resource/Monster name options | `0xD5820D–0xD58239` | `0xD5940D` | `HAVING name_key IS NOT NULL AND name_key<>''`; preceding exact CASE selects resourceNameKey versus monsterNameKey (first paths at raw `0xD580C6` / `0xD5811A`). Null/missing JSON extracts cannot yield a retained name option. |
| D03 Resource name filter | `0xD5EABB–0xD5EAF8` | `0xD5FCBB` | `CAST(json_extract(data_json,'$.resourceNameKey') AS TEXT) = ?`; missing/null extract cannot equal a non-null bound name. |
| D04 Monster name filter | `0xD5E916–0xD5E952` | `0xD5FB16` | `CAST(json_extract(data_json,'$.monsterNameKey') AS TEXT) = ?`; same null predicate distinction. |
| D05 Truck/Railway remaining loot filter | `0xD5E3F8–0xD5E4D4` | `0xD5F5F8` | `COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),MAX(COALESCE(CAST(json_extract(data_json,'$.maxLootCount') AS INTEGER),0)-COALESCE(CAST(json_extract(data_json,'$.robTimes') AS INTEGER),0),0)) > 0`. Missing/null remaining count falls back to nonnegative max-minus-rob; explicit zero remains zero. |
| D06 Ordinary special-UR exclusion | `0xD5EBC2–0xD5EC11` | `0xD5FDC2` | `COALESCE(CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER),0) = 0`; missing/null defaults to zero for this predicate. |
| D07 General arrival visibility | `0xD5E154–0xD5E1BF` | `0xD5F354` | `(json_extract(data_json,'$.arriveTs') IS NULL OR CAST(json_extract(data_json,'$.arriveTs') AS INTEGER) > ?)`; missing/null stays visible under this arrival predicate. |
| D08 Reward-option key/name guards | `0xD58930–0xD5895C`; `0xD58974–0xD589A1` | `0xD59B30`; `0xD59B74` | `json_extract(good.value,'$.key') IS NOT NULL`; `json_extract(good.value,'$.name') IS NOT NULL`, in the currentGoods/json_each option query. No reward elements exist for missing/null currentGoods; neither guard itself claims a DTO coercion. |
| D09 Dispatch/Ghost pending completion | `0xD5ECEF–0xD5EDBB` | `0xD5FEEF` | `(CAST(json_extract(data_json,'$.completionTime') AS INTEGER) IS NULL OR CAST(json_extract(data_json,'$.completionTime') AS INTEGER) <= 0 OR CAST(json_extract(data_json,'$.completionTime') AS INTEGER) > ?)`; missing/null satisfies pending. |
| D10 Positive completion requirement | `0xD5E4D4–0xD5E51F` | `0xD5F6D4` | `COALESCE(CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0) > 0`; missing/null fails this separate plunderability requirement. |
| D11 Dispatch plunder-time fallback | `0xD5E51F–0xD5E5A1` | `0xD5F71F` | `COALESCE(CAST(json_extract(data_json,'$.plunderAt') AS INTEGER),CAST(json_extract(data_json,'$.completionTime') AS INTEGER),0) > 0`; missing/null plunderAt uses completion then zero; explicit zero does not fall through. |
| D12 Dispatch expiry bound | `0xD5E5A1–0xD5E632` | `0xD5F7A1` | `(COALESCE(CAST(json_extract(data_json,'$.taskExpireTime') AS INTEGER),0) <= 0 OR CAST(json_extract(data_json,'$.taskExpireTime') AS INTEGER) > ?)`; missing/null expiry takes the nonpositive/unbounded branch. |
| D13 Dispatch steal-limit bound | `0xD5E632–0xD5E704` | `0xD5F832` | `(COALESCE(CAST(json_extract(data_json,'$.maxStealCount') AS INTEGER),0) <= 0 OR COALESCE(CAST(json_extract(data_json,'$.stolenCount') AS INTEGER),0) < CAST(json_extract(data_json,'$.maxStealCount') AS INTEGER))`; missing/null max takes the nonpositive branch, missing/null stolen defaults zero. |
| D14 Stable record-key tie ordering | `0xD5F2C8–0xD5F2DB` | `0xD604C8` | `page.record_key ASC`; exact tie expression, separate from primary sort/direction ownership. |
| D15 Loot sorting distinction | `0xD5F78D–0xD5F7D8` | `0xD6098D` | `COALESCE(CAST(json_extract(data_json,'$.remainingLootCount') AS INTEGER),0)` is the sorting expression. It does **not** use D05's max-minus-rob fallback. |
| D16 Special-UR quality sort | `0xD5F859–0xD5F8BE` | `0xD60A59` | `CASE WHEN CAST(json_extract(data_json,'$.isSpecialURQuality') AS INTEGER)=1 THEN 100 ELSE quality END`; separate from D06's ordinary-UR predicate. |

## Treasure options and radar — EXACT_BYTES

Direct options expressions:

- Raw `0xD58329–0xD583EE`, RVA `0xD59529`: `CASE WHEN COALESCE(CAST(json_extract(data_json,'$.suppliesType') AS INTEGER),0)>0` then integer suppliesType, else0, `END AS supplies_type`.
- Raw `0xD58407–0xD584D8`, RVA `0xD59607`: same positive supplies test then0, else `COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0)`, `END AS treasure_type`.
- Raw `0xD5861E–0xD586E2`, RVA `0xD5981E`: `WHERE supplies_type>0 OR treasure_type>0`, then `GROUP BY supplies_type,treasure_type`, ordered by `CASE WHEN supplies_type>0 THEN 1 ELSE 0 END,treasure_type,supplies_type`.
- Raw `0xD5E1DB–0xD5E275`, RVA `0xD5F3DB`: `(COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0)<>1` or equal bound allianceId. This verifies the radar-type1/null-zero alternative for this literal variant. The following distinct viewerAllianceId fallback variant also exists in the same region; binding/admission choice requires its original branch locator and is not guessed here.

Thus supplies>0 overrides treasure type in option grouping; missing/null supplies and treasure coalesce to zero, and both-zero rows are omitted from options. This is stronger authority than a general Treasure field-ownership reference, but not proof of claim/inspection provider state or all radar viewer branches.

## Mapping to current checks and evidence limits

Relevant current implementation locators: `src/LWBridge.Map-0.3.17/MapStore.Query.cs:233–246` currentGoods/arrival options; `:271–291` Treasure option CASE/group/order; `:354–376` ordinary-UR/completion/plunderability predicates; `:409–420` stable tie ordering; `:438` loot sort. Binder names and page/table qualification differ from original static formatter fragments; the SQL semantic expressions above match the corresponding bounded rules.

Fixture/check mapping:

- City rule → D01/D14, plus separate ingestion preservation check.
- Resource/Monster → D02/D03/D04. Monster forced-ascending distance remains the earlier `0x3E10E7–0x3E6952` review claim and current code `Query.cs:414–416`; this audit did not recover a narrow original instruction branch proving the forced direction, so it must not be presented as newly narrowed here.
- Truck → D05/D06/D08/D15/D16; distinguish filtering default from sorting default.
- Railway → D07/D08; fixture missing/null arriveTs visible is an ordinary visibility claim, **not** proof that such rows are plunderable (the separate native `json_extract(...,'$.arriveTs') IS NOT NULL` literal exists at raw `0xD5E3C8` region and rejects that condition).
- Dispatch → D09/D10/D11/D12/D13. Missing/null completion is pending but fails positive completion admission; do not summarize all missing limits as globally fail-open.
- Ghost → D09 only for pending/completion behavior; Ghost execution/provider availability is separate.
- Treasure → the four exact ranges above; options and filter semantics do not establish live inspection/claim state.

`map317-recovery-dto-matrix.json` correctly labels rows synthetic; neither these bytes nor offline DTO round-trip assertions turn them into original/runtime population records. Schema `data_json TEXT NOT NULL` and current raw-JSON round trips preserve absence versus explicit null in clone storage; claiming original ingestion serializer preservation needs its own narrow recovery. Public query→kind ownership still references the existing hash-pinned 0.3.17 contracts/review. No new branch xref, success criterion, limit, acquisition or protected provider is inferred.

Recommendation: parent may reference these exact ranges from the fixture authority/review without changing synthetic-data labeling. Upgrade only the specific formula/default expressions that now have exact byte locators; keep unresolved branch/routing/runtime facts separate. Existing history/review artifacts remain immutable.
