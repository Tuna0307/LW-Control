// Deterministic browser-only fixture values. Structure and render predicates are
// recovered from AutomationPanel-BJ0gIqFh.js; these IDs/names/counts are not game facts.

export const railwayWeeklyQualities = ["ssr", "ur", "ssr", "ssr", "ssr", "ur", "ssr"];
export const dispatchWeeklyQualities = ["none", "ur", "none", "none", "none", "ur", "none"];

export const previewTradeGoods = [
  {
    itemId: 7001,
    name: "Fixture Speedup Chest",
    quality: 5,
    count: 2,
    offers: [
      { currencyId: 15, currencyName: "Fixture Trade Token", price: 120, exclusive: false },
      { currencyId: 650053, currencyName: "Fixture Market Credit", price: 48, exclusive: false },
    ],
  },
  {
    itemId: 7002,
    name: "Fixture Resource Crate",
    quality: 4,
    count: 5,
    offers: [{ currencyId: 15, currencyName: "Fixture Trade Token", price: 80, exclusive: false }],
  },
  {
    itemId: 7003,
    name: "Fixture City-Owner Offer",
    quality: 6,
    count: 1,
    offers: [{ currencyId: 650053, currencyName: "Fixture Market Credit", price: 200, exclusive: true }],
  },
];

export const previewTradePurchases = [
  {
    id: "fixture-purchase-1",
    serverDayStartAt: Date.UTC(2026, 9, 1, 0, 0, 0),
    purchasedAt: Date.UTC(2026, 9, 1, 8, 14, 0),
    itemId: 7001,
    itemName: "Fixture Speedup Chest",
    quantity: 2,
    currencyId: 15,
    currencyName: "Fixture Trade Token",
    price: 120,
    serverId: 901,
    dailyPurchaseIndex: 1,
    confirmedAfterTimeout: false,
  },
  {
    id: "fixture-purchase-2",
    serverDayStartAt: Date.UTC(2026, 9, 1, 0, 0, 0),
    purchasedAt: Date.UTC(2026, 9, 1, 9, 22, 0),
    itemId: 7002,
    itemName: "Fixture Resource Crate",
    quantity: 5,
    currencyId: 650053,
    currencyName: "Fixture Market Credit",
    price: 44,
    serverId: 901,
    dailyPurchaseIndex: 2,
    confirmedAfterTimeout: true,
  },
];

const previewTradeHistoryPurchases = [
  ...previewTradePurchases,
  {
    purchaseKey: "fixture-history-3",
    serverDayStartAt: Date.UTC(2026, 9, 2, 0, 0, 0),
    purchasedAt: Date.UTC(2026, 9, 2, 7, 5, 0),
    itemId: 7001,
    itemNameKey: "fixture.trade.speedup",
    itemName: "Fixture Speedup Chest",
    quantity: 3,
    quality: 5,
    iconPath: "fixture/items/speedup.png",
    currencyId: 15,
    currencyNameKey: "fixture.trade.token",
    currencyName: "Fixture Trade Token",
    currencyIconPath: "fixture/currency/token.png",
    price: 118,
    serverId: 902,
    tradeId: 3103,
    configId: 4103,
    dailyPurchaseIndex: 1,
    confirmedAfterTimeout: false,
  },
  {
    purchaseKey: "fixture-history-4",
    serverDayStartAt: Date.UTC(2026, 9, 2, 0, 0, 0),
    purchasedAt: Date.UTC(2026, 9, 2, 8, 10, 0),
    itemId: 7002,
    itemName: "Fixture Resource Crate",
    quantity: 4,
    quality: 4,
    currencyId: 650053,
    currencyName: "Fixture Market Credit",
    price: 43,
    serverId: 902,
    tradeId: 3104,
    configId: 4104,
    dailyPurchaseIndex: 2,
    confirmedAfterTimeout: true,
  },
  {
    purchaseKey: "fixture-history-5",
    serverDayStartAt: Date.UTC(2026, 9, 2, 0, 0, 0),
    purchasedAt: Date.UTC(2026, 9, 2, 9, 15, 0),
    itemId: 7001,
    itemNameKey: "fixture.trade.speedup",
    itemName: "Fixture Speedup Chest",
    quantity: 6,
    quality: 5,
    iconPath: "fixture/items/speedup.png",
    currencyId: 15,
    currencyNameKey: "fixture.trade.token",
    currencyName: "Fixture Trade Token",
    currencyIconPath: "fixture/currency/token.png",
    price: 116,
    serverId: 902,
    tradeId: 3105,
    configId: 4105,
    dailyPurchaseIndex: 3,
    confirmedAfterTimeout: false,
  },
  {
    purchasedAt: Date.UTC(2026, 9, 2, 18, 30, 0),
    itemId: 7004,
    itemNameKey: "fixture.trade.decorated",
    itemName: "Fixture Fallback Item",
    quantity: 1,
    currencyId: 77,
    currencyNameKey: "fixture.trade.decoratedCurrency",
    currencyName: "Fixture Fallback Currency",
    price: 12345,
    serverId: 903,
    tradeId: 3106,
    configId: 4106,
    dailyPurchaseIndex: null,
    confirmedAfterTimeout: false,
  },
];

