import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const source = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-PM-UI-FINISH-001/auto-regression-rerun.json");
const target = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/regression-results.json");
const expectedLength = 6793;
const expectedSha256 = "4118E07D74304963C8093EC9305D0D178A94A3F75C48D3C6A87059AE3F80D70C";
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();

const preserved = fs.readFileSync(source, "utf8");
assert.equal((preserved.match(/338\.5/g) || []).length, 1, "preserved rerun has one elapsed-seconds value");
let recovered = null;
let recoveredSeconds = null;
for (let tenths = 1000; tenths <= 9999; tenths += 1) {
  const seconds = (tenths / 10).toFixed(1);
  const candidate = Buffer.from(preserved.replace("338.5", seconds), "utf8");
  if (candidate.length === expectedLength && hash(candidate) === expectedSha256) {
    recovered = candidate;
    recoveredSeconds = seconds;
    break;
  }
}
assert.ok(recovered, "protected assignment-start Auto regression bytes are recoverable from preserved rerun");
fs.writeFileSync(target, recovered);
assert.equal(fs.readFileSync(target).length, expectedLength);
assert.equal(hash(fs.readFileSync(target)), expectedSha256);
console.log(JSON.stringify({ marker: "LWB317_MAP_TOOLBAR_PROTECTED_WIP_RECOVERED", seconds: recoveredSeconds, length: expectedLength, sha256: expectedSha256 }));
