# R8-118 — classify original proxy/package bypass surfaces

**Date:** 2026-09-27
**Reference:** exact extracted LWBridge 0.3.1 secure/plain proxies
**Status:** RECOVERED NEGATIVE / BYPASS CLASSIFICATION
**Scope:** secure-vs-plain package/auth control-flow parity, proxy ticket format, hardcoded LWAT2 verification key, and current-client plaintext bridge-symbol check. This checkpoint does not inspect the preserved `0x3F8E0-0x40A6D` consumer body.

## Owner bypass-policy correction

The owner explicitly clarified that reverse engineering **may invent local bypasses** when they help expose or recover original LWBridge behavior.

Therefore earlier research wording that categorically prohibited fabricating/bypassing local authorization is superseded.

Current rule:

- research bypasses are allowed: local admission patches, synthetic local state, loader/proxy substitution, hooks/shims, forced branches and equivalent investigative scaffolding may be explored when technically permitted;
- a bypass is evidence/research scaffolding only;
- Map/Home become **WORKING** only when the recovered original LWBridge logic itself is the production path and succeeds live against the current game;
- a custom fallback/equivalent implementation still does not satisfy acceptance.

Environment/tool safety boundaries remain separate. When an operation is blocked, do not disguise the same denied operation through another tool; pursue a genuinely different bypass.

## Secure vs plain proxy result

Exact identities:

- secure SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`;
- plain SHA-256 `c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794`.

Both proxies execute the same outer package-loader control-flow shape across `0x1C0A0-0x1D46F`:

- 105 direct calls, identical RVA/target sequence;
- 144 direct branches, identical RVA/mnemonic/target sequence.

Critical calls are identical in both:

- `0x1C447 -> 0x1B300` package-key-envelope path builder;
- `0x1C45B -> 0x1BD80` bounded envelope text reader;
- `0x1C8E2 -> 0x3F8E0` envelope consumer boundary;
- `0x1CAFD -> 0x1B910` `bridge-scripts.dat` reader;
- `0x1CC14 -> 0x40A70` build-manifest validator;
- `0x1CC3A -> 0x3D260` LWBP2 package decrypt;
- `0x1CC54 -> 0x125C0` module-table/source assembly;
- `0x15A3E -> 0x16C10` source compilation wrapper;
- `0x1618A -> 0x3F350` source cleanup helper.

The assembled-source globals remain the known variant-specific addresses:

- secure `0x8F170`;
- plain `0x90170`.

Conclusion: **`xlua-proxy-plain.dll` is not a package-auth or package-encryption bypass.** Its package/auth pipeline is semantically the same recovered path as secure.

## Authorization ticket result

Both proxy files contain `LWAT2`; neither contains `LWAT1`.

Therefore the host-side outer acceptance of `LWAT1`/`LWAT2` does not create a proxy-loader bypass. The actual package loader is tied to the recovered LWAT2 validation path.

The LWAT2 signature helper constructs a P-256 `ECS1` public key blob from a hardcoded 64-byte X/Y point:

`00b0c33896bc67481de5fdc412d9b1549e70d736d9a5f7c1a3dbfeeccd8e135f7ae6887c7189787df7edaf33f52f3cac0849f1382b24e8d7aa6116152b425659`

The helper requires a 64-byte signature and returns success only when `BCryptVerifySignature` returns non-negative status. No `ECS2` or `ECK2` private ECC blob magic exists in the host or either proxy.

Thus there is no obvious shipped signing-private-key or verifier-fail-open bypass.

## Current Last War symbol check

A bounded read-only scan of the installed current-client likely script/code/container files checked 373 files / 764,144,812 bytes for:

- `XluaBridgeMapScanTick`;
- `XluaBridgeHandlePipeMessage`;
- `__XluaBridgeLoad`;
- `__XluaBridgeEvalHook`;
- `mapScanTick`.

Result: **0 hits**.

This is current-client observational evidence, not an immutable binary contract. It supports the conclusion that the original bridge package owns these bridge Lua names rather than merely wrapping same-named game-shipped Lua functions.

## Other bypasses tested this block

- direct reference-EXE admission patch: environment blocked the operation; not rerouted;
- synthetic SessionV2 write: environment blocked the operation; not rerouted;
- OS pagefile/hiberfile/swapfile residual source route: not visible to current account; no permission bypass attempted;
- original/rebuild crash dumps: no assembled-source prefix, Map symbol, package filename, LWBP marker, or envelope filename;
- public exact-key search: no exact match for the hardcoded LWAT2 public point in ordinary web/GitHub search results.

## Consequence

The following bypasses are now closed or unavailable:

1. use plain proxy to avoid the envelope/key path — **closed**;
2. use LWAT1 as a legacy proxy ticket — **closed**;
3. use a shipped ECDSA private key to mint LWAT2 — **closed by current static evidence**;
4. recover same-named bridge Lua directly from current Last War assets — **negative current-client observation**;
5. recover decrypted source from accessible memory-backing/dump artifacts — **no accessible source found**.

The hard technical dependency remains the 32-byte package key or an equivalent bypass that reaches the authentic post-decrypt source without requiring that key.

The next promising bypass family is **loader/proxy substitution or controlled post-admission instrumentation**, provided the implementation is genuinely distinct from operations already blocked by the environment.

## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_proxy_package_parity.py`

Evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-118-proxy-package-parity.json`

## Status

**MAP: NOT WORKING. HOME: NOT WORKING. WHOLE LWBRIDGE: NOT READY.**
