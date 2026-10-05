# Milestone 2 coordinator closeout — full AFK/Squads compositions

Status: source/local correction and executable proof complete at the coordinator gate.
The two earlier independent reviews in `review/` are preserved as pre-fix findings;
their listed production gaps were subsequently corrected and revalidated rather than
rewriting those historical reports.

## Exact recovered source identities

- `SquadPanel-HC3-DJei.js` function `I`: UTF-8 byte 28,070, length 22,554,
  SHA-256 `0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E`.
- `pe`: UTF-8 byte 4,586, length 11,256,
  SHA-256 `4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85`.
- `me`: UTF-8 byte 15,842, length 687,
  SHA-256 `7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8`.
- `he`: UTF-8 byte 16,529, length 1,957,
  SHA-256 `37F600C70C0F2F1A2F5722DC33C6E62075A7976C86ECA48001E97F1FA39BC32B`.

The immutable pre-correction baselines remain unchanged:

- `profile-failing-baseline.json` SHA-256
  `52CE90805182ECC94B711EB375286D399A9F78CE133D891DCD6852959725F280`.
- `garrison-zombie-failing-baseline.json` SHA-256
  `542A589A15A3E7D6AE9762D248C8AC0E04713F395604ABA7A06D487456B9911E`.

## Corrected production boundary

Current task-owned hashes recomputed by `validate-milestone-2.mjs`:

- `SquadsPage.jsx` SHA-256
  `16370281810045DF67CA9FF8E324BEE60E3C99DA1E7C493005D2B92E7769A4F4`.
- `previewAfkCloseoutFixtures.js` SHA-256
  `11B5AE1D449B61296E5150DEA355A82AFE8BF0C23497583D840057AAEC8CF3DD`.

The correction restores the recovered source ownership and whole-composition rules:
Master/profiles/Drill share one config store, Potion/Garrison/Zombie each own their
independent stores; Master-disable and profile reorder have the recovered busy
lifetime; source-valid discovered squad indexes feed Add/Drill/Garrison; runtime
target-name and AFK errors use the recovered translation preference; composition
error clear/set lifetime follows `I`; `pe` and `he` preserve their whole-fragment
toolbar order; Garrison uses the recovered native dialog/focus/Escape/Tab semantics,
localized building names, pending/failure discovery summary, and source-valid
available squads; Zombie error ownership remains the recovered sibling block.

Accepted pre-existing `AfkProfileEditor`, `EquipmentContent`, and `CompactAfkCard`
source slices remain byte-identical to the frozen pre-correction snapshot. Historical
whole-file/two-store replay scripts that intentionally pin the pre-milestone module
shape remain unchanged; the task-local validator records this compatibility boundary.

## Executable and browser proof

Fresh task-local validation reports
`LWB317_REMAINING_M2_VALIDATION_OK`, with 15 explicit source contracts, 70 routed
browser assertions, 9 screenshots and zero console errors. The browser packet covers
EN/light and JA/dark desktop plus EN/dark and JA/light narrow modes, Potion/Drill,
runtime translated/error rows, Garrison missing/running/pending/failed/localized
states, Zombie error placement, discovered squad sets, Master-stop busy, drag/
keyboard/Add-menu interactions and AFK↔Equipment Activity retention.

The paired original/current composition runner executes the exact recovered `pe/he`
functions and canonical current components with independent original/current CSS and
locale catalogs. `zombie-waiting` is measurement- and PNG-byte-identical. The
`garrison-pending` pair has one measured difference, corresponding to the disclosed
native-action fence: current `Run Now` remains disabled/presentation-only while the
recovered source can invoke its native provider. There are zero browser console
errors.

Canonical affected gates are green:

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`
- `node evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs`
- `git diff --check`

## Intentional fences

Do not enable or fake Garrison Run-now/stop/recall, Zombie provider/status polling,
protected original runtime/gameplay, or other native/backend actions. Positive data
remains restricted to explicit preview fixtures and does not masquerade as a live
provider. DOM-dispatched drag checks handler/state semantics only and are not claimed
as physical pointer-driven HTML5 drag.

No remaining source/local Milestone-2 correction is known at the coordinator gate.
Final project-lead acceptance remains separate from this worker checkpoint.
