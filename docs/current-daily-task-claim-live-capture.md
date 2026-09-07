# Current daily-task bounded live claim — 2026-09-06

This checkpoint records the first current-build daily-task reward request made by
the reconstruction after the daily server reset. The test was bounded to one
freshly selected target, one reward request, no retry, and exact game-file
restoration.

## Fresh precondition

A read-only capture completed at `2026-09-06T04:08:01Z` after one
`DailyQuestLs` refresh. It contained 23 task records, `currentPoint = 0`, no
received chest stages, and these exact `CanReceive` task IDs:

- `101` (`templatePoint = 10`)
- `102` (`templatePoint = 10`)
- `119` (`templatePoint = 40`)

All five chest states were `NoComplete`, so the offline selector had no chest
candidate and task `101` was the first deterministic task candidate.

## Candidate and live sequence

The encrypted `a36` candidate used content version 12 and passed source parsing,
one-send source checks, and encrypted round-trip verification before installation.
Its package SHA-256 was
`8858136a4ebbbc96e64f9be47ebaf62a1ebb6cacbcb807aa5db016a0d93ca927`;
its generated probe-source SHA-256 was
`8024ed320c419dfc681f9d38b28ca688cb099f0f662eaf7e0ac3561cfba58a16`.

The live sequence was:

1. Confirm Last War was closed and create an exact backup.
2. Install only the already verified candidate script package and metadata.
3. Launch through the official launcher.
4. Request one `DailyQuestLs` refresh because the manager initially lacked the
   five daily-box thresholds.
5. Build pre-action capture `claim-before-1788667935` at
   `2026-09-06T04:12:15Z`.
6. Select task `101`, which was still explicitly `CanReceive`.
7. Send exactly one `SFSNetwork.SendMessage(MsgDefines.DailyTaskReward, "101")`.
8. Do not retry or select another reward.
9. Wait until the bounded timeout for the wrapped
   `DailyTaskRewardMessageHandle`.
10. Close the game and restore the exact original package in `finally`.

The runtime status reached `sendCount = 1` and `sent_waiting_response`. The
wrapped response handler did not fire before timeout, so this run did not produce
a correlated claim-result file or an in-probe post-action snapshot.

## Independent post-state

After restoration, a separate read-only run refreshed current server state and
captured it at `2026-09-06T04:14:28Z`. The relevant transition was:

| Task | Before | After | Template point |
| --- | --- | --- | ---: |
| `101` | `CanReceive` | `Received` | 10 |
| `102` | `CanReceive` | `Received` | 10 |
| `119` | `CanReceive` | `Received` | 40 |

`currentPoint` therefore changed from `0` to `60`. `receivedStages` remained
empty, and chest stage `1` changed from `NoComplete` to `CanReceive` because its
threshold is `40`.

This proves the state-effect half for the exact selected task `101` after the one
owned claim request. It does not prove why tasks `102` and `119` also became
`Received`. The current evidence is compatible with more than one explanation,
so the multi-task response semantics remain UNKNOWN until the actual response
payload is captured.

## Restoration and validation

After the bounded claim and the independent read-only capture, the installed
package was back to the official 18,686-entry content-version-12 state. The
custom loader probe was absent and `BaseUtils.IsDebug` remained false. Final
protected-file SHA-256 values were:

- `LWScripts.data`: `73f69efea8bcd121ede8fa292d621eb68cea4c6368e331e8c614e95fff5a6a0c`
- `LWScripts.txt`: `314a2f87ac7f4697df31db7bb73303efaed472ec0ec0d74046d27a9f3d28fdac`
- `version.txt`: `6b51d431df5d7f141cbececcf79edf3dd861c3b4069f0b11661a3eefacbba9188`
- `BaseUtils.rdl`: `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`

`LWControl.Core.Checks` remained `35/35`, the focused claim/installer Python
checks were `8/8`, the generated Lua source parsed successfully, and the desktop
project built successfully after the bilingual UI changes.

## Evidence boundary

**PROVEN current-build:** a fresh explicit task `101` was `CanReceive`; exactly
one owned `DailyTaskReward("101")` request was sent with no retry; a later fresh
server-state capture showed task `101` as `Received`; all protected game files
were restored to the official package afterward.

At this first task-only checkpoint, the matching task-response payload/correlation,
the reason tasks `102` and `119` also became `Received`, the special
`DailyQuestReward(-1)` semantics, and explicit chest-stage behavior were still
unknown. The chest-stage item is superseded by the follow-up proof below.

## Explicit chest-stage follow-up

A later read-only refresh at `2026-09-06T04:35:28Z` showed the post-task state
needed for a chest test: `currentPoint = 60`, `receivedStages = []`, and stage `1`
(`40` points) was `CanReceive`. Stages `2..5` were `NoComplete`.

The `a38` candidate used the same one-request/no-retry boundary and selected exact
stage `1`. It sent exactly one
`SFSNetwork.SendMessage(MsgDefines.DailyQuestReward, 1)` request. The injected
`DailyQuestRewardMessageHandle` wrapper did run. It observed no `errorCode`, and
the original handler returned without error. The captured wire event was a
`DailyQuestReward` event with an empty `stages` array because the response's
`stageArr` was empty.

The immediate state inside that response handler did not yet change: stage `1`
was still `CanReceive` and `receivedStages` was still empty. The original strict
probe therefore reported `verification_failed`; that result correctly rejected an
unproven immediate effect, but its assumption that `stageArr` must echo the target
was disproved by this live response.

No second reward request was sent. After exact restoration, a fresh independent
read-only `DailyQuestLs` capture at `2026-09-06T04:38:18Z` showed:

| Field | Before request | Fresh post-state |
| --- | --- | --- |
| chest stage `1` | `CanReceive` | `Received` |
| `receivedStages` | `[]` | `[1]` |
| `currentPoint` | `60` | `60` |
| task `101` | `Received` | `Received` |
| task `102` | `Received` | `Received` |
| task `119` | `Received` | `Received` |

This is authoritative current-build proof of the chest-stage effect after the
single owned `DailyQuestReward(1)` request. The exact delayed update message that
caused the manager/server state to reflect stage `1` remains unclassified.

The protected current-game files were restored exactly after both runs. The final
official hashes remained:

- `LWScripts.data`: `73f69efea8bcd121ede8fa292d621eb68cea4c6368e331e8c614e95fff5a6a0c`
- `LWScripts.txt`: `314a2f87ac7f4697df31db7bb73303efaed472ec0ec0d74046d27a9f3d28fdac`
- `version.txt`: `6b51d431df5d7f141cbececcf79edf3dd861c3b4069f0b11661a3eefacbba9188`
- `BaseUtils.rdl`: `b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`

**PROVEN current-build:** explicit task reward send and exact selected-task state
effect; explicit chest stage-1 send; live chest response handler with no error;
fresh stage-1 `Received` server state; exact restoration after each bounded run.

**UNKNOWN:** why the task-101 request coincided with task `102` and `119` becoming
`Received`; the exact delayed message/push that updated chest stage `1`; and the
special `DailyQuestReward(-1)` semantics.
