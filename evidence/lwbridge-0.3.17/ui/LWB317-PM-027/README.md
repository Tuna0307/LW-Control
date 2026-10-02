# PM-027 independent lead review

Target worker delivery: `c99c7613c6320fe5659e96e4a7235896e9ad9770`.
Outcome: **CHANGES_REQUIRED** for one options-response/server-sync mismatch.

`independent-cases.mjs` was written by the project lead after inspecting the
actual production diff and exact original source. It evaluates both actual
components through existing persistent hook runners. All responses are
synthetic; there are no native/gameplay actions.

Run from the repository root:

    node evidence/lwbridge-0.3.17/ui/LWB317-PM-027/independent-cases.mjs

On the submitted delivery this exits 1: one source parity failure and five
passing additional canonical ownership cases. To inspect all outcomes without
the final failure exit, use `--audit`. `--record` writes the JSON; the committed
`independent-results.json` preserves the lead's submitted-delivery observation.
Future corrections must preserve that historical record and store fresh results
under their own evidence directory. Normal execution does not write any files.

Mismatch: initial source scan server 321; the current options request for 321
returns a payload with serverId 322. Original settles at 321 after requests
`[321,322,321]`. Current settles at 322 after `[321,322]`. Both initially discard
the mismatched payload's lists. Production is missing the original interaction
between the options redirect and the scan-server synchronization effect.

Original `MapDataPanel-B4GXEND2.js`:
SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
UTF-8 byte anchors: options effect 34871-35650; server synchronization 33502-33682.
The latter depends on both data-server R and source scan-state C.serverId.
Clear success also updates parent scan state, so it must be tested separately
and must retain the already corrected delayed-Clear `[322,321]` behavior.

Lead replays passed: worker source/checker/validator; request lifetime (38,
current 0 failures); navigation (17 searches, current 0 failures); interactions
(38/38); strict integration (14/14); historical filter/table/Checking/row/state
assertions; canonical check/build/package; protected WIP (5 files); diff checks.
Package source/artifact fingerprints matched the worker:
`9c710c0c28448648769682aaf12073d2d1d2523b2ee96e5e1031fbed931a5bfd` /
`5a925d4c24e7f02e8056002b3c4b99d5d2c6000740dafdb0f07994d6e95b7e8f`.
Original EXE hash was rechecked directly and matches AGENTS.md.

The lead inspected the worker's narrow screenshot and browser record; no fresh
interactive browser session was run in PM-027. Those observations remain
worker browser evidence. Successful Clear remains synthetic-only. No original
post-auth pixels, native producer/persistence or gameplay proof is established.
