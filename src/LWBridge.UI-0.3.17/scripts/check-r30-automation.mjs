import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { retiredAutomationKey } from "./r30-retired-automation.mjs";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), React = req("react"), { build } = req("esbuild");
const dom = new JSDOM('<div id="root"></div>', { url: "http://localhost/" });
for (const key of ["window", "document", "HTMLElement", "Node", "MutationObserver", "localStorage", "Event", "MouseEvent"]) globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
const { createRoot } = req("react-dom/client");
const retired = ["Auto Training", "Automatic Construction", "Free Stamina", "Trucks", "Automatic Alliance Train Boarding", "Building Resource Collection", "Armed Truck", "Weekend Shield", "Attack Shield"];
for (const file of ["AutomationPage.jsx", "previewAutomationContracts.js", "previewAutomationFixtures.js"]) {
  const source = fs.readFileSync(path.join(root, "src", file), "utf8");
  for (const title of retired) assert.ok(!source.includes(title), `${file}: ${title} retired`);
}
for (const code of ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]) {
  const messages = (await import(`../src/locales/${code}.js`)).default;
  for (const [key, value] of Object.entries(messages)) {
    assert.ok(!retiredAutomationKey(key), `${code}: ${key} retired`);
    assert.ok(!retired.includes(value), `${code}: ${value} retired`);
  }
}
const bundle = await build({ stdin: { contents: 'export { NativeChatAutomationCard } from "./NativeChatAutomationCard.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const mod = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);
const { NativeChatAutomationCard, I18nProvider } = mod.exports;
const bridge = { currentProfileOwner: () => ({ profileId: "a", generation: 1 }), isCurrentProfileOwner: o => o.profileId === "a", invokeProfileScoped: async command => {
  if (command === "feature018_chat_config_get") return { enabled: false };
  if (command === "feature018_chat_status") return { services: {} };
  throw new Error(`Unexpected action: ${command}`);
} };
const flush = async fn => React.act(async () => { if (fn) await fn(); await new Promise(resolve => setTimeout(resolve, 0)); });
for (const title of ["Red Packet", "Fireworks / Egg", "Treasure"]) {
  let mounted;
  const mount = async () => { mounted = createRoot(document.getElementById("root")); await flush(() => mounted.render(React.createElement(I18nProvider, null, React.createElement(NativeChatAutomationCard, { title, description: "", online: false, profileId: "a", availableSquads: [], bridge })))); await flush(); };
  await mount();
  let trigger = document.querySelector(".automation-config-trigger");
  assert.equal(trigger.getAttribute("aria-expanded"), "false");
  assert.equal(document.querySelector("fieldset").style.display, "none");
  await flush(() => trigger.click());
  assert.equal(trigger.getAttribute("aria-expanded"), "true");
  await flush(() => mounted.unmount());
  await mount();
  trigger = document.querySelector(".automation-config-trigger");
  assert.equal(trigger.getAttribute("aria-expanded"), "true", `${title} remembers expansion`);
  await flush(() => trigger.click());
  await flush(() => mounted.unmount());
}
dom.window.close();
console.log("FEATURE018_R30_REMOVALS_CHAT_COLLAPSE_PASS");
