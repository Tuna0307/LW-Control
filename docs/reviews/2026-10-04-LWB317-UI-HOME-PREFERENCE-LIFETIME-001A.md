# Auto Launch preference lifetime — LWB317-UI-HOME-PREFERENCE-LIFETIME-001A

Date: 2026-10-04. State: **AWAITING_REVIEW**. Worker implementation; project-lead
review remains with the lead AI through the owner's relay. Dispatch baseline:
`52dd38a6c5f003ef6f9caebf681d3fbd1ed824b5`.

## Result

Home's **Open games at startup / Auto Launch** preference now follows the recovered
0.3.17 frontend lifetime: its local-storage value and visible React value change
immediately, the switch remains editable while persistence is unfinished, and a
second edit can be made while the first request is still pending.

The clone still mirrors the preference through its existing `local_config_set`
native adapter. Writes are serialized, each local edit advances a revision, stale
results cannot replace a newer edit, and a failure of the latest persistence request
reconciles storage/UI to the last native-confirmed value and reports the failure.
Native config polling updates that rollback reference only when no newer edit began
during the read; it does not own the visible Auto Launch value.

Automatic Reconnection was not changed. Its existing profile-scoped
`set_automation` callback, acknowledged update, busy predicate and transport are
byte-equivalent to dispatch after line-ending normalization.

## Exact recovery

Target EXE SHA-256:
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered main asset SHA-256:
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

Zero-based UTF-8 byte locators in `index-BVfnK1wp.js`:

- 246750 — complete storage getter/setter contract;
- 246758 — storage key `lwbridge.autoLaunchGame`;
- 247632 — Auto Launch `useState(xr)` initializer;
- 248381 — setter `setAutoLaunchGame:e=>{Sr(localStorage,e),p(e)}`;
- 338639 — Home Auto Launch binding with label/checked/onChange and no disabled prop;
- 213332 — recovered `Bn` switch, whose `disabled` argument defaults false.

The getter is exactly `storage.getItem(key) !== "false"`: missing key defaults true,
literal `"false"` reloads false, and all other values reload true. The setter writes
`String(value)` to storage before invoking the React state setter.

The current proof records byte locators in `milestone-b/current-results.json`:
Auto Launch state initializer 7236, `updateAutoLaunch` callback 17654, and Home
Auto Launch `ToggleRow` 7799.

## Verification

The immutable dispatch baseline reproduces both assigned defects: the visible value
stays old until native acknowledgement and the mounted Auto Launch switch is disabled
while saving.

The current mounted suite executes actual `App.jsx`, `HomePage.jsx` and shared
`ToggleRow` with controlled inert responses. All seven cases pass: exact storage/default
and reload; immediate off→on→off during a deferred first request; late stale success;
stale rejection; latest rejection with rollback/error; native-confirmed rollback
reference; a late stale config-poll result fenced after a newer native save
acknowledgement; five-second config polling; profile replacement; and unrelated Auto
Reconnect config preservation. Serialized native writes structurally prevent
out-of-order native acknowledgements because N+1 is not dispatched until N settles.

Real browser QA used production App/Home code behind an inert WebView response
harness. Two real clicks produced false→true→false while the first save remained
deferred and the control stayed enabled. A late true acknowledgement did not replace
the newer false edit; status refresh changed Auto Reconnect to true without changing
Auto Launch; the second false acknowledgement completed cleanly. A separate rejected
save rolled optimistic true back to native-confirmed false and showed the localized
error. Clean verification pages produced zero console errors.

Preservation proof compares dispatch/current source for ten unrelated App callbacks,
the root acknowledgement effect, selected-profile expression, retained/lazy page
ownership, root picker and Auto Reconnect toggle. All match. The five-second refresh,
Map status/scan listeners and recovery listener remain present. All ten pre-existing
dirty/untracked paths are byte-identical, including the seven protected WIP paths and
the two equipment-motion vendor notice files.

Delivery commands passed:

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`;
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`;
- task current/preservation/WIP/evidence adapters.

Build/package fingerprints are
`7c31d29fbab27d5f785f97323e7a4f9824f74ad7400358b74a2372482019fedc` /
`f1e58a73dc134eb4436e91187ef35d241a4a95e2cf041b9c04196fe3a9fdaa8d`.

Two older historical execution checkers are topology-stale before this task's
changes: HOME-ERROR-001 expects an older inline App preview-state expression, and
SWITCH-LOCALE expects `ToggleRow` inside `Pages.jsx`. Their saved-evidence validators
pass. Historical writers/assertions were preserved; the task uses narrow current
adapters against the actual current files as required by the assignment.

## Limits and continuation

This proves the recovered local frontend contract and the clone's controlled adapter
policy, not original service/runtime access. Browser and mounted native responses were
inert; Last War, gameplay, authentication and live native persistence were not used.

Continuation after project-lead acceptance is the separate Automatic Reconnection
preference-lifetime unit from parent `LWB317-UI-HOME-PREFERENCE-LIFETIME-001`.
Do not reopen Auto Launch unless review finds counter-evidence in this packet.
