import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const relative = file => path.relative(repo, file).replaceAll('\\', '/');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const artifactHash = (file, bytes) => hash(/\.(png|jpg)$/.test(file) ? bytes : bytes.toString('utf8').replaceAll('\r\n','\n'));
const dimensions = bytes => {
  if (bytes.subarray(1,4).toString() === 'PNG') return { imageWidth: bytes.readUInt32BE(16), imageHeight: bytes.readUInt32BE(20), format: 'PNG' };
  assert.equal(bytes.readUInt16BE(0), 0xffd8);
  let offset = 2;
  while (offset < bytes.length) {
    assert.equal(bytes[offset++], 0xff);
    while (bytes[offset] === 0xff) offset++;
    const marker = bytes[offset++];
    const length = bytes.readUInt16BE(offset);
    if ([0xc0,0xc1,0xc2,0xc3,0xc5,0xc6,0xc7,0xc9,0xca,0xcb,0xcd,0xce,0xcf].includes(marker)) return { imageWidth: bytes.readUInt16BE(offset+5), imageHeight: bytes.readUInt16BE(offset+3), format: 'JPEG' };
    offset += length;
  }
  throw new Error('Missing JPEG size');
};
const walk = dir => fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
  const file = path.join(dir, entry.name);
  return entry.isDirectory() ? walk(file) : [file];
});
const productNames = ['App.jsx','Pages.jsx','DispatchAssistManual.jsx','MapDataPage.jsx','MapRetainedGoodsFilter.jsx','ScheduledPlunder.jsx','i18n.jsx','AppExitDialog.jsx','GameAssetImage.jsx','ProfileSidebar.jsx','ProfileSwitchState.jsx','ShellPresentation.jsx','shellState.js','shellTheme.js','assets/icon-warning.png'];
const productFiles = productNames.map(name => path.join(repo, 'src/LWBridge.UI-0.3.17/src', name));
const evidenceFiles = walk(here).filter(file => {
  const local = path.relative(here, file).replaceAll('\\', '/');
  return !/^(route-loading|motion|current-regressions-v2)\//.test(local) && !['checkpoint-manifest.json','validate-checkpoint.mjs'].includes(local);
});
const manifestPath = path.join(here, 'checkpoint-manifest.json');
if (process.argv.includes('--record')) {
  const manifest = {
    scope: 'Shared UI checkpoint only. Subsequent route/motion work uses separate proof. Hashes pin bytes, not global parity.',
    referenceExe: { path: 'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe', sha256: '4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783' },
    hashPolicy: 'Images use exact bytes. UTF-8 text uses LF normalization to survive configured Git CRLF checkout.',
    files: [...productFiles, ...evidenceFiles].sort().map(file => ({ path: relative(file), sha256: artifactHash(file, fs.readFileSync(file)) })),
    images: evidenceFiles.filter(file => /\.(png|jpg)$/.test(file)).map(file => {
      const bytes = fs.readFileSync(file);
      return { path: relative(file), ...dimensions(bytes), units: 'Image pixels; CSS viewports recorded separately' };
    })
  };
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
}
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const revisionIndex = process.argv.indexOf('--revision');
const revision = revisionIndex >= 0 ? process.argv[revisionIndex + 1] : null;
assert.equal(hash(fs.readFileSync(manifest.referenceExe.path)), manifest.referenceExe.sha256);
for (const file of manifest.files) {
  const bytes = revision ? execFileSync('git', ['show', `${revision}:${file.path}`], { cwd: repo, maxBuffer: 8 * 1024 * 1024 }) : fs.readFileSync(path.join(repo, file.path));
  assert.equal(artifactHash(file.path, bytes), file.sha256, file.path);
  if (file.path.endsWith('.json')) JSON.parse(bytes.toString('utf8'));
}
assert.ok(manifest.images.length >= 8);
console.log(`LWB317_FINAL_SHARED_CHECKPOINT_OK files=${manifest.files.length} screenshots=${manifest.images.length} revision=${revision || 'working'}`);
