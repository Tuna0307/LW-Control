import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const req = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const parse = req("@babel/parser").parse;
const traverse = req("@babel/traverse").default;
const sha = (b) => crypto.createHash("sha256").update(b).digest("hex").toUpperCase();
const assets = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/";
const squad = fs.readFileSync(path.join(repo, assets, "SquadPanel-HC3-DJei.js"));
const main = fs.readFileSync(path.join(repo, assets, "index-BVfnK1wp.js"));
assert.equal(sha(squad), "ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7");
assert.equal(sha(main), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const start = squad.indexOf(Buffer.from("var Ee=(0,O.createContext)({});"));
const end = squad.indexOf(Buffer.from("var td=$u,nd="));
assert.equal(start, 50624); assert.equal(end, 175682);
const closure = squad.subarray(start, end);
const helperStart = main.indexOf(Buffer.from("var e=Object.create,"));
const helperEnd = main.indexOf(Buffer.from("(function(){"), helperStart);
const helper = main.subarray(helperStart, helperEnd);
assert.ok(helperStart > 0 && helperEnd > helperStart);
const code = closure.toString();
const ast = parse(code, { sourceType: "module" });
let globals, graph;
traverse(ast, { Program(program) {
  globals = Object.keys(program.scope.globals).sort();
  graph = {};
  for (const [name, binding] of Object.entries(program.scope.bindings)) {
    const dependencies = new Set();
    binding.path.traverse({ ReferencedIdentifier(p) {
      const resolved = p.scope.getBinding(p.node.name);
      if (resolved && resolved.scope === program.scope && resolved !== binding) dependencies.add(p.node.name);
    } });
    graph[name] = [...dependencies].sort();
  }
  program.stop();
} });
assert.deepEqual(globals.filter((name) => !["A", "O", "l", "s", "n"].includes(name)), ["AbortController", "Array", "Element", "Error", "EventTarget", "HTMLButtonElement", "HTMLElement", "IntersectionObserver", "JSON", "Map", "Math", "Number", "Object", "PointerEvent", "Promise", "Proxy", "ResizeObserver", "Set", "Symbol", "WeakMap", "WeakSet", "arguments", "document", "getComputedStyle", "isNaN", "navigator", "parseFloat", "parseInt", "performance", "queueMicrotask", "requestAnimationFrame", "window"].sort());
const prefix = `// Exact Motion/Framer Motion dependency closure recovered from the owner-supplied\n// LWBridge 0.3.17 SquadPanel asset. Source version is UNKNOWN; no package is guessed.\n// SPDX-License-Identifier: MIT\n// Attribution: Motion B.V. / Framer B.V.; see equipmentMotion317.NOTICE.md.\nimport * as O from "react";\nimport * as A from "react/jsx-runtime";\nconst { d: n, o: s, c: l } = (() => {\n`;
const helperSuffix = "\nreturn {d,o,c};\n})();\n";
const suffix = "\nexport { $u as motion, rl as AnimatePresence, ed as useReducedMotion };\n";
const vendor = prefix + helper.toString() + helperSuffix + code + suffix;
const output = path.join(repo, "src/LWBridge.UI-0.3.17/src/vendor/equipmentMotion317.js");
const manifest = {
  marker: "LWB317_MOTION_CLOSURE_EXACT", source: assets + "SquadPanel-HC3-DJei.js", sourceSha256: sha(squad),
  closure: { utf8ByteOffset: start, utf8ByteLength: closure.length, sha256: sha(closure) },
  helper: { source: assets + "index-BVfnK1wp.js", sourceSha256: sha(main), utf8ByteOffset: helperStart, utf8ByteLength: helper.length, sha256: sha(helper) },
  vendorSha256: sha(vendor), globals, roots: ["$u", "rl", "ed"], graph,
  imports: ["react", "react/jsx-runtime"],
  exclusions: ["No original main/app import", "No original React bundle", "No original bridge/auth/native/service code", "No original startup/modulepreload side effect", "No guessed package version or animation defaults"],
};
if (process.argv.includes("--record")) {
  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, vendor);
  fs.writeFileSync(path.join(here, "closure-manifest.json"), JSON.stringify(manifest, null, 2) + "\n");
} else {
  assert.equal(fs.readFileSync(output, "utf8"), vendor);
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "closure-manifest.json"), "utf8")), manifest);
}
console.log(`LWB317_MOTION_CLOSURE_OK bytes=${closure.length} bindings=${Object.keys(graph).length} imports=2`);
