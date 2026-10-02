# LWB317-REVIEW-MAP-FILTERS-001 independent evidence

Date: 2026-10-02

Reference executable SHA-256 was independently verified as `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

## Original-source locators checked directly

Recovered asset: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`.

- UTF-8 bytes around 30908-31016: four independent object state hooks exist for quality (`It/Lt`), retained goods (`Rt/zt`), completion status (`Ht/Ut`), and plunderable-only (`Wt/Gt`).
- Bytes 37497 and 37940-38120: query construction reads those objects by active kind; `itemKey` is restricted to Truck/Train; completion is Dispatch/Ghost; plunderable-only is restricted by applicable kind; Secret Task writes the same selected `Kt` value to both `minLevel` and `maxLevel`.
- Byte 52663: completion handler updates `{...current, [L]: value}`.
- Byte 53247: quality handler updates `{...current, [L]: value}`.
- Byte 53914: retained-goods handler updates only `[L]`; clearing removes `itemCount` only from `Jt[L]`.
- Byte 54210: plunderable-only handler updates only `[L]`.
- Byte 56776: table receives active-tab `sortState:Jt[L]` and original Treasure refreshing flag `jn`.
- Bytes 13020-13187: missing Treasure world/player state becomes `verifying` only while the refreshing flag is true.
- Byte 5025: player-state formatter gives `claimed` first precedence, then `other_alliance`, then no-scout/no-squad/squad-reserved blocking reasons, before the ordinary state map.

## Production code cases

`independent-cases.mjs` imports the production `getMapPreviewProvider` and `buildMapColumns` modules. It does not consume the author contract/harness. Result:

```text
PASS
checkingRows: 45
missing world -> map.treasureStateVerifying
missing player -> map.treasurePlayerVerifying
missing player + other_alliance -> map.treasurePlayerOtherAlliance
known claimable -> map.treasureStateClaimable
known claimed -> map.treasurePlayerClaimed
map-table-states refreshing flag -> false
native/native-unavailable/non-map provider -> null
```

## Independent browser observations

Browser preview was exercised in the existing Chrome session against a local Vite server at `127.0.0.1:4317`.

- Truck: selected Reindeer quality and checked plunderable-only. Switching to Train displayed blank quality and unchecked plunderable-only. Switching back to Truck restored Reindeer and checked state. This distinguishes active-tab state ownership from one shared scalar.
- Secret Task: selecting `special` quality did not alter Truck's Reindeer selection, again distinguishing per-tab quality ownership.
- Native action controls remained disabled in preview mode.
- Console capture after attachment contained only Vite connect/connected debug messages and the React DevTools development info message; no warnings, uncaught exceptions, or console errors were recorded.

The author screenshots `treasure-checking-en.png` / `treasure-checking-ja.png` were visually inspected. They are synthetic browser-preview evidence and are not treated as original-product visual proof.

## Visual/reproduction limitations

- A fresh preview URL can open the shell on Home and requires entering Map before the fixture page is visible. This is existing preview-shell routing behavior outside the reviewed production diff.
- During the independent `map-treasure-checking` browser pass, the shell retained an empty Map summary (`serverId: -`) and therefore did not issue a populated table query in that session. Checking semantics and fencing were consequently verified independently through the exported production provider/table code, while the supplied author browser capture provides the populated visual example.
- Post-auth original pixel comparison remains blocked by the original application's authentication boundary; this review does not claim full Map/UI/pixel parity.

## Requested checks

All passed read-only:

- `check-filters.mjs`
- `check-treasure-checking.mjs`
- `check-table-regression.mjs`
- `validate-evidence.mjs`
- `npm.cmd run check`
- `npm.cmd run build`
- `npm.cmd run check:production-build`

Production/package fingerprints reported by the build checks:

```text
b09ef2120b8f67b5de0f9430a814ae0c9d33e50a5e0ba267597063c70c10f7eb
b56793f5b4a7934be8e56032ca447c87ee0f81eb3f430e9def803dd015119d34
```
