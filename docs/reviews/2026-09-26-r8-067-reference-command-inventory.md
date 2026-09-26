# R8-067 — exact frontend command inventory and production route audit

**Date:** 2026-09-26
**Classification:** exact reference-frontend inventory; production routing audit; complete native inventory still open.

## Authority and method

The command authority for this checkpoint is the exact recovered LWBridge 0.3.1 API asset:

`evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js`

SHA-256: `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`.

The current production WebUi copy hashes to `8b978e9fa87f412990be510ca567b1a74d2a1acb14ef637b15d65d1428337064`, so it is not used as reference authority for this inventory.

Reference commands are the unique literal command names passed to the shared `U(...)` invoke wrapper in the exact API asset. Reference events are every unique `app://` or `bridge://` literal across all recovered reference frontend JavaScript.

Production routing is counted only when a retained command has a specific path in the central backend, one of the three WebView host special branches, or one of the production-composed `INativeAsyncCommandService` implementations. Merely appearing in comments, diagnostics or owner-evidence blocklists does not count as a production route.

## Exact frontend inventory

The reference API exposes 104 unique command names.

Ten are explicitly outside retained scope by owner direction because they are Account/Login/Authentication or account-purpose activation/entitlement commands: `auth_activate`, `auth_credentials`, `auth_login`, `auth_logout`, `auth_renew`, `auth_state`, `auth_unbind`, `auth_unbind_preview`, `multi_activate`, and `multi_entitlement_get`.

That leaves 94 retained frontend commands.
Across all recovered reference frontend chunks there are 15 product event literals:

- `app://close-requested`
- `bridge://auth-state`
- `bridge://automation-status`
- `bridge://dispatch-plunder-changed`
- `bridge://equipment-apply-progress`
- `bridge://feedback-export-progress`
- `bridge://game-recovery`
- `bridge://map-scan-status`
- `bridge://multi-entitlement`
- `bridge://player-mark-changed`
- `bridge://profile-identity`
- `bridge://resource-automation-status`
- `bridge://status`
- `bridge://truck-plunder-changed`
- `bridge://update-status`

Event inclusion here is descriptive only; retained/excluded event semantics still require their own parity classification.

## Production route coverage

Of the 94 retained frontend commands, 61 currently have a specific production route. This is a routing fact, not a parity verdict: some routed commands remain partial, equivalent, or deliberately fail-closed.

Thirty-three retained frontend commands have no specific production route.

Fifteen of those 33 have already had their observable native boundary materially recovered and are intentionally fenced rather than guessed: `app_exit_confirm`, `automation_inspect`, `city_layout_apply_status`, `city_layout_snapshot_get`, `construction_rewards_claim`, `dispatch_assist_state`, `monopoly_cell_open`, `profile_create`, `profile_delete`, `update_check`, `update_download_and_open`, `vip18_base_config_get`, `vip18_base_config_save`, `vip18_base_list`, and `watermark_lookup`.
The remaining 18 are the concrete unrouted retained frontend command gaps:

- `automation_configure`, `automation_start`, `automation_stop`
- `chat_automation_configure`, `chat_automation_run_pending`
- `city_layout_apply_cancel`, `city_layout_apply_start`, `city_layout_validate`
- `dispatch_assist_cancel`, `dispatch_assist_retry`, `dispatch_assist_schedule`
- `equipment_preset_apply`
- `map_dispatch_share_alliance`
- `map_treasure_claim`
- `resource_automation_run`
- `trade_station_configure`
- `vip18_base_apply`, `vip18_base_restore`

These 18 are now a concrete retained routing queue rather than an unspecified whole-program gap.

## Why the full native inventory remains open

The recovered frontend is not the complete native command universe. Existing exact native checkpoints confirm at least eight original handlers with no matching literal anywhere in the recovered frontend assets: `equipment_initial_apply`, `profile_enable_set`, `profile_primary_set`, `profile_settings_get`, `profile_settings_save`, `red_packet_delay_configure`, `server_jump_history_get`, and `treasure_delay_configure`.

A raw executable string scan was tested but rejected as inventory authority because adjacent/packed strings merge command names with neighboring text. Native inventory must continue from handler/callsite recovery, not heuristic string splitting.

Therefore R8-067 closes the exact **reference frontend command/event inventory and production route audit**, but it does not mark the backlog's complete original command/service inventory item done.

Machine-readable evidence: `evidence/lwbridge-implementation/2026-09-26-r8-067-reference-command-inventory.json`.
