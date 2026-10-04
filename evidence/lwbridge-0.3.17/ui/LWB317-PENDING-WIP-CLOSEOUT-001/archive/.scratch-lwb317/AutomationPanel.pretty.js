import { At as e, D as t, Dt as n, E as r, Et as i, F as a, G as o, H as s, It as c, Mt as l, Ot as u, Q as ee, T as d, Tt as te, U as ne, W as f, g as p, jt as m, kt as h, l as g, m as _, o as v, rt as re, ut as ie, w as ae, wt as oe } from "./index-BVfnK1wp.js";
import { t as se } from "./rewardDisplay-eZWrd6iS.js";
import { t as y } from "./AutomationCard-LCx_jIi7.js";
import { t as b } from "./GameAssetImage-Diy9VTIr.js";
var x = c(l()), S = i(), ce = { enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] };
function le(e2) {
  return { ...ce, ...e2, selectedItemIds: e2?.selectedItemIds ?? [], selectedCurrencyIds: e2?.selectedCurrencyIds?.length ? e2.selectedCurrencyIds : ce.selectedCurrencyIds };
}
function C(e2, t2, n2, r2) {
  return ((t2 && e2[t2] && e2[t2] !== t2 ? e2[t2] : n2) || `#${r2}`).replace(/^<(.+)>$/, `$1`).replace(/<\/?color(?:=[^>]+)?>/gi, ``);
}
function ue({ online: e2, saving: t2, showExclusive: n2, selectedItemIds: r2, items: i2, gameTexts: a2 = {}, onToggle: o2 }) {
  let { t: s2 } = te();
  return (0, S.jsx)(`div`, { className: `trade-station-good-grid`, children: [...i2].filter((e3) => n2 || !e3.offers.every((e4) => e4.exclusive)).sort((e3, t3) => t3.quality - e3.quality || e3.itemId - t3.itemId).map((n3) => {
    let i3 = r2.includes(n3.itemId), c2 = n3.offers.every((e3) => e3.exclusive), l2 = C(a2, n3.nameKey, n3.name, n3.itemId), u2 = [], ee2 = /* @__PURE__ */ new Set();
    return n3.offers.forEach((e3) => {
      ee2.has(e3.currencyId) || (ee2.add(e3.currencyId), u2.push(C(a2, e3.currencyNameKey, e3.currencyName, e3.currencyId)));
    }), (0, S.jsxs)(`label`, { className: `trade-station-good${i3 ? ` selected` : ``}${c2 ? ` disabled` : ``}`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: i3, disabled: !e2 || t2 || c2, onChange: (e3) => o2(n3.itemId, e3.target.checked) }), (0, S.jsxs)(`span`, { className: `trade-station-good-frame quality-${n3.quality || 0}`, children: [n3.quality > 0 && (0, S.jsx)(b, { spriteName: `cfm_tongyong_daojukuang_${n3.quality}`, alt: ``, className: `trade-station-good-frame-image`, deferUntilVisible: true }), (0, S.jsx)(b, { assetPath: n3.iconPath, alt: l2, className: `trade-station-good-icon`, deferUntilVisible: true })] }), (0, S.jsxs)(`span`, { className: `trade-station-good-copy`, children: [(0, S.jsx)(`b`, { children: l2 }), (0, S.jsx)(`small`, { children: s2(`automation.tradeStation.buyWheneverAvailable`) }), (0, S.jsxs)(`small`, { children: [s2(`automation.tradeStation.possibleCurrencies`), u2.join(` / `) || `-`] }), c2 && (0, S.jsx)(`small`, { className: `trade-station-good-exclusive`, children: s2(`automation.tradeStation.exclusiveSkipped`) })] }), i3 && (0, S.jsx)(`span`, { className: `trade-station-good-check`, "aria-hidden": `true`, children: `\u2713` })] }, n3.itemId);
  }) });
}
function de({ language: e2, gameTexts: t2, purchases: n2 }) {
  let { t: r2 } = te();
  if (n2.length === 0) return (0, S.jsx)(`span`, { className: `muted`, children: r2(`automation.tradeStation.noPurchases`) });
  let i2 = [];
  for (let e3 of [...n2].reverse()) {
    let t3 = new Date(e3.purchasedAt), n3 = e3.serverDayStartAt ?? new Date(t3.getFullYear(), t3.getMonth(), t3.getDate()).getTime(), r3 = i2[i2.length - 1];
    r3?.dayStartAt !== n3 && (r3 = { dayStartAt: n3, purchases: [], totalQuantity: 0, itemCounts: /* @__PURE__ */ new Map() }, i2.push(r3)), r3.purchases.push(e3), r3.totalQuantity += e3.quantity;
    let a2 = r3.itemCounts.get(e3.itemId);
    a2 ? a2.quantity += e3.quantity : r3.itemCounts.set(e3.itemId, { purchase: e3, quantity: e3.quantity });
  }
  return (0, S.jsx)(`div`, { className: `trade-station-purchase-history`, children: i2.map((n3) => (0, S.jsxs)(`section`, { className: `trade-station-purchase-day`, children: [(0, S.jsxs)(`h4`, { className: `trade-station-purchase-day-heading`, children: [(0, S.jsx)(`time`, { dateTime: new Date(n3.dayStartAt).toISOString(), children: new Date(n3.dayStartAt).toLocaleDateString(e2, { year: `numeric`, month: `long`, day: `numeric`, weekday: `short` }) }), (0, S.jsxs)(`span`, { className: `trade-station-purchase-day-summary`, children: [(0, S.jsx)(`span`, { children: r2(`automation.tradeStation.purchasedDayTotal`, { count: n3.totalQuantity.toLocaleString(e2) }) }), [...n3.itemCounts.values()].map((n4) => (0, S.jsxs)(`span`, { className: `trade-station-purchase-day-item`, children: [C(t2, n4.purchase.itemNameKey, n4.purchase.itemName, n4.purchase.itemId), ` \xD7`, n4.quantity.toLocaleString(e2)] }, n4.purchase.itemId))] })] }), (0, S.jsx)(`div`, { className: `trade-station-purchase-list`, children: n3.purchases.map((n4, i3) => {
    let a2 = C(t2, n4.itemNameKey, n4.itemName, n4.itemId), o2 = C(t2, n4.currencyNameKey, n4.currencyName, n4.currencyId);
    return (0, S.jsxs)(`article`, { className: `trade-station-purchase`, children: [(0, S.jsxs)(`span`, { className: `trade-station-good-frame quality-${n4.quality || 0}`, children: [n4.quality > 0 && (0, S.jsx)(b, { spriteName: `cfm_tongyong_daojukuang_${n4.quality}`, alt: ``, className: `trade-station-good-frame-image`, deferUntilVisible: true }), (0, S.jsx)(b, { assetPath: n4.iconPath, alt: a2, className: `trade-station-good-icon`, deferUntilVisible: true })] }), (0, S.jsxs)(`span`, { className: `trade-station-purchase-copy`, children: [(0, S.jsx)(`b`, { children: a2 }), (0, S.jsx)(`small`, { children: r2(`automation.tradeStation.quantity`, { count: n4.quantity.toLocaleString(e2) }) }), (0, S.jsxs)(`small`, { className: `trade-station-purchase-price`, children: [n4.currencyIconPath && (0, S.jsx)(b, { assetPath: n4.currencyIconPath, alt: ``, className: `trade-station-currency-icon`, deferUntilVisible: true }), r2(`automation.tradeStation.price`, { price: n4.price.toLocaleString(e2), currency: o2 })] }), (0, S.jsx)(`small`, { children: r2(`automation.tradeStation.purchasedAt`, { time: new Date(n4.purchasedAt).toLocaleTimeString(e2) }) }), (0, S.jsx)(`small`, { children: r2(`automation.tradeStation.server`, { server: n4.serverId }) }), (0, S.jsx)(`small`, { children: r2(`automation.tradeStation.dailyPurchaseIndex`, { count: n4.dailyPurchaseIndex == null ? `-` : n4.dailyPurchaseIndex.toLocaleString(e2) }) }), n4.confirmedAfterTimeout && (0, S.jsx)(`small`, { children: r2(`automation.tradeStation.confirmedAfterTimeout`) })] })] }, n4.purchaseKey || `${n4.purchasedAt}:${n4.serverId}:${n4.tradeId}:${n4.configId}:${i3}`);
  }) })] }, n3.dayStartAt)) });
}
function fe({ online: e2, saving: t2, selectedCurrencyIds: n2, items: r2, gameTexts: i2 = {}, onToggle: a2 }) {
  let { t: o2 } = te(), s2 = /* @__PURE__ */ new Map();
  for (let e3 of r2) for (let t3 of e3.offers) t3.currencyId > 0 && !s2.has(t3.currencyId) && s2.set(t3.currencyId, t3);
  let c2 = [...s2.values()].sort((e3, t3) => e3.currencyId - t3.currencyId);
  return (0, S.jsxs)(`fieldset`, { className: `trade-station-currencies`, children: [(0, S.jsx)(`legend`, { children: o2(`automation.tradeStation.currencies`) }), (0, S.jsx)(`div`, { children: c2.map((r3) => {
    let o3 = r3.currencyId, s3 = n2.includes(o3), c3 = C(i2, r3.currencyNameKey, r3.currencyName, o3);
    return (0, S.jsxs)(`label`, { className: s3 ? `selected` : ``, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: s3, disabled: !e2 || t2 || s3 && n2.length === 1, onChange: (e3) => a2(o3, e3.target.checked) }), r3.currencyIconPath && (0, S.jsx)(b, { assetPath: r3.currencyIconPath, alt: ``, className: `trade-station-currency-icon` }), (0, S.jsx)(`span`, { children: c3 })] }, o3);
  }) })] });
}
function pe({ profileId: e2, online: r2, config: i2, status: a2 }) {
  let { language: s2, t: c2 } = te(), l2 = n(`tradeStation`, le(i2), { read: async () => le((await f(e2)).config?.trade_station), write: async (n2) => (await t(n2, e2)).config }, e2), u2 = l2.draft, [d2, ne2] = (0, x.useState)([]), [p2, m2] = (0, x.useState)(`goods`), [h2, _2] = (0, x.useState)(false), [v2, re2] = (0, x.useState)({}), [ie2, ae2] = (0, x.useState)(false), [se2, b2] = (0, x.useState)(``);
  (0, x.useEffect)(() => {
    let t2 = false;
    return b2(``), r2 ? (ae2(true), o(e2).then((e3) => {
      t2 || ne2(e3.items);
    }).catch((e3) => {
      t2 || b2(String(e3));
    }).finally(() => {
      t2 || ae2(false);
    }), () => {
      t2 = true;
    }) : (ae2(false), () => {
      t2 = true;
    });
  }, [r2, e2]);
  let ce2 = [...new Set([...d2.flatMap((e3) => [e3.nameKey, ...e3.offers.map((e4) => e4.currencyNameKey)]), ...(a2?.purchases ?? []).flatMap((e3) => [e3.itemNameKey, e3.currencyNameKey])].filter(Boolean))].sort().join(`\0`);
  (0, x.useEffect)(() => {
    let e3 = false;
    return re2({}), ce2 && ee(s2, ce2.split(`\0`)).then((t2) => {
      e3 || re2(t2);
    }).catch(() => {
    }), () => {
      e3 = true;
    };
  }, [s2, ce2]);
  async function C2(e3) {
    l2.state.edit(e3, false), await l2.state.flush().catch(() => void 0);
  }
  function pe2(e3, t2) {
    let n2 = t2 ? [.../* @__PURE__ */ new Set([...u2.selectedItemIds, e3])].sort((e4, t3) => e4 - t3) : u2.selectedItemIds.filter((t3) => t3 !== e3);
    C2({ ...u2, enabled: n2.length > 0 && u2.enabled, selectedItemIds: n2 });
  }
  function me2(e3, t2) {
    if (!t2 && u2.selectedCurrencyIds.length === 1) return;
    let n2 = t2 ? [.../* @__PURE__ */ new Set([...u2.selectedCurrencyIds, e3])].sort((e4, t3) => e4 - t3) : u2.selectedCurrencyIds.filter((t3) => t3 !== e3);
    C2({ ...u2, selectedCurrencyIds: n2 });
  }
  let he2 = u2.enabled || u2.selectedItemIds.length > 0, w2 = a2?.lastResult?.state;
  return (0, S.jsxs)(`div`, { className: `trade-station-panel`, children: [(0, S.jsx)(oe, { state: l2.state, label: c2(`automation.tradeStation.title`), disabled: !r2 }), (0, S.jsxs)(y, { online: r2, configDisabled: !r2, title: `automation.tradeStation.title`, description: `automation.tradeStation.description`, state: u2.enabled ? `common.waiting` : `common.disabled`, enabled: u2.enabled, disabled: !r2 || !he2, onToggle: (e3) => {
    C2({ ...u2, enabled: e3 });
  }, children: [(0, S.jsx)(`div`, { className: `trade-station-warning`, children: c2(`automation.tradeStation.warning`) }), (0, S.jsx)(g, { label: `automation.tradeStation.crossServer`, checked: u2.crossServerEnabled, disabled: !r2, onChange: (e3) => {
    C2({ ...u2, crossServerEnabled: e3 });
  } }), (0, S.jsx)(fe, { online: r2, saving: false, selectedCurrencyIds: u2.selectedCurrencyIds, items: d2, gameTexts: v2, onToggle: me2 }), (0, S.jsxs)(`div`, { className: `trade-station-stats`, children: [(0, S.jsxs)(`span`, { children: [c2(`automation.tradeStation.detected`), `: `, a2?.detectedCount ?? 0] }), (0, S.jsxs)(`span`, { children: [c2(`automation.tradeStation.attempted`), `: `, a2?.attemptedCount ?? 0] }), (0, S.jsxs)(`span`, { children: [c2(`automation.tradeStation.succeeded`), `: `, a2?.succeededCount ?? 0] }), (0, S.jsxs)(`span`, { children: [c2(`automation.tradeStation.lastResult`), `: `, w2 ? c2(`automation.tradeStation.state.${w2}`) : `-`] })] }), (0, S.jsxs)(`div`, { className: `trade-station-tabs`, role: `tablist`, children: [(0, S.jsx)(`button`, { type: `button`, role: `tab`, "aria-selected": p2 === `goods`, className: p2 === `goods` ? `active` : ``, onClick: () => m2(`goods`), children: c2(`automation.tradeStation.goods`) }), (0, S.jsx)(`button`, { type: `button`, role: `tab`, "aria-selected": p2 === `purchases`, className: p2 === `purchases` ? `active` : ``, onClick: () => m2(`purchases`), children: c2(`automation.tradeStation.purchasedItems`, { count: a2?.purchases?.length ?? 0 }) })] }), p2 === `goods` ? (0, S.jsxs)(`div`, { className: `trade-station-goods`, role: `tabpanel`, children: [(0, S.jsxs)(`div`, { className: `trade-station-goods-heading`, children: [(0, S.jsx)(`strong`, { children: c2(`automation.tradeStation.goodsToBuy`) }), (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: h2, onChange: (e3) => _2(e3.target.checked) }), c2(`automation.tradeStation.showExclusive`)] })] }), ie2 && (0, S.jsx)(`span`, { className: `muted`, children: c2(`automation.tradeStation.loading`) }), !ie2 && d2.length === 0 && (0, S.jsx)(`span`, { className: `muted`, children: c2(`automation.tradeStation.noGoods`) }), (0, S.jsx)(ue, { online: r2, saving: false, showExclusive: h2, selectedItemIds: u2.selectedItemIds, items: d2, gameTexts: v2, onToggle: pe2 })] }) : (0, S.jsx)(`div`, { role: `tabpanel`, children: (0, S.jsx)(de, { language: s2, gameTexts: v2, purchases: a2?.purchases ?? [] }) }), se2 && (0, S.jsx)(`div`, { className: `automation-error`, children: se2 })] })] });
}
function me({ config: e2, status: t2, squadIndexes: n2, onSave: r2, online: i2 = true }) {
  let { t: a2, language: o2 } = te(), [s2, c2] = (0, x.useState)({}), [l2, u2] = (0, x.useState)(``), d2 = t2?.gatherResources ?? [], ne2 = JSON.stringify(d2.map((e3) => e3.nameKey).filter(Boolean));
  (0, x.useEffect)(() => {
    let e3 = true;
    return c2({}), ee(o2, JSON.parse(ne2)).then((t3) => {
      e3 && c2(t3);
    }).catch(() => {
    }), () => {
      e3 = false;
    };
  }, [o2, ne2]);
  let f2 = e2?.enabled ?? false, p2 = t2?.worldTileCount ?? 1e3, m2 = [.../* @__PURE__ */ new Set([...n2, ...t2?.gatherSquadIndexes ?? [], ...(e2?.squads ?? []).map((e3) => e3.squadIndex)])].sort((e3, t3) => e3 - t3).map((t3) => e2?.squads?.find((e3) => e3.squadIndex === t3) ?? { squadIndex: t3, enabled: false, resource: `metal`, level: 10 });
  function h2(t3, n3) {
    if (t3 && !n3.some((e3) => e3.enabled)) {
      u2(`automation.resourceGather.squadRequired`);
      return;
    }
    u2(``), r2({ ...e2, enabled: t3, squads: n3 });
  }
  return (0, S.jsxs)(y, { online: i2, title: `automation.category.resourceGather`, description: `automation.resourceGather.description`, state: f2 ? `common.waiting` : `common.disabled`, enabled: f2, onToggle: (e3) => h2(e3, m2), error: l2 || void 0, children: [(0, S.jsxs)(`div`, { className: `automation-resource-gather-options`, children: [(0, S.jsxs)(`label`, { className: `automation-resource-gather-radius`, children: [(0, S.jsx)(`span`, { children: a2(`automation.resourceGather.scanRadius`) }), (0, S.jsx)(`select`, { "aria-label": a2(`automation.resourceGather.scanRadius`), value: e2?.scanRadius ?? 200, onChange: (t3) => r2({ ...e2, enabled: f2, squads: m2, scanRadius: Number(t3.target.value) }), children: [50, 100, 150, 200, 250, 300, 400, 500].map((e3) => (0, S.jsx)(`option`, { value: e3, children: e3 }, e3)) })] }), (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`span`, { children: a2(`automation.resourceGather.manualResumeDelay`) }), (0, S.jsx)(`select`, { value: (e2?.manualResumeDelaySeconds ?? 120) / 60, onChange: (t3) => r2({ ...e2, enabled: f2, squads: m2, manualResumeDelaySeconds: Number(t3.target.value) * 60 }), children: Array.from({ length: 30 }, (e3, t3) => t3 + 1).map((e3) => (0, S.jsx)(`option`, { value: e3, children: e3 }, e3)) })] }), (0, S.jsxs)(`label`, { className: `automation-resource-gather-recall`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: e2?.recallOnDisable ?? false, onChange: (t3) => r2({ ...e2, enabled: f2, squads: m2, recallOnDisable: t3.target.checked }) }), (0, S.jsx)(`span`, { children: a2(`automation.resourceGather.recallOnDisable`) })] }), (0, S.jsx)(`small`, { className: `automation-resource-gather-radius-hint muted`, children: a2(`automation.resourceGather.scanRadiusHint`, { size: p2 }) })] }), m2.length === 0 && (0, S.jsx)(`p`, { className: `muted`, children: a2(`automation.resourceGather.squadsLoading`) }), m2.map((e3) => {
    let n3 = d2.find((t3) => t3.resource === e3.resource), r3 = t2?.gatherSquads?.find((t3) => t3.squadIndex === e3.squadIndex), i3 = (t3) => {
      let n4 = m2.map((n5) => n5.squadIndex === e3.squadIndex ? { ...n5, ...t3 } : n5);
      h2(f2 && n4.some((e4) => e4.enabled), n4);
    }, c3 = r3?.step === `recalling` || r3?.step === `recall_failed` || r3?.step === `state_unconfirmed` ? r3.step : f2 && e3.enabled ? t2?.step === `runtime_wait` ? `runtime_wait` : r3?.step ?? `idle` : `disabled`;
    return (0, S.jsxs)(`div`, { className: `automation-resource-gather-squad`, role: `group`, "aria-label": a2(`automation.squad`, { index: e3.squadIndex }), children: [(0, S.jsxs)(`label`, { className: `automation-resource-gather-enable`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: e3.enabled, onChange: (e4) => i3({ enabled: e4.target.checked }) }), (0, S.jsx)(`span`, { children: a2(`automation.squad`, { index: e3.squadIndex }) })] }), (0, S.jsx)(`select`, { "aria-label": `${a2(`automation.squad`, { index: e3.squadIndex })} ${a2(`automation.resourceGather.resource`)}`, value: e3.resource, onChange: (e4) => i3({ resource: e4.target.value }), children: [`metal`, `food`, `gold`].map((e4) => {
      let t3 = d2.find((t4) => t4.resource === e4);
      return (0, S.jsx)(`option`, { value: e4, children: t3 && s2[t3.nameKey] || a2(`common.loading`) }, e4);
    }) }), (0, S.jsx)(`select`, { "aria-label": `${a2(`automation.squad`, { index: e3.squadIndex })} ${a2(`automation.resourceGather.level`)}`, value: e3.level, onChange: (e4) => i3({ level: Number(e4.target.value) }), children: Array.from({ length: Math.min(10, n3?.maxLevel ?? 10) }, (e4, t3) => t3 + 1).map((e4) => (0, S.jsx)(`option`, { value: e4, disabled: e4 > (n3?.maxLevel ?? 10), children: e4 }, e4)) }), (0, S.jsxs)(`span`, { className: `automation-resource-gather-state muted`, children: [a2(`automation.resourceGather.state.${c3}`), f2 && e3.enabled && r3?.pauseReason === `manual_wait` && r3.manualResumeAt && (0, S.jsx)(`small`, { children: a2(`automation.resourceGather.manualResumeAt`, { time: new Date(r3.manualResumeAt).toLocaleTimeString(o2) }) }), f2 && e3.enabled && r3?.shieldEndAt && (0, S.jsx)(`small`, { children: a2(`automation.resourceGather.shieldEndAt`, { time: new Date(r3.shieldEndAt).toLocaleString(o2) }) })] })] }, e3.squadIndex);
  })] });
}
function he({ profileId: e2, config: t2, status: n2, disabled: r2, online: i2 = true, onPatch: a2, onFlush: o2 }) {
  let { t: s2, language: c2 } = te(), [l2, u2] = (0, x.useState)({}), [d2, ne2] = (0, x.useState)(false), f2 = n2?.camps ?? [], p2 = JSON.stringify([...new Set(f2.flatMap((e3) => [e3.nameKey, ...Object.values(e3.soldierNameKeys ?? {})]).filter(Boolean))].sort());
  (0, x.useEffect)(() => {
    let e3 = true;
    return u2({}), p2 !== `[]` && ee(c2, JSON.parse(p2)).then((t3) => {
      e3 && u2(t3);
    }).catch(() => {
    }), () => {
      e3 = false;
    };
  }, [p2, c2, e2]);
  let m2 = n2?.order, h2 = t2?.totalCount ?? 0, g2 = Number.isInteger(h2) && h2 > 0 && h2 <= 1e6, v2 = t2?.trainEnabled === true && m2?.reason !== `identity_changed` && !(t2.enabled && m2?.activationId === t2.activationId && n2?.trainEnabled === false), re2 = t2?.enabled ?? false, ie2 = t2?.targetLevel ?? 0, ae2 = [.../* @__PURE__ */ new Set([...f2.flatMap((e3) => e3.availableLevels), ...ie2 > 0 ? [ie2] : []])].sort((e3, t3) => e3 - t3);
  return (0, S.jsxs)(y, { online: i2, title: `automation.soldierTraining.title`, description: `automation.soldierTraining.description`, state: n2?.state === `error` ? `common.failed` : n2?.state === `checking` ? `automation.running` : `common.waiting`, enabled: re2, onToggle: (e3) => {
    if (!e3) {
      a2({ enabled: false });
      return;
    }
    g2 && a2({ enabled: true, trainEnabled: true, promoteEnabled: true, collectEnabled: true, orderId: m2 && ![`identity_changed`, `unconfirmed`].includes(m2.reason) && m2.total === h2 && m2.completed < m2.total && m2.orderId ? m2.orderId : crypto.randomUUID(), activationId: crypto.randomUUID() });
  }, disabled: r2, children: [n2?.state === `error` && (0, S.jsx)(`p`, { role: `alert`, children: s2(`automation.soldierTraining.stateError`) }), (0, S.jsxs)(`div`, { className: `soldier-training-fields`, children: [(0, S.jsxs)(`label`, { children: [s2(`automation.soldierTraining.totalCount`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1e6, step: 1, value: h2 || ``, disabled: r2 || re2 && v2, "aria-invalid": h2 !== 0 && !g2, onBlur: o2, onChange: (e3) => a2({ totalCount: Number(e3.target.value), trainEnabled: false }) })] }), (0, S.jsxs)(`label`, { children: [s2(`automation.soldierTraining.target`), (0, S.jsxs)(`select`, { value: ie2, disabled: r2, onChange: (e3) => a2({ targetLevel: Number(e3.target.value) }), children: [(0, S.jsx)(`option`, { value: 0, children: s2(`automation.soldierTraining.highest`) }), ae2.map((e3) => {
    let t3 = f2.find((t4) => t4.availableLevels.includes(e3)), n3 = l2[t3?.soldierNameKeys?.[String(e3)] ?? ``];
    return (0, S.jsxs)(`option`, { value: e3, disabled: !t3, children: [s2(`automation.soldierTraining.level`, { level: e3 }), n3 ? ` \xB7 ${n3}` : ``] }, e3);
  })] })] })] }), (0, S.jsx)(`p`, { className: `muted`, children: s2(`automation.soldierTraining.quantityHint`) }), h2 !== 0 && !g2 && (0, S.jsx)(`p`, { role: `alert`, children: s2(`automation.soldierTraining.quantityError`) }), m2?.orderId && (0, S.jsxs)(`div`, { role: `status`, children: [(0, S.jsx)(`p`, { children: s2(`automation.soldierTraining.progress`, m2) }), (0, S.jsx)(`p`, { className: `muted`, children: s2(`automation.soldierTraining.order.${m2.reason}`) })] }), (0, S.jsx)(`p`, { className: `muted`, children: s2(`automation.soldierTraining.summary`, { trained: n2?.trained ?? 0, promoted: n2?.promoted ?? 0, collected: n2?.collected ?? 0 }) }), f2.length === 0 && (0, S.jsx)(`p`, { className: `muted`, children: s2(`automation.soldierTraining.noCamps`) }), n2?.reason === `data_unavailable` && (0, S.jsx)(`p`, { role: `status`, children: s2(`automation.soldierTraining.reason.data_unavailable`) }), d2 && (0, S.jsx)(`p`, { role: `alert`, children: s2(`automation.soldierTraining.openFailed`) }), (0, S.jsx)(`div`, { className: `soldier-training-camps`, children: f2.map((t3, i3) => (0, S.jsxs)(`div`, { className: `soldier-training-camp`, children: [(0, S.jsxs)(`button`, { type: `button`, title: s2(`automation.soldierTraining.openCamp`), disabled: r2 || !n2?.atHome, onClick: () => {
    ne2(false), _(`openSoldierTrainingCamp`, { uuid: t3.uuid }, e2).catch(() => ne2(true));
  }, children: [l2[t3.nameKey] || s2(`automation.soldierTraining.building`), ` `, i3 + 1] }), (0, S.jsx)(`span`, { className: `muted`, children: s2(`automation.soldierTraining.level`, { level: t3.level }) }), (0, S.jsx)(`span`, { className: `soldier-training-camp-detail`, title: t3.lastError || void 0, children: t3.productionCount > 0 ? s2(`automation.soldierTraining.production`, { level: t3.productionLevel, count: t3.productionCount, time: new Date(t3.completionTime).toLocaleString(c2, { month: `numeric`, day: `numeric`, hour: `2-digit`, minute: `2-digit` }) }) : s2(`automation.soldierTraining.reason.${t3.reason}`) })] }, t3.uuid)) })] });
}
var w = [{ name: `buildingResources`, title: `automation.buildingResources.title`, description: `automation.buildingResources.description` }, { name: `armedTruckReward`, title: `automation.armedTruckReward.title`, description: `automation.armedTruckReward.description` }], ge = [`ssr`, `ur`, `ssr`, `ssr`, `ssr`, `ur`, `ssr`], _e = [`none`, `ur`, `none`, `none`, `none`, `ur`, `none`], ve = [{ id: 10002, label: `automation.position.vicePresident` }, { id: 10003, label: `automation.position.strategyMinister` }, { id: 10004, label: `automation.position.defenseMinister` }, { id: 10005, label: `automation.position.constructionMinister` }, { id: 10006, label: `automation.position.scienceMinister` }, { id: 10007, label: `automation.position.internalAffairsMinister` }], ye = [`n`, `r`, `sr`, `ssr`, `ur`, `special`], be = [`sr`, `ur`, `special`], xe = [1, 2, 3, 4];
function T(e2, t2) {
  return e2 ? new Date(e2).toLocaleString(t2) : `-`;
}
function E(e2, t2, n2 = Date.now()) {
  if (!e2 || !Number.isFinite(e2) || e2 <= n2) return null;
  let r2 = new Date(e2), i2 = new Date(n2), a2 = Math.round((Date.UTC(r2.getFullYear(), r2.getMonth(), r2.getDate()) - Date.UTC(i2.getFullYear(), i2.getMonth(), i2.getDate())) / 864e5), o2 = a2 === 0 || a2 === 1 ? `${new Intl.RelativeTimeFormat(t2, { numeric: `auto` }).format(a2, `day`)} ${r2.toLocaleTimeString(t2, { hour: `2-digit`, minute: `2-digit` })}` : r2.toLocaleString(t2, { year: r2.getFullYear() === i2.getFullYear() ? void 0 : `numeric`, month: `numeric`, day: `numeric`, hour: `2-digit`, minute: `2-digit` });
  return (0, S.jsx)(`time`, { dateTime: r2.toISOString(), title: r2.toLocaleString(t2), children: o2 });
}
function D(e2) {
  return e2 == null || e2 === `` ? `-` : String(e2);
}
function Se(e2, t2, n2) {
  let r2 = e2.indexOf(t2), i2 = e2.indexOf(n2);
  if (r2 < 0 || i2 < 0 || r2 === i2) return e2;
  let a2 = [...e2];
  return a2.splice(i2, 0, a2.splice(r2, 1)[0]), a2;
}
function O(e2, t2) {
  return t2 === false || e2 === `disabled` || t2 === void 0 && e2 === `stopped` ? `common.disabled` : e2 === `running` || e2 === `checking` ? `automation.running` : e2 === `success` ? `common.success` : e2 === `failed` || e2 === `error` ? `common.failed` : `common.waiting`;
}
function Ce(e2, t2) {
  return e2 === true ? `common.available` : e2 === false ? t2 : `common.notChecked`;
}
function we(e2) {
  return [...new Set(e2.split(/\r?\n/).map((e3) => e3.trim()).filter(Boolean))];
}
function Te(e2, t2) {
  return e2 === `reward` || t2 === `reward`;
}
var Ee = [`construction`, `officialPosition`, `allianceTrainRide`, `stamina`, `treatment`, `soldierTraining`, `allianceDonate`, `allianceHelp`, `allianceGift`, `strongholdResource`, `allianceCenterResource`, `allianceGather`, `resourceGather`, `railway`, `dispatch`, `dispatchAssist`, `ghostRecon`], De = /* @__PURE__ */ new Set([`targetLevel`, `maxBuilders`, `amountPerArmy`, `threshold`, `intervalMinutes`, `delayMinutes`, `positionId`, `ticketCount`, `intervalSeconds`]);
function Oe(e2) {
  return Object.fromEntries(Object.entries(e2).map(([e3, t2]) => [e3, De.has(e3) ? Number(t2) : e3 === `delaySeconds` && Array.isArray(t2) ? t2.map(Number) : t2]));
}
function ke(e2) {
  return { enabled: e2?.enabled === true, min: String(e2?.claimDelaySeconds?.[0] ?? 0), max: String(e2?.claimDelaySeconds?.[1] ?? 0), replyEnabled: e2?.replyEnabled === true, replies: (e2?.replyPhrases ?? []).join(`
`), dispatchEnabled: e2?.dispatchEnabled === true, dispatchSquads: e2?.dispatchSquadPriority ?? [], searchEnabled: e2?.searchEnabled === true, dispatchRetrySeconds: String(e2?.dispatchRetrySeconds ?? 30), replyMin: String(e2?.replyDelaySeconds?.[0] ?? 2), replyMax: String(e2?.replyDelaySeconds?.[1] ?? 5), dispatchMin: String(e2?.dispatchDelaySeconds?.[0] ?? 2), dispatchMax: String(e2?.dispatchDelaySeconds?.[1] ?? 5) };
}
function Ae({ activeCategory: t2, onActiveCategoryChange: n2, profileId: i2, autoWeekendShield: o2, autoAttackShield: c2, online: l2, busy: _2, resourceBusy: ce2, chatBusy: le2, config: C2, redPacketConfig: ue2, treasureConfig: de2, fireworksConfig: fe2, tradeStationConfig: Ae2, status: k, resourceStatus: je, onToggle: Me, onBackgroundToggle: A, onBackgroundRun: Ne, onConstructionClaimAll: Pe, onInspect: Fe, onRunTask: Ie, onResourceRun: Le, onChatRun: Re }) {
  let { language: j, t: M } = te(), N = i2 ?? u(), ze = (0, x.useRef)(l2);
  ze.current = l2, (0, x.useSyncExternalStore)(m, e, e);
  let Be = JSON.stringify(C2 ?? {}), P = Object.fromEntries(Ee.map((e2) => [e2, h(N, `task:${e2}`, C2?.[e2] ?? {}, { read: async () => (await f(N)).config?.tasks?.[e2] ?? {}, write: async (t3) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n3 = Oe(t3);
    return await ae(e2, n3, N), n3;
  }, valid: (t3) => {
    if (e2 === `soldierTraining`) {
      let e3 = Number(t3.totalCount ?? 0);
      return Number.isInteger(e3) && e3 >= 0 && e3 <= 1e6 && (!t3.trainEnabled || e3 > 0);
    }
    let n3 = { targetLevel: [1, 100], maxBuilders: [1, Math.max(1, k?.tasks.construction?.totalBuilders ?? 20)], amountPerArmy: [1, 1e6], threshold: [1, 30], intervalMinutes: [1, 1440], delayMinutes: [0, 1440], ticketCount: [1, 9999], intervalSeconds: [5, 300], positionId: [0, 10007] };
    if (!Object.entries(t3).every(([e3, t4]) => !De.has(e3) || String(t4).trim() !== `` && Number.isInteger(Number(t4)) && Number(t4) >= n3[e3][0] && Number(t4) <= n3[e3][1])) return false;
    if (Array.isArray(t3.delaySeconds)) {
      let [e3, n4] = t3.delaySeconds;
      if ([e3, n4].some((e4) => String(e4).trim() === `` || !Number.isInteger(Number(e4))) || Number(e3) < 0 || Number(n4) > 86400 || Number(e3) > Number(n4)) return false;
    }
    return true;
  } })])), F = Object.fromEntries(Ee.map((e2) => [e2, P[e2].getSnapshot().draft])), I = Object.fromEntries([`redPacket`, `treasure`, `fireworks`].map((e2) => [e2, h(N, `chat:${e2}`, ke(e2 === `redPacket` ? ue2 : e2 === `treasure` ? de2 : fe2), { read: async () => {
    let t3 = (await f(N)).config?.chat_automation;
    return ke(e2 === `redPacket` ? t3?.red_packet : e2 === `treasure` ? t3?.treasure : t3?.fireworks);
  }, write: async (t3) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n3 = Vr(e2, t3);
    if (!n3) throw Error(`CONFIG_DRAFT_INVALID`);
    return await d(e2, n3, N), t3;
  }, valid: (t3) => Vr(e2, t3) !== null })])), L = Object.fromEntries(w.map(({ name: e2 }) => [e2, h(N, `resource:${e2}`, { enabled: je?.tasks[e2].enabled ?? false, intervalMinutes: je?.tasks[e2].intervalMinutes ?? 60 }, { read: async () => {
    let t3 = (await s(N)).tasks[e2];
    return { enabled: t3.enabled, intervalMinutes: t3.intervalMinutes };
  }, write: async (t3) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n3 = { ...t3, intervalMinutes: Number(t3.intervalMinutes) };
    return await r(e2, n3, N), n3;
  }, valid: (e3) => Number.isInteger(Number(e3.intervalMinutes)) && Number(e3.intervalMinutes) >= 1 && Number(e3.intervalMinutes) <= 1440 })])), Ve = Object.fromEntries(w.map(({ name: e2 }) => [e2, String(L[e2].getSnapshot().draft.intervalMinutes)])), He = Object.fromEntries(w.map(({ name: e2 }) => [e2, L[e2].getSnapshot().draft.enabled])), Ue = Object.fromEntries([...Ee.map((e2) => [e2, P[e2].getSnapshot()]), ...Object.entries(I).map(([e2, t3]) => [`chat:${e2}`, t3.getSnapshot()]), ...Object.entries(L).map(([e2, t3]) => [`resource:${e2}`, t3.getSnapshot()])].map(([e2, t3]) => [e2, t3.error ? `error` : t3.saving ? `saving` : ``]));
  function R(e2) {
    let [t3, n3] = e2.split(`:`);
    return t3 === `chat` ? I[n3] : t3 === `resource` ? L[n3] : P[t3];
  }
  function z(e2) {
    R(e2)?.flush().catch(() => void 0);
  }
  let We = (0, x.useRef)(false), Ge = I.redPacket.getSnapshot().draft, Ke = I.treasure.getSnapshot().draft, qe = I.fireworks.getSnapshot().draft, Je = (e2) => I.redPacket.edit(e2, false), Ye = (e2) => I.treasure.edit(e2, false), Xe = (e2) => I.fireworks.edit(e2, false), Ze = String(P.allianceDonate.getSnapshot().draft.threshold ?? 15), Qe = (e2) => P.allianceDonate.edit((t3) => ({ ...t3, threshold: e2 }), false), $e = String(P.treatment.getSnapshot().draft.amountPerArmy ?? 1), et = (e2) => P.treatment.edit((t3) => ({ ...t3, amountPerArmy: e2 }), false), [tt, nt] = (0, x.useState)({}), [rt, it] = (0, x.useState)({}), [at, ot] = (0, x.useState)(`daily`), B = t2 ?? at;
  (0, x.useEffect)(() => {
    l2 && B === `resourceGather` && Fe(`resourceGather`), l2 && B === `daily` && (Fe(`soldierTraining`), Fe(`construction`));
  }, [l2, B, i2]);
  let [V, st] = (0, x.useState)(() => /* @__PURE__ */ new Set([B])), ct = String(P.railway.getSnapshot().draft.delayMinutes ?? 2), lt = (e2) => P.railway.edit((t3) => ({ ...t3, delayMinutes: e2 }), false), ut = String(P.dispatch.getSnapshot().draft.delayMinutes ?? 3), dt = (e2) => P.dispatch.edit((t3) => ({ ...t3, delayMinutes: e2 }), false), ft = String(P.allianceGift.getSnapshot().draft.intervalMinutes ?? 120), pt = (e2) => P.allianceGift.edit((t3) => ({ ...t3, intervalMinutes: e2 }), false), mt = String(P.strongholdResource.getSnapshot().draft.intervalMinutes ?? 60), ht = (e2) => P.strongholdResource.edit((t3) => ({ ...t3, intervalMinutes: e2 }), false), gt = String(P.allianceCenterResource.getSnapshot().draft.intervalMinutes ?? 60), _t = (e2) => P.allianceCenterResource.edit((t3) => ({ ...t3, intervalMinutes: e2 }), false), vt = String(P.construction.getSnapshot().draft.maxBuilders ?? 1), yt = (e2) => P.construction.edit((t3) => ({ ...t3, maxBuilders: e2 }), false), bt = String(P.officialPosition.getSnapshot().draft.positionId ?? 0), xt = (e2) => P.officialPosition.edit((t3) => ({ ...t3, positionId: e2 }), false), [H, St] = (0, x.useState)([]), [Ct, wt] = (0, x.useState)(null), [Tt, Et] = (0, x.useState)(null), [Dt, Ot] = (0, x.useState)(null), [kt, At] = (0, x.useState)(null), [jt, Mt] = (0, x.useState)(null), [Nt, Pt] = (0, x.useState)(null), [Ft, It] = (0, x.useState)(``), [Lt, Rt] = (0, x.useState)(false), [zt, Bt] = (0, x.useState)(`like`), Vt = String(P.allianceTrainRide.getSnapshot().draft.ticketCount ?? 1), Ht = (e2) => P.allianceTrainRide.edit((t3) => ({ ...t3, ticketCount: e2 }), false), [Ut, Wt] = (0, x.useState)([]), [Gt, Kt] = (0, x.useState)(null), qt = P.dispatchAssist.getSnapshot().draft.qualities ?? [], Jt = (e2) => P.dispatchAssist.edit((t3) => ({ ...t3, qualities: e2 }), false), Yt = String(P.dispatchAssist.getSnapshot().draft.delaySeconds?.[0] ?? 0), Xt = (e2) => P.dispatchAssist.edit((t3) => ({ ...t3, delaySeconds: [e2, t3.delaySeconds?.[1] ?? 0] }), false), Zt = String(P.dispatchAssist.getSnapshot().draft.delaySeconds?.[1] ?? 0), Qt = (e2) => P.dispatchAssist.edit((t3) => ({ ...t3, delaySeconds: [t3.delaySeconds?.[0] ?? 0, e2] }), false), $t = String(P.dispatchAssist.getSnapshot().draft.intervalSeconds ?? 30), en = (e2) => P.dispatchAssist.edit((t3) => ({ ...t3, intervalSeconds: e2 }), false), [tn, nn] = (0, x.useState)([]), [rn, an] = (0, x.useState)(``), [on, U] = (0, x.useState)(``);
  (0, x.useEffect)(() => {
    for (let e2 of Ee) P[e2].receive(C2?.[e2] ?? {});
  }, [Be]), (0, x.useEffect)(() => {
    I.redPacket.receive(ke(ue2)), I.treasure.receive(ke(de2)), I.fireworks.receive(ke(fe2));
    for (let { name: e2 } of w) {
      let t3 = je?.tasks[e2];
      t3 && L[e2].receive({ enabled: t3.enabled, intervalMinutes: t3.intervalMinutes });
    }
  }, [JSON.stringify([ue2, de2, fe2, je?.tasks])]);
  async function W(e2, t3) {
    P[e2].edit((e3) => ({ ...e3, ...t3 }), false), await P[e2].flush().catch(() => void 0);
  }
  function G(e2, t3, n3) {
    P[t3].edit((e3) => ({ ...e3, ...n3 }));
  }
  (0, x.useEffect)(() => {
    if (!l2) return;
    let e2 = false;
    return ne().then((t3) => {
      if (e2) return;
      let n3 = [...new Set((t3.squads || []).map((e3) => e3.index))].sort((e3, t4) => e3 - t4);
      n3.length > 0 && Wt(n3);
    }).catch(() => {
    }), () => {
      e2 = true;
    };
  }, [l2, B]), (0, x.useEffect)(() => {
    if (!l2 || B !== `daily`) return;
    let e2 = true, t3 = () => a().then((t4) => {
      e2 && Kt(t4);
    }).catch(() => void 0);
    t3();
    let n3 = Math.min(300, Math.max(5, Number(F?.dispatchAssist?.intervalSeconds) || 30)), r2 = window.setInterval(t3, n3 * 1e3);
    return () => {
      e2 = false, window.clearInterval(r2);
    };
  }, [B, F?.dispatchAssist?.intervalSeconds, l2, M]);
  function sn(e2, t3) {
    let n3 = H.indexOf(e2), r2 = n3 + t3;
    if (n3 < 0 || r2 < 0 || r2 >= H.length) return;
    let i3 = [...H];
    [i3[n3], i3[r2]] = [i3[r2], i3[n3]], St(i3), W(`allianceTrainRide`, { preferredRewardKeys: i3 });
  }
  (0, x.useEffect)(() => {
    St(F?.allianceTrainRide?.preferredRewardKeys ?? []), Rt(F?.allianceTrainRide?.preferRewardQuantity ?? false), Bt(F?.allianceTrainRide?.thanksMode ?? `like`);
  }, [(F?.allianceTrainRide?.preferredRewardKeys ?? []).join(`\0`), F?.allianceTrainRide?.preferRewardQuantity, F?.allianceTrainRide?.thanksMode, F?.allianceTrainRide?.ticketCount]), (0, x.useEffect)(() => {
    if (!l2) {
      We.current = false;
      return;
    }
    B === `alliance` && !We.current && (We.current = true, Fe(`allianceTrainRide`));
  }, [B, l2, Fe]);
  let K = k?.tasks.construction, cn = Array.isArray(K?.buildingTypes) ? K.buildingTypes : [], [ln, un] = (0, x.useState)({}), dn = JSON.stringify([...new Set(cn.map((e2) => e2.nameKey).filter(Boolean))].sort());
  (0, x.useEffect)(() => {
    let e2 = true;
    return un({}), dn !== `[]` && ee(j, JSON.parse(dn)).then((t3) => {
      e2 && un(t3);
    }).catch(() => {
    }), () => {
      e2 = false;
    };
  }, [j, dn, N]);
  let fn = (0, x.useId)(), [pn, mn] = (0, x.useState)(`all`);
  (0, x.useEffect)(() => {
    mn(`all`);
  }, [N]);
  let q = [`all`, `economy`, `military`, `decoration`, `season`, `other`].filter((e2) => e2 === `all` || cn.some((t3) => (t3.category ?? `other`) === e2)), hn = q.includes(pn) ? pn : `all`, gn = cn.filter((e2) => hn === `all` || (e2.category ?? `other`) === hn), _n = F?.construction?.targetEnabled ?? false, vn = F?.construction?.buildingTypeIds ?? [], yn = F?.construction?.targetLevel ?? 30, bn = (e2) => {
    let t3 = ln[e2.nameKey];
    return t3 && t3 !== e2.nameKey ? t3 : e2.name || M(`automation.currentBuilding`);
  }, xn = k?.tasks.stamina, J = k?.tasks.treatment, Sn = k?.tasks.allianceDonate, Cn = k?.tasks.officialPosition, Y = k?.tasks.allianceTrainRide, wn = k?.tasks.allianceHelp, Tn = k?.tasks.allianceGift, En = k?.tasks.strongholdResource, Dn = k?.tasks.allianceCenterResource, On = k?.tasks.allianceGather, X = k?.tasks.railway, Z = k?.tasks.dispatch, kn = k?.tasks.ghostRecon, An = F?.construction?.enabled ?? K?.enabled ?? false, jn = F?.construction?.autoClaimCompleted ?? true, Mn = Number(vt), Nn = Math.max(1, K?.totalBuilders ?? 20), Pn = Number.isInteger(Mn) && Mn >= 1 && Mn <= Nn, Fn = F?.stamina?.enabled ?? xn?.enabled ?? false, In = F?.treatment?.enabled ?? J?.enabled ?? false, Ln = Number($e), Rn = Number.isInteger(Ln) && Ln >= 1 && Ln <= 1e6, zn = F?.allianceDonate?.enabled ?? Sn?.enabled ?? false, Bn = F?.officialPosition?.enabled ?? Cn?.enabled ?? false, Vn = Number(bt), Hn = Vn === 0 || ve.some((e2) => e2.id === Vn), Un = F?.allianceTrainRide?.enabled ?? Y?.enabled ?? false, Wn = F?.allianceTrainRide?.autoAcceptVip ?? false, Gn = (F?.allianceTrainRide?.fixedCarriageIds ?? []).filter((e2) => e2 >= 1 && e2 <= 4), Kn = (F?.allianceTrainRide?.vipFixedCarriageIds ?? []).filter((e2) => e2 >= 1 && e2 <= 4), qn = F?.allianceTrainRide?.selectionMode ?? (Gn.length === 1 ? `fixed` : `reward`), Jn = F?.allianceTrainRide?.vipSelectionMode ?? (Kn.length === 2 ? `fixed` : `reward`), Yn = Te(qn, Jn), Xn = Y?.rewardOptions ?? [], Zn = new Map(Xn.map((e2) => [e2.key, e2])), Qn = new Set(H), $n = [...H.map((e2) => Zn.get(e2)).filter((e2) => e2 !== void 0), ...Xn.filter((e2) => !Qn.has(e2.key))];
  Number(Ze);
  let er = F?.allianceHelp?.enabled ?? wn?.enabled ?? false, tr = F?.allianceGift?.enabled ?? Tn?.enabled ?? false, nr = F?.strongholdResource?.enabled ?? En?.enabled ?? false, rr = F?.allianceCenterResource?.enabled ?? Dn?.enabled ?? false, ir = F?.allianceGather?.enabled ?? On?.enabled ?? false, ar = F?.allianceGather?.squadPriority ?? [], or = new Set(ar), sr = [...ar, ...Ut.filter((e2) => !or.has(e2))], cr = F?.railway?.enabled ?? X?.enabled ?? false, lr = F?.railway?.departWhenTicketsInsufficient ?? false, ur = F?.dispatch?.autoExecute ?? F?.dispatch?.enabled ?? false, dr = F?.dispatch?.collectRewards ?? F?.dispatch?.enabled ?? false, fr = ur || dr, pr = F?.ghostRecon?.autoStartOwn ?? false, mr = F?.ghostRecon?.autoJoinAlliance ?? false, hr = F?.ghostRecon?.autoClaimRewards ?? false, gr = F?.ghostRecon?.allianceFilter ?? `special`, _r = pr || mr || hr, vr = F?.dispatchAssist?.autoHelp ?? F?.dispatchAssist?.enabled ?? false, yr = Number(Yt), br = Number(Zt), xr = Number($t), Sr = new Map((Gt?.jobs ?? []).map((e2) => [e2.uuid, e2])), Cr = F?.railway?.weeklyQualities ?? ge, wr = F?.dispatch?.weeklyQualities ?? _e, Tr = Number(ct), Er = Number(ut), Dr = Number(ft), Or = Number.isInteger(Tr) && Tr >= 0 && Tr <= 1440, kr = Number.isInteger(Er) && Er >= 0 && Er <= 1440, Ar = Number.isInteger(Dr) && Dr >= 1 && Dr <= 1440, jr = Number(mt), Mr = Number(gt), Nr = Number.isInteger(jr) && jr >= 1 && jr <= 1440, Pr = Number.isInteger(Mr) && Mr >= 1 && Mr <= 1440, Fr = Z?.details, Q = k?.services?.shield, $ = (e2) => !l2 || _2 === e2, Ir = Object.values(Ue), Lr = Ir.includes(`error`) ? `error` : Ir.includes(`saving`) ? `saving` : Ir.includes(`saved`) ? `saved` : ``, Rr = Array.from({ length: 7 }, (e2, t3) => new Intl.DateTimeFormat(j, { weekday: `short`, timeZone: `UTC` }).format(new Date(Date.UTC(2024, 0, t3 + 1))));
  function zr(e2, t3, n3) {
    return (0, S.jsx)(`div`, { className: `automation-weekly-quality`, children: Rr.map((r2, i3) => (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`span`, { children: r2 }), (0, S.jsxs)(`select`, { value: t3[i3], disabled: $(e2) || n3, onChange: (n4) => {
      let r3 = [...t3];
      r3[i3] = n4.target.value, W(e2, { weeklyQualities: r3 });
    }, children: [(0, S.jsx)(`option`, { value: `none`, children: M(`automation.noQualityRefresh`) }), (0, S.jsx)(`option`, { value: `ssr`, children: M(`automation.ssrOrAbove`) }), (0, S.jsx)(`option`, { value: `ur`, children: `UR` })] })] }, r2)) });
  }
  async function Br(e2, t3, n3 = Number(Ve[e2])) {
    L[e2].edit({ enabled: t3, intervalMinutes: n3 }, false), await L[e2].flush().catch(() => void 0);
  }
  function Vr(e2, t3) {
    let n3 = Number(t3.min), r2 = Number(t3.max), i3 = we(t3.replies), a2 = e2 === `treasure` ? 600 : 60, o3 = t3.min.trim() !== `` && t3.max.trim() !== `` && Number.isFinite(n3) && Number.isFinite(r2) && n3 >= 0 && r2 <= a2 && n3 <= r2, s2 = ``;
    if (o3 ? [[t3.replyMin, t3.replyMax], ...e2 === `treasure` ? [[t3.dispatchMin, t3.dispatchMax]] : []].some(([e3, t4]) => !e3.trim() || !t4.trim() || !Number.isFinite(Number(e3)) || !Number.isFinite(Number(t4)) || Number(e3) < 0.1 || Number(t4) > 600 || Number(e3) > Number(t4)) ? s2 = M(`automation.interactionDelayError`) : t3.replyEnabled && i3.length === 0 ? s2 = M(`automation.replyRequired`) : e2 === `treasure` && t3.dispatchEnabled && !t3.dispatchSquads?.length ? s2 = M(`automation.treasureDispatchSquadRequired`) : e2 === `treasure` && (!Number.isInteger(Number(t3.dispatchRetrySeconds)) || Number(t3.dispatchRetrySeconds) < 1 || Number(t3.dispatchRetrySeconds) > 300) && (s2 = M(`automation.treasureDispatchRetryError`)) : s2 = M(e2 === `treasure` ? `automation.treasureDelayError` : e2 === `fireworks` ? `automation.fireworksDelayError` : `automation.redPacketDelayError`), nt((t4) => ({ ...t4, [e2]: s2 })), s2) return null;
    let c3 = { enabled: t3.enabled, claimDelaySeconds: [n3, r2], replyEnabled: t3.replyEnabled, replyPhrases: i3, replyDelaySeconds: [Number(t3.replyMin), Number(t3.replyMax)] };
    return e2 === `treasure` && (c3.searchEnabled = t3.searchEnabled === true, c3.dispatchDelaySeconds = [Number(t3.dispatchMin), Number(t3.dispatchMax)], c3.dispatchEnabled = t3.dispatchEnabled === true, c3.dispatchSquadPriority = t3.dispatchSquads ?? [], c3.dispatchRetrySeconds = Number(t3.dispatchRetrySeconds)), c3;
  }
  async function Hr(e2, t3) {
    I[e2].edit(t3, false), await I[e2].flush().catch(() => void 0);
  }
  async function Ur(e2) {
    try {
      let t3 = await I[e2].runAction(() => Re(e2));
      it((n3) => ({ ...n3, [e2]: t3 > 0 ? M(`automation.queued`, { count: t3 }) : M(`automation.nothingToClaim`) }));
    } catch {
      it((t3) => ({ ...t3, [e2]: M(`automation.runFailed`) }));
    }
  }
  function Wr(e2, t3 = qt, n3 = yr, r2 = br, i3 = xr) {
    if (!Number.isInteger(n3) || !Number.isInteger(r2) || n3 < 0 || r2 > 86400 || n3 > r2 || !Number.isInteger(i3) || i3 < 5 || i3 > 300 || e2 && !t3.length) {
      U(M(`automation.dispatchAssistConfigError`));
      return;
    }
    U(``), W(`dispatchAssist`, { autoHelp: e2, qualities: t3, delaySeconds: [n3, r2], intervalSeconds: i3 });
  }
  function Gr(e2, t3, n3) {
    if (!(Number.isInteger(e2) && Number.isInteger(t3) && e2 >= 0 && t3 <= 86400 && e2 <= t3 && Number.isInteger(n3) && n3 >= 5 && n3 <= 300)) {
      R(`dispatchAssist:settings`)?.pause(), U(M(`automation.dispatchAssistConfigError`));
      return;
    }
    U(``), P.dispatchAssist.edit((r2) => ({ ...r2, autoHelp: vr, qualities: qt, delaySeconds: [e2, t3], intervalSeconds: n3 }));
  }
  function Kr(e2) {
    let t3 = e2.qualityKey.toUpperCase(), n3 = e2.isSpecial ? ` \xB7 ${M(`automation.assistQuality.special`)}` : ``;
    return `${e2.ownerName || e2.ownerUid} \xB7 ${t3}${n3}`;
  }
  async function qr() {
    if (tn.length) {
      an(`schedule`);
      try {
        await ie(tn), nn([]), Kt(await a()), U(``);
      } catch {
        U(``);
      } finally {
        an(``);
      }
    }
  }
  async function Jr(e2, t3) {
    an(`${e2}:${t3}`);
    try {
      e2 === `cancel` ? await p(t3) : await re(t3), Kt(await a()), U(``);
    } catch {
      U(``);
    } finally {
      an(``);
    }
  }
  function Yr(e2) {
    return e2.starIcons?.length ? (0, S.jsx)(`span`, { className: `automation-assist-stars`, "aria-label": `${e2.star}`, children: e2.starIcons.map((e3, t3) => (0, S.jsx)(b, { assetPath: e3, alt: ``, className: `automation-assist-star` }, `${e3}:${t3}`)) }) : null;
  }
  function Xr(e2) {
    return (0, S.jsx)(`span`, { className: `automation-assist-rewards`, children: (e2.items ?? []).map((e3, t3) => {
      let n3 = `${e3.name || e3.key} \xD7${D(e3.count)}`;
      return (0, S.jsxs)(`span`, { className: `map-reward-item`, title: n3, "aria-label": n3, children: [(0, S.jsx)(b, { assetPath: e3.iconPath, alt: e3.name || e3.key, className: `map-reward-icon` }), (0, S.jsxs)(`strong`, { children: [`\xD7`, se(e3.count)] })] }, `${e3.key}:${t3}`);
    }) });
  }
  function Zr(e2) {
    let t3 = e2 === `redPacket`, n3 = e2 === `treasure`, r2 = t3 ? k?.services?.redPacket : n3 ? k?.services?.treasure : k?.services?.fireworks, i3 = t3 ? Ge : n3 ? Ke : qe, a2 = t3 ? Je : n3 ? Ye : Xe, o3 = `chat:${e2}`, s2 = (t4, n4 = false) => {
      let r3 = { ...i3, ...t4 };
      a2(r3), n4 ? (R(o3)?.pause(), Hr(e2, r3)) : I[e2].edit(r3);
    }, c3 = t3 ? `automation.redPacket.title` : n3 ? `automation.treasure.title` : `automation.fireworks.title`, u2 = i3.dispatchSquads ?? [], ee2 = new Set(u2), d2 = [...u2, ...Ut.filter((e3) => !ee2.has(e3))];
    return (0, S.jsxs)(y, { online: l2, title: c3, description: M(`automation.chatClaimDescription`, { type: M(c3) }), state: O(r2?.lastResult?.state, i3.enabled), enabled: i3.enabled, disabled: !l2 || le2 === e2, onToggle: (e3) => s2({ enabled: e3 }, true), actionLabel: `common.runNow`, actionBusy: le2 === e2, onAction: () => void Ur(e2), summaryRows: [[`automation.pendingClaims`, r2?.pendingCount], n3 ? [`automation.treasureDispatchPending`, r2?.dispatchPendingCount] : [`automation.latestResult`, rt[e2] || (r2?.lastResult?.state === `success` ? `common.success` : r2?.lastResult?.state === `failed` ? `common.failed` : null)]], error: tt[e2] || void 0, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.claim`) }), n3 && (0, S.jsx)(`p`, { className: `hint`, children: M(`automation.treasureTargetDelayHint`) }), (0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.minDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: n3 ? 600 : 60, step: 0.01, value: i3.min, onChange: (e3) => s2({ min: e3.target.value }), onBlur: () => z(o3) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.maxDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: n3 ? 600 : 60, step: 0.01, value: i3.max, onChange: (e3) => s2({ max: e3.target.value }), onBlur: () => z(o3) })] })] })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.autoReply`, checked: i3.replyEnabled, disabled: !l2 || le2 === e2, onChange: (e3) => s2({ replyEnabled: e3 }, true) }), i3.replyEnabled && (0, S.jsxs)(`div`, { className: `automation-subsettings`, children: [(0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.replyDelayMin`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i3.replyMin, onChange: (e3) => s2({ replyMin: e3.target.value }), onBlur: () => z(o3) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.replyDelayMax`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i3.replyMax, onChange: (e3) => s2({ replyMax: e3.target.value }), onBlur: () => z(o3) })] })] }), (0, S.jsxs)(`label`, { className: `automation-replies`, children: [M(`automation.replyPhrases`), (0, S.jsx)(`textarea`, { rows: 4, value: i3.replies, onChange: (e3) => s2({ replies: e3.target.value }), onBlur: () => z(o3) })] })] })] }), n3 && (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.treasureAutoSearch`, checked: i3.searchEnabled === true, disabled: !l2 || le2 === e2, onChange: (e3) => s2({ searchEnabled: e3 }, true) }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.treasureAutoSearchHint`) }), (0, S.jsx)(g, { label: `automation.treasureAutoDispatch`, checked: i3.dispatchEnabled === true, disabled: !l2 || le2 === e2, onChange: (e3) => s2({ dispatchEnabled: e3 }, true) }), i3.dispatchEnabled === true && (0, S.jsxs)(`div`, { className: `automation-subsettings`, children: [(0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.dispatchDelayMin`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i3.dispatchMin, onChange: (e3) => s2({ dispatchMin: e3.target.value }), onBlur: () => z(o3) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.dispatchDelayMax`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i3.dispatchMax, onChange: (e3) => s2({ dispatchMax: e3.target.value }), onBlur: () => z(o3) })] })] }), (0, S.jsxs)(`label`, { children: [M(`automation.treasureDispatchRetrySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 300, step: 1, value: i3.dispatchRetrySeconds, onChange: (e3) => s2({ dispatchRetrySeconds: e3.target.value }), onBlur: () => z(o3) })] }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": M(`automation.treasureDispatchPriorityHint`), children: d2.map((e3) => {
      let t4 = u2.indexOf(e3) >= 0;
      return (0, S.jsxs)(`div`, { className: `automation-squad-priority-item${t4 ? ` selected` : ``}${Dt === e3 ? ` dragging` : ``}${kt === e3 ? ` drag-over` : ``}`, draggable: t4, onDragStart: (n4) => {
        t4 && (n4.dataTransfer.effectAllowed = `move`, n4.dataTransfer.setData(`text/plain`, String(e3)), Ot(e3));
      }, onDragOver: (n4) => {
        !t4 || Dt === null || Dt === e3 || (n4.preventDefault(), n4.dataTransfer.dropEffect = `move`, At(e3));
      }, onDrop: (n4) => {
        n4.preventDefault();
        let r3 = Dt;
        if (Ot(null), At(null), r3 === null || !t4) return;
        let i4 = Se(u2, r3, e3);
        i4 !== u2 && s2({ dispatchSquads: i4 }, true);
      }, onDragEnd: () => {
        Ot(null), At(null);
      }, children: [(0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t4, onChange: (t5) => s2({ dispatchSquads: t5.target.checked ? [...u2, e3] : u2.filter((t6) => t6 !== e3) }, true) }), (0, S.jsx)(`span`, { children: M(`automation.squad`, { index: e3 }) })] }), t4 && (0, S.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) })] }, e3);
    }) }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.treasureDispatchPriorityHint`) })] })] })] }, e2);
  }
  return (0, S.jsxs)(`section`, { className: `panel`, children: [Object.entries(P).map(([e2, t3]) => (0, S.jsx)(oe, { disabled: !l2, state: t3, label: M(e2 === `dispatch` ? `automation.secretTask.title` : e2 === `dispatchAssist` ? `automation.dispatchAssist` : e2 === `ghostRecon` ? `automation.ghost.title` : `automation.${e2}.title`) }, e2)), Object.entries(I).map(([e2, t3]) => (0, S.jsx)(oe, { disabled: !l2, state: t3, label: M(`automation.${e2}.title`) }, e2)), w.map(({ name: e2, title: t3 }) => (0, S.jsx)(oe, { disabled: !l2, state: L[e2], label: M(t3) }, e2)), (0, S.jsxs)(`div`, { className: `panel-title`, children: [(0, S.jsx)(`h2`, { children: M(`nav.automation`) }), (0, S.jsx)(`span`, { className: Lr === `error` ? `error-text` : `muted`, children: M(Lr ? `automation.configSave.${Lr}` : l2 ? `status.gameConnected` : `status.gameDisconnectedDisabled`) })] }), (0, S.jsx)(`div`, { className: `automation-categories`, role: `tablist`, children: [`daily`, `alliance`, `resourceGather`, `resources`, `chat`, `trade`, `system`].map((e2) => (0, S.jsx)(`button`, { className: B === e2 ? `active` : ``, role: `tab`, "aria-selected": B === e2, onClick: () => {
    st((t3) => {
      if (t3.has(e2)) return t3;
      let n3 = new Set(t3);
      return n3.add(e2), n3;
    }), n2 ? n2(e2) : ot(e2);
  }, children: M(`automation.category.${e2}`) }, e2)) }), (0, S.jsxs)(`div`, { className: `automation-grid`, children: [V.has(`resourceGather`) && (0, S.jsx)(x.Activity, { mode: B === `resourceGather` ? `visible` : `hidden`, children: (0, S.jsx)(me, { online: l2, config: P.resourceGather.getSnapshot().draft, status: k?.tasks.resourceGather, squadIndexes: Ut, onSave: (e2) => {
    W(`resourceGather`, e2);
  } }) }), V.has(`trade`) && (0, S.jsx)(x.Activity, { mode: B === `trade` ? `visible` : `hidden`, children: (0, S.jsx)(pe, { profileId: i2, online: l2, config: Ae2, status: k?.services?.tradeStation }) }), V.has(`system`) && (0, S.jsx)(x.Activity, { mode: B === `system` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(y, { online: l2, title: `automation.weekendShield.title`, description: `automation.weekendShield.description`, state: o2 ? `common.waiting` : `common.disabled`, enabled: o2, disabled: !l2, onToggle: (e2) => Me(`autoWeekendShield`, e2), statusRows: [[`automation.currentShield`, Q?.shielded ? `common.on` : `common.off`], [`automation.shieldEnds`, T(Q?.shieldEndAt, j)], [`automation.weekendWindow`, Q?.inWeekendWindow ? `common.yes` : `common.no`], [`automation.weekendWindowLocal`, Q?.weekendStartAt && Q?.weekendEndAt ? `${T(Q.weekendStartAt, j)} \u2013 ${T(Q.weekendEndAt, j)}` : `-`]] }), (0, S.jsx)(y, { online: l2, title: `automation.attackShield.title`, description: `automation.attackShield.description`, state: c2 ? `common.waiting` : `common.disabled`, enabled: c2, disabled: !l2, onToggle: (e2) => Me(`autoAttackShield`, e2), statusRows: [[`automation.currentShield`, Q?.shielded ? `common.on` : `common.off`], [`automation.pendingReason`, Q?.pendingReason || `-`]] })] }) }), V.has(`chat`) && (0, S.jsx)(x.Activity, { mode: B === `chat` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [Zr(`redPacket`), Zr(`fireworks`), Zr(`treasure`)] }) }), V.has(`resources`) && (0, S.jsx)(x.Activity, { mode: B === `resources` ? `visible` : `hidden`, children: w.map((e2) => {
    let t3 = je?.tasks[e2.name], n3 = He[e2.name], r2 = Number(Ve[e2.name]), i3 = Number.isInteger(r2) && r2 >= 1 && r2 <= 1440;
    return (0, S.jsx)(y, { online: l2, title: e2.title, settingsCollapsible: false, description: e2.description, state: O(t3?.state, n3), enabled: n3, disabled: !l2 || ce2 === e2.name, onToggle: (t4) => {
      Br(e2.name, t4);
    }, actionLabel: `common.runNow`, actionBusy: ce2 === e2.name, onAction: () => {
      L[e2.name].runAction(() => Le(e2.name)).catch(() => void 0);
    }, statusRows: [[`automation.lastRun`, T(t3?.lastRunAt, j)], [`automation.nextRun`, n3 ? E(t3?.nextRunAt, j) : `-`]], error: i3 ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: Ve[e2.name], onBlur: () => {
      z(`resource:${e2.name}`);
    }, onChange: (t4) => {
      let n4 = t4.target.value;
      L[e2.name].edit((e3) => ({ ...e3, intervalMinutes: n4 }));
    } })] }) }) }, e2.name);
  }) }), V.has(`daily`) && (0, S.jsx)(x.Activity, { mode: B === `daily` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(he, { online: l2, profileId: N, config: F.soldierTraining, status: k?.tasks.soldierTraining, disabled: $(`soldierTraining`), onPatch: (e2) => P.soldierTraining.edit((t3) => ({ ...t3, ...e2 })), onFlush: () => {
    P.soldierTraining.flush().catch(() => void 0);
  } }, N), (0, S.jsxs)(y, { online: l2, title: `automation.construction.title`, description: `automation.construction.description`, state: O(K?.state, An), enabled: An, disabled: $(`construction`), onToggle: (e2) => A(`construction`, e2), actionLabel: `automation.claimAllBuildingRewards`, actionBusy: _2 === `construction`, onAction: Pe, summaryRows: [[`automation.automaticBuilders`, K?.automaticBuilders == null ? null : `${K.automaticBuilders} / ${K.maxBuilders ?? Mn}`], [`automation.buildersTotal`, K?.occupiedBuilders == null || K.totalBuilders == null ? null : `${K.occupiedBuilders} / ${K.totalBuilders}`]], statusRows: [[`automation.currentBuilding`, ln[cn.find((e2) => e2.itemId === K?.candidate?.itemId)?.nameKey ?? ``] || K?.candidate?.name || K?.candidate?.uuid || `-`], [`automation.currentLevel`, D(K?.candidate?.level)], [`automation.automaticBuilders`, `${D(K?.automaticBuilders ?? 0)} / ${D(K?.maxBuilders ?? Mn)}`], [`automation.buildersTotal`, `${D(K?.occupiedBuilders ?? 0)} / ${D(K?.totalBuilders ?? 0)}`], [`automation.processed`, D(K?.processed ?? 0)], [`automation.nextCheck`, T(K?.nextRunAt, j)]], error: Pn ? void 0 : `automation.builderLimitError`, children: [(0, S.jsx)(g, { variant: `checkbox`, label: `automation.construction.targetEnabled`, checked: _n, disabled: $(`construction`), onChange: (e2) => {
    W(`construction`, { targetEnabled: e2 });
  } }), _n && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.construction.targetLevel`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 100, step: 1, value: yn, disabled: $(`construction`), onChange: (e2) => P.construction.edit((t3) => ({ ...t3, targetLevel: e2.target.value })), onBlur: () => z(`construction:targetLevel`) })] }) }), (0, S.jsxs)(`details`, { className: `construction-type-select`, children: [(0, S.jsxs)(`summary`, { children: [(0, S.jsx)(`span`, { children: M(`automation.construction.buildingTypes`) }), (0, S.jsx)(`strong`, { children: vn.length ? cn.filter((e2) => vn.includes(e2.itemId)).map(bn).join(`\u3001`) : M(`automation.construction.selectTypes`) }), (0, S.jsx)(`span`, { className: `construction-select-arrow`, "aria-hidden": `true`, children: `\u2304` })] }), (0, S.jsx)(`div`, { className: `construction-category-tabs`, role: `tablist`, "aria-label": M(`automation.construction.buildingTypes`), children: q.map((e2) => (0, S.jsx)(`button`, { type: `button`, role: `tab`, id: `${fn}-${e2}`, "aria-controls": fn, "aria-selected": hn === e2, tabIndex: hn === e2 ? 0 : -1, onClick: () => mn(e2), onKeyDown: (t3) => {
    let n3 = q.indexOf(e2), r2 = t3.key === `ArrowRight` ? (n3 + 1) % q.length : t3.key === `ArrowLeft` ? (n3 + q.length - 1) % q.length : t3.key === `Home` ? 0 : t3.key === `End` ? q.length - 1 : -1;
    r2 < 0 || (t3.preventDefault(), mn(q[r2]), t3.currentTarget.parentElement?.querySelectorAll(`[role="tab"]`)[r2]?.focus());
  }, children: M(`automation.construction.category.${e2}`) }, e2)) }), (0, S.jsx)(`fieldset`, { className: `construction-target-types`, role: `tabpanel`, id: fn, "aria-labelledby": `${fn}-${hn}`, disabled: $(`construction`), children: gn.map((e2) => (0, S.jsxs)(`label`, { className: `automation-checkbox-row`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: vn.includes(e2.itemId), onChange: (t3) => {
    W(`construction`, { buildingTypeIds: t3.target.checked ? [...vn, e2.itemId] : vn.filter((t4) => t4 !== e2.itemId) });
  } }), (0, S.jsx)(`span`, { children: bn(e2) })] }, e2.itemId)) })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(vn.length ? `automation.construction.targetHint` : `automation.construction.selectTypes`) })] }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoCollectRewards`, checked: jn, disabled: $(`construction`), onChange: (e2) => {
    W(`construction`, { autoClaimCompleted: e2 });
  } }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.maxBuilders`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: Nn, step: 1, value: vt, disabled: $(`construction`), onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    yt(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= Nn ? G(`construction:maxBuilders`, `construction`, { maxBuilders: n3 }) : R(`construction:maxBuilders`)?.pause();
  }, onBlur: () => z(`construction:maxBuilders`) })] }) })] }), (0, S.jsx)(y, { online: l2, title: `automation.stamina.title`, description: `automation.stamina.description`, state: O(xn?.state, Fn), enabled: Fn, disabled: $(`stamina`), onToggle: (e2) => A(`stamina`, e2), statusRows: [[`automation.claimedToday`, `${D(xn?.todayCount)} / ${D(xn?.dailyLimit)}`], [`automation.processed`, D(xn?.processed ?? 0)], [`automation.nextClaim`, E(xn?.nextClaimAt ?? xn?.nextRunAt, j)]] }), (0, S.jsx)(y, { online: l2, title: `automation.treatment.title`, settingsCollapsible: false, description: `automation.treatment.description`, state: O(J?.state, In), enabled: In, disabled: $(`treatment`), onToggle: (e2) => A(`treatment`, e2), summaryRows: [[`automation.wounded`, J?.wounded], [`automation.treating`, J?.treating]], statusRows: [[`automation.wounded`, D(J?.wounded ?? 0)], [`automation.treating`, D(J?.treating ?? 0)], [`automation.treatmentBatches`, D(J?.batchesStarted ?? 0)], [`automation.soldiersQueued`, D(J?.soldiersQueued ?? 0)], [`automation.helpsRequested`, D(J?.helpsRequested ?? 0)], [`automation.collected`, D(J?.collected ?? 0)], [`automation.nextCheck`, T(J?.nextRunAt, j)]], error: Rn ? void 0 : `automation.treatmentAmountError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.treatmentAmountPerArmy`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1e6, step: 1, value: $e, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    et(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= 1e6 ? G(`treatment:amountPerArmy`, `treatment`, { amountPerArmy: n3 }) : R(`treatment:amountPerArmy`)?.pause();
  }, onBlur: () => z(`treatment:amountPerArmy`) })] }) }) })] }) }), V.has(`alliance`) && (0, S.jsx)(x.Activity, { mode: B === `alliance` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(y, { online: l2, title: `automation.allianceDonate.title`, settingsCollapsible: false, description: `automation.allianceDonate.description`, state: O(Sn?.state, zn), enabled: zn, disabled: $(`allianceDonate`), onToggle: (e2) => A(`allianceDonate`, e2), summaryRows: [[`automation.remaining`, Sn?.remaining], [`automation.donated`, Sn?.donated]], statusRows: [[`automation.currentTech`, D(Sn?.scienceId)], [`automation.remaining`, D(Sn?.remaining)], [`automation.donated`, D(Sn?.donated ?? 0)], [`automation.nextCheck`, T(Sn?.nextRunAt, j)]], children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.donateThreshold`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 30, step: 1, value: Ze, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    Qe(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= 30 ? G(`allianceDonate:threshold`, `allianceDonate`, { threshold: n3 }) : R(`allianceDonate:threshold`)?.pause();
  }, onBlur: () => z(`allianceDonate:threshold`) })] }) }) }), (0, S.jsx)(y, { online: l2, title: `automation.officialPosition.title`, settingsCollapsible: false, description: `automation.officialPosition.description`, state: O(Cn?.state, Bn), enabled: Bn, disabled: $(`officialPosition`) || Vn === 0, configDisabled: $(`officialPosition`), onToggle: (e2) => A(`officialPosition`, e2), statusRows: [[`automation.targetPosition`, D(Cn?.positionId || Vn)], [`automation.currentPosition`, D(Cn?.currentPositionId)], [`automation.applicationQueue`, D(Cn?.applyQueueLength ?? 0)], [`automation.nextCheck`, T(Cn?.nextRunAt, j)]], error: Hn ? void 0 : `automation.positionError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.targetPosition`), (0, S.jsxs)(`select`, { value: bt, disabled: $(`officialPosition`), onChange: (e2) => {
    let t3 = Number(e2.target.value);
    xt(e2.target.value), W(`officialPosition`, { positionId: t3 });
  }, children: [(0, S.jsx)(`option`, { value: `0`, children: M(`automation.position.none`) }), ve.map((e2) => (0, S.jsx)(`option`, { value: e2.id, children: M(e2.label) }, e2.id))] })] }) }) }), (0, S.jsxs)(y, { online: l2, title: `automation.allianceTrainRide.title`, description: `automation.allianceTrainRide.description`, state: O(Y?.state, Un), enabled: Un, disabled: $(`allianceTrainRide`), onToggle: (e2) => A(`allianceTrainRide`, e2), summaryRows: [[`automation.currentCarriage`, Y?.currentCarriage], [`automation.queueCapacity`, Y?.queueLength == null || Y.maxPassenger == null ? null : `${Y.queueLength} / ${Y.maxPassenger}`]], statusRows: [[`automation.currentCarriage`, D(Y?.currentCarriage)], [`automation.queueCapacity`, `${D(Y?.queueLength ?? 0)} / ${D(Y?.maxPassenger)}`], [`automation.currentReward`, D(Y?.currentReward)], [`automation.trainVipStatus`, Y?.isVip ? M(`common.yes`) : M(`common.no`)], [`automation.trainVipCarriages`, Y?.vipCarriages?.join(`, `) || `\u2014`], [`automation.nextCheck`, T(Y?.nextRunAt, j)]], children: [(0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.normalCarriageSelection`) }), (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.normalCarriageHint`) }), (0, S.jsx)(`fieldset`, { className: `automation-compact-choice-group automation-selection-mode-choices`, "aria-label": M(`automation.normalCarriageSelection`), children: [`reward`, `fixed`].map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `train-normal-selection-mode`, checked: qn === e2, disabled: $(`allianceTrainRide`), onChange: () => {
    W(`allianceTrainRide`, { selectionMode: e2 });
  } }), (0, S.jsx)(`span`, { children: M(e2 === `reward` ? `automation.rewardSelectionMode` : `automation.fixedSelectionMode`) })] }, `normal-mode-${e2}`)) }), qn === `fixed` && (0, S.jsxs)(`fieldset`, { className: `automation-compact-choice-group automation-carriage-choices`, "aria-label": M(`automation.normalCarriageSelection`), children: [(0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, disabled: true }), (0, S.jsx)(`span`, { children: M(`automation.driver`) })] }), xe.map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: Gn.includes(e2), disabled: $(`allianceTrainRide`), onChange: (t3) => {
    W(`allianceTrainRide`, { fixedCarriageIds: t3.target.checked ? [e2] : [] });
  } }), (0, S.jsx)(`span`, { children: M(`automation.carriage${e2}`) })] }, `normal-${e2}`))] })] }), Yn && (0, S.jsxs)(`div`, { className: `automation-train-rewards automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.rewardPreferences`) }), (0, S.jsxs)(`div`, { className: `automation-preference-list`, children: [H.length === 0 && (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.noPreferredRewards`) }), $n.map((e2) => {
    let t3 = H.indexOf(e2.key), n3 = t3 >= 0;
    return (0, S.jsxs)(`div`, { className: `automation-preference-item${n3 ? ` selected` : ``}${Ct === e2.key ? ` dragging` : ``}${Tt === e2.key ? ` drag-over` : ``}`, draggable: n3, onDragStart: (t4) => {
      n3 && (t4.dataTransfer.effectAllowed = `move`, wt(e2.key));
    }, onDragOver: (t4) => {
      !n3 || !Ct || Ct === e2.key || (t4.preventDefault(), t4.dataTransfer.dropEffect = `move`, Et(e2.key));
    }, onDrop: (t4) => {
      t4.preventDefault();
      let r2 = Ct;
      if (wt(null), Et(null), !r2 || !n3) return;
      let i3 = H.indexOf(r2), a2 = H.indexOf(e2.key);
      if (i3 < 0 || a2 < 0 || i3 === a2) return;
      let o3 = [...H];
      o3.splice(a2, 0, o3.splice(i3, 1)[0]), St(o3), W(`allianceTrainRide`, { preferredRewardKeys: o3 });
    }, onDragEnd: () => {
      wt(null), Et(null);
    }, children: [(0, S.jsxs)(`label`, { className: `automation-reward-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: n3, onChange: (t4) => {
      let n4 = t4.target.checked ? [...H, e2.key] : H.filter((t5) => t5 !== e2.key);
      St(n4), W(`allianceTrainRide`, { preferredRewardKeys: n4 });
    } }), (0, S.jsx)(b, { assetPath: e2.iconPath, alt: e2.name || e2.key, className: `automation-reward-icon` }), (0, S.jsx)(`span`, { children: e2.name || e2.key })] }), (0, S.jsx)(`strong`, { className: `automation-reward-count`, children: e2.count === void 0 ? `\u2014` : `\xD7${se(e2.count)}` }), n3 && (0, S.jsxs)(`span`, { className: `automation-reward-order`, children: [(0, S.jsx)(`button`, { type: `button`, title: M(`automation.moveUp`), "aria-label": M(`automation.moveUp`), disabled: t3 === 0, onClick: () => sn(e2.key, -1), children: (0, S.jsx)(v, { name: `arrow-up` }) }), (0, S.jsx)(`button`, { type: `button`, title: M(`automation.moveDown`), "aria-label": M(`automation.moveDown`), disabled: t3 === H.length - 1, onClick: () => sn(e2.key, 1), children: (0, S.jsx)(v, { name: `arrow-down` }) }), (0, S.jsx)(`span`, { className: `automation-reward-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) })] })] }, e2.key);
  })] })] }), (0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.vipCarriageSelection`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoAcceptTrainVip`, checked: Wn, disabled: $(`allianceTrainRide`), onChange: (e2) => {
    W(`allianceTrainRide`, { autoAcceptVip: e2 });
  } }), (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.vipCarriageHint`) }), (0, S.jsx)(`fieldset`, { className: `automation-compact-choice-group automation-selection-mode-choices`, "aria-label": M(`automation.vipCarriageSelection`), children: [`reward`, `fixed`].map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `train-vip-selection-mode`, checked: Jn === e2, disabled: $(`allianceTrainRide`), onChange: () => {
    W(`allianceTrainRide`, { vipSelectionMode: e2 });
  } }), (0, S.jsx)(`span`, { children: M(e2 === `reward` ? `automation.rewardSelectionMode` : `automation.fixedSelectionMode`) })] }, `vip-mode-${e2}`)) }), Jn === `fixed` && (0, S.jsxs)(`fieldset`, { className: `automation-compact-choice-group automation-carriage-choices`, "aria-label": M(`automation.vipCarriageSelection`), children: [(0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, disabled: true }), (0, S.jsx)(`span`, { children: M(`automation.driver`) })] }), xe.map((e2) => {
    let t3 = Kn.includes(e2);
    return (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t3, disabled: $(`allianceTrainRide`) || !t3 && Kn.length >= 2, onChange: (t4) => {
      W(`allianceTrainRide`, { vipFixedCarriageIds: t4.target.checked ? [...Kn, e2] : Kn.filter((t5) => t5 !== e2) });
    } }), (0, S.jsx)(`span`, { children: M(`automation.carriage${e2}`) })] }, `vip-${e2}`);
  })] })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.additional`) }), Yn && (0, S.jsx)(g, { variant: `checkbox`, label: `automation.preferRewardQuantity`, checked: Lt, disabled: $(`allianceTrainRide`), onChange: (e2) => {
    Rt(e2), W(`allianceTrainRide`, { preferRewardQuantity: e2 });
  } }), (0, S.jsxs)(`div`, { className: `automation-actions`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.thanksMode`), (0, S.jsxs)(`select`, { value: zt, onChange: (e2) => {
    let t3 = e2.target.value;
    Bt(t3), W(`allianceTrainRide`, { thanksMode: t3 });
  }, children: [(0, S.jsx)(`option`, { value: `like`, children: M(`automation.thanksLike`) }), (0, S.jsx)(`option`, { value: `tickets`, children: M(`automation.thanksTickets`) })] })] }), zt === `tickets` && (0, S.jsxs)(`label`, { children: [M(`automation.ticketCount`), (0, S.jsxs)(`select`, { value: Vt, onChange: (e2) => {
    Ht(e2.target.value), W(`allianceTrainRide`, { ticketCount: Number(e2.target.value) });
  }, children: [(0, S.jsx)(`option`, { value: `1`, children: `1` }), (0, S.jsx)(`option`, { value: `2`, children: `2` }), (0, S.jsx)(`option`, { value: `3`, children: `3` })] })] })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.ticketFallbackLike`) })] })] }), (0, S.jsx)(y, { online: l2, title: `automation.allianceHelp.title`, description: `automation.allianceHelp.description`, state: O(wn?.state, er), enabled: er, disabled: $(`allianceHelp`), onToggle: (e2) => A(`allianceHelp`, e2), actionLabel: `common.runNow`, actionBusy: _2 === `allianceHelp`, onAction: () => Ne(`allianceHelp`), statusRows: [[`automation.available`, D(wn?.available ?? 0)], [`automation.processed`, D(wn?.processed ?? 0)], [`automation.lastRun`, T(wn?.lastRunAt, j)], [`automation.nextRun`, er ? E(wn?.nextRunAt, j) : `-`]] }), (0, S.jsx)(y, { online: l2, title: `automation.allianceGift.title`, settingsCollapsible: false, description: `automation.allianceGift.description`, state: O(Tn?.state, tr), enabled: tr, disabled: $(`allianceGift`), onToggle: (e2) => A(`allianceGift`, e2), actionLabel: `common.runNow`, actionBusy: _2 === `allianceGift`, onAction: () => Ne(`allianceGift`), statusRows: [[`automation.normalClaimed`, D(Tn?.normalClaimed ?? 0)], [`automation.advancedClaimed`, D(Tn?.advancedClaimed ?? 0)], [`automation.lastRun`, T(Tn?.lastRunAt, j)], [`automation.nextRun`, tr ? E(Tn?.nextRunAt, j) : `-`]], error: Ar ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: ft, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    pt(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= 1440 ? G(`allianceGift:intervalMinutes`, `allianceGift`, { intervalMinutes: n3 }) : R(`allianceGift:intervalMinutes`)?.pause();
  }, onBlur: () => z(`allianceGift:intervalMinutes`) })] }) }) }), (0, S.jsx)(y, { online: l2, title: `automation.strongholdResource.title`, settingsCollapsible: false, description: `automation.strongholdResource.description`, state: O(En?.state, nr), enabled: nr, disabled: $(`strongholdResource`), onToggle: (e2) => A(`strongholdResource`, e2), actionLabel: `common.runNow`, actionBusy: _2 === `strongholdResource`, onAction: () => Ne(`strongholdResource`), statusRows: [[`automation.available`, D(En?.available ?? 0)], [`automation.claimed`, D(En?.claimed ?? 0)], [`automation.lastRun`, T(En?.lastRunAt, j)], [`automation.nextRun`, nr ? E(En?.nextRunAt, j) : `-`]], error: Nr ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: mt, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    ht(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= 1440 ? G(`strongholdResource:intervalMinutes`, `strongholdResource`, { intervalMinutes: n3 }) : R(`strongholdResource:intervalMinutes`)?.pause();
  }, onBlur: () => z(`strongholdResource:intervalMinutes`) })] }) }) }), (0, S.jsx)(y, { online: l2, title: `automation.allianceCenterResource.title`, settingsCollapsible: false, description: `automation.allianceCenterResource.description`, state: O(Dn?.state, rr), enabled: rr, disabled: $(`allianceCenterResource`), onToggle: (e2) => A(`allianceCenterResource`, e2), actionLabel: `common.runNow`, actionBusy: _2 === `allianceCenterResource`, onAction: () => Ne(`allianceCenterResource`), statusRows: [[`automation.available`, D(Dn?.available ?? 0)], [`automation.claimed`, D(Dn?.claimed ?? 0)], [`automation.lastRun`, T(Dn?.lastRunAt, j)], [`automation.nextRun`, rr ? E(Dn?.nextRunAt, j) : `-`]], error: Pr ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: gt, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    _t(t3), Number.isInteger(n3) && n3 >= 1 && n3 <= 1440 ? G(`allianceCenterResource:intervalMinutes`, `allianceCenterResource`, { intervalMinutes: n3 }) : R(`allianceCenterResource:intervalMinutes`)?.pause();
  }, onBlur: () => z(`allianceCenterResource:intervalMinutes`) })] }) }) }), (0, S.jsxs)(y, { online: l2, title: `automation.allianceGather.title`, description: `automation.allianceGather.description`, state: O(On?.state, ir), enabled: ir, disabled: $(`allianceGather`), onToggle: (e2) => {
    if (e2 && ar.length === 0) {
      It(`automation.allianceGatherSquadRequired`);
      return;
    }
    It(``), A(`allianceGather`, e2);
  }, actionLabel: `common.runNow`, actionBusy: _2 === `allianceGather`, onAction: () => {
    if (ar.length === 0) {
      It(`automation.allianceGatherSquadRequired`);
      return;
    }
    It(``), Ne(`allianceGather`);
  }, error: Ft || void 0, children: [(0, S.jsxs)(`div`, { className: `automation-status-grid`, children: [(0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.allianceGatherAvailable`) }), (0, S.jsx)(`strong`, { children: D(On?.available ?? 0) })] }), (0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.dispatched`) }), (0, S.jsx)(`strong`, { children: D(On?.dispatched ?? 0) })] }), (0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.lastRun`) }), (0, S.jsx)(`strong`, { children: T(On?.lastRunAt, j) })] })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.allianceGatherSquadPriority`) }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": M(`automation.allianceGatherSquadPriority`), children: sr.map((e2) => {
    let t3 = or.has(e2);
    return (0, S.jsxs)(`div`, { className: `automation-squad-priority-item${t3 ? ` selected` : ``}${jt === e2 ? ` dragging` : ``}${Nt === e2 ? ` drag-over` : ``}`, draggable: t3, onDragStart: (n3) => {
      t3 && (n3.dataTransfer.effectAllowed = `move`, n3.dataTransfer.setData(`text/plain`, String(e2)), Mt(e2));
    }, onDragOver: (n3) => {
      !t3 || jt === null || jt === e2 || (n3.preventDefault(), n3.dataTransfer.dropEffect = `move`, Pt(e2));
    }, onDrop: (n3) => {
      n3.preventDefault();
      let r2 = jt;
      if (Mt(null), Pt(null), r2 === null || !t3) return;
      let i3 = Se(ar, r2, e2);
      i3 !== ar && W(`allianceGather`, { squadPriority: i3 });
    }, onDragEnd: () => {
      Mt(null), Pt(null);
    }, children: [(0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t3, onChange: (t4) => {
      let n3 = t4.target.checked ? [...ar, e2] : ar.filter((t5) => t5 !== e2);
      if (ir && n3.length === 0) {
        It(`automation.allianceGatherSquadRequired`);
        return;
      }
      It(``), W(`allianceGather`, { squadPriority: n3 });
    } }), (0, S.jsx)(`span`, { children: M(`automation.squad`, { index: e2 }) })] }), t3 ? (0, S.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) }) : null] }, e2);
  }) })] })] }) }), V.has(`daily`) && (0, S.jsx)(x.Activity, { mode: B === `daily` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsxs)(y, { online: l2, title: `automation.railway.title`, description: `automation.railway.description`, state: O(X?.state, cr), enabled: cr, disabled: $(`railway`), onToggle: (e2) => {
    W(`railway`, { enabled: e2 });
  }, actionLabel: X?.running ? `common.stop` : `automation.departAll`, actionBusy: _2 === `railway`, onAction: () => Ie(`railway`, X?.running === true, { weeklyQualities: Cr, departWhenTicketsInsufficient: lr }), summaryRows: [[`automation.departed`, X?.departed], [`automation.pendingClaims`, X?.pendingClaims]], statusRows: [[`automation.nextRun`, cr ? E(X?.nextScheduledAt, j) : `-`], [`automation.qualifiedTotal`, `${D(X?.qualified ?? 0)} / ${D(X?.total ?? 0)}`], [`automation.refreshed`, D(X?.refreshed ?? 0)], [`automation.departed`, D(X?.departed ?? 0)], [`automation.claimed`, D(X?.claimed ?? 0)], [`automation.pendingClaims`, D(X?.pendingClaims ?? 0)], [`automation.nextClaim`, E(X?.nextContinuationAt, j)], [`automation.batchDeparture`, Ce(X?.capabilities?.batchDeparture, `common.degraded`)]], error: Or ? void 0 : `automation.dailyDelayError`, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.schedule`) }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.delayAfterResetMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 1440, step: 1, value: ct, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    lt(t3), Number.isInteger(n3) && n3 >= 0 && n3 <= 1440 ? G(`railway:delayMinutes`, `railway`, { delayMinutes: n3 }) : R(`railway:delayMinutes`)?.pause();
  }, onBlur: () => z(`railway:delayMinutes`) })] }) })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.quality`) }), zr(`railway`, Cr, X?.running)] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.departure`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.railwayDepartWhenTicketsInsufficient`, checked: lr, disabled: $(`railway`) || X?.running, onChange: (e2) => {
    W(`railway`, { departWhenTicketsInsufficient: e2 });
  } })] })] }), (0, S.jsxs)(y, { online: l2, title: `automation.secretTask.title`, description: `automation.secretTask.description`, state: O(Z?.state, fr), enabled: ur, active: fr, disabled: $(`dispatch`), onToggle: (e2) => {
    W(`dispatch`, { autoExecute: e2 });
  }, actionLabel: Z?.running ? `common.stop` : `automation.dispatchAll`, actionBusy: _2 === `dispatch`, onAction: () => Ie(`dispatch`, Z?.running === true, { runNow: true, weeklyQualities: wr }), summaryRows: [[`automation.dispatched`, Z?.dispatched], [`automation.pendingClaims`, Z?.pendingClaims]], statusRows: [[`automation.nextRun`, fr ? E(Z?.nextScheduledAt, j) : `-`], [`automation.qualifiedTotal`, `${D(Z?.qualified ?? Fr?.qualified ?? 0)} / ${D(Z?.available ?? Fr?.available ?? 0)}`], [`automation.dispatched`, D(Z?.dispatched ?? 0)], [`automation.refreshed`, D(Z?.refreshed ?? 0)], [`automation.claimed`, D(Z?.claimed ?? 0)], [`automation.pendingClaims`, D(Z?.pendingClaims ?? 0)], [`automation.nextClaim`, E(Z?.nextContinuationAt, j)], [`automation.superRefresh`, Ce(Z?.capabilities?.superRefresh, `automation.normalRefresh`)]], error: kr ? void 0 : `automation.dailyDelayError`, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`squad.afkExecutionSettings`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoCollectRewards`, checked: dr, disabled: $(`dispatch`), onChange: (e2) => {
    W(`dispatch`, { collectRewards: e2 });
  } }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.delayAfterResetMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 1440, step: 1, value: ut, onChange: (e2) => {
    let t3 = e2.target.value, n3 = Number(t3);
    dt(t3), Number.isInteger(n3) && n3 >= 0 && n3 <= 1440 ? G(`dispatch:delayMinutes`, `dispatch`, { delayMinutes: n3 }) : R(`dispatch:delayMinutes`)?.pause();
  }, onBlur: () => z(`dispatch:delayMinutes`) })] }) })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.quality`) }), zr(`dispatch`, wr, Z?.running)] }), (0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.dispatchAssist`, checked: vr, disabled: !l2 || rn !== ``, onChange: Wr }), vr && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`div`, { className: `automation-squad-choices`, children: ye.map((e2) => (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: qt.includes(e2), onChange: (t3) => {
    let n3 = t3.target.checked ? [...qt, e2] : qt.filter((t4) => t4 !== e2);
    Jt(n3), Wr(vr, n3);
  } }), e2 === `special` ? M(`automation.assistQuality.special`) : e2.toUpperCase()] }, e2)) }), (0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.minDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 86400, step: 1, value: Yt, onChange: (e2) => {
    let t3 = e2.target.value;
    Xt(t3), Gr(Number(t3), br, xr);
  }, onBlur: () => z(`dispatchAssist:settings`) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.maxDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 86400, step: 1, value: Zt, onChange: (e2) => {
    let t3 = e2.target.value;
    Qt(t3), Gr(yr, Number(t3), xr);
  }, onBlur: () => z(`dispatchAssist:settings`) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.assistIntervalSeconds`), (0, S.jsx)(`input`, { type: `number`, min: 5, max: 300, step: 1, value: $t, onChange: (e2) => {
    let t3 = e2.target.value;
    en(t3), Gr(yr, br, Number(t3));
  }, onBlur: () => z(`dispatchAssist:settings`) })] })] })] }), !vr && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`strong`, { children: M(`automation.allySecretTasks`) }), (0, S.jsxs)(`div`, { className: `automation-task-list`, children: [(Gt?.tasks ?? []).map((e2) => {
    let t3 = Sr.get(e2.uuid), n3 = t3 && [`scheduled`, `waiting_connection`, `retry_wait`, `running`].includes(t3.scheduleStatus);
    return (0, S.jsxs)(`label`, { className: `automation-task-row automation-assist-task-row`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: tn.includes(e2.uuid), disabled: !!n3 || rn !== ``, onChange: (t4) => nn((n4) => t4.target.checked ? [...n4, e2.uuid] : n4.filter((t5) => t5 !== e2.uuid)) }), (0, S.jsxs)(`span`, { className: `automation-assist-task-main`, children: [(0, S.jsxs)(`span`, { className: `automation-assist-task-title`, children: [(0, S.jsx)(`strong`, { children: Kr(e2) }), Yr(e2)] }), Xr(e2)] }), (0, S.jsx)(`span`, { children: e2.helpAvailable ? M(`automation.helpAvailable`) : T(e2.completionTime, j) }), (0, S.jsx)(`span`, { children: M(n3 ? `automation.assistStatus.${t3.scheduleStatus}` : `automation.assistStatus.untracked`) })] }, e2.uuid);
  }), !Gt?.tasks.length && (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.noAllySecretTasks`) })] }), (0, S.jsx)(`button`, { disabled: !tn.length || rn !== ``, onClick: () => void qr(), children: M(`automation.scheduleSelectedHelp`) }), (0, S.jsx)(`div`, { className: `automation-assist-queue`, children: (Gt?.jobs ?? []).filter((e2) => e2.scheduleSource === `manual` && [`scheduled`, `waiting_connection`, `retry_wait`, `running`, `failed`, `expired`].includes(e2.scheduleStatus)).map((e2) => (0, S.jsxs)(`div`, { className: `automation-inline-status`, children: [(0, S.jsxs)(`span`, { children: [Kr(e2), ` \xB7 `, M(`automation.assistStatus.${e2.scheduleStatus}`), ` \xB7 `, T(e2.assistAt, j)] }), [`scheduled`, `waiting_connection`, `retry_wait`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`cancel`, e2.uuid), children: M(`common.cancel`) }), [`failed`, `expired`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`retry`, e2.uuid), children: M(`common.retry`) })] }, e2.uuid)) })] }), vr && on && (0, S.jsx)(`p`, { className: `error-text`, children: on })] })] }), (0, S.jsxs)(y, { online: l2, title: `automation.ghost.title`, description: `automation.ghost.description`, state: O(kn?.state, _r), enabled: pr, active: _r, toggleLabel: `automation.ghost.autoStartOwn`, disabled: $(`ghostRecon`), onToggle: (e2) => {
    W(`ghostRecon`, { autoStartOwn: e2 });
  }, statusRows: [[`automation.nextClaim`, hr ? E(kn?.nextClaimAt, j) : `-`]], children: [(0, S.jsx)(g, { label: `automation.ghost.autoJoinAlliance`, checked: mr, disabled: $(`ghostRecon`), onChange: (e2) => {
    W(`ghostRecon`, { autoJoinAlliance: e2 });
  } }), mr && (0, S.jsxs)(`div`, { className: `automation-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.ghost.allianceFilter`) }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group`, children: be.map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `ghost-alliance-filter`, value: e2, checked: gr === e2, disabled: $(`ghostRecon`), onChange: () => {
    W(`ghostRecon`, { allianceFilter: e2 });
  } }), (0, S.jsx)(`span`, { children: M(`automation.ghost.filter.${e2}`) })] }, e2)) })] }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.ghost.autoClaimRewards`, checked: hr, disabled: $(`ghostRecon`), onChange: (e2) => {
    W(`ghostRecon`, { autoClaimRewards: e2 });
  } }), (0, S.jsxs)(`div`, { className: `automation-inline-status`, children: [(0, S.jsxs)(`span`, { children: [M(`automation.ghost.ownPending`), `: `, D(kn?.ownPending ?? 0)] }), (0, S.jsxs)(`span`, { children: [M(`automation.ghost.allianceCandidates`), `: `, D(kn?.allianceCandidates ?? 0)] }), (0, S.jsxs)(`span`, { children: [M(`automation.claimed`), `: `, D(kn?.claimed ?? 0)] })] })] })] }) })] })] });
}
export {
  Ae as AutomationPanel,
  E as formatAutomationNextTime,
  Se as reorderSquadPriority,
  Te as usesTrainRewardSelection
};
