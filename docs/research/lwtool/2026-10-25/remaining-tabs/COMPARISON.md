# Remaining tabs: LwTool and LW-Control

Status: comparison and candidate controlled checks complete; final-source package
and live action qualification remain open. No merge or release.

The 25 October normal owner-authenticated LwTool download recovered a new core
and UI. AES-GCM authentication passed. Core SHA-256 is
`f9f910e2f42ea5215fce1391ac7c6685ff4f16b7926200aa203265875c4b0ec3`;
UI SHA-256 is `5de6a7c1c9a83fdd2e141989e5998535f0a1f0e64778c8dcc2e44d9493ff05b8`.
The focused Equipment/Hotkey/settings Python API bodies are unchanged from the
23 October recovery. Other APIs changed, so earlier whole-product rankings are
not assumed current. Private recovered code, credentials and runtime rows stay
ignored locally. The public evidence index is [EVIDENCE.json](EVIDENCE.json).

## Side-by-side feature comparison

RECOVERED means current shipped client source; PROVEN means our source or an
identified native observation. A host API contract is not proof of every game-side
engine detail. LwTool's separately downloaded Lua engine is not recovered here.

| Feature | LW-Control | LwTool, recovered client | Decision |
|---|---|---|---|
| Four equipment presets | Four ordinals capture the current squad/position loadout; retain native item IDs as strings | Four per-team named profile slots, also string item UUIDs | Retain our native/schema contract |
| Save current gear | Read-only native snapshot plus persisted capture; save does not equip | Team snapshot copied into `equipProfiles` | Both present |
| Rename without recapturing | Previously no separate action: Save captures current gear | Dedicated rename updates the profile name | Implement a separate atomic name-only Rename |
| Apply preset | Preflight, one-shot transaction, query-only confirmation, exact account/generation fences | `py_apply_equip_profile` queues a worker, returns `true`; UI announces applied then schedules refreshes | Retain our acknowledged transaction; do not copy early success feedback |
| Individual equip/remove/move | Native inventory, drag/drop and same-slot selectors | Hero equipment and custom team-pair operations | Both expose gear editing; internal LwTool engine sequencing remains unknown |
| Team-wide equipment swap | Preset plan helpers and live per-item/preset editor | Dedicated custom/position team swap RPC | Retain current editor; no unqualified new batch sender |
| Equipment shortcut | Alt+1–4 | Z/X/C/V, double press according to hotkey contract | Retain accepted bindings; avoid silently changing keys |
| City building move plan | Native snapshot, geometry validation, drafts, temporary-buffer swaps | No matching City Layout editor/RPC found in current UI/API inventory | Retain ours; world base scanner is a different feature |
| City apply/cancel/restore | Native job IDs, authoritative per-move observation, immutable baseline restoration | No matching exposed flow recovered | Qualify ours directly |
| Q/W/E/R | Exact squad dispatch/redirect, native target/ownership checks | Q/W/E/R contract, Quick Attack settings | Shared feature; native engine/focus details are not established by the host contract |
| A/S/D/F recall | Native owned-march recall | A/S/D/F contract | Shared feature; qualify our available squads |
| March speed-ups | Intentionally removed | Shift+1–4 contract and a free-speed Quick Attack option | Owner explicitly chose comparison only on 25 October; keep removed |
| Shields/relocation keys | F6/F7/F8 shields; F9/F10 relocation | F6 alliance teleport, F7 random, F8 advanced, F11 fullscreen | Retain accepted meanings; direct copying would conflict |
| G | Frontline reinforcement with configurable count | Double-press defence action | Different actions; do not present as equivalent |
| Space shield countdown | Removed by owner decision | Hold-Space contract | Keep removed |
| Key admission and repeats | Exact owned foreground, text-input gate, release-to-arm, single-primary edge, native broker | Host lists bindings/gates; implementation is in the unrecovered engine | Retain qualified recognizer; no claim that a binding list proves equivalent protection |
| Hotkeys/Mini Games setting saves | Shared serial fresh-read/save queue | Window/version metadata accompanies setting saves | Fix our queued edit migration across profile generations |
| Food House | LWSheep board/solver and server-confirmed automation | No matching exposed Food House/LWSheep flow found | Retain ours; current native event is expired/unavailable |
| Black Market/chest hint | Native active-view eligibility and confirmed marker; no claim/open packet required for hint | Only `Black Market Cash` item label found; no matching exposed hint flow | Retain ours; current native chest view is closed |
| Related frontline mini game | Native status/G action | Front-break and stage-feature APIs exist | Related game systems, not proof of an equivalent Food House solver |
| FPS/ping display | Per-profile config, native apply/rollback and confirmation | `showFPS`/`showPING` settings delivered to engine | Both present; fix our pre-yield owner capture and generation rollback |
| Application language | Nine catalogs; game language remains Chinese | Multiple language catalogs and persisted selection | Qualify our language changes and persistence; no game-language change |
| Application theme | Light/dark persisted app choice | No theme-selection control found in current recovered UI | Retain ours |
| Extra FPS cap/low-spec flags | Outside this requested display-settings scope | FPS-cap selector and low-spec flag exposed | Defer: engine behavior and equivalent resource measurements are not recovered |

