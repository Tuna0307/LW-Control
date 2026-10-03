import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { compile, flatten, Fragment, h, read, require } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import { routes } from "../../../../../src/LWBridge.UI-0.3.17/src/routes.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const app = read("src/LWBridge.UI-0.3.17/src/App.jsx");
const react = require("react");
const results = [];
const record = (name, count) => results.push({ name, count, result: "PASS" });

assert.equal(react.version, "19.3.0");
assert.equal(typeof react.Activity, "symbol");

const RetainedPages = compile(app, "RetainedPages", {
  h,
  Fragment,
  Activity: react.Activity,
  routes,
  PageForRoute: "PageForRoute",
});

function render(activeRoute, visitedKeys, selectedProfileId = "profile-a", pageProps = {}) {
  return RetainedPages({ activeRoute, visitedRoutes: new Set(visitedKeys), selectedProfileId, pageProps });
}

function activities(tree) {
  return flatten(tree).filter((node) => node.type === react.Activity);
}

function pages(tree) {
  return flatten(tree).filter((node) => node.type === "PageForRoute");
}

let tree = render("overview", ["overview"], "profile-a", { marker: "first" });
assert.equal(activities(tree).length, 1);
assert.equal(activities(tree)[0].props.mode, "visible");
assert.equal(pages(tree).length, 1);
assert.equal(pages(tree)[0].props.routeKey, "overview");
assert.equal(pages(tree)[0].props.marker, "first");
record("initial route only; never-visited routes are not mounted", 5);

tree = render("march", ["overview", "march"], "profile-a", { marker: "updated" });
const twoActivities = activities(tree);
assert.equal(twoActivities.length, 2);
assert.equal(twoActivities.filter((node) => node.props.mode === "visible").length, 1);
assert.equal(twoActivities.filter((node) => node.props.mode === "hidden").length, 1);
assert.equal(pages(tree).find((node) => node.props.routeKey === "overview").props.marker, "updated");
assert.equal(pages(tree).find((node) => node.props.routeKey === "march").props.marker, "updated");
record("visited routes retain one visible/one hidden Activity and receive current props", 5);

tree = render("settings", routes.map((route) => route.key), "profile-b", { marker: "all" });
const allActivities = activities(tree);
assert.equal(allActivities.length, 8);
assert.equal(allActivities.filter((node) => node.props.mode === "visible").length, 1);
assert.equal(routes[allActivities.findIndex((node) => node.props.mode === "visible")].key, "settings");
assert.deepEqual(allActivities.map((node) => node.props.key), routes.map((route) => route.key));
assert.equal(tree.props.key, "profile-b");
record("all eight routes keep stable route identity and recovered profile reset key", 5);

for (const sourceGuard of [
  "const [visitedRoutes, setVisitedRoutes] = useState(() => new Set([initialRoute()]));",
  "if (routeKey === activeRoute) return;",
  "startRouteTransition(() => {",
  "if (current.has(routeKey)) return current;",
  "if (activeRoute === \"map-data\" && backendBridge.available) refreshMapSummary().catch(() => {});",
  "const timer = window.setInterval(refreshStatus, 5000);",
  "const timer = window.setInterval(poll, 5000);",
]) assert.ok(app.includes(sourceGuard), `Missing current shell guard: ${sourceGuard}`);
record("same-tab/first-visit transition and App-owned polling/Map guards remain present", 7);

const report = {
  result: "LWB317_SHELL_RETENTION_FOCUSED_OK",
  assertions: results.reduce((sum, entry) => sum + entry.count, 0),
  results,
  reactVersion: react.version,
  limits: "Executable component-tree proof covers first-visit mounting, Activity modes, stable route/profile identity and current prop flow. Effect suspension and retained hook state are proven in the real browser mount, not inferred from this tree harness.",
};

if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "focused-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
