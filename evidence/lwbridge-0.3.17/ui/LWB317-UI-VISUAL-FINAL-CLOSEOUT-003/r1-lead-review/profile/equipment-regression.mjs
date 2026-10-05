import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = "C:\\Users\\chimw\\OneDrive\\Desktop\\Github\\LW-Control";
const args = new Map(process.argv.slice(2).map((arg) => {
  const [key, ...rest] = arg.replace(/^--/, "").split("=");
  return [key, rest.join("=") || true];
}));
const verifyOnly = args.has("verify");
const port = Number(args.get("port") || 4451);
const baseUrl = `http://127.0.0.1:${port}/`;
const output = path.join(here, "equipment-regression-results.json");
const screenshotDir = path.join(here, "equipment-regression");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const describe = (relative) => {
  const bytes = fs.readFileSync(path.join(repo, relative));
  return { path: relative.replaceAll("\\", "/"), bytes: bytes.length, sha256: sha(bytes) };
};

const requirePlaywright = createRequire("C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json");
const { chromium } = requirePlaywright("playwright");
const browser = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
const assertions = [];
const screenshots = [];
const issues = [];
const check = (name, actual, expected) => {
  assert.deepEqual(actual, expected, name);
  assertions.push({ name, actual, expected });
};

async function open(state) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: "reduce" });
  const page = await context.newPage();
  page.on("console", (message) => {
    if (["warning", "error"].includes(message.type())) issues.push({ state, type: message.type(), text: message.text() });
  });
  page.on("pageerror", (error) => issues.push({ state, type: "pageerror", text: String(error?.stack || error) }));
  const params = new URLSearchParams({ previewPage: "march", previewState: state, previewLanguage: "en", previewTheme: "light" });
  await page.goto(`${baseUrl}?${params}`, { waitUntil: "domcontentloaded" });
  await page.locator(".equipment-preset-layout").waitFor();
  await page.evaluate(() => document.fonts?.ready);
  return { context, page };
}

