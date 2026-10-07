# Windows-MCP and Remote Desktop Commander

Owner resumption, 2026-10-07: Windows-MCP and bounded live desktop control are
permitted for the lead-reissued HOME-MAP-LIVE-PILOT-001. The older hold below is
superseded only for that task. Fresh lead discovery lists 21 tools, ControlStatus
works, and a display-0 Screenshot saved/decoded at 1920x1080. An earlier Snapshot
returned USER_CONTROL with approximately 10 seconds remaining; no gate was
bypassed. A later snapshot succeeded. Input/app-window control is not yet proven
by these read-only captures. See the pilot's lead-preflight record.

Owner direction, 2026-10-07: feature recovery continues offline. Live Last War
testing and shared-desktop screenshots/input are on hold until the owner explicitly
resumes them after arranging a second screen and availability. Installing these
tools does not authorize desktop/game actions. Other owner-relayed workers work
alone, with no subagents or GPT Work/Codex delegation; the Codex lead is separate.

## Installed location and identity

The complete existing installation was moved from
`C:\Users\chimw\OneDrive\Desktop\Github\LW\Windows-MCP` to
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control\Windows-MCP`.
The old location no longer exists. Existing helper files, research screenshots,
bundled Python runtimes and the nested source Git repository were preserved.
This machine-local directory is ignored by LW-Control Git; it is not a submodule.

Source: https://github.com/CursorTouch/Windows-MCP.
Current installed source commit: `d48d4b5fee897b76f66fafebc889a419680d796b`,
matching upstream `main`/HEAD when checked on 2026-10-07.
Package version: `0.8.7`; installed source requires Python `>=3.14`.
Latest published release at that check is `v0.8.7`, commit
`6885dc1fc44b4fbe591d0c2a656e5b8face6901c`; the installed main commit includes
eight subsequent upstream fixes. It was fast-forwarded from the preserved initial
0.8.6 commit `a74df211d8dcb4cdc49f58a5a18d816d79bd6c3b`, with no local source edits.

The new active environment is `Windows-MCP\.venv`, using bundled CPython 3.14.7.
It was rebuilt with the existing `src\uv.lock` and uv 0.12.19, rather than using
the relocated old environment's absolute paths. The old environment remains in
`.venv-before-relocation` as installation history; do not run it. Active 3.13 and
3.14 bundled-runtime junctions point to the new directory; old junctions and
`.uv-bootstrap` are preserved history, not active launch paths.

Rebuild, if needed, from the repository root in PowerShell:

```powershell
$env:UV_PROJECT_ENVIRONMENT = Join-Path $PWD 'Windows-MCP\.venv'
& 'C:\Users\chimw\AppData\Roaming\Python\Python312\Scripts\uv.exe' sync --locked --project Windows-MCP/src --python "$PWD\Windows-MCP\.python\cpython-3.14.7-windows-x86_64-none\python.exe" --no-dev
```

## Client connection

The machine's Codex configuration had pointed to nonexistent
`LW-Control\tools\Windows-MCP`. That entry now launches the rebuilt interpreter
directly. A matching entry was added to the installed Claude Desktop configuration,
preserving existing preferences and other server entries:

- `C:\Users\chimw\.codex\config.toml`
- `C:\Users\chimw\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json`

Pre-edit configuration backups are outside Git in
`C:\Users\chimw\.codex\tmp\windows-mcp-relocation-20261007`.
No owner app was restarted. Existing chats do not acquire new tools merely because
these files changed; reconnect/restart the relevant client when convenient.
These entries do not grant other workers permission to use Codex.

Equivalent MCP configuration for another supported client:

```json
{
  "mcpServers": {
    "windows-mcp": {
      "command": "C:\\Users\\chimw\\OneDrive\\Desktop\\Github\\LW-Control\\Windows-MCP\\.venv\\Scripts\\python.exe",
      "args": ["-m", "windows_mcp", "serve"],
      "env": {
        "ANONYMIZED_TELEMETRY": "false",
        "WINDOWS_MCP_WATCHDOG": "off",
        "PYTHONIOENCODING": "utf-8"
      }
    }
  }
}
```

## Access through Remote Desktop Commander

If the worker's host does not directly expose Windows-MCP, use Remote Desktop
Commander to run the tracked stdio bridge. It starts its own local MCP subprocess,
uses the same installed server, and closes the connection afterward. It does not
require a background HTTP listener or a scheduled startup service.

From `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`:

```powershell
& '.\Windows-MCP\.venv\Scripts\python.exe' tools/windows_mcp_client.py
```

This lists actual tool descriptions and argument schemas without invoking desktop
tools. Read those schemas before using a tool; do not guess names or arguments.
For a separately authorized action, the bridge accepts `--tool EXACT_NAME`,
`--arguments-file ABSOLUTE_JSON_PATH` and optional `--image-output ABSOLUTE_PATH`.
It reports tool errors and saves returned images without overwriting existing
evidence. An omitted image path does not save an image. Historical helpers in
`Windows-MCP` that target HTTP port 8765 are not the configured entry point.

Use Remote Desktop Commander for its available filesystem/process/shell functions
and Windows-MCP for registered desktop capabilities. If one cannot perform a needed
step, inspect the other before reporting a blocker. If neither works, record exact
errors, connection/session state and the attempted capability. Do not fabricate
success, bypass scope, or interpret tool availability as permission to act.

## Verification and limits

Verified on 2026-10-07: `python -m windows_mcp --help`, successful stdio MCP
initialization/tool discovery after the update, and 21 registered tools:
ControlStatus, App, DisplayInventory,
PowerShell, FileSystem, Snapshot, Screenshot, Click, Type, Scroll, Move, Shortcut,
Wait, WaitFor, Scrape, MultiSelect, MultiEdit, Clipboard, Process, Notification,
Registry. Snapshot/Screenshot advertise screenshot support; Click/Type/Move/
Shortcut advertise interaction support. This verifies registration and startup,
not actual desktop capture or input operation. **Zero desktop tools were called.**

The updated upstream adds a desktop-control ownership gate and `ControlStatus`.
Actual tool calls may be blocked while the user is active; do not bypass that gate
or take over the owner's desktop. Tool discovery does not request an AI input lease.
Read the actual tool schemas/status and respect user takeover during a later
authorized task. The owner hold remains in force.

The upstream startup emits two Python event-loop deprecation warnings; discovery
and orderly shutdown succeeded. No application focus/input was changed, no owner
app/game was launched or stopped, and no scheduled service was installed.
Remote Desktop Commander was reachable through its read-only configuration API.
Actual desktop use and live feature validation remain deferred by the owner.
