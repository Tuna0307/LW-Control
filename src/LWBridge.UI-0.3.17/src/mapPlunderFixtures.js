// Deterministic OFFLINE presentation fixtures for the recovered Map "Scheduled Plunder" tab.
//
// Every row below is SYNTHETIC preview data (names/ids/rewards are labelled "Synthetic"). The rows are
// display input only: nothing here schedules, cancels, shares or claims anything, no durable job is ever
// created, and a row showing "Succeeded"/"Victory" etc. is only a rendering of one recovered branch, not the
// result of any game operation. All timestamps are derived from the `now` argument, so the same (state, now)
// always yields identical rows.
//
// `online` is an explicit preview PRESENTATION input that selects the original `online` prop of the Scheduled
// Plunder groups. The preview provider itself stays online:false; the page decides how to feed this value.
// `busyKey` is the original busyKey prop (a Cancel/Plunder Again in-flight key or a `clear:<kind>` / `schedule`
// marker); it only drives the recovered disabled predicates.
//
// Only fields the original `ot`/`st` components (or their helpers) read are used:
//   Dispatch/Ghost: serverId, uuid, ownerName, ownerUid, isSpecial, quality, rewards[{key,name,nameKey,count}],
//     completionTime, plunderAt, taskExpireTime, stolenCount, maxStealCount, scheduleStatus, lastError, taskKind.
//   Truck: serverId, uuid, jobId, scheduledAt, ownerName, allianceName, isSpecialURQuality, quality, maxLootCount,
//     robTimes, protectTime, arriveTs, scheduleStatus, lastError, battleWon, plunderRewards[...].
// Recovered game-text keys used as lastError/nameKey values are in `plunderFixtureGameTexts`.

export const PLUNDER_FIXTURE_STATES = Object.freeze(["map-scheduled", "map-scheduled-populated", "map-scheduled-conditional"]);

export const plunderFixtureGameTexts = Object.freeze({
  dispatch_des041: "Synthetic mapped game text for dispatch_des041",
  ghostrecon_901: "Synthetic mapped game text for ghostrecon_901",
  "synthetic-reward-key": "Synthetic mapped reward name",
});

const SERVER = 9001;
const TRUCK_SERVER = 9002;

function reward(index, count, extra = {}) {
  return { key: `synthetic-reward-${index}`, name: `Synthetic reward ${index}`, count, ...extra };
}

