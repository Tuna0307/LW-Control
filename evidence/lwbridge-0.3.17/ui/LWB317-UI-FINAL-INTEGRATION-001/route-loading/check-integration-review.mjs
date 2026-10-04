import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(here,'../../../../..');
const src=path.join(root,'src/LWBridge.UI-0.3.17/src');
const req=createRequire(path.join(src,'../package.json'));
const {parse}=req('@babel/parser');
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const ast=source=>parse(source,{sourceType:'module',plugins:['jsx']});
function declarations(source) {
  const result=new Map();
  for(const statement of ast(source).program.body) {
    const node=statement.type==='ExportNamedDeclaration'?statement.declaration:statement;
    if(node?.type==='FunctionDeclaration')result.set(node.id.name,{node,text:source.slice(node.start,node.end)});
    if(node?.type==='VariableDeclaration')for(const child of node.declarations)if(child.id.type==='Identifier')result.set(child.id.name,{node,text:source.slice(node.start,node.end)});
  }
  return result;
}
const prepared=JSON.parse(fs.readFileSync(path.join(here,'module-preparation.json'),'utf8'));
const all=new Map();
for(const entry of prepared.prepared) {
  const source=fs.readFileSync(path.join(src,entry.file),'utf8');
  for(const [name,entryDeclaration] of declarations(source)){assert.ok(!all.has(name),'unique owner '+name);all.set(name,{...entryDeclaration,file:entry.file});}
}
const unchanged=[];
for(const expected of prepared.declarations) {
  const current=all.get(expected.name);assert.ok(current,expected.name);assert.equal(current.file,expected.file);
  if(expected.name==='EquipmentContent')continue;
  assert.equal(hash(current.text),expected.originalSha256,expected.name+' unchanged declaration bytes');
  unchanged.push(expected.name);
}
assert.equal(unchanged.length,47);

