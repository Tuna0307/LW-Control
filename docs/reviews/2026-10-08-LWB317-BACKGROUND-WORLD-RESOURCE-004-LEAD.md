# World/Resource 004: independent project-lead review

Date: 2026-10-08. Reviewed checkpoint: `ba97b8bf8fa8438004ae8d69ff95b9a4a1d8695d`.
Includes `df4c9551` heartbeat wiring, `d59a4aaa` runner/completion,
`5f2140cd` active Stop, and the latest provenance correction.

Disposition: **ACCEPTED for bounded current-v22 native Resource completion,
separate active Stop, and durable storage proof.** Full Home/Map remains PARTIAL.
This does not accept protected original-provider equivalence or complete UI/live parity.

## Independently executed

New proof is under the 004 packet's `lead-review-2026-10-08/`:
`independent-dual-run-audit.json`, `inventory.json`, `check-results.json`.
Historical evidence was not rewritten.

- Reran the auditor against both retained real databases with SQLite read-only/query-only access.
  Completed run `b439bfa57cd54405a14e803d21964c9d`: server 2212,
  2500/2500 blocks, zero failures, 8008 published Resource records, zero staging.
  Separate run `b9fc31a14f6f4c3d819c0e17efaddbe8`: cancelled,
  zero staged/published records, with active Stop while one block was in flight.
- Retained world-ready records match profile/session/PID and 1000x1000 dimensions.
  Retained manifests/backups and post-run hashes validate restoration and absent journals.
- Fresh installed script triplet and reference EXE hash match the expected originals;
  fresh process inventory is empty. The direct remote matched the reviewed checkpoint.
- Fresh heartbeat structural/syntax checks 4/4, direct delegate checks 4/4,
  file ownership checks 5/5, and production Lua lease checks 6/6 pass.
- Fresh actual production state-machine/adapter inverse suite passes 6/6.
  The stale/replaced-session cases inject the provider's failure; they are not a
  new live session-replacement race proof.
- Inspected the worker's retained production MapStore reopen proof, Release/frontend/
  package logs and actual changes. Did not independently rerun every broader suite
  or launch a game/perform desktop capture/input during this review.

## Implementation assessment

The runner previously rejected city state before reaching the existing production
world-entry path. Removing that checks-only gate preserves server admission and
allows the normal provider to establish world readiness. Correct query envelopes
are now `{kind,query:{serverId,page,pageSize,...}}`.

The heartbeat change calls the already-existing authenticated five-second sender
from Pump. Pump checks the active lease and clears obsolete session state before
this call; the sender requires an active connected adapter and uses profile/session
identity. The host's 30-second timeout is preserved. This is a current-client
transport adaptation, not a recovered new original protocol. Fresh tests establish
syntax/structure and inherited ownership behavior; the real completed scan supplies
bounded integration evidence. No new transport, relaxed lease or fake success was added.

The first completed run's test terminal is still a failure because its post-scan
query envelope was malformed. This does not negate the independently observed
durable completed publication. Preserve that negative event; do not relabel the
whole attempt as an end-to-end command PASS.

## Remaining proof gaps

1. Positive high-level `map_search` against the completed dataset was not proved.
   The actual MapStore positive reopen/pagination/filter proof is a different layer.
   Next assignment closes command, frontend payload and profile-reopen coverage.
2. Both world-ready results use `already_world_scene`; the alternative
   `SceneUtils.ChangeToWorld(callback)` has not been live witnessed here.
3. Active Stop occurred before any completed block. Cancellation with positive
   staging/late responses is not newly live-proven by this packet.
4. 8008 is an observed published population, not proof of game-universe completeness
   or equality with the original protected extractor.
5. Canonical desktop UI operation, original provider plaintext/behavior, native assets
   and protected original-runtime pixel comparison remain separate. Resource export
   is unsupported in the recovered City-only export path; do not add it for this proof.

## Provenance and next ownership

Use the corrected account at `ba97b8bf`: independently running chats overlapped
checkout work; one authored heartbeat, another launched the completed run, and
the reporting chat subsequently launched the separate active-Stop session.
No delegation is established; the two game sessions did not overlap. The relay's
claim that it did not launch the additional process is superseded by the committed
correction. Keep this history intact.

Next solo manual-relay task:
`LWB317-FUNCTION-HOME-MAP-BACKGROUND-COMMAND-CLOSEOUT-005`.
One coordinator owns its files/processes. Reuse a safe copy of the real completed
dataset to close command-layer coverage; no unnecessary repeat full-world scan.
Current background-only restriction remains; no shared-desktop capture/input/focus.