async function capture(page, id) {
  await page.evaluate(() => {
    if (document.activeElement instanceof HTMLElement) document.activeElement.blur();
    return new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  const buffer = await page.locator("section.squad-panel").screenshot();
  const file = path.join(screenshotDir, `${id}.png`);
  if (!verifyOnly) {
    fs.mkdirSync(screenshotDir, { recursive: true });
    fs.writeFileSync(file, buffer);
  }
  screenshots.push({ id, file: path.relative(repo, file).replaceAll("\\", "/"), bytes: buffer.length, sha256: sha(buffer) });
}

try {
  {
    const { context, page } = await open("squads-equipment");
    try {
      const presets = page.locator(".equipment-preset-list > button");
      check("Equipment retains four recovered preset slots", await presets.count(), 4);
      check("Equipment first preset starts selected", await presets.first().getAttribute("class"), "active");
      const originalName = (await presets.first().innerText()).split("\n")[0].trim();
      await page.getByRole("button", { name: /^Rename$/ }).click();
      const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
      check("Equipment rename opens native dialog", await dialog.getAttribute("open"), "");
      const input = dialog.locator("input");
      await input.fill("R1 retained equipment");
      await dialog.locator(".equipment-preset-actions .primary").click();
      await dialog.waitFor({ state: "hidden" });
      check("Equipment successful rename commits profile config", (await presets.first().innerText()).includes("R1 retained equipment"), true);
      check("Equipment successful rename clears derived dirty marker", (await presets.first().innerText()).includes("*"), false);
      await page.locator(".squad-tabs > button").nth(0).click();
      await page.locator(".squad-tabs > button").nth(1).click();
      check("Equipment config survives same-profile AFK hide-return", (await presets.first().innerText()).includes("R1 retained equipment"), true);
      check("Equipment original name was distinct", originalName !== "R1 retained equipment", true);
      await capture(page, "equipment-confirmed-hide-return");
    } finally {
      await context.close();
    }
  }

  {
    const { context, page } = await open("squads-equipment-rename-error");
    try {
      const presets = page.locator(".equipment-preset-list > button");
      await page.getByRole("button", { name: /^Rename$/ }).click();
      const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
      await dialog.locator("input").fill("R1 retry equipment");
      await dialog.locator(".equipment-preset-actions .primary").click();
      const error = dialog.locator(".automation-error");
      await error.waitFor();
      check("Equipment failed save keeps renamed draft dirty", (await presets.first().innerText()).includes("R1 retry equipment *"), true);
      check("Equipment failed save exposes Retry", await error.locator("button").nth(0).count(), 1);
      check("Equipment failed save exposes Discard", await error.locator("button").nth(1).count(), 1);
      await error.locator("button").nth(0).click();
      await error.waitFor({ state: "hidden" });
      check("Equipment Retry confirms only the retained draft", (await presets.first().innerText()).includes("*"), false);
      await dialog.locator(".equipment-preset-actions .primary").click();
      await dialog.waitFor({ state: "hidden" });
      check("Equipment Retry path retains renamed confirmed value", (await presets.first().innerText()).includes("R1 retry equipment"), true);
    } finally {
      await context.close();
    }
  }

  {
    const { context, page } = await open("squads-equipment-rename-error");
    try {
      const presets = page.locator(".equipment-preset-list > button");
      const initial = (await presets.first().innerText()).split("\n")[0].trim();
      await page.getByRole("button", { name: /^Rename$/ }).click();
      const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
      await dialog.locator("input").fill("R1 discarded equipment");
      await dialog.locator(".equipment-preset-actions .primary").click();
      const error = dialog.locator(".automation-error");
      await error.waitFor();
      await error.locator("button").nth(1).click();
      await error.waitFor({ state: "hidden" });
      check("Equipment Discard restores owning confirmed preset", (await presets.first().innerText()).includes(initial), true);
      check("Equipment Discard removes owning dirty marker", (await presets.first().innerText()).includes("*"), false);
      await dialog.locator(".equipment-preset-actions > button").first().click();
      await dialog.waitFor({ state: "hidden" });
    } finally {
      await context.close();
    }
  }

  {
    const { context, page } = await open("squads-equipment-rename-pending");
    try {
      await page.getByRole("button", { name: /^Rename$/ }).click();
      const dialog = page.locator("dialog.equipment-preset-dialog-backdrop");
      await dialog.locator("input").fill("R1 pending equipment");
      await dialog.locator(".equipment-preset-actions .primary").click();
      await page.waitForFunction(() => document.querySelector("dialog.equipment-preset-dialog-backdrop")?.getAttribute("aria-busy") === "true");
      check("Equipment deferred save marks dialog busy", await dialog.getAttribute("aria-busy"), "true");
      check("Equipment deferred save disables rename input", await dialog.locator("input").isDisabled(), true);
      check("Equipment deferred save keeps draft visible", (await page.locator(".equipment-preset-list > button").first().innerText()).includes("R1 pending equipment"), true);
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

assert.deepEqual(issues, [], `Equipment browser issues: ${JSON.stringify(issues)}`);
const result = {
  marker: "LWB317_FINAL_CLOSEOUT_R1_EQUIPMENT_REGRESSION_OK",
  browser: await (async () => {
    const probe = await chromium.launch({ headless: true, executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe" });
    try { return await probe.version(); } finally { await probe.close(); }
  })(),
  assertions,
  screenshots,
  issues,
  sources: [
    describe("src/LWBridge.UI-0.3.17/src/SquadsPage.jsx"),
    describe("src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx"),
    describe("src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js"),
    describe("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js"),
  ],
  limits: "Source/local Equipment configuration, error/retry/discard, deferred busy and same-profile Activity retention only. Native Equipment read/apply/save providers and physical HTML5 drag remain fenced.",
};
if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(output, "utf8"));
  assert.deepEqual(result, recorded, "recorded R1 Equipment regression drifted");
  console.log(`R1_EQUIPMENT_REGRESSION_VERIFY_OK assertions=${assertions.length}`);
} else {
  fs.writeFileSync(output, `${JSON.stringify(result, null, 2)}\n`);
  console.log(`R1_EQUIPMENT_REGRESSION_OK assertions=${assertions.length}`);
}
