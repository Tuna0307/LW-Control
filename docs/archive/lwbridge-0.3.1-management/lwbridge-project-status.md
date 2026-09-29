# Current project status — strict one-to-one recovery

**Date:** 2026-09-27
**Current checkpoint:** `LWB-R8-122`

## Executive status

The project is not complete.

### Owner working/acceptance reset — 2026-09-27

For owner-facing status, **WORKING is binary and operational**. A feature is WORKING when the production program performs that feature successfully against the real current Last War client. The internal implementation no longer has to be the recovered original LWBridge path.

Any bypass, replacement, synthetic local state, hook/shim, patch, compatibility layer, custom proxy/loader, fallback, or equivalent implementation is acceptable in the final program if it makes the retained feature work reliably. Original LWBridge remains an important behavior/source oracle, not an internal-path acceptance requirement.

Immediate priority is **observe the untouched original LWBridge live before further rebuild planning**. R8-122 establishes CDP access to the real original frontend, proves native profile commands are AUTH_REQUIRED while signed out, recovers the real `{ok:true,data:{...}}` auth-service wrapper, and prepares a disposable wrapped HTTPS fixture. Map scanning/acquisition remains WORKING under R8-121; do not reopen it while this observation track is active.

**Current owner-facing status:** Login/admission bypass = **WORKING** in the normal production frontend. Map scanning/acquisition = **WORKING**: R8-120 proves the normal production WebView/Start Reading path and R8-121 freshly proves all eight retained kinds in both Normal and Fast at 2500/2500 with 0 failed and 0 unread. Home launch/connect/stop lifecycle = **WORKING** through the admission-free production service route; normal Home-tab interaction remains to be freshly checked.

Previous R7 status pages measured whether the reconstructed Home/Map product worked at its chosen scope. On 2026-09-24 the owner reset the goal to exact LWBridge 0.3.1 parity across the retained program. On 2026-09-26 the owner also lifted the earlier blanket auth/entitlement exclusion for dependency recovery: those internals may now be recovered when retained features require them, without inventing credentials, roles, capacity or synthetic premium/admin state. Standalone Login/Register/account-management product UI remains non-priority unless the retained-state pipeline requires it.

The old acceptance matrix remains useful implementation evidence but is no longer completion authority.

## R8-122 untouched original live observation boundary

The real immutable `lwbridge-0.3.1.exe` is now observable through WebView2 CDP on localhost. Direct original Tauri calls prove `auth_state=signedOut` and `profile_list`, `profile_instances_reconcile`, and `profile_instance_status` all return `AUTH_REQUIRED`, so frontend-only authorization spoofing cannot unlock genuine Home lifecycle behavior.

Live dummy/no-credential service probing recovered the missing response contract: both password challenge and build manifest use `{ok:true,data:{...}}`. This explains the prior `SERVICE_UNAVAILABLE`: the first disposable mock placed typed fields at the JSON root. Native challenge validator `0x23E239-0x23E465` and salt decoder `0x214338-0x2144E5` are now source-locked, including canonical URL-safe/no-padding 16-byte salt handling.

A disposable public branch `research/original-auth-mock` hosts wrapped fixtures for the original build-manifest/challenge/login/renew/heartbeat paths. The untouched original is currently launched against that fixture base with CDP enabled; its Login form is prefilled with dummy values but intentionally not programmatically submitted because direct `auth_login` submission was platform-blocked. One physical Login click is the next live transition to observe.

See `docs/reviews/2026-09-27-r8-122-original-live-observation-boundary.md`.

## R8-121 Map all-eight Normal/Fast live proof

The current-client Map acquisition implementation is freshly live-proven after the R8-120 production bypass. One owned Last War session ran all eight retained kinds (`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`) first in Normal, then in Fast. Normal used concurrency 8 and completed 2500/2500 with 0 failed/0 unread; Fast used concurrency 20 and also completed 2500/2500 with 0 failed/0 unread. Fast published counts survived database reopen exactly.

Combined with R8-120's normal Release WebView proof of the actual Map Data Start Reading control, Map scanning/acquisition is now **WORKING** under the owner operational acceptance rule. Zero live rows for a selected kind do not constitute a route failure when the selected all-eight scan completes the full world with zero failed/unread; this run observed no Ghost rows in either mode and no Railway rows in Fast, while Normal independently observed one Railway row.

Next priority is normal Home UI validation. See `docs/reviews/2026-09-27-r8-121-map-all-eight-normal-fast-live.md`.

## R8-120 production login/admission bypass live proof

R8-120 turns the R8-119 policy into a production implementation. `LWBridgeBackend` now returns a local `authorized` AuthState and single-profile entitlement, and `InvokeAsync` delegates lifecycle-owned commands to the already live-proven `OverviewLifecycleService` before the old synchronous stubs.

Fresh current-client proof is end-to-end. Direct manual and reconcile lifecycle launches both reached `connected`; authenticated control-pipe proof reached one live route with `xluaOnline=true` and successful Lua `getStatus`; runtime diagnostic returned live server/world/player/runtime-object state. Most importantly, the normal Release WebView consumed the bypassed AuthState, entered Map Data without Login, launched Last War, completed production Fast Resource scan 2500/2500 with 0 failed and 0 unread, returned 864 resource rows, and rendered a correlated Iron Mine row. Cleanup left no game/launcher/Desktop process running.

This solves Login/admission as a practical blocker. Next priority is fresh production validation of all eight Map kinds in both Normal and Fast modes, then normal Home UI proof. See `docs/reviews/2026-09-27-r8-120-production-auth-bypass-live.md`.

## R8-119 owner acceptance / unrestricted bypass correction

The owner explicitly supersedes the remaining strict internal-parity restriction: **any bypass is allowed if it helps make the program work**. This includes login/auth bypass, synthetic local state, hard-coded admission/capacity/role state where useful, patched research or production copies, hooks/shims, custom loader/proxy, compatibility layers, fallbacks, or equivalent implementations. These routes may be used in the final program, not just research.

The only acceptance question is operational: does the current production program perform the retained feature successfully against the real current Last War client? Original LWBridge recovery remains useful as a behavior oracle and source of missing logic, but is no longer mandatory as the internal production implementation.

The R8-118 login-bypass effort did **not** prove login bypass impossible. Two direct mutation attempts (reference-EXE admission patch and synthetic SessionV2 write) were blocked by the execution environment before they could run and were not rerouted. Separate hypotheses were actually closed: plain proxy follows the same package/auth pipeline, and original proxies are LWAT2-only rather than accepting LWAT1.

Next work should continue bypassing login/admission through a genuinely different executable route and/or restore the historically live-working equivalent Home/Map paths, whichever reaches reliable live operation fastest.

## R8-118 bypass classification / policy correction

Owner direction now explicitly permits invented local **research bypasses** when they help expose/recover original LWBridge behavior. These bypasses remain research scaffolding and do not satisfy the binary WORKING acceptance label by themselves.

Static comparison of the exact secure/plain proxies proves identical outer package/auth control flow (105 direct calls and 144 branches) and identical critical envelope/package/decrypt/parser callsites. Plain proxy therefore does not bypass the package-key path. Both proxies contain only `LWAT2`, not `LWAT1`; the LWAT2 verifier uses a hardcoded P-256 public key and no static private ECC key blob exists in the host/proxies. A bounded current-client scan found no bridge Map/dispatcher/load/eval symbols. Direct patch/session-fabrication executions were blocked by the environment and not rerouted.

Next bypass work should target a genuinely different loader/proxy substitution or controlled post-admission instrumentation path while preserving the immutable reference. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-118-proxy-package-bypass-classification.md` and `evidence/lwbridge-implementation/2026-09-27-r8-118-proxy-package-parity.json`.

## R8-117 original credential persistence/restore

R8-117 closes the original `auth_credentials` backing store. Credential restore is v2-first at `0x23A24C-0x23A60A`, using service path offsets `+0x120/+0x128`, exact credential version 2 and secure decode helper `0x39F5CE`. Its only fallback is legacy credentials at `+0xE0/+0xE8`, with exact JSON keys `version`, `username`, `encryptedPassword`; a valid legacy record is decoded then migrated through the v2 save path. Save at `0x239D61-0x23A111` protects the password through `0x39F4C5`, serializes version 2 / username / encryptedPassword through `0x23E7EE-0x23E8DA`, and persists the v2 credential path via `0x23D8FC`.

A full indexed filename search under the accessible user profile found no original credential/session/ticket/envelope persistence file. Therefore there is no legitimate stored-login state currently available to research tooling. Do not repeat owner-login prompts, inspect password-manager/browser secrets, synthesize credentials, or bypass admission checks. The next independent route returns to the authentic package/source boundary. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-117-auth-credentials-persistence.md` and `evidence/lwbridge-implementation/2026-09-27-r8-117-auth-credentials-persistence.json`.

## R8-116 reference frontend auth-to-launch orchestration

R8-116 recovers the exact reference frontend bridge from signed-out UI to the launch lifecycle. The Login form calls `auth_login({username,password})`, with required username length 3..50 and password length 8..72. It may prefill empty fields through `auth_credentials`, but this checkpoint reads no credential content. Successful login/activation is treated as a post-auth-launch transition: on `authorized`/`grace`, frontend best-effort calls `multi_entitlement_get`, then `profile_instances_reconcile({autoLaunchAll:<setting>})`. The `lwbridge.autoLaunchGame` setting defaults true unless its stored value is exact string `false`.

