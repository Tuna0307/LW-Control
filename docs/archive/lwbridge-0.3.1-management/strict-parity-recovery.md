# Strict one-to-one parity recovery directive

**Effective:** 2026-09-24
**Checkpoint:** `LWB-R8-122`
**Reference authority:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**Verified SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

This document supersedes every earlier project direction that allowed redesign, feature retirement, convenience behavior, performance-driven substitution, or owner-specific UI/product changes. Historical evidence remains valid at its recorded scope, but it is no longer product-design authority.

## End goal

The end product must reproduce LWBridge 0.3.1 one-for-one as a working program for every retained product feature, subject only to the explicit owner exception below. The reference executable decides what retained features are called, how they look, how they behave, what defaults they use, what errors they show, what requests they send, and what results they expose.

The implementation language, compatibility shims, internal process structure, or current-client adaptation may differ only when necessary to make the recovered original behavior work. Those internal differences must not intentionally change observable product behavior.


### Owner working/acceptance reset — 2026-09-27

For owner-facing status, **WORKING is binary and operational**. A feature is WORKING when the production program performs that feature successfully against the real current Last War client. It is no longer required that the final production path be the recovered original LWBridge 0.3.1 implementation.

**Any bypass, replacement, shim, patch, synthetic local state, custom loader/proxy, compatibility layer, fallback, equivalent implementation, or other technical route is allowed in research and in the final production program if it helps make the program work.** The owner explicitly does not care which internal route is used as long as the retained program functions correctly. Original LWBridge behavior/source remains valuable as an oracle and recovery target, not as a mandatory implementation constraint.

Environment-denied operations remain a separate tooling boundary: do not disguise the same denied operation through another tool, but pursue any genuinely different bypass or implementation route.

Immediate priority is **normal Home UI validation**. R8-120 live-proves the Login/admission bypass and normal Map WebView path; R8-121 live-proves all eight Map acquisition kinds in both Normal and Fast, so Map scanning/acquisition is WORKING. Do not reopen Login or Map acquisition unless a concrete regression appears.


## Owner direction — authentication dependencies may be recovered

The owner originally excluded the account/login/authentication feature family. On 2026-09-26 the owner explicitly superseded that restriction: authentication, authorization, entitlement, account/session and related state **may be researched, restored or implemented when doing so helps recover or make retained LWBridge functions work correctly against the live game**.

The priority remains retained product behavior, especially Home and Map. Synthetic credentials/state, hard-coded research admission, roles/capacity, or equivalent local bypass state may be used when they are the shortest path to a working retained feature. Prefer recovering original derivation when it is useful, but do not block implementation on it.

Standalone account-product work such as redesigning Login/Register/account management remains non-priority unless it is required by a retained feature or by the original state pipeline being reproduced. Historical reviews that called authorization owner-excluded remain valid descriptions of the scope at the time; new checkpoints may supersede those fences with stronger recovered evidence.

## Non-negotiable parity rules

1. Do not add a feature because it seems useful.
2. Do not remove a retained reference feature because it is inconvenient, slow, obsolete, gated, or difficult. Authentication/authorization/entitlement dependencies may be recovered when retained behavior requires them; standalone account-product work remains non-priority unless that retained-state pipeline requires it.
3. Do not redesign workflows, labels, tabs, defaults, timing, search semantics, scan semantics, navigation, storage behavior, or error handling without recovered reference evidence.
4. Different algorithms, bypasses, shims, fallbacks and equivalent implementations are acceptable production paths when they make the retained feature work correctly against the current client. Prefer recovered original behavior where it reduces uncertainty, but do not block delivery on internal one-to-one parity.
5. Do not treat current Last War APIs, LW Atlas, community tools, or our previous implementation as product authority. They are research aids only.
6. When exact original bytes are recovered, preserve them byte-for-byte. Do not hand-edit extracted frontend chunks, icons, locale bundles, embedded assets, or other recovered payloads.
7. When original bytes are not yet recovered, use the strongest available evidence, but a practical compatible implementation or bypass may be built when needed to make the retained feature work.
8. A previous rebuild feature that is not demonstrated in LWBridge 0.3.1 is a deviation and must be removed or quarantined.
9. A previous owner-requested retirement or customization is historical only unless the same behavior exists in the reference. The 2026-09-26 auth direction supersedes the earlier blanket exclusion only for dependency recovery required by retained behavior.
10. Passing tests is not enough by itself; the decisive acceptance proof is successful live operation against the current Last War client. The reference remains the behavioral oracle where relevant, not a mandatory internal implementation path.

