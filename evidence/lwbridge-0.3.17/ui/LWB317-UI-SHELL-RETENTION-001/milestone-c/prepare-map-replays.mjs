// Preserve worker continuation bytes and adapt only their dependency locations.
// Historical packet files are read-only inputs; all outputs stay in this packet.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const sourceDir = path.resolve(here, '../../LWB317-UI-MAP-AUTO-CONFIG-001');
const snapshots = path.join(here, 'continuation-snapshots');
fs.mkdirSync(snapshots, { recursive: true });
const files = ['app-harness.mjs', 'regression-results.json', 'regression-refresh-ownership.json'];
const manifest = [];
for (const file of files) {
  const bytes = fs.readFileSync(path.join(sourceDir, file));
  const snapshot = path.join(snapshots, file);
  if (!fs.existsSync(snapshot)) fs.writeFileSync(snapshot, bytes);
  const preserved = fs.readFileSync(snapshot);
  manifest.push({ file, sha256: crypto.createHash('sha256').update(preserved).digest('hex'), bytes: preserved.length });
}
fs.writeFileSync(path.join(snapshots, 'manifest.json'), JSON.stringify({ purpose: 'Worker continuation bytes before restoring historical packet; not revised historical evidence.', files: manifest }, null, 2) + '\n');
let harness = fs.readFileSync(path.join(snapshots, 'app-harness.mjs'), 'utf8');
harness = harness.replaceAll('../LWB317-UI-MAP-REFRESH-FEEDBACK-001/', '../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/').replaceAll('../../../../src/', '../../../../../src/').replace('path.resolve(here, "../../../..")', 'path.resolve(here, "../../../../..")');
fs.writeFileSync(path.join(here, 'app-ownership-harness.mjs'), '// Shell-local location adapter of preserved continuation-snapshots/app-harness.mjs. Activity is inert; this proves App ownership only.\n' + harness);
let checker = fs.readFileSync(path.join(sourceDir, 'check-refresh-ownership-current.mjs'), 'utf8');
checker = checker.replaceAll('../LWB317-UI-MAP-REFRESH-FEEDBACK-001/', '../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/').replaceAll('../../../../src/', '../../../../../src/').replace('"./app-harness.mjs"', '"./app-ownership-harness.mjs"').replaceAll('regression-refresh-ownership.json', 'map-ownership-results.json');
fs.writeFileSync(path.join(here, 'replay-map-ownership.mjs'), '// Shell-local location/output adapter of maintained Auto check-refresh-ownership-current.mjs; historical inputs/results remain unchanged.\n' + checker);
console.log('LWB317_SHELL_MAP_REPLAYS_PREPARED');
