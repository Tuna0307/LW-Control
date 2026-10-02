# Milestone B — summary/options refresh ownership

Authority is the exact recovered 0.3.17 frontend: `MapDataPanel-B4GXEND2.js`
(`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`)
and `index-BVfnK1wp.js`
(`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`).

The original parent owns Map summary bootstrap, scan events, completion summary
refresh and the connected five-second summary poll. The mounted Map panel owns
data options by data-server plus an explicit options revision. Scan progress
refreshes rows only; scan completion refreshes rows and options. A summary-only
poll does not request options.

Exact UTF-8 anchors are recorded in `source-locators.json`: parent summary
producer byte 363957, parent visible/connected poll byte 370275, panel scan
completion byte 34224, progress timer byte 34459, options request generation
byte 34913, count acknowledgement byte 35085, and the options
`[dataServer, optionsRevision]` dependency boundary byte 35643.

`check-refresh-ownership.mjs` executes both current production owners. The App
harness compiles the actual `App.jsx` callback/effect body with inert bridge
responses and a deterministic interval+timeout clock. The panel harness executes
the actual current `MapDataPage.jsx` in its production controlled shape. The
checker also preserves the immutable `123459d` counterexample: that baseline
nested `loadOptions(summary.serverId)` inside the child summary callback.

The current proof distinguishes ownership directly: App mount creates the selected-
profile bootstrap plus the connected immediate summary request, the five-second
poll does not overlap itself, completion requests one parent summary, profile
replacement fences obsolete replies, cleanup/unmount fences deferred work, and
listener subscriptions are balanced. The controlled panel makes zero summary
requests and installs zero scan listeners; its options trace is `[321,321]` for
bootstrap plus completion. Progress advances rows only, mount-already-reading then
completion advances options once, and a completion options refresh clears a now-
invalid Resource name. Clearing the controlled parent summary also clears local
count readiness, matching the recovered parent-summary effect.

The existing exact original/current lifecycle suites remain the behavioral
oracle for mismatched option-server redirects, obsolete replies, data-server
change/loss, delayed Clear and request retirement:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/milestone-b/check-refresh-ownership.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/milestone-b/replay-filter-lifecycle.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-cases.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-PM-027/independent-cases.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/check-feedback.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/milestone-b/validate-evidence.mjs
```

The broad historical FILTER-LIFECYCLE checker still looks for the pre-recovery
Treasure native `<select>`, so it is stale against the current picker and
cannot reach its later Clear scenario. The focused replay above executes the
current production source directly and preserves the accepted delayed-Clear
`[322,321]` request trace without touching the protected historical result.
The R1 and PM-027 suites independently retain options redirect, obsolete reply,
server-loss and lifetime coverage.

The old standalone NAVIGATION-001 extractor is also pinned to an older component
shape and lacks later constants such as `DEFAULT_RANDOM_DELAY_TEXT`. Current
navigation behavior is exercised through the maintained actual-production
interactions suite, including the server-change/page-3 boundary. The historical
integration scenario that advances five seconds and expects Resource options to
reload is intentionally not adopted: exact 0.3.17 keeps options on
`[dataServer, optionsRevision]`, so the current checker exercises the same name
invalidation on the source-backed completion revision instead.

Canonical `npm.cmd run check`, `npm.cmd run build` and
`npm.cmd run check:production-build` pass. Offline browser smoke is recorded in
`browser-smoke-results.json` with inspected screenshots under `screenshots/` for
English/light and Japanese/dark. Both states render server 321, counts
`52..59,0`, 50 City rows and no console warning/error. These captures are preview
evidence only; no native operation was performed.

This milestone changes frontend refresh ownership only. It does not launch the
game, add a native provider, or execute a Map scan.
