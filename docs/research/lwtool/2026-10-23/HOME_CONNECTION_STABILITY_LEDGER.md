# Home connection stability ledger — 2026-10-23

This ledger records the owner-priority Home investigation assigned by
`docs/work-items/LWTOOL-HOME-CONNECTION-STABILITY.md`.  It intentionally keeps
credentials, account identifiers, pipe tokens, challenges and recovered private
competitor payloads out of Git.

## Identity and restoration baseline

- Released comparison baseline: `main@45e151c8`.
- Previous completed LwTool-improvement checkpoint:
  `0cdf4de8daa9d45f8b1db808c0081801044541cf`.
- Home-stability assignment started from
  `98af12e2b762a5e830b84d8088dc134332da950e` on
  `codex/lwtool-full-improvement` / draft PR #16.
- Current official `LastWar.exe` SHA-256:
  `905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d`.
- Current official `LastWarLauncher.exe` SHA-256:
  `bf924a1b06348cb2c2c58eebac8a780eb3663e5b13957bcc9a83602ece7b41fb`.
- Current xLua SHA-256:
  `d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45`.
- Fresh pre-test official script restore baseline:
  - `LWScripts.data` `8f61102f401f3052598365ca9750c2c496969895b5add2da138cb2147d045493`
  - `LWScripts.txt` `32dbdf2762a8658a0c803713ca9856042938277c820288f419ebf546928d4095`
  - `version.txt` `670671cd97404156226e507973f2ab8330d3022ca96e0c93bdbdb320c41adcaf`
- The older HOME-004 October 9 script fixture is deliberately not a restore
  source for this assignment.

## Source diagnosis before live tests

The production host route is authenticated and generation-scoped.  Ordinary
transport teardown removes only the current connected generation while retaining
the claimed registration, so the same exact session can reauthenticate with the
same launch identity.  The older F-04 test hook deliberately unregistered the
identity and therefore represented forced replacement, not an ordinary EOF.

The original current-client bridge coupled two different liveness questions:

1. Lua/game-thread work wrote `heartbeat.json` roughly every 0.75 s.
2. Lua also serialized a named-pipe heartbeat every five seconds into a numbered
   disk mailbox; the managed adapter polled the mailbox and forwarded it to the
   authenticated host transport.

The host retires a route after roughly 30 seconds without frames.  Home strict
readiness additionally requires an exact-session heartbeat no older than five
seconds.  This made a short Lua publication/game-thread stall capable of
presenting as a bridge disconnect even while the route and game remained healthy.

## Baseline connected observation

The fresh isolated baseline reached final game/account health in 70.271 s from
desktop-host start.  A 900-second unperturbed observation then produced 901/901
healthy samples:

- one exact game process throughout;
- authenticated route present in every fresh monitor sample;
- bridge online in every fresh monitor sample;
- game/server health true in every fresh monitor sample;
- heartbeat age max 0.854 s, p95 0.798 s;
- zero sampled `connect to server failed`, `E005`, `Loading error : E109` or
  `OnLoginError` markers.

**The owner's natural intermittent disconnect did not reproduce during this
baseline.**  No artificial fault below is claimed as proof of the spontaneous
root cause.

## Controlled heartbeat/publication stall

A bounded heartbeat-publication stall held the exact game process and local pipe
healthy while heartbeat age reached 13.316 s.  The first failed predicate was
`lua_heartbeat`; the adapter remained connected, the authenticated route remained
present, and no game login/server failure marker appeared.  Strict Home bridge
readiness became false and recovered when publication resumed.

A separate exact-handle 12-second suspension reproduced the same separation:
heartbeat age reached 12.741 s while the same process, adapter and authenticated
route remained intact.  Resume returned the same process to ready/logged-in/
server-connected health with no matching network/login error.

These tests demonstrate a false-disconnect presentation boundary.  They do not
prove that the owner's natural event is caused by the same stall.

## Controlled local route loss — five same-game cycles

The stability-only route fault was corrected to retain the claimed registration
and to require that the in-game adapter actually observe the broken pipe before a
cycle can count as recovered.  Five live cycles all stayed on the same exact game
PID.  The first observed failure in every cycle was the managed adapter
(`PeekNamedPipe` class), followed by authenticated-route loss on the host monitor.
The game heartbeat remained fresh and game/server health remained true.

Observed adapter-fault to full local reconnect times were:

`1.245006, 1.272854, 1.231089, 1.248673, 1.266387` seconds.

Median: 1.248673 s.  Maximum: 1.272854 s.  No account-changing command was sent
during the cycles, and strict request/reply at-most-once/generation fencing is
covered separately by the direct-queue transport checks.

## Whole-game recovery and cancellation

An exact-handle owned game exit verified PID, path and 100 ns process-creation
identity before termination.  Recovery classified `processExit`, launched one
new exact owner and reached fully healthy state in 26.031 s.  No image-wide kill
was used.

Stop was also exercised after a retained-registration pipe fault had become
visible to the adapter.  The final candidate additionally exercised the atomic
retained-route-drop + production Stop hook.  Production Stop cleared
desired-running state, retired the route, exited the exact game, left no successor
owner, and restored all three official script files byte-for-byte to the fresh
backup.

## Implemented candidate mechanisms

### Corroborated presentation state

Strict command authority remains unchanged: exact process + authenticated route +
ready heartbeat within five seconds.  A new presentation-only grace, bounded to
15 seconds, can show `checking` instead of `Bridge disconnected` only when the
same exact process identity and authenticated route still exist and a previously
validated exact ready heartbeat is temporarily unreadable/stale.  A readable
wrong identity or `ready=false` heartbeat defeats the grace immediately.  This
state never changes command admission.