Absence statements apply to the recovered client UI/API inventory. They do not
prove that no hidden or separately distributed engine code could implement a
capability. No paid feature was enabled by altering entitlement or authentication.

## Evidence anchors and methods

Current LwTool core anchors (line numbers belong to the exact private hash above):

- `py_get_hotkeys` 29387–29414: key/gate/gesture contract; the docstring identifies
  `Lw_Tick.lua` as the actual physical-key implementation.
- `py_get_settings` 29320–29358; `py_save_setting` 29655–29713;
  `py_save_settings_bulk` 29716–29782: current host settings/window routing.
- `py_swap_team_equip_custom` 37082–37100; `py_apply_equip_profile` 37133–37152;
  `py_ov_equip_profile` 37386–37426: equipment RPCs and profile operations.
- UI `saveEquipProfile`, `renameEquipProfile`, `applyEquipProfile`: separate name
  update/capture/application; application RPC completion is followed by delayed
  `loadTeams` calls, not a host-verified final equipment state.
- Current static page IDs: home, scanner, auto, battle, combat, president,
  checkbase. Keyword/API inventory found no matching City Layout editor,
  Food House/LWSheep or Black Market chest-hint control.

Our active sources are EquipmentConfigCommandService/ProfileRuntimeConfigStore,
equipmentRuntime/NativeEquipmentContent, CityLayoutPage/feature018_city_game,
Feature018HotkeyCapture/Actions/NativeProviders, HotkeyPages, SettingsPage and
feature018_hotkey_game/mini_game. Accepted historical feature proofs remain in
the FEATURE-RECOVERY-018 review; they are not relabelled as fresh live positives.

## Baseline and candidate checks

The fresh isolated baseline used product package `2b32d64f` (same product inputs
as released main `122d1989`) and verified the approved sub-account before feature
commands. It read three squads, 25 equipment items and 111 City buildings.
Food House reported unavailable/expired; no active TreasureChest view or
compatible frontline battle was present. The read-only run closed with exact
original-script restoration. Raw admission and snapshots remain private.

Both profile bugs have distinguishing mounted regressions: each fails on the
old implementation and passes with the fix, including A→B→A generation changes.
Rename uses a native locked read/patch/write and saved preset ID, preserving
newer gear, other presets, restoration data and unrelated preferences. Its
mounted test verifies one name-only command and no game gear call; native tests
reject stale IDs and malformed requests without writing the file.

No cross-product speed/CPU superiority is claimed. The selected changes improve
correctness and avoid recapturing gear for a rename; final package/live receipts
are still required before handoff.

## Retained first-package refusal and correction

The first package candidate (`1eb1a32f`) completed native gear attach/drag/back
steps, then a read-only snapshot failed its ready-session freshness check. No
preset-rename or later City/Settings action is certified from that attempt. An
initial diagnostic lookup used the wrong receipt filename; a native gear
comparison stopped any workflow replay. One moved item was restored through the
saved native baseline, and every slot and inventory wearer matched the original.
The owned game exited and original scripts were restored. The partial receipt
and screenshots remain private and are indexed by hash in EVIDENCE.json.

Eight subsequent read-only snapshots succeeded with the same owned process and
account. The exact cause of the brief readiness gap is not established. The
selected correction waits up to 500 ms for fresh authority **only** for the exact
read-only equipment-snapshot command. It requires the same retained process/
launch/session and current account; changed owners/accounts, expiry or
cancellation fail closed. It neither publishes nor accepts a reply while
authority is missing and never replays a published envelope. Non-City mutation
commands retain their original immediate-fail policy; City behavior is unchanged.

Distinguishing controlled cases cover gaps before/after publication, changed
owner, changed account, persistent missing authority, late readiness, cancellation
and the unchanged mutation policy. Fresh package qualification remains required.

## Retained second-package refusal and correction

Candidate `a3f28f50` reached attach and cross-hero drag, then its drag-back
confirmation encountered a native precondition refusal. The gear had moved,
but that alone did not complete the original native transaction. A restore was
refused by its existing pending-operation guard; no restore RPC was dispatched.
The original edit ID was recovered by a bounded read-only diagnostic of our
exact task-owned host. Query-only requests confirmed the original transaction
without replay. The saved restoration then completed; all 20 equipped slots and
25 inventory wearers matched the initial snapshot. Normal owned Stop verified
all three original script files restored and no recovery journal remained.

The live UI now tolerates a precondition refusal during confirmation by querying
the same frozen operation within its existing deadline. A persistent refusal
keeps its fence and exposes Resume; it never permits another edit or retries the
mutation. Separate native-confirmed completion from a later failed snapshot:
confirmed changes use neutral refresh-pending feedback, recover success after a
fresh read, and leave controls disabled if that read remains unavailable. This
second distinction is independently controlled-tested, not claimed as the cause
of this native refusal. The read-only R28 observer also retries transient
snapshots within its bound, rechecking the original owner and account.
