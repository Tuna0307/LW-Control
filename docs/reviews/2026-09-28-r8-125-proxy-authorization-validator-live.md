# R8-125 — original secure-proxy authorization validator live boundary

**Date:** 2026-09-28
**Status:** LIVE-PROVEN VALIDATOR STAGES / PROCESS-MEMORY SIGNATURE HOOK

## Goal

Continue from R8-124's exact original proxy failure:

`authorization missing -> BRIDGE_START_TIMEOUT`

without returning to the tool-layer-blocked synthetic package-key-envelope responder route.

The target in this block was the original secure xLua proxy's own `LWAT2` authorization validator.

## Baseline

The untouched original host remained open on CDP port 9229.

Native `auth_state` still reported `signedOut`.

The R8-124 process-memory-only host admission hook remained active.

The normal runtime directory initially contained only:

- `authorization.challenge`;
- `build.manifest`.

No `authorization.ticket` or `package-key.envelope` existed.

## Secure proxy extraction identity

The verified secure proxy embedded in `lwbridge-0.3.1.exe` was re-extracted read-only:

- host raw offset: `0x987374`;
- size: `612352`;
- SHA-256: `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`;
- image base: `0x180000000`.

The exact `authorization missing` string is proxy RVA `0x714C0`.

Its only validator reference is RVA `0x202B5`.

## Exact meaning of authorization missing

Proxy validator RVA `0x201C0-0x212B9` calls dedicated ticket reader `0x1BB20` at `0x202A6`.

The sequence is exact:

- `0x202A6 -> 0x1BB20`;
- `test al, al` at `0x202AB`;
- false path loads `authorization missing` at `0x202B5`.

The dedicated reader was disassembled read-only.

It:

- opens the fixed `authorization.ticket` path;
- accepts file length only in the range 1..4096 bytes;
- reads the full file;
- trims trailing CR/LF bytes;
- returns false if the resulting text is empty;
- returns false on open/read/stream failure.

Therefore `authorization missing` is a **ticket-file read/nonempty failure**, before token framing, Base64, signature, claims, clock or envelope processing.

## Live validator ladder

All probe ticket files were temporary and removed after each observation.

### 1. Missing file

With no `authorization.ticket`:

`authorization missing`

This was already observed in R8-124 and reconfirmed by the source-locked reader branch above.

### 2. Readable but malformed ticket

Temporary file:

`x`

Original proxy result:

`authorization format invalid`

Therefore a readable nonempty ticket passes the `authorization missing` gate.

### 3. Correct one-dot framing but invalid Base64url

Temporary file:

`!.!`

Original proxy result:

`authorization encoding invalid`

Therefore framing was admitted and the decoder boundary was reached.

### 4. Canonical Base64url segments but invalid signature

Temporary file:

`eA.eA`

Both segments decode canonically.

Original proxy result:

`authorization signature invalid`

This live-proves the signature stage after the read/framing/encoding stages.

## Static validator error order

The secure proxy contains these exact validator error references:

- `authorization missing` -> RVA `0x202B5`;
- `authorization encoding invalid` -> `0x204BA`;
- `authorization signature invalid` -> `0x2067A`;
- `authorization clock invalid` -> `0x20AEE`;
- `authorization expired` -> `0x20CB5`;
- `authorization lifetime invalid` -> `0x20E29`;
- `authorization launch mismatch` -> `0x20EDA`;
- `authorization device mismatch` -> `0x20F5D`;
- `authorization payload invalid` -> `0x21093`;
- `authorization format invalid` -> `0x2126E`.

The earlier R8 source recovery remains consistent with the seven-field decoded first segment:

`LWAT2|issuedAt|expiry|claim3|claim4|deviceBinding64Hex|launchNonce43`

with field3/field4 nonzero, field5 64 lowercase hex, field6 43 URL-safe Base64 characters, and lifetime <= 1200 seconds.

## Signature-gate process-memory hook

The signature verifier is called at proxy RVA `0x20667`.

The following branch is:

- RVA `0x2066C`: `test al, al`;
- RVA `0x2066E`: conditional jump to the normal parser on signature success;
- success target: RVA `0x2072E`.

Original six bytes at `0x2066E`:

`0F 85 BA 00 00 00`

For research observation only, the loaded proxy process memory was changed to an unconditional jump:

`E9 BB 00 00 00 90`

No DLL or EXE on disk was modified.

### Host-embedded-copy attempt

The same branch was first patched in the secure-proxy byte image inside the running original host.

That patch did **not** propagate into the subsequently injected proxy; the launched proxy still reported `authorization signature invalid`.

The host-memory embedded-copy patch was restored immediately.

This proves the launcher does not consume that already-mapped host byte copy in the tested path.

### Actual loaded proxy module

The running Last War process exposed the genuine module:

`C:\Users\chimw\AppData\Local\LastWar-xLua-Bridge\runtime\9BupJXpEgm34lybhNhbbcQ\xlua-proxy-secure.dll`

Observed load base for the tested process:

`0x7FFFD4B40000`

The live bytes at module base + RVA `0x2066E` matched the immutable proxy.

The six-byte process-memory hook was applied there successfully.

## Live proof that signature hook works

While the same game/proxy process remained running, `authorization.ticket` still contained `eA.eA`.

Before the hook:

`authorization signature invalid`

After the hook, on the proxy's next authorization retry:

`authorization payload invalid`

No game relaunch was required.

Therefore the actual loaded proxy signature result was bypassed in process memory and the validator advanced into decoded payload validation.

This is LIVE-PROVEN.

## Structured payload probe

The live challenge file currently contains a 43-character URL-safe launch nonce.

A temporary structurally correct seven-field `LWAT2` payload was then prepared with:

- current issued-at;
- expiry +600 seconds;
- claim3 = 1;
- claim4 = 1;
- intentionally wrong all-zero 64-hex device binding;
- exact current local launch nonce;
- signature segment left invalid because the process-memory signature hook was active.

The proxy did not perform another retry before the 20-minute block checkpoint, so **no result is claimed for this structured probe**.

The temporary ticket was deleted before checkpoint.

## Cleanup / current machine state

At checkpoint:

- no `LastWar.exe` process remains;
- no `authorization.ticket` remains;
- no `package-key.envelope` exists;
- runtime directory is back to `authorization.challenge` + `build.manifest`;
- the per-game loaded-proxy signature patch disappeared with the game process;
- the original `lwbridge-0.3.1.exe` remains open;
- the R8-124 host admission hook remains active in process memory;
- original files on disk remain unchanged.

## Current exact boundary

The original secure proxy has now been live-driven through:

`ticket absent -> authorization missing`

`readable malformed -> authorization format invalid`

`framed non-Base64 -> authorization encoding invalid`

`Base64 token -> authorization signature invalid`

`live signature hook -> authorization payload invalid`

The next high-value step is to relaunch once, reapply the loaded-proxy signature hook, install the structurally correct seven-field ticket using the real challenge nonce, and observe the device-binding/clock result.

No claim is made that authorization is valid, that the envelope is accepted, or that the bridge is connected.