const previewTradeHistoryGameTexts = {
  "fixture.trade.speedup": "Fixture <color=#ff9900>Localized</color> Speedup",
  "fixture.trade.token": "<Fixture Localized Token>",
  "fixture.trade.decorated": "Fixture <color=#00ff00>Decorated</color> Item",
  "fixture.trade.decoratedCurrency": "Fixture <color=#00aaff>Decorated</color> Credit",
};

export function previewTradeFixture(previewState = "") {
  // App removes preview state in native/native-unavailable modes. An inactive
  // preview must not supply synthetic goods, purchase history or success stats.
  if (!previewState.startsWith("automation-")) return { goods: [], purchases: [], gameTexts: {}, loading: false, error: "", status: undefined };
  const empty = previewState === "automation-trade-empty";
  const loading = previewState === "automation-trade-loading" || previewState === "automation-trade-loading-retained";
  const fetchFailed = previewState === "automation-trade-error" || previewState === "automation-trade-error-retained";
  const retainedGoods = previewState === "automation-trade-loading-retained" || previewState === "automation-trade-error-retained";
  const absentStatus = previewState === "automation-trade-status-absent";
  const historyQa = previewState === "automation-trade-history";
  const emptyHistoryQa = previewState === "automation-trade-history-empty";
  // Template fetching and purchase status are independent in the recovered UI.
  // These are disclosed QA scenarios, not simulated template/network operations.
  const purchases = empty || emptyHistoryQa || absentStatus ? [] : historyQa ? previewTradeHistoryPurchases : previewTradePurchases;
  const resultState = previewState === "automation-trade-confirmed-timeout" ? "confirmed_after_timeout" : previewState === "automation-trade-status-failed" ? "failed" : previewState === "automation-trade-status-skipped" ? "skipped" : "success";
  return {
    goods: empty || ((loading || fetchFailed) && !retainedGoods) ? [] : previewTradeGoods,
    purchases,
    gameTexts: historyQa ? previewTradeHistoryGameTexts : {},
    loading,
    error: fetchFailed ? String(new Error("Fixture Trade goods request failed")) : "",
    status: absentStatus ? undefined : {
      detectedCount: empty ? 0 : 3,
      attemptedCount: empty ? 0 : 2,
      succeededCount: empty ? 0 : 2,
      lastResult: empty ? undefined : { state: resultState },
    },
  };
}

export const previewAssistTasks = [
  {
    uuid: "fixture-assist-1",
    ownerUid: "81001",
    ownerName: "Fixture Ally One",
    qualityKey: "ssr",
    isSpecial: false,
    star: 4,
    helpAvailable: true,
    completionTime: Date.UTC(2026, 9, 1, 19, 10, 0),
    items: [{ key: "fixture-assist-reward-1", name: "Fixture Reward", count: 3 }],
  },
  {
    uuid: "fixture-assist-2",
    ownerUid: "81002",
    ownerName: "Fixture Ally Two",
    qualityKey: "ur",
    isSpecial: true,
    star: 5,
    helpAvailable: false,
    completionTime: Date.UTC(2026, 9, 1, 20, 5, 0),
    items: [{ key: "fixture-assist-reward-2", name: "Fixture Premium Reward", count: 1 }],
  },
];

