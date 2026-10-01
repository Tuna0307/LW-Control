# LWB317-REVIEW-HOME-ERROR-002 evidence

Independent review evidence for the Home root/action error channels. The review
recommendation is `CHANGES_REQUIRED` because current `refreshStatus` repeatedly
polls `game_root_status` but does not use the original `Jt` acknowledgement
semantics. The recovered original fetches root status separately through `Jt`,
which updates status and clears the root error, while its five-second poll covers
status/proxy only.

`review-home-error-channels.mjs` parses the pinned original source and current
`App.jsx`, executes the actual extracted Home callbacks through
`createBackendBridge` with synthetic response envelopes, and records eight
distinguishing scenarios plus the polling/clearing reproduction in
`review-results.json`. It verifies exact zero-based UTF-8 anchors `qr` 336694,
`Jt` 367489 and `Xt` 367703 against the original asset hash.

`browser-review.json` records the two bounded browser fixtures required by the
assignment. `both-errors-review.jpg` is the fresh reviewer screenshot. Native
controls were disabled in both fixtures and no captured console errors occurred.

Reproduce from the repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/review-home-error-channels.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/validate-review-evidence.mjs
```

This evidence is synthetic/local only. It does not execute the native picker,
gameplay, persistence, protected original runtime, or original pixel comparison.
