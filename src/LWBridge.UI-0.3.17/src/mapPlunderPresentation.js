// Recovered Scheduled Plunder presentation helpers (pure; no native calls).
// Source of truth: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js
// (SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089); exact locators and the
// differential check are under evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/scheduled/.
// Original binding names are noted next to each helper. Timing/state helpers that are already the accepted
// original equivalents are reused from mapTablePresentation.js (mapTaskState = F, mapTimestamp = P,
// mapDuration = Je, truckMaxLootCount = Fe, truckState = Ie).

import { mapDuration, mapTaskState } from "./mapTablePresentation.js";

// Original table `b` (MapDataPanel, ordered; first substring match wins).
const DISPATCH_PLUNDER_ERROR_TABLE = Object.freeze([
  ["invalid dispatch plunder target", "DISPATCH_PLUNDER_INVALID_TARGET"],
  ["invalid scheduled target", "DISPATCH_PLUNDER_INVALID_TARGET"],
  ["dispatch plunder request already pending", "DISPATCH_PLUNDER_REQUEST_PENDING"],
  ["dispatch manager unavailable", "DISPATCH_PLUNDER_MANAGER_UNAVAILABLE"],
  ["dispatch steal limit reached", "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED"],
  ["cross-server dispatch steal unavailable", "DISPATCH_PLUNDER_CROSS_SERVER_UNAVAILABLE"],
  ["server response timeout", "DISPATCH_PLUNDER_RESPONSE_TIMEOUT"],
  ["game disconnected", "DISPATCH_PLUNDER_GAME_DISCONNECTED"],
  ["dispatch_des040", "DISPATCH_PLUNDER_TASK_COMPLETED"],
  ["dispatch_des043", "DISPATCH_PLUNDER_TASK_DISAPPEARED"],
  ["task expired", "DISPATCH_PLUNDER_TASK_EXPIRED"],
  ["client restarted", "DISPATCH_PLUNDER_CLIENT_RESTARTED"],
  ["invalid map plunder schedule", "DISPATCH_PLUNDER_INVALID_SCHEDULE"],
  ["map plunder schedule already armed", "DISPATCH_PLUNDER_ALREADY_ARMED"],
].map(Object.freeze));

// Original table `Te` (a plain object literal on purpose: the original looks keys up with `Te[text.toLowerCase()]`
// on an ordinary object, so inherited property names behave exactly as they do in the original).
const TRUCK_PLUNDER_ERROR_TABLE = {
  457567: "TRUCK_PLUNDER_FULLY_LOOTED",
  457589: "TRUCK_PLUNDER_DAILY_LIMIT_REACHED",
  458632: "TRUCK_PLUNDER_CROSS_ZONE_SEGMENT_MISMATCH",
  season_mastery_s3_tips_12: "TRUCK_PLUNDER_ALLIED_TARGET",
  truck_tips10008: "TRUCK_PLUNDER_REINDEER_ALREADY_LOOTED",
  trade_person_tips1013: "TRUCK_PLUNDER_ATTACK_LIMIT_REACHED",
  "truck expired": "TRUCK_PLUNDER_TARGET_EXPIRED",
  "game disconnected": "TRUCK_PLUNDER_GAME_DISCONNECTED",
  "client restarted": "TRUCK_PLUNDER_CLIENT_RESTARTED",
  "server response timeout": "TRUCK_PLUNDER_RESPONSE_TIMEOUT",
};

export const DISPATCH_PLUNDER_ERROR_CODES = Object.freeze([...new Set(DISPATCH_PLUNDER_ERROR_TABLE.map(([, code]) => code)),
  "DISPATCH_PLUNDER_SERVER_REJECTED", "DISPATCH_PLUNDER_UNKNOWN"]);
export const TRUCK_PLUNDER_ERROR_CODES = Object.freeze([...new Set(Object.values(TRUCK_PLUNDER_ERROR_TABLE)), "TRUCK_PLUNDER_SERVER_REJECTED"]);

// Original `ee`-style statuses used by the Clear/Cancel/Plunder Again predicates.
export const PLUNDER_TERMINAL_STATUSES = Object.freeze(["succeeded", "failed", "cancelled", "expired"]);
export const PLUNDER_INFLIGHT_STATUSES = Object.freeze(["scheduled", "waiting_connection", "running"]);

// Original `x` (dispatch) and `S` (truck): identical bodies.
export function plunderErrorMessage(error) {
  if (error instanceof Error) return error.message;
  if (error && typeof error === "object") return [error.code, error.message].filter((part) => typeof part === "string").join(": ");
  return String(error ?? "");
}

// Original `Ce`.
export function compactPlunderErrorText(value) {
  const text = value.replace(/\s+/g, " ").trim();
  return text.length > 60 ? `${text.slice(0, 57)}...` : text;
}

// Original `we(t, error)`.
export function translateDispatchPlunderError(t, error) {
  const text = compactPlunderErrorText(plunderErrorMessage(error));
  const explicit = text.match(/DISPATCH_PLUNDER_[A-Z_]+/)?.[0];
  let code = explicit;
  if (!code) {
    const lowered = text.toLowerCase();
    code = DISPATCH_PLUNDER_ERROR_TABLE.find(([needle]) => lowered.includes(needle))?.[1];
  }
  if (!code && /^\d+$/.test(text)) code = "DISPATCH_PLUNDER_SERVER_REJECTED";
  code ||= "DISPATCH_PLUNDER_UNKNOWN";
  const remainder = explicit ? text.slice(text.indexOf(explicit) + explicit.length).replace(/^\s*[:：-]\s*/, "") : "";
  const detail = compactPlunderErrorText(remainder || text) || code;
  const shown = code === "DISPATCH_PLUNDER_SERVER_REJECTED" ? compactPlunderErrorText(remainder || text) : detail;
  return shown === "E000000" ? t("error.PLUNDER_POSSIBLE_DAILY_LIMIT_REACHED") : t(`error.${code}`, { detail, code: shown });
}

