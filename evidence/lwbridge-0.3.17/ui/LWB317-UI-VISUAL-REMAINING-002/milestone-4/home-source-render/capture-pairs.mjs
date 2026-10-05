import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const pw = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = pw("playwright");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const generated = path.join(here, "generated");
const out = path.join(here, "pixels");
fs.mkdirSync(out, { recursive: true });

const scenarios = [
  "checking",
  "missing-error-busy",
  "busy-overlap",
  "recovery-failed-busy",
  "preference-launch-saving",
  "preference-reconnect-saving",
  "stopped",
  "running-connected",
  "repair",
  "unavailable",
];
const variants = [
  { language: "en", theme: "light", width: 1280, height: 900 },
  { language: "en", theme: "light", width: 740, height: 600 },
  { language: "ja", theme: "dark", width: 1280, height: 900 },
  { language: "ja", theme: "dark", width: 740, height: 600 },
];

const browser = await chromium.launch({ executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe", headless: true });
const pairs = [];
const errors = [];
for (const variant of variants) {
  for (const scenario of scenarios) {
    const id = `${variant.language}-${variant.theme}-${variant.width}-${scenario}`;
    const sides = {};
    for (const side of ["original", "current"]) {
      const context = await browser.newContext({
        viewport: { width: variant.width, height: variant.height },
        locale: variant.language,
        timezoneId: "Asia/Singapore",
        colorScheme: variant.theme,
        deviceScaleFactor: 1,
      });
      const page = await context.newPage();
      page.on("console", (message) => {
        if (["warning", "error"].includes(message.type())) errors.push({ id, side, type: message.type(), text: message.text() });
      });
      page.on("pageerror", (error) => errors.push({ id, side, type: "pageerror", text: error.message }));
      const html = fs.readFileSync(path.join(generated, `${variant.language}-${variant.theme}-${scenario}-${side}.html`), "utf8");
      await page.setContent(html);
      await page.evaluate(() => document.fonts.ready);
      const measurement = await page.evaluate(() => {
        const root = document.querySelector(".quick-actions-panel");
        const rect = (element) => {
          const box = element.getBoundingClientRect();
          return { x: box.x, y: box.y, width: box.width, height: box.height };
        };
        return {
          root: rect(root),
          scrollWidth: document.documentElement.scrollWidth,
          clientWidth: document.documentElement.clientWidth,
          controls: [...root.querySelectorAll("button,[role='switch']")].map((element) => ({
            tag: element.tagName,
            cls: element.className,
            disabled: element.disabled === true,
            ariaChecked: element.getAttribute("aria-checked"),
            text: element.textContent.trim(),
            rect: rect(element),
          })),
        };
      });
      const screenshot = `${id}-${side}.png`;
      await page.screenshot({ path: path.join(out, screenshot), fullPage: true });
      const bytes = fs.readFileSync(path.join(out, screenshot));
      sides[side] = { screenshot, sha256: hash(bytes), measurement };
      await context.close();
    }
    pairs.push({ id, scenario, ...variant, ...sides });
  }
}
await browser.close();
fs.writeFileSync(path.join(out, "pairs.json"), JSON.stringify({ pairs, errors, tool: { playwright: pw("playwright/package.json").version } }, null, 2) + "\n");
console.log(JSON.stringify({ marker: "LWB317_REMAINING_M4_HOME_BROWSER_PAIRS_OK", pairs: pairs.length, errors: errors.length }));
