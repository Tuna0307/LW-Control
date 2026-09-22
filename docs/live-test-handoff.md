# Current live-test handoff

**Current through:** `LWB-R7-145`, 2026-09-22.

The old Resource/Player City owner-test packet has been retired. Ordinary Home/Map acceptance no longer needs the owner to repeat those historical checks unless a future code/client change causes a regression.

## Tests still waiting on external conditions

### 1. Ghost positive-row proof

Status: implementation ready; positive population unavailable/deferred.

Earliest owner-deferred checkpoint: **2026-09-24**. When authentic Ghost rows exist, ChatGPT can rerun the existing read-only strict harness. The owner should not need to run terminal commands.

### 2. Supplies positive-row proof

Status: implementation/read-only harness ready; positive population unavailable.

On 2026-09-22, full-world strict scans on servers 2212 and 2213 both completed 2,500/2,500 with zero failed/unread and returned zero Supplies. Rerun only when an authentic `WorldSuppliesPoint` appears.

### 3. Simultaneous real multi-account UI population

Status: deterministic multi-profile UI plus real single-session transport are proven separately. Integrated proof needs several simultaneously active usable accounts/sessions.

### 4. State-changing acceptance

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

See `docs/tabs/home.md`, `docs/tabs/map-data.md`, `docs/external-audit-guide.md`, and `evidence/lwbridge-implementation/2026-09-22-r7-current-evidence-index.json`.
