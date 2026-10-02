import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import * as backend from '../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js';
import { getMapPreviewProvider } from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here,'../../../..');
const require = createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
const { transformSync } = require('esbuild');
const contract = JSON.parse(fs.readFileSync(path.join(here,'source-contract.json')));
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const original = fs.readFileSync(path.join(repo,contract.source.path),'utf8');
assert.equal(hash(original),contract.source.sha256);
function verifyLocator(record,text=original) {
  assert.equal(Buffer.from(text).subarray(record.byte,record.byte+Buffer.byteLength(record.expression)).toString('utf8'),record.expression);
}
for (const records of [contract.states,contract.sets,contract.helpers,contract.handlers]) Object.values(records).forEach(record=>verifyLocator(record));
const index = fs.readFileSync(path.join(repo,contract.treasure.index.path),'utf8');
assert.equal(hash(index),contract.treasure.index.sha256);
Object.values(contract.treasure.dependencies).forEach(record=>verifyLocator(record,index));
function walk(node,list=[]) {
  if(!node || typeof node!=='object') return list;
  if(node.type) list.push(node);
  for(const value of Object.values(node)) if(Array.isArray(value)) value.forEach(item=>walk(item,list)); else if(value && typeof value==='object') walk(value,list);
  return list;
}
function treeNodes(node,list=[]) {
  if(Array.isArray(node)) node.forEach(item=>treeNodes(item,list));
  else if(node && typeof node==='object') { list.push(node); treeNodes(node.props?.children,list); }
  return list;
}
const text = node=>node==null || typeof node==='boolean' ? '' : Array.isArray(node)?node.map(text).join(''):typeof node==='object'?text(node.props?.children):String(node);
const originalHelpers = new Function(Object.values(contract.sets).map(record=>'const '+record.expression+';').join('\n')+['Ue','A','We','ve'].map(name=>contract.helpers[name].expression).join('\n')+';return {Ue,A,We,ve};')();
function reference() {
  const state = {quality:{},item:{},completion:{},plunderable:{},sorts:Object.fromEntries(backend.MAP_KIND_KEYS.map(kind=>[kind,[{sortBy:'updatedAt',sortOrder:'desc'}]])),page:1,level:''};
  const apply = (name,update)=>state[name]=typeof update==='function'?update(state[name]):update;
  return {
    state,
    change(kind,field,value) {
      if(field==='level') { state.level=value; state.page=1; return; }
      const handler = new Function('L','Lt','zt','Ut','Gt','Yt','Jt','V','return ('+contract.handlers[field].expression+');')(
        kind,fn=>apply('quality',fn),fn=>apply('item',fn),fn=>apply('completion',fn),fn=>apply('plunderable',fn),fn=>apply('sorts',fn),state.sorts,number=>state.page=number,
      );
      handler(field==='item'?value:{target:{value,checked:value}});
    },
    query(kind) {
      return new Function('B','je','L','tt','Ot','Ue','A','It','Rt','Ct','R','et','At','Bt','Mt','Jt','Ht','Wt','ve','We','Nn','J','In','Kt',contract.helpers.nr.expression+';return nr();')(
        state.page,50,kind,()=>null,'all',originalHelpers.Ue,originalHelpers.A,state.quality,state.item,[],321,'',{},'',false,state.sorts,state.completion,state.plunderable,originalHelpers.ve,originalHelpers.We,false,false,{},state.level,
      );
    },
  };
}
function production(source) {
  const ast=parse(source,{sourceType:'module',plugins:['jsx']});
  const statements=ast.program.body.filter(node=>node.type!=='ImportDeclaration').map(node=>node.type==='ExportNamedDeclaration'?node.declaration:node);
  const code=transformSync(statements.map(node=>source.slice(node.start,node.end)).join('\n'),{loader:'jsx',jsxFactory:'h',jsxFragment:'Fragment'}).code;
  const pageNode=walk(ast).find(node=>node.type==='FunctionDeclaration' && node.id?.name==='MapDataPage');
  const stateNames=walk(pageNode).filter(node=>node.type==='VariableDeclarator' && node.id.type==='ArrayPattern' && node.init?.callee?.name==='useState').map(node=>node.id.elements[0].name);
  let stateCursor=0,refCursor=0,effects=[],lastQuery;
  const cells=[],refs=[];
  const useState=initial=>{const slot=stateCursor++;if(!(slot in cells))cells[slot]=typeof initial==='function'?initial():initial;return[cells[slot],value=>cells[slot]=typeof value==='function'?value(cells[slot]):value];};
  const useRef=initial=>{const slot=refCursor++;return refs[slot]??=( {current:initial} );};
  const api={profileId:'test-profile',search:async(kind,query)=>{lastQuery={kind,query};return {rows:[],total:0};}};
  const fakeWindow={localStorage:{getItem:()=>null,setItem:()=>{} }};
  const h=(type,props,...children)=>({type,props:{...props,children}});
  const names=['useState','useRef','useCallback','useMemo','useEffect','useI18n','window','h','Fragment',...Object.keys(backend)];
  const values=[useState,useRef,fn=>fn,fn=>fn(),(fn,deps)=>effects.push({fn,deps}),()=>({language:'en',t:key=>key}),fakeWindow,h,'Fragment',...Object.values(backend)];
  const Page=new Function(...names,code+';return MapDataPage;')(...values);
  const getState=name=>cells[stateNames.indexOf(name)];
  const setState=(name,value)=>{assert.ok(stateNames.includes(name),name);cells[stateNames.indexOf(name)]=value;};
  let tree;
  function render() { stateCursor=0;refCursor=0;effects=[];tree=Page({mapApi:api,bridgeMode:'preview',previewState:'map-truck',backendAvailable:true,online:false,currentServerId:321});return tree; }
  render();
  return {
    render,getState,setState,
    navigate(kind) { render();const tab=treeNodes(tree).find(node=>node.type==='button' && node.props.role==='tab' && text(node).includes({railway:'map.allianceTrain',dispatch:'map.secretTask',ghost:'map.ghostScout',truck:'map.truck',city:'map.city',resource:'map.resource',monster:'map.monster',treasure:'map.treasure'}[kind]));assert.ok(tab,kind);tab.props.onClick();render(); },
    change(field,value) {
      render();const nodes=treeNodes(tree);
      const labels={quality:'map.quality',item:'map.itemFilter',completion:'common.status',level:'map.level'};
      const node=field==='plunderable'?nodes.find(n=>n.type==='label' && text(n)==='map.plunderableOnly')?.props.children?.flat().find(n=>n?.type==='input'):nodes.find(n=>n.type==='select' && n.props['aria-label']===labels[field]);
      assert.ok(node,field);node.props.onChange({target:{value,checked:value}});render();
    },
    async query() {
      render();const effect=effects.find(entry=>entry.fn.toString().includes('mapApi.search'));
      assert.ok(effect);const cleanup=effect.fn();await Promise.resolve();await Promise.resolve();await Promise.resolve();cleanup?.();return lastQuery;
    },
    tableProps() { render();return treeNodes(tree).find(node=>typeof node.type==='function' && node.type.name==='MapTable').props; },
  };
}
const fields=['quality','specialOnly','reindeerOnly','itemKey','completionStatus','plunderableOnly','minLevel','maxLevel'];
const projection=query=>Object.fromEntries(fields.filter(field=>query[field]!==undefined).map(field=>[field,query[field]]));
const relative='src/LWBridge.UI-0.3.17/src/MapDataPage.jsx';
const current=fs.readFileSync(path.join(repo,relative),'utf8');
const baseline=fs.readFileSync(path.join(here,'baseline.MapDataPage.jsx'),'utf8');
async function campaign(source) {
  const actual=production(source),expected=reference(),failures=[];let comparisons=0;
  async function compare(label) {
    for(const kind of backend.MAP_KIND_KEYS) {
      actual.navigate(kind);
      const result=await actual.query();
      assert.equal(result.kind,kind);
      const got=projection(result.query),want=projection(expected.query(kind));
      try { assert.deepEqual(got,want); } catch { failures.push({label,kind,actual:got,expected:want}); }
      comparisons++;
      // A hidden item choice must also stay out of the table's conditional sort.
      const props=actual.tableProps();
      const wantedItem=originalHelpers.A(kind)?expected.state.item[kind]||'':'';
      if(props.itemKey!==wantedItem)failures.push({label,kind,tableItemKey:props.itemKey,wantedItem});
    }
  }
  await compare('defaults');
  const operations=[
    ['truck','quality','reindeer'],['truck','item','fixture-reward'],['truck','plunderable',true],
    ['railway','quality','ssr'],['railway','item','fixture-supply'],['railway','plunderable',false],
    ['dispatch','quality','special'],['dispatch','completion','pending'],['dispatch','plunderable',true],['dispatch','level','6'],
    ['ghost','quality','ur'],['ghost','completion','completed'],
    ['truck','quality',''],['truck','item',''],['truck','plunderable',false],
    ['dispatch','completion',''],['dispatch','level',''],['ghost','quality',''],
  ];
  for(const [kind,field,value] of operations) {
    actual.navigate(kind);actual.setState('page',4);expected.state.page=4;
    actual.change(field,value);expected.change(kind,field,value);
    assert.equal(actual.getState('page'),1,`${kind}/${field} page reset`);
    await compare(`${kind}/${field}/${String(value)}`);
  }
  // Distinct item-count sorts: clearing Truck must preserve the Train choice/sorts.
  const sorts={...expected.state.sorts,truck:[{sortBy:'itemCount',sortOrder:'asc'},{sortBy:'power',sortOrder:'desc'}],railway:[{sortBy:'itemCount',sortOrder:'desc'}]};
  actual.setState('sortsByKind',structuredClone(sorts));expected.state.sorts=structuredClone(sorts);
  actual.navigate('truck');actual.change('item','');expected.change('truck','item','');
  assert.deepEqual(actual.getState('sortsByKind'),expected.state.sorts);
  await compare('sort-clear Truck only');
  // Every valid quality/status/checkbox value through actual production handlers.
  for(const kind of ['truck','railway','dispatch','ghost']) {
    for(const value of ['n','r','sr','ssr','ur','',...(kind==='truck'?['reindeer']:['dispatch','ghost'].includes(kind)?['special']:[])]) {
      actual.navigate(kind);actual.change('quality',value);expected.change(kind,'quality',value);await compare(`${kind}/quality/${value}`);
    }
  }
  for(const kind of ['dispatch','ghost']) for(const value of ['pending','completed','']) {
    actual.navigate(kind);actual.change('completion',value);expected.change(kind,'completion',value);await compare(`${kind}/completion/${value}`);
  }
  for(const kind of ['truck','railway','dispatch']) for(const value of [true,false]) {
    actual.navigate(kind);actual.change('plunderable',value);expected.change(kind,'plunderable',value);await compare(`${kind}/plunderable/${value}`);
  }
  return {comparisons,failures};
}
const old=await campaign(baseline);
assert.ok(old.failures.length>0,'baseline must distinguish demonstrated defects');
const now=await campaign(current);
assert.deepEqual(now.failures,[],'current original-vs-production mismatches');
for(const mode of ['native','native-unavailable']) assert.equal(getMapPreviewProvider(mode,'map-truck'),null);
const provider=getMapPreviewProvider('preview','map-truck');
assert.equal(provider.online,false);
for(const action of ['start','stop','clear','jumpServer','coordinateJump','setPlayerMark','exportCities']) await assert.rejects(provider.mapApi[action](),{code:'PREVIEW_NATIVE_ACTION_BLOCKED'});
const exactLevel=await provider.mapApi.search('dispatch',{serverId:321,page:1,minLevel:6,maxLevel:6,sorts:[{sortBy:'updatedAt',sortOrder:'desc'}]});
assert.ok(exactLevel.rows.length>0);assert.ok(exactLevel.rows.every(row=>row.level===6));
const output={status:'PASS',scope:'Extracted actual production component with persistent hook cells, actual rendered controls and search effect; unrelated effects suppressed. Exact original callbacks/query executed. Not native or pixel proof.',original:contract.source,current:{path:relative,sha256:hash(current)},baseline:{path:'baseline.MapDataPage.jsx',sha256:hash(baseline),...old},currentResults:now,preview:{offline:true,blockedActions:7,nativeIsolation:true,exactLevelRows:exactLevel.rows.length}};
if(process.argv.includes('--record')) fs.writeFileSync(path.join(here,'filter-results.json'),JSON.stringify(output,null,2)+'\n');
console.log(`LWB317_MAP_FILTERS_OK ${now.comparisons} original/current queries; baseline ${old.failures.length} mismatches; fences PASS`);
