# Windows-MCP relocation and current worker policy

Status: **COMPLETE**, 2026-10-07. Tool installation/configuration and owner policy
change only; no product behavior or live parity status was upgraded.

## Owner request and result

The owner needs this computer, postpones live testing until explicit resumption
after arranging a second screen/availability, and requires other worker AIs to
work without subagents, GPT Work or Codex. AGENTS/protocols, current master/queue/
handoff/continuation documents now reflect that direction. The prepared live pilot
is ON_HOLD_BY_OWNER. Historical permissions and recovery evidence remain intact.

The existing Windows-MCP installation was moved into `LW-Control\Windows-MCP`,
not deleted/replaced with an unexamined new checkout. Old path is absent; nested
source commit is unchanged and clean at `a74df211d8dcb4cdc49f58a5a18d816d79bd6c3b`,
version 0.8.6. The original bundled interpreter was retained, active runtime
junctions were repaired, and the environment was rebuilt from its existing lock.
Old environment and junctions remain archived locally. The entire third-party
installation is ignored by the main Git repository.

Codex's nonexistent-path configuration was repaired. The installed Claude Desktop
configuration received the same direct-interpreter stdio launch entry. Strict
parsed pre/post comparisons show unrelated configuration unchanged. Backups
remain outside Git. The owner apps were not restarted.

`tools/windows_mcp_client.py` provides actual stdio MCP access through Remote
Desktop Commander shell operations when the worker host lacks a native MCP entry.
It lists schemas by default; explicit authorized calls can return text/images.
No HTTP listener or scheduled service was added. Setup and future action boundaries
are documented in `docs/WINDOWS_MCP_SETUP.md`.

## Executed verification

- uv 0.12.19 `sync --locked --project Windows-MCP/src --python` bundled 3.14.7
  `--no-dev`: PASS; rebuilt environment imports the unchanged source checkout.
- `python -m windows_mcp --help`: PASS.
- Bridge stdio initialization/list-tools/orderly shutdown: PASS, 20 tools;
  Snapshot/Screenshot/Click/Type and their real schemas present. Desktop calls: 0.
- Local relocation validator: PASS; source commit/clean state, old path absence,
  active interpreter junctions and preservation of unrelated Codex/Claude settings.
- Remote Desktop Commander read-only `get_config`: reachable, version 0.2.52.
- Git ignore check excludes the nested checkout; diff check passes. Production
  frontend/native sources were not modified, so product build suites were not rerun.

Upstream emitted two Python asyncio event-loop deprecation warnings during startup;
they did not prevent initialization/discovery/shutdown. Desktop capture/input and
client reconnection were deliberately not tested while the owner uses the computer.
Registration proves tool availability at the server boundary, not success on a
particular future desktop task. Current chat tools do not hot-load this new server.

## Continuation

The owner can reconnect/restart the relevant MCP client when convenient. A worker
can use the CLI bridge through Remote Desktop Commander immediately for headless
schema discovery. Both tools must be checked before declaring a capability blocker
on a later authorized desktop task; neither tool authorizes live testing now.

The next bounded manual-relay offline assignment is
`LWB317-FUNCTION-HOME-MAP-OFFLINE-HOST-CONTRACT-001`: recover real host/delegate/
capture readiness contracts from static sources, prove actual inert seams and fix
only demonstrated in-scope defects. Real xLua/current-game proof remains UNKNOWN
until the owner resumes live work. RECOVERY-003 and accepted local UIUX remain.
