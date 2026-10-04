import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const browserDir = path.join(here, "browser");
const generatedDir = path.join(here, "generated");
const serverPort = 4344;
const debugPort = 4345;
const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
fs.mkdirSync(browserDir, { recursive: true });
assert.ok(fs.existsSync(chrome), `Chrome not found at ${chrome}`);

const pairManifest = JSON.parse(fs.readFileSync(path.join(here, "browser-pairs.json"), "utf8"));
assert.equal(pairManifest.marker, "LWB317_MAP_AUTO_REMOVE_C_BROWSER_PAIRS_PREPARED");
const pairs = pairManifest.pairs;
assert.equal(pairs.length, 4, "four milestone C browser pairs");

async function assertPortFree(port) {
  await new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", (error) => reject(new Error(`port ${port} unavailable: ${error.message}`)));
    server.listen(port, "127.0.0.1", () => server.close(resolve));
  });
}
await assertPortFree(serverPort);
await assertPortFree(debugPort);

const profile = fs.mkdtempSync(path.join(os.tmpdir(), "lwb317-map-auto-remove-c-"));
let server;
let chromeProcess;

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
async function waitForJson(url, attempts = 100) {
  let last;
  for (let i = 0; i < attempts; i += 1) {
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
async function waitForPage(url, attempts = 100) {
  let last;
  for (let i = 0; i < attempts; i += 1) {
    try {
      const response = await fetch(url, { cache: "no-store" });
      if (response.ok) return;
      last = new Error(`${response.status} ${response.statusText}`);
    } catch (error) {
      last = error;
    }
    await delay(50);
  }
  throw new Error(`timed out waiting for page server: ${last?.message || "unknown"}`);
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

const styleKeys = [
  "display", "flexDirection", "flexWrap", "alignItems", "justifyContent", "gap",
  "gridTemplateColumns", "gridTemplateRows", "columnGap", "rowGap",
  "width", "height", "minWidth", "minHeight",
  "paddingTop", "paddingRight", "paddingBottom", "paddingLeft",
  "marginTop", "marginRight", "marginBottom", "marginLeft",
  "borderTopWidth", "borderRightWidth", "borderBottomWidth", "borderLeftWidth",
  "borderTopColor", "borderRightColor", "borderBottomColor", "borderLeftColor",
  "borderRadius", "color", "backgroundColor", "fontFamily", "fontSize", "fontWeight",
  "lineHeight", "opacity", "cursor", "boxShadow", "overflow", "whiteSpace", "position",
];

const measurementExpression = `(() => {
  const panel = document.querySelector('.map-panel');
  if (!panel) throw new Error('missing .map-panel');
  const card = panel.querySelector('.map-auto-scan-card');
  if (!card) throw new Error('missing .map-auto-scan-card');
  const q = (selector, index = 0) => document.querySelectorAll(selector)[index] || null;
  const cq = (selector, index = 0) => card.querySelectorAll(selector)[index] || null;
  const direct = (selector, index = 0) => [...card.querySelectorAll(selector)].filter((el) => el.parentElement === card)[index] || null;
  const gridLabels = [...card.querySelectorAll('.map-auto-scan-grid > label')];
  const chipSpans = [...card.querySelectorAll('.map-auto-scan-server-chips > span')];
  const chipButtons = [...card.querySelectorAll('.map-auto-scan-server-chips > span > button')];
  const typeLabels = [...card.querySelectorAll('.map-controls .map-types--compact label')];
  const typeInputs = [...card.querySelectorAll('.map-controls .map-types--compact input[type=checkbox]')];
  const styleKeys = ${JSON.stringify(styleKeys)};
  const measure = (element) => {
    if (!element) return null;
    const rect = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    const before = getComputedStyle(element, '::before');
    return {
      tag: element.tagName,
      attrs: [...element.attributes].sort((a,b)=>a.name.localeCompare(b.name)).map(a=>[a.name,a.value]),
      text: element.textContent,
      rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
      styles: Object.fromEntries(styleKeys.map(key => [key, style[key]])),
      before: {
        content: before.content,
        display: before.display,
        color: before.color,
        opacity: before.opacity,
        width: before.width,
        height: before.height,
      },
    };
  };
  const anchors = {
    panel: measure(panel),
    card: measure(card),
    master: measure(cq('.map-auto-scan-master')),
    masterCheckbox: measure(cq('.map-auto-scan-master > input')),
    masterStrong: measure(cq('.map-auto-scan-master > strong')),
    masterStatus: measure(cq('.map-auto-scan-master > span')),
    grid: measure(cq('.map-auto-scan-grid')),
    serverField: measure(cq('.map-auto-scan-server-field')),
    serverLabel: measure(cq('.map-auto-scan-server-field > span', 0)),
    serverInputWrap: measure(cq('.map-auto-scan-server-input')),
    serverInput: measure(cq('.map-auto-scan-server-input > input')),
    addButton: measure(cq('.map-auto-scan-server-input > button')),
    serverHint: measure(cq('.map-auto-scan-server-field > small')),
    serverChips: measure(cq('.map-auto-scan-server-chips')),
    intervalLabel: measure(gridLabels[0]),
    intervalInput: measure(gridLabels[0]?.querySelector('input')),
    speedLabel: measure(gridLabels[1]),
    speedSelect: measure(gridLabels[1]?.querySelector('select')),
    controls: measure(cq('.map-controls')),
    typeGroup: measure(cq('.map-types--compact')),
    options: measure(cq('.map-auto-scan-options')),
    returnLabel: measure(cq('.map-auto-scan-options > label')),
    returnInput: measure(cq('.map-auto-scan-options > label > input')),
    runNow: measure(cq('.map-auto-scan-options > button')),
    notice: measure(direct('small', 0)),
    nextScan: measure(direct('small', 1)),
  };
  chipSpans.forEach((element,index) => anchors['serverChip'+index] = measure(element));
  chipButtons.forEach((element,index) => {
    anchors['serverChipButton'+index] = measure(element);
    anchors['serverChipChild'+index] = measure(element.firstElementChild);
  });
  typeLabels.forEach((element,index) => anchors['typeLabel'+index] = measure(element));
  typeInputs.forEach((element,index) => anchors['typeInput'+index] = measure(element));
  const dom = [card, ...card.querySelectorAll('*')].map((element) => ({
    tag: element.tagName,
    attrs: [...element.attributes].sort((a,b)=>a.name.localeCompare(b.name)).map(a=>[a.name,a.value]),
    directText: [...element.childNodes].filter(node=>node.nodeType===3).map(node=>node.textContent).join(''),
    childElements: [...element.children].map(child=>child.tagName),
  }));
  return {
    title: document.title,
    language: document.documentElement.lang,
    theme: document.documentElement.dataset.theme,
    viewport: {
      innerWidth, innerHeight,
      clientWidth: document.documentElement.clientWidth,
      clientHeight: document.documentElement.clientHeight,
      devicePixelRatio,
      scrollWidth: document.documentElement.scrollWidth,
      scrollHeight: document.documentElement.scrollHeight,
    },
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    anchors,
    dom,
  };
})()`;

function compareObjects(original, current) {
  const domMatch = JSON.stringify(original.dom) === JSON.stringify(current.dom);
  const geometryDifferences = [];
  const styleDifferences = [];
  const anchorNames = new Set([...Object.keys(original.anchors), ...Object.keys(current.anchors)]);
  for (const name of anchorNames) {
    const left = original.anchors[name];
    const right = current.anchors[name];
    if (!left || !right) {
      if (left || right) geometryDifferences.push({ anchor: name, original: left ? "present" : null, current: right ? "present" : null });
      continue;
    }
    for (const key of ["x", "y", "width", "height"]) {
      if (left.rect[key] !== right.rect[key]) {
        geometryDifferences.push({ anchor: name, property: key, original: left.rect[key], current: right.rect[key] });
      }
    }
    for (const key of styleKeys) {
      if (left.styles[key] !== right.styles[key]) {
        styleDifferences.push({ anchor: name, property: key, original: left.styles[key], current: right.styles[key] });
      }
    }
    for (const key of ["content", "display", "color", "opacity", "width", "height"]) {
      if (left.before[key] !== right.before[key]) {
        styleDifferences.push({ anchor: `${name}::before`, property: key, original: left.before[key], current: right.before[key] });
      }
    }
  }
  return { domMatch, geometryDifferences, styleDifferences };
}

let cdp;
try {
  server = spawn(process.execPath, [path.join(here, "serve-reference.mjs"), "--port", String(serverPort)], {
    cwd: repo,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
  await waitForPage(`http://127.0.0.1:${serverPort}/${pairs[0].id}-original.html`);

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
    `--remote-debugging-address=127.0.0.1`,
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

  const measurements = [];
  const consoleEntries = [];
  const comparisons = [];

  for (const pair of pairs) {
    const sides = {};
    for (const side of ["original", "current"]) {
      cdp.events.length = 0;
      await cdp.send("Emulation.setDeviceMetricsOverride", {
        width: pair.width,
        height: pair.height,
        deviceScaleFactor: 1,
        mobile: false,
      }, sessionId);
      const url = `http://127.0.0.1:${serverPort}/${pair.id}-${side}.html`;
      await cdp.send("Page.navigate", { url }, sessionId);
      for (let attempt = 0; attempt < 100; attempt += 1) {
        const state = await cdp.send("Runtime.evaluate", {
          expression: `document.readyState === 'complete' && location.href === ${JSON.stringify(url)} && document.title === ${JSON.stringify(`${pair.id} ${side}`)} && document.documentElement.lang === ${JSON.stringify(pair.language)} && document.documentElement.dataset.theme === ${JSON.stringify(pair.theme)}`,
          returnByValue: true,
        }, sessionId);
        if (state.result?.value === true) break;
        await delay(20);
        if (attempt === 99) throw new Error(`page did not complete: ${url}`);
      }
      await cdp.send("Runtime.evaluate", {
        expression: "document.fonts.ready.then(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))).then(() => true)",
        returnByValue: true,
        awaitPromise: true,
      }, sessionId);
      const evaluated = await cdp.send("Runtime.evaluate", {
        expression: measurementExpression,
        returnByValue: true,
        awaitPromise: true,
      }, sessionId);
      if (evaluated.exceptionDetails) throw new Error(`measurement exception: ${evaluated.exceptionDetails.text}`);
      const measurement = evaluated.result.value;
      assert.equal(measurement.viewport.innerWidth, pair.width, `${pair.id}/${side} viewport width`);
      assert.equal(measurement.viewport.innerHeight, pair.height, `${pair.id}/${side} viewport height`);
      assert.equal(measurement.viewport.devicePixelRatio, 1, `${pair.id}/${side} DPR`);
      assert.equal(measurement.timezone, "Asia/Singapore", `${pair.id}/${side} timezone`);

      const capture = await cdp.send("Page.captureScreenshot", {
        format: "png",
        fromSurface: true,
        captureBeyondViewport: false,
      }, sessionId);
      const screenshot = Buffer.from(capture.data, "base64");
      assert.equal(screenshot.readUInt32BE(0), 0x89504e47, `${pair.id}/${side} PNG signature`);
      const screenshotFile = path.join(browserDir, `${pair.id}-${side}.png`);
      fs.writeFileSync(screenshotFile, screenshot);

      const relevantEvents = cdp.events.filter((event) =>
        event.sessionId === sessionId
        && (
          (event.method === "Log.entryAdded" && ["warning", "error"].includes(event.params?.entry?.level))
          || event.method === "Runtime.exceptionThrown"
          || (event.method === "Runtime.consoleAPICalled" && ["warning", "error"].includes(event.params?.type))
        )
      );
      const record = {
        pairId: pair.id,
        caseId: pair.caseId,
        language: pair.language,
        theme: pair.theme,
        side,
        url,
        viewport: { width: pair.width, height: pair.height, deviceScaleFactor: 1 },
        measurement,
        screenshot: rel(screenshotFile),
        screenshotSha256: hash(screenshot),
        screenshotBytes: screenshot.length,
        consoleIssueCount: relevantEvents.length,
      };
      measurements.push(record);
      sides[side] = record;
      consoleEntries.push({
        pairId: pair.id,
        side,
        issues: relevantEvents.map((event) => ({ method: event.method, params: event.params })),
      });
    }

    const comparison = compareObjects(sides.original.measurement, sides.current.measurement);
    comparisons.push({
      pairId: pair.id,
      caseId: pair.caseId,
      language: pair.language,
      theme: pair.theme,
      viewport: { width: pair.width, height: pair.height, deviceScaleFactor: 1 },
      originalScreenshotSha256: sides.original.screenshotSha256,
      currentScreenshotSha256: sides.current.screenshotSha256,
      exactScreenshotBytes: sides.original.screenshotSha256 === sides.current.screenshotSha256,
      ...comparison,
    });
  }

  writeJson(path.join(browserDir, "measurements.json"), {
    marker: "LWB317_MAP_AUTO_REMOVE_C_BROWSER_MEASURED",
    browser: browserVersion,
    environment: {
      executable: chrome,
      timezone: "Asia/Singapore",
      deviceScaleFactor: 1,
      serverPort,
      debugPort,
      fontFamily: measurements[0]?.measurement?.anchors?.panel?.styles?.fontFamily,
    },
    records: measurements,
  });
  writeJson(path.join(browserDir, "console.json"), consoleEntries);
  writeJson(path.join(browserDir, "comparison-results.json"), {
    marker: "LWB317_MAP_AUTO_REMOVE_C_BROWSER_COMPARED",
    pairs: comparisons,
  });

  console.log(JSON.stringify({
    marker: "LWB317_MAP_AUTO_REMOVE_C_BROWSER_OK",
    pairs: comparisons.length,
    exactScreenshots: comparisons.filter((item) => item.exactScreenshotBytes).length,
    geometryDifferenceCount: comparisons.reduce((sum, item) => sum + item.geometryDifferences.length, 0),
    styleDifferenceCount: comparisons.reduce((sum, item) => sum + item.styleDifferences.length, 0),
    consoleIssues: consoleEntries.reduce((sum, item) => sum + item.issues.length, 0),
  }));

  assert.equal(comparisons.length, 4, "four milestone C browser comparisons");
  assert.ok(comparisons.every((pair) => pair.domMatch), "all four browser DOM pairs match");
  assert.ok(comparisons.every((pair) => pair.geometryDifferences.length === 0), "all four browser geometry pairs match");
  assert.ok(comparisons.every((pair) => pair.styleDifferences.length === 0), "all four browser style pairs match");
  assert.ok(comparisons.every((pair) => pair.exactScreenshotBytes), "all four screenshot pairs are byte-exact");

  await cdp.send("Browser.close").catch(() => {});
} finally {
  cdp?.close();
  if (server && !server.killed) server.kill();
  if (chromeProcess && chromeProcess.exitCode === null) {
    chromeProcess.kill();
    await Promise.race([
      new Promise((resolve) => chromeProcess.once("exit", resolve)),
      delay(2_000),
    ]);
  }
  const resolvedProfile = path.resolve(profile);
  assert.equal(path.dirname(resolvedProfile), path.resolve(os.tmpdir()), "cleanup profile must be an immediate OS temp child");
  assert.ok(path.basename(resolvedProfile).startsWith("lwb317-map-auto-remove-c-"), "cleanup only the task-owned temporary profile");
  try {
    fs.rmSync(resolvedProfile, { recursive: true, force: true, maxRetries: 20, retryDelay: 100 });
  } catch {
    // Chrome may leave a short-lived Windows file handle after Browser.close.
    // The profile lives under the OS temp directory and is not durable evidence.
  }
}
