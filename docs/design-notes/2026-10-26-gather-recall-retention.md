# Repeated gathering recall failure: retain exact receipts across authority gaps

> **Status (2026-10-27):** merged to `main` through PR #17 and released as `lw-control-v1.2-2026-10-27`. Statements below about branches, draft PRs or "not merged" describe the state on the note's date.

The owner's test of package `552c1a8a7791` still failed: Off showed Disabled
with six confirmed gathering sends, no recall count/error, and four squads
collecting. This supersedes any broad completion claim from the earlier
callback-fix report. Continue `codex/lwtool-remaining-tabs`, draft PR #17;
no merge or release.

## Evidence and defect

Read-only inspection of the still-running owner session preserved the evidence
before shutdown. All four current collecting UUIDs, squad slots and destination
points matched that session's successful-send receipts; current formation
ownership and home server matched too. Creation GUIDs had become empty after
arrival. Nevertheless, the last scheduler observation labeled every squad
`manual_wait`, and Off had no cleanup submissions. The native Stop/observation
snapshot had zero hooks. This proves lost/unusable tracking, not its exact
historical trigger.

The actual scheduler and adapter reproduce a causal failure: when the fresh
owner token is briefly unavailable, `CleanupAfterLeaseLoss` called gathering
Stop with no cleanup authority. Stop erased its private `known` records.
Subsequent status/configuration restored the same session, but its collecting
squads were then treated as manual marches. Off silently produced zero recalls.
The pre-fix source fails the distinguishing lease-gap test.

The earlier short tests covered callback coexistence and arrival clearing the
GUID; they did not cover record retention through a lease interruption. A
finite test pass must not be described as every long-session recall being fixed.

## Correction

On authority loss the scheduler now calls a gathering-specific suspension path.
It detaches transient observers and cancels planning, but retains the exact
private records and one-send tombstones in the same Lua VM. No network action
is allowed while authority is absent. Fresh configuration/Stop can reuse them
only if the full original owner token, account, player identity, formation,
march UUID, server, GUID rule and destination still match. A changed owner or
march is refused. Another session cannot adopt these records.

On same-owner recovery, gathering reinstalls its cooperating observers even
when every squad is busy, including Stop before another Plan. An uncertain
recall is never replayed. Interrupted confirmation remains visible.

Stop also reads the host's latest trusted configuration rather than relying
on whichever checkbox value the last Plan saw. Immediate check/uncheck then
Off now honors the current setting. Missing ownership for selected collecting
squads produces an explanation instead of a silent Disabled success. Errors
preparing cleanup are reported. Read-only diagnostics expose retained record
matches and suspension count.

March revalidation first checks the current owned formation view, then falls
back to the exact UUID for a returning march. Both paths use the same strict
ownership/GUID checks. This is not proof that the owner's historical failure
was caused by a specific native lookup: REA rejected the game's custom RGMD
assembly format; the existing pinned RGMD reader supplied limited retained IL.
No game debugger, anti-cheat change, new decompiler installation or live code
patching was used.

## Authority and live preparation

The current login was the owner's main account. The owner explicitly approved
fresh sends and recalls of up to four squads on empty ordinary resource tiles
for this test. This narrowly extends the earlier recall-only authority; items,
purchases, chat and other main-account gameplay remain outside scope.

The owner session's remaining three collecting trips were each recalled once
through the game's native buttons. A subsequent game observation showed 0/4
out. This is a manual control/cleanup, not evidence of automatic Off working.
Home Close then shut down the exact game and removed its recovery journal;
the old app closed normally. The isolated profile has other automation disabled.

The staged queue's default approved-account list is unchanged. An explicit
per-run gathering-only opt-in permits an exact owner-approved account and only
the Resource Gathering proof, read-only observations/status, its Stop and
finish. Controlled tests reject other feature requests before file access.

## Controlled checks

