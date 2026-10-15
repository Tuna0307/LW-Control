// LEADMAP014-FINAL-01: mounted production Map, not a mocked coordinate-cell
// implementation. A visible Railway or moving Monster must age out without
// navigating away, while native click-time validation remains independent.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const uiRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(uiRoot, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const bundle = await build({ stdin: {
  contents: 'export {MapDataPage} from "./MapDataPage.jsx"; export {I18nProvider} from "./i18n.jsx";',
  resolveDir: path.join(uiRoot, "src"), loader: "jsx",
}, bundle: true, write: false, platform: "browser", format: "cjs",
jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"],
logLevel: "silent" });
const module = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, module, module.exports);
const { MapDataPage, I18nProvider } = module.exports;
const flush = async (fn) => React.act(async () => {
  if (fn) await fn();
  for (let i = 0; i < 24; i += 1) await Promise.resolve();
});
const originalNow = Date.now;
let createRoot;
const results = [];

try {
  for (const kind of ["railway", "monster"]) {
    const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
      url: "http://127.0.0.1/map014",
    });
    for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event",
      "MouseEvent", "Node", "MutationObserver", "localStorage"]) {
      globalThis[key] = key === "window" ? dom.window : dom.window[key];
    }
    Object.defineProperty(globalThis, "navigator", {
      configurable: true, value: dom.window.navigator,
    });
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    if (!createRoot) ({ createRoot } = req("react-dom/client"));
    window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
    localStorage.setItem("lwbridge.language", "en");
    localStorage.setItem("lwbridge.theme", "light");
    const timers = new Map();
    let timerId = 0;
    window.setInterval = (callback, delay) => {
      const id = ++timerId;
      timers.set(id, { callback, delay });
      return id;
    };
    window.clearInterval = (id) => timers.delete(id);
    window.setTimeout = () => 1;
    window.clearTimeout = () => {};

    let now = 1_800_000_000_000;
    Date.now = () => now;
    const arrival = now + 5000;
    const state = { serverId: 2212, isReading: false, phase: "idle", selectedTypes: [kind] };
    const requests = [];
    const rows = [
      { uuid: "known-" + kind, marchUuid: "1234567", serverId: 2212, arriveTs: arrival, x: 1, y: 2, quality: 5 },
      { uuid: "unknown-" + kind, marchUuid: "7654321", serverId: 2212, arriveTs: null, x: 3, y: 4, quality: 5 },
    ];
    if (kind === "monster") {
      rows.push({ uuid: "stationary-monster", serverId: 2212, x: 5, y: 6 });
    }
    const makeApi = (owner) => ({
      dataOptions: async () => ({ serverId: 2212, counts: { [kind]: rows.length },
        names: { resource: [], monster: [] }, alliances: [], dispatchLevels: [],
        treasureTypes: [], rewardItems: { truck: [], railway: [] } }),
      search: async (requestedKind) => ({
        rows: requestedKind === kind ? rows : [],
        total: requestedKind === kind ? rows.length : 0,
      }),
      listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
      scanStatus: async () => state,
      marchFollow: async (row) => { requests.push({ owner, uuid: row.marchUuid, now }); },
    });
    let mapApi = makeApi("A");
    const root = createRoot(document.getElementById("root"));
    const render = () => flush(() => root.render(
      React.createElement(I18nProvider, null,
        React.createElement(MapDataPage, {
          key: mapApi === apiA ? "A" : "B", mapApi,
          bridgeMode: "native", backendAvailable: true, online: true,
          currentServerId: 2212, scanState: state,
          summary: { serverId: 2212, counts: { [kind]: rows.length } },
          onState() {}, onCounts() {},
        })),
    ));
    const apiA = mapApi;
    const goto = async (index) => {
      await flush(() => document.querySelectorAll(".map-tabs [role=tab]")[index].click());
    };
    const buttons = () => [...document.querySelectorAll(`.map-table--${kind} .map-coordinate-button`)];
    const timerCount = (delay) => [...timers.values()].filter((timer) => timer.delay === delay).length;
    const click = (button) => flush(() => {
      assert.ok(button && !button.disabled, "expected an enabled native control");
      button.click();
    });

    try {
      await render();
      await goto(kind === "railway" ? 4 : 2);
      assert.equal(buttons().length, rows.length, `${kind}: mounted source rows missing`);
      assert.equal(timerCount(1000), 1, `${kind}: missing expiry clock while tab is visible`);
      assert.equal(buttons()[0].disabled, false);
      assert.equal(buttons()[1].disabled, false, "unknown arrival must not be disabled");
      if (kind === "monster") assert.match(buttons()[2].textContent, /5,6/);

      // Time-of-click guard must refuse an expired row even BEFORE the visual
      // interval runs, without issuing any native Follow request.
      now = arrival + 1000;
      assert.equal(buttons()[0].disabled, false, "expected stale visual state before a tick");
      await click(buttons()[0]);
      assert.equal(requests.length, 0, `${kind}: expired stale click reached native`);
      now = arrival - 1000;
      await click(buttons()[0]);
      assert.deepEqual(requests.map((call) => call.uuid), ["1234567"]);
      assert.equal(timerCount(3000), 1, `${kind}: active native Follow did not poll`);
      assert.match(buttons()[0].textContent, /Stop/i, "active Follow must expose Stop");

      // A known arrival must NOT hide Stop on an ongoing Follow. Once Stop is
      // clicked, the row must be disabled and must not resume native tracking.
      now = arrival + 1000;
      const clockTimer = [...timers.values()].find((timer) => timer.delay === 1000);
      await flush(() => clockTimer.callback());
      assert.match(buttons()[0].textContent, /Stop/i);
      assert.equal(buttons()[0].disabled, false, "arrival suppressed ongoing Stop");
      await click(buttons()[0]);
      assert.equal(timerCount(3000), 0, "Stop retained an observation interval");
      assert.equal(buttons()[0].disabled, true, `${kind}: newly expired Follow remained enabled`);
      assert.equal(buttons()[1].disabled, false, "unknown native arrival was guessed");
      assert.equal(requests.length, 1, "Stop issued a new native request");

      // Unknown native arrival still delegates to the native provider. Switching
      // tabs retires its following state, clock and polling timers.
      await click(buttons()[1]);
      assert.equal(requests.at(-1).uuid, "7654321");
      assert.equal(timerCount(3000), 1);
      const oldPoll = [...timers.values()].find((timer) => timer.delay === 3000);
      await goto(1);
      assert.equal(timerCount(1000), 0, `${kind}: inactive tab retained expiry interval`);
      assert.equal(timerCount(3000), 0, `${kind}: tab retire retained native poll`);
      const beforeRetired = requests.length;
      await flush(() => oldPoll.callback());
      assert.equal(requests.length, beforeRetired, "retired tab still issued native Follow");

      // A new owner/profile must dispose the old page's time and Follow state.
      await goto(kind === "railway" ? 4 : 2);
      await click(buttons()[1]);
      assert.equal(timerCount(1000), 1);
      assert.equal(timerCount(3000), 1);
      mapApi = makeApi("B");
      await render();
      assert.equal(timerCount(3000), 0, "profile retirement retained old follow polling");
      await goto(kind === "railway" ? 4 : 2);
      assert.equal(timerCount(1000), 1, "replacement owner did not start active-tab clock");
      assert.equal(buttons()[0].disabled, true, "new owner reenabled an expired Follow");
    } finally {
      await flush(() => root.unmount());
      assert.equal(timers.size, 0, `${kind}: unmount leaked timer(s)`);
      dom.window.close();
    }
    results.push({ kind, deadlineDisabled: true, stopPreserved: true,
      unknownArrivalAllowed: true, staleClickBlocked: true,
      tabAndProfileRetirement: true, unmountTimers: timers.size });
  }
} finally {
  Date.now = originalNow;
}
console.log("LEADMAP014_FINAL_01_MOUNTED_CLOCK_PASS", JSON.stringify(results));