export const previewAssistJobs = [
  { uuid: "fixture-assist-1", scheduleSource: "manual", scheduleStatus: "scheduled", assistAt: Date.UTC(2026, 9, 1, 19, 2, 0), qualityKey: "ssr", ownerName: "Fixture Ally One", ownerUid: "81001", isSpecial: false },
  { uuid: "fixture-assist-2", scheduleSource: "manual", scheduleStatus: "failed", assistAt: Date.UTC(2026, 9, 1, 18, 48, 0), qualityKey: "ur", ownerName: "Fixture Ally Two", ownerUid: "81002", isSpecial: true },
];

export function previewAssistFixture(previewState) {
  const tasks = previewState === "automation-assist-empty" ? [] : previewAssistTasks;
  const task = previewAssistTasks[0];
  const statusByState = {
    "automation-assist-waiting": "waiting_connection",
    "automation-assist-retry-wait": "retry_wait",
    "automation-assist-running": "running",
    "automation-assist-failed": "failed",
    "automation-assist-expired": "expired",
  };
  const status = statusByState[previewState];
  const jobs = previewState === "automation-assist-schedule"
    ? previewAssistJobs
    : status
      ? [{ ...task, scheduleSource: "manual", scheduleStatus: status, assistAt: Date.UTC(2026, 9, 1, 19, 2, 0) }]
      : [];
  return { tasks, jobs, busy: previewState === "automation-assist-busy" };
}

export function previewResourceGatherConfig(previewState) {
  const noSquads = previewState === "automation-gather-no-squads";
  const enabled = [
    "automation-gather-runtime_wait",
    "automation-gather-manual_wait",
    "automation-gather-shield_paused",
    "automation-gather-recalling",
    "automation-gather-recall_failed",
    "automation-gather-state_unconfirmed",
    "automation-gather-view-change",
  ].includes(previewState);
  return {
    enabled,
    scanRadius: 200,
    manualResumeDelaySeconds: 120,
    recallOnDisable: false,
    squads: noSquads ? [] : [
      { squadIndex: 1, enabled: true, resource: "metal", level: 10, maxLevel: 10 },
      { squadIndex: 2, enabled: false, resource: "food", level: 8, maxLevel: 9 },
    ],
  };
}

export function previewResourceGatherRuntime(previewState) {
  const hasSquadData = previewState.startsWith("automation-") && previewState !== "automation-gather-no-squads";
  const status = {
    worldTileCount: 1000,
    gatherSquadIndexes: !hasSquadData ? [] : previewState === "automation-gather-view-change" ? [1, 2, 3] : [1, 2],
    gatherResources: [
      { resource: "metal", maxLevel: 10 },
      { resource: "food", maxLevel: 9 },
      { resource: "gold", maxLevel: 8 },
    ],
    step: previewState === "automation-gather-runtime_wait" ? "runtime_wait" : "idle",
    gatherSquads: [],
  };
  const squad = { squadIndex: 1, step: "idle" };
  if (previewState === "automation-gather-manual_wait") Object.assign(squad, { step: "manual_wait", pauseReason: "manual_wait", manualResumeAt: Date.UTC(2026, 9, 1, 19, 30, 0) });
  if (previewState === "automation-gather-shield_paused") Object.assign(squad, { step: "shield_paused", shieldEndAt: Date.UTC(2026, 9, 1, 20, 30, 0) });
  if (previewState === "automation-gather-recalling") squad.step = "recalling";
  if (previewState === "automation-gather-recall_failed") squad.step = "recall_failed";
  if (previewState === "automation-gather-state_unconfirmed") squad.step = "state_unconfirmed";
  if (hasSquadData) status.gatherSquads.push(squad);
  if (previewState === "automation-gather-view-change") status.gatherSquads.push({ squadIndex: 3, step: "idle" });
  return status;
}

