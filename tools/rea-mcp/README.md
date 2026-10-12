# Project-local REA MCP

Installed 2026-10-12 from the official npm `rea-agents` release **6.4.0**;
upstream https://github.com/morluto/rea. Exact dependency versions and package
integrity are pinned in package-lock.json. No global npm installation is required.

From repository root, restore dependencies with:

```powershell
npm.cmd ci --prefix tools/rea-mcp --ignore-scripts --no-audit --no-fund
tools/rea-mcp/rea.cmd --version
tools/rea-mcp/rea.cmd mcp doctor --json
```

Codex registration is project-scoped in `.codex/config.toml`; existing user MCP
servers are preserved. `codex.cmd mcp get rea --json` verifies the effective entry.
Restart/reconnect the Codex project session to load the new tools. Project `.mcp.json`
also contains the stdio entry for clients supporting that format. These files have
absolute paths for this Windows checkout; update them if moving the checkout.

The owner-relayed worker can immediately use `tools/rea-mcp/rea.cmd` through its
terminal. Chat On Steroids' own MCP plugin list was not modified; project JSON is
not proof that another host has imported/connected the server. CLI inspection
remains available without restarting or controlling any application window.

The matching release workflow is in `.agents/skills/reverse-engineer-anything`.
Its seven instruction files match the npm bundle. They are REA third-party
instructions, subject to project/owner execution authority. Read them when using
REA for shipped artifacts; ordinary repository reads do not require REA.

Actual installed production stdio verification passed initialize, tools/list
(170), prompts/list (6), binary_session and inspect_managed_artifact against the
built Map assembly. Repeat a bounded read-only integration check with:

```powershell
node tools/rea-mcp/verify.mjs src/LWBridge.Map-0.3.17/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Map_0_3_17.dll
```

JavaScript and managed .NET static analysis work without an external engine.
Native machine-code decompilation needs a compatible Ghidra, Hopper or IDA
provider. No provider is configured on this machine: Ghidra doctor reports
`GHIDRA_INSTALL_DIR is not set`; this installation did not download another
analysis engine or alter Java. REA setup/doctor's *user-level* client/skill checks
do not validate this manually registered project-scoped configuration.

No game was launched, game scripts changed, or scanner behavior altered during
installation. Installing REA supplies analysis tools, not automatic decryption of
the original protected controllers or proof of scanner speed/accuracy.

Raw installation smoke outputs stay under ignored `artifacts/rea-*`. The small
receipt is `docs/proof/REA-MCP-INSTALL-2026-10-12.json`. Source/skill attribution
and license are in this directory's `LICENSE-REA.txt`.
