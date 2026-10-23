# MAP-019 scanner method review — phase 1 report

Status: **READY_FOR_OWNER_DECISION** (lead-completed, 2026-10-23, Asia/Singapore).

Branch `codex/map-scan-method-019`, draft PR [15](https://github.com/Tuna0307/LW-Control/pull/15). The worker (Codex)
built the tools, source-route review and first live runs; the lead stopped the worker's queue mid-run (owner
request; exact Stop and original-script restoration verified, 14 completed rounds kept) and ran the remaining
matrix. Production behaviour is unchanged in phase 1.

## Answer in one paragraph

**Yes, for finding objects the current camera-free method is the best measured method for every kind**: about
9–11 times faster than the old camera scan at the same accuracy (point kinds 17–24 s), and for Trucks and Railway it
finds 2–3 times **more** than the camera scan. No tested alternative beats it: smaller requests are 87 % slower,
the coarse Zombie Boss map is 2–3× faster but misses 60–75 % of bosses, and a lower Truck request rate is not
faster. **Two real defects were found instead:** on a server with no Zombie Bosses (home 2212 today) a Zombie Boss
scan wastes ~224 s and errors, and an **all-nine scan there fails completely after ~5 minutes and saves nothing**.

## 1. Speed and accuracy per kind (same game session, current method vs old camera scan)

"Diff" = identities present in only one of the two scans. A method is accurate when its cross-method diff is no
larger than the same-method drift (the change between two scans of the same method, from objects really moving,
spawning or disappearing).

| Kind (server) | Current | Camera | Rows (cur / cam) | Cross diff | Same-method drift | Verdict |
|---|---|---|---|---|---|---|
| City (2212) | 20.8–23.5 s | 196.9–199.1 s | 4,395 / 4,395 | 0–2 | 0 (cur), 2 (cam) | equal, 9× faster |
| Resource (2212) | 21.9–22.6 s | 195.9–198.8 s | 7,996–7,999 / 8,001–8,004 | 4–175 | 179 (cur, ~7 min apart), 31 (cam, adjacent) | equal (drift grows with time) |
| Monster (2212) | 24.0 s | 201.0–201.3 s | 9,583–9,595 / 9,605–9,642 | 92–157 | 130 (cur), 119 (cam) | equal |
| Secret Task (2212) | 16.9 s | 188.9 s | 349 / 349 | 2 | 0 (earlier pair) | equal, 11× faster |
| Ghost (2212) | 16.6–16.9 s | 191.4 s | 208–209 / 209 | 0–1 | 1 | equal |
| Treasure (2212) | 16.5–16.9 s | 193.7 s | 24–25 / 25 | 0–1 | 1 | equal |
| Railway (2212) | 60.4–61.2 s | 204.6 s | 4 / **2** | 2 | 0 | current finds more |
| Truck (2212) | 52.2–79.0 s | 197.9 s | 196 / **74** | 178–186 | 160 | current finds 2.6× more |
| Zombie Boss (2180) | 54.7–55.3 s | 199.0 s | 220–226 / 225 | 19–25 | 40 | equal |
| Zombie Boss (2198) | 54.0–55.0 s | 197.5 s | 278–286 / 279 | 17–27 | 42 | equal |
| Zombie Boss (2203) | 33.5–36.9 s | 197.9 s | 121–127 / 124 | 9–11 | 20 | equal |
| Zombie Boss (2175, worker) | 31.8–35.4 s | 213.6–213.7 s | 90–103 / 93 | 5–24 | 23 (cur), 12 (cam) | equal within the larger drift |
| City + Resource (2212) | 25.0–27.4 s | — | 4,395 + 7,998 | — | 0 / 1 | — |
| All nine (2175) | 114.0–120.5 s | — | 3,523 City, 8,007 Res, 13,919 Mon, 173 Boss, 197 Truck, 1 Rail, 564 Secret, 201 Ghost, 14 Treasure | — | City 0, Res 1, Mon 71, Boss 9 | — |
| **All nine (2212, no bosses)** | **failed after 306–315 s** | — | **nothing saved** | — | — | **defect D2** |
| **Zombie Boss (2212, no bosses)** | **failed after 224–225 s** | — | — | — | — | **defect D1** |

Fields were compared, not only counts: City names, levels, owners, alliance, protection and shield times are equal
across methods (only `health` changes between any two scans); Monster positions differ only for moving monsters;
Treasure `remainingBoxes` and Secret Task completion change naturally; Zombie Boss protection times change naturally.

## 2. Alternatives tested

