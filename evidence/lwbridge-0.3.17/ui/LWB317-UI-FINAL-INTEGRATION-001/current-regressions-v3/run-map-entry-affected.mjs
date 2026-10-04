import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const { parse } = requireUi("@babel/parser");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex");
const hashFile = (file) => sha256(fs.readFileSync(file));
const normalize = (value) => path.resolve(value).replaceAll("\\", "/").toLowerCase();

const acceptedOutputs = [
  {
    name: "Cross-server focused mount",
    script: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v3/run-crossserver-focused.mjs"),
    historical: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-b/focused-results.json"),
    redirected: path.join(here, "crossserver-focused-results.json"),
  },
  {
    name: "Cross-server App ownership and Home acknowledgement regressions",
    script: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v3/run-crossserver-regressions.mjs"),
    historical: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/regression-results.json"),
    redirected: path.join(here, "crossserver-regression-results.json"),
  },
  {
    name: "accepted Map parent ownership replay",
    script: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v3/replay-map-ownership.mjs"),
    historical: path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-RETENTION-001/milestone-c/map-ownership-results.json"),
    redirected: path.join(here, "map-ownership-results.json"),
  },
];

const historicalBefore = Object.fromEntries(acceptedOutputs.map((entry) => [entry.name, hashFile(entry.historical)]));
const realWriteFileSync = fs.writeFileSync.bind(fs);
const redirectByHistorical = new Map(acceptedOutputs.map((entry) => [normalize(entry.historical), entry.redirected]));
fs.writeFileSync = (target, data, ...args) => {
  const redirect = redirectByHistorical.get(normalize(target));
  if (redirect) return realWriteFileSync(redirect, data, ...args);
  return realWriteFileSync(target, data, ...args);
};

try {
  for (const [index, entry] of acceptedOutputs.entries()) {
    await import(`${pathToFileURL(entry.script).href}?map-entry-current=${index}`);
    assert.ok(fs.existsSync(entry.redirected), `${entry.name}: task-local redirected output`);
  }
} finally {
  fs.writeFileSync = realWriteFileSync;
}

const historicalAfter = Object.fromEntries(acceptedOutputs.map((entry) => [entry.name, hashFile(entry.historical)]));
assert.deepEqual(historicalAfter, historicalBefore, "accepted historical result files must remain byte-identical");

const currentPath = path.join(ui, "src/App.jsx");
const baselinePath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/recovery/baseline-74ceaf7-App.jsx");
const current = fs.readFileSync(currentPath, "utf8");
const baseline = fs.readFileSync(baselinePath, "utf8");

function declaration(source, name) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  for (const entry of ast.program.body) {
    const node = entry.type === "ExportNamedDeclaration" ? entry.declaration : entry;
    if (node?.type === "FunctionDeclaration" && node.id?.name === name) return source.slice(node.start, node.end).replaceAll("\r\n", "\n");
  }
  throw new Error(`missing function ${name}`);
}

const retainedCurrent = declaration(current, "RetainedPages");
const retainedBaseline = declaration(baseline, "RetainedPages");
assert.ok(retainedCurrent.includes('pagePropsByRoute'));
assert.equal(retainedCurrent.replace(', pagePropsByRoute })', ' })').replace(' {...pagePropsByRoute?.[route.key]}', ''), retainedBaseline, "accepted Activity/profile boundary preserved with reviewed controlled-prop addition");

const report = {
  result: "LWB317_MAP_ENTRY_AFFECTED_OK",
  currentApp: { path: "src/LWBridge.UI-0.3.17/src/App.jsx", sha256: sha256(current) },
  acceptedChecks: acceptedOutputs.map((entry) => ({
    name: entry.name,
    status: "PASS",
    sourceScript: path.relative(repo, entry.script).replaceAll("\\", "/"),
    taskLocalOutput: path.relative(repo, entry.redirected).replaceAll("\\", "/"),
    historicalOutput: path.relative(repo, entry.historical).replaceAll("\\", "/"),
    historicalSha256Before: historicalBefore[entry.name],
    historicalSha256After: historicalAfter[entry.name],
  })),
  retention: {
    status: "PASS",
    method: "Exact normalized RetainedPages equality after removing only reviewed per-route controlled-prop parameter/spread; actual profile/selection behavior has separate mounted source/current proof.",
    sha256: sha256(retainedCurrent),
  },
  coverage: [
    "current Cross-server mounted interaction and deferred summary ordering",
    "current parent Map bootstrap/poll/scan-completion ownership",
    "current Home auto-reconnect acknowledgement ordering",
    "accepted RetainedPages Activity/profile identity",
  ],
  preservation: "Owned adapters preserve historical assertions; new import bindings execute current helpers. Their writes are redirected to this task's milestone-b directory, and the original result-file hashes are asserted byte-identical before/after.",
  limits: "Controlled local React/jsdom/hook transports only. No native provider, server jump, Last War, live scan, network or gameplay action is invoked.",
};
realWriteFileSync(path.join(here, "affected-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_MAP_ENTRY_AFFECTED_OK checks=${acceptedOutputs.length + 1}`);
