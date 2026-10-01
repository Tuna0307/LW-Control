# LWB317-UI-CORRECT-001 interaction and negative-state QA

Date: 2026-10-01

Status: **AWAITING_REVIEW**. These checks prove the worker correction and clone
behavior that could be exercised without crossing the original authorization or
native/gameplay boundary. They are not original-runtime pixel-parity proof.

## QA labels

- **BROWSER_REAL_INPUT** — a click, keyboard action, fill/select, or toggle was
  sent through the background browser input path and the React state was read
  back afterward.
- **BROWSER_DOM_DRAG** — a browser-side HTML5 DragEvent was dispatched against
  the real rendered React nodes. This is stronger than source inspection but is
  explicitly not called a physical-pointer drag proof.
- **BROWSER_RENDER** — a pinned query state was rendered in Edge and inspected or
  captured.
- **CONTRACT_TEST** — a repeatable Node/.NET integration/static contract check.
- **SOURCE_HANDLER** — exact recovered source plus the clone handler/state path
  were inspected; the browser connector could not faithfully drive that gesture
  or the action legitimately belongs to a native provider.

## Canonical command verification

Run from src/LWBridge.UI-0.3.17 on implementation commit 591409d:

1. npm.cmd run check
   - LWB317_HOME_UI_INTEGRATION_CHECKS_OK
   - LWB317_MAP_UI_INTEGRATION_CHECKS_OK
   - LWB317_UI_COMPLETE_CHECKS_OK
   - nine generated locale catalogs each contained 1383 messages.
2. npm.cmd run build
   - Vite production build succeeded.
3. npm.cmd run check:production-build
   - LWB317_PRODUCTION_UI_BUILD_OK
   - index SHA256 71705b869d4a80d0774b69760bd5678d996e45dc516cef8d315b2542a9eb10af
   - asset SHA256 331eef9754888657e8dfb4be0178c4c46c9c1ef620d1081e7b3ecb5cdea0f68b
   - LWB317_PRODUCTION_UI_PACKAGE_OK with the same hashes.
4. dotnet run --project ..\..\tests\LWBridge.Map-0.3.17.Checks\LWBridge.Map-0.3.17.Checks.csproj -c Release
   - LWBridge.Map-0.3.17 scan-state checks passed.
   - LWB317_MAP_CHECKS_OK
5. git diff --check
   - exit 0.

The final evidence-only/checker commit is followed by another check/diff-check
run before delivery; its final revision is recorded in the worker review.

## Focused contract checks

### Home R1

scripts/check-home-integration.mjs verifies:

- missing selected profile rejects with code PROFILE_REQUIRED;
- no native bridge message is posted on that failure;
- selected profileId is injected into set_automation;
- acknowledgement resolves before the local checked state changes;
- native error code/details propagate;
- busy state is entered before invoke and cleared on success/error;
- the Home switch remains disabled while the write is busy.

This fixes the concrete PM-006 R1 defect without implementing the separate
native launch/close/repair lifecycle family.

### Map preview/native boundary

scripts/check-map-integration.mjs verifies:

- native and native-unavailable bootstrap ignore map-* preview fixtures;
- the preview provider identifies itself as previewFixture and online:false;
- all eight map kinds have deterministic read rows;
- fixture totals exceed one page so pagination is reachable;
- sort/selection/data normalization still follow the canonical Map contract;
- Start Scan and coordinate jump reject with PREVIEW_NATIVE_ACTION_BLOCKED;
- preview fixtures do not replace native production provider behavior.

## Browser interaction record

### Automation

URL:
http://127.0.0.1:4173/?previewPage=automation&previewState=automation-config&previewTheme=light&previewLanguage=en

**BROWSER_REAL_INPUT**

- Changed category Daily → Chat using the rendered tab.
- Verified Chat cards Red Packet, Fireworks / Egg, Treasure.
- Expanded Treasure Settings.
- Verified claim delay min/max values 0/0.
- Clicked Auto Reply. React revealed:
  - Minimum reply delay (seconds) = 2
  - Maximum reply delay (seconds) = 5
  - Reply phrases textarea.
- Clicked Automatically dispatch a squad to shared treasure. React revealed:
  - minimum/maximum dispatch delays;
  - retry-when-no-squad-is-idle seconds;
  - Squad 1 through Squad 4 choices;
  - recovered priority hint.
- Treasure Run now remained disabled with the disconnected/native boundary.

This directly exercises the PM-006 R2 conditional-form correction rather than
only checking marker strings.

### Map Data

**BROWSER_REAL_INPUT / CONTRACT_TEST**

