# Loop start prompt — LWB317 UI campaign

Use this file when starting the Chat On Steroids Loop worker.

## Loop continuation instruction

Paste the following into the Loop continuation-instructions box:

> Continue only the active project-lead-authorized LWBridge 0.3.17 campaign.
>
> Repository:
> `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
>
> At the start of every continuation:
> 1. read `AGENTS.md`;
> 2. read `task.md`;
> 3. read `docs/PROJECT_LEAD.md`;
> 4. read `docs/AI_WORK_PROTOCOL.md`;
> 5. read `docs/LOOP_WORKER_PROTOCOL.md`;
> 6. read `docs/LOOP_QUEUE.md`;
> 7. read `docs/LOOP_CAMPAIGN_8H.md`;
> 8. inspect `git status --short`;
> 9. if the tree is clean and only behind the remote, run
>    `git pull --ff-only origin research/offline-controller`;
> 10. obtain current machine time, compare it to the campaign start time, and resume the first incomplete authorized stage.
>
> Follow `docs/LOOP_CAMPAIGN_8H.md` exactly.
>
> The campaign is UI-only. Do not start gameplay/backend function reverse engineering.
>
> The project does not recreate LWBridge's original login/account/licensing/entitlement system. Do not bypass auth. Do not reverse engineer credentials, tokens, license validation, purchases or subscriptions. If login blocks runtime observation, document the boundary and continue only with independently authorized static/offline UI work.
>
> Do not launch or control Last War.
>
> Do not modify legacy `src/LWBridge.Desktop` as the new 0.3.17 clone.
>
> Use exact 0.3.17 runtime/static evidence. Never invent unseen UI. Mark unknown or blocked states explicitly.
>
> After each coherent campaign stage, finish its evidence/review, run applicable checks plus `git diff --check`, commit, push to `research/offline-controller`, update the campaign table, and continue to the next authorized stage.
>
> At 7 hours 45 minutes elapsed, do not begin another stage. Finish the current coherent checkpoint, commit/push, close LWBridge if you launched it, change the campaign queue state to `AWAITING_REVIEW`, produce the final handoff, and stop.
>
> After the campaign finishes, reply exactly `WAITING_FOR_PROJECT_LEAD` and make no further project changes.

## First normal message

Send this once after Loop is enabled:

> Start the active LWBridge 0.3.17 UI campaign now.
>
> Open:
> `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
>
> First synchronize safely with `origin/research/offline-controller` using fast-forward-only if the working tree is clean.
>
> Then read all current authority documents listed by the Loop protocol, verify the exact reference SHA-256, record the actual local campaign start time in `docs/LOOP_CAMPAIGN_8H.md`, and begin with `LWB317-UI-001B`.
>
> Chat On Steroids Core and Desktop are authorized for the UI campaign.
>
> Work continuously through the pre-authorized UI stages until the campaign time limit or a defined stop condition. Do not start function reverse engineering or auth/licensing reconstruction.

## Expected final state

The worker should end by reporting:

`WAITING_FOR_PROJECT_LEAD`

with the campaign queue set to `AWAITING_REVIEW`, all completed stage commits pushed, and no Phase 2 work started.
