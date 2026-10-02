# Original scan header and summary recovery

Lead-owned UI-only unit; baseline 315c5a7df6ccf69f30d5f1045274d9789efc1a18.
COMPLETE implementation for focused source/local UI scope. Returning-worker
independent review remains a follow-up; full UI/pixels/native is not accepted.

## Source authority

Target EXE SHA-256:
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
MapDataPanel-B4GXEND2.js SHA-256:
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
UTF-8 byte locators (EXACT_BYTES / EXACT_CONTRACT):

- 7885/8022/8197/8415/8598/8666: N/Je/Ye/Xe/Ze/P date/duration/time helpers.
- 32960: qn/Z/Yn/Xn/Zn stored-run matching, times, duration and progress.
- 33895: page clock effect (isReading or Dispatch/Ghost/Truck/Scheduled).
- 44467: header timing rendering, independent of manual/auto tab.
- 48969: summary label/progress rendering; no diagnostic counters follow it.

Original stored progress qualifies only while not reading, with matching browse
server and strict run ID and non-running stored status. Completed requires
that qualifying run to have completed status. Publishing takes precedence even
when isReading is false. Fractional percentages clamp but are never rounded.
Timing uses source start/end precedence, localized full-date tooltips, same-day
end-time shortening, ISO attributes and elapsed formatting. Source raw elapsed
arithmetic is preserved even for second timestamps; formatting converts units.

## Canonical implementation

mapScanPresentation.js recovers the pure helpers/expressions. MapDataPage renders
source-like timing and summary, tracks whether a real local provider state was
acknowledged, and runs the existing page clock while reading. Availability is a
frontend adaptation of the original optional scanState prop: missing => dash;
acknowledged zero state => 0%. Summary/listener/start/stop/Clear/Auto status paths
mark state available; no new backend contract/method/operation is added.
The original speed accessible label is restored. The six English diagnostic
counters were absent from the reference and removed from the user-facing UI.
Recovered CSS/catalogs, native availability/online action fences, existing
row-owned clocks and accepted query/filter/scheduling behavior remain intact.

mapScanHeaderFixtures has four explicit browser presentation states: reading,
completed, publishing and stopped. All stay online:false, reject all native
actions, and cannot be selected in native/native-unavailable modes. No fixture
data is copied into real native state and no scan is launched.

## Evidence

check-header.mjs executes actual original R through the existing exact-asset
runner and actual canonical Page through the real-import hook adapter. 270
timing/summary comparisons cover nine languages (including real locale tooltip
inputs) and 30 states. Immutable baseline differs in 29/30 English cases. Max
attributes are normalized as DOM-equivalent strings/numbers in the serializer.
Stored mismatches, zero/missing state, publishing/reading, fraction/clamp limits,
cross-day, seconds/string timestamps and missing/backward end cases are covered.
Six original/current clock stages cover tick/stop/freeze/resume; unmount cleanup
is also verified. Backend responses are controlled inert local objects.

Browser QA uses fresh owned IAB localhost:4329. English/light reading shows
37.75%, no end, ticking duration and timing preserved across Auto/Manual tabs.
Japanese/dark completed uses matching stored times, localized labels/tooltips,
short same-day end and 00:01:05. Publishing without reading shows Processing
99.75%; stopped shows 49.5% and fixed duration. Counters are absent; captures
were saved and visually inspected. Console error/warning capture is empty.

Run from repository root:

    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/check-header.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/check-fixtures.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/run-regressions.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/validate-evidence.mjs

Replay adapters preserve historical assertions/evidence and redirect parent/R1
outputs into this packet. Legacy bindings supply actual new pure helper exports
to old tests that strip imports. Maintained picker/lifecycle/request/navigation/
interaction/integration/filter/table/Checking/row/state tests pass. Canonical
check/build/package passes; hashes and WIP guard are validated. The initial
wrong-root npm command was rerun successfully from the canonical UI directory.

## Limits and continuation

No original protected runtime/pixel comparison or native timing/scan operation.
Current native MapScanState type (MapContracts.cs:64) has StartedAt, not UpdatedAt;
MapScanRun (line 101) has both stored timestamps. This is current source surface
evidence, not live timing proof, and backend producers were not modified.
Native stored-run/current-run availability and exact timing remain unproved.
Auto configuration parity, scan-row-refresh/poll cadence and start/export error
feedback remain separate UI follow-ups. All-page/global status stays unchanged.
