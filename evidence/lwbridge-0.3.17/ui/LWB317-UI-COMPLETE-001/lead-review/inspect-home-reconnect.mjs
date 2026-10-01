// Read-only review harness: execute the checked-in Home callback against the
// actual frontend bridge and an in-memory WebView transport. No native host,
// configuration file, game, or service is contacted.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../../../..");
const appPath = path.join(root, "src/LWBridge.UI-0.3.17/src/App.jsx");
const source = fs.readFileSync(appPath, "utf8");
const match = source.match(/const updateAutoReconnect = useCallback\(async \(value\) => \{([\s\S]*?)\n  \}, \[\]\);/);
if (!match) throw new Error("Reviewed callback shape changed; inspect the new implementation.");
const { createBackendBridge } = await import(pathToFileURL(path.join(root, "src/LWBridge.UI-0.3.17/src/backendBridge.js")));
let onMessage;
const envelopes = [];
const errors = [];
const host = {
  __LWBridgeBootstrap: { mode: "live", sessionId: "offline-review-session", profiles: { selectedProfileId: "offline-review-profile" } },
  setTimeout, clearTimeout,
  chrome: { webview: {
    addEventListener(_name, callback) { onMessage = callback; },
    removeEventListener() {},
    postMessage(message) {
      envelopes.push(message);
      if (message.kind === "invoke") onMessage({ data: { kind: "response", sessionId: message.sessionId, id: message.id, ok: true, result: {} } });
    },
  } },
};
const bridge = createBackendBridge(host);
const callback = new Function("backendBridge", "setHomeBusy", "setHomeError", "setLocalConfig", `return async function(value) {${match[1]}}`)(bridge, () => {}, (value) => errors.push(value), () => {});
await callback(true);
bridge.dispose();
const invoke = envelopes.find((message) => message.command === "set_automation");
if (!invoke) throw new Error("No set_automation request was produced.");
console.log(JSON.stringify({
  selectedProfileId: bridge.profileId,
  command: invoke.command,
  payload: invoke.payload,
  profileIdPresent: Object.hasOwn(invoke.payload, "profileId"),
  transport: "in-memory frontend-only; successful reply is synthetic, not native validation",
}, null, 2));
