// Run only at the reviewed source checkpoint. Historical observations must not
// be repinned to a worker fix; use a new review directory for subsequent audits.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(here,'../../../../..');
const local='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-review-2026-10-07';
const packet='evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/recovery-002-2026-10-06';
const paths=[
  'src/LWBridge.Desktop/OverviewLifecycleService.cs','src/LWBridge.Desktop/OverviewLifecycleRecovery.cs',
  'src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs','src/LWBridge.Desktop/Map317CommandService.cs',
  'src/LWBridge.Map-0.3.17/MapScanStateMachine.cs','src/LWBridge.UI-0.3.17/src/App.jsx',
  'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx','src/LWBridge.UI-0.3.17/src/autoScanNativeCoordinator.js',
  ...['check-counterexamples.mjs','counterexamples.json','native-counterexamples/Program.cs',
    'native-counterexamples/results.json','runner-results.json'].map(p=>`${local}/${p}`),
  ...['package-en-light.json','package-ja-dark-narrow.json','package-en-light.png','package-ja-dark-narrow.png'].map(p=>`${packet}/${p}`),
];
const observations={
  date:'2026-10-07',reviewedHead:'59d3cde5192f03d39ae01580e05d1a6249158b20',decision:'CHANGES_REQUIRED',
  externalGameActions:0,actualNativeCapture:false,desktopSessionIndependentlyRerun:false,
  processInventory:'No LastWar/LWBridge process found before isolated execution',
  checks:['frontend check PASS','frontend build PASS','production-package check PASS',
    'focused Release compile PASS zero compiler warnings/errors','full Release Desktop compile/package PASS zero compiler warnings/errors',
    'canonical Map check PASS','nine focused isolated flags PASS','two frontend witnesses reproduce',
    'three controlled native/I-O witnesses reproduce','reference EXE matches AGENTS','historical previous lead packet unchanged'],
  uiSourceFingerprint:'cd86aec04987bf5683d38e27d05af5a4c26a773e3be65ea3dc5133463be2d150',
  uiArtifactFingerprint:'1ae836263c78106f014fcc44d23f9df5b6f2b22453f0462c9e968094419390c5',
  rebuiltAssemblyRevision:'59d3cde5192f03d39ae01580e05d1a6249158b20',
  captureImplementationRevision:'b2e6089f4f0a99be53087482f04a240ac59d22a4',
  binaryDifferenceAdjudication:'Rebuilt revision metadata differs; no worker source/package mismatch established',
  workerPackets:[
    {path:`${packet}/package-en-light.json`,screenshot:`${packet}/package-en-light.png`,language:'en',theme:'light',width:1120,height:720},
    {path:`${packet}/package-ja-dark-narrow.json`,screenshot:`${packet}/package-ja-dark-narrow.png`,language:'ja',theme:'dark',width:900,height:720},
  ],
  pins:paths.map(p=>({path:p,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex')})),
};
fs.writeFileSync(path.join(here,'observations.json'),JSON.stringify(observations,null,2)+'\n');
console.log('Recorded reviewed source/evidence observations');
