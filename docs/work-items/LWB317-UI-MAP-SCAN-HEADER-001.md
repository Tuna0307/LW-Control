# LWB317-UI-MAP-SCAN-HEADER-001

Owner: project-lead takeover. Baseline: 315c5a7df6ccf69f30d5f1045274d9789efc1a18.
Status: COMPLETE implementation for focused source/local UI scope;
independent peer review follow-up. Phase: UI parity.

Goal: recover original scan timing/header and summary presentation from exact
MapDataPanel-B4GXEND2.js. Relevant UTF-8 bytes: helpers N/Je/Ye/Xe/Ze/P at
7885/8022/8197/8415/8598/8666, qn/Z/Yn/Xn/Zn derivation 32960, timing caller
44467 and summary 48969. Target/source hashes must be verified.

Scope: start/end/duration rendering, matching stored scan progress precedence,
publishing/reading/completed/stopped labels, exact progress fraction/clamp,
state-present/absent distinction, page clock while reading, speed accessible
label, removal of the extra diagnostic counters absent from the source.

Intentional constraints: preserve native availability/online admission fences,
accepted query/filter/cache/scheduling behavior, backend/provider schemas, and
all protected WIP. Reuse original CSS/catalogs. Preview states remain explicit,
offline and unavailable in native modes; all operations reject.

Non-goals: live scans/gameplay, native timing producers, Auto configuration
parity, row-refresh/poll cadence, start/export error feedback, original protected
runtime/pixels, other pages. Do not open a function-integration campaign.

Acceptance: actual original/current summary/timing renders and clock/lifecycle
cases; immutable baseline mismatches; all nine languages; stored mismatch,
publishing without reading, fractional/boundary progress and timestamp cases;
fresh en/light and ja/dark browser evidence; fixture fences and Map regressions;
canonical check/build/package; hashes/evidence/protected-WIP guard; docs/commit/
push/direct remote verification. Independent returning-worker review follow-up.

Delivery: 270 original/current comparisons, 29 distinguishing baseline failures,
six clock stages/unmount, four offline states/eight native-mode fences/28 action
rejections, fresh browser timing/status/progress/tab QA and maintained Map tests
pass. Canonical check/build/package and evidence/WIP guard pass. See the dated
review and matching evidence README. Native/global/original-pixel scope unchanged.
