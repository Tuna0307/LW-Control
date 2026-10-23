import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const base = "http://127.0.0.1:45329/";
const executablePath = process.env.R29_BROWSER_EXE ||
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const widths = [900, 1600];
const languages = ["en", "zh-CN"];
const themes = ["light", "dark"];
const preview = spawn(process.execPath, [
  path.join(root, "node_modules/vite/bin/vite.js"),
  "--host", "127.0.0.1", "--port", "45329", "--strictPort",
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
  throw new Error("R29 Mini Games responsive preview server did not start exclusively");
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
          view: "mini-games",
          previewState: "mini-games-active",
          previewLanguage: language,
          previewTheme: theme,
        });
        await page.goto(`${base}?${params}`, { waitUntil: "networkidle" });
        await page.locator('[data-hotkey-category="miniGames"]').waitFor({ state: "visible" });
        await page.evaluate(async () => { if (document.fonts?.ready) await document.fonts.ready; });

        const metrics = await page.evaluate(() => {
          const root = document.documentElement;
          const body = document.body;
          const panel = document.querySelector('[data-hotkey-category="miniGames"]');
          const cards = [...panel.querySelectorAll(".hotkey-grid > .hotkey-card")];
          const offenders = [...panel.querySelectorAll("*")].filter((element) => {
            const rect = element.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0 &&
              (rect.left < -1 || rect.right > window.innerWidth + 1);
          });
          const clipped = [...panel.querySelectorAll(".hotkey-card h3, .hotkey-card p, .hotkey-attack-toggle")]
            .filter((element) => element.scrollWidth > element.clientWidth + 1 ||
              element.scrollHeight > element.clientHeight + 1);
          const gCard = cards.find((card) => card.querySelector("kbd")?.textContent === "G");
          return {
            language: document.documentElement.lang,
            theme: document.documentElement.dataset.theme,
            cardCount: cards.length,
            hasG: !!gCard,
            hasGCount: !!gCard?.querySelector('input[type="number"]'),
            pageOverflow: Math.max(root.scrollWidth, body.scrollWidth) - window.innerWidth,
            offenders: offenders.length,
            clipped: clipped.length,
          };
        });

        assert.equal(metrics.language, language, `Mini Games language mismatch at ${width}px`);
        assert.equal(metrics.theme, theme, `Mini Games theme mismatch at ${width}px`);
        assert.equal(metrics.cardCount, 4,
          `Mini Games must render G, Black Market, Land and Food House at ${width}px ${language} ${theme}`);
        assert.equal(metrics.hasG, true, `Mini Games lost the G card at ${width}px ${language} ${theme}`);
        assert.equal(metrics.hasGCount, true,
          `Mini Games lost the G count field at ${width}px ${language} ${theme}`);
        assert.ok(metrics.pageOverflow <= 1,
          `Mini Games page overflowed by ${metrics.pageOverflow}px at ${width}px ${language} ${theme}`);
        assert.equal(metrics.offenders, 0,
          `Mini Games has viewport offenders at ${width}px ${language} ${theme}`);
        assert.equal(metrics.clipped, 0,
          `Mini Games has clipped card text at ${width}px ${language} ${theme}`);
        rows.push({ width, language, theme, ...metrics });
        await context.close();
      }
    }
  }
} finally {
  await browser.close();
  preview.kill();
}

console.log(`R29_HOTKEYS_MINI_RESPONSIVE_OK cases=${rows.length} widths=900,1600 languages=en,zh-CN cards=4 g=true overflow=0 clipped=0`);
