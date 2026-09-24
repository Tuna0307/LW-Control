# LWB-R8-006 — recover opaque envelope-consumer package-key output flow

**Date:** 2026-09-24
**Scope:** permitted caller-side dataflow from the original `package-key.envelope` consumer into the exact 32-byte AES key used for `bridge-scripts.dat`.

## Result

R8-006 closes a major ownership gap without inspecting the historically restricted SB-79 consumer body.

The verified secure-proxy enclosing loader `RVA 0x1C0A0-0x1D46F` constructs the original `package-key.envelope` path and calls the opaque consumer `RVA 0x3F8E0-0x40A6D` at `RVA 0x1C8E2`.

Immediately before that call, the enclosing loader explicitly zero-initializes the vector at `rsp+0x48`.

The caller then passes:

- RCX / arg1: the `package-key.envelope` path;
- RDX / arg2: caller context at `rbp-0x78`;
- R8 / arg3: context object at `rbp+0x80`;
- R9 / arg4: the zero-initialized vector at `rsp+0x48`;
- stack arg5: error/output string at `rbp+0x40`;
- a later stack argument is exact integer `1`.

The call returns a boolean in AL, tested immediately at `0x1C8E7`.

## Exact output ownership

On the successful route, package function `RVA 0x3D260` is called at `RVA 0x1CC3A`.

At that call:

- package arg2 / RDX is the **same** `rbp+0x80` object used as opaque-consumer arg3;
- package arg3 / R8 is the **same** `rsp+0x48` vector used as opaque-consumer arg4;
- package arg5 is the **same** `rbp+0x40` error/output string.

R8-003 already proves that package arg3 is saved from R8, then supplied as RCX/key to the recovered AES-GCM helper at package callsite `0x3DA4A`.

That AES helper requires the key vector length to be exactly 32 bytes.

Therefore the opaque `package-key.envelope` consumer is now source-attributed as the **producer boundary for the exact 32-byte package AES key** used to decrypt the original protected script package.

This is stronger than the earlier statement that the envelope “somehow” leads to the package key: the precise output vector identity is now recovered across both calls.

## Shared context

The same `rbp+0x80` pointer flows from opaque-consumer arg3 directly into package-function arg2.

That proves a shared context object spans envelope validation and package validation/decryption. Its internal fields are not inferred here.

## Restriction boundary

The verifier does **not** disassemble, read instructions from, invoke, emulate, patch, or reroute secure-proxy RVA `0x3F8E0-0x40A6D`.

Only the permitted enclosing loader and the already recovered package/AES helpers are decoded.

## Source identity

Reference EXE:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Secure proxy SHA-256:

`481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`

## Validation

- `python tools\inspect_lwbridge_proxy_envelope_key_flow.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-24-r8-006-envelope-key-output-flow.json` — PASS.
- `python -m py_compile tools\inspect_lwbridge_proxy_envelope_key_flow.py` — PASS.
- evidence JSON parses successfully.
- no authentication, network request, private-key access/export, or production behavior was performed.

## Remaining P0 seam

R8-003 tells us exactly how the encrypted package consumes its key. R8-004 recovers the outer `LWKE1` transport grammar. R8-005 recovers the client P-256 public-key and launch-nonce encoding. R8-006 now proves exactly which envelope-consumer output becomes the 32-byte package key.

The remaining seam is therefore:

`original LWKE1 decoded server fields -> peer P-256 public point / encrypted key material -> opaque consumer output vector -> known 32-byte package key -> known LWBP2 AES-GCM decrypt`

The next useful evidence must identify the original server-returned `LWKE1` field semantics through an independent permitted source, such as a surviving original envelope artifact or another non-restricted original contract. Do not infer field positions from the restricted body.
