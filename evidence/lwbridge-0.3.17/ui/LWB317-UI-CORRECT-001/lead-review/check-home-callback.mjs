// Read-only check of the actual Home callback and bridge with deferred responses.
// No native host, preferences, game or service is contacted.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../../../..");
const source = fs.readFileSync(path.join(root, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
const body = source.match(/const updateAutoReconnect = useCallback\(async \(value\) => \{([\s\S]*?)\n  \}, \[\]\);/)?.[1];
assert.ok(body, "Home callback extraction failed; review changed source.");
const { createBackendBridge } = await import(pathToFileURL(path.join(root, "src/LWBridge.UI-0.3.17/src/backendBridge.js")));
for (const outcome of ["acknowledged", "rejected", "missing-profile"]) {
  let handler;
  const posted = [];
  const events = [];
  let config = { autoReconnect: false };
  const host = {
    __LWBridgeBootstrap: { mode: "live", sessionId: "review", profiles: { selectedProfileId: outcome === "missing-profile" ? "" : "review-profile" } },
    setTimeout, clearTimeout,
    chrome: { webview: { addEventListener(_event, callback) { handler = callback; }, removeEventListener() {}, postMessage(message) { posted.push(message); } } },
  };
  const bridge = createBackendBridge(host);
  const callback = new Function("backendBridge", "setHomeBusy", "setHomeError", "setLocalConfig", `return async function(value) {${body}}`)(
    bridge, (value) => events.push(["busy", value]), (value) => events.push(["error", value]),
    (update) => { config = update(config); events.push(["config", config.autoReconnect]); },
  );
  const pending = callback(true);
  assert.deepEqual(events[0], ["busy", "autoReconnect"]);
  assert.equal(config.autoReconnect, false, "No checked-state update before response.");
  if (outcome === "missing-profile") assert.equal(posted.length, 0);
  else {
    assert.equal(posted[0].payload.profileId, "review-profile");
    handler({ data: { kind: "response", sessionId: "review", id: posted[0].id, ok: outcome === "acknowledged", result: {}, error: { code: "TEST_REJECTED", message: "review rejection" } } });
  }
  await pending;
  assert.equal(config.autoReconnect, outcome === "acknowledged");
  assert.deepEqual(events.at(-1), ["busy", ""]);
  if (outcome !== "acknowledged") assert.ok(events.some(([kind, value]) => kind === "error" && value));
  bridge.dispose();
  console.log(`HOME_CALLBACK_${outcome.toUpperCase().replaceAll("-", "_")}_OK`);
}
