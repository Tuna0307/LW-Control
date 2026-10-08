# d-map-decode: Map handler recovery (static, 0.3.17)

Reference SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
(asserted by `d_disasm.py` on import). Method: static x64 disassembly, no execution.
Addresses are RVAs. `d_map_verify.py` re-checks 62 byte-level facts
(`map-handlers-verify.json`). Machine-readable detail: `map-handlers-recovery.json`.
Rust niche note: result word `0x8000000000000000` = Ok; `neg; jno` = not-Ok branch.

## 1. map_scan_start validation order

Public handler `0x143D67-0x144C41` only reads `resume`/`resumeAvailable` and the
`selectedTypes` length (logging); it validates nothing and calls the service
`0xFA09E` at `0x14476C`. Order inside the service (confidence high unless noted):

| # | Check (address) | code / message |
|---|---|---|
| 1 | game connection predicate `0xFA105`, `je 0xFA146` | `GAME_CONNECTION_UNAVAILABLE` / `game connection unavailable` |
| 2 | active-scan predicate `0xFA112` | `SCAN_RUNNING` / `map scan already running` |
| 3 | status refresh `0xFA1D2` (->`0xF8CB2`); provider errors propagate. If live `isInWorld` is not bool `true` (`0xFA307-0xFA314`): provider `enterWorldMap` (5000 ms, `0xFA35D`), then re-poll status each 500 ms (`0xFA4AF`, medium) until in world or 10 000 ms elapsed (`0xFA440`) | `WORLD_MAP_FAILED` / `failed to enter world map` |
| 4 | `state.serverId > 0` (`0xFA8C8`) and `state.serverIdSource == "live"` (`0xFA909`, literal `0x82EEEE`) | `SERVER_UNAVAILABLE` / `current server id unavailable` |
| 5 | selectedTypes (below) | `INVALID_SCAN_TYPES` / `no valid map scan types selected` |
| 6 | scanMode (below) | `INVALID_SCAN_MODE` / `map scan mode must be normal or fast` |
| 7 | `tileWidth>0 && tileHeight>0` (`0xFABAC`,`0xFABBC`->`0xFC2FE`) | `MAP_SIZE_UNAVAILABLE` / `world map dimensions are unavailable` |
| 8 | provider `startMapScan`, accepted/block-count | `MAP_SCAN_START_FAILED` / `MAP_SCAN_REJECTED` (RE-MAP-002) |

Coercion inside 5/6:
- `selectedTypes` is honoured only if it is a JSON array (tag 4, `0xFA931`); missing or any other
  type -> default `[city,resource,monster,truck,railway,dispatch,ghost,treasure]` (table `0x82DE50`).
  Elements: only JSON strings that equal one of the 8 names exactly (case-sensitive, no trim;
  closure `0x396F04`, vocabulary `0xD56ED8`) are kept, de-duplicated in first-seen order. Empty -> error.
- `scanMode` is honoured only if a JSON string (`0xFAA38`); missing or non-string -> `normal` (no error).
  String must be exactly `normal` (8) or `fast` (20; `0xFC49C`/`0xFC4A8`), otherwise error.
- Types is validated BEFORE mode (`0xFA9FB` before `0xFAA09`); size last.

Clone: `MapScanStateMachine.cs:159-177` order (game -> running -> enterWorldMap -> server -> types ->
mode -> size) is already equal; `Map317CommandService.cs:482-499,805-808` coercion is equal
(non-array types -> all, non-string elements dropped, non-string mode -> normal). Residual:
original polls up to 10 s for in-world after `enterWorldMap`; clone makes one provider call and
fails if the returned context is not in world (`MapScanStateMachine.cs:486-503`). **No order change needed.**

## 2. 'current server changed during map scan' guard (status service `0xF8CB2`)

String `0x82EEC1` is referenced only at `0xF9783`. Trigger (all must hold, high):
1. game connected and `getWorldMapState` (fallback `getCurrentServerId`) succeeded;
2. live `isInWorld` is NOT boolean false (`0xF9559` takes the in-world/unknown path `0xF96E3`);
3. live serverId `L > 0` (`0xF96E3`);
4. stored shared state `isReading` is JSON bool `true` (`0xF9751-0xF9756`);
5. stored `serverId` `P > 0` and `P != L` (`0xF975C`: `setg`/`setne`).

Effect (`0xF9770` -> failure constructor `0x426D67`, flags stop=1 / preserve=0, msg length 0x26):
- DB tx `0x3E79F8`: `UPDATE scan_runs SET status='failed',error=?1,updated_at=?2 WHERE id=?3 AND status='running'`,
  then `DELETE FROM scan_records WHERE run_id=?1` (staging cleared, not preserved);
- state: `isReading=false`, `phase="idle"` (literal `0xD69130`), `inflightBlocks=0`,
  `lastError="current server changed during map scan"`, `resumeAvailable=false`;
- provider `stopMapScan` when the game connection is up; emit `bridge://map-scan-status` (`0x428A92`);
- NOT an error to the caller: the constructor returns Ok, the status call continues (`0xF97AA`) and writes
  `serverId=L`, `serverIdSource="live"`, worldId and tiles, returning that state.
Non-triggers: `L <= 0` while reading -> state returned unchanged (serverId NOT overwritten, `0xF971D`-`0xF9DBC`);
`isInWorld==false` while reading with `P>0` -> serverId kept (`0xF9567`..`0xF9DBC`, medium).

