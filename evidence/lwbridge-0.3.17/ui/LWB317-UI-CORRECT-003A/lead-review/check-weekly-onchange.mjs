import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createConfigDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js";
import { initialAutomationDraft } from "../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../../../..");
const ui = path.join(root, "src/LWBridge.UI-0.3.17");
const require = createRequire(path.join(ui, "package.json"));
const { parse } = require("@babel/parser");
const pages = fs.readFileSync(path.join(ui, "src/Pages.jsx"), "utf8");
const ast = parse(pages, { sourceType: "module", plugins: ["jsx"] });
const callbacks = [];
function visit(node) {
  if (!node || typeof node !== "object") return;
  if (node.type === "JSXOpeningElement" && node.name?.name === "WeeklyQualityPreview") {
    const attribute = node.attributes.find((entry) => entry.name?.name === "onChange");
    const expression = attribute.value.expression;
    callbacks.push({ expression: pages.slice(expression.start, expression.end), line: expression.loc.start.line });
  }
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(visit);
    else if (value && typeof value === "object") visit(value);
  }
}
visit(ast);
if (callbacks.length !== 2) throw Error(`Expected the two weekly UI callbacks; found ${callbacks.length}`);

// Invoke actual callback expressions extracted from Pages.jsx against the real
// config store. There is no manually added flush, blur, or elapsed debounce.
const results = [];
for (const [index, callback] of callbacks.entries()) {
  const title = ["Trucks", "Secret Task"][index];
  const initial = initialAutomationDraft(title, "automation-config");
  let writes = 0;
  const pendingReplies = [];
  const store = createConfigDraft(structuredClone(initial), {
    valid: () => true,
    read: async () => structuredClone(initial),
    write: (draft) => { writes++; return new Promise((resolve) => pendingReplies.push(() => resolve(structuredClone(draft)))); },
  });
  const next = [...initial.weeklyQualities];
  next[0] = next[0] === "ur" ? "ssr" : "ur";
  const onChange = new Function("config", `return (${callback.expression});`)({ store });
  onChange(next);
  const snapshot = store.getSnapshot();
  results.push({ title, line: callback.line, callback: callback.expression,
    expectedImmediateWrites: 1, actualImmediateWrites: writes,
    saving: snapshot.saving, dirty: snapshot.dirty,
    draftMatchesEdit: JSON.stringify(snapshot.draft.weeklyQualities) === JSON.stringify(next),
    passed: writes === 1 && snapshot.saving });
  store.dispose();
  pendingReplies.forEach((resolve) => resolve());
}
console.log(JSON.stringify({ sourceSha256: crypto.createHash("sha256").update(pages).digest("hex"), results }, null, 2));
if (results.some((result) => !result.passed)) process.exitCode = 1;
