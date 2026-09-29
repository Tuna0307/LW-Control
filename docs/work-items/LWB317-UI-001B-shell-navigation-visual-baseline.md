# LWB317-UI-001B — runtime visual shell/navigation baseline

## Goal

Capture and document the **runtime visual contract of the LWBridge 0.3.17 app
shell and top-level navigation only**.

This is an observation task, not an implementation task and not a gameplay
reverse-engineering task.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Expected SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Read first

1. `AGENTS.md`
2. `task.md`
3. `docs/README.md`
4. `docs/AI_WORK_PROTOCOL.md`
5. `docs/lwbridge-ui.md`
6. `docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`
7. this work-item file

## Allowed

- use Chat On Steroids Desktop / approved Windows desktop-control tooling;
- launch the exact LWBridge 0.3.17 reference;
- observe the initial window and app shell;
- capture screenshots of the shell/navigation;
- activate only top-level navigation entries needed to observe selected-state
  behavior;
- record exact window/client geometry, sidebar/navigation geometry, order,
  labels, icons, colors, typography, spacing, borders, selected/hover states,
  and obvious shell-level status indicators;
- compare runtime observations with the static package recovered by UI-001A.

## Explicit non-goals

- do not implement the clone;
- do not reverse engineer gameplay functions;
- do not inspect or trigger Last War;
- do not bypass login/auth/entitlement;
- do not press feature/action controls inside pages;
- do not inspect backend IPC meaning;
- do not modify legacy `src/LWBridge.Desktop`;
- do not broaden into full page inventories;
- do not infer hidden states that were not observed.

If the app opens into a login/locked state, document that exact state and stop
at the accessible shell boundary. Do not bypass it.

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
- initial shell captured;
- top-level navigation order visually checked against UI-001A static evidence;
- selected-state appearance captured for every accessible top-level item without
  entering function/action workflows;
- no Last War process launched by the worker;
- no auth bypass;
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

Stop after this work item. Do not start UI implementation or the next page
inventory automatically.