`tests/GameScripts.Checks/feature018_gather_lease_checks.py` runs the actual scheduler, native
adapter and Gather module together: same-session loss/recovery/Off sends one
recall, no send during the gap, changed account/token/formation/march refusal,
uncertain recall no-replay, latest checked/unchecked option, and explicit
untracked/interrupted explanations. The old scheduler/Gather source fails the
same-session retention assertion. Existing callback, Gather, Automation,
Attack, idle Drill, Hotkeys, Resource reply, UI and Desktop checks are retained.

Live fault-injection, final package identity and limits are recorded below
after the actual test completes. Private raw evidence is under
`artifacts/owner-gather-repeat-20261026/` and the exact native run directories;
account/session/march identifiers remain local.

## Live diagnostic fault test

`artifacts/map014-takeover/native-ui-20261026-110050` used the real main
account under the owner's new approval. Four fresh gathering sends were
confirmed, then the controller paused only the exact isolated test-host EXE
for 12 seconds and resumed it in `finally`; the game process was never paused.
The normal five-second game-side lease limit expired. No fake lease, altered
account token or in-game debugger was used.

Before Off, native diagnostics reported one suspension, four retained records
matching their current marches, fresh original owner, and an intact cooperating
observer chain. A real Resource Map scan was active. The real UI Off then
confirmed exactly four recalls, Disabled and no error. This is native returning
state, not an assertion that every squad had already arrived. Owned exit
restored all three original files with no recovery journal.

The full UI/Desktop suites, existing Lua suites, twelve Resource reply cases,
current-probe AST/LENC checks, default/explicit gathering-scope checks and
31 package-auditor cases passed. The final package is rebuilt from the source
checkpoint containing this correction; final all-home proof follows separately.

The first extracted-package attempt, native root `20261026-110929`, is not an
accepted automatic-recall positive. Four records survived its real authority
gap, but the saved recall option was **false**, so zero recalls were correctly
submitted. Its scripted setup had used fixed delays and could click a checkbox
while a preceding save disabled the fieldset. The proof now waits for enabled
controls between edits and requires both a checked checkbox and a persisted
true value before any gather dispatch. This is a test-precondition correction,
not evidence that a user-selected true value was overwritten by the product.
The failed run and its normal script-restoration receipt are retained.

## Final accepted test result (independent review still open)

The corrected extracted package completed the exact main-account four-squad
test in `artifacts/map014-takeover/native-ui-20261026-112313`:

- Checked and persisted recall preconditions were both true before dispatch.
- Four new gathers were confirmed. The exact test app was paused for 12 seconds
  and resumed; the game was not paused. One native suspension and four matching
  retained records were observed before Off, with the original owner and known
  callback chain intact. A Resource Map scan was active.
- Real UI Off produced exactly four recall submissions and four confirmed
  native returning marches, Disabled and no error.
- Read-only follow-ups waited for **all four formations to have no active
  march**. The test ended with all four home, not merely in BACK_HOME travel.
- Owned exit restored all original files and removed the recovery journal.
  No task app/game processes remain. The prior test trips were manually cleaned
  up before fresh dispatch; no old record was adopted by the new engine.

Product/package commit: `31523249098c00a60a44a5cfa6dd033272d4ebbe`.
The final source-pinned package contains 105 archive entries, 754 source inputs
and 29 verified bridges. Extraction and every payload file still match after
live execution. ZIP SHA-256:
`6e35240a8bf253e1283db806354dab18f9a51c05f18297f0dfa230b2c9263c2f`.

Local ZIP:
`artifacts/owner-gather-repeat-20261026/package-qualified/LW-Control-lwtool-improvement-31523249098c.zip`.
Sanitized evidence: [evidence](evidence/GATHER-RECALL-RETENTION-2026-10-26.json).
Retain the unsuccessful package attempt; it is explicitly excluded as a
positive. A read-only preparation run (`20261026-112008`) also exited/restored
after a transient account-read failure during base/world transition; no fresh
gather was dispatched in that run. The corrected controller only retries such
read-only unavailability briefly and keeps exact account/session admission.

This completes the scoped recall correction and finite fault test, not a proof
of every arbitrary-duration disconnect or a patched live Alliance Drill join.
The historical screenshot's exact trigger remains unrecorded. Independent
lead review/owner acceptance remain separate. No main merge or release.
