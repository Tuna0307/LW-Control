# External AI audit guide — strict parity phase

Use this document when reviewing the repository after the 2026-09-24 direction reset.

## Read in this order

1. `AGENTS.md`
2. `docs/strict-parity-recovery.md`
3. `docs/lwbridge-parity-matrix.md`
4. `docs/implementation-handoff.md`
5. `BACKLOG.md`
6. `docs/lwbridge-project-status.md`
7. `docs/deep-binary-handoff.md`
8. cumulative technical ledgers/evidence as needed

## Audit question

Do not ask only “does the rebuild work?”

Ask:

**“Is this behavior demonstrated in the verified LWBridge 0.3.1 reference, and does the rebuild reproduce it without intentional deviation?”**

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Classification

Challenge every feature into one of:

- EXACT_BYTES
- EXACT_CONTRACT
- EQUIVALENT_REIMPLEMENTATION
- DEVIATION
- UNKNOWN

“LIVE-PROVEN” is useful operational evidence but does not prove original parity by itself.

## High-value challenge areas

- Are recovered frontend chunks still byte-identical, or did the generator mutate product logic?
- Are original auth/account flows missing?
- Is City Excel export still missing?
- Are original Scheduled Plunder surfaces still missing?
- Does any rebuild-only Quick Find or other convenience feature remain?
- Are Map acquisition strategies supported by original LWBridge evidence or only current-game experimentation?
- Is the full `bridge-scripts.dat` package recovered?
- Is the exact host/proxy request-result protocol recovered?
- Are Automation, Squads/AFK, City Layout, Hotkeys, Mini-games and Settings backends actually audited, or only their UI assets?
- Do current-client compatibility shims preserve original visible behavior?
- Are historical “PASS” claims being mistaken for one-to-one completion?

## Historical restrictions

Preserve old restriction records exactly. A denied historical operation is not evidence that its underlying question is impossible. Review whether the project has a genuinely distinct permitted method; do not reroute a prohibited operation through another executor.

## Historical evidence

R1-R7 reviews and evidence are intentionally retained. They should be used as provenance and current-client capability evidence, not as automatic authority for R8 product design.

The old 47-case matrix is therefore a historical reconstruction matrix, not the current whole-program release matrix.
