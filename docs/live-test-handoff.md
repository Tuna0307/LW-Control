# Current live-test handoff

**Current through:** `LWB-R7-154`, 2026-09-24.

The old Resource/Player City owner-test packet has been retired. Ordinary Home/Map acceptance no longer needs the owner to repeat those historical checks unless a future code/client change causes a regression.

## Tests still waiting on external conditions

### 1. Ghost positive-row proof

Status: implementation ready; positive population currently unavailable.

R7-152 reran the strict current-v21 proof on 2212, 2175, 2180, 2185, and 2207. Every server completed 2,500/2,500 cleanly and returned zero authentic Ghost rows. Rerun only when population appears; ChatGPT can target a server with the proof harness and the owner should not need to run terminal commands.

### 2. Supplies positive-row proof

Status: implementation/read-only harness ready; positive population unavailable.

R7-153 current-v21 strict scans on 2212, 2175, 2180, 2185, 2207, and 2213 all completed 2,500/2,500 cleanly and returned zero authentic `WorldSuppliesPoint` rows. Rerun only when Supplies population appears; the existing harness already handles safe target-server jump/return.

### 3. Fresh Railway v21 positive row / Follow

Status: historical v20 Railway positive acquisition/Follow is valid provenance; fresh v21 positive acceptance is pending.

R7-154 attempted the strict server-2207 proof, but the lifecycle correctly refused because Last War was already running outside LWBridge ownership. Retry the unchanged harness only when LWBridge can legitimately own the game session and a Train row is available.

### 4. Simultaneous real multi-account UI population

Status: deterministic multi-profile UI plus real single-session transport are proven separately. Integrated proof needs several simultaneously active usable accounts/sessions.

### 5. State-changing acceptance

Treasure Claim and Alliance-share delivery are not read-only tests. Scheduled Plunder was retired by owner in R7-149 and has no live-action acceptance test. Do not run them merely for coverage. They require suitable expendable targets and the required explicit authorization at the time of the test.

## What the owner may be asked to do later

If a test genuinely requires owner observation, instructions must be simple UI steps plus screenshots/descriptions. The owner is not expected to run commands, inspect JSON, calculate hashes, locate databases, or diagnose recovery state.

ChatGPT should automate technical evidence collection first and ask the owner only for the minimum visible interaction that cannot be collected directly.

## Tests that do not need repeating now

- Player City saved-row visibility / Search correlation.
- ordinary Home Launch/Close/reconnect/status behavior.
- normal Release Overview <-> Map Data navigation across restart.
- ordinary full-world City/Resource/Monster/Truck/Railway/Dispatch/Treasure scanning.
- Auto Scan ordered multi-server cycles/return/restart ownership.

Repeat those only after a relevant code change, official-client compatibility change, or reproduced regression.

## Current evidence

See `evidence/lwbridge-implementation/2026-09-24-r7-154-railway-v21-status-hygiene.json`, R7-153 Supplies, R7-152 Ghost, `docs/tabs/map-data.md`, `docs/external-audit-guide.md`, and the curated current evidence index.
