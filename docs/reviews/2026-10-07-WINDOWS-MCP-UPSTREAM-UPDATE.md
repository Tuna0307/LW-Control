# Windows-MCP upstream update

Status: **COMPLETE**, 2026-10-07. Owner requested current upstream verification
and update if behind. No product sources, owner desktop actions or live tests.

## Version comparison

- Initial installed version: 0.8.6 at `a74df211d8dcb4cdc49f58a5a18d816d79bd6c3b`.
- Fresh GitHub fetch and `ls-remote --symref origin HEAD`: upstream main/HEAD
  `d48d4b5fee897b76f66fafebc889a419680d796b`; local checkout was 14 commits behind.
- GitHub releases API: latest release v0.8.7, published 2026-09-30,
  tagged commit `6885dc1fc44b4fbe591d0c2a656e5b8face6901c`.
- Installed update: main `d48d4b5`, package 0.8.7, including eight commits after
  the latest published release. Current upstream identity is a check-time snapshot.

Sources: https://github.com/CursorTouch/Windows-MCP,
https://github.com/CursorTouch/Windows-MCP/releases/tag/v0.8.7,
and exact fetched commit/history/pyproject/lock bytes. Source was clean before
update; `git merge --ff-only origin/main` preserved history and made no local edits.

## Installation and checks

Synced the updated source's unchanged-by-us `uv.lock` into the existing active
environment using uv 0.12.19 and bundled CPython 3.14.7 (`sync --locked --no-dev`).
The direct-interpreter Codex/Claude entries and tracked stdio bridge still work;
neither client configuration nor owner apps needed mutation/restart in this pass.

Fresh bridge MCP initialization, list-tools and shutdown pass: 21 registered tools,
including newly added ControlStatus and existing Snapshot/Screenshot/Click/Type.
No tools were called, no AI desktop-control lease was requested and no owner
application/game was launched/stopped. The subprocess was gone after shutdown.
Two upstream Python asyncio deprecation warnings remain non-fatal.

The updated source introduces desktop ownership/input monitoring and a tool-call
control gate. Listing tools bypasses action acquisition; source inspection and
discovery are not proof of actual screenshot/input behavior. Workers must honor
user takeover and actual tool errors, in addition to the current owner desktop/live
hold. Do not bypass that gate to satisfy a capture/control request.

New update evidence is in `evidence/tooling/windows-mcp-update-2026-10-07/`.
Initial relocation review, source/version proofs and its 20-tool results remain
historical and were not rewritten. Current setup and owner-relay prompt were updated.
The external source/runtime stays ignored by the main project Git checkout.

## Continuation

Reconnect/restart the relevant AI app when convenient for already loaded MCP
servers to use the updated source. No app was restarted while the owner used the
computer. The next worker assignment remains the solo, headless/offline
`LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001`; UIUX/R3 acceptance and the
live hold remain unchanged. Project-lead acceptance is independent of delivery.
