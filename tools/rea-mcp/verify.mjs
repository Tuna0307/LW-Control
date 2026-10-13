import { Client } from "@modelcontextprotocol/client";
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));
const target = process.argv[2];
const client = new Client({ name: "lw-control-rea-install-check", version: "1" });
const transport = new StdioClientTransport({
  command: process.execPath,
  args: [path.join(directory, "node_modules/rea-agents/scripts/rea.mjs"), "mcp"],
  cwd: path.resolve(directory, "../.."),
  stderr: "pipe",
});
transport.stderr?.on("data", (data) => process.stderr.write(data));
try {
  await client.connect(transport, { timeout: 120000 });
  const tools = await client.listTools();
  const prompts = await client.listPrompts();
  const summary = {
    server: client.getServerVersion(),
    toolCount: tools.tools.length,
    promptCount: prompts.prompts.length,
  };
  if (target) {
    const result = await client.callTool({
      name: "inspect_managed_artifact", arguments: { path: path.resolve(target) },
    }, undefined, { timeout: 120000 });
    if (result.isError) throw new Error(JSON.stringify(result));
    const payload = result.structuredContent ?? JSON.parse(
      result.content.find((item) => item.type === "text")?.text ?? "null",
    );
    summary.managedInspection = {
      evidenceId: payload.evidence_id,
      operation: payload.operation,
      provider: payload.provider?.id,
      sha256: payload.subject?.digest?.sha256,
      classification: payload.normalized_result?.classification,
      metadataStatus: payload.normalized_result?.metadata?.status,
      methodCount: payload.normalized_result?.metadata?.table_row_counts?.MethodDef,
    };
  }
  console.log(JSON.stringify(summary));
} finally {
  await client.close();
}
