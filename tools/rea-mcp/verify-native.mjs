// MAP-013: static, source-digested native PE verification through REA MCP.
// Run against a hash-admitted original artifact; NEVER executes target code.
// GHIDRA_INSTALL_DIR, JAVA_HOME and REA_ANALYSIS_PROVIDER must be scoped to this process.
import { Client } from "@modelcontextprotocol/client";
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";
import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));
const target = path.resolve(process.argv[2] ?? "");
const output = path.resolve(process.argv[3] ?? "");
const requestedProcedure = process.argv[4];
if (process.argv.length < 4 || !/\.(exe|dll)$/i.test(target) || !output.includes("rea-toolchain")) {
  throw new Error("Usage: node verify-native.mjs <absolute x64 PE .exe/.dll> <ignored rea-toolchain dir> [procedure]");
}
const digest = createHash("sha256").update(await readFile(target)).digest("hex");
await mkdir(output, { recursive: true });
const client = new Client({ name: "lw-control-map013-native-proof", version: "1" });
const transport = new StdioClientTransport({
  command: process.execPath,
  args: [path.join(directory, "node_modules/rea-agents/scripts/rea.mjs"), "mcp"],
  cwd: path.resolve(directory, "../.."),
  stderr: "pipe",
  env: process.env,
});
transport.stderr?.on("data", (data) => process.stderr.write(data));
const log = { target, sha256: digest, provider: "ghidra", operations: [] };
let opened = false;
let nativeCodeProven = false;
async function invoke(name, args = {}, timeout = 600000) {
  let normalized;
  try {
    // MCP SDK 6.4.0 client callTool(params, options); a third argument is ignored
    // and silently falls back to the default 60-second request deadline.
    const response = await client.callTool({ name, arguments: args }, { timeout });
    normalized = response.structuredContent ?? response;
    if (response.isError) throw new Error(JSON.stringify(normalized).slice(0, 2500));
    await writeFile(path.join(output, `${String(log.operations.length).padStart(2, "0")}-${name}.json`), JSON.stringify(normalized, null, 2));
    const result = { name, success: true, evidenceId: normalized?.evidence_id ?? null, resultKeys: Object.keys(normalized ?? {}) };
    log.operations.push(result);
    console.log(JSON.stringify(result));
    return normalized;
  } catch (error) {
    const result = { name, success: false, error: String(error).slice(0, 3000) };
    log.operations.push(result);
    console.log(JSON.stringify(result));
    return null;
  }
}
try {
  await client.connect(transport, { timeout: 120000 });
  const openedResult = await invoke("open_binary", { path: target, provider_id: "ghidra" }, 120000);
  if (!openedResult) throw new Error("Native target was not admitted by REA");
  opened = true;
  const overview = await invoke("binary_overview", {}, 600000);
  if (!overview) throw new Error("Native import/analysis failed; inspect saved diagnostic");
  const procedures = await invoke("list_procedures", {}, 120000);
  const serialized = JSON.stringify(procedures ?? overview);
  const candidates = [...new Set((serialized.match(/FUN_[0-9a-fA-F]{6,}|\bentry\b|\bWinMain\b|\bmain\b/g) ?? []))];
  const procedure = requestedProcedure ?? candidates[0];
  log.procedureCandidates = candidates.slice(0, 24);
  log.selectedProcedure = procedure ?? null;
  if (!procedure) throw new Error("No native procedure available for code inspection");
  const info = await invoke("procedure_info", { procedure }, 180000);
  const assembly = await invoke("procedure_assembly", { procedure }, 180000);
  const pseudo = await invoke("procedure_pseudo_code", { procedure }, 180000);
  const callers = await invoke("procedure_callers", { procedure }, 180000);
  const callees = await invoke("procedure_callees", { procedure }, 180000);
  const xrefs = await invoke("procedure_references", { procedure, direction: "incoming" }, 180000);
  nativeCodeProven = !!info?.normalized_result?.body?.available &&
    typeof assembly?.normalized_result === "string" && assembly.normalized_result.trim().length > 0 &&
    typeof pseudo?.normalized_result === "string" && pseudo.normalized_result.trim().length > 0 &&
    Array.isArray(callers?.normalized_result) && Array.isArray(callees?.normalized_result) &&
    Array.isArray(xrefs?.normalized_result?.references);
  log.nativeCodeProven = nativeCodeProven;
  if (!nativeCodeProven) throw new Error("Native assembly, pseudocode, body and references were not all demonstrated");
} finally {
  if (opened) await invoke("close_binary", {}, 120000);
  await client.close();
  await writeFile(path.join(output, "summary.json"), JSON.stringify(log, null, 2));
}
