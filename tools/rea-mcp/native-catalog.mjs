// Read-only REA MCP capability/schema discovery; does not open or run a target.
import { Client } from "@modelcontextprotocol/client";
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));
const client = new Client({ name: "lw-control-map013-native-catalog", version: "1" });
const transport = new StdioClientTransport({
  command: process.execPath,
  args: [path.join(directory, "node_modules/rea-agents/scripts/rea.mjs"), "mcp"],
  cwd: path.resolve(directory, "../.."),
  stderr: "pipe",
  env: process.env,
});
transport.stderr?.on("data", (data) => process.stderr.write(data));
try {
  await client.connect(transport, { timeout: 120000 });
  const { tools } = await client.listTools();
  const selected = /binary|pseudocode|assembly|function|caller|callee|cross.reference|xref|reference|procedure|address|string|native/i;
  console.log(JSON.stringify(tools.filter((tool) => selected.test(tool.name)).map(({ name, inputSchema }) => ({ name, inputSchema })), null, 2));
} finally {
  await client.close();
}
