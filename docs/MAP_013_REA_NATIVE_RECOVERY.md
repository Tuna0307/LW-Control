# MAP-013 — REA native recovery and scanner boundary (2026-10-12)

**Independent lead review:** source `c22a88b9` native-toolchain milestone passes
independent actual REA/Ghidra re-execution. Overall assignment remains PARTIAL.
LEADMAP013-01: `0x140191253` retained pseudocode is silently truncated by the
helper's10000-character limit. LEADMAP013-02: eight-second function budgets and
default330s full-import timeout are not exhausted recovery options. Correct and
continue the assigned task; see `reviews/2026-10-12-MAP-013-LEAD-REVIEW.md`.
Historical raw outputs stay preserved. No new scanner acceptance or merge.

**Status: PARTIAL — native tooling proven; production scanner unchanged.**
Continuation of `LWB317-MAP-SCANNER-QUESTIONS-012`; this is an evidence supplement,
not a second conflicting Q1–Q8 matrix. The canonical current matrix is
[`MAP_012_SCANNER_QUESTIONS.md`](MAP_012_SCANNER_QUESTIONS.md).

## What was genuinely recovered

1. **Native analysis is functional.** Installed official Ghidra 12.1.4 with
   full x64 Temurin JDK 21.0.12.1 in ignored project-local directories;
   validated both official release SHA-256s and actual Ghidra bridge policy.
   REA 6.4.0 provider doctor passed native decompiler, JDK, OS, host isolation
   and startup requirements; global Java 17/PATH were not replaced. The
   original-associated x64 PE `xlua-proxy-plain.dll` was hash-verified and
   analyzed through actual REA MCP/Ghidra, not merely a tool list. At
   `0x18004ac00` the native entry checks `param_2 == 1`, calls
   `FUN_18004afa0` when true and then calls `FUN_18004aac0`. Both actual
   assembly and pseudocode were returned; native callers, callees and
   references were queried and the helper was closed. This proves that
   machine-code analysis works, **not** original Map scanner reconstruction.
2. **Original EXE identity and bounded code sites.** The original real
   `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`
   is a 15,866,880-byte x86-64 Rust PE with required SHA-256
   `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`.
   REA admitted that exact target but its default Ghidra full auto-analysis
   reached the **330-second provider deadline** before a code overview could
   finish; the session closed. A **different** bounded offline approach used
   original PE `.pdata` unwind function bounds and Capstone/RIP-relative string
   references (22,976 decoded unwind bodies, 230 string/pointer hits). A
   hash-checked Ghidra `-noanalysis -readOnly -deleteProject` import then
   disassembled four source-related sites. Example native instruction:
   `0x14013a8da MOVZX EAX,[RCX+0xe30]`, `TEST EAX,EAX`, `JZ 0x14013a8f6`,
   `CMP EAX,3`, `JZ 0x14013a90f`; the `map_scan` literal/pointer xref is at
   `0x14013a965`. Additional candidate function starts:
   `0x140143d67`, `0x140159925`, `0x140427120` (last uses byte comparisons
   near `map.scan`). **Those four large functions individually timed out**
   in the decompiler. Two *different, smaller original EXE* functions,
   however, produced real pseudocode on the second selective pass, and three
   further called functions decompiled on a third pass; see next paragraph.
   All temporary headless projects were discarded and the original left intact.
3. **Actual original code branches and value-flow link.** At original
   `0x140191253` (2,167-byte unwind range, `map_scan`-related string reference
   at `0x1401912fb`), Ghidra recovered explicit state tests against offsets
   `param_1+0x860`, `+0x850`, `+0x848`, and `+0x840`, followed by divergent
   initialization/return paths. The decompiled body calls
   `FUN_140297a7f`, then on one path copies a `0x168`-byte state region
   and invokes `FUN_140344cbb` with saved state and two additional arguments;
   another path invokes `FUN_1400dc886` and branches on its returned tagged
   state. The individually decompiled `0x140297a7f` demonstrates **actual**
   two-way value flow: it reads through offsets `+0x20` and `+0x200`, checks
   the result of `FUN_140284612`, assigns output tag `6` plus returned pointer
   when nonzero, or invokes `FUN_1407a63da` when zero.
   `0x140344cbb` checks a returned status, dispatches via
   `FUN_140308ba1` only when a source pointer exists, and releases a
   reference-counted owner. `0x1400dc886` checks an input bit flag and
   performs guarded reference count/response-state operations, including
   a final state byte assignment. The other decompiled original function
   `0x1400dc3f4` distinguishes state bytes `0`/`3`, performs result/status
   transitions and contains a literal `10000` (not yet attributable to a
   scanner retry or timeout). **These are original EXE code bodies, not
   string-only names.** Without recovered type layouts and the callee's
   concrete message fields, their exact scanner semantics (kind dispatch,
   batching, retries, cancellation, original controller handoff) remain
   hypotheses, **not** proven contracts. Raw code is ignored at
   `artifacts/rea-toolchain/original-selective-small/selected-small.txt` and
   `original-selective-callees/selected-callees.txt`.
4. **Protected feature boundary remains precise.** The admitted original PE
   includes literal diagnostics `package decrypt failed` at
   `0x140abc7e3` and `0x140b539e3`, and `key envelope decrypt failed` at
   `0x140abe99b` and `0x140b55b9b`, plus encrypted lease/ticket names and
   `BCryptDecrypt` imports. This is evidence of diagnostics/imports, **not**
   a recovered original decryption call graph, algorithm/key source, valid
   envelope or ticket. A bounded direct-code/pointer scan found no direct
   reference to those precise error literals. The source-level required
   external key/ticket fields and exact acquisition point therefore remain
   unresolved. No original protected process, service or entitlement path
   was executed or bypassed.
