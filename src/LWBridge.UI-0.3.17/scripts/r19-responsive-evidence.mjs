import { chromium } from "playwright";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "../../..");
const artifactRoot = path.join(repoRoot, "artifacts", "feature018-r19-ui");
const phaseArg = process.argv.find((arg) => arg.startsWith("--phase="));
const phase = phaseArg ? phaseArg.slice("--phase=".length) : "after";
if (!new Set(["before", "after"]).has(phase)) throw new Error(`Unsupported phase: ${phase}`);

const pages = [
  ["squads-afk", "march", "squads-profile"],
  ["squads-equipment", "march", "squads-equipment"],
  ["automation", "automation", "automation-runtime-running"],
  ["mini-games", "mini-games", "mini-games-active"],
  ["map-data", "map-data", "map-actions-ready"],
  ["city-layout", "city-layout", "city-layout-populated-moved"],
  ["overview", "overview", "home-connected"],
  ["hotkeys", "hotkeys", "hotkeys-connected"],
  ["settings", "settings", ""],
];
const widths = [900, 1100, 1400, 1600];
const variants = phase === "before"
  ? [["en", "light"]]
  : [["en", "light"], ["en", "dark"], ["zh-CN", "light"], ["zh-CN", "dark"]];

const browser = await chromium.launch({
  headless: true,
  ...(process.env.R19_BROWSER_EXE ? { executablePath: process.env.R19_BROWSER_EXE } : {}),
});
const results = [];