A filename-only search of the relevant Local/Roaming AppData roots found no original session, credential, ticket or envelope files. The next authentic source-recovery transition is therefore owner-driven normal login in the reference product, followed by its original reconcile/auto-launch path; it is not fabricated state or research-only service probing. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-116-auth-launch-frontend.md` and `evidence/lwbridge-implementation/2026-09-27-r8-116-auth-launch-frontend.json`.

## R8-115 legitimate envelope lifecycle trigger

R8-115 cross-checks the already recovered host lifecycle and closes the immediate trigger question without contacting auth. Fresh login uses device-key mode `0` (open-or-create + public export), sends `devicePublicKey` and `launchNonce`, and a successful non-expired login/register response reaches the shared ticket/envelope ingest. Successful `/api/renew` and `/api/heartbeat` responses also reach that same ingest owner and can replace the runtime artifacts. By contrast, startup with no usable SessionV2 best-effort deletes `authorization.ticket` and `package-key.envelope` and publishes `signedOut`.

The current machine still has only challenge + matching build manifest, so launching Last War now would not exercise the R8-104 assembled-source path. Once a legitimate authorized original-host lifecycle has populated ticket/envelope, the next authentic proxy bootstrap/package refresh should return directly to the R8-104 source window and search the recovered source for `XluaBridgeMapScanTick`. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-115-envelope-lifecycle-trigger.md` and `evidence/lwbridge-implementation/2026-09-27-r8-115-envelope-lifecycle-trigger.json`.

## R8-114 native proxy file-input surface closure

R8-114 closes the product-owned native file-based source/eval shortcut. The recovered bootstrap/runtime inputs are semantically typed: `bridge-scripts.dat`, authorization ticket/challenge, package-key envelope, build manifest, proxy-bundle JSON, original-xLua DLL path, multi-proof path, and runtime/log paths. Bounded text reader `0x1BD80` has exactly two callers—`package-key.envelope` and `LWBRIDGE_MULTI_PROOF_PATH`—both with max `0x1000`. The encrypted `bridge-scripts.dat` package has dedicated reader `0x1B910` with exactly one caller, `0x1CAFD`, inside the authentic package/auth flow. No recovered native file path/reader provides arbitrary Lua source, eval input, alternate chunk loading or diagnostic script override.

This is not a claim about every CRT/internal filesystem helper. It closes the product-owned bootstrap/runtime source-recovery file surface. After R8-113/R8-114 there is no environment or native-file shortcut around the authentic package/source route; return to the R8-104 legitimate-source-state bottleneck. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-114-proxy-file-input-surface.md` and `evidence/lwbridge-implementation/2026-09-27-r8-114-proxy-file-input-surface.json`.

## R8-113 native proxy environment surface closure

R8-113 exhaustively recovers the original secure/plain proxy environment-variable surface. Both proxies expose the same exact 13 UTF-16 `LWBRIDGE_*` keys, and every caller of the two generic `GetEnvironmentVariableW` wrappers supplies a constant member of that same set. Direct reads are likewise limited to profile/instance/pipe identity, the diagnostic toggle, and runtime-root log-path construction.

The only diagnostic key is `LWBRIDGE_HOST_DIAGNOSTIC`: it must equal exact wide value `"1"` and triggers the fixed embedded host diagnostic no more often than every 2000 ms. `LWBRIDGE_PROFILE_RUNTIME_ROOT` additionally constructs the ordinary `\logs\xlua-proxy.log` path (with bridge-runtime fallback). No environment variable supplies Lua source, eval input, alternate bridge-script source/package, chunk path, or diagnostic source override. This closes the environment/config shortcut raised after R8-112; it does not claim every possible file/registry/protocol input elsewhere is absent. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-113-proxy-environment-surface.md` and `evidence/lwbridge-implementation/2026-09-27-r8-113-proxy-environment-surface.json`.

## R8-112 native pipe-to-Lua dispatch boundary

R8-112 closes the native half of original host→proxy call dispatch. R7-097 proves the host sends `command/kind=call` with `payload.fn` and `payload.args`, but both verified proxies contain no native `"kind":"call"` or `"fn"` routing literal. Instead the native update loop iterates 32-byte string records and calls one fixed Lua global, `XluaBridgeHandlePipeMessage`, through helper `0x13890`. That helper performs `xlua_getglobal(globalName)`, pushes the entire record through `lua_pushlstring`, then executes `lua_pcall(L,1,0,0)`.

Therefore `payload.fn` routing belongs below the native boundary in the unrecovered bridge Lua. The R8-111 scratch discovery of Lua global `__XluaBridgeLoad` does **not** prove that `call_lua(fn="__XluaBridgeLoad")` is reachable or equivalent; that would require the authentic `XluaBridgeHandlePipeMessage` dispatch/provider contract. Do not use generic `call_lua` as a source-loader/eval shortcut without that evidence. R8-111 remains scratch-only because its verifier execution was blocked; R8-112 is independently verifier-backed. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-112-proxy-pipe-dispatch-boundary.md` and `evidence/lwbridge-implementation/2026-09-27-r8-112-proxy-pipe-dispatch-boundary.json`.

## R8-110 compiled-bytecode lifetime closure

R8-110 closes the proposed compiled-bytecode route as a later retained artifact. `0x16C10` has two branches: a direct `luaL_loadbufferx(..., mode="t")` text-load branch that creates no compiled vector, and a compiler branch where `lua_dump` writes to a wrapper-local byte vector. The compiler branch inserts `LENC` at vector begin, applies an in-place ChaCha-family stream transform to bytes after the four-byte prefix, passes that same vector synchronously to `xluaL_loadbuffer`, and frees the vector allocation before `0x16C10` returns.

For `@bridge-scripts.dat`, this owned encoded-bytecode lifetime ends before the caller later zeroizes the assembled source at `0x16183-0x1618A`. The vector is freed rather than explicitly zeroized, so transient allocator residue is not disproven, but there is no recovered global/file/cache owner for the raw `LENC` buffer. The proxy's `lua_dump` slot is confined to resolver/compiler use and exposes no later function-dump surface. Therefore compiled-bytecode caching is not a better/later recovery route than the R8-104 source global. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-110-compiled-bytecode-lifetime.md` and `evidence/lwbridge-implementation/2026-09-27-r8-110-compiled-bytecode-lifetime.json`.

## R8-109 envelope exposure-surface closure

R8-109 exhaustively closes exact host literal/xref ownership for `packageKeyEnvelope`, `packageKeyEnvelopeExpiresAt`, `package-key.envelope`, and `KEY_ENVELOPE_EXPIRED`. The response fields are referenced only by auth-response ingest, the runtime filename only by auth-service path construction, and the expired label only by the static auth error/status table. No second host command result, IPC/provider payload, log/debug message, status payload, or alternate persisted-owner surface carries the actual envelope value.

Combined with R8-107, the host-side lifecycle is closed as `auth response -> stack-local envelope -> package-key.envelope -> local response strings destroyed`. Do not repeat searches for an envelope-returning host command/debug endpoint unless new evidence changes this exact inventory. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-109-envelope-exposure-surface.md` and `evidence/lwbridge-implementation/2026-09-27-r8-109-envelope-exposure-surface.json`.

## R8-108 assembled-source capture specification

R8-108 converts the R8-104 authentic assembled-source lifetime into a hash-locked offline capture specification. For both verified proxies, the source object is an MSVC 32-byte `std::string`: data/heap pointer at `+0x00`, logical length at `+0x10`, capacity at `+0x18`, and small-string threshold `15`. The capture identity must be the exact secure/plain proxy SHA-256, the observation must be stable across a header-before/header-after read, and captured bytes must begin with the exact R8-099 prefix `local __bridge_preload = package.preload\n`.

A live read-only process-memory reader was started during this checkpoint, but the environment blocked further implementation. That rejected operation was not rerouted and the incomplete reader file was deleted. Therefore only the offline capture specification is READY; live capture is NOT AVAILABLE and no original source bytes have been recovered. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-108-source-capture-spec.md` and `evidence/lwbridge-implementation/2026-09-27-r8-108-source-capture-spec.json`.

## R8-107 envelope post-response ownership

R8-107 closes the host-side copy/lifetime question for `packageKeyEnvelope`. The response value is extracted into a stack-local string, validated, serialized and persisted to the service-owned `package-key.envelope` runtime path; the stack-local envelope and expiry strings are then destroyed. Exhaustive direct references to the auth-service object inside `0x23C7EC-0x23CB51` are all read-only path accesses. The envelope text is never assigned into persistent service state. SessionV2's exact seven fields exclude the envelope.

