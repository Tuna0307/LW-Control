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

    // R7-070: pin the shipped post-R7-067 Auto Scan configuration surface.
    const card = page.locator('.map-auto-scan-card');
    const master = card.locator('.map-auto-scan-master input[type="checkbox"]');
    const serverInput = card.locator('.map-auto-scan-server-input input');
    const intervalInput = card.locator('input[type="number"]');
    const returnToggle = card.locator('.map-auto-scan-options input[type="checkbox"]');
    assert.equal(await master.count(), 1, 'Auto Scan must expose one durable enable toggle');
    assert.equal(await serverInput.count(), 1, 'Auto Scan must expose target-server entry');
    assert.equal(await intervalInput.count(), 1, 'Auto Scan must expose one interval input');
    assert.equal(await intervalInput.getAttribute('min'), '20', 'Auto interval minimum must remain 20 minutes');
    assert.equal(await intervalInput.getAttribute('max'), '1440', 'Auto interval maximum must remain 1440 minutes');
    assert.equal(await returnToggle.count(), 1, 'Auto Scan must expose return-to-original option');
    assert.equal(await card.locator('.map-auto-scan-options button.primary').count(), 1,
      'Auto Scan must retain its Run Now primary action');

    const before = Date.now();
    await master.check();
    await page.waitForTimeout(20);
    const autoKey = await page.evaluate(() =>
      Object.keys(localStorage).find(key => key.startsWith('lwbridge.mapAutoScan.')) || '');
    assert.notEqual(autoKey, '', 'enabling Auto Scan must create per-profile durable config');
    let persisted = JSON.parse(await page.evaluate(key => localStorage.getItem(key), autoKey));
    assert.equal(persisted.enabled, true, 'enabled state must persist');
    assert.equal(persisted.nextRunAt >= before, true,
      'enabling Auto Scan must schedule an immediate eligible run');

    await serverInput.fill('2212');
    await card.getByRole('button', {name:'Add'}).click();
    await intervalInput.fill('20');
    await intervalInput.dispatchEvent('change');
    await returnToggle.uncheck();
    await page.waitForTimeout(20);
    persisted = JSON.parse(await page.evaluate(key => localStorage.getItem(key), autoKey));
    assert.deepEqual(persisted.serverIds, [2212], 'target server list must persist');
    assert.equal(persisted.intervalMinutes, 20, 'scan interval must persist');
    assert.equal(persisted.returnToOriginalServer, false, 'return-to-original preference must persist');

    await master.uncheck();
    await page.waitForTimeout(20);
    persisted = JSON.parse(await page.evaluate(key => localStorage.getItem(key), autoKey));
    assert.equal(persisted.enabled, false, 'disabling Auto Scan must persist');
    assert.equal(persisted.nextRunAt, 0, 'disabling Auto Scan must clear the scheduled deadline');

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
    for (const token of [
      'n.enabled&&!t.enabled&&(n.nextRunAt=Date.now()),n.enabled||(n.nextRunAt=0)',
      'function Zn(e,t,n,r,i){return e.enabled&&n&&!r&&!i&&t>=e.nextRunAt}',
      'for(let t of n){if(e||!Je.current.enabled||!autoOnlineRef.current)break',
      'try{let n=await Se(t);if(e||!Je.current.enabled||!autoOnlineRef.current)break;F(n.changed?',
      'if(!e&&i.returnToOriginalServer&&a>0',
      'let e=Xn(Je.current,Date.now());Je.current=e,We(e),$n(n,e)',
      'window.setInterval(()=>{i()},5e3)',
      'lwbridge.mapAutoScanCycle.${e}',
      'function readAutoCycleMarker(e)',
      'async function autoRestartRecovery()',
      'if(await autoRestartRecovery())return',
      'writeAutoCycleMarker(u.selectedProfileId,{schemaVersion:1,startedAt:Date.now(),originalServerId:a,returnToOriginalServer:i.returnToOriginalServer})',
      '&&writeAutoCycleMarker(n,null)',
      'h.has(`map-data`)&&(0,M.jsx)(j.Activity,{mode:p===`map-data`?`visible`:`hidden`',
    ]) {
      assert.equal(index.includes(token), true,
        `current top-level Auto scheduler must retain ${token}`);
    }

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
