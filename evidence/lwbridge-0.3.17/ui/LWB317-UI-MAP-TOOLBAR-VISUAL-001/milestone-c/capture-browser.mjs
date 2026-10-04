import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const browserDir = path.join(here, "browser");
fs.mkdirSync(browserDir, { recursive: true });
const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");

const manifest = readJson(path.join(here, "browser-pairs.json"));
assert.equal(manifest.marker, "LWB317_MAP_TOOLBAR_BROWSER_PAIRS_PREPARED");
assert.equal(manifest.pairs.length, 6);

const playwrightRequire = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = playwrightRequire("playwright");
const chromePath = "C:/Program Files/Google/Chrome/Application/chrome.exe";
assert.ok(fs.existsSync(chromePath), "Chrome executable");

const styleKeys = [
  "display", "flexDirection", "flexWrap", "alignItems", "justifyContent", "gap",
  "gridTemplateColumns", "gridTemplateRows", "columnGap", "rowGap", "width", "height",
  "minWidth", "minHeight", "paddingTop", "paddingRight", "paddingBottom", "paddingLeft",
  "marginTop", "marginRight", "marginBottom", "marginLeft", "borderTopWidth", "borderRightWidth",
  "borderBottomWidth", "borderLeftWidth", "borderTopColor", "borderRightColor", "borderBottomColor",
  "borderLeftColor", "borderRadius", "color", "backgroundColor", "fontFamily", "fontSize",
  "fontWeight", "lineHeight", "opacity", "cursor", "boxShadow", "overflow", "whiteSpace", "position",
];

async function measure(page) {
  return page.evaluate((keys) => {
    const panel = document.querySelector(".map-panel");
    const search = panel?.querySelector(".map-search");
    if (!panel || !search) throw new Error("missing scoped Map proof root");
    const styleOf = (element) => {
      if (!element) return null;
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return {
        tag: element.tagName,
        attrs: [...element.attributes].sort((a, b) => a.name.localeCompare(b.name)).map((attr) => [attr.name, attr.value]),
        directText: [...element.childNodes].filter((node) => node.nodeType === Node.TEXT_NODE).map((node) => node.textContent).join(""),
        text: element.textContent,
        rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
        styles: Object.fromEntries(keys.map((key) => [key, style[key]])),
      };
    };
    const anchors = {
      panel: styleOf(panel),
      search: styleOf(search),
      tabs: styleOf(search.querySelector(".map-tabs")),
      searchbar: styleOf(search.querySelector(".map-searchbar")),
      resultCount: styleOf(search.querySelector(".map-result-count")),
      pagination: styleOf(search.querySelector(".map-pagination")),
      previous: styleOf(search.querySelector(".map-pagination > button:first-child")),
      pageInfo: styleOf(search.querySelector(".map-pagination > span")),
      next: styleOf(search.querySelector(".map-pagination > button:last-child")),
    };
    [...search.querySelectorAll(".map-tabs > button")].forEach((element, index) => { anchors[`tab${index}`] = styleOf(element); });
    [...search.querySelectorAll(".map-searchbar > *")].forEach((element, index) => { anchors[`searchChild${index}`] = styleOf(element); });
    [...search.querySelectorAll("details.map-item-filter")].forEach((element, index) => {
      anchors[`itemFilter${index}`] = styleOf(element);
      anchors[`itemSummary${index}`] = styleOf(element.querySelector("summary"));
      anchors[`itemMenu${index}`] = styleOf(element.querySelector(".map-item-filter-menu"));
    });
    return {
      title: document.title,
      language: document.documentElement.lang,
      theme: document.documentElement.dataset.theme,
      fontsStatus: document.fonts?.status || "unsupported",
      timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      viewport: {
        innerWidth, innerHeight, devicePixelRatio,
        clientWidth: document.documentElement.clientWidth,
        clientHeight: document.documentElement.clientHeight,
        scrollWidth: document.documentElement.scrollWidth,
        scrollHeight: document.documentElement.scrollHeight,
      },
      outerHTML: panel.outerHTML,
      anchors,
    };
  }, styleKeys);
}

function compareMeasurements(original, current) {
  const geometryDifferences = [];
  const styleDifferences = [];
  const attributeDifferences = [];
  const names = new Set([...Object.keys(original.anchors), ...Object.keys(current.anchors)]);
  for (const anchor of names) {
    const left = original.anchors[anchor];
    const right = current.anchors[anchor];
    if (!left || !right) {
      if (left || right) geometryDifferences.push({ anchor, original: left ? "present" : null, current: right ? "present" : null });
      continue;
    }
    if (left.tag !== right.tag) attributeDifferences.push({ anchor, property: "tag", original: left.tag, current: right.tag });
    if (JSON.stringify(left.attrs) !== JSON.stringify(right.attrs)) attributeDifferences.push({ anchor, property: "attrs", original: left.attrs, current: right.attrs });
    if (left.directText !== right.directText) attributeDifferences.push({ anchor, property: "directText", original: left.directText, current: right.directText });
    for (const key of ["x", "y", "width", "height"]) {
      if (left.rect[key] !== right.rect[key]) geometryDifferences.push({ anchor, property: key, original: left.rect[key], current: right.rect[key] });
    }
    for (const key of styleKeys) {
      if (left.styles[key] !== right.styles[key]) styleDifferences.push({ anchor, property: key, original: left.styles[key], current: right.styles[key] });
    }
  }
  return {
    domExact: original.outerHTML === current.outerHTML,
    originalDomSha256: hash(Buffer.from(original.outerHTML, "utf8")),
    currentDomSha256: hash(Buffer.from(current.outerHTML, "utf8")),
    geometryDifferences,
    styleDifferences,
    attributeDifferences,
  };
}

