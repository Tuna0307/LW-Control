import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const browserDir = path.join(here, "browser");
const serverPort = 4340;
const debugPort = 4341;
const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = fs.mkdtempSync(path.join(os.tmpdir(), "lwb317-map-manual-types-"));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
fs.mkdirSync(browserDir, { recursive: true });
assert.ok(fs.existsSync(chrome), `Chrome not found at ${chrome}`);

async function assertPortFree(port) {
  await new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", (error) => reject(new Error(`port ${port} unavailable: ${error.message}`)));
    server.listen(port, "127.0.0.1", () => server.close(resolve));
  });
}

async function waitForPage(url, attempts = 120) {
  let last;
  for (let index = 0; index < attempts; index += 1) {
    try {
      const response = await fetch(url, { cache: "no-store" });
      if (response.ok) return;
      last = new Error(`${response.status} ${response.statusText}`);
    } catch (error) {
      last = error;
    }
    await delay(50);
  }
  throw new Error(`timed out waiting for ${url}: ${last?.message || "unknown"}`);
}

async function waitForJson(url, attempts = 120) {
  let last;
  for (let index = 0; index < attempts; index += 1) {
    try {
      const response = await fetch(url, { cache: "no-store" });
      if (response.ok) return await response.json();
      last = new Error(`${response.status} ${response.statusText}`);
    } catch (error) {
      last = error;
    }
    await delay(50);
  }
  throw new Error(`timed out waiting for ${url}: ${last?.message || "unknown"}`);
}

class Cdp {
  constructor(url) {
    this.url = url;
    this.nextId = 1;
    this.pending = new Map();
    this.events = [];
  }
  async open() {
    this.ws = new WebSocket(this.url);
    await new Promise((resolve, reject) => {
      this.ws.addEventListener("open", resolve, { once: true });
      this.ws.addEventListener("error", reject, { once: true });
    });
    this.ws.addEventListener("message", (event) => {
      const message = JSON.parse(event.data);
      if (message.id) {
        const pending = this.pending.get(message.id);
        if (!pending) return;
        this.pending.delete(message.id);
        if (message.error) pending.reject(new Error(`${message.error.code}: ${message.error.message}`));
        else pending.resolve(message.result);
      } else {
        this.events.push(message);
      }
    });
  }
  send(method, params = {}, sessionId = undefined) {
    const id = this.nextId++;
    const message = { id, method, params };
    if (sessionId) message.sessionId = sessionId;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.ws.send(JSON.stringify(message));
    });
  }
  close() {
    try { this.ws?.close(); } catch {}
  }
}

