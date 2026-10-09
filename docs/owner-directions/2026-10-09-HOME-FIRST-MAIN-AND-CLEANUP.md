# Owner direction — Home first, working A -> A main, and cleanup

Date: 2026-10-09. Source: direct owner instruction in the project-lead chat.

The owner requested: focus on Home and finish it before moving around to other
features; remove useless completed branches/worktrees; record these obligations
in repository Markdown; merge only confirmed working features behaving 1:1 like
original LWBridge 0.3.17. This supersedes the proposed City-marking next task.

## Acceptance

Main is the usable product branch. Research keeps original evidence and unresolved
work. Before merging, the lead must inspect the actual production diff, independently
check original source/authorized observation against equivalent inputs and results,
execute meaningful native/integration verification, and verify the actual package.
Working current-game behavior alone is not original parity. Original source facts
alone are not working native proof. Both are required for the named feature scope.
Unknown required behavior, demonstrated A -> B differences and unavailable providers
remain incomplete. Do not promote a supported subset to full Home completion.
The existing published subsets retain their documented limits; this instruction
does not retrospectively certify the whole released app.

No forced minimum work duration or evidence volume. Reuse existing original traces
and live receipts, then fill concrete gaps. Keep new evidence compact, replayable
and relevant to user-visible Home behavior. Preserve historical negatives.

## Lead cleanup after acceptance and merge

1. Inspect clean/dirty state, branch ancestry, open PRs and all linked worktrees.
2. Delete a completed feature branch locally and remotely after its exact head is
   retained by accepted main/history. For a redundant non-feature checkpoint,
   verify all commits are retained by main/research before deleting the ref.
3. Preserve unique commits and any needed untracked/ignored evidence. Do not remove
   unfinished branches merely because their names are old.
4. Retire task worktrees after checking activity and preserving needed local files.
   Build caches can be regenerated. Explain any worktree that must remain.
5. Verify final local/remote branch and worktree inventories and record the result.

Default steady state: main, research/offline-controller, and at most one active
Home feature branch. Prefer the existing checkout. A worktree is an additional
checkout sharing the same Git history, not a separate product or new main branch.
Use it only for a concrete isolation need and clean it up when that need ends.

Worker remains solo and begins only on owner relay. This direction does not
expand protected-service/gameplay permissions or bypass automatic approval denials.
Existing authorized bounded Home verification, technical identity/isolation,
backup/restoration and explicit owner pause/stop rules remain in force.
