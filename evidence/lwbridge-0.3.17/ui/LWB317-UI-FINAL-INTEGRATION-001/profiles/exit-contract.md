# App exit modal — recovered/local proof

Exact main-bundle contracts: `qi` producer UTF-8 byte **375835**, `Ji` renderer
**376111**, shared `In` modal **209017**, native-command marker `Bt` **205293**.
`exit-results.json` records exact byte lengths/hashes and current product identity.
The native marker is evidence only; no command is invoked by the new code.

`AppExitDialog.jsx` accepts `instanceCount`, `busy`, `onCancel` and `onConfirm`.
`instanceCount === null` renders nothing; zero still renders the source dialog.
The source has **no countdown**. Count is used in the description/Confirm text.
Cancel is initially autofocus; both controls disable while busy, busy confirmation
shows localized Closing, Escape closes only while idle, backdrop does not dismiss,
and the native dialog uses the recovered Tab wrap and conditional focus restoration.
Original warning PNG is reused byte-for-byte as `assets/icon-warning.png`, SHA-256
`DA1A18E0007068C11D371A9D130C598F9B9A38F39F87D758FCC7FB9CC467853F`.

Optional `AppExitPrompt` implements the supplied event lifecycle: opening event
resets busy and stores its count, Cancel stores null, Confirm marks busy and invokes
the callback immediately, rejection clears busy. Successful acknowledgement does
not dismiss the modal in recovered source: original native exit closes the process.
An explicit preview fixture may dismiss its own modal without closing the browser,
game, native app, or any owner's session; that fixture behavior is separate from the
original success path. Missing confirm callback disables the confirm control and
cannot invoke a native action, including direct production callback invocation.

Root integration owns any event/provider binding and explicit browser-only fixtures.
This isolated component neither registers bridge listeners nor imports a bridge or
native command. Production has no supplied exit provider. Absent callbacks remain
fenced; no source behavior is claimed for an unavailable native producer.

`check-exit.mjs` passes **33 case groups**, 32 against the executed actual original
renderer/controller and one deliberate absent-provider boundary. EN/JA nullable,
zero, one, several, many and busy/idle states are compared without DOM normalization.
Cancel/Confirm/Tab/Escape/backdrop and deferred rejection/success/new-event behavior
use actual rendered React handlers. `--record` writes only `exit-results.json`.
`validate-exit.mjs` verifies target EXE, source slices/current component and exact PNG.

Dialog/client-rect APIs are controlled jsdom shims. No physical browser modal,
native process close, gameplay action, original service access or original pixel
comparison occurred. Root must independently review and perform local browser QA.