5. **Current installed game:** hash-locked v24
   `Assembly-CSharp.rdl` (`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`)
   exposes `WorldPointManager.UpdateLWAoi_Normal`, `SendAoiRequest`,
   `SendViewRequest`, `StartViewRequest`, `UpdateViewRequest` and
   `HandleViewPointsReply` managed CIL bodies. Actual `SceneUtils.TileToWorld`
   and view/point/detail Lua were decoded by the already hash-locked v24
   inspectors (custom containers are **not** native PEs). Source demonstrates
   viewport/LOD-index requests and native reply/cache handling, but does not
   establish the game-server generation or target-native reply identity in
   the R15/R16 failed trial.

## Why the R15/R16 second region still fails

**Precisely established:** the logical camera tile/actual anchor rectangle,
native manager-selected AOIs and native response arrival are distinct stages.
R14's logically arrived second tile left the camera anchor at the old region;
R15 corrected anchor selection and computed 80 target-region LOD0 AOIs,
including `(545,495)` index `4954`. R16 additionally invoked managed
`SendAoiRequest` with `_addViewIndex` 80 items. **Neither yielded a fresh
independently populated second-region point/Train/Monster record.**

The R15/R16 raw receipts record `_msgViewIndex`-related observation count
zero after the invocation, current/add membership 80, `allViewPoints` target
absent and **zero** second-region source rows. Their ready flags, local app
requestId and unscoped packet counters do not tell which outbound native
packet was transmitted or which reply segment the native handler accepted.
The original archival map DB has historically populated nearby objects, but
is **not** a contemporaneous independent same-instant witness.

**Causal boundary:** a source-local AOI set selection and direct invocation
do **not** establish a completed source-owned network subscription/reply.
**The exact failed link is still unlocated:** no authoritative second-target
outbound command ID and payload, matching inbound segment/generation and
server/world source, or independently traced target cache insert exists.
Competing causes are unsent/new view, stale `_msgViewIndex` subscription,
unattributed/dropped target reply, or reading the wrong cache/manager family.
No one hypothesis is promoted as proven. The next distinguishing trial must
instrument the actual native transport send and `HandleViewPointsReply`
entry, capture headers/index segment and native cache insertion for a known-
positive first region, then the second and a third separated region under
the strict task-owned isolation/Stop/restore fences. Do **not** repeat an
arbitrary anchor/camera flag change.

## Q1–Q8 product consequence (refer to canonical detailed matrix)

| Q | MAP-013 added evidence / outstanding qualification |
| --- | --- |
| Q1 | Reconfirmed managed sender/receiver IL, but not the source-owned second-region request/reply and segment generation association. |
| Q2 | No native camera-independent complete unknown City, Resource, Monster, Truck, Railway, Dispatch, Ghost, Treasure or mixed discovery yet demonstrated. |
| Q3 | R15/R16 second AOI geometry without source objects is insufficient for one-final-home restoration. Keep current per-region validated restore. |
| Q4 | Known remote detail reply source inconclusive; region and entire remote-world discovery without server switch unqualified. Current-server serial scans remain supported. |
| Q5 | Current production serial AOI remains best **qualified among tested**. Historical all-eight Normal median 209.278s, Fast 207.142s (n=2 each), **not** a valid n>=3 new-candidate speed claim. |
| Q6 | Historical 25,393 native-field comparisons with no sampled mismatches; geometry alone does not prove exhaustive recall. Inert corruption audits and historical negative witnesses preserved. |
| Q7 | Shared mixed-kind native AOI pass preserved; per-kind marginal time/native work needs paired stable population trials. |
| Q8 | No production change, ZIP, merge or release. Normal/Fast, accepted Home, names, owner-keyed Map data, saved datasets and Auto Stop-before-return remain unchanged. Ghost/full seasonal Treasure still deferred. |

## Evidence and preservation

Compact tracked source/check evidence: `proof/MAP-013-REA-NATIVE-2026-10-12.json`.
Ignored large/private evidence: `artifacts/rea-toolchain/xlua-plain-proof-v2`,
`original-native-proof-v2`, `original-static-native-xrefs.json`,
`original-selective-ghidra/selected-source.txt`,
`original-selective-small/selected-small.txt`,
`original-selective-callees/selected-callees.txt`, `doctor.json`, tool ZIPs,
analysis scratch and pre-edit configuration backups. Direct headless Ghidra also
generated per-user `%APPDATA%/ghidra` and `%LOCALAPPDATA%/ghidra` caches from
initially absent paths at 19:46; after confirming zero Ghidra processes,
both task-created trees were **moved without deletion** to the ignored
`artifacts/rea-toolchain/owned-user-cache-backups` directory, returning both
user profile locations to their prior absence. Use process-scoped isolated
`APPDATA`/`LOCALAPPDATA` for any future *direct* Ghidra headless launches;
REA's own bridge instead starts with an isolated `-Duser.home` under its
session root. Offline genuine native
code decompilation and original candidate assembly must not be confused with
live network messages, source authority of an encrypted module, or new
positive full-map scanner measurements. No game or original commercial
runtime was started for MAP-013.
