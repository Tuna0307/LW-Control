# Regular AI task — review 12

Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md), [the current audit](lwbridge-project-status.md), [the live-result evidence/task](first-live-result.md) and the relevant [evidence index](README.md). Inspect current HEAD/worktree before work; review 12 audited `749b8e3`. All 47 acceptance cases remain required.

## PM decision — continue the accepted current-client route

R7-003 is accepted for the bounded fresh-acquisition/backend portion of PM10-01, including its two recorded reads. Original LWBridge pipe parity is not a prerequisite for that portion. Normal Map Data display remains unverified and reliability defects keep the whole milestone partial. Do not ask for this route decision again or stop with the old 'awaiting PM acceptance' message.

Regular AI owns the ordered queue below. After a coherent checkpoint is tested, documented, committed, pushed and verified, continue the next supported item when asked to continue; no new PM permission is needed for those routine steps. Keep one active owner and preserve concurrent work. Specialist assignment, genuinely unavailable capabilities and full feature sign-off remain distinct.

## Ordered work and acceptance

### PM12-A — recover reliably from partial install; own the actual target

Start here. Read the audit and reproduce the known defect with the isolated [dummy-file script](../evidence/lwbridge-implementation/2026-09-10-pm12-partial-install-repro.py). It shows failures on replacement 2/3 leave changed files because rollback is not armed until after all writes.

Implement restoration from every partial stage, durable backup/recovery state and exact before/after hashes for data, metadata and version. Preserve both the original and cleanup error if both fail. Add a shared operation lease covering all app/helper instances; per-service SemaphoreSlim is insufficient for the shared game files and command/result paths. Resolve and validate the chosen installation and session before any restart, target its exact PID, and prefer normal close. Do not stop every process by image name. Reuse valid current-build hashes rather than creating an updater.

Validate injected failure after each install/restore stage, overlapping instances and interrupted recovery using isolated dummy files. Do not run further installed-file mutation tests until those restoration/ownership checks pass. That is a defect-fix prerequisite, not a request for user permission. Do not erase backups or conceal known failures to make checks pass.

### PM12-B — make Start, Stop, cancellation and close coherent

Reject duplicate Start rather than queue another acquisition behind the first. Route Stop to the real operation and expose truthful reading/cancelling/restoring/failure states. A cancelled/timed-out/closed document must not publish a fresh success or commit an unapproved late result. Allow restoration to finish safely and coordinate store/window disposal with cleanup. Reconcile the UI's 30-second request lifetime with the helper's bounded operation and provide supervision for a stuck helper; killing it blindly during restoration is not a solution. Label rebuilt timeout/state decisions as implementation policy where not recovered.

Use fake helpers to test duplicate starts, slow/hung work, Stop, cancellation, timeout, late results and app close. Preserve normal launch/online and unsupported-kind gates; no fixtures may trigger the game.

### PM12-C — immutable result ingestion and verified scope

Hash, validate and ingest the same immutable bytes; do not validate the shared file then reopen it for persistence. Prefer request-owned output paths and ensure an older/foreign session cannot supply a result or heartbeat accepted for the current operation. Validate the actual supported profile/session/server/build/time scope, including the reused-probe route, rather than using process name and heartbeat age as identity. Missing fields remain unknown; remove unjustified optional zero fallbacks and do not broaden the demo's numeric bounds without evidence.

Add deterministic replacement-between-reads, stale/foreign server/session, mismatched source and concurrent-output tests. Keep authoritative before/after/source correlation for live acceptance; a request ID string alone is not proof of the selected point's full game semantics.

### PM12-D — show and verify the fresh resource in the normal app

After A–C are supported, verify the normal window's resource-only Start Scan through its real native boundary, summary/query and rendered row; then verify another fresh read and refresh. The old `--live-resource-proof` backend runner is useful evidence but does not create a window. Record session/context/time/point identity plus actual UI diagnostics and screenshot when permitted. Keep unknown resource name/occupancy honest: the fresh path must not inherit Idle merely because gather IDs are absent. Describe the supported current-view/resource-only scope without implying full-world scan, continuous connection, or different Normal/Fast behavior that is not implemented.

Respect disabled native-control capabilities and prior rejected capture operations. If UI validation is unavailable, record the exact remaining visual gate and continue independent supported work in A–C or a directly dependent Map Data task. Do not reroute a denied operation or self-impose another blanket PM waiting gate.

## After this queue

When these defects are fixed and the fresh row is actually demonstrated in the normal UI, continue the highest-priority evidence-backed remaining Map Data function under `task.md`: supported names/occupancy and current-view record integration, then scan coverage/lifecycle and the remaining queries/actions. Recover each missing contract before enabling it. A one-row result does not close full-world scanning, every category, export, auto scan, Overview or all 47 cases. Updater, broad export/migrations and unrelated research remain deferred until the current milestone's user-visible proof; do not chase marker-only findings.

PM8 reinstall/recovery and PM7-A's bounded aggregate service are already complete in their stated scopes. Preserve them and the login-free recovered UI. R5-006/007 framing/hello and R7-001/002 replay findings remain useful; do not repeat them.

## Daybreak and blockers

ESC-005 is **NOT_ASSIGNED / deferred original-pipe parity** after PM review 12. Its more detailed packet is retained; the current route removes it from the immediate dependency. No Daybreak task is approved. Reopen only for a concrete feature dependency and a clearly permitted bounded method; do not transfer denied host-range/search/pointer operations. ESC-001 remains closed, ESC-003 not assigned, ESC-002/004 incomplete and deferred. See [the register](daybreak-escalations.md).

Your existing game-control, Computer Use and tool-installation permissions remain granted under AGENTS.md. Environment restrictions remain separate. If no meaningful permitted work remains, name the exact missing capability/condition/decision, preserve the handoff and stop rather than inventing filler work.

## Checkpoint delivery

Run checks appropriate to the change, including new fault regressions. Existing commands: Release desktop build; deterministic desktop suite with `--verify-real-config-unchanged`; `python tools/build_lwbridge_frontend.py --check`; preference and both transport checks. Sequence builds sharing output. Additional UI/live checks must be explicitly distinguished from offline success.

Save findings and update backlog/ledger/handoff incrementally. Commit coherent tested fixes and evidence, push the existing branch and verify GitHub. Do not wait for the whole milestone to finish before preserving a useful checkpoint. Report what the user can use, exact known failures/live-proof limits, delivery commit and next action. Never claim a checkpoint preserving known failures is a release.
