# Team workflow - Player City owner check approved

Player City remains the active Map Data delivery. Review 18 accepted implementation `7da866595089698a00d89b14cd650b849a5bac90`; GitHub Actions `34726315539` completed SUCCESS on that exact implementation SHA. S03/S06 remain deferred, S02 remains unfinished/unassigned, and the corrected-build Overview live regression remains prepared but unperformed.

## Current gate

`LWB-PC-001` LIVE-PROVES two distinct fresh city-only current-client acquisitions, correct active-profile/server persistence, normal `map_search`/summary, a strictly newer second capture, exact restoration and clean ownership. `LWB-PC-002` LIVE-PROVES fresh-process same-profile reopen/search of the saved city.

Review 18 found no PM-blocking defect in the selected Player City path or the read-only owner-evidence package. The remaining selected-path gate is now only the **owner-visible normal Map Data City row**.

## Owner instruction now

1. Make sure Last War and LWBridge are closed.
2. Double-click repo-root `Start Owner Player City Check.cmd`.
3. After the ready message, use the Player City tab and click Search once.
4. Take one screenshot showing the visible saved city row and send it to ChatGPT.
5. Close LWBridge normally. Do not retry on failure; send the screenshot/error instead.

This check is read-only. Do not run Start Scan/Start Reading, Clear, Export, Jump, cross-server travel, Resource actions, or any game action. The collector automatically records the exact-session sanitized Search/render evidence and cleanup state.

After the owner result, Web interprets the automatic evidence and screenshot, records PASS/FAIL for the visible-row gate, fixes only the first failed link if necessary, then commits/pushes/verifies the resulting checkpoint. Do not begin another Map Data category until this selected Player City gate is resolved.
