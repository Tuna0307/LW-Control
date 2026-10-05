import crypto from 'node:crypto';
import {
  read, squad, compile, h, Fragment, hooks, flatten, text, stable,
} from '../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import * as fixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js';
import {refreshGarrisonTargets} from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js';
import en from '../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';
import ja from '../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js';

export const currentSource = read('evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-2/baseline/pre-fix-SquadsPage.jsx');
export const hash = (value) => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
export const tFor = (catalog) => (key, vars = {}) => String(catalog[key] || key || '')
  .replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);
const catalogs = {en, ja};

const SourceM = compile(squad, 'M');
const SourceN = compile(squad, 'N');
const SourceFe = compile(squad, 'fe');
const SourceDe = compile(squad, 'de');

function sourceStore(draft, error = null) {
  let current = structuredClone(draft);
  const state = {
    edit(next) { current = typeof next === 'function' ? next(current) : next; },
    getSnapshot: () => ({draft: current, confirmed: draft}),
    flush: async () => current,
    runAction: async (action) => action(),
    refresh: async () => current,
  };
  return {get draft() { return current; }, state, error};
}

function sourceIcon({name}) {
  if (name !== 'drag') return h('SourceIcon', {name});
  return h('svg', {className: 'ui-icon', viewBox: '0 0 16 16', 'aria-hidden': 'true'},
    h('path', {d: 'M5 4h.01M11 4h.01M5 8h.01M11 8h.01M5 12h.01M11 12h.01'}));
}

function sourceConfigError({state}) {
  return state.__error ? h('div', {className: 'config-save-error'}, 'configSave.failed') : null;
}

function sourceModal({className, children}) {
  return h('div', {className, role: 'dialog'}, children);
}

function sourceGarrison({language='en', config, discovery, status, squads=[1,2,3,4], modalOpen=false, search='', localError='', configError=false}) {
  const t = tFor(catalogs[language]);
  const sourceHooks = hooks([discovery, status, squads, {}, modalOpen, search, new Set(), '', localError, '', null]);
  sourceHooks.useCallback = (fn) => fn;
  const store = sourceStore(config, configError ? 'configSave.failed' : null);
  store.state.__error = configError;
  const Original = compile(squad, 'pe', {
    A: {jsx:(type,props)=>({type,props}), jsxs:(type,props)=>({type,props}), Fragment},
    O: sourceHooks,
    d: () => ({language, t}), c: () => 1,
    e: () => store, j: {enabled:false,recallOnDisable:true,squadPriority:[],targets:[]},
    M: SourceM, N: SourceN, fe: SourceFe, de: SourceDe,
    m: async () => ({}), S: async () => ({}), te: async () => discovery || {buildings:[],allies:[]},
    f: async () => ({squads: squads.map((index) => ({index}))}), u: async () => ({}),
    C: async () => ({tasks:{allianceGarrison:status}}), re: async () => ({}), b: async () => ({}),
    _: (translate, error) => translate(typeof error === 'string' ? error : 'common.actionFailed'),
    T: sourceIcon, w: ({label,checked}) => h('SourceToggle',{label,checked}),
    ne: sourceConfigError, y: sourceModal,
    window:{setInterval:()=>0,clearInterval(){}},
  });
  sourceHooks.begin();
  return Original({online:true,serverId:321,config,open:true,onOpenChange(){},onLog(){}});
}

function currentGarrison({language='en', value, previewState='squads-profile-garrison-open', modalOpen=false, search=''}) {
  const t = tFor(catalogs[language]);
  const currentHooks = hooks([modalOpen, search, new Set(), '', null, '']);
  const Current = compile(currentSource, 'GarrisonPreviewSettings', {
    h, Fragment, ...currentHooks,
    useI18n: () => ({t}),
    previewGarrisonRuntime: fixtures.previewGarrisonRuntime,
    previewGarrisonMemberFixture: fixtures.previewGarrisonMemberFixture,
    refreshGarrisonTargets,
    ToggleRow: ({label,checked}) => h('CurrentToggle',{label,checked}),
    window:{addEventListener(){},removeEventListener(){}},
  });
  currentHooks.begin();
  return Current({disabled:false,value,onChange(){},previewState});
}

