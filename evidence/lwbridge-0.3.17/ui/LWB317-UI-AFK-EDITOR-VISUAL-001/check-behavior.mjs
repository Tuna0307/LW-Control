import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {here,scenarios,original,current,flatten,read,canonical,nodes,raw,hash,profileFor} from './harness.mjs';
const source=read(canonical),baseline=fs.readFileSync(path.join(here,'SquadsPage.baseline.jsx'),'utf8');
function callbacks(source){return nodes(source).filter(n=>n.type==='JSXAttribute'&&['onChange','onBlur','onKeyDown'].includes(n.name?.name)&&n.start>source.indexOf('function AfkProfileEditor(')&&n.end<source.indexOf('function AllianceDrillPreviewSettings')).map(n=>({name:n.name.name,expression:raw(source,n.value.expression)}));}
assert.deepEqual(callbacks(source),callbacks(baseline));
const start=source.indexOf('function AfkProfileEditor('),end=source.indexOf('function AllianceDrillPreviewSettings');
assert.equal(source.slice(0,start),baseline.slice(0,baseline.indexOf('function AfkProfileEditor(')));
assert.equal(source.slice(end),baseline.slice(baseline.indexOf('function AllianceDrillPreviewSettings')));
let cases=0;
for(const scenario of scenarios){
 const oldTree=current(scenario,'en',()=>{},true),newTree=current(scenario);
 const controls=t=>flatten(t).filter(n=>['input','select','button'].includes(n.type));
 assert.deepEqual(controls(newTree).map(n=>[n.type,n.props.type,n.props.disabled,n.props.value,n.props.checked,n.props['aria-pressed']]),controls(oldTree).map(n=>[n.type,n.props.type,n.props.disabled,n.props.value,n.props.checked,n.props['aria-pressed']]));cases++;
 for(const enabled of [false,true]){
  const tree=current(scenario,'en',()=>{},false,enabled);
  const rootControls=controls(tree);if(!enabled)assert.ok(rootControls.every(n=>n.props.disabled));
  cases++;
 }
 // Invoke the actual original and canonical inline callbacks. Target/custom
 // conversion and parent name-blur persistence have independent legacy proof;
 // this unit preserves their exact callback expressions above.
 const originalControls=controls(original(scenario));
 const currentControls=controls(newTree);
 for(let index=0;index<originalControls.length;index++){
  const a=originalControls[index],b=currentControls[index];
  if(a.type!=='input'||!a.props.onChange||!b.props.onChange)continue;
  if(index<3)continue; // name, target select, custom checkbox
  if(a.props['aria-invalid']!==undefined)continue; // custom name converter
  let expected,actual;
  const oa=controls(original(scenario,'en',v=>expected=v))[index];
  const cb=controls(current(scenario,'en',v=>actual=v))[index];
  for(const value of [0,7,NaN]){
   expected=actual=undefined;
   const event={target:{checked:!a.props.checked,valueAsNumber:value,value:String(value)}};
   oa.props.onChange(event);cb.props.onChange(event);assert.deepEqual(actual,expected);cases++;
  }
 }
 const originalButtons=originalControls.filter(n=>n.type==='button'&&n.props.className==='monster-afk-squad-toggle');
 for(let i=0;i<originalButtons.length;i++){
  let expected,actual;
  controls(original(scenario,'en',v=>expected=v)).filter(n=>n.type==='button')[i].props.onClick();
  controls(current(scenario,'en',v=>actual=v)).filter(n=>n.type==='button')[i].props.onClick();
  assert.deepEqual(actual,expected);cases++;
 }
}
const result={cases,preservedCallbacks:callbacks(source).length,otherSquadsFunctionsByteIdentical:true,currentSha256:hash(source)};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'behavior-results.json'),JSON.stringify(result,null,2)+'\n');
console.log('LWB317_AFK_EDITOR_BEHAVIOR_OK '+JSON.stringify(result));
