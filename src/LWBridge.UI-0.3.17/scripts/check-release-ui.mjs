import { chromium } from "playwright";
import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const base = "http://127.0.0.1:45317/";
const projectRoot = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/(?=[A-Za-z]:)/, "")), "..");
const preview = spawn(process.execPath, [path.join(projectRoot, "node_modules/vite/bin/vite.js"), "preview", "--host", "127.0.0.1", "--port", "45317", "--strictPort"], { cwd: projectRoot, windowsHide: true, stdio: "ignore" });
let ready = false;
for (let attempt = 0; attempt < 80; attempt++) {
  if (preview.exitCode !== null) break;
  try { const response = await fetch(base); if (response.ok) { ready = true; break; } } catch {}
  await new Promise(resolve => setTimeout(resolve, 125));
}
if (!ready || preview.exitCode !== null) { preview.kill(); throw new Error("Release preview server did not start exclusively"); }
const target = path.join(os.tmpdir(), "lwbridge-ui-release-proof");
fs.mkdirSync(target, { recursive: true });
const routes = ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "settings"];
const cases = [
  { code: "en", theme: "light", width: 1365, height: 900 },
  { code: "ja", theme: "dark", width: 640, height: 840 },
];
const result = { browser: "installed Microsoft Edge / Chromium headless", url: base, cases: [], conditionalStates: [], errors: [], screenshots: [] };
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true, args: ["--no-first-run"] });
async function capture(page, name) {
  const filename = path.join(target, name + ".png");
  await page.screenshot({ path: filename, animations: "disabled" });
  result.screenshots.push(filename);
}
async function wait(page) {
  await page.locator(".app-shell").waitFor({ state: "visible", timeout: 15000 });
  await page.waitForTimeout(120);
}
function watch(page, label) {
  page.on("pageerror", e => result.errors.push({ label, kind: "pageerror", text: e.message }));
  page.on("console", m => { if(m.type() === "error") result.errors.push({ label, kind: "console", text: m.text() }); });
  page.on("requestfailed", r => result.errors.push({ label, kind: "requestfailed", text: r.url() + " " + (r.failure()?.errorText || "") }));
}
try {
  for (const variant of cases) {
    const context = await browser.newContext({ viewport: { width: variant.width, height: variant.height }, reducedMotion: "reduce" });
    await context.addInitScript(({code,theme}) => {
      if (!localStorage.getItem("lwbridge.language")) localStorage.setItem("lwbridge.language", code);
      if (!localStorage.getItem("lwbridge.theme")) localStorage.setItem("lwbridge.theme", theme);
    }, variant);
    const page = await context.newPage();
    watch(page, variant.code);
    await page.goto(base, { waitUntil: "networkidle" });
    await wait(page);
    const coverage = { code: variant.code, theme: variant.theme, viewport: [variant.width, variant.height], pages: [], interactions: [] };
    for (let i=0; i<routes.length; i++) {
      await page.locator(".side-nav button").nth(i).click();
      await page.waitForFunction((index) => document.querySelectorAll(".side-nav button")[index]?.getAttribute("aria-current") === "page", i, {timeout:10000});
      await page.waitForTimeout(160);
      const info = await page.evaluate(() => ({
        title: document.querySelector(".side-nav button[aria-current=page]")?.innerText,
        bodyTextLength: document.querySelector(".main-view")?.innerText?.length || 0,
        visibleShell: !!document.querySelector(".app-shell"),
        scrollWidth: document.documentElement.scrollWidth,
        viewport: innerWidth,
        lang: document.documentElement.lang,
        theme: document.documentElement.dataset.theme,
      }));
      if (!info.visibleShell || !info.bodyTextLength || info.lang !== variant.code || info.theme !== variant.theme) {
        throw new Error(JSON.stringify({route:routes[i],variant,info}));
      }
      coverage.pages.push({ route: routes[i], ...info });
      await capture(page, variant.code+"-"+variant.theme+"-"+routes[i]);
    }
    await page.locator(".side-nav button").first().click();
    await page.waitForFunction(() => document.querySelector(".side-nav button")?.getAttribute("aria-current") === "page",null,{timeout:10000});
    coverage.interactions.push("route return to Home");
    await page.locator(".server-jump > button").click();
    const shown = await page.locator(".server-jump-popover").isVisible();
    if (!shown) throw new Error("server jump popover did not open");
    coverage.interactions.push("server-jump popover open");
    await page.locator(".server-jump > button").click();
    if (await page.locator(".server-jump-popover").count()) throw new Error("server jump did not close");
    coverage.interactions.push("server-jump popover close");
    await page.locator(".theme-toggle").click();
    const nextTheme = variant.theme === "dark" ? "light" : "dark";
    if (await page.evaluate(() => document.documentElement.dataset.theme) !== nextTheme) throw new Error("theme control did not apply");
    await page.reload({ waitUntil: "networkidle" });
    await wait(page);
    if (await page.evaluate(() => document.documentElement.dataset.theme) !== nextTheme) throw new Error("theme reload lost");
    coverage.interactions.push("theme toggle and reload persisted");
    await page.locator(".language-select select").selectOption(variant.code === "en" ? "ja" : "en");
    await page.waitForTimeout(280);
    const otherLang = variant.code === "en" ? "ja" : "en";
    if (await page.evaluate(() => document.documentElement.lang) !== otherLang) throw new Error("language switch did not apply");
    await page.reload({ waitUntil: "networkidle" });
    await wait(page);
    if (await page.evaluate(() => document.documentElement.lang) !== otherLang) throw new Error("language reload lost");
    coverage.interactions.push("language switch and reload persisted");
    result.cases.push(coverage);
    await context.close();
  }
  const context = await browser.newContext({viewport:{width:1080,height:800},reducedMotion:"reduce"});
  await context.addInitScript(() => { localStorage.setItem("lwbridge.language","en"); localStorage.setItem("lwbridge.theme","light"); });
  for (const [state,route] of [["home-connected","overview"],["automation-save-error","automation"],["map-city","map-data"],["map-error","map-data"],["squads-equipment-rename","march"],["city-layout-populated","city-layout"],["hotkeys-save-error","hotkeys"],["mini-games-conflict","mini-games"],["settings-update-error","settings"],["shell-exit-busy","overview"]]) {
    const page=await context.newPage();
    watch(page,state);
    await page.goto(base+"?previewState="+encodeURIComponent(state), {waitUntil:"networkidle"});
    await wait(page);
    const i=routes.indexOf(route);
    if (state !== "shell-exit-busy") await page.locator(".side-nav button").nth(i).click();
    else await page.locator("dialog[aria-busy=true]").waitFor();
    await page.waitForTimeout(200);
    const data=await page.evaluate(() => ({
      textLength: document.querySelector(".main-view")?.innerText.length || 0,
      dialogs: document.querySelectorAll("[role=dialog]").length,
      busy: document.querySelectorAll("[aria-busy=true]").length
    }));
    if (!data.textLength) throw new Error("conditional view empty: "+state);
    await capture(page,"conditional-"+state);
    result.conditionalStates.push({state,route,...data});
    await page.close();
  }
  await context.close();
} finally {
  await browser.close();
  preview.kill();
  fs.writeFileSync(path.join(target,"coverage.json"), JSON.stringify(result,null,2));
}
console.log(JSON.stringify({cases:result.cases.map(x=>({variant:x.code+"/"+x.theme,viewport:x.viewport,routes:x.pages.map(p=>p.route),interactions:x.interactions,overflow:x.pages.filter(p=>p.scrollWidth>p.viewport).map(p=>p.route)})), conditionalStates:result.conditionalStates, errors:result.errors, screenshotCount:result.screenshots.length, proof:target},null,2));
if(result.errors.length) process.exitCode=1;
