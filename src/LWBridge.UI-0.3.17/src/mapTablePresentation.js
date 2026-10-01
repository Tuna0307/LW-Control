// Recovered Map table presentation. Exact source locators are recorded under
// evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/.

const LIVE_TARGET_KINDS = new Set(["truck", "railway"]);
const TREASURE_TYPE_KEYS = {
  1: "map.treasureTypeRadar", 2: "map.treasureTypeSiege", 3: "map.treasureTypeActivityRadar",
  5: "map.treasureTypeInvasion", 6: "map.treasureTypeZoneMobilization", 7: "map.treasureTypeSandworm",
  8: "map.treasureTypeAllianceBossSandbox", 9: "map.treasureTypePlayerKillMonster", 10: "map.treasureTypeFlowerCar",
  11: "map.treasureTypeOffSeason", 12: "map.treasureTypeBloodyQueen", 13: "map.treasureTypeGeneFragment",
  15: "map.treasureTypeFlowerTrainUpgrade", 16: "map.treasureTypeFlowerTrainCheer", 17: "map.treasureTypeWolfShadow",
  18: "map.treasureTypeSeasonBoss", 19: "map.treasureTypeWorldBoss", 20: "map.treasureTypeZwlGift",
};

function field(row, key) { return row[key]; }
function isLiveTarget(kind) { return LIVE_TARGET_KINDS.has(kind); }
export function lookupMapText(texts, key, defaultName) {
  const nameKey = String(key || "").trim();
  if (!nameKey) return defaultName;
  const name = texts[nameKey];
  return name && name !== nameKey ? name : defaultName;
}
export function mapNumber(value, language) {
  const number = Number(value);
  return Number.isFinite(number) ? number.toLocaleString(language) : "-";
}
export function mapTimestamp(value) {
  const number = Number(value);
  return !Number.isFinite(number) || number <= 0 ? 0 : number < 1_000_000_000_000 ? number * 1000 : number;
}
export function mapDate(value, language) {
  const time = mapTimestamp(value);
  return time ? new Date(time).toLocaleString(language) : "-";
}
function mapUpdated(value, language) { return value ? mapDate(value, language) : "-"; }
function mapShield(value, language) {
  const time = mapTimestamp(value);
  return time > Date.now() ? new Date(time).toLocaleString(language) : "-";
}
export function mapQuality(value) {
  const quality = Number(value);
  return quality === 1 ? "N" : quality === 2 ? "R" : quality === 3 ? "SR" : quality === 4 ? "SSR" : quality >= 5 ? "UR" : "-";
}
function mapCoordinates(row) {
  const x = Number(row.x), y = Number(row.y);
  return Number.isFinite(x) && Number.isFinite(y) ? `${x},${y}` : "-";
}
function originalResourceOccupied(row) {
  return ["gatherMarchUuid", "gatherUid"].some(key => {
    const value = String(row[key] || "").trim();
    return value !== "" && value !== "0";
  });
}
export function resourceOccupancy(row) {
  if (row.rebuildGatherOccupancyKnown === false) return null;
  if (row.rebuildGatherOccupancyKnown === true) return row.rebuildGatherOccupied === true;
  return originalResourceOccupied(row);
}
export function mapResourceStatus(row, t, texts) {
  const occupied = resourceOccupancy(row);
  if (occupied === null) return "—";
  return lookupMapText(texts, occupied ? "300039" : "372138", t(occupied ? "map.resourceGathering" : "map.resourceIdle"));
}
export function mapDuration(value) {
  if (!Number.isFinite(value) || value < 0) return "-";
  const seconds = Math.floor(value / 1000);
  return [Math.floor(seconds / 3600), Math.floor(seconds % 3600 / 60), seconds % 60]
    .map(part => String(part).padStart(2, "0")).join(":");
}
export function truckMaxLootCount(row) {
  if (row.isSpecialURQuality) return 1;
  const count = Number(row.maxLootCount);
  return Number.isFinite(count) && count > 0 ? Math.floor(count) : 0;
}
export function truckState(row, now) {
  if (!String(row.uuid || "").trim() || !Number.isInteger(row.serverId) || Number(row.serverId) <= 0) return "invalid";
  const arrival = Number(row.arriveTs) || 0;
  if (arrival > 0 && arrival <= now) return "expired";
  const maximum = truckMaxLootCount(row);
  const robbed = Math.max(0, Math.floor(Number(row.robTimes) || 0));
  return maximum > 0 && robbed >= maximum ? "full" : (Number(row.protectTime) || 0) > now ? "protected" : "ready";
}
export function mapTaskState(row, now) {
  const expires = mapTimestamp(row.taskExpireTime);
  const completed = mapTimestamp(row.completionTime);
  const plunderAt = mapTimestamp(row.plunderAt) || completed;
  const stolen = Number(row.stolenCount), maximum = Number(row.maxStealCount);
  return expires && now >= expires ? "expired" : maximum > 0 && stolen >= maximum ? "full"
    : !completed || now < completed ? "pending" : plunderAt && now < plunderAt ? "protected" : "ready";
}
export function mapTaskLabel(state, t) {
  return t(state === "expired" ? "map.taskExpired" : state === "full" ? "map.taskPlunderFull"
    : state === "protected" ? "map.taskProtected" : state === "ready" ? "map.taskReady" : "map.taskPending");
}
export function mapTaskSelectable(kind, row, now) {
  if (kind === "truck") return !["invalid", "full", "expired"].includes(truckState(row, now));
  const uuid = String(row.uuid || "").trim();
  return /^\d+$/.test(uuid) && !!mapTimestamp(row.completionTime) && !!mapTimestamp(row.plunderAt)
    && !["expired", "full"].includes(mapTaskState(row, now));
}
function treasureTypeKey(type) {
  const number = Number(type);
  return Number.isFinite(number) && TREASURE_TYPE_KEYS[number] ? TREASURE_TYPE_KEYS[number] : "map.treasureTypeUnknown";
}
function suppliesNameKey(type) {
  const number = Number(type);
  return number === 1 ? "season_s2_ice_supplies_11" : number === 3 ? "season_s3_supplies_1"
    : number === 4 ? "season4_supplies_name_2" : "";
}
function suppliesTypeKey(type) {
  const number = Number(type);
  return number === 1 ? "map.treasureTypeIceSupplies" : number === 3 ? "map.treasureTypeDesert"
    : number === 4 ? "map.treasureTypeLargeLuckyCat" : "map.treasureTypeUnknown";
}
export function treasureName(t, texts, type, suppliesType, nameKey) {
  const key = String(suppliesNameKey(suppliesType) || nameKey || "").trim();
  const name = t(key === "challenge_zombie_box_title" ? "map.treasureTypeTrialGift" : suppliesTypeKey(suppliesType));
  return key ? lookupMapText(texts, key, name) : t(treasureTypeKey(type));
}
function treasureWorldKey(state) {
  return ({ charging: "map.treasureStateCharging", claimable: "map.treasureStateClaimable",
    depleted: "map.treasureStateDepleted", expired: "map.treasureStateExpired", verifying: "map.treasureStateVerifying" })[String(state || "")] || "map.treasureStateUnknown";
}
function treasurePlayerKey(state, reason) {
  if (state === "claimed") return "map.treasurePlayerClaimed";
  if (reason === "other_alliance") return "map.treasurePlayerOtherAlliance";
  if (["no_scout", "no_squad", "squad_reserved"].includes(reason)) return "map.treasurePlayerNoScout";
  return ({ unclaimed: "map.treasurePlayerUnclaimed", dispatching: "map.treasurePlayerDispatching",
    scouting: "map.treasurePlayerScouting", digging: "map.treasurePlayerDigging", claiming: "map.treasurePlayerClaiming",
    claimed: "map.treasurePlayerClaimed", failed: "map.treasurePlayerUnclaimed", verifying: "map.treasurePlayerVerifying" })[String(state || "")] || "map.treasurePlayerUnknown";
}
export function mapRewardName(item, texts) { return lookupMapText(texts, item.nameKey, item.name || item.key); }
export function mapRewardCount(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return "-";
  const absolute = Math.abs(number), sign = number < 0 ? "-" : "";
  return absolute >= 1e9 ? `${sign}${(absolute / 1e9).toFixed(1)}G` : absolute >= 1e6 ? `${sign}${(absolute / 1e6).toFixed(1)}M`
    : absolute >= 1e3 ? `${sign}${(absolute / 1e3).toFixed(1)}K` : String(number);
}

