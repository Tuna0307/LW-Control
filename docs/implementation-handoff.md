# Current implementation/research handoff

**Target:** LWBridge 0.3.17  
**Branch:** `research/offline-controller`  
**Phase:** UI parity / 8-hour UI campaign active

## Important reset

The previous bot rebuild was deliberately deleted.

The repository has returned to reverse-engineering-first work.

The current plan is:

1. reproduce the in-scope post-auth 0.3.17 UI one-for-one;
2. then recover in-scope functions slowly and individually;
3. then map them to the current Last War client;
4. live-prove them.

## Explicit auth exception

The clone will **not** recreate the original LWBridge login/account/licensing/entitlement system.

Do not:

- rebuild login/account/licensing UI;
- reverse engineer credential/token/license/purchase protocols;
- bypass authentication/entitlement.

If an in-scope feature later proves it needs a specific auth-produced state, the project lead may assign a narrow dependency investigation for that state only.

## Current reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Old research

Extensive 0.3.1 research is preserved in Git history, reviews, evidence and archived management docs.

Do not blindly resume the old R8/R9 next step. The target has changed.

Use old findings as hypotheses and tooling references only until revalidated against 0.3.17.

## Accepted 0.3.17 baseline

`LWB317-UI-001A` is accepted at commit:

`8d71cce99a6daf522950b63f1d790e5a5a49b7ae`

It established the exact static frontend package and recovered/hash-locked 24 embedded frontend assets.

## Desktop-control state

Desktop-control tooling is re-enabled for the active UI-parity campaign.

Last War/gameplay control, backend function reverse engineering and auth bypass are not authorized.

## Active continuation

The project lead has activated:

`docs/LOOP_CAMPAIGN_8H.md`

The Loop worker may execute that campaign's ordered UI-only stages without waiting for review between each stage.

The campaign must stop before Phase 2 function reverse engineering and return
`WAITING_FOR_PROJECT_LEAD`.

## Required worker/campaign output

The campaign file defines per-stage evidence/checkpoint requirements and final handoff format.

See:

- `docs/LOOP_WORKER_PROTOCOL.md`
- `docs/LOOP_QUEUE.md`
- `docs/LOOP_CAMPAIGN_8H.md`
