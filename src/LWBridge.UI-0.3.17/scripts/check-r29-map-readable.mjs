import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const base = "http://127.0.0.1:45330/";
const executablePath = process.env.R29_BROWSER_EXE ||
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const widths = [900, 1600];
const languages = ["en", "zh-CN"];
const themes = ["light", "dark"];
const preview = spawn(process.execPath, [
  path.join(root, "node_modules/vite/bin/vite.js"),
  "--host", "127.0.0.1", "--port", "45330", "--strictPort",
], { cwd: root, windowsHide: true, stdio: "ignore" });
let ready = false;
for (let attempt = 0; attempt < 80; attempt += 1) {
  if (preview.exitCode !== null) break;
  try {
    const response = await fetch(base);
    if (response.ok) {
      ready = true;
      break;
    }
  } catch {}
  await new Promise((resolve) => setTimeout(resolve, 125));
}
if (!ready || preview.exitCode !== null) {
  preview.kill();
  throw new Error("R29 Map labels preview server did not start exclusively");
}

const browser = await chromium.launch({ executablePath, headless: true, args: ["--no-first-run"] });
const rows = [];

try {
  for (const width of widths) {
    for (const language of languages) {
      for (const theme of themes) {
        const context = await browser.newContext({ viewport: { width, height: 960 }, deviceScaleFactor: 1 });
        const page = await context.newPage();
        const params = new URLSearchParams({
          view: "map-data",
          previewState: "map-city",
          previewLanguage: language,
          previewTheme: theme,
        });
        await page.goto(`${base}?${params}`, { waitUntil: "networkidle" });
        await page.locator('.map-tabs').waitFor({ state: "visible" });
        const metrics = await page.evaluate(() => {
          const tabs=[...document.querySelectorAll('.map-tabs button')];
          return { count:tabs.length, overflow:document.documentElement.scrollWidth-window.innerWidth,
            clipped:tabs.filter(b=>b.scrollWidth>b.clientWidth+1).map(b=>b.textContent),
            wrapped:tabs.filter(b=>getComputedStyle(b).whiteSpace!=="nowrap").length };
        });
        assert.equal(metrics.count,10);
        assert.equal(metrics.wrapped,0,`Map result labels split at ${width}px ${language}`);
        assert.equal(metrics.clipped.length,0,JSON.stringify({width,language,...metrics}));
        assert.ok(metrics.overflow<=1);
        rows.push({ width, language, theme, ...metrics });
        await context.close();
      }
    }
  }
} finally {
  await browser.close();
  preview.kill();
}

console.log(`R29_MAP_READABLE_PASS cases=${rows.length} widths=900,1600 languages=en,zh-CN no-wrap/no-clipping`);
