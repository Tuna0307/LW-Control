# LWB317-UI-MAP-TREASURE-PICKER-001

Owner: project lead takeover, authorized by owner 2026-10-02.
Status: **COMPLETE implementation / independent peer review follow-up**.
Starting product: ff4ed369; preserve unrelated WIP.

Delivery: new source-derived picker and parent integration, 28 exact renderer/
callback comparisons, parent query/page assertions, documented historical adapters,
fresh en/ja browser pointer/keyboard QA, canonical gates and evidence validation
pass. See the dated review and evidence README. No original/native/full Map parity.

## Goal

Recover and implement the original Treasure type picker surface: details/summary,
menu buttons, source-resolved names and raw counts, strict key selection including
numeric keys, active classes, All option and close-after-selection. Use the
existing recovered CSS and canonical treasureName resolver. Preserve parent
filter validation, page reset, query projection and Treasure preferences.

Read AGENTS.md and AI_WORK_PROTOCOL. Exact original MapDataPanel-B4GXEND2.js is
SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
Original lt starts at UTF-8 29030; resolver Ke at 7610; parent caller at 52026.
Revalidate full expressions and locators. Canonical frontend is src/LWBridge.UI-0.3.17.

## Proof and completion

Compare unmodified original component renderer against actual production markup
and callbacks with distinguishing baseline evidence, English/Japanese labels,
empty/options/unknown/missing/strict numeric-versus-string keys, counts and
close semantics. Test parent page reset/query via actual callbacks. Real offline
browser QA must cover opening, selecting, All, reopening, selected label and
keyboard activation, plus screenshots/console. Use existing original menu CSS;
do not redesign or add unsupported outside-click/Escape behavior.

Run affected filter/state/navigation/Checking regressions with new read-only
adapters where historical select-based harnesses are stale. Keep historical
scripts/results/manifests unchanged. Run canonical check/build/package, new
evidence validation, protected-WIP and diff checks. Write evidence/review and
current docs, commit/push and verify remote. Stop at this bounded unit.

Do not launch/control Last War, implement native producers/actions, or claim
original runtime pixels/full Map UI completion. Keep previewAfkFixtures.js,
.scratch-lwb317/, CORRECT-003 screenshots and the existing historical lifecycle
JSON normalization untouched/unstaged. Retain coherent checkpoints and handoff.
