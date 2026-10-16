# MAP-017 lead audit — 2026-10-17 (source d788b797, draft PR #13)

Verdict: **ACCEPTED, pending owner test.** If the owner's test passes, merge to main and mark the Map tab
complete for the owner-defined scope (scanning, browsing, filters, Jump, Clear, Auto/one-off multi-server).

Verified by lead: remote SHA d788b797; ZIP `artifacts/release-map017-d788b797/LW-Control-MAP017-OWNER-RC-d788b7971027.zip`
SHA-256 afe97b59…0d6ec6 matches its integrity record (source d788b797); no product source change after the tested
checkpoint 5abfc7ad; Desktop.Checks (incl. MAP017_SCAN_CONTROLS_OK, MAP014_ZOMBIE_BOSS_CHECKS_OK), Zombie source Lua
check, MAP-017 audit tests, map_014 native tests and UI checks pass (lead re-run). Lead re-read the three fresh-launch
Zombie Boss-only proof databases: 137, 136, 133 Bosses, runs completed.

- Item 1 Zombie Boss 0: root cause = scanner read Monster/Boss marches before the game had processed the matching
  march reply. Fixed by waiting for the game's own receive/handle timestamps; a selected kind with zero source
  records now fails instead of silently "completing". Accepted.
- Item 2 empty picker and item 3 schedule-off one-off Run now (2211+2213, returned to 2212): accepted.
- Item 4 dropped by owner.
Out of owner scope (removed or owner-only): plunder/claims/sharing actions; season-dependent Treasure claims.
