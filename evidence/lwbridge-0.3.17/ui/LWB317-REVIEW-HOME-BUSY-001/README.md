# Independent Home busy presentation review evidence

LWB317-REVIEW-HOME-BUSY-001, 2026-10-02. Verdict: ACCEPT for the focused
recovered-source/current-local presentation scope.

`check-review.mjs` independently extracts the recovered original `Kr`/`qr`
presentation and the current `HomePage`/`App` expressions. It verifies six source
anchors and executes 17 explicit distinguishing cases covering root resolution,
folder/proxy/launch/preference busy states, overlaps, running/stopped, repair,
recovery and online/offline behavior. Current start/stop predicates are also
evaluated with an isolated available-provider input to compare them with original
`Kr`; the actual product provider remains unavailable and the controls fail closed.

`browser-results.json` records the assignment's bounded five-state browser recheck.
`home-launching-ja.jpg` is the inspected saved screenshot. `validate-evidence.mjs`
checks the review JSON, source byte anchors, current App hash, saved screenshot,
historical HOME-BUSY source/image records and final verification manifest.

The earlier HOME-BUSY report intentionally remains historical. Its saved App hash
is `136C6361...`, while the accepted root correction makes current App
`BA4A300D...`; the review therefore reruns its checker without rewriting the old
report and validates the unchanged original source/image evidence separately.

From repo root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-BUSY-001/check-review.mjs --verify-record
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-BUSY-001/validate-evidence.mjs
```

App currently produces `gameRoot`, `autoLaunchGame` and `autoReconnect` busy
values. `proxyBusy` and `gameLaunchBusy` remain recovered display inputs present in
preview fixtures but have no App production producer. No native picker, lifecycle
provider, game action, protected runtime or original pixel parity is claimed.