// MapDataPanel nt, UTF-8 byte 9594; binding names expanded for readability.
export function buildMapColumns(kind, t, language, gameTexts, itemKey, treasureStatesRefreshing) {
  let coordinates = {
      label: isLiveTarget(kind) ? t(`map.liveTarget`) : t(`map.coordinates`),
      width: `190px`,
      className: `map-column-coordinate`,
      value: mapCoordinates,
      coordinate: !0
    },
    updated = {
      label: t(`map.updatedAt`),
      width: `150px`,
      sortBy: `updatedAt`,
      value: e => mapUpdated(e.updatedAt, language)
    };
  return kind === `city` ? [{
    label: t(`map.marked`),
    width: `48px`,
    className: `map-column-mark`,
    mark: !0
  }, coordinates, {
    label: t(`map.player`),
    width: `minmax(130px, 1fr)`,
    value: e => String(field(e, `ownerName`) || `-`)
  }, {
    label: t(`map.alliance`),
    width: `minmax(110px, 1fr)`,
    value: e => String(field(e, `allianceName`) || `-`)
  }, {
    label: t(`map.level`),
    width: `70px`,
    sortBy: `level`,
    value: e => mapNumber(field(e, `level`), language)
  }, {
    label: `HP`,
    width: `90px`,
    sortBy: `health`,
    value: e => mapNumber(field(e, `health`), language)
  }, {
    label: t(`automation.shieldEnds`),
    width: `150px`,
    sortBy: `shield`,
    value: e => mapShield(field(e, `protectEndTime`) || field(e, `shieldEndTime`), language)
  }, updated] : kind === `resource` ? [coordinates, {
    label: t(`map.resource`),
    width: `minmax(150px, 1fr)`,
    value: e => lookupMapText(gameTexts, field(e, `resourceNameKey`), t(`map.unknownResource`))
  }, {
    label: t(`map.level`),
    width: `70px`,
    sortBy: `level`,
    value: e => mapNumber(field(e, `level`), language)
  }, {
    label: t(`common.status`),
    width: `80px`,
    value: e => {
      let n = originalResourceOccupied(e);
      return lookupMapText(gameTexts, n ? `300039` : `372138`, t(n ? `map.resourceGathering` : `map.resourceIdle`));
    }
  }, updated] : kind === `monster` ? [coordinates, {
    label: t(`common.name`),
    width: `minmax(150px, 1fr)`,
    value: e => lookupMapText(gameTexts, field(e, `monsterNameKey`), t(`map.unknownMonster`))
  }, {
    label: t(`map.level`),
    width: `70px`,
    sortBy: `level`,
    value: e => mapNumber(field(e, `level`), language)
  }, {
    label: t(`map.distance`),
    width: `90px`,
    sortBy: `distance`,
    value: e => mapNumber(field(e, `distanceFromHome`), language)
  }, updated] : kind === `truck` ? [{
    label: t(`map.selectTask`),
    width: `56px`,
    className: `map-task-select`,
    select: !0
  }, coordinates, {
    label: t(`map.playerAlliance`),
    width: `minmax(170px, 1fr)`,
    value: e => String(field(e, `ownerName`) || field(e, `allianceName`) || `-`)
  }, {
    label: t(`map.quality`),
    width: `70px`,
    sortBy: `quality`,
    value: e => field(e, `isSpecialURQuality`) === !0 ? t(`map.reindeerQuality`) : mapQuality(field(e, `quality`))
  }, {
    label: t(`map.escortPower`),
    width: `120px`,
    sortBy: `power`,
    value: e => mapNumber(field(e, `power`), language)
  }, {
    label: t(`map.retainedGoods`),
    width: `minmax(360px, 2fr)`,
    sortBy: itemKey ? `itemCount` : void 0,
    rewards: `currentGoods`
  }, {
    label: t(`map.plunderStatus`),
    width: `190px`,
    sortBy: `remainingLootCount`,
    value: e => {
      let n = e,
        i = truckMaxLootCount(n),
        a = Math.max(0, Number(n.robTimes) || 0),
        o = truckState(n, Date.now());
      return o === `full` ? `${t(`map.truckPlunderFull`)} ${a}/${i}` : o === `protected` ? `${t(`map.truckProtected`)} ${mapDuration(Number(n.protectTime) - Date.now())}｜${t(`map.truckRobbedCount`, {
        count: a,
        max: i
      })}` : `${t(`map.truckReady`)}｜${t(`map.truckRobbedCount`, {
        count: a,
        max: i
      })}`;
    }
  }, {
    label: t(`map.arrivalTime`),
    width: `150px`,
    sortBy: `arriveTime`,
    value: e => mapDate(field(e, `arriveTs`), language)
  }, updated] : kind === `railway` ? [coordinates, {
    label: t(`map.alliance`),
    width: `minmax(160px, 1fr)`,
    value: e => String(field(e, `allianceName`) || field(e, `allianceAbbr`) || `-`)
  }, {
    label: t(`map.quality`),
    width: `70px`,
    sortBy: `quality`,
    value: e => mapQuality(field(e, `quality`))
  }, {
    label: t(`map.power`),
    width: `120px`,
    sortBy: `power`,
    value: e => mapNumber(field(e, `power`), language)
  }, {
    label: t(`map.retainedGoods`),
    width: `minmax(360px, 2fr)`,
    sortBy: itemKey ? `itemCount` : void 0,
    rewards: `currentGoods`
  }, {
    label: t(`map.protectionTime`),
    width: `150px`,
    sortBy: `protectTime`,
    value: e => mapDate(field(e, `protectTime`), language)
  }, updated] : kind === `treasure` ? [coordinates, {
    label: t(`map.treasureType`),
    width: `160px`,
    value: e => treasureName(t, gameTexts, field(e, `treasureType`), field(e, `suppliesType`), field(e, `treasureNameKey`))
  }, {
    label: t(`map.remainingBoxes`),
    width: `90px`,
    value: e => mapNumber(field(e, `remainingBoxes`), language)
  }, {
    label: t(`map.treasureWorldState`),
    width: `110px`,
    value: e => {
      let n = field(e, `worldClaimState`) || (treasureStatesRefreshing ? `verifying` : `unknown`),
        r = Number(field(e, `chargePercent`));
      return n === `charging` && Number.isFinite(r) ? `${t(treasureWorldKey(n))} ${Math.round(r * 100)}%` : t(treasureWorldKey(n));
    }
  }, {
    label: t(`map.treasurePlayerState`),
    width: `110px`,
    value: e => t(treasurePlayerKey(field(e, `playerClaimState`) || (treasureStatesRefreshing ? `verifying` : `unknown`), field(e, `claimBlockReason`)))
  }, {
    label: t(`map.claimedCount`),
    width: `90px`,
    value: e => mapNumber(field(e, `rewardedCount`), language)
  }, {
    label: t(`map.diggingCount`),
    width: `90px`,
    value: e => mapNumber(field(e, `diggingCount`), language)
  }, {
    label: t(`map.expireTime`),
    width: `150px`,
    value: e => mapDate(Number(field(e, `expireTime`)), language)
  }, {
    label: t(`map.owner`),
    width: `minmax(140px, 1fr)`,
    value: e => String(field(e, `ownerName`) || field(e, `ownerUid`) || `-`)
  }, {
    label: t(`map.alliance`),
    width: `minmax(110px, 1fr)`,
    value: e => String(field(e, `allianceAbbr`) || field(e, `allianceId`) || `-`)
  }, {
    label: t(`map.actions`),
    width: `90px`,
    action: !0
  }, updated] : [...(kind === `dispatch` || kind === `ghost` ? [{
    label: t(`map.selectTask`),
    width: `56px`,
    className: `map-task-select`,
    select: !0
  }] : []), coordinates, {
    label: t(`map.owner`),
    width: `minmax(150px, 1fr)`,
    value: e => String(field(e, `ownerName`) || field(e, `ownerUid`) || `-`)
  }, {
    label: t(`map.level`),
    width: `70px`,
    sortBy: `level`,
    value: e => mapNumber(field(e, `level`), language)
  }, {
    label: t(`map.quality`),
    width: `110px`,
    sortBy: `quality`,
    value: e => field(e, `isSpecial`) === !0 ? t(`map.specialQuality`) : mapQuality(field(e, `quality`))
  }, {
    label: t(`map.taskStatus`),
    width: `90px`,
    status: !0
  }, {
    label: t(`map.rewards`),
    width: `minmax(360px, 2fr)`,
    rewards: `rewards`
  }, {
    label: t(`map.completionTime`),
    width: `150px`,
    sortBy: `completionTime`,
    value: e => mapDate(field(e, `completionTime`), language)
  }, updated];
}
