# Current implementation/research handoff

**Target:** LWBridge 0.3.17  
**Branch:** `research/offline-controller`  
**Phase:** Phase 2 function recovery / Map Data Goal ready

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

Desktop-control tooling was used for the UI-parity campaign's clone-side smoke
checks and captures.

Static/headless function recovery is authorized for Map Data, followed by
bounded live Last War Map validation using an assistant-owned session where
possible. Auth bypass and unrelated gameplay actions remain unauthorized.

The local clone Vite preview used for validation was stopped during final
cleanup. No Last War process was launched or controlled by this campaign.

## Phase 1 campaign handoff

The project-lead-authorized campaign:

`docs/LOOP_CAMPAIGN_8H.md`

has returned `AWAITING_REVIEW`.

The separate 0.3.17 UI project is `src/LWBridge.UI-0.3.17/`. Its static shell,
eight effective navigation entries and inventoried page surfaces are
implemented from exact recovered frontend evidence. UI-007 completed the
available static contract/fix pass; direct post-auth reference visual
comparison remains auth-blocked.

UI stage checkpoints end at `fc760bc`. See
`docs/reviews/2026-09-29-LWB317-UI-CAMPAIGN-8H-handoff.md` for the final worker
handoff.

The Phase 1A static UI recovery/reconstruction is accepted. Direct post-auth
reference visual validation remains blocked at the excluded auth boundary.

## Active continuation

The next authorized Goal is:

`docs/GOAL_CAMPAIGN_PHASE2_MAP.md`

It opens Phase 2 for the **Map Data subsystem only**. Other function families
remain blocked until project-lead review.

## Required worker/campaign output

The campaign file defines per-stage evidence/checkpoint requirements and final handoff format.

See:

- `docs/LOOP_WORKER_PROTOCOL.md`
- `docs/LOOP_QUEUE.md`
- `docs/LOOP_CAMPAIGN_8H.md`