const beforePath=path.join(here,'../motion/squads-before-motion.jsx');
const beforeSource=fs.readFileSync(beforePath,'utf8');
const before=declarations(beforeSource).get('EquipmentContent');
const currentSource=fs.readFileSync(path.join(src,'SquadsPage.jsx'),'utf8');
const after=declarations(currentSource).get('EquipmentContent');
const expectedEquipment=prepared.declarations.find(node=>node.name==='EquipmentContent');
assert.equal(hash(before.text),expectedEquipment.originalSha256,'preserved motion baseline equals immutable page migration body');
function prefix(source,node,removeMotion=false) {
  const returnNode=node.body.body.find(child=>child.type==='ReturnStatement');assert.ok(returnNode);
  return source.slice(node.start,returnNode.start).replace(removeMotion?/^\s*const reducedMotion = useReducedMotion\(\);\r?\n/m:/^$/,'');
}
assert.equal(prefix(currentSource,after.node,true),prefix(beforeSource,before.node),'all handler/state/ref/effect/busy/save code before JSX unchanged except added source reduced-motion hook');
function eventAttributes(source,node) {
  const entries=[];
  const walk=value=>{if(!value||typeof value!=='object')return;if(value.type==='JSXAttribute'&&/^on[A-Z]/.test(value.name.name))entries.push(source.slice(value.start,value.end));for(const child of Object.values(value)){if(Array.isArray(child))child.forEach(walk);else if(child&&typeof child==='object')walk(child);}};
  walk(node);return entries.sort();
}
const beforeEvents=eventAttributes(beforeSource,before.node),afterEvents=eventAttributes(currentSource,after.node);
assert.deepEqual(afterEvents,beforeEvents,'every JSX event handler remains exact across motion wrappers');
const propertyKinds=['presets','squad','slot','position','rename','toast','progress'];
for(const kind of propertyKinds)assert.ok(currentSource.includes(`equipmentMotionProps("${kind}"`),kind+' canonical caller integration');
assert.match(currentSource,/<motion\.article className=\{`equipment-position-card/);
assert.match(currentSource,/<AnimatePresence mode="wait">/);
assert.match(currentSource,/<AnimatePresence>\s*\{renameOpen \?/);
assert.match(currentSource,/<EquipmentDialog busy=\{busy\} onClose=\{closeRename\}><motion\.div className="equipment-preset-dialog"/);

const manifest=JSON.parse(fs.readFileSync(path.join(here,'../motion/closure-manifest.json'),'utf8'));
const vendor=fs.readFileSync(path.join(src,'vendor/equipmentMotion317.js'),'utf8');
assert.equal(hash(vendor),manifest.vendorSha256,'actual recovered engine identity');
const imports=ast(vendor).program.body.filter(node=>node.type==='ImportDeclaration').map(node=>node.source.value);
assert.deepEqual(imports,['react','react/jsx-runtime'],'motion engine has only existing React imports');
const originalBytes=fs.readFileSync(path.join(root,manifest.source));
assert.equal(hash(originalBytes),manifest.sourceSha256);
const classKinds={'equipment-preset-squads':'presets','equipment-preset-squad':'squad','preset-equipment-slot':'slot','equipment-position-card':'position','equipment-preset-dialog':'rename','equipment-toast':'toast','equipment-apply-progress':'progress'};
const originalTags={},currentTags={};
function walk(value,visit){if(!value||typeof value!=='object')return;visit(value);for(const child of Object.values(value)){if(Array.isArray(child))child.forEach(node=>walk(node,visit));else if(child&&typeof child==='object')walk(child,visit);}}
function classHead(node){const text=node?.type==='TemplateLiteral'?node.quasis[0].value.raw:node?.value;return typeof text==='string'?text.trim().split(/\s/)[0]:'';}
walk(ast(originalBytes.toString()),node=>{
  if(node.type!=='CallExpression'||node.arguments[0]?.type!=='MemberExpression'||node.arguments[0].object.name!=='td')return;
  const object=node.arguments[1];if(object?.type!=='ObjectExpression')return;
  const classProperty=object.properties.find(property=>property.key?.name==='className');const kind=classKinds[classHead(classProperty?.value)];
  if(kind)originalTags[kind]=node.arguments[0].property.name;
});
walk(after.node,node=>{
  if(node.type!=='JSXOpeningElement'||node.name.type!=='JSXMemberExpression'||node.name.object.name!=='motion')return;
  const attribute=node.attributes.find(attribute=>attribute.name?.name==='className');const value=attribute?.value?.type==='JSXExpressionContainer'?attribute.value.expression:attribute?.value;const kind=classKinds[classHead(value)];
  if(kind)currentTags[kind]=node.name.property.name;
});
assert.equal(Object.keys(originalTags).length,7);assert.deepEqual(currentTags,originalTags,'all seven integrated motion element tags match actual original');
const originalClosure=originalBytes.subarray(manifest.closure.utf8ByteOffset,manifest.closure.utf8ByteOffset+manifest.closure.utf8ByteLength);
assert.equal(hash(originalClosure),manifest.closure.sha256);
assert.ok(vendor.includes(originalClosure.toString()),'vendor contains exact original closure bytes');
const originalHelperBytes=fs.readFileSync(path.join(root,manifest.helper.source));
assert.equal(hash(originalHelperBytes),manifest.helper.sourceSha256);
const helper=originalHelperBytes.subarray(manifest.helper.utf8ByteOffset,manifest.helper.utf8ByteOffset+manifest.helper.utf8ByteLength);
assert.equal(hash(helper),manifest.helper.sha256);assert.ok(vendor.includes(helper.toString()),'exact original helper bytes');
const result={status:'PASS',unchangedDeclarations:unchanged.length,unchangedNames:unchanged,equipmentPrefixUnchanged:true,eventHandlersUnchanged:beforeEvents.length,reducedMotionOnlyAddedHook:true,motionCallerKinds:propertyKinds,elementTags:{original:originalTags,current:currentTags,comparisons:7},positionTag:'article',presenceMode:'wait',nativeRenameDialogHierarchy:true,engineImports:imports,engineClosureSHA256:manifest.closure.sha256,sourceHashes:{App:hash(fs.readFileSync(path.join(src,'App.jsx'))),Pages:hash(fs.readFileSync(path.join(src,'Pages.jsx'))),SquadsPage:hash(Buffer.from(currentSource))},limits:'Source/AST/declaration/handler integration review. Engine runtime/real mounted route/caller props proof is recorded separately; this does not establish native persistence/gameplay or original pixels.'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'independent-integration-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_INDEPENDENT_INTEGRATION_REVIEW_OK declarations=47 handlers=${beforeEvents.length} prefix=unchanged position=article closure=EXACT_BYTES`);
