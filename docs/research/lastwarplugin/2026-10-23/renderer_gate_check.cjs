// Executes the exact recovered renderer in an offline DOM with mocked native IPC.
// It neither launches LastWarPlugin nor sends a game/native command.
const fs = require("node:fs");
const path = require("node:path");
const { createRequire } = require("node:module");
let repo = __dirname;
while (!fs.existsSync(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"))) {
  const parent = path.dirname(repo);
  if (parent === repo) throw new Error("Cannot find LW-Control UI dependency root");
  repo = parent;
}
const uiRequire = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { JSDOM } = uiRequire("jsdom");
const html = fs.readFileSync(path.join(__dirname, "embedded-02.html"), "utf8");
const script = fs.readFileSync(path.join(__dirname, "scripts/embedded-02-inline-01.js"), "utf8");
const dom = new JSDOM(html, { url: "http://127.0.0.1/", runScripts: "outside-only" });
try {
  const window = dom.window;
  const calls = [];
  window.ipc = { postMessage(raw) { calls.push(JSON.parse(raw)); } };
  window.eval(script);
  window.lwInit({ lang: "en", mwMax: 5, profiles: [
    { id: "audit-A", name: "Audit A" }, { id: "audit-B", name: "Audit B" },
  ] });
  window.lwSetStatus("audit-A", "ing", 1);
  if (!window.document.getElementById("launchBtn").disabled || !window.anyConnecting() || window._launching)
    throw new Error("The intended connecting/busy precondition was not established");
  const tile = [...window.document.querySelectorAll(".tile")]
    .find(item => item.querySelector(".nm")?.textContent === "Audit B");
  tile.ondblclick();
  if (calls.length !== 1 || calls[0].action !== "launch" || calls[0].profile !== "audit-B")
    throw new Error("Recovered renderer did not reproduce the recorded IPC submission");
  console.log("LASTWARPLUGIN_RENDERER_GATE_REPRODUCED mock_ipc=true native_launch=false backend_admission=UNKNOWN");
} finally {
  dom.window.close();
}
