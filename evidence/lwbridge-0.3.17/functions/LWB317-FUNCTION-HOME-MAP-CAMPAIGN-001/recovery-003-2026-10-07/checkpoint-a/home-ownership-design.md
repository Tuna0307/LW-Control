# R3-01/R3-02 shared runtime identity transaction

Status: IMPLEMENTED_NOT_VALIDATED for actual current-game integration. Coordinator owns native build/test execution and final acceptance. This worker executed only isolated Lua checks in a task-owned Python environment; no native host/game/update/browser/helper entry point was executed by this worker.

## Design and evidence boundary

This is clone runtime implementation policy, not a recovered original .3.17 native protocol. Existing filenames and serialized fields remain unchanged.

`OverviewRuntimeFileOwnership.TryWrite` opens the current destination ReadWrite with FileShare.None. It validates session/challenge from the bytes read through that identity, writes/truncates/flushes through the same handle, then releases it. An absent destination uses CreateNew; a competing creator wins admission instead of being overwritten. Foreign, malformed UTF-8, duplicate or unverifiable metadata fails closed. This replaces atomic rename publication with exclusive publication: successful reads observe complete bytes; sharing contention must be retried by consumers.

`TryDelete` requests GENERIC_READ | DELETE, OPEN_EXISTING and no sharing. Exact ownership validation uses that handle. FILE_DISPOSITION_INFO (class 4, one-byte BOOLEAN DeleteFile) is submitted on the verified handle before close. No pathname DeleteFile call occurs after validation. The new test hook runs under the real guard and cannot substitute deletion. The old DeleteFile hook no longer intercepts runtime cleanup.

Windows supports these exclusion and same-handle semantics: [CreateFile sharing/access](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea) and [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle). A process mutex or a second unlocked pathname check cannot protect against another process's atomic replacement.

`tools/run_overview_bridge.py` now guards canonical control/lease publication and all owned shared cleanup, including old temporary filenames, with the equivalent CreateFileW identity transaction. Its former check-then-unlink and os.replace paths are removed from those operations. Invalid/foreign writes raise rather than reporting success. Non-Windows shared mutations fail closed. Other helper launch/install/restore behavior is untouched.

`PipeClientAdapter.LeaseIsFresh` retries only IOException Win32 sharing/lock violations 32/33 at the existing 10ms cadence. It never returns cached bytes as fresh. The retry ends at the prior validated timestamp's original five-second expiry, or after five seconds for initial read. Complete malformed, foreign, missing or stale reads return false. This is a necessary current-client consumer adaptation, not a claim about original timing.

## Isolated checks prepared

`HomeRuntimeFileOwnershipChecks` is invoked from ProfileRuntimeOwnerChecks. Its root is a child of that check's unique system-temp directory. It checks absent admission, owned refresh/deletion, foreign/malformed refusal, read exclusion, atomic replacement barriers during writes/deletion and preservation of a foreign identity after guard release. It loads only the packaged adapter's private file predicate: no Connect or pipe/game entry point. It checks valid contention, freshness expiry, and malformed/foreign/stale/missing rejection. A pending lifecycle uses inert official/helper/process seams and explicit temp runtime/evidence/backup roots to prove Close/cancellation cannot overwrite foreign cancellation metadata.

The existing deferred running profile test now uses actual isolated lease publication. It verifies active profile refusal, foreign lease preservation across periodic refresh and exact owned Stop cleanup; helper/process identity remains synthetic.

`tests/home_runtime_file_ownership_checks.py` stubs the imported live-resource module before loading helper definitions. It never calls overview_paths, run_start, run_close, package/update or game functions. Every runtime path is TemporaryDirectory. It verifies canonical write_control/write_lease, guard contention against os.replace/read, same-handle deletion, foreign/malformed refusal, JSON cleanup, cancellation preservation and non-Windows refusal.

## Lua consumers and executable proof

The initial source review found current_overview_bridge.lua Pump classified unreadable lease metadata as stale and abandoned actions; current_live_resource_probe.lua also rejected unavailable shared bytes. Coordinator explicitly expanded this lane to their lease/control read seams. The adapter now provides a read-only ReadRuntimeSnapshot delegate which classifies actual .NET IOException HResult 32/33 before xLua can convert an exception to an unstructured string. Lua never guesses that io.open returning nil means sharing contention.

Overview defers its pump only on typed busy status when an earlier fully verified same profile/session/challenge/PID lease remains inside its existing inclusive five-second expiry. Full new bytes must validate before work resumes. Foreign, malformed, missing and unknown failures receive no busy grace. It clears its verified timestamp and follows existing abandonment beyond expiry. There is no synchronous game-thread sleep.

Probe verifies shared identity before any queued input read/removal. Typed contention within the same owner's verified TTL returns false without consuming or executing requests. The successful preflight snapshot exists only inside that synchronous pump and is cleared on normal/error exit; nine request readers reuse it. Unavailable/expired metadata cannot authorize any lane, preserves pending files, and terminalizes all eight already admitted lanes through existing result/cleanup primitives. Resource detail/scan terminalization uses failed state with an explicit error; normal completion behavior remains unchanged. Retired asset callbacks retain their existing request-identity guard.

`tests/home_runtime_lease_lua_checks.py` executes both complete production modules, with unique temporary roots and in-memory file/CS delegates. Six checks pass under Lua 5.3, 5.4 and Lua 5.5 (lupa 2.8): busy control/lease deferral, same-owner TTL boundary/expiry, foreign/malformed/missing/unknown rejection, all queued inputs untouched during busy, resume after full valid publication, each of eight active lane cleanup/result signatures, actual late asset callback fencing, and error-path snapshot clearing. The real Windows busy classifier is separately exercised by the coordinator's passing native profile check; the Lua checks use the resulting typed status contract and do not claim actual xLua/native game binding proof.

## Limits

OS ownership protects admitted identities against cooperating and ordinary independent Windows file writers/replacers. It does not establish original native parity or live gameplay proof. A write/process crash can leave incomplete metadata, which is rejected on subsequent admission; no recovery fallback is invented. Coordinator native/Python reruns passed; see home-isolated-checks.md and profile-owner-corrected.txt. Lua 5.1 rejects the bulk AOI function's existing greater-than-60-upvalue shape; coordinator's baseline `138ea` parse confirms the same historical failure. That unrelated function was not changed in this lane. Historic repository bootstrap evidence names Lua 5.3 (`docs/reviews/2026-09-30-LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001.md:474-478` and `docs/lwbridge-map-scan.md:1426`); these are not an exact-current bytecode/header/version fingerprint. Exact current-game Lua engine version and actual xLua/adapter binding remain UNKNOWN in this offline lane.
