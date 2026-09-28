# R8-130 — post-manifest controlled package reaches original decrypt/parser live

**Date:** 2026-09-28
**Reference:** verified LWBridge 0.3.1 secure proxy
**Status:** LIVE-PROVEN controlled-package decrypt/parser reachability

## Purpose

R8-128 proved a synthetic LWKE1 envelope can produce a package key and reach the original package function, but a controlled package on disk failed earlier with `build manifest mismatch`.

R8-130 isolates the signed build-manifest gate from the later package decryptor without changing the signed manifest or replacing `bridge-scripts.dat` on disk.

## Source-locked dataflow

The package reader `0x1CAFD -> 0x1B910` moves authentic `bridge-scripts.dat` bytes into caller local `rbp-0x20`.

The build-manifest validator is `0x1CC14 -> 0x40A70`. Its primary inputs are:
- build.manifest path;
- current loaded proxy DLL path from `0x1F570`;
- proxy-bundle path;
- envelope-produced identity strings.

`0x1F570` is source-locked to `GetModuleHandleExW(flags=6)` plus `GetModuleFileNameW`; it is not a package-data alias.

The validator has no direct reference to the package byte vector. It validates signed manifest and proxy/bundle identity including `compositeSha256`, `proxySha256`, and `exportFingerprint`.

The envelope consumer `0x1C8E2 -> 0x3F8E0` populates one shared context beginning at caller `rbp+0x80` plus the 32-byte package-key vector at `rsp+0x48`.

The shared context fields used later are:
- `context+0x20` / caller `rbp+0xA0`: expected package build ID;
- `context+0x40` / caller `rbp+0xC0`: expected package SHA-256.

Those same two strings feed the signed manifest comparison.

At `0x1CC3A -> 0x3D260`, arguments are:
- arg1: package bytes at caller `rbp-0x20`;
- arg2: shared envelope context at caller `rbp+0x80`;
- arg3: exact 32-byte package key at `rsp+0x48`;
- arg4: plaintext output at `rbp-0x60`;
- arg5: error/output string at `rbp+0x40`.

Inside `0x3D260`, package SHA-256 is compared with `arg2+0x40`, embedded build ID with `arg2+0x20`, and AES-GCM uses arg3.

## Controlled package

A fresh 99-byte LWBP2 package contained one format-2 module named `bootstrap` with source `return true\n`.
The successful live run used controlled SHA-256:
`482bde0c9859e375d79ca221c744e6d4c44bf1485f99887b99642aa40ea75207`.

The synthetic LWKE1 envelope deliberately claimed the authentic on-disk package SHA:
`a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d`.

The authentic runtime `bridge-scripts.dat` remained unchanged on disk and was reverified before the live run at the authentic SHA above.

After manifest success, a RAM-only trampoline at secure-proxy `0x1CC1D`:
1. replaced the already-read package buffer with the 99 controlled bytes;
2. changed that std::string logical length to 99;
3. replaced the 64-byte `context+0x40` package SHA with the controlled SHA;
4. replayed the overwritten original instructions and returned at `0x1CC2A`.

The context build ID was unchanged. The controlled 32-byte AES key was already produced by the synthetic envelope.

The two previously established RAM-only fixed-signature research patches were also applied:
- authorization ticket: RVA `0x2066E`;
- package envelope: RVA `0x3FBC1`.

No original executable, proxy DLL, signed manifest, or runtime package file was patched on disk.

## Live result

At 2026-09-28 05:33:38 the real secure proxy logged:
- `loaded script package format=2 revision=1`;
- `authorization valid`;
- then `game main chunk loaded`.

This run emitted none of:
- `build manifest mismatch`;
- `package integrity invalid`;
- `package build mismatch`;
- `package decrypt failed`.

Therefore controlled bytes + controlled AES key + post-validation context SHA passed the original package validation/decrypt function and the format-2 module-table/source-assignment path live.

## Compiler classification

R8-098/R8-104 already source-lock the successful package branch as assigning the proxy-owned assembled Lua source that bootstrap later reads and passes to `0x16C10` as `@bridge-scripts.dat`.

This R8-130 run directly proves controlled decrypt/parser/source-assignment reachability. It does **not** separately instrument `0x15A3E`, so controlled-source compiler execution remains **SOURCE-LOCKED**, not independently LIVE-PROVEN in this checkpoint.

## First-attempt correction

The first R8-130 live attempt applied the two signature patches but failed before installing the new hook because Python ctypes rejected a `bytearray` passed to `WriteProcessMemory`.

Classification: **TECHNICAL FAILURE**.

The only correction was converting that local trampoline buffer to immutable `bytes`. The clean second process then armed all three patches and produced the successful log above.

## Current boundary

- post-manifest controlled package swap: **LIVE-PROVEN**
- original package integrity/build validation with controlled bytes: **LIVE-PROVEN**
- original AES-GCM package decrypt with controlled key: **LIVE-PROVEN**
- original format-2 package parser/source assignment: **LIVE-PROVEN**
- bootstrap source compiler relation: **SOURCE-LOCKED**
- authentic original bridge Lua plaintext: **NOT RECOVERED**
- original Map engine: **NOT WORKING**

The next direct source-recovery target is now the R8-104 assembled-source window, because a successful package-processing state is operationally reachable without modifying the signed manifest or authentic package on disk.