function buildPopulated(now) {
  const ms = (offset) => now + offset;
  const seconds = (offset) => Math.floor((now + offset) / 1000);
  const secret = (id, fields) => ({ serverId: SERVER, uuid: `9001000${id}`, ownerName: `Synthetic owner ${id}`, quality: 4, taskKind: "dispatch", stolenCount: 0, maxStealCount: 3, ...fields });
  const truck = (id, fields) => ({ serverId: TRUCK_SERVER, uuid: `9002000${id}`, jobId: `synthetic-job-${id}`, scheduledAt: ms(-3_600_000), ownerName: `Synthetic escort ${id}`, quality: 4, maxLootCount: 3, robTimes: 0, protectTime: ms(-60_000), arriveTs: ms(7_200_000), ...fields });
  const dispatchJobs = [
    // Dispatch group: one scheduled row per recovered timing state, online (Pending / Protected / Ready).
    secret("01", { scheduleStatus: "scheduled", completionTime: ms(600_000), plunderAt: ms(900_000), taskExpireTime: ms(3_600_000), rewards: [reward(1, 1250), reward(2, 2_500_000, { nameKey: "synthetic-reward-key" })] }),
    secret("02", { scheduleStatus: "scheduled", completionTime: seconds(-60_000), plunderAt: seconds(120_000), taskExpireTime: seconds(3_600_000), quality: 3, rewards: [reward(1, 999)] }),
    secret("03", { scheduleStatus: "scheduled", completionTime: ms(-300_000), plunderAt: ms(-60_000), taskExpireTime: ms(3_600_000), isSpecial: true, rewards: [reward(3, 1_000_000_000)] }),
    // Scheduled rows whose timing state is "expired" / "full": the original falls through to the Pending label.
    secret("04", { scheduleStatus: "scheduled", completionTime: ms(-300_000), plunderAt: ms(-60_000), taskExpireTime: ms(-1), rewards: [] }),
    secret("05", { scheduleStatus: "scheduled", completionTime: ms(-300_000), plunderAt: ms(-60_000), taskExpireTime: ms(3_600_000), stolenCount: 3, maxStealCount: 3 }),
    secret("06", { scheduleStatus: "waiting_connection", completionTime: ms(60_000), plunderAt: ms(120_000), rewards: [reward(1, 10)] }),
    secret("07", { scheduleStatus: "running", completionTime: ms(-120_000), plunderAt: ms(-30_000), rewards: [reward(2, 5)] }),
    secret("08", { scheduleStatus: "succeeded", completionTime: ms(-900_000), plunderAt: ms(-600_000), rewards: [reward(1, 1250), reward(2, 75)] }),
    secret("09", { scheduleStatus: "failed", completionTime: ms(-900_000), plunderAt: ms(-600_000), lastError: "dispatch_des041" }),
    secret("10", { scheduleStatus: "cancelled", completionTime: ms(-900_000), plunderAt: ms(-600_000) }),
    secret("11", { scheduleStatus: "expired", completionTime: ms(-900_000), plunderAt: ms(-600_000), taskExpireTime: ms(-300_000) }),
    // Ghost Scout group.
    secret("21", { taskKind: "ghost", scheduleStatus: "scheduled", completionTime: ms(-300_000), plunderAt: ms(-60_000), taskExpireTime: ms(3_600_000), rewards: [reward(4, 3)] }),
    secret("22", { taskKind: "ghost", scheduleStatus: "scheduled", completionTime: ms(300_000), plunderAt: ms(600_000), taskExpireTime: ms(3_600_000) }),
    secret("23", { taskKind: "ghost", scheduleStatus: "succeeded", completionTime: ms(-900_000), plunderAt: ms(-600_000), rewards: [reward(4, 1500)] }),
    secret("24", { taskKind: "ghost", scheduleStatus: "failed", completionTime: ms(-900_000), plunderAt: ms(-600_000), lastError: "DISPATCH_PLUNDER_GAME_DISCONNECTED" }),
  ];
  const truckJobs = [
    truck("01", { scheduleStatus: "scheduled" }),
    truck("02", { scheduleStatus: "scheduled", protectTime: ms(3_723_000), robTimes: 1 }),
    truck("03", { scheduleStatus: "waiting_connection" }),
    truck("04", { scheduleStatus: "running", robTimes: 2 }),
    // Succeeded rows: Plunder Again is offered only while online and the truck is still plunderable.
    truck("05", { scheduleStatus: "succeeded", battleWon: true, robTimes: 1, plunderRewards: [reward(1, 4200), reward(2, 15, { nameKey: "synthetic-reward-key" })] }),
    truck("06", { scheduleStatus: "succeeded", battleWon: false, robTimes: 1 }),
    truck("07", { scheduleStatus: "succeeded", robTimes: 1 }),
    truck("08", { scheduleStatus: "succeeded", battleWon: true, robTimes: 3, plunderRewards: [reward(3, 900)] }),
    truck("09", { scheduleStatus: "succeeded", battleWon: true, robTimes: 1, arriveTs: ms(-1) }),
    // Invalid truck row (blank uuid) never offers Plunder Again.
    truck("14", { scheduleStatus: "succeeded", battleWon: false, uuid: "   " }),
    // Duplicate in-flight pair: the succeeded row below hides Plunder Again because the next row is still scheduled.
    truck("10", { scheduleStatus: "succeeded", battleWon: true, robTimes: 1, plunderRewards: [reward(2, 12)] }),
    truck("10", { scheduleStatus: "scheduled", jobId: "synthetic-job-10b", robTimes: 1 }),
    truck("11", { scheduleStatus: "failed", lastError: "457567" }),
    truck("12", { scheduleStatus: "cancelled" }),
    truck("13", { scheduleStatus: "expired", lastError: "truck expired" }),
  ];
  return { dispatchJobs, truckJobs };
}

