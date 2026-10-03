import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const assetPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const baselineSpec = "55c9ca118d31a9fcdbc0ec0cf86930474303930e:src/LWBridge.UI-0.3.17/src/App.jsx";
const baselinePath = path.join(here, "baseline-55c9ca1-App.jsx");
const slicePath = path.join(here, "original-marker-slices.json");
const identityPath = path.join(here, "identity.json");
const popoverSlicePath = path.join(here, "original-popover-byte349300-355100.js");
const parentSlicePath = path.join(here, "original-parent-byte362000-373700.js");
const apiSlicePath = path.join(here, "original-api-byte204900-207400.js");

const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const asset = fs.readFileSync(assetPath);
const baseline = execFileSync("git", ["cat-file", "blob", baselineSpec], { cwd: repo, encoding: null });
fs.writeFileSync(baselinePath, baseline);
fs.writeFileSync(popoverSlicePath, asset.subarray(349300, 355100));
fs.writeFileSync(parentSlicePath, asset.subarray(362000, 373700));
fs.writeFileSync(apiSlicePath, asset.subarray(204900, 207400));

const markers = [
  "lastwar.serverJumpHistory",
  "server_jump_history_import",
  "server_jump_history_set",
  "server_jump",
  "server.switchLabelWithId",
  "server.dialogLabel",
  "server.targetPlaceholder",
  "server.seasonServers",
  "server.plunderableServers",
  "server.recent",
  "server.stopScanFirst",
  "server.returnHome",
  "onJumpServer",
  "scanActive",
  "map_summary",
  "map_scan_status",
  "server_jump_history_import",
];

const slices = [];
for (const marker of markers) {
  const needle = Buffer.from(marker, "utf8");
  let cursor = 0;
  let occurrence = 0;
  while (true) {
    const offset = asset.indexOf(needle, cursor);
    if (offset < 0) break;
    occurrence += 1;
    const start = Math.max(0, offset - 1400);
    const end = Math.min(asset.length, offset + needle.length + 1800);
    slices.push({
      marker,
      occurrence,
      offset,
      start,
      end,
      utf8: asset.subarray(start, end).toString("utf8"),
    });
    cursor = offset + needle.length;
  }
}

fs.writeFileSync(slicePath, JSON.stringify({ assetPath: path.relative(repo, assetPath).replaceAll("\\", "/"), slices }, null, 2) + "\n");
fs.writeFileSync(identityPath, JSON.stringify({
  asset: {
    path: path.relative(repo, assetPath).replaceAll("\\", "/"),
    sha256: sha256(asset),
    bytes: asset.length,
  },
  baseline: {
    spec: baselineSpec,
    path: path.relative(repo, baselinePath).replaceAll("\\", "/"),
    sha256: sha256(baseline),
    bytes: baseline.length,
  },
}, null, 2) + "\n");

console.log(`asset_sha256=${sha256(asset)}`);
console.log(`baseline_sha256=${sha256(baseline)}`);
console.log(`markers=${slices.length}`);