## What "not even a single byte" means here

The immutable reference executable and every extracted original asset are hash-locked and must never be modified. Any recovered payload that can be reused directly should remain byte-identical.

The rebuilt executable itself is not required to have the same PE hash as the Rust/Tauri reference because the reconstruction may use a different compiler/runtime and must remain compatible with the current Last War client. The required result is exact reference product behavior and byte-identical reuse of original recoverable assets, with no intentional user-visible deviation.

## Parity evidence classes

- **EXACT_BYTES** — bytes extracted from the verified reference and reused unchanged.
- **EXACT_CONTRACT** — behavior/fields/constants/control flow recovered from the verified reference with durable locators.
- **EQUIVALENT_REIMPLEMENTATION** — implementation differs internally but is proven to reproduce the recovered original contract.
- **DEVIATION** — rebuild behavior added, removed, altered, optimized, or customized without reference authority.
- **UNKNOWN** — original behavior is not yet recovered enough to reproduce safely.

A release is not one-to-one for the retained product scope while any required retained reference feature remains `DEVIATION` or `UNKNOWN`.

## Current correction of direction

The R7 period produced useful evidence, but parts of the rebuild drifted into solving our own design. Examples include the wide-FOV Map scan optimization, the Secret Task Quick Find product addition, direct-list routing choices, removal of original product surfaces, and other rebuild policies that were not first established as original LWBridge behavior.

Those checkpoints remain evidence of experiments and current-client capabilities. They are not parity authority.

From R8 onward, research order is:

**reference LWBridge -> recover exact implementation/contract -> map to current Last War -> implement only what was recovered -> compare against reference -> live-prove functionality.**

## R8-118 current dependency correction

