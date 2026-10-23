# LWB317-FEATURE-RECOVERY-018: recover City Layout, Hotkeys and Mini Games (owner assignment, 2026-10-17)

Assigned by the project lead. Map tab is complete (MAP-017 merged, PR #13). Branch `codex/feature-recovery-018`
from main; open a **draft** PR to main. Never merge or publish.

## Goal
Reverse engineer the original LWBridge **0.3.17** (EXE SHA-256 in AGENTS.md "Product and acceptance") and recover
**every** feature of three tabs, working for real against the current admitted Last War client:
1. **City Layout** (`src/LWBridge.UI-0.3.17/src/CityLayoutPage.jsx` exists — find what is real vs placeholder).
2. **Hotkeys** (`HotkeyPages.jsx`).
3. **Mini Games** (tab exists in navigation).

## Method
1. **Inventory first** (commit it before coding): from the original artifacts (EXE, bundled UI, any Lua/bridge
   payloads, settings, network/game commands; tools: `tools/rea-mcp`, `tools/map_014_il_decode`, the
   `research/offline-controller` branch via `git show`) list every control, setting, default, workflow and game
   action of each tab, with evidence locators. Mark each RECOVERED, CURRENT_ADAPTATION, OWN_DESIGN or UNKNOWN.
   File: `docs/FEATURE_RECOVERY_018_INVENTORY.md`.
2. Compare against our current code: what exists, what is fake/disabled/placeholder, what is missing.
3. Implement each feature with real game behaviour through the supported bridge/xLua route (same style as Map:
   decoded client calls, read-only where possible, exact restore). No fixture-only positives.
4. Test each feature live on the owner's sub-account in the real packaged app, then fix/retest.

## Authority (new — read `docs/OWNER_DIRECTION_SUB_ACCOUNT_ACTIONS_2026-10-17.md`)
On this sub-account you may perform real in-game actions needed to recover and prove features (attacks, plunder,
marches, claims, sharing, building, items/gems, relocation, mini-games). No real-money purchases or payment details;
no login/password/binding changes; no anti-cheat/licensing bypass; record the account identity each session.
Background-only, game stays Chinese, stop only your own exact session and restore scripts, never bypass a tool
denial, no merge/publish/force-push/reset/discard. Subagents allowed; one agent controls the live game at a time.

## Acceptance (lead will audit)
- Inventory complete with evidence; every item ends as WORKING (genuine packaged proof), or NOT_POSSIBLE with the
  exact technical reason. No silent omissions, no placeholders presented as working.
- Real packaged app proof per feature (screenshots/logs, before/after game state where an action changes it).
- Map and Home regressions still pass; all existing checks pass plus new checks per feature.
- Behaviour-difference ledger entries; `docs/FEATURE_RECOVERY_018_WORKER_REPORT.md`; one final ZIP from the final
  commit with SHA-256; status READY_FOR_LEAD_AUDIT or PARTIAL.
- Work tab by tab (City Layout → Hotkeys → Mini Games); commit and push a checkpoint after each tab.
