import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const { transformSync } = require("esbuild");

const referencePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const pagesPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx";
const reference = fs.readFileSync(path.join(repo, referencePath), "utf8");
const pages = fs.readFileSync(path.join(repo, pagesPath), "utf8");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();

assert.equal(hash(reference), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

const originalAst = parse(reference, { sourceType: "module" });
const pageAst = parse(pages, { sourceType: "module", plugins: ["jsx"] });

function walk(node, output = []) {
  if (!node || typeof node !== "object") return output;
  if (node.type) output.push(node);
  for (const child of Object.values(node)) {
    if (Array.isArray(child)) child.forEach((value) => walk(value, output));
    else if (child && typeof child === "object") walk(child, output);
  }
  return output;
}

function declaration(ast, name) {
  const node = ast.program.body
    .map((entry) => entry.declaration || entry)
    .find((entry) => entry.type === "FunctionDeclaration" && entry.id?.name === name);
  assert.ok(node, `missing function ${name}`);
  return node;
}

const originalBn = declaration(originalAst, "Bn");
const cloneToggle = declaration(pageAst, "ToggleRow");
const originalLocator = {
  utf8ByteOffset: Buffer.byteLength(reference.slice(0, originalBn.start)),
  expression: reference.slice(originalBn.start, originalBn.end),
};
const cloneLocator = {
  utf8ByteOffset: Buffer.byteLength(pages.slice(0, cloneToggle.start)),
  expression: pages.slice(cloneToggle.start, cloneToggle.end),
};
assert.equal(originalLocator.utf8ByteOffset, 213332);
assert.match(originalLocator.expression, /"aria-label":\`\$\{a\(e\)\}: \$\{a\(t\?\`common\.enabled\`:\`common\.disabled\`\)\}\`/);
assert.match(cloneLocator.expression, /aria-label=\{\`\$\{label\}: \$\{t\(checked \? "common\.enabled" : "common\.disabled"\)\}\`\}/);
assert.match(cloneLocator.expression, /onClick=\{\(\) => onChange\?\.\(!checked\)\}/);

const cloneCode = transformSync(cloneLocator.expression, {
  loader: "jsx",
  jsxFactory: "h",
  format: "cjs",
}).code;

function jsx(type, props, ...children) {
  const finalProps = { ...(props || {}) };
  if (children.length) finalProps.children = children.length === 1 ? children[0] : children;
  return typeof type === "function" ? type(finalProps) : { type, props: finalProps };
}
const M = { jsx, jsxs: jsx };
const OriginalSwitch = ({ checked }) => jsx("span", { className: `ui-switch${checked ? " is-on" : ""}`, "aria-hidden": "true" });
const CloneSwitch = OriginalSwitch;

function treeText(tree) {
  if (tree == null || typeof tree === "boolean") return "";
  if (Array.isArray(tree)) return tree.map(treeText).join("");
  if (typeof tree === "object") return treeText(tree.props?.children);
  return String(tree);
}

function switchView(tree) {
  assert.equal(tree.type, "button");
  return {
    type: tree.props.type,
    className: tree.props.className,
    role: tree.props.role,
    ariaChecked: tree.props["aria-checked"],
    ariaLabel: tree.props["aria-label"],
    disabled: tree.props.disabled,
    visibleLabel: treeText(tree.props.children?.[0] ?? tree.props.children),
  };
}

function attrExpression(node, name) {
  const attr = node.openingElement.attributes.find((entry) => entry.type === "JSXAttribute" && entry.name.name === name);
  if (!attr) return null;
  if (!attr.value) return "true";
  if (attr.value.type === "StringLiteral") return JSON.stringify(attr.value.value);
  assert.equal(attr.value.type, "JSXExpressionContainer");
  return pages.slice(attr.value.expression.start, attr.value.expression.end);
}

const allPageNodes = walk(pageAst);
const functionNodes = allPageNodes.filter((node) => node.type === "FunctionDeclaration" && node.id?.name);
const toggleCallers = allPageNodes
  .filter((node) => node.type === "JSXElement" && node.openingElement.name.type === "JSXIdentifier" && node.openingElement.name.name === "ToggleRow")
  .map((node) => {
    const owner = functionNodes
      .filter((fn) => fn.start <= node.start && fn.end >= node.end)
      .sort((a, b) => (a.end - a.start) - (b.end - b.start))[0];
    assert.ok(owner, "ToggleRow caller owner");
    const labelExpression = attrExpression(node, "label");
    const labelAttr = node.openingElement.attributes.find((entry) => entry.type === "JSXAttribute" && entry.name.name === "label");
    const labelCall = labelAttr?.value?.expression;
    assert.equal(labelCall?.type, "CallExpression");
    assert.equal(labelCall.callee?.type, "Identifier");
    assert.equal(labelCall.callee.name, "t");
    assert.equal(labelCall.arguments[0]?.type, "StringLiteral");
    const family = {
      HomePage: "Home",
      TradeStationCard: "Automation/Trade",
      AfkProfileEditor: "Squads/AFK",
      AllianceDrillPreviewSettings: "Squads/AFK",
      GarrisonPreviewSettings: "Squads/AFK",
      MiniGamesPage: "Mini Games",
      SettingsPage: "Settings",
    }[owner.id.name];
    assert.ok(family, `unexpected ToggleRow owner ${owner.id.name}`);
    return {
      owner: owner.id.name,
      family,
      utf8ByteOffset: Buffer.byteLength(pages.slice(0, node.start)),
      expression: pages.slice(node.start, node.end),
      labelExpression,
      labelKey: labelCall.arguments[0].value,
      checkedExpression: attrExpression(node, "checked"),
      disabledExpression: attrExpression(node, "disabled"),
      onChangeExpression: attrExpression(node, "onChange"),
    };
  });

assert.equal(toggleCallers.length, 11);
assert.deepEqual(
  [...new Set(toggleCallers.map((entry) => entry.family))].sort(),
  ["Automation/Trade", "Home", "Mini Games", "Settings", "Squads/AFK"].sort(),
);
assert.ok(toggleCallers.every((entry) => entry.labelExpression?.startsWith("t(")));

const languages = ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"];
const labelKeys = [...new Set(toggleCallers.map((entry) => entry.labelKey))];
const differential = [];
const localeSummary = [];

for (const language of languages) {
  const { default: catalog } = await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
  const t = (key, values = {}) => (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
  assert.notEqual(t("common.enabled"), "common.enabled", `${language} common.enabled`);
  assert.notEqual(t("common.disabled"), "common.disabled", `${language} common.disabled`);

  const OriginalToggle = new Function("M", "De", "zn", `${originalLocator.expression}\nreturn Bn;`)(M, () => ({ t }), OriginalSwitch);
  const CloneToggle = new Function("h", "useI18n", "Switch", `${cloneCode}\nreturn ToggleRow;`)(jsx, () => ({ t }), CloneSwitch);
  let comparisons = 0;

  for (const labelKey of labelKeys) {
    const translatedLabel = t(labelKey);
    assert.notEqual(translatedLabel, labelKey, `${language} ${labelKey}`);
    for (const checked of [false, true]) {
      for (const disabled of [false, true]) {
        const originalCalls = [];
        const cloneCalls = [];
        const originalTree = OriginalToggle({ label: labelKey, checked, disabled, onChange: (value) => originalCalls.push(value) });
        const cloneTree = CloneToggle({ label: translatedLabel, checked, disabled, onChange: (value) => cloneCalls.push(value) });
        const expected = switchView(originalTree);
        const actual = switchView(cloneTree);
        assert.deepEqual(actual, expected, `${language} ${labelKey} checked=${checked} disabled=${disabled}`);
        if (!disabled) {
          originalTree.props.onClick();
          cloneTree.props.onClick();
          assert.deepEqual(originalCalls, [!checked]);
          assert.deepEqual(cloneCalls, [!checked]);
        } else {
          assert.equal(originalTree.props.disabled, true);
          assert.equal(cloneTree.props.disabled, true);
        }
        differential.push({ language, labelKey, checked, disabled, expected, actual });
        comparisons++;
      }
    }
  }

  const noCallbackTree = CloneToggle({ label: t("settings.visualMetrics.showFps"), checked: false, disabled: false });
  assert.doesNotThrow(() => noCallbackTree.props.onClick());

  localeSummary.push({
    language,
    comparisons,
    enabled: t("common.enabled"),
    disabled: t("common.disabled"),
    representativeLabels: Object.fromEntries(labelKeys.map((key) => [key, t(key)])),
    pass: true,
  });
}

const report = {
  task: "LWB317-UI-SWITCH-LOCALE-001",
  result: "LWB317_UI_SWITCH_LOCALE001_OK",
  reference: {
    path: referencePath,
    sha256: hash(reference),
    Bn: originalLocator,
  },
  production: {
    path: pagesPath,
    sha256: hash(pages),
    ToggleRow: cloneLocator,
  },
  cloneLabelAdapter: "Original Bn receives a translation key and translates the label internally. Clone ToggleRow callers already pass t(labelKey), so the differential passes the translated label value to production ToggleRow and compares only equivalent rendered output.",
  counts: {
    languages: languages.length,
    callers: toggleCallers.length,
    uniqueCallerLabels: labelKeys.length,
    renderComparisons: differential.length,
  },
  callerInventory: toggleCallers,
  localeSummary,
  differential,
  callbackChecks: {
    enabled: "Actual original Bn and production ToggleRow onClick handlers each received !checked for every enabled differential case.",
    disabled: "Both helpers render disabled=true for disabled cases; browser verification separately confirms disabled controls cannot toggle.",
    omittedCloneCallback: "Production ToggleRow preserves optional-callback behavior: invoking onClick with onChange omitted does not throw.",
  },
  limits: "Executes the recovered original Bn switch branch and actual production ToggleRow with local catalogs. The recovered Bn checkbox variant is outside this work item. Browser evidence is local preview only; no original post-auth runtime, native lifecycle, gameplay, or auth action is invoked.",
};

if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "switch-results.json"), JSON.stringify(report, null, 2) + "\n");
}
if (process.argv.includes("--verify-record")) {
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "switch-results.json"), "utf8")), report);
}
console.log(JSON.stringify({ result: report.result, ...report.counts }, null, 2));
