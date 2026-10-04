import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json")), parse = req("@babel/parser").parse, traverse = req("@babel/traverse").default, types = req("@babel/types");
const baseline = fs.readFileSync(path.join(here, "squads-before-motion.jsx"), "utf8");
const source = fs.readFileSync(path.join(ui, "src/SquadsPage.jsx"), "utf8");
const wrapper = fs.readFileSync(path.join(ui, "src/EquipmentMotion.jsx"), "utf8");
const recovered = JSON.parse(fs.readFileSync(path.join(here, "motion-results.json"), "utf8"));
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
assert.equal(sha(baseline), "5A81A549A7FF33FE559F0C9283CA03A03DAC2AF45BBF408ACFE266D8A87E8324");
const props = new Function(wrapper.replace(/^import[^\n]+\n/gm, "").replace(/^export \{[^\n]+\n/gm, "").replace(/export /g, "") + "\nreturn equipmentMotionProps;")();
const parseJsx = (code) => parse(code, { sourceType: "module", plugins: ["jsx"] });
const semantic = (value) => {
  if (Array.isArray(value)) return value.filter((entry) => !(entry?.type === "JSXText" && !entry.value.trim())).map(semantic);
  if (!value || typeof value !== "object") return value;
  const output = {};
  for (const [key, entry] of Object.entries(value)) if (!["start", "end", "loc", "extra", "leadingComments", "trailingComments", "innerComments"].includes(key)) output[key] = semantic(entry);
  return output;
};
const originalTags = { presets: "div", squad: "section", position: "article", slot: "div", rename: "div", toast: "div", progress: "div" };
function proof(code) {
  const ast = parseJsx(code), calls = {}, presence = [], changes = [];
  let hooks = 0, params = 0, imports = 0, propComparisons = 0;
  traverse(ast, {
    ImportDeclaration(p) { if (p.node.source.value !== "./EquipmentMotion.jsx") return; assert.deepEqual(p.node.specifiers.map((entry) => entry.imported.name).sort(), ["AnimatePresence", "equipmentMotionProps", "motion", "useReducedMotion"].sort()); p.remove(); imports++; },
    VariableDeclaration(p) { if (!p.node.declarations.some((declaration) => declaration.id.name === "reducedMotion")) return; assert.equal(p.node.declarations.length, 1); const declaration = p.node.declarations[0]; assert.equal(declaration.init.type, "CallExpression"); assert.equal(declaration.init.callee.name, "useReducedMotion"); assert.equal(declaration.init.arguments.length, 0); p.remove(); hooks++; },
    JSXElement: { exit(p) {
      const opening = p.node.openingElement;
      if (opening.name.type === "JSXIdentifier" && opening.name.name === "AnimatePresence") {
        const attributes = opening.attributes;
        presence.push(attributes.map((attribute) => [attribute.name.name, attribute.value?.value]));
        const children = p.node.children.filter((child) => !(child.type === "JSXText" && !child.value.trim()));
        if (attributes.length) { assert.deepEqual(presence.at(-1), [["mode", "wait"]]); assert.equal(children.length, 1); assert.equal(children[0].expression?.type, "ConditionalExpression"); assert.equal(children[0].expression.test.name, "selectedPreset"); }
        else { assert.equal(children.length, 3); assert.equal(children[0].expression.test.name, "renameOpen"); assert.equal(children[1].expression.test.name, "toast"); assert.equal(children[2].expression.test.type, "MemberExpression"); assert.equal(children[2].expression.test.object.name, "fixture"); assert.equal(children[2].expression.test.property.name, "progress"); }
        p.replaceWithMultiple(children); return;
      }
      if (opening.name.type !== "JSXMemberExpression" || opening.name.object.name !== "motion") return;
      const spread = opening.attributes.find((attribute) => attribute.type === "JSXSpreadAttribute" && attribute.argument.callee?.name === "equipmentMotionProps");
      assert.ok(spread, "A recovered motion node must receive its exact props");
      const call = spread.argument, kind = call.arguments[0].value;
      assert.ok(originalTags[kind], kind); assert.equal(opening.name.property.name, originalTags[kind], kind);
      assert.equal(call.arguments[1].name, "reducedMotion"); assert.ok(!calls[kind], kind);
      const callText = code.slice(call.start, call.end); calls[kind] = callText;
      const actual = new Function("equipmentMotionProps", "reducedMotion", "squadArrayIndex", "selectedPreset", "savedEquip", "return " + callText + ";");
      const expected = new Function("s", "r", "N", "e", "return ({" + recovered.locators[kind].map((locator) => locator.expression).join(",") + "});");
      for (const reduced of [false, true]) for (const index of [0, 1, 3]) for (const saved of [false, true]) {
        const selected = { id: "preset-A" }, equip = saved ? { equipUuid: 42 } : undefined;
        assert.deepEqual(actual(props, reduced, index, selected, equip), expected(reduced, index, selected, equip), kind); propComparisons++;
      }
      opening.attributes = opening.attributes.filter((attribute) => attribute !== spread);
      if (kind === "presets") {
        const key = opening.attributes.find((attribute) => attribute.name?.name === "key");
        assert.equal(key?.value.expression.object.name, "selectedPreset"); assert.equal(key?.value.expression.property.name, "id"); opening.attributes = opening.attributes.filter((attribute) => attribute !== key);
      }
      opening.name = types.jsxIdentifier(originalTags[kind]); if (p.node.closingElement) p.node.closingElement.name = types.jsxIdentifier(originalTags[kind]);
      changes.push({ kind, sourceTag: originalTags[kind], productionCall: callText });
    } },
    ArrowFunctionExpression: { exit(p) {
      if (p.node.params[0]?.name !== "squadIndex" || p.node.params[1]?.name !== "squadArrayIndex") return;
      assert.equal(p.node.params.length, 2); p.node.params.pop(); params++;
    } },
  });
  assert.equal(imports, 1); assert.equal(hooks, 1); assert.equal(params, 1); assert.equal(presence.length, 2);
  assert.deepEqual(Object.keys(calls).sort(), Object.keys(originalTags).sort()); assert.equal(propComparisons, 84);
  assert.deepEqual(semantic(ast), semantic(parseJsx(baseline)), "Only explicitly recovered motion additions may differ; all prior state/effects/handlers/content must survive");
  return { propComparisons, changes, presence, hookAdditions: hooks, normalizedModuleSha256: sha(JSON.stringify(semantic(ast))) };
}
const result = proof(source), mutations = [
  source.replace("return <motion.article", "return <motion.div").replace("</motion.article>;", "</motion.div>;"),
  source.replace('<AnimatePresence mode="wait">', '<AnimatePresence mode="sync">'),
  source.replace('const reducedMotion = useReducedMotion();', 'const reducedMotion = false;'),
  source.replace('index: squadArrayIndex', 'index: squadIndex'),
  source.replace('hasSavedEquip: !!savedEquip', 'hasSavedEquip: true'),
  source.replace('key={selectedPreset.id}', 'key="constant"'),
  source.replace('disabled={!selectedPreset || busy}', 'disabled={!selectedPreset}'),
  source.replace('setDropTarget(""); swapSquads(squadIndex);', 'setDropTarget("");'),
];
let mutationDetections = 0;
for (const mutation of mutations) { assert.notEqual(mutation, source); assert.throws(() => proof(mutation)); mutationDetections++; }
const report = { marker: "LWB317_EQUIPMENT_MOTION_INTEGRATION_OK", baselineSha256: sha(baseline), productionSha256: sha(source), wrapperSha256: sha(wrapper), ...result, mutationDetections, preserved: "Full module AST equals immutable pre-motion module after removing only the seven exact motion spreads/tags, two source presence boundaries, selected-preset key, reduced hook/import and stagger index parameter. State, effects, handlers, dialog semantics, Activity and rendered content are otherwise identical." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "integration-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify(report));
