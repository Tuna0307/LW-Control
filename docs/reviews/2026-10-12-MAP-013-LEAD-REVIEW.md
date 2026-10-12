# MAP-013 independent lead review

Worker checkpoint `c22a88b9ead0db74ced19dc280942b34740c712a`, same branch/PR #9;
initial tree clean, direct remote exact match. No product-source change in delivery.

**ACCEPT the portable native-toolchain milestone and bounded recovered code
observations. Overall MAP-013/Q1-Q8 remain PARTIAL. Research helper/output corrections
below are required before treating the selective work as full function recovery.**
No merge/publication or scanner route approval. No game or original protected
runtime executed by the lead.

## Independent verification

- Both official tool ZIPs independently hashed: Ghidra12.1.4
  `ddac49f903da9d5bac833e5cc79395098b9c33cfd3279be5f31bd00387d2d4db`;
  Temurin21.0.12.1+1
  `f9d6e191ab098c0d416e7d588a24420a8621cd2f4720dab2459b8b7b2d2d8b4e`.
- Project `.codex/config.toml` and `.mcp.json` provider environment entries match.
  Provider-specific doctor independently PASS: Ghidra12.1.4, full64-bit JDK21,
  native decompiler, local Windows authority and process/isolation checks.
- Reference Rust EXE independently hashed at required
  `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`,
  15,866,880bytes. Original-associated proxy DLL independently hashed at
  `266129c6c92f3ae89001493d61ccfdf4f5dac1fe66cff4fbb4799e6c01a6c902`.
- Reran actual `verify-native.mjs` on that DLL, procedure entry, fresh ignored
  output `artifacts/rea-toolchain/lead-review-native`. PASS assembly/pseudocode,
  available61-byte body, callers/callees/references, nativeCodeProven=true and
  successful close. Cold import ~123s, unlike mere tools/list readiness.
  No remaining Java/decompile processes appeared in the post-close query.
- Independently read the worker DLL result and original selective output. Five
  selected original EXE functions report decompile completion; four larger
  functions report process timeouts. The returned first Map-related pseudocode
  is a truncated prefix, detailed below. Types/payload semantics remain unresolved.
- Reran 24 offline body/model tests PASS; both new Node helper syntax checks PASS.
  No product changes requiring another release build or game attempt were made.

## LEADMAP013-01 — selective output silently truncates a relevant body

`Map013Selective.java` writes only `pseudo.substring(0, Math.min(10000,pseudo.length()))`
and has no original-length/truncation marker. In selected-small.txt, recovered
`0x140191253` ends inside `if (*ppppplVar11 == ... ) {` with its remaining code
absent. Four other inspected small-function outputs end with closing return/body.

Thus five successful decompilations are real, but five retained complete bodies
are not established. The retained Map-related prefix cannot support its unseen
tail, cancellation/error ordering or full controller-handoff interpretation.

Correction: preserve historical output; save complete pseudocode to ignored raw
files, record original/retained lengths, function body/source bounds, tool/input
identity and warnings. For deliberate excerpts provide exact offsets and explicit
truncation metadata. A decompileCompleted flag does not certify excerpt completeness.
Re-extract this specific body and follow its relevant call/value flow.

## LEADMAP013-02 — analysis timeouts are budget outcomes, not missing input

The selective helper hardcodes eight seconds per decompilation. The four large
functions failed under this chosen budget. Original full REA import reached
the default330000ms deadline. Installed REA supports an explicit positive
REA_GHIDRA_STARTUP_TIMEOUT_MS; default is not a format/access impossibility.

Correction: choose a justified bounded budget for relevant large functions, seed
verified function starts/bounds and inspect any decompiler warnings/type problems.
Use saved/targeted analysis to avoid repeating the entire import unnecessarily.
Preserve old timeout negatives; report whether larger budgets or better function
boundaries change outcomes. Do not claim native data is unrecoverable solely
because these short/default budgets expired. No unbounded runs are required.

## Scanner question disposition

The worker correctly states PARTIAL. Native tools now work; original smaller
code sites are genuinely accessible. No original protected scan algorithm,
decryption/key-input pipeline, current-client second-region request/reply/cache
causal link, camera-free full map, remote complete-map acquisition or faster
accuracy-qualified candidate was established. No new live experiment in MAP-013.
Existing per-region camera-assisted/shared mixed production scan remains unchanged.

Continue the already assigned MAP-013 task after the two helper/evidence fixes:
recover useful original/current native value flow, then target actual outbound
request -> matching response/segments -> cache insertion, using existing IL/Lua
decoders for custom formats. Do not stop after another installation/code dump.
All prior background-only/no-mouse, ownership/restoration and account-action
restrictions persist. No new branch/campaign or Home/UI reopening is needed.
