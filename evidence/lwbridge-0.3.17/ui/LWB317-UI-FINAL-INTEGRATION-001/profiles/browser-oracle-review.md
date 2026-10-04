# Busy exit Escape comparison — 2026-10-04

Classification: **no clone-specific defect demonstrated**. The lead's real IAB
comparison executes exact original `In` (UTF-8 byte 209017, length 1209) and `Ji`
(byte 376111, length 881) against the canonical `AppExitDialog` body in the same
browser with English, `busy=true`, count 2, and inert callbacks.

In both paths the dialog starts open and busy. Trusted Escape keydown is followed
by trusted `cancel` and `close` events. Both recorded cancel events still have
`defaultPrevented=false` after dispatch; both finish with `dialog.open=false`,
`aria-busy="true"`, and the component still mounted. Neither trace records an
`onCancel` callback. The observed behavior is identical in this browser fixture.

The source/current callback contract remains verified separately: `onCancel`
calls `preventDefault()` and only calls the close callback when not busy. The
33-case controlled Exit suite passes. That synthetic callback proof does **not**
prove physical busy-Escape fencing in the browser, and this browser observation
does not establish its cause or behavior in the protected original runtime.
No speculative product fix is justified by this comparison. Japanese physical
Escape was not separately driven in the supplied trace.

## Pinned evidence

- `../browser/exit-browser-comparison.json`: SHA-256
  `3B02F3A262A67054F648FF9D71C1257781D205F743A70D3282E5A36C225081C4`.
- `browser-oracle.html`: SHA-256
  `F0FE4CDF723F8D62ADFE698AB43CF80139AEB639D4A36C89700C127690382F3F`.
- `browser-oracle.bundle.js`: SHA-256
  `67276E767F5B684B7550E6471B8E8B7A2B51AC059B44E647081F3EFECBA6790D`.
- Canonical `src/LWBridge.UI-0.3.17/src/AppExitDialog.jsx`: SHA-256
  `AFA79399788E4F8C58FA81C11FF86CB7128CBF5DD02A84BD511E88A928A6549D`.

`browser-oracle-identities.json` additionally pins exact original slices, the
original asset and generated artifacts. The fixture captures events without
replacing the renderer's handlers. No native exit, gameplay, service access,
master-status change, or product edit occurred in this review lane.