function sourceZombie({language='en', enabled=true, open=true, status=null, configError=false, names={}}) {
  const t = tFor(catalogs[language]);
  const sourceHooks = hooks([status, names]);
  const store = sourceStore({enabled}, configError ? 'configSave.failed' : null);
  store.state.__error = configError;
  const Original = compile(squad, 'he', {
    A: {jsx:(type,props)=>({type,props}), jsxs:(type,props)=>({type,props}), Fragment},
    O: sourceHooks,
    d: () => ({language,t}), c:()=>1, e:()=>store,
    m:async()=>({config:{tasks:{zombieBus:{enabled}}}}), D:async()=>({}), C:async()=>({tasks:{zombieBus:status}}),
    u:async()=>names,
    ie:(props)=>h('SourceCompact',props), me:compile(squad,'me',{A:{jsx:(type,props)=>({type,props}),jsxs:(type,props)=>({type,props}),Fragment},d:()=>({t})}),
    ne:sourceConfigError,
    window:{setInterval:()=>0,clearInterval(){}},
  });
  sourceHooks.begin();
  return Original({online:true,config:{enabled},open,onOpenChange(){},onLog(){}});
}

function currentZombie({language='en', previewState='squads-profile-zombie-error'}) {
  const t = tFor(catalogs[language]);
  const Current = compile(currentSource,'ZombieBusPreviewSettings',{
    h,Fragment,useI18n:()=>({t}),previewZombieBusRuntime:fixtures.previewZombieBusRuntime,
  });
  return Current({previewState});
}

export function settingsShape(tree) {
  const nodes = flatten(tree);
  return {
    settingsBlocks: nodes.filter((node)=>String(node.props?.className||'').split(/\s+/).includes('monster-afk-toolbar-settings')).map((node)=>({type:node.type,className:node.props.className,text:text(node)})),
    muted: nodes.filter((node)=>node.props?.className==='muted').map((node)=>({type:node.type,text:text(node)})),
    priorityLists: nodes.filter((node)=>node.props?.className==='garrison-priority-list').map((node)=>stable(node)),
    selectedAllies: nodes.filter((node)=>node.props?.className==='garrison-selected-allies').map((node)=>stable(node)),
    dragPaths: nodes.filter((node)=>node.type==='path').map((node)=>node.props?.d).filter(Boolean),
    arrowTextCount: nodes.filter((node)=>text(node)==='↕').length,
    alerts: nodes.filter((node)=>['automation-error','inline-error'].includes(node.props?.className)).map((node)=>({type:node.type,className:node.props.className,text:text(node)})),
    allyRows: nodes.filter((node)=>node.type==='label' && (node.props?.className==='' || node.props?.className==='unavailable')).map((node)=>({className:node.props.className,text:text(node),em:flatten(node).filter((n)=>n.type==='em').map(text)})),
  };
}

export const scenarios = {
  empty(language='en') {
    const config={enabled:false,recallOnDisable:true,targets:[],squadPriority:[]};
    const discovery={buildings:fixtures.previewGarrisonBuildings||[],allies:fixtures.previewAllianceMembers||[]};
    return {source:settingsShape(sourceGarrison({language,config,discovery,status:{assignments:[],guardingCount:0},squads:[1,2,3,4]})),current:settingsShape(currentGarrison({language,value:config}))};
  },
  positive(language='en') {
    const config={enabled:true,recallOnDisable:true,targets:[{kind:'allianceBuilding',buildId:1,nameSnapshot:'Fixture Alliance Center'}],squadPriority:[1,2]};
    const discovery={buildings:[{key:'building:1',buildId:1,role:'center',name:'Fixture Alliance Center',available:true}],allies:[]};
    return {source:settingsShape(sourceGarrison({language,config,discovery,status:{assignments:[],guardingCount:0},squads:[1,2,3,4]})),current:settingsShape(currentGarrison({language,value:config}))};
  },
  modalUid(language='en') {
    const config={enabled:false,recallOnDisable:true,targets:[],squadPriority:[1]};
    const allies=fixtures.previewAllianceMembers.map((member,index)=>({...member,available:index!==3,unavailableReason:index===3?'cross_server':''}));
    const discovery={buildings:[],allies};
    return {source:settingsShape(sourceGarrison({language,config,discovery,status:null,squads:[1,2],modalOpen:true,search:'10003'})),current:settingsShape(currentGarrison({language,value:config,modalOpen:true,search:'10003'}))};
  },
  statusError(language='en') {
    const config={enabled:false,recallOnDisable:true,targets:[],squadPriority:[1]};
    const discovery={buildings:[],allies:[]};
    return {source:settingsShape(sourceGarrison({language,config,discovery,status:{assignments:[],guardingCount:0,lastError:'common.actionFailed'},squads:[1]})),current:settingsShape(currentGarrison({language,value:config,previewState:'squads-profile-garrison-error'}))};
  },
  zombieOpenError(language='en') {
    const status={busAssignments:[],lastError:'common.actionFailed'};
    return {source:settingsShape(sourceZombie({language,enabled:true,open:true,status})),current:settingsShape(currentZombie({language,previewState:'squads-profile-zombie-error'}))};
  },
};
