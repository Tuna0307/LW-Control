import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const campaign = path.resolve(here, "..");
const sourceManifestPath = path.join(campaign, "milestone-a/source-manifest.json");
const sourceManifest = JSON.parse(fs.readFileSync(sourceManifestPath, "utf8"));
const hash = (buffer) => crypto.createHash("sha256").update(buffer).digest("hex").toUpperCase();
const hashFile = (relativePath) => hash(fs.readFileSync(path.join(repo, relativePath)));

const expectedCurrent = {
  "src/LWBridge.UI-0.3.17/src/Pages.jsx": "25B0908A7FFC548047ED045C53E42BCAF1370044F6175DC7041AAA16DB75EA21",
  "src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js": "C0F81103BC85D3B27D7A952B13EB76173EA27393AEB72B0AD312A31C8AB6CDA7",
};

const expectedEvidence = {
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-a/baseline-results.json": "FD342E3A54C3BD24C0437C8F88758D771E5F1205364DBAE72C071B0925527DBD",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-a/inventory.json": "093B2C18F8628A46085F00A04303F16FAAC38BF774DA16B538A50008D79077D1",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-a/source-manifest.json": "E4479CFB706E0CADBC1139C0E4706D805CC7464F12AF63D1C2C00ABF054A1BB1",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-b/browser-results.json": "8FCF51AED206F1E024E0D6E083857B4B75F8541027B6704C32113865F3ADE4C8",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-b/results.json": "A9D7723F07432D8F9C57E9B36A543D756314D4994E94A9714D3D4B8A3BE5DCE3",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-c/browser-results.json": "6B03C4B2F3D7237B063D61ABE1ED36674713C831858793B08C084C39E3A37164",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-c/results.json": "74EC761EB56075903CB7D30116664CF5F1B8B39827D33EF0DC1969D7468B282C",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-d/browser-results.json": "C057B83BE9FF9622A8195C541566A73A607393BEFE6B3D6E8D14FD251CAED815",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-d/results.json": "57EE615498B9B2A222D240B63A52C3659D9AEA84C5E774E2D8D733C3FECE60A5",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-e/browser-results.json": "F975297F61652CE235C6AE11AD03CD88F2D61ED1D2727CCBA8A225C100C2C775",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-e/results.json": "31339EC56BD9E7B19DA0F4EB1B6826A85F79D793E9CF578C3E5BFD716CBFB97A",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-f/browser-results.json": "F5E46ABB1B9E0A4DF6D1C35ED90C961D593315BD53D28F820E5DA09476D5D601",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-f/closeout-results.json": "373E79368E76DCDF4CFAA653B408D9BF76918DA52DDD5CF4DB71870839099D5D",
};

const expectedScreenshots = {
  "city-layout-moved-en-light-wide.jpg": "54D02A6AD4130DF0846FEEE93082DFB27128321FEF9AB43C7F694A84889C8279",
  "city-layout-stale-ja-dark-narrow.jpg": "CB4640E6A82FC8027CD2E3E60233D5A858AD59903A8E15C594DBD8C19F4887A5",
  "city-layout-stale-ja-dark-wide.jpg": "9D6DAD4180DAE1B48D8627F324B6CF58A849CB49BB71972D6142B1D3B9486549",
  "hotkeys-disconnected-en-light-wide.jpg": "58ADAC5FE969E2B615CEBFE8C3B337D72E3775F49B0A91EC9F126F767AA63D7A",
  "hotkeys-save-error-ja-dark-narrow.jpg": "FF680971E9917F90D93404AC21FCA70890ED0CED5E1897176D9A523551B0BD4E",
  "hotkeys-save-error-ja-dark-wide.jpg": "ACE2489AD4235DB8AE96754E3B498D87C96C743869CFFDC039D59FB5DF293051",
  "mini-games-active-en-light-wide.jpg": "829F21145E9B3B79A49B7B101146DA7218B4FC5DF906F578D4DC7D20CFB67D5C",
  "mini-games-config-loading-ja-dark-narrow.jpg": "2C07CB4862CBEC27E5C7C7BB140C5C880691BE2DDD09F03AE3BC31D325251352",
  "mini-games-config-loading-ja-dark-wide.jpg": "009E3F1DB478C1CE146039BC98AA232CF36AE98478F453FDD4CBA326A3D63457",
  "settings-complete-en-light-wide.jpg": "10374AF073F63E3AACC8D065C2CE6D51F2618621C64584CA05455728E89F9A43",
  "settings-update-available-ja-dark-narrow.jpg": "6B7C9AA71A0F4F57DB65F468B8A8B3006C13D64A24B5CA4A7ABD183C93B86C88",
  "settings-update-available-ja-dark-wide.jpg": "48839095E2CAEBA802290E633EF525C1EEAF8DA446E93DBAB32DE5E89F27BC3D",
};

