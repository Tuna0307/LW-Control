# PM-018 — integrate Home error-channel review

Date: 2026-10-02. Reviewed worker checkpoint:
`db3aae317321d9ac21774f421dd363daa8a146c3`.
Decision: **CHANGES_REQUIRED** for LWB317-UI-HOME-ERROR-002.
Independent review is complete; the correction is not implemented or accepted.

Lead independently reproduced the review's eight actual-callback/real local
bridge scenarios with `review-home-error-channels.mjs --verify-record` and ran
its evidence validator. Both pass while reproducing the parity defect. Separate
root/action placement, picker cancellation/invalid/failure behavior and preference
profile/acknowledgement behavior are retained useful implementation.

The exact original index-BVfnK1wp.js SHA-256 is
44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 locators: qr 336694, Jt 367489, Xt 367703,
initial root request 369540, periodic status/proxy requests 369979.
Jt sets root status and clears root error. The initial request uses
`Ht().then(Jt).catch(e=>x(String(e)))`. Lead directly inspected its enclosing
effect: it returns without a selected profile and depends on
`[r.selectedProfileId]`. Thus "one-time" means once for this profile effect,
not an unconditional process-wide request. The five-second interval polls only
status/proxy and does not refresh root status. Current App refreshStatus instead
repeats root-status requests and applies only the status setter.

Adding error clearing to that repeated request would erase a canceled picker's
preserved prior error on a later timer tick. The assigned correction separates
root retrieval from periodic polling, matches the recovered acknowledgement/error
path and existing local profile lifecycle, and applies the same successful
acknowledgement semantics after a valid selection. Other polling/listeners and
independent action errors stay outside the correction.

Canonical `npm.cmd run check` and `check:production-build` pass. Product code did
not change, so no additional rebuild was needed. Package fingerprints remain
53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e /
96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782.
Target executable hash and direct remote revision were independently verified.
Lead verification is under `evidence/lwbridge-0.3.17/ui/LWB317-PM-018/`.

Next: `../work-items/LWB317-UI-HOME-ERROR-002-R1.md`, one bounded correction.
Historical exact-source reports must remain immutable; a changed App hash or
callback expression requires correction-specific verification, not rewriting an
old report to appear current. Busy rendering can also be checked by running its
existing assertion harness without the saved-record equality flag.

Translation and shared switch localization remain accepted. Busy and Trade 003E
remain AWAITING_REVIEW. Global UI/native/original-pixel acceptance is unchanged.
No native picker, game, protected service or new browser session was opened.
AFK/scratch/parent screenshot WIP remains untouched and unstaged.
