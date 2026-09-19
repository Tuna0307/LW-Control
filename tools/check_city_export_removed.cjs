/* Owner override regression: City Excel export must not be reachable in shipped Map Data. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {chromium} = require('playwright');

const root = path.resolve(__dirname, '..');
const candidate = path.join(root, 'src/LWBridge.Desktop/WebUi');

async function main() {
  const server=http.createServer((req,res)=>{
    const url=new URL(req.url,'http://localhost');
    const rel=url.pathname.slice(1)||'index.html';
    try {
      const file=path.resolve(candidate,rel);
      if(!file.startsWith(candidate+path.sep)&&file!==path.join(candidate,'index.html')) throw new Error('outside root');
      const data=fs.readFileSync(file);
      res.setHeader('Content-Type',rel.endsWith('.js')?'text/javascript':rel.endsWith('.css')?'text/css':'text/html');
      res.end(data);
    } catch {res.writeHead(404);res.end();}
  }).listen(0,'127.0.0.1');
  await new Promise(r=>server.once('listening',r));
  const origin=`http://127.0.0.1:${server.address().port}`;
  const browser=await chromium.launch({channel:process.env.LWBRIDGE_BROWSER||'msedge',headless:true});
  const page=await browser.newPage({viewport:{width:1280,height:900}});
  const errors=[];
  page.on('pageerror',error=>errors.push(error.stack||String(error)));
  try {
    await page.goto(`${origin}/index.html?view=map-data&language=en`);
    await page.locator('.panel.map-panel').waitFor();
    await page.waitForTimeout(300);
    assert.deepEqual(errors,[],'generated Map Data must load without JavaScript errors');
    assert.equal(await page.getByRole('button',{name:'Export Excel'}).count(),0,'City Export Excel button must be absent');
    assert.equal(await page.getByText(/Exporting\.|Exported .* cities to/).count(),0,'City export status UI must be absent');
    const commands=await page.evaluate(()=>window.LWBridgePreview.calls);
    assert.equal(commands.includes('map_city_export'),false,'Map Data must never invoke retired map_city_export');
    const api=fs.readFileSync(path.join(candidate,'assets','api-ClPPi2JT.js'),'utf8');
    const panel=fs.readFileSync(path.join(candidate,'assets','MapDataPanel-C1HVeNHr.js'),'utf8');
    assert.equal(api.includes('map_city_export'),false,'shipped API bundle must not contain map_city_export');
    assert.equal(panel.includes('map.exportExcel'),false,'shipped Map Data bundle must not contain export UI translation keys');
    for(const name of fs.readdirSync(path.join(candidate,'assets')).filter(name=>/^(en|id|ja|ko|pt|ru|vi|zh-CN|zh-TW)-.*\.js$/.test(name))) {
      const locale=fs.readFileSync(path.join(candidate,'assets',name),'utf8');
      assert.equal(locale.includes('"map.exportExcel":'),false,`${name} must not ship City export translations`);
    }
    console.log('City Excel export removal browser check passed.');
  } finally {
    await page.close();
    await browser.close();
    server.close();
  }
}
main().catch(error=>{console.error(error);process.exitCode=1;});
