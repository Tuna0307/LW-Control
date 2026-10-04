import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {repo,read,squad,fn,raw,nodes,evaluate,compile,h,jsx,Fragment,hooks,flatten,text} from '../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import {originalCatalogs,currentCatalogs,translate,markup,shared,hash} from '../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-e/page-harness.mjs';
import * as afk from '../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js';
import * as fixtures from '../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js';
export {repo,read,squad,fn,raw,nodes,evaluate,compile,h,jsx,Fragment,hooks,flatten,text,markup,hash,afk};
export const here=path.dirname(fileURLToPath(import.meta.url));
export const canonical='src/LWBridge.UI-0.3.17/src/SquadsPage.jsx';
export const editor=nodes(squad).find(n=>n.type==='CallExpression'&&n.arguments[1]?.properties?.some(p=>p.key?.name==='className'&&raw(squad,p.value).includes('monster-afk-editor ${')));
if(!editor)throw Error('Original editor subtree missing');
export const joinReturn=fn(squad,'ye').body.body.find(n=>n.type==='ReturnStatement').argument;
export const scenarios=[
 {id:'default',changes:{}},
 {id:'new',changes:{name:'Fixture new profile'},isNew:true},
 {id:'filters',changes:{levelFilterEnabled:true,distanceFilterEnabled:true,minLevel:3,maxLevel:8,maxDistance:120}},
 {id:'progressive',changes:{levelFilterEnabled:true,progressiveLevels:true,minLevel:4}},
 {id:'warning',changes:{levelFilterEnabled:true,minLevel:11,maxLevel:12}},
 {id:'custom-empty',changes:{customTarget:true,targetKey:'query:',targetNameQuery:'',searchable:false}},
 {id:'custom-name',changes:{customTarget:true,targetKey:'query:Fixture',targetNameQuery:'Fixture',searchable:false}},
 {id:'rally',kind:'farm',target:'boss',changes:{}},
 {id:'join-disabled',kind:'join',changes:{}},
 {id:'join-delay',kind:'join',changes:{joinRestrictions:{...afk.normalizeJoinRestrictions(undefined,1,true),enabled:true}}},
 {id:'join-slot',kind:'join',changes:{joinRestrictions:{...afk.normalizeJoinRestrictions(undefined,1,true),enabled:true,mode:'slot'}}},
 {id:'nan',changes:{executionLimit:NaN,minLevel:NaN,maxLevel:NaN,maxDistance:NaN,levelFilterEnabled:true,distanceFilterEnabled:true}},
];
export const profileFor=s=>({...afk.makePreviewAfkProfile('fixture','Fixture profile',s.kind??'farm',s.target??(s.kind==='join'?'boss':'steel')),...s.changes});
function originalJoin(lang){const o=translate(originalCatalogs[lang]);return props=>evaluate(raw(squad,joinReturn),{A:jsx,e:props.value,t:props.onChange,n:false,r:props.disabled,i:props.label,o,x:null,v:false,S:[],C:new Map(),T:props.value.leaders,ee:[],f:false,m:'',g:new Set(),_:()=>{},h:()=>{},p:()=>{},w:p=>h(shared(lang,true).ToggleRow,{...p,label:o(p.label)}),y:'Modal',se:compile(squad,'se')});}
export function original(s,lang='en',update=()=>{}){
 const L=profileFor(s), M=afk.previewAfkTargets.filter(t=>t.group!=='drill'),h2=translate(originalCatalogs[lang]);
 const Ke=compile(squad,'ue')(M,L),Ge=L.kind;
 const Oe=L.targetKey.startsWith('query:'),Je=Oe?L.lastListTargetKey||'':Ke?.key||L.targetKey||'';
 const Ye=M.filter(e=>!e.key.startsWith('name:')&&(Ge==='farm'?!e.joinOnly:e.rally));
 const Xe=['normal','elite','running','leader','ally','drill','invader','other'].map(group=>({group,targets:Ye.filter(t=>t.group===group)})).filter(x=>x.targets.length);
 const env={A:jsx,L,M,H:{current:null},h:h2,We:!!s.isNew,Oe,Je,qe:!Oe&&!Ke,Xe,Ge,Ke,Ne:[1,2,3,4],B:'',t:false,P:()=>{},Qe:()=>{},R:next=>update(typeof next==='function'?next(L):next),at:()=>{},ct:()=>{},V:()=>{},oe:compile(squad,'oe'),ye:originalJoin(lang),ft:compile(squad,'we')(L,Ke)};
 env.dt=compile(squad,'dt',{h:h2});env.G=compile(squad,'G',{h:h2,Ge,dt:env.dt});
 return evaluate(raw(squad,editor),env);
}
export function current(s,lang='en',update=()=>{},baseline=false,enabled=true){
 const source=baseline?fs.readFileSync(path.join(here,'SquadsPage.baseline.jsx'),'utf8'):read(canonical);
 const hook=hooks(),t=translate(currentCatalogs[lang]);
 const joinSource=read('src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx');
 const Join=props=>{const child=hooks();return compile(joinSource,'RallyJoinSettings',{...child,A:jsx,useI18n:()=>({t}),previewMemberFixture:fixtures.previewMemberFixture,se:afk.validJoinRestrictions,JoinModal:'Modal'})({...props,ToggleRow:shared(lang).ToggleRow});};
 const Component=compile(source,'AfkProfileEditor',{h,Fragment,...hook,useI18n:()=>({t}),...afk,RallyJoinSettings:Join,ToggleRow:shared(lang).ToggleRow});
 hook.begin();return Component({enabled,isNew:!!s.isNew,profile:profileFor(s),onProfileChange:update,previewState:''});
}
export const normalizeHtml=html=>html.replace(/ data-preview-fixture="afk-profile-editor"/g,'');
export const sourceLocator={asset:'SquadPanel-HC3-DJei.js',assetSha256:hash(squad),editorUtf8Start:Buffer.byteLength(squad.slice(0,editor.start)),editorUtf8End:Buffer.byteLength(squad.slice(0,editor.end)),editorSha256:hash(raw(squad,editor)),joinUtf8Start:Buffer.byteLength(squad.slice(0,joinReturn.start))};
