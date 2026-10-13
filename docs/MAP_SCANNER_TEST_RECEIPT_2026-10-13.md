# Scanner test build — merged and published

Owner requested the current best verified scanner on main for personal testing.
Bounded acceptance: [scope](MAP_SCANNER_TEST_ACCEPTANCE_2026-10-13.md).
Whole Map and scanner optimization remain PARTIAL.

- PR #9: https://github.com/Tuna0307/LW-Control/pull/9 — MERGED.
- Reviewed feature head: `aa5b432376d3bafcee1531adb54590e6e786f6cb`.
- Main merge and release source: `6f93c109436ebb80f4f344ad25380151b4f9aba5`.
- Tag: `map-scanner-test-2026-10-13`, direct remote target matches source.
- Release: https://github.com/Tuna0307/LW-Control/releases/tag/map-scanner-test-2026-10-13.
- Download: https://github.com/Tuna0307/LW-Control/releases/download/map-scanner-test-2026-10-13/LW-Control-MAP-SCANNER-TEST-RC-6f93c109436e.zip.
- Test prerelease; existing stable v0.6.0 remains available.
- ZIP: **9,936,194 bytes**, **83 entries / 81 application files**.
- ZIP SHA-256: `AC2E1718DE2B1FB94BC0EA139D5C5E5886AA707FF04DF65F261C105B6B2BF71C`.
- Uploaded GitHub asset digest and size: exact match, read independently via API.
- EXE SHA-256: `23AF7F7FF48D9F6B9CC1078222491486750E46B56EE8750B3DA891AEAE4FFD86`.
- EXE product version: `1.0.0+6f93c109436ebb80f4f344ad25380151b4f9aba5`.
- Embedded SOURCE-COMMIT.txt: exact merge source.
- Local ZIP: `artifacts/map-scanner-test-20261013/package/`.
- Owner copy: Windows Downloads, same filename/hash.

## Executed final gates

Canonical frontend check, production build/package identity, mounted names,
lifetime and saved-owner checks pass. Desktop Release default native suite passes,
including Home, storage, publication/cancellation, Auto Stop/return, dedicated
Ghost routing and controlled Treasure inspection. Actual engine/planner controlled
tests pass all 255 selections and 15 adverse cases (not native speed proof).

Fresh headless production browser sweep: 39 captures, EN/light, JA/dark and 375px,
zero recorded errors or document overflow. Selected captures inspected. Fixtures
are explicitly UI smoke, not new native population or full original pixel proof.

Fresh Windows x64 framework-dependent publish passes. ZIP CRC, extracted/published
byte identity, source version, canonical ProductionUi and private/diagnostic-file
exclusion pass. Extracted fixture Home capture exits successfully with no errors.
Normal production startup using a disabled isolated profile creates the expected
native window, starts zero games and exits through its own close request. Final
process query finds no LastWar, launcher or Desktop process.

## Cleanup exception retained honestly

The normal smoke's exact PID 49340 exited and the disabled profile stayed disabled.
Initial temporary-folder cleanup failed because the Python process retained a
SQLite handle. A subsequent validated PowerShell removal was rejected by automatic
tool review with `blocked by policy`; it was not repeated or rerouted.
The remaining isolated directory is
`%TEMP%/lwb317-normal-ui-disabled-94wwxe81`. Its receipts are copied under local
ignored release artifacts. It contains no live owned game or recovery journal,
is excluded from the ZIP, and does not affect owner configuration or executable
integrity. Temporary-directory deletion is not claimed complete.

No new live scan, prohibited account action or installed game-script mutation
was performed by this release verification. Historical negative trial receipts
remain preserved. Runtime packaging excludes REA, managed observers and private
experimental Lua transformations.

After merge, the feature branch has zero unique commits versus main. Retire its
local/remote refs, retain the one checkout, main/research and ignored artifacts.
Further development needs a lead-assigned feature branch; workers should not
resume the closed PR #9 or implement directly on main. This receipt changes docs
only after packaging; shipped source remains the merge above.