Current-machine checks found no final or temporary filename containing `package-key.envelope` under Local/Roaming AppData and no crash/WER dump of the original `lwbridge-0.3.1.exe`. No File History source is configured; elevated VSS/USN access was not bypassed. Therefore no permitted surviving local copy is currently available from the known host/session/runtime/dump paths. Authentic login can self-provision the local ECDH key (R8-106), but a legitimately issued envelope or genuinely new authentic artifact is still required before the R8-104 assembled-source capture window can be reached. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-107-envelope-copy-ownership.md` and `evidence/lwbridge-implementation/2026-09-27-r8-107-envelope-copy-ownership.json`.

## R8-106 device-key mode contract

R8-106 closes the remaining local device-key provisioning ambiguity. The normal original login path explicitly selects device-key operation mode `0`; the auth wrapper carries that byte unchanged into Tokio task `+0x40`, and worker `0x318D61` dispatches mode `0` to `0x39ED6B`, the open-or-create persisted-key helper. Exact `NCryptOpenKey == 0x80090016` therefore triggers local creation/finalization of the named `ECDH_P256` key before public export.

The normal supervisor selects mode `1`, which dispatches to `0x39ECBD` for open-existing + export only. The cleanup-pending path selects mode `2`, which reaches `0x39F03D` delete/already-missing cleanup.

Therefore the currently absent persisted device key is not a lasting local reachability blocker: authentic login is designed to self-provision it. The current missing package-decrypt material is `package-key.envelope`; no remote auth request, credential use, synthetic key, private-key export, or envelope synthesis was performed. Map remains **NOT WORKING** because neither the envelope/package key nor authentic assembled Lua source has been recovered.

See `docs/reviews/2026-09-27-r8-106-device-key-mode-contract.md` and `evidence/lwbridge-implementation/2026-09-27-r8-106-device-key-mode-contract.json`.

## R8-105 current-machine source reachability

R8-105 applies the already recovered original runtime-material/device-key/package contracts to the machine's current state. The profile runtime root currently contains a structurally valid R8-005 `authorization.challenge` and an `LWBM2` `build.manifest` whose build/package/launcher/hook/composite identity fields match the extracted original 0.3.1 runtime. The challenge contents are not preserved in repository evidence. `authorization.ticket` and `package-key.envelope` are absent.

A non-exporting CNG readiness check opens `Microsoft Software Key Storage Provider` successfully but cannot open the exact recovered persisted device-key name `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}` (status `0x80090016`). The machine's current at-rest proxy prerequisites are therefore incomplete: the persisted ECDH device key is not presently openable and the package-key envelope is absent. R8-105 does not establish whether the original host lifecycle can locally provision the missing key before proxy bootstrap. No credentials, auth request, private-key export, synthetic key, or envelope were used. Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-105-current-runtime-reachability.md` and `evidence/lwbridge-implementation/2026-09-27-r8-105-current-runtime-reachability.json`.

## R8-104 assembled-source capture window

R8-104 recovers the exact bootstrap-owned lifetime of the authentic assembled Lua source. In both verified proxies, bootstrap calls package/auth refresh at `0x153E2`, checks the proxy-owned source global length at `0x1547A`, reads that same source object into the `@bridge-scripts.dat` compiler at `0x15A08-0x15A3E`, then zeroizes the same global only at `0x16183-0x1618A`.

This means raw decrypted module-table bytes do not need to survive loader cleanup for later compilation: parser output is promoted into the source global. If a permitted authentic package/bootstrap state can be reached, the assembled source can be preserved during this later pre-zeroization window and searched directly for `XluaBridgeMapScanTick`. No source bytes are recovered yet, so Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-104-source-capture-window.md` and `evidence/lwbridge-implementation/2026-09-27-r8-104-source-capture-window.json`.

## R8-103 sensitive-buffer cleanup

R8-103 recovers the common cleanup for the exact package-key vector and decrypted module-table buffer used by the authentic package path. In both verified proxies, loader cleanup at `0x1D0F0` calls `0x3F4A0` on the `rsp+0x48` key vector; that helper overwrites every byte in `[begin,end)` with zero before shrinking/releasing storage. Loader cleanup at `0x1D0F9` calls `0x3F350` on the `rbp-0x60` decrypted buffer; that helper overwrites every logical byte with zero, sets length to zero and terminates before later storage cleanup.

R8-102 already ties that `rbp-0x60` object to `0x3D260` decrypt output and `0x125C0` format-2 parser input. Therefore post-return allocator residue is not an intended recovery boundary for either the package key or decrypted module table; any legitimate runtime capture must occur while the enclosing package-processing call still owns those buffers, before common cleanup. Authentic bytes remain unrecovered, so Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-103-sensitive-cleanup.md` and `evidence/lwbridge-implementation/2026-09-27-r8-103-sensitive-cleanup.json`.

## R8-102 build-manifest boundary

R8-102 closes the adjacent post-envelope false lead at proxy RVA `0x40A70`. In both verified proxies the enclosing package/auth loader orders `0x1CC14 -> 0x40A70`, then `0x1CC3A -> 0x3D260`, then `0x1CC54 -> 0x125C0`. The first helper is a build-manifest/proxy-bundle validator with `LWBM1`/`LWBM2`, `build manifest missing/invalid/mismatch`, and bundle fields including `compositeSha256`, `proxySha256`, and `exportFingerprint`.

The actual decrypted module-table ownership is separate: `0x3D260` writes plaintext to caller local `rbp-0x60`, and `0x125C0` immediately consumes that same buffer as the R8-099 format-2 module table. Therefore `0x40A70` is not a hidden package-key/plaintext/cache owner and should not be pursued further for Map source recovery. Authentic decrypted bytes remain unavailable, so Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-102-build-manifest-boundary.md` and `evidence/lwbridge-implementation/2026-09-27-r8-102-build-manifest-boundary.json`.

## R8-100 bounded-work / new-chat handoff checkpoint

The owner requires ChatGPT Web work to run in bounded blocks of roughly 20 minutes maximum because longer uninterrupted sessions have been observed to risk cutoff/stall near ~25 minutes. Around minute 17–18, stop starting new investigations, preserve evidence/state, close a coherent checkpoint where appropriate, return a summary with the exact resume point, and wait for the owner to say `continue`. The rule is durable in `AGENTS.md`, `docs/team-workflow.md`, `docs/implementation-handoff.md`, and the full new-chat handoff at `docs/handoffs/2026-09-27-r8-100-chat-handoff.md`.

R8-100 does not change production behavior or Map/Home acceptance. It is a documentation/continuity checkpoint; the technical recovery baseline remains R8-099 and Map/Home remain **NOT WORKING**.

## R8-099 decrypted module-table/source assembly

R8-099 recovers the exact post-AES plaintext format before the R8-098 compiler boundary. The decrypted package is a format-2 module table: little-endian module count 1..1024 followed by repeated `u32 nameLength`, `u32 sourceLength`, name bytes and source bytes. Module names use only `.0-9A-Z_a-z`; non-bootstrap modules are wrapped into exact `package.loaded` / `package.preload` source, while one non-empty `bootstrap` module is appended raw. Terminal success requires exact byte consumption and a bootstrap. This means authentic AES plaintext can be deterministically split into original modules and searched directly for `XluaBridgeMapScanTick`. Authentic decrypted bytes are still unavailable, so Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-099-module-table.md` and `evidence/lwbridge-implementation/2026-09-27-r8-099-module-table.json`.

## R8-098 original bridge-script compiler/source boundary

Both hash-verified original xLua proxies contain the same Lua source-to-bytecode compiler. They resolve `luaL_newstate`, `lua_close`, `luaL_loadbufferx`, `lua_dump` and `lua_tolstring`; compile text with mode `"t"`; dump bytecode with `strip=0`; and report exact compiler/state/compile/dump failures. The bootstrap passes a proxy-owned source `std::string` to that compiler as chunk `@bridge-scripts.dat`. The package/auth function owns the same source string and contains the package-decrypt/load path. This creates a direct recovery target for the original Map Lua implementation: preserve the authentic source bytes and recover `XluaBridgeMapScanTick` from source rather than redesigning traversal. Plaintext bytes are not yet recovered, so Map remains **NOT WORKING**.

See `docs/reviews/2026-09-27-r8-098-proxy-bridge-compiler.md` and `evidence/lwbridge-implementation/2026-09-27-r8-098-proxy-bridge-compiler.json`.

## R8-065 fresh current-client acceptance

The owner now requires a stricter operational baseline: a feature is not called working merely because its contract is recovered, its UI exists, or offline checks pass. It must be freshly verified against the real installed game.

R8-065 re-established that baseline on current-v21. Home manual launch/connect/close and startup auto-launch/connect/close both reached `connectionState="connected"` in the dedicated live lifecycle proof. Map initially failed live on a current-v21 3×9 AOI contraction; after a bounded conservative compatibility fix, same-session all-eight Normal and Fast scans both completed 2500/2500 with 0 failed and 0 unread, and published Fast counts survived database reopen. The wide-FOV shortcut remains removed.

This proves only that the equivalent reconstructed Home lifecycle and Manual Map scanner can operate live on that client. Under R8-097 they are **NOT WORKING for owner acceptance** because the recovered original LWBridge logic is not yet the production path. See `docs/reviews/2026-09-26-r8-065-live-home-map-v21.md`.

## R8-066 production UI acceptance

