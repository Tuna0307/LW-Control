# Resource function: test readiness and acceptance

Review 14, 2026-09-11; implementation audited at `7ca6d5c`.

## What is ready

The resource-only implementation is ready for controlled validation **subject to current-client integrity and a permitted live test path**. It is not release-ready, and the complete Overview/Map Data feature set is not ready. Computer Use setup can be checked by the standard AI separately; that setup is not evidence of game functionality.

Use the latest Release rebuild, not the LWBridge reference or an old preview:

`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control\src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe`

Use the same profile and launch context across tests. Packaged Codex and standalone desktop launches may expose different data roots. The AI must identify the actual selected store before diagnosing missing saved data; do not seed a test row or copy historical data to manufacture a fresh success.

## Checks available without a new game acquisition

1. Open Map Data, select the Resource result tab, and press Search. If this profile already contains a real saved resource, it should display its stored coordinates, level and acquisition time. Historical evidence has server 2212 / coordinates 481,32 / level 3. An old timestamp is expected for saved data; it is not a fresh scan.
2. Close and reopen the same app, select Resource, and press Search again. The same saved result should remain. With no saved server in this profile, expect a clear missing-context message instead; that state alone is not a reopen regression.
3. With the default multi-category scan selection, Start should explain that live scanning currently supports Resource Point only. It must not pretend monsters or other categories were scanned. This guard check should not start a game/helper.

## The real-game test that still needs to pass

This is the acceptance specification, not authorization to rerun a denied operation. SB-97's prepared live proof remains restricted. The AI must establish an independently permitted test path or record the required environment change before performing live acquisition; do not switch executors/models or ask the owner to run that rejected harness.

For a permitted test, first fingerprint the actual installed client and verify the documented restoration/preflight requirements. Preserve unrelated processes and profile data. Do not tell the owner to use Overview Launch Game as a prerequisite: its bridge bootstrap is incomplete. Use only the documented supported bounded resource lifecycle.

1. In the normal rebuilt Map Data window, select only Resource Point and start a scan. Select the Resource results tab and Search. A real resource must appear with coordinates, level and a new acquisition time. Progress reaching 100% is insufficient.
2. Repeat a fresh resource scan and Search. The new acquisition time must advance. The same point may return and the count may remain one; those facts alone are not failures or proof of freshness.
3. Close/reopen the rebuilt app in the same profile and Search again. The second result and its saved time must remain visible without another scan.
4. The AI must correlate the real source/session/request, stored row, actual Search request/result and rendered row, then verify documented cleanup/restoration. Screenshots alone are insufficient. Do not force-kill processes or delete/reinstall game data to turn a failed test into a pass.

Report the first failed step, exact visible error, and screenshot if available. The AI records build/client identity and detailed correlation; the owner need not interpret protocol logs. Stop at the first failure, diagnose and fix it, and retest that step before moving on. A missing permitted execution path means BLOCKED, not passed.

## What not to expect yet

Monster/other-category acquisition, full-world/automatic scanning, complete Normal/Fast behavior, Overview launch/close/reconnect integration, complete export/filter/action parity, and every conditional treasure/plunder workflow are unfinished. Unknown resource names and unproven live Gathering remain explicit limitations. Do not treat successful saved-data search as completion of these features.

Once the resource gate passes, the next user-visible delivery is real monster acquisition and search. It is not assigned to Daybreak unless a specific unresolved contract receives a reviewed escalation.
