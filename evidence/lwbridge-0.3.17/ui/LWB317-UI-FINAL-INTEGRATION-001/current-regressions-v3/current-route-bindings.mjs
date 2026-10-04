import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../../../..');
const requireUi=createRequire(path.join(root,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=requireUi('@babel/parser');
const source=fs.readFileSync(path.join(root,'src/LWBridge.UI-0.3.17/src/Pages.jsx'),'utf8');
const ast=parse(source,{sourceType:'module',plugins:['jsx']});
const nodes=ast.program.body.map(node=>node.type==='ExportNamedDeclaration'?node.declaration:node);
const preload=nodes.find(node=>node.type==='FunctionDeclaration'&&node.id.name==='preloadRoute');
const loaders=nodes.flatMap(node=>node.type==='VariableDeclaration'?node.declarations:[]).find(node=>node.id.name==='routeLoaders');
assert.ok(preload&&loaders?.init.type==='ObjectExpression');
// The actual preload dispatcher is compiled unchanged. Only its import-promise
// transport boundary is inert in these existing App callback/effect assertions.
// Real deferred loader/Suspense behavior is mounted in route-loading's proof.
const routeLoaders=Object.fromEntries(loaders.init.properties.map(property=>[
  property.key.name||property.key.value,
  property.value.name==='undefined'?undefined:()=>Promise.resolve({}),
]));
export const preloadRoute=new Function('routeLoaders',`${source.slice(preload.start,preload.end)};return preloadRoute;`)(routeLoaders);