R8-066 then proved the normal Release desktop application's actual user-facing Map path rather than a service-only harness: normal persistent profile, Home lifecycle, recovered Map Data WebView, production Manual scanner, native `map_search`, and rendered Resource table. The final current-v21 run reached `connected`, completed Fast Resource scanning at 2500/2500 with 0 failed and 0 unread, returned 515 Resource rows, correlated the first native row to the rendered six-cell table, exited 0, and left neither Last War nor LWBridge running.

The stronger gate also corrected stale proof infrastructure without changing the product scanner: nullable numeric status readers now respect explicit JSON nulls, startup observation uses the exact R8-042 native `profile_instance_status` projection rather than racing reconcile, and Resource render correlation accepts the current six-cell shape while preserving legacy five-cell coverage.

This upgrades historical operational evidence for the equivalent Map reconstruction from backend/service execution to the real desktop/WebView path. It does not make Map WORKING under R8-097 and does not close original acquisition parity. See `docs/reviews/2026-09-26-r8-066-production-map-ui-live.md`.

## R8-096 multi-entitlement capacity ownership

R8-096 closes the profile-capacity source/policy that remained open after R8-063/R8-064. The original service normalizes `maxProfiles` to exactly 1, 2 or 5: only raw 2 and raw 5 survive; all other raw values become 1. The shared eight-field multi-entitlement state publishes through `bridge://multi-entitlement`, stores public `maxProfiles` at `+0xA8`, and is consumed directly by the shared profile-capacity extractor.

Exact public multi-entitlement phases are `single`, `initializing`, `authorized`, `grace`, and `restricted`. Initialization/restriction control-flow gates force public capacity to 1; otherwise the normalized service value is preserved. `multi_entitlement_get` refreshes `/api/multi/entitlement`; `multi_activate` sends `licenseCode` to `/api/multi/activate`. A separate automatic subsystem shares the fetch path while also owning lease recovery/proof/expiry; its exact cadence is still unknown.

Profile create/enable-set are therefore no longer fenced because capacity itself is mysterious. They remain fenced until the authenticated multi-entitlement/lease lifecycle is represented exactly; profile create also retains its separate post-create runtime-cleanup gap.

See `docs/reviews/2026-09-27-r8-096-multi-entitlement-capacity.md`.

## R8-095 auth-service response projection

R8-095 closes the successful login/register/renew/heartbeat response-to-state layer. Login/Register share one projector: raw `token` is mandatory and empty/missing/non-string becomes `SESSION_INVALID`; only exact JSON boolean `expired=true` cleans runtime artifacts and yields `locked / ACCOUNT_EXPIRED`. The projector also owns `username`, `accessRole`, `watermarkTraceCode`, and `expiresAt`.

Role normalization is exact `normal|premium|admin` with fallback `normal`. Watermark trace code must be exactly 12 characters from `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, otherwise it normalizes empty. Renew and Heartbeat retain prior username on omission; only Heartbeat retains prior `expiresAt` when omitted/mistyped, while Renew passes an empty optional expiry into the authorized emitter.

SessionV2 persistence is now tied to its plaintext source: raw service token -> host protection -> `encryptedToken`; normalized `{accessRole,watermarkTraceCode}` JSON -> host protection -> `encryptedMetadata`. R8-096 later closes multi-entitlement capacity ownership/refresh boundaries. No production auth route is enabled yet; credential/device-key bootstrap, real service execution, automatic entitlement cadence/multi-lease lifecycle, remaining scheduling/error semantics, rebuild wiring and live proof remain open.

See `docs/reviews/2026-09-27-r8-095-auth-response-projection.md`.

## R8-094 runtime cleanup correction

R8-094 corrects the R8-093 interpretation of helper `0x14023CBB8`: it is shared best-effort runtime-artifact cleanup, not a grace-clock reset. The no-usable-session branch invokes that cleanup and then publishes normal `signedOut`. See `docs/reviews/2026-09-27-r8-094-auth-runtime-cleanup.md`.

## R8-093 SessionV2 persistence and restore source

R8-093 identifies SessionV2's seventh field as exact `version`, closes the typed 0x88-byte seven-field layout and proves native persists `version=2` and rejects other versions as `SESSION_INVALID`. Restore prefers `auth-session.v2.json` and falls back to legacy `auth-session.json` only when v2 is absent.

The heartbeat/session supervisor is the centralized restore owner. Restored encrypted token and metadata pass through original secure-storage decode helpers; failed restored material normalizes to `SESSION_INVALID`. Successful decoded metadata feeds the common authorization-role projection alongside persisted identity/timing state, so role is reconstructed rather than directly persisted in SessionV2. A no-usable-session result best-effort deletes stored `authorization.ticket` and `package-key.envelope` before publishing normal `signedOut`.

The auth service also owns `LWBRIDGE_AUTH_URL` with default `https://auth.songunity.com`; heartbeat defaults to 120 seconds and only accepts configured integers 30..300 inclusive. R8-095 later closes successful service-response projection and R8-096 closes entitlement/capacity ownership. No production auth path is enabled yet; credential/device-key bootstrap, legitimate service execution, automatic entitlement cadence/multi-lease lifecycle, remaining service scheduling/error behavior, implementation and live proof remain open.

See `docs/reviews/2026-09-27-r8-093-session-v2-source.md`.

## R8-092 public AuthState producer

R8-092 recovers the shared 0xC0-byte AuthState object and exact public schema: `phase,username,accessRole,watermarkTraceCode,expiresAt,lastHeartbeatAt,lockedUntil,errorCode`. Exact phases are `checking`, `authorized`, `grace`, `locked`, and `signedOut`; mutations publish through `bridge://auth-state`.

Authorized refresh copies the successful identity/role projection, refreshes public `lastHeartbeatAt`, and clears lock/error. Grace preserves prior identity while changing phase and carrying the current error. Checking preserves the state while clearing the public error. Normal signed-out reset clears identity/timing/error state and restores `accessRole="normal"`; logout uses this exact emitter. A separate session-error transformer maps exact `SESSION_INVALID` to phase `signedOut`, otherwise `locked`.

Heartbeat uses a separate internal grace clock at `+0x420`: after its eligibility predicate, grace is admitted while elapsed time is <=900000 ms, matching R8-091's command-admission `<900001` rule. Renew publishes authorized/locked; heartbeat authorized/grace/locked; the supervisor checking/session-error/signedOut. `auth_state` itself is a snapshot read and does not directly mutate the producer.

No live auth route is enabled yet. R8-093 subsequently closes persisted SessionV2 restore, R8-095 closes successful service-to-session mutation, and R8-096 closes entitlement/capacity ownership. Remaining dependencies are credential/device-key bootstrap, legitimate service execution, automatic entitlement cadence/multi-lease lifecycle, remaining service scheduling/error behavior, and rebuild/live validation.

See `docs/reviews/2026-09-27-r8-092-auth-state-producer.md`.

## R8-091 authorization projection and Automation request correction

R8-091 recovers the shared authorization admission far enough to replace several old assumptions. Exact host behavior recognizes `authorized`; permits `grace` only with a positive grace timestamp and `now - graceStartedAt < 900001` ms; surfaces `ACCOUNT_EXPIRED` for expiry and `AUTH_REQUIRED` as the no-usable-state fallback. Role admission compares exact `accessRole` membership. Watermark's role policy is `premium|admin` followed by an `admin`-only gate.

`EntitlementResponse` is exact as `planCode,maxProfiles,expiresAt,accountExpiresAt,serverTime`. R8-091 initially recovered six SessionV2 names; R8-093 later closes the seventh as exact `version`, persisted and required as value `2`.

Direct provider-request tracing also corrects R8-051/R8-068: generic Automation sends only `{task,config}` / `{task,config}` / `{task,options}`, and Inspect sends only `{task}`. The prior `premium/admin` request-field claim was a raw-metadata adjacency inference and is withdrawn. No live route is enabled yet: R8-092/R8-093 recover the AuthState producer and SessionV2 persistence contract, but the rebuild still lacks the lower-level device-key/ticket-envelope/service/entitlement source and implementation.

See `docs/reviews/2026-09-27-r8-091-authorization-projection.md`.

## R8-090 pending-call teardown boundary

R8-090 narrows the remaining disconnect/write-failure gap without changing production behavior. Original `bridge_store.rs` metadata source-links a distinct `BRIDGE_STOPPED / bridge result channel closed` call-wait branch and a separate `LUA_CALL_TIMEOUT` branch, while also exposing `call`, `command` and `timeout` source locations.

What remains unproven is the causal edge from ordinary named-pipe disconnect/write failure to all outstanding result receivers. No eager route-wide `BRIDGE_STOPPED` policy is added; the equivalent transport keeps the R8-075 teardown behavior as research/compatibility evidence until stronger source evidence exists. It is not owner-accepted WORKING or a fallback acceptance path.

See `docs/reviews/2026-09-27-r8-090-pending-call-teardown-boundary.md`.

## R8-089 host↔proxy heartbeat boundary

R8-089 proves the original host bridge-pipe heartbeat metadata references exact `/payload/time` and tracks heartbeat/activity state beside the no-inbound-frame idle-timeout path. It also proves both verified proxy natives classify an already serialized `heartbeat` separately from `result`; the native classifier is not itself the heartbeat JSON serializer.

