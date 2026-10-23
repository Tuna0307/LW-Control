# FEATURE-RECOVERY-018 foreign recovery-state investigation (read-only)

Date: 2026-10-17. This audit did not launch a game, run an updater, call a recovery operation, alter any game file, or mutate, adopt, delete or rename the foreign journal or backup.

## Evidence and interpretation

The global journal `%LOCALAPPDATA%/LWBridgeRebuild/overview-bridge/recovery.json` exists and is not owned by FEATURE-RECOVERY-018. Its SHA-256 at review was `7FDFC54C09E1D6B384123C355CB55D7FA39928BDB0A2D23637A6FAB00146C88` **(recheck before relying on the digest; see independent audit command below)**. It is schema 1, stage `active_ready_deferred_restore`, modified `2026-10-16T17:49:40Z`, with a foreign local-profile ID, foreign session ID, and old game PID 31476. Its backup points into `%LOCALAPPDATA%/LWBridgeRebuild/overview-bridge-backups/`, outside this worker's isolated app proof root. The owner-supplied field values are never passed to this worker's launch or Close service. The historical game PID is not running; process absence does not establish safe transfer or completion of restoration.

The referenced backup directory and manifest **exist**, and its manifest has `state: backup_ready`, not `restored`. Read-only hashing gave:

| File | Preserved foreign owner's original SHA-256 | Currently installed SHA-256 | Finding |
|---|---|---|---|
| `LWScripts.data` | `49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d` | `ec8f2c3806a845e57e5974fb8efe00590a4fd0cb4e7942adf35fd1864ee40859` | **different; injected candidate retained** |
| `LWScripts.txt` | `52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0` | `09c7881b7e55b61bbb6d6cc683175b20d0808f44575f7bd3da7ac947cb5ca3e2` | **different; injected candidate retained** |
| `version.txt` | `5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca` | same | unchanged |

The journal's own `originalFiles` digests match the preserved backup bytes. Its `candidate.packageSha256` is precisely `ec8f2c3806a845e57e5974fb8efe00590a4fd0cb4e7942adf35fd1864ee40859`, the installed `LWScripts.data` hash. This is direct evidence that the foreign runtime's modified game package remains installed after its process ended, not merely an obsolete journal after a completed restore.

## Safe admission outcome

The existing `tools/map_014_lead_native/run_ui.py:213-235` rejects any existing journal **before** reading or modifying installed package bytes or starting a process. The opt-in FEATURE-RECOVERY-018 packaged proof uses that exact preflight. Its rejection is correctly maintained. `tools/recover_overview_pending_current.py` invokes `recover_pending` under an operation lease and **would mutate** the foreign owner's recovery state; it is not a read-only audit and is not authorized for this worker. The presence of compatible original backup bytes cannot authorize this worker to install them or mark that foreign session restored. No alternate application root or altered admission check may be used to avoid the lock.

Only the original owning session's responsible recovery mechanism or an explicit independently authorized restoration process may reconcile that foreign journal and its installed candidate, producing a same-owner restoration receipt and verified original triplet. After that independent process is complete, check the global journal is gone, all three installed file hashes match the admitted original, no foreign processes remain, and the existing exact-owner game-session admission is satisfied **before** starting any FEATURE-RECOVERY-018 live proof. Until then all live-feature verdicts remain *unverified due to blocked admission*, regardless of offline method discovery or code changes.

One reproducible **read-only** inspection uses PowerShell `Get-FileHash` on the three installed files and journal, and Python `json.loads`/`hashlib.sha256` on the original backup and manifest. No code in this assignment writes to these paths. This report deliberately omits the foreign session's raw profile/session identifiers to avoid accidentally making them active owner inputs.
