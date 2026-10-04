# LWB317-UI-MAP-AUTO-VISUAL-001

Status: AWAITING_REVIEW through owner relay, 2026-10-04. Size: small.
Baseline: 5dfedb0edba0a0c1db9f9f5446f9e8d67f33a65e.

## Goal and scope

Compare only the Auto configuration card (.map-auto-scan-card) against the actual
recovered original rendering. This is evidence-only; no production changes.
Cover labels/fields/master switch/server chips/type chips/options/Run now inside
the card. Exclude Manual header, summary, browsing/filter/table surfaces, native
execution and new behavior research. Accepted Auto interactions and the Manual
type correction must remain unchanged. Work alone, no subagents.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, docs/lwbridge-ui.md.
Use research/offline-controller and inventory/preserve all starting WIP.

## Inputs and method

- Source: frontend-package/web/assets/MapDataPanel-B4GXEND2.js under the 0.3.17
  UI evidence tree; verify the EXE/asset identities and exact locators using the
  accepted packets. Current source: src/LWBridge.UI-0.3.17/src/MapDataPage.jsx.
- Reuse actual original/current execution from the INTERACTIONS original harness
  and FILTER-LIFECYCLE harness. The MANUAL-VISUAL-001 packet shows how to render
  executed trees and compare browser measurements. Keep all parent scripts and
  recorded results immutable; place adapters/results in this new packet.
- AUTO-CONFIG-001/original-source-locators.json and check-controls.mjs provide
  recovered configuration inputs/control references; derive fields/defaults
  from those sources instead of inventing them.

Use two samples only: original default config, then a source-valid enabled config
with multiple server chips and selected types. Execute both original/current
components with identical inert inputs and enter Auto via their actual tab
callback. Scope output to the executed card with the same documented container.
Do not handwrite reference markup or invoke Run now/scan/native handlers.

Capture two original/current browser pairs: default EN/light at1280 CSS px;
configured JA/dark at375 CSS px. Use original recovered CSS on original and
current reference.css then styles.css on current. Pin source slices/dependencies,
config inputs and stylesheet hashes. Record raw DOM differences, measured
rectangles/styles and screenshot hashes; inspect all four screenshots. Keep
provider/state differences visible. Do not normalize mismatches into a pass.
Isolated source-rendered proof does not establish full-shell/protected-runtime
pixels. A mismatch is a useful result; no code correction is assigned here.

## Delivery and stopping point

Write one concise README, reproducible comparison script(s), source/input pins,
two-pair browser records/screenshots, integrity checker and dated review under
LWB317-UI-MAP-AUTO-VISUAL-001. Include each discrepancy's original/current locator
and reproduction. Reuse existing tooling; avoid a new generic QA framework.

Run the focused comparison/integrity checker, canonical frontend check and
check:production-build, git diff --check and HOME-PREFERENCE-LIFETIME-001B's
ten-file WIP guard without --record. No production build is needed for an
evidence-only task. Do not rewrite historical validators for changed hashes.
Preserve owner port4335 and clean up only owned browser/server helpers.

Update this item's delivery, commit only owned evidence/review/work-item paths,
push and verify exact full remote SHA. Return AWAITING_REVIEW to the lead through
the owner, including matches/differences/limits/checks/SHA. Stop after these two
pairs; do not expand to another Map surface or page. No fixed elapsed-time stop.

## Delivery

Exactly two executed-source browser pairs are recorded under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-VISUAL-001`. Default EN/light at
1280x720 is structural/markup/pixel exact. Source-valid configured JA/dark at
375x1000 matches all labels, controls and containing geometry, with one localized
server-chip mismatch: recovered original renders its remove child through the
14x14 `Vr(remove)` SVG while current `MapDataPage.jsx` line 1041 renders a text
`×` span (~7.59x13). All 12 measured geometry and 9 style differences belong to
those three child glyphs; parent chip/button/card/type/option anchors match.

No production code changed and no Run now/scan/native handler was invoked. See
the packet README/pins/browser records and
`docs/reviews/2026-10-04-LWB317-UI-MAP-AUTO-VISUAL-001.md`. Required focused,
canonical/package, protected-WIP and Git checks pass; package integrity reports
`865208f6…` / `7f3bbbf9…`, and the protected guard checked all 10 paths. The
final commit/remote SHA is reported in the owner handoff.
