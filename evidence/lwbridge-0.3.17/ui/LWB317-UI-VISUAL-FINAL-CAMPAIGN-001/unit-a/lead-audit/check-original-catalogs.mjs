// Actual original string literals are independently parsed from all nine recovered assets.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../../../..');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser');
const assets=path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets');
const indexSource=fs.readFileSync(path.join(assets,'index-BVfnK1wp.js'),'utf8');
const indexAst=parse(indexSource,{sourceType:'module'});
const declarations=indexAst.program.body.filter(n=>n.type==='VariableDeclaration').flatMap(n=>n.declarations);
const data={};
for(const name of ['jr','Mr','Nr','Pr']){const node=declarations.find(n=>n.id.name===name);assert.ok(node);data[name]=new Function(`return (${indexSource.slice(node.init.start,node.init.end)});`)();}
const sharedNode=declarations.find(n=>n.id.name==='Fr');assert.ok(sharedNode);
const shared=new Function(...Object.keys(data),`return (${indexSource.slice(sharedNode.init.start,sharedNode.init.end)});`)(...Object.values(data));
const hashes=[],differences=[];let count=0;
for(const locale of ['en','ja','id','ko','pt','ru','vi','zh-CN','zh-TW']){
 const filename=fs.readdirSync(assets).find(f=>f.startsWith(locale+'-')&&f.endsWith('.js'));
 assert.ok(filename,locale+' actual original locale asset');
 const file=path.join(assets,filename),source=fs.readFileSync(file,'utf8');
 const ast=parse(source,{sourceType:'module'});
 const exported=ast.program.body.find(n=>n.type==='ExportNamedDeclaration')?.specifiers.find(s=>s.exported.name==='default')?.local.name;
 const object=ast.program.body.filter(n=>n.type==='VariableDeclaration').flatMap(n=>n.declarations).find(d=>d.id.name===exported)?.init;
 assert.equal(object?.type,'ObjectExpression');
 const original={};
 for(const prop of object.properties){if(prop.type==='SpreadElement'){assert.equal(prop.argument.type,'MemberExpression');const property=prop.argument.property;const spreadLocale=prop.argument.computed?(property.type==='TemplateLiteral'?property.quasis[0].value.cooked:property.value):property.name;assert.equal(spreadLocale,locale);Object.assign(original,shared[locale]);continue;}assert.equal(prop.type,'ObjectProperty');const key=prop.key.value||prop.key.name;const value=prop.value;assert.ok(value.type==='StringLiteral'||(value.type==='TemplateLiteral'&&value.expressions.length===0),'literal-only '+key);original[key]=value.type==='StringLiteral'?value.value:value.quasis[0].value.cooked;}
 const current=(await import(pathToFileURL(path.join(repo,`src/LWBridge.UI-0.3.17/src/locales/${locale}.js`)).href)).default;
 for(const key of Object.keys(original)){count++;if(current[key]!==original[key])differences.push({locale,key,original:original[key],current:current[key]});}
 hashes.push({locale,path:path.relative(repo,file).replaceAll('\\','/'),sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'),objectByteOffset:Buffer.byteLength(source.slice(0,object.start)),objectBytes:Buffer.byteLength(source.slice(object.start,object.end)),keys:Object.keys(original).length});
}
console.log(JSON.stringify({result:differences.length?'CHANGES_REQUIRED':'ORIGINAL_CATALOG_VALUES_EXACT',count,hashes,differences,sharedErrorLocator:{asset:'index-BVfnK1wp.js',byteOffset:Buffer.byteLength(indexSource.slice(0,sharedNode.init.start)),bytes:Buffer.byteLength(indexSource.slice(sharedNode.init.start,sharedNode.init.end)),dependencies:['jr','Mr','Nr','Pr']}},null,2));
process.exitCode=differences.length?1:0;