This narrows the protocol gap but does not close exact heartbeat parity. The protected/package serializer, `payload.time` type/value source, requestId/timestamp choices, cadence and public `lastHeartbeatAt` assignment remain unrecovered. The current five-second Lua writer remains equivalent plumbing.

See `docs/reviews/2026-09-27-r8-089-pipe-heartbeat-boundary.md`.

## R8-088 native-capture point population

R8-088 closes safe point-side population in both verified proxies. The fixed 50-field IL2CPP bundle plus R8-084 identity fields and R8-086 getters now source-account for all 57 R8-083 point serializer fields.

Exact transforms include five `List<T>._size` count projections, IL2CPP runtime-class naming, special/complete boolean derivation, three int32-to-i64 time sign extensions, eleven string conversions, TreasurePointInfo start/expire/create plus treasureType fallback logic, and WorldSuppliesPoint cfgId override.

Together with R8-087, both safe full-record producer population models are closed; the central Map acquisition gap is now the protected MapScanTick traversal/work layer. See `docs/reviews/2026-09-27-r8-088-native-capture-point-population.md`.

## R8-087 native-capture march/train population

R8-087 closes the safe march-side raw population model in both verified proxies. The producer resolves a fixed 23-field IL2CPP bundle, maps its raw scalar/string fields into the R8-083 march schema, and uses a source-backed `pointIndex` precedence of positive `GetMarchCurPosIndex`, positive `targetPos`, positive `startPos`, then nullable `homePos`.

It also closes the `isMonster` fallback (`IsMonsterOrOrdinaryBoss > 0`, otherwise present `monsterId > 0`) and the nested train composition from outer `uuid/cfgId/type/config` plus `config.id/quality/carriageNum` into the exact six-field train object at march offset `0x170` with presence at `0x1A8`.

The larger point producer and protected MapScanTick internals remain open. See `docs/reviews/2026-09-27-r8-087-native-capture-march-population.md`.

## R8-086 native-capture getter population routes

R8-086 proves nine resolved game-facing methods feed exact R8-083 native-record fields. Point routes are resource type/level under the `pointType == 7` branch, treasure type only for `TreasurePointInfo`, and config ID only for `WorldSuppliesPoint`. March routes are positive preferred current point index, max HP, and the three monster/rally/normal booleans; `IsMonsterOrOrdinaryBoss` is normalized with `> 0`.

This removes ambiguity for those nine routes while leaving the rest of point/march field population, protected MapScanTick traversal/work and retry behavior open. See `docs/reviews/2026-09-27-r8-086-native-capture-population-routes.md`.

## R8-085 native-capture memory budget

R8-085 proves both verified proxies share one 32 MiB pending-record byte budget across full point and march captures. The point formula is fixed `0x3A0` plus capacity+1 for each present optional string; the march formula is fixed `0x1B0` plus capacity+1 for each present optional string.

The admission rule only charges positive replacement growth. Equal/smaller same-identity replacements remain admissible at a full budget; growth beyond remaining bytes enters native dropped handling. Point and march drains subtract their exact footprints from the same counter, and run-ID change resets queues, dropped count and byte accounting.

This closes memory-pressure admission separately from R8-084's 65,536-record ceiling. See `docs/reviews/2026-09-27-r8-085-native-capture-memory-budget.md`.

## R8-084 native-capture hook routing and queue identity

R8-084 proves both verified proxies install the same ten world-capture hooks and route them through the same safe-region producers/removal helpers. AddPointInfo captures post-original into point producer `0x34CC0`; AddMarch, UpdateMarch, AddOrUpdateMarch and UpdateTroop capture post-original into march producer `0x34090`.

Point capture requires positive pointIndex, normalizes non-positive mainIndex to pointIndex, and only admits the canonical/main point. Pending points upsert by normalized mainIndex; pending marches upsert by captured UUID. Direct point removals are post-original, while ParseWorldPointRemove/FoldUp parse the exact SFS key `pointIds` before the original handler and feed the same positive-ID removal helper.

The installer resolves exact point/march field offsets and game-facing resource/treasure/config/march/SFS getter methods. Producer paths share a 65,536 aggregate pending-record ceiling; same-identity replacement remains allowed at the ceiling, while a new identity enters the native dropped path.

The protected MapScanTick traversal/order/coordinates, per-tick request pacing, retry/backoff, any separate protected acknowledgement semantics and remaining per-field population transformations are still unrecovered. See `docs/reviews/2026-09-27-r8-084-native-capture-hooks.md`.

## R8-083 native-capture point/march serializers

R8-083 recovers the exact native-capture point/march JSON schemas in both verified proxies. Point records are `0x3A0` bytes and serialize to exactly 57 ordered fields; march records are `0x1B0` bytes and serialize to exactly 25 ordered fields. The nullable nested `train` object has exactly six ordered fields.

The serializers now distinguish fixed fields, nullable integer/boolean/float values, quoted 64-bit IDs, escaped strings, and nullable train data. The shared optional-string helper proves absent string values are emitted as `null`, not omitted.

This closes native-capture record representation. Producer hook population plus `XluaBridgeMapScanTick` traversal, block/request work, retry/backoff and any separate script acknowledgement mechanism remain unrecovered.

See `docs/reviews/2026-09-27-r8-083-native-capture-item-serializers.md`.

## R8-082 native-capture service / flush gates

R8-082 proves both verified proxies service native capture no more frequently than every 16 ms using `GetTickCount64`. A changed `scanRunId` updates stored run identity, resets the active emission clock, and sets a one-shot wake flag.

Nonempty `scanRunId` uses a 250 ms forced-emission clock; empty `scanRunId` uses 1000 ms. Final emission is driven by drained vectors, remaining pending counts, the run-ID-change wake flag, or the applicable forced clock. After an actual JSON envelope is assembled, the corresponding active/idle clock is updated.

Together R8-080/R8-081/R8-082 now close outer Map tick cadence, native capture batch order/budget, and native capture service/flush timing. Traversal/coordinates, per-tick work/request generation, retries and remaining per-kind serialization still remain unrecovered.

See `docs/reviews/2026-09-27-r8-082-native-capture-flush.md`.

## R8-081 native-capture drain budget/order

R8-081 proves both verified proxies drain native capture with one shared 1024-item budget in fixed order: points → marches → point removals → march removals. The budget is not reset between categories, and every drained entry is removed from its queue while decrementing that queue's pending count.

Point removals are numeric 32-bit values; march removals are quoted strings. ACKs remain outside this drain because R8-079 proves the capture serializer emits `acks=[]` and `pendingAcks=0`.

Traversal/coordinates, per-tick work, retries and exact publication/flush timing remain unrecovered. See `docs/reviews/2026-09-27-r8-081-native-capture-drain.md`.

## R8-080 outer MapScanTick cadence

R8-080 recovers the proxy-pump timing boundary around `XluaBridgeMapScanTick`. Both verified embedded proxies call `GetTickCount64` and gate Map tick on unsigned elapsed time `>= 50 ms`, updating the last-run timestamp before dispatch.

The same pump uses fixed outer gates of 16 ms for `XluaBridgeInputPoll`, 200 ms for `XluaBridgeNativeUpdate`, and 500 ms for `XluaBridgePoll`. No Normal/Fast branch appears in the outer Map gate.

This closes outer tick cadence only. It does not recover the amount of scan work per tick, traversal/coordinates, internal mode differences, retry/backoff or queue/completion behavior. See `docs/reviews/2026-09-27-r8-080-map-tick-cadence.md`.

## R8-079 native-capture ACK constants

R8-079 recovers the original native-capture ACK serialization in both verified embedded proxies. Serializer `RVA 0x38AB0-0x39F15` emits `acks=[]` and `pendingAcks=0` as constants, while the four point/march/removal pending counts are dynamic.

Therefore the native-capture envelope itself has no dynamic ACK item queue to reproduce. R8-077's host-side `pendingAcks` reader remains real, but the verified capture producer supplies zero at this boundary. A separate acknowledgement concept elsewhere in the protected script/`XluaBridgeMapScanTick` layer is not ruled out.

Block traversal/order/coordinates, tick scheduling/pacing, queue→batch timing and retry/backoff remain unrecovered. See `docs/reviews/2026-09-27-r8-079-native-capture-ack-constants.md`.

## R8-078 Map diagnostic/error and failed-run persistence

R8-078 recovers the host failure split after Map producer events. `map.scan.diagnostic` is only formatted/logged and does not drive scan-state transitions in its bounded branch.

`map.scan.error` chooses its message from event `error`, then scan-state `lastError`, then `map scan failed`. It uses ordinary fail-map-scan persistence: conditional running→failed transition, staging deletion, shared visible-state cleanup, and conditional `stopMapScan`; staged rows are not published.

A failure found after `map.scan.complete` uses the preserve-failed transaction instead: mark the run failed, copy `scan_records` into published `map_records`, clear staging, and do not redundantly request native Stop. This proves the original intentionally preserves completed acquisition data even when host terminal admission later rejects the run.

Producer-side traversal, tick pacing, acknowledgement consumption/drain and retries remain unrecovered. See `docs/reviews/2026-09-27-r8-078-map-scan-error-failure.md`.

