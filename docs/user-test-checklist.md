# Owner guide — Overview Launch, bridge message and Close

Updated by owner instruction, 2026-09-11. **The Overview feature is in implementation; a verified test package is not yet delivered.** This page is the guide Web must complete when ready, not a request to test now.

## What you will verify

1. Open the exact rebuilt app or tested shortcut Web supplies.
2. On **Overview / 首页**, click **Launch Game / 启动游戏**.
3. Wait for the actual game to open. Web must describe the expected ready screen and what to do if it fails.
4. Confirm **LWbridge is running** appears at the **top centre inside the game**. Take a screenshot showing the game and the message.
5. Return to Overview and click **Close Game / 关闭游戏**.
6. Confirm that the game window closes and Overview returns to the stopped state. Tell Web what you saw.
7. Only when you are satisfied that the real sequence works, confirm acceptance. You choose the next feature; it does not start automatically.

Web must fill in verified prerequisites, exact app/shortcut paths, completion/failure indications and stop/cleanup instructions before asking you to execute this sequence. If the expected outcome differs, stop and send a description/screenshot; you do not diagnose the technical cause. Do not use the old **Start Owner Resource Check.cmd** for this milestone: it is a read-only resource recorder that blocks game launch/close.

## Your evidence is simple

You only follow visible UI steps, describe what appears and supply screenshots. You do not run commands, copy terminal output, find logs/databases, inspect JSON, compare hashes or debug recovery.

Use: **I reached step __. I clicked __. I saw __. Here is the screenshot.**

Web must automatically record actual build/client/profile/game session, bridge readiness and message state, close outcome and cleanup. It must distinguish a real ready bridge from a process or displayed text alone. Any script/shortcut must exist and be verified before Web calls it ready. Web reads and interprets the technical bundle directly.

## Restrictions and readiness

This is a specification for the owner's requested feature, not authorization to replay a previously rejected automated operation. Web must satisfy the applicable implementation, evidence and permitted-execution conditions before issuing the finished guide. The prior empty-profile resource check is complete; do not repeat it. Resource scans and other functions are deferred.

[Active delivery plan](overview-live-delivery.md) and [current Web prompt](team-workflow.md) contain the technical tasks.
