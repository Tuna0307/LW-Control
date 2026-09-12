# SB-97 log-only rejection diagnosis — 2026-09-11

Scope: diagnose the existing Computer Use rejection from Sol attempt `20260911T044419Z-5af44117` using saved logs/configuration only. The rejected `get_window_state` observation was not retried, Resource Start was not sent, approval settings were not changed, safeguards were not disabled, and no alternate executor was used.

## Confirmed facts

- Installed adapter: `codex-chatgpt-web` `5.0.6`. Source: `C:\Users\chimw\AppData\Local\Programs\Codex Web GPT\resources\runtime\app\package.json` and sanitized `C:\Users\chimw\.codex-chatgpt-web\config.json` (`releaseVersion=5.0.6`).
- Adapter operating configuration: `mode=full`, `browserInteractionMode=automatic`, `autoApproveToolCalls=false`, `appName=Codex Native2`, `browserHost=launcher`; `solAvailable=true`, `proAvailable=false`. Source: sanitized adapter config above.
- Selected Codex model/provider for the affected task: `chatgpt-web/high` through provider `openai`; reasoning effort `high`; collaboration mode `Default`. The affected turn also recorded `approval_policy=never` and `approvals_reviewer=user`. Sources: saved Codex session/turn metadata and `C:\Users\chimw\.codex\config.toml`.
- A successful immediate pre-Start process/recovery check finished at `2026-09-11T04:47:36.999Z`; its observation timestamp was `2026-09-11T04:47:36.2978222Z`. It reported Resource Point as the only selected category, no selected game/launcher process, no recovery owner, and zero direct-profile `resource_point` rows.
- The token-count telemetry immediately after that check, at `2026-09-11T04:47:37.455Z`, recorded `credits=null`, `spend_control_reached=null`, and `rate_limit_reached_type=null`.
- The rejection was reported at `2026-09-11T04:48:14.276Z` (assistant response item at `04:48:14.299Z`). Exact error text: `This tool call was blocked by OpenAI because we couldn't determine the safety status of the request.`
- The attempted operation was native Computer Use `get_window_state` immediately before the fresh Resource Start. No Start was sent. This matches `attempt.json` and `blocked-poststate.json`.
- No normal local Computer Use function-call/dispatch/execution record for that rejected `get_window_state` exists between the successful pre-Start check and the blocker message. Earlier successful Computer Use calls in the same attempt did produce normal tool-call/execution records. Therefore the failure occurred before normal local `node_repl` / `@oai/sky` dispatch.
- The model bridge itself was not the failing component: the saved diagnostics show the local `POST http://127.0.0.1:17841/v1/responses` completed HTTP 200 at `2026-09-11T04:47:37.935911100Z` before the blocker message.
- The failure is not an LWBridge application error, Windows/`@oai/sky` execution error, or `node_repl` handler error. The evidence places it in the upstream automatic approval/safety-review path before local Computer Use dispatch.

## Reviewer/quota conclusion

The available sanitized local diagnostics do not expose the upstream reviewer identity, its availability, a timeout result, or a reviewer-specific quota/capacity field. No target-thread WARN/ERROR event or detailed Guardian decision event was found in the saved Codex diagnostics around the original rejection; Guardian strings seen there were feature metadata only.

Ordinary Codex usage quota/spend exhaustion is **not indicated** for the original rejection because the contemporaneous rate-limit telemetry had `rate_limit_reached_type=null` and `spend_control_reached=null`. That does not prove the upstream reviewer had capacity, so reviewer availability/quota contribution remains **UNKNOWN**.

## Hypotheses, not facts

- The reviewer may have returned an indeterminate safety verdict, or the review path may have failed to produce a determinate verdict. The user-visible error is consistent with either possibility, but the saved sanitized diagnostics do not distinguish them.
- Reviewer timeout, reviewer unavailability, or reviewer-specific quota exhaustion must not be stated as the cause without an upstream decision log that is not present locally.

## Diagnostic limits

During this log-only follow-up, two broader read-only diagnostic queries were themselves rejected by the same platform review message before execution. They were not retried, and no approval/safeguard/executor setting was changed. The final conclusion above therefore uses only the already collected saved diagnostics plus narrower successful reads.

Primary saved sources:

- `C:\Users\chimw\.codex\sessions\2026\09\11\rollout-2026-09-11T12-42-42-01a08ec6-6918-7343-bb68-39b507eea55d.jsonl`
- `C:\Users\chimw\.codex\logs_2.sqlite`
- `C:\Users\chimw\.codex-chatgpt-web\config.json`
- `C:\Users\chimw\.codex-chatgpt-web\responses-state.json`
- `C:\Users\chimw\.codex-chatgpt-web\diagnostics\browser-turns\`
- `C:\Users\chimw\.codex-chatgpt-web\runtime\thread-environments.json`
- `C:\Users\chimw\.codex\config.toml`
- `attempt.json`, `blocked-poststate.json`, and `step1-prestart.json` in this evidence directory.

PM-facing outcome: **BLOCKED / SB-97 remains valid.** The first failure is an environment/platform approval-review rejection before native Computer Use dispatch. There is no evidence-backed LWBridge code defect to return to Web from this attempt. Reviewer availability/quota remains unknown; ordinary Codex rate-limit/spend telemetry did not show exhaustion.
