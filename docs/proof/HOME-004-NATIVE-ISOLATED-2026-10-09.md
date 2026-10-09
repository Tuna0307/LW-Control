# HOME-004 — native isolated Home receipt (2026-10-09)

Proof type: **normal Windows Release executable, genuine production WebView2
Home controls and native profile/config persistence**, plus a separate
isolated-inert packaged fixture capture. This is a new observation of the
current production binary, **not** an execution of protected LWBridge 0.3.17.

Reference SHA-256 verified: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Source: `codex/home-complete-delivery-004` pre-release working build on
`origin/main` ancestor `0f0a435c5804db84107af3980f68686248e78775`;
the final candidate is rebuilt from the committed source separately.

## Preparation and identity

- Used `tools/home_004_safe_profile.py` to create a *fresh* ignored directory
  `artifacts/home-004/isolated-normal`; intentionally seeded one disabled
  registry owner `home-004-disabled-a` (`enabled=0`, primary=1). The isolated
  native config initialized `autoLaunchGame=false`, no game root or desired
  game, no reconnect. No previous profile file was overwritten.
- Inspected normal-app `--isolated-root` path separation, startup ordered
  registry reconciliation and the skip for disabled/locked profiles before
  launch. No LastWar or LastWarLauncher was running at the preflight.
- Opened **normal packaged application**, with exact executable beneath
  `artifacts/home-004/preflight-publish/`, PID **30372**, Home view. The
  shipping WebView preference was visibly **ON** at first launch despite
  `autoLaunchGame=false` in native config; the disabled registry ensured
  no game was launched. This is a concrete reminder that native config false
  alone does not disable the original local WebView startup default.

## UI operations and native responses

| Normal Windows Home control | Observed result |
|---|---|
| Initial Chinese/Simplified light Home | Game stopped, Launch enabled, Close disabled, Auto Launch ON, Automatic Reconnection OFF |
| Click actual Auto Launch switch | Switch displayed OFF; isolated `config.json` contained `autoLaunchGame: false` |
| Select English | Home displayed `Game stopped`, `Launch Game`, `Close game`, `Open games at startup: Disabled`, `Automatic Reconnection: Disabled` |
| Toggle Automatic Reconnection ON | Native app accessibility displayed `Automatic Reconnection: Enabled` |
| Toggle Automatic Reconnection OFF | Native app accessibility displayed `Automatic Reconnection: Disabled`; isolated native config `autoReconnect: false` |
| Select Japanese / switch to dark | Actual WebView screenshot and accessibility displayed `ゲーム停止中`, `ゲームを起動`, disabled `ゲームを閉じる`, `起動時にゲームを開く: 無効`, `自動再接続: 無効` and dark theme |
| Click native window Close | PID **30372** exited; no game/launcher process |
| Relaunch the same isolated native profile | New app PID **48528** reopened **Japanese/dark**, both Home switches OFF, stopped state, Close disabled; no game or launcher appeared |
| Close reopened app | PID **48528** exited; `profiles` registry remained `('home-004-disabled-a',0,1)`, both native booleans remained false, no game/launcher process |

`tools/check_packaged_home.ps1` against the same freshly published binary
also returned `ok=true`, mode `isolated-inert-capture`, exit code **0**, and
**0** temporary roots remaining. The actual published `ProductionUi`
integrity check passed (canonical UI hash
`cc7ce094a249a68e32fd4e1ba1efaf69bd39a8a9c4e261db2b0e4aa2d2a88af6`).
All task-specific raw images/config/registry/cache are ignored under
`artifacts/home-004/`. No authentication tokens, live gameplay IDs or
private datasets were copied into Git.

## Scope and limits

This receipt is **native positive proof** for mounted Home preference toggles,
language/theme, isolated startup, reopened persistence and process cleanup.
It is **not** a new live Launch→authenticated Connected→Close witness.
The earlier lead-accepted single-profile witness and verified exact script
restoration remain cited separately in `docs/HOME_LAUNCH_LEAD_ACCEPTANCE.md`;
no owned game or installed script was touched during HOME-004's new native
run, so no new restoration is asserted. Distinguishing native tests cover
the local Start/reconcile/repair *controlled* error cases; absent live
original lease/ticket/finalizer, same-build restart/adoption, adverse
repair/recovery/reconnection and simultaneous multi-owner process proof
remain **PARTIAL**. See `docs/HOME_004_CONTRACT_MATRIX.md` for every row.
