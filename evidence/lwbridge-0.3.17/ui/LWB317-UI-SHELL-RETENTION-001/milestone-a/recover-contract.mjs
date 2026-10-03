import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const bundlePath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const exePath = "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe";
const baselineCommit = "35b5cab9ce42eb8514145ce815af0432e48e4c10";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";

const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const bundle = fs.readFileSync(bundlePath);

assert.equal(sha256(fs.readFileSync(exePath)), "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
assert.equal(sha256(bundle), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function uniqueOffset(anchor) {
  const needle = Buffer.from(anchor, "utf8");
  const first = bundle.indexOf(needle);
  assert.notEqual(first, -1, `Missing source anchor: ${anchor}`);
  assert.equal(bundle.indexOf(needle, first + 1), -1, `Source anchor is not unique: ${anchor}`);
  return { first, needle };
}

function exactLocator(name, text) {
  const { first, needle } = uniqueOffset(text);
  return { name, utf8ByteOffset: first, utf8ByteLength: needle.length, sha256: sha256(needle) };
}

function rangedLocator(name, startAnchor, endAnchor, includeEnd = false) {
  const start = uniqueOffset(startAnchor).first;
  const endNeedle = Buffer.from(endAnchor, "utf8");
  const endOffset = bundle.indexOf(endNeedle, start + Buffer.byteLength(startAnchor));
  assert.notEqual(endOffset, -1, `${name}: missing end anchor`);
  const end = endOffset + (includeEnd ? endNeedle.length : 0);
  const bytes = bundle.subarray(start, end);
  return { name, utf8ByteOffset: start, utf8ByteLength: bytes.length, sha256: sha256(bytes) };
}

const appBlob = execFileSync("git", ["rev-parse", `${baselineCommit}:${appPath}`], { cwd: repo, encoding: "utf8" }).trim();
assert.equal(appBlob, "0af21cf0500ef28037f5d4f4c7a52813f58289bc");
const baselineApp = execFileSync("git", ["show", `${baselineCommit}:${appPath}`], { cwd: repo });

const manifest = {
  workItem: "LWB317-UI-SHELL-RETENTION-001",
  referenceExecutable: exePath,
  referenceExecutableSha256: sha256(fs.readFileSync(exePath)),
  originalShellAsset: path.relative(repo, bundlePath).replaceAll("\\", "/"),
  originalShellAssetSha256: sha256(bundle),
  baseline: {
    commit: baselineCommit,
    appPath,
    appGitBlob: appBlob,
    appSha256FromGitBlobBytes: sha256(baselineApp),
  },
  locators: [
    rangedLocator("shell.routeCatalog", "var Zr=[{key:`overview`", "function Qr("),
    exactLocator("shell.initialActiveAndVisited", "[i,a]=(0,b.useState)(`overview`),[o,s]=(0,b.useState)(()=>new Set([`overview`])),[,c]=(0,b.useTransition)()"),
    rangedLocator("shell.lazyPanelPreloadMap", "Ui={advanced:void 0,automation:Mi", "function Gi({initialTheme:e=_i()}){"),
    rangedLocator("shell.selectRoute", "function Tt(e){e!==i", "function Et(){"),
    rangedLocator("shell.retainedVisitedActivitiesAndProfileKey", "o.has(`overview`)", "]})]},r.selectedProfileId)})", true),
  ],
};

fs.writeFileSync(path.join(here, "source-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`LWB317_SHELL_SOURCE_CONTRACT_OK locators=${manifest.locators.length}`);
