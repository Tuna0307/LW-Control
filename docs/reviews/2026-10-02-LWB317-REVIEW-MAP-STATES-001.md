# Independent Map state review — LWB317-REVIEW-MAP-STATES-001

Date: 2026-10-02. Recommendation: **ACCEPT for the three-table source/local scope**.
Reviewed current checkpoint: `ceffe100f896af7dc8ce6ea5f443c9ace31ea6fd`.
Reviewed implementation: `bf84bbdca8a86c1a45e9cbb3bd0170c8fbfcdd7f`.
This review changes no product code and makes no gameplay/native acceptance.

## Finding

No acceptance-blocking source/local mismatch was found in Secret Task, Ghost Ops or
Treasure presentation. The reference executable remains SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`, and the
recovered Map asset remains SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

The independent harness parses that exact recovered asset and executes the actual
original functions rather than copying their logic. Original timestamp normalizer
`P` is anchored at UTF-8 byte 8666, task-state function `F` at 8753, Treasure world
resolver `w` at 4705, player resolver `T` at 4957, Treasure name resolver `Ke` at
7610, and column factory `nt` at 9594. The original row status renderer `pe` is at
16822 and selector renderer `me` at 17072. Exact expressions and matching current
production locators are recorded in `source-local-results.json`.

With fixed time `1799000000000`, fourteen distinguishing task rows per kind and
locale exercise the recovered functions, current helpers and actual production
`MapTable`. The recovered precedence is preserved: expiry wins at the exact expiry
boundary, full wins over pending, completion at the current instant can enter the
protected state, and the exact plunder boundary becomes ready. Missing or invalid
expiry does not expire a task. Seconds and milliseconds normalize identically.
Missing completion remains pending. A visually ready row with missing `plunderAt`
is still unselectable, matching original `me` rather than treating visual readiness
as scheduling eligibility.

Selection eligibility also matches the original numeric-task-ID rule. Empty and
alphanumeric UUIDs are disabled; leading-zero numeric strings and numeric UUID
values are accepted. Expired/full rows are disabled. Pending/protected rows with
valid numeric IDs and both required timestamps remain selectable, as in the source.
The production render emits the same `map-task-status pending/full/protected/expired/ready`
classes and the corresponding English and Japanese labels.

Treasure cases cover world/player precedence, missing values and name resolution.
`37.5%` charging renders as `Charging 38%`; an invalid percentage keeps the plain
`Charging` label. Claimable, depleted, expired and explicit verifying states match,
while an unknown world state falls back to `-`. Claimed player state wins over both
foreign-alliance and no-scout blockers. Foreign-alliance wins over unclaimed or
failed player state; `no_scout`, `no_squad` and `squad_reserved` share the recovered
`No free squad` label; failed without a blocker falls back to `Not claimed`; unknown
player state falls back to `-`. When the recovered refreshing input is supplied,
missing world/player states correctly become `Checking`.

Treasure type/name handling also matches. Known type IDs use the recovered type
map, unknown type is `Unknown Treasure`, supplies keys take precedence over the row
name key, the challenge-zombie key falls back to `Trial Gift`, supplied game text
wins when it resolves to a different value, and a supplied value equal to its key is
treated as unresolved and falls back. English and Japanese table-column metadata
and rendered labels match the original column factory.

## Browser verification

Bounded browser QA used the reviewer-owned Vite server on port 4178 with
`previewPage=map-data&previewState=map-table-states`. A fresh English Secret Task
preview displayed, in fixture order, `In progress`, `Fully plundered`, `Protected`,
`Expired`, and `Available`, with classes `pending`, `full`, `protected`, `expired`,
and `ready`. Checkbox disablement was `[false, true, false, true, false]` and all
coordinate row controls were disabled. A fresh Ghost Ops load produced the same
states and disablement.

The first Ghost Ops observation was intentionally discarded after the synthetic
one-minute deadline elapsed: its pending/protected rows had correctly advanced to
Available. Reloading rebuilt fresh fixture timestamps and the final recorded check
captured the intended pending/protected states before their deadline. This prevents
using stale expected labels against time-relative fixture data.

English Treasure showed `Charging 38% / Not claimed`, `Claimable / Claimed`, and
`Depleted / Other alliance` in the first distinguishing visible rows. Japanese
Treasure showed `充電中 38% / 未受取`, `受取可能 / 受取済み`, and
`受取終了 / 他同盟`. Treasure claim buttons and coordinate row buttons were disabled
throughout the offline preview. The attached browser console contained no error
entries; only Vite connection debug messages and React's development-info notice
were present.

Two exact saved captures were visually inspected. `secret-task-states.jpg` is
78,712 bytes with SHA-256
`0C3A09F64D9C54A255755CE3E6553D4552C21DB7BDFD4B999A623C4CC8EEE98D`;
`treasure-ja.jpg` is 71,691 bytes with SHA-256
`D01C8862DB0B906CB0E7245B524F9235C818367799CFC51FC0195BB00F8BF612`.
The reviewer-owned tab was closed and the reviewer-owned Vite server was stopped.

## Checks

The prior lead Map checker passes `--verify-record`, including its 15,264 value
comparisons and 3,150 Treasure cases, and its evidence validator reports
`LWB317_LEAD_TABLES_EVIDENCE_OK`. The independent review harness passes
`--verify-record` with 56 task comparisons, nine Treasure world cases per locale,
eleven player cases per locale, nine Treasure-name cases per locale and six actual
production-table renders. The new evidence validator reports
`LWB317_REVIEW_MAP_STATES_EVIDENCE_OK`.

`npm.cmd run check` passes from `src/LWBridge.UI-0.3.17`, including static, Home,
Map, complete-key and draft checks. `npm.cmd run check:production-build` passes with
source fingerprint `1280d8a4df7aec2f81260a8081c0dda7626bad06e8771d25cea4e3ae0c49daf2`
and package fingerprint
`b2a903176188b57cd02f281ffaded42aa05a0d6503595e25ee3e5fcfa09127ae`.
An earlier invocation of those npm commands from repository root stopped at npm
`ENOENT` because that directory has no `package.json`; no project script ran in
that attempt. `git diff --check` passes; its only output is Git's line-ending warning
for the explicitly preserved pre-existing `previewAfkFixtures.js` WIP.

## Remaining UI limitations

The recovered original owns a Treasure-refreshing state `[jn,Mn]` at UTF-8 byte
31675 and passes `treasureStatesRefreshing:jn` to its table at byte 56776. Current
production `MapTable` accepts `treasureStatesRefreshing = false`, and the reviewed
helper produces the correct original `Checking` labels when that input is `true`.
However, the current `MapDataPage` call at normalized-LF UTF-8 byte 40877 does not
pass a refreshing producer flag, so that transient presentation is not reachable
through the current page. The work item explicitly separates formatter verification
from producer reachability, and working Treasure/native refresh is outside this
UI-only acceptance; this remains a follow-up limitation rather than a finding that
changes this review recommendation.

This review does not establish successful task scheduling, Treasure claiming,
coordinate jumping, native/gameplay providers, live game-text acquisition, or
original post-auth pixel parity. Synthetic supplied game text and placeholder
assets remain preview evidence only. Other Map tabs, Scheduled Plunder, Home and
Trade are outside this review.

Reproducible review evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-STATES-001/`.
