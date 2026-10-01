import { useEffect, useMemo, useRef, useState } from "react";
import { MapDataPage } from "./MapDataPage.jsx";
import { getMapPreviewProvider } from "./mapPreviewApi.js";
import { useI18n } from "./i18n.jsx";
import { usePreviewConfig, PreviewConfigError } from "./previewConfigHook.jsx";
import { normalizeJoinRestrictions, validJoinRestrictions, previewAfkTargets, makePreviewAfkProfile, previewAfkProfileValid, applyAfkTarget, previewAfkLevelOutOfRange } from "./previewAfkContracts.js";
import { initialAutomationDraft, automationDraftError, previewTrainingOrder, activateTraining } from "./previewAutomationContracts.js";
import { dispatchWeeklyQualities, previewAssistJobs, previewAssistTasks, previewAutomationRuntime, previewResourceGatherConfig, previewTradeFixture, railwayWeeklyQualities, validPreviewResourceGatherConfig } from "./previewAutomationFixtures.js";
import { initialAfkToolbarConfig, previewAllianceMembers, previewGarrisonBuildings, previewGarrisonRuntime, previewMemberFixture, previewZombieBusRuntime } from "./previewAfkFixtures.js";
import { buildTradePurchaseDays, resolveTradeName, tradePurchaseRowKey } from "./tradePurchaseHistory.js";

export { MapDataPage } from "./MapDataPage.jsx";

function Switch({ checked = false, disabled = false, label, onChange }) {
  return (
    <span
      className={`ui-switch${checked ? " is-on" : ""}`}
      aria-hidden="true"
    />
  );
}

function ToggleRow({ label, checked = false, disabled = false, onChange }) {
  const { t } = useI18n();
  return (
    <button
      type="button"
      className="toggle-row"
      role="switch"
      aria-checked={checked}
      aria-label={`${label}: ${t(checked ? "common.enabled" : "common.disabled")}`}
      disabled={disabled}
      onClick={() => onChange?.(!checked)}
    >
      <span>{label}</span>
      <Switch checked={checked} />
    </button>
  );
}

function PanelTitle({ title, subtitle }) {
  return (
    <div className="panel-title">
      <h2>{title}</h2>
      {subtitle ? <span className="muted">{subtitle}</span> : null}
    </div>
  );
}

const RECOVERY_ACTIVE_STATES = new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]);

function previewHomeState(name) {
  const base = {
    rootResolved: true,
    gameRootStatus: { valid: true, root: "C:\\Games\\Last War-Survival Game" },
    proxyStatus: { gameRunning: false, repairRequired: false },
    online: false,
    gameRecoveryStatus: { state: "idle" },
    autoLaunchGame: false,
    autoReconnect: false,
    busy: "",
    proxyBusy: false,
    gameLaunchBusy: false,
    gameRootError: "",
    gameActionError: "",
    production: false,
  };
  switch (name) {
    case "home-checking": return { ...base, rootResolved: false, gameRootStatus: null, proxyStatus: null };
    case "home-missing": return { ...base, gameRootStatus: { valid: false, root: "" } };
    case "home-launching": return { ...base, gameLaunchBusy: true };
    case "home-proxy-busy-stopped": return { ...base, proxyBusy: true };
    case "home-proxy-busy-running": return { ...base, proxyBusy: true, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-proxy-busy-repair": return { ...base, proxyBusy: true, proxyStatus: { gameRunning: true, repairRequired: true } };
    case "home-busy-overlap": return { ...base, proxyBusy: true, gameLaunchBusy: true, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-root-busy-missing": return { ...base, busy: "gameRoot", gameRootStatus: { valid: false, root: "" } };
    case "home-root-busy-valid": return { ...base, busy: "gameRoot" };
    case "home-running-disconnected": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false } };
    case "home-connected": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, online: true, autoLaunchGame: true, autoReconnect: true };
    case "home-repair": return { ...base, proxyStatus: { gameRunning: true, repairRequired: true } };
    case "home-recovery-waiting": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "waiting" } };
    case "home-recovery-updating": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "updating" } };
    case "home-recovery-repairing": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "repairing" } };
    case "home-recovery-launching": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "launching" } };
    case "home-recovery-verifying": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "verifying" } };
    case "home-recovery-maintenance": return { ...base, proxyStatus: { gameRunning: true, repairRequired: false }, gameRecoveryStatus: { state: "maintenance" } };
    case "home-recovery-failed": return { ...base, gameRecoveryStatus: { state: "failed", error: "GAME_RECOVERY_FAILED" } };
    case "home-error-unknown": return { ...base, gameActionError: "HOME_ERROR001_UNKNOWN_QA" };
    case "home-error-embedded": return { ...base, gameActionError: "QA fixture: GAME_XLUA_ABI_UNSUPPORTED while preparing startup" };
    case "home-recovery-error-unknown": return { ...base, gameRecoveryStatus: { state: "failed", error: "HOME_ERROR001_UNKNOWN_QA" } };
    case "home-errors-both": return { ...base, gameRootStatus: { valid: false, root: "" }, gameRootError: "INVALID_GAME_ROOT", gameActionError: "GAME_XLUA_ABI_UNSUPPORTED" };
    case "home-error-root": return { ...base, gameRootStatus: { valid: false, root: "" }, gameRootError: "INVALID_GAME_ROOT" };
    case "home-error-action-missing-root": return { ...base, gameRootStatus: { valid: false, root: "" }, gameActionError: "GAME_XLUA_ABI_UNSUPPORTED" };
    default: return null;
  }
}

function translatedError(t, value) {
  const codes = [];
  if (value && typeof value === "object" && "code" in value && typeof value.code === "string") codes.push(value.code);
  const message = value instanceof Error ? value.message : String(value ?? "");
  codes.push(...message.match(/\b[A-Z][A-Z0-9_]{2,}\b/g) || []);
  for (const code of [...new Set(codes)].reverse()) {
    for (const namespace of ["error", "auth.error", "update.error"]) {
      const key = `${namespace}.${code}`;
      const translated = t(key);
      if (translated !== key) return translated;
    }
  }
  return t("common.actionFailed");
}

export function HomePage({
  previewState = "",
  homeState,
  onAutoLaunchGameChange,
  onAutoReconnectChange,
  onGameRootSelect,
}) {
  const { t } = useI18n();
  const preview = previewHomeState(previewState);
  const state = preview || homeState || {};
  const rootResolved = state.rootResolved === true;
  const rootValid = state.gameRootStatus?.valid === true;
  const gameRunning = state.proxyStatus?.gameRunning === true;
  const repairRequired = state.proxyStatus?.repairRequired === true;
  const recoveryState = state.gameRecoveryStatus?.state || "idle";
  const recovering = RECOVERY_ACTIVE_STATES.has(recoveryState);
  const launching = state.gameLaunchBusy === true;
  const proxyBusy = state.proxyBusy === true;
  const rootBusy = state.busy === "gameRoot";
  const showRootPicker = rootResolved && !rootValid;
  const repairOnClose = rootResolved && rootValid && gameRunning && repairRequired && !recovering;
  const lifecycleProviderAvailable = false;
  const canStart = lifecycleProviderAvailable && rootResolved && rootValid && !gameRunning && !recovering && !proxyBusy && !launching;
  const canStop = lifecycleProviderAvailable && rootResolved && rootValid && (gameRunning || recovering) && !proxyBusy && !launching;

  let status = t("setup.checking");
  if (rootResolved) {
    if (showRootPicker) status = t("setup.gameRootMissing");
    else if (launching) status = t("setup.launchingGame");
    else if (proxyBusy) status = t("common.processing");
    else if (recovering) status = t(`recovery.state.${recoveryState}`);
    else if (repairRequired) status = t("setup.repairRequired");
    else if (gameRunning) status = t(state.online ? "status.gameRunning" : "setup.bridgeDisconnected");
    else status = t("setup.gameStopped");
  }

  return (
    <section className="panel quick-actions-panel" data-preview-fixture={preview ? previewState : undefined}>
      <div className="panel-title">
        <h2>{t("setup.title")}</h2>
        <span className={gameRunning && state.online ? "status-ok" : "muted"}>{status}</span>
      </div>
      {rootResolved ? showRootPicker ? (
        <div className="game-root-missing">
          <span>{state.gameRootError ? translatedError(t, state.gameRootError) : t("setup.gameRootMissing")}</span>
          <button className="primary" type="button" disabled={rootBusy || !state.production} onClick={onGameRootSelect}>
            {t(rootBusy ? "common.processing" : "setup.gameRootSelect")}
          </button>
        </div>
      ) : (
        <div className="game-controls">
          {!repairOnClose ? (
            <button className="primary" type="button" disabled={!canStart}>{t(launching ? "setup.launchingGame" : proxyBusy ? "common.processing" : "top.launchGame")}</button>
          ) : null}
          <button type="button" disabled={!canStop}>
            {t(proxyBusy && gameRunning ? "common.processing" : repairOnClose ? "setup.updateAndLaunch" : "setup.closeGameAction")}
          </button>
          {repairOnClose ? <span className="muted">{t("setup.updateCloseGame")}</span> : null}
        </div>
      ) : null}
      {state.gameActionError ? <span className="game-root-error">{translatedError(t, state.gameActionError)}</span> : null}
      <ToggleRow
        label={t("auth.autoLaunchGame")}
        checked={state.autoLaunchGame === true}
        disabled={state.autoLaunchGame == null || state.busy === "autoLaunchGame" || !state.production}
        onChange={onAutoLaunchGameChange}
      />
      <ToggleRow
        label={t("automation.autoReconnect.title")}
        checked={state.autoReconnect === true}
        disabled={state.autoReconnect == null || state.busy === "autoReconnect" || !state.production}
        onChange={onAutoReconnectChange}
      />
      {recoveryState === "failed" && state.gameRecoveryStatus?.error ? (
        <span className="game-root-error">{t("recovery.failedDetail", { error: translatedError(t, state.gameRecoveryStatus.error) })}</span>
      ) : null}
    </section>
  );
}

const automationCategories = [
  ["daily", "Daily Tasks"],
  ["alliance", "Alliance"],
  ["resourceGather", "Resource gathering"],
  ["resources", "Resource Claims"],
  ["chat", "Chat"],
  ["trade", "Trade Station"],
  ["system", "Protection & System"],
];

const automationCategoryKeys = {
  daily: "automation.category.daily",
  alliance: "automation.category.alliance",
  resourceGather: "automation.category.resourceGather",
  resources: "automation.category.resources",
  chat: "automation.category.chat",
  trade: "automation.category.trade",
  system: "automation.category.system",
};

const automationCards = {
  daily: [
    ["Auto Training", "Train and promote in large batches, then finish the remaining troops."],
    ["Automatic Construction", "Upgrade the lowest-level building when the queue is idle."],
    ["Free Stamina", "Claim daily free Stamina when available."],
    ["Automatic Treatment", "Treat wounded soldiers, request Alliance help, and collect completed treatment every 2 seconds without using Gold."],
    ["Trucks", "Claim arrived Truck rewards, refresh to the target quality, and continue dispatching available Trucks."],
    ["Secret Task", "Claim completed rewards, then continue refreshing and dispatching eligible Secret Tasks."],
    ["Ghost Ops", "Start and share your Ghost Ops, join eligible alliance missions, and claim rewards on time."],
  ],
  alliance: [
    ["Alliance Tech Donations", "Automatically donate standard resources to Alliance Tech."],
    ["Automatic Official Application", "Automatically apply for one selected official position."],
    ["Automatic Alliance Train Boarding", "Select and rank target rewards, or let reward quantity override the ranking."],
    ["Alliance Help", "Help Alliance members automatically, with a check every minute."],
    ["Alliance Gifts", "Claim normal and advanced Alliance Gifts at the configured interval."],
    ["Excavation Stronghold Resources", "Claim resources from occupied excavation strongholds at the configured interval."],
    ["Alliance Center Resources", "Claim production resources from Alliance Center buildings at the configured interval."],
    ["Alliance Gathering Dispatch", "Automatically send selected idle squads to Alliance Gathering Points."],
  ],
  resources: [
    ["Building Resource Collection", "Collect resources produced by city buildings on a schedule."],
    ["Armed Truck", "Claim Armed Truck idle rewards on a schedule."],
  ],
  chat: [
    ["Red Packet", "Listen for chat messages and automatically claim Red Packet."],
    ["Fireworks / Egg", "Listen for chat messages and automatically claim Fireworks / Egg."],
    ["Treasure", "Listen for chat messages and automatically claim Treasure."],
  ],
  system: [
    ["Weekend Shield", "Maintain a Shield throughout Saturday server time."],
    ["Attack Shield", "Try 8, 12, and 24-hour Shields in order when attacked."],
  ],
};

const nonCollapsibleAutomationSettings = new Set([
  "Automatic Treatment",
  "Alliance Tech Donations",
  "Automatic Official Application",
  "Alliance Gifts",
  "Excavation Stronghold Resources",
  "Alliance Center Resources",
  "Building Resource Collection",
  "Armed Truck",
]);

const automationCardsWithoutSettings = new Set([
  "Free Stamina",
  "Alliance Help",
  "Weekend Shield",
  "Attack Shield",
]);

const automationActionLabels = {
  "Automatic Construction": "automation.claimAllBuildingRewards",
  Trucks: "automation.departAll",
  "Secret Task": "automation.dispatchAll",
  "Alliance Help": "common.runNow",
  "Alliance Gifts": "common.runNow",
  "Excavation Stronghold Resources": "common.runNow",
  "Alliance Center Resources": "common.runNow",
  "Red Packet": "common.runNow",
  "Fireworks / Egg": "common.runNow",
  Treasure: "common.runNow",
};

function WeeklyQualityPreview({ enabled, values, onChange, running = false }) {
  const { language, t } = useI18n();
  const days = Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat(language, { weekday: "short", timeZone: "UTC" }).format(new Date(Date.UTC(2024, 0, index + 1))));
  return <div className="automation-weekly-quality">{days.map((day, index) => <label key={day}><span>{day}</span><select value={values[index]} disabled={!enabled || running} onChange={(event) => { const next = [...values]; next[index] = event.target.value; onChange(next); }}><option value="none">{t("automation.noQualityRefresh")}</option><option value="ssr">{t("automation.ssrOrAbove")}</option><option value="ur">UR</option></select></label>)}</div>;
}

const previewConstructionBuildingTypes = [
  { itemId: 1101, category: "economy", name: "Fixture Steelworks" },
  { itemId: 1102, category: "economy", name: "Fixture Farm" },
  { itemId: 1201, category: "military", name: "Fixture Barracks" },
  { itemId: 1301, category: "decoration", name: "Fixture Monument" },
  { itemId: 1401, category: "season", name: "Fixture Season Hall" },
  { itemId: 1501, category: "other", name: "Fixture Headquarters" },
];

const previewSoldierCamps = [
  { uuid: "fixture-camp-1", level: 30, availableLevels: [1, 2, 3, 4, 5, 6, 7], reason: "train", productionCount: 0 },
  { uuid: "fixture-camp-2", level: 28, availableLevels: [1, 2, 3, 4, 5, 6], reason: "training", productionCount: 240, productionLevel: 6, completionTime: 1_799_000_900_000 },
];

const previewTrainRewards = [
  { key: "fixture-medal", name: "Fixture Medal", count: 8 },
  { key: "fixture-parts", name: "Fixture Parts", count: 5 },
  { key: "fixture-chest", name: "Fixture Chest", count: 2 },
];

function AutomationConfigPreview({ title, enabled, previewState = "", config }) {
  const { t } = useI18n();
  const error = automationDraftError(title, config.draft);
  return <div data-draft-dirty={config.dirty} data-draft-saving={config.saving} onBlur={() => config.store.flush().catch(() => {})}>
    <PreviewConfigError config={config} t={t} />
    <AutomationFields title={title} enabled={enabled} previewState={previewState} config={config} />
    {error && title !== "Automatic Construction" && title !== "Auto Training" ? <p className="automation-error" role="alert">{t(error)}</p> : null}
  </div>;
}

function previewTime(value, language) {
  return value ? new Date(value).toLocaleString(language) : "-";
}

function sourceAutomationState(runtimeState, enabled) {
  if (enabled === false || runtimeState === "disabled" || (enabled === undefined && runtimeState === "stopped")) return "common.disabled";
  if (runtimeState === "running" || runtimeState === "checking") return "automation.running";
  if (runtimeState === "success") return "common.success";
  if (runtimeState === "failed" || runtimeState === "error") return "common.failed";
  return "common.waiting";
}

