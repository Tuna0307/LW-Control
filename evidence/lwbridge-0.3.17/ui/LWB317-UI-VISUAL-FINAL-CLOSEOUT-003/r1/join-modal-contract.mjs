import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { fn, raw, read } from "../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const outputPath = path.join(here, "join-modal-contract-results.json");
const verifyOnly = process.argv.includes("--verify");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const main = read("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const join = read("src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx");
const originalNode = fn(main, "In");
const currentNode = fn(join, "JoinModal");
const original = raw(main, originalNode);
const current = raw(join, currentNode);

const required = [
  ["native dialog", /[`\"]dialog[`\"]/],
  ["app-dialog class", /app-dialog/],
  ["modal aria", /aria-modal/],
  ["busy aria", /aria-busy/],
  ["showModal", /showModal\(\)/],
  ["close on cleanup", /\.close\(\)/],
  ["focus restore", /focus\(\{\s*preventScroll/],
  ["cancel prevention", /preventDefault\(\)/],
  ["Tab focus trap query", /querySelectorAll\([`\"]button, \[href\], input, select, textarea, \[tabindex\]/],
  ["stop propagation", /stopPropagation\(\)/],
  ["pointer backdrop origin", /PointerDown|onPointerDown/],
];
for (const [label, pattern] of required) {
  assert.match(original, pattern, `recovered modal lost ${label}`);
  assert.match(current, pattern, `current modal missing recovered ${label}`);
}
assert.doesNotMatch(current, /onMouseDown/, "current join modal must not use the replaced backdrop-mousedown close path");
assert.doesNotMatch(current, /addEventListener\([`\"]keydown/, "current join modal must keep Escape/cancel ownership on the native dialog");

const result = {
  marker: "LWB317_FINAL_CLOSEOUT_JOIN_MODAL_CONTRACT_OK",
  original: {
    byteOffset: Buffer.byteLength(main.slice(0, originalNode.start)),
    byteLength: Buffer.byteLength(original),
    sha256: sha(original),
  },
  current: {
    byteOffset: Buffer.byteLength(join.slice(0, currentNode.start)),
    byteLength: Buffer.byteLength(current),
    sha256: sha(current),
  },
  recoveredDefaults: { busy: false, dismissOnBackdrop: false },
  checks: required.map(([label]) => label),
  scriptSha256: sha(fs.readFileSync(fileURLToPath(import.meta.url))),
};
if (verifyOnly) {
  assert.deepEqual(result, JSON.parse(fs.readFileSync(outputPath, "utf8")), "recorded join-modal contract is stale");
} else {
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: result.marker, checks: result.checks.length, verified: verifyOnly }));
