# Active standard-AI task — display one real resource point

User-approved priority correction, 2026-09-10, after review 9. This supersedes the earlier instruction to implement PM9-A first. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md) and [the standard handoff](implementation-handoff.md). No new model/task is dispatched by this document.

## One concrete outcome

**Establish a supported connection to the running official game, acquire one real resource point, and display that automatically acquired record in the rebuilt Map Data page.** Reuse the current application and valid research. Research only the specific contracts still needed to deliver this result.

Scope spans R5 connection/identity, the necessary R6 normalization/index/display path, and the smallest R7 acquisition. It does not require finishing every R5 reconnect feature or every R6 export/filter first; equally, later-area research does not count as a working connection. A supported connection to a manually launched, explicitly authorized session may support the demonstration, but does not prove the rebuilt Launch Game/Close Game buttons work. Record that distinction and label any deliberate rebuild choice as IMPLEMENTATION POLICY.

## Testing permission is already granted

The user authorizes opening, closing and restarting the game/launcher whenever needed, controlling the computer through Computer Use for this project, navigating the game/app and collecting a bounded real-data sample and evidence. This includes the user's current game session. Follow the active Computer Use skill/tool instructions; identify the target window/process and coordinate shared access. Do not ask the user to operate the game simply because the AI has not tried its available permitted controls. Actual platform restrictions remain in force and must be reported rather than bypassed.

## Execution

1. Inspect HEAD/worktree and current installation/process state. PM8 recovery is closed by the user's reinstall; do not repeat it. Refresh the minimal source hash/integrity checks needed for this test using existing diagnostics. Building future auto-update infrastructure is deferred.
2. Start/open the game as needed and establish the supported session/connection. Identify the next missing prerequisite before implementing it; a process or visible game window alone is not bridge readiness. Reuse PM7-B/R5 evidence. Do not invent bootstrap values, commands or packet layouts.
3. Select one real resource point and trace its current-client data through the actual acquisition/serializer into the original normalized builder/storage/display inputs. R6-047/049/051 cover original layout/identity/scalars; R6-048/050 cover current managed resource fields. They do not automatically prove the connection between the two. Resolve only the missing links for this sample, preserving lossless IDs and unknown fields.
4. Implement the required supported path into the existing Map Data UI. The row must come from the live source, not a mock, hardcoded fixture, manually typed row or screenshot transcription. Display only source-backed fields; do not fill absent values with plausible numbers. Keep unrelated unsupported commands unavailable.
5. Verify the displayed point against the actual source/game: identify the session and server/context, source record identity, acquisition time, supported coordinates/type and the displayed values. Preserve sanitized correlated input/result evidence plus an app screenshot; screenshots alone are not dataflow proof. Verify refresh does not silently fall back to fixture data.
6. Run the checks appropriate to changed behavior. Deliver a runnable build/path and concise reproduction steps. State whether game startup was manual/official or performed by the rebuilt app, and whether repeated acquisition was tested. No single-point success closes all 47 acceptance cases or proves full-map scanning/reconnect.

## If a link remains blocked

Name the first missing link in the actual connection -> acquisition -> normalization -> display path. Record the exact question, source/build, attempts/results, permitted alternatives and next method or external condition. Do not replace it with unrelated research. Prepare/revise an ESC entry only under AGENTS.md's method/exhaustion rules; an old ESC or denial is not a Daybreak assignment. Continue directly relevant permitted work and report honestly if no live result was achieved.

## Deferred work and reporting

PM9-A automatic-update readiness, full export/migrations, broad alternative sorting and other features remain in the backlog until the first result, unless a narrowly identified prerequisite is essential. The user wants a working demonstration, not a larger research inventory.

At each checkpoint report: **what visibly works; whether the sample is genuinely live; what prevents the next step; the next action; checks and delivered commit**. Save confirmed findings immediately, update the feature ledger/backlog, commit/push and verify the existing GitHub branch. Do not stop with a claim that offline tests prove the game integration.
