# Live attempt 4 — abandoned runtime metadata

Date: 2026-10-07
Disposition: FAILED_SAFE / RESTORED

After checkpoint-1 restoration and all pre-live gates, canonical Home PID 25800
started a fresh isolated launch. Launcher PID 27480 and game PID 35600 were newly
created. Recovery armed backup
`overview-bridge-backups/20261007-154136-0478e77b73234a65afd10a7b171b9639`.

The helper failed before publishing fresh control metadata with:

`refusing to overwrite foreign or malformed runtime metadata`

Fresh session ID: `8bbc7bf2677642dc9b8d679313ddcff8`.

The preserved `stale-control.txt`, `stale-ready.json`,
`stale-heartbeat.json` and `stale-pipe-transport.json` identify the earlier
completed session/PID 40992. They were not fresh evidence for PID 35600.

Cleanup was safe and complete: helper-start-cleanup reports owned game PID 35600,
launcher PID 27480, both closed, recovery cleared, and the installed script
triplet was restored to the exact original hashes.

Root cause: normal start proves no selected game process and rejects any fresh
foreign lease, but its existing `clear_stale_runtime` intentionally deletes only
files matching the *new* session identity. Therefore abandoned previous-session
control/lease/ready/heartbeat files survived and `write_control` correctly refused
to overwrite them.

Correction: keep ownership-preserving `clear_stale_runtime` unchanged. Add a
separate abandoned-runtime cleanup used only after (1) the process gate proves no
selected LastWar process and (2) the foreign-lease gate proves no fresh lease.
It deletes only the exact opened identities for control/lease/ready/heartbeat and
does not touch cancel markers, evidence, Map data, or unrelated runtime files.
