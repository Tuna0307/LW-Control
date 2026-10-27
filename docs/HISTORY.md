# Project history

A short timeline. The detailed day-by-day work items, reviews, receipts and proofs were removed from
`main` in the 2026-10-27 cleanup. They are preserved unchanged in two places:

- **`archive/pre-cleanup-2026-10-27`**: tag on the last `main` commit before the cleanup
  (`20b3c3d`). It has every old `docs/` file, all old `tools/` scripts, and the old 56 KB
  `AGENTS.md` with the full chronological log.
- **`archive/research-offline-controller`**: tag on the former `research/offline-controller`
  branch (`44bca12f`, about 500 MB). It holds the decompiled original LWBridge 0.3.17 evidence
  (`evidence/lwbridge-0.3.17/...`), early reverse-engineering tools and research notes. Use
  `git show <tag>:<path>`; don't merge it.

## Releases

| Tag | Date | Content | PR |
|---|---|---|---|
| `lw-control-v1.2-2026-10-27` | 2026-10-27 | Remaining tabs vs LwTool, Chat rewards, gathering recall and Drill fixes, startup sync, Treasure on LwTool logic | #17 |
| `lw-control-v1.1-2026-10-25` | 2026-10-25 | Stable Home connection, faster and more reliable Map scanner | #16 |
| `lw-control-v1.0-2026-10-23` | 2026-10-23 | City Layout, Hotkeys, Mini Games, Squads/AFK, Automation (R30), Map scanner review | #14, #15 |
| `map-owner-test-2026-10-17` | 2026-10-17 | Map complete for owner scope | #13 |
| `map-owner-test-2026-10-15` | 2026-10-15 | Map owner fixes | #10-#12 |
| `map-scanner-test-2026-10-13` | 2026-10-13 | Shared scanner, native names | #9 |
| `map-available-v0.6.0` | 2026-10-11 | Six Map kinds + Auto | #8 |
| `manual-map-v0.5.0` | 2026-10-11 | Manual City/Resource scan | #7 |
| `home-single-game-v0.4.0` | 2026-10-10 | Single-game Home lifecycle/recovery/repair | #6 |
| `map-data-v0.3.0` | 2026-10-09 | Stored Map browsing + City XLSX export | #5 |
| `home-launch-v0.2.0` | 2026-10-09 | Home Launch → Connected → Close | #4 |
| `ui-baseline-v0.1.0` | 2026-10-09 | Reconstructed 0.3.17 UI | #3 |
| `hotkeys-test-r15` … `r29` | 2026-10-18 – 10-21 | Owner test pre-releases during FEATURE-RECOVERY-018 | — |

## Phases

1. **Sept 2026: research.** Reverse engineering of LWBridge 0.3.1 / 0.3.17 and the game client on
   `research/offline-controller`.
2. **Oct 9-11: UI, Home, first Map.** The UI was reconstructed. Home went from exact-clone
   ambitions to the 2026-10-10 owner direction: "faithful, fully functional replacement"
   (differences allowed if verified).
3. **Oct 11-17: Map (MAP-005 … MAP-017).** Manual/Auto scanning, native names, saved servers, a
   camera-free direct AOI scanner (City ~24 s instead of ~196 s), Railway/Truck sweeps, and owner
   removals (plunder, schedule, share, Normal/Fast).
4. **Oct 17-23: FEATURE-RECOVERY-018 (rounds R1-R30).** City Layout, Hotkeys, Mini Games,
   Equipment, Squads/AFK and Automation, all tested on a series of sub-accounts. Released as v1.0.
5. **Oct 23-25: LwTool comparison.** The competing LwTool client was studied. Home connection
   stability was improved (soak, fault and reconnect campaigns) and the Map scanner tightened.
   Released as v1.1.
6. **Oct 25-27: remaining tabs.** Equipment/City/Settings/Hotkeys/Mini compared with LwTool,
   Chat rewards, gathering recall and Alliance Drill fixes, startup synchronization, Treasure.
   Released as v1.2.
7. **Oct 27: repository cleanup.** The current layout and docs were set up, and the history was
   archived as described above.

## Working model used so far

The owner directed the work. A "lead" AI (Claude) wrote assignments and independently reviewed and
merged. "Worker" AIs (mainly Codex) implemented on `codex/*` branches through draft PRs. Live
evidence came from isolated runs of the packaged app on a test account. Nothing reached `main`
without independent review.
