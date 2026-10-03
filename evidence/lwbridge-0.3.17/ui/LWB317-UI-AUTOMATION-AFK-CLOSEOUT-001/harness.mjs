import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
export const here = path.dirname(fileURLToPath(import.meta.url));
export const repo = path.resolve(here, '../../../..');
export const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
export const {parse} = require('@babel/parser');
export const {transformSync} = require('esbuild');
export const read = file => fs.readFileSync(path.join(repo,file),'utf8');
export const squad = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js');
export const automation = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js');
export const pages = read('src/LWBridge.UI-0.3.17/src/Pages.jsx');
export function nodes(source) {
 const out=[];
 function walk(n) { if(!n||typeof n!=='object')return; if(n.type)out.push(n); for(const v of Object.values(n))if(Array.isArray(v))v.forEach(walk);else if(v&&typeof v==='object')walk(v); }
 walk(parse(source,{sourceType:'module',plugins:['jsx']})); return out;
}
const astCache=new Map();
export const fn = (source,name) => {
 let ast=astCache.get(source);
 if(!ast){ast=parse(source,{sourceType:'module',plugins:['jsx']});astCache.set(source,ast);}
 return ast.program.body.map(n=>n.type==='ExportNamedDeclaration'?n.declaration:n).find(n=>n?.type==='FunctionDeclaration'&&n.id.name===name)
   || nodes(source).find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);
};
export const raw = (source,node) => source.slice(node.start,node.end);
export const evaluate = (code,env={}) => new Function(...Object.keys(env),`return (${code});`)(...Object.values(env));
export function compile(source,name,env={}) {
 const code=transformSync(raw(source,fn(source,name)),{loader:'jsx',jsxFactory:'h',jsxFragment:'Fragment'}).code;
 return new Function(...Object.keys(env),code+`\nreturn ${name};`)(...Object.values(env));
}
export const h = (type,props,...children) => ({type,props:{...props,children: children.length ? children : props?.children}});
export const Fragment = 'Fragment';
export const jsx = {jsx:(type,props)=>({type,props}),jsxs:(type,props)=>({type,props}),Fragment};
export function flatten(tree) { if(tree==null||typeof tree==='boolean')return []; if(Array.isArray(tree))return tree.flatMap(flatten); if(typeof tree!=='object')return []; return [tree,...flatten(tree.props?.children)]; }
export function text(tree) { if(tree==null||typeof tree==='boolean')return ''; if(Array.isArray(tree))return tree.map(text).join(''); if(typeof tree==='object')return text(tree.props?.children); return String(tree); }
export function stable(tree) {
 if(tree==null||typeof tree==='boolean')return null;
 if(Array.isArray(tree))return tree.map(stable).flat().filter(n=>n!==null);
 if(typeof tree!=='object')return String(tree);
 if(typeof tree.type==='function') {
   if(tree.type.name==='JoinModal')return stable({...tree,type:'Modal'});
   return stable(tree.type(tree.props));
 }
 const props={}; for(const [key,value] of Object.entries(tree.props||{}))if(key!=='children'&&key!=='key'&&typeof value!=='function'&&value!==undefined)props[key]=value;
 return {type:tree.type,props,children:stable([tree.props?.children])};
}
export function hooks(seed=[]) {
 const values=[...seed]; let cursor=0;
 const useState = initial => { const index=cursor++; if(!(index in values))values[index]=typeof initial==='function'?initial():initial; return [values[index], next=>values[index]=typeof next==='function'?next(values[index]):next]; };
 return {values,useState,useEffect:()=>{},useMemo:fn=>fn(),useRef:initial=>{const index=cursor++;return values[index]??(values[index]={current:initial});},begin:()=>cursor=0};
}
if(process.argv.includes('--inventory')){
 console.log(JSON.stringify(nodes(automation).filter(n=>n.type==='CallExpression'&&n.arguments[1]?.type==='ObjectExpression').map(n=>n.arguments[1]).filter(n=>n.properties.some(p=>p.key?.name==='title')).map(n=>({offset:Buffer.byteLength(automation.slice(0,n.start)),props:Object.fromEntries(n.properties.filter(p=>['title','statusRows','summary','summaryRows','state'].includes(p.key?.name)).map(p=>[p.key.name,raw(automation,p.value)]))})),null,2));
}