const browser = await chromium.launch({ headless: true, executablePath: chromePath });
const browserVersion = await browser.version();
const measurements = [];
const consoleRecords = [];
const comparisons = [];

try {
  for (const pair of manifest.pairs) {
    const context = await browser.newContext({
      viewport: { width: pair.width, height: pair.height },
      deviceScaleFactor: 1,
      timezoneId: "Asia/Singapore",
    });
    const sides = {};
    try {
      for (const side of ["original", "current"]) {
        const page = await context.newPage();
        const issues = [];
        page.on("console", (message) => { if (["warning", "error"].includes(message.type())) issues.push({ type: message.type(), text: message.text() }); });
        page.on("pageerror", (error) => issues.push({ type: "pageerror", text: String(error?.stack || error) }));
        const generated = path.join(repo, pair.generatedFiles[side].path);
        await page.goto(pathToFileURL(generated).href, { waitUntil: "load" });
        await page.evaluate(() => document.fonts?.ready);
        await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        const measurement = await measure(page);
        assert.equal(measurement.language, pair.language, `${pair.id}/${side}: language`);
        assert.equal(measurement.theme, pair.theme, `${pair.id}/${side}: theme`);
        assert.equal(measurement.fontsStatus, "loaded", `${pair.id}/${side}: fonts ready`);
        assert.equal(measurement.timezone, "Asia/Singapore", `${pair.id}/${side}: timezone`);
        assert.equal(measurement.viewport.innerWidth, pair.width, `${pair.id}/${side}: viewport width`);
        assert.equal(measurement.viewport.innerHeight, pair.height, `${pair.id}/${side}: viewport height`);
        assert.equal(measurement.viewport.devicePixelRatio, 1, `${pair.id}/${side}: DPR`);
        const screenshotFile = path.join(browserDir, `${pair.id}-${side}.png`);
        const screenshot = await page.screenshot({ path: screenshotFile, fullPage: false, animations: "disabled" });
        const record = {
          pairId: pair.id, tab: pair.tab, language: pair.language, theme: pair.theme, side,
          viewport: pair.viewport, generatedFile: pair.generatedFiles[side].path,
          measurement, screenshot: rel(screenshotFile), screenshotSha256: hash(screenshot), screenshotBytes: screenshot.length,
          consoleIssueCount: issues.length,
        };
        sides[side] = record;
        measurements.push(record);
        consoleRecords.push({ pairId: pair.id, side, issues });
        await page.close();
      }
      const comparison = compareMeasurements(sides.original.measurement, sides.current.measurement);
      comparisons.push({
        pairId: pair.id, tab: pair.tab, language: pair.language, theme: pair.theme, viewport: pair.viewport,
        originalScreenshotSha256: sides.original.screenshotSha256,
        currentScreenshotSha256: sides.current.screenshotSha256,
        exactScreenshotBytes: sides.original.screenshotSha256 === sides.current.screenshotSha256,
        ...comparison,
      });
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

writeJson(path.join(browserDir, "measurements.json"), {
  task: manifest.task,
  marker: "LWB317_MAP_TOOLBAR_BROWSER_MEASURED",
  environment: { browserVersion, executable: chromePath, timezone: "Asia/Singapore", deviceScaleFactor: 1, cssOrder: manifest.fixedEnvironment.cssOrder, combinedCssSha256: manifest.fixedEnvironment.combinedCssSha256 },
  records: measurements,
});
writeJson(path.join(browserDir, "console.json"), consoleRecords);
writeJson(path.join(browserDir, "comparison-results.json"), { task: manifest.task, marker: "LWB317_MAP_TOOLBAR_BROWSER_COMPARED", pairs: comparisons });

const summary = {
  marker: "LWB317_MAP_TOOLBAR_BROWSER_CAPTURE_OK",
  pairs: comparisons.length,
  screenshots: measurements.length,
  exactScreenshotPairs: comparisons.filter((pair) => pair.exactScreenshotBytes).length,
  exactDomPairs: comparisons.filter((pair) => pair.domExact).length,
  geometryDifferences: comparisons.reduce((sum, pair) => sum + pair.geometryDifferences.length, 0),
  styleDifferences: comparisons.reduce((sum, pair) => sum + pair.styleDifferences.length, 0),
  attributeDifferences: comparisons.reduce((sum, pair) => sum + pair.attributeDifferences.length, 0),
  consoleIssues: consoleRecords.reduce((sum, entry) => sum + entry.issues.length, 0),
};
console.log(JSON.stringify(summary, null, 2));
assert.equal(comparisons.length, 6, "six required pairs");
assert.equal(measurements.length, 12, "twelve browser screenshots");
assert.equal(summary.consoleIssues, 0, "browser console/page errors");
