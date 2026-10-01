import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as current from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
import { getMapPreviewProvider } from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser'), { transformSync } = require('esbuild');
const hash = value => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const sourcePath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const mainPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js';
const rewardPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/rewardDisplay-eZWrd6iS.js';
const source = fs.readFileSync(path.join(repo, sourcePath), 'utf8');
const main = fs.readFileSync(path.join(repo, mainPath), 'utf8');
const rewards = fs.readFileSync(path.join(repo, rewardPath), 'utf8');
assert.equal(hash(source), 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089');
assert.equal(hash(main), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
function walk(node, output = []) {
  if (!node || typeof node !== 'object') return output;
  if (node.type) output.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(child => walk(child, output));
    else if (value && typeof value === 'object') walk(value, output);
  }
  return output;
}
const ast = parse(source, { sourceType: 'module' });
const mainAst = parse(main, { sourceType: 'module' });
function declaration(ast, name) {
  const node = ast.program.body.map(n => n.declaration || n).find(n => n.type === 'FunctionDeclaration' && n.id.name === name);
  assert.ok(node, name); return node;
}
const helperNames = ['k', 'A', 'j', 'M', 'De', 'Oe', 'ke', 'w', 'T', 'Ke', 'qe', 'Je', 'N', 'P', 'F', 'Qe', '$e', 'I', 'L', 'et', 'nt'];
const helperNodes = helperNames.map(name => declaration(ast, name));
const typeMap = ast.program.body.filter(n => n.type === 'VariableDeclaration').flatMap(n => n.declarations).find(n => n.id.name === 'Ee');
const liveKinds = ast.program.body.filter(n => n.type === 'VariableDeclaration').flatMap(n => n.declarations).find(n => n.id.name === 'O');
assert.equal(source.slice(liveKinds.start, liveKinds.end), 'O=new Set([`truck`,`railway`])');
const truckNodes = ['Fe', 'Ie'].map(name => declaration(mainAst, name));
const rewardNode = declaration(parse(rewards, { sourceType: 'module' }), 'e');
const tableNode = ast.program.body.filter(n => n.type === 'VariableDeclaration').flatMap(n => n.declarations).find(n => n.id.name === 'at');
const tableFunctions = Object.fromEntries(walk(tableNode).filter(n => n.type === 'FunctionDeclaration' && ['me', 'pe', 'fe'].includes(n.id.name)).map(n => [n.id.name, source.slice(n.start, n.end)]));
const tableWidths = walk(tableNode).find(n => n.type === 'VariableDeclarator' && n.id.name === 'le');
const originalWidths = new Function('ce','return ('+source.slice(tableWidths.init.start,tableWidths.init.end)+');');
const now = 1_799_000_000_000;
const RealDate = globalThis.Date;
class FixedDate extends RealDate { constructor(...args) { super(...(args.length ? args : [now])); } static now() { return now; } }
const original = new Function('Date', helperNodes.map(n => source.slice(n.start, n.end)).join('\n')
  + '\n' + truckNodes.map(n => main.slice(n.start, n.end)).join('\n')
  + '\nconst l=Fe,r=Ie;const ' + source.slice(liveKinds.start, liveKinds.end) + ';const ' + source.slice(typeMap.start, typeMap.end)
  + ';return {columns:nt,taskState:F,resource:qe,quality:L,duration:Je,number:I,timestamp:P,rewardName:M,truckState:Ie,truckMaximum:Fe};')(FixedDate);
const compact = new Function(rewards.slice(rewardNode.start, rewardNode.end) + ';return e;')();
const jsx = { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) };
const h = (type, props, ...children) => ({ type, props: { ...props, ...(children.length ? { children } : {}) } });
function text(tree) {
  if (tree == null || typeof tree === 'boolean') return '';
  if (Array.isArray(tree)) return tree.map(text).join('');
  return typeof tree === 'object' ? text(tree.props.children) : String(tree);
}
function nodes(tree, out = []) {
  if (Array.isArray(tree)) tree.forEach(n => nodes(n, out));
  else if (tree && typeof tree === 'object') { out.push(tree); nodes(tree.props.children, out); }
  return out;
}
const metadata = columns => columns.map(column => Object.fromEntries(Object.entries(column).filter(([, value]) => typeof value !== 'function' && value !== undefined)));
const pages = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'), 'utf8').replace(/\r\n/g, '\n');
const currentAst = parse(pages, { sourceType: 'module', plugins: ['jsx'] });
const currentTable = declaration(currentAst, 'MapTable');
const currentFilter = walk(currentAst).find(n => n.type === 'FunctionDeclaration' && n.id.name === 'changeItemFilter');
const originalFilterProps = walk(ast).find(n => n.type === 'ObjectExpression' && n.properties.some(p => p.key?.name === 'label' && source.slice(p.value.start,p.value.end) === 'S(`map.itemFilter`)'));
const originalFilter = originalFilterProps.properties.find(p => p.key?.name === 'onChange').value;
const currentTableCode = transformSync(pages.slice(currentTable.start, currentTable.end), { loader: 'jsx', jsxFactory: 'h' }).code;
const coordinates = declaration(currentAst, 'coordinateText');
const coordinateText = new Function(pages.slice(coordinates.start, coordinates.end) + ';return coordinateText;')();
const scanTypes = ast.program.body.filter(n => n.type === 'VariableDeclaration').flatMap(n => n.declarations).find(n => n.id.name === 'Ie');
const tabKeys = Object.fromEntries(new Function('return ('+source.slice(scanTypes.init.start,scanTypes.init.end)+');')().map(({key,label})=>[key,label]));
const baseline = execFileSync('git', ['show', '4e29806db72c83e37c0ba96f82270c66b5a4cbba:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'], { cwd: repo, encoding: 'utf8' });
const baselineAst = parse(baseline, { sourceType: 'module', plugins: ['jsx'] });
const oldNames = ['rowColumns','dateText','numberText','uiLocale','qualityText','resourceStatus','taskStatusText','truckPlunderText'];
const oldColumns = new Function('Date', 'document', oldNames.map(name => { const n = declaration(baselineAst,name);return baseline.slice(n.start,n.end); }).join('\n') + ';return {columns:rowColumns,taskStatus:taskStatusText};')(FixedDate, { documentElement: { lang: 'en' } });
const base = { serverId:321, uuid:'12345', x:12, y:34, updatedAt:now-1000, ownerName:'Fixture owner', allianceName:'Fixture alliance',
  level:12, health:99999, protectEndTime:now+60000, resourceNameKey:'resource-key', monsterNameKey:'monster-key', quality:6,
  power:1000000, robTimes:1, maxLootCount:3, arriveTs:now+100000, protectTime:now+3000, completionTime:now-1000,
  taskExpireTime:now+10000, plunderAt:now-500, stolenCount:0, maxStealCount:2, treasureType:1, suppliesType:0,
  remainingBoxes:3, rewardedCount:1, diggingCount:2, worldClaimState:'charging', chargePercent:0.375, playerClaimState:'unclaimed', expireTime:now+10000 };