## R8-077 Map acknowledgement host boundary

R8-077 narrows the remaining acknowledgement/drain problem without changing production code. In the original `map.scan.complete` host path, `pendingPoints`, `pendingMarches`, `pendingPointRemovals`, `pendingMarchRemovals` and `pendingAcks` are count fields that are summed into the reported `nativePendingRecords` metric.

The aggregate is serialized into scan state and then its working registers are overwritten before the terminal failure/publication branch. The recovered host decision path does not parse `acks[]` items and does not use `nativePendingRecords > 0` as a terminal admission predicate. Positive `dropped` remains a separate explicit failure/cleanup input.

Therefore acknowledgement item schema, acknowledgement consumption and any acknowledgement-to-block completion/retry linkage are owned below the recovered host event layer, before or while protected game-side code emits `map.scan.progress` / `map.scan.complete`. The next original-Map target is now the producer-side tick/drain boundary, not host publication logic.

See `docs/reviews/2026-09-27-r8-077-map-ack-host-boundary.md` and `evidence/lwbridge-implementation/2026-09-27-r8-077-map-ack-host-boundary.json`.

## R8-076 original Map acquisition ingestion

R8-076 narrows one of the largest remaining parity gaps. Original Map acquisition is no longer wholly UNKNOWN: the host-side production ingestion architecture is now source-linked.

The common native Map event handler distinguishes `map.records`, `map.scan.diagnostic`, `map.native.capture`, `map.scan.progress`, `map.scan.complete`, and `map.scan.error`. Direct-scan `map.records` batches carry event `scanRunId` and `records`; each accepted record is a `{type,payload}` entry filtered against the original eight kinds/current selection, augmented with scan-state server/dimensions, normalized by the shared record builder, and transactionally upserted into run-scoped `scan_records`.

The separate `map.native.capture` event uses the same normalized builder but writes published `map_records`, proving that passive native-hook updates and direct full-scan staging are distinct original paths.

The protected remaining work is game-side block traversal/order/coordinates, `XluaBridgeMapScanTick` registration/scheduling/pacing, native point/march/removal/ack queue-to-batch drain/retry semantics, and remaining per-kind serializer details. The current v21 movement/AOI scanner remains historical live-success equivalent evidence/comparison tooling only. Under R8-097 it is NOT WORKING and must not be used as a production fallback.

See `docs/reviews/2026-09-26-r8-076-original-map-acquisition-ingestion.md` and `evidence/lwbridge-implementation/2026-09-26-r8-076-original-map-acquisition-ingestion.json`.

## R8-075 host↔proxy protocol re-audit

R8-075 narrows the previously broad host/proxy protocol gap. The source-backed host wire now includes exact pipe identity/framing, `hello` and `hello.ack`, PID/path/build/token authentication, protected listener/accept-loop semantics, `command/call`, first `cmd_1`, `result`, `payload.id` correlation, queue/byte limits, principal timeouts, reconnect generation and terminal route cleanup. R7-127 already live-proves the production authenticated named-pipe route and correlated read-only `getStatus` response against the real game.

Public readiness semantics are also split exactly: R8-043 proves `get_status.backend/xluaOnline` are route-presence state, while R8-042 proves retained `profile_instance_status.connectionState=connected` additionally requires identity confirmation and a heartbeat fresher than 15,001 ms.

R8-089 narrows the first item: the host bridge-pipe heartbeat path references exact `/payload/time`, and both proxy natives classify a pre-serialized `heartbeat`, but the protected/package serializer, `payload.time` type/value source, requestId/timestamp choices, cadence and public `lastHeartbeatAt` assignment remain open. The other exact protocol gaps are disconnect/write-failure to outstanding result-channel/public-call completion mapping and original secure/plain xLua script/provider dispatch. The current GamePipeAdapter/mailbox/Lua command handler remains equivalent compatibility/research plumbing, not original proxy parity and not owner-accepted WORKING.

See `docs/reviews/2026-09-26-r8-075-host-proxy-protocol-reaudit.md` and `evidence/lwbridge-implementation/2026-09-26-r8-075-host-proxy-protocol-reaudit.json`.

## R8-074 retained frontend routing inventory closed

R8-074 closes the final two genuinely-unclosed retained frontend contracts: `map_dispatch_share_alliance` and `map_treasure_claim`.

Dispatch Share now has exact authorization-first admission, complete row validation, sequential 5-second `shareDispatchTaskToAlliance` calls, exact `shared=true` success predicate and aggregate result. Treasure Claim now has exact authorization-first admission, server/scope/default validation, 5-second `getCurrentServerId` preflight, dedicated candidate-query ownership, 5-second `claimTreasures` request, ten immediate counters and the frontend's 1-second / 1800-iteration polling contract.

No live routes are added because both state-changing actions require owner-excluded authorization-state admission before protected provider execution. The retained frontend routing inventory is now fully classified: **61 specifically routed, 33 audited/fenced, 0 genuinely unclosed**.

This does not mean the project is complete. Native-only commands, host/proxy protocol, script/proxy internals, original Map acquisition/action internals, reconstruction drift and final one-to-one validation remain open.

See `docs/reviews/2026-09-26-r8-074-final-retained-frontend-routing-close.md` and `evidence/lwbridge-implementation/2026-09-26-r8-074-final-retained-frontend-routing-close.json`.

## R8-073 provider-backed Automation/Squad fences

R8-073 closes `equipment_preset_apply`, `resource_automation_run`, and `trade_station_configure` far enough to move them from genuinely unclosed to audited/fenced.

Equipment Apply now has exact preset admission, live `getSquads` and `applyHeroEquipment` 5-second calls, `bridge://equipment-apply-progress`, and provider-result boundaries; the remaining multi-hero planner/aggregation is protected. Resource Run now has exact two-task mapping, unknown-task/state-unavailable/busy behavior, dedicated 10-second runner boundary and status-event ownership, but its live request contains owner-excluded authorization-derived `premium/admin`. Trade Configure now has exact retained fields, enabling validation, shared `trade_station`/`config.json` ownership and 5-second `configureTradeStation` calls.

No runtime routes are added because reproducing any of these exactly would still require excluded authorization state and/or unrecovered live planner/shared-config semantics. The 33 unrouted retained frontend commands now split into 31 audited/fenced and 2 genuinely unclosed: `map_dispatch_share_alliance` and `map_treasure_claim`.

See `docs/reviews/2026-09-26-r8-073-final-provider-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-073-final-provider-actions-fence.json`.

## R8-072 VIP18 Base Apply / Restore fence

R8-072 closes `vip18_base_apply` and `vip18_base_restore` at the retained host/provider/config boundary. Apply validates `skinId`, calls `setLocalVip18BaseSkin({skinId})` at 10 seconds, then patches `selectedSkinId`. Restore calls `restoreLocalBaseSkin({})` at 10 seconds, then patches `selectedSkinId=null` and `autoApplyOnStart=false`.

Both commands perform the game action before shared VIP18 config reconciliation. A later config-state failure can therefore occur after the in-game change; full success returns the provider JSON through the shared generic converter.

No runtime route is added because exact execution still needs owner-excluded authorization-state admission and the unrecovered shared `config.json` migration/merge/write owner. These two commands move to audited/fenced, leaving 5 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-072-vip18-base-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-072-vip18-base-actions-fence.json`.

## R8-071 Dispatch Assist action fence

R8-071 closes the recoverable host/database/live-helper boundaries for `dispatch_assist_schedule`, `dispatch_assist_cancel`, and `dispatch_assist_retry`. Native `getAllianceDispatchTasks` and `armAllianceDispatchAssist` helpers use 5-second deadlines; Cancel uses `cancelAllianceDispatchAssist` at 5 seconds. The persisted `dispatch_assist_jobs` read/insert/retry/cancel/delete helpers and `bridge://dispatch-assist-changed` publication are now mapped.

Schedule’s 1–200 UUID validation, missing-live-task and already-scheduled failures are recovered; Retry’s missing-job/task-unavailable/not-retryable failures are recovered; Cancel’s scheduled-job-not-found boundary is recovered.

