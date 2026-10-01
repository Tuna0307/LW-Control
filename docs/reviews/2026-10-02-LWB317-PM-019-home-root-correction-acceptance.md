# PM-019 — accept Home root acknowledgement correction

Date: 2026-10-02. Worker checkpoint:
`8415316ef1c7bd66ca94ff44670a7654d03688d6`.
Decision: **COMPLETE / ACCEPTED for focused recovered-source/local scope** for
LWB317-UI-HOME-ERROR-002-R1 and the corrected HOME-ERROR-002 channel unit.
PM-018's required root acknowledgement/polling correction is resolved.

Lead reviewed the full production diff: only App.jsx changed product behavior.
The selected-profile root effect uses the existing immutable bootstrap profile ID,
and is gated by native availability and selected profile. A stable acknowledgement
callback sets root status then clears only root error. Both initial retrieval and
valid post-selection retrieval use it. The repeated refresh omits root status;
the other status/proxy/scan/recovery/config requests, listeners and timer remain.
The explicit server-jump refresh no longer retrieves root status, as disclosed.

Exact source index-BVfnK1wp.js SHA-256:
44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 bytes: Jt 367489, selected-profile effect 369239,
initial root request 369540, periodic status/proxy poll 369979.
Lead inspected the extracted original and current effect/dependency expressions,
worker checker, all fourteen scenario expectations and recorded results.
The checker executes actual callbacks/effect bodies through the real frontend
bridge; it is not a parallel implementation of the corrected handler.

Lead reproduced check-root-acknowledgement.mjs --verify-record and the R1 evidence
validator. Fourteen cases pass, covering success/failure/deferred acknowledgement,
profile/transport gating, cancellation surviving later polling, picker failures,
post-selection acknowledgement and preference/reconnect isolation/order.
An additional independent lead differential executes exact Jt and the current
acknowledgement for four payloads, comparing setter order, root/error state and
preservation of the action channel. It passes; checker and report are under
`evidence/lwbridge-0.3.17/ui/LWB317-PM-019/`.

The worker's two-command "native inventory" is specifically the direct bridge
inventory in a test that stubs mapApi. In production, the unchanged three mapApi
reads also invoke native status/proxy/scan commands. This acceptance establishes
that root status is absent from repeated refresh, not that only two total native
requests occur. No remaining polling subsystem is accepted as exact original parity.

Accepted translation checker/validator (60 edge cases, 4,230 catalog checks, 27
renders) and switch checker/validator (360 comparisons) pass unchanged. Busy
assertions pass without historical App-hash record equality: 13,824 render
comparisons, 384 predicates, 27 preference cases and 63 localized preview cases.
Busy acceptance remains separate. Historical reports were preserved, not regenerated.
Canonical check and production package verification pass. No product changes
were made by this review, so another build was unnecessary. Verified package
fingerprints are 8055b198949dcac217b15c7b8dac40e6f965847ef472906cf27b1a68388aeb0b /
3a01e609bf7f5281757cf843181bc2c2a1fdf5f544a62a1fae50b87a12616cb4.
Target executable hash and direct remote worker revision were verified.

Worker browser evidence records unchanged simultaneous/missing-root error
placement, disabled native controls and no captured errors. Lead opened no browser,
picker, game or protected original session. Native rejection-string contracts,
dynamic profile switching beyond the existing bootstrap contract, persistence,
native lifecycle and original runtime pixels remain unproved. Existing rejection
message reduction remains unchanged and is outside this acknowledgement acceptance.
Global UI remains IMPLEMENTED_NOT_VALIDATED; original pixels remain BLOCKED.

Next bounded assignment: `../work-items/LWB317-REVIEW-HOME-BUSY-001.md`.
Trade 003E remains AWAITING_REVIEW. Protected AFK/scratch/parent screenshot WIP
remains untouched and unstaged.
