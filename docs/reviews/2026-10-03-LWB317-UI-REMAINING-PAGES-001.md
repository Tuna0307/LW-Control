# LWB317-UI-REMAINING-PAGES-001 worker campaign — 2026-10-03

Status: **IN PROGRESS**. This worker owns only City Layout, Hotkeys, Mini Games and Settings source/local UI closeout. Native gameplay/service integration, login/account/licensing UI and unrelated accepted surfaces remain outside the assignment.

## Milestones

| Milestone | Status | Durable result |
|---|---|---|
| A — inventory/baseline | COMPLETE | 54 inventoried source branches, 15 exact recovered byte-locator groups, immutable 16-failure dispatch baseline and protected-WIP starting guard |
| B — City Layout | COMPLETE | Recovered placement/cell validation, pointer selection/group movement, 4–72 zoom, history/shortcuts, stale/outside-city/server/apply progress, 500 ms draft effect and inert apply confirmation |
| C — Hotkeys | PENDING | — |
| D — Mini Games | PENDING | — |
| E — Settings | PENDING | — |
| F — integration/evidence | PENDING | — |

The starting HEAD is `0c181ebc353556bc3c505eb93f1568f7d429172d`. Seven pre-existing protected paths were present exactly as expected and the protected-WIP checker passed before campaign edits. The reference executable hash also matches the required `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Milestone A treats the recovered minified assets as authority rather than the current screenshots. The inventory records source branch ownership and unavailable producers explicitly, and `coverage-matrix.md` fixes the finite page scope before product correction. `recover-inventory.mjs` passes against the exact packaged assets. The baseline checker intentionally exits non-zero at dispatch and preserves all 16 demonstrated defects in `milestone-a/baseline-results.json`; later milestones rerun the same predicates without rewriting that immutable record.

Milestone B removes the clone's guessed `cityCellKind`/8×8 validation contract. The preview fixture remains synthetic but now carries the same cell/building fields consumed by recovered `te/ne/m/re/ie/oe` semantics. `check-city-layout.mjs` executes **935 assertions**: 897 direct recovered/current helper comparisons across every fixture building/cell, 16 invalid/boundary scenario assertions, 16 production callback/state assertions in English/Japanese, two zoom callback assertions and four timer/cleanup/native-confirm guards. The four immutable City baseline predicates now pass while the later-page predicates remain intentionally open.

Real browser QA exercised the rendered zoom button, inert Apply confirmation, Japanese/dark stale branch and physical pointer movement. The first physical move exposed duplicate `pointerup` bubbling that created a no-op Undo entry; the building handler now stops propagation and one Undo restores the prior placement. Fresh browser console capture is empty. The recovered shell retains visited top-level pages through `Activity`; this clone's existing `PageForRoute` shell unmounts them. That cross-page shell architecture is outside this bounded page-component campaign and remains recorded as a known limit rather than being silently converted into a pass.
