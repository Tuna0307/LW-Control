# Current Web -> Sol resource test packet

## Coordination status

- Function: PM13-04 / PM12-D — resource scan -> normal Search/display -> newer refresh -> saved reopen.
- Current owner: **ChatGPT Web**.
- Packet state: **NEEDS_WEB_PREPARATION**. This PM-created outline is not a ready build/test declaration.
- Technical audit: review 14; audited application implementation `7ca6d5c`; PM documentation checkpoint `c6415c3`. Web must inspect current HEAD and record the actual candidate it delivers.
- Next owner: Sol after Web delivers a concrete packet for the permitted test scope. No Daybreak assignment.
- PM acceptance: **OPEN**. No new live outcome recorded in this packet.

Follow [team-workflow.md](team-workflow.md), [user-test-checklist.md](user-test-checklist.md) and [first-live-result.md](first-live-result.md). Keep all 47 acceptance cases unchanged. This packet tracks one bounded function, not complete-page acceptance.

## Web preparation — fill before handing off

| Required field | Current value |
|---|---|
| Implementation commit and branch; verified GitHub revision | TO BE VERIFIED BY WEB |
| Build command/time; executable absolute path and SHA-256 | TO BE VERIFIED BY WEB; canonical path is in the test checklist |
| Necessary offline checks and durable results | Prior review-14 checks passed; Web records candidate-specific applicability or reruns when warranted |
| Actual installed client/launcher identity, hashes and compatibility limits | TO BE VERIFIED; do not inherit a stale game build |
| How Sol identifies the selected profile and actual store | TO BE DOCUMENTED; packaged and standalone roots may differ |
| Supported startup/scan lifecycle and required initial game state | TO BE DOCUMENTED from existing evidence; Overview Launch is not an implemented prerequisite |
| Exact normal UI actions and expected visible outcomes | Use the checklist; add candidate-specific labels/preconditions without guessing |
| Source/request/query/render evidence collection | TO BE DOCUMENTED with permitted existing capture/inspection steps; no blocked harness rerouting |
| Cleanup/restoration ownership and checks on success/failure/disconnect | TO BE DOCUMENTED from supported lifecycle; preserve unrelated work/game files |
| Restrictions and permitted scope | SB-97 remains unresolved for the prepared live proof; record any independently permitted scope or exact external requirement |
| Readiness / next owner | NEEDS_WEB_PREPARATION; choose READY_FOR_SOL or BLOCKED with reason when complete |

Attach/link findings and commands rather than duplicating specifications. If instrumentation cannot collect needed proof, state the gap; never lower the acceptance requirement to a screenshot. A build-ready packet can remain live-execution BLOCKED. Record these separately.

## Sol execution — fill during each attempt

Record attempt ID/date, packet/build commit, executable/hash, current client, actual profile/store, exact tested scope and available tool capability. Save evidence after each completed segment. A pending step must never be inherited as passed from an earlier build.

| Step | Result | Evidence / first failure |
|---|---|---|
| Build/client/profile and permitted test scope verified | NOT_RUN | |
| Supported native Computer Use available | NOT_RUN | Setup only; not feature success |
| First real resource scan -> explicit normal Search -> matching rendered row | NOT_RUN | |
| Second distinct newer acquisition -> Search -> matching rendered row | NOT_RUN | Same point/count allowed, older acquisition time is not |
| Same-profile app reopen -> Search -> second result retained | NOT_RUN | No new scan or manually seeded row |
| Cleanup/restoration/integrity verified | NOT_RUN | Record remaining ownership if interrupted |

Allowed outcomes: PASS, FAIL, BLOCKED, NOT_RUN. Include exact errors and relevant source/session/request/record/query/render correlation. Keep historical saved-data observations separate from fresh acquisition. Unknown resource naming and unobserved Gathering remain limitations, not inferred mappings.

## Return / resume record

- Last completed step and durable evidence: NOT_RUN.
- Next exact action: Web completes preparation.
- Processes/files/cleanup obligations owned by this attempt: no test attempt started by this packet.
- First failure or external blocker: unresolved SB-97 scope must be carried forward; no new execution attempted.
- Next owner and reason: Web, preparation.
- Fix commit and required retest (when applicable): none.
- Checkpoint commit/push verification: record on delivery; report final commit in completion message without amending solely to embed its own hash.
- PM decision: OPEN; workers may report test results but do not self-approve PM acceptance.

Preserve completed attempt evidence under a distinct durable path before resetting rows for a new attempt. Do not edit previous failures into passes; link a newer successful retest.
