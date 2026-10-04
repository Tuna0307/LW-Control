import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const sourcePath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const source = fs.readFileSync(sourcePath);
const text = source.toString("utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(
  sha256(source),
  "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6",
  "recovered main asset hash drifted",
);

function locate(exact) {
  const bytes = Buffer.from(exact, "utf8");
  const offset = source.indexOf(bytes);
  assert.ok(offset >= 0, `missing recovered source slice: ${exact.slice(0, 80)}`);
  return {
    utf8ByteOffset: offset,
    byteLength: bytes.length,
    sha256: sha256(bytes),
    text: exact,
  };
}

const storageContract = "var br=`lwbridge.autoLaunchGame`;function xr(e=localStorage){return e.getItem(br)!==`false`}function Sr(e=localStorage,t){e.setItem(br,String(t))}";
const stateInitializer = "[f,p]=(0,b.useState)(xr)";
const setter = "setAutoLaunchGame:e=>{Sr(localStorage,e),p(e)}";
const homeBinding = "(0,M.jsx)(Bn,{label:`auth.autoLaunchGame`,checked:l,onChange:d})";
const switchStart = "function Bn({label:e,checked:t,disabled:n=!1,variant:r=`switch`,onChange:i})";
const switchOffset = source.indexOf(Buffer.from(switchStart, "utf8"));
assert.equal(switchOffset, 213332, "recovered Bn locator changed");

const storage = new Map();
const fakeStorage = {
  getItem(key) { return storage.has(key) ? storage.get(key) : null; },
  setItem(key, value) { storage.set(key, String(value)); },
};
const { get, set } = new Function(
  `${storageContract}; return { get: xr, set: Sr };`,
)();

assert.equal(get(fakeStorage), true, "missing key must default true");
fakeStorage.setItem("lwbridge.autoLaunchGame", "false");
assert.equal(get(fakeStorage), false, "literal false must reload false");
fakeStorage.setItem("lwbridge.autoLaunchGame", "true");
assert.equal(get(fakeStorage), true, "literal true must reload true");
fakeStorage.setItem("lwbridge.autoLaunchGame", "anything-else");
assert.equal(get(fakeStorage), true, "only literal false disables the preference");
set(fakeStorage, false);
assert.equal(fakeStorage.getItem("lwbridge.autoLaunchGame"), "false");
set(fakeStorage, true);
assert.equal(fakeStorage.getItem("lwbridge.autoLaunchGame"), "true");

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A",
  date: "2026-10-04",
  classification: "EXACT_BYTES / EXACT_CONTRACT",
  source: {
    path: path.relative(repo, sourcePath).replaceAll("\\", "/"),
    sha256: sha256(source),
    storageContract: locate(storageContract),
    key: {
      value: "lwbridge.autoLaunchGame",
      utf8ByteOffset: source.indexOf(Buffer.from("lwbridge.autoLaunchGame", "utf8")),
    },
    stateInitializer: locate(stateInitializer),
    setter: locate(setter),
    homeBinding: locate(homeBinding),
    switchComponent: { utf8ByteOffset: switchOffset },
  },
  contract: {
    defaultWhenMissing: true,
    falseEncoding: "false",
    trueEncoding: "true",
    readRule: "storage.getItem('lwbridge.autoLaunchGame') !== 'false'",
    changeOrder: ["localStorage.setItem", "React state setter"],
    producer: "setAutoLaunchGame writes Sr(localStorage, value), then p(value) synchronously",
    consumer: "Home qr passes the current autoLaunchGame value directly to Bn.checked and the setter directly to Bn.onChange",
    disabledBySave: false,
  },
  execution: {
    missing: true,
    literalFalse: false,
    literalTrue: true,
    nonFalseString: true,
    finalStoredValue: fakeStorage.getItem("lwbridge.autoLaunchGame"),
  },
  limit: "This recovers the original frontend-local preference contract only. It does not prove compatibility or failure semantics for the clone's separate local_config_set native persistence adapter.",
};

fs.writeFileSync(path.join(here, "source-recovery.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log("LWB317_HOME_PREFERENCE_SOURCE_RECOVERY_OK");
