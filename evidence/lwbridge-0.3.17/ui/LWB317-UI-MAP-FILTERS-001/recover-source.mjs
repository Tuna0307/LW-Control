import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
export function walk(node, list = []) {
  if (!node || typeof node !== 'object') return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(item => walk(item, list));
    else if (value && typeof value === 'object') walk(value, list);
  }
  return list;
}
const assetRoot = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/';
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
function asset(name, expected) {
  const relative = assetRoot + name;
  const bytes = fs.readFileSync(path.join(repo, relative));
  assert.equal(hash(bytes), expected);
  const text = bytes.toString('utf8');
  const nodes = walk(parse(text, { sourceType: 'module' }));
  const record = node => ({ byte: Buffer.byteLength(text.slice(0, node.start)), expression: text.slice(node.start, node.end) });
  return { relative, text, nodes, record, sha256: hash(bytes) };
}
const map = asset('MapDataPanel-B4GXEND2.js', 'ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089');
const index = asset('index-BVfnK1wp.js', '44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6');
const component = map.nodes.find(n => n.type === 'FunctionDeclaration' && n.id?.name === 'R');
const componentNodes = walk(component);
const functionNode = name => map.nodes.find(n => n.type === 'FunctionDeclaration' && n.id?.name === name);
const helpers = Object.fromEntries(['Ue', 'A', 'We', 've', 'nr', 'rr', 'He'].map(name => [name, map.record(functionNode(name))]));
const sets = Object.fromEntries(['Re','O'].map(name => [name, map.record(map.nodes.find(n => n.type === 'VariableDeclarator' && n.id?.name === name))]));
const states = {};
for (const name of ['It','Rt','Ht','Wt','jn']) {
  states[name] = map.record(componentNodes.find(n => n.type === 'VariableDeclarator' && n.id.type === 'ArrayPattern' && n.id.elements[0]?.name === name));
}
const handlers = {};
for (const [name, marker] of Object.entries({ quality:'Lt(', item:'zt(', completion:'Ut(', plunderable:'Gt(' })) {
  const node = componentNodes.find(n => n.type === 'ObjectProperty' && n.key.name === 'onChange' && n.value.type === 'ArrowFunctionExpression' && map.text.slice(n.value.start,n.value.end).includes(marker));
  assert.ok(node, name);
  handlers[name] = map.record(node.value);
}
const query = helpers.nr;
const tableCaller = map.record(componentNodes.find(n => n.type === 'ObjectProperty' && n.key.name === 'treasureStatesRefreshing'));
const treasureEffect = componentNodes.find(n => n.type === 'CallExpression' && map.text.slice(n.callee.start,n.callee.end).includes('useEffect') && map.text.slice(n.start,n.end).includes('Mn(!0)'));
const dependencies = {};
for (const exported of ['B','nt','tt']) {
  const specifier = index.nodes.find(n => n.type === 'ExportSpecifier' && n.exported.name === exported);
  assert.ok(specifier, exported);
  const local = specifier.local.name;
  const node = index.nodes.find(n => n.type === 'FunctionDeclaration' && n.id?.name === local) || index.nodes.find(n => n.type === 'VariableDeclarator' && n.id?.name === local);
  assert.ok(node, local);
  dependencies[exported] = { local, ...index.record(node) };
}
const result = {
  evidenceState:'EXACT_BYTES', scope:'Local frontend source only; no service or gameplay execution',
  source:{path:map.relative,sha256:map.sha256}, states, sets, helpers, handlers,
  treasure:{tableCaller,effect:map.record(treasureEffect),search:helpers.rr,index:{path:index.relative,sha256:index.sha256},dependencies},
};
fs.writeFileSync(path.join(here,'source-contract.json'), JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