### Direct bounded adapter↔Lua exchange

The managed adapter now exposes bounded generation-scoped queues that xLua calls
only from the already proven game-thread pump.  The managed pipe worker never
invokes Lua/game APIs.  Host→Lua and Lua→host queues preserve FIFO and strict
request IDs, use the recovered 4096-item / 64 MiB / 32 MiB boundaries, cap frames
at 8 MiB, fail the current generation on overflow, wake the pipe worker with an
event, and reject stale-generation publish/retire operations.  The numbered-file
mailbox remains a compatibility fallback if all direct delegates are not present.

The transport heartbeat intentionally remains Lua-authored.  The adapter does
not synthesize liveness independently, so a stalled game thread is still visible
rather than being hidden by a healthy background worker.

The live candidate reports `adapterReadMode=direct`, `directCapable=true`,
`directActive=true`, an authenticated connected route and a fully healthy
game/account.  The final 30-minute soak had direct mode active in all 1801
samples and zero numbered inbound/outbound mailbox backlog; the only
`pipe-*.json` files on the direct path are bounded diagnostics.

## Final candidate live qualification

- Cold host start→healthy: `62.295 / 32.302 / 33.594 s`.
- Warm same-host Start→healthy: `24.937 / 24.516 / 25.656 s`, median
  `24.937 s`.
- 30-minute soak: 1801 samples, one exact game PID, no sampled route or
  game/server failure and zero matching login/network log markers.
- One natural single-sample heartbeat read/publication transient occurred at
  1793.234 s; it recovered on the next sample with the exact process, route,
  direct transport and game/server state intact.  This is not promoted to the
  owner's visible intermittent symptom.
- Five final candidate route faults all kept the same exact game.  The managed
  adapter failed first, then the host route.  Adapter-fault→reconnect times:
  `1.272470, 1.230109, 1.256323, 1.279593, 1.258315 s`; median
  `1.258315 s`, max `1.279593 s`.
- Candidate 12 s exact process pause: `lua_heartbeat` first; max heartbeat age
  `12.841 s`; route remained authenticated; no matching login/network marker.
- Candidate exact process exit: new exact owner, changed session + creation
  identity, healthy in `41.717 s`.
- 20 s pre-ready suspension: same exact reported PID/path/start/session reached
  healthy state after resume.
- Atomic Stop during retained-route loss: exact process exited, desired-running
  false, no successor, byte-exact official script restoration.

## Read-only transport timing

An isolated real-Windows-named-pipe probe uses the production authenticated host,
strict request/reply correlation and a read-only `getStatus({})`-sized workload.
For 300 measured calls after 20 warmups it recorded p50 0.192 ms, p95 0.313 ms,
p99 0.659 ms and max 1.105 ms.  This is **host transport RTT only**; it is not a
claim that the live game-thread/direct-queue round trip has the same latency.

## LwTool comparison boundary

Recovered authenticated LwTool evidence shows the useful mechanisms being
compared: retained same-process Route-C sessions, late connect polling of an
existing game for up to roughly 600 s, same-process self-heal after IPC probes,
and native/shared-memory transport with correlation.  Its broad image-wide Home
Stop remains unsuitable for LW-Control.

A live LwTool idle comparison is blocked by the assignment's own prerequisite:
recovered production UI says AFK/chat are auto-started on connect and the old
buttons are removed.  Chat can be stopped through a free supported call, but AFK
Stop is Premium-gated and is restarted again after late-connect/self-heal.  A
non-Premium run therefore cannot prove AFK is disabled without bypassing a paid
gate; doing so is prohibited.  Offline competitor evidence is retained instead,
and LW-Control live qualification continues.

## Final inert regression status

- full Desktop checks: `HOME_FEATURE_DELIVERY_CHECKS_OK`;
- direct queue + real inert direct/fallback named-pipe checks: PASS;
- isolated authenticated pipe recovery/reconnect/stale-generation checks: PASS;
- Lua checks: 49/49 PASS;
- UI full check: PASS;
- release UI: 3 viewport/language variants, 16 conditional states,
  41 screenshots, 0 errors;
- Home owned-fault harness: 6/6 PASS;
- programme/R30 package-boundary checks: PASS;
- `git diff --check`: PASS (line-ending warnings only).

Remaining boundary at this ledger revision:

- independent lead review.

## Final source-pinned package receipt

- pushed source commit:
  `eefa29e4571b28dfcb7a6f7300865b76d9672123`;
- package SHA-256:
  `84605da8b97d176c945ce3cfaec201e0bbcae365238c0c26988b0c8b7c7cae0b`;
- package entries: 105; size: 10,360,495 bytes; verified bridges: 29;
- clean-extract inert Home capture: PASS;
- clean-extract disabled-profile normal-release smoke: PASS, zero new game
  processes;
- packaged production Start→healthy: `44.312 s`, direct mode active, no
  numbered mailbox files;
- five packaged same-game route recoveries:
  `1.248597 / 1.276668 / 1.277956 / 1.281073 / 1.268071 s`, median
  `1.276668 s`, max `1.281073 s`;
- packaged atomic Stop-during-reconnect: PASS; exact old owner exited, no
  successor, byte-exact official script restoration;
- target LW-Control/LastWar test processes closed after the final receipt.

Package status remains `INDEPENDENT_REVIEW_REQUIRED`; there is no main merge or
release authorization in this work item.

No merge to `main` and no release are part of this work item.
