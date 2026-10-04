import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const { parse } = createRequire(path.join(ui, "package.json"))("@babel/parser");
const source = fs.readFileSync(path.join(ui, "src/App.jsx"), "utf8");
const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
const declaration = ast.program.body.flatMap((node) => node.type === "VariableDeclaration" ? node.declarations : []).find((node) => node.id?.name === "nativeAssetReader");
assert.ok(declaration);
const expression = source.slice(declaration.init.start, declaration.init.end);
const cases = [];
for (const mode of ["preview", "native-unavailable", "native"]) {
  const calls = [];
  const bridge = { mode, available: mode === "native", invoke: async (...args) => { calls.push(args); return { dataUrl: "INERT" }; } };
  const reader = new Function("backendBridge", "return (" + expression + ");")(bridge);
  if (bridge.available) {
    assert.equal(typeof reader, "function");
    const request = { assetPath: "icons/a" };
    assert.deepEqual(await reader(request), { dataUrl: "INERT" });
    assert.deepEqual(calls, [["game_asset_image", request]]);
  } else { assert.equal(reader, null); assert.deepEqual(calls, []); }
  cases.push({ mode, available: bridge.available, reader: reader === null ? "null" : "function", calls, pass: true });
}
assert.match(source, /<GameAssetImageProvider readImage=\{nativeAssetReader\}>/);
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const report = { marker: "LWB317_GAME_ASSET_READER_BOUNDARY_OK", cases, source: { AppSha256: hash(source), expression,
  utf8ByteOffset: Buffer.byteLength(source.slice(0, declaration.init.start)), utf8ByteLength: Buffer.byteLength(expression), sha256: hash(expression) },
  limits: "Executes the actual module-level reader initializer with inert bridge objects. Checks canonical provider binding by source. No actual native reader, resolved game assets or live producer availability is proven." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "reader-boundary-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report));
