# LWB317-UI-004 — clean 0.3.17 UI project scaffold

Date: 2026-09-29

## Result

A new standalone frontend project now exists at:

`src/LWBridge.UI-0.3.17/`

The legacy `src/LWBridge.Desktop` project was not modified.

## Technology choice

The exact recovered 0.3.17 frontend is a React web UI hosted by the reference
desktop application. The reconstruction therefore uses React with Vite for a
small, maintainable static UI project. The Phase 1 preview runs in a browser;
desktop hosting can be decided later without changing the recovered visual
contract.

This scaffold does not recreate the reference authorization system and contains
no gameplay/backend bridge. It exists only to reproduce the in-scope post-auth
UI.

## Initial static contract

The scaffold records the exact recovered top-level navigation order:

1. Home
2. Automation
3. Map Data
4. Squads / AFK
5. City Layout
6. Hotkeys
7. Mini Games
8. Settings

The initial route is exact recovered `overview` / Home. The navigation helper
can insert an `advanced` entry, but this exact build computes
`showAdvanced = ci(false, accessRole)` at main-bundle byte `0x58B10`; the
helper requires its first argument to be true. The effective static 0.3.17
navigation for this build is therefore the eight entries above, so the scaffold
does not add an Advanced item.

At this stage the shell is intentionally minimal. UI-005 applies the recovered
shell/navigation visual system; UI-006 replaces page placeholders with the
inventoried static page structures.

## Boundaries

- no login/account/licensing implementation;
- no Last War launch/control;
- no gameplay/backend function wiring;
- no synthetic success results;
- no changes to `src/LWBridge.Desktop`;
- preview data, when later needed, must remain clearly static/test-only.

## Validation

The project includes `scripts/check-static.mjs` to verify the required scaffold,
exact navigation labels and exact default route before building. The stage is
accepted only after dependency installation, `npm.cmd run check`,
`npm.cmd run build`, and repository `git diff --check` succeed.
