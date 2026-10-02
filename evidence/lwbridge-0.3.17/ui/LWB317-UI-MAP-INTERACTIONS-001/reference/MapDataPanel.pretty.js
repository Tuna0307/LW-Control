import { A as e, B as t, C as n, Ct as r, Et as i, It as a, Mt as o, Q as s, S as c, St as l, Tt as u, X as d, Y as f, Z as p, _ as ee, bt as te, c as m, dt as h, ft as ne, ht as re, k as ie, mt as ae, n as oe, nt as g, o as se, pt as ce, r as le, t as ue, tt as de, v as fe, vt as pe, x as me, y as he, z as ge } from "./index-BVfnK1wp.js";
import { t as _ } from "./rewardDisplay-eZWrd6iS.js";
import { t as v } from "./GameAssetImage-Diy9VTIr.js";
var y = a(o());
function _e(e2) {
  return { rows: true, options: e2 === `scan-complete` };
}
function ve(e2, t2) {
  return t2 && (e2 === `truck` || e2 === `railway` || e2 === `dispatch`) ? true : void 0;
}
function ye(e2, t2, n2, r2) {
  return t2 && e2.set(t2, n2), r2 ? e2.get(r2) ?? { page: 1, rows: [], total: 0 } : n2;
}
function be(e2, t2) {
  let n2 = e2.findIndex((e3) => e3.sortBy === t2);
  if (n2 < 0) return [{ sortBy: t2, sortOrder: `desc` }, ...e2];
  let r2 = e2[n2];
  return r2.sortOrder === `desc` ? [{ ...r2, sortOrder: `asc` }, ...e2.filter((e3, t3) => t3 !== n2)] : e2.filter((e3, t3) => t3 !== n2);
}
var xe = { city: 0, resource: 0, monster: 0, truck: 0, railway: 0, dispatch: 0, ghost: 0, treasure: 0 };
function Se(e2, t2) {
  return t2 > 0 && e2?.serverId === t2 ? e2.counts : null;
}
var b = [[`invalid dispatch plunder target`, `DISPATCH_PLUNDER_INVALID_TARGET`], [`invalid scheduled target`, `DISPATCH_PLUNDER_INVALID_TARGET`], [`dispatch plunder request already pending`, `DISPATCH_PLUNDER_REQUEST_PENDING`], [`dispatch manager unavailable`, `DISPATCH_PLUNDER_MANAGER_UNAVAILABLE`], [`dispatch steal limit reached`, `DISPATCH_PLUNDER_DAILY_LIMIT_REACHED`], [`cross-server dispatch steal unavailable`, `DISPATCH_PLUNDER_CROSS_SERVER_UNAVAILABLE`], [`server response timeout`, `DISPATCH_PLUNDER_RESPONSE_TIMEOUT`], [`game disconnected`, `DISPATCH_PLUNDER_GAME_DISCONNECTED`], [`dispatch_des040`, `DISPATCH_PLUNDER_TASK_COMPLETED`], [`dispatch_des043`, `DISPATCH_PLUNDER_TASK_DISAPPEARED`], [`task expired`, `DISPATCH_PLUNDER_TASK_EXPIRED`], [`client restarted`, `DISPATCH_PLUNDER_CLIENT_RESTARTED`], [`invalid map plunder schedule`, `DISPATCH_PLUNDER_INVALID_SCHEDULE`], [`map plunder schedule already armed`, `DISPATCH_PLUNDER_ALREADY_ARMED`]];
function x(e2) {
  if (e2 instanceof Error) return e2.message;
  if (e2 && typeof e2 == `object`) {
    let t2 = e2;
    return [t2.code, t2.message].filter((e3) => typeof e3 == `string`).join(`: `);
  }
  return String(e2 ?? ``);
}
function Ce(e2) {
  let t2 = e2.replace(/\s+/g, ` `).trim();
  return t2.length > 60 ? `${t2.slice(0, 57)}...` : t2;
}
function we(e2, t2) {
  let n2 = Ce(x(t2)), r2 = n2.match(/DISPATCH_PLUNDER_[A-Z_]+/)?.[0], i2 = r2;
  if (!i2) {
    let e3 = n2.toLowerCase();
    i2 = b.find(([t3]) => e3.includes(t3))?.[1];
  }
  !i2 && /^\d+$/.test(n2) && (i2 = `DISPATCH_PLUNDER_SERVER_REJECTED`), i2 ||= `DISPATCH_PLUNDER_UNKNOWN`;
  let a2 = r2 ? n2.slice(n2.indexOf(r2) + r2.length).replace(/^\s*[:：-]\s*/, ``) : ``, o2 = Ce(a2 || n2) || i2, s2 = i2 === `DISPATCH_PLUNDER_SERVER_REJECTED` ? Ce(a2 || n2) : o2;
  return s2 === `E000000` ? e2(`error.PLUNDER_POSSIBLE_DAILY_LIMIT_REACHED`) : e2(`error.${i2}`, { detail: o2, code: s2 });
}
var Te = { 457567: `TRUCK_PLUNDER_FULLY_LOOTED`, 457589: `TRUCK_PLUNDER_DAILY_LIMIT_REACHED`, 458632: `TRUCK_PLUNDER_CROSS_ZONE_SEGMENT_MISMATCH`, season_mastery_s3_tips_12: `TRUCK_PLUNDER_ALLIED_TARGET`, truck_tips10008: `TRUCK_PLUNDER_REINDEER_ALREADY_LOOTED`, trade_person_tips1013: `TRUCK_PLUNDER_ATTACK_LIMIT_REACHED`, "truck expired": `TRUCK_PLUNDER_TARGET_EXPIRED`, "game disconnected": `TRUCK_PLUNDER_GAME_DISCONNECTED`, "client restarted": `TRUCK_PLUNDER_CLIENT_RESTARTED`, "server response timeout": `TRUCK_PLUNDER_RESPONSE_TIMEOUT` };
function S(e2) {
  if (e2 instanceof Error) return e2.message;
  if (e2 && typeof e2 == `object`) {
    let t2 = e2;
    return [t2.code, t2.message].filter((e3) => typeof e3 == `string`).join(`: `);
  }
  return String(e2 ?? ``);
}
function C(e2, t2) {
  let n2 = S(t2).replace(/\s+/g, ` `).trim();
  if (n2 === `E000000`) return e2(`error.PLUNDER_POSSIBLE_DAILY_LIMIT_REACHED`);
  let r2 = Te[n2.toLowerCase()];
  return r2 ? e2(`error.${r2}`) : e2(`error.TRUCK_PLUNDER_SERVER_REJECTED`, { detail: n2 || `-` });
}
var Ee = { 1: `map.treasureTypeRadar`, 2: `map.treasureTypeSiege`, 3: `map.treasureTypeActivityRadar`, 5: `map.treasureTypeInvasion`, 6: `map.treasureTypeZoneMobilization`, 7: `map.treasureTypeSandworm`, 8: `map.treasureTypeAllianceBossSandbox`, 9: `map.treasureTypePlayerKillMonster`, 10: `map.treasureTypeFlowerCar`, 11: `map.treasureTypeOffSeason`, 12: `map.treasureTypeBloodyQueen`, 13: `map.treasureTypeGeneFragment`, 15: `map.treasureTypeFlowerTrainUpgrade`, 16: `map.treasureTypeFlowerTrainCheer`, 17: `map.treasureTypeWolfShadow`, 18: `map.treasureTypeSeasonBoss`, 19: `map.treasureTypeWorldBoss`, 20: `map.treasureTypeZwlGift` };
function De(e2) {
  let t2 = Number(e2);
  return Number.isFinite(t2) && Ee[t2] ? Ee[t2] : `map.treasureTypeUnknown`;
}
function Oe(e2) {
  let t2 = Number(e2);
  return t2 === 1 ? `season_s2_ice_supplies_11` : t2 === 3 ? `season_s3_supplies_1` : t2 === 4 ? `season4_supplies_name_2` : ``;
}
function ke(e2) {
  let t2 = Number(e2);
  return t2 === 1 ? `map.treasureTypeIceSupplies` : t2 === 3 ? `map.treasureTypeDesert` : t2 === 4 ? `map.treasureTypeLargeLuckyCat` : `map.treasureTypeUnknown`;
}
function Ae(e2) {
  return e2.online && !e2.isReading && e2.dataServerId > 0 && e2.currentServerId === e2.dataServerId && !e2.busy;
}
function w(e2) {
  return { charging: `map.treasureStateCharging`, claimable: `map.treasureStateClaimable`, depleted: `map.treasureStateDepleted`, expired: `map.treasureStateExpired`, verifying: `map.treasureStateVerifying` }[String(e2 || ``)] || `map.treasureStateUnknown`;
}
function T(e2, t2) {
  return e2 === `claimed` ? `map.treasurePlayerClaimed` : t2 === `other_alliance` ? `map.treasurePlayerOtherAlliance` : t2 === `no_scout` || t2 === `no_squad` || t2 === `squad_reserved` ? `map.treasurePlayerNoScout` : { unclaimed: `map.treasurePlayerUnclaimed`, dispatching: `map.treasurePlayerDispatching`, scouting: `map.treasurePlayerScouting`, digging: `map.treasurePlayerDigging`, claiming: `map.treasurePlayerClaiming`, claimed: `map.treasurePlayerClaimed`, failed: `map.treasurePlayerUnclaimed`, verifying: `map.treasurePlayerVerifying` }[String(e2 || ``)] || `map.treasurePlayerUnknown`;
}
var E = i(), je = 50, Me = `lwbridge.mapScanMode`, Ne = `lwbridge.mapIncludeForeignRadarTreasures`, Pe = `lwbridge.mapLuckyTreasurePriority`, Fe = 1e3, Ie = [{ key: `city`, label: `map.playerCity`, enabled: true }, { key: `resource`, label: `map.resourcePoint`, enabled: true }, { key: `monster`, label: `map.monster`, enabled: true }, { key: `truck`, label: `map.truck`, enabled: true }, { key: `railway`, label: `map.allianceTrain`, enabled: true }, { key: `dispatch`, label: `map.secretTask`, enabled: true }, { key: `ghost`, label: `map.ghostScout`, enabled: true }, { key: `treasure`, label: `map.treasure`, enabled: true }], D = Ie.filter((e2) => e2.enabled).map((e2) => e2.key), Le = new Set(D), Re = /* @__PURE__ */ new Set([`truck`, `railway`, `dispatch`, `ghost`]), O = /* @__PURE__ */ new Set([`truck`, `railway`]), ze = { city: `map.city`, resource: `map.resource`, monster: `map.monster`, truck: `map.truck`, railway: `map.allianceTrain`, dispatch: `map.secretTask`, ghost: `map.ghostScout`, treasure: `map.treasure` }, Be = { city: [{ sortBy: `updatedAt`, sortOrder: `desc` }], resource: [{ sortBy: `updatedAt`, sortOrder: `desc` }], monster: [{ sortBy: `updatedAt`, sortOrder: `desc` }], truck: [{ sortBy: `updatedAt`, sortOrder: `desc` }], railway: [{ sortBy: `updatedAt`, sortOrder: `desc` }], dispatch: [{ sortBy: `updatedAt`, sortOrder: `desc` }], ghost: [{ sortBy: `updatedAt`, sortOrder: `desc` }], treasure: [{ sortBy: `updatedAt`, sortOrder: `desc` }] }, Ve = { serverId: 0, serverIdSource: `none`, scanRunId: ``, isReading: false, phase: `idle`, selectedTypes: [...D], totalBlocks: 0, readBlocks: 0, unreadBlocks: 0, failedBlocks: 0, inflightBlocks: 0, scanMode: `normal`, concurrency: 8, retryCount: 2, scanRate: 0, progressPercent: 0, nativeCaptureReady: false, nativePendingRecords: 0, nativeDroppedRecords: 0 };
function k(e2, t2) {
  return e2[t2];
}
function He(e2, t2) {
  let n2 = Array.isArray(t2) ? t2 : Object.values(t2), r2 = new Map(n2.map((e3) => [String(e3.uuid || ``), e3]));
  return r2.size === 0 ? e2 : e2.map((e3) => {
    let t3 = r2.get(String(k(e3, `uuid`) || ``));
    return t3 ? { ...e3, ...t3 } : e3;
  });
}
function Ue(e2) {
  return Re.has(e2);
}
function A(e2) {
  return O.has(e2);
}
function We(e2) {
  return e2 === `truck` || e2 === `railway` || e2 === `dispatch`;
}
function Ge(e2) {
  return e2 === `resource` || e2 === `monster`;
}
function j(e2, t2, n2) {
  let r2 = String(t2 || ``).trim();
  if (!r2) return n2;
  let i2 = e2[r2];
  return i2 && i2 !== r2 ? i2 : n2;
}
function M(e2, t2) {
  return j(t2, e2.nameKey, e2.name || e2.key);
}
function Ke(e2, t2, n2, r2, i2) {
  let a2 = String(Oe(r2) || i2 || ``).trim(), o2 = e2(a2 === `challenge_zombie_box_title` ? `map.treasureTypeTrialGift` : ke(r2));
  return a2 ? j(t2, a2, o2) : e2(De(n2));
}
function qe(e2) {
  return [`gatherMarchUuid`, `gatherUid`].some((t2) => {
    let n2 = String(k(e2, t2) || ``).trim();
    return n2 !== `` && n2 !== `0`;
  });
}
function N(e2, t2) {
  let n2 = Number(e2);
  if (!Number.isFinite(n2) || n2 <= 0) return `-`;
  let r2 = n2 < 1e12 ? n2 * 1e3 : n2;
  return new Date(r2).toLocaleString(t2);
}
function Je(e2) {
  if (!Number.isFinite(e2) || e2 < 0) return `-`;
  let t2 = Math.floor(e2 / 1e3);
  return [Math.floor(t2 / 3600), Math.floor(t2 % 3600 / 60), t2 % 60].map((e3) => String(e3).padStart(2, `0`)).join(`:`);
}
function Ye(e2, t2 = true) {
  let n2 = P(e2);
  if (!n2) return `-`;
  let r2 = new Date(n2), i2 = (e3) => String(e3).padStart(2, `0`), a2 = `${i2(r2.getHours())}:${i2(r2.getMinutes())}:${i2(r2.getSeconds())}`;
  return t2 ? `${i2(r2.getMonth() + 1)}-${i2(r2.getDate())} ${a2}` : a2;
}
function Xe(e2, t2) {
  let n2 = P(e2), r2 = P(t2);
  if (!n2 || !r2) return false;
  let i2 = new Date(n2), a2 = new Date(r2);
  return i2.getFullYear() === a2.getFullYear() && i2.getMonth() === a2.getMonth() && i2.getDate() === a2.getDate();
}
function Ze(e2) {
  let t2 = P(e2);
  return t2 ? new Date(t2).toISOString() : void 0;
}
function P(e2) {
  let t2 = Number(e2);
  return !Number.isFinite(t2) || t2 <= 0 ? 0 : t2 < 1e12 ? t2 * 1e3 : t2;
}
function F(e2, t2) {
  let n2 = P(k(e2, `taskExpireTime`)), r2 = P(k(e2, `completionTime`)), i2 = P(k(e2, `plunderAt`)) || r2, a2 = Number(k(e2, `stolenCount`)), o2 = Number(k(e2, `maxStealCount`));
  return n2 && t2 >= n2 ? `expired` : o2 > 0 && a2 >= o2 ? `full` : !r2 || t2 < r2 ? `pending` : i2 && t2 < i2 ? `protected` : `ready`;
}
function Qe(e2, t2) {
  return e2 ? N(e2, t2) : `-`;
}
function $e(e2, t2) {
  let n2 = Number(e2);
  if (!Number.isFinite(n2) || n2 <= 0) return `-`;
  let r2 = n2 < 1e12 ? n2 * 1e3 : n2;
  return r2 > Date.now() ? new Date(r2).toLocaleString(t2) : `-`;
}
function I(e2, t2) {
  let n2 = Number(e2);
  return Number.isFinite(n2) ? n2.toLocaleString(t2) : `-`;
}
function L(e2) {
  let t2 = Number(e2);
  return t2 === 1 ? `N` : t2 === 2 ? `R` : t2 === 3 ? `SR` : t2 === 4 ? `SSR` : t2 >= 5 ? `UR` : `-`;
}
function et(e2) {
  let t2 = Number(k(e2, `x`)), n2 = Number(k(e2, `y`));
  return Number.isFinite(t2) && Number.isFinite(n2) ? `${t2},${n2}` : `-`;
}
function tt(e2) {
  if (!e2.startsWith(`name:`)) return ``;
  try {
    return decodeURIComponent(e2.slice(5));
  } catch {
    return ``;
  }
}
function nt(e2, t2, n2, i2, a2, o2) {
  let s2 = { label: A(e2) ? t2(`map.liveTarget`) : t2(`map.coordinates`), width: `190px`, className: `map-column-coordinate`, value: et, coordinate: true }, c2 = { label: t2(`map.updatedAt`), width: `150px`, sortBy: `updatedAt`, value: (e3) => Qe(e3.updatedAt, n2) };
  return e2 === `city` ? [{ label: t2(`map.marked`), width: `48px`, className: `map-column-mark`, mark: true }, s2, { label: t2(`map.player`), width: `minmax(130px, 1fr)`, value: (e3) => String(k(e3, `ownerName`) || `-`) }, { label: t2(`map.alliance`), width: `minmax(110px, 1fr)`, value: (e3) => String(k(e3, `allianceName`) || `-`) }, { label: t2(`map.level`), width: `70px`, sortBy: `level`, value: (e3) => I(k(e3, `level`), n2) }, { label: `HP`, width: `90px`, sortBy: `health`, value: (e3) => I(k(e3, `health`), n2) }, { label: t2(`automation.shieldEnds`), width: `150px`, sortBy: `shield`, value: (e3) => $e(k(e3, `protectEndTime`) || k(e3, `shieldEndTime`), n2) }, c2] : e2 === `resource` ? [s2, { label: t2(`map.resource`), width: `minmax(150px, 1fr)`, value: (e3) => j(i2, k(e3, `resourceNameKey`), t2(`map.unknownResource`)) }, { label: t2(`map.level`), width: `70px`, sortBy: `level`, value: (e3) => I(k(e3, `level`), n2) }, { label: t2(`common.status`), width: `80px`, value: (e3) => {
    let n3 = qe(e3);
    return j(i2, n3 ? `300039` : `372138`, t2(n3 ? `map.resourceGathering` : `map.resourceIdle`));
  } }, c2] : e2 === `monster` ? [s2, { label: t2(`common.name`), width: `minmax(150px, 1fr)`, value: (e3) => j(i2, k(e3, `monsterNameKey`), t2(`map.unknownMonster`)) }, { label: t2(`map.level`), width: `70px`, sortBy: `level`, value: (e3) => I(k(e3, `level`), n2) }, { label: t2(`map.distance`), width: `90px`, sortBy: `distance`, value: (e3) => I(k(e3, `distanceFromHome`), n2) }, c2] : e2 === `truck` ? [{ label: t2(`map.selectTask`), width: `56px`, className: `map-task-select`, select: true }, s2, { label: t2(`map.playerAlliance`), width: `minmax(170px, 1fr)`, value: (e3) => String(k(e3, `ownerName`) || k(e3, `allianceName`) || `-`) }, { label: t2(`map.quality`), width: `70px`, sortBy: `quality`, value: (e3) => k(e3, `isSpecialURQuality`) === true ? t2(`map.reindeerQuality`) : L(k(e3, `quality`)) }, { label: t2(`map.escortPower`), width: `120px`, sortBy: `power`, value: (e3) => I(k(e3, `power`), n2) }, { label: t2(`map.retainedGoods`), width: `minmax(360px, 2fr)`, sortBy: a2 ? `itemCount` : void 0, rewards: `currentGoods` }, { label: t2(`map.plunderStatus`), width: `190px`, sortBy: `remainingLootCount`, value: (e3) => {
    let n3 = e3, i3 = l(n3), a3 = Math.max(0, Number(n3.robTimes) || 0), o3 = r(n3, Date.now());
    return o3 === `full` ? `${t2(`map.truckPlunderFull`)} ${a3}/${i3}` : o3 === `protected` ? `${t2(`map.truckProtected`)} ${Je(Number(n3.protectTime) - Date.now())}\uFF5C${t2(`map.truckRobbedCount`, { count: a3, max: i3 })}` : `${t2(`map.truckReady`)}\uFF5C${t2(`map.truckRobbedCount`, { count: a3, max: i3 })}`;
  } }, { label: t2(`map.arrivalTime`), width: `150px`, sortBy: `arriveTime`, value: (e3) => N(k(e3, `arriveTs`), n2) }, c2] : e2 === `railway` ? [s2, { label: t2(`map.alliance`), width: `minmax(160px, 1fr)`, value: (e3) => String(k(e3, `allianceName`) || k(e3, `allianceAbbr`) || `-`) }, { label: t2(`map.quality`), width: `70px`, sortBy: `quality`, value: (e3) => L(k(e3, `quality`)) }, { label: t2(`map.power`), width: `120px`, sortBy: `power`, value: (e3) => I(k(e3, `power`), n2) }, { label: t2(`map.retainedGoods`), width: `minmax(360px, 2fr)`, sortBy: a2 ? `itemCount` : void 0, rewards: `currentGoods` }, { label: t2(`map.protectionTime`), width: `150px`, sortBy: `protectTime`, value: (e3) => N(k(e3, `protectTime`), n2) }, c2] : e2 === `treasure` ? [s2, { label: t2(`map.treasureType`), width: `160px`, value: (e3) => Ke(t2, i2, k(e3, `treasureType`), k(e3, `suppliesType`), k(e3, `treasureNameKey`)) }, { label: t2(`map.remainingBoxes`), width: `90px`, value: (e3) => I(k(e3, `remainingBoxes`), n2) }, { label: t2(`map.treasureWorldState`), width: `110px`, value: (e3) => {
    let n3 = k(e3, `worldClaimState`) || (o2 ? `verifying` : `unknown`), r2 = Number(k(e3, `chargePercent`));
    return n3 === `charging` && Number.isFinite(r2) ? `${t2(w(n3))} ${Math.round(r2 * 100)}%` : t2(w(n3));
  } }, { label: t2(`map.treasurePlayerState`), width: `110px`, value: (e3) => t2(T(k(e3, `playerClaimState`) || (o2 ? `verifying` : `unknown`), k(e3, `claimBlockReason`))) }, { label: t2(`map.claimedCount`), width: `90px`, value: (e3) => I(k(e3, `rewardedCount`), n2) }, { label: t2(`map.diggingCount`), width: `90px`, value: (e3) => I(k(e3, `diggingCount`), n2) }, { label: t2(`map.expireTime`), width: `150px`, value: (e3) => N(Number(k(e3, `expireTime`)), n2) }, { label: t2(`map.owner`), width: `minmax(140px, 1fr)`, value: (e3) => String(k(e3, `ownerName`) || k(e3, `ownerUid`) || `-`) }, { label: t2(`map.alliance`), width: `minmax(110px, 1fr)`, value: (e3) => String(k(e3, `allianceAbbr`) || k(e3, `allianceId`) || `-`) }, { label: t2(`map.actions`), width: `90px`, action: true }, c2] : [...e2 === `dispatch` || e2 === `ghost` ? [{ label: t2(`map.selectTask`), width: `56px`, className: `map-task-select`, select: true }] : [], s2, { label: t2(`map.owner`), width: `minmax(150px, 1fr)`, value: (e3) => String(k(e3, `ownerName`) || k(e3, `ownerUid`) || `-`) }, { label: t2(`map.level`), width: `70px`, sortBy: `level`, value: (e3) => I(k(e3, `level`), n2) }, { label: t2(`map.quality`), width: `110px`, sortBy: `quality`, value: (e3) => k(e3, `isSpecial`) === true ? t2(`map.specialQuality`) : L(k(e3, `quality`)) }, { label: t2(`map.taskStatus`), width: `90px`, status: true }, { label: t2(`map.rewards`), width: `minmax(360px, 2fr)`, rewards: `rewards` }, { label: t2(`map.completionTime`), width: `150px`, sortBy: `completionTime`, value: (e3) => N(k(e3, `completionTime`), n2) }, c2];
}
function rt(e2) {
  let t2 = [...new Set(e2.filter((e3) => Le.has(e3)))];
  return t2.length > 0 ? t2 : [...D];
}
function it({ page: e2, totalPages: t2, onPage: n2 }) {
  let { t: r2 } = u();
  return t2 <= 1 ? null : (0, E.jsxs)(`div`, { className: `map-pagination`, children: [(0, E.jsx)(`button`, { disabled: e2 <= 1, onClick: () => n2(e2 - 1), children: r2(`map.previousPage`) }), (0, E.jsx)(`span`, { children: r2(`map.pageInfo`, { page: e2, total: t2 }) }), (0, E.jsx)(`button`, { disabled: e2 >= t2, onClick: () => n2(e2 + 1), children: r2(`map.nextPage`) })] });
}
var at = (0, y.memo)(function({ kind: e2, rows: t2, loading: n2, gameTexts: i2, itemKey: a2, currentTime: o2, sortState: s2, selectedDispatchKeys: c2, selectedTruckKeys: l2, jumpingKey: d2, jumpDisabled: f2, treasureStatesRefreshing: p2, treasureClaimBusy: ee2, treasureClaimDisabled: te2, onSort: m2, onToggleDispatch: h2, onToggleTruck: ne2, onCoordinateJump: re2, onPlayerMark: ie2, onClaimTreasure: ae2 }) {
  let { language: oe2, t: g2 } = u(), ce2 = (0, y.useMemo)(() => nt(e2, g2, oe2, i2, a2, p2), [i2, a2, e2, oe2, g2, p2]), le2 = ce2.map((e3) => Number(e3.width.match(/\d+/)[0]) + 8);
  function ue2(t3) {
    let n3 = k(t3, `uuid`) || k(t3, `marchUuid`) || k(t3, `recordKey`) || `${k(t3, `pointIndex`) || ``}:${k(t3, `ownerUid`) || ``}:${k(t3, `updatedAt`) || ``}`;
    return `${e2}:${t3.serverId}:${String(n3)}`;
  }
  function de2(t3) {
    if (A(e2)) {
      let e3 = String(k(t3, `marchUuid`) || ``).trim();
      if (!e3) return `-`;
      let n4 = `${t3.serverId}:${e3}`;
      return (0, E.jsxs)(`button`, { className: `map-coordinate-button`, disabled: f2, onClick: () => re2(t3), children: [(0, E.jsx)(`span`, { className: `map-coordinate-icon`, "aria-hidden": `true` }), (0, E.jsx)(`strong`, { children: g2(d2 === n4 ? `map.following` : `map.follow`) })] });
    }
    let n3 = Number(k(t3, `x`)), r2 = Number(k(t3, `y`));
    if (!Number.isInteger(n3) || !Number.isInteger(r2) || n3 < 1 || r2 < 1) return `-`;
    let i3 = `${t3.serverId}:${n3}:${r2}`;
    return (0, E.jsxs)(`button`, { className: `map-coordinate-button`, disabled: f2, onClick: () => re2(t3), children: [(0, E.jsx)(`span`, { className: `map-coordinate-icon`, "aria-hidden": `true` }), (0, E.jsx)(`span`, { children: et(t3) }), (0, E.jsx)(`strong`, { children: g2(d2 === i3 ? `map.jumping` : `map.jump`) })] });
  }
  function fe2(e3, t3) {
    let n3 = k(e3, t3);
    if (!Array.isArray(n3) || n3.length === 0) return `-`;
    let r2 = [...n3];
    return t3 === `currentGoods` && a2 && r2.sort((e4, t4) => Number(t4.key === a2) - Number(e4.key === a2)), (0, E.jsx)(`span`, { className: `map-reward-list map-reward-list--${t3 === `lostGoods` ? `lost` : `retained`}`, children: r2.map((e4, t4) => {
      let n4 = M(e4, i2), r3 = `${n4} \xD7${I(e4.count, oe2)}`;
      return (0, E.jsxs)(`span`, { className: `map-reward-item`, title: r3, "aria-label": r3, children: [(0, E.jsx)(v, { assetPath: e4.iconPath, alt: n4, className: `map-reward-icon` }), (0, E.jsxs)(`strong`, { children: [`\xD7`, _(e4.count)] })] }, `${e4.key}:${t4}`);
    }) });
  }
  function pe2(e3) {
    let t3 = F(e3, o2), n3 = g2(t3 === `expired` ? `map.taskExpired` : t3 === `full` ? `map.taskPlunderFull` : t3 === `protected` ? `map.taskProtected` : t3 === `ready` ? `map.taskReady` : `map.taskPending`);
    return (0, E.jsx)(`span`, { className: `map-task-status ${t3}`, children: n3 });
  }
  function me2(t3) {
    if (e2 === `truck`) {
      let e3 = t3, n4 = String(e3.uuid || ``).trim(), i4 = `${e3.serverId}:${n4}`, a4 = r(e3, o2);
      return (0, E.jsx)(`input`, { type: `checkbox`, "aria-label": g2(`map.selectNamedTask`, { name: String(k(t3, `ownerName`) || k(t3, `allianceName`) || k(t3, `uuid`)), server: t3.serverId }), checked: l2.has(i4), disabled: [`invalid`, `full`, `expired`].includes(a4), onChange: () => ne2(e3) });
    }
    let n3 = t3, i3 = String(n3.uuid || ``).trim(), a3 = `${n3.serverId}:${i3}`, s3 = !/^\d+$/.test(i3) || !P(n3.completionTime) || !P(n3.plunderAt) || [`expired`, `full`].includes(F(n3, o2));
    return (0, E.jsx)(`input`, { type: `checkbox`, "aria-label": g2(`map.selectNamedTask`, { name: String(k(t3, `ownerName`) || k(t3, `allianceName`) || k(t3, `uuid`)), server: t3.serverId }), checked: c2.has(a3), disabled: s3, onChange: () => h2({ ...n3, taskKind: e2 === `ghost` ? `ghost` : `dispatch` }) });
  }
  function he2(e3) {
    let t3 = e3, n3 = t3.trackerState === `missing` ? g2(`map.positionMissing`) : t3.trackerState === `replaced` ? g2(`map.positionReplaced`) : ``, r2 = t3.marked ? g2(`map.unmarkPlayer`) : g2(`map.markPlayer`);
    return (0, E.jsx)(`button`, { className: `map-mark-button${t3.marked ? ` active` : ``}`, disabled: !t3.ownerUid, title: n3 ? `${r2} \xB7 ${n3}` : r2, "aria-label": r2, onClick: () => ie2(t3), children: (0, E.jsx)(se, { name: t3.marked ? `favorite-filled` : `favorite` }) });
  }
  function ge2(e3) {
    let t3 = e3, n3 = String(t3.uuid || ``).trim(), r2 = Number(t3.suppliesType), i3 = r2 === 1 || r2 === 3 || r2 === 4, a3 = String(t3.playerClaimState || ``);
    return (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: te2 || !n3 || n3 === `0` || !i3 && t3.complete !== true || [`dispatching`, `scouting`, `digging`, `claiming`, `claimed`].includes(a3) || t3.worldClaimState === `depleted` || t3.worldClaimState === `expired` || t3.claimBlockReason === `other_alliance`, onClick: () => ae2(t3), children: g2(ee2 ? `map.claimingTreasure` : `map.claimTreasure`) });
  }
  return (0, E.jsx)(`div`, { className: `map-table-scroll`, children: (0, E.jsxs)(`table`, { className: `map-table map-table--${e2}`, style: { minWidth: le2.reduce((e3, t3) => e3 + t3, 0) }, "aria-label": g2(Ie.find((t3) => t3.key === e2).label), "aria-busy": n2, children: [(0, E.jsx)(`colgroup`, { children: le2.map((e3, t3) => (0, E.jsx)(`col`, { style: { width: e3 } }, t3)) }), (0, E.jsx)(`thead`, { children: (0, E.jsx)(`tr`, { className: `map-row map-head`, children: ce2.map((e3) => {
    let t3 = e3.sortBy ? s2.findIndex((t4) => t4.sortBy === e3.sortBy) : -1, n3 = t3 >= 0 ? s2[t3] : void 0;
    return (0, E.jsx)(`th`, { scope: `col`, className: e3.className || ``, "aria-sort": t3 === 0 ? n3?.sortOrder === `asc` ? `ascending` : `descending` : void 0, children: e3.sortBy ? (0, E.jsxs)(`button`, { type: `button`, className: `map-sort-button${n3 ? ` active` : ``}`, "aria-label": n3 ? g2(`map.sortDescription`, { column: e3.label, direction: g2(n3.sortOrder === `asc` ? `map.sortAscending` : `map.sortDescending`), priority: t3 + 1 }) : e3.label, onClick: () => m2(e3.sortBy), children: [e3.label, n3 && (0, E.jsx)(`span`, { className: `map-sort-priority`, "aria-hidden": `true`, children: t3 + 1 }), (0, E.jsxs)(`span`, { className: `map-sort-arrows`, "aria-hidden": `true`, children: [(0, E.jsx)(`span`, { className: `map-sort-arrow${n3?.sortOrder === `asc` ? ` active` : ``}`, children: (0, E.jsx)(se, { name: `arrow-up` }) }), (0, E.jsx)(`span`, { className: `map-sort-arrow${n3?.sortOrder === `desc` ? ` active` : ``}`, children: (0, E.jsx)(se, { name: `arrow-down` }) })] })] }) : e3.label }, e3.label);
  }) }) }), (0, E.jsxs)(`tbody`, { children: [t2.map((e3) => (0, E.jsx)(`tr`, { className: `map-row${k(e3, `marked`) === true ? ` is-marked` : ``}${k(e3, `trackerState`) === `missing` ? ` is-missing` : ``}${k(e3, `trackerState`) === `replaced` ? ` is-replaced` : ``}`, children: ce2.map((t3) => (0, E.jsx)(`td`, { className: [t3.className, t3.rewards ? `map-reward-cell` : ``].filter(Boolean).join(` `), children: t3.mark ? he2(e3) : t3.select ? me2(e3) : t3.coordinate ? de2(e3) : t3.rewards ? fe2(e3, t3.rewards) : t3.status ? pe2(e3) : t3.action ? ge2(e3) : t3.value?.(e3) }, t3.label)) }, ue2(e3))), t2.length === 0 && (0, E.jsx)(`tr`, { children: (0, E.jsx)(`td`, { colSpan: ce2.length, className: `map-empty`, children: g2(n2 ? `common.processing` : `map.empty`) }) })] })] }) });
});
function ot({ jobs: e2, gameTexts: t2, currentTime: n2, online: r2, busyKey: i2, onCancel: a2, onClear: o2, kind: s2 = `dispatch` }) {
  let { language: c2, t: l2 } = u(), d2 = [78, 150, 92, 280, 135, 135, 180, 74].map((e3) => e3 + 8);
  function f2(e3) {
    if (e3.scheduleStatus === `succeeded`) return [`succeeded`, l2(`map.plunderSucceeded`)];
    if (e3.scheduleStatus === `failed`) return [`failed`, e3.lastError ? e3.lastError !== `E000000` && t2[e3.lastError] || we(l2, e3.lastError) : l2(`map.plunderReasonUnavailable`)];
    if (e3.scheduleStatus === `cancelled`) return [`cancelled`, l2(`map.plunderCancelled`)];
    if (e3.scheduleStatus === `expired`) return [`expired`, l2(`map.taskExpired`)];
    if (e3.scheduleStatus === `running`) return [`running`, l2(`map.plunderRunning`)];
    if (!r2 || e3.scheduleStatus === `waiting_connection`) return [`waiting`, l2(`map.waitingConnection`)];
    let i3 = F(e3, n2);
    return i3 === `ready` ? [`ready`, l2(`map.taskReady`)] : i3 === `protected` ? [`protected`, l2(`map.taskProtected`)] : [`pending`, l2(`map.taskPending`)];
  }
  return (0, E.jsxs)(`section`, { className: `map-scheduled-group map-scheduled-group--secret`, children: [(0, E.jsxs)(`header`, { className: `map-scheduled-group-title`, children: [(0, E.jsx)(`strong`, { className: `map-scheduled-kind map-scheduled-kind--secret`, children: l2(s2 === `ghost` ? `map.ghostScout` : `map.secretTask`) }), (0, E.jsx)(`span`, { children: l2(`common.itemCount`, { count: e2.length }) }), (0, E.jsx)(`button`, { disabled: !!i2 || !e2.some((e3) => [`succeeded`, `failed`, `cancelled`, `expired`].includes(e3.scheduleStatus)), onClick: () => o2(s2), children: l2(`map.clearPlunderHistory`) })] }), (0, E.jsx)(`div`, { className: `map-table-scroll`, children: (0, E.jsxs)(`table`, { className: `map-table map-table--scheduled-plunder map-table--scheduled-secret`, style: { minWidth: d2.reduce((e3, t3) => e3 + t3, 0) }, "aria-label": l2(s2 === `ghost` ? `map.ghostScout` : `map.secretTask`), children: [(0, E.jsx)(`colgroup`, { children: d2.map((e3, t3) => (0, E.jsx)(`col`, { style: { width: e3 } }, t3)) }), (0, E.jsx)(`thead`, { children: (0, E.jsxs)(`tr`, { className: `map-row map-head`, children: [(0, E.jsx)(`th`, { scope: `col`, children: l2(`map.server`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.owner`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.quality`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.rewards`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.completionTime`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.plunderAt`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.plunderResult`) }), (0, E.jsx)(`th`, { scope: `col`, children: l2(`map.actions`) })] }) }), (0, E.jsxs)(`tbody`, { children: [e2.map((e3) => {
    let n3 = `${e3.serverId}:${e3.uuid}`, [r3, o3] = f2(e3), s3 = e3.scheduleStatus === `scheduled` || e3.scheduleStatus === `waiting_connection`;
    return (0, E.jsxs)(`tr`, { className: `map-row`, children: [(0, E.jsxs)(`td`, { children: [`#`, e3.serverId] }), (0, E.jsx)(`td`, { children: e3.ownerName || e3.ownerUid || e3.uuid }), (0, E.jsx)(`td`, { children: e3.isSpecial ? l2(`map.specialQuality`) : L(e3.quality) }), (0, E.jsx)(`td`, { className: `map-reward-cell`, children: e3.rewards?.length ? (0, E.jsx)(`span`, { className: `map-reward-list map-reward-list--retained`, children: e3.rewards.map((e4) => {
      let n4 = M(e4, t2), r4 = `${n4} \xD7${I(e4.count, c2)}`;
      return (0, E.jsxs)(`span`, { className: `map-reward-item`, title: r4, "aria-label": r4, children: [(0, E.jsx)(v, { assetPath: e4.iconPath, alt: n4, className: `map-reward-icon` }), (0, E.jsxs)(`strong`, { children: [`\xD7`, _(e4.count)] })] }, e4.key);
    }) }) : `-` }), (0, E.jsx)(`td`, { children: N(e3.completionTime, c2) }), (0, E.jsx)(`td`, { children: N(e3.plunderAt, c2) }), (0, E.jsx)(`td`, { className: `map-plunder-result`, title: o3, children: (0, E.jsx)(`span`, { className: `map-task-status ${r3}`, children: o3 }) }), (0, E.jsxs)(`td`, { className: `map-plunder-actions`, children: [s3 && (0, E.jsx)(`button`, { disabled: i2 === n3, onClick: () => a2(e3), children: l2(`common.cancel`) }), !s3 && `-`] })] }, n3);
  }), e2.length === 0 && (0, E.jsx)(`tr`, { children: (0, E.jsx)(`td`, { className: `map-empty`, colSpan: d2.length, children: l2(`map.empty`) }) })] })] }) })] });
}
function st({ jobs: e2, gameTexts: t2, currentTime: n2, online: i2, busyKey: a2, onCancel: o2, onPlunderAgain: s2, onClear: c2 }) {
  let { language: d2, t: f2 } = u(), p2 = [78, 150, 86, 110, 200, 280, 92].map((e3) => e3 + 8), ee2 = new Set(e2.filter((e3) => [`scheduled`, `waiting_connection`, `running`].includes(e3.scheduleStatus)).map((e3) => `${e3.serverId}:${e3.uuid}`));
  return (0, E.jsxs)(`section`, { className: `map-scheduled-group map-scheduled-group--truck`, children: [(0, E.jsxs)(`header`, { className: `map-scheduled-group-title`, children: [(0, E.jsx)(`strong`, { className: `map-scheduled-kind map-scheduled-kind--truck`, children: f2(`map.truck`) }), (0, E.jsx)(`span`, { children: f2(`common.itemCount`, { count: e2.length }) }), (0, E.jsx)(`button`, { disabled: !!a2 || !e2.some((e3) => [`succeeded`, `failed`, `cancelled`, `expired`].includes(e3.scheduleStatus)), onClick: c2, children: f2(`map.clearPlunderHistory`) })] }), (0, E.jsx)(`div`, { className: `map-table-scroll`, children: (0, E.jsxs)(`table`, { className: `map-table map-table--scheduled-plunder map-table--scheduled-truck`, style: { minWidth: p2.reduce((e3, t3) => e3 + t3, 0) }, "aria-label": f2(`map.truck`), children: [(0, E.jsx)(`colgroup`, { children: p2.map((e3, t3) => (0, E.jsx)(`col`, { style: { width: e3 } }, t3)) }), (0, E.jsx)(`thead`, { children: (0, E.jsxs)(`tr`, { className: `map-row map-head`, children: [(0, E.jsx)(`th`, { scope: `col`, children: f2(`map.server`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.playerAlliance`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.quality`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.plunderCount`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.plunderResult`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.plunderRewards`) }), (0, E.jsx)(`th`, { scope: `col`, children: f2(`map.actions`) })] }) }), (0, E.jsxs)(`tbody`, { children: [e2.map((e3) => {
    let c3 = `${e3.serverId}:${e3.uuid}`, u2 = `truck:${c3}`, p3 = `truck:${e3.jobId || `${c3}:${e3.scheduledAt}`}`, te2 = l(e3), m2 = Math.max(0, Number(e3.robTimes) || 0), h2 = r(e3, n2), ne2 = e3.lastError ? C(f2, e3.lastError) : f2(`map.plunderReasonUnavailable`), re2 = e3.scheduleStatus === `succeeded` && e3.battleWon === true ? f2(`map.plunderWon`) : e3.scheduleStatus === `succeeded` && e3.battleWon === false ? f2(`map.plunderLost`) : e3.scheduleStatus === `succeeded` ? `-` : e3.scheduleStatus === `failed` ? ne2 : e3.scheduleStatus === `cancelled` ? f2(`map.plunderCancelled`) : e3.scheduleStatus === `expired` ? ne2 : e3.scheduleStatus === `running` ? f2(`map.plunderRunning`) : !i2 || e3.scheduleStatus === `waiting_connection` ? f2(`map.waitingConnection`) : h2 === `expired` ? f2(`map.taskExpired`) : h2 === `protected` ? `${f2(`map.truckProtected`)} ${Je(Number(e3.protectTime) - n2)}` : f2(h2 === `full` ? `map.truckPlunderFull` : `map.truckReady`), ie2 = e3.scheduleStatus === `scheduled` || e3.scheduleStatus === `waiting_connection`, ae2 = i2 && e3.scheduleStatus === `succeeded` && h2 !== `invalid` && h2 !== `full` && h2 !== `expired` && !ee2.has(c3);
    return (0, E.jsxs)(`tr`, { className: `map-row`, children: [(0, E.jsxs)(`td`, { children: [`#`, e3.serverId] }), (0, E.jsx)(`td`, { children: e3.ownerName || e3.allianceName || e3.uuid }), (0, E.jsx)(`td`, { children: e3.isSpecialURQuality ? f2(`map.reindeerQuality`) : L(e3.quality) }), (0, E.jsxs)(`td`, { children: [m2, `/`, te2] }), (0, E.jsx)(`td`, { className: `map-plunder-result`, title: re2 === `-` ? `` : re2, children: re2 }), (0, E.jsx)(`td`, { className: `map-reward-cell`, children: e3.plunderRewards?.length ? (0, E.jsx)(`span`, { className: `map-reward-list map-reward-list--retained`, children: e3.plunderRewards.map((e4) => {
      let n3 = M(e4, t2), r2 = `${n3} \xD7${I(e4.count, d2)}`;
      return (0, E.jsxs)(`span`, { className: `map-reward-item`, title: r2, "aria-label": r2, children: [(0, E.jsx)(v, { assetPath: e4.iconPath, alt: n3, className: `map-reward-icon` }), (0, E.jsxs)(`strong`, { children: [`\xD7`, _(e4.count)] })] }, e4.key);
    }) }) : `-` }), (0, E.jsxs)(`td`, { className: `map-plunder-actions`, children: [ie2 && (0, E.jsx)(`button`, { disabled: a2 === u2, onClick: () => o2(e3), children: f2(`common.cancel`) }), ae2 && (0, E.jsx)(`button`, { disabled: a2 === u2, onClick: () => s2(e3), children: f2(`map.plunderAgain`) }), !ie2 && !ae2 && `-`] })] }, p3);
  }), e2.length === 0 && (0, E.jsx)(`tr`, { children: (0, E.jsx)(`td`, { className: `map-empty`, colSpan: p2.length, children: f2(`map.empty`) }) })] })] }) })] });
}
function ct({ items: e2, value: t2, label: n2, allLabel: r2, onChange: i2 }) {
  let a2 = (0, y.useRef)(null), o2 = e2.find((e3) => e3.key === t2), s2 = (e3) => {
    i2(e3), a2.current?.removeAttribute(`open`);
  };
  return (0, E.jsxs)(`details`, { ref: a2, className: `map-item-filter`, children: [(0, E.jsxs)(`summary`, { "aria-label": n2, title: n2, children: [o2 && (0, E.jsx)(v, { assetPath: o2.iconPath, alt: o2.name, className: `map-item-filter-icon` }), (0, E.jsx)(`span`, { children: o2?.name || r2 })] }), (0, E.jsxs)(`div`, { className: `map-item-filter-menu`, children: [(0, E.jsx)(`button`, { type: `button`, className: t2 ? `` : `active`, onClick: () => s2(``), children: r2 }), e2.map((e3) => (0, E.jsxs)(`button`, { type: `button`, className: e3.key === t2 ? `active` : ``, onClick: () => s2(e3.key), children: [(0, E.jsx)(v, { assetPath: e3.iconPath, alt: e3.name, className: `map-item-filter-icon` }), (0, E.jsx)(`span`, { children: e3.name })] }, e3.key))] })] });
}
function lt({ items: e2, value: t2, gameTexts: n2, onChange: r2 }) {
  let { t: i2 } = u(), a2 = (0, y.useRef)(null), o2 = e2.find((e3) => e3.key === t2), s2 = (e3) => Ke(i2, n2, e3.treasureType, e3.suppliesType, e3.treasureNameKey), c2 = (e3) => {
    r2(e3), a2.current?.removeAttribute(`open`);
  };
  return (0, E.jsxs)(`details`, { ref: a2, className: `map-item-filter`, children: [(0, E.jsx)(`summary`, { "aria-label": i2(`map.treasureType`), title: i2(`map.treasureType`), children: (0, E.jsx)(`span`, { children: o2 ? s2(o2) : i2(`map.allTreasureTypes`) }) }), (0, E.jsxs)(`div`, { className: `map-item-filter-menu`, children: [(0, E.jsx)(`button`, { type: `button`, className: t2 ? `` : `active`, onClick: () => c2(``), children: i2(`map.allTreasureTypes`) }), e2.map((e3) => (0, E.jsx)(`button`, { type: `button`, className: e3.key === t2 ? `active` : ``, onClick: () => c2(e3.key), children: (0, E.jsxs)(`span`, { children: [s2(e3), ` (`, e3.count, `)`] }) }, e3.key))] })] });
}
function R({ activeTab: r2, onActiveTabChange: i2, scanState: a2, summary: o2, online: l2, onState: _2, onCounts: v2, onLog: b2, autoScanConfig: x2, autoScanRunning: Ce2, onAutoScanConfig: we2 }) {
  let { language: Te2, t: S2 } = u(), C2 = a2 || Ve, Ee2 = Se(o2, C2.serverId), De2 = (0, y.useRef)(_2), ke2 = (0, y.useRef)(v2), w2 = (0, y.useRef)(b2), T2 = (0, y.useRef)(0), D2 = (0, y.useRef)(/* @__PURE__ */ new Map()), Le2 = (0, y.useRef)(0), Re2 = (0, y.useRef)(``), O2 = (0, y.useRef)(0), M2 = (0, y.useRef)(0), [Ke2, qe2] = (0, y.useState)(() => rt(C2.selectedTypes)), P2 = (0, y.useRef)(false), [F2, Qe2] = (0, y.useState)(() => {
    let e2 = localStorage.getItem(Me);
    return e2 === `normal` || e2 === `fast` ? e2 : C2.scanMode || `normal`;
  }), [$e2, I2] = (0, y.useState)(`city`), L2 = r2 ?? $e2, [et2, nt2] = (0, y.useState)(``), [R2, ut] = (0, y.useState)(() => C2.serverId), [dt, ft] = (0, y.useState)([]), [pt, mt] = (0, y.useState)({ resource: [], monster: [] }), [ht, gt] = (0, y.useState)([]), [_t, vt] = (0, y.useState)(() => Ee2 || {}), [yt, bt] = (0, y.useState)(Ee2 !== null), [xt, St] = (0, y.useState)({ truck: [], railway: [] }), [Ct, wt] = (0, y.useState)([]), [Tt, Et] = (0, y.useState)(0), [z, Dt] = (0, y.useState)(null), [Ot, kt] = (0, y.useState)(`all`), [At, jt] = (0, y.useState)({}), [Mt, Nt] = (0, y.useState)(false), [Pt, Ft] = (0, y.useState)(0), [It, Lt] = (0, y.useState)({}), [Rt, zt] = (0, y.useState)({}), [Bt, Vt] = (0, y.useState)(``), [Ht, Ut] = (0, y.useState)({}), [Wt, Gt] = (0, y.useState)({}), [Kt, qt] = (0, y.useState)(``), [Jt, Yt] = (0, y.useState)(Be), [B, V] = (0, y.useState)(1), [H, U] = (0, y.useState)([]), [Xt, Zt] = (0, y.useState)(() => C2.serverId > 0), [Qt, $t] = (0, y.useState)({}), [en, W] = (0, y.useState)(0), [tn, nn] = (0, y.useState)(``), [rn, an] = (0, y.useState)(Date.now()), [G, on] = (0, y.useState)({}), [sn, cn] = (0, y.useState)({}), [ln, un] = (0, y.useState)([]), [dn, fn] = (0, y.useState)([]), [pn, K] = (0, y.useState)(``), [mn, hn] = (0, y.useState)(`0`), [gn, _n] = (0, y.useState)(0), [vn, yn] = (0, y.useState)(0), [bn, xn] = (0, y.useState)(``), [Sn, Cn] = (0, y.useState)(false), [wn, Tn] = (0, y.useState)(``), [En, q] = (0, y.useState)(``), [Dn, On] = (0, y.useState)(false), [kn, An] = (0, y.useState)(false), [jn, Mn] = (0, y.useState)(false), [Nn, Pn] = (0, y.useState)(() => localStorage.getItem(Ne) === `true`), [J, Fn] = (0, y.useState)(() => localStorage.getItem(Pe) !== `false`), [In, Ln] = (0, y.useState)({ playerUid: ``, allianceId: `` }), [Y, Rn] = (0, y.useState)(`manual`), [zn, Bn] = (0, y.useState)(``), Vn = (0, y.useRef)(C2.isReading), X = (0, y.useRef)(null);
  (0, y.useEffect)(() => {
    localStorage.setItem(Me, F2);
  }, [F2]), (0, y.useEffect)(() => {
    localStorage.setItem(Ne, String(Nn));
  }, [Nn]), (0, y.useEffect)(() => {
    localStorage.setItem(Pe, String(J));
  }, [J]), (0, y.useEffect)(() => {
    if (L2 !== `treasure` || !l2 || R2 <= 0 || C2.serverId !== R2) return;
    let e2 = false;
    return (async () => {
      let n2 = await t();
      if (e2) return;
      if (Ln({ playerUid: n2.playerUid, allianceId: n2.allianceId }), U((e3) => He(e3, n2.states)), !J || C2.isReading) {
        Re2.current = ``;
        return;
      }
      let r3 = `${R2}:${C2.scanRunId || `stored`}`;
      if (Re2.current !== r3) {
        Re2.current = r3, Mn(true);
        try {
          let t2 = await de(R2);
          if (e2) return;
          Ln({ playerUid: t2.playerUid, allianceId: t2.allianceId }), U((e3) => He(e3, t2.states)), _n((e3) => e3 + 1);
        } catch (e3) {
          throw Re2.current = ``, e3;
        } finally {
          e2 || Mn(false);
        }
      }
    })().catch((e3) => w2.current(`treasure context error ` + String(e3))), () => {
      e2 = true;
    };
  }, [L2, R2, l2, J, C2.isReading, C2.scanRunId, C2.serverId]);
  let Hn = Math.max(1, Math.ceil(en / je)), Un = (0, y.useMemo)(() => new Set(Object.keys(G)), [G]), Wn = (0, y.useMemo)(() => new Set(Object.keys(sn)), [sn]), Gn = Number(mn), Kn = Number.isSafeInteger(Gn) && Gn >= 0, qn = !C2.isReading && z != null && z.serverId === R2 && z.id === C2.scanRunId && z.status !== `running`, Jn = qn ? z.serverId : C2.serverId, Z = qn ? z.createdAt : C2.startedAt || 0, Yn = C2.isReading ? 0 : qn ? z.updatedAt : C2.updatedAt || 0, Xn = Z > 0 ? Math.max(0, (C2.isReading ? rn : Yn || Z) - Z) : -1, Zn = Math.max(0, Math.min(100, Number(C2.progressPercent) || 0)), Qn = qn ? z.error : C2.lastError;
  (0, y.useEffect)(() => {
    De2.current = _2, ke2.current = v2, w2.current = b2;
  }, [v2, b2, _2]), (0, y.useEffect)(() => {
    let e2 = Se(o2, R2);
    e2 ? (vt(e2), bt(true)) : (vt({}), bt(false));
  }, [R2, o2]), (0, y.useEffect)(() => {
    P2.current || qe2(rt(C2.selectedTypes));
  }, [C2.selectedTypes]), (0, y.useEffect)(() => {
    if (C2.serverId !== R2) {
      if (T2.current += 1, D2.current.clear(), C2.serverId <= 0) {
        ut(0), V(1), U([]), W(0), Zt(false);
        return;
      }
      ut(C2.serverId), V(1), U([]), W(0), Zt(true);
    }
  }, [R2, C2.serverId]), (0, y.useEffect)(() => p(`bridge://player-mark-changed`, () => {
    Ft((e2) => e2 + 1);
  }), []), (0, y.useEffect)(() => {
    Q();
    let e2 = p(`bridge://dispatch-plunder-changed`, Q), t2 = p(`bridge://truck-plunder-changed`, Q);
    return () => {
      e2(), t2();
    };
  }, []), (0, y.useEffect)(() => {
    if (!C2.isReading && L2 !== `dispatch` && L2 !== `ghost` && L2 !== `truck` && L2 !== `scheduledPlunder`) return;
    an(Date.now());
    let e2 = window.setInterval(() => an(Date.now()), 1e3);
    return () => window.clearInterval(e2);
  }, [L2, C2.isReading]), (0, y.useEffect)(() => {
    on({}), L2 === `scheduledPlunder` && Q();
  }, [L2]), (0, y.useEffect)(() => {
    let e2 = Vn.current;
    if (Vn.current = C2.isReading, e2 && !C2.isReading) {
      let e3 = _e(`scan-complete`);
      e3.rows && _n((e4) => e4 + 1), e3.options && yn((e4) => e4 + 1);
    }
  }, [C2.isReading]), (0, y.useEffect)(() => {
    if (!C2.isReading) {
      X.current !== null && (window.clearTimeout(X.current), X.current = null);
      return;
    }
    X.current === null && (X.current = window.setTimeout(() => {
      X.current = null, _e(`scan-progress`).rows && _n((e2) => e2 + 1);
    }, Fe));
  }, [C2.isReading, C2.readBlocks]), (0, y.useEffect)(() => () => {
    X.current !== null && window.clearTimeout(X.current);
  }, []), (0, y.useEffect)(() => {
    if (L2 !== `scheduledPlunder` && !(L2 === `treasure` && l2 && J && !In.playerUid)) {
      if (!R2) {
        T2.current += 1, V(1), U([]), W(0);
        return;
      }
      rr(B);
    }
  }, [L2, Ot, R2, B, It, Rt, Bt, Ht, Wt, Kt, At, Jt, Mt, Pt, gn, Nn, l2, J, In]), (0, y.useEffect)(() => {
    if (R2 <= 0) return;
    let e2 = O2.current + 1;
    O2.current = e2, ge(R2).then((t2) => {
      if (e2 !== O2.current) return;
      let n2 = t2.serverId;
      if (n2 !== R2) {
        ut(n2);
        return;
      }
      ft(t2.alliances), mt(t2.names), gt(t2.dispatchLevels), vt(t2.counts), bt(true), ke2.current(t2.serverId, t2.counts), St(t2.rewardItems), wt(t2.treasureTypes), Et(t2.noAllianceCount), Dt(t2.scanProgress), kt((e3) => {
        if (e3 === `none`) return t2.noAllianceCount > 0 ? e3 : `all`;
        let n3 = tt(e3);
        return !n3 || t2.alliances.some((e4) => e4.name === n3) ? e3 : `all`;
      }), jt((e3) => ({ resource: t2.names.resource.some((t3) => t3.key === e3.resource) ? e3.resource : void 0, monster: t2.names.monster.some((t3) => t3.key === e3.monster) ? e3.monster : void 0 })), qt((e3) => !e3 || t2.dispatchLevels.includes(Number(e3)) ? e3 : ``), Vt((e3) => !e3 || t2.treasureTypes.some((t3) => t3.key === e3) ? e3 : ``);
    }).catch((t2) => {
      e2 === O2.current && w2.current(`map options error ` + String(t2));
    });
  }, [R2, vn]), (0, y.useEffect)(() => {
    let e2 = /* @__PURE__ */ new Set();
    if (L2 === `scheduledPlunder`) {
      for (let t3 of ln) {
        t3.lastError && /^(?:\d+|dispatch_des\d+|ghostrecon_\d+)$/.test(t3.lastError) && e2.add(t3.lastError);
        for (let n2 of t3.rewards || []) n2.nameKey && e2.add(n2.nameKey);
      }
      for (let t3 of dn) for (let n2 of t3.plunderRewards || []) n2.nameKey && e2.add(n2.nameKey);
    } else L2 === `resource` ? (pt.resource.forEach((t3) => e2.add(t3.key)), H.forEach((t3) => {
      let n2 = String(k(t3, `resourceNameKey`) || ``).trim();
      n2 && e2.add(n2);
    }), e2.add(`300039`), e2.add(`372138`)) : L2 === `monster` ? (pt.monster.forEach((t3) => e2.add(t3.key)), H.forEach((t3) => {
      let n2 = String(k(t3, `monsterNameKey`) || ``).trim();
      n2 && e2.add(n2);
    })) : L2 === `treasure` ? (Ct.forEach((t3) => {
      let n2 = String(Oe(t3.suppliesType) || t3.treasureNameKey || ``).trim();
      n2 && e2.add(n2);
    }), H.forEach((t3) => {
      let n2 = String(Oe(k(t3, `suppliesType`)) || k(t3, `treasureNameKey`)).trim();
      n2 && e2.add(n2);
    })) : A(L2) && H.forEach((t3) => {
      for (let n2 of [`currentGoods`, `lostGoods`]) {
        let r3 = k(t3, n2);
        if (Array.isArray(r3)) for (let t4 of r3) t4.nameKey && e2.add(t4.nameKey);
      }
    });
    let t2 = M2.current + 1;
    M2.current = t2, $t({}), e2.size !== 0 && s(Te2, [...e2]).then((e3) => {
      t2 === M2.current && $t(e3);
    }).catch((e3) => {
      t2 === M2.current && w2.current(`LastWar locale error ` + String(e3));
    });
  }, [L2, Te2, pt, ln, H, Ct, dn]);
  async function $n() {
    let e2 = rt(Ke2);
    qe2(e2), Dt(null), xn(``);
    try {
      _2(await pe({ selectedTypes: e2, scanMode: F2 })), b2(`full map scan started`);
    } catch (e3) {
      let t2 = String(e3);
      xn(t2), b2(`map scan start error ` + t2);
    }
  }
  async function er() {
    try {
      _2(await te()), b2(`full map scan stopped`);
    } catch (e2) {
      b2(`map scan stop error ` + String(e2));
    }
  }
  async function tr() {
    xn(``);
    try {
      let e2 = await c(R2);
      O2.current += 1, _2(e2), ft([]), mt({ resource: [], monster: [] }), gt([]), vt(xe), bt(true), ke2.current(e2.serverId, xe), St({ truck: [], railway: [] }), wt([]), Et(0), Dt(null), kt(`all`), jt({}), qt(``), zt({}), Vt(``), ut(e2.serverId), D2.current.clear(), V(1), U([]), W(0), Zt(false), on({}), cn({}), _n((e3) => e3 + 1), yn((e3) => e3 + 1), b2(`map scan data cleared`);
    } catch (e2) {
      let t2 = String(e2);
      xn(t2), b2(`map clear error ` + t2);
    }
  }
  function nr(e2 = B, t2 = je) {
    let n2 = L2 === `scheduledPlunder` ? `city` : L2, r3 = tt(Ot), i3 = Ue(n2) ? n2 : null, a3 = A(n2) ? n2 : null, o3 = i3 ? It[i3] : void 0, s2 = n2 === `treasure` ? Ct.find((e3) => e3.key === Bt) : void 0;
    return { serverId: R2, keyword: et2, resourceNameKey: n2 === `resource` ? At.resource : void 0, monsterNameKey: n2 === `monster` ? At.monster : void 0, treasureType: s2?.treasureType, suppliesType: s2?.suppliesType, alliance: n2 === `city` && r3 ? r3 : void 0, withoutAlliance: n2 === `city` && Ot === `none` ? true : void 0, markedOnly: n2 === `city` && Mt ? true : void 0, page: e2, pageSize: t2, sorts: Jt[n2], quality: o3 === `special` || o3 === `reindeer` ? void 0 : o3, specialOnly: o3 === `special` ? true : void 0, reindeerOnly: o3 === `reindeer` ? true : void 0, itemKey: a3 ? Rt[a3] : void 0, completionStatus: n2 === `dispatch` || n2 === `ghost` ? Ht[n2] : void 0, plunderableOnly: ve(n2, We(n2) && Wt[n2] === true), includeForeignRadarTreasures: n2 === `treasure` ? Nn : void 0, luckyFirst: n2 === `treasure` ? J : void 0, viewerUid: n2 === `treasure` ? In.playerUid : void 0, viewerAllianceId: n2 === `treasure` ? In.allianceId : void 0, minLevel: n2 === `dispatch` && Kt ? Number(Kt) : void 0, maxLevel: n2 === `dispatch` && Kt ? Number(Kt) : void 0 };
  }
  async function rr(e2 = B) {
    if (L2 === `scheduledPlunder`) return;
    let t2 = T2.current + 1;
    T2.current = t2, Zt(true);
    try {
      let n2 = nr(e2), r3 = await ce(L2, n2);
      if (t2 === T2.current) {
        let t3 = Math.max(1, Math.ceil(r3.total / je));
        if (e2 > t3) {
          V(t3);
          return;
        }
        if (U(r3.rows), W(r3.total), L2 === `treasure` && l2 && !C2.isReading && C2.serverId === R2 && r3.rows.length > 0) {
          let e3 = Le2.current + 1;
          Le2.current = e3, Mn(true), g(R2, r3.rows).then((t4) => {
            e3 === Le2.current && U((e4) => He(e4, t4.states));
          }).catch((e4) => {
            let t4 = String(e4);
            !t4.includes(`SCAN_RUNNING`) && !t4.includes(`stop the map scan first`) && w2.current(`treasure state refresh error ` + t4);
          }).finally(() => {
            e3 === Le2.current && Mn(false);
          });
        } else L2 === `treasure` && Mn(false);
      }
    } catch (e3) {
      t2 === T2.current && (L2 === `treasure` && Mn(false), U([]), W(0), b2(`map search error ` + String(e3)));
    } finally {
      t2 === T2.current && Zt(false);
    }
  }
  function ir(e2) {
    if (e2 === L2) return;
    T2.current += 1, Le2.current += 1;
    let t2 = L2 === `scheduledPlunder` ? null : L2, n2 = e2 === `scheduledPlunder` ? null : e2, r3 = n2 ? D2.current.has(n2) : true, a3 = ye(D2.current, t2, { page: B, rows: H, total: en }, n2);
    i2 ? i2(e2) : I2(e2), V(a3.page), U(a3.rows), W(a3.total), Zt(!!(n2 && !r3)), q(``);
  }
  let ar = (0, y.useCallback)((e2) => {
    L2 !== `scheduledPlunder` && (Yt((t2) => ({ ...t2, [L2]: be(t2[L2], e2) })), V(1));
  }, [L2]), or = (0, y.useCallback)((e2) => {
    let t2 = `${e2.serverId}:${e2.uuid || ``}`;
    on((n2) => {
      if (n2[t2]) {
        let e3 = { ...n2 };
        return delete e3[t2], e3;
      }
      return { ...n2, [t2]: e2 };
    });
  }, []), sr = (0, y.useCallback)((e2) => {
    let t2 = `${e2.serverId}:${e2.uuid || ``}`;
    cn((n2) => {
      if (n2[t2]) {
        let e3 = { ...n2 };
        return delete e3[t2], e3;
      }
      return { ...n2, [t2]: e2 };
    });
  }, []);
  async function Q() {
    try {
      let e2 = await d();
      un(e2.dispatchJobs), fn(e2.truckJobs);
    } catch (e2) {
      w2.current(`dispatch plunder list error ` + String(e2));
    }
  }
  async function cr(e2, n2) {
    Cn(true), Tn(``);
    try {
      let r3 = await he(R2, e2, J, String(n2?.uuid || ``));
      if (Tn(S2(`map.treasuresQueued`, r3)), b2(`treasure claims queued eligible=${r3.eligible} queued=${r3.queued} skipped=${r3.skipped}`), r3.queued > 0) for (let e3 = 0; e3 < 1800; e3 += 1) {
        await new Promise((e5) => window.setTimeout(e5, 1e3));
        let e4 = await t();
        if (U((t2) => He(t2, e4.states)), e4.batch && (Tn(S2(`map.treasureClaimSummary`, e4.batch)), e4.batch.state !== `running`)) {
          _n((e5) => e5 + 1);
          break;
        }
      }
    } catch (e3) {
      let t2 = String(e3);
      Tn(m(S2, t2)), b2(`treasure claim error ` + t2);
    } finally {
      Cn(false);
    }
  }
  function $(e2) {
    we2({ ...x2, ...e2 });
  }
  function lr() {
    oe(zn).length !== 0 && ($({ serverIds: ue(x2.serverIds, zn) }), Bn(``));
  }
  async function ur() {
    let e2 = Object.values(sn);
    if (e2.length !== 0) {
      K(`schedule-truck`);
      try {
        await ne(e2), cn({}), await Q(), ir(`scheduledPlunder`), b2(`scheduled ${e2.length} truck plunder jobs`);
      } catch (e3) {
        b2(`truck plunder schedule error ` + String(e3));
      } finally {
        K(``);
      }
    }
  }
  async function dr() {
    let e2 = Object.values(G);
    if (e2.length !== 0 && Kn) {
      K(`schedule`);
      try {
        await h(e2, Gn), on({}), await Q(), ir(`scheduledPlunder`), b2(`scheduled ${e2.length} secret task plunder jobs`);
      } catch (e3) {
        b2(`dispatch plunder schedule error ` + String(e3));
      } finally {
        K(``);
      }
    }
  }
  async function fr() {
    if (!(L2 !== `city` || R2 <= 0)) {
      On(true), q(``);
      try {
        let e2 = await ie(nr(1, 200), { headers: [S2(`map.server`), `X`, `Y`, S2(`map.player`), `UID`, `UUID`, S2(`map.alliance`), S2(`map.level`), `HP`, S2(`automation.shieldEnds`), S2(`map.marked`), S2(`map.updatedAt`)], sheetName: S2(`map.city`), yesLabel: S2(`common.yes`), noLabel: S2(`common.no`) });
        e2.canceled || (q(S2(`map.exportExcelSuccess`, { count: e2.rowCount, path: e2.path })), b2(`exported ${e2.rowCount} city rows to ${e2.path}`));
      } catch (e2) {
        q(m(S2, e2)), b2(`city export error ` + String(e2));
      } finally {
        On(false);
      }
    }
  }
  async function pr() {
    let e2 = Object.values(G);
    if (e2.length !== 0) {
      An(true), q(``);
      try {
        let t2 = await re(e2), n2 = new Set(t2.sharedUuids);
        on((e3) => Object.fromEntries(Object.entries(e3).filter(([, e4]) => !n2.has(String(e4.uuid || ``))))), q(t2.failed > 0 ? S2(`map.shareAlliancePartial`, { shared: t2.shared, failed: t2.failed }) : S2(`map.shareAllianceSuccess`, { count: t2.shared })), b2(`shared ${t2.shared} secret tasks to alliance channel failed=${t2.failed}`);
      } catch (e3) {
        q(m(S2, e3)), b2(`dispatch alliance share error ` + String(e3));
      } finally {
        An(false);
      }
    }
  }
  async function mr(e2) {
    K(`clear:${e2}`), q(``);
    try {
      let t2 = Date.now();
      e2 === `truck` ? await n(t2) : await me(t2, e2), await Q();
    } catch (e3) {
      q(m(S2, e3)), b2(`plunder history clear error ` + String(e3));
    } finally {
      K(``);
    }
  }
  async function hr(e2) {
    let t2 = `${e2.serverId}:${e2.uuid}`;
    K(t2);
    try {
      await ee(e2.serverId, e2.taskKind === `ghost` ? `ghost:${e2.uuid}` : e2.uuid), await Q();
    } catch (e3) {
      b2(`dispatch plunder cancel error ` + String(e3));
    } finally {
      K(``);
    }
  }
  async function gr(e2) {
    let t2 = `truck:${e2.serverId}:${e2.uuid}`;
    K(t2);
    try {
      await fe(e2.serverId, e2.uuid), await Q();
    } catch (e3) {
      b2(`truck plunder cancel error ` + String(e3));
    } finally {
      K(``);
    }
  }
  async function _r(e2) {
    let t2 = `truck:${e2.serverId}:${e2.uuid}`;
    K(t2);
    try {
      await ne([e2]), await Q();
    } catch (e3) {
      b2(`truck plunder reschedule error ` + String(e3));
    } finally {
      K(``);
    }
  }
  let vr = (0, y.useCallback)(async (t2) => {
    if (t2.serverId !== C2.serverId) {
      w2.current(`map jump blocked stale server=${t2.serverId} current=${C2.serverId}`);
      return;
    }
    let n2 = String(k(t2, `marchUuid`) || ``).trim();
    if (L2 !== `scheduledPlunder` && A(L2) && n2) {
      let r4 = `${t2.serverId}:${n2}`;
      nn(r4);
      try {
        let r5 = await e({ serverId: t2.serverId, marchUuid: n2 });
        w2.current(`map march follow server=${r5.serverId} march=${r5.marchUuid}`);
      } catch (e2) {
        w2.current(`map march follow error ` + String(e2));
      } finally {
        nn(``);
      }
      return;
    }
    let r3 = Number(k(t2, `x`)), i3 = Number(k(t2, `y`));
    if (!Number.isInteger(r3) || !Number.isInteger(i3) || r3 < 1 || i3 < 1) return;
    let a3 = `${t2.serverId}:${r3}:${i3}`;
    nn(a3);
    try {
      let e2 = await f({ serverId: t2.serverId, x: r3, y: i3 });
      w2.current(`map coordinate jump server=${e2.serverId} x=${e2.x} y=${e2.y}`);
    } catch (e2) {
      w2.current(`map coordinate jump error ` + String(e2));
    } finally {
      nn(``);
    }
  }, [L2, C2.serverId]), yr = (0, y.useCallback)(async (e2) => {
    if (e2.ownerUid) try {
      await ae(e2, !e2.marked);
    } catch (e3) {
      w2.current(`player mark error ` + String(e3));
    }
  }, []);
  return (0, E.jsxs)(`section`, { className: `panel map-panel`, children: [(0, E.jsxs)(`div`, { className: `map-scan-tabs`, role: `tablist`, "aria-label": S2(`map.scanModeTabs`), children: [(0, E.jsx)(`button`, { type: `button`, role: `tab`, "aria-selected": Y === `manual`, className: Y === `manual` ? `active` : ``, onClick: () => Rn(`manual`), children: S2(`map.manualScan`) }), (0, E.jsx)(`button`, { type: `button`, role: `tab`, "aria-selected": Y === `auto`, className: Y === `auto` ? `active` : ``, onClick: () => Rn(`auto`), children: S2(`map.autoScan`) })] }), (0, E.jsxs)(`div`, { className: `map-header`, children: [(0, E.jsx)(`h2`, { children: S2(`map.title`) }), (0, E.jsxs)(`div`, { className: `map-actions`, children: [Z > 0 && (0, E.jsxs)(`div`, { className: `map-scan-timing`, children: [(0, E.jsxs)(`span`, { title: N(Z, Te2), children: [(0, E.jsx)(`small`, { children: S2(`map.startTime`) }), (0, E.jsx)(`time`, { dateTime: Ze(Z), children: Ye(Z) })] }), (0, E.jsx)(`i`, { "aria-hidden": `true`, children: `\u2192` }), (0, E.jsxs)(`span`, { title: N(Yn, Te2), children: [(0, E.jsx)(`small`, { children: S2(`map.endTime`) }), (0, E.jsx)(`time`, { dateTime: Ze(Yn), children: Ye(Yn, !Xe(Z, Yn)) })] }), (0, E.jsxs)(`span`, { className: `map-scan-duration`, children: [(0, E.jsx)(`small`, { children: S2(`map.totalDuration`) }), (0, E.jsx)(`time`, { children: Je(Xn) })] })] }), Y === `manual` && (0, E.jsxs)(E.Fragment, { children: [(0, E.jsxs)(`fieldset`, { className: `map-speed-toggle${F2 === `fast` ? ` fast` : ``}`, "aria-label": S2(`map.speed`), disabled: C2.isReading, children: [(0, E.jsx)(`span`, { className: `map-speed-slider`, "aria-hidden": `true` }), (0, E.jsxs)(`label`, { children: [(0, E.jsx)(`input`, { type: `radio`, name: `map-scan-speed`, checked: F2 === `normal`, onChange: () => Qe2(`normal`) }), (0, E.jsx)(`span`, { children: S2(`map.normalSpeed`) })] }), (0, E.jsxs)(`label`, { children: [(0, E.jsx)(`input`, { type: `radio`, name: `map-scan-speed`, checked: F2 === `fast`, onChange: () => Qe2(`fast`) }), (0, E.jsx)(`span`, { children: S2(`map.fastSpeed`) })] })] }), (0, E.jsx)(`button`, { className: C2.isReading ? `` : `primary`, onClick: $n, disabled: C2.isReading, children: S2(`map.startReading`) }), (0, E.jsx)(`button`, { className: C2.isReading ? `danger` : ``, onClick: er, disabled: !C2.isReading, children: S2(`common.stop`) }), (0, E.jsx)(`button`, { onClick: tr, disabled: !R2 || C2.isReading, children: S2(`map.clearServer`) })] })] })] }), Y === `auto` && (0, E.jsxs)(`div`, { className: `map-auto-scan-card`, children: [(0, E.jsxs)(`label`, { className: `map-auto-scan-master`, children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: x2.enabled, onChange: (e2) => $({ enabled: e2.target.checked }) }), (0, E.jsx)(`strong`, { children: S2(`map.enableAutoScan`) }), (0, E.jsx)(`span`, { children: Ce2 ? S2(`map.autoScanRunning`) : x2.enabled ? S2(`map.autoScanWaiting`) : S2(`map.autoScanDisabled`) })] }), (0, E.jsxs)(`div`, { className: `map-auto-scan-grid`, children: [(0, E.jsxs)(`div`, { className: `map-auto-scan-server-field`, children: [(0, E.jsx)(`span`, { children: S2(`map.targetServers`) }), (0, E.jsxs)(`span`, { className: `map-auto-scan-server-input`, children: [(0, E.jsx)(`input`, { value: zn, inputMode: `numeric`, placeholder: C2.serverId > 0 ? String(C2.serverId) : `8, 15, 120`, onChange: (e2) => Bn(e2.target.value), onKeyDown: (e2) => {
    e2.key === `Enter` && (e2.preventDefault(), lr());
  } }), (0, E.jsx)(`button`, { type: `button`, disabled: oe(zn).length === 0, onClick: lr, children: S2(`common.add`) })] }), (0, E.jsx)(`small`, { children: S2(`map.targetServersHint`) }), (0, E.jsx)(`span`, { className: `map-auto-scan-server-chips`, children: x2.serverIds.map((e2) => (0, E.jsxs)(`span`, { children: [e2, (0, E.jsx)(`button`, { type: `button`, "aria-label": `${S2(`common.remove`)} ${e2}`, onClick: () => $({ serverIds: le(x2.serverIds, e2) }), children: (0, E.jsx)(se, { name: `remove` }) })] }, e2)) })] }), (0, E.jsxs)(`label`, { children: [(0, E.jsx)(`span`, { children: S2(`map.scanIntervalMinutes`) }), (0, E.jsx)(`input`, { type: `number`, min: 20, max: `1440`, value: x2.intervalMinutes, onChange: (e2) => $({ intervalMinutes: Number(e2.target.value) }) })] }), (0, E.jsxs)(`label`, { children: [(0, E.jsx)(`span`, { children: S2(`map.speed`) }), (0, E.jsxs)(`select`, { value: x2.scanMode, onChange: (e2) => $({ scanMode: e2.target.value === `fast` ? `fast` : `normal` }), children: [(0, E.jsx)(`option`, { value: `normal`, children: S2(`map.normalSpeed`) }), (0, E.jsx)(`option`, { value: `fast`, children: S2(`map.fastSpeed`) })] })] })] }), (0, E.jsxs)(`div`, { className: `map-controls`, children: [(0, E.jsx)(`span`, { className: `map-controls-label`, children: S2(`map.scanTypes`) }), (0, E.jsx)(`div`, { className: `map-types map-types--compact`, children: Ie.map((e2) => (0, E.jsxs)(`label`, { children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: x2.selectedTypes.includes(e2.key), disabled: x2.selectedTypes.length === 1 && x2.selectedTypes[0] === e2.key, onChange: (t2) => $({ selectedTypes: t2.target.checked ? [...x2.selectedTypes, e2.key] : x2.selectedTypes.filter((t3) => t3 !== e2.key) }) }), S2(e2.label)] }, e2.key)) })] }), (0, E.jsxs)(`div`, { className: `map-auto-scan-options`, children: [(0, E.jsxs)(`label`, { children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: x2.returnToOriginalServer, onChange: (e2) => $({ returnToOriginalServer: e2.target.checked }) }), S2(`map.returnAfterAutoScan`)] }), (0, E.jsx)(`button`, { type: `button`, className: `primary`, disabled: !l2 || !x2.enabled || Ce2 || C2.isReading, onClick: () => $({ nextRunAt: Date.now() }), children: S2(`map.runAutoScanNow`) })] }), (0, E.jsx)(`small`, { children: S2(`map.autoScanNavigationNotice`) }), (0, E.jsxs)(`small`, { children: [S2(`map.nextAutoScan`), `: `, x2.enabled && x2.nextRunAt > 0 ? N(x2.nextRunAt, Te2) : `-`] })] }), (0, E.jsxs)(`div`, { className: `map-scan-summary`, children: [(0, E.jsx)(`span`, { className: `map-status-pill${C2.isReading ? ` active` : ``}`, children: C2.phase === `publishing` ? S2(`common.processing`) : C2.isReading ? S2(`map.reading`) : qn && z.status === `completed` ? S2(`common.completed`) : S2(`common.stopped`) }), (0, E.jsxs)(`span`, { children: [S2(`map.server`), ` `, (0, E.jsx)(`strong`, { children: Jn || `-` })] }), (0, E.jsxs)(`div`, { className: `map-progress${Zn < 50 ? ` low` : ``}${C2.isReading ? ` active` : ``}`, children: [(0, E.jsx)(`progress`, { className: `map-progress-bar`, max: 100, value: a2 ? Zn : void 0, "aria-label": S2(`map.scanProgress`) }), (0, E.jsx)(`span`, { children: a2 ? `${Zn}%` : `\u2014` })] })] }), bn && (0, E.jsx)(`div`, { className: `map-scan-error`, role: `alert`, children: m(S2, bn) }), !bn && Qn && (0, E.jsx)(`div`, { className: `map-scan-error`, role: `status`, children: m(S2, Qn) }), Y === `manual` && (0, E.jsxs)(`div`, { className: `map-controls`, children: [(0, E.jsx)(`span`, { className: `map-controls-label`, children: S2(`map.scanTypes`) }), (0, E.jsx)(`div`, { className: `map-types map-types--compact`, children: Ie.map((e2) => (0, E.jsxs)(`label`, { className: e2.enabled ? `` : `disabled`, children: [(0, E.jsx)(`input`, { type: `checkbox`, disabled: !e2.enabled, checked: Ke2.includes(e2.key), onChange: (t2) => {
    P2.current = true, qe2((n2) => t2.target.checked ? [...n2, e2.key] : n2.filter((t3) => t3 !== e2.key));
  } }), S2(e2.label)] }, e2.key)) })] }), (0, E.jsxs)(`div`, { className: `map-search`, children: [(0, E.jsxs)(`div`, { className: `map-tabs`, children: [Ie.map(({ key: e2 }) => (0, E.jsxs)(`button`, { className: L2 === e2 ? `active` : ``, onClick: () => ir(e2), children: [(0, E.jsx)(`span`, { className: `map-tab-label`, children: S2(ze[e2]) }), (0, E.jsx)(`span`, { className: `map-tab-count`, children: yt ? _t[e2] || 0 : `\u2014` })] }, e2)), (0, E.jsxs)(`button`, { className: L2 === `scheduledPlunder` ? `active` : ``, onClick: () => ir(`scheduledPlunder`), children: [(0, E.jsx)(`span`, { className: `map-tab-label`, children: S2(`map.scheduledPlunder`) }), (0, E.jsx)(`span`, { className: `map-tab-count`, children: ln.length + dn.length })] })] }), L2 !== `scheduledPlunder` && (0, E.jsxs)(`div`, { className: `map-searchbar`, children: [(0, E.jsx)(`input`, { value: et2, onChange: (e2) => {
    nt2(e2.target.value), Ge(L2) && At[L2] && jt((e3) => ({ ...e3, [L2]: void 0 }));
  }, "aria-label": S2(`map.searchLabel`), placeholder: S2(`map.searchLabel`) }), Ge(L2) && (0, E.jsxs)(`select`, { "aria-label": S2(`common.name`), value: At[L2] || ``, onChange: (e2) => {
    jt((t2) => ({ ...t2, [L2]: e2.target.value || void 0 })), nt2(``), V(1);
  }, children: [(0, E.jsx)(`option`, { value: ``, children: S2(`map.allNames`) }), pt[L2].map((e2) => (0, E.jsxs)(`option`, { value: e2.key, children: [j(Qt, e2.key, e2.key), ` (`, e2.count, `)`] }, e2.key))] }), L2 === `city` && (0, E.jsxs)(`select`, { "aria-label": S2(`map.allianceFilter`), value: Ot, onChange: (e2) => {
    kt(e2.target.value), V(1);
  }, children: [(0, E.jsx)(`option`, { value: `all`, children: S2(`map.allAlliances`) }), Tt > 0 && (0, E.jsxs)(`option`, { value: `none`, children: [S2(`map.noAlliance`), ` (`, Tt, `)`] }), dt.map((e2) => (0, E.jsxs)(`option`, { value: `name:${encodeURIComponent(e2.name)}`, children: [e2.name, ` (`, e2.count, `)`] }, e2.name))] }), L2 === `city` && (0, E.jsxs)(`label`, { className: `map-filter-field`, children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: Mt, onChange: (e2) => {
    Nt(e2.target.checked), V(1);
  } }), (0, E.jsx)(`span`, { children: S2(`map.markedOnly`) })] }), L2 === `treasure` && (0, E.jsx)(lt, { items: Ct, value: Bt, gameTexts: Qt, onChange: (e2) => {
    Vt(e2), V(1);
  } }), L2 === `treasure` && (0, E.jsxs)(`label`, { className: `map-filter-field`, children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: Nn, onChange: (e2) => {
    Pn(e2.target.checked), V(1);
  } }), S2(`map.showForeignRadarTreasures`)] }), L2 === `treasure` && (0, E.jsxs)(`label`, { className: `map-filter-field`, children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: J, onChange: (e2) => {
    Fn(e2.target.checked), V(1);
  } }), S2(`map.prioritizeLuckyTreasures`)] }), Ue(L2) && (0, E.jsxs)(E.Fragment, { children: [(L2 === `dispatch` || L2 === `ghost`) && (0, E.jsxs)(`select`, { "aria-label": S2(`common.status`), value: Ht[L2] || ``, onChange: (e2) => {
    Ut((t2) => ({ ...t2, [L2]: e2.target.value || void 0 })), V(1);
  }, children: [(0, E.jsx)(`option`, { value: ``, children: S2(`common.status`) }), (0, E.jsx)(`option`, { value: `completed`, children: S2(`common.completed`) }), (0, E.jsx)(`option`, { value: `pending`, children: S2(`common.inProgress`) })] }), L2 === `dispatch` && (0, E.jsxs)(`select`, { "aria-label": S2(`map.level`), value: Kt, onChange: (e2) => {
    qt(e2.target.value), V(1);
  }, children: [(0, E.jsx)(`option`, { value: ``, children: S2(`squad.afkAnyLevel`) }), ht.map((e2) => (0, E.jsx)(`option`, { value: e2, children: e2 }, e2))] }), (0, E.jsxs)(`select`, { "aria-label": S2(`map.quality`), value: It[L2] || ``, onChange: (e2) => {
    Lt((t2) => ({ ...t2, [L2]: e2.target.value || void 0 })), V(1);
  }, children: [(0, E.jsx)(`option`, { value: ``, children: S2(`map.allQualities`) }), (0, E.jsx)(`option`, { value: `n`, children: `N` }), (0, E.jsx)(`option`, { value: `r`, children: `R` }), (0, E.jsx)(`option`, { value: `sr`, children: `SR` }), (0, E.jsx)(`option`, { value: `ssr`, children: `SSR` }), (0, E.jsx)(`option`, { value: `ur`, children: `UR` }), (L2 === `dispatch` || L2 === `ghost`) && (0, E.jsx)(`option`, { value: `special`, children: S2(`map.specialQuality`) }), L2 === `truck` && (0, E.jsx)(`option`, { value: `reindeer`, children: S2(`map.reindeerQuality`) })] }), A(L2) && (0, E.jsx)(ct, { label: S2(`map.itemFilter`), allLabel: S2(`map.allRetainedGoods`), items: xt[L2], value: Rt[L2] || ``, onChange: (e2) => {
    zt((t2) => ({ ...t2, [L2]: e2 || void 0 })), !e2 && Jt[L2].some((e3) => e3.sortBy === `itemCount`) && Yt((e3) => ({ ...e3, [L2]: e3[L2].filter((e4) => e4.sortBy !== `itemCount`) })), V(1);
  } }), We(L2) && (0, E.jsxs)(`label`, { className: `map-filter-field map-plunderable-filter`, children: [(0, E.jsx)(`input`, { type: `checkbox`, checked: Wt[L2] === true, onChange: (e2) => {
    Gt((t2) => ({ ...t2, [L2]: e2.target.checked || void 0 })), V(1);
  } }), (0, E.jsx)(`span`, { children: S2(`map.plunderableOnly`) })] })] }), (0, E.jsx)(`button`, { onClick: () => {
    B === 1 ? rr(1) : V(1);
  }, children: S2(`common.search`) }), L2 === `city` && (0, E.jsx)(`button`, { disabled: Dn || R2 <= 0 || C2.isReading, onClick: fr, children: S2(Dn ? `map.exportingExcel` : `map.exportExcel`) }), L2 === `treasure` && (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: !Ae({ online: l2, isReading: C2.isReading, dataServerId: R2, currentServerId: C2.serverId, busy: Sn }), onClick: () => cr(`boxes`), children: S2(Sn ? `map.claimingTreasures` : `map.claimTreasureBoxes`) }), L2 === `treasure` && (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: !Ae({ online: l2, isReading: C2.isReading, dataServerId: R2, currentServerId: C2.serverId, busy: Sn }), onClick: () => cr(`season`), children: S2(Sn ? `map.claimingTreasures` : `map.claimSeasonTreasures`) }), L2 === `treasure` && wn && (0, E.jsx)(`span`, { className: `map-claim-result`, role: `status`, children: wn }), (L2 === `dispatch` || L2 === `ghost`) && (0, E.jsxs)(`label`, { className: `map-random-delay-field`, children: [(0, E.jsx)(`span`, { children: S2(`map.randomDelaySeconds`) }), (0, E.jsx)(`input`, { type: `number`, min: `0`, step: `1`, "aria-label": S2(`map.randomDelaySeconds`), value: mn, onChange: (e2) => hn(e2.target.value) })] }), (L2 === `dispatch` || L2 === `ghost`) && (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: Object.keys(G).length === 0 || pn === `schedule` || kn || !Kn, onClick: dr, children: S2(`map.scheduleSelected`, { count: Object.keys(G).length }) }), L2 === `dispatch` && (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: !l2 || C2.isReading || Object.keys(G).length === 0 || pn === `schedule` || kn, onClick: pr, children: S2(kn ? `map.sharingAlliance` : `map.shareAlliance`) }), L2 === `truck` && (0, E.jsx)(`button`, { className: `map-schedule-button`, disabled: Object.keys(sn).length === 0 || pn === `schedule-truck`, onClick: ur, children: S2(`map.scheduleSelectedTrucks`, { count: Object.keys(sn).length }) }), En && (0, E.jsx)(`span`, { className: `map-claim-result`, role: `status`, children: En }), (0, E.jsx)(`span`, { className: `map-result-count`, children: S2(`common.itemCount`, { count: en }) })] }), L2 === `scheduledPlunder` && (0, E.jsxs)(`div`, { className: `map-searchbar`, children: [En && (0, E.jsx)(`span`, { className: `map-claim-result`, role: `status`, children: En }), (0, E.jsx)(`span`, { className: `map-result-count`, children: S2(`common.itemCount`, { count: ln.length + dn.length }) })] }), L2 !== `scheduledPlunder` && (0, E.jsxs)(E.Fragment, { children: [(0, E.jsx)(at, { kind: L2, rows: H, loading: Xt, gameTexts: Qt, itemKey: A(L2) && Rt[L2] || ``, currentTime: rn, sortState: Jt[L2], selectedDispatchKeys: Un, selectedTruckKeys: Wn, jumpingKey: tn, jumpDisabled: !l2 || C2.isReading || !!tn, treasureStatesRefreshing: jn, treasureClaimBusy: Sn, treasureClaimDisabled: !Ae({ online: l2, isReading: C2.isReading, dataServerId: R2, currentServerId: C2.serverId, busy: Sn }), onSort: ar, onToggleDispatch: or, onToggleTruck: sr, onCoordinateJump: vr, onPlayerMark: yr, onClaimTreasure: (e2) => cr(`single`, e2) }), (0, E.jsx)(it, { page: B, totalPages: Hn, onPage: V })] }), L2 === `scheduledPlunder` && (0, E.jsx)(ot, { jobs: ln.filter((e2) => e2.taskKind !== `ghost`), gameTexts: Qt, currentTime: rn, online: l2, busyKey: pn, onCancel: hr, onClear: mr }), L2 === `scheduledPlunder` && (0, E.jsx)(ot, { kind: `ghost`, jobs: ln.filter((e2) => e2.taskKind === `ghost`), gameTexts: Qt, currentTime: rn, online: l2, busyKey: pn, onCancel: hr, onClear: mr }), L2 === `scheduledPlunder` && (0, E.jsx)(st, { jobs: dn, gameTexts: Qt, currentTime: rn, online: l2, busyKey: pn, onCancel: gr, onPlunderAgain: _r, onClear: () => mr(`truck`) })] })] });
}
export { R as MapDataPanel };