- A populated City fixture was rendered through the actual Map table.
- The data tab was changed City → Resource using the real React tab.
- Resource name filter was changed to 100281.
- Populated fixture rows stayed rendered through the canonical filter/query path.
- The fixture remained offline/disconnected.
- Additional map-city/resource/monster/truck/railway/dispatch/ghost/treasure,
  scheduled, auto-scheduled, loading and error states are pinned by the expanded
  marker gate and Map integration test.

No preview query can make a native/native-unavailable bridge use the fixture.

### Squads / AFK / Equipment

**BROWSER_REAL_INPUT**

- Opened Auto Garrison Settings from the compact card.
- Verified Alliance Center selected, Adjacent building available, squads 1/2
  selected, recall-on-disable enabled, and the provider action disabled.
- Opened Choose allies.
- Filled search with Casey, selected Casey, Confirm selection.
- Modal closed; selected-allies became Casey and target count changed 1 → 2;
  target priority became Alliance Center → Casey.
- Clicked Add in Shared AFK Profiles; a third New AFK Profile appeared and editor
  opened.
- Sent ArrowDown to Reorder Steel Hunt. List changed from Steel, Gold, New to
  Gold, Steel, New.
- Switched AFK Tasks → Equipment Schemes using the rendered tab.
- Verified Refresh, Read current equipment, per-squad Apply and Save and apply
  controls remain disabled/native-fenced.
- Opened Rename. Cleared the input: Save became disabled.
- Filled Raid and pressed Enter: dialog closed and selected preset renamed Raid.
- Reopened Rename and pressed Escape: dialog closed without another rename.

**SOURCE_HANDLER**

- Equipment same-slot item drag, hero-loadout drag and squad-loadout drag use the
  exact source-recovered HTML5 draggable/drop model.
- The connector's physical drag primitive did not reliably trigger the native
  HTML5 draggable handler, so those gestures are not upgraded to physical-pointer
  browser proof. The source/clone handlers and result/progress fixtures remain
  separately evidenced in coverage-matrix.md.

### City Layout

URL:
http://127.0.0.1:4173/?previewPage=city-layout&previewState=city-layout-populated&previewTheme=light&previewLanguage=en

**BROWSER_REAL_INPUT**

- Pressed Escape: initial HQ selection cleared.
- Clicked Barracks: Barracks became the selected inspector/building.
- After a draft move, clicked Undo: Barracks returned from draft (6,5) to (5,2);
  Undo disabled and Redo enabled.
- Pressed Ctrl+Y: Barracks returned to draft (6,5); Undo enabled and Redo
  disabled.

**BROWSER_DOM_DRAG**

The background connector's physical drag primitive does not reproduce the page's
HTML5 draggable contract. To still exercise the real React handlers, the browser
harness dispatched DragEvent dragstart/dragover/drop against the rendered
Barracks and grid:

- before: grid-area 2 / 5 / span 2 / span 2;
- valid drop target: logical cell (6,5);
- after: grid-area 5 / 6 / span 2 / span 2;
- Pending changes became 1;
- Undo and Restore initial enabled;
- local validation remained Local layout checks passed.

A second DOM drag attempted to drop Barracks onto HQ. The style remained
grid-area 5 / 6 / span 2 / span 2, proving the overlap path rejected the commit.

The committed implementation performs pointer-box selection and drops on the
grid container, so it no longer depends on city-layout-cell pointer-events.
Box/Shift-add selection is SOURCE_HANDLER-verified but not claimed as physical
pointer-box browser proof.

### Hotkeys

URL:
http://127.0.0.1:4173/?previewPage=hotkeys&previewState=hotkeys-save-error&previewTheme=dark&previewLanguage=ja

**BROWSER_REAL_INPUT**

- Japanese save-error alert rendered.
- Attack switch changed disabled → enabled.
- Item speedup and diamond fallback checkboxes became enabled.
- Both nested attack options were clicked and read back checked.

The save-error state remained visible; no native settings save was fabricated.

### Mini Games

URL:
http://127.0.0.1:4173/?previewPage=mini-games&previewState=mini-games-solve-failed&previewTheme=light&previewLanguage=en

**BROWSER_REAL_INPUT / BROWSER_RENDER**

- Black Market grand-prize chest presentation switch changed off → on.
- Food House rendered level 4, elapsed 00:00 and the recovered solve-failed
  message No safe item-free solution was found.
- Unlock One Cell remained disabled.
- Food House Start remained disabled.

The other recovered result/status states are deterministic preview fixtures and
are checked by the expanded marker gate. No gameplay execution is simulated.