Clone: `Map317CommandService.cs:560-595` overwrites `ServerId`/`ServerIdSource`/`IsInWorld` with live values
unconditionally, even while reading and even when live id <= 0 (`:583-584`). Legacy
`ManualMapScanCommandService.cs:1313` has the guard but without the `isInWorld != false` and `L>0`-while-idle nuances.
Change: in `ReadScanStatusAsync`, when `state.IsReading && context.IsInWorld != false && context.ServerId>0 &&
state.ServerId>0 && state.ServerId != context.ServerId`: fail the run in the store (status failed, error text, delete
staging rows), stop the provider scan, set state idle with `Error` = message and `ResumeAvailable=false`, raise state
changed, then apply live `ServerId/Source`. While reading with live id <= 0, return state unchanged.

## 3. Search/options query coercion

The original validates NOTHING with an `INVALID_MAP_QUERY`-style code (string absent from the EXE). It coerces:
- `0x3EB60E` loose `Option<i64>` (query must be an object; Number -> int, float truncated toward zero,
  saturating, NaN -> 0; String -> Rust `i64::from_str` (`+`/`-` sign, digits only, no spaces); bool/null/array/object -> None).
- `0x39F992` same, None -> 0. `0x3EB56C` loose `Option<f64>` (Number or numeric string, finite only).
- `0x42C30B` strict `i64` (Number only, else 0) used on shared state JSON.

| field | rule (address) |
|---|---|
| page | None -> 1; `<2` -> 1; else value, no upper clamp (`0x3E1144`) |
| pageSize | None -> 50; `<2` -> 1; `>=200` -> 200 (`0x3E1174-0x3E1194`) |
| serverId | wrapper `0x42B787`: strict read; `<=0` -> replaced by shared-state serverId; search re-reads loosely, None -> 0; final `<=0` -> EMPTY page `{total:0,page,pageSize,rows:[]}` (`0x3E1211`->`0x3E126C`), not an error; `>99999` accepted (matches nothing) |
| kind | handler `0x1761C1`: non-string -> `""`; unknown kind: `INVALID_MAP_KIND` (`0xD56FA5`, from `0x3C3BED`) is swallowed at `0x3E120C` -> same empty page (medium-high) |
| query | missing -> `{page:1,pageSize:50}`; typed-wrong filters coerced, never rejected |
| treasureType/suppliesType | loose i64, filter applied only when `> 0` (`0x3E36EF`,`0x3E37D1`) |
| minLevel/maxLevel/minPower/maxPower | loose f64; swapped when min > max (`0x3E27F6`); None -> ignored |
| options serverId | `0x427069`: loose `<=0` -> strict shared-state serverId (effect of 0 inside `0x3C722A`: UNKNOWN) |

Clone divergences: `MapDataQueryContract.cs:72-78` (unknown kind / non-object query rejected), `:80` +
`:162-170` (serverId rejected unless number 0..99999), `:375-420` (`INVALID_MAP_QUERY` for wrong types; page/pageSize/
min-max rejected), `MapStore.cs:833-837` (`INVALID_SERVER_ID` for serverId<1). Change: make `NormalizeSearch` total:
loose-coerce each field as above, never throw; substitute shared-state serverId when requested `<=0`; return the
empty-page shape when serverId or kind is unusable; keep `INVALID_MAP_KIND` only for non-search paths.
Note `MapStore.Search` must also accept `serverId<=0` input by returning the empty page.

## 4. City export (`0x129FA7-0x12BC48`)

Check order (high): (1) `headers`: only string elements collected (`0x39D084`), count must equal 12
(`0x12A52E`) -> `MAP_EXPORT_FAILED` / `city export headers are invalid`; (2) `sheetName` default `Cities`,
`yesLabel`/`noLabel` defaults `Yes`/`No`, no errors; (3) `query` = payload.query, missing -> `{}` (not required);
(4) final export server: loose `query.serverId`; if `<=0` the shared-state `serverId` is cloned into the query
(`0x12A8D5-0x12A97A`) and re-read; still `<=0` -> `MAP_EXPORT_FAILED` / `city export server is unavailable`
(`0x12A9C5`->`0x12AD42`); (5) default file name time, save dialog "Excel workbook" (cancel -> `{canceled:true}`),
export, row-limit error (`0x2A2607`, `city export excee...`; remainder not re-decoded).

Clone `LWBridgeBackend.cs:908-927`: requires `query` object (`INVALID_MAP_QUERY`) and `query.serverId` int `>0`
BEFORE headers (`:938`, `:1033-1055`), no shared-state fallback. Change: validate headers first; treat missing/non-object
query as `{}`; derive server = loose(query.serverId) else shared-state serverId; only then raise
`city export server is unavailable`. Blank header strings: original acceptance UNKNOWN (clone rejects).

## 5. Plunder fan-out / prune / clear
Not examined (not cheaply decodable in the time-box). State UNKNOWN; no claim made.

## Remaining unknowns
- serverId==0 behaviour inside options/summary aggregators (`0x3C722A`, `0x112B45`).
- status service not-in-world branch persistence (`0x3CEAA9`) and exact overwrite rule.
- blank header handling in the workbook writer; post-dialog export details.
- item 5 entirely.

## Files
`d_disasm.py` (helper, SHA-gated), `d_map_verify.py` (62 checks), `map-handlers-verify.json`,
`map-handlers-recovery.json`.