const variants = [base, {}, { ...base, protectEndTime: now-1000, quality:0, worldClaimState:'claimable',playerClaimState:'claimed',claimBlockReason:'other_alliance' },
  { ...base, gatherUid:' 0 ', worldClaimState:'',playerClaimState:'',suppliesType:1 },
  { ...base, gatherMarchUuid:'123',worldClaimState:'depleted',playerClaimState:'failed',claimBlockReason:'no_squad',suppliesType:3 },
  { ...base, isSpecialURQuality:true, robTimes:1,worldClaimState:'expired',playerClaimState:'digging',suppliesType:4 },
  { ...base, quality:2, level:null, health:'bad', updatedAt:1_799_000_000, expireTime:1_799_000_100,treasureNameKey:'challenge_zombie_box_title',worldClaimState:'verifying',playerClaimState:'verifying' },
  { ...base, resourceNameKey:'unknown-key',monsterNameKey:'unknown-key',treasureNameKey:'missing',worldClaimState:'new-state',playerClaimState:'new-state' }];
const texts = { 'resource-key':'Fixture localized resource', 'monster-key':'Fixture localized monster', '300039':'Fixture occupied', '372138':'Fixture idle' };
const gaps = [], cases = [], clockChecks = [];
let columnCases = 0, valueComparisons = 0, selectCases = 0, taskCases = 0, treasureCases = 0, filterCases = 0;
globalThis.Date = FixedDate;
try {
  for (const language of ['en','zh-CN','zh-TW','ja','ko','vi','id','ru','pt']) {
    const catalog = (await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
    const t = (key, values={}) => { assert.ok(key in catalog, `catalog ${language}: ${key}`); return catalog[key].replace(/\{(\w+)\}/g,(m,k)=>String(values[k]??m)); };
    for (const kind of Object.keys(tabKeys)) for (const itemKey of ['', 'item-a']) for (const refreshing of [false,true]) {
      const expected=original.columns(kind,t,language,texts,itemKey,refreshing),actual=current.buildMapColumns(kind,t,language,texts,itemKey,refreshing);
      assert.deepEqual(metadata(actual),metadata(expected));columnCases++;
      for (const row of variants) for (let index=0; index<expected.length;index++) if(expected[index].value) {
        assert.equal(actual[index].value(row),expected[index].value(row),`${language}/${kind}/${index}`);valueComparisons++;
      }
      if(language==='en'&&!itemKey&&!refreshing){
        const old=oldColumns.columns(kind,t);
        for(let index=0;index<expected.length;index++){
          if(expected[index].value&&old[index]?.value){const before=old[index].value(base),after=expected[index].value(base);if(before!==after)gaps.push({kind,column:expected[index].label,before,sourceExpected:after});}
        }
      }
    }
    const originalSelect = kind => new Function('e','o','c','l','g','E','h','ne','F','P','k','r',tableFunctions.me+';return me;')(
      kind,now,new Set(),new Set(),t,jsx,()=>{},()=>{},original.taskState,original.timestamp,(row,key)=>row[key],original.truckState);
    const originalStatus = new Function('o','g','E','F',tableFunctions.pe+';return pe;')(now,t,jsx,original.taskState);
    const originalRewards = new Function('k','a','i','oe','M','I','_','E','v',tableFunctions.fe+';return fe;')((row,key)=>row[key],'item-a',texts,language,original.rewardName,original.number,compact,jsx,'RecoveredAsset');
    const taskVariants=[{...base,taskExpireTime:now-1},{...base,stolenCount:2},{...base,completionTime:now+1},{...base,plunderAt:now+1},base,{...base,uuid:'bad'},{...base,completionTime:0},{...base,plunderAt:0},{...base,arriveTs:now-1}];
    for(const kind of ['truck','dispatch','ghost'])for(const row of taskVariants){assert.equal(!current.mapTaskSelectable(kind,row,now),originalSelect(kind)(row).props.disabled);selectCases++;}
    for(const row of taskVariants){assert.equal(current.mapTaskLabel(current.mapTaskState(row,now),t),text(originalStatus(row)));taskCases++;}
    for(const type of Array.from({length:25},(_,index)=>index))for(const suppliesType of [0,1,3,4])for(const nameKey of ['', 'challenge_zombie_box_title','missing-key']){
      const row={...base,treasureType:type,suppliesType,treasureNameKey:nameKey};
      assert.equal(current.buildMapColumns('treasure',t,language,texts,'',false)[1].value(row),original.columns('treasure',t,language,texts,'',false)[1].value(row));treasureCases++;
    }
    for(const state of ['','unclaimed','dispatching','scouting','digging','claiming','claimed','failed','verifying','other'])for(const reason of ['', 'other_alliance','no_scout','no_squad','squad_reserved']){
      const row={...base,playerClaimState:state,claimBlockReason:reason};
      assert.equal(current.buildMapColumns('treasure',t,language,texts,'',false)[4].value(row),original.columns('treasure',t,language,texts,'',false)[4].value(row));treasureCases++;
    }
    const effects=[],cleared=[];
    const table = new Function('helpers','h','useI18n','useMemo','useState','useEffect','coordinateText','SCAN_TYPE_LABEL_KEYS','EMPTY_GAME_TEXTS','window',
      'const {buildMapColumns,mapNumber,mapResourceStatus,mapRewardCount,mapRewardName,mapTaskLabel,mapTaskSelectable,mapTaskState}=helpers;\n'+currentTableCode+';return MapTable;')(
        current,h,()=>({language,t}),fn=>fn(),fn=>[typeof fn==='function'?fn():fn,()=>{}],fn=>effects.push(fn),coordinateText,tabKeys,{},
        {setInterval:(_,ms)=>{assert.equal(ms,1000);return 17;},clearInterval:id=>cleared.push(id)});
    const row={...base,currentGoods:[{key:'item-b',name:'Fixture B',count:1250},{key:'item-a',name:'Fixture A',count:2500000}],rewards:[{key:'reward',name:'Fixture reward',count:1000}]};
    for(const kind of Object.keys(tabKeys)){
      const tree=table({kind,rows:[row],loading:false,sorts:[],onSort:()=>{},onCoordinateJump:()=>{},onPlayerMark:()=>{},actionBusy:false,actionDisabled:true,selectedKeys:new Set(),onSelect:()=>{},gameTexts:texts,itemKey:'item-a'});
      const flat=nodes(tree),cols=flat.filter(n=>n.type==='col'),th=flat.filter(n=>n.type==='th'),td=flat.filter(n=>n.type==='td');
      const metadataColumns=original.columns(kind,t,language,texts,'item-a',false);
      assert.equal(cols.length,metadataColumns.length);assert.deepEqual(cols.map(n=>n.props.style.width),originalWidths(metadataColumns));
      assert.equal(flat.find(n=>n.type==='table').props['aria-label'],t(tabKeys[kind]));
      assert.deepEqual(th.map(n=>text(n)),metadataColumns.map(c=>c.label));
      assert.ok(flat.filter(n=>n.type==='button'&&['map-coordinate-button','map-schedule-button'].includes(n.props.className)).every(n=>n.props.disabled));
      if(['dispatch','ghost'].includes(kind))assert.equal(text(flat.find(n=>n.props.className==='map-task-status ready')),text(originalStatus(row)));
      const rewardItems=flat.filter(n=>n.props.className==='map-reward-item');
      const rewardColumn=metadataColumns.find(c=>c.rewards);
      if(rewardColumn){const expectedItems=nodes(originalRewards(row,rewardColumn.rewards)).filter(n=>n.props.className==='map-reward-item');assert.deepEqual(rewardItems.map(n=>({title:n.props.title,label:n.props['aria-label'],count:text(nodes(n).find(c=>c.type==='strong'))})),expectedItems.map(n=>({title:n.props.title,label:n.props['aria-label'],count:text(nodes(n).find(c=>c.type==='strong'))})));}
      if(rewardItems.length)assert.ok(rewardItems.every(n=>n.props.title===n.props['aria-label']));
      cases.push({language,kind,columns:th.length,rewards:rewardItems.length,tableWidth:flat.find(n=>n.type==='table').props.style.minWidth});
      const effect=effects.pop();const cleanup=effect();if(['truck','dispatch','ghost'].includes(kind)){assert.equal(typeof cleanup,'function');cleanup();assert.equal(cleared.pop(),17);clockChecks.push({language,kind,milliseconds:1000,cleanup:true});}else assert.equal(cleanup,undefined);
    }
  }
  for(const value of [undefined,null,0,-1,999,1000,1250,1e6,1e9,-1234,'bad'])assert.equal(current.mapRewardCount(value),compact(value));
  assert.equal(current.mapResourceStatus({rebuildGatherOccupancyKnown:false},key=>key,{}),'—');
  assert.equal(current.resourceOccupancy({rebuildGatherOccupancyKnown:true,rebuildGatherOccupied:true}),true);
  assert.equal(current.resourceOccupancy({rebuildGatherOccupancyKnown:true,rebuildGatherOccupied:false}),false);
  gaps.push({kind:'dispatch/ghost',column:'Task status',before:oldColumns.taskStatus(base,key=>key),sourceExpected:original.taskState(base,now)});
  for (const kind of ['truck','railway']) for (const value of ['', 'item-a']) for (const hasItemSort of [false,true]) {
    const initial = { truck:[{sortBy:'updatedAt',sortOrder:'desc'}], railway:[{sortBy:'updatedAt',sortOrder:'desc'}] };
    if (hasItemSort) initial[kind].unshift({sortBy:'itemCount',sortOrder:'desc'});
    let expectedSorts=structuredClone(initial), actualSorts=structuredClone(initial), expectedKey, actualKey, expectedPage, actualPage;
    const expected = new Function('L','Jt','zt','Yt','V','return ('+source.slice(originalFilter.start,originalFilter.end)+');')(
      kind,expectedSorts,fn=>{expectedKey=fn({[kind]:'old'})[kind];},fn=>{expectedSorts=fn(expectedSorts);},n=>{expectedPage=n;});
    const actual = new Function('tab','setItemKey','setSortsByKind','setPage',pages.slice(currentFilter.start,currentFilter.end)+';return changeItemFilter;')(
      kind,n=>{actualKey=n;},fn=>{actualSorts=fn(actualSorts);},n=>{actualPage=n;});
    expected(value);actual(value);
    assert.deepEqual(actualSorts,expectedSorts);assert.equal(actualKey||undefined,expectedKey);assert.equal(actualPage,expectedPage);filterCases++;
  }
  const fixture=getMapPreviewProvider('preview','map-city');
  assert.equal(fixture.online,false);
  assert.equal((await fixture.mapApi.search('city')).total,52);
  assert.equal((await fixture.mapApi.search('city')).rows.length,50);
  assert.equal((await fixture.mapApi.search('city',{page:2})).rows.length,2);
  assert.equal((await fixture.mapApi.search('city',{keyword:'no-fixture-matches-this'})).total,0);
  const alliance=(await fixture.mapApi.search('city',{alliance:'TST',pageSize:200})).rows;
  assert.ok(alliance.length>0&&alliance.every(row=>row.allianceName==='TST'));
  const levels=(await fixture.mapApi.search('city',{minLevel:25,maxLevel:27,sorts:[{sortBy:'level',sortOrder:'desc'}]})).rows;
  assert.ok(levels.length>0&&levels.every((row,i)=>row.level>=25&&row.level<=27&&(!i||levels[i-1].level>=row.level)));
  for (const mode of ['native','native-unavailable']) for (const state of ['map-resource','map-table-states']) assert.equal(getMapPreviewProvider(mode,state),null);
  for (const method of ['start','stop','clear','jumpServer','coordinateJump','setPlayerMark','exportCities']) await assert.rejects(()=>fixture.mapApi[method](),{code:'PREVIEW_NATIVE_ACTION_BLOCKED'});
  const states=getMapPreviewProvider('preview','map-table-states');
  const pending=await states.mapApi.search('dispatch',{completionStatus:'pending'});
  const completed=await states.mapApi.search('dispatch',{completionStatus:'completed'});
  assert.equal(pending.total,1);assert.equal(completed.total,56);
  assert.ok(pending.rows.every(row=>current.mapTaskState(row,now)==='pending'));
} finally { globalThis.Date=RealDate; }
const locator=(text,node)=>({utf8ByteOffset:Buffer.byteLength(text.slice(0,node.start)),expression:text.slice(node.start,node.end)});
const widthLocator=locator(source,tableWidths);
const report={task:'LWB317-UI-LEAD-TABLES-001',result:'LWB317_MAP_TABLES_SOURCE_LOCAL_OK',counts:{columnCases,valueComparisons,selectCases,taskCases,treasureCases,filterCases,renderCases:cases.length,clockCleanupCases:clockChecks.length,compactCases:11},fixtureRegression:{pagination:true,empty:true,alliance:true,levelSort:true,mutationsRejected:7,nativeModesFenced:2,variantPending:1,variantCompleted:56},baseline:'4e29806db72c83e37c0ba96f82270c66b5a4cbba',source:{path:sourcePath,sha256:hash(source),locators:Object.fromEntries([...helperNodes,liveKinds,typeMap,scanTypes].map(n=>[n.id.name,locator(source,n)])),itemFilter:locator(source,originalFilter),tableFunctions:Object.fromEntries(Object.entries(tableFunctions).map(([name,expression])=>[name,{utf8ByteOffset:Buffer.byteLength(source.slice(0,source.indexOf(expression))),expression}]))},truckSource:{path:mainPath,sha256:hash(main),locators:Object.fromEntries(truckNodes.map(n=>[n.id.name,locator(main,n)]))},rewardSource:{path:rewardPath,sha256:hash(rewards),locator:locator(rewards,rewardNode)},gaps,cases,clockChecks,limits:'Source differential and synthetic render cases. Rebuild occupancy unknown is a current-client adapter extension; unavailable images use placeholders. No native/gameplay/original pixel proof.'};
report.source.tableWidths=widthLocator;
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'map-table-results.json'),JSON.stringify(report,null,2)+'\n');
if(process.argv.includes('--verify-record'))assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here,'map-table-results.json'),'utf8')),report);
console.log(JSON.stringify({result:report.result,...report.counts,baselineValueGaps:gaps.length},null,2));
