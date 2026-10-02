import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const relative = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js";
const bytes = fs.readFileSync(path.join(repo, relative));
const source = bytes.toString("utf8");
const sha256 = crypto.createHash("sha256").update(bytes).digest("hex");
assert.equal(sha256, "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");

function walk(node, result = []) {
  if (!node || typeof node !== "object") return result;
  if (node.type) result.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, result));
    else if (value && typeof value === "object") walk(value, result);
  }
  return result;
}

const ast = parse(source, { sourceType: "module" });
const nodes = walk(ast);
const component = nodes.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "R");
assert.ok(component, "Map component R");
const componentNodes = walk(component);
const raw = (node) => source.slice(node.start, node.end);
const record = (node) => ({
  byte: Buffer.byteLength(source.slice(0, node.start)),
  expression: raw(node),
});
const functionNode = (name) => nodes.find((node) => node.type === "FunctionDeclaration" && node.id?.name === name);
const effect = (marker) => componentNodes.find((node) =>
  node.type === "CallExpression"
  && raw(node.callee).includes("useEffect")
  && raw(node).includes(marker));

const helper = functionNode("ye");
const search = functionNode("rr");
const tabHandler = functionNode("ir");
const serverTransition = effect("D.current.clear(),C.serverId<=0");
const tabSelection = effect("if(!R){T.current+=1,V(1),U([]),W(0);return}rr(B)");
assert.ok(helper && search && tabHandler && serverTransition && tabSelection);

const contract = {
  evidenceState: "EXACT_BYTES",
  source: { path: relative, sha256 },
  cacheHelper: record(helper),
  serverTransitionEffect: record(serverTransition),
  tabSelectionEffect: record(tabSelection),
  search: record(search),
  tabHandler: record(tabHandler),
};

assert.equal(contract.cacheHelper.byte, 554);
assert.ok(Math.abs(contract.serverTransitionEffect.byte - 33502) < 300);
assert.ok(Math.abs(contract.search.byte - 38513) < 300);
assert.ok(Math.abs(contract.tabHandler.byte - 39238) < 300);

fs.writeFileSync(path.join(here, "source-contract.json"), JSON.stringify(contract, null, 2) + "\n");
console.log(JSON.stringify(contract, null, 2));
