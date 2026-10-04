import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const { buildSync } = requireUi("esbuild");
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"));
const originalModal = asset.subarray(209017, 209017 + 1209).toString("utf8");
const originalRenderer = asset.subarray(376111, 376111 + 881).toString("utf8");
const current = fs.readFileSync(path.join(ui, "src/AppExitDialog.jsx"), "utf8");
const canonicalBody = current.replace(/^import[^\n]+\n/gm, "").replace(/export /g, "");
const icon = "data:image/png;base64," + fs.readFileSync(path.join(ui, "src/assets/icon-warning.png")).toString("base64");
const entry = `
import * as React from "react";
import * as M from "react/jsx-runtime";
import { createRoot } from "react-dom/client";
import en from "./src/locales/en.js";
import ja from "./src/locales/ja.js";
const { useEffect, useRef, useState } = React;
const b = React;
const params = new URLSearchParams(location.search);
const mode = params.get("mode") === "original" ? "original" : "current";
const language = params.get("lang") === "ja" ? "ja" : "en";
const busy = params.get("busy") !== "0";
const catalog = language === "ja" ? ja : en;
const translate = (key, values = {}) => (catalog[key] || key).replace(/\\{(\\w+)\\}/g, (match, name) => String(values[name] ?? match));
const useI18n = () => ({ t: translate });
const warningIcon = ${JSON.stringify(icon)};
const Ki = warningIcon;
const Original = (() => { ${originalModal}\n${originalRenderer}\nreturn Ji; })();
${canonicalBody}
const log = [];
const output = document.getElementById("observations");
function snapshot(label, event = null) {
  const dialog = document.querySelector("dialog");
  log.push({ label, mode, language, requestedBusy: busy, type: event?.type || null,
    trusted: event?.isTrusted ?? null, defaultPrevented: event?.defaultPrevented ?? null,
    open: dialog?.open ?? null, busy: dialog?.getAttribute("aria-busy") ?? null,
    activeElement: document.activeElement?.tagName || null,
    dialogStillMounted: Boolean(dialog), time: Math.round(performance.now()) });
  output.textContent = JSON.stringify(log, null, 2);
}
// Capture native DOM events without replacing or altering original/current handlers.
for (const name of ["keydown", "keyup", "cancel", "close", "focusin"]) {
  document.addEventListener(name, (event) => {
    if (name === "keydown" || name === "keyup") {
      if (event.key !== "Escape") return;
    }
    snapshot(name + ":capture", event);
    queueMicrotask(() => snapshot(name + ":after-dispatch", event));
    window.setTimeout(() => snapshot(name + ":next-task", event), 0);
  }, true);
}
function Fixture() {
  const [mounted, setMounted] = useState(true);
  useEffect(() => { window.setTimeout(() => snapshot("settled"), 100); }, []);
  const props = { instanceCount: 2, busy, onCancel: () => { snapshot("callback:onCancel"); setMounted(false); },
    onConfirm: () => snapshot("callback:onConfirm") };
  return mounted ? mode === "original"
    ? React.createElement(Original, { ...props, t: translate })
    : React.createElement(AppExitDialog, props) : null;
}
document.getElementById("metadata").textContent = JSON.stringify({ mode, language, busy, proof: "Actual exact original In/Ji vs canonical AppExitDialog body, same React/browser, inert callbacks; event capture only" });
createRoot(document.getElementById("root")).render(React.createElement(Fixture));
`;
const result = buildSync({ stdin: { contents: entry, resolveDir: ui, sourcefile: "browser-oracle-entry.jsx", loader: "jsx" }, bundle: true,
  write: false, format: "iife", platform: "browser", target: "es2022", jsxFactory: "React.createElement" });
const js = result.outputFiles[0].text;
fs.writeFileSync(path.join(here, "browser-oracle.bundle.js"), js);
const css = fs.readFileSync(path.join(ui, "src/reference.css"), "utf8");
const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Exact original/current busy exit browser oracle</title><style>${css}</style></head><body><p id="metadata"></p><pre id="observations" style="white-space:pre-wrap"></pre><div id="root"></div><script>${js.replace(/<\/script/gi, "<\\/script")}</script></body></html>`;
fs.writeFileSync(path.join(here, "browser-oracle.html"), html);
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
fs.writeFileSync(path.join(here, "browser-oracle-identities.json"), JSON.stringify({ originalAssetSha256: hash(asset), originalModal: { byte: 209017, length: 1209, sha256: hash(originalModal) }, originalRenderer: { byte: 376111, length: 881, sha256: hash(originalRenderer) }, canonicalAppExitDialogSha256: hash(current), bundledJsSha256: hash(js), htmlSha256: hash(html), limits: "No product edits, native invocation, game action, protected original runtime or invented Escape fix. Compare original/current with mode=original/current, busy=1/0 and lang=en/ja." }, null, 2) + "\n");
console.log("LWB317_BROWSER_EXIT_ORACLE_BUILT");