function automationRuntimePresentation(title, runtime, enabled, configDraft, t, language) {
  const text = (value) => value == null || value === "" ? "-" : String(value);
  const yesNo = (value) => t(value ? "common.yes" : "common.no");
  const rows = (...entries) => entries.filter((entry) => entry && entry[1] != null);
  switch (title) {
    case "Automatic Construction":
      return { summary: rows(["automation.automaticBuilders", `${text(runtime.automaticBuilders)} / ${text(runtime.maxBuilders)}`], ["automation.buildersTotal", `${text(runtime.occupiedBuilders)} / ${text(runtime.totalBuilders)}`]), status: rows(["automation.currentBuilding", text(runtime.candidateName)], ["automation.currentLevel", text(runtime.candidateLevel)], ["automation.automaticBuilders", `${text(runtime.automaticBuilders)} / ${text(runtime.maxBuilders)}`], ["automation.buildersTotal", `${text(runtime.occupiedBuilders)} / ${text(runtime.totalBuilders)}`], ["automation.processed", text(runtime.processed)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Free Stamina":
      return { status: rows(["automation.claimedToday", `${text(runtime.todayCount)} / ${text(runtime.dailyLimit)}`], ["automation.processed", text(runtime.processed)], ["automation.nextClaim", previewTime(runtime.nextClaimAt, language)]) };
    case "Automatic Treatment":
      return { summary: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)]), status: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)], ["automation.treatmentBatches", text(runtime.batchesStarted)], ["automation.soldiersQueued", text(runtime.soldiersQueued)], ["automation.helpsRequested", text(runtime.helpsRequested)], ["automation.collected", text(runtime.collected)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Trucks":
      return { summary: rows(["automation.departed", text(runtime.departed)], ["automation.pendingClaims", text(runtime.pendingClaims)]), status: rows(["automation.nextRun", enabled ? previewTime(runtime.nextScheduledAt, language) : "-"], ["automation.qualifiedTotal", `${text(runtime.qualified)} / ${text(runtime.total)}`], ["automation.refreshed", text(runtime.refreshed)], ["automation.departed", text(runtime.departed)], ["automation.claimed", text(runtime.claimed)], ["automation.pendingClaims", text(runtime.pendingClaims)], ["automation.nextClaim", previewTime(runtime.nextContinuationAt, language)], ["automation.batchDeparture", t(runtime.batchDeparture ? "common.available" : "common.degraded")]) };
    case "Secret Task":
      return { summary: rows(["automation.dispatched", text(runtime.dispatched)], ["automation.pendingClaims", text(runtime.pendingClaims)]), status: rows(["automation.nextRun", (enabled || configDraft.collectRewards) ? previewTime(runtime.nextScheduledAt, language) : "-"], ["automation.qualifiedTotal", `${text(runtime.qualified)} / ${text(runtime.available)}`], ["automation.dispatched", text(runtime.dispatched)], ["automation.refreshed", text(runtime.refreshed)], ["automation.claimed", text(runtime.claimed)], ["automation.pendingClaims", text(runtime.pendingClaims)], ["automation.nextClaim", previewTime(runtime.nextContinuationAt, language)], ["automation.superRefresh", t(runtime.superRefresh ? "common.available" : "automation.normalRefresh")]) };
    case "Ghost Ops":
      return { status: rows(["automation.nextClaim", configDraft.ghostClaimRewards ? previewTime(runtime.nextClaimAt, language) : "-"]) };
    case "Alliance Tech Donations":
      return { summary: rows(["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)]), status: rows(["automation.currentTech", text(runtime.scienceId)], ["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Automatic Official Application":
      return { status: rows(["automation.targetPosition", text(runtime.positionId || configDraft.positionId)], ["automation.currentPosition", text(runtime.currentPositionId)], ["automation.applicationQueue", text(runtime.applyQueueLength)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Automatic Alliance Train Boarding":
      return { summary: rows(["automation.currentCarriage", text(runtime.currentCarriage)], ["automation.queueCapacity", `${text(runtime.queueLength)} / ${text(runtime.maxPassenger)}`]), status: rows(["automation.currentCarriage", text(runtime.currentCarriage)], ["automation.queueCapacity", `${text(runtime.queueLength)} / ${text(runtime.maxPassenger)}`], ["automation.currentReward", text(runtime.currentReward)], ["automation.trainVipStatus", yesNo(runtime.isVip)], ["automation.trainVipCarriages", runtime.vipCarriages?.join(", ") || "—"], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Alliance Help":
      return { status: rows(["automation.available", text(runtime.available)], ["automation.processed", text(runtime.processed)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gifts":
      return { status: rows(["automation.normalClaimed", text(runtime.normalClaimed)], ["automation.advancedClaimed", text(runtime.advancedClaimed)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewTime(runtime.nextRunAt, language) : "-"]) };
    case "Excavation Stronghold Resources":
    case "Alliance Center Resources":
      return { status: rows(["automation.available", text(runtime.available)], ["automation.claimed", text(runtime.claimed)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gathering Dispatch":
      return { status: rows(["automation.allianceGatherAvailable", text(runtime.available)], ["automation.dispatched", text(runtime.dispatched)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)]) };
    case "Building Resource Collection":
    case "Armed Truck":
      return { status: rows(["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewTime(runtime.nextRunAt, language) : "-"]) };
    case "Red Packet":
    case "Fireworks / Egg":
      return { summary: rows(["automation.pendingClaims", text(runtime.pendingCount)], ["automation.latestResult", t(runtime.lastResult === "success" ? "common.success" : runtime.lastResult === "failed" ? "common.failed" : "common.waiting")]) };
    case "Treasure":
      return { summary: rows(["automation.pendingClaims", text(runtime.pendingCount)], ["automation.treasureDispatchPending", text(runtime.dispatchPendingCount)]) };
    case "Weekend Shield":
      return { status: rows(["automation.currentShield", t(runtime.shielded ? "common.on" : "common.off")], ["automation.shieldEnds", previewTime(runtime.shieldEndAt, language)], ["automation.weekendWindow", yesNo(runtime.inWeekendWindow)], ["automation.weekendWindowLocal", runtime.weekendStartAt && runtime.weekendEndAt ? `${previewTime(runtime.weekendStartAt, language)} – ${previewTime(runtime.weekendEndAt, language)}` : "-"]) };
    case "Attack Shield":
      return { status: rows(["automation.currentShield", t(runtime.shielded ? "common.on" : "common.off")], ["automation.pendingReason", runtime.pendingReason || "-"]) };
    default:
      return {};
  }
}

function AutomationFields({ title, enabled, previewState, config }) {
  const { t, language } = useI18n();
  const fieldState = (key, initial) => [config.draft[key] ?? initial, (value) => config.store.edit((draft) => ({ ...draft, [key]: typeof value === "function" ? value(draft[key] ?? initial) : value }))];
  const field = (key, initial) => ({ value: config.draft[key] ?? initial, onChange: (event) => config.store.edit((draft) => ({ ...draft, [key]: event.target.value })) });
  const check = (key, initial = false) => ({ checked: config.draft[key] ?? initial, onChange: (event) => config.store.edit((draft) => ({ ...draft, [key]: event.target.checked })) });
  const [constructionTargetEnabled, setConstructionTargetEnabled] = fieldState("constructionTargetEnabled", false);
  const [constructionCategory, setConstructionCategory] = useState("all");
  const [constructionBuildingTypeIds, setConstructionBuildingTypeIds] = fieldState("constructionBuildingTypeIds", []);
  const [trainingTotalCount, setTrainingTotalCount] = fieldState("trainingTotalCount", 0);
  const [trainingTargetLevel, setTrainingTargetLevel] = fieldState("trainingTargetLevel", 0);
  const [trainingOpenFailed, setTrainingOpenFailed] = useState(previewState === "automation-training-open-failed");
  const [replyEnabled, setReplyEnabled] = fieldState("replyEnabled", false);
  const [treasureDispatchEnabled, setTreasureDispatchEnabled] = fieldState("treasureDispatchEnabled", false);
  const [dispatchAssistEnabled, setDispatchAssistEnabled] = fieldState("dispatchAssistEnabled", false);
  const [ghostJoinEnabled, setGhostJoinEnabled] = fieldState("ghostJoinEnabled", false);
  const [trainMode, setTrainMode] = fieldState("trainMode", "reward");
  const [vipTrainMode, setVipTrainMode] = fieldState("vipTrainMode", "reward");
  const [normalFixedCarriageIds, setNormalFixedCarriageIds] = fieldState("normalFixedCarriageIds", [1]);
  const [vipFixedCarriageIds, setVipFixedCarriageIds] = fieldState("vipFixedCarriageIds", [1, 2]);
  const [preferredRewardKeys, setPreferredRewardKeys] = fieldState("preferredRewardKeys", ["fixture-medal"]);
  const [preferRewardQuantity, setPreferRewardQuantity] = fieldState("preferRewardQuantity", false);
  const [thanksMode, setThanksMode] = fieldState("thanksMode", "like");
  const [draggedReward, setDraggedReward] = useState("");
  const [draggedGatherSquad, setDraggedGatherSquad] = useState(null);
  const [gatherSquadError, setGatherSquadError] = useState("");
  const [assistSelected, setAssistSelected] = useState([]);
  const [assistJobs, setAssistJobs] = useState(() => previewState === "automation-assist-schedule" ? previewAssistJobs : []);
  const assistTasks = previewState === "automation-assist-empty" ? [] : previewAssistTasks;
  const assistJobByTask = new Map(assistJobs.map((job) => [job.uuid, job]));
  const assistTitle = (task) => `${task.ownerName || task.ownerUid} · ${String(task.qualityKey).toUpperCase()}${task.isSpecial ? ` · ${t("automation.assistQuality.special")}` : ""}`;
  if (title === "Auto Training") {
    const camps = previewState === "automation-training-no-camps" ? [] : previewSoldierCamps;
    const availableLevels = [...new Set([...camps.flatMap((camp) => camp.availableLevels), ...(trainingTargetLevel > 0 ? [trainingTargetLevel] : [])])].sort((a, b) => a - b);
    const validQuantity = Number.isInteger(trainingTotalCount) && trainingTotalCount > 0 && trainingTotalCount <= 1000000;
    const order = previewTrainingOrder(previewState);
    return <>
      {previewState === "automation-training-error" ? <p role="alert">{t("automation.soldierTraining.stateError")}</p> : null}
      <div className="soldier-training-fields">
        <label><span>{t("automation.soldierTraining.totalCount")}</span><input type="number" min="1" max="1000000" step="1" value={trainingTotalCount || ""} aria-invalid={trainingTotalCount !== 0 && !validQuantity} disabled={!enabled || (config.draft.enabled && config.draft.trainEnabled)} onChange={(event) => config.store.edit((draft) => ({ ...draft, trainingTotalCount: Number(event.target.value), trainEnabled: false }))} /></label>
        <label><span>{t("automation.soldierTraining.target")}</span><select value={trainingTargetLevel} disabled={!enabled} onChange={(event) => setTrainingTargetLevel(Number(event.target.value))}><option value="0">{t("automation.soldierTraining.highest")}</option>{availableLevels.map((level) => { const available = camps.some((camp) => camp.availableLevels.includes(level)); return <option key={level} value={level} disabled={!available}>{t("automation.soldierTraining.level", { level })}</option>; })}</select></label>
      </div>
      <p className="muted">{t("automation.soldierTraining.quantityHint")}</p>
      {trainingTotalCount !== 0 && !validQuantity ? <p role="alert" className="automation-error">{t("automation.soldierTraining.quantityError")}</p> : null}
      {order ? <div role="status"><p>{t("automation.soldierTraining.progress", order)}</p><p className="muted">{t(`automation.soldierTraining.order.${order.reason}`)}</p></div> : null}
      <p className="muted">{t("automation.soldierTraining.summary", { trained: 420, promoted: 180, collected: 360 })}</p>
      {camps.length === 0 ? <p className="muted">{t("automation.soldierTraining.noCamps")}</p> : null}
      {previewState === "automation-training-data-unavailable" ? <p role="status">{t("automation.soldierTraining.reason.data_unavailable")}</p> : null}
      {trainingOpenFailed ? <p role="alert">{t("automation.soldierTraining.openFailed")}</p> : null}
      <div className="soldier-training-camps">{camps.map((camp, index) => <div className="soldier-training-camp" key={camp.uuid}><button type="button" title={t("automation.soldierTraining.openCamp")} disabled={!enabled} onClick={() => setTrainingOpenFailed(true)}>{t("automation.soldierTraining.building")} {index + 1}</button><span className="muted">{t("automation.soldierTraining.level", { level: camp.level })}</span><span className="soldier-training-camp-detail">{camp.productionCount > 0 ? t("automation.soldierTraining.production", { level: camp.productionLevel, count: camp.productionCount, time: new Date(camp.completionTime).toLocaleString(language, { month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" }) }) : t(`automation.soldierTraining.reason.${camp.reason}`)}</span></div>)}</div>
    </>;
  }
  if (title === "Automatic Construction") {
    const builderLimit = config.draft.maxBuilders ?? (previewState === "automation-validation-error" ? "21" : "1");
    const invalidBuilderLimit = !Number.isInteger(Number(builderLimit)) || Number(builderLimit) < 1 || Number(builderLimit) > 20;
    const categories = ["all", "economy", "military", "decoration", "season", "other"].filter((category) => category === "all" || previewConstructionBuildingTypes.some((building) => building.category === category));
    const activeCategory = categories.includes(constructionCategory) ? constructionCategory : "all";
    const visibleBuildings = previewConstructionBuildingTypes.filter((building) => activeCategory === "all" || building.category === activeCategory);
    const selectedSummary = constructionBuildingTypeIds.length ? previewConstructionBuildingTypes.filter((building) => constructionBuildingTypeIds.includes(building.itemId)).map((building) => building.name).join("、") : t("automation.construction.selectTypes");
    const moveConstructionTab = (event, category) => {
      const index = categories.indexOf(category);
      const nextIndex = event.key === "ArrowRight" ? (index + 1) % categories.length : event.key === "ArrowLeft" ? (index + categories.length - 1) % categories.length : event.key === "Home" ? 0 : event.key === "End" ? categories.length - 1 : -1;
      if (nextIndex < 0) return;
      event.preventDefault();
      setConstructionCategory(categories[nextIndex]);
      event.currentTarget.parentElement?.querySelectorAll('[role="tab"]')[nextIndex]?.focus();
    };
    return <><label className="automation-checkbox-row"><input type="checkbox" checked={constructionTargetEnabled} disabled={!enabled} onChange={(event) => setConstructionTargetEnabled(event.target.checked)} /><span>{t("automation.construction.targetEnabled")}</span></label>{constructionTargetEnabled ? <><div className="automation-actions"><label><span>{t("automation.construction.targetLevel")}</span><input type="number" min="1" max="100" step="1" {...field("constructionTargetLevel", "30")} disabled={!enabled} /></label></div><details className="construction-type-select"><summary><span>{t("automation.construction.buildingTypes")}</span><strong>{selectedSummary}</strong><span className="construction-select-arrow" aria-hidden="true">⌄</span></summary><div className="construction-category-tabs" role="tablist" aria-label={t("automation.construction.buildingTypes")}>{categories.map((category) => <button type="button" role="tab" id={`preview-construction-${category}`} aria-controls="preview-construction-types" aria-selected={activeCategory === category} tabIndex={activeCategory === category ? 0 : -1} key={category} onClick={() => setConstructionCategory(category)} onKeyDown={(event) => moveConstructionTab(event, category)}>{t(`automation.construction.category.${category}`)}</button>)}</div><fieldset className="construction-target-types" role="tabpanel" id="preview-construction-types" aria-labelledby={`preview-construction-${activeCategory}`} disabled={!enabled}>{visibleBuildings.map((building) => <label className="automation-checkbox-row" key={building.itemId}><input type="checkbox" checked={constructionBuildingTypeIds.includes(building.itemId)} onChange={(event) => setConstructionBuildingTypeIds((current) => event.target.checked ? [...current, building.itemId] : current.filter((itemId) => itemId !== building.itemId))} /><span>{building.name}</span></label>)}</fieldset></details><p className="muted">{t(constructionBuildingTypeIds.length ? "automation.construction.targetHint" : "automation.construction.selectTypes")}</p></> : null}<label className="automation-checkbox-row"><input type="checkbox" {...check("autoClaimCompleted", true)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label><span>{t("automation.maxBuilders")}</span><input type="number" min="1" max="20" step="1" {...field("maxBuilders", previewState === "automation-validation-error" ? "21" : "1")} disabled={!enabled} aria-invalid={invalidBuilderLimit || undefined} /></label></div>{invalidBuilderLimit ? <p className="automation-error" role="alert">{t("automation.builderLimitError")}</p> : null}</>;
  }
  if (title === "Automatic Treatment") {
    return <div className="automation-actions"><label><span>{t("automation.treatmentAmountPerArmy")}</span><input type="number" min="1" max="1000000" step="1" {...field("amountPerArmy", "1")} disabled={!enabled} /></label></div>;
  }
  if (title === "Automatic Official Application") {
    return <div className="automation-actions"><label><span>{t("automation.targetPosition")}</span><select {...field("positionId", "0")} disabled={!enabled}><option value="0">{t("automation.position.none")}</option><option value="10002">{t("automation.position.vicePresident")}</option><option value="10003">{t("automation.position.strategyMinister")}</option><option value="10004">{t("automation.position.defenseMinister")}</option><option value="10005">{t("automation.position.constructionMinister")}</option><option value="10006">{t("automation.position.scienceMinister")}</option><option value="10007">{t("automation.position.internalAffairsMinister")}</option></select></label></div>;
  }
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) {
    const treasure = title === "Treasure";
    return <><section className="automation-settings-section"><h3>{t("automation.section.claim")}</h3>{treasure ? <p className="hint">{t("automation.treasureTargetDelayHint")}</p> : null}<div className="automation-form-grid"><label><span>{t("automation.minDelaySeconds")}</span><input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMin", "0")} disabled={!enabled} /></label><label><span>{t("automation.maxDelaySeconds")}</span><input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMax", "0")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" checked={replyEnabled} disabled={!enabled} onChange={(event) => setReplyEnabled(event.target.checked)} /><span>{t("automation.autoReply")}</span></label>{replyEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label><span>{t("automation.replyDelayMin")}</span><input type="number" min="0.1" max="600" step="0.1" {...field("replyMin", "2")} /></label><label><span>{t("automation.replyDelayMax")}</span><input type="number" min="0.1" max="600" step="0.1" {...field("replyMax", "5")} /></label></div><label className="automation-replies"><span>{t("automation.replyPhrases")}</span><textarea rows="4" {...field("replies", "")} /></label></div> : null}</section>{treasure ? <section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" {...check("treasureSearchEnabled")} disabled={!enabled} /><span>{t("automation.treasureAutoSearch")}</span></label><p className="muted">{t("automation.treasureAutoSearchHint")}</p><label className="automation-checkbox-row"><input type="checkbox" checked={treasureDispatchEnabled} disabled={!enabled} onChange={(event) => setTreasureDispatchEnabled(event.target.checked)} /><span>{t("automation.treasureAutoDispatch")}</span></label>{treasureDispatchEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label><span>{t("automation.dispatchDelayMin")}</span><input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMin", "2")} /></label><label><span>{t("automation.dispatchDelayMax")}</span><input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMax", "5")} /></label></div><label><span>{t("automation.treasureDispatchRetrySeconds")}</span><input type="number" min="1" max="300" step="1" {...field("dispatchRetry", "30")} /></label><div className="automation-squad-choices">{[1,2,3,4].map((index) => <label key={index}><input type="checkbox" checked={(config.draft.dispatchSquads ?? [1]).includes(index)} onChange={(event) => config.store.edit((draft) => ({ ...draft, dispatchSquads: event.target.checked ? [...(draft.dispatchSquads ?? [1]), index] : (draft.dispatchSquads ?? [1]).filter((entry) => entry !== index) }))} /><span>{t("automation.squad", { index })}</span></label>)}</div><p className="muted">{t("automation.treasureDispatchPriorityHint")}</p></div> : null}</section> : null}</>;
  }
  if (title === "Trucks") {
    const weekly = config.draft.weeklyQualities ?? railwayWeeklyQualities;
    return <><section className="automation-settings-section"><h3>{t("automation.section.schedule")}</h3><div className="automation-actions"><label><span>{t("automation.delayAfterResetMinutes")}</span><input type="number" min="0" max="1440" step="1" {...field("delayMinutes", "2")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} values={weekly} running={previewState === "automation-runtime-running"} onChange={async (values) => { config.store.edit((draft) => ({ ...draft, weeklyQualities: values }), false); await config.store.flush().catch(() => {}); }} /></section><section className="automation-settings-section"><h3>{t("automation.section.departure")}</h3><label className="automation-checkbox-row"><input type="checkbox" {...check("departWhenTicketsInsufficient")} disabled={!enabled} /><span>{t("automation.railwayDepartWhenTicketsInsufficient")}</span></label></section></>;
  }
  if (title === "Secret Task") {
    const weekly = config.draft.weeklyQualities ?? dispatchWeeklyQualities;
    const activeJob = (task) => { const job = assistJobByTask.get(task.uuid); return job && ["scheduled", "waiting_connection", "retry_wait", "running"].includes(job.scheduleStatus) ? job : null; };
    const scheduleSelected = () => {
      if (!assistSelected.length) return;
      const scheduledAt = Date.UTC(2026, 9, 1, 19, 30, 0);
      setAssistJobs((current) => [...current.filter((job) => !assistSelected.includes(job.uuid)), ...assistSelected.map((uuid, index) => { const task = assistTasks.find((entry) => entry.uuid === uuid); return { ...task, uuid, scheduleSource: "manual", scheduleStatus: "scheduled", assistAt: scheduledAt + index * 60000 }; })]);
      setAssistSelected([]);
    };
    const changeJobStatus = (uuid, status) => setAssistJobs((current) => current.map((job) => job.uuid === uuid ? { ...job, scheduleStatus: status } : job));
    return <><section className="automation-settings-section"><h3>{t("squad.afkExecutionSettings")}</h3><label className="automation-checkbox-row"><input type="checkbox" {...check("collectRewards", false)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label><span>{t("automation.delayAfterResetMinutes")}</span><input type="number" min="0" max="1440" step="1" {...field("delayMinutes", "3")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} values={weekly} running={previewState === "automation-runtime-running"} onChange={async (values) => { config.store.edit((draft) => ({ ...draft, weeklyQualities: values }), false); await config.store.flush().catch(() => {}); }} /></section><section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" checked={dispatchAssistEnabled} disabled={!enabled} onChange={(event) => setDispatchAssistEnabled(event.target.checked)} /><span>{t("automation.dispatchAssist")}</span></label>{dispatchAssistEnabled ? <><div className="automation-squad-choices">{["n","r","sr","ssr","ur","special"].map((quality) => <label key={quality}><input type="checkbox" checked={(config.draft.assistQualities ?? []).includes(quality)} onChange={(event) => config.store.edit((draft) => ({ ...draft, assistQualities: event.target.checked ? [...(draft.assistQualities ?? []), quality] : (draft.assistQualities ?? []).filter((entry) => entry !== quality) }))} /><span>{quality === "special" ? t("automation.assistQuality.special") : quality.toUpperCase()}</span></label>)}</div><div className="automation-form-grid"><label><span>{t("automation.minDelaySeconds")}</span><input type="number" min="0" max="86400" step="1" {...field("assistMin", "0")} /></label><label><span>{t("automation.maxDelaySeconds")}</span><input type="number" min="0" max="86400" step="1" {...field("assistMax", "0")} /></label><label><span>{t("automation.assistIntervalSeconds")}</span><input type="number" min="5" max="300" step="1" {...field("assistInterval", "30")} /></label></div></> : <><strong>{t("automation.allySecretTasks")}</strong><div className="automation-task-list">{assistTasks.map((task) => { const job = activeJob(task); return <label className="automation-task-row automation-assist-task-row" key={task.uuid}><input type="checkbox" checked={assistSelected.includes(task.uuid)} disabled={!!job} onChange={(event) => setAssistSelected((current) => event.target.checked ? [...current, task.uuid] : current.filter((uuid) => uuid !== task.uuid))} /><span className="automation-assist-task-main"><span className="automation-assist-task-title"><strong>{assistTitle(task)}</strong><span className="automation-assist-stars" aria-label={String(task.star)}>{"★".repeat(Math.max(0, Number(task.star) || 0))}</span></span><span className="automation-assist-rewards">{task.items.map((item) => <span className="map-reward-item" key={item.key} title={`${item.name} ×${item.count}`}><strong>×{item.count}</strong></span>)}</span></span><span>{task.helpAvailable ? t("automation.helpAvailable") : new Date(task.completionTime).toLocaleString(language)}</span><span>{t(`automation.assistStatus.${job?.scheduleStatus || "untracked"}`)}</span></label>; })}{assistTasks.length === 0 ? <span className="muted">{t("automation.noAllySecretTasks")}</span> : null}</div><button type="button" disabled={!assistSelected.length} data-preview-action="presentation-only" onClick={scheduleSelected}>{t("automation.scheduleSelectedHelp")}</button><div className="automation-assist-queue">{assistJobs.filter((job) => job.scheduleSource === "manual" && ["scheduled", "waiting_connection", "retry_wait", "running", "failed", "expired"].includes(job.scheduleStatus)).map((job) => <div className="automation-inline-status" key={job.uuid}><span>{assistTitle(job)} · {t(`automation.assistStatus.${job.scheduleStatus}`)} · {new Date(job.assistAt).toLocaleString(language)}</span>{["scheduled", "waiting_connection", "retry_wait"].includes(job.scheduleStatus) ? <button type="button" data-preview-action="presentation-only" onClick={() => changeJobStatus(job.uuid, "cancelled")}>{t("common.cancel")}</button> : null}{["failed", "expired"].includes(job.scheduleStatus) ? <button type="button" data-preview-action="presentation-only" onClick={() => changeJobStatus(job.uuid, "scheduled")}>{t("common.retry")}</button> : null}</div>)}</div></>}</section></>;
  }
  if (title === "Ghost Ops") {
    return <><label className="automation-checkbox-row"><input type="checkbox" checked={ghostJoinEnabled} disabled={!enabled} onChange={(event) => setGhostJoinEnabled(event.target.checked)} /><span>{t("automation.ghost.autoJoinAlliance")}</span></label>{ghostJoinEnabled ? <div className="automation-section"><strong>{t("automation.ghost.allianceFilter")}</strong><div className="automation-compact-choice-group">{["sr","ur","special"].map((filter) => <label className="automation-compact-choice" key={filter}><input type="radio" name="preview-ghost-filter" value={filter} checked={(config.draft.ghostFilter ?? "special") === filter} onChange={() => config.store.edit((draft) => ({ ...draft, ghostFilter: filter }))} disabled={!enabled} /><span>{t(`automation.ghost.filter.${filter}`)}</span></label>)}</div></div> : null}<label className="automation-checkbox-row"><input type="checkbox" {...check("ghostClaimRewards")} disabled={!enabled} /><span>{t("automation.ghost.autoClaimRewards")}</span></label><div className="automation-inline-status"><span>{t("automation.ghost.ownPending")}: 0</span><span>{t("automation.ghost.allianceCandidates")}: 0</span><span>{t("automation.claimed")}: 0</span></div></>;
  }
  if (title === "Automatic Alliance Train Boarding") {
    const rewardSelectionVisible = trainMode === "reward" || vipTrainMode === "reward";
    const orderedRewards = [...preferredRewardKeys.map((key) => previewTrainRewards.find((reward) => reward.key === key)).filter(Boolean), ...previewTrainRewards.filter((reward) => !preferredRewardKeys.includes(reward.key))];
    const moveReward = (key, direction) => setPreferredRewardKeys((current) => { const index = current.indexOf(key); const target = index + direction; if (index < 0 || target < 0 || target >= current.length) return current; const next = [...current]; const [moved] = next.splice(index, 1); next.splice(target, 0, moved); return next; });
    return <><section className="automation-settings-section"><strong>{t("automation.normalCarriageSelection")}</strong><span className="muted">{t("automation.normalCarriageHint")}</span><div className="automation-compact-choice-group automation-selection-mode-choices">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-train-mode" checked={trainMode === mode} disabled={!enabled} onChange={() => setTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</div>{trainMode === "fixed" ? <fieldset className="automation-compact-choice-group automation-carriage-choices"><label className="automation-compact-choice"><input type="checkbox" disabled /><span>{t("automation.driver")}</span></label>{[1,2,3,4].map((carriage) => <label className="automation-compact-choice" key={carriage}><input type="checkbox" checked={normalFixedCarriageIds.includes(carriage)} disabled={!enabled} onChange={(event) => setNormalFixedCarriageIds(event.target.checked ? [carriage] : [])} /><span>{t(`automation.carriage${carriage}`)}</span></label>)}</fieldset> : null}</section>{rewardSelectionVisible ? <section className="automation-train-rewards automation-settings-section"><h3>{t("automation.section.rewardPreferences")}</h3><div className="automation-preference-list">{preferredRewardKeys.length === 0 ? <span className="muted">{t("automation.noPreferredRewards")}</span> : null}{orderedRewards.map((reward) => { const order = preferredRewardKeys.indexOf(reward.key); const selected = order >= 0; return <div className={`automation-preference-item${selected ? " selected" : ""}${draggedReward === reward.key ? " dragging" : ""}`} key={reward.key} draggable={selected} onDragStart={(event) => { if (selected) { event.dataTransfer.effectAllowed = "move"; setDraggedReward(reward.key); } }} onDragOver={(event) => { if (selected && draggedReward && draggedReward !== reward.key) { event.preventDefault(); event.dataTransfer.dropEffect = "move"; } }} onDrop={(event) => { event.preventDefault(); if (selected && draggedReward) setPreferredRewardKeys((current) => { const source = current.indexOf(draggedReward), target = current.indexOf(reward.key); if (source < 0 || target < 0 || source === target) return current; const next = [...current]; next.splice(target, 0, next.splice(source, 1)[0]); return next; }); setDraggedReward(""); }} onDragEnd={() => setDraggedReward("")}><label className="automation-reward-choice"><input type="checkbox" checked={selected} disabled={!enabled} onChange={(event) => setPreferredRewardKeys((current) => event.target.checked ? [...current, reward.key] : current.filter((key) => key !== reward.key))} /><span className="automation-reward-icon game-asset-placeholder" aria-hidden="true" /> <span>{reward.name}</span></label><strong className="automation-reward-count">×{reward.count}</strong>{selected ? <span className="automation-reward-order"><button type="button" title={t("automation.moveUp")} aria-label={t("automation.moveUp")} disabled={!enabled || order === 0} onClick={() => moveReward(reward.key, -1)}>↑</button><button type="button" title={t("automation.moveDown")} aria-label={t("automation.moveDown")} disabled={!enabled || order === preferredRewardKeys.length - 1} onClick={() => moveReward(reward.key, 1)}>↓</button><span className="automation-reward-drag-handle" aria-hidden="true">↕</span></span> : null}</div>; })}</div></section> : null}<section className="automation-settings-section"><strong>{t("automation.vipCarriageSelection")}</strong><label className="automation-checkbox-row"><input type="checkbox" {...check("autoAcceptVip")} disabled={!enabled} /><span>{t("automation.autoAcceptTrainVip")}</span></label><span className="muted">{t("automation.vipCarriageHint")}</span><div className="automation-compact-choice-group automation-selection-mode-choices">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-vip-train-mode" checked={vipTrainMode === mode} disabled={!enabled} onChange={() => setVipTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</div>{vipTrainMode === "fixed" ? <fieldset className="automation-compact-choice-group automation-carriage-choices"><label className="automation-compact-choice"><input type="checkbox" disabled /><span>{t("automation.driver")}</span></label>{[1,2,3,4].map((carriage) => { const selected = vipFixedCarriageIds.includes(carriage); return <label className="automation-compact-choice" key={carriage}><input type="checkbox" checked={selected} disabled={!enabled || (!selected && vipFixedCarriageIds.length >= 2)} onChange={(event) => setVipFixedCarriageIds((current) => event.target.checked ? [...current, carriage] : current.filter((item) => item !== carriage))} /><span>{t(`automation.carriage${carriage}`)}</span></label>; })}</fieldset> : null}</section><section className="automation-settings-section"><h3>{t("automation.section.additional")}</h3>{rewardSelectionVisible ? <label className="automation-checkbox-row"><input type="checkbox" checked={preferRewardQuantity} disabled={!enabled} onChange={(event) => setPreferRewardQuantity(event.target.checked)} /><span>{t("automation.preferRewardQuantity")}</span></label> : null}<div className="automation-actions"><label><span>{t("automation.thanksMode")}</span><select value={thanksMode} disabled={!enabled} onChange={(event) => setThanksMode(event.target.value)}><option value="like">{t("automation.thanksLike")}</option><option value="tickets">{t("automation.thanksTickets")}</option></select></label>{thanksMode === "tickets" ? <label><span>{t("automation.ticketCount")}</span><select {...field("ticketCount", "1")} disabled={!enabled}><option>1</option><option>2</option><option>3</option></select></label> : null}</div><p className="muted">{t("automation.ticketFallbackLike")}</p></section></>;
  }
  if (title === "Alliance Tech Donations") {
    return <div className="automation-actions"><label><span>{t("automation.donateThreshold")}</span><input type="number" min="1" max="30" step="1" {...field("threshold", "15")} disabled={!enabled} /></label></div>;
  }
  if (["Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources", "Building Resource Collection", "Armed Truck"].includes(title)) {
    const defaultInterval = title === "Alliance Gifts" ? 120 : 60;
    return <div className="automation-actions"><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" max="1440" step="1" {...field("intervalMinutes", defaultInterval)} disabled={!enabled} /></label></div>;
  }
  if (title === "Alliance Gathering Dispatch") {
    const selected = config.draft.allianceGatherSquads ?? [1, 2];
    const ordered = [...selected, ...[1, 2, 3, 4].filter((index) => !selected.includes(index))];
    return <><p className="muted">{t("automation.allianceGatherSquadPriority")}</p><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.allianceGatherSquadPriority")}>{ordered.map((index) => <div className={`automation-squad-priority-item${selected.includes(index) ? " selected" : ""}`} draggable={selected.includes(index)} key={index} onDragStart={(event) => { if (selected.includes(index)) { event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(index)); setDraggedGatherSquad(index); } }} onDragOver={(event) => { if (selected.includes(index) && draggedGatherSquad !== null && draggedGatherSquad !== index) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); const source = selected.indexOf(draggedGatherSquad), target = selected.indexOf(index); if (source >= 0 && target >= 0 && source !== target) { const next = [...selected]; next.splice(target, 0, next.splice(source, 1)[0]); config.store.edit((draft) => ({ ...draft, allianceGatherSquads: next })); } setDraggedGatherSquad(null); }} onDragEnd={() => setDraggedGatherSquad(null)}><label><input type="checkbox" checked={selected.includes(index)} disabled={!enabled} onChange={(event) => { const next = event.target.checked ? [...selected, index] : selected.filter((entry) => entry !== index); if (config.draft.enabled && next.length === 0) { setGatherSquadError("automation.allianceGatherSquadRequired"); return; } setGatherSquadError(""); config.store.edit((draft) => ({ ...draft, allianceGatherSquads: next })); }} /><span>{t("automation.squad", { index })}</span></label>{selected.includes(index) ? <span className="automation-squad-drag-handle" aria-hidden="true">↕</span> : null}</div>)}</div>{gatherSquadError ? <p className="automation-error" role="alert">{t(gatherSquadError)}</p> : null}</>;
  }
  return null;
}

function AutomationCard({ title, description, previewEnabled, previewState = "", onConfigStatus }) {
  const { english, language, t } = useI18n();
  const failFirstSave = previewState === "automation-save-error" || (previewState === "automation-weekly-save-error" && title === "Trucks") || (previewState === "automation-secret-weekly-save-error" && title === "Secret Task");
  const config = usePreviewConfig(() => initialAutomationDraft(title, previewState), (draft) => !automationDraftError(title, draft), failFirstSave, `automation:${previewState}:${title}`);
  const enabled = config.draft.enabled === true;
  const active = title === "Secret Task" ? enabled || config.draft.collectRewards === true : title === "Ghost Ops" ? enabled || config.draft.ghostJoinEnabled === true || config.draft.ghostClaimRewards === true : enabled;
  const runtime = previewAutomationRuntime(title, previewState);
  const runtimeState = title === "Auto Training" ? previewState === "automation-training-error" ? "error" : previewState === "automation-training-checking" ? "checking" : "waiting" : runtime.state;
  const presentation = automationRuntimePresentation(title, runtime, active, config.draft, t, language);
  const [toggleError, setToggleError] = useState("");
  const setEnabled = (next) => {
    const value = typeof next === "function" ? next(enabled) : next;
    if (title === "Auto Training" && value && !(Number.isInteger(config.draft.trainingTotalCount) && config.draft.trainingTotalCount > 0 && config.draft.trainingTotalCount <= 1000000)) return;
    if (title === "Alliance Gathering Dispatch" && value && !(config.draft.allianceGatherSquads ?? [1, 2]).length) { setToggleError("automation.allianceGatherSquadRequired"); return; }
    setToggleError("");
    config.store.edit((draft) => title === "Auto Training" ? activateTraining(draft, value, previewTrainingOrder(previewState)) : { ...draft, enabled: value });
  };
  useEffect(() => { onConfigStatus?.(title, config.error ? "error" : config.saving ? "saving" : ""); }, [title, config.error, config.saving]);
  const settingsCollapsible = !nonCollapsibleAutomationSettings.has(title);
  const hasSettings = !automationCardsWithoutSettings.has(title);
  const [expanded, setExpanded] = useState(previewEnabled && title === "Automatic Construction");
  const actionLabel = (title === "Trucks" || title === "Secret Task") && runtime.running ? "common.stop" : automationActionLabels[title];
  const toggleLabel = title === "Ghost Ops" ? "automation.ghost.autoStartOwn" : "automation.autoExecute";
  return (
    <article className="automation-card" data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{english(title)}</h3>
          {description ? <p>{english(description)}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled || (title === "Automatic Official Application" && Number(config.draft.positionId ?? 0) === 0)} aria-label={`${t(toggleLabel)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
        </div>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${active ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(sourceAutomationState(runtimeState, active)) : t("status.disconnected")}</span>
      </div>
      {previewEnabled && presentation.summary?.length ? <div className="automation-card-summary">{presentation.summary.map(([key, value]) => <span key={key}>{t(key)} <strong>{value}</strong></span>)}</div> : null}
      {previewEnabled && presentation.status?.length ? <div className="automation-status-grid">{presentation.status.map(([key, value]) => <div className="automation-status-row" key={key}><span>{t(key)}</span><strong>{value}</strong></div>)}</div> : null}
      {previewEnabled && previewState === "automation-runtime-error" && !["Weekend Shield", "Attack Shield"].includes(title) ? <p className="automation-error" role="alert">{t("common.actionFailed")}</p> : null}
      {toggleError ? <p role="alert" className="automation-error">{t(toggleError)}</p> : null}
      {(hasSettings || actionLabel) ? <div className="automation-config">
        {(settingsCollapsible || actionLabel) ? <div className="automation-config-actions">
          {hasSettings && settingsCollapsible ? <button className={`automation-config-trigger${expanded ? " is-open" : ""}`} type="button" disabled={!previewEnabled} aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>
            <span className="automation-config-trigger-label">
              <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
              <span>{t("settings.title")}</span>
            </span>
            <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 6l4 4 4-4" /></svg>
          </button> : null}
          {actionLabel ? <button type="button" className="automation-run-action" disabled title={t("status.gameDisconnectedDisabled")}>{t(actionLabel)}</button> : null}
        </div> : null}
        {hasSettings ? <fieldset className="automation-config-body" style={settingsCollapsible && !expanded ? { display: "none" } : undefined} disabled={!previewEnabled}><AutomationConfigPreview title={title} enabled={previewEnabled} previewState={previewState} config={config} /></fieldset> : null}
      </div> : null}
    </article>
  );
}

function ResourceGatherCard({ previewEnabled, previewState }) {
  const { t } = useI18n();
  const config = usePreviewConfig(() => previewEnabled ? previewResourceGatherConfig(previewState) : previewResourceGatherConfig("automation-gather-no-squads"), validPreviewResourceGatherConfig, previewState === "automation-gather-save-error", `automation:gather:${previewState}`);
  const enabled = config.draft.enabled === true;
  const squads = config.draft.squads ?? [];
  const [error, setError] = useState("");
  const save = (value) => {
    config.store.edit(value, false);
    config.store.flush().catch(() => {});
  };
  const updateSquad = (squadIndex, patch) => {
    const next = squads.map((squad) => squad.squadIndex === squadIndex ? { ...squad, ...patch } : squad);
    if (next.some((squad) => squad.enabled)) setError("");
    else if (enabled) {
      setError("");
    }
    save({ ...config.store.getSnapshot().draft, enabled: enabled && next.some((squad) => squad.enabled), squads: next });
  };
  const toggleEnabled = (nextEnabled) => {
    if (nextEnabled && !squads.some((squad) => squad.enabled)) {
      setError("automation.resourceGather.squadRequired");
      return;
    }
    setError("");
    save({ ...config.store.getSnapshot().draft, enabled: nextEnabled, squads });
  };
  const resourceLabels = { metal: "squad.afkResourceMetal", food: "squad.afkResourceFood", gold: "squad.afkResourceGold" };
  return (
    <article className="automation-card" data-preview-fixture={previewEnabled ? "automation-resource-gather" : "runtime-config-unobserved"} data-draft-dirty={config.dirty} data-draft-saving={config.saving}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{t("automation.category.resourceGather")}</h3>
          <p>{t("automation.resourceGather.description")}</p>
        </div>
        <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled} onClick={() => toggleEnabled(!enabled)}><Switch checked={enabled} /></button>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.waiting" : "common.disabled") : t("status.disconnected")}</span>
      </div>
      <PreviewConfigError config={config} t={t} />
      <div className="automation-config">
        <fieldset className="automation-config-body" disabled={!previewEnabled}>
          <div className="automation-resource-gather-options">
            <label className="automation-resource-gather-radius">
              <span>{t("automation.resourceGather.scanRadius")}</span>
              <select value={config.draft.scanRadius ?? 200} onChange={(event) => save({ ...config.store.getSnapshot().draft, scanRadius: Number(event.target.value) })}>{[50,100,150,200,250,300,400,500].map((radius) => <option value={radius} key={radius}>{radius}</option>)}</select>
            </label>
            <label>
              <span>{t("automation.resourceGather.manualResumeDelay")}</span>
              <select value={(config.draft.manualResumeDelaySeconds ?? 120) / 60} onChange={(event) => save({ ...config.store.getSnapshot().draft, manualResumeDelaySeconds: Number(event.target.value) * 60 })}>{Array.from({ length: 30 }, (_, index) => index + 1).map((minutes) => <option value={minutes} key={minutes}>{minutes}</option>)}</select>
            </label>
            <label className="automation-resource-gather-recall">
              <input type="checkbox" checked={config.draft.recallOnDisable ?? false} disabled={!previewEnabled} onChange={(event) => save({ ...config.store.getSnapshot().draft, recallOnDisable: event.target.checked })} />
              <span>{t("automation.resourceGather.recallOnDisable")}</span>
            </label>
            <small className="automation-resource-gather-radius-hint muted">
              {t("automation.resourceGather.scanRadiusHint", { size: 1000 })}
            </small>
          </div>
          {squads.length === 0 ? <p className="muted">{t("automation.resourceGather.squadsLoading")}</p> : null}
          {squads.map((squad) => <div className="automation-resource-gather-squad" role="group" aria-label={t("automation.squad", { index: squad.squadIndex })} key={squad.squadIndex}><label className="automation-resource-gather-enable"><input type="checkbox" checked={squad.enabled} onChange={(event) => updateSquad(squad.squadIndex, { enabled: event.target.checked, state: event.target.checked ? "idle" : "disabled" })} /><span>{t("automation.squad", { index: squad.squadIndex })}</span></label><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.resource")}`} value={squad.resource} onChange={(event) => updateSquad(squad.squadIndex, { resource: event.target.value })}>{["metal","food","gold"].map((resource) => <option value={resource} key={resource}>{t(resourceLabels[resource])}</option>)}</select><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.level")}`} value={squad.level} onChange={(event) => updateSquad(squad.squadIndex, { level: Number(event.target.value) })}>{Array.from({ length: Math.min(10, ({ metal: 10, food: 9, gold: 8 })[squad.resource]) }, (_, index) => index + 1).map((level) => <option value={level} key={level}>{level}</option>)}</select><span className="automation-resource-gather-state muted">{t(`automation.resourceGather.state.${["recalling", "recall_failed", "state_unconfirmed"].includes(previewState.replace("automation-gather-", "")) ? previewState.replace("automation-gather-", "") : enabled && squad.enabled ? previewState.startsWith("automation-gather-") ? previewState.replace("automation-gather-", "") : squad.state || "idle" : "disabled"}`)}{enabled && squad.enabled && previewState === "automation-gather-manual_wait" ? <small>{t("automation.resourceGather.manualResumeAt", { time: new Date(1799000300000).toLocaleTimeString() })}</small> : null}{enabled && squad.enabled && previewState === "automation-gather-shield_paused" ? <small>{t("automation.resourceGather.shieldEndAt", { time: new Date(1799003600000).toLocaleString() })}</small> : null}</span></div>)}
          {error ? <p className="automation-error" role="alert">{t(error)}</p> : null}
        </fieldset>
      </div>
    </article>
  );
}

function TradeStationCard({ previewEnabled, previewState = "" }) {
  const { language, t } = useI18n();
  const fixture = previewTradeFixture(previewState);
  const config = usePreviewConfig(() => ({ enabled: false, crossServerEnabled: false, selectedItemIds: previewState === "automation-trade-positive" || previewState === "automation-trade-history" ? [7001] : [], selectedCurrencyIds: [15, 650053] }), (draft) => typeof draft.enabled === "boolean" && typeof draft.crossServerEnabled === "boolean" && Array.isArray(draft.selectedItemIds) && Array.isArray(draft.selectedCurrencyIds) && draft.selectedCurrencyIds.length > 0, previewState === "automation-trade-save-error", `automation:trade:${previewState}`);
  const [tradeTab, setTradeTab] = useState("goods");
  const [showExclusive, setShowExclusive] = useState(false);
  const save = (draft) => { config.store.edit(draft, false); config.store.flush().catch(() => {}); };
  const enabled = config.draft.enabled === true;
  const currencies = [...fixture.goods.flatMap((item) => item.offers).filter((offer) => offer.currencyId > 0).reduce((offers, offer) => offers.has(offer.currencyId) ? offers : offers.set(offer.currencyId, offer), new Map()).values()].sort((a, b) => a.currencyId - b.currencyId);
  const visibleGoods = [...fixture.goods].filter((item) => showExclusive || !item.offers.every((offer) => offer.exclusive)).sort((a, b) => b.quality - a.quality || a.itemId - b.itemId);
  const toggleItem = (itemId, checked) => {
    const selectedItemIds = checked ? [...new Set([...config.draft.selectedItemIds, itemId])].sort((a, b) => a - b) : config.draft.selectedItemIds.filter((entry) => entry !== itemId);
    save({ ...config.store.getSnapshot().draft, enabled: selectedItemIds.length > 0 && enabled, selectedItemIds });
  };
  const toggleCurrency = (currencyId, checked) => {
    if (!checked && config.draft.selectedCurrencyIds.length === 1) return;
    const selectedCurrencyIds = checked ? [...new Set([...config.draft.selectedCurrencyIds, currencyId])].sort((a, b) => a - b) : config.draft.selectedCurrencyIds.filter((entry) => entry !== currencyId);
    save({ ...config.store.getSnapshot().draft, selectedCurrencyIds });
  };
  const purchaseDays = buildTradePurchaseDays(fixture.purchases);
  return (
    <div className="trade-station-panel" data-preview-fixture={previewEnabled ? `trade:${previewState}` : "runtime-config-unobserved"} data-draft-dirty={config.dirty} data-draft-saving={config.saving}>
      <PreviewConfigError config={config} t={t} />
      <article className="automation-card">
        <div className="automation-card-header">
          <div className="automation-card-title-group">
            <h3>{t("automation.tradeStation.title")}</h3>
            <p>{t("automation.tradeStation.description")}</p>
          </div>
          <div className="automation-card-header-actions">
            <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled || (!enabled && config.draft.selectedItemIds.length === 0)} onClick={() => save({ ...config.store.getSnapshot().draft, enabled: !enabled })}><Switch checked={enabled} /></button>
          </div>
        </div>
        <div className="automation-card-meta-row" role="status">
          <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.waiting" : "common.disabled") : t("status.disconnected")}</span>
        </div>
        <div className="automation-config">
          <fieldset className="automation-config-body" disabled={!previewEnabled}>
            <div className="trade-station-warning">
              {t("automation.tradeStation.warning")}
            </div>
            <ToggleRow label={t("automation.tradeStation.crossServer")} checked={config.draft.crossServerEnabled} disabled={!previewEnabled} onChange={(value) => save({ ...config.store.getSnapshot().draft, crossServerEnabled: value })} />
            <fieldset className="trade-station-currencies">
              <legend>{t("automation.tradeStation.currencies")}</legend>
              <div>{currencies.map((currency) => { const selected = config.draft.selectedCurrencyIds.includes(currency.currencyId); return <label className={selected ? "selected" : ""} key={currency.currencyId}><input type="checkbox" checked={selected} disabled={selected && config.draft.selectedCurrencyIds.length === 1} onChange={(event) => toggleCurrency(currency.currencyId, event.target.checked)} /><span className="trade-station-currency-icon game-asset-placeholder" aria-hidden="true" /><span>{currency.currencyName}</span></label>; })}</div>
            </fieldset>
            <div className="trade-station-stats">
              <span>{t("automation.tradeStation.detected")}: {fixture.status?.detectedCount ?? 0}</span>
              <span>{t("automation.tradeStation.attempted")}: {fixture.status?.attemptedCount ?? 0}</span>
              <span>{t("automation.tradeStation.succeeded")}: {fixture.status?.succeededCount ?? 0}</span>
              <span>{t("automation.tradeStation.lastResult")}: {fixture.status?.lastResult?.state ? t(`automation.tradeStation.state.${fixture.status.lastResult.state}`) : "-"}</span>
            </div>
            <div className="trade-station-tabs" role="tablist">
              <button type="button" role="tab" aria-selected={tradeTab === "goods"} className={tradeTab === "goods" ? "active" : ""} onClick={() => setTradeTab("goods")}>{t("automation.tradeStation.goods")}</button>
              <button type="button" role="tab" aria-selected={tradeTab === "purchases"} className={tradeTab === "purchases" ? "active" : ""} onClick={() => setTradeTab("purchases")}>{t("automation.tradeStation.purchasedItems", { count: fixture.purchases.length })}</button>
            </div>
            {tradeTab === "goods" ? <div className="trade-station-goods" role="tabpanel">
              <div className="trade-station-goods-heading">
                <strong>{t("automation.tradeStation.goodsToBuy")}</strong>
                <label><input type="checkbox" checked={showExclusive} disabled={!previewEnabled} onChange={(event) => setShowExclusive(event.target.checked)} />{t("automation.tradeStation.showExclusive")}</label>
              </div>
              {fixture.loading ? <span className="muted">{t("automation.tradeStation.loading")}</span> : null}
              {!fixture.loading && fixture.goods.length === 0 ? <span className="muted">{t("automation.tradeStation.noGoods")}</span> : null}
              <div className="trade-station-good-grid">{visibleGoods.map((item) => { const selected = config.draft.selectedItemIds.includes(item.itemId); const exclusive = item.offers.every((offer) => offer.exclusive); const possibleCurrencies = [...item.offers.reduce((labels, offer) => labels.has(offer.currencyId) ? labels : labels.set(offer.currencyId, offer.currencyName), new Map()).values()]; return <label className={`trade-station-good${selected ? " selected" : ""}${exclusive ? " disabled" : ""}`} key={item.itemId}><input type="checkbox" checked={selected} disabled={exclusive} onChange={(event) => toggleItem(item.itemId, event.target.checked)} /><span className={`trade-station-good-frame quality-${item.quality}`}><span className="trade-station-good-icon game-asset-placeholder" aria-hidden="true" /></span><span className="trade-station-good-copy"><b>{item.name}</b><small>{t("automation.tradeStation.buyWheneverAvailable")}</small><small>{t("automation.tradeStation.possibleCurrencies")} {possibleCurrencies.join(" / ") || "-"}</small>{exclusive ? <small className="trade-station-good-exclusive">{t("automation.tradeStation.exclusiveSkipped")}</small> : null}</span>{selected ? <span className="trade-station-good-check" aria-hidden="true">✓</span> : null}</label>; })}</div>
            </div> : <div role="tabpanel">{purchaseDays.length === 0 ? <span className="muted">{t("automation.tradeStation.noPurchases")}</span> : <div className="trade-station-purchase-history">{purchaseDays.map((day) => <section className="trade-station-purchase-day" key={day.dayStartAt}><h4 className="trade-station-purchase-day-heading"><time dateTime={new Date(day.dayStartAt).toISOString()}>{new Date(day.dayStartAt).toLocaleDateString(language, { year: "numeric", month: "long", day: "numeric", weekday: "short" })}</time><span className="trade-station-purchase-day-summary"><span>{t("automation.tradeStation.purchasedDayTotal", { count: day.totalQuantity.toLocaleString(language) })}</span>{[...day.itemCounts.values()].map(({ purchase, quantity }) => <span className="trade-station-purchase-day-item" key={purchase.itemId}>{resolveTradeName(fixture.gameTexts, purchase.itemNameKey, purchase.itemName, purchase.itemId)} ×{quantity.toLocaleString(language)}</span>)}</span></h4><div className="trade-station-purchase-list">{day.purchases.map((purchase, index) => { const itemName = resolveTradeName(fixture.gameTexts, purchase.itemNameKey, purchase.itemName, purchase.itemId); const currencyName = resolveTradeName(fixture.gameTexts, purchase.currencyNameKey, purchase.currencyName, purchase.currencyId); return <article className="trade-station-purchase" key={tradePurchaseRowKey(purchase, index)}><span className={`trade-station-good-frame quality-${purchase.quality || 0}`}>{purchase.quality > 0 ? <span className="trade-station-good-frame-image game-asset-placeholder" aria-hidden="true" /> : null}<span className="trade-station-good-icon game-asset-placeholder" aria-hidden="true" /></span><span className="trade-station-purchase-copy"><b>{itemName}</b><small>{t("automation.tradeStation.quantity", { count: purchase.quantity.toLocaleString(language) })}</small><small className="trade-station-purchase-price">{purchase.currencyIconPath ? <span className="trade-station-currency-icon game-asset-placeholder" aria-hidden="true" /> : null}{t("automation.tradeStation.price", { price: purchase.price.toLocaleString(language), currency: currencyName })}</small><small>{t("automation.tradeStation.purchasedAt", { time: new Date(purchase.purchasedAt).toLocaleTimeString(language) })}</small><small>{t("automation.tradeStation.server", { server: purchase.serverId })}</small><small>{t("automation.tradeStation.dailyPurchaseIndex", { count: purchase.dailyPurchaseIndex == null ? "-" : purchase.dailyPurchaseIndex.toLocaleString(language) })}</small>{purchase.confirmedAfterTimeout ? <small>{t("automation.tradeStation.confirmedAfterTimeout")}</small> : null}</span></article>; })}</div></section>)}</div>}</div>}
            {fixture.error ? <div className="automation-error" role="alert">{fixture.error}</div> : null}
          </fieldset>
        </div>
      </article>
    </div>
  );
}

export function AutomationPage({ previewState = "" }) {
  const { t } = useI18n();
  const [category, setCategory] = useState("daily");
  const [configStates, setConfigStates] = useState({});
  const onConfigStatus = (title, state) => setConfigStates((current) => current[title] === state ? current : { ...current, [title]: state });
  const aggregateState = Object.values(configStates).includes("error") ? "error" : Object.values(configStates).includes("saving") ? "saving" : "";
  const previewEnabled = previewState.startsWith("automation-");
  const pageStatus = aggregateState === "error" ? "automation.configSave.error" : aggregateState === "saving" ? "automation.configSave.saving" : previewState === "automation-saving"
    ? "automation.configSave.saving"
    : previewState === "automation-save-error"
      ? "automation.configSave.error"
      : previewState === "automation-saved"
        ? "automation.configSave.saved"
        : "";
  return (
    <section className="panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("nav.automation")} subtitle={previewEnabled ? (pageStatus ? t(pageStatus) : "") : t("status.gameDisconnected")} />
      <div className="automation-categories" role="tablist" aria-label={t("nav.automation")}>
        {automationCategories.map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            className={category === key ? "active" : ""}
            aria-selected={category === key}
            onClick={() => setCategory(key)}
          >
            {t(automationCategoryKeys[key]) || label}
          </button>
        ))}
      </div>
      <>{automationCategories.map(([key]) => <div className="automation-grid" key={key} style={category === key ? undefined : { display: "none" }}>
        {key === "resourceGather" ? <ResourceGatherCard previewEnabled={previewEnabled} previewState={previewState} /> : null}
        {key === "trade" ? <TradeStationCard previewEnabled={previewEnabled} previewState={previewState} /> : null}
        {(automationCards[key] ?? []).map(([title, description]) => <AutomationCard key={title} title={title} description={description} previewEnabled={previewEnabled} previewState={previewState} onConfigStatus={onConfigStatus} />)}
      </div>)}</>
    </section>
  );
}

export function SquadsPage({ previewState = "" }) {
  const { t } = useI18n();
  const equipmentPreview = previewState.startsWith("squads-equipment");
  const previewEnabled = previewState.startsWith("squads-profile") || equipmentPreview;
  const [tab, setTab] = useState(equipmentPreview ? "equipment" : "afk");
  return (
    <section className="panel squad-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <div className="squad-header">
        <h2>{t("squad.title")}</h2>
        {tab === "equipment" ? <button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("squad.refresh")}</button> : null}
      </div>
      <div className="squad-tabs" role="tablist" aria-label={t("squad.title")}>
        <button type="button" role="tab" className={tab === "afk" ? "active" : ""} aria-selected={tab === "afk"} onClick={() => setTab("afk")}>{t("squad.tabAfk")}</button>
        <button type="button" role="tab" className={tab === "equipment" ? "active" : ""} aria-selected={tab === "equipment"} onClick={() => setTab("equipment")}>{t("squad.tabEquipment")}</button>
      </div>
      <div style={tab === "afk" ? undefined : { display: "none" }}><AfkContent previewEnabled={previewEnabled} previewState={previewState} /></div><div style={tab === "equipment" ? undefined : { display: "none" }}><EquipmentContent previewEnabled={previewEnabled} previewState={previewState} /></div>
    </section>
  );
}

function AfkProfileEditor({ enabled, isNew = false, profile, onProfileChange, previewState = "" }) {
  const { t } = useI18n();
  const [memberPickerOpen, setMemberPickerOpen] = useState(false);
  const [memberSearch, setMemberSearch] = useState("");
  const [selectedMemberIds, setSelectedMemberIds] = useState(new Set());
  const [targetError, setTargetError] = useState("");
  const memberFixture = previewMemberFixture(previewState);
  const currentMemberMap = new Map(memberFixture.members.map((member) => [member.uid, member]));
  const selectableMembers = memberFixture.members.filter((member) => member.uid !== memberFixture.selfUid);
  const filteredMembers = selectableMembers.filter((member) => !memberSearch.trim() || `${member.name} ${member.uid}`.toLowerCase().includes(memberSearch.trim().toLowerCase()));
  const targetDiscoveryReady = !["squads-profile-target-loading", "squads-profile-target-failed"].includes(previewState);
  const sourceTargets = previewState === "squads-profile-target-undiscovered" ? previewAfkTargets.filter((target) => target.key !== profile.targetKey) : previewAfkTargets;
  const availableTargets = sourceTargets.filter((target) => profile.kind === "join" ? target.rally : !target.joinOnly);
  const selectedTarget = sourceTargets.find((target) => target.key === profile.targetKey) || null;
  const targetMissing = targetDiscoveryReady && !profile.customTarget && !selectedTarget;
  const attackRangeWarning = profile.kind === "farm" && profile.searchable && previewAfkLevelOutOfRange(profile, selectedTarget);
  const join = profile.joinRestrictions || normalizeJoinRestrictions(undefined, 1, true);
  const patch = (changes) => onProfileChange({ ...profile, ...changes });
  const patchJoin = (changes) => patch({ joinRestrictions: { ...join, ...changes } });
  useEffect(() => {
    if (!memberPickerOpen) return;
    const closeOnEscape = (event) => { if (event.key === "Escape") setMemberPickerOpen(false); };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [memberPickerOpen]);
  useEffect(() => {
    setMemberPickerOpen(false);
    setMemberSearch("");
    setSelectedMemberIds(new Set());
    setTargetError("");
  }, [profile.id]);
  return (
    <section className={`monster-afk-editor ${isNew ? "is-new" : "is-editing"}`} data-preview-fixture="afk-profile-editor">
      <div className="monster-afk-editor-heading"><strong>{t(isNew ? "squad.afkNewProfile" : "squad.afkEditProfile")}</strong><span className={`monster-afk-mode-badge ${profile.kind}`}>{t(profile.kind === "farm" ? "squad.afkActiveAttack" : "squad.afkJoin")}</span></div>
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkBasicSettings")}</strong>
        <div className="monster-afk-basic-grid">
          <label><span>{t("squad.afkProfileName")}</span><input value={profile.name} disabled={!enabled} onChange={(event) => patch({ name: event.target.value })} onBlur={() => { if (previewAfkProfileValid(profile)) patch({ name: profile.name.trim() }); }} onKeyDown={(event) => { if (event.key === "Enter" && !event.nativeEvent.isComposing) event.currentTarget.blur(); }} /></label>
          <label><span>{t("squad.afkTarget")}</span><select value={profile.customTarget ? profile.lastListTargetKey : profile.targetKey} disabled={!enabled || !targetDiscoveryReady} onChange={(event) => { const target = sourceTargets.find((entry) => entry.key === event.target.value); if (!target) return; setTargetError(""); onProfileChange(applyAfkTarget(profile, target)); }}>{targetMissing ? <option value={profile.targetKey}>{profile.targetNameQuery || profile.monsterNameKey || profile.targetKey} · {t("squad.afkUndiscovered")}</option> : null}{profile.customTarget && !availableTargets.some((target) => target.key === profile.lastListTargetKey) ? <option value={profile.lastListTargetKey}>{t("squad.afkSelectListTarget")}</option> : null}{["normal", "elite", "running", "leader", "ally", "drill", "invader", "other"].filter((group) => availableTargets.some((target) => target.group === group)).map((group) => <optgroup label={t(`squad.afkGroup.${group}`)} key={group}>{availableTargets.filter((target) => target.group === group).map((target) => <option value={target.key} key={target.key}>{target.name || (target.labelKey ? t(target.labelKey) : target.key)} · {t(target.source === "search" ? "squad.afkSearchable" : "squad.afkLocalTarget")} · {t(profile.kind === "join" ? "squad.afkJoin" : target.action === "rally" ? "squad.afkActionRally" : "squad.afkActionAttack")}{target.searchable ? ` · ${target.attackMinLevel != null && target.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: target.attackMinLevel, max: target.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}` : ""}</option>)}</optgroup>)}</select>{previewState === "squads-profile-target-loading" ? <span className="muted">{t("status.checking")}</span> : null}{previewState === "squads-profile-target-failed" ? <span className="status-error">{t("common.actionFailed")}</span> : null}</label>
        </div>
        <div className="monster-afk-custom-target"><label className="monster-afk-check-row"><input type="checkbox" checked={profile.customTarget} disabled={!enabled} onChange={(event) => { if (event.target.checked) { setTargetError(""); patch({ customTarget: true, lastListTargetKey: profile.targetKey, targetKey: "query:", targetNameQuery: "", monsterType: 0, monsterNameKey: undefined, monsterIds: [], source: "undiscovered", action: profile.kind === "join" ? "rally" : "attack", rally: profile.kind === "join", searchable: false, minLevel: 1, maxLevel: 999 }); return; } const restored = availableTargets.find((target) => target.key === profile.lastListTargetKey); if (!restored) { setTargetError("squad.afkRestoreTargetRequired"); return; } setTargetError(""); onProfileChange(applyAfkTarget(profile, restored)); }} />{t("squad.afkCustomTarget")}</label>{profile.customTarget ? <label className="monster-afk-custom-name"><span>{t("squad.afkCustomName")}</span><input value={profile.targetNameQuery} aria-invalid={!profile.targetNameQuery.trim()} disabled={!enabled} onChange={(event) => patch({ targetKey: `query:${event.target.value.trim()}`, targetNameQuery: event.target.value })} /><span className="muted">{t("squad.afkCustomTargetHint")}</span>{!profile.targetNameQuery.trim() ? <span role="status" className="status-error">{t("squad.afkCustomTargetRequired")}</span> : null}</label> : null}{targetError ? <span role="status" className="status-error">{t(targetError)}</span> : null}</div>
      </div>
      {profile.kind === "join" ? <div className="monster-afk-config-section monster-afk-join-section">
        <strong>{t("squad.afkJoinConditions")}</strong>
        <div className="rally-join-settings">
          <ToggleRow label={t("squad.afkJoinConditions")} checked={join.enabled} disabled={!enabled} onChange={(value) => patchJoin({ enabled: value })} />
          {join.enabled ? <fieldset disabled={!enabled}>
          <label><span>{t("squad.join.mode")}</span><select value={join.mode} disabled={!enabled} onChange={(event) => patchJoin({ mode: event.target.value })}><option value="slot">{t("squad.join.mode.slot")}</option><option value="delay">{t("squad.join.mode.delay")}</option></select></label>
          {join.mode === "slot" ? <div className="rally-join-range">{[0,1].map((index) => <label key={index}><span>{t(`squad.join.slotRange.${index === 0 ? "min" : "max"}`)}</span><input type="number" min="2" max="5" step="1" value={Number.isNaN(join.slotRange[index]) ? "" : join.slotRange[index]} disabled={!enabled} onChange={(event) => { const next = [...join.slotRange]; next[index] = Math.min(5, Math.max(2, Number.isFinite(event.target.valueAsNumber) ? Math.trunc(event.target.valueAsNumber) : 2)); patchJoin({ slotRange: next }); }} /></label>)}</div> : <div className="rally-join-range">{[0,1].map((index) => <label key={index}><span>{t(`squad.join.delaySeconds.${index === 0 ? "min" : "max"}`)}</span><input type="number" min="0" max="6000" step="0.01" value={Number.isNaN(join.delaySeconds[index]) ? "" : join.delaySeconds[index]} disabled={!enabled} onChange={(event) => { const next = [...join.delaySeconds]; next[index] = event.target.valueAsNumber; patchJoin({ delaySeconds: next }); }} /></label>)}</div>}
          {join.mode === "slot" ? <label><span>{t("squad.join.slotDelay")}</span><input type="number" min="0" max="6000" step="0.01" value={Number.isNaN(join.slotDelaySeconds) ? "" : join.slotDelaySeconds} disabled={!enabled} onChange={(event) => patchJoin({ slotDelaySeconds: event.target.valueAsNumber })} /></label> : null}
          <label><span>{t("squad.join.maxWait")}</span><input type="number" min="0" step="1" value={Number.isNaN(join.maxWaitMinutes) ? "" : join.maxWaitMinutes} disabled={!enabled} onChange={(event) => patchJoin({ maxWaitMinutes: event.target.valueAsNumber })} /></label>
          <label><span>{t("squad.join.list")}</span><select value={join.leaderListMode} disabled={!enabled} onChange={(event) => patchJoin({ leaderListMode: event.target.value })}><option value="off">{t("squad.join.list.off")}</option><option value="blacklist">{t("squad.join.list.blacklist")}</option><option value="whitelist">{t("squad.join.list.whitelist")}</option></select></label>
          {join.leaderListMode !== "off" ? <div className="rally-join-leaders"><button type="button" disabled={!enabled || !memberFixture.ready} onClick={() => { setSelectedMemberIds(new Set(join.leaders.filter((leader) => leader.uid !== memberFixture.selfUid).map((leader) => leader.uid))); setMemberSearch(""); setMemberPickerOpen(true); }}>{t("squad.join.chooseMembers")}</button>{!memberFixture.ready ? <span className={memberFixture.failed ? "status-error" : "muted"}>{t(!memberFixture.online ? "squad.join.membersOffline" : memberFixture.failed ? "squad.join.membersFailed" : "squad.join.membersLoading")}</span> : null}{join.leaders.map((leader) => { const current = currentMemberMap.get(leader.uid); const left = memberFixture.ready && !current; return <div className="rally-join-leader" key={leader.uid}><span className="rally-join-member-name"><strong>{current?.name || leader.name || leader.uid}</strong><small>{leader.uid}{left ? ` · ${t("squad.join.memberLeft")}` : ""}</small></span><button type="button" className="danger" onClick={() => patchJoin({ leaders: join.leaders.filter((entry) => entry.uid !== leader.uid) })}>{t("common.delete")}</button></div>; })}{memberFixture.ready && join.leaders.some((leader) => !currentMemberMap.has(leader.uid)) ? <span className="muted">{t("squad.join.memberLeftHint")}</span> : null}{join.leaderListMode === "whitelist" && join.leaders.length === 0 ? <span className="muted">{t("squad.join.emptyWhitelist")}</span> : null}</div> : null}
          <label><input type="checkbox" checked={join.skipSoloLeader} disabled={!enabled} onChange={(event) => patchJoin({ skipSoloLeader: event.target.checked })} />{t("squad.join.skipSolo")}</label><label><input type="checkbox" checked={join.skipKicked} disabled={!enabled} onChange={(event) => patchJoin({ skipKicked: event.target.checked })} />{t("squad.join.skipKicked")}</label>
          {!validJoinRestrictions(join) ? <p role="alert" className="status-error">{t("squad.join.invalid")}</p> : null}
          </fieldset> : null}
        </div>
      </div> : null}
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkExecutionSettings")}</strong>
        <div className="monster-afk-basic-grid monster-afk-execution-grid">
          <div className="monster-afk-squad-field"><span>{t("squad.afkAssignments")}</span><div className="monster-afk-squads" role="group" aria-label={t("squad.afkAssignments")}>{[1,2,3,4].map((number) => <button className="monster-afk-squad-toggle" type="button" aria-label={t("squad.number", { number })} aria-pressed={profile.squadIndexes.includes(number)} disabled={!enabled} key={number} onClick={() => patch({ squadIndexes: profile.squadIndexes.includes(number) ? profile.squadIndexes.filter((entry) => entry !== number) : [...profile.squadIndexes, number].sort((a, b) => a - b) })}>{number}</button>)}</div></div>
          <label title={t("squad.afkExecutionLimitHint")}><span>{t("squad.afkExecutionLimitLabel")}</span><input type="number" min="0" step="1" value={Number.isNaN(profile.executionLimit) ? "" : profile.executionLimit} disabled={!enabled} onChange={(event) => patch({ executionLimit: event.target.valueAsNumber })} /></label>
        </div>
        {profile.kind === "farm" ? <label className="monster-afk-check-row"><input type="checkbox" checked={profile.continuousAttack} disabled={!enabled || profile.rally} onChange={(event) => patch({ continuousAttack: event.target.checked })} />{t("squad.afkContinuousAttack")}{profile.rally ? <span className="muted">{t("squad.afkContinuousAttackUnavailable")}</span> : null}</label> : <label className="monster-afk-check-row"><input type="checkbox" checked={profile.continuousJoin} disabled={!enabled} onChange={(event) => patch({ continuousJoin: event.target.checked })} />{t("squad.afkContinuousJoin")}</label>}
      </div>
      <div className="monster-afk-config-section monster-afk-filter">
        <strong>{t("squad.afkTargetFilters")}</strong>
        {profile.kind === "farm" && profile.searchable ? <p className={attackRangeWarning ? "monster-afk-level-warning" : "muted"}>{selectedTarget?.attackMinLevel != null && selectedTarget?.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: selectedTarget.attackMinLevel, max: selectedTarget.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}{attackRangeWarning ? ` · ${t("squad.afkLevelOutOfRange")}` : ""}</p> : null}
        <label className="monster-afk-check-row"><input type="checkbox" checked={profile.levelFilterEnabled} disabled={!enabled} onChange={(event) => patch({ levelFilterEnabled: event.target.checked, progressiveLevels: event.target.checked && profile.progressiveLevels })} />{t("squad.afkLevelFilter")}</label>
        {profile.levelFilterEnabled ? <div className="monster-afk-filter-content"><div className="monster-afk-number-grid"><label><span>{t("squad.afkMinLevel")}</span><input type="number" min="1" value={Number.isNaN(profile.minLevel) ? "" : profile.minLevel} disabled={!enabled} onChange={(event) => patch({ minLevel: event.target.valueAsNumber })} /></label>{!profile.progressiveLevels ? <label><span>{t("squad.afkMaxLevel")}</span><input type="number" min="1" value={Number.isNaN(profile.maxLevel) ? "" : profile.maxLevel} disabled={!enabled} onChange={(event) => patch({ maxLevel: event.target.valueAsNumber })} /></label> : null}</div>{profile.kind === "farm" && profile.searchable ? <label className="monster-afk-check-row"><input type="checkbox" checked={profile.progressiveLevels} disabled={!enabled} onChange={(event) => patch({ progressiveLevels: event.target.checked })} />{t("squad.afkProgressiveLevels")}</label> : null}</div> : null}
        <label className="monster-afk-check-row"><input type="checkbox" checked={profile.distanceFilterEnabled} disabled={!enabled} onChange={(event) => patch({ distanceFilterEnabled: event.target.checked })} />{t("squad.afkDistanceFilter")}</label>
        {profile.distanceFilterEnabled ? <div className="monster-afk-filter-content"><label><span>{t("squad.maxDistance")}</span><input type="number" min="1" value={Number.isNaN(profile.maxDistance) ? "" : profile.maxDistance} disabled={!enabled} onChange={(event) => patch({ maxDistance: event.target.valueAsNumber })} /></label></div> : null}
      </div>

      {profile.kind === "join" && memberPickerOpen ? <div className="garrison-modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setMemberPickerOpen(false); }}><section className="garrison-modal" role="dialog" aria-modal="true" aria-label={t("squad.join.chooseMembers")}><div className="garrison-modal-heading"><strong>{t("squad.join.chooseMembers")}</strong><button type="button" onClick={() => setMemberPickerOpen(false)}>{t("common.cancel")}</button></div><input aria-label={t("squad.join.searchMembers")} placeholder={t("squad.join.searchMembers")} value={memberSearch} onChange={(event) => setMemberSearch(event.target.value)} autoFocus /><div className="garrison-ally-list">{filteredMembers.map((member) => <label key={member.uid}><input type="checkbox" checked={selectedMemberIds.has(member.uid)} onChange={(event) => setSelectedMemberIds((current) => { const next = new Set(current); if (event.target.checked) next.add(member.uid); else next.delete(member.uid); return next; })} /><span><strong>{member.name}</strong><small>{member.uid}</small></span></label>)}{filteredMembers.length === 0 ? <span className="muted">{t("squad.join.noMembers")}</span> : null}</div><div className="garrison-modal-actions"><button type="button" className="primary-action" onClick={() => { const retained = join.leaders.filter((leader) => leader.uid === memberFixture.selfUid || !currentMemberMap.has(leader.uid)); const selected = selectableMembers.filter((member) => selectedMemberIds.has(member.uid)).map(({ uid, name }) => ({ uid, name })); patchJoin({ leaders: [...retained, ...selected] }); setMemberPickerOpen(false); }}>{t("squad.join.confirmMembers")}</button></div></section></div> : null}
    </section>
  );
}

function AllianceDrillPreviewSettings({ disabled, value, onChange, previewState = "" }) {
  const { t } = useI18n();
  const join = value.joinRestrictions || normalizeJoinRestrictions(undefined, 1, true);
  const patch = (changes) => onChange({ ...value, ...changes });
  const patchJoin = (changes) => patch({ joinRestrictions: { ...join, ...changes } });
  const runtimeState = previewState === "squads-profile-drill-leading" ? "squad.afkAllianceDrillLeading" : previewState === "squads-profile-drill-joining" ? "squad.afkAllianceDrillJoining" : "squad.afkAllianceDrillWaiting";
  return <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("squad.afkAllianceDrill")}</strong><span>{t("squad.afkAllianceDrillDescription")}</span></div><div className="monster-afk-drill-inline"><span className="muted">{t("squad.afkAllianceDrillOrder", { order: value.squadIndexes.join(" → ") || "-" })} · {t(runtimeState)}</span><ToggleRow label={t("squad.afkAllianceDrillActive")} checked={value.activeRally} disabled={disabled} onChange={(activeRally) => patch({ activeRally })} /></div><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("squad.afkAllianceDrillSquads")}>{[1,2,3,4].map((number) => { const selected = value.squadIndexes.includes(number); return <label className={`automation-squad-priority-item${selected ? " selected" : ""}`} key={number}><input type="checkbox" checked={selected} disabled={disabled} onChange={(event) => { const squadIndexes = event.target.checked ? [...value.squadIndexes, number].sort((a,b) => a-b) : value.squadIndexes.filter((item) => item !== number); patch({ enabled: value.enabled && squadIndexes.length > 0, squadIndexes }); }} /><span>{t("squad.number", { number })}</span></label>; })}</div>{value.squadIndexes.length === 0 ? <span className="status-error">{t("squad.afkAllianceDrillSquadRequired")}</span> : null}<div className="rally-join-settings"><ToggleRow label={t("squad.afkJoinConditions")} checked={join.enabled} disabled={disabled} onChange={(enabled) => patchJoin({ enabled })} />{join.enabled ? <fieldset disabled={disabled}><label><span>{t("squad.join.mode")}</span><select value={join.mode} onChange={(event) => patchJoin({ mode: event.target.value })}><option value="slot">{t("squad.join.mode.slot")}</option><option value="delay">{t("squad.join.mode.delay")}</option></select></label>{join.mode === "slot" ? <div className="rally-join-range">{[0,1].map((index) => <label key={index}><span>{t(`squad.join.slotRange.${index === 0 ? "min" : "max"}`)}</span><input type="number" min="2" max="5" step="1" value={join.slotRange[index]} onChange={(event) => { const slotRange = [...join.slotRange]; slotRange[index] = event.target.valueAsNumber; patchJoin({ slotRange }); }} /></label>)}</div> : <div className="rally-join-range">{[0,1].map((index) => <label key={index}><span>{t(`squad.join.delaySeconds.${index === 0 ? "min" : "max"}`)}</span><input type="number" min="0" max="6000" step="0.01" value={join.delaySeconds[index]} onChange={(event) => { const delaySeconds = [...join.delaySeconds]; delaySeconds[index] = event.target.valueAsNumber; patchJoin({ delaySeconds }); }} /></label>)}</div>}<label><span>{t("squad.join.maxWait")}</span><input type="number" min="0" step="1" value={join.maxWaitMinutes} onChange={(event) => patchJoin({ maxWaitMinutes: event.target.valueAsNumber })} /></label><label><span>{t("squad.join.list")}</span><select value={join.leaderListMode} onChange={(event) => patchJoin({ leaderListMode: event.target.value })}><option value="off">{t("squad.join.list.off")}</option><option value="blacklist">{t("squad.join.list.blacklist")}</option><option value="whitelist">{t("squad.join.list.whitelist")}</option></select></label><label><input type="checkbox" checked={join.skipSoloLeader} onChange={(event) => patchJoin({ skipSoloLeader: event.target.checked })} />{t("squad.join.skipSolo")}</label><label><input type="checkbox" checked={join.skipKicked} onChange={(event) => patchJoin({ skipKicked: event.target.checked })} />{t("squad.join.skipKicked")}</label>{!validJoinRestrictions(join) ? <p role="alert" className="status-error">{t("squad.join.invalid")}</p> : null}</fieldset> : null}</div></section>;
}

function GarrisonPreviewSettings({ disabled, value, onChange, previewState = "" }) {
  const { t } = useI18n();
  const runtime = previewGarrisonRuntime(previewState);
  const memberFixture = previewMemberFixture(previewState);
  const members = memberFixture.members.filter((member) => member.uid !== memberFixture.selfUid);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pickerSearch, setPickerSearch] = useState("");
  const [draftAllies, setDraftAllies] = useState(new Set());
  const filtered = members.filter((member) => !pickerSearch.trim() || `${member.name} ${member.uid}`.toLowerCase().includes(pickerSearch.trim().toLowerCase()));
  const selected = new Set(value.targets.map((target) => target.kind === "allianceBuilding" ? `building:${target.buildId}` : `ally:${target.uid}`));
  const selectedAllies = value.targets.filter((target) => target.kind === "allyCity");
  const toggleBuilding = (building) => { const key = `building:${building.buildId}`; const targets = selected.has(key) ? value.targets.filter((target) => !(target.kind === "allianceBuilding" && target.buildId === building.buildId)) : [...value.targets, { kind: "allianceBuilding", buildId: building.buildId, nameSnapshot: building.name }]; onChange({ ...value, enabled: value.enabled && targets.length > 0, targets }); };
  return <section className="automation-card monster-afk-toolbar-settings garrison-settings"><div className="monster-afk-toolbar-settings-heading"><div><strong>{t("garrison.settings")}</strong><span>{t("garrison.settingsDescription")}</span></div><span className="garrison-count">{t("garrison.selectedTargets", { count: value.targets.length })}</span></div><div className="garrison-settings-grid"><div className="garrison-target-panel"><div className="garrison-section-heading"><strong>{t("garrison.buildings")}</strong><span>{previewGarrisonBuildings.filter((building) => selected.has(`building:${building.buildId}`)).length}/{previewGarrisonBuildings.length}</span></div><div className="garrison-building-grid">{runtime.buildings.map((building) => <label className={`garrison-building ${building.role}${building.available ? "" : " unavailable"}`} key={building.key}><input type="checkbox" checked={selected.has(`building:${building.buildId}`)} disabled={disabled || !building.available} onChange={() => toggleBuilding(building)} /><span>{building.name}<small>{building.unavailableReason === "season_settled" ? t("garrison.seasonEnded") : building.available ? t(building.role === "center" ? "garrison.center" : "garrison.attachment") : t("garrison.full")}</small></span></label>)}</div><div className="garrison-section-heading"><strong>{t("garrison.allies")}</strong><button type="button" disabled={disabled || !memberFixture.ready} onClick={() => { setDraftAllies(new Set(selectedAllies.map((target) => target.uid))); setPickerSearch(""); setPickerOpen(true); }}>{t("garrison.chooseAllies")}</button></div>{!memberFixture.ready ? <span className={memberFixture.failed ? "status-error" : "muted"}>{t(!memberFixture.online ? "squad.join.membersOffline" : memberFixture.failed ? "squad.join.membersFailed" : "squad.join.membersLoading")}</span> : null}<div className="garrison-selected-allies">{selectedAllies.length ? selectedAllies.map((target) => <span key={target.uid}>{members.find((member) => member.uid === target.uid)?.name || target.nameSnapshot}</span>) : <p className="muted">{t("garrison.noSelectedAllies")}</p>}</div><div className="garrison-section-heading"><strong>{t("garrison.targetPriority")}</strong><span>{t("garrison.dragHint")}</span></div><div className="garrison-priority-list">{value.targets.map((target, index) => <div className="garrison-priority-item" key={`${target.kind}:${target.buildId || target.uid}`}><span>{index + 1}</span><strong>{target.nameSnapshot}</strong></div>)}</div>{value.targets.length === 0 ? <span className="status-error">{t("garrison.targetRequired")}</span> : null}</div><div className="garrison-runtime-panel"><div className="garrison-section-heading"><strong>{t("garrison.squadPriority")}</strong><span>{value.squadPriority.join(" → ") || "-"}</span></div><div className="automation-compact-choice-group automation-squad-priority">{[1,2,3,4].map((number) => { const isSelected = value.squadPriority.includes(number); return <label className={`automation-squad-priority-item${isSelected ? " selected" : ""}`} key={number}><input type="checkbox" checked={isSelected} disabled={disabled} onChange={(event) => { const squadPriority = event.target.checked ? [...value.squadPriority, number].sort((a,b) => a-b) : value.squadPriority.filter((item) => item !== number); onChange({ ...value, enabled: value.enabled && squadPriority.length > 0, squadPriority }); }} /><span>{t("squad.number", { number })}</span></label>; })}</div>{value.squadPriority.length === 0 ? <span className="status-error">{t("garrison.squadRequired")}</span> : <p className="muted">{t("garrison.squadHint")}</p>}<div className="garrison-section-heading"><strong>{t("garrison.current")}</strong><span>{runtime.guardingCount}/{Math.min(value.squadPriority.length, value.targets.length)}</span></div><div className="garrison-assignment-list">{runtime.assignments.length ? runtime.assignments.map((assignment) => <div className="garrison-assignment" key={`${assignment.squadIndex}:${assignment.targetKey}`}><b>{assignment.squadIndex}</b><span><strong>{assignment.targetName}</strong><small>{t(`garrison.state.${assignment.state}`)}</small></span><em className={assignment.state}>{t(assignment.owned ? "garrison.automatic" : "garrison.manual")}</em></div>) : <p className="muted">{t("garrison.noAssignments")}</p>}</div></div></div>{runtime.lastError ? <p className="automation-error" role="alert">{t(runtime.lastError)}</p> : null}<div className="garrison-actions"><ToggleRow label={t("garrison.recallOnDisable")} checked={value.recallOnDisable !== false} disabled={disabled} onChange={(recallOnDisable) => onChange({ ...value, recallOnDisable })} /><button type="button" className="primary-action" disabled data-preview-action="presentation-only" title={t("status.gameDisconnectedDisabled")}>{t("garrison.runNow")}</button></div>{pickerOpen ? <div className="garrison-modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setPickerOpen(false); }}><section className="garrison-modal" role="dialog" aria-modal="true" aria-label={t("garrison.chooseAllies")}><div className="garrison-modal-heading"><strong>{t("garrison.chooseAllies")}</strong><button type="button" onClick={() => setPickerOpen(false)}>{t("common.cancel")}</button></div><input aria-label={t("garrison.searchAlly")} value={pickerSearch} onChange={(event) => setPickerSearch(event.target.value)} placeholder={t("garrison.searchAlly")} autoFocus /><div className="garrison-ally-list">{filtered.map((member) => <label className={member.online ? "" : "unavailable"} key={member.uid}><input type="checkbox" checked={draftAllies.has(member.uid)} onChange={(event) => setDraftAllies((current) => { const next = new Set(current); if (event.target.checked) next.add(member.uid); else next.delete(member.uid); return next; })} /><span><strong>{member.name}</strong><small>{t("garrison.allyDetail", { level: member.level, power: Math.round(member.power / 1000) })} · {t(member.online ? "garrison.online" : "garrison.offlineMember")}</small></span></label>)}</div><div className="garrison-modal-actions"><button type="button" className="primary-action" onClick={() => { const retained = value.targets.filter((target) => target.kind !== "allyCity"); const allies = members.filter((member) => draftAllies.has(member.uid)).map((member) => ({ kind: "allyCity", uid: member.uid, nameSnapshot: member.name })); onChange({ ...value, targets: [...retained, ...allies] }); setPickerOpen(false); }}>{t("squad.join.confirmMembers")}</button></div></section></div> : null}</section>;
}

function ZombieBusPreviewSettings({ previewState = "" }) {
  const { t } = useI18n();
  const runtime = previewZombieBusRuntime(previewState);
  return <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("zombieBus.title")}</strong></div><p className="muted">{t("zombieBus.description")}</p>{runtime.assignments.length ? <div className="zombie-bus-assignments"><table><thead><tr>{["squad","ally","bus","status"].map((key) => <th key={key}>{t(`zombieBus.${key}`)}</th>)}</tr></thead><tbody>{runtime.assignments.map((row) => <tr key={row.squadIndex}><td>{row.squadIndex}</td><td>{row.name || row.uid}</td><td>{t(row.gold === true ? "zombieBus.gold" : row.gold === false ? "zombieBus.normal" : "zombieBus.unknown")}</td><td>{t(`zombieBus.state.${row.state}`)}</td></tr>)}</tbody></table></div> : null}{runtime.lastError ? <p className="automation-error" role="alert">{t(runtime.lastError)}</p> : null}</section>;
}

function validAfkToolbarConfig(config) {
  return Number.isInteger(config.minStamina) && config.minStamina >= 0 && config.minStamina <= 9999
    && typeof config.preferFifty === "boolean"
    && (!config.allianceDrill.enabled || config.allianceDrill.squadIndexes.length > 0)
    && validJoinRestrictions(config.allianceDrill.joinRestrictions)
    && (!config.garrison.enabled || (config.garrison.targets.length > 0 && config.garrison.squadPriority.length > 0));
}

function initialAfkProfiles(previewState) {
  const steel = makePreviewAfkProfile("steel", "Fixture Steel Hunt", "farm", "steel");
  const gold = { ...makePreviewAfkProfile("gold", "Fixture Gold Hunt", "farm", "gold"), squadIndexes: [2], minLevel: 3, maxLevel: 8, levelFilterEnabled: true };
  if (previewState === "squads-profile-range-warning") return [{ ...makePreviewAfkProfile("steel", "Fixture Range Warning", "farm", "steel"), minLevel: 11, maxLevel: 12, levelFilterEnabled: true }];
  if (previewState === "squads-profile-target-undiscovered") return [{ ...steel, monsterNameKey: "fixture-missing-target" }];
  if (previewState.startsWith("squads-profile-members-")) {
    const join = makePreviewAfkProfile("join", "Fixture Rally Join", "join", "boss");
    const leaders = previewState === "squads-profile-members-left" ? [{ uid: "19999", name: "Fixture Departed" }]
      : previewState === "squads-profile-members-self" ? [{ uid: "10000", name: "Fixture Self" }]
      : [{ uid: "10001", name: "Fixture Avery" }];
    return [{ ...join, joinRestrictions: { ...join.joinRestrictions, enabled: true, leaderListMode: "whitelist", leaders } }];
  }
  return [steel, gold];
}

function AfkContent({ previewEnabled, previewState }) {
  const { t } = useI18n();
  const [showEditor, setShowEditor] = useState(previewEnabled);
  const config = usePreviewConfig(() => previewEnabled ? initialAfkProfiles(previewState) : [], (profiles) => profiles.every(previewAfkProfileValid), previewState === "squads-profile-save-error", `afk:${previewState}`);
  const toolbar = usePreviewConfig(() => initialAfkToolbarConfig(previewState), validAfkToolbarConfig, previewState === "squads-profile-toolbar-save-error", `afk-toolbar:${previewState}`);
  const profiles = config.draft;
  const setProfiles = (value, delay = 400) => config.store.edit(value, delay);
  const [editingId, setEditingId] = useState(() => previewEnabled ? initialAfkProfiles(previewState)[0]?.id || "" : "");
  const [draggedProfileId, setDraggedProfileId] = useState("");
  const [addMenuOpen, setAddMenuOpen] = useState(false);
  const [toolbarPanel, setToolbarPanel] = useState(() => previewState.includes("potion") ? "potion" : previewState.includes("drill") ? "drill" : previewState.includes("garrison") ? "garrison" : previewState.includes("zombie") ? "zombieBus" : null);
  const newProfileCounter = useRef(1);
  const editingProfile = profiles.find((profile) => profile.id === editingId) || null;
  const updateProfile = (nextProfile) => setProfiles((current) => current.map((profile) => profile.id === nextProfile.id ? nextProfile : profile));
  const addProfile = (kind) => { const id = `preview-new-${newProfileCounter.current++}`; const profile = makePreviewAfkProfile(id, t("squad.afkNewProfile"), kind); setProfiles((current) => [...current, profile]); setEditingId(id); setShowEditor(true); setAddMenuOpen(false); };
  const moveProfile = (sourceId, targetId) => setProfiles((current) => {
    const sourceIndex = current.findIndex((profile) => profile.id === sourceId);
    const targetIndex = current.findIndex((profile) => profile.id === targetId);
    if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) return current;
    const next = [...current];
    const [moved] = next.splice(sourceIndex, 1);
    next.splice(targetIndex, 0, moved);
    return next;
  });
  const flushToolbar = (next) => { toolbar.store.edit(next, false); toolbar.store.flush().catch(() => {}); };
  const updateToolbar = (mutator, immediate = true) => {
    const current = toolbar.store.getSnapshot().draft;
    const next = typeof mutator === "function" ? mutator(current) : mutator;
    if (immediate) flushToolbar(next); else toolbar.store.edit(next);
  };
  const garrisonRuntime = previewGarrisonRuntime(previewState);
  const zombieRuntime = previewZombieBusRuntime(previewState);
  const drillState = previewState === "squads-profile-drill-leading" ? "squad.afkAllianceDrillLeading" : previewState === "squads-profile-drill-joining" ? "squad.afkAllianceDrillJoining" : "squad.afkAllianceDrillWaiting";
  const drillOrder = toolbar.draft.allianceDrill.squadIndexes.join(" → ") || "-";
  const toggleToolbarPanel = (panel) => setToolbarPanel((current) => current === panel ? null : panel);
  return (
    <div className="monster-afk-layout" data-draft-dirty={config.dirty || toolbar.dirty} data-draft-saving={config.saving || toolbar.saving} data-preview-fixture={previewEnabled ? "squads-profile" : "runtime-config-unobserved"}>
      <PreviewConfigError config={config} t={t} />
      <PreviewConfigError config={toolbar} t={t} />
      <div className="monster-afk-toolbar">
        <CompactAfkCard title={t("squad.afkMaster")} description={t("squad.afkMasterDescription")} summary={t(toolbar.draft.masterEnabled ? "common.enabled" : "common.disabled")} enabled={toolbar.draft.masterEnabled} disabled={!previewEnabled} settingsOpen={false} onToggle={(masterEnabled) => updateToolbar((draft) => ({ ...draft, masterEnabled }))} />
        <CompactAfkCard title={t("automation.autoUsePotion")} description={t("automation.potionMonsterOnly")} summary={`${t("automation.minStamina")} ${toolbar.draft.minStamina}`} enabled={toolbar.draft.potionEnabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "potion"} onSettings={() => toggleToolbarPanel("potion")} onToggle={(potionEnabled) => updateToolbar((draft) => ({ ...draft, potionEnabled }))} />
        <CompactAfkCard title={t("squad.afkAllianceDrill")} description={t("squad.afkAllianceDrillDescription")} summary={toolbar.draft.allianceDrill.enabled && previewState.startsWith("squads-profile-drill-") ? `${t(drillState)} · ${drillOrder}` : `${t("squad.afkAllianceDrillOrder", { order: drillOrder })} · ${t(toolbar.draft.allianceDrill.activeRally ? "squad.afkAllianceDrillActive" : "squad.afkJoin")}`} enabled={toolbar.draft.allianceDrill.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "drill"} onSettings={() => toggleToolbarPanel("drill")} onToggle={(enabled) => { if (enabled && toolbar.draft.allianceDrill.squadIndexes.length === 0) { setToolbarPanel("drill"); return; } updateToolbar((draft) => ({ ...draft, allianceDrill: { ...draft.allianceDrill, enabled } })); }} />
        <CompactAfkCard title={t("garrison.title")} description={t("garrison.description")} summary={toolbar.draft.garrison.enabled ? t("garrison.summary", { active: garrisonRuntime.guardingCount, total: Math.min(toolbar.draft.garrison.squadPriority.length, toolbar.draft.garrison.targets.length) }) : t("common.disabled")} enabled={toolbar.draft.garrison.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "garrison"} onSettings={() => toggleToolbarPanel("garrison")} onToggle={(enabled) => { if (enabled && (!toolbar.draft.garrison.targets.length || !toolbar.draft.garrison.squadPriority.length)) { setToolbarPanel("garrison"); return; } updateToolbar((draft) => ({ ...draft, garrison: { ...draft.garrison, enabled } })); }} />
        <CompactAfkCard title={t("zombieBus.title")} description={t("zombieBus.description")} summary={previewEnabled ? t(zombieRuntime.lastError ? "common.failed" : zombieRuntime.assignments.length ? "automation.running" : toolbar.draft.zombieBus.enabled ? "zombieBus.waiting" : "common.disabled") : t("status.disconnected")} enabled={toolbar.draft.zombieBus.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "zombieBus"} onSettings={() => toggleToolbarPanel("zombieBus")} onToggle={(enabled) => updateToolbar((draft) => ({ ...draft, zombieBus: { ...draft.zombieBus, enabled } }))} />
      </div>
      {toolbarPanel === "potion" ? <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("automation.autoUsePotion")}</strong><span>{t("automation.potionMonsterOnly")}</span></div><div className="monster-afk-card-settings"><label><span>{t("automation.minStamina")}</span><input type="number" min="0" max="9999" step="1" value={Number.isNaN(toolbar.draft.minStamina) ? "" : toolbar.draft.minStamina} disabled={!previewEnabled} onChange={(event) => updateToolbar((draft) => ({ ...draft, minStamina: event.target.valueAsNumber }), false)} onBlur={() => toolbar.store.flush().catch(() => {})} /></label><label><input type="checkbox" checked={toolbar.draft.preferFifty} disabled={!previewEnabled} onChange={(event) => updateToolbar((draft) => ({ ...draft, preferFifty: event.target.checked }))} /><span>{t("automation.preferFifty")}</span></label></div></section> : null}
      {toolbarPanel === "drill" ? <AllianceDrillPreviewSettings disabled={!previewEnabled} value={toolbar.draft.allianceDrill} previewState={previewState} onChange={(allianceDrill) => updateToolbar((draft) => ({ ...draft, allianceDrill }))} /> : null}
      {toolbarPanel === "garrison" ? <GarrisonPreviewSettings disabled={!previewEnabled} value={toolbar.draft.garrison} previewState={previewState} onChange={(garrison) => updateToolbar((draft) => ({ ...draft, garrison }))} /> : null}
      {toolbarPanel === "zombieBus" ? <ZombieBusPreviewSettings previewState={previewState} /> : null}
      <section className="monster-afk-profiles">
        <div className="monster-section-title">
          <strong>{t("squad.afkProfiles")}</strong>
          <div className="monster-afk-add-control"><button type="button" aria-haspopup="menu" aria-expanded={addMenuOpen} disabled={!previewEnabled} onClick={() => setAddMenuOpen((open) => !open)}>{t("common.add")}</button>{addMenuOpen ? <div className="monster-afk-add-menu" role="menu"><button type="button" role="menuitem" onClick={() => addProfile("farm")}><strong>{t("squad.afkActiveAttack")}</strong><span>{t("squad.afkActionAttack")}</span></button><button type="button" role="menuitem" onClick={() => addProfile("join")}><strong>{t("squad.afkJoin")}</strong><span>{t("squad.autoJoinRally")}</span></button></div> : null}</div>
        </div>
        <div className="monster-afk-profile-list">
          {profiles.length ? profiles.map((profile) => { const target = (previewState === "squads-profile-target-undiscovered" ? [] : previewAfkTargets).find((entry) => entry.key === profile.targetKey) || null; const source = target?.source || (previewEnabled ? "undiscovered" : profile.source); const rangeWarning = profile.kind === "farm" && profile.searchable && previewAfkLevelOutOfRange(profile, target); return <article className={`monster-afk-profile-card ${editingId === profile.id ? "active" : ""} ${profile.enabled ? "" : "disabled"}`} key={profile.id} onDragOver={(event) => { if (draggedProfileId) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); moveProfile(draggedProfileId, profile.id); setDraggedProfileId(""); }}><label className={`monster-afk-enabled ${profile.enabled ? "is-enabled" : ""} ${profile.squadIndexes.length === 0 ? "is-disabled" : ""}`}><input type="checkbox" checked={profile.enabled} disabled={!previewEnabled || profile.squadIndexes.length === 0} onChange={(event) => updateProfile({ ...profile, enabled: event.target.checked })} /><span className="monster-afk-enabled-track" aria-hidden="true" /><span>{t(profile.enabled ? "common.enabled" : "common.disabled")}</span></label><button type="button" className="monster-afk-profile-select" aria-pressed={editingId === profile.id} onClick={() => { setEditingId(profile.id); setShowEditor(true); }}><span className="monster-afk-profile-heading"><strong>{profile.name}</strong><span className={`monster-afk-mode-badge ${profile.kind}`}>{t(profile.kind === "farm" ? "squad.afkActiveAttack" : "squad.afkJoin")}</span></span><span>{profile.levelFilterEnabled ? `${profile.minLevel}-${profile.maxLevel}` : t("squad.afkAnyLevel")} · {profile.distanceFilterEnabled ? profile.maxDistance : t("squad.afkAnyDistance")}</span>{profile.kind === "farm" && profile.searchable ? <span className={rangeWarning ? "monster-afk-level-warning" : ""}>{target?.attackMinLevel != null && target?.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: target.attackMinLevel, max: target.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}{rangeWarning ? ` · ${t("squad.afkLevelOutOfRange")}` : ""}</span> : null}<span>{`${t(source === "search" ? "squad.afkSearchable" : source === "map" ? "squad.afkLocalTarget" : "squad.afkUndiscovered")} · ${t(profile.kind === "join" ? "squad.afkJoin" : profile.action === "rally" ? "squad.afkActionRally" : "squad.afkActionAttack")} · ${t("squad.afkBoundSquads", { count: profile.squadIndexes.join(", ") || "-" })}`}</span></button><button type="button" className="danger" onClick={() => { setProfiles((current) => current.filter((entry) => entry.id !== profile.id)); if (editingId === profile.id) { setEditingId(""); setShowEditor(false); } }}>{t("common.delete")}</button><button type="button" className="monster-afk-profile-drag" draggable aria-label={t("squad.afkReorder", { name: profile.name })} title={t("squad.afkReorder", { name: profile.name })} onDragStart={() => setDraggedProfileId(profile.id)} onDragEnd={() => setDraggedProfileId("")} onKeyDown={(event) => { if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return; event.preventDefault(); const index = profiles.findIndex((entry) => entry.id === profile.id); const moveTarget = profiles[index + (event.key === "ArrowUp" ? -1 : 1)]; if (moveTarget) moveProfile(profile.id, moveTarget.id); }}>↕</button></article>; }) : <span className="muted">{t("squad.afkNoProfiles")}</span>}
        </div>
        {showEditor && editingProfile ? <AfkProfileEditor enabled={previewEnabled} isNew={!config.confirmed.some((profile) => profile.id === editingProfile.id)} profile={editingProfile} onProfileChange={updateProfile} previewState={previewState} /> : null}
      </section>
    </div>
  );
}

function SettingsGlyph() {
  return (
    <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

function CompactAfkCard({ title, description, summary, enabled, disabled = false, settingsOpen = false, onToggle, onSettings }) {
  const { t } = useI18n();
  return (
    <article className={`automation-card monster-afk-compact-card${onSettings ? " is-selectable" : ""}${settingsOpen ? " is-selected" : ""}`} title={description || title} role={onSettings ? "button" : undefined} tabIndex={onSettings ? 0 : undefined} onClick={onSettings} onKeyDown={(event) => { if (onSettings && (event.key === "Enter" || event.key === " ")) { event.preventDefault(); onSettings(); } }}>
      <div className="automation-card-header">
        <div><h3>{title}</h3><p>{summary}</p></div>
        <div className="monster-afk-compact-actions">
          {onSettings ? <button type="button" disabled={disabled} className={settingsOpen ? "active" : ""} aria-label={t("nav.settings")} aria-pressed={settingsOpen} onClick={(event) => { event.stopPropagation(); onSettings(); }}><SettingsGlyph /></button> : null}
          <label className={`monster-afk-compact-toggle ${enabled ? "enabled" : ""}`}>
            <input type="checkbox" disabled={disabled} checked={enabled} onClick={(event) => event.stopPropagation()} onChange={(event) => onToggle?.(event.target.checked)} aria-label={title} />
            <span className="monster-afk-master-track" aria-hidden="true" />
          </label>
        </div>
      </div>
    </article>
  );
}

function previewEquipmentPreset(number, t) {
  return {
    id: `preview-preset-${number}`,
    name: t("squad.presetDefaultName", { number }),
    squads: [1,2,3,4].map((squadIndex) => ({
      squadIndex,
      positions: [1,2,3,4,5].map((position) => ({
        position,
        equips: position <= 2 ? [
          { slot: 1, equipUuid: `${number}-${squadIndex}-${position}-1`, level: 40 - position, quality: position === 1 ? 5 : 4 },
          { slot: 2, equipUuid: `${number}-${squadIndex}-${position}-2`, level: 38 - position, quality: 4 },
        ] : [],
      })),
    })),
  };
}

function EquipmentContent({ previewEnabled, previewState = "" }) {
  const { t } = useI18n();
  const [presets, setPresets] = useState(() => previewEnabled ? [1,2,3,4].map((number) => previewEquipmentPreset(number, t)) : []);
  const [selectedPresetId, setSelectedPresetId] = useState(() => presets[0]?.id || "");
  const [dirtyPresetIds, setDirtyPresetIds] = useState(() => new Set());
  const [renameOpen, setRenameOpen] = useState(previewState === "squads-equipment-rename" || previewState === "squads-equipment-rename-busy");
  const [renameValue, setRenameValue] = useState(() => presets[0]?.name || "");
  const [dragged, setDragged] = useState(null);
  const [dropTarget, setDropTarget] = useState("");
  const [dropSuccess, setDropSuccess] = useState([]);
  const [toast, setToast] = useState("");
  const dialogBusy = previewState === "squads-equipment-rename-busy";
  const selectedPreset = presets.find((preset) => preset.id === selectedPresetId) || presets[0];
  const fixtureResult = previewState === "squads-equipment-result" ? { state: "partial", applied: 7, requested: 8, failedHeroName: "Hero 8", reason: "EQUIPMENT_STATE_CHANGED" } : null;
  const fixtureProgress = previewState === "squads-equipment-progress" ? { phase: "running", current: 3, total: 8, heroName: "Hero 3" } : null;
  const positionCount = selectedPreset?.squads.reduce((count, squad) => count + squad.positions.filter((position) => position.equips.length > 0).length, 0) || 0;
  const equipmentCount = selectedPreset?.squads.reduce((count, squad) => count + squad.positions.reduce((sum, position) => sum + position.equips.length, 0), 0) || 0;

  const updateSelectedPreset = (mutator) => {
    if (!selectedPreset) return;
    setPresets((current) => current.map((preset) => {
      if (preset.id !== selectedPreset.id) return preset;
      const next = structuredClone(preset);
      mutator(next);
      return next;
    }));
    setDirtyPresetIds((current) => new Set(current).add(selectedPreset.id));
  };

  const markDropSuccess = (keys) => {
    setDropSuccess(keys);
    window.setTimeout(() => setDropSuccess([]), 450);
  };

  const swapPositions = (target) => {
    if (!dragged || !selectedPreset || dragged.kind === "squad" || dragged.kind !== target.kind) return;
    if (dragged.kind === "equip" && dragged.slot !== target.slot) {
      setToast(t("squad.sameSlotRequired"));
      return;
    }
    const sourceKey = `${dragged.squadIndex}-${dragged.position}`;
    const targetKey = `${target.squadIndex}-${target.position}`;
    if (sourceKey === targetKey) return;
    updateSelectedPreset((preset) => {
      const sourceSquad = preset.squads.find((squad) => squad.squadIndex === dragged.squadIndex);
      const targetSquad = preset.squads.find((squad) => squad.squadIndex === target.squadIndex);
      const sourcePosition = sourceSquad?.positions.find((position) => position.position === dragged.position);
      const targetPosition = targetSquad?.positions.find((position) => position.position === target.position);
      if (!sourcePosition || !targetPosition) return;
      if (dragged.kind === "loadout") {
        [sourcePosition.equips, targetPosition.equips] = [targetPosition.equips, sourcePosition.equips];
        return;
      }
      const sourceIndex = sourcePosition.equips.findIndex((equip) => equip.slot === dragged.slot);
      if (sourceIndex < 0) return;
      const targetIndex = targetPosition.equips.findIndex((equip) => equip.slot === dragged.slot);
      const sourceEquip = sourcePosition.equips[sourceIndex];
      if (targetIndex >= 0) {
        const targetEquip = targetPosition.equips[targetIndex];
        sourcePosition.equips[sourceIndex] = targetEquip;
        targetPosition.equips[targetIndex] = sourceEquip;
      } else {
        sourcePosition.equips.splice(sourceIndex, 1);
        targetPosition.equips.push(sourceEquip);
        targetPosition.equips.sort((a, b) => a.slot - b.slot);
      }
    });
    markDropSuccess([sourceKey, targetKey]);
  };

  const swapSquads = (targetSquadIndex) => {
    if (dragged?.kind !== "squad" || dragged.squadIndex === targetSquadIndex) return;
    updateSelectedPreset((preset) => {
      const sourceSquad = preset.squads.find((squad) => squad.squadIndex === dragged.squadIndex);
      const targetSquad = preset.squads.find((squad) => squad.squadIndex === targetSquadIndex);
      if (!sourceSquad || !targetSquad) return;
      const count = Math.min(sourceSquad.positions.length, targetSquad.positions.length, 5);
      for (let index = 0; index < count; index += 1) {
        [sourceSquad.positions[index].equips, targetSquad.positions[index].equips] = [targetSquad.positions[index].equips, sourceSquad.positions[index].equips];
      }
    });
    markDropSuccess([`squad-${dragged.squadIndex}`, `squad-${targetSquadIndex}`]);
  };

  const openRename = () => {
    if (!selectedPreset) return;
    setRenameValue(selectedPreset.name);
    setRenameOpen(true);
  };

  const closeRename = () => {
    if (dialogBusy) return;
    setRenameOpen(false);
    setRenameValue("");
  };

  const saveRename = () => {
    const name = renameValue.trim();
    if (!name || !selectedPreset || dialogBusy) return;
    setPresets((current) => current.map((preset) => preset.id === selectedPreset.id ? { ...preset, name } : preset));
    setRenameOpen(false);
    setRenameValue("");
    setToast(t("squad.equipmentConfigSaved"));
  };

  const savePreviewConfig = () => {
    if (!selectedPreset) return;
    setDirtyPresetIds((current) => {
      const next = new Set(current);
      next.delete(selectedPreset.id);
      return next;
    });
    setToast(t("squad.equipmentConfigSaved"));
  };

  const resultText = fixtureResult ? t(
    fixtureResult.state === "success" ? "squad.equipmentApplySuccess" : fixtureResult.state === "partial" ? "squad.equipmentApplyPartial" : "squad.equipmentApplyRejected",
    { applied: fixtureResult.applied, requested: fixtureResult.requested, hero: fixtureResult.failedHeroName || "-", reason: fixtureResult.reason || "-" },
  ) : "";

  const progressText = fixtureProgress
    ? fixtureProgress.phase === "preparing"
      ? t("squad.equipmentApplyPreparing")
      : fixtureProgress.phase === "verifying"
        ? t("squad.equipmentApplyVerifying", { current: fixtureProgress.current, total: fixtureProgress.total })
        : t("squad.equipmentApplyRunning", { current: fixtureProgress.current, total: fixtureProgress.total, hero: fixtureProgress.heroName || "-" })
    : "";

  useEffect(() => {
    if (!toast) return undefined;
    const timer = window.setTimeout(() => setToast(""), 1800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  return (
    <div className="equipment-preset-layout" data-preview-fixture={previewEnabled ? previewState || "squads-equipment" : "no-equipment-presets"}>
      <aside className="equipment-preset-rail">
        <strong>{t("squad.equipmentPresets")}</strong>
        <div className="equipment-preset-list">{presets.length ? presets.map((preset, index) => <button type="button" key={preset.id} className={preset.id === selectedPreset?.id ? "active" : ""} onClick={() => setSelectedPresetId(preset.id)}><span>{preset.name}{dirtyPresetIds.has(preset.id) ? " *" : ""}</span><small>{index < 4 ? `Alt+${index + 1} · ` : ""}{t("squad.presetSummary", { positions: preset.squads.reduce((count, squad) => count + squad.positions.filter((position) => position.equips.length > 0).length, 0), equips: preset.squads.reduce((count, squad) => count + squad.positions.reduce((sum, position) => sum + position.equips.length, 0), 0) })}</small></button>) : <span className="muted">{t("squad.noEquipmentPresets")}</span>}</div>
      </aside>
      <main className="equipment-preset-main">
        <div className="equipment-preset-toolbar">
          <div><strong>{selectedPreset?.name || t("squad.noEquipmentPresets")}</strong>{selectedPreset ? <span>{t("squad.allSquadPresetSummary", { positions: positionCount, equips: equipmentCount })}{dirtyPresetIds.has(selectedPreset.id) ? ` · ${t("squad.equipmentConfigUnsaved")}` : ""}</span> : null}<span className="equipment-current-config">{t("squad.currentEquipmentPreset", { name: t("squad.unmatchedEquipmentPreset") })}</span></div>
          <div className="equipment-preset-actions">
            <button type="button" disabled={!selectedPreset || dialogBusy} onClick={openRename}>{t("common.rename")}</button>
            <button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("squad.loadCurrentEquipment")}</button>
            <button type="button" disabled={!selectedPreset || dialogBusy} onClick={savePreviewConfig}>{t("squad.saveEquipmentConfig")}</button>
            <button type="button" className="primary" disabled title={t("status.gameDisconnectedDisabled")}>{t("squad.saveAndApplyEquipmentConfig")}</button>
          </div>
        </div>
        {selectedPreset ? <div className="equipment-preset-squads">{selectedPreset.squads.map((squad) => {
          const squadKey = `squad-${squad.squadIndex}`;
          const squadPositions = squad.positions.filter((position) => position.equips.length > 0).length;
          const squadEquips = squad.positions.reduce((sum, position) => sum + position.equips.length, 0);
          return <section className={`equipment-preset-squad ${dropTarget === squadKey ? "drop-target" : ""} ${dropSuccess.includes(squadKey) ? "drop-success" : ""}`} key={squad.squadIndex} onDragOver={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); setDropTarget(squadKey); } }} onDragLeave={() => setDropTarget((current) => current === squadKey ? "" : current)} onDrop={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); swapSquads(squad.squadIndex); setDropTarget(""); setDragged(null); } }}>
            <div className="equipment-preset-squad-header"><div><strong>{t("squad.number", { number: squad.squadIndex })}</strong><span>{t("squad.positionEquipmentCount", { positions: squadPositions, equips: squadEquips })}</span><span className="equipment-current-config">{t("squad.currentEquipmentPreset", { name: t("squad.unmatchedEquipmentPreset") })}</span></div><div className="equipment-preset-actions"><div className="equipment-squad-drag-handle" draggable={previewEnabled} onDragStart={() => setDragged({ kind: "squad", squadIndex: squad.squadIndex })} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>{t("squad.dragSquadLoadout")}</div><button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("squad.applySquad")}</button></div></div>
            <div className="equipment-preset-positions">{squad.positions.map((position) => {
              const positionKey = `${squad.squadIndex}-${position.position}`;
              const loadoutKey = `loadout-${positionKey}`;
              return <article className={`equipment-position-card ${dropTarget === loadoutKey ? "drop-target" : ""} ${dropSuccess.includes(positionKey) ? "drop-success" : ""}`} key={position.position} onDragOver={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); setDropTarget(loadoutKey); } }} onDragLeave={() => setDropTarget((current) => current === loadoutKey ? "" : current)} onDrop={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); swapPositions({ kind: "loadout", squadIndex: squad.squadIndex, position: position.position }); setDropTarget(""); setDragged(null); } }}>
                <div className="equipment-position-header"><strong>{t("squad.position", { number: position.position })}</strong><span>{t("squad.heroFixed")}</span></div>
                <div className="equipment-position-hero"><span className="equipment-position-hero-icon game-asset-placeholder" /><div><strong>Hero {squad.squadIndex}-{position.position}</strong><span>Lv.{30 - position.position}</span></div></div>
                <div className="equipment-loadout-handle" draggable={previewEnabled} onDragStart={() => setDragged({ kind: "loadout", squadIndex: squad.squadIndex, position: position.position })} onDragEnd={() => { setDragged(null); setDropTarget(""); }}><span>{t("squad.dragLoadout")}</span></div>
                <div className="equipment-position-items">{[1,2,3,4].map((slot) => {
                  const equip = position.equips.find((item) => item.slot === slot);
                  const equipTarget = `equip-${squad.squadIndex}-${position.position}-${slot}`;
                  return <div className={`preset-equipment-slot quality-${equip?.quality || 0} ${dropTarget === equipTarget ? "drop-target" : ""}`} key={slot} draggable={!!equip && previewEnabled} title={t(`squad.equipmentSlot${slot}`)} onDragStart={(event) => { if (!equip) return; event.stopPropagation(); setDragged({ kind: "equip", squadIndex: squad.squadIndex, position: position.position, slot }); }} onDragOver={(event) => { if (dragged?.kind === "equip" && dragged.slot === slot) { event.preventDefault(); event.stopPropagation(); setDropTarget(equipTarget); } }} onDragLeave={() => setDropTarget((current) => current === equipTarget ? "" : current)} onDrop={(event) => { if (dragged?.kind === "equip" && dragged.slot === slot) { event.preventDefault(); event.stopPropagation(); swapPositions({ kind: "equip", squadIndex: squad.squadIndex, position: position.position, slot }); setDropTarget(""); setDragged(null); } }} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>{equip ? <><span className="equipment-icon game-asset-placeholder" /><span>Lv.{equip.level ?? "-"}</span></> : <><span className="equipment-icon game-asset-placeholder" /><span>{t("squad.emptyEquipment")}</span></>}</div>;
                })}</div>
              </article>;
            })}</div>
          </section>;
        })}</div> : <div className="map-empty">{t("squad.createFirstPreset")}</div>}
        <div className="equipment-preset-hint">
          <span>{t("squad.dragEquipmentHint")}</span>
          <strong>{t("squad.quickShortcutHint")}</strong>
        </div>
        {fixtureResult ? <div className={`equipment-result equipment-result-${fixtureResult.state}`}>{resultText}</div> : null}
      </main>
      {renameOpen ? <div className="equipment-preset-dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) closeRename(); }}><div className="equipment-preset-dialog" role="dialog" aria-modal="true" aria-labelledby="equipment-preset-title"><strong id="equipment-preset-title">{t("common.rename")}</strong><label>{t("squad.presetNamePrompt")}<input autoFocus disabled={dialogBusy} value={renameValue} onChange={(event) => setRenameValue(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") saveRename(); if (event.key === "Escape") closeRename(); }} /></label><div className="equipment-preset-actions"><button type="button" disabled={dialogBusy} onClick={closeRename}>{t("common.cancel")}</button><button type="button" className="primary" disabled={!renameValue.trim() || dialogBusy} onClick={saveRename}>{t("common.saveConfig")}</button></div></div></div> : null}
      {toast ? <div className="equipment-toast" role="status">{toast}</div> : null}
      {fixtureProgress ? <div className="equipment-apply-progress" role="status"><strong>{progressText}</strong><progress max={Math.max(1, fixtureProgress.total)} value={fixtureProgress.total > 0 ? fixtureProgress.current : undefined} /></div> : null}
    </div>
  );
}

const CITY_PREVIEW_BUILDINGS = Object.freeze([
  { id: 1, name: "HQ", level: 30, x: 2, y: 2, width: 2, height: 2, movable: false },
  { id: 2, name: "Barracks", level: 28, x: 5, y: 2, width: 2, height: 2, movable: true },
  { id: 3, name: "Hospital", level: 27, x: 3, y: 5, width: 2, height: 1, movable: true },
]);

function sameCityLayout(a, b) {
  return a.length === b.length && a.every((building, index) => building.id === b[index]?.id && building.x === b[index]?.x && building.y === b[index]?.y);
}

function cityBoxesOverlap(a, b) {
  return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
}

function cityCellKind(x, y) {
  if (y === 8) return "locked";
  if (x === 1) return "road";
  if (y === 4 && x >= 7) return "flag-only";
  return "available";
}

function cityLayoutConflicts(buildings) {
  const conflicts = [];
  for (let index = 0; index < buildings.length; index += 1) {
    const building = buildings[index];
    if (building.x < 1 || building.y < 1 || building.x + building.width - 1 > 8 || building.y + building.height - 1 > 8) conflicts.push(building.id);
    if (building.movable) {
      for (let y = building.y; y < building.y + building.height; y += 1) {
        for (let x = building.x; x < building.x + building.width; x += 1) {
          if (cityCellKind(x, y) !== "available") conflicts.push(building.id);
        }
      }
    }
    for (let otherIndex = index + 1; otherIndex < buildings.length; otherIndex += 1) {
      if (cityBoxesOverlap(building, buildings[otherIndex])) {
        conflicts.push(building.id, buildings[otherIndex].id);
      }
    }
  }
  return [...new Set(conflicts)];
}

export function CityLayoutPage({ previewState = "" }) {
  const { t } = useI18n();
  const previewEnabled = previewState.startsWith("city-layout-populated");
  const [zoom, setZoom] = useState(24);
  const [buildings, setBuildings] = useState(() => CITY_PREVIEW_BUILDINGS.map((building) => previewState === "city-layout-populated-conflict" && building.id === 3 ? { ...building, x: 5, y: 2 } : { ...building }));
  const [selectedIds, setSelectedIds] = useState(() => new Set(previewEnabled ? [1] : []));
  const [history, setHistory] = useState({ past: [], future: [] });
  const [dragAnchorId, setDragAnchorId] = useState(null);
  const [selectionBox, setSelectionBox] = useState(null);
  const gridRef = useRef(null);
  const cells = Array.from({ length: 64 }, (_, index) => index);
  const selected = buildings.find((building) => selectedIds.has(building.id)) || null;
  const changedIds = new Set(buildings.filter((building) => {
    const initial = CITY_PREVIEW_BUILDINGS.find((entry) => entry.id === building.id);
    return initial && (initial.x !== building.x || initial.y !== building.y);
  }).map((building) => building.id));
  const conflictIds = cityLayoutConflicts(buildings);
  const hasChanges = changedIds.size > 0;

  const commitBuildings = (next) => {
    if (sameCityLayout(buildings, next)) return;
    setHistory((current) => ({ past: [...current.past, buildings.map((building) => ({ ...building }))], future: [] }));
    setBuildings(next.map((building) => ({ ...building })));
  };

  const undo = () => {
    setHistory((current) => {
      if (!current.past.length) return current;
      const previous = current.past[current.past.length - 1];
      setBuildings(previous.map((building) => ({ ...building })));
      return { past: current.past.slice(0, -1), future: [buildings.map((building) => ({ ...building })), ...current.future] };
    });
  };

  const redo = () => {
    setHistory((current) => {
      if (!current.future.length) return current;
      const next = current.future[0];
      setBuildings(next.map((building) => ({ ...building })));
      return { past: [...current.past, buildings.map((building) => ({ ...building }))], future: current.future.slice(1) };
    });
  };

  const moveSelection = (anchorId, x, y) => {
    const anchor = buildings.find((building) => building.id === anchorId);
    if (!anchor?.movable) return;
    const movingIds = selectedIds.has(anchorId)
      ? new Set([...selectedIds].filter((id) => buildings.find((building) => building.id === id)?.movable))
      : new Set([anchorId]);
    const dx = x - anchor.x;
    const dy = y - anchor.y;
    const next = buildings.map((building) => movingIds.has(building.id) ? { ...building, x: building.x + dx, y: building.y + dy } : { ...building });
    if (cityLayoutConflicts(next).length === 0) commitBuildings(next);
  };

  const finishSelectionBox = (event) => {
    if (!selectionBox || !gridRef.current) return;
    const left = Math.min(selectionBox.startX, event.clientX);
    const right = Math.max(selectionBox.startX, event.clientX);
    const top = Math.min(selectionBox.startY, event.clientY);
    const bottom = Math.max(selectionBox.startY, event.clientY);
    const hits = [...gridRef.current.querySelectorAll("[data-city-building-id]")].filter((element) => {
      const rect = element.getBoundingClientRect();
      return rect.left < right && rect.right > left && rect.top < bottom && rect.bottom > top;
    }).map((element) => Number(element.dataset.cityBuildingId));
    setSelectedIds((current) => selectionBox.additive ? new Set([...current, ...hits]) : new Set(hits));
    setSelectionBox(null);
  };

  useEffect(() => {
    if (!previewEnabled) return undefined;
    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        setSelectedIds(new Set());
        return;
      }
      if (!(event.ctrlKey || event.metaKey)) return;
      if (event.key.toLowerCase() === "z") {
        event.preventDefault();
        if (event.shiftKey) redo();
        else undo();
      } else if (event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  if (!previewEnabled) return <div className="panel city-layout-empty">{t("cityLayout.offline")}</div>;
  return (
    <section className="panel city-layout-panel" data-preview-fixture="city-layout-populated">
      <div className="city-layout-header">
        <div><h2>{t("cityLayout.title")}</h2><span>{t("cityLayout.stats", { cells: 64, buildings: buildings.length, movable: buildings.filter((item) => item.movable).length })}</span></div>
        <div className="city-layout-actions">
          <button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("common.refresh")}</button>
          <button type="button" disabled={!history.past.length} onClick={undo}>{t("cityLayout.undo")}</button>
          <button type="button" disabled={!history.future.length} onClick={redo}>{t("cityLayout.redo")}</button>
          <button type="button" disabled={!hasChanges} onClick={() => commitBuildings(CITY_PREVIEW_BUILDINGS)}>{t("cityLayout.restoreInitial")}</button>
          <button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("cityLayout.saveDraft")}</button>
          <button type="button" className="primary" disabled title={t("status.gameDisconnectedDisabled")}>{t("cityLayout.apply")}</button>
        </div>
      </div>
      <div className="city-layout-workbench">
        <main className="city-layout-center">
          <div className="city-layout-main-heading">
            <div className="city-layout-zoom" aria-label={t("cityLayout.zoom")}>
              <button type="button" onClick={() => setZoom((value) => Math.max(16, value - 2))} aria-label={t("cityLayout.zoomOut")}>−</button>
              <span>{zoom}px</span>
              <button type="button" onClick={() => setZoom((value) => Math.min(40, value + 2))} aria-label={t("cityLayout.zoomIn")}>+</button>
              <button type="button" onClick={() => setZoom(24)}>{t("cityLayout.fitCanvas")}</button>
            </div>
            <span className="city-layout-name-preview">{selected?.name || t("cityLayout.selectBuilding")}</span>
          </div>
          <details className="city-layout-controls"><summary>{t("cityLayout.controls.title")}</summary><div>{t("cityLayout.controls.boxSelect")} · {t("cityLayout.controls.addSelect")} · {t("cityLayout.controls.toggle")} · {t("cityLayout.controls.move")}</div><small>{t("cityLayout.controls.history")}</small></details>
          <div className="city-layout-grid-viewport">
            <div
              ref={gridRef}
              className="city-layout-grid"
              style={{ gridTemplateColumns: "repeat(8, " + zoom + "px)", gridAutoRows: zoom + "px", position: "relative" }}
              onPointerDown={(event) => {
                const canStart = event.target === event.currentTarget || (event.target instanceof HTMLElement && event.target.dataset.cityCell != null) || event.shiftKey;
                if (!canStart || event.button !== 0) return;
                event.currentTarget.setPointerCapture?.(event.pointerId);
                setSelectionBox({ startX: event.clientX, startY: event.clientY, currentX: event.clientX, currentY: event.clientY, additive: event.shiftKey });
              }}
              onPointerMove={(event) => {
                if (selectionBox) setSelectionBox((current) => current ? { ...current, currentX: event.clientX, currentY: event.clientY } : current);
              }}
              onPointerUp={finishSelectionBox}
              onDragOver={(event) => { if (dragAnchorId) event.preventDefault(); }}
              onDrop={(event) => {
                if (!dragAnchorId || !gridRef.current) return;
                event.preventDefault();
                const rect = gridRef.current.getBoundingClientRect();
                const x = Math.max(1, Math.min(8, Math.floor((event.clientX - rect.left) / zoom) + 1));
                const y = Math.max(1, Math.min(8, Math.floor((event.clientY - rect.top) / zoom) + 1));
                moveSelection(dragAnchorId, x, y);
                setDragAnchorId(null);
              }}
            >
              {cells.map((index) => {
                const x = index % 8 + 1;
                const y = Math.floor(index / 8) + 1;
                return <span
                  key={index}
                  data-city-cell={index}
                  className={"city-layout-cell " + cityCellKind(x, y)}
                  style={{ gridColumn: x, gridRow: y }}
                />;
              })}
              {buildings.map((building) => <button
                key={building.id}
                data-city-building-id={building.id}
                type="button"
                draggable={building.movable}
                className={"city-layout-building" + (selectedIds.has(building.id) ? " selected" : "") + (changedIds.has(building.id) ? " changed" : "")}
                style={{ gridColumn: building.x + " / span " + building.width, gridRow: building.y + " / span " + building.height }}
                onClick={(event) => setSelectedIds((current) => {
                  if (event.ctrlKey || event.metaKey) {
                    const next = new Set(current);
                    if (next.has(building.id)) next.delete(building.id);
                    else next.add(building.id);
                    return next;
                  }
                  return new Set([building.id]);
                })}
                onDragStart={() => {
                  if (!selectedIds.has(building.id)) setSelectedIds(new Set([building.id]));
                  setDragAnchorId(building.id);
                }}
                onDragEnd={() => setDragAnchorId(null)}
              ><span className="city-layout-building-name">{building.name}</span><span className="city-layout-building-level">{t("cityLayout.level", { level: building.level })}</span></button>)}
              {selectionBox ? <span className="city-layout-selection-box" aria-hidden="true" style={{ position: "fixed", pointerEvents: "none", zIndex: 20, left: Math.min(selectionBox.startX, selectionBox.currentX), top: Math.min(selectionBox.startY, selectionBox.currentY), width: Math.abs(selectionBox.currentX - selectionBox.startX), height: Math.abs(selectionBox.currentY - selectionBox.startY), border: "1px solid currentColor", background: "rgba(80, 130, 255, 0.12)" }} /> : null}
            </div>
          </div>
          <div className="city-layout-legend"><span className="available">{t("cityLayout.cell.available")}</span><span className="road">{t("cityLayout.cell.road")}</span><span className="flag-only">{t("cityLayout.cell.flagOnly")}</span><span className="locked">{t("cityLayout.cell.locked")}</span></div>
        </main>
        <aside className="city-layout-inspector">
          <strong>{t("cityLayout.properties")}</strong>
          {selected ? <div className="city-layout-selected"><div><strong>{selected.name}</strong><span>{t("cityLayout.level", { level: selected.level })}</span></div><dl><dt>{t("cityLayout.footprint")}</dt><dd>{selected.width}×{selected.height}</dd><dt>{t("cityLayout.current")}</dt><dd>{(() => { const initial = CITY_PREVIEW_BUILDINGS.find((building) => building.id === selected.id); return (initial?.x ?? selected.x) + "," + (initial?.y ?? selected.y); })()}</dd><dt>{t("cityLayout.target")}</dt><dd>{selected.x},{selected.y}</dd><dt>{t("cityLayout.movable")}</dt><dd>{t(selected.movable ? "common.yes" : "common.no")}</dd><dt>{t("cityLayout.rule")}</dt><dd>{t(selected.movable ? "cityLayout.rule.normal" : "cityLayout.rule.flag")}</dd></dl></div> : <span className="muted">{t("cityLayout.selectBuilding")}</span>}
          <div className={"city-layout-validation " + (conflictIds.length ? "invalid" : "valid")}>{conflictIds.length ? t("cityLayout.conflicts", { count: conflictIds.length }) : t("cityLayout.validation.valid")}</div>
          <div className="city-layout-changes"><strong>{hasChanges ? t("cityLayout.changes", { count: changedIds.size }) : t("cityLayout.noChanges")}</strong>{hasChanges ? <button type="button" onClick={undo}>{t("cityLayout.undo")}</button> : null}</div>
        </aside>
      </div>
      <footer className="city-layout-footer"><span>{t("cityLayout.draftRevision", { revision: 1 })}</span><span>{t("cityLayout.conflicts", { count: conflictIds.length })}</span></footer>
    </section>
  );
}

const hotkeyCards = [
  ["Q / W / E / R", "Attack target", "Q/W/E/R each send one march at whatever is under the cursor: enemy city, ally city reinforcement, monster, resource node, or city ruin. World bosses and rally monsters are skipped. A/S/D/F recalls."],
  ["A / S / D / F", "Recall squad", "Recall squads 1–4 to the Headquarters."],
  ["Space", "Shield countdown", "Hold Space to display remaining Shield time above protected cities."],
  ["F6 / F7 / F8", "Use Shield", "F6 uses an 8-hour Shield, F7 a 12-hour Shield, and F8 a 24-hour Shield.", "This shortcut consumes the matching Shield item immediately."],
  ["Alt + 1～4", "Equipment preset", "Apply equipment presets 1–4 to all configured squads."],
  ["F9", "Random relocation", "Press F9 to use a Random Relocator.", "Relocation may consume an item. This shortcut is disabled by default."],
  ["F10", "Alliance relocation", "Press F10 to relocate to the Alliance rally point.", "Relocation may consume an item. This shortcut is disabled by default."],
];

function HotkeyCard({ binding, title, description, warning, attack = false, previewEnabled = false }) {
  const { english, t } = useI18n();
  const [enabled, setEnabled] = useState(false);
  const [itemSpeedup, setItemSpeedup] = useState(false);
  const [diamondSpeedup, setDiamondSpeedup] = useState(false);
  return (
    <article className={`hotkey-card${warning ? " hotkey-card-danger" : ""}`} data-preview-fixture={previewEnabled ? "hotkeys-connected" : "runtime-config-unobserved"}>
      <div className="hotkey-card-header">
        <kbd className="hotkey-binding">{binding}</kbd>
        <button className="hotkey-switch" type="button" disabled={!previewEnabled} aria-label={`${english(title)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
      </div>
      <h3>{english(title)}</h3>
      <p>{english(description)}</p>
      {attack ? (
        <div className="hotkey-attack-settings">
          <label className="hotkey-attack-toggle"><input type="checkbox" disabled={!previewEnabled || !enabled} checked={itemSpeedup} onChange={(event) => setItemSpeedup(event.target.checked)} /><span>{t("hotkeys.attackSpeedupItem")}</span></label>
          <label className="hotkey-attack-toggle"><input type="checkbox" disabled={!previewEnabled || !enabled} checked={diamondSpeedup} onChange={(event) => setDiamondSpeedup(event.target.checked)} /><span>{t("hotkeys.attackSpeedupDiamond")}</span></label>
          <small>{t("hotkeys.attackSpeedupWarning")}</small>
        </div>
      ) : null}
      <span className={`hotkey-state${enabled ? " enabled" : ""}`}>{t(enabled ? "common.enabled" : "common.disabled")}</span>
      {warning ? <div className="hotkey-danger">{english(warning)}</div> : null}
    </article>
  );
}

export function HotkeysPage({ previewState = "" }) {
  const { t } = useI18n();
  const previewEnabled = previewState === "hotkeys-connected" || previewState === "hotkeys-save-error";
  const hotkeyError = previewState === "hotkeys-load-error" ? t("hotkeys.loadFailed") : previewState === "hotkeys-save-error" ? t("hotkeys.saveFailed") : "";
  return (
    <section className="panel hotkey-panel" data-preview-fixture={previewState.startsWith("hotkeys-") ? previewState : undefined}>
      <PanelTitle title={t("hotkeys.title")} subtitle={t("hotkeys.description")} />
      {!previewEnabled ? <div className="hotkey-status">{t("hotkeys.offlineHint")}</div> : null}
      {hotkeyError ? <div className="automation-error" role="alert">{hotkeyError}</div> : null}
      <div className="hotkey-grid">
        {hotkeyCards.map(([binding, title, description, warning], index) => (
          <HotkeyCard key={title} binding={binding} title={title} description={description} warning={warning} attack={index === 0} previewEnabled={previewEnabled} />
        ))}
      </div>
    </section>
  );
}

export function MiniGamesPage({ previewState = "" }) {
  const { t } = useI18n();
  const previewEnabled = previewState.startsWith("mini-games-");
  const [chestEnabled, setChestEnabled] = useState(false);
  const foodRunning = previewState === "mini-games-active" || previewState === "mini-games-executing";
  const foodStatusKey = {
    "mini-games-solving": "miniGames.sheep.solving",
    "mini-games-executing": "miniGames.sheep.executing",
    "mini-games-complete": "miniGames.sheep.dailyLimit",
    "mini-games-all-complete": "miniGames.sheep.allCompleted",
    "mini-games-activity-ended": "miniGames.sheep.activityEnded",
    "mini-games-ui-open": "miniGames.sheep.uiOpen",
    "mini-games-conflict": "miniGames.sheep.conflict",
    "mini-games-solve-failed": "miniGames.sheep.solveFailed",
    "mini-games-unsupported": "miniGames.sheep.unsupported",
    "mini-games-start-failed": "miniGames.sheep.failed",
  }[previewState];
  const landResult = previewState === "mini-games-land-success"
    ? t("miniGames.landCellSent", { id: 17 })
    : previewState === "mini-games-land-error"
      ? t("miniGames.landCellFailed")
      : "";
  return (
    <section className="panel hotkey-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("miniGames.title")} subtitle={t("miniGames.description")} />
      {!previewEnabled ? <div className="hotkey-status">{t("hotkeys.offlineHint")}</div> : null}
      <div className="hotkey-grid">
        <HotkeyCard binding="G" title="Frontline reinforcement" description="Press G after a Frontline Breakthrough battle starts to add 5 soldiers each time." previewEnabled={previewEnabled} />
        <article className="hotkey-card">
          <h3>{t("miniGames.treasureChest.title")}</h3>
          <p>{t("miniGames.treasureChest.description")}</p>
          <ToggleRow label={t("miniGames.treasureChest.enabled")} checked={chestEnabled} disabled={!previewEnabled} onChange={setChestEnabled} />
        </article>
        <article className="hotkey-card">
          <h3>{t("miniGames.landCell.title")}</h3>
          <p>{t("miniGames.landCell.description")}</p>
          <div className="mini-game-actions"><button className="primary" type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("miniGames.landCellAction")}</button></div>
          {landResult ? <span className={"hotkey-state" + (previewState === "mini-games-land-success" ? " enabled" : "")}>{landResult}</span> : null}
        </article>
        <article className="hotkey-card">
          <h3>{t("miniGames.sheep.title")}</h3>
          <p>{t("miniGames.sheep.description")}</p>
          {previewEnabled ? <span className="hotkey-state">{t("miniGames.sheep.level", { level: 4 })}</span> : null}
          {previewEnabled ? <span className="hotkey-state">{t("miniGames.sheep.elapsed", { time: foodRunning ? "00:31" : "00:00" })}</span> : null}
          <span className={"hotkey-state" + (previewState === "mini-games-complete" || previewState === "mini-games-all-complete" ? " enabled" : "")}>{foodStatusKey ? t(foodStatusKey) : foodRunning ? t("miniGames.sheep.progress", { confirmed: 18, total: 42 }) : t("common.stopped")}</span>
          <div className="mini-game-actions"><button className={foodRunning ? "danger" : "primary"} type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t(foodRunning ? "common.stop" : "common.start")}</button></div>
        </article>
      </div>
    </section>
  );
}

export function SettingsPage({ previewState = "" }) {
  const { t } = useI18n();
  const previewEnabled = previewState.startsWith("settings-");
  const showProfileFocus = previewState === "settings-multiprofile" || previewState === "settings-complete";
  const [showFps, setShowFps] = useState(false);
  const [showPing, setShowPing] = useState(false);
  const [focusGame, setFocusGame] = useState(true);
  const visualError = previewState === "settings-visual-error";
  const updateError = previewState === "settings-update-error" ? t("update.error.UPDATE_STATUS_FAILED") : "";
  const updatePhase = previewState === "settings-update-available" ? "available" : previewState === "settings-update-downloading" ? "downloading" : previewState === "settings-update-checking" ? "checking" : previewEnabled ? "upToDate" : "idle";
  return (
    <section className="panel settings-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("settings.title")} subtitle={t("settings.description")} />
      <section className="update-panel">
        <div className="update-heading">
          <div>
            <strong>{t("settings.visualMetrics.title")}</strong>
            <span>{t("settings.visualMetrics.description")}</span>
          </div>
        </div>
        {visualError ? <p className="update-error" role="alert">{t("settings.visualMetrics.loadFailed")}</p> : previewEnabled ? <div className="settings-stack"><ToggleRow label={t("settings.visualMetrics.showFps")} checked={showFps} onChange={setShowFps} /><ToggleRow label={t("settings.visualMetrics.showPing")} checked={showPing} onChange={setShowPing} /></div> : <p>{t("common.processing")}</p>}
      </section>
      {showProfileFocus ? <section className="update-panel"><div className="update-heading"><div><strong>{t("settings.accountInteraction.title")}</strong><span>{t("settings.accountInteraction.description")}</span></div></div><ToggleRow label={t("settings.accountInteraction.focusGameOnProfileSelect")} checked={focusGame} onChange={setFocusGame} /></section> : null}
      <section className="update-panel feedback-panel">
        <div className="update-heading"><div><strong>{t("feedback.title")}</strong><span>{t("feedback.description")}</span></div></div>
        <p className="feedback-privacy">{t("feedback.privacyNotice")}</p>
        {previewState === "settings-feedback" ? <div className="feedback-export-progress" role="progressbar" aria-label={t("feedback.progress.label")} aria-valuenow="45"><span>{t("feedback.progress.preparing")}</span><span>{t("feedback.progress.exporting")}</span><span>{t("feedback.progress.finalizing")}</span></div> : null}
        {previewState === "settings-feedback-success" ? <p className="update-success feedback-result">{t("feedback.success", { size: "4.2 MB", path: "Preview/diagnostics.zip" })}</p> : null}
        {previewState === "settings-feedback-error" ? <p className="update-error" role="alert">{t("feedback.failed")}</p> : null}
        <div className="update-actions"><button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t(previewState === "settings-feedback" ? "feedback.exporting" : "feedback.export")}</button></div>
      </section>
      <section className="update-panel">
        <div className="update-heading">
          <div><strong>{t("update.title")}</strong><span>{t(`update.${updatePhase}`)}</span></div>
          <span className="update-version">{t("update.currentVersion", { version: "0.3.17" })}</span>
        </div>
        {updateError ? <p className="update-error" role="alert">{updateError}</p> : null}
        {updatePhase === "available" ? <><span>{t("update.latestVersion", { version: "0.3.18" })}</span><div className="update-actions"><button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("update.downloadAndOpen")}</button></div></> : updatePhase === "downloading" ? <div className="update-progress"><progress max="100" value="42" /><span>{t("update.downloading", { progress: 42 })}</span></div> : <div className="update-actions"><button type="button" disabled title={t("status.gameDisconnectedDisabled")}>{t("update.check")}</button></div>}
      </section>
    </section>
  );
}

export function PageForRoute({ routeKey, ...pageProps }) {
  switch (routeKey) {
    case "overview": return <HomePage {...pageProps} />;
    case "automation": return <AutomationPage {...pageProps} />;
    case "map-data": {
      const previewProvider = getMapPreviewProvider(pageProps.bridgeMode, pageProps.previewState);
      return <MapDataPage {...pageProps} {...(previewProvider || {})} />;
    }
    case "march": return <SquadsPage {...pageProps} />;
    case "city-layout": return <CityLayoutPage {...pageProps} />;
    case "hotkeys": return <HotkeysPage {...pageProps} />;
    case "mini-games": return <MiniGamesPage {...pageProps} />;
    case "settings": return <SettingsPage {...pageProps} />;
    default: return <HomePage {...pageProps} />;
  }
}
