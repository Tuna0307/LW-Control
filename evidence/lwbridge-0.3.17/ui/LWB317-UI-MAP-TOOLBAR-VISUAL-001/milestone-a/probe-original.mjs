import { pathToFileURL } from "node:url";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createOriginalHarness, nodeText, optionsReply, treeNodes } from "../../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const locale = (await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/locales/en.js")).href)).default;
const t = (key, values = {}) => (locale[key] || key).replace(/\{(\w+)\}/g, (m, name) => String(values[name] ?? m));
const configured = optionsReply({
  rewardItems: {
    truck: [{ key: "gold", name: "Gold", count: 3, iconPath: "Item/Gold.png" }],
    railway: [{ key: "iron", name: "Iron", count: 4, iconPath: "Item/Iron.png" }],
  },
  treasureTypes: [{ key: "1:2", treasureType: 1, suppliesType: 2, treasureNameKey: "Treasure.Test", count: 5 }],
});

for (const tab of ["truck", "treasure"]) {
  const h = await createOriginalHarness({ online: true, translate: t, language: "en", stubs: { dataOptions: { mode: "auto", value: configured } } });
  await h.mount();
  await h.clickTab(tab);
  const search = h.findNodes((node) => node?.props?.className === "map-search")[0];
  const functionNodes = treeNodes(search).filter((node) => typeof node.type === "function");
  console.log(JSON.stringify({
    tab,
    searchText: nodeText(search),
    functions: functionNodes.map((node) => ({ name: node.type.name, displayName: node.type.displayName || "", props: Object.keys(node.props || {}) })),
    native: treeNodes(search).filter((node) => typeof node.type === "string").map((node) => ({
      type: node.type,
      className: node.props?.className || "",
      ariaLabel: node.props?.["aria-label"] || "",
      text: nodeText(node),
    })),
    expanded: functionNodes.map((node) => {
      const out = h.expand(node);
      return {
        name: node.type.name || node.type.displayName || "anonymous",
        text: nodeText(out),
        native: treeNodes(out).filter((child) => typeof child.type === "string").map((child) => ({
          type: child.type,
          className: child.props?.className || "",
          ariaLabel: child.props?.["aria-label"] || "",
          text: nodeText(child),
        })),
      };
    }),
  }, null, 2));
  await h.unmount();
}
