import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as helpers from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
import { getMapPreviewProvider } from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
const { transformSync } = require('esbuild');
const originalPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const pagePath = 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx';
const read = p => fs.readFileSync(path.join(repo, p), 'utf8');
const source = read(originalPath);
const page = read(pagePath);
const hash = value => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
assert.equal(hash(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
function walk(n, list = []) {
  if (!n || typeof n !== 'object') return list;
  if (n.type) list.push(n);
  for (const v of Object.values(n)) if (Array.isArray(v)) v.forEach(x => walk(x, list)); else if (v && typeof v === 'object') walk(v, list);
  return list;
}
const originalNodes = walk(parse(source, { sourceType: 'module' }));
const pageNodes = walk(parse(page, { sourceType: 'module', plugins: ['jsx'] }));
const decl = (nodes, name) => nodes.find(n => n.type === 'FunctionDeclaration' && n.id?.name === name);
const extract = (text, node) => text.slice(node.start, node.end);
const de = decl(originalNodes, 'de');
const liveSet = originalNodes.find(n => n.type === 'VariableDeclarator' && n.id?.name === 'O');
const originalHelpers = new Function(['k', 'A', 'et'].map(name => extract(source, decl(originalNodes, name))).join('\n') + '\nconst ' + extract(source, liveSet) + ';return {k,A,et};')();
const runtime = { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) };
const h = (type, props, ...children) => ({ type, props: { ...props, children } });
const text = n => n == null || typeof n === 'boolean' ? '' : Array.isArray(n) ? n.map(text).join('') : typeof n === 'object' ? text(n.props?.children) : String(n);
function nodes(n, list = []) { if (Array.isArray(n)) n.forEach(x => nodes(x, list)); else if (n && typeof n === 'object') { list.push(n); nodes(n.props?.children, list); } return list; }
const tableCode = transformSync(extract(page, decl(pageNodes, 'MapTable')), { loader: 'jsx', jsxFactory: 'h' }).code;
const coordinate = new Function(extract(page, decl(pageNodes, 'coordinateText')) + ';return coordinateText;')();
let language, t;
const Table = new Function('helpers','h','useI18n','useMemo','useState','useEffect','coordinateText','SCAN_TYPE_LABEL_KEYS','EMPTY_GAME_TEXTS',
  'const {buildMapColumns,mapNumber,mapResourceStatus,mapRewardCount,mapRewardName,mapTaskLabel,mapTaskSelectable,mapTaskState}=helpers;\n' + tableCode + ';return MapTable;')(
  helpers,h,()=>({language,t}),fn=>fn(),fn=>[typeof fn === 'function' ? fn() : fn,()=>{}],()=>{},coordinate,{truck:'map.truck',railway:'map.allianceTrain'},{},
);
const shape = n => typeof n === 'string' ? {text:n,button:false} : {text:text(n),button:true,disabled:Boolean(n.props.disabled),strong:nodes(n).filter(x=>x.type==='strong').map(text)};
let count = 0;
for (language of ['en','ja']) {
  const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
  t = (key, values = {}) => catalog[key].replace(/\{(\w+)\}/g, (m,k) => String(values[k] ?? m));
  for (const kind of ['truck','railway']) for (const marchUuid of [undefined,'','  ','live-march',' live-march ']) {
    for (const disabled of [false,true]) for (const busy of [false,true]) for (const following of [false,true]) {
      const row = {serverId:321,uuid:'123',marchUuid,x:12,y:34,currentGoods:[]};
      const jumpingKey = following ? '321:live-march' : '';
      const calls = [];
      const Original = new Function('e','k','A','g','d','f','re','E','et',extract(source,de)+';return de;')(
        kind, originalHelpers.k, originalHelpers.A, t, jumpingKey, disabled || busy, r=>calls.push(r), runtime, originalHelpers.et,
      );
      const expected = Original(row);
      const tree = Table({kind,rows:[row],sorts:[],actionBusy:busy,actionDisabled:disabled,liveTargetDisabled:false,jumpingKey,onCoordinateJump:r=>calls.push(r),selectedKeys:new Set()});
      const cell = nodes(tree).filter(x=>x.type==='td')[kind==='truck'?1:0];
      const button = nodes(cell).find(x=>x.type==='button');
      const actual = button || text(cell);
      assert.deepEqual(shape(actual),shape(expected),`${language}/${kind}/${String(marchUuid)}/${disabled}/${busy}/${following}`);
      if (button && !disabled && !busy) { expected.props.onClick(); button.props.onClick(); assert.deepEqual(calls,[row,row]); }
      count++;
    }
  }
}
// Presentation parity can be checked with an available callback, while the
// canonical page's absent native tracking provider remains explicitly fenced.
const fenced = Table({kind:'truck',rows:[{serverId:321,uuid:'123',marchUuid:'live'}],sorts:[],actionBusy:false,actionDisabled:false,selectedKeys:new Set()});
assert.equal(nodes(fenced).find(x=>x.type==='button' && x.props.className==='map-coordinate-button').props.disabled,true);
const tableCall = pageNodes.find(n => n.type==='JSXOpeningElement' && n.name.name==='MapTable');
const keyExpression = tableCall.attributes.find(n=>n.name?.name==='jumpingKey').value.expression;
const key = new Function('previewFixture','previewState','previewJumpingKeys','tab','actionBusy','return '+extract(page,keyExpression));
assert.equal(key(false,'map-row-actions',{truck:'synthetic'},'truck',''),'');
assert.equal(key(true,'map-table-states',{truck:'synthetic'},'truck',''),'');
assert.equal(key(true,'map-row-actions',{truck:'synthetic'},'truck',''),'synthetic');
assert.equal(key(false,'',{truck:'synthetic'},'city','jump:321:1:2'),'321:1:2');
for (const mode of ['native','native-unavailable']) assert.equal(getMapPreviewProvider(mode,'map-row-actions'),null);
const provider = getMapPreviewProvider('preview','map-row-actions');
assert.equal(provider.online,false);
const rows = await provider.mapApi.search('truck',{serverId:321,page:1,pageSize:50,sorts:[{sortBy:'updatedAt',sortOrder:'desc'}]});
assert.equal(rows.rows.length,4);
assert.equal(rows.rows[2].marchUuid,'fixture-following');
for (const action of ['start','stop','clear','jumpServer','coordinateJump','setPlayerMark','exportCities']) await assert.rejects(provider.mapApi[action](),{code:'PREVIEW_NATIVE_ACTION_BLOCKED'});
const result = {status:'PASS',actualOriginalCurrentComparisons:count,source:{path:originalPath,sha256:hash(source),de:{byte:Buffer.byteLength(source.slice(0,de.start)),expression:extract(source,de)}},current:{path:pagePath,sha256:hash(page)},nativeTrackingUnavailableFence:true,previewIsolation:true,blockedNativeActions:7};
fs.writeFileSync(path.join(here,'row-action-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_MAP_ROW_ACTIONS_OK ${count} comparisons; preview/native fences PASS`);
