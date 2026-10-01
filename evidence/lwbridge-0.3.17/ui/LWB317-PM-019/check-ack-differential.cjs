const fs = require('fs');
const path = require('path');
const { createRequire } = require('module');
const assert = require('assert/strict');
const crypto = require('crypto');
const repo = path.resolve(__dirname, '../../../..');
const req = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = req('@babel/parser');
const src = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'), 'utf8');
const app = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/App.jsx'), 'utf8').replace(/\r\n/g, '\n');
const sha256 = text => crypto.createHash('sha256').update(text).digest('hex').toUpperCase();
assert.equal(sha256(src), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
function walk(x, out = []) {
  if (!x || typeof x !== 'object') return out;
  if (x.type) out.push(x);
  for (const y of Object.values(x)) {
    if (Array.isArray(y)) y.forEach(z => walk(z, out));
    else if (y && typeof y === 'object') walk(y, out);
  }
  return out;
}
const jt = walk(parse(src, { sourceType: 'module' })).find(n => n.type === 'FunctionDeclaration' && n.id?.name === 'Jt' && Buffer.byteLength(src.slice(0, n.start)) === 367489);
assert.ok(jt);
const actual = walk(parse(app, { sourceType: 'module', plugins: ['jsx'] })).find(n => n.type === 'VariableDeclarator' && n.id?.name === 'acknowledgeGameRootStatus').init.arguments[0];
const expressions = { original: src.slice(jt.start, jt.end), current: app.slice(actual.start, actual.end) };
const results = [];
for (const payload of [{ valid: true, root: 'C:\\LastWar' }, { valid: false, root: '' }, null, { valid: true, root: 'other', additional: 'retained' }]) {
  function run(original) {
    const trace = [];
    const state = { root: { valid: false }, rootError: 'ROOT_PRIOR', actionError: 'ACTION_PRIOR' };
    const set = value => { state.root = value; trace.push('status'); };
    const clear = value => { state.rootError = value; trace.push('root-error'); };
    const fn = original
      ? new Function('g', 'x', expressions.original + ';return Jt;')(set, clear)
      : new Function('setGameRootStatus', 'setGameRootError', 'return (' + expressions.current + ');')(set, clear);
    fn(payload);
    return { state, trace };
  }
  const current = run(false);
  assert.deepEqual(current, run(true));
  assert.equal(current.state.actionError, 'ACTION_PRIOR');
  results.push({ payload, current });
}
const report = { task: 'LWB317-PM-019', result: 'PM019_INDEPENDENT_ACK_DIFFERENTIAL_OK', cases: results.length, sourceSha256: sha256(src), appSha256NormalizedLF: sha256(app), expressions, results, limits: 'Extracted acknowledgement behavior with synthetic payloads only; no native invocation.' };
if (process.argv.includes('--record')) fs.writeFileSync(path.join(__dirname, 'ack-results.json'), JSON.stringify(report, null, 2) + '\n');
if (process.argv.includes('--verify-record')) assert.deepEqual(report, JSON.parse(fs.readFileSync(path.join(__dirname, 'ack-results.json'), 'utf8')));
console.log(JSON.stringify({ result: report.result, cases: report.cases }));
