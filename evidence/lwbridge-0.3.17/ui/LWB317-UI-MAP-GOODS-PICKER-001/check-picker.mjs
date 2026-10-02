import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createOriginalHarness, optionsReply } from '../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs';
import { createHarness, treeNodes, repo } from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.join(repo, 'src/LWBridge.UI-0.3.17/src');
const require = createRequire(path.join(src, '../package.json'));
const { transformSync } = require('esbuild');
const source = fs.readFileSync(path.join(src, 'MapRetainedGoodsFilter.jsx'), 'utf8');
const code = transformSync(source.replace(/^import .*;\r?$/gm, '').replace('export function', 'function'), { loader: 'jsx', jsxFactory: 'h' }).code;
const h = (type, attrs, ...children) => ({ type, props: { ...(attrs || {}), children } });
const compiled = new Function('useRef', 'h', code + '\nreturn { MapRetainedGoodsFilter, MapFilterAssetPlaceholder };')(() => ({ current: null }), h);
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const items = [
  { key: 2, name: 'Numeric key', iconPath: 'fixture/icon-2' },
  { key: '2', name: 'String key', iconPath: '' },
  { key: 0, name: '', iconPath: null },
  { key: 'null', name: null },
  { key: 'missing', iconPath: 'fixture/missing' },
  { key: '東京/%', name: '東京 & %', iconPath: 'fixture/unavailable' },
];
function shape(node) {
  if (Array.isArray(node)) return node.filter(n => n != null && typeof n !== 'boolean').map(shape).flat();
  if (node == null || typeof node === 'boolean') return [];
  if (typeof node !== 'object') return String(node);
  if (typeof node.type === 'function' && 'assetPath' in node.props) return { type: 'game-asset-slot', props: Object.fromEntries(['assetPath','alt','className'].map(k => [k,node.props[k]])) };
  const children = shape(node.props.children);
  return { type: node.type, attrs: Object.fromEntries(['className','type','aria-label','title'].filter(k => node.props[k] !== undefined).map(k => [k,node.props[k]])), children: Array.isArray(children) ? children : [children] };
}
const comparisons = [];
for (const language of ['en','ja','zh-CN','zh-TW','ko','pt','id','vi','ru']) {
  const messages = (await import(pathToFileURL(path.join(src, 'locales/' + language + '.js')).href)).default;
  const t = key => messages[key] || key;
  const o = await createOriginalHarness({ online: false, initialTab: 'truck', tabMode: 'controlled', translate: t,
    stubs: { dataOptions: { mode: 'auto', value: optionsReply({ rewardItems: { truck: items, railway: items } }) } } });
  await o.mount();
  const originalComponent = o.findNodes(n => typeof n.type === 'function' && n.type.name === 'ct')[0]?.type;
  assert.ok(originalComponent);
  for (const value of ['',2,'2',0,'null','missing','absent','東京/%']) for (const choices of [[],items]) {
    const originalTrace = [], currentTrace = [];
    const props = { items: choices, value, label: t('map.itemFilter'), allLabel: t('map.allRetainedGoods') };
    const original = originalComponent({ ...props, onChange: key => originalTrace.push(['change',key]) });
    const current = compiled.MapRetainedGoodsFilter({ ...props, onChange: key => currentTrace.push(['change',key]) });
    assert.deepEqual(shape(current), shape(original), `${language}/${JSON.stringify(value)}/${choices.length}`);
    original.props.ref.current = { removeAttribute: attr => originalTrace.push(['remove',attr]) };
    current.props.ref.current = { removeAttribute: attr => currentTrace.push(['remove',attr]) };
    const ob = treeNodes(original).filter(n => n.type === 'button'), cb = treeNodes(current).filter(n => n.type === 'button');
    for (let index=0; index<cb.length; index++) { ob[index].props.onClick(); cb[index].props.onClick(); assert.deepEqual(currentTrace,originalTrace); }
    comparisons.push({ language,value,items:choices.length,buttons:cb.length,pass:true });
  }
  // Source callback rejection must not close the menu. A missing DOM ref is safe.
  for (const component of [originalComponent, compiled.MapRetainedGoodsFilter]) {
    const error = new Error('controlled callback rejection'); let closed=false;
    const tree = component({ items, value:'', label:'label', allLabel:'all', onChange:()=>{throw error;} });
    tree.props.ref.current = { removeAttribute:()=>{closed=true;} };
    assert.throws(()=>treeNodes(tree).find(n=>n.type==='button').props.onClick(),e=>e===error); assert.equal(closed,false);
    const noRef = component({items:[],value:'',label:'label',allLabel:'all',onChange:()=>{}});
    noRef.props.ref.current=null; treeNodes(noRef).find(n=>n.type==='button').props.onClick();
  }
  await o.unmount();
}

