# Milestone A — semantic inspection toolchain

Starting checkpoint: `b48b3bdbacd94566019679026126f9a2e4ae8ba0`

## Current chunk format

The exact installed v22 package remains SHA-256
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`,
fileVersion 3, contentVersion 22, 18,741 entries.

Decoded Lua chunks use:

- signature `1b 4c 75 61`;
- version byte `0x53` (Lua 5.3);
- format byte `0x01`;
- standard `LUAC_DATA = 19 93 0d 0a 1a 0a`;
- serialized size bytes `04 04 08 08`.

The repository-local parser establishes that format 1 omits the ordinary Lua-5.3
`size_t` header byte while retaining the Lua-5.3 prototype, string, constant and
instruction layout. The new campaign tool
`tools/lwbridge317/inspect_map_provider_semantics.py` reuses that parser,
hash-gates the exact current package, adds register-aware root method mapping,
prototype paths, instruction PCs/source lines and scratch-only normalization.

## Independent format/execution proof

The pre-existing isolated environment
`C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe`
contains lupa 2.8 with `lupa.lua53`; its runtime reports `Lua 5.3`.

A fresh Lua-5.3 function was compiled with `string.dump`. Its standard header
contained `04 08 04 08 08`. The fixture was transformed to the observed game
format by changing format 0->1 and removing only that `size_t=8` byte. The compact
fixture parsed through the current package parser. Reversing the transformation
produced a byte-for-byte identical standard chunk. Executing the original and
round-tripped functions for four distinguishing inputs produced identical results.
Evidence: `a-lua53-format-fixture.json`.

This proves the transformation itself. It does not claim arbitrary game bodies can
execute without their required globals/upvalues; selected actual bodies still need
explicit isolated stubs in F.

## Parser breadth

The campaign parsed 373 Treasure/Ghost/Scout/Supplies candidate modules from the
exact v22 package with **zero parse errors**. A register-aware correction was
necessary: some root chunks batch multiple `CLOSURE` instructions before their
`SETTABLE` assignments. The older adjacent-pair helper can therefore misname child
functions. The new tool follows the closure value register into the named table
assignment and correctly maps, for example:

- `DetectEventClaimTreasureMessage.OnCreate` -> root/child[0], lines 10-22;
- `DetectEventClaimTreasureMessage.HandleMessage` -> root/child[1], lines 24-71;
- `PushDetectTreasureClaimMessage.OnCreate` -> root/child[0], lines 9-12;
- `PushDetectTreasureClaimMessage.HandleMessage` -> root/child[1], lines 14-28;
- all 12 `ActGhostreconTaskInfo` methods;
- all 10 `ActGhostreconTaskTemplate` methods.

Prototype/PC reports are in `a-semantic-core.json`, `a-target-disassembly.json`
and the broader candidate/cross-reference evidence.

## Exact artifact pins

Reference 0.3.17 EXE remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Source and tool hashes are recorded in `a-source-hashes.json`.

## Preserved negative attempts

- The first Lua-5.3 fixture run passed all binary-transform and execution assertions,
  then failed while JSON-serializing a returned byte string. The rerun converted
  only evidence presentation to text and preserved the same semantic assertions.
- An initial semantic-inspector invocation used PowerShell backticks inside the
  outer JavaScript tool-call string and failed before reaching the remote shell.
  It was rerun with a PowerShell argument array.
- A selected-body console dump later encountered a Windows cp1252 encoding error
  on a Unicode constant. The structured UTF-8 JSON report was already complete;
  subsequent console extraction used `PYTHONIOENCODING=utf-8`.

No installed artifact was rewritten and no live/game process was invoked.

## A disposition

A is COMPLETE. B–E may now rely on exact package hashes, prototype paths,
instruction PCs/source lines and compatible isolated Lua-5.3 body execution.
