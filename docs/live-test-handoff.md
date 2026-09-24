# Current live-test handoff — strict parity phase

**Current through:** `LWB-R8-008`, 2026-09-24.

Live testing is no longer driven by the old “close remaining Home/Map acceptance rows” matrix. The primary task is now original-reference recovery and parity implementation.

## Current testing rule

Do not ask the owner to repeatedly test reconstructed behavior that has not first been tied back to LWBridge 0.3.1.

For each parity feature:

1. recover the original reference contract/bytes;
2. implement or map it to the current Last War client;
3. run automated reference-vs-rebuild checks where possible;
4. run automated current-client technical checks;
5. ask the owner only for the minimal visible interaction that cannot be captured automatically.

## Owner interaction

The owner is not expected to run commands, inspect JSON, calculate hashes, locate databases, or diagnose technical state.

ChatGPT should operate the technical collection path directly whenever permitted and request only simple UI observations/screenshots when necessary.

## Historical R7 live evidence

Ghost/Supplies population checks, Railway population checks, multi-account availability, lifecycle stress, Map scan proofs and other R7 results remain useful current-client evidence. They are not current parity gates by themselves.

Previously retired state-changing features such as Scheduled Plunder are no longer considered out of product scope merely because R7 removed them. Before live testing them, first recover the original 0.3.1 behavior and restore the exact intended implementation.

## Safety / action boundary

Do not perform irreversible or state-changing actions merely to make a matrix row green. Follow current tool/environment rules and preserve explicit authorization requirements for messaging/spending/consuming actions.

## Current technical sources

- `docs/strict-parity-recovery.md`
- `docs/lwbridge-parity-matrix.md`
- `docs/implementation-handoff.md`
- `BACKLOG.md`
- `evidence/lwbridge-implementation/2026-09-24-r8-current-evidence-index.json`
