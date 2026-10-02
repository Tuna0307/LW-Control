# PM-026 — project-lead Map interactions closeout

Review target: `5b76ae8111e160ad7673e1162e8a354d1c3cbd45`.
Project-lead decision: **COMPLETE / ACCEPTED for focused source/local UI scope**.
All four INTERACTIONS-001 milestones are complete; NAVIGATION-001's PM-025
request-lifetime defect is resolved and its focused scope is accepted too.
Owner requested project-lead continuation after the worker reached its usage limit.

## Work and verification

All four campaign milestones exist in the checkout at worker commits 3e8617c,
c57be79 and 5b76ae8. Lead inspected the production diff, request/search/selection
implementation, provider availability fences, preview wiring, Scheduled Plunder
presentation and recorded original-source contracts. Target EXE and original
asset identities remain pinned. No additional production change has been required
by this review; the completion work is independent validation and evidence/docs.

Lead reran without rewriting worker results:

- Request lifetime: 38 runs; baseline 0 defects, 305240e 4, current 0.
- Navigation replay: six preserved baseline defects, current 0, 17 searches.
- Original/current interactions: 38/38 match, 31 baseline differences preserved.
- Page mutation controls: 14/14 caught; integration 14/14 pass.
- Original runtime: 59 scenario IDs, 145 claims, zero failed.
- Exhaustive original/canonical Scheduled Plunder: 10,482 renders, 106 time
  boundaries, 51/51 mutations caught, 61 fixture branches, zero React warnings.
  Lead replay completed in 702.2 seconds; output is `scheduled-replay.txt` under
  PM-026. No reduced or skipped mutation run was used for acceptance.
- Historical filter/table/Checking/row/state replays pass through the documented
  adapter; original historical scripts and evidence remain untouched.
- Current worker evidence validator and all five protected-WIP entries pass.
- Canonical check, rebuild and production package check pass at source fingerprint
  `0310083d7ba2b09360a2af1ea48ee3facf84517ec4cad6088b6596eabba7ad7a` and artifact
  `32dc7d48186fa1396e727949c1f9f267c5cc9eb4bba05ca256a236c0f87f505b`.
  Build reports its existing large-chunk warning; the build succeeds.

## Browser completion

Lead used an owned in-app browser tab against the existing worker Vite listener
on 5173. The listener was reused, not claimed as lead-owned or stopped. Browser
records and inspected screenshots are under `LWB317-PM-026` in the UI evidence tree.
Inputs used documented semantic browser controls; no native/gameplay operation ran.

Fresh final-state checks establish:

- City typing keeps 52 items/50 rows; Search reduces this to the matching single
  Fixture Commander 7 row.
- Two Dispatch selections enable its preview schedule control; the rejection
  keeps both selected and creates no job. Ghost/Dispatch navigation clears that
  selection as recovered.
- Two Truck selections remain after pagination and Train/Truck navigation; page 2
  and Truck Owner 51 are restored.
- Truck Clear history rejection leaves all three job groups at 11/4/15 rows,
  total 30; English message is `The action could not be completed.` and Japanese
  is `操作を完了できませんでした。`.
- Japanese/dark at 375x812 has no horizontal page overflow. Viewport was reset.
  Captured browser error/warning entries are empty.

This supersedes the worker's BR6 pre-final-translation message. Preserve that
historical browser JSON; do not falsely rewrite it as a final observation.
The lead did not independently repeat every worker browser flow or compare
pixels against the protected original post-auth runtime.

## Review findings and remaining boundaries

| Reported issue | Lead assessment / reason | Change in this closeout |
|---|---|---|
| Backend-loss/unmount stale searches | Valid PM-025 regression; current lifetime tests prove disposal now fences both outcomes | Already corrected by worker; no new code needed |
| Stale-page first request on server change | Valid; current original comparison and mutation control pass the corrected page-1 behavior | Already corrected by worker |
| Action message retained on tab change; raw Share/Clear error | Valid; source comparisons and final browser checks prove both corrected | Correct stale README/coverage/review entries and add final browser evidence |
| Option refresh alliance/level/Treasure validation | Valid open source/local UI gap; broader accepted filter behavior was explicitly preserved in this campaign | Record as follow-up; no scope expansion |
| Clear-data filter resets | Valid open gap; all-data clearing workflow explicitly excluded | Record as follow-up |
| Export/scan-start feedback translation and export success presentation | Valid open gap outside assigned search/selection/scheduled surfaces | Record as follow-up |
| Scan-progress/completion row refresh and options polling | Valid remaining timing difference; broader scan/options polling excluded | Record as follow-up |
| Treasure lucky default/persistence and false boolean fields | Valid remaining original difference; unrelated Treasure filter/native dependency scope was preserved | Record as follow-up, not native completion |
| Alliance named none/all collides with sentinel | Valid edge case in the existing City filter representation outside assigned name inputs | Record as follow-up |
| Player-mark offline/scanning availability | A real original/canonical predicate difference; removing the existing runtime fence was not authorized by this campaign | Preserve the fence; future bounded source/provider review required |
| Raw C04/C14/C18 diagnostic differences | Diagnostic harness/context limits need to be distinguished from current product defects; empty-name C04b matches, count stubs differ, and clock ownership differs | No blanket parity claim based on diagnostic labels |

Original pixel comparison, native job producer/full row schema, native scheduling/
sharing/cancel/clear/claims, native images/text and Treasure wiring remain open.
Current evidence is source/local and offline preview evidence. Full Map and
overall UI parity are not established. Protected AFK/scratch/screenshot WIP is
unchanged and unstaged. Exact continuation is the next separately assigned UI
gap, not a native/gameplay phase or a repeat of completed campaign work.
