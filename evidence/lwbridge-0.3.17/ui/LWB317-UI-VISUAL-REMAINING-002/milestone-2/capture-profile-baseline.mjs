import crypto from 'node:crypto';
import {read} from '../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import {
  renderOriginalProfile, renderCurrentProfile, profileSummary, writeJson,
} from './full-afk-harness.mjs';

const hash = (value) => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const cases = [];
for (const language of ['en', 'ja']) {
  const original = profileSummary(renderOriginalProfile({language}));
  const current = profileSummary(renderCurrentProfile({language}));
  cases.push({
    id: `${language}-runtime`,
    original,
    current,
    mismatch: {
      runtimeRows: original.statusRows.length !== current.statusRows.length,
      dragGlyph: JSON.stringify(original.buttons.at(-1)) !== JSON.stringify(current.buttons.at(-1)),
    },
  });
  const originalBusy = profileSummary(renderOriginalProfile({language, busy: 'reorder'}));
  const currentSaving = profileSummary(renderCurrentProfile({language}));
  cases.push({
    id: `${language}-busy`,
    original: originalBusy,
    current: currentSaving,
    mismatch: {
      buttonDisabled: JSON.stringify(originalBusy.buttons.map((row) => row.disabled)) !== JSON.stringify(currentSaving.buttons.map((row) => row.disabled)),
      inputDisabled: JSON.stringify(originalBusy.inputs.map((row) => row.disabled)) !== JSON.stringify(currentSaving.inputs.map((row) => row.disabled)),
    },
  });
}

const report = {
  marker: 'LWB317_REMAINING_M2_PROFILE_FAILING_BASELINE',
  immutableBaseline: true,
  source: {
    asset: 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js',
    sha256: hash(read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js')),
    function: 'I', utf8ByteOffset: 28070, byteLength: 22554,
  },
  current: {
    path: 'src/LWBridge.UI-0.3.17/src/SquadsPage.jsx',
    sha256: hash(read('evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-2/baseline/pre-fix-SquadsPage.jsx')),
  },
  cases,
  expectedFailures: {
    runtimeRows: cases.filter((entry) => entry.mismatch.runtimeRows).length,
    busyControls: cases.filter((entry) => entry.mismatch.buttonDisabled || entry.mismatch.inputDisabled).length,
  },
  limits: 'Actual recovered full I and current AfkContent are executed with inert source-valid config/runtime inputs. Accepted compact/editor/Garrison/Zombie children are replaced only outside the profile card under comparison. No native/gameplay action is executed.',
  busyModel: 'The original case executes the explicit recovered reorder action busy branch. Current has no explicit delete/reorder action-busy owner, so the corresponding current card remains in its ordinary interactive state.',
};
if (report.expectedFailures.runtimeRows !== 2 || report.expectedFailures.busyControls !== 2) {
  throw new Error(`Unexpected failing-baseline shape ${JSON.stringify(report.expectedFailures)}`);
}
writeJson('profile-failing-baseline.json', report);
console.log(JSON.stringify({marker: report.marker, expectedFailures: report.expectedFailures}, null, 2));