The original auth lifecycle now has an exact device-key mode contract: login uses mode `0` (open-or-create persisted `ECDH_P256` key + public export), the session supervisor uses mode `1` (open-existing + export), and cleanup uses mode `2` (delete/already-missing). The wrapper operation byte is proven to propagate unchanged into Tokio task `+0x40` and worker `0x318D61`. Therefore a missing local device key is not a reason to invent or synthesize one; authentic login is designed to provision it. The current unresolved package-decrypt material is `package-key.envelope`, which must not be fabricated or forced through unauthorized probing. R8-107 additionally proves the host does not retain a second envelope copy in service state or SessionV2 after auth ingestion; the response token is persisted to the runtime envelope file and local response strings are destroyed. Current permitted local storage/dump checks found no surviving copy. Do not bypass OS permission boundaries or probe remote auth merely to create one. R8-108 formalizes the exact assembled-source capture contract but does not provide a live reader: further live process-memory implementation was blocked by the environment, the rejection was not rerouted, and the incomplete reader was deleted. Do not describe the capture path as operational until an actually permitted implementation exists and succeeds. R8-109 additionally closes host envelope exposure: exact literal/xref ownership shows no separate command/IPC/log/debug/status surface that returns the envelope value. Do not repeat that speculative route without genuinely new evidence. R8-110 also closes compiled-bytecode caching as a better/later route: `LENC` bytes live in a wrapper-local vector, are passed synchronously to xLua, then freed before the assembled source is later zeroized. The allocation is not explicitly zeroized, so unowned allocator residue is not disproven, but no proxy-owned cache/file/global or later `lua_dump` surface exists. Keep the R8-104 source global as the preferred authentic artifact. R8-112 additionally proves that host `payload.fn` routing is not native: the proxy forwards the whole serialized message to fixed Lua global `XluaBridgeHandlePipeMessage`. Therefore do not use generic `call_lua(fn=__XluaBridgeLoad)` as a recovery shortcut unless the authentic Lua dispatcher/provider contract proves that route. R8-111 remains scratch-only because its verifier execution was blocked. R8-113 closes environment-based source injection: the complete native proxy environment surface is 13 constant `LWBRIDGE_*` keys, with no source/eval/script/chunk override. `LWBRIDGE_HOST_DIAGNOSTIC=1` only runs the fixed embedded diagnostic and `PROFILE_RUNTIME_ROOT` owns ordinary runtime/log paths. Do not invent a source-loader environment switch. R8-114 additionally closes native file-based source injection: `0x1BD80` is only envelope/multi-proof text, and `0x1B910` is package-only `bridge-scripts.dat` loading. Do not invent an alternate source/eval file path; the authentic package/source path remains mandatory. R8-115 now closes the legitimate host trigger ordering: fresh login can provision the device key and populate ticket/envelope through the shared artifact-ingest path; successful renew/heartbeat responses can refresh the same artifacts; a supervisor with no usable SessionV2 deletes them and signs out. Therefore a challenge/build-manifest-only game launch is not a useful source-recovery test. Wait for a legitimate authorized original-host lifecycle to create the runtime artifacts, then return directly to the R8-104 source window. R8-116 proves the reference frontend's normal owner login is exactly such a lifecycle trigger: `auth_login({username,password})` is followed, on `authorized`/`grace`, by best-effort `multi_entitlement_get` and `profile_instances_reconcile({autoLaunchAll:<setting>})`; auto-launch defaults true unless localStorage contains exact `lwbridge.autoLaunchGame=false`. A filename-only current-machine check found no original session/credential/ticket/envelope files, so do not fabricate restore state. R8-117 closes the saved-credential branch: `auth_credentials` restores only the constructor-owned v2 credential file (`+0x120/+0x128`, exact version 2, secure decode) or the legacy credential file (`+0xE0/+0xE8`, exact `version`/`username`/`encryptedPassword`, then migration to v2). Save DPAPI-protects the password and persists only v2. The accessible user profile contains no copy of any original credential/session/ticket/envelope persistence filename. R8-118 supersedes the earlier no-bypass research wording: invented **local research bypasses are allowed and encouraged** when they help expose/recover original LWBridge logic. They remain scaffolding only and do not satisfy WORKING acceptance. Exact secure/plain comparison proves plain proxy is not a package-key bypass; both proxies require the same envelope/package/decrypt/parser chain, contain `LWAT2` but no `LWAT1`, and use a hardcoded P-256 verification public key with no shipped private ECC blob. Direct EXE patch and synthetic SessionV2 writes were environment-blocked and were not rerouted. Continue with genuinely distinct loader/proxy substitution or controlled post-admission instrumentation; do not confuse a research bypass with a production fallback.

## Protected bridge scripts remain a retained recovery target

`bridge-scripts.dat` is part of the original implementation and remains important to exact parity. Under the R8-097 owner reset, still in force through R8-118, immediate work stays on the original Map acquisition engine and then Home lifecycle. General package/auth recovery resumes when it directly answers a blocker for those surfaces or after they meet owner acceptance.

The goal is to recover the original script/package contents through permitted analysis methods, preserve the recovered bytes, identify every handler and contract they contain, and use those findings to replace guesses in the rebuild.

Historical environment denials such as SB-79 remain historical restriction records. Do not reroute an operation that an environment forbids. The underlying recovery question is still active and should be pursued through genuinely permitted methods, tools, artifacts, metadata, runtime observations, or other distinct analysis paths.

## Historical evidence policy

Do not rewrite or delete old reviews/evidence to make history look cleaner. Add superseding notes and current parity classifications. Historical R1-R7 acceptance matrices now prove only the reconstructed behavior they actually tested; they do not establish one-to-one completion unless separately tied back to the original reference.

## Delivery rule

Every future checkpoint must state:

- exact reference feature/function being recovered;
- original source locator and artifact identity;
- recovered contract or bytes;
- current implementation mapping;
- parity classification before and after;
- tests against the reference or extracted reference assets;
- current-client live proof where the feature requires it;
- remaining parity gaps.

Performance work is allowed only after parity is established, and only if it does not change observable behavior.
