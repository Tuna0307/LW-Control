import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../../..');
const pw = createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const { chromium } = pw('playwright');
const hash = (value) => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const input = JSON.parse(fs.readFileSync(path.join(here, 'renderer-inputs.json'), 'utf8'));
const out = path.join(here, 'browser');
fs.mkdirSync(out, { recursive: true });
const styles = {
  original: fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'), 'utf8'),
  current: `${fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/reference.css'), 'utf8')}\n${fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/styles.css'), 'utf8')}`,
};
const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
const pairs = [];
const consoleIssues = [];
const props = ['display','position','fontFamily','fontSize','fontWeight','lineHeight','color','backgroundColor','borderColor','borderWidth','borderRadius','padding','margin','gap','gridTemplateColumns','gridTemplateRows','flexDirection','alignItems','justifyContent','overflow','opacity','cursor'];

for (const data of input.cases) {
  const sides = {};
  for (const side of ['original', 'current']) {
    const context = await browser.newContext({ viewport: { width: data.width, height: data.width <= 760 ? 1000 : 900 }, locale: data.language, timezoneId: 'Asia/Singapore', colorScheme: data.theme, deviceScaleFactor: 1 });
    const page = await context.newPage();
    page.on('console', (message) => { if (['warning','error'].includes(message.type())) consoleIssues.push({ id: data.id, side, type: message.type(), text: message.text() }); });
    page.on('pageerror', (error) => consoleIssues.push({ id: data.id, side, type: 'pageerror', text: error.message }));
    const raw = fs.readFileSync(path.join(here, data[`${side}Html`]), 'utf8');
    await page.setContent(`<!doctype html><html lang="${data.language}" data-theme="${data.theme}"><head><meta charset="utf-8"><style>${styles[side]}</style></head><body>${raw}</body></html>`, { waitUntil: 'load' });
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(() => { for (const dialog of document.querySelectorAll('dialog')) if (!dialog.open) dialog.showModal(); });
    if (data.browserHover) await page.locator(data.browserHover).first().hover();
    if (data.browserFocus) await page.locator(data.browserFocus).first().focus();
    const measurement = await page.evaluate((styleProps) => {
      const rect = (element) => { const box = element.getBoundingClientRect(); return { x: box.x, y: box.y, width: box.width, height: box.height }; };
      const shell = document.querySelector('main.app-shell');
      const all = [shell, ...shell.querySelectorAll('*'), ...document.querySelectorAll('body > dialog')].filter(Boolean);
      const elements = all.map((element, index) => {
        const style = getComputedStyle(element);
        return {
          index, tag: element.tagName, id: element.id, cls: element.className?.baseVal ?? element.className,
          directText: [...element.childNodes].filter((node) => node.nodeType === Node.TEXT_NODE).map((node) => node.textContent).join(''),
          attrs: [...element.attributes].map((attr) => [attr.name, attr.value]), rect: rect(element),
          visible: !!(element.getClientRects().length && style.display !== 'none'), focused: element === document.activeElement,
          styles: Object.fromEntries(styleProps.map((key) => [key, style[key]])),
          pseudo: Object.fromEntries(['::before','::after'].map((pseudo) => { const value = getComputedStyle(element, pseudo); return [pseudo, { content: value.content, display: value.display, width: value.width, height: value.height, color: value.color }]; })),
        };
      });
      const keySelectors = ['main.app-shell','.top-bar','.app-layout','.profile-sidebar','.profile-list','.side-nav','.main-view','.server-jump-popover','.profile-switch-loading','.automation-error','dialog.app-exit-backdrop','dialog.profile-dialog-backdrop'];
      const keyGeometry = Object.fromEntries(keySelectors.map((selector) => {
        const found = [...document.querySelectorAll(selector)];
        return [selector, found.map((element) => ({ rect: rect(element), className: element.className, text: element.textContent, styles: Object.fromEntries(styleProps.map((key) => [key, getComputedStyle(element)[key]])) }))];
      }));
      const textRuns = [];
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT); let node;
      while ((node = walker.nextNode())) {
        if (!node.textContent.trim()) continue;
        const range = document.createRange(); range.selectNodeContents(node); const box = range.getBoundingClientRect();
        if (!box.width && !box.height) continue;
        const style = getComputedStyle(node.parentElement);
        textRuns.push({ text: node.textContent, rect: { x: box.x, y: box.y, width: box.width, height: box.height }, color: style.color, fontSize: style.fontSize, fontFamily: style.fontFamily, fontWeight: style.fontWeight });
      }
      const profileList = document.querySelector('.profile-list');
      const ancestry = profileList ? { parent: profileList.parentElement?.tagName, parentClass: profileList.parentElement?.className, grandparentClass: profileList.parentElement?.parentElement?.className } : null;
      const dialog = document.querySelector('dialog.app-exit-backdrop');
      return { viewport: { width: innerWidth, height: innerHeight }, html: document.body.innerHTML, elements, keyGeometry, textRuns, ancestry, exitParent: dialog?.parentElement?.tagName || null, exitAfterShell: dialog ? shell.compareDocumentPosition(dialog) & Node.DOCUMENT_POSITION_FOLLOWING ? true : false : null, focused: document.activeElement?.outerHTML || null };
    }, props);
    const measurementName = `${data.id}-${side}.json`;
    const screenshotName = `${data.id}-${side}.png`;
    const htmlName = `${data.id}-${side}.html`;
    fs.writeFileSync(path.join(out, measurementName), `${JSON.stringify(measurement, null, 2)}\n`);
    fs.writeFileSync(path.join(out, htmlName), raw);
    await page.screenshot({ path: path.join(out, screenshotName), fullPage: true });
    sides[side] = {
      measurement: measurementName,
      measurementSha256: hash(fs.readFileSync(path.join(out, measurementName))),
      screenshot: screenshotName,
      screenshotSha256: hash(fs.readFileSync(path.join(out, screenshotName))),
      html: htmlName,
      htmlSha256: hash(fs.readFileSync(path.join(out, htmlName))),
    };
    await context.close();
  }
  pairs.push({ id: data.id, language: data.language, theme: data.theme, width: data.width, hover: data.browserHover || null, focus: data.browserFocus || null, original: sides.original, current: sides.current, masks: [] });
}
await browser.close();
fs.writeFileSync(path.join(out, 'pairs.json'), `${JSON.stringify({ pairs, consoleIssues, tool: { playwright: pw('playwright/package.json').version, node: process.version }, css: { originalSha256: hash(styles.original), currentSha256: hash(styles.current) }, scope: 'Independent recovered CSS versus current reference.css + styles.css. Source-rendered HTML only; no compatibility overlay and no pixel masks.' }, null, 2)}\n`);
console.log(JSON.stringify({ result: 'SHELL_BROWSER_CAPTURE_OK', pairs: pairs.length, consoleIssues: consoleIssues.length }));
