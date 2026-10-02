// Shared fixtures/helpers for check-original-runtime.mjs and negative-cases.mjs.
// Everything observed is produced by executing the ORIGINAL component through original-runtime.mjs.
import { createOriginalHarness, nodeText, treeNodes } from "./original-runtime.mjs";

export { createOriginalHarness, nodeText, treeNodes };
export const NOW = Date.UTC(2026, 9, 2, 12, 0, 0);
export const SERVER = 321;

export const optionsReply = (overrides = {}) => (sid) => ({
  serverId: sid,
  counts: { city: 5, resource: 4, monster: 3, truck: 2, railway: 1, dispatch: 6, ghost: 7, treasure: 8 },
  alliances: [{ name: "Foo Bar", count: 2 }],
  names: { resource: [{ key: "r1", count: 3 }, { key: "r2", count: 5 }], monster: [{ key: "m1", count: 2 }, { key: "m2", count: 1 }] },
  dispatchLevels: [1, 5, 9],
  rewardItems: { truck: [], railway: [] },
  treasureTypes: [],
  noAllianceCount: 1,
  scanProgress: null,
  ...overrides,
});

export async function boot(options = {}) {
  const stubs = { dataOptions: { mode: "auto", value: optionsReply() }, ...(options.stubs || {}) };
  const h = await createOriginalHarness({ online: true, now: NOW, ...options, stubs });
  await h.mount();
  return h;
}

// Resolve every unresolved search request (repeatedly: a reply may trigger a follow-up request).
export async function drain(h, reply = { rows: [], total: 0 }, limit = 20) {
  for (let i = 0; i < limit; i += 1) {
    const open = h.requests.filter((r) => !r.done);
    if (open.length === 0) return;
    for (const request of open) { request.done = true; request.resolve(typeof reply === "function" ? reply(request) : reply); }
    await h.settle();
  }
  throw new Error("drain did not quiesce");
}
export const summarize = (r) => ({
  id: r.id, kind: r.kind, keyword: r.query.keyword, page: r.query.page, serverId: r.query.serverId,
  resourceNameKey: r.query.resourceNameKey, monsterNameKey: r.query.monsterNameKey,
});
export const since = (h, n) => h.requests.slice(n).map(summarize);
export const definedKeys = (q) => Object.entries(q).filter(([, v]) => v !== undefined).map(([k]) => k);

// ---- row fixtures (times in ms, relative to the fake clock NOW) -------------------------------------------
export const dispatchRow = (uuid, extra = {}) => ({
  uuid, serverId: SERVER, x: 10, y: 20, ownerName: `Owner ${uuid}`, quality: 3, level: 5,
  completionTime: NOW - 60000, plunderAt: NOW + 3600000, taskExpireTime: NOW + 7200000, stolenCount: 0, maxStealCount: 3,
  rewards: [], updatedAt: NOW - 1000, ...extra,
});
export const truckRow = (uuid, extra = {}) => ({
  uuid, serverId: SERVER, x: 11, y: 21, ownerName: `Truck ${uuid}`, quality: 3, power: 1000, arriveTs: NOW + 3600000,
  maxLootCount: 3, robTimes: 0, protectTime: 0, currentGoods: [], updatedAt: NOW - 1000, ...extra,
});
export const cityRow = (uuid, extra = {}) => ({ uuid, serverId: SERVER, x: 3, y: 4, ownerName: `Player ${uuid}`, level: 10, updatedAt: NOW - 1000, ...extra });

export async function openTab(h, kind, rows = [], total = rows.length) {
  await h.clickTab(kind);
  await drain(h, { rows, total });
}

export const checkboxView = (h) => h.tableCheckboxes().map((c) => ({ ariaLabel: c.ariaLabel, checked: c.checked, disabled: c.disabled }));
export const buttonView = (b) => (b ? { disabled: b.props.disabled, text: nodeText(b) } : null);

export function filterCheckbox(h, labelKey) {
  // label.map-filter-field > [input, span|text]
  h.tree();
  const text = h.translate(labelKey);
  return h.findNodes((n) => n.type === "label" && typeof n.props?.className === "string" && n.props.className.includes("map-filter-field")
    && nodeText(n) === text)[0]?.props.children[0];
}