### Settings

URL:
http://127.0.0.1:4173/?previewPage=settings&previewState=settings-complete&previewTheme=light&previewLanguage=en

**BROWSER_REAL_INPUT**

- Show FPS changed off → on.
- Show Ping changed off → on.
- Focus corresponding game changed on → off.
- Export diagnostic archive and Check for updates remained disabled.

Available/downloading/checking/error and feedback progress/success/error are
deterministic presentation fixtures only; updater/export actions remain fenced.

### Shared shell

**BROWSER_REAL_INPUT**

- Theme button changed light → dark and aria-pressed false → true.
- Opened Cross-server popover; it showed Current: -, disconnected status and a
  disabled Jump button.
- Nine-language selector options were present in the DOM.

The active previewLanguage query intentionally pins the route locale, so a
select attempt on that pinned route did not change from en. Locale rendering is
instead evidenced with pinned en, ja and zh-CN screenshots and the nine-catalog
coverage test. This is recorded as a harness/fixture constraint, not a locale
failure.

## Clone screenshots

All images are repeatable clone-only evidence in screenshots/. They are not
original-reference screenshots.

| File | Viewport | State | Theme | Locale | SHA256 |
|---|---:|---|---|---|---|
| automation-config-light-en-1440x900.png | 1440x900 | automation-config | light | en | B82760BE473C84DEB2F7C70E0E78F0ADB8E5CEA12772527BCD837AB38B3A8397 |
| city-layout-conflict-light-en-1440x900.png | 1440x900 | city-layout-populated-conflict | light | en | 166CB60C5A541055584B3CD9C1E6EFA096346638D34603C306CCC7C969C1E9C7 |
| city-layout-populated-light-1440x900.png | 1440x900 | city-layout-populated | light | en | 6BF92694A295E622C76A7753465D5FA70F0CEC47335E27645A90C56D996B5C1D |
| home-recovery-failed-dark-zh-CN-768x900.png | 768x900 | home-recovery-failed | dark | zh-CN | D15F5AAC9A5E0CB15B08AFE943EDFC74BAE117D34CD374CAF45084B8B841F0F1 |
| hotkeys-save-error-dark-ja-1440x900.png | 1440x900 | hotkeys-save-error | dark | ja | 158B39DA5379A78E8853E3C102CDDCBD030A253EE61A3F348F69B52F340721D0 |
| map-auto-scheduled-dark-en-1440x900.png | 1440x900 | map-auto-scheduled | dark | en | 6DD8D26C50C652114F72017A9C71E400D0DF5A225C2407B7BCB49002CEB1478E |
| map-resource-light-en-1440x900.png | 1440x900 | map-resource | light | en | 18EC9C13F3A437E8E6CB6D9381F593634F0CA29DC595A5EABD3A8670A4DCCB2E |
| mini-games-solve-failed-light-en-1440x900.png | 1440x900 | mini-games-solve-failed | light | en | 4F057ECE04883CDD70816D01DBB320859A2C704D2D2F35DC13F06045790F68A1 |
| settings-update-available-dark-en-1440x900.png | 1440x900 | settings-update-available | dark | en | A9760AD9BA1E53FBE3A2E9BE2CB055570412E0E2848F4F7C74EEF62902AEBE90 |
| squads-equipment-rename-dark-en-1440x900.png | 1440x900 | squads-equipment-rename | dark | en | B50ED4DA826A2D314C4AA252D15B0E8642EA5ACE8CFF6A596061CE60EE4A98DD |
| squads-profile-light-en-1440x900.png | 1440x900 | squads-profile | light | en | CA9852243599D3D69131ACD276477C3CC26CE755E1195158A31E358039E427F4 |

The screenshot set deliberately includes desktop and responsive width, light and
dark themes, and en/ja/zh-CN locale examples.

## Remaining validation blockers, not implementation claims

1. Post-authenticated original 0.3.17 page pixels remain BLOCKED by the existing
   authorization boundary. No bypass attempt was made.
2. Native Home launch/close/repair remains its separately assigned lifecycle
   family; this task only repaired the demonstrated preference-write defect.
3. Native task/game providers for Automation, Squads, City, Hotkeys, Mini Games
   and Settings were not invented.
4. Map live-proof status from its dedicated campaign is unchanged; this task
   added only a browser-only read fixture for recoverable positive UI states.
5. Physical HTML5 drag through the connector is not claimed where the connector
   could not reproduce browser draggable semantics. City uses a disclosed DOM
   DragEvent harness; Equipment remains source-handler verified for drag gestures.
