import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../..');
const relative = file => path.relative(repo, file).replaceAll('\\', '/');
const sourcePacket = path.resolve(here, '../../LWB317-UI-MAP-AUTO-CONFIG-001');
const protectedPaths = ['app-harness.mjs','regression-results.json','regression-refresh-ownership.json'].map(file => path.join(sourcePacket,file)).concat([path.resolve(here, '../../LWB317-UI-MAP-NAVIGATION-001/navigation-results.json'),path.resolve(here, '../../LWB317-UI-MAP-INTERACTIONS-001/request-lifetime-results.json')]);
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const before = protectedPaths.map(file => ({ path: relative(file), sha256: sha(file) }));
const scripts = ['evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs',relative(path.join(sourcePacket,'replay-navigation-current.mjs')),relative(path.join(here,'replay-map-ownership.mjs'))];
const results = [];
for (const script of scripts) {
  const run = spawnSync(process.execPath,[script],{cwd:repo,encoding:'utf8'});
  results.push({script,exitCode:run.status,stdout:run.stdout.trim(),stderr:run.stderr.trim()});
  assert.equal(run.status,0,`${script}\n${run.stdout}\n${run.stderr}`);
}
for (const entry of before) assert.equal(sha(path.join(repo,entry.path)),entry.sha256,`Historical bytes changed: ${entry.path}`);
const report = { status:'PASS', scope:'Minimal current request disposal, maintained navigation adapter and shell-local App/controlled Map ownership replay. No browser/native execution; Activity remains inert in ownership adapter. Scheduled campaign not rerun.', historicalBytesUnchanged:before, results };
fs.writeFileSync(path.join(here,'map-replay-results.json'),JSON.stringify(report,null,2)+'\n');
console.log('LWB317_SHELL_MAP_REPLAYS_OK count=3');
