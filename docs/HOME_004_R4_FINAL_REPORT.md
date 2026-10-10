# HOME-004 R4 — final worker delivery and lead handoff

**2026-10-10 — Bounded implementation READY FOR INDEPENDENT LEAD REVIEW;
whole Home PARTIAL.** Project lead alone decides acceptance. Do not merge,
publish or represent the replica as a fully original-0.3.17 Home feature.
Earlier R1/R2/R3/R3-R1 negative records and separately accepted genuine native
Launch/Connected/Close, automatic startup, same-build adoption, unexpected exit
and recovered hung-game success are preserved unchanged.

Source base was `13615874c4261f58f60222c97544633e6fb432b1`, branch
`codex/home-complete-delivery-004`, draft [PR #6](https://github.com/Tuna0307/LW-Control/pull/6).
Final commit, source-identified ZIP path/hash and extracted startup receipt
are recorded in the draft PR after the final release passes.
The authoritative [original contracts](HOME_004_CONTRACT_MATRIX.md) and
[47-row R4 next-action queue](HOME_004_R4_QUEUE.md) remain the complete Home
scope; the appended [current matrix](HOME_004_R1_CURRENT_MATRIX.md)
identifies changed H rows and does not rewrite historical failures.

## Corrected functional paths

- **H-41**, `OverviewLifecycleService.RebindGameRootSelection`,
  `StartAsync`: baseline pending/unknown restoration journal incorrectly
  blocked a new configured folder from being saved. The saved next selection
  is now staged, the active old process/root/journal remains captured, and a
  new Start is blocked until uncertain old-root restoration is resolved.
  Controlled native proof verifies pending journal save, blocked premature
  Start, old owner untouched and subsequent Start at the new folder. Mounted
  production A-running→select next B root→Close A→Start B also passed.
- **H-38/H-39/H-40/H-22/H-32/H-37/H-42/H-45**,
  `LWBridgeWindow`, `ProfileRuntimeOwner`: actual production owner
  registry retains a separate lifecycle/backend/config/runtime/evidence/
  backup root per profile across Home selection. A can remain active while
  B is selected and independently stopped; B reconnect preference saving
  leaves A unchanged; returning to A retains the original exact instance.
  Ordered reconciliation now reaches all enabled registered owners. Closing
  the host shuts down every retained monitor and attached native service.
  A delayed A-status response after selecting B is fenced from the B Home
  view. No artificial profile capacity/entitlement was added.
- **H-27/H-29/H-38**, `LWBridgeControlPipeIsolatedAcceptLoop`,
  `LWBridgeControlPipeRegistry`,
  `LWBridgeControlPipeIsolatedHandshake`,
  `LWBridgeControlPipeHostState`: a discovered production blocker left the
  single shared pipe listener waiting for A's entire long-lived RPC session
  before accepting B. Session ownership and cleanup now run independently,
  with a continuing shared accept loop and exact generation-bound teardown.
  The registered expected executable is bound to each matching
  profile/session/token; the existing strict OS client PID/build/path
  handshake remains enforced. Concurrent B RPC while A remains connected
  passes through actual isolated Windows pipes. The listener's general
  same-build and old-owner reconnect rules remain.
- **H-06/H-09/H-46**, real current `tools/run_overview_bridge.py`
  producer and `OverviewLifecycleService`: official-launcher
  `Popen` OS failure is typed `LauncherSpawnError` and maps to
  `LAUNCH_TASK_FAILED`. Update failure, PID timeout, unexpected script error
  and original typed self-restart are not conflated. No retry was invented
  without the original typed restart event.
- **H-34/H-35/H-36/H-40/H-47**, actual lifecycle recovery service:
  an explicit user Stop now invalidates a pending recovery launch with no
  currently owned game PID, clears `GameDesiredRunning`, and prevents a late
  successful helper from publishing or adopting a process. The native
  controlled test holds an actual recovery helper completion past user Stop
  and requires exact restoration and no further attempt.
- **H-18/H-19/H-20/H-45**: existing strongly typed repair/adoption producer
  and mapped Home button are exercised from the mounted production App in
  both required locales. The observed repair is an **inert controlled
  producer**, never called a genuine outdated-client update.

## Verification execution and evidence classification

The following checks ran through the exact source paths named above:

| Verification | Observed result | Evidence level |
| --- | --- | --- |
| `dotnet run --project tests/LWBridge.Desktop.Checks -c Release` | Home Launch negatives/root change, typed R1 repair, R2 monitor including held late cleanup and R4 pending Stop, ordered profile backend routing, R4 token-bound different-image/profile/expiry/generation identity, and unchanged Map stored-data regressions all PASS; **game launches 0** | Production/native code with isolated inert process providers |
| `python tools/test_home_r4_launcher.py` | OS `Popen` failure type/cause, successful unchanged result and unrelated exception classification PASS; no launcher spawned | Actual helper producer, controlled OS process-creation boundary |
| Actual Windows `tools/home_004_pipe_recovery_probe` | Five unauthenticated rejections, authenticated malformed frame, real 30 s idle expiration, same listener recovery, higher reconnect generation, two simultaneously connected profiles, B RPC while A connected, exact shutdown and invalid image rejection PASS; **game launches 0** | Real named pipes with inert client processes |
| `artifacts/home-004/r4-final-en-light.json` and `r4-final-ja-dark.json` | Actual packaged production App/WebView Home, two inert independent production owners, original A retained after B selection/Close, B own setting, delayed A reply, A running-root selection/old-root Close/new-root Start and Update-and-Launch pass; captured full-frame connected screenshots, clean owned shutdown | Mounted real UI/backend controls; **inert game/producers** |
| Frontend `npm.cmd run check`, `check:production-build`, `check:release-ui` | Production Home integration, unchanged Map and UI draft checks, translation keys, shipped UI consistency | Canonical production frontend |
| Windows Release publish / source-identified ZIP / extracted application | See PR/package receipt for exact commands, commit and SHA after successful final pass | Actual Windows executable and packaged runtime; disabled isolated real window where applicable |

The R4 screenshots demonstrate the real mounted shell and independently
selected profiles; they are controlled Connected states, **not** official
Last War authentication evidence. Existing accepted live game receipts are
unchanged and were deliberately not rerun. Historical R3 unsuccessful
attempts still failed; R3-R1 later independently accepted hang success
supersedes only that specific earlier recovery gap.

## Exact remaining inputs and follow-up

1. **H-05/H-13 and H-38 capacity:** verified original 0.3.17 protected
   entitlement/ticket/lease/finalizer callback responses, legitimate
   maxProfiles decision and supported simultaneous-client admission.
   Recovered SessionV2/EntitlementResponse field names are a schema,
   **not** proof of a successful entitlement.
2. **H-07/H-08/H-09:** original 0.3.17 typed
   `OFFICIAL_LAUNCHER_RESTARTED` producer, helper mutex/descriptor and
   current official updater/launcher self-restart traces; then determine
   exact retry and error serialization. An OS `Popen` failure is a
   distinct failure and cannot trigger the original retry rule.
3. **Controller module table:** a legitimate signed
   `package-key.envelope` and matching persisted Windows CNG private key
   are absent from the supplied 0.3.17 authorized artifacts. Without
   them the encrypted original Lua controller plaintext cannot be
   recovered or fairly claimed. No protected service or credential was
   accessed.
4. **H-27/H-29 transport original:** 30-second adapter reader timeout
   derives from historical 0.3.1 and is **not** source-confirmed 0.3.17;
   exact original timer/service control path still needs original authority.
5. **H-20/H-34–36/H-40/H-43/H-45/H-47 genuine adverse cases:**
   real supported original/current outdated-build Update-and-Launch,
   same-profile pending Stop during official recovery, still-alive
   offline-only loss, prolonged genuine failure/maintenance and full
   conditional error/busy/pixel original-vs-native evidence. Controlled
   long-duration clocks, inert repair, and existing successful genuine
   hung-game recovery do not supply these different witnesses. Do not
   manufacture old client versions or disable networking to force one.
6. **H-41 original status negative:** paired 0.3.17 root-picker while
   running/pending restoration on a compatible independently backed-up
   second installation. R4 already verifies the corrected current behavior
   in production routing and controlled native seams.

Before any new live Home experiment verify compatible client build,
isolated original A/install, exact user-owned PID/session, script triplet
backup/hash and journal/adoption safety, finite restoration procedure and
scope authorization. No R4 genuine-game fault was induced. Do not rerun
the accepted hang success merely for coverage.

## Restoration, change control and next handoff

R4 inert proof uses disposable per-run isolated configuration, two named
inert profile owners and system-file fixture copies under that root. The
mounted proof JSON documents removal of those roots, drained UI requests
and detached native listeners. Native image-path checks create two
temporary stub executables and remove their exact test root on completion.
No owner game installation, protected original credential, updater,
published release or Map scanner was touched. Task-specific ignored
build/proof artifacts are preserved for independent review; no prior
negative evidence or unrelated work was deleted.

**Next lead action:** independently review the complete R4 diff, verify the
source commit and extracted package hash, repeat the real Windows pipe
two-session probe and mounted Home controls if desired, and accept only
the bounded implemented cases. Continue the per-row
[R4 action queue](HOME_004_R4_QUEUE.md) when the listed original or
genuine-client dependencies become legitimately available. PR #6 stays
draft and whole Home stays **PARTIAL**.
