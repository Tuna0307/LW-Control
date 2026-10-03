import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const { parse } = require('@babel/parser');
const { transformSync } = require('esbuild');
const source = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js'), 'utf8');
if (crypto.createHash('sha256').update(source).digest('hex') !== 'ed466e353aa896d8c3fc783ff1802930156fb9e9eb54327aeF73f2ff86fa06c7'.toLowerCase()) throw Error('Source hash mismatch');
const fn = parse(source, { sourceType: 'module' }).program.body.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'ye');
const ret = fn.body.body.find(n => n.type === 'ReturnStatement').argument;
const expression = source.slice(ret.start, ret.end);
const header = `// Join form and member confirmation renderer recovered from SquadPanel-HC3-DJei.js ye.
// Local data is supplied only by the fenced preview adapter; no native operation.
import { useEffect, useMemo, useState } from 'react';
import * as A from 'react/jsx-runtime';
import { useI18n } from './i18n.jsx';
import { validJoinRestrictions as se } from './previewAfkContracts.js';
import { previewMemberFixture } from './previewAfkCloseoutFixtures.js';

function JoinModal({ className, label, onClose, children }) {
  useEffect(() => {
    const close = event => { if (event.key === 'Escape') onClose(); };
    window.addEventListener('keydown', close);
    return () => window.removeEventListener('keydown', close);
  }, [onClose]);
  return A.jsx('div', {className, role: 'presentation', onMouseDown: event => {
    if (event.target === event.currentTarget) onClose();
  }, children: A.jsx('div', {role: 'dialog', 'aria-modal': true, 'aria-label': label, children})});
}

export function RallyJoinSettings({ value: e, onChange: t, disabled: r = false,
  label: i = 'squad.afkJoinConditions', previewState = '', ToggleRow }) {
  const { t: o } = useI18n();
  const fixture = previewMemberFixture(previewState);
  const n = fixture.online;
  const x = fixture.ready ? { membersReady: true, members: fixture.members, selfUid: fixture.selfUid } : null;
  const v = fixture.failed;
  const [f, p] = useState(false);
  const [m, h] = useState('');
  const [g, _] = useState(new Set());
  const S = x?.membersReady ? x.members.filter(member => member.uid !== x.selfUid) : [];
  const C = new Map((x?.membersReady ? x.members : []).map(member => [member.uid, member]));
  const T = e.leaders.filter(member => !C.has(member.uid));
  const E = m.trim().toLowerCase();
  const ee = S.filter(member => member.name.toLowerCase().includes(E) || member.uid.includes(E));
  const w = useMemo(() => ({label, ...props}) => {
    const {t} = useI18n();
    return A.jsx(ToggleRow, {label: t(label), ...props});
  }, [ToggleRow]);
  const y = JoinModal;
  return ${expression};
}
`;
const output = transformSync(header, { loader: 'js', format: 'esm', target: 'es2022' }).code;
if (process.argv.includes('--write')) fs.writeFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx'), output);
else {
  const current = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx'), 'utf8');
  if (current !== output) throw Error('Recovered renderer changed; re-review required');
}
console.log(JSON.stringify({result: 'RECOVERED_JOIN_RENDERER_OK', utf8ByteOffset: Buffer.byteLength(source.slice(0, ret.start))}));
