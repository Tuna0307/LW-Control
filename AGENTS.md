# Mandatory rules for every AI and contributor

Read this file before doing any work.

## 1. Current target

The active product/research target is:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

The old 0.3.1 research remains historical evidence only unless revalidated against 0.3.17.

## 2. Product goal

The goal is one-for-one reproduction of the **in-scope post-auth LWBridge 0.3.17 product experience** as observed/recovered, while keeping the final implementation compatible with the current Last War client.

Login/account/licensing is an explicit scope exception. We are **not** rebuilding the original authentication, entitlement, purchase, account-binding or licensing system as a product feature.

Do not redesign in-scope workflows, labels, defaults, tabs, states, timing or behavior merely because another design seems better.

## 3. Work order

Current order:

1. prepare/clean the research project;
2. reproduce the in-scope 0.3.17 post-auth UI one-for-one;
3. reverse engineer each in-scope function;
4. map recovered behavior to the current game;
5. live-prove parity.

Do not begin a different phase unless the project lead assigns it.

## 4. Evidence first

Never invent command names, IDs, fields, limits, timing, defaults, formulas, offsets, request schemas, success criteria or UI behavior.

Use these states:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

Every recovered fact must name its source and locator.

## 5. 0.3.1 evidence policy

Do not delete old reviews/evidence.

A 0.3.1 finding may be used as a hypothesis or shortcut, but it must not be reported as 0.3.17 fact until checked against the 0.3.17 executable/assets/runtime.

## 6. Login/auth/licensing boundary

Login, account management, entitlement, subscription/purchase and licensing are **not reconstruction targets**.

If the reference exposes a login/locked boundary:

- record only the visible boundary needed to understand access;
- do not implement the login/account/licensing UI in the new clone;
- do not reverse engineer credentials, token exchange, purchase, subscription or license-validation protocols;
- do not bypass authentication or entitlement;
- use only legitimate owner-provided/existing access state when available.

A later in-scope function may justify **minimal dependency tracing** if it demonstrably consumes state produced by auth, such as current account/server/profile/session readiness. In that case recover only the downstream state contract required by the feature. Do not broaden the task into reconstructing the original auth system.

## 7. Desktop-control rule

As of 2026-09-29, the owner has explicitly re-enabled desktop-control tooling
for the UI-parity phase.

Chat On Steroids Desktop / equivalent approved desktop tooling may be used for
bounded LWBridge 0.3.17 UI observation tasks assigned by the project lead.

This permission does **not** automatically authorize gameplay/live-function
testing. Do not launch or control Last War unless the assigned work item
explicitly requires it.

For UI capture work:

- observe before acting;
- use the exact 0.3.17 reference executable;
- do not bypass login/auth/entitlement merely to reach another screen;
- do not press gameplay/function controls outside the assigned scope;
- preserve screenshots/state evidence under the 0.3.17 evidence tree.

## 8. Live-session safety

Before any future live test:

- inspect existing Last War/LWBridge processes;
- treat unexplained existing game sessions as owner activity;
- do not close or repurpose an owner session;
- prefer assistant-owned sessions for disruptive tests;
- fail closed if session ownership is ambiguous.

## 9. Bounded work blocks

Keep primary work blocks to roughly 20 minutes.

Near minute 17–18, stop opening new branches of investigation, preserve useful state, update durable documentation, and summarize the exact continuation point.

Loop campaigns may run longer only when a project-lead-authored campaign file explicitly allows it. Even then, preserve coherent checkpoints frequently.

## 10. Documentation rule

A useful finding is not complete until it is written into the repository.

For each completed work item:

- update the relevant current master document;
- create a dated finding/review when detailed evidence is needed;
- update the parity matrix/feature ledger if status changed;
- update the handoff if the continuation point changed.

## 11. Git rule

Work on `research/offline-controller` unless the project lead explicitly assigns another branch.

For coherent checkpoints:

- review the diff;
- run applicable checks;
- commit;
- push to `origin/research/offline-controller`;
- verify the remote revision.

Never force-push or discard unrelated work.

## 12. Tooling

Project-related tool discovery/installation is pre-authorized.

Prefer existing tools first. Record tool/version/source when a new tool materially affects a recovery finding.

PowerShell 7 is available as `pwsh` (7.6.6).

## 13. Worker-AI discipline

Worker AIs must follow `docs/AI_WORK_PROTOCOL.md`.

Loop-mode workers must additionally follow `docs/LOOP_WORKER_PROTOCOL.md` and the active project-lead campaign/queue.

A worker does not redefine product scope, evidence standards, phase order or master status. Return findings to the project lead through durable files/checkpoints.
