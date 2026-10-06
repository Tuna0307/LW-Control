# RECOVERY-003 coordination

Starting checkpoint: `138ea26469b7a35c0758198c98f8c44f352037b0`, clean
`research/offline-controller`. Scope is exactly R3-01 through R3-05 and bounded
offline/inert integration. All historical lead-negative evidence remains immutable.

- Coordinator owns App, MapDataPage, Auto helpers/coordinator, Window package
  proof, current check adapters, integration evidence, status documents and Git.
- Home agent owns Overview lifecycle/file transactions, pipe lease consumer,
  Python helper, narrowly coupled Lua lease-read consumers and isolated tests.
- Map agent owns MapScanStateMachine and native-boundary Stop test only.
- Frontend reviewer owns checkpoint-B design/source-locator report and extracted
  current-handler inverse test/evidence only; it does not edit production.

Only coordinator builds the shared native output, drives package-owned processes,
integrates/stages/commits/pushes. No shared browser or process is controlled by
multiple agents. Agents execute only explicitly isolated lane-local tests.

New runtime publication uses a verified exclusive Windows handle rather than an
unlocked pathname replace. Coupled readers must distinguish transient sharing
contention from invalid, foreign or expired data. Such adaptation is clone-internal
implementation policy, not an original-service protocol claim.

Checkpoint A closes native ownership/Stop; B closes frontend rollback/semantic
rebase; C proves actual mounted packaged controls and preserves session issues
and cleanup. Worker delivery remains AWAITING_REVIEW; project lead accepts.
