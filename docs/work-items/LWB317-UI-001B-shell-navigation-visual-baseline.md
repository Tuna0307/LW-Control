# LWB317-UI-001B — runtime visual shell/navigation baseline

## Goal

Capture and document the **runtime visual contract of the in-scope LWBridge 0.3.17 app shell and top-level navigation only**.

This is an observation task, not an implementation task and not a gameplay reverse-engineering task.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Expected SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Read first

1. `AGENTS.md`
2. `task.md`
3. `docs/README.md`
4. `docs/AI_WORK_PROTOCOL.md`
5. `docs/LOOP_WORKER_PROTOCOL.md` when Loop mode is active
6. `docs/lwbridge-ui.md`
7. `docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`
8. this work-item file

## Allowed

- use Chat On Steroids Desktop / approved Windows desktop-control tooling;
- launch the exact LWBridge 0.3.17 reference;
- observe the initial window and accessible in-scope app shell;
- capture screenshots of the shell/navigation;
- activate only top-level navigation entries needed to observe selected-state behavior when legitimately accessible;
- record exact window/client geometry, sidebar/navigation geometry, order, labels, icons, colors, typography, spacing, borders, selected/hover states, and obvious shell-level status indicators;
- compare runtime observations with the static package recovered by UI-001A.

## Login/auth boundary

The project is **not recreating the original login/account/licensing system**.

If the app opens into a login/locked state:

- capture/document only that boundary;
- do not implement the login screen;
- do not bypass it;
- do not reverse engineer credentials/tokens/licenses;
- mark post-auth runtime shell observations that cannot be reached as `BLOCKED` / not visually validated;
- when this work item runs inside the active 8-hour campaign, continue only with the later static/offline stages that the campaign explicitly permits.

## Explicit non-goals

- do not implement the clone in this work item;
- do not reverse engineer gameplay functions;
- do not inspect or trigger Last War;
- do not bypass login/auth/entitlement;
- do not press feature/action controls inside pages;
- do not inspect backend IPC meaning;
- do not modify legacy `src/LWBridge.Desktop`;
- do not broaden into full page inventories;
- do not infer hidden states that were not observed.

## Required evidence

Create:

`docs/reviews/2026-09-29-LWB317-UI-001B-shell-navigation-visual-baseline.md`

Store durable screenshots under:

`evidence/lwbridge-0.3.17/ui/shell-navigation/`

For each screenshot/state, record:

- screenshot filename/hash;
- window title;
- outer window bounds;
- client/content bounds if measurable;
- active navigation item;
- exact visible labels;
- shell/navigation dimensions;
- any visible status/version/account text;
- whether the observation is direct or inferred.

## Acceptance

- reference hash verified before launch;
- initial accessible boundary/shell captured;
- accessible top-level navigation visually checked against UI-001A static evidence;
- selected-state appearance captured for every legitimately accessible top-level item without entering function/action workflows;
- inaccessible post-auth states clearly marked instead of bypassed;
- no Last War process launched by the worker;
- no auth bypass/reconstruction;
- no backend/function recovery;
- `git diff --check` passes;
- one coherent commit is pushed to `research/offline-controller`.

## Worker return format

```text
Work item: LWB317-UI-001B
Status:
Reference:
Files changed:
Evidence created:
Facts established:
Still unknown:
Checks:
Commit:
Recommended next task:
```

When executed as a standalone work item, stop after it.

When executed inside the project-lead-authored `LWB317-UI-CAMPAIGN-8H`,
checkpoint/commit it, then return to the campaign file and continue with the
next pre-authorized stage.
