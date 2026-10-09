# HOME-004 R4 — close the active game-root contract

READY FOR OWNER RELAY. One medium Home-only task, three sequential checkpoints.
Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control
Branch: codex/home-complete-delivery-004; existing checkout and draft PR #6.
Required reviewed ancestor: 48fe84354b47733fca48d9a15e9d9cdc3f997b5e.
Worker works solo; no subagents, other AI chats, GPT Work or Codex delegation.

Read AGENTS.md, HOME_004_R3_R1_LEAD_REVIEW.md, HOME_004_R1_CURRENT_MATRIX.md,
the current acceptance proposal and the applicable original COMPLETION-010
root-selection evidence. Missing research-only files may be read from
origin/research/offline-controller with git show; do not replace this checkout
or cherry-pick unrelated research history.

## A — determine the exact H-41 difference

Revalidate the reference EXE hash. Trace original game_root_select at
0x188F12–0x18A2AD and its 0x413185/0x41281d/0x412d56/0x41253e consumers,
plus game_root_status and monitor/start consumers. Current code already saves
a new configured root during a running session and stages its lifecycle binding.
Do not describe that as a blanket picker rejection. Determine precisely which
observable status, recovery, next Start or error behavior still differs.

Compare actual SaveNativeGameRootSelection, RebindGameRootSelection, status,
monitor and Start/Stop paths. Record a small source-backed obligation table
and executable distinguishing baseline. Separate installation ownership
mechanisms from original visible behavior. Never guess how original running-root
changes affect recovery, and never restore or terminate using a newly selected
path instead of the exact captured installation/process.

## B — correct demonstrated differences

Implement the smallest evidence-backed correction. Cover valid A→B while A is
running, same-root selection, cancel/invalid choice, config write failure,
selection during start/Stop/recovery, next eligible Start and late acknowledgements.
Tests must use actual production backend/lifecycle handlers and isolated roots;
an old implementation must fail the distinguishing assertions.

Preserve exact PID/path/creation/session ownership, recovery registration/lease,
original-script backup/restoration, deferred owner retirement, fresh authenticated
readiness and the newly repaired shared listener. Do not stop an existing game
merely to make the test easy. Do not silently alter retry/deadline policies or
substitute mock Connected for product success. If current behavior already matches
an obligation, retain it and prove that conclusion rather than redesigning it.

## C — integration and bounded delivery

Exercise actual mounted Home root acknowledgement/error behavior in EN/light
and JA/dark with isolated inert responses. If source recovery establishes a
specific additional native observation that is essential, use only the already
authorized bounded Home scope after fresh compatibility, isolation, backup and
identity gates; respect explicit owner pause and approval denial, never bypass.
No Map/gameplay, spending, updater or protected-original service access.

Run affected Home native checks, the real isolated pipe recovery probe,
frontend check, fresh Release/build/package checks and extracted inert startup.
Run builds sequentially: they share production and npm dependency directories.
Save compact receipts; preserve prior failed attempts and historical evidence.
Update H-41 and the lead continuation only according to executed evidence.
Do not rewrite the entire matrix or count unrelated repeated tests as new work.

Commit coherent checkpoints on the existing branch, push normally, update
draft PR #6 and verify direct remote SHA. Preserve unrelated work. Return
READY_FOR_LEAD_REVIEW only for the assigned completed unit; otherwise PARTIAL
with exact unresolved value flow and completed work. Whole Home remains PARTIAL.
No merge/publication or branch/worktree cleanup before lead acceptance.
