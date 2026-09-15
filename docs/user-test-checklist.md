# Owner guide - Player City visible-row check

**Completed 2026-09-13 under `LWB-PC-003`. Do not rerun this check unless ChatGPT/PM explicitly requests a regression retest.** The owner-visible normal Map Data row, Search correlation and clean close are already recorded.

The completed check was deliberately read-only. It did **not** start Last War, run a scan, travel cross-server, clear data, export, jump coordinates, or modify the saved map row. The saved row was already visible because Map Data automatically performs a local persisted-data City query when the page opens; pressing Search simply repeated that local query.

## Historical retest steps - only if explicitly requested later

1. Make sure **Last War** and **LWBridge** are closed.
2. Open the `LW-Control` folder and double-click **Start Owner Player City Check.cmd**.
3. Wait for the message titled **LWBridge Player City check**; if preparation is blocked or failed, stop and send ChatGPT a screenshot. Do not retry.
4. LWBridge opens directly on **Map Data**. Do not press **Start Scan / Start Reading**.
5. Click the **City / Player City** tab if needed, then click **Search** once.
6. Confirm a saved city row is visible, take one screenshot, send it to ChatGPT, and close LWBridge normally.

If anything differs from the expected read-only flow, stop and send a screenshot plus a short description. Do not use other Map Data buttons to troubleshoot.
