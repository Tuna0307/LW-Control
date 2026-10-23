# LW-Control LwTool improvement programme final qualification

Date: 2026-10-23

Branch: `codex/lwtool-full-improvement`

Runtime/tool checkpoint qualified: `b2c94dc4`

The commit that adds this receipt is documentation-only. No `src/`, feature Lua,
package helper or benchmark implementation changes follow the checkpoint above
before source-pinned packaging.

## Full regression

All commands were run from the repository root on Windows unless noted.

| Gate | Result |
| --- | --- |
| `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore` | PASS, `HOME_FEATURE_DELIVERY_CHECKS_OK`, 64.735 s |
| `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check` | PASS, 53.606 s |
| `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:release-ui` | PASS, 3 viewport/language variants, 16 conditional states, 41 screenshots, zero errors |
| `python tools/feature018_all_lua_checks.py --original-dir artifacts/feature018-r30/current-original-v27 --output artifacts/lwtool-improvement-final-all-lua-b2c94dc4` | PASS, `FEATURE018_ALL_INERT_SUITES_PASS count=49` |
| `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release --no-restore` | PASS, `HOME004_R3_R1_ISOLATED_PIPE_RECOVERY_OK` |
| `python tools/lwtool_full_improvement_package_checks.py` | PASS, 2/2 programme package-boundary tests |
| `python tools/feature018_r30_package_checks.py` | PASS, 8/8 inherited package/privacy boundary tests |

The Desktop suite output includes the current Map control/publication/reopen,
all-server clear, mixed-kind, resource hydration, source-completeness, bounded
lens, camera-drift, Railway, installed-name and Map UI/native contract checks.
There is no separate tracked `LWBridge.Map-0.3.17.Checks` project; an attempted
standalone path was a command mistake and was not treated as a product failure.

The full UI run initially failed because local `node_modules` had lost `jsdom`.
`npm.cmd --prefix src/LWBridge.UI-0.3.17 ci` restored exactly the lockfile-pinned
dependency tree (113 packages, zero reported vulnerabilities), after which both
the full UI and release-UI checks passed. No package version or source changed.

## Distinguishing changed-path checks

- Adapter lease: fresh exact-session admission; bounded cache path; immediate
  same-instance lease deletion rejection; stale replacement rejection; successor
  identity fence; due sharing contention fails closed.
- AFK: 4 enabled profiles x 4 free squads, no target, one Pump: 16 -> 1 world
  march enumerations, zero sends; engine lifecycle/no-replay suite also passes.
- UI: five-second status/proxy watchdog retains owner generation while skipping
  redundant config reads; status-only polling cannot retire a config-bearing
  generation; hidden retained Map route no longer polls summary.

## Transport stability

The existing production-host probe passed:

- five unauthenticated/malformed handshake rejections without listener loss;
- authenticated malformed-frame session retirement;
- real 30-second authenticated idle timeout without shared-listener loss;
- reconnect with higher generation and stale-generation removal fence;
- simultaneous two-profile authenticated RPC isolation;
- ordinary authenticated RPC round-trip;
- pending RPC retirement on explicit host shutdown;
- wrong executable-image rejection.

The separate programme inert RTT/soak harness measured 500 authenticated
read-only echoes at P50 0.211 ms / P95 0.3472 ms / P99 0.478 ms, followed by a
60-second heartbeat-only route with zero failed sessions. This is host/pipe
evidence only and is not relabelled as connected-game performance.

## Safety and scope

- Game launches during the programme: **0**.
- Gameplay/account-changing actions during the programme: **0**.
- Competitor idle benchmark: **not run**. Active recovered LwTool source can
  auto-start AFK/chat on connect/late-connect, so any future live idle benchmark
  must first disable those through supported controls and prove the resulting
  admission. That prerequisite was never bypassed.
- `.claude/` remained unrelated/untracked and untouched.
- No competitor payload, credential, saved session, license/token, account
  identity, memory dump or user-specific capture is included in Git or intended
  package payloads.

## Remaining evidence limits

The programme does not claim an equivalent live LwTool-versus-LW-Control winner
for cold/warm Start-to-connected, connected-idle CPU/RAM/disk/IPC, real
adapter/game RTT or game process cost. No task-owned background-safe connected
pair was available. Those cross-product figures remain UNKNOWN rather than
being inferred from source structure or an unauthenticated launcher.

Direct adapter-to-Lua queue replacement, long late-connect retention, Map
request pipelining, cross-engine squad reservation and metadata-only proxy hash
caching remain deferred/rejected for the concrete correctness reasons in
`FULL_PRODUCT_METHOD_MATRIX.md`.

## Packaging boundary

Final Windows packaging must be performed only after this documentation-only
receipt is committed and the exact branch head is pushed. The programme
packager then requires:

- exact branch and exact `origin` SHA match;
- no tracked changes and no unexpected untracked content other than `.claude/`;
- PE ProductVersion ending in the exact source commit;
- canonical ProductionUi source/artifact fingerprint;
- source-equal linked OverviewBridge files;
- tracked `src/` + `tools/` source digest;
- proof/test UID, private-data, research/evidence and competitor-payload gates;
- per-entry SHA-256 + size manifest, ZIP CRC replay and external package receipt.

Independent lead acceptance, main merge and public release remain separate and
are not performed by this programme.