No runtime route is added. Schedule/Retry still require the unrecovered live task projection already fenced in R8-045, and all three retain mandatory owner-excluded authorization-state admission. These three commands move to audited/fenced, leaving 7 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-071-dispatch-assist-actions-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-071-dispatch-assist-actions-fence.json`.

## R8-070 City Layout live-command fence

R8-070 corrects the older “implementation-ready” classification for `city_layout_validate`, `city_layout_apply_start`, and `city_layout_apply_cancel`. Fresh native xrefs prove all three first await the shared owner-excluded authorization-state future, then resolve the selected runtime and invoke their protected game providers.

The recoverable contracts are now exact at the host boundary: Validate sends `{baseRevision,placements}` to `validateCityLayout` at 10 seconds; Apply Start performs the fresh-snapshot/`isInCity` gate before `startCityLayoutApply` at 10 seconds; Cancel sends `{jobId}` to `cancelCityLayoutApply` at 5 seconds. Normal results use the shared generic JSON converter.

No runtime route is added because bypassing mandatory authorization-state admission would change native public behavior. These three commands move to audited/fenced, leaving 10 genuinely-unclosed retained frontend routing gaps.

See `docs/reviews/2026-09-26-r8-070-city-layout-live-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-070-city-layout-live-fence.json`.

## R8-069 Chat Automation fence

R8-069 closes the retained native/public boundaries of `chat_automation_configure` and `chat_automation_run_pending`. Retained kinds, host-side Configure validation, provider methods, exact 5-second deadlines and generic result conversion are recovered. Both handlers still require the shared authorization-state future before selected-runtime/game-route admission.

No `premium/admin` provider fields are observed here. R8-091 later proves the same for generic Automation; the fence is the authorization-state admission itself. Skipping it changes native error precedence. No runtime route is added.

The 33 unrouted retained frontend commands now split into 20 audited/fenced and 13 genuinely unclosed.

See `docs/reviews/2026-09-26-r8-069-chat-automation-fence.md`.

## R8-068 generic Automation live-command fence

R8-068 closes the recoverable native/public contract for `automation_configure`, `automation_start`, and `automation_stop`. Their exact handlers, immutable frontend payload shapes, shared authorization/profile/runtime/game-route admission, provider methods, 5-second deadlines, timeout behavior and generic provider-result conversion are now identified.

Configure additionally runs the native task-config validator and normalization path before its provider call; the task-specific validation vocabulary is substantially recovered. R8-091 corrects the earlier adjacency-based inference: Configure sends `{task,config}`, Start sends `{task,config}`, Stop sends `{task,options}`, and none of the three handlers adds `premium` / `admin` fields. The live boundary remains fenced because all three still require the original shared authorization-state admission, whose producer is not yet implemented in the rebuild.

No runtime route is added. The three commands move from the genuinely-unclosed bucket to audited/fenced. The 33 unrouted retained frontend commands now split into 18 audited/fenced and 15 genuinely unclosed.

See `docs/reviews/2026-09-26-r8-068-generic-automation-live-fence.md` and `evidence/lwbridge-implementation/2026-09-26-r8-068-generic-automation-live-fence.json`.

## R8-067 command inventory checkpoint

R8-067 closes the exact recovered frontend command/event inventory and turns the production routing gap into a counted queue. The authoritative reference API asset contains 104 unique command literals; 10 are explicitly excluded Account/Login/Authentication or account-purpose activation/entitlement commands, leaving 94 retained frontend commands. Across the current production backend, host-special branches and composed command services, 61 of those 94 have a specific route and 33 do not.

The 33 unrouted retained commands split into 15 whose native boundary has already been materially recovered and deliberately fenced, and 18 genuinely unclosed routing gaps. The 18 are now the concrete retained command queue rather than an unspecified whole-program backend gap.

The frontend inventory is not the complete native inventory. Existing exact native checkpoints prove at least eight original handlers have no matching literal anywhere in the recovered frontend assets, so the backlog's complete command/service inventory remains open. Raw executable string splitting was tested and rejected as authority because packed/adjacent strings merge command names with neighboring text.

See `docs/reviews/2026-09-26-r8-067-reference-command-inventory.md` and `evidence/lwbridge-implementation/2026-09-26-r8-067-reference-command-inventory.json`.

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

Verified SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Strongest parity already achieved

The recovered frontend is the closest portion to true one-to-one recovery. Original React/Vite chunks, stylesheet, icons and nine locale bundles were extracted, and prior visual comparison showed 31/32 tested feature-region pairs pixel-identical with the remaining pair negligible.

That proof is limited because the rebuild intentionally transformed the main/API boundary and removed/changed some product surfaces.

## Largest parity gaps — inventory, not execution priority

R8-097 execution priority is fixed: **original Map acquisition engine first, then remaining original Home lifecycle**. The numbered list below inventories large whole-program gaps and is not a work-order ranking.

1. Full plaintext/original handler recovery from `bridge-scripts.dat`.
2. Remaining exact host/proxy parity: wire-heartbeat payload/activity ownership, disconnect/write-failure to outstanding-call completion mapping, and original secure/plain xLua script/provider dispatch. R8-075 confirms the rest of the retained host wire/session contract is recovered and the read-only equivalent production route has historical live-success evidence; that does not by itself satisfy the R8-097 owner WORKING label.
3. Remaining original Map Scan internals: game-side traversal/order/coordinates, per-tick work/request pacing and retry/backoff inside `XluaBridgeMapScanTick`, any separate protected script/tick acknowledgement semantics, and still-unmapped per-field population/transformation rules. R8-076 closes host event/normalization/staging; R8-079 closes native-capture ACK fields; R8-080 closes the 50 ms outer Map tick gate; R8-081 closes shared 1024-item queue draining; R8-082 closes 16 ms service plus 250/1000 ms active/idle flush timing; R8-083 closes exact point/march/train JSON representation; R8-084 closes safe-region hook routing, queue identity/admission, `pointIds` removal parsing and named producer field/getter inputs. The lane remains PARTIAL EXACT_CONTRACT.
4. Removal of rebuild-only additions such as Secret Task Quick Find.
5. Restoration of remaining retained product features previously retired/customized. City Excel is restored in R8-007, Map Clear in R8-008, `server_jump` in R8-009, `map_summary` in R8-010, `map_data_options` in R8-011, Manual Scan public contract in R8-012, status/Stop lifecycle in R8-013, `map_search` public kind/filter ownership in R8-014, the original Auto Scan frontend scheduler/control plane in R8-015, and Scheduled Plunder list/schedule/cancel persistence/events/original UI in R8-016. R8-017 removes speculative Monster-distance, City-shield and Railway-quality sort implementations and gates those branches pending stronger evidence. R8-044 restores the native `game_asset_image` public request/result/error contract, one 15-second game call, PNG-signature validation and persistent profile-runtime `asset-cache` behavior; the exact native SHA-256 cache-key preimage and original transport/file-I/O plumbing remain unclaimed. Complete native multi-sort assembly and protected Plunder execution remain partial/unknown. R8-054 re-audits the existing `lastwar_localize` path: requested-locale→English→key fallback, the exact 200-key cap and locale-cache error vocabulary remain evidence-backed, but the command is only partial/equivalent because the rebuild bypasses native owner-excluded authorization-state admission and has stricter outer payload parsing. R8-055 additionally audits `map_treasure_claim_status`: native awaits owner-excluded authorization state, calls `getTreasureClaimStatus` once with a 5-second deadline, transactionally maintains `treasure_claim_states`, then returns the original provider JSON. The current rebuild status route remains a documented deviation because it adds scan/operation/server guards, uses an 8-second inspection path, rebuilds a fixed DTO, and cannot preserve provider extras such as optional `batch`. R8-074 now closes the original `map_treasure_claim` host contract but keeps live claim/scout execution fenced on owner-excluded authorization state plus unrecovered protected executor internals. Authentication/authorization/entitlement/session dependencies may now be recovered when retained behavior requires them; standalone account-product UI remains non-priority unless that dependency pipeline requires it.
6. Whole-program backend parity for Automation, Squads/AFK, City Layout, Hotkeys, Mini-games and Settings. R8-018 restores City Layout drafts; R8-019 Hotkeys; R8-020 visual metrics; R8-021 updater idle status; R8-022 feedback-export contract/fence; R8-023 equipment config; R8-024 Monster AFK config; R8-025 Alliance Garrison config; R8-026 Resource Automation configure/persistence plus exact normal idle status/event behavior; R8-027 generic `automation_status` exact normal missing-file idle projection for all 18 native tasks and valid persisted-status passthrough; R8-028 `profile_settings_save` revisioned per-profile settings persistence for the current local profile; R8-029 normal retained single-profile `profile_list` controller-registry persistence/public field shape; R8-030 exact `profile_note_set` Unicode validation, note persistence, errors and refreshed profile-list result; R8-031 exact `profile_reorder` profile-ID/permutation validation and transactional display-order persistence; R8-032 fixed `profile_primary_set` guard plus native primary uniqueness invariant; R8-033 exact red-packet/treasure claim-delay validation and mirrored scheduler/chat runtime-config persistence; R8-034 exact `profile_select` selection guards, selected-profile persistence, and best-effort verified game-window focus; R8-035 exact `set_window_theme` DWM attributes/colors plus native semantic/failure errors; R8-036 exact server-jump history normalization/get/set/import migration plus per-profile `map-data.db/app_settings` ownership; R8-037 exact `append_log` runtime file path, line format, 2,000-scalar cap, CR/LF sanitation and best-effort write behavior; R8-038 exact `game_root_status` public four-field/candidate schema, source ordering, empty state, lightweight root predicate and `STATE_UNAVAILABLE`, with equivalent LocalConfig/process/registry plumbing and stricter launch admission kept separate; R8-039 exact `game_root_select` three-field cancel/invalid/valid results, path normalization, native lightweight validation, valid-only persistence and exact `STATE_UNAVAILABLE`/`INVALID_GAME_ROOT` vocabulary, with equivalent folder-dialog/LocalConfig plumbing; R8-040 exact native `proxy_status` full/reduced field projection, proxy target/original path layout, state precedence, secure/plain hash classification, runtime-managed projection, repair predicate and stable profile/runtime error codes, with read-only resource discovery as equivalent plumbing; R8-041 exact `game_recovery_status`/`bridge://game-recovery` 11-field order, scalar/null representation, idle defaults, numeric notice identity/visibility, terminal `succeeded`/`failed` publication and strict profile/runtime/`STATE_UNAVAILABLE` handling, with existing recovery process-control thresholds/actions unchanged; R8-042 restores exact `profile_instance_status` null-or-12-field public output and retained single-profile active connection-state projection while leaving start/stop/reconcile process control unchanged; R8-043 closes the exact native `get_status` seven-field top-level projection (`ok`, `backend`, `runtimeRoot`, `pending`, `xluaOnline`, `lastXluaActivity`, `config`), route-backed offline/named-pipe ownership, Unix-ms activity rule and `STATE_UNAVAILABLE`, but fences implementation because the full native config migration/normalization routine remains partial and the current rebuild status result is therefore still a documented deviation. R8-045 closes the native `dispatch_assist_state` six-field top-level schema, exact unavailable-state error and persisted-job selection/order, but leaves success unimplemented because the live alliance-dispatch provider/task-row projection is not yet recovered. R8-046 restores native `squad_list` profile/runtime/disconnect errors, one `getSquads` call with empty args, exact 5-second timeout and raw correlated JSON result forwarding through the retained authenticated transport. R8-047 restores native `monster_catalog_options` with the same strict profile/runtime admission, exact disconnect behavior, one `getMonsterCatalogOptions` call with empty args, exact 5-second timeout and raw correlated JSON forwarding. R8-048 restores native read-only `trade_station_catalog` profile/runtime/disconnect behavior, one `getTradeStationCatalog` call with empty args, exact 10-second timeout and raw correlated JSON forwarding while leaving `trade_station_configure` fenced. R8-049 fences the hidden VIP18 base read family after proving its public/cache/refresh behavior depends on two boundaries we must not fake: an excluded authorization-state-dependent path for the catalog-cache identity and the still-unrecovered shared config-state migration/normalizer for config get. R8-050 additionally closes the observable `city_layout_snapshot_get` host boundary—exact authorization-state unavailable error, game disconnect error, `getCityLayoutSnapshot`, 10-second deadline, and raw provider JSON forwarding—but leaves implementation fenced because native supplies an owner-excluded authorization-state-derived request argument. R8-051 closes the observable `automation_inspect` authorization-unavailable/disconnect/`inspectAutomationTask`/5-second boundary and identifies the special `allianceGarrison` result rewrite over `allies`/`uuid`/`serverId`/`uid`/`updatedAt`, but leaves implementation fenced because authorization-derived request material is owner-excluded and the exact rewrite is not yet closed. R8-052 closes the observable `city_layout_apply_status` authorization-unavailable/disconnect/`getCityLayoutApplyStatus`/5-second/generic-result boundary, but leaves runtime status fenced because native still requires owner-excluded authorization-state admission and the live apply provider/status schema is unrecovered. R8-053 closes `profile_settings_get` required `profileId`, the native singleton settings read, `PROFILE_DATA_INVALID` vocabulary and exact `{profileId,revision,value}` result, but leaves the public getter fenced because native first requires owner-excluded authorization-state admission. R8-056 closes `watermark_lookup` trace-code normalization, authorization/role admission, `/api/watermark/lookup` request ownership, generic service error vocabulary and raw JSON success return, while runtime remains fenced at the excluded authorization-state boundary. R8-057 closes native `update_status` beyond idle: exact nine-field read-only snapshots, seven phases (`idle`, `checking`, `upToDate`, `available`, `downloading`, `opening`, `error`), 60-second manual-check cooldown, manifest metadata, download progress/directory, opening at 100%, generic/fixed error transitions and `bridge://update-status` publication. The rebuild remains exact only at idle because `update_check` and `update_download_and_open` are still fenced. R8-058 closes the native main-window close request and global `app_exit_confirm` boundary: `app://close-requested` with numeric `instanceCount`, per-managed-runtime `bridge_exit`, exact game-close timeout, original-proxy restoration requirement, `APP_EXIT_FAILED`, and JSON-null success; implementation remains fenced because the rebuild still lacks the original proxy install/backup/restore mutation lifecycle. R8-059 closes `monopoly_cell_open` selected-profile injection, authorization/profile/runtime/disconnect admission order, one `openMonopolyCell` `{}` call at 5 seconds, shared Lua-call failure mapping and raw provider JSON return, while runtime remains fenced because native authorization-state admission is owner-excluded. R8-060 closes `construction_rewards_claim` under the same retained wrapper/admission pattern with one exact `claimConstructionRewards` `{}` call at 5 seconds and raw provider JSON return, while the live claim remains fenced on owner-excluded authorization state. R8-061 closes hidden `vip18_base_config_save` field validation, exact invalid skin/auto-apply errors, `vip18_profiles`/`config.json` persistence ownership, config-state unavailable behavior and normalized three-field success projection; runtime save remains fenced on owner-excluded authorization admission plus the unrecovered shared config-state migration/normalizer. R8-062 closes native `profile_delete` authorization-first admission, running/not-found/primary guards, the fact that public delete disables the generic bound-profile guard, transactional registry/selection mutation, post-commit profile-data cleanup with `IO_ERROR`, runtime reconciliation, and refreshed profile-list success; destructive execution remains fenced on owner-excluded authorization state. R8-063 closes native `profile_enable_set` as capacity reconciliation: required `profileIds` array, capacity domain 1/2/5, primary/selection invariants, per-profile enabled/`license_capacity` locking, selected-profile repair and refreshed list result; execution remains fenced because capacity is owner-excluded authorization/license state. R8-064 closes native `profile_create` authorization-derived capacity rejection, 16-byte unpadded Base64URL ID generation, next-order `账号 N` naming, exact enabled/unlocked/non-primary row defaults, 13-field created-profile success and separate frontend `profile_select(created.id)` behavior; execution remains fenced because native `maxProfiles` is entitlement-derived and exact post-create runtime failure cleanup remains unclosed. City Layout validate/apply-start/cancel providers, Hotkey actions, live Squads/AFK/Resource actions, generic Automation configure/start/stop and Dispatch Assist live state/actions are now audited/fenced rather than unknown; remaining profile registry mutations and original proxy install/backup/restore plus launcher lifecycle, full feedback/updater execution behavior, exact log rotation/segment retention, and remaining Mini-game handlers remain incomplete.
7. Exact reference-vs-rebuild behavior validation across connected/live states.

