# Chat On Steroids collaboration

Owner policy, adapted for LW-Control on 2026-10-02. Read with `AGENTS.md` and
`AI_WORK_PROTOCOL.md`. The project lead assigns work, checks evidence and makes
the final acceptance decision.

## Model

Always use **5.6 Thinking / reasoning, High effort**. The connected account
currently exposes it as `gpt-5-6-thinking` with `reasoning_effort: high`.
Specify both when creating a worker and verify the returned selection. Do not
silently choose another model or effort, or change the app's global defaults.
An invitation is not proof that the prompt was sent or the review completed.

## Assigning work

Prefer Chat On Steroids for routine research, first drafts, mechanical edits and
file reviews. Do not replace it with native/local subagents. Use its direct
connector when available to save the owner from copying prompts between chats.
Reuse a suitable worker whose ownership, model and effort are confirmed.

Give each fresh chat one concrete, bounded assignment containing:

- Task ID, goal, repository, branch and baseline revision.
- Files and source evidence to inspect, with exact locators where known.
- Observed symptoms, background and the exact allowed scope.
- Intentional design decisions and existing behavior that must stay unchanged.
- Allowed edits, protected unrelated work, acceptance checks and expected handoff.

Let the collaborator inspect actual files, diffs, commands and test results.
Reviewers are read-only; implementation workers may edit their assigned files.
Avoid overlapping writes. Workers must not create further workers, broaden the
campaign, discard unrelated changes or accept their own implementation as an
independent review. Only the lead integrates master status.

## Before meaningful code changes

Write explicit hypotheses explaining the observed problem. Give Chat On Steroids
the symptoms, relevant code, source evidence, background and constraints. Ask it
to find counter-evidence, missing considerations, incorrect assumptions and
alternative explanations. Evaluate its response against the actual evidence
before editing. Revisit this checkpoint if the diagnosis or scope changes
materially. Read-only investigation can start before this review.

## Before submitting meaningful implementation work

Give Chat On Steroids the complete owned diff, including new files, plus design
context, acceptance criteria and verification results. Identify unrelated dirty
files so they are not mistaken for the assignment. Ask only:

> Where could this break?

Have it focus on regressions, edge cases, missing changes, incorrect assumptions
and conflicts with the intended design. Independently evaluate each issue and
check any resulting fix. Material changes need another review of the updated
diff. A worker's self-check does not replace an independent review.

## Practical exceptions and connector problems

Minor changes may skip these checkpoints with a short recorded reason. Routine
handoffs, progress records, research notes and documentation housekeeping can
proceed after lead verification. Small code changes affecting defaults, timing,
state, persistence or permissions are not automatically minor. Documentation
that changes product scope, contracts or acceptance criteria needs a diff review.

If a required review is unavailable, record the exact failed operation and
continuation point. Continue useful investigation and documentation; do not claim
review completion or submit meaningful implementation that still needs review
unless the owner explicitly authorizes an exception. A worker error does not
mean functioning file or command tools are unavailable. Do not bypass worker
ownership checks, create duplicate workers to evade them or substitute models.
There is no fixed 20-minute limit.

## Evaluating and reporting reviews

Collaborator output counts only after the lead has reviewed and verified it.
Record the reviewed revision/diff, model/effort, evidence and relevant checks.
After each review, report concisely using this structure:

| What Chat On Steroids reported | Valid / invalid / unverified | Why | Changed anything because of it? |
|---|---|---|---|
| Each issue, or its explicit no-issues result | Lead judgment | Source, code or test evidence | Fix made, or reason for no change |

Use `unverified` when evidence is insufficient. Report a missing worker response
as pending; do not invent a review verdict. The collaborator advises; the lead
remains responsible for the result.

## LW-Control constraints

Reproduce the in-scope LWBridge 0.3.17 UI/UX without redesigning recovered
behavior. Exclude login/licensing UI and preserve one canonical product path.
Keep source/local proof separate from original pixel or live-game proof.
Follow the named assignment, existing evidence rules and gameplay/session gates.
Include these constraints in every relevant worker brief.
