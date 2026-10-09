# Home Launch delivery â€” independent lead acceptance, 2026-10-09

Accepted for the supported single-profile **Launch â†’ authenticated Connected â†’ Close** feature after a source-backed correction and an independent live repeat. PR #4 is the delivery vehicle; full Home/Map original-runtime parity remains incomplete.

## Original Close correction

The worker correctly changed an explicitly wrong instance string to `INSTANCE_MISMATCH`, but incorrectly retained a mandatory-string requirement. Hash-gated original 0.3.17 disassembly shows the optional-string extraction at `0x199AC4â€“0x199B38`; `0x199BB0` preserves the optional pointer; `0x199D4B` bypasses comparison when absent; `0x199D50` and the call to `0x7A3E30` compare a supplied string; `0x199D78` supplies `INSTANCE_MISMATCH` on mismatch. Missing/null/non-string identity therefore closes the captured current profile owner. An empty string is supplied and mismatches. No current owner remains `INSTANCE_NOT_OWNED`.

The lead corrected production `StopAsync` and tested omitted, null, number, boolean, array and object values through the actual native backend with inert helpers. All six close the exact captured owner; explicit stale/empty strings leave its desired state, root and process untouched. PID/path/creation/session and confirmed-exit restoration gates remain intact. This supersedes the missing-ID claim in the worker review without changing its historical receipts.

## Independent live repeat

The corrected Release application was run with its actual production WebView2 and native providers, with a separate task root and disabled setup profile. The real UI turned Auto Launch off before enabling that profile while the host was closed. The shipped default remains true. Initial hidden setup host 27632 had no visible window and launched no game; it was closed and visible setup host 50060 was used. Neither is counted as a game launch.

Host 4284's English/light Launch button started the official launcher. A transient game PID 5332 was followed by owned PID 4580, created `2026-10-09T10:01:58.6398322Z`. Original typed launcher-restart behavior is not inferred from that transition. The ready, heartbeat, adoption and recovery records matched profile, session, challenge, PID and creation time; the current pipe record reported connected and the heartbeat was fresh. Home displayed Connected on server 2212 in English/light and Japanese/dark.

The Japanese Home Close button exited that exact process. The native Stop receipt proves a signalled process handle before restoration. All three installed script hashes independently matched preflight afterward, the recovery journal disappeared, and the task host closed. No game, launcher or clone process remained. Automatic approval review rejected deletion of the isolated temporary test root; its backups are retained without retry or bypass. No Map scan, gameplay, updater or protected-original service action was performed.

One dropdown coordinate input was rejected by the computer-use target guard because the popup belonged to WebView2. The target was reobserved and the ordinary keyboard select succeeded. Initial language/theme screenshots during asynchronous acknowledgement were not treated as settled captures. The app required foreground reobservation after the game raised its loading window. These were tooling/transition observations, not hidden product passes.

Compact sanitized native receipts and three inspected screenshots are in `proof/home-launch-002-lead/`. Raw task-only connection records remain in the ignored lead artifacts; authentication tokens and game account identifiers are excluded from the committed receipt. The live repeat used the corrected lead working tree based on `6d8f4bdc`, not the unchanged worker ZIP. The subsequently committed identical production source is freshly published for release.

## Verification and limits

Canonical frontend checks and production UI integrity pass. Fresh Desktop and Desktop.Checks Release builds have zero warnings/errors; the actual-backend root/registry/Home checks pass, including the six optional-identity cases. The worker's extracted ZIP SHA was independently checked; the accepted release is rebuilt from the merged main revision and separately smoke-tested.

This accepts a usable current-client single-profile feature and the recovered contracts specifically exercised here. It does not certify every original launch/failure/recovery path, multi-profile entitlement, original protected complete-App pixels, Map scanner traversal or other native functions. Current-client bindings remain adaptations and require revalidation when the game changes.
