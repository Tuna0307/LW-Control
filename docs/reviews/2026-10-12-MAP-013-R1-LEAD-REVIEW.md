# MAP-013 correction checkpoint — independent lead review

Source `75c70cbe3131d54c6ddd4b0fe463da96fefe959b`, same branch/PR #9;
initial working tree clean, direct remote exact match.

**ACCEPT LEADMAP013-01 and LEADMAP013-02's bounded correction/retest scope.**
LEADMAP013-02 closes the inadequate initial-budget review issue; it does not
recover the four functions or prove them unrecoverable. Overall MAP-013 scanner
objective remains PARTIAL. No production-source delta, faster route, new live
witness, merge or release in this checkpoint.

## Independent execution and inspection

- Ran `python tools/rea-mcp/validate-selective.py artifacts/rea-toolchain/lead013-source-bounded/report.txt`.
  Original EXE SHA matches required reference; `.pdata` bounds match.
  `0x140191253` raw pseudocode16,815 characters/bytes, SHA
  `908b8e7194bdf6f1d73b135129f0923c41fc57fbbc513d6e74e7c17cb98a64be`;
  source `[0x140191253,0x140191aca)` is2,167 bytes. Read actual tail ending
  in state assignment and final return/closing brace. Full retention fixed.
- Three audit tests independently PASS: retained source/hash, missing truncation
  metadata rejection and wrong unwind bounds rejection. The tests actually ran,
  with required local evidence present (not skipped).
- Twenty-four offline current-game/body/accuracy tests independently PASS.
- Source inspection confirms parameterized10..300s helper budget, full raw UTF8
  output, SHA/length and full/excerpt distinction. Fresh report confirms75s budget,
  verified ranges and timeouts for all four larger candidates, no false positives.
- `git diff --name-only 1d20506b..75c70cbe -- src` returns no product change;
  no new product release sweep was needed for this lead audit. Diff check PASS.
- No Ghidra Java/decompiler helper processes appeared in the post-audit query.
  Lead performed read-only offline validation, no new game/desktop action.

## What remains open

The retained pseudocode is an actual original code view, not original authored
source or a proven scanner algorithm. Complete text alone does not assign Rust
state offsets/tags their feature meaning. Four large functions remain bounded
analysis negatives; no missing-key or format impossibility follows from that.

Current-game IL confirms sender, receiver and cache insertion code exists. R15/R16
counts59->7, message indices0 and unscoped immediate send counter203->203 still
do not identify the actual failed edge. No matched packet/payload/segment/cache
trace was added by this checkpoint. Q1-Q8 and production method are unchanged.

## Next execution priority inside the same assignment

Continue MAP-013 stage C: compare an actual working baseline region with the
failing consecutive-region candidate through source-supported observations at
outgoing request construction/dispatch, incoming parser/segments and native
cache insert/readout. Start with fresh independently populated known regions;
capture the concrete first divergent predicate/value/route, not generic counters.
Preserve original static recovery for targeted calls that bear on that mechanism.
Do not spend the next checkpoint repeating the four timed-out decompilations
unchanged or rerunning accepted installation verification.

Loop available diagnosis/fix/retest work yourself within existing background-only,
passive-query, ownership/isolation/restoration authority. No mouse/focus manipulation,
account-changing action or unrelated game Stop. Keep baseline and published data.
No new branch/campaign; original MAP-012/013 answer and benchmark gates apply.
Tool/evidence corrections accepted; scanner-goal acceptance stays withheld.
