# LWB317-UI-HOME-PREFERENCE-LIFETIME-001A evidence

Date: 2026-10-04. Scope: Home **Open games at startup / Auto Launch** only.
Automatic Reconnection remains outside this unit.

## Recovered source contract

Target EXE SHA-256 was rechecked as
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The recovered main frontend asset SHA-256 is
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

`recovery/source-recovery.json` records exact zero-based UTF-8 byte locators:

- storage/get/set contract starts at 246750; key `lwbridge.autoLaunchGame` starts at 246758;
- recovered getter is `storage.getItem(key) !== "false"`, so a missing key defaults to `true` and only literal `"false"` reads false;
- App state initializer `[f,p]=(0,b.useState)(xr)` starts at 247632;
- recovered setter `setAutoLaunchGame:e=>{Sr(localStorage,e),p(e)}` starts at 248381 and writes storage before state;
- Home Auto Launch binding starts at 338639 and passes only label, checked and onChange;
- recovered shared switch `Bn` starts at 213332 and defaults `disabled` to false.

`recovery/recover-source.mjs` executes the exact recovered getter/setter slice, not a
manual paraphrase. `recovery/run-baseline.mjs` reads immutable dispatch
`52dd38a6c5f003ef6f9caebf681d3fbd1ed824b5` through `git show`; its saved baseline
proves the clone waited for `local_config_set` before changing the visible value
and disabled the mounted Auto Launch switch while that request was pending.

## Current proof

`milestone-b/run-current.mjs` mounts the actual current `App.jsx`, `HomePage.jsx`
and shared `ToggleRow` with controlled inert bridge responses. Seven cases pass:

1. exact key/default/encoding/reload rule;
2. mounted off → on → off while request 1 is deferred, including native config
   polling, profile replacement and a late stale acknowledgement;
3. stale rejection fencing followed by a newer successful save;
4. latest rejection rollback to the last native-confirmed value plus visible error;
5. native polling establishing the rollback reference without owning the visible
   source-local value;
6. a config poll started before a save acknowledgement cannot return late and regress
   the native-confirmed rollback reference used by a later failed save;
7. missing-key default and literal-false remount/reload behavior.

Current exact locators are saved in `milestone-b/current-results.json`: current
Auto Launch state initializer byte 7236, callback byte 17654 and Home Auto Launch
`ToggleRow` byte 7799. The native mirror keeps the existing `local_config_set`
command/result contract, serializes writes, and revision-fences late/stale results.
Serialization means native acknowledgement N+1 cannot arrive before N because
request N+1 is not dispatched until N settles. A newer *local* edit remains visible
while N is pending, which is the source behavior being recovered.

`milestone-b/check-preservation.mjs` compares dispatch/current code and passes ten
unchanged App callbacks, selected-profile ownership, root acknowledgement effect,
retained/lazy page ownership, Auto Reconnect control and root picker. It also checks
the existing recovery/status listeners and five-second refresh timer. The only
intentional refresh-status addition updates the native-confirmed rollback reference;
it does not set the visible Auto Launch value.

## Browser verification

`browser-harness.html` is an inert native-WebView response harness used only for
browser QA. It was copied temporarily into the Vite root and removed afterward.
`browser-results.json` records two real-input runs against production App/Home code:

- deferred success: false → true → false remains immediately editable, the first
  late true acknowledgement cannot overwrite the newer false value, polling changes
  Auto Reconnect without changing Auto Launch, then the second false acknowledgement
  leaves storage/UI false;
- rejection: the optimistic true value is rolled back to native-confirmed false,
  storage returns to `"false"`, the switch remains enabled, and a localized error is
  visible.

The clean verification pages produced zero browser-console errors. The harness used
inert responses only; Last War, gameplay, authentication and native production
persistence were not exercised.

## Delivery checks

Passing checks:

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build` — production fingerprints
  `7c31d29fbab27d5f785f97323e7a4f9824f74ad7400358b74a2372482019fedc` /
  `f1e58a73dc134eb4436e91187ef35d241a4a95e2cf041b9c04196fe3a9fdaa8d`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build` with the same fingerprints;
- `milestone-b/run-current.mjs` — 7/7;
- `milestone-b/check-preservation.mjs` — 10 exact callbacks plus preserved ownership/listeners;
- `check-protected-wip.mjs --record` — 10/10 starting WIP paths unchanged, including all seven protected paths;
- historical HOME-ERROR-001 and SWITCH-LOCALE saved-evidence validators.

Two historical *execution* checkers are intentionally preserved but are no longer
compatible with the repository topology that predates this task: HOME-ERROR-001
expects the old inline `previewState={...}` App shape, and SWITCH-LOCALE expects
`ToggleRow` to still live in `Pages.jsx`. They fail before reaching this task's
changed behavior. Their saved validators pass; the current adapters above execute
the actual current App/Home/shared switch instead of rewriting historical records.

## Protection and limits

`protected-wip-start.json` pins every dirty/untracked path present at assignment
start. `protected-wip-final.json` proves all ten stayed byte-identical, including
the seven protected WIP files and the two required untracked equipment-motion
LICENSE/NOTICE files.

This unit proves recovered frontend-local preference semantics plus the clone's
controlled native persistence adapter. It does not claim original service/runtime
access or native live persistence equivalence beyond the existing command/result
contract. Automatic Reconnection is unchanged and remains the next separate unit.