| Alternative | Result | Verdict |
|---|---|---|
| Smaller requests (80 areas instead of 160; worker) | 41.1–42.9 s vs 22.3–22.6 s, same 4,395 Cities | 87 % slower — reject |
| Bigger requests (320/640 areas; MAP-015) | server returns almost nothing; 160 is the server limit | reject |
| Coarse Zombie Boss map (LOD2, opt-in; worker, 2175) | 13.7–14.4 s but 27 bosses vs 90–103 | misses 60–75 % — reject |
| Truck request rate 15/s vs 20/s (lead) | 20/s: 38.7–47.6 s, 180–192 Trucks; 15/s: 48.5–48.8 s, 179–193 | not faster, same count — keep 20/s |
| Several requests in flight (MAP-015) | server answers about one at a time; overlapped requests lost half / 7 % of points | reject as tested; a reply-time capture variant is untested (see 4) |
| Game's own search/list messages (source review) | Monster/Resource/Secret Task searches return one nearest result; Train list is an "enemy recommendation"; minimap is alliance-only | none is a full list — complement only |
| Remote server without switching (source review) | the game's parser drops replies for another server | keep switching (≈1.5 s each way measured) |
| Incremental refresh (source review) | update messages exist but no replay/gap guarantee | not a safe full refresh |

Source locators for every route: `docs/MAP_019_SOURCE_ROUTES.md`.

## 3. Load and risk

Point kinds use 63 requests per scan (one at a time, 160 areas each); Trucks/Railway use 625 small requests at
20/s with missing ones re-sent. This matches what the game itself sends while a player scrolls the map, at a
higher rate; the owner accepted camera-free requests on 2026-10-16. Network bytes were not measured (local file
sizes are not wire bytes). No game action was sent in any run.

## 4. Defects found (recommended phase 2)

- **D1 — Zombie Boss with no bosses on the server.** The scanner treats "zero bosses" as unverified, falls back to
  the 200 s camera scan, which also finds none, then reports an error (`CurrentClientMapBlockSource.FastCity.cs`,
  `RequireZombieBossSourceRows`). Fix: accept a verified empty result when the full sweep was proven complete (the
  same rule Railway already uses), so the scan returns "none" in ~20–50 s.
- **D2 — All-nine fails completely on such a server.** Because of D1, the whole all-nine scan (306–315 s) fails and
  saves none of the other eight kinds. Fix D1; additionally, a failure in one kind should not discard the others.
- Optional, untested: two requests in flight with each reply captured as it arrives. Earlier evidence says the
  server answers one at a time, so the expected gain is small; only worth a bounded experiment.

## 4b. D1/D2 fixed (lead, 2026-10-23, commits 5742f9df and 762a7404)

"No Zombie Boss" is now accepted when all 10,000 areas were covered and the game Monster list delivered rows;
an unproven empty source still fails. Live results with the fix (owner-test-762a7404 source):

| Scan | Before | After |
|---|---|---|
| Zombie Boss only, 2212 (no bosses) | error after 224–225 s | 20.4 / 20.5 s, 0 bosses (camera also 0) |
| All nine, 2212 | failed after 306–315 s, nothing saved | 83.9 / 84.1 s, all kinds saved, 0 bosses |
| Zombie Boss only, 2203 (bosses) | 33.5–36.9 s, 121–127 | 34.3 s, 124 (unchanged) |

Receipts: `map019/fix-queue.log`, `map019/fix-native-benchmark-review-20261023-*-audit.json`. Remaining, not changed:
a single-kind scan whose kind is genuinely empty (e.g. Ghost-only at 11:00, camera also 0) still falls back to the
camera scan (~195 s) before returning 0.

## 5. Evidence (private, under `artifacts/`, ignored)

| Run | Receipt |
|---|---|
| Worker: City control, singles, Boss 2175, coarse Boss, shape | `map019/live-singles-audit.json`, `live-boss-2175-audit.json`, `shapes-audit.json`, `coarse-boss-*.json` |
| Worker queue (stopped by lead; 14 rounds kept) | `map019/live-singles-ab-partial-audit.json` ← `map014-lead-live/native-benchmark-review-20261023-014746` |
| Lead: Ghost/Treasure/Railway/Truck A/B | `map019/lead-rest-ab-audit.json` ← `native-benchmark-review-20261023-021732` |
| Lead: Truck rate | `map019/lead-truck-rate-audit.json` ← `native-mobile-review-20261023-023707` |
| Lead: Zombie Boss 2180 / 2198 / 2203 | `map019/lead-boss-2180-audit.json`, `-2198-`, `-2203-` ← `…-024733`, `…-025335`, `…-025936` |
| Lead: City+Resource, all nine at home | `map019/lead-home-combined-audit.json` ← `…-030457` |
| Lead: all nine on 2175 | `map019/lead-all-nine-2175-audit.json` ← `…-031701` |
| Lead queue log | `map019/lead-queue.log` (every step: exact Stop true, installed files unchanged) |

Commands: `python tools/map_014_lead_native/run_owned.py benchmark-review` with `LWBRIDGE_MAP019_WORKLOADS`,
`_METHODS`, `_ROUNDS`, `_MAX_MINUTES`, `_SERVER` (2175/2180/2198/2203 only); `run_owned.py mobile-review`; audits with
`tools/map_019_review/audit_live.py` / `audit_mobile.py --run <dir> --output <json>`. Every run: one owned game
session, read-only scans, no input or game actions, exact Stop, original scripts restored. Game language Chinese.

## Limits

A finite set of trials cannot prove no better method exists. Untested: reply-time capture with several requests
in flight; LOD1 for kinds other than Zombie Boss. Timings are on this PC and network on 2026-10-23.
