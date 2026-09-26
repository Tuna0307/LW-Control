# R8-041 — restore game_recovery_status public contract

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the native recovery-status command/event projection and status-only transitions without changing recovered process-control thresholds or launcher/proxy mutation behavior.

## Authority

Primary native evidence:

- `game_recovery_status` command handler `0x140199C30-0x14019A46F`;
- selected-profile runtime resolver `0x1402AE43C`;
- recovery-state reader/error helper `0x14033A877-0x14033AA17`;
- recovery event publisher `0x14033A045-0x14033A19B`;
- recovery-status serializer `0x14033B396-0x14033B5FA`;
- default recovery-status constructor `0x14031894C-0x1403189CB`;
- recovery-start transition `0x140338FD4-0x140339356`;
- automation-disabled/default reset path `0x14033A19B-0x14033A3E2`;
- terminal succeeded/failed transition `0x14033A3E2-0x14033A877`;
- retry-status transition `0x14033AA65-0x14033ADDF`.

The existing static recovery inspector was rerun against the verified reference and returned `ok=true`. This checkpoint does not enter the protected package-key RVA lane.

## Exact public schema and scalar/null behavior

The native serializer emits exactly these fields in order:

1. `state`
2. `reason`
3. `updateDetected`
4. `restarted`
5. `startedAt`
6. `completedAt`
7. `attempts`
8. `nextRetryAt`
9. `error`
10. `noticeId`
11. `noticeVisible`
Recovered type behavior:

- `state`: string;
- `reason`: string or JSON null;
- `updateDetected`: boolean;
- `restarted`: boolean;
- `startedAt`: scalar integer, never JSON null;
- `completedAt`: integer or JSON null;
- `attempts`: integer;
- `nextRetryAt`: integer or JSON null;
- `error`: string or JSON null;
- `noticeId`: scalar unsigned 64-bit identifier, serialized as a JSON number;
- `noticeVisible`: boolean.

The old rebuild record incorrectly modeled `startedAt` as nullable and `noticeId` as a nullable string. R8-041 corrects both public representations.

## Native idle/default status

The default constructor writes:

- `state="idle"`;
- `reason=null`;
- `updateDetected=false`;
- `restarted=false`;
- `startedAt=0`;
- `completedAt=null`;
- `attempts=0`;
- `nextRetryAt=null`;
- `error=null`;
- `noticeId=0`;
- `noticeVisible=false`.

When recovery automation is disabled, native resets to the same default object while preserving the current `noticeId`. R8-041 mirrors that status reset.
## Native notice and terminal transitions

Recovery start increments the existing `noticeId`, stamps `startedAt`, writes `state="waiting"`, clears attempts/next-retry/error, preserves the incoming update-detected flag, sets `restarted=false`, and sets `noticeVisible=true`.

Status-only parity changes in the rebuild now preserve that notice identifier across the same recovery attempt. Direct process-exit/hang/disconnect recovery creates one notice; an event-latched recovery that later becomes a process exit continues using the already-created notice instead of generating a second one.

Native terminal completion writes `state="succeeded"` or `state="failed"`, stamps `completedAt`, clears `nextRetryAt`, stores an error only on failure, preserves the existing reason/start/attempt/notice fields, and keeps `noticeVisible=true`. The previous rebuild simplified successful terminal state back to `idle`; R8-041 restores the native terminal vocabulary in the status/event projection.

The retry helper writes `state="waiting"`, the current attempt count, `nextRetryAt`, error text, and visible notice while preserving the same recovery identity. Existing recovered retry timing/process behavior is otherwise unchanged by this checkpoint.

## Command validation and state availability

The native command uses the same selected-profile runtime resolver recovered for R8-040. Stable error codes are:

- `PROFILE_ID_REQUIRED`;
- `PROFILE_RUNTIME_UNAVAILABLE`;
- `STATE_UNAVAILABLE`.

For missing recovery state, the exact recovered message is `game recovery state is unavailable`.

The rebuild previously accepted an optional profile and fabricated an idle status when no recovery lifecycle existed. R8-041 removes that fallback: `game_recovery_status` now requires the selected profile runtime and returns `STATE_UNAVAILABLE` when the state object is absent. Exact prose for the first two runtime-resolution errors remains unclaimed.

## Boundary and validation

No process termination rule, reconnect threshold, retry table, updater suppression rule, launcher behavior, proxy install/backup/restore behavior, or protected package-key behavior is changed here. R8-041 changes public recovery status/state publication only.

Deterministic coverage proves exact field order, idle defaults, numeric scalar types, succeeded/failed examples, strict profile/runtime/state errors, notice stability/visibility through an actual fake-clock recovery, default reset with preserved notice ID, and the existing recovered recovery-policy suite. Release/checks builds and the full deterministic suite are required before checkpoint commit.