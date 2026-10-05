import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createAppHarness } from "../../LWB317-UI-VISUAL-REMAINING-002/milestone-5/map-entry-current-harness.mjs";
import { runEquipmentSaveContract } from "./equipment-save-contract.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const resultPath = path.join(here, "final-current-results.json");
const manifestPath = path.join(here, "final-current-manifest.json");
const captureDir = path.join(here, "browser-current");
const args = new Set(process.argv.slice(2));
const record = args.has("--record");
const replace = args.has("--replace");
const portArg = process.argv.slice(2).find((value) => /^\d+$/.test(value));
const port = Number(portArg || 4319);
const baseUrl = `http://127.0.0.1:${port}/`;
const chromeExe = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const playwrightPackage = "C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/node_modules/playwright/package.json";
const requirePlaywright = createRequire(playwrightPackage);
const { chromium } = requirePlaywright("playwright");
const requireUi = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const parser = requireUi("@babel/parser");

const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const relative = (file) => path.relative(repo, file).replaceAll("\\", "/");
const hashFile = (file) => ({ bytes: fs.statSync(file).size, sha256: sha256(fs.readFileSync(file)) });
const describe = (file) => ({ path: relative(file), ...hashFile(file) });
function treeDigest(root) {
  const entries = [];
  const walk = (folder) => {
    for (const item of fs.readdirSync(folder, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const full = path.join(folder, item.name);
      if (item.isDirectory()) walk(full);
      else if (item.isFile()) entries.push({ path: path.relative(root, full).replaceAll("\\", "/"), ...hashFile(full) });
    }
  };
  walk(root);
  return {
    path: root.replaceAll("\\", "/"),
    fileCount: entries.length,
    bytes: entries.reduce((sum, entry) => sum + entry.bytes, 0),
    sha256: sha256(Buffer.from(JSON.stringify(entries))),
  };
}

if (record && fs.existsSync(resultPath) && !replace) throw new Error("final-current-results.json already exists; use --replace only for an intentional pre-review refresh");
if (record && fs.existsSync(manifestPath) && !replace) throw new Error("final-current-manifest.json already exists; use --replace only for an intentional pre-review refresh");

const LOCAL_EXTENSIONS = ["", ".js", ".jsx", ".mjs", ".css", ".json", ".png", ".svg", ".webp", ".jpg", ".jpeg", ".gif"];
function resolveLocalSpecifier(importer, specifier) {
  if (!specifier.startsWith(".")) return null;
  const clean = specifier.split(/[?#]/, 1)[0];
  const base = path.resolve(path.dirname(importer), clean);
  for (const extension of LOCAL_EXTENSIONS) {
    const candidate = `${base}${extension}`;
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  for (const extension of [".js", ".jsx", ".mjs", ".css", ".json"]) {
    const candidate = path.join(base, `index${extension}`);
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }
  throw new Error(`unresolved local dependency ${specifier} from ${relative(importer)}`);
}
function moduleSpecifiers(source) {
  const found = new Set();
  for (const pattern of [
    /\b(?:import|export)\s+[\s\S]*?\bfrom\s*["']([^"']+)["']/g,
    /\bimport\s*["']([^"']+)["']/g,
    /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g,
  ]) {
    let match;
    while ((match = pattern.exec(source))) found.add(match[1]);
  }
  return [...found];
}
function cssSpecifiers(source) {
  const found = [];
  const pattern = /url\(\s*["']?([^"')]+)["']?\s*\)/g;
  let match;
  while ((match = pattern.exec(source))) {
    const specifier = match[1].trim();
    if (!specifier || /^(?:data:|https?:|#|\/)/.test(specifier)) continue;
    found.push(specifier);
  }
  return found;
}
function collectAppDependencyClosure() {
  const uiRoot = path.join(repo, "src/LWBridge.UI-0.3.17");
  const visited = new Set([path.join(uiRoot, "index.html")]);
  const queue = [path.join(uiRoot, "src/main.jsx")];
  while (queue.length) {
    const file = queue.shift();
    if (visited.has(file)) continue;
    visited.add(file);
    const extension = path.extname(file).toLowerCase();
    if (![".js", ".jsx", ".mjs", ".css"].includes(extension)) continue;
    const source = fs.readFileSync(file, "utf8");
    const specifiers = extension === ".css" ? cssSpecifiers(source) : moduleSpecifiers(source);
    for (const specifier of specifiers) {
      const resolved = resolveLocalSpecifier(file, specifier);
      if (resolved && !visited.has(resolved)) queue.push(resolved);
    }
  }
  return [...visited].map(relative).sort();
}
function closureHashes() {
  return Object.fromEntries(collectAppDependencyClosure().map((name) => [name, sha256(fs.readFileSync(path.join(repo, name)))]));
}
const closureDigest = (closure) => sha256(Buffer.from(JSON.stringify(closure)));
const normalizeServedIndex = (source) => source
  .replace(/<script type="module">import \{ injectIntoGlobalHook \}[\s\S]*?<\/script>\s*/g, "")
  .replace(/\s*<script type="module" src="\/@vite\/client"><\/script>\s*/g, "\n")
  .replace(/\?t=\d+(?=")/g, "")
  .replace(/\r\n/g, "\n")
  .replace(/^\s+/gm, "")
  .replace(/\n{2,}/g, "\n")
  .trim();

async function verifyServedClosure(browser, sourceHashes) {
  const prefix = "src/LWBridge.UI-0.3.17/";
  const context = await browser.newContext();
  const page = await context.newPage();
  const matched = {};
  try {
    await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
    const servedIndex = await (await fetch(baseUrl)).text();
    const diskIndex = fs.readFileSync(path.join(repo, `${prefix}index.html`), "utf8");
    assert.equal(normalizeServedIndex(servedIndex), normalizeServedIndex(diskIndex), "Vite endpoint index does not match current workspace index after known dev-server injection");
    matched[`${prefix}index.html`] = sourceHashes[`${prefix}index.html`];
    for (const [name, expectedHash] of Object.entries(sourceHashes)) {
      if (name === `${prefix}index.html`) continue;
      assert.ok(name.startsWith(prefix), `served closure path outside UI root: ${name}`);
      const served = name.slice(prefix.length);
      const url = new URL(`/${served}`, baseUrl);
      const extension = path.extname(served).toLowerCase();
      let actualHash;
      if ([".js", ".jsx", ".mjs", ".css", ".json", ".svg"].includes(extension)) {
        const rawUrl = `${url.href}${url.search ? "&" : "?"}raw`;
        const raw = await page.evaluate(async (href) => (await import(href)).default, rawUrl);
        actualHash = sha256(Buffer.from(raw));
      } else {
        const response = await fetch(url.href);
        assert.equal(response.ok, true, `served asset request failed: ${name}`);
        actualHash = sha256(Buffer.from(await response.arrayBuffer()));
      }
      assert.equal(actualHash, expectedHash, `owned Vite endpoint is not serving current workspace bytes: ${name}`);
      matched[name] = actualHash;
    }
  } finally {
    await context.close();
  }
  assert.deepEqual(matched, sourceHashes, "served endpoint provenance must cover the full source closure");
  return { baseUrl, port, matchedFiles: Object.keys(matched).length, sourceClosureSha256: closureDigest(matched), sourceFiles: matched };
}

function decodePng(bytes) {
  assert.equal(bytes.subarray(0, 8).toString("hex"), "89504e470d0a1a0a", "PNG signature");
  let offset = 8;
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  const idat = [];
  while (offset < bytes.length) {
    const length = bytes.readUInt32BE(offset);
    const type = bytes.subarray(offset + 4, offset + 8).toString("ascii");
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      assert.equal(data[12], 0, "interlaced PNGs are not accepted");
    } else if (type === "IDAT") idat.push(data);
    else if (type === "IEND") break;
    offset += 12 + length;
  }
  assert.ok(width > 0 && height > 0 && idat.length > 0, "PNG must have dimensions and IDAT data");
  assert.equal(bitDepth, 8, "final captures must use 8-bit PNG channels");
  const channels = ({ 0: 1, 2: 3, 4: 2, 6: 4 })[colorType];
  assert.ok(channels, `unsupported PNG color type ${colorType}`);
  const rowBytes = width * channels;
  const inflated = zlib.inflateSync(Buffer.concat(idat));
  assert.equal(inflated.length, height * (rowBytes + 1), "PNG inflated byte count");
  const pixels = Buffer.alloc(height * rowBytes);
  const paeth = (a, b, c) => {
    const p = a + b - c;
    const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
    return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
  };
  for (let y = 0; y < height; y += 1) {
    const input = y * (rowBytes + 1);
    const filter = inflated[input];
    const out = y * rowBytes;
    for (let x = 0; x < rowBytes; x += 1) {
      const raw = inflated[input + 1 + x];
      const left = x >= channels ? pixels[out + x - channels] : 0;
      const up = y > 0 ? pixels[out - rowBytes + x] : 0;
      const upperLeft = y > 0 && x >= channels ? pixels[out - rowBytes + x - channels] : 0;
      const value = filter === 0 ? raw
        : filter === 1 ? raw + left
          : filter === 2 ? raw + up
            : filter === 3 ? raw + Math.floor((left + up) / 2)
              : filter === 4 ? raw + paeth(left, up, upperLeft)
                : (() => { throw new Error(`unsupported PNG filter ${filter}`); })();
      pixels[out + x] = value & 0xff;
    }
  }
  return { width, height, channels, pixelSha256: sha256(pixels) };
}

function astNodes(source) {
  const output = [];
  const walk = (node) => {
    if (!node || typeof node !== "object") return;
    if (node.type) output.push(node);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === "object") walk(value);
    }
  };
  walk(parser.parse(source, { sourceType: "module", plugins: ["jsx"] }));
  return output;
}
function evaluate(source, node, environment, prefix = "") {
  return new Function(...Object.keys(environment), `${prefix}return (${source.slice(node.start, node.end)});`)(...Object.values(environment));
}
function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((onResolve, onReject) => { resolve = onResolve; reject = onReject; });
  return { promise, resolve, reject };
}
async function settleCallbacks() {
  await Promise.resolve();
  await new Promise((resolve) => setTimeout(resolve, 0));
}

async function runCorrectedRaceProofs() {
  const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
  const source = fs.readFileSync(appPath, "utf8");
  const nodes = astNodes(source);
  const { unwrapProfileEvent } = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/mapBackend.js")).href);

  const recoveryEffect = nodes.find((node) => node.type === "CallExpression" && node.callee.name === "useEffect" && source.slice(node.start, node.end).includes("game_recovery_status"))?.arguments[0];
  assert.ok(recoveryEffect, "recovery effect missing");
  let recoveryListener;
  const recoveryAccepted = [];
  const selectedProfileIdRef = { current: "A" };
  const recoveryCleanup = evaluate(source, recoveryEffect, {
    backendBridge: { available: true, listen: (_name, callback) => { recoveryListener = callback; return () => {}; }, invoke: () => new Promise(() => {}) },
    selectedProfileId: "A", selectedProfileIdRef, setGameRecoveryStatus: (value) => recoveryAccepted.push(value), unwrapProfileEvent,
  })();
  recoveryListener({ profileId: "B", payload: { state: "failed" } });
  recoveryListener({ profileId: "A", payload: { state: "ready" } });
  recoveryListener({ state: "raw" });
  assert.deepEqual(recoveryAccepted, [{ state: "ready" }, { state: "raw" }], "recovery profile ownership");
  selectedProfileIdRef.current = "B";
  recoveryListener({ profileId: "A", payload: { state: "late" } });
  selectedProfileIdRef.current = "A";
  recoveryCleanup();
  recoveryListener({ profileId: "A", payload: { state: "closed" } });
  assert.equal(recoveryAccepted.length, 2, "replaced/closed recovery listener must retire acknowledgements");

  const readStatusNode = nodes.find((node) => node.type === "VariableDeclarator" && node.id.name === "readStatusSnapshot")?.init?.arguments?.[0];
  const pollEffect = nodes.find((node) => node.type === "CallExpression" && node.callee.name === "useEffect" && source.slice(node.start, node.end).includes("const pollStatus = async () =>"))?.arguments[0];
  assert.ok(readStatusNode && pollEffect, "status poll callbacks missing");
  const proxyRequests = [];
  const runtimeWrites = [];
  const proxyWrites = [];
  const connectionWrites = [];
  const refs = {
    reconnectStatusGeneration: { current: 0 }, autoLaunchSaveRevisionRef: { current: 0 },
    autoLaunchNativeCommitEpochRef: { current: 0 }, autoLaunchConfigPollGenerationRef: { current: 0 }, autoLaunchCommittedRef: { current: false },
  };
  const backendBridge = { available: true, invoke: () => Promise.resolve({ autoLaunchGame: true }) };
  const mapApi = {
    readStatus: () => Promise.resolve({ ok: true }),
    readProxyStatus: () => { const request = deferred(); proxyRequests.push(request); return request.promise; },
    listenStatus: () => () => {}, listenScanStatus: () => () => {},
  };
  const readStatusSnapshot = evaluate(source, readStatusNode, {
    backendBridge, ...refs, mapApi, acknowledgeRuntimeStatus: (value) => runtimeWrites.push(value),
    setProxyStatus: (value) => proxyWrites.push(value), setConnectionError: (value) => connectionWrites.push(value),
  });
  const pollProfileRef = { current: "A" };
  let intervalCallback;
  let intervalClears = 0;
  const effect = evaluate(source, pollEffect, {
    backendBridge, selectedProfileId: "A", selectedProfileIdRef: pollProfileRef, mapApi,
    acknowledgeRuntimeStatus: (value) => runtimeWrites.push(value), acknowledgeMapScan() {}, readStatusSnapshot,
    window: { setInterval: (callback) => { intervalCallback = callback; return 91; }, clearInterval: (id) => { assert.equal(id, 91); intervalClears += 1; } },
  });
  const pollCleanup = effect();
  assert.equal(proxyRequests.length, 1, "initial periodic request");
  intervalCallback();
  assert.equal(proxyRequests.length, 1, "periodic overlap must be suppressed");
  pollProfileRef.current = "B";
  proxyRequests[0].resolve({ gameRunning: true });
  await settleCallbacks();
  assert.deepEqual(runtimeWrites, [], "obsolete periodic runtime write");
  assert.deepEqual(proxyWrites, [], "obsolete periodic proxy write");
  assert.deepEqual(connectionWrites, [], "obsolete periodic error write");
  pollCleanup();
  assert.equal(intervalClears, 1, "periodic timer cleanup");

  const callbacks = nodes.find((node) => node.type === "VariableDeclarator" && node.id.name === "profilePreviewCallbacks");
  const selectNode = callbacks?.init?.consequent?.properties?.find((node) => node.key.name === "onSelect")?.value;
  assert.ok(selectNode, "preview profile select callback missing");
  let selected = "A";
  let loading = false;
  const frames = [];
  const cache = new Set(["A"]);
  const previewProfileLoadGeneration = { current: 0 };
  const select = evaluate(source, selectNode, {
    shellProfiles: { selectedProfileId: "A" }, previewProfileCache: { current: cache }, previewProfileLoadGeneration,
    setPreviewProfileLoading: (value) => { loading = value; },
    setShellProfiles: (change) => { selected = change({ selectedProfileId: selected }).selectedProfileId; },
    window: { requestAnimationFrame: (callback) => { frames.push(callback); } },
  });
  const loadB = select("B");
  frames.shift()();
  const loadC = select("C");
  frames.shift()();
  await loadB;
  assert.equal(selected, "C");
  assert.equal(loading, true, "abandoned B must not clear C loading");
  assert.equal(frames.length, 1, "C bootstrap remains pending");
  frames.shift()();
  frames.shift()();
  await loadC;
  assert.equal(loading, false, "C owns its loading completion");

  const mapHarness = await createAppHarness(source, "milestone-e obsolete Map request");
  await mapHarness.mount();
  await mapHarness.resolveSummary(mapHarness.summaryRequests[0], 321);
  await mapHarness.clickRoute("map-data");
  const oldEntry = mapHarness.summaryRequests.at(-1);
  await mapHarness.setProfileId("profile-b");
  const replacement = mapHarness.summaryRequests.at(-1);
  await mapHarness.resolveSummary(oldEntry, 411, 9);
  assert.equal(mapHarness.getState("mapSummary"), null, "obsolete Map request must stay retired");
  await mapHarness.resolveSummary(replacement, 422, 10);
  assert.equal(mapHarness.getState("mapSummary").serverId, 422, "replacement Map request becomes authoritative");
  await mapHarness.unmount();

  return {
    recovery: { accepted: recoveryAccepted.length, crossProfileRejected: true, replacedAndClosedRetired: true },
    periodicStatus: { requestsDuringOverlap: proxyRequests.length, obsoleteWrites: runtimeWrites.length + proxyWrites.length + connectionWrites.length, timerClears: intervalClears },
    previewLoading: { selected, loading, cache: [...cache] },
    mapRequestRetirement: { oldRequest: oldEntry.id, replacementRequest: replacement.id, authoritativeServer: 422 },
  };
}

async function auditLocales(sourceClosure, sourceProvenFallbackKeys = []) {
  const codes = ["en", "id", "ja", "ko", "pt", "ru", "vi", "zh-CN", "zh-TW"];
  const catalogs = {};
  for (const code of codes) catalogs[code] = (await import(`${pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${code}.js`)).href}?final=${Date.now()}`)).default;
  const referenced = new Set();
  for (const name of sourceClosure.filter((name) => /\.(?:js|jsx|mjs)$/.test(name) && !name.includes("/locales/"))) {
    const source = fs.readFileSync(path.join(repo, name), "utf8");
    for (const pattern of [/\bt\(\s*["']([^"']+)["']/g, /\blabelKey\s*:\s*["']([^"']+)["']/g]) {
      let match;
      while ((match = pattern.exec(source))) referenced.add(match[1]);
    }
  }
  // The recovered locale catalogs are a canonical, same-key-set surface. Using the
  // complete English key set closes dynamic assignments such as t(`${category}.title`)
  // that a literal-only source scan cannot enumerate soundly.
  const requiredKeys = Object.keys(catalogs.en).sort();
  const referencedKeys = [...referenced].sort();
  const summary = {};
  for (const code of codes) {
    const missing = requiredKeys.filter((key) => !Object.hasOwn(catalogs[code], key));
    const extra = Object.keys(catalogs[code]).filter((key) => !Object.hasOwn(catalogs.en, key)).sort();
    assert.deepEqual(missing, [], `${code} missing assigned locale keys`);
    assert.deepEqual(extra, [], `${code} has locale keys outside recovered English catalog`);
    summary[code] = { catalogKeys: Object.keys(catalogs[code]).length, assignedKeysPresent: requiredKeys.length, missing, extra };
  }
  const literalMissing = referencedKeys.filter((key) => !Object.hasOwn(catalogs.en, key)).sort();
  assert.deepEqual(literalMissing, [...sourceProvenFallbackKeys].sort(), "literal assigned locale keys outside the recovered catalog changed from the source-proven fallback set");
  return { codes, requiredKeys, referencedKeys, sourceProvenFallbackKeys: [...sourceProvenFallbackKeys].sort(), literalMissing, dynamicAssignedKeys: ["hotkeys.title", "hotkeys.description", "miniGames.title", "miniGames.description"], summary };
}

const assertions = [];
const screenshots = [];
const consoleIssues = [];
const styleSnapshots = [];
function check(name, actual, expected) {
  assert.deepEqual(actual, expected, name);
  assertions.push({ name, actual, expected });
}

async function openPage(browser, { id = "case", language = "en", theme = "light", width = 1280, height = 900, state = "", route = "overview" } = {}) {
  const context = await browser.newContext({ viewport: { width, height }, locale: language, timezoneId: "Asia/Singapore", colorScheme: theme, deviceScaleFactor: 1, reducedMotion: "reduce" });
  await context.addInitScript((lang) => {
    localStorage.clear();
    localStorage.setItem("lwbridge.language", lang);
    const listeners = new Map();
    const intervals = new Set();
    const timeouts = new Set();
    const add = window.addEventListener.bind(window);
    const remove = window.removeEventListener.bind(window);
    const setIntervalNative = window.setInterval.bind(window);
    const clearIntervalNative = window.clearInterval.bind(window);
    const setTimeoutNative = window.setTimeout.bind(window);
    const clearTimeoutNative = window.clearTimeout.bind(window);
    window.addEventListener = (type, listener, options) => {
      const owners = listeners.get(type) || new Set();
      owners.add(listener); listeners.set(type, owners);
      return add(type, listener, options);
    };
    window.removeEventListener = (type, listener, options) => {
      listeners.get(type)?.delete(listener);
      return remove(type, listener, options);
    };
    window.setInterval = (callback, delay, ...rest) => {
      const id = setIntervalNative(callback, delay, ...rest); intervals.add(id); return id;
    };
    window.clearInterval = (id) => { intervals.delete(id); return clearIntervalNative(id); };
    window.setTimeout = (callback, delay, ...rest) => {
      let id;
      id = setTimeoutNative((...args) => { timeouts.delete(id); callback(...args); }, delay, ...rest);
      timeouts.add(id); return id;
    };
    window.clearTimeout = (id) => { timeouts.delete(id); return clearTimeoutNative(id); };
    window.__finalOwnerCounts = () => ({
      keydown: listeners.get("keydown")?.size || 0,
      pointerdown: listeners.get("pointerdown")?.size || 0,
      intervals: intervals.size,
      timeouts: timeouts.size,
    });
  }, language);
  const page = await context.newPage();
  page.on("console", (message) => { if (["warning", "error"].includes(message.type())) consoleIssues.push({ id, language, theme, state, type: message.type(), text: message.text() }); });
  page.on("pageerror", (error) => consoleIssues.push({ id, language, theme, state, type: "pageerror", text: String(error?.stack || error) }));
  const url = new URL(baseUrl);
  url.searchParams.set("previewPage", route);
  url.searchParams.set("previewLanguage", language);
  url.searchParams.set("previewTheme", theme);
  if (state) url.searchParams.set("previewState", state);
  await page.goto(url.href, { waitUntil: "domcontentloaded" });
  await page.locator("main.app-shell").waitFor();
  await page.waitForFunction((lang) => document.documentElement.lang === lang, language);
  await settlePage(page);
  return { context, page };
}

async function settlePage(page) {
  await page.evaluate(() => document.fonts.ready);
  await page.waitForFunction(() => document.fonts.status === "loaded");
  await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
  await page.waitForFunction(() => !document.querySelector("section.main-view > .panel > .muted"));
  await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

async function inspectSettledPage(page, id) {
  const snapshot = await page.evaluate(() => {
    const visibleNode = (selector) => [...document.querySelectorAll(selector)].find((candidate) => {
      const rect = candidate.getBoundingClientRect();
      const style = getComputedStyle(candidate);
      return rect.width > 0 && rect.height > 0 && style.display !== "none" && style.visibility !== "hidden";
    }) || null;
    const metric = (selector) => {
      const node = visibleNode(selector);
      if (!node) return null;
      const rect = node.getBoundingClientRect();
      const style = getComputedStyle(node);
      return {
        selector, rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
        display: style.display, color: style.color, backgroundColor: style.backgroundColor,
        fontFamily: style.fontFamily, fontSize: style.fontSize, lineHeight: style.lineHeight,
      };
    };
    return {
      language: document.documentElement.lang,
      theme: document.documentElement.dataset.theme,
      visibleText: document.body.innerText.trim(),
      svgCount: document.querySelectorAll("svg").length,
      imageCount: document.querySelectorAll("img").length,
      loadingPlaceholders: document.querySelectorAll(".profile-switch-loading, section.main-view > .panel > .muted").length,
      shell: metric("main.app-shell"), topBar: metric(".top-bar"), sideNav: metric(".side-nav"), mainView: metric(".main-view"),
      activePanel: metric(".main-view .panel, .main-view .map-panel, .main-view .squad-panel, .main-view .city-layout-panel"),
      heading: metric(".main-view h1, .main-view h2, .main-view h3, .main-view strong"),
      button: metric(".main-view button"), formControl: metric(".main-view input, .main-view select, .main-view textarea"),
      icon: metric(".main-view svg, .main-view img"), dialog: metric("dialog[open], [role='dialog']"), popover: metric(".server-jump-popover"),
    };
  });
  assert.equal(snapshot.loadingPlaceholders, 0, `${id} capture must be settled`);
  assert.ok(snapshot.visibleText.length > 0, `${id} capture must contain rendered text`);
  const visibleText = snapshot.visibleText;
  delete snapshot.visibleText;
  styleSnapshots.push({ id, textLength: visibleText.length, textSha256: sha256(Buffer.from(visibleText)), textSample: visibleText.slice(0, 240), ...snapshot });
}

async function capture(page, id) {
  await settlePage(page);
  await inspectSettledPage(page, id);
  const bytes = await page.screenshot({ fullPage: true });
  const decoded = decodePng(bytes);
  const file = `${id}.png`;
  if (record) {
    fs.mkdirSync(captureDir, { recursive: true });
    fs.writeFileSync(path.join(captureDir, file), bytes);
  }
  screenshots.push({ file, bytes: bytes.length, sha256: sha256(bytes), ...decoded });
}

const routes = [
  ["overview", ".quick-actions-panel"], ["automation", ".automation-categories"], ["map-data", ".map-panel"], ["march", ".squad-panel"],
  ["city-layout", ".city-layout-empty, .city-layout-panel"], ["hotkeys", ".hotkey-panel[data-hotkey-category='hotkeys']"],
  ["mini-games", ".hotkey-panel[data-hotkey-category='miniGames']"], ["settings", ".settings-panel"],
];
async function selectRoute(page, index, selector) {
  await page.locator(".side-nav > button").nth(index).click();
  await page.locator(selector).filter({ visible: true }).first().waitFor();
  await settlePage(page);
}

const sourceStart = closureHashes();
const sourceClosureSha256Start = closureDigest(sourceStart);
const sourceClosure = Object.keys(sourceStart);
const r1HostManifestPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/host-reconciliation-manifest.json");
const r1HostResultPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/host-reconciliation-results.json");
const cConditionalPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/conditional-composition-results.json");
const cOwnershipPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership-results.json");
const r1OwnershipPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/profile-ownership-results.json");
const r1EquipmentPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/equipment-regression-results.json");
const r2EquipmentContractPath = path.join(here, "equipment-save-contract-results.json");
const r2EquipmentRegressionPath = path.join(here, "equipment-regression-results.json");
const campaignLocaleInventoryPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/locale-inventory.json");
for (const file of [r1HostManifestPath, r1HostResultPath, cConditionalPath, cOwnershipPath, r1OwnershipPath, r1EquipmentPath, r2EquipmentContractPath, r2EquipmentRegressionPath, campaignLocaleInventoryPath]) assert.ok(fs.existsSync(file), `required closeout dependency missing: ${relative(file)}`);
const r1HostManifest = JSON.parse(fs.readFileSync(r1HostManifestPath, "utf8"));
const r1HostResult = JSON.parse(fs.readFileSync(r1HostResultPath, "utf8"));
const cConditional = JSON.parse(fs.readFileSync(cConditionalPath, "utf8"));
const cOwnership = JSON.parse(fs.readFileSync(cOwnershipPath, "utf8"));
const r1Ownership = JSON.parse(fs.readFileSync(r1OwnershipPath, "utf8"));
const r1Equipment = JSON.parse(fs.readFileSync(r1EquipmentPath, "utf8"));
const r2EquipmentContract = JSON.parse(fs.readFileSync(r2EquipmentContractPath, "utf8"));
const r2EquipmentRegression = JSON.parse(fs.readFileSync(r2EquipmentRegressionPath, "utf8"));
const campaignLocaleInventory = JSON.parse(fs.readFileSync(campaignLocaleInventoryPath, "utf8"));
assert.equal(r1HostManifest.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_D_MANIFEST", "R1 host manifest marker");
assert.equal(r1HostResult.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_D_HOST_RECONCILIATION_OK", "R1 host result marker");
assert.deepEqual(hashFile(r1HostResultPath), { bytes: r1HostManifest.result.bytes, sha256: r1HostManifest.result.sha256 }, "frozen R1 host result hash drifted before R2");
const intentionalSourcePath = "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx";
const r1SourceFiles = r1HostManifest.sourceClosure.hashes;
assert.deepEqual(Object.keys(sourceStart), Object.keys(r1SourceFiles), "R2 source closure membership differs from frozen R1 host closure");
const intentionalSourceChanges = Object.keys(sourceStart).filter((name) => sourceStart[name] !== r1SourceFiles[name]);
assert.deepEqual(intentionalSourceChanges, [intentionalSourcePath], "R2 must change only SquadsPage within the served App source closure");
assert.equal(r1SourceFiles[intentionalSourcePath], "920892FCC3069D4F98B0662E4502910DDF5901EE707C8A663B751BBB3A2E4623", "frozen R1 SquadsPage source anchor");
assert.equal(sourceStart[intentionalSourcePath], "5D0AE58CBE199441A046E326D699808CA31AEB96D6CC7DAA0E7EC0A8C3B231D7", "corrected R2 SquadsPage source anchor");
assert.equal(cConditional.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONDITIONAL_COMPOSITION_OK", "Milestone C conditional marker");
assert.equal(cOwnership.marker, "LWB317_FINAL_CLOSEOUT_MILESTONE_C_CONFIG_STORE_OWNERSHIP_OK", "Milestone C config-store marker");
assert.equal(r1HostManifest.inheritedProofFiles[relative(cConditionalPath)], hashFile(cConditionalPath).sha256, "R1 host no longer binds final Milestone C conditional result");
assert.equal(r1HostManifest.inheritedProofFiles[relative(cOwnershipPath)], hashFile(cOwnershipPath).sha256, "R1 host no longer binds final Milestone C config-store result");
assert.equal(r1HostManifest.inheritedProofFiles[relative(r1OwnershipPath)], hashFile(r1OwnershipPath).sha256, "R1 host no longer binds focused profile ownership result");
assert.equal(r1HostManifest.inheritedProofFiles[relative(r1EquipmentPath)], hashFile(r1EquipmentPath).sha256, "R1 host no longer binds historical Equipment regression result");
assert.equal(r1Ownership.status, "PASS", "R1 focused profile ownership result");
assert.equal(r1Ownership.immutableFailureBaseline.counterexample, true, "R1 focused proof must retain the independent lead counterexample");
assert.equal(r1Equipment.marker, "LWB317_FINAL_CLOSEOUT_R1_EQUIPMENT_REGRESSION_OK", "R1 Equipment regression result");
assert.equal(r1Equipment.issues.length, 0, "R1 Equipment regression browser issues");
assert.equal(r2EquipmentContract.marker, "LWB317_FINAL_CLOSEOUT_R2_EQUIPMENT_SAVE_CONTRACT_OK", "R2 Equipment save-contract result");
assert.equal(r2EquipmentContract.assertions.length, 38, "R2 Equipment save-contract assertion count");
assert.equal(r2EquipmentContract.issues.length, 0, "R2 Equipment save-contract browser issues");
assert.equal(r2EquipmentContract.cases.noSecondSave.afterFirstAck.dirty, true, "R2 distinguishing first acknowledgement must leave later move dirty");
assert.equal(r2EquipmentContract.cases.noSecondSave.afterExplicitSave.dirty, false, "R2 later explicit Save must confirm later move");
assert.equal(r2EquipmentContract.cases.queuedSave.settled.dirty, false, "R2 explicitly queued second Save must drain");
assert.equal(r2EquipmentRegression.marker, "LWB317_FINAL_CLOSEOUT_R2_EQUIPMENT_REGRESSION_OK", "R2 Equipment regression result");
assert.equal(r2EquipmentRegression.assertions.length, 17, "R2 Equipment regression assertion count");
assert.equal(r2EquipmentRegression.issues.length, 0, "R2 Equipment regression browser issues");
assert.equal(campaignLocaleInventory.result, "LWB317_VISUAL_FINAL_UNIT_C_LOCALES_OK", "accepted Unit C locale inventory marker");
assert.deepEqual(campaignLocaleInventory.fallbackKeys, ["common.loading"], "source-proven locale fallback set changed");
assert.deepEqual(campaignLocaleInventory.partialMissing, [], "accepted Unit C locale inventory has partial locale holes");
assert.deepEqual(campaignLocaleInventory.missing, ["en", "id", "ja", "ko", "pt", "ru", "vi", "zh-CN", "zh-TW"].map((locale) => ({ locale, key: "common.loading" })), "accepted Unit C all-nine fallback inventory changed");
const acceptanceChain = {
  r1HostBaseline: { marker: r1HostResult.marker, manifestMarker: r1HostManifest.marker, sourceClosureSha256: r1HostManifest.sourceClosure.digest, resultSha256: r1HostManifest.result.sha256 },
  intentionalSourceDelta: { path: intentionalSourcePath, beforeSha256: r1SourceFiles[intentionalSourcePath], afterSha256: sourceStart[intentionalSourcePath], changedFiles: intentionalSourceChanges },
  milestoneC: { conditionalMarker: cConditional.marker, conditionalSha256: hashFile(cConditionalPath).sha256, configStoreMarker: cOwnership.marker, configStoreSha256: hashFile(cOwnershipPath).sha256 },
  r1Ownership: { status: r1Ownership.status, leadCounterexample: r1Ownership.immutableFailureBaseline.counterexample, resultSha256: hashFile(r1OwnershipPath).sha256, historicalEquipmentSha256: hashFile(r1EquipmentPath).sha256 },
  r2Equipment: { contractMarker: r2EquipmentContract.marker, contractSha256: hashFile(r2EquipmentContractPath).sha256, contractAssertions: r2EquipmentContract.assertions.length, regressionMarker: r2EquipmentRegression.marker, regressionSha256: hashFile(r2EquipmentRegressionPath).sha256, regressionAssertions: r2EquipmentRegression.assertions.length },
  inheritedLocaleFallback: { marker: campaignLocaleInventory.result, sha256: hashFile(campaignLocaleInventoryPath).sha256, keys: campaignLocaleInventory.fallbackKeys },
};
const localeAudit = await auditLocales(sourceClosure, campaignLocaleInventory.fallbackKeys);
const raceProofs = await runCorrectedRaceProofs();
const uiRoot = path.join(repo, "src/LWBridge.UI-0.3.17");
const playwrightRoot = path.dirname(requirePlaywright.resolve("playwright/package.json"));
const playwrightCoreRoot = path.dirname(requirePlaywright.resolve("playwright-core/package.json"));
const babelParserRoot = path.dirname(requireUi.resolve("@babel/parser/package.json"));
const esbuildRoot = path.dirname(requireUi.resolve("esbuild/package.json"));
const esbuildBinary = path.join(path.dirname(requireUi.resolve("@esbuild/win32-x64/package.json")), "esbuild.exe");
const viteRoot = path.dirname(requireUi.resolve("vite/package.json"));
const pluginReactRoot = path.join(uiRoot, "node_modules/@vitejs/plugin-react");
const reactRoot = path.dirname(requireUi.resolve("react/package.json"));
const reactDomRoot = path.dirname(requireUi.resolve("react-dom/package.json"));
const toolFiles = [
  chromeExe, playwrightPackage, path.join(uiRoot, "package.json"), path.join(uiRoot, "package-lock.json"), path.join(uiRoot, "vite.config.js"),
  path.join(uiRoot, "node_modules/react/package.json"), path.join(uiRoot, "node_modules/react-dom/package.json"), path.join(uiRoot, "node_modules/vite/package.json"), path.join(uiRoot, "node_modules/@vitejs/plugin-react/package.json"),
  path.join(uiRoot, "node_modules/@babel/parser/package.json"), path.join(uiRoot, "node_modules/esbuild/package.json"),
  requirePlaywright.resolve("playwright"), requirePlaywright.resolve("playwright-core"), requireUi.resolve("@babel/parser"), requireUi.resolve("esbuild"), requireUi.resolve("vite"), requireUi.resolve("react"), requireUi.resolve("react-dom"), esbuildBinary,
].map((file) => ({ path: file.replaceAll("\\", "/"), ...hashFile(file) }));
const toolTrees = [playwrightRoot, playwrightCoreRoot, babelParserRoot, esbuildRoot, viteRoot, pluginReactRoot, reactRoot, reactDomRoot].map(treeDigest);

const browser = await chromium.launch({ executablePath: chromeExe, headless: true });
let browserVersion = "";
let endpointProvenance = null;
try {
  browserVersion = await browser.version();
  endpointProvenance = await verifyServedClosure(browser, sourceStart);
  check("owned Vite endpoint serves full current source closure", endpointProvenance.matchedFiles, sourceClosure.length);
  const integratedEquipment = await runEquipmentSaveContract({ browser, baseUrl, record: false });
  check("Equipment R2 integrated contract executes all focused assertions", integratedEquipment.assertions.length, 38);
  check("Equipment pending acknowledgement leaves later unrequested move dirty", integratedEquipment.cases.noSecondSave.afterFirstAck.dirty, true);
  check("Equipment later explicit Save confirms retained move", integratedEquipment.cases.noSecondSave.afterExplicitSave.dirty, false);
  check("Equipment explicitly queued second Save confirms later move", integratedEquipment.cases.queuedSave.settled.dirty, false);
  check("Equipment rejection Retry confirms retained owning draft", integratedEquipment.cases.retry.retried.dirty, false);
  check("Equipment rejection Discard restores owning confirmed draft", integratedEquipment.cases.discard.discarded.dirty, false);
  check("Equipment R2 integrated contract has no browser issues", integratedEquipment.issues.length, 0);
  const modes = [
    { id: "en-light-desktop", language: "en", theme: "light", width: 1280, height: 900 },
    { id: "ja-dark-desktop", language: "ja", theme: "dark", width: 1280, height: 900 },
    { id: "en-light-narrow", language: "en", theme: "light", width: 375, height: 1000 },
    { id: "ja-dark-narrow", language: "ja", theme: "dark", width: 375, height: 1000 },
    { id: "en-dark-desktop", language: "en", theme: "dark", width: 1280, height: 900 },
    { id: "ja-light-narrow", language: "ja", theme: "light", width: 375, height: 1000 },
  ];
  for (const mode of modes) {
    const { context, page } = await openPage(browser, mode);
    try {
      check(`${mode.id} exact navigation count`, await page.locator(".side-nav > button").count(), 8);
      const identities = new Map();
      for (let index = 0; index < routes.length; index += 1) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        const token = `final-${mode.id}-${route}`;
        await page.locator(selector).filter({ visible: true }).first().evaluate((node, value) => node.setAttribute("data-final-retained-id", value), token);
        identities.set(route, token);
        check(`${mode.id}/${route} selected nav`, await page.locator(".side-nav > button").nth(index).getAttribute("aria-current"), "page");
        check(`${mode.id}/${route} settled language`, await page.locator("html").getAttribute("lang"), mode.language);
        check(`${mode.id}/${route} settled theme`, await page.locator("html").getAttribute("data-theme"), mode.theme);
      }
      for (let index = routes.length - 1; index >= 0; index -= 1) {
        const [route, selector] = routes[index];
        await selectRoute(page, index, selector);
        check(`${mode.id}/${route} return keeps DOM identity`, await page.locator(`[data-final-retained-id='${identities.get(route)}']`).count(), 1);
      }
      if (mode.width <= 760) check(`${mode.id} no document horizontal overflow`, await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth), false);
      await capture(page, `${mode.id}-eight-route-return`);
    } finally { await context.close(); }
  }

  // Full parent/subtab ownership plus profile replacement and child reset.
  {
    const { context, page } = await openPage(browser, { id: "profile-subtabs", state: "shell-profiles", route: "overview" });
    try {
      await selectRoute(page, 1, ".automation-categories");
      const automationTabs = page.locator(".automation-categories > button");
      check("Automation exposes seven categories", await automationTabs.count(), 7);
      for (let index = 0; index < 7; index += 1) { await automationTabs.nth(index).click(); check(`Automation category ${index} selected`, await automationTabs.nth(index).getAttribute("aria-selected"), "true"); }
      await automationTabs.nth(0).click();
      const automationLabel = await automationTabs.nth(0).innerText();
      const trainingCardA = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
      const trainingTriggerA = trainingCardA.locator(".automation-config-trigger");
      if ((await trainingTriggerA.getAttribute("aria-expanded")) !== "true") await trainingTriggerA.click();
      const trainingInputA = trainingCardA.locator("input[type='number']").first();
      await trainingInputA.evaluate((node) => {
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set;
        setter.call(node, "1000001");
        node.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: "1000001" }));
        node.dispatchEvent(new Event("change", { bubbles: true }));
      });
      await page.waitForFunction(() => document.querySelector(".automation-card:has(h3):not([hidden]) .automation-config-body[data-draft-dirty='true']") !== null);
      check("Automation profile A child draft is dirty before switch", await trainingCardA.locator(".automation-config-body").getAttribute("data-draft-dirty"), "true");
      check("Automation profile A child draft records invalid local edit", await trainingInputA.inputValue(), "1000001");

      await selectRoute(page, 2, ".map-panel");
      const mapTabs = page.locator(".map-tabs > button");
      check("Map exposes eight data tabs plus Scheduled", await mapTabs.count(), 9);
      for (let index = 0; index < 9; index += 1) { await mapTabs.nth(index).click(); check(`Map subtab ${index} selected`, await mapTabs.nth(index).getAttribute("aria-selected"), "true"); }
      await mapTabs.nth(3).click();
      const search = page.locator(".map-searchbar input").first();
      await search.fill("final-profile-keyword");
      await page.locator(".map-searchbar select").filter({ visible: true }).first().selectOption("ssr");
      await page.locator(".map-plunderable-filter input[type='checkbox']").check();
      await page.locator(".map-scan-tabs > button").nth(1).click();
      await page.locator(".map-auto-scan-grid input[type='number']").fill("75");

      await selectRoute(page, 3, ".squad-panel");
      const squadTabs = page.locator(".squad-tabs > button");
      check("Squads exposes AFK and Equipment", await squadTabs.count(), 2);
      await squadTabs.nth(0).click();
      const afkPotionCardA = page.locator(".monster-afk-compact-card").nth(1);
      await afkPotionCardA.locator('button[aria-label="Settings"]').click();
      const afkPotionInputA = page.locator('.monster-afk-toolbar-settings input[type="number"]');
      await afkPotionInputA.evaluate((node) => {
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set;
        setter.call(node, "10000");
        node.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: "10000" }));
        node.dispatchEvent(new Event("change", { bubbles: true }));
      });
      await page.waitForFunction(() => document.querySelector(".monster-afk-layout")?.getAttribute("data-draft-dirty") === "true");
      check("AFK profile A Potion draft is dirty before switch", await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"), "true");
      check("AFK profile A Potion draft records local edit", await afkPotionInputA.inputValue(), "10000");
      await squadTabs.nth(1).click();
      check("Equipment selected", await squadTabs.nth(1).getAttribute("aria-selected"), "true");

      await page.locator(".profile-collapse").click();
      const profiles = page.locator(".profile-item");
      await selectRoute(page, 2, ".map-panel");
      await page.evaluate(() => {
        window.__finalSawProfileLoading = false;
        window.__finalProfileObserver = new MutationObserver(() => { if (document.querySelector(".profile-switch-loading")) window.__finalSawProfileLoading = true; });
        window.__finalProfileObserver.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(1).click();
      await page.waitForFunction(() => window.__finalSawProfileLoading === true);
      await page.waitForFunction(() => !document.querySelector(".profile-switch-loading"));
      await page.evaluate(() => window.__finalProfileObserver?.disconnect());
      check("uncached profile crosses loading boundary", await page.evaluate(() => window.__finalSawProfileLoading), true);
      check("App-owned Map tab persists across profile replacement", await mapTabs.nth(3).getAttribute("aria-selected"), "true");
      check("Map keyword resets at profile boundary", await search.inputValue(), "");
      check("Map scan mode resets at profile boundary", await page.locator(".map-scan-tabs > button").nth(0).getAttribute("aria-selected"), "true");
      check("Map quality resets at profile boundary", await page.locator(".map-searchbar select").filter({ visible: true }).first().inputValue(), "");
      check("Map plunderable resets at profile boundary", await page.locator(".map-plunderable-filter input[type='checkbox']").isChecked(), false);
      await page.locator(".map-scan-tabs > button").nth(1).click();
      check("Map Auto interval resets at profile boundary", await page.locator(".map-auto-scan-grid input[type='number']").inputValue(), "60");
      await selectRoute(page, 1, ".automation-categories");
      check("Automation parent category persists across profile replacement", await page.locator(".automation-categories > button[aria-selected='true']").innerText(), automationLabel);
      const trainingCardB = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
      const trainingTriggerB = trainingCardB.locator(".automation-config-trigger");
      if ((await trainingTriggerB.getAttribute("aria-expanded")) !== "true") await trainingTriggerB.click();
      check("Automation child draft resets at profile boundary", await trainingCardB.locator("input[type='number']").first().inputValue(), "");
      check("Automation profile B starts with clean child draft", await trainingCardB.locator(".automation-config-body").getAttribute("data-draft-dirty"), "false");
      await selectRoute(page, 3, ".squad-panel");
      check("Squads parent subtab persists across profile replacement", await page.locator(".squad-tabs > button").nth(1).getAttribute("aria-selected"), "true");
      await page.locator(".squad-tabs > button").nth(0).click();
      const afkPotionCardB = page.locator(".monster-afk-compact-card").nth(1);
      await afkPotionCardB.locator('button[aria-label="Settings"]').click();
      const afkPotionInputB = page.locator('.monster-afk-toolbar-settings input[type="number"]');
      check("AFK profile B starts with clean Potion draft", await afkPotionInputB.inputValue(), "50");
      check("AFK profile B starts with clean AFK store", await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"), "false");
      await page.evaluate(() => {
        window.__finalSawCachedLoading = false;
        window.__finalCachedObserver = new MutationObserver(() => { if (document.querySelector(".profile-switch-loading")) window.__finalSawCachedLoading = true; });
        window.__finalCachedObserver.observe(document.body, { childList: true, subtree: true });
      });
      await profiles.nth(0).click();
      await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      check("cached first profile return bypasses loading", await page.evaluate(() => window.__finalSawCachedLoading), false);
      if (!(await page.locator('.monster-afk-toolbar-settings input[type="number"]').count())) {
        await page.locator(".monster-afk-compact-card").nth(1).locator('button[aria-label="Settings"]').click();
      }
      check("AFK profile A Potion draft returns only with profile A", await page.locator('.monster-afk-toolbar-settings input[type="number"]').inputValue(), "10000");
      check("AFK profile A store remains dirty on return", await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"), "true");
      await selectRoute(page, 1, ".automation-categories");
      const returnedTrainingA = page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first();
      const returnedTriggerA = returnedTrainingA.locator(".automation-config-trigger");
      if ((await returnedTriggerA.getAttribute("aria-expanded")) !== "true") await returnedTriggerA.click();
      check("Automation profile A child draft returns only with profile A", await returnedTrainingA.locator("input[type='number']").first().inputValue(), "1000001");
      await profiles.nth(1).click();
      await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      check("cached second profile return bypasses loading", await page.evaluate(() => window.__finalSawCachedLoading), false);
      check("Automation profile B remains isolated after cached return", await page.locator(".automation-card:visible").filter({ hasText: "Auto Training" }).first().locator("input[type='number']").first().inputValue(), "");
      await page.evaluate(() => window.__finalCachedObserver?.disconnect());
      await selectRoute(page, 3, ".squad-panel");
      check("cached profile return keeps current Squads route", await page.locator(".side-nav > button").nth(3).getAttribute("aria-current"), "page");
      const returnedPotionCardB = page.locator(".monster-afk-compact-card").nth(1);
      if (!(await page.locator('.monster-afk-toolbar-settings input[type="number"]').count())) await returnedPotionCardB.locator('button[aria-label="Settings"]').click();
      check("AFK profile B remains clean after cached return", await page.locator('.monster-afk-toolbar-settings input[type="number"]').inputValue(), "50");
      check("AFK profile B remains isolated after cached return", await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"), "false");
      await capture(page, "en-light-profile-subtabs-reset");
    } finally { await context.close(); }
  }

  // Populated Map pagination survives an ordinary Activity hide/return.
  {
    const { context, page } = await openPage(browser, { id: "map-pagination", state: "map-city", route: "map-data" });
    try {
      await page.locator(".map-pagination").waitFor();
      check("Map populated result count", await page.locator(".map-result-count").innerText(), "52 items");
      check("Map populated first page rows", await page.locator(".map-table tbody tr").count(), 50);
      await page.locator(".map-pagination button").last().click();
      await page.waitForFunction(() => document.querySelectorAll(".map-table tbody tr").length === 2);
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 2, ".map-panel");
      check("Map page 2 survives route hiding", await page.locator(".map-pagination span").innerText(), "Page 2 of 2");
      check("Map page 2 row remainder", await page.locator(".map-table tbody tr").count(), 2);
      await capture(page, "en-light-map-page2-return");
    } finally { await context.close(); }
  }

  // Automation dirty draft/category Activity retention and representative C conditionals.
  {
    const { context, page } = await openPage(browser, { id: "automation-retention", state: "automation-squads-34", route: "automation" });
    try {
      const tabs = page.locator(".automation-categories > button");
      await tabs.nth(4).click();
      const treasure = page.locator(".automation-card:visible").filter({ hasText: "Treasure" }).first();
      const trigger = treasure.locator(".automation-config-trigger");
      if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
      const toggles = treasure.locator('.toggle-row[role="switch"]');
      check("Treasure exposes three conditional toggles", await toggles.count(), 3);
      check("Treasure discovered squad priority", await treasure.locator(".automation-squad-priority-item").allTextContents(), ["Squad 3", "Squad 4"]);
      await toggles.nth(0).click();
      const replyInput = treasure.locator(".automation-subsettings input[type='number']").first();
      await replyInput.fill("17");
      check("Automation conditional edit is dirty before hide", await treasure.locator(".automation-config-body").getAttribute("data-draft-dirty"), "true");
      await tabs.nth(5).click();
      const trade = page.locator(".automation-card:visible").first();
      const tradeTrigger = trade.locator(".automation-config-trigger");
      if ((await tradeTrigger.getAttribute("aria-expanded")) !== "true") await tradeTrigger.click();
      await selectRoute(page, 0, ".quick-actions-panel");
      await selectRoute(page, 1, ".automation-categories");
      await tabs.nth(4).click();
      check("Automation conditional draft survives category and route hiding", await replyInput.inputValue(), "17");
      await capture(page, "en-light-automation-conditional-return");
    } finally { await context.close(); }
  }

  // AFK/Equipment retention plus recovered native-dialog lifetime. No provider action is invoked.
  {
    const { context, page } = await openPage(browser, { id: "squads-retention", state: "squads-profile-members-positive", route: "march" });
    try {
      const draft = page.locator(".monster-afk-editor:visible input").first();
      await draft.fill("Milestone E retained AFK draft");
      check("AFK editor is dirty before hide", await page.locator(".monster-afk-layout").getAttribute("data-draft-dirty"), "true");
      const choose = page.locator(".rally-join-leaders > button").first();
      await choose.focus();
      await choose.click();
      const dialog = page.locator("dialog.app-dialog.garrison-modal-backdrop");
      check("AFK Join opens native dialog", await dialog.getAttribute("open"), "");
      check("AFK Join focus remains inside dialog", await dialog.evaluate((node) => node === document.activeElement || node.contains(document.activeElement)), true);
      await capture(page, "en-light-afk-join-dialog");
      await page.keyboard.press("Escape");
      await dialog.waitFor({ state: "detached" });
      check("AFK Join cleanup restores trigger focus", await page.evaluate(() => document.activeElement === document.querySelector(".rally-join-leaders > button")), true);
      await page.locator(".squad-tabs > button").nth(1).click();
      check("Equipment mounts while AFK hides", await page.locator(".equipment-preset-layout:visible").count(), 1);
      await page.locator(".squad-tabs > button").nth(0).click();
      check("AFK draft survives Equipment return", await draft.inputValue(), "Milestone E retained AFK draft");
      await capture(page, "en-light-squads-dialog-retention");
    } finally { await context.close(); }
  }

  // Hidden Activity effect owners: City keyboard listener and Mini Games running timer.
  {
    const { context, page } = await openPage(browser, { id: "city-effect", state: "city-layout-populated", route: "city-layout" });
    try {
      const visible = await page.evaluate(() => window.__finalOwnerCounts());
      await selectRoute(page, 0, ".quick-actions-panel");
      const hidden = await page.evaluate(() => window.__finalOwnerCounts());
      check("City hide removes exactly one keydown owner", hidden.keydown, visible.keydown - 1);
      await selectRoute(page, 4, ".city-layout-panel");
      const returned = await page.evaluate(() => window.__finalOwnerCounts());
      check("City return restores exactly one keydown owner", returned.keydown, visible.keydown);
    } finally { await context.close(); }
  }
  {
    const { context, page } = await openPage(browser, { id: "mini-timer", state: "mini-games-active", route: "mini-games" });
    try {
      const visible = await page.evaluate(() => window.__finalOwnerCounts());
      check("Mini Games running state owns a timer", visible.intervals > 0, true);
      await selectRoute(page, 0, ".quick-actions-panel");
      const hidden = await page.evaluate(() => window.__finalOwnerCounts());
      check("Mini Games hide suspends at least one interval owner", hidden.intervals < visible.intervals, true);
      await selectRoute(page, 6, ".hotkey-panel[data-hotkey-category='miniGames']");
      const returned = await page.evaluate(() => window.__finalOwnerCounts());
      check("Mini Games return restores interval ownership", returned.intervals, visible.intervals);
    } finally { await context.close(); }
  }

  // Shared popover/dialog boundaries and live locale/theme transition on a retained page.
  {
    const { context, page } = await openPage(browser, { id: "shared-host", state: "shell-profiles" });
    try {
      await page.locator(".server-jump > button").click();
      check("Cross-server popover mounts once", await page.locator(".server-jump-popover").count(), 1);
      check("Cross-server provider remains fenced", await page.locator(".server-jump-error").count(), 1);
      await capture(page, "en-light-cross-server-popover");
      await page.locator(".brand-lockup").click({ position: { x: 4, y: 4 } });
      await page.locator(".server-jump-popover").waitFor({ state: "detached" });
      await page.locator(".profile-collapse").click();
      const row = page.locator(".profile-row").first();
      await row.hover();
      await row.locator(".profile-note-edit").click();
      const note = page.locator("dialog.profile-dialog-backdrop");
      check("Profile note dialog stays inside shell host", await page.locator("main.app-shell dialog.profile-dialog-backdrop").count(), 1);
      check("Profile note dialog stays outside routed page", await page.locator(".main-view dialog.profile-dialog-backdrop").count(), 0);
      await capture(page, "en-light-profile-note-dialog");
      await page.keyboard.press("Escape");
      await note.waitFor({ state: "detached" });
      await selectRoute(page, 2, ".map-panel");
      await page.locator(".language-select select").selectOption("ja");
      await page.waitForFunction(() => document.documentElement.lang === "ja");
      await page.locator(".theme-toggle").click();
      await page.waitForFunction(() => document.documentElement.dataset.theme === "dark");
      await settlePage(page);
      check("retained Map survives live locale/theme transition", await page.locator(".map-panel").count(), 1);
      await capture(page, "ja-dark-live-locale-theme-map");
    } finally { await context.close(); }
  }

  for (const [state, busy] of [["shell-exit", false], ["shell-exit-busy", true]]) {
    const { context, page } = await openPage(browser, { id: state, language: busy ? "ja" : "en", theme: busy ? "dark" : "light", width: busy ? 740 : 1280, height: busy ? 650 : 900, state });
    try {
      const dialog = page.locator("dialog.app-exit-backdrop");
      await dialog.waitFor();
      check(`${state} dialog is sibling after shell`, await page.locator("main.app-shell ~ dialog.app-exit-backdrop").count(), 1);
      await capture(page, busy ? "ja-dark-shell-exit-busy-dialog" : "en-light-shell-exit-dialog");
      if (!busy) { await page.keyboard.press("Escape"); await dialog.waitFor({ state: "detached" }); }
      else { await page.keyboard.press("Escape"); check("busy exit ignores Escape", await dialog.count(), 1); }
    } finally { await context.close(); }
  }
} finally {
  await browser.close();
}

assert.deepEqual(consoleIssues, [], JSON.stringify(consoleIssues, null, 2));
const sourceEnd = closureHashes();
assert.deepEqual(sourceEnd, sourceStart, "served App source closure changed during final capture");
const sourceClosureSha256End = closureDigest(sourceEnd);
assert.equal(sourceClosureSha256End, sourceClosureSha256Start, "served App source closure digest changed during final capture");
const evidenceFiles = ["run-final-current.mjs", "validate-final-current.mjs", "mutation-check.mjs", "equipment-save-contract.mjs", "equipment-regression.mjs", "README.md"].map((name) => describe(path.join(here, name)));
const supportFiles = [
  path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/map-entry-current-harness.mjs"),
  path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs"),
].map(describe);
const acceptanceFiles = [r1HostManifestPath, r1HostResultPath, cConditionalPath, cOwnershipPath, r1OwnershipPath, r1EquipmentPath, r2EquipmentContractPath, r2EquipmentRegressionPath, campaignLocaleInventoryPath].map(describe);
const result = {
  marker: "LWB317_FINAL_CLOSEOUT_R2_CURRENT_OK",
  task: "LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2",
  recordedAt: new Date().toISOString(),
  sourceStart, sourceEnd, sourceClosureSha256Start, sourceClosureSha256End, endpointProvenance, acceptanceChain, localeAudit, raceProofs, assertions, screenshots, styleSnapshots, consoleIssues,
  tools: { node: process.version, browserVersion, files: toolFiles, trees: toolTrees },
  limits: [
    "Current canonical App and controlled source/local preview/deferred callbacks only.",
    "Provider-backed native/gameplay/updater/OS actions remain unavailable and are never invoked; disabled controls remain visible fences.",
    "Protected original-runtime complete-App pixels and loaded native game assets are not established by this current-App packet.",
  ],
};

if (record) {
  fs.writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`);
  const manifest = {
    schema: 1,
    task: result.task,
    marker: "LWB317_FINAL_CLOSEOUT_R2_MANIFEST",
    createdAt: new Date().toISOString(),
    sourceFiles: sourceEnd,
    sourceClosureSha256: sourceClosureSha256End,
    tools: result.tools,
    evidenceFiles,
    supportFiles,
    acceptanceFiles,
    acceptanceChain,
    endpointProvenance,
    result: { path: relative(resultPath), ...hashFile(resultPath) },
    screenshots: screenshots.map((item) => ({ ...item, path: relative(path.join(captureDir, item.file)) })),
  };
  fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
}

console.log(JSON.stringify({ marker: result.marker, recorded: record, assertions: assertions.length, screenshots: screenshots.length, localeKeys: localeAudit.requiredKeys.length, consoleIssues: consoleIssues.length, sourceFiles: Object.keys(sourceEnd).length, browserVersion }));
