import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { fn, raw, read } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const sourcePath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const source = read(sourcePath);
const node = fn(source, "RetainedPages");
assert.ok(node);
const exactFunction = raw(source, node);
const sha256 = (text) => crypto.createHash("sha256").update(text).digest("hex").toUpperCase();
const wrapper = 'import { Activity, Fragment } from "react";\nimport { PageForRoute } from "/src/Pages.jsx";\nimport { routes } from "/src/routes.js";\n\n' + exactFunction + '\n\nexport { RetainedPages as BrowserDialogRetainedPages };\n';
const generated = path.join(here, "browser-dialog-retained.jsx");
const manifestPath = path.join(here, "browser-dialog-source-manifest.json");
const manifest = {
  source: { path: sourcePath, sha256: sha256(source) },
  extractedFunction: { name: "RetainedPages", utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(exactFunction), sha256: sha256(exactFunction) },
  wrapper: { path: "browser-dialog-retained.jsx", sha256: sha256(wrapper) },
  limits: "Exact production RetainedPages function bytes with imports and export alias added for evidence harness. Actual PageForRoute/Pages/I18nProvider are imported through the existing Vite server. The harness controls route state only; it uses the existing squads-equipment recognized fixture and has no native transport/provider/updater/export/gameplay action.",
};
if (process.argv.includes("--prepare")) {
  fs.writeFileSync(generated, wrapper);
  fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
}
assert.equal(fs.readFileSync(generated, "utf8"), wrapper);
assert.deepEqual(JSON.parse(fs.readFileSync(manifestPath, "utf8")), manifest);
console.log("LWB317_SHELL_BROWSER_DIALOG_SOURCE_OK exact production wrapper pinned");