export function validPreviewResourceGatherConfig(config) {
  return typeof config.enabled === "boolean"
    && [50, 100, 150, 200, 250, 300, 400, 500].includes(Number(config.scanRadius))
    && Number.isInteger(Number(config.manualResumeDelaySeconds))
    && Number(config.manualResumeDelaySeconds) >= 60
    && Number(config.manualResumeDelaySeconds) <= 1800
    && typeof config.recallOnDisable === "boolean"
    && Array.isArray(config.squads)
    && config.squads.every((squad) => Number.isInteger(squad.squadIndex) && ["metal", "food", "gold"].includes(squad.resource) && Number.isInteger(Number(squad.level)) && Number(squad.level) >= 1);
}

const fixtureTime = (hour, minute = 0) => Date.UTC(2026, 9, 1, hour, minute, 0);

export function previewAutomationRuntime(title, previewState = "") {
  const failed = previewState === "automation-runtime-error";
  const running = previewState === "automation-runtime-running";
  const state = failed ? "error" : running ? "running" : "waiting";
  const common = { state, lastRunAt: fixtureTime(17, 40), nextRunAt: fixtureTime(19, 20) };
  const byTitle = {
    "Automatic Construction": { ...common, candidateName: "Fixture Steelworks", candidateLevel: 29, automaticBuilders: 1, maxBuilders: 2, occupiedBuilders: 2, totalBuilders: 4, processed: 3, nextRunAt: fixtureTime(19, 5) },
    "Free Stamina": { ...common, todayCount: 1, dailyLimit: 2, processed: 1, nextClaimAt: fixtureTime(20, 0) },
    "Automatic Treatment": { ...common, wounded: 148, treating: 96, batchesStarted: 2, soldiersQueued: 96, helpsRequested: 2, collected: 52 },
    "Trucks": { ...common, qualified: 3, total: 4, refreshed: 2, departed: 2, claimed: 1, pendingClaims: 1, nextScheduledAt: fixtureTime(20, 15), nextContinuationAt: fixtureTime(19, 45), capabilities: { batchDeparture: true }, running },
    "Secret Task": { ...common, qualified: 4, available: 6, refreshed: 2, dispatched: 3, claimed: 2, pendingClaims: 1, nextScheduledAt: fixtureTime(20, 25), nextContinuationAt: fixtureTime(19, 55), capabilities: { superRefresh: false }, running },
    "Ghost Ops": { ...common, nextClaimAt: fixtureTime(19, 35), ownPending: 1, allianceCandidates: 2, claimed: 4 },
    "Alliance Tech Donations": { ...common, scienceId: 71001, remaining: 6, donated: 24 },
    "Automatic Official Application": { ...common, positionId: 10005, currentPositionId: 0, applyQueueLength: 3 },
    "Automatic Alliance Train Boarding": { ...common, currentCarriage: 2, queueLength: 3, maxPassenger: 5, currentReward: "Fixture Reward", isVip: true, vipCarriages: [1, 3] },
    "Alliance Help": { ...common, available: 7, processed: 5 },
    "Alliance Gifts": { ...common, normalClaimed: 8, advancedClaimed: 2 },
    "Excavation Stronghold Resources": { ...common, available: 3, claimed: 2 },
    "Alliance Center Resources": { ...common, available: 4, claimed: 3 },
    "Alliance Gathering Dispatch": { ...common, available: 2, dispatched: 1 },
    "Building Resource Collection": { ...common },
    "Armed Truck": { ...common },
    "Red Packet": { ...common, pendingCount: 2, lastResult: failed ? "failed" : "success" },
    "Fireworks / Egg": { ...common, pendingCount: 1, lastResult: failed ? "failed" : "success" },
    Treasure: { ...common, pendingCount: 2, dispatchPendingCount: 1, lastResult: failed ? "failed" : "success" },
    "Weekend Shield": { ...common, shielded: true, shieldEndAt: fixtureTime(23, 59), inWeekendWindow: true, weekendStartAt: fixtureTime(0, 0), weekendEndAt: fixtureTime(23, 59) },
    "Attack Shield": { ...common, shielded: previewState !== "automation-shield-unshielded", pendingReason: previewState === "automation-shield-pending" ? "fixture_attack_detected" : "-" },
  };
  return byTitle[title] || common;
}