function buildConditional(now) {
  const ms = (offset) => now + offset;
  const seconds = (offset) => Math.floor((now + offset) / 1000);
  const secret = (id, fields) => ({ serverId: SERVER, uuid: `9001100${id}`, ownerName: `Synthetic owner ${id}`, quality: 4, taskKind: "dispatch", stolenCount: 0, maxStealCount: 3, completionTime: ms(-900_000), plunderAt: ms(-600_000), ...fields });
  const truck = (id, fields) => ({ serverId: TRUCK_SERVER, uuid: `9002100${id}`, jobId: `synthetic-job-c${id}`, scheduledAt: ms(-3_600_000), ownerName: `Synthetic escort ${id}`, quality: 4, maxLootCount: 3, robTimes: 0, protectTime: ms(-60_000), arriveTs: ms(7_200_000), ...fields });
  const dispatchJobs = [
    // Failed-row error text branches (Dispatch/Ghost use game text -> DISPATCH_PLUNDER_* -> table -> numeric -> unknown).
    secret("01", { scheduleStatus: "failed", lastError: "E000000" }),
    secret("02", { scheduleStatus: "failed", lastError: "dispatch_des041" }),
    secret("03", { scheduleStatus: "failed", lastError: "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED" }),
    secret("04", { scheduleStatus: "failed", lastError: "DISPATCH_PLUNDER_SEND_FAILED: synthetic detail text" }),
    secret("05", { scheduleStatus: "failed", lastError: "dispatch plunder request already pending" }),
    secret("06", { scheduleStatus: "failed", lastError: "457567" }),
    secret("07", { scheduleStatus: "failed", lastError: "synthetic unrecognised server text that is intentionally longer than sixty characters in total" }),
    secret("08", { scheduleStatus: "failed" }),
    secret("09", { scheduleStatus: "failed", lastError: "dispatch_des040" }),
    // Status labels independent of timing.
    secret("10", { scheduleStatus: "succeeded", isSpecial: true }),
    secret("11", { scheduleStatus: "cancelled", quality: 0 }),
    secret("12", { scheduleStatus: "expired", quality: 6 }),
    secret("13", { scheduleStatus: "running" }),
    // Scheduled / waiting rows: offline (online:false) shows "Waiting for connection"; Cancel stays available.
    secret("14", { scheduleStatus: "scheduled", completionTime: ms(60_000), plunderAt: ms(120_000), taskExpireTime: ms(3_600_000) }),
    secret("15", { scheduleStatus: "waiting_connection" }),
    // Unknown / other scheduleStatus value: original falls through to timing state (offline: waiting) with no Cancel.
    secret("16", { scheduleStatus: "synthetic-unknown-status" }),
    // Owner fallbacks: ownerName -> ownerUid -> uuid.
    secret("17", { scheduleStatus: "succeeded", ownerName: "", ownerUid: "synthetic-uid-17" }),
    secret("18", { scheduleStatus: "succeeded", ownerName: "", ownerUid: "" }),
    // Invalid timestamps render "-": null / negative / NaN-like / zero.
    secret("19", { scheduleStatus: "scheduled", completionTime: null, plunderAt: -5 }),
    secret("20", { scheduleStatus: "waiting_connection", completionTime: "not-a-number", plunderAt: 0 }),
    secret("21", { scheduleStatus: "scheduled", completionTime: seconds(60_000), plunderAt: seconds(120_000) }),
    // Rewards present with missing counts / empty list / no field.
    secret("22", { scheduleStatus: "succeeded", rewards: [reward(1, 1234), reward(2, undefined), reward(3, -1500), reward(4, 2_999_999)] }),
    secret("23", { scheduleStatus: "succeeded", rewards: [] }),
    // Ghost rows.
    secret("31", { taskKind: "ghost", scheduleStatus: "failed", lastError: "ghostrecon_901" }),
    secret("32", { taskKind: "ghost", scheduleStatus: "waiting_connection", isSpecial: true }),
    secret("33", { taskKind: "ghost", scheduleStatus: "failed", lastError: "server response timeout" }),
    secret("34", { taskKind: "ghost", scheduleStatus: "cancelled" }),
  ];
  const truckJobs = [
    // Result text branches for truck rows (E000000 / table / allied / numeric rejection / unknown / missing).
    truck("01", { scheduleStatus: "failed", lastError: "E000000" }),
    truck("02", { scheduleStatus: "failed", lastError: "457589" }),
    truck("03", { scheduleStatus: "failed", lastError: "season_mastery_s3_tips_12" }),
    truck("04", { scheduleStatus: "failed", lastError: "Trade_Person_Tips1013" }),
    truck("05", { scheduleStatus: "failed", lastError: "999999" }),
    truck("06", { scheduleStatus: "failed", lastError: "synthetic   unrecognised\ttruck text" }),
    truck("07", { scheduleStatus: "failed" }),
    truck("08", { scheduleStatus: "expired" }),
    truck("09", { scheduleStatus: "expired", lastError: "Game Disconnected" }),
    // Offline presentation: scheduled/unknown rows show "Waiting for connection" and never offer Plunder Again.
    truck("10", { scheduleStatus: "scheduled", robTimes: 1 }),
    truck("11", { scheduleStatus: "succeeded", battleWon: true, robTimes: 1, plunderRewards: [reward(1, 77)] }),
    truck("12", { scheduleStatus: "synthetic-unknown-status", robTimes: 1 }),
    // Truck state variants: reindeer (isSpecialURQuality forces max 1), invalid row, full, expired, negative / NaN counts.
    truck("13", { scheduleStatus: "scheduled", isSpecialURQuality: true, quality: 6, maxLootCount: 9, robTimes: 1 }),
    truck("14", { scheduleStatus: "scheduled", uuid: "   " }),
    truck("15", { scheduleStatus: "scheduled", serverId: "not-an-integer" }),
    truck("16", { scheduleStatus: "scheduled", robTimes: 3 }),
    truck("17", { scheduleStatus: "scheduled", arriveTs: ms(-1) }),
    truck("18", { scheduleStatus: "scheduled", robTimes: -3, maxLootCount: 0 }),
    truck("19", { scheduleStatus: "scheduled", robTimes: "abc", maxLootCount: "xyz", quality: null }),
    truck("20", { scheduleStatus: "scheduled", ownerName: "", allianceName: "Synthetic alliance 20" }),
    truck("21", { scheduleStatus: "scheduled", ownerName: "", allianceName: "" }),
    truck("22", { scheduleStatus: "scheduled", protectTime: ms(5_000), arriveTs: 0 }),
    truck("23", { scheduleStatus: "succeeded", battleWon: true, plunderRewards: [] }),
  ];
  return { dispatchJobs, truckJobs };
}

// Returns { dispatchJobs, truckJobs, online, busyKey } for a recovered preview state name, or null.
export function plunderFixtureFor(previewState, now) {
  if (!Number.isFinite(now)) throw new TypeError("plunderFixtureFor requires a finite `now` timestamp");
  if (previewState === "map-scheduled") return { dispatchJobs: [], truckJobs: [], online: false, busyKey: "" };
  if (previewState === "map-scheduled-populated") return { ...buildPopulated(now), online: true, busyKey: "" };
  if (previewState === "map-scheduled-conditional") {
    // Offline presentation with one Dispatch Cancel marked in-flight (original busyKey `${serverId}:${uuid}`).
    return { ...buildConditional(now), online: false, busyKey: `${SERVER}:900110014` };
  }
  return null;
}
