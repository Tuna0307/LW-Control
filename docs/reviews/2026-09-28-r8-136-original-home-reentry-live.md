# R8-136 — untouched original Home re-entry live

**Date:** 2026-09-28
**Status:** LIVE-PROVEN ORIGINAL HOME SHELL RE-ENTRY / INNER PROFILE CONTEXT STILL LOADING

## Correction

The project already had R8-124 live proof that the untouched original Home shell could be entered with a reversible process-memory-only native admission hook plus the original frontend auth-state event path.

This checkpoint re-ran that exact route from a clean process and confirmed it still works.

## Live route

Untouched original:
`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

CDP:
`--remote-debugging-port=9229`

Native admission hook:
- target RVA `0x23DE50`;
- original bytes `48 8B`;
- temporary bytes `EB 68`;
- effect: branch directly to accepted native admission path at `0x23DEBA`;
- EXE on disk unchanged.

Fresh live process:
- PID 24660;
- image base `0x7FF78A320000`;
- bytes before patch matched `48 8B`;
- bytes after patch `EB 68`.

Frontend auto-launch was set to false before the observation event.

The original Tauri event plugin then emitted:
`bridge://auth-state`
with an observation-only authorized payload.

## Genuine original Home shell

The untouched original frontend switched from the Login screen to its real application shell.

Freshly observed navigation:
- 首页;
- 自动化;
- 地图数据;
- 小队/挂机;
- 内城编辑;
- 快捷键;
- 小游戏;
- 设置.

Account control displayed observation expiry `01/01/2099`.

## Native command result

Native `auth_state` remains honestly:
- `phase = signedOut`;
- username empty;
- accessRole normal;
- errorCode null.

With the memory-only admission hook active:
- `profile_list` succeeded;
- real profile `wHR5C9cTgD0aC_2XegklLA` returned;
- displayName `主账号`;
- selectedProfileId matched the same profile;
- maxProfiles = 1;
- `profile_instances_reconcile({autoLaunchAll:false})` succeeded with no errors.

Therefore original Home shell reachability does not require solving SessionV2 first for observation purposes.

## Map tab observation

The genuine original `地图数据` navigation button was clicked successfully and became `active`.

However the page body remains only:
`处理中`

No Map tabs/controls are instantiated in the live DOM yet.

Clicking the original top-level `刷新状态` action did not clear that state.

This matches R8-124: the remaining frontend blocker is the profile-view/provider initialization state, not the ability to enter the original shell.

Fresh screenshot:
`evidence/lwbridge-implementation/2026-09-28-original-map-tab-r8136.png`

## Current boundary

- untouched original application shell: **LIVE-PROVEN / REACHED**
- original Home navigation: **LIVE-PROVEN**
- original native profile commands behind admission helper: **LIVE-PROVEN**
- native AuthState itself: **signedOut**
- original Map navigation selection: **LIVE-PROVEN**
- original Map inner controls: **NOT YET REACHED**
- profile-view context: **still processing**
- original Map engine: **NOT WORKING through this observation route**

## Next continuation

Do not spend another block solving legacy session crypto merely to enter Home.

The next direct target is the retained frontend profile-view/provider condition that renders `profile-switch-loading`. Recover the exact state variable/command/event required to clear it while the R8-124 admission hook is active, then observe the genuine original Map controls.
