# MAP-015 camera-free full-world scan — findings (2026-10-16)

Branch `codex/map-owner-fixes-015` (local commits moved off local `main`; PR #10 had already been merged and
published by the lead). Nothing below is shipped; these are harness-only experiments (owned test game,
exact Stop and script restoration verified on every run). Owner accepted the anti-cheat risk for this research.

## How the client really requests map data (static decode, `tools/map_014_il_decode/`)
- `Assembly-CSharp.rdl` IL operands are encrypted with one fixed transform
  (`x = ror32(enc,5); raw = ((x ^ 0x0ED2D2D2) - 0x17E52197) ^ 0x02090CBA`, table byte remapped). Long branch offsets
  use the same scheme with different constants. `verify_decoder.py` checks the whole assembly
  (regenerate `all_operands.json` with `scan_codes.py`; it is git-ignored). Static analysis only; no process
  memory, debugger or anti-cheat interaction.
- `WorldPointManager.SendAoiRequest` builds `WorldGetBlockMessage.Request` and calls
  `WorldGetBlockMessage.Instance.Send(request)` (`params object[]`). Earlier failures came from passing an
  explicit `object[]` (double wrapping → swallowed exception). The client caps a request at 160 areas.

## Live results (harness fixture `map014_fullscan_fixture.lua`, server 2212)
| Method | Time | Cities | Resources | Secret Task | Ghost | Monsters | Trucks |
|---|---|---|---|---|---|---|---|
| Camera scan (production, earlier today) | ~200–225 s | 4,172 | ~8,000 | 371–422 | 182–415 | 13,200–14,700 | ~180–260 |
| Camera-free, 63×160-area requests, next sent when previous reply lands | **15.5–15.8 s** (3 runs) | 4,176 | 7,990–7,996 | 369–374 | 182–184 | 12,835–13,565 | **5–14** |

- 320 or 640 areas per request: server returns almost nothing (160 is the limit).
- Pipelining (0.1 s spacing or 4 per frame): data lost (half / 7 %); the server answers about one at a time.
- Points for areas outside the camera are deleted by the client shortly after arrival, so a production scanner
  must capture records when each reply lands (the probe already reads the native cache; it would read per reply).
- Trucks are not delivered by `world.get.block`; they need another source (the current camera route works).
- Monster counts include Zombie Bosses (special 10).

## Not done
No production code path uses this yet. Open: Truck source, Monster detail/protection hydration timing, Resource
detail (remaining amount) requests per reply, remote-server variant, packaged-app proof, owner decision.
