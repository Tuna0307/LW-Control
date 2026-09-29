# LWB317-UI-002H — Settings UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Settings observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This stage is a static `EXACT_BYTES` frontend inventory
only. No update, download, diagnostic-export or gameplay action was invoked.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary recovered component:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SettingsPanel-DqxIWv_E.js`

- recovered size: 3,673 bytes;
- SHA-256: `129CCBACDD5F309070A3E2728912165006F0FA35FCF748D56F7AA9500D690C2B`.

The Settings navigation entry is directly present in the exact main bundle near
byte `0x5562B`. The same main bundle contains the shared client-update component
and the parent-side multi-profile/focus setting. English copy is in
`en-BisSXcTB.js`; styling is in `index-rIL9Fpht.css`.

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/settings/static-inventory.json`

## Page heading

Exact heading:

- `Settings`
- `In-game display, application information, and client updates.`

## In-game performance

The first static section is `In-game performance` with description:

`Show real-time performance and network latency in the top-left corner of the game.`

It loads persisted visual-metrics state and then exposes two shared switch rows:

- `Show FPS`
- `Show Ping`

While loading, the section can show `Processing`. Exact error copy is `Failed
to load in-game display settings.` and `Failed to save in-game display
settings.`. The effective FPS/Ping switch values are loaded state and therefore
remain `UNKNOWN`.

## Account interaction

The section is conditional. Exact main-bundle evidence supplies it only when
the profile capability indicates more than one profile (`maxProfiles > 1`).
When present, its exact copy is:

- `Account interaction`
- `Control how the account list interacts with running game windows.`
- `Focus the corresponding game when selecting an account`

The main-bundle local setting defaults to enabled unless
`lwbridge.focusGameOnProfileSelect` is explicitly stored as `false`. Whether
this section is actually visible for a particular runtime account remains
unobserved because post-auth access is blocked.

## Issue feedback

The Settings component always renders an exact `Issue feedback` section with
description `Export recent local diagnostic logs to help investigate issues.`

It contains the action `Export diagnostic archive`, busy copy `Collecting and
compressing logs…`, progress states `Preparing logs`, `Exporting`, `Finishing`
and `Export complete`, plus exact success/failure result copy.

The exact privacy notice says the archive automatically masks player names,
UIDs, authorization data and local paths; it is saved only to the chosen
location and is never uploaded automatically. This is visible UI copy only; no
export was started.

## Client Update

The Settings page also renders the shared `Client Update` panel. Exact static
copy includes current/latest version labels, automatic-check status, checking,
up-to-date, update-available and opening states, published date, download
progress/directory, `Check for updates`, cooldown text and `Download and open`.

The locale contains the exact updater error messages for status, signature,
manifest, URL, check timeout/failure, unavailable update, download timeout/
failure, size/hash mismatch, shortcut failure and default update failure. These
establish possible visible error states only; this UI campaign did not exercise
the updater.

## Exact styling

Recovered CSS establishes:

- `.settings-panel`: content aligned to the start; panel subtitle uses muted
  body text;
- `.settings-stack`: grid with `8px` gap;
- `.update-panel`: `1px` line border, soft surface, `10px` radius, `12px` gap,
  `16px` padding;
- update heading/actions/progress: flex rows with `10px` gap;
- update metadata: muted footnote text;
- `.update-version`: info border/background, `7px` radius, `5px 8px` padding;
- `.update-notes`: normal surface, body size, `8px` radius, `10px` padding,
  `1.6` line-height;
- progress bars use the blue accent;
- feedback progress: three-column grid that becomes full-width in the
  responsive layout.

## Runtime gap

`BLOCKED` / not visually validated:

- rendered post-auth Settings geometry/theme;
- loaded Show FPS / Show Ping values;
- whether Account interaction is visible for the current entitlement/profile
  capability;
- current updater version/phase/notes/directory/errors;
- current diagnostic-export state/result;
- live hover/focus states;
- page screenshot and pixel comparison.

No gameplay/backend function recovery was performed.
