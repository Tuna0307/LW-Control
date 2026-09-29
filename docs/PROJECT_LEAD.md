# Project lead control sheet

The main project lead owns integration and should keep this file small and current.

## Current target

LWBridge 0.3.17

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Current parity scope

Target one-for-one parity for the **in-scope post-auth product experience**.

The original login/account/licensing/entitlement system is intentionally
`OUT_OF_SCOPE` for reconstruction.

Only a narrowly defined downstream auth-produced dependency may be investigated
later if an in-scope feature demonstrably requires it.

## Current phase

**Phase 1 — UI parity**

Status: static frontend package baseline accepted. The project lead has
pre-authorized an 8-hour UI-only Loop campaign covering runtime/static UI
inventory, common visual-system consolidation, clean 0.3.17 UI scaffolding,
static page reproduction and a visual comparison pass.

No gameplay/function reverse engineering is authorized in this campaign.

## Active work items

| Work item / campaign | Owner | State | Scope |
|---|---|---|---|
| LWB317-PM-001 | Project lead | COMPLETE | Repository/documentation reset for 0.3.17 |
| LWB317-UI-001A | Worker AI | COMPLETE / ACCEPTED | Static frontend package inventory/extraction |
| LWB317-UI-CAMPAIGN-8H | Loop worker | ACTIVE | Pre-authorized UI-only campaign in `docs/LOOP_CAMPAIGN_8H.md` |
| LWB317-RE-* | None | BLOCKED BY PHASE ORDER | Begins only after project-lead review opens Phase 2 |
| Login/auth/licensing reconstruction | None | OUT_OF_SCOPE | Boundary/dependency only |

Loop-mode workers must follow `docs/LOOP_WORKER_PROTOCOL.md`,
`docs/LOOP_QUEUE.md`, and the active campaign file.

## Project-lead responsibilities

Before assigning a worker/campaign:

1. define explicit goals/non-goals;
2. name the reference artifact/state;
3. name required evidence/output paths;
4. define acceptance/stop checks;
5. prevent two workers from editing the same master workstream unsafely.

After a worker/campaign returns:

1. review evidence/source identity;
2. reject unsupported inferences;
3. inspect each campaign checkpoint/commit;
4. update parity matrix/feature ledger conservatively;
5. run applicable checks;
6. accept/fix/revert only coherent changes;
7. decide whether another UI pass is needed before Phase 2.

## Status discipline

The project lead should distinguish:

- what the reference demonstrably contains;
- what has been observed at runtime;
- what has been copied visually;
- what has been reverse engineered;
- what has been implemented;
- what has been live-proven.

Do not compress those into a single vague “done” percentage.

## Desktop constraint

Desktop-control tooling is available for the active UI campaign.

It does not authorize Last War/gameplay testing, auth bypass or backend function reverse engineering.