const exe = fs.readFileSync(sourceManifest.referenceExecutable);
const exeSha256 = hash(exe);
assert.equal(exeSha256, sourceManifest.referenceExecutableSha256, "Reference executable hash changed");

const assetRoot = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets");
const assetHashes = {};
for (const asset of sourceManifest.assets) {
  const absolute = path.join(repo, asset.path);
  const actual = hash(fs.readFileSync(absolute));
  assert.equal(actual, asset.sha256, `Recovered asset hash changed: ${asset.path}`);
  assetHashes[asset.path] = actual;
}

const locatorHashes = {};
for (const [name, locator] of Object.entries(sourceManifest.locators)) {
  const bytes = fs.readFileSync(path.join(assetRoot, locator.asset));
  const slice = bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.utf8ByteLength);
  const actual = hash(slice);
  assert.equal(actual, locator.sha256, `Recovered byte slice changed: ${name}`);
  locatorHashes[name] = actual;
}

const currentHashes = {};
for (const [relativePath, expected] of Object.entries(expectedCurrent)) {
  const actual = hashFile(relativePath);
  assert.equal(actual, expected, `Current production hash changed: ${relativePath}`);
  currentHashes[relativePath] = actual;
}

const evidenceHashes = {};
for (const [relativePath, expected] of Object.entries(expectedEvidence)) {
  const actual = hashFile(relativePath);
  assert.equal(actual, expected, `Pinned evidence hash changed: ${relativePath}`);
  evidenceHashes[relativePath] = actual;
}

const screenshotHashes = {};
for (const [name, expected] of Object.entries(expectedScreenshots)) {
  const relativePath = `evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001/milestone-f/screenshots/${name}`;
  const actual = hashFile(relativePath);
  assert.equal(actual, expected, `Screenshot hash changed: ${name}`);
  screenshotHashes[name] = actual;
}

const guard = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs");
const protectedWip = execFileSync(process.execPath, [guard], { cwd: repo, encoding: "utf8" }).trim();
assert.match(protectedWip, /LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7/);

const result = {
  result: "LWB317_REMAINING_PAGES_CURRENT_INTEGRITY_OK",
  referenceExecutableSha256: exeSha256,
  recoveredAssets: assetHashes,
  recoveredSlices: locatorHashes,
  currentProduct: currentHashes,
  evidence: evidenceHashes,
  screenshots: screenshotHashes,
  protectedWip,
};

fs.writeFileSync(path.join(here, "validation-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify({
  result: result.result,
  referenceExecutableSha256: result.referenceExecutableSha256,
  recoveredAssets: Object.keys(assetHashes).length,
  recoveredSlices: Object.keys(locatorHashes).length,
  currentProductFiles: Object.keys(currentHashes).length,
  evidenceFiles: Object.keys(evidenceHashes).length,
  screenshots: Object.keys(screenshotHashes).length,
  protectedWip,
}, null, 2));
