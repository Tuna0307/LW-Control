# Project lead control sheet

The main project lead owns integration and should keep this file small and current.

## Current target

LWBridge 0.3.17

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Current phase

**Phase 0 — preparation COMPLETE**

Status: project baseline is clean and pushed. No 0.3.17 UI implementation or reverse-engineering task has been opened yet.

## Active work items

| Work item | Owner | State | Scope |
|---|---|---|---|
| LWB317-PM-001 | Project lead | COMPLETE | Repository/documentation reset for 0.3.17 |
| LWB317-UI-001 | Unassigned | QUEUED | Inventory top-level shell/navigation only |
| LWB317-RE-* | None | BLOCKED BY PHASE ORDER | Begins after UI baseline |

## Project-lead responsibilities

Before assigning a worker:

1. pick one bounded task;
2. define explicit non-goals;
3. name the reference artifact/state;
4. name required evidence/output paths;
5. define acceptance checks;
6. avoid two workers editing the same master docs.

After a worker returns:

1. review evidence/source identity;
2. reject unsupported inferences;
3. update parity matrix/feature ledger conservatively;
4. integrate only coherent changes;
5. run applicable checks;
6. commit/push the checkpoint;
7. assign the next smallest useful task.

## Status discipline

The project lead should distinguish:

- what the reference demonstrably contains;
- what has been copied visually;
- what has been reverse engineered;
- what has been implemented;
- what has been live-proven.

Do not compress those into a single vague “done” percentage.

## Current desktop constraint

Windows-MCP remains paused until the owner explicitly re-enables it.

The first queued UI task therefore must not start yet.
