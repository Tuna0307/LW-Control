import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const assetPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const exePath = "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe";
const baselineCommit = "74ceaf78dc24c08068320dfc9766cf349faa1b7e";
const baselineSpec = `${baselineCommit}:src/LWBridge.UI-0.3.17/src/App.jsx`;
const baselinePath = path.join(here, "baseline-74ceaf7-App.jsx");
const originalSlicePath = path.join(here, "original-map-entry-byte363600-366200.js");
const manifestPath = path.join(here, "source-manifest.json");

const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const asset = fs.readFileSync(assetPath);
const exe = fs.readFileSync(exePath);
const baseline = execFileSync("git", ["cat-file", "blob", baselineSpec], { cwd: repo, encoding: null });

const tStart = asset.indexOf(Buffer.from("function Tt(e){"), 360000);
if (tStart !== 364377) throw new Error(`unexpected Tt offset ${tStart}`);
const tEndMarker = Buffer.from("function Et(){");
const tEnd = asset.indexOf(tEndMarker, tStart);
if (tEnd < 0) throw new Error("Tt end marker not found");
const tBytes = asset.subarray(tStart, tEnd);
const preloadStart = asset.lastIndexOf(Buffer.from("function Wi(e){"), tStart);
if (preloadStart < 0) throw new Error("Wi preload helper not found before Tt");
const refreshStart = asset.lastIndexOf(Buffer.from("async function _t("), tStart);
if (refreshStart < 0) throw new Error("_t summary helper not found before Tt");
const connectedPollNeedle = Buffer.from("map summary error `+String(e))}finally{e=!1}}};t();let n=window.setInterval");
const connectedPollOffset = asset.indexOf(connectedPollNeedle, tEnd);
if (connectedPollOffset < 0) throw new Error("connected summary poll effect not found");

fs.writeFileSync(baselinePath, baseline);
fs.writeFileSync(originalSlicePath, asset.subarray(363600, 366200));

const manifest = {
  task: "LWB317-UI-SHELL-MAP-ENTRY-001",
  dispatchCommit: baselineCommit,
  referenceExe: {
    path: exePath,
    bytes: exe.length,
    sha256: sha256(exe),
  },
  originalAsset: {
    path: path.relative(repo, assetPath).replaceAll("\\", "/"),
    bytes: asset.length,
    sha256: sha256(asset),
  },
  baselineApp: {
    spec: baselineSpec,
    path: path.relative(repo, baselinePath).replaceAll("\\", "/"),
    bytes: baseline.length,
    sha256: sha256(baseline),
    gitBlob: execFileSync("git", ["rev-parse", `${baselineCommit}:src/LWBridge.UI-0.3.17/src/App.jsx`], { cwd: repo, encoding: "utf8" }).trim(),
  },
  originalLocators: {
    mapEntryFunction: {
      name: "Tt",
      byteStart: tStart,
      byteEndExclusive: tEnd,
      bytes: tBytes.length,
      sha256: sha256(tBytes),
      utf8: tBytes.toString("utf8"),
    },
    summaryHelperByteStart: refreshStart,
    preloadHelperByteStart: preloadStart,
    connectedSummaryPollMarkerByteStart: connectedPollOffset,
    savedSlice: {
      byteStart: 363600,
      byteEndExclusive: 366200,
      path: path.relative(repo, originalSlicePath).replaceAll("\\", "/"),
      sha256: sha256(asset.subarray(363600, 366200)),
    },
  },
};

fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`LWB317_MAP_ENTRY_EXTRACT_OK Tt=${tStart}..${tEnd} baseline=${manifest.baselineApp.gitBlob}`);
