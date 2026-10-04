import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { initialProfileFocus, saveProfileFocus } from '../../../../src/LWBridge.UI-0.3.17/src/shellState.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const original = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'), 'utf8');
const begin = original.indexOf('var li=`lwbridge.focusGameOnProfileSelect`');
const end = original.indexOf('function fi()', begin);
assert.ok(begin >= 0 && end > begin);
const source = original.slice(begin, end);
const recovered = new Function(source + ';return {ui,di};')();
let cases = 0;
for (const value of [null, '', 'false', 'true', '0', 'FALSE', 'garbage']) {
  const storage = { getItem: () => value };
  assert.equal(initialProfileFocus(storage), recovered.ui(storage)); cases++;
}
for (const value of [true, false]) {
  const trace = [];
  recovered.di({ setItem: (...args) => trace.push(args) }, value);
  const current = [];
  saveProfileFocus({ setItem: (...args) => current.push(args) }, value);
  assert.deepEqual(current, trace); cases++;
}
const app = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/App.jsx'), 'utf8');
assert.match(app, /showProfileFocus: showProfiles/);
assert.match(app, /onFocusGameOnProfileSelectChange: updateProfileFocus/);
assert.match(app, /ProfileSidebar state=\{shellProfiles\} focusGameOnProfileSelect=\{focusGameOnProfileSelect\}/);
const hash = text => crypto.createHash('sha256').update(text).digest('hex');
fs.writeFileSync(path.join(here, 'profile-focus-results.json'), JSON.stringify({result:'LWB317_FINAL_PROFILE_FOCUS_OK', cases,
  locator:{utf8ByteOffset:Buffer.byteLength(original.slice(0,begin)),utf8ByteLength:Buffer.byteLength(source),sha256:hash(source)},
  appSha256:hash(app), proof:'Exact helpers, parent binding assertions and separate real browser toggle/reload. No game-window focus action.'},null,2)+'\n');
console.log('LWB317_FINAL_PROFILE_FOCUS_OK cases='+cases);