await assertPortFree(serverPort);
await assertPortFree(debugPort);
let vite;
let chromeProcess;
let cdp;
try {
  vite = spawn(process.execPath, [path.join(ui, "node_modules/vite/bin/vite.js"), "--host", "127.0.0.1", "--port", String(serverPort), "--strictPort"], {
    cwd: ui,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
  const url = `http://127.0.0.1:${serverPort}/?view=map-data&previewState=map-idle&previewTheme=light&previewLanguage=en`;
  await waitForPage(url);

  chromeProcess = spawn(chrome, [
    "--headless=new",
    "--disable-gpu",
    "--hide-scrollbars",
    "--no-first-run",
    "--disable-background-networking",
    "--disable-default-apps",
    "--disable-sync",
    "--metrics-recording-only",
    "--force-device-scale-factor=1",
    "--window-size=1280,900",
    "--remote-debugging-address=127.0.0.1",
    `--remote-debugging-port=${debugPort}`,
    `--user-data-dir=${profile}`,
    "about:blank",
  ], {
    cwd: repo,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });

  const versionEndpoint = await waitForJson(`http://127.0.0.1:${debugPort}/json/version`);
  cdp = new Cdp(versionEndpoint.webSocketDebuggerUrl);
  await cdp.open();
  const browserVersion = await cdp.send("Browser.getVersion");
  const { targetId } = await cdp.send("Target.createTarget", { url: "about:blank" });
  const { sessionId } = await cdp.send("Target.attachToTarget", { targetId, flatten: true });
  await cdp.send("Page.enable", {}, sessionId);
  await cdp.send("Runtime.enable", {}, sessionId);
  await cdp.send("Log.enable", {}, sessionId);
  await cdp.send("Emulation.setTimezoneOverride", { timezoneId: "Asia/Singapore" }, sessionId);
  await cdp.send("Emulation.setDeviceMetricsOverride", { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false }, sessionId);
  await cdp.send("Page.navigate", { url }, sessionId);

  for (let attempt = 0; attempt < 120; attempt += 1) {
    const ready = await cdp.send("Runtime.evaluate", {
      expression: "document.readyState === 'complete' && !!document.querySelector('.map-panel') && document.querySelectorAll('.map-controls .map-types--compact input[type=checkbox]').length === 8",
      returnByValue: true,
    }, sessionId);
    if (ready.result?.value === true) break;
    await delay(25);
    if (attempt === 119) throw new Error("Map Manual controls did not become ready");
  }

  const readState = async () => {
    const result = await cdp.send("Runtime.evaluate", {
      expression: `(() => [...document.querySelectorAll('.map-controls .map-types--compact label')].map((label, index) => {
        const input = label.querySelector('input[type=checkbox]');
        return { index, text: label.textContent.trim(), checked: input.checked, disabled: input.disabled };
      }))()`,
      returnByValue: true,
    }, sessionId);
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  };
  const clickIndex = async (index) => {
    const result = await cdp.send("Runtime.evaluate", {
      expression: `(() => { const inputs = [...document.querySelectorAll('.map-controls .map-types--compact input[type=checkbox]')]; const input = inputs[${index}]; if (!input) throw new Error('missing input ${index}'); input.click(); return { checked: input.checked, disabled: input.disabled }; })()`,
      returnByValue: true,
    }, sessionId);
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    await cdp.send("Runtime.evaluate", {
      expression: "new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))).then(() => true)",
      returnByValue: true,
      awaitPromise: true,
    }, sessionId);
  };

  const initial = await readState();
  assert.equal(initial.length, 8);
  assert.equal(initial.filter((entry) => entry.checked).length, 8, "preview begins with all eight Manual types selected");
  const steps = [];
  for (let index = 0; index < initial.length; index += 1) {
    const before = await readState();
    assert.equal(before[index].disabled, false, `Manual checkbox ${index} remains enabled before deselection`);
    await clickIndex(index);
    const after = await readState();
    assert.equal(after[index].checked, false, `Manual checkbox ${index} deselected in browser`);
    steps.push({ action: `deselect-${index}`, checked: after.filter((entry) => entry.checked).map((entry) => entry.index) });
  }
  const empty = await readState();
  assert.equal(empty.filter((entry) => entry.checked).length, 0, "real browser can deselect the final Manual type");
  assert.equal(empty.some((entry) => entry.disabled), false, "empty Manual state leaves all types selectable");

  await clickIndex(1);
  const finalState = await readState();
  assert.deepEqual(finalState.filter((entry) => entry.checked).map((entry) => entry.index), [1], "selecting Resource after empty selects only Resource");

  const screenshotData = await cdp.send("Page.captureScreenshot", { format: "png", fromSurface: true, captureBeyondViewport: false }, sessionId);
  const screenshot = Buffer.from(screenshotData.data, "base64");
  const screenshotFile = path.join(browserDir, "manual-empty-to-resource.png");
  fs.writeFileSync(screenshotFile, screenshot);

  const issues = cdp.events.filter((event) =>
    event.sessionId === sessionId
    && (
      (event.method === "Log.entryAdded" && ["warning", "error"].includes(event.params?.entry?.level))
      || event.method === "Runtime.exceptionThrown"
      || (event.method === "Runtime.consoleAPICalled" && ["warning", "error"].includes(event.params?.type))
    )
  ).map((event) => ({ method: event.method, params: event.params }));

  const result = {
    marker: "LWB317_MAP_MANUAL_TYPES_BROWSER_OK",
    browser: browserVersion,
    url,
    viewport: { width: 1280, height: 900, deviceScaleFactor: 1 },
    initial,
    steps,
    empty,
    final: finalState,
    clickedControls: ["Manual scan-type checkboxes only"],
    nativeActionsInvoked: [],
    screenshot: rel(screenshotFile),
    screenshotSha256: hash(screenshot),
    screenshotBytes: screenshot.length,
    consoleIssueCount: issues.length,
  };
  fs.writeFileSync(path.join(browserDir, "results.json"), `${JSON.stringify(result, null, 2)}\n`);
  fs.writeFileSync(path.join(browserDir, "console.json"), `${JSON.stringify({ issues }, null, 2)}\n`);
  assert.equal(issues.length, 0, "offline browser console warnings/errors");
  console.log(JSON.stringify({ marker: result.marker, finalSelectedIndexes: [1], consoleIssues: issues.length, screenshot: result.screenshot }));

  await cdp.send("Browser.close").catch(() => {});
} finally {
  cdp?.close();
  if (vite && !vite.killed) vite.kill();
  if (chromeProcess && chromeProcess.exitCode === null) chromeProcess.kill();
  const resolvedProfile = path.resolve(profile);
  assert.equal(path.dirname(resolvedProfile), path.resolve(os.tmpdir()));
  assert.ok(path.basename(resolvedProfile).startsWith("lwb317-map-manual-types-"));
  try { fs.rmSync(resolvedProfile, { recursive: true, force: true, maxRetries: 20, retryDelay: 100 }); } catch {}
}