## Map status under the new goal

The current Map acquisition implementation is still a reconstruction, not a recovered copy of the original protected traversal algorithm. R8-012 closes the Manual `map_scan_start` kind/mode/error/UI boundary; R8-013 closes the shared status/Stop lifecycle; R8-014 closes public `map_search` eight-kind/filter/query-builder ownership; R8-015 restores the original Auto Scan frontend scheduler/control state machine byte-for-byte; R8-016 restores Scheduled Plunder list/schedule/cancel persistence, events and original result-tab UI while protected action execution remains absent; R8-017 removes three unsupported alternate-sort guesses and makes Monster distance, City shield and Railway quality fail closed until their native expressions are recovered; R8-044 restores the read-only `game_asset_image` public boundary and native-style profile-runtime PNG cache, while the exact cache-key preimage remains unclaimed. R8-076 closes the original host event/normalization/staging split between run-scoped `map.records` → `scan_records` and passive `map.native.capture` → `map_records`. R8-079 closes the native-capture ACK fields as constant empty/zero; R8-080 closes the outer `XluaBridgeMapScanTick` gate as a 50 ms `GetTickCount64` minimum interval; R8-081 closes the shared 1024-item point/march/removal drain; R8-082 closes native-capture 16 ms service plus 250/1000 ms active/idle flush timing; R8-083 closes exact point/march/train item representation; and R8-084 closes safe-region hook routing, canonical point/march queue identity, `pointIds` removal parsing, resolved producer field/getter inputs and the shared 65,536 pending-record ceiling/drop boundary. Complete multi-sort details plus protected traversal, per-tick request/work pacing, retry/backoff, remaining per-field population/transformation semantics, any separate protected script-level acknowledgement mechanism, travel, and action internals are still partial/unknown.

The R7-151 wide-FOV work demonstrated why coverage metrics are insufficient: a scan could report full logical coverage while returning only a fraction of Player Cities. The old traversal still found roughly the expected full population.

Therefore no R7 performance optimization is accepted as original parity unless tied to reference evidence.

## Protected package status

Earlier R8 work recovered substantial package/crypto structure, but the remaining LWKE1 field map/AAD is evidence-limited and requires a genuinely new permitted artifact/source. Do not keep repeating the same searches, cross the protected boundary, or expand this lane into Account/Login/Authentication recovery.

The package lane is parked while the main researcher finishes the original Map acquisition engine and then the remaining original Home lifecycle. It may resume earlier only when new permitted evidence directly blocks those priority surfaces, or later when genuinely new permitted evidence exists.

## Completion rule

The current whole-program parity matrix is `docs/lwbridge-parity-matrix.md`.

A final release requires every required retained original feature to be classified as exact or proven equivalent, no unexplained rebuild-only deviations in retained scope, and a working current-client product.
