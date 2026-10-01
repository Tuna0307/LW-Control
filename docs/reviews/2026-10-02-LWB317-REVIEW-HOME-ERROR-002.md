# LWB317-REVIEW-HOME-ERROR-002 — independent Home error-channel review

Date: 2026-10-02. Baseline: `a4efdd0c2f195f5b1878492c5fce60a949e14813`.
Recommendation: **CHANGES_REQUIRED** for the focused source/local scope.

The separate root/action channels themselves are correctly wired. `Pages.jsx`
keeps `gameRootError` in the missing-root picker row and renders
`gameActionError` independently below the root controls. The actual current
callbacks also preserve the opposite channel: picker cancel/invalid/failure,
deferred valid selection plus status acknowledgement, and status failure leave a
prior action error intact; preference/reconnect outcomes leave a prior root error
intact. Reconnect still injects the selected profile, rejects a missing profile
before dispatch, and updates checked/config state only after the deferred native
acknowledgement.

One required parity defect remains in the root-status producer/clearing path.
The pinned original asset is SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
Direct zero-based UTF-8 source checks locate `qr` at 336694, `Jt` at 367489 and
`Xt` at 367703. `Jt` is exactly `function Jt(e){g(e),x(\`\`)}`: a successful
root-status acknowledgement updates root status and clears the root error. The
original effect routes `game_root_status` through `Ht().then(Jt)` at byte 369540,
while the original five-second poll at byte 369979 is
`Promise.all([ot(e).catch(()=>null),Vt(e).catch(()=>null)])`, polling status and
proxy status only.

Current `App.jsx` instead includes `game_root_status` inside `refreshStatus`, and
that same callback runs every five seconds. Its fulfilled `rootResult` branch only
calls `setGameRootStatus(rootResult.value)`. The independent checker starts with
`gameRootError = "ROOT_PRIOR"`, delivers a successful synthetic root-status reply
through the real frontend bridge, and observes a valid new root status while the
root error remains `"ROOT_PRIOR"`; executing the exact original `Jt` with the same
status clears the root error. This retained polling difference matters to the
error-channel contract rather than being a harmless implementation detail.

The correction should preserve both sides of the original behavior. Merely adding
`setGameRootError("")` to every current five-second root reply would cause an
Xt-style canceled picker, which correctly preserves its prior root error, to lose
that error on the next poll even though the original periodic poll never requests
root status. The smallest source-shaped correction is to move `game_root_status`
out of the repeated `refreshStatus` poll into a one-time root-status
acknowledgement path, apply Jt semantics there (set `gameRootStatus` and clear only
`gameRootError`), and reuse the same acknowledgement behavior for the explicit
post-selection status reply. `gameActionError` should remain untouched, and the
existing five-second status/proxy polling can remain as-is.

The current local host mapping matches the callback assumptions: the existing
`NativeGameRootSelectionResult` exposes `Canceled`, `Path`, `Valid`; cancellation,
invalid and valid returns map to the expected values; `LWBridgeWindow` serializes
with the shared web JSON options; and `LocalConfigStore` applies camelCase naming.
The reviewed source hashes are `31BC28DA...E96911` for
`GameInstallationService.cs`, `15397E98...3238F2` for `LWBridgeWindow.cs`, and
`A3154052...E66C9C` for `LocalConfigStore.cs`. No host code was changed or run.

Browser QA on the existing `home-errors-both` fixture showed the translated invalid
root message inside the picker row and the xLua action error independently below
it. The `home-error-action-missing-root` fixture showed `Not detected` in the
picker row while retaining the same independent action error. Choose Folder, Open
games at startup and Automatic Reconnection were disabled in both browser-only
fixtures. No browser console errors were captured. The fresh screenshot is
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/both-errors-review.jpg`,
SHA-256 `BFAFAA4AF46668BF58C19DA0928D3DE789EB18EE8E915DD6A0036E1FB6A75D36`.

The existing HOME-ERROR-002 checker still passes its 9 callback scenarios, 4
original picker cases and 72 render comparisons, and its saved evidence validator
passes unchanged. HOME-ERROR-001 translation, shared switch localization and the
pending Home busy checker all pass as regressions. Canonical `npm.cmd run check`,
production build and package verification pass with source fingerprint
`53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e` and artifact
fingerprint `96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782`.
The reviewer checker and evidence validator are under
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/`.

Limits: callback/native transport responses were synthetic and local. The review
did not execute a native picker, gameplay action, preference persistence, protected
original runtime, or original pixel comparison. Rejection string reduction remains
the pre-existing current behavior and was not changed. Home busy was exercised only
as a regression check and remains outside this review. Product code and prior task
evidence were left unchanged.