try {
  for (const [pageKey, view, previewState] of pages) {
    for (const width of widths) {
      for (const [language, theme] of variants) {
        const context = await browser.newContext({ viewport: { width, height: 960 }, deviceScaleFactor: 1 });
        const page = await context.newPage();
        const params = new URLSearchParams({ view, previewLanguage: language, previewTheme: theme });
        if (previewState) params.set("previewState", previewState);
        const url = `http://127.0.0.1:4317/?${params}`;
        await page.goto(url, { waitUntil: "networkidle" });
        await page.evaluate(async () => { if (document.fonts?.ready) await document.fonts.ready; });
        await page.waitForTimeout(120);

        const metrics = await page.evaluate(() => {
          const root = document.documentElement;
          const body = document.body;
          const mainView = document.querySelector(".main-view");
          const appLayout = document.querySelector(".app-layout");
          const pageOverflow = Math.max(root.scrollWidth, body.scrollWidth) - window.innerWidth;
          const mainOverflow = mainView ? mainView.scrollWidth - mainView.clientWidth : 0;
          const layoutOverflow = appLayout ? appLayout.scrollWidth - appLayout.clientWidth : 0;
          const allowedScroll = new Set(["map-table-scroll", "city-layout-grid-viewport", "automation-categories"]);
          const hasAllowedScrollAncestor = (element) => {
            let current = element.parentElement;
            while (current && current !== body) {
              if ([...allowedScroll].some((name) => current.classList.contains(name))) return true;
              current = current.parentElement;
            }
            return false;
          };
          const viewportOffenders = [...document.querySelectorAll(".main-view *")]
            .map((element) => ({ element, rect: element.getBoundingClientRect() }))
            .filter(({ element, rect }) => {
              if (rect.width <= 0 || rect.height <= 0) return false;
              if (hasAllowedScrollAncestor(element)) return false;
              return rect.right > window.innerWidth + 1 || rect.left < -1;
            })
            .slice(0, 20)
            .map(({ element, rect }) => ({
              tag: element.tagName,
              className: String(element.className || "").slice(0, 120),
              text: String(element.textContent || "").trim().replace(/\s+/g, " ").slice(0, 120),
              left: Math.round(rect.left),
              right: Math.round(rect.right),
            }));
          const textSelectors = [
            ".monster-afk-compact-card .automation-card-header h3",
            ".monster-afk-compact-card .automation-card-header p",
            ".monster-afk-profile-heading strong",
            ".equipment-preset-list>button span",
            ".equipment-position-hero strong",
            ".preset-equipment-slot span",
            ".automation-card-header h3",
            ".automation-card-header p",
            ".hotkey-card h3",
            ".hotkey-card p",
          ];
          const clippedText = textSelectors.flatMap((selector) => [...document.querySelectorAll(selector)].map((element) => {
            const clipped = element.scrollWidth > element.clientWidth + 1 || element.scrollHeight > element.clientHeight + 1;
            if (!clipped) return null;
            return {
              selector,
              text: String(element.textContent || "").trim().replace(/\s+/g, " ").slice(0, 160),
              title: element.getAttribute("title") || "",
              clientWidth: element.clientWidth,
              scrollWidth: element.scrollWidth,
              clientHeight: element.clientHeight,
              scrollHeight: element.scrollHeight,
            };
          }).filter(Boolean));
          const mapScroll = document.querySelector(".map-table-scroll");
          const automationTabs = document.querySelector(".automation-categories");
          const topBar = document.querySelector(".top-bar");
          const statusStrip = document.querySelector(".top-bar .status-strip");
          const miniCards = [...document.querySelectorAll('[data-hotkey-category="miniGames"] .hotkey-grid > .hotkey-card')];
          return {
            pageOverflow,
            mainOverflow,
            layoutOverflow,
            viewportOffenders,
            clippedText,
            mapTable: mapScroll ? { clientWidth: mapScroll.clientWidth, scrollWidth: mapScroll.scrollWidth, overflowX: getComputedStyle(mapScroll).overflowX } : null,
            automationTabs: automationTabs ? { clientWidth: automationTabs.clientWidth, scrollWidth: automationTabs.scrollWidth, overflowX: getComputedStyle(automationTabs).overflowX } : null,
            topBar: topBar ? { height: Math.round(topBar.getBoundingClientRect().height) } : null,
            statusStrip: statusStrip ? { top: Math.round(statusStrip.getBoundingClientRect().top), left: Math.round(statusStrip.getBoundingClientRect().left) } : null,
            miniCardCount: miniCards.length,
            miniHasG: miniCards.some((card) => card.querySelector("kbd")?.textContent === "G"),
          };
        });

        const row = { phase, page: pageKey, width, language, theme, url, ...metrics };
        results.push(row);

        if (language === "en" && theme === "light") {
          const outDir = path.join(artifactRoot, phase);
          await fs.mkdir(outDir, { recursive: true });
          await page.screenshot({ path: path.join(outDir, `${pageKey}-${width}-en-light.png`), fullPage: true });
        }
        await context.close();
      }
    }
  }
} finally {
  await browser.close();
}

await fs.mkdir(artifactRoot, { recursive: true });
await fs.writeFile(path.join(artifactRoot, `${phase}-metrics.json`), `${JSON.stringify(results, null, 2)}\n`, "utf8");

const failures = phase === "after" ? results.filter((row) =>
  row.pageOverflow > 1 ||
  row.layoutOverflow > 1 ||
  row.viewportOffenders.length > 0 ||
  row.clippedText.some((entry) => !entry.title) ||
  (row.page === "mini-games" && (row.miniCardCount !== 4 || !row.miniHasG))
) : [];

console.log(`R19 ${phase}: ${results.length} viewport variants; screenshots=${pages.length * widths.length}`);
if (phase === "after") {
  if (failures.length) {
    console.error(`R19 responsive acceptance failed in ${failures.length} viewport variants.`);
    for (const failure of failures.slice(0, 20)) {
      console.error(`${failure.page} ${failure.width} ${failure.language} ${failure.theme}: pageOverflow=${failure.pageOverflow}, layoutOverflow=${failure.layoutOverflow}, offenders=${failure.viewportOffenders.length}, clipped=${failure.clippedText.length}`);
    }
    process.exitCode = 1;
  } else {
    console.log("R19 responsive acceptance PASS");
  }
}
