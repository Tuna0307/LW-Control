import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../../../..');
const req=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json')),{parse}=req('@babel/parser');
const sourcePath='evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js',source=fs.readFileSync(path.join(repo,sourcePath),'utf8');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
function walk(node,out=[]){if(!node||typeof node!=='object')return out;if(node.type)out.push(node);for(const value of Object.values(node))Array.isArray(value)?value.forEach(child=>walk(child,out)):value&&typeof value==='object'&&walk(value,out);return out;}
function literal(node){return node?.value??(node?.type==='TemplateLiteral'&&node.expressions.length===0?node.quasis[0].value.cooked:undefined);}
function prop(node,key){return node.properties.find(item=>item.key?.name===key)?.value;}
const locator=node=>({utf8ByteOffset:Buffer.byteLength(source.slice(0,node.start)),utf8ByteLength:Buffer.byteLength(source.slice(node.start,node.end)),sha256:hash(source.slice(node.start,node.end)),text:source.slice(node.start,node.end)});
const ast=parse(source,{sourceType:'module'}),entry=ast.program.body.find(node=>node.type==='FunctionDeclaration'&&node.id.name==='Ae'),nodes=walk(entry);
const grid=nodes.find(node=>node.type==='CallExpression'&&node.arguments[1]?.type==='ObjectExpression'&&literal(prop(node.arguments[1],'className'))==='automation-grid');
assert.ok(grid);
const children=prop(grid.arguments[1],'children').elements;
assert.equal(children.length,8);
const groups=children.map(node=>{assert.equal(node.type,'LogicalExpression');const category=literal(node.left.arguments[0]);const activity=node.right;assert.equal(activity.arguments[0].object.name,'x');assert.equal(activity.arguments[0].property.name,'Activity');const props=activity.arguments[1],child=prop(props,'children');return {category,gate:locator(node.left),mode:locator(prop(props,'mode')),boundary:locator(node),children:locator(child),titles:walk(child).filter(item=>item.type==='ObjectProperty'&&item.key.name==='title').map(item=>literal(item.value)).filter(Boolean),leafTypes:[...new Set(walk(child).filter(item=>item.type==='CallExpression'&&item.arguments[0]?.type==='Identifier').map(item=>item.arguments[0].name))]};});
assert.deepEqual(groups.map(item=>item.category),['resourceGather','trade','system','chat','resources','daily','alliance','daily']);
const replacement=children.map((node,index)=>{const child=prop(node.right.arguments[1],'children');return {start:child.start-grid.start,end:child.end-grid.start,text:`(0,S.jsx)(Probe,{category:${JSON.stringify(groups[index].category)}})`};});
let probeGrid=source.slice(grid.start,grid.end);for(const edit of replacement.sort((a,b)=>b.start-a.start))probeGrid=probeGrid.slice(0,edit.start)+edit.text+probeGrid.slice(edit.end);
const declarations=nodes.filter(node=>node.type==='VariableDeclarator'&&(node.id.name==='B'||node.id.type==='ArrayPattern'&&['at','V'].includes(node.id.elements[0]?.name)));
const header=nodes.find(node=>node.type==='CallExpression'&&node.arguments[1]?.type==='ObjectExpression'&&literal(prop(node.arguments[1],'className'))==='automation-categories');
const record={marker:'LWB317_AUTOMATION_CATEGORY_LIFETIME_RECOVERED',source:{path:sourcePath,sha256:hash(source)},grid:locator(grid),header:locator(header),declarations:declarations.map(locator),groups,probeGrid,probeChildrenSubstitution:replacement.map(({start,end})=>({start,end})),limits:'Only leaf children are replaced in probeGrid; exact original gates, Activity types, mode expressions, group order, category header/click handlers and state declarations are retained. Probe subscriptions/timers/dialogs establish the React boundary behavior, not the actual original leaf producer contracts.'};
fs.writeFileSync(path.join(here,'source-contract.json'),JSON.stringify(record,null,2)+'\n');
console.log(JSON.stringify({marker:record.marker,groups:groups.map(item=>item.category),gridByte:record.grid.utf8ByteOffset}));
