# HOME-004 R6 — original error oracle and same-owner lifecycle admission

Date: 2026-10-10. Baseline `41b7417f30310c8d4a9e1110144d0106fe9640ea`;
original campaign starting SHA `f12dd566bd1410c2ccb17e054cd85978ace1c8a4`.
Whole Home: **PARTIAL_NEEDS_INPUT**; draft PR #6, not merged or published.

## Direct recovered original 0.3.17 authority corrects R5

Read-only `git show origin/research/offline-controller` recovered
`evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-001/helper-render-results.json`.
It pins original frontend `index-BVfnK1wp.js` SHA256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
Exact original **Ir byte 328453** pushes optional `Error.code`, then
regex-matched message codes, removes duplicates, and explicitly calls
`[...new Set(codes)].reverse()`. **Lr byte 328684** searches
`error`, `auth.error`, `update.error` per code before
`common.actionFailed`. Original independent research review
`docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-001.md` confirms this
order and original nine-locale rendering.

R5's first-token-priority 'fix' was a **regression**. R6 restores exact
reverse-code iteration in both actual `HomePage.jsx` and `ProfileSidebar.jsx`.
Source-helper regression asserts later `LAUNCH_TASK_FAILED` wins over earlier
`GAME_CLOSE_FAILED`, deduplication precedes reversal, structured `code`
is considered after translated message codes, and unknown text falls back
to localized generic. Preserve R5's original failed tests as historical
records; they were not proof of original-first priority.

Original **H-18** manual Home Update-and-Launch displays the first *global*
restart error; **H-23** startup reconciliation filters errors to the
currently selected profile. An actual mounted B-only inert helper Stop
failure while A was selected initially failed an R6 assertion that B's
error should be hidden:
`artifacts/home-004/r6-foreign-error-inverse-en-light.json.error.txt`.
This is an incorrect test hypothesis, **not an original-contract defect**.
The corrected production-mounted test affirms B's global error is shown,
selected A still completes its own repair, B later retries successfully,
and exact Stop All restores both. No frontend result was fabricated.

## R6 independently reproduced H-47 UI lifecycle race

The actual production App's Home Start and selected-profile sidebar Start
used separate in-flight sets. With Home A Start delayed *before its native
producer*, clicking sidebar A Start entered that same owner, bypassing Home's
busy guard. Original actual-WebView negative:
`artifacts/home-004/r6-cross-control-inverse-en-light.json.error.txt`.

R6 shares per-profile in-flight ownership between Home and sidebar, and
holds an explicit all-owner guard during startup reconcile or global
Update-and-Restart. Actual mounted proof checks **both directions**:
held Home A then sidebar A; held sidebar A then Home A. Each completes
one and only one native Start with exact later Stop. In parallel an
unselected B can Start and Stop while A is pending, without changing
selected A or either owner's lifetime.

## Verified scope and open inputs

Actual mounted production native App/WebView Home-only EN/light and JA/dark
proof receipts `artifacts/home-004/r6-final-en-light.json` and
`r6-final-ja-dark.json` pass. Owners A/B stopped; zero genuine Last War
launches, zero Map scans, native requests/subscriptions/handlers retired
and isolated roots removed. Desktop Release native checks, Python launcher
tests, concurrent real Windows named-pipe transport and frontend checks
remain independent verifications; final ZIP/source identity and normal GUI
receipts are recorded under `artifacts/home-004/r6-*` after release build.
These checks are **controlled**, distinct from previously accepted genuine
single-client Launch/Connected/Close, adoption and hang/exit results.

Remaining original/protected inputs: legitimate 0.3.17 ticket/lease/
finalizer responses and signed controller plaintext (H-05/H-13/H-28/H-33);
real official launcher restart/update/descriptor/mutex/failure traces
(H-06–09/H-18–20/H-44/H-46); independently supported two genuine client
installations and licensed capacity (H-22/H-24/H-38–40); genuine paired
offline-only, prolonged maintenance, pending Stop, restoration/repair
failures and original conditional EN/JA states (H-34–36/H-40–47).
Current authorized read-only original source and inert local probes cannot
establish those protected/genuine outcomes. No unsupported schema,
service access, game modification, Map work or main publication. Next
action only when legitimate inputs are available: capture original and
current paired evidence with exact identity and backed-up installation,
correct any actual new discrepancy, then seek independent lead review.