// Original `C(t, error)`.
export function translateTruckPlunderError(t, error) {
  const text = plunderErrorMessage(error).replace(/\s+/g, " ").trim();
  if (text === "E000000") return t("error.PLUNDER_POSSIBLE_DAILY_LIMIT_REACHED");
  const code = TRUCK_PLUNDER_ERROR_TABLE[text.toLowerCase()];
  return code ? t(`error.${code}`) : t("error.TRUCK_PLUNDER_SERVER_REJECTED", { detail: text || "-" });
}

// Group filters (original main component: `ln.filter(e => e.taskKind !== "ghost")` / `=== "ghost"`).
export function dispatchGroupJobs(jobs) { return jobs.filter((job) => job.taskKind !== "ghost"); }
export function ghostGroupJobs(jobs) { return jobs.filter((job) => job.taskKind === "ghost"); }
// Original tab count `ln.length + dn.length` (all dispatch+ghost rows plus all truck rows).
export function scheduledPlunderCount(dispatchJobs, truckJobs) { return dispatchJobs.length + truckJobs.length; }

// Original `${serverId}:${uuid}` row key; also the busyKey of a Dispatch/Ghost Cancel (`hr`).
export function plunderJobKey(job) { return `${job.serverId}:${job.uuid}`; }
// Original `truck:${serverId}:${uuid}` busyKey of a Truck Cancel / Plunder Again (`gr`, `_r`).
export function truckBusyKey(job) { return `truck:${plunderJobKey(job)}`; }
// Original React row key of a Truck row: `truck:${jobId || `${key}:${scheduledAt}`}`.
export function truckRowKey(job) { return `truck:${job.jobId || `${plunderJobKey(job)}:${job.scheduledAt}`}`; }

// Cancel is offered for scheduled / waiting_connection rows only (original `s3` in `ot`, `ie2` in `st`).
export function plunderCancelVisible(job) { return job.scheduleStatus === "scheduled" || job.scheduleStatus === "waiting_connection"; }
// Clear-history button: disabled while any busyKey is set or when no finished row exists.
export function plunderHasFinishedJobs(jobs) { return jobs.some((job) => PLUNDER_TERMINAL_STATUSES.includes(job.scheduleStatus)); }
export function plunderClearDisabled(jobs, busyKey) { return !!busyKey || !plunderHasFinishedJobs(jobs); }
// Original `ee2`: keys of Truck rows that are still scheduled/waiting/running (blocks a duplicate Plunder Again).
export function truckInflightKeys(jobs) {
  return new Set(jobs.filter((job) => PLUNDER_INFLIGHT_STATUSES.includes(job.scheduleStatus)).map(plunderJobKey));
}
// Original `ae2`. Intentionally returns the original short-circuit value (not coerced to boolean).
export function truckPlunderAgainVisible(job, online, state, inflightKeys) {
  return online && job.scheduleStatus === "succeeded" && state !== "invalid" && state !== "full" && state !== "expired" && !inflightKeys.has(plunderJobKey(job));
}

// Original `f2` inside `ot`: [css status modifier, text] for a Dispatch/Ghost row.
export function secretPlunderStatus(job, { t, gameTexts, online, now }) {
  if (job.scheduleStatus === "succeeded") return ["succeeded", t("map.plunderSucceeded")];
  if (job.scheduleStatus === "failed") {
    return ["failed", job.lastError ? job.lastError !== "E000000" && gameTexts[job.lastError] || translateDispatchPlunderError(t, job.lastError) : t("map.plunderReasonUnavailable")];
  }
  if (job.scheduleStatus === "cancelled") return ["cancelled", t("map.plunderCancelled")];
  if (job.scheduleStatus === "expired") return ["expired", t("map.taskExpired")];
  if (job.scheduleStatus === "running") return ["running", t("map.plunderRunning")];
  if (!online || job.scheduleStatus === "waiting_connection") return ["waiting", t("map.waitingConnection")];
  const state = mapTaskState(job, now);
  return state === "ready" ? ["ready", t("map.taskReady")] : state === "protected" ? ["protected", t("map.taskProtected")] : ["pending", t("map.taskPending")];
}

// Original `re2` inside `st` (with `ne2` = lastError text): the Truck "Result" text. `state` = truckState(job, now).
export function truckPlunderResult(job, { t, online, now, state }) {
  const errorText = job.lastError ? translateTruckPlunderError(t, job.lastError) : t("map.plunderReasonUnavailable");
  return job.scheduleStatus === "succeeded" && job.battleWon === true ? t("map.plunderWon")
    : job.scheduleStatus === "succeeded" && job.battleWon === false ? t("map.plunderLost")
      : job.scheduleStatus === "succeeded" ? "-"
        : job.scheduleStatus === "failed" ? errorText
          : job.scheduleStatus === "cancelled" ? t("map.plunderCancelled")
            : job.scheduleStatus === "expired" ? errorText
              : job.scheduleStatus === "running" ? t("map.plunderRunning")
                : !online || job.scheduleStatus === "waiting_connection" ? t("map.waitingConnection")
                  : state === "expired" ? t("map.taskExpired")
                    : state === "protected" ? `${t("map.truckProtected")} ${mapDuration(Number(job.protectTime) - now)}`
                      : t(state === "full" ? "map.truckPlunderFull" : "map.truckReady");
}
