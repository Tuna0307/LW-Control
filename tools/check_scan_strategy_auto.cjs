/* R7-067 browser regression: Map Data exposes no Normal/Fast user control. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {chromium} = require('playwright');

const root = path.resolve(__dirname, '..');
const candidate = path.join(root, 'src/LWBridge.Desktop/WebUi');
const assets = path.join(candidate, 'assets');

async function main() {
  const server = http.createServer((req,res) => {
    const url = new URL(req.url, 'http://localhost');
    const rel = url.pathname.slice(1) || 'index.html';
    try {
      const file = path.resolve(candidate, rel);
      if (!file.startsWith(candidate + path.sep) && file !== path.join(candidate, 'index.html'))
        throw new Error('outside root');
      const data = fs.readFileSync(file);
      res.setHeader('Content-Type',
        rel.endsWith('.js') ? 'text/javascript' :
        rel.endsWith('.css') ? 'text/css' : 'text/html');
      res.end(data);
    } catch {
      res.writeHead(404);
      res.end();
    }
  }).listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({channel: process.env.LWBRIDGE_BROWSER || 'msedge', headless: true});
  const page = await browser.newPage({viewport:{width:1280,height:900}});
  const errors = [];
  page.on('pageerror', error => errors.push(error.stack || String(error)));
  try {
    await page.goto(`${origin}/index.html?view=map-data&language=en`);
    await page.locator('.panel.map-panel').waitFor();
    await page.waitForTimeout(300);
    assert.deepEqual(errors, [], 'Map Data must load without JavaScript errors');

    assert.equal(await page.locator('.map-speed-toggle').count(), 0,
      'Manual Scan must not expose the retired speed toggle');
    assert.equal(await page.locator('input[name="map-scan-speed"]').count(), 0,
      'Manual Scan must not expose Normal/Fast radio inputs');
    assert.equal(await page.getByRole('button', {name:'Start Scan'}).count() > 0, true,
      'Manual Scan must retain Start Scan');

    await page.getByRole('tab', {name:'Auto Scan'}).click();
    await page.locator('.map-auto-scan-card').waitFor();
    assert.equal(await page.locator('.map-auto-scan-card select').count(), 0,
      'Auto Scan must not expose a speed selector');
    assert.equal(await page.getByText('Speed', {exact:true}).count(), 0,
      'Auto Scan must not expose a Speed label');

    const panel = fs.readFileSync(path.join(assets,'MapDataPanel-C1HVeNHr.js'),'utf8');
    const index = fs.readFileSync(path.join(assets,'index-sfL2sT3K.js'),'utf8');
    for (const token of ['lwbridge.mapScanMode','map-speed-toggle','map-scan-speed','scanMode:P','value:S.scanMode']) {
      assert.equal(panel.includes(token), false, `Map Data bundle must not contain retired token ${token}`);
    }
    for (const token of ['scanMode:i.scanMode','e?.scanMode','scanMode:\`fast\`']) {
      assert.equal(index.includes(token), false, `Auto Scan bundle must not persist/emit retired token ${token}`);
    }
    assert.equal(panel.includes('v(await ae({selectedTypes:e}))'), true,
      'Manual Start must send selectedTypes without scanMode');
    assert.equal(index.includes('Mt(await Te({selectedTypes:i.selectedTypes,resume:!1}))'), true,
      'Auto scheduler must start scans without scanMode');

    const localeNames = fs.readdirSync(assets).filter(name =>
      /^(en|id|ja|ko|pt|ru|vi|zh-CN|zh-TW)-.*\.js$/.test(name));
    assert.equal(localeNames.length, 9, 'all nine shipped locale bundles must be checked');
    for (const name of localeNames) {
      const locale = fs.readFileSync(path.join(assets,name),'utf8');
      for (const key of ['"map.speed":','"map.normalSpeed":','"map.fastSpeed":'])
        assert.equal(locale.includes(key), false, `${name} must not ship retired speed string ${key}`);
    }

    console.log('Automatic scan strategy UI browser check passed.');
  } finally {
    await page.close();
    await browser.close();
    server.close();
  }
}
main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
