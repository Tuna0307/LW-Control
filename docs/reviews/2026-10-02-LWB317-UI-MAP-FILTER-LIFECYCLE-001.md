# LWB317-UI-MAP-FILTER-LIFECYCLE-001 delivery review

Date: 2026-10-02
Worker status: **AWAITING_REVIEW**
Project lead owns acceptance.

## Scope

This delivery implements the four assigned source-backed Map UI lifecycle
behaviors only: refreshed option validation, alliance sentinel separation,
acknowledged Clear frontend reset handling, and Treasure preference
defaults/persistence/query projection. It preserves the previously accepted
INTERACTIONS-001 and corrected NAVIGATION-001 behavior. No native Map,
gameplay, auth or unrelated Automation/AFK work is included.

The exact original frontend authority is
\`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js\`
with SHA-256
\`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089\`.
The assigned production baseline is the exact
\`c72aae6:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx\` blob preserved in this
campaign's evidence packet.

## Delivered behavior

### Refreshed filters and alliance identity

The production options callback now validates the current City alliance,
Secret Task level and Treasure type on every current options response, while
retaining a still-valid choice. Resource/Monster name validation remains
intact.

Real alliances are represented as \`name:<encoded name>\` and are decoded only
when constructing City search/export queries. Raw \`all\` and \`none\` remain
sentinels. This distinguishes actual alliances literally named \`all\` or
\`none\`; Unicode and URI-significant names round-trip. A malformed \`name:\`
selection follows the original decoder contract: it is retained by the option
validator when its decoded name is empty, but projects no alliance query value.

Options responses have their own generation fence. A response from a prior
server, Clear generation or unmounted effect cannot replace current options.
The existing search/page/cache ownership is unchanged.

### Acknowledged Clear lifecycle

On a successful acknowledgement, the frontend now resets the recovered state
set: alliance, Resource/Monster name, Secret Task level, per-kind item key,
Treasure type, options/count/result/cache state and both selection maps. It
retains the original's keyword, marked-only choice, per-kind
quality/status/plunderable filters, sorts, Treasure preferences, random-delay
input and active tab.

Failed or still-pending Clear preserves that data/filter state. Pre-Clear
search and options responses are fenced from restoring cleared data.

The original intentionally does not generation-fence the Clear request itself.
An independent final reviewer found one request-multiplicity mismatch in the
first implementation: Clear(321), transition to server 322, then delayed
acknowledgement for 321 produced option calls \`[322,321,321]\` instead of the
original \`[322,321]\`. Commit \`3e20fa1\` makes same-server Clear explicitly
reload options while allowing the existing server-transition effect to own the
reload when the acknowledgement changes the data server. The strengthened
test and independent replay both now observe exactly \`[322,321]\`.

Successful deletion is **synthetic-only proof** for this UI task. The
controlled hook runner returns a disclosed success acknowledgement to the
actual production callback/effects. Browser preview/native Clear remains
unavailable and was not changed to report success.

### Treasure preferences

The two recovered local-storage contracts are implemented exactly:

- \`lwbridge.mapIncludeForeignRadarTreasures\` initializes true only for the
  exact stored string \`true\`.
- \`lwbridge.mapLuckyTreasurePriority\` initializes true unless the exact stored
  string is \`false\`.
- both effects write \`String(boolean)\`;
- Treasure queries include both booleans even when false;
- unrelated tabs omit them;
- Treasure type keys map back to the source option's \`treasureType\` and
  \`suppliesType\` fields.

No Treasure native refresh/viewer/claim producer was connected.

## Baseline versus corrected evidence

\`check-filter-lifecycle.mjs\` runs the immutable c72aae6 page and current
production through the same persistent callback/effect harness. The baseline
reproduces the distinguishing defects: named \`none\`/\`all\` collide with
sentinels, invalid refreshed alliance/level/Treasure choices remain, an old
server options reply can win, successful Clear retains resettable filters, and
missing Treasure storage starts lucky priority false while Treasure queries
omit false booleans.

Current production passes the inverse cases plus preserved-valid selection,
no-alliance disappearance, malformed encoding, City export decode, Clear
success/failure/deferred/stale responses, same-server/post-transition option
reloads, storage missing/true/false/unexpected strings, toggle persistence,
remount/reload and tab-scoped query projection.

## Browser evidence

The worker exercised real controls against the deterministic offline preview.
In City, the select exposes raw sentinels \`all\`/\`none\` separately from
\`name:none\`, \`name:all\` and encoded \`A/B 東京 & %\`. Choosing those values
produced respectively the named fixture row, the unallied rows, the named
\`all\` row and the URI/Unicode commander row.

Treasure initially rendered the recovered missing-storage defaults
foreign-radar=false and lucky-priority=true. Real pointer toggles persisted
\`"true"\` / \`"false"\`; remount and full reload restored the same states. The
Lucky Treasure option produced the Lucky fixture rows and native claim controls
remained disabled. English/light and Japanese/dark were checked with zero
captured console errors. Task-owned storage keys were removed before closing
the attached browser tab.

The attached browser connector uses a fixed 1920 CSS-pixel viewport. Two
reversible native-window resize attempts did not alter that viewport and both
windows were restored. A separate isolated headless Edge profile therefore
captured the same offline Treasure surface at a real 375-pixel width in
Japanese/dark. The inspected 375x3000 screenshot shows the Treasure type
selector, both checkboxes, Search, disabled claim controls, result count and
rows; the existing table overflow remains internal. The task-local Edge
profile was deleted after capture.

## Regression and review result

Maintained INTERACTIONS/NAVIGATION replay remains green: request lifetime has
zero current failures across 38 scenarios, navigation replay has zero current
failures across 17 searches, the interaction differential matches the exact
original in 38/38 scenarios, and the strict integration suite passes all 14
scenarios. Historical replay covers accepted filters/tables/row states,
Treasure Checking isolation and Scheduled Plunder boundaries without a new
failure.

Canonical frontend validation also passes from
\`src/LWBridge.UI-0.3.17\`: \`npm.cmd run check\`, \`npm.cmd run build\`, and
\`npm.cmd run check:production-build\`. The production build reports package
hashes \`9c710c0c28448648769682aaf12073d2d1d2523b2ee96e5e1031fbed931a5bfd\`
and \`5a925d4c24e7f02e8056002b3c4b99d5d2c6000740dafdb0f07994d6e95b7e8f\`.
The campaign evidence validator reports
\`LWB317_UI_MAP_FILTER_LIFECYCLE_EVIDENCE_VALID\`, and \`git diff --check\` passes.

The older standalone NAVIGATION-001 \`check-navigation.mjs\` no longer evaluates
the later production page because its historical harness lacks
\`DEFAULT_RANDOM_DELAY_TEXT\`. That harness compatibility failure is preserved;
the maintained navigation replay used for PM-026 and this campaign passes.

Independent source recovery is committed at \`d04f470\`. The initial independent
final review at \`e1d556a\` identified the duplicate post-Clear options request.
After correction \`3e20fa1\`, the superseding independent review at \`6b654c5\`
reports PASS / ACCEPT for the assigned focused source/local UI scope, with no
remaining blocker or scope creep.

## Remaining limits

Overall UI remains \`IMPLEMENTED_NOT_VALIDATED\`. This delivery is not original
post-auth pixel proof, native Clear/Treasure persistence proof or gameplay
validation. Original runtime comparison remains constrained by the documented
auth boundary. Scan polling/header timing, row refresh timing, export/start
feedback, Treasure refresh/viewer/claim producers, native Scheduled actions,
native assets/texts and other separately tracked Map gaps remain outside this
assignment.

Protected pre-existing WIP remains unchanged and unstaged:
\`previewAfkFixtures.js\`, \`.scratch-lwb317/\`, and
\`LWB317-UI-CORRECT-003/screenshots/\`.
