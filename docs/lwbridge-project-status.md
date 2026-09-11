# Project-manager checkpoint — review 14

Audited 2026-09-11 at `7ca6d5cc8d47f14fde6b73af21cf19d2bedbb23d`, branch `research/offline-controller`, clean and synchronized at entry. Eight commits since `d4571bd` were reviewed. [Review 13 and contributor follow-ups](reviews/2026-09-11-review-13-and-followups.md) are historical.

## Owner-directed delivery split — 2026-09-11

PM now performs coordination/evidence audit only; no new PM implementation or game testing is assigned. Web prepares/fixes the resource build; Sol verifies it through native Computer Use/live testing; Daybreak has no current assignment. [Team workflow/prompts](team-workflow.md) and [current test packet](live-test-handoff.md) define Web-first dispatch and resumable Sol results. This documentation update does not advance live acceptance or reopen completed repairs. The historical PM checks below remain evidence of that earlier audit.

## Result for the owner

**Resource scanning is an implemented test candidate, not a completed live feature. The two pages are not fully working.** The latest work includes real implementation repairs, not just research. Saved resource browsing/reopening has recorded normal-window proof; fresh acquisition through the latest ordinary window still lacks its final two-read/Search/render/reopen proof.

Computer Use setup and a real-game feature test are different tasks. A working screen/click tool proves only that an AI can operate the app. It does not prove a scan works or clear a restriction on a specific operation. The standard AI owns final resource verification; no new Daybreak assignment is warranted for a validation/capability gap.

## Accepted repairs and remaining deliverable

| Item | Audit decision |
|---|---|
| PM13-01 saved browsing | Closed for the documented historical real resource: server 2212, point 32482, coordinates 481,32, level 3. Existing normal-window evidence shows explicit Resource Search across restart. Saved context is distinct from live readiness; no fresh acquisition is claimed. |
| PM13-01b feedback | Closed at offline scope, with recorded ordinary-window unsupported-selection/missing-context checks. Unsupported categories are not silently discarded. |
| PM13-03 lifecycle | Closed at bounded offline scope. Commit/cancel ownership and helper-start ownership have deterministic race coverage. This is not all live fault acceptance. |
| PM13-02 proof correlation | Closed offline; subsequent coordinate-button correction and historical saved-reopen proof accepted. Empty/loading/stale/wrong-source results cannot satisfy the exact proof contract. |
| PM13-04 / PM12-D | **ACTIVE/BLOCKED final resource gate.** Need a permitted real Resource-only Start, correlated normal Search/render, second newer read/Search/render, then saved reopen and cleanup evidence. |
| Monster and other categories | Acquisition unavailable; monster acquisition/search is queued after resource acceptance. An empty Monster table is not evidence it scanned successfully. |
| Remaining two-page scope | Overview launch/close/reconnect integration, automatic/full-world scanning, Normal/Fast parity, remaining filters/options/export and conditional game actions are incomplete. A visible control or persisted switch is not functional acceptance. |

The historical bounded helper route already acquired real resources twice. Those results make the work useful, but do not sign off the latest user-facing workflow. Resource naming and live Gathering also remain limited. All 47 full acceptance cases remain required; no full case is newly signed off in this review. Do not convert four closed repair items into an overall percentage.

## Verification in this PM audit

Fresh Release build passed with 0 warnings/errors. Desktop checks with `--verify-real-config-unchanged` passed: six groups true, no failures. Frontend regeneration/hash check, resource feedback checks, five resource proof browser cases, and the three isolated PM12 recovery/session/scoped-close regressions passed. These are build/offline checks, not a new live-game run.

Source review covered saved-server selection, validate-before-store and commit/cancel boundaries, helper ownership, exact query/result/render matching, and the saved-reopen verifier. Contributor evidence reviewed includes `2026-09-10-pm13-saved-target-rediscovered.json`, `2026-09-11-pm13-normal-window-gate.json`, and `2026-09-11-pm13-reopen-proof-correction.json` under `evidence/lwbridge-implementation/`.

This PM environment successfully launched/observed the canonical normal app through native sky. It did not complete a new saved-row/reopen audit or perform fresh acquisition. Per the user's clarification, Computer Use capability testing may be performed by the standard AI; it is not the requested product deliverable.

[Audit evidence](../evidence/lwbridge-implementation/pm-review-14/audit.json) records seven matching historical proof/source hashes and unchanged full acceptance sections.

## Exact next task and restriction status

Follow [the test checklist](user-test-checklist.md) and [regular-AI handoff](implementation-handoff.md). Verify the current installed client fingerprint before live testing; historical client proof does not automatically cover a game update. Stay on the resource function and repair its first failed step before continuing.

SB-97 records automatic-review rejection of execution of the prepared live persistent-window proof because its safety status could not be determined. It remains unresolved. Do not replay or repackage that operation through a different model, native clicking, WebView or another executor. Tool availability is a separate question. A PM cannot waive an environment/platform restriction: any new live test must be independently permitted, otherwise record the exact required external change and stop that action. Do not ask the user to run the rejected harness as a workaround.

No Daybreak assignment. ESC-005 remains NOT_ASSIGNED/deferred original-pipe parity. No unrelated research or filler checkpoint replaces this final resource gate.
