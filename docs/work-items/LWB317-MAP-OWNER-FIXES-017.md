# LWB317-MAP-OWNER-FIXES-017: owner test feedback round 2 (2026-10-17)

Assigned by the project lead after the owner tested main `6b26a194` (MAP-016 merged, PR #12).
Branch `codex/map-owner-fixes-017` from main; open a **draft** PR to main. Never merge or publish.
Read first: `docs/reviews/2026-10-17-MAP-016-LEAD-AUDIT.md`, `docs/MAP_016_WORKER_REPORT.md`.

## Items
1. **Zombie Boss scan intermittently returns 0.** Owner on 2212, Manual Scan, Scan contents = Zombie Boss only:
   scan completed 100 % in 20 s with **0 Zombie Boss** while a live level-80 喪屍首領 was on screen (recommended power
   33,000,000). After running other scans, a later Zombie Boss scan found **131**. Reproduce (fresh launch, Zombie
   Boss as the first scan; also after Resource-only), find the cause (detail hydration, kind filter, first-scan
   state, Monster/Zombie Boss split, timing) and fix. A scan must never report "Completed" with a silently empty
   kind when the source had objects; if the source fails, say so or fall back.
2. **Scan contents picker cannot untick the last item.** Manual and Auto: the user must be able to untick any
   category, including the last one. With nothing selected, Start Scan / Run now are disabled with a short hint.
3. **One-off multi-server scan without a schedule.** Today Run now is disabled unless "Enable automatic scanning" is
   on. Allow Run now (scan the listed target servers once, then return home) with the schedule switched off.
   Enabling the schedule must stay separate and must not start a scan immediately (MAP-016 item 2 behaviour).
4. **Remote scan without switching servers: dropped by owner.** Keep the MAP-016 switch→fast scan→return flow; do
   not spend more time on it.

## Acceptance (lead will audit)
- Item 1: reproduction evidence of the 0-result case, root cause, fix, then ≥3 genuine packaged Zombie Boss-only
  scans from a fresh launch each finding Zombie Bosses, plus one all-nine scan.
- Items 2–3: real packaged UI proof (screenshots) incl. a one-off Run now over two servers with the schedule off,
  returning home; regression checks for each.
- All existing checks pass; `docs/MAP_017_WORKER_REPORT.md`; one final ZIP from the final commit with SHA-256;
  status READY_FOR_LEAD_AUDIT or PARTIAL.

## Rules (unchanged)
Background only; game stays Chinese; no claims/plunder/sharing/attacks/marches/spending/relocation/updater actions;
stop only your own exact game session and restore scripts after confirmed exit; never bypass a tool denial; no
merge/publish/force-push/reset/discard. Subagents allowed, one agent controls the live game at a time.
