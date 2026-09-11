# Owner resource check — PM review package

Updated 2026-09-11. **Do not start this check until PM/ChatGPT explicitly says the package has been reviewed.** This guide covers only the permitted saved Resource Search/reopen check. It does not authorize a fresh scan and does not clear SB-97.

## What you need to do

You only click normal app buttons, describe what you see, and send screenshots. You do **not** need Command Prompt, PowerShell, JSON, database files, hashes, logs, or technical diagnosis. ChatGPT handles all technical recording and interpretation automatically.

Before starting, close any open **LWBridge** and **Last War** windows normally. If a hidden launcher/process is still active, the recorder will detect it and tell you to stop; you do not need to find it yourself.

## First check

1. Open `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control` and double-click **`Start Owner Resource Check.cmd`**. Do not open the LWBridge `.exe` directly.
2. A message titled **"LWBridge owner check"** should say **"Technical recording is ready."** Click **OK**. If it instead says the recorder is not ready, take a screenshot of that message and stop.
3. LWBridge opens directly on **Map Data**. Click the **Resource** tab once.
4. Look just above **Scan contents**. If you see the red message **"No saved map server is available for this profile. Run a Resource Point scan first, then search again."**, do **not** click Search. Take one screenshot showing the whole LWBridge window, close LWBridge normally with the **X**, and continue to step 7. This is the normal no-saved-context branch.
5. If that red no-saved-map-server message is **not** present, click **Search** once. **Do not click Start / Start Reading / Start Scan.**
6. Wait until the Resource table stops visibly changing and either a Resource row or the normal no-saved-data message is visible. Take one screenshot showing the whole LWBridge window, then close LWBridge normally with the **X**.
7. The recorder shows a result message. If it says the profile has **no saved map server** or **no saved Resource rows**, stop and do not run the shortcut again. If it says **saved Resource data** was recorded, continue to the reopen check below only if this PM-reviewed guide is the instruction you were given. If it says recording was incomplete, stop and send ChatGPT the screenshot; do not repeat the check.

## Reopen check — only when the first result contained a saved Resource row

1. Double-click **`Start Owner Resource Check.cmd`** a second time.
2. When the recorder says **“Reopen recording is ready”**, click **OK**.
3. In LWBridge, click **Resource** (`资源`) once, then click **Search** (`搜索`) once.
4. Wait for the table to settle. Take one screenshot showing the whole LWBridge window.
5. Close LWBridge normally with the **X** button.
6. The recorder should say either that the same saved Resource row was verified or that the reopen did not match. Stop either way and send ChatGPT the screenshot plus what the message said. Do not run the shortcut a third time.

## Stop immediately if anything differs

Stop and send a screenshot/description if the recorder says it is not ready, Last War opens unexpectedly, the page is not Map Data, you accidentally press a Start/Scan button, or any instruction above does not match what is on screen. Recorder mode blocks state-changing commands. A red **"This owner evidence session is read-only"** banner can appear immediately during normal page startup when background initialization commands are blocked; include it in the screenshot, but do not treat that startup-only banner as evidence that you pressed Start. If it appears after you accidentally press a state-changing button, stop immediately. Do not retry, delete anything, reinstall anything, or press Start repeatedly.

## What ChatGPT records automatically

The shortcut saves a separate attempt bundle under `%LOCALAPPDATA%\LWBridgeRebuild\owner-evidence\...`. It records the exact app build, current-client compatibility, profile/store identity, read-only published server IDs/Resource rows, process/recovery state, and pre/post official-runtime fingerprints. When a saved server exists it also records the actual normal Resource `map_search` request/result plus passive rendered-table correlation. When no saved server exists, it records the normal `map_summary` `MAP_SAVED_CONTEXT_UNAVAILABLE` error and verifies that no published server exists instead of inventing a Search result. It also verifies cleanup before calling a recorded session complete.

Your screenshot is complementary visual evidence. A screenshot, saved database row, or empty table by itself is **not** fresh acquisition proof. The required first/second fresh Resource Start remains BLOCKED/NOT_RUN under SB-97 and is not part of this owner check.

## How to report back

Use one simple message such as: **“I reached step 5. I clicked Resource and Search. I saw [what was visible]. Here is the screenshot.”** ChatGPT will read the technical bundle directly and do the diagnosis.
