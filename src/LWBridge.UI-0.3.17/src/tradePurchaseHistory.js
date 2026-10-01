export function resolveTradeName(gameTexts = {}, nameKey, fallbackName, id) {
  return ((nameKey && gameTexts[nameKey] && gameTexts[nameKey] !== nameKey ? gameTexts[nameKey] : fallbackName) || `#${id}`)
    .replace(/^<(.+)>$/, "$1")
    .replace(/<\/?color(?:=[^>]+)?>/gi, "");
}

export function tradePurchaseDayStart(purchase) {
  const purchasedAt = new Date(purchase.purchasedAt);
  return purchase.serverDayStartAt ?? new Date(purchasedAt.getFullYear(), purchasedAt.getMonth(), purchasedAt.getDate()).getTime();
}

export function buildTradePurchaseDays(purchases) {
  const days = [];
  for (const purchase of [...purchases].reverse()) {
    const dayStartAt = tradePurchaseDayStart(purchase);
    let day = days[days.length - 1];
    if (day?.dayStartAt !== dayStartAt) {
      day = { dayStartAt, purchases: [], totalQuantity: 0, itemCounts: new Map() };
      days.push(day);
    }
    day.purchases.push(purchase);
    day.totalQuantity += purchase.quantity;
    const item = day.itemCounts.get(purchase.itemId);
    if (item) item.quantity += purchase.quantity;
    else day.itemCounts.set(purchase.itemId, { purchase, quantity: purchase.quantity });
  }
  return days;
}

export function tradePurchaseRowKey(purchase, index) {
  return purchase.purchaseKey || `${purchase.purchasedAt}:${purchase.serverId}:${purchase.tradeId}:${purchase.configId}:${index}`;
}