// Compare actual missing-image rendering with the original asset component.
// useEffect is intentionally inert: no native image loading or timing claim.
const assetPath = path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js');
const asset = fs.readFileSync(assetPath,'utf8');
assert.equal(hash(asset),'2f92a87c3268497df6425b1175db6140e10aabcbdfae02615f065d00e16458e0');
const hooks = { useRef:()=>({current:null}), useState:v=>[v,()=>{}], useEffect:()=>{} };
const originalAsset = new Function('e','t','r','n',asset.replace(/^import\{[^}]+\}from"[^"]+";/,'').replace(/export\{x as t\};?\s*$/,'')+'\nreturn x;')(
  ()=>({jsx:(type,props)=>({type,props})}), v=>v, ()=>hooks, ()=>{throw new Error('native loader must not run');});
for (const item of items) assert.deepEqual(shape(compiled.MapFilterAssetPlaceholder({alt:item.name,className:'map-item-filter-icon'})),shape(originalAsset({assetPath:item.iconPath,alt:item.name,className:'map-item-filter-icon'})));

// Actual original/current parent callbacks with equivalent provider inputs.
const page = fs.readFileSync(path.join(src,'MapDataPage.jsx'),'utf8');
async function parentScenario(original) {
  const options = optionsReply({rewardItems:{truck:items,railway:items}});
  const p = original
    ? await createOriginalHarness({online:false,initialTab:'truck',tabMode:'controlled',stubs:{dataOptions:{mode:'auto',value:options}}})
    : await createHarness(page,'goods-parent',{props:{previewState:'map-truck'},dataOptions:options(321)});
  await p.mount();
  const child=()=>p.findNodes(n=>typeof n.type==='function' && n.type.name===(original?'ct':'MapRetainedGoodsFilter'))[0];
  const table=()=>p.findNodes(n=>typeof n.type==='function' && typeof n.props?.onSort==='function')[0];
  const trace=[];
  const choose=async(key)=>{ child().props.onChange(key); await p.settle(); const q=p.requests.at(-1).query;trace.push({kind:p.requests.at(-1).kind,page:q.page,itemKey:q.itemKey,sorts:q.sorts}); };
  await p.setPage(3); await choose(2); assert.equal(p.requests.at(-1).query.page,1);
  table().props.onSort('itemCount'); await p.settle();
  await choose('2'); await choose(''); assert.ok(!p.requests.at(-1).query.sorts.some(s=>s.sortBy==='itemCount'));
  await choose('東京/%');
  await p.clickTab('railway'); await choose('2');
  table().props.onSort('itemCount'); await p.settle();
  await p.clickTab('truck'); assert.equal(child().props.value,'東京/%'); await choose('');
  await p.clickTab('railway'); assert.equal(child().props.value,'2'); await choose('2');
  assert.ok(p.requests.at(-1).query.sorts.some(s=>s.sortBy==='itemCount'));
  await p.unmount(); return trace;
}
const originalParent=await parentScenario(true), currentParent=await parentScenario(false);
assert.deepEqual(currentParent,originalParent);
const baseline=execFileSync('git',['show','fbde5d6:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'],{cwd:repo,encoding:'utf8'});
const old=await createHarness(baseline,'goods-baseline',{props:{previewState:'map-truck'},dataOptions:optionsReply({rewardItems:{truck:items,railway:items}})(321)});
await old.mount(); assert.ok(old.findNodes(n=>n.type==='select' && n.props['aria-label']==='map.itemFilter').length); await old.unmount();
const result={comparisons:comparisons.length,cases:comparisons,parentOriginal:originalParent,parentCurrent:currentParent,parent:'PASS',placeholderComparisons:items.length,callbackExceptions:'PASS',baseline:'native select mismatch confirmed',sourceSha256:hash(source.replace(/\r\n/g,'\n')),pageSha256:hash(page.replace(/\r\n/g,'\n'))};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'picker-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_MAP_GOODS_PICKER_OK comparisons=${comparisons.length} parent=PASS placeholders=${items.length} baselineMismatch=YES`);
