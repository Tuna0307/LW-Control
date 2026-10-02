// Regenerates a re-printed (esbuild, no minification) copy of the original Map panel asset so the
// minified source can be read and cited by line. The asset is hash-checked; printing changes
// layout only, never identifiers or logic. Byte locators in docs are UTF-8 offsets into the
// ORIGINAL asset, not into this re-print.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { transformSync } = require("esbuild");
const asset = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js");
const bytes = fs.readFileSync(asset);
assert.equal(crypto.createHash("sha256").update(bytes).digest("hex"), "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");
const pretty = transformSync(bytes.toString("utf8"), { loader: "js", minify: false, target: "esnext" }).code;
fs.writeFileSync(path.join(here, "reference/MapDataPanel.pretty.js"), pretty);
console.log(`pretty copy: ${pretty.split("\n").length} lines sha256=${crypto.createHash("sha256").update(pretty).digest("hex")}`);
