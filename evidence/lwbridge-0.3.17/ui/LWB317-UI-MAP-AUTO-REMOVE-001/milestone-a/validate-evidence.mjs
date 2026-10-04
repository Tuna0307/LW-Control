import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));
const fromRepo = (relative) => path.join(repo, relative);
const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));

const pinned = readJson(path.join(here, "pinned-inputs.json"));
assert.equal(pinned.task, "LWB317-UI-MAP-AUTO-REMOVE-001");
assert.equal(pinned.milestone, "A");
assert.equal(hashFile(pinned.reference.exe.path), pinned.reference.exe.sha256, "reference EXE pin");
for (const key of ["panel", "index", "css"]) {
  assert.equal(hashFile(fromRepo(pinned.reference[key].path)), pinned.reference[key].sha256, `reference ${key} pin`);
}
for (const item of [...pinned.current.files, ...Object.values(pinned.current.locales), ...pinned.dependencies]) {
  assert.equal(hashFile(fromRepo(item.path)), item.sha256, `current/dependency pin ${item.path}`);
}

const currentSource = fs.readFileSync(fromRepo(pinned.current.removeIconChild.file));
const icon = pinned.current.removeIconChild;
const iconBytes = currentSource.subarray(icon.utf8ByteOffset, icon.utf8ByteOffset + icon.byteLength);
assert.equal(hashBytes(iconBytes), icon.sha256, "current remove SVG locator hash");
assert.equal(iconBytes.toString("utf8"), icon.text, "current remove SVG locator text");
assert.equal(icon.text, '<svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">\n                        <path d="m4 4 8 8M12 4l-8 8" />\n                      </svg>');

const cardBytes = currentSource.subarray(pinned.current.autoCard.utf8ByteOffset, pinned.current.autoCard.utf8ByteOffset + pinned.current.autoCard.byteLength);
const cardText = cardBytes.toString("utf8");
assert.ok(cardText.includes('<button type="button" aria-label={`${t("common.remove")} ${id}`} onClick={() => emitAutoConfig({ serverIds: removeAutoServerId(autoConfig.serverIds, id) })}>'), "server remove button label/callback preserved");

const parent = readJson(path.join(here, "../../LWB317-UI-MAP-AUTO-VISUAL-001/render-results.json"));
const parentConfigured = parent.pairs.find((pair) => pair.id === "configured");
assert.ok(parentConfigured, "accepted pre-fix configured comparison exists");
assert.equal(parentConfigured.exactViewMatch, false, "accepted pre-fix mismatch remains preserved");
assert.ok(parentConfigured.currentView.server.chips.every((chip) => chip.removeChildTag === "span" && chip.removeChildText === "×"), "accepted pre-fix text glyph evidence remains preserved");

const render = readJson(path.join(here, "render-results.json"));
assert.equal(render.marker, "LWB317_MAP_AUTO_REMOVE_A_RENDER_COMPARED");
assert.equal(render.pairs.length, 1);
const pair = render.pairs[0];
assert.equal(pair.pairId, "ja-dark-configured");
assert.equal(pair.enteredAutoViaActualTabCallback, true);
assert.equal(pair.exactViewMatch, true);
assert.equal(pair.exactMarkupMatch, true);
assert.deepEqual(pair.differences, []);
assert.ok(pair.currentView.server.chips.every((chip) => chip.removeChildTag === "svg"
  && chip.removeChildClassName === "ui-icon"
  && chip.removeViewBox === "0 0 16 16"
  && chip.removeAriaHidden === "true"
  && chip.removeFocusable === "false"
  && chip.removePathD === "m4 4 8 8M12 4l-8 8"));

const measurements = readJson(path.join(here, "browser/measurements.json"));
const browser = readJson(path.join(here, "browser/comparison-results.json"));
const consoleEntries = readJson(path.join(here, "browser/console.json"));
const inspection = readJson(path.join(here, "browser/inspection.json"));
assert.equal(measurements.marker, "LWB317_MAP_AUTO_REMOVE_A_BROWSER_MEASURED");
assert.equal(browser.marker, "LWB317_MAP_AUTO_REMOVE_A_BROWSER_COMPARED");
assert.equal(measurements.records.length, 2);
assert.equal(browser.pairs.length, 1);
assert.equal(consoleEntries.length, 2);
assert.ok(consoleEntries.every((entry) => entry.issues.length === 0));
const browserPair = browser.pairs[0];
assert.equal(browserPair.exactScreenshotBytes, true);
assert.equal(browserPair.domMatch, true);
assert.deepEqual(browserPair.geometryDifferences, []);
assert.deepEqual(browserPair.styleDifferences, []);
assert.equal(browserPair.originalScreenshotSha256, browserPair.currentScreenshotSha256);
for (const record of measurements.records) {
  assert.equal(record.consoleIssueCount, 0);
  assert.equal(hashFile(fromRepo(record.screenshot)), record.screenshotSha256, `${record.side} screenshot hash`);
}
assert.equal(inspection.marker, "LWB317_MAP_AUTO_REMOVE_A_SCREENSHOTS_INSPECTED");
assert.equal(inspection.screenshots.length, 2);
assert.ok(inspection.screenshots.every((entry) => entry.status === "INSPECTED" && entry.sha256 === browserPair.originalScreenshotSha256));

console.log(`LWB317_MAP_AUTO_REMOVE_A_EVIDENCE_OK screenshot=${browserPair.originalScreenshotSha256} geometry=0 styles=0 consoleIssues=0`);
