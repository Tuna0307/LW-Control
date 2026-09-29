# LWB317-UI-005 — shell/navigation reproduction

Date: 2026-09-29

## Result

The standalone `src/LWBridge.UI-0.3.17` project now reproduces the recovered
0.3.17 post-auth shell/navigation contract from exact static frontend evidence.
The reference post-auth shell is still inaccessible without crossing the
out-of-scope authorization boundary, so this stage is
`IMPLEMENTED_NOT_VALIDATED` rather than runtime-proven parity.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary static sources:

- main bundle `index-BVfnK1wp.js`, SHA-256
  `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`;
- common stylesheet `index-rIL9Fpht.css`, SHA-256
  `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`;
- English locale `en-BisSXcTB.js`, SHA-256
  `0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731`.

## Exact assets reused

The clone copies the recovered stylesheet byte-for-byte to
`src/LWBridge.UI-0.3.17/src/reference.css`; its SHA-256 is unchanged:

`3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`

The recovered status dots are also reused unchanged:

- offline:
  `F118D321CCD185313695DB337823E1CFD04CCA96EB9FA3447B65455E2673C31D`;
- online:
  `2875CC9945B55F113A7D1E194EE2A13050BF9DE6AAEC94D1DE1D664112A027D6`.

The project check now verifies these exact hashes before accepting a build.

## Reproduced shell structure

The exact recovered shell component establishes:

`main.app-shell -> top + div.app-layout -> optional profile sidebar + nav + section.main-view`

The clone implements the statically supported single-profile path:

- `.app-shell` with the exact common stylesheet;
- `.top-bar` with `LW` brand mark, `lwbridge` title and known `v0.3.17`
  product version;
- exact status-strip structure with a static `Checking` / pending `0` preview
  state, without claiming that state for the blocked reference runtime;
- exact theme-toggle SVGs and the recovered `lwbridge.theme` storage key;
- exact language names/order from the recovered main bundle;
- recovered closed `Cross-server` shell control and its exact disconnected
  popover copy; no server-jump action is wired;
- exact `Refresh Status` shell control with no backend wiring;
- `.app-layout.single-profile`, `.side-nav` and `.main-view` content frame.

The original top bar's account/grace controls remain excluded because
login/account/licensing is explicitly out of scope. Update-available UI is
state-dependent and is not fabricated in the static idle preview.

The profile sidebar is conditional on entitlement (`maxProfiles > 1`). That
runtime entitlement state is unavailable behind the excluded auth boundary, so
the clone uses the exact single-profile shell path for its default evidence
state instead of inventing a multi-profile account list.

## Exact navigation

The effective exact 0.3.17 navigation is:

1. Home
2. Automation
3. Map Data
4. Squads / AFK
5. City Layout
6. Hotkeys
7. Mini Games
8. Settings

`NavIcon.jsx` reproduces the exact inline SVG primitives/path data recovered
from main-bundle `Xr`. The clone uses the exact `.side-nav`, `.nav-heading`,
`.nav-icon`, `.nav-label`, active and hover selectors from the recovered CSS.

The main-bundle navigation helper can insert an Advanced entry, but the exact
0.3.17 call site computes `nt=ci(false, accessRole)` at byte `0x58B10`, while
`ci` requires the first argument to be true. Advanced is therefore statically
forced off in this exact build and is not reproduced as a visible navigation
entry.

## Clone smoke evidence

The Vite preview was opened in the authorized Desktop browser at
`http://127.0.0.1:4317/`.

Observed clone DOM at the browser's desktop viewport confirmed:

- top-bar height `52px`;
- single-profile navigation column `164px`;
- side-nav button height `38px`;
- exact eight navigation labels/icons;
- Home selected on initial load;
- Settings selection updates the active navigation and content-frame heading;
- theme toggle changes `document.documentElement.dataset.theme` to `dark` and
  persists `lwbridge.theme=dark`.

Durable clone capture:

`evidence/lwbridge-0.3.17/ui/clone/shell-navigation/shell-light-1120x720.png`

SHA-256:

`D71528A733695962D9ED0A0ED76035218F103686115B0507E54AE8B2178C4B02`

The capture viewport is `1120 x 720`. That size is a stable clone evidence
canvas; it must not be described as runtime proof of the original post-auth
window.

## Validation gap

Still `BLOCKED` against the reference:

- post-auth rendered shell/window geometry;
- active reference theme/language;
- conditional profile-sidebar presence/content;
- reference hover/focus/selected pixels;
- direct reference-vs-clone screenshot or pixel comparison.

No Last War process was launched and no auth/gameplay/backend behavior was
recovered or invoked.
