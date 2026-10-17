# FEATURE-RECOVERY-018 lead audit — 2026-10-17 (source 8d99f358, draft PR #14)

Verdict: **NOT ACCEPTED — PARTIAL, no live proof.** Inventory, City/Hotkey/Mini Game bridge code and offline checks
exist (lead re-ran Desktop.Checks incl. FEATURE018_* and UI checks: pass), but no feature was proven in the game.

## Live blocker resolved by lead
`%LOCALAPPDATA%\LWBridgeRebuild\overview-bridge\recovery.json` (profile `local-9370d887…`, game PID 31476, stage
`active_ready_deferred_restore`, 2026-10-16T17:49Z) came from the **owner's own app session**; the app was closed
without restoring. Installed lwScripts were still the bridge candidate (data sha ec8f2c38…). With no game/app
running, the lead restored the three files from the journal's backup
(`overview-bridge-backups/20261016-174910-1d5efc208d2a48db937cfde18eaec76c`), verified original hashes
(data 49f6249d…, metadata 52d5b30c…, version 5f9c4ab0…) and archived the journal as
`recovery.lead-restored-20261017.json`. Live admission is unblocked.

## R2 assignment — same branch/PR #14
0. LEAD018-01: find why the owner's app exit left the game on modified scripts (stage
   `active_ready_deferred_restore`); fix so closing the app (or the next app start) always restores originals,
   with a regression check.
1. Run the live sessions now (sub-account authority, record UID). For every feature in the inventory: make it
   work and prove it in the real packaged app with before/after game state — City snapshot/plan/apply moves
   (incl. cancel), all Hotkeys (Q/W/E/R attack + speedups, A/S/D/F recall, Space shield countdown, F6/F7/F8
   shields, Alt+1–4 equipment, F9/F10 relocation), Mini Games (G reinforcements, Black Market marker, Land Cell
   unlock, Food House solver). Items/gems/relocation/attacks may be used on this account.
2. Hotkey proof without stealing the owner's focus: deliver keys to the task-owned game window only (e.g. window
   messages to that HWND), or bring only the task-owned game window forward. Never touch owner windows.
3. A feature may end NOT_POSSIBLE only with a live-tested reason (not "no provider yet").
4. Final ZIP from the final commit, report update, status READY_FOR_LEAD_AUDIT or PARTIAL.

## R2 audit — source c12bc5d0 (lead)
Worker correctly refused all gameplay: the logged-in account was UID 3133546747002289 on server 2289, while the
lead's authority note wrongly named 2212. Owner confirmed 2289 is the sub-account and 2212 the main account. The
authority doc is corrected. No action was performed on any account; four live sessions restored original scripts.
LEAD018-01 (exit restore) fixed in 0982279e. Status remains PARTIAL — no live feature proof yet.

## R3 assignment — same branch/PR #14
Run the live proof on the sub-account (2289, UID 3133546747002289) now. Replace every server-2212 action gate with
the exact 2289 UID+server gate; keep 2212 read-only. For each inventoried feature: real before/after proof, or
NOT_POSSIBLE only with a live-tested reason. Resolve SOURCE_GAP items (Q/W/E/R cursor target, F6/F7/F8 shield
items, Alt+1-4 equipment, diamond speedups) by live experiments on the sub-account rather than static analysis.
Final ZIP, report, status READY_FOR_LEAD_AUDIT or PARTIAL.
