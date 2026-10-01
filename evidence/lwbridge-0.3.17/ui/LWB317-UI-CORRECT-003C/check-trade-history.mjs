import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildTradePurchaseDays,
  resolveTradeName,
  tradePurchaseDayStart,
  tradePurchaseRowKey,
} from "../../../../src/LWBridge.UI-0.3.17/src/tradePurchaseHistory.js";
import { previewTradeFixture } from "../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const sourcePath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js");
const pagesPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx");
const source = fs.readFileSync(sourcePath);
const pages = fs.readFileSync(pagesPath, "utf8");

const sha256 = (buffer) => crypto.createHash("sha256").update(buffer).digest("hex").toUpperCase();
assert.equal(sha256(source), "6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725");

const sourceLocators = {
  nameResolver: source.indexOf(Buffer.from("function C(")),
  historyRenderer: source.indexOf(Buffer.from("function de(")),
  purchasedItemsCaller: source.indexOf(Buffer.from("(0,S.jsx)(de,{language:s,gameTexts:v,purchases:a?.purchases??[]})")),
};
assert.deepEqual(sourceLocators, { nameResolver: 658, historyRenderer: 2378, purchasedItemsCaller: 10193 });

const dayOne = Date.UTC(2026, 9, 1, 0, 0, 0);
const dayTwo = Date.UTC(2026, 9, 2, 0, 0, 0);
const purchases = [
  { purchaseKey: "d1-a", serverDayStartAt: dayOne, purchasedAt: dayOne + 1, itemId: 11, quantity: 2 },
  { purchaseKey: "d1-b", serverDayStartAt: dayOne, purchasedAt: dayOne + 2, itemId: 12, quantity: 3 },
  { purchaseKey: "d2-a", serverDayStartAt: dayTwo, purchasedAt: dayTwo + 1, itemId: 11, quantity: 4 },
  { purchaseKey: "d2-b", serverDayStartAt: dayTwo, purchasedAt: dayTwo + 2, itemId: 12, quantity: 5 },
  { purchaseKey: "d2-c", serverDayStartAt: dayTwo, purchasedAt: dayTwo + 3, itemId: 11, quantity: 6 },
];
const days = buildTradePurchaseDays(purchases);
assert.deepEqual(days.map((day) => day.dayStartAt), [dayTwo, dayOne]);
assert.deepEqual(days[0].purchases.map((purchase) => purchase.purchaseKey), ["d2-c", "d2-b", "d2-a"]);
assert.equal(days[0].totalQuantity, 15);
assert.deepEqual([...days[0].itemCounts.values()].map(({ purchase, quantity }) => [purchase.itemId, quantity]), [[11, 10], [12, 5]]);

const adjacentOnly = buildTradePurchaseDays([
  { serverDayStartAt: dayOne, purchasedAt: dayOne + 1, itemId: 1, quantity: 1 },
  { serverDayStartAt: dayTwo, purchasedAt: dayTwo + 1, itemId: 2, quantity: 1 },
  { serverDayStartAt: dayOne, purchasedAt: dayOne + 2, itemId: 3, quantity: 1 },
]);
assert.deepEqual(adjacentOnly.map((day) => day.dayStartAt), [dayOne, dayTwo, dayOne]);

const localPurchaseTime = new Date(2026, 9, 3, 1, 30, 0).getTime();
assert.equal(tradePurchaseDayStart({ purchasedAt: localPurchaseTime }), new Date(2026, 9, 3, 0, 0, 0).getTime());
assert.equal(tradePurchaseDayStart({ purchasedAt: localPurchaseTime, serverDayStartAt: 123456 }), 123456);

assert.equal(resolveTradeName({ keyed: "Prefix <color=#ff00ff>Translated</color> suffix" }, "keyed", "Fallback", 7), "Prefix Translated suffix");
assert.equal(resolveTradeName({ keyed: "keyed" }, "keyed", "<Fallback>", 7), "Fallback");
assert.equal(resolveTradeName({}, "missing", "", 77), "#77");
assert.equal(tradePurchaseRowKey({ purchaseKey: "stable" }, 4), "stable");
assert.equal(tradePurchaseRowKey({ purchasedAt: 10, serverId: 20, tradeId: 30, configId: 40 }, 4), "10:20:30:40:4");

const historyFixture = previewTradeFixture("automation-trade-history");
assert.equal(historyFixture.purchases.length, 6);
assert.equal(historyFixture.purchases.filter((purchase) => purchase.itemId === 7001).length, 3);
assert.equal(historyFixture.purchases.at(-1).serverDayStartAt, undefined);
assert.equal(historyFixture.purchases.at(-1).dailyPurchaseIndex, null);
assert.equal(previewTradeFixture("automation-trade-history-empty").purchases.length, 0);

assert.match(pages, /const purchaseDays = buildTradePurchaseDays\(fixture\.purchases\);/);
assert.match(pages, /tradeTab === "purchases"/);
assert.match(pages, /setTradeTab\("purchases"\)/);
assert.match(pages, /fixture\.purchases\.length/);
assert.match(pages, /purchase\.dailyPurchaseIndex == null \? "-" : purchase\.dailyPurchaseIndex\.toLocaleString\(language\)/);
assert.match(pages, /purchase\.confirmedAfterTimeout \? <small>/);
assert.match(pages, /quality-\$\{purchase\.quality \|\| 0\}/);
assert.match(pages, /purchase\.currencyIconPath \? <span className="trade-station-currency-icon game-asset-placeholder"/);
assert.match(pages, /key=\{tradePurchaseRowKey\(purchase, index\)\}/);
assert.match(pages, /resolveTradeName\(fixture\.gameTexts, purchase\.itemNameKey, purchase\.itemName, purchase\.itemId\)/);
assert.match(pages, /<div role="tabpanel">\{purchaseDays\.length === 0 \? <span className="muted">/);

console.log(JSON.stringify({
  result: "LWB317_UI_CORRECT003C_TRADE_HISTORY_OK",
  sourceHash: sha256(source),
  sourceLocators,
  groupedDayStarts: days.map((day) => day.dayStartAt),
  newestDayRows: days[0].purchases.map((purchase) => purchase.purchaseKey),
  newestDayTotal: days[0].totalQuantity,
  newestDaySummary: [...days[0].itemCounts.values()].map(({ purchase, quantity }) => ({ itemId: purchase.itemId, quantity })),
  localFallbackDayStart: tradePurchaseDayStart({ purchasedAt: localPurchaseTime }),
  historyFixtureCount: historyFixture.purchases.length,
}, null, 2));
