import { useI18n } from "./i18n.jsx";
import { PreviewConfigError, usePreviewConfig } from "./previewConfigHook.jsx";
import { activateTraining, automationDraftError, initialAutomationDraft, previewTrainingOrder } from "./previewAutomationContracts.js";
import { AutomationMeta, previewFutureTime } from "./AutomationMeta.jsx";
import { DispatchAssistManual } from "./DispatchAssistManual.jsx";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { dispatchWeeklyQualities, previewAssistFixture, previewAutomationRuntime, previewResourceGatherConfig, previewResourceGatherRuntime, previewTradeFixture, railwayWeeklyQualities, validPreviewResourceGatherConfig } from "./previewAutomationFixtures.js";
import { Activity, useEffect, useId, useState } from "react";
import { buildTradePurchaseDays, resolveTradeName, tradePurchaseRowKey } from "./tradePurchaseHistory.js";
import { Switch, ToggleRow, PanelTitle } from "./sharedPageUI.jsx";

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
  "Building Resource Collection": "common.runNow",
  "Armed Truck": "common.runNow",
  "Alliance Gathering Dispatch": "common.runNow",
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
  return <>
    <PreviewConfigError config={config} t={t} />
    <AutomationFields title={title} enabled={enabled} previewState={previewState} config={config} />
    {error && title !== "Automatic Construction" && title !== "Auto Training" ? <p className="automation-error" role="alert">{t(error)}</p> : null}
  </>;
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
  const rows = (...entries) => entries;
  switch (title) {
    case "Automatic Construction":
      return { summary: rows(["automation.automaticBuilders", `${text(runtime.automaticBuilders)} / ${text(runtime.maxBuilders)}`], ["automation.buildersTotal", `${text(runtime.occupiedBuilders)} / ${text(runtime.totalBuilders)}`]), status: rows(["automation.currentBuilding", text(runtime.candidateName)], ["automation.currentLevel", text(runtime.candidateLevel)], ["automation.automaticBuilders", `${text(runtime.automaticBuilders)} / ${text(runtime.maxBuilders)}`], ["automation.buildersTotal", `${text(runtime.occupiedBuilders)} / ${text(runtime.totalBuilders)}`], ["automation.processed", text(runtime.processed ?? 0)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Free Stamina":
      return { status: rows(["automation.claimedToday", `${text(runtime.todayCount)} / ${text(runtime.dailyLimit)}`], ["automation.processed", text(runtime.processed ?? 0)], ["automation.nextClaim", previewTime(runtime.nextClaimAt, language)]) };
    case "Automatic Treatment":
      return { summary: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)]), status: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)], ["automation.treatmentBatches", text(runtime.batchesStarted)], ["automation.soldiersQueued", text(runtime.soldiersQueued)], ["automation.helpsRequested", text(runtime.helpsRequested)], ["automation.collected", text(runtime.collected)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Trucks":
      return { summary: rows(["automation.departed", runtime.departed], ["automation.pendingClaims", runtime.pendingClaims]), status: rows(["automation.nextRun", enabled ? previewFutureTime(runtime.nextScheduledAt, language) : "-"], ["automation.qualifiedTotal", `${text(runtime.qualified ?? 0)} / ${text(runtime.total ?? 0)}`], ["automation.refreshed", text(runtime.refreshed ?? 0)], ["automation.departed", text(runtime.departed ?? 0)], ["automation.claimed", text(runtime.claimed ?? 0)], ["automation.pendingClaims", text(runtime.pendingClaims ?? 0)], ["automation.nextClaim", previewFutureTime(runtime.nextContinuationAt, language)], ["automation.batchDeparture", t(runtime.capabilities?.batchDeparture === true ? "common.available" : runtime.capabilities?.batchDeparture === false ? "common.degraded" : "common.notChecked")]) };
    case "Secret Task":
      return { summary: rows(["automation.dispatched", runtime.dispatched], ["automation.pendingClaims", runtime.pendingClaims]), status: rows(["automation.nextRun", (enabled || configDraft.collectRewards) ? previewFutureTime(runtime.nextScheduledAt, language) : "-"], ["automation.qualifiedTotal", `${text(runtime.qualified ?? runtime.details?.qualified ?? 0)} / ${text(runtime.available ?? runtime.details?.available ?? 0)}`], ["automation.dispatched", text(runtime.dispatched ?? 0)], ["automation.refreshed", text(runtime.refreshed ?? 0)], ["automation.claimed", text(runtime.claimed ?? 0)], ["automation.pendingClaims", text(runtime.pendingClaims ?? 0)], ["automation.nextClaim", previewFutureTime(runtime.nextContinuationAt, language)], ["automation.superRefresh", t(runtime.capabilities?.superRefresh === true ? "common.available" : runtime.capabilities?.superRefresh === false ? "automation.normalRefresh" : "common.notChecked")]) };
    case "Ghost Ops":
      return { status: rows(["automation.nextClaim", configDraft.ghostClaimRewards ? previewFutureTime(runtime.nextClaimAt, language) : "-"]) };
    case "Alliance Tech Donations":
      return { summary: rows(["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)]), status: rows(["automation.currentTech", text(runtime.scienceId)], ["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Automatic Official Application":
      return { status: rows(["automation.targetPosition", text(runtime.positionId || configDraft.positionId)], ["automation.currentPosition", text(runtime.currentPositionId)], ["automation.applicationQueue", text(runtime.applyQueueLength)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Automatic Alliance Train Boarding":
      return { summary: rows(["automation.currentCarriage", text(runtime.currentCarriage)], ["automation.queueCapacity", `${text(runtime.queueLength)} / ${text(runtime.maxPassenger)}`]), status: rows(["automation.currentCarriage", text(runtime.currentCarriage)], ["automation.queueCapacity", `${text(runtime.queueLength)} / ${text(runtime.maxPassenger)}`], ["automation.currentReward", text(runtime.currentReward)], ["automation.trainVipStatus", yesNo(runtime.isVip)], ["automation.trainVipCarriages", runtime.vipCarriages?.join(", ") || "—"], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Alliance Help":
      return { status: rows(["automation.available", text(runtime.available ?? 0)], ["automation.processed", text(runtime.processed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gifts":
      return { status: rows(["automation.normalClaimed", text(runtime.normalClaimed ?? 0)], ["automation.advancedClaimed", text(runtime.advancedClaimed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Excavation Stronghold Resources":
    case "Alliance Center Resources":
      return { status: rows(["automation.available", text(runtime.available ?? 0)], ["automation.claimed", text(runtime.claimed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gathering Dispatch":
      return { status: rows(["automation.allianceGatherAvailable", text(runtime.available ?? 0)], ["automation.dispatched", text(runtime.dispatched ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)]) };
    case "Building Resource Collection":
    case "Armed Truck":
      return { status: rows(["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
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

// Recovered index-BVfnK1wp.js Vr: source glyphs used by Automation ordering controls.
function AutomationIcon({ name }) {
  return <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
    {name === "arrow-up" ? <path d="M8 13.5v-11M3.5 7 8 2.5 12.5 7" /> : null}
    {name === "arrow-down" ? <path d="M8 2.5v11M3.5 9 8 13.5 12.5 9" /> : null}
    {name === "drag" ? <path d="M5 4h.01M11 4h.01M5 8h.01M11 8h.01M5 12h.01M11 12h.01" /> : null}
  </svg>;
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
  const runtime = previewAutomationRuntime(title, previewState);
  const assistFixture = previewAssistFixture(previewState);
  const [assistJobs, setAssistJobs] = useState(() => assistFixture.jobs);
  const assistTasks = assistFixture.tasks;
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
        <label>{t("automation.soldierTraining.totalCount")}<input type="number" min="1" max="1000000" step="1" value={trainingTotalCount || ""} aria-invalid={trainingTotalCount !== 0 && !validQuantity} disabled={!enabled || (config.draft.enabled && config.draft.trainEnabled)} onChange={(event) => config.store.edit((draft) => ({ ...draft, trainingTotalCount: Number(event.target.value), trainEnabled: false }))} /></label>
        <label>{t("automation.soldierTraining.target")}<select value={trainingTargetLevel} disabled={!enabled} onChange={(event) => setTrainingTargetLevel(Number(event.target.value))}><option value="0">{t("automation.soldierTraining.highest")}</option>{availableLevels.map((level) => { const available = camps.some((camp) => camp.availableLevels.includes(level)); return <option key={level} value={level} disabled={!available}>{t("automation.soldierTraining.level", { level })}</option>; })}</select></label>
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
    return <><label className="automation-checkbox-row"><input type="checkbox" checked={constructionTargetEnabled} disabled={!enabled} onChange={(event) => setConstructionTargetEnabled(event.target.checked)} /><span>{t("automation.construction.targetEnabled")}</span></label>{constructionTargetEnabled ? <><div className="automation-actions"><label>{t("automation.construction.targetLevel")}<input type="number" min="1" max="100" step="1" {...field("constructionTargetLevel", "30")} disabled={!enabled} /></label></div><details className="construction-type-select"><summary><span>{t("automation.construction.buildingTypes")}</span><strong>{selectedSummary}</strong><span className="construction-select-arrow" aria-hidden="true">⌄</span></summary><div className="construction-category-tabs" role="tablist" aria-label={t("automation.construction.buildingTypes")}>{categories.map((category) => <button type="button" role="tab" id={`preview-construction-${category}`} aria-controls="preview-construction-types" aria-selected={activeCategory === category} tabIndex={activeCategory === category ? 0 : -1} key={category} onClick={() => setConstructionCategory(category)} onKeyDown={(event) => moveConstructionTab(event, category)}>{t(`automation.construction.category.${category}`)}</button>)}</div><fieldset className="construction-target-types" role="tabpanel" id="preview-construction-types" aria-labelledby={`preview-construction-${activeCategory}`} disabled={!enabled}>{visibleBuildings.map((building) => <label className="automation-checkbox-row" key={building.itemId}><input type="checkbox" checked={constructionBuildingTypeIds.includes(building.itemId)} onChange={(event) => setConstructionBuildingTypeIds((current) => event.target.checked ? [...current, building.itemId] : current.filter((itemId) => itemId !== building.itemId))} /><span>{building.name}</span></label>)}</fieldset></details><p className="muted">{t(constructionBuildingTypeIds.length ? "automation.construction.targetHint" : "automation.construction.selectTypes")}</p></> : null}<label className="automation-checkbox-row"><input type="checkbox" {...check("autoClaimCompleted", true)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label>{t("automation.maxBuilders")}<input type="number" min="1" max="20" step="1" {...field("maxBuilders", previewState === "automation-validation-error" ? "21" : "1")} disabled={!enabled} aria-invalid={invalidBuilderLimit || undefined} /></label></div>{invalidBuilderLimit ? <p className="automation-error" role="alert">{t("automation.builderLimitError")}</p> : null}</>;
  }
  if (title === "Automatic Treatment") {
    return <div className="automation-actions"><label>{t("automation.treatmentAmountPerArmy")}<input type="number" min="1" max="1000000" step="1" {...field("amountPerArmy", "1")} disabled={!enabled} /></label></div>;
  }
  if (title === "Automatic Official Application") {
    return <div className="automation-actions"><label>{t("automation.targetPosition")}<select {...field("positionId", "0")} disabled={!enabled}><option value="0">{t("automation.position.none")}</option><option value="10002">{t("automation.position.vicePresident")}</option><option value="10003">{t("automation.position.strategyMinister")}</option><option value="10004">{t("automation.position.defenseMinister")}</option><option value="10005">{t("automation.position.constructionMinister")}</option><option value="10006">{t("automation.position.scienceMinister")}</option><option value="10007">{t("automation.position.internalAffairsMinister")}</option></select></label></div>;
  }
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) {
    const treasure = title === "Treasure";
    return <><section className="automation-settings-section"><h3>{t("automation.section.claim")}</h3>{treasure ? <p className="hint">{t("automation.treasureTargetDelayHint")}</p> : null}<div className="automation-form-grid"><label>{t("automation.minDelaySeconds")}<input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMin", "0")} disabled={!enabled} /></label><label>{t("automation.maxDelaySeconds")}<input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMax", "0")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><ToggleRow label={t("automation.autoReply")} checked={replyEnabled} disabled={!enabled} onChange={setReplyEnabled} />{replyEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.replyDelayMin")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMin", "2")} /></label><label>{t("automation.replyDelayMax")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMax", "5")} /></label></div><label className="automation-replies">{t("automation.replyPhrases")}<textarea rows="4" {...field("replies", "")} /></label></div> : null}</section>{treasure ? <section className="automation-settings-section"><ToggleRow label={t("automation.treasureAutoSearch")} checked={config.draft.treasureSearchEnabled ?? false} disabled={!enabled} onChange={(value) => check("treasureSearchEnabled").onChange({ target: { checked: value } })} /><p className="muted">{t("automation.treasureAutoSearchHint")}</p><ToggleRow label={t("automation.treasureAutoDispatch")} checked={treasureDispatchEnabled} disabled={!enabled} onChange={setTreasureDispatchEnabled} />{treasureDispatchEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.dispatchDelayMin")}<input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMin", "2")} /></label><label>{t("automation.dispatchDelayMax")}<input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMax", "5")} /></label></div><label>{t("automation.treasureDispatchRetrySeconds")}<input type="number" min="1" max="300" step="1" {...field("dispatchRetry", "30")} /></label><div className="automation-squad-choices">{[1,2,3,4].map((index) => <label key={index}><input type="checkbox" checked={(config.draft.dispatchSquads ?? [1]).includes(index)} onChange={(event) => config.store.edit((draft) => ({ ...draft, dispatchSquads: event.target.checked ? [...(draft.dispatchSquads ?? [1]), index] : (draft.dispatchSquads ?? [1]).filter((entry) => entry !== index) }))} /><span>{t("automation.squad", { index })}</span></label>)}</div><p className="muted">{t("automation.treasureDispatchPriorityHint")}</p></div> : null}</section> : null}</>;
  }
  if (title === "Trucks") {
    const weekly = config.draft.weeklyQualities ?? railwayWeeklyQualities;
    return <><section className="automation-settings-section"><h3>{t("automation.section.schedule")}</h3><div className="automation-actions"><label>{t("automation.delayAfterResetMinutes")}<input type="number" min="0" max="1440" step="1" {...field("delayMinutes", "2")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} values={weekly} running={previewState === "automation-runtime-running"} onChange={async (values) => { config.store.edit((draft) => ({ ...draft, weeklyQualities: values }), false); await config.store.flush().catch(() => {}); }} /></section><section className="automation-settings-section"><h3>{t("automation.section.departure")}</h3><label className="automation-checkbox-row"><input type="checkbox" {...check("departWhenTicketsInsufficient")} disabled={!enabled} /><span>{t("automation.railwayDepartWhenTicketsInsufficient")}</span></label></section></>;
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
    return <><section className="automation-settings-section"><h3>{t("squad.afkExecutionSettings")}</h3><label className="automation-checkbox-row"><input type="checkbox" {...check("collectRewards", false)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label>{t("automation.delayAfterResetMinutes")}<input type="number" min="0" max="1440" step="1" {...field("delayMinutes", "3")} disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} values={weekly} running={previewState === "automation-runtime-running"} onChange={async (values) => { config.store.edit((draft) => ({ ...draft, weeklyQualities: values }), false); await config.store.flush().catch(() => {}); }} /></section><section className="automation-settings-section"><ToggleRow label={t("automation.dispatchAssist")} checked={dispatchAssistEnabled} disabled={!enabled || assistFixture.busy} onChange={setDispatchAssistEnabled} />{dispatchAssistEnabled ? <><div className="automation-squad-choices">{["n","r","sr","ssr","ur","special"].map((quality) => <label key={quality}><input type="checkbox" disabled={assistFixture.busy} checked={(config.draft.assistQualities ?? []).includes(quality)} onChange={(event) => config.store.edit((draft) => ({ ...draft, assistQualities: event.target.checked ? [...(draft.assistQualities ?? []), quality] : (draft.assistQualities ?? []).filter((entry) => entry !== quality) }))} /><span>{quality === "special" ? t("automation.assistQuality.special") : quality.toUpperCase()}</span></label>)}</div><div className="automation-form-grid"><label>{t("automation.minDelaySeconds")}<input type="number" min="0" max="86400" step="1" disabled={assistFixture.busy} {...field("assistMin", "0")} /></label><label>{t("automation.maxDelaySeconds")}<input type="number" min="0" max="86400" step="1" disabled={assistFixture.busy} {...field("assistMax", "0")} /></label><label>{t("automation.assistIntervalSeconds")}<input type="number" min="5" max="300" step="1" disabled={assistFixture.busy} {...field("assistInterval", "30")} /></label></div></> : <DispatchAssistManual fixture={{ ...assistFixture, jobs: assistJobs }} selected={assistSelected} onSelectionChange={setAssistSelected} onSchedule={scheduleSelected} onJobAction={(action, uuid) => changeJobStatus(uuid, action === "cancel" ? "cancelled" : "scheduled")} />}</section></>;
  }
  if (title === "Ghost Ops") {
    return <><ToggleRow label={t("automation.ghost.autoJoinAlliance")} checked={ghostJoinEnabled} disabled={!enabled} onChange={setGhostJoinEnabled} />{ghostJoinEnabled ? <div className="automation-section"><strong>{t("automation.ghost.allianceFilter")}</strong><div className="automation-compact-choice-group">{["sr","ur","special"].map((filter) => <label className="automation-compact-choice" key={filter}><input type="radio" name="preview-ghost-filter" value={filter} checked={(config.draft.ghostFilter ?? "special") === filter} onChange={() => config.store.edit((draft) => ({ ...draft, ghostFilter: filter }))} disabled={!enabled} /><span>{t(`automation.ghost.filter.${filter}`)}</span></label>)}</div></div> : null}<label className="automation-checkbox-row"><input type="checkbox" {...check("ghostClaimRewards")} disabled={!enabled} /><span>{t("automation.ghost.autoClaimRewards")}</span></label><div className="automation-inline-status"><span>{t("automation.ghost.ownPending")}: {runtime.ownPending ?? 0}</span><span>{t("automation.ghost.allianceCandidates")}: {runtime.allianceCandidates ?? 0}</span><span>{t("automation.claimed")}: {runtime.claimed ?? 0}</span></div></>;
  }
  if (title === "Automatic Alliance Train Boarding") {
    const rewardSelectionVisible = trainMode === "reward" || vipTrainMode === "reward";
    const orderedRewards = [...preferredRewardKeys.map((key) => previewTrainRewards.find((reward) => reward.key === key)).filter(Boolean), ...previewTrainRewards.filter((reward) => !preferredRewardKeys.includes(reward.key))];
    const moveReward = (key, direction) => setPreferredRewardKeys((current) => { const index = current.indexOf(key); const target = index + direction; if (index < 0 || target < 0 || target >= current.length) return current; const next = [...current]; const [moved] = next.splice(index, 1); next.splice(target, 0, moved); return next; });
    return <><div className="automation-settings-section"><strong>{t("automation.normalCarriageSelection")}</strong><span className="muted">{t("automation.normalCarriageHint")}</span><fieldset className="automation-compact-choice-group automation-selection-mode-choices">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-train-mode" checked={trainMode === mode} disabled={!enabled} onChange={() => setTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</fieldset>{trainMode === "fixed" ? <fieldset className="automation-compact-choice-group automation-carriage-choices"><label className="automation-compact-choice"><input type="checkbox" disabled /><span>{t("automation.driver")}</span></label>{[1,2,3,4].map((carriage) => <label className="automation-compact-choice" key={carriage}><input type="checkbox" checked={normalFixedCarriageIds.includes(carriage)} disabled={!enabled} onChange={(event) => setNormalFixedCarriageIds(event.target.checked ? [carriage] : [])} /><span>{t(`automation.carriage${carriage}`)}</span></label>)}</fieldset> : null}</div>{rewardSelectionVisible ? <div className="automation-train-rewards automation-settings-section"><h3>{t("automation.section.rewardPreferences")}</h3><div className="automation-preference-list">{preferredRewardKeys.length === 0 ? <span className="muted">{t("automation.noPreferredRewards")}</span> : null}{orderedRewards.map((reward) => { const order = preferredRewardKeys.indexOf(reward.key); const selected = order >= 0; return <div className={`automation-preference-item${selected ? " selected" : ""}${draggedReward === reward.key ? " dragging" : ""}`} key={reward.key} draggable={selected} onDragStart={(event) => { if (selected) { event.dataTransfer.effectAllowed = "move"; setDraggedReward(reward.key); } }} onDragOver={(event) => { if (selected && draggedReward && draggedReward !== reward.key) { event.preventDefault(); event.dataTransfer.dropEffect = "move"; } }} onDrop={(event) => { event.preventDefault(); if (selected && draggedReward) setPreferredRewardKeys((current) => { const source = current.indexOf(draggedReward), target = current.indexOf(reward.key); if (source < 0 || target < 0 || source === target) return current; const next = [...current]; next.splice(target, 0, next.splice(source, 1)[0]); return next; }); setDraggedReward(""); }} onDragEnd={() => setDraggedReward("")}><label className="automation-reward-choice"><input type="checkbox" checked={selected} disabled={!enabled} onChange={(event) => setPreferredRewardKeys((current) => event.target.checked ? [...current, reward.key] : current.filter((key) => key !== reward.key))} /><GameAssetImage assetPath={reward.iconPath} alt={reward.name || reward.key} className="automation-reward-icon" /><span>{reward.name || reward.key}</span></label><strong className="automation-reward-count">×{reward.count}</strong>{selected ? <span className="automation-reward-order"><button type="button" title={t("automation.moveUp")} aria-label={t("automation.moveUp")} disabled={!enabled || order === 0} onClick={() => moveReward(reward.key, -1)}><AutomationIcon name="arrow-up" /></button><button type="button" title={t("automation.moveDown")} aria-label={t("automation.moveDown")} disabled={!enabled || order === preferredRewardKeys.length - 1} onClick={() => moveReward(reward.key, 1)}><AutomationIcon name="arrow-down" /></button><span className="automation-reward-drag-handle" aria-hidden="true"><AutomationIcon name="drag" /></span></span> : null}</div>; })}</div></div> : null}<div className="automation-settings-section"><strong>{t("automation.vipCarriageSelection")}</strong><label className="automation-checkbox-row"><input type="checkbox" {...check("autoAcceptVip")} disabled={!enabled} /><span>{t("automation.autoAcceptTrainVip")}</span></label><span className="muted">{t("automation.vipCarriageHint")}</span><fieldset className="automation-compact-choice-group automation-selection-mode-choices">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-vip-train-mode" checked={vipTrainMode === mode} disabled={!enabled} onChange={() => setVipTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</fieldset>{vipTrainMode === "fixed" ? <fieldset className="automation-compact-choice-group automation-carriage-choices"><label className="automation-compact-choice"><input type="checkbox" disabled /><span>{t("automation.driver")}</span></label>{[1,2,3,4].map((carriage) => { const selected = vipFixedCarriageIds.includes(carriage); return <label className="automation-compact-choice" key={carriage}><input type="checkbox" checked={selected} disabled={!enabled || (!selected && vipFixedCarriageIds.length >= 2)} onChange={(event) => setVipFixedCarriageIds((current) => event.target.checked ? [...current, carriage] : current.filter((item) => item !== carriage))} /><span>{t(`automation.carriage${carriage}`)}</span></label>; })}</fieldset> : null}</div><section className="automation-settings-section"><h3>{t("automation.section.additional")}</h3>{rewardSelectionVisible ? <label className="automation-checkbox-row"><input type="checkbox" checked={preferRewardQuantity} disabled={!enabled} onChange={(event) => setPreferRewardQuantity(event.target.checked)} /><span>{t("automation.preferRewardQuantity")}</span></label> : null}<div className="automation-actions"><label>{t("automation.thanksMode")}<select value={thanksMode} disabled={!enabled} onChange={(event) => setThanksMode(event.target.value)}><option value="like">{t("automation.thanksLike")}</option><option value="tickets">{t("automation.thanksTickets")}</option></select></label>{thanksMode === "tickets" ? <label>{t("automation.ticketCount")}<select {...field("ticketCount", "1")} disabled={!enabled}><option>1</option><option>2</option><option>3</option></select></label> : null}</div><p className="muted">{t("automation.ticketFallbackLike")}</p></section></>;
  }
  if (title === "Alliance Tech Donations") {
    return <div className="automation-actions"><label>{t("automation.donateThreshold")}<input type="number" min="1" max="30" step="1" {...field("threshold", "15")} disabled={!enabled} /></label></div>;
  }
  if (["Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources", "Building Resource Collection", "Armed Truck"].includes(title)) {
    const defaultInterval = title === "Alliance Gifts" ? 120 : 60;
    return <div className="automation-actions"><label>{t("automation.intervalMinutes")}<input type="number" min="1" max="1440" step="1" {...field("intervalMinutes", defaultInterval)} disabled={!enabled} /></label></div>;
  }
  if (title === "Alliance Gathering Dispatch") {
    const selected = config.draft.allianceGatherSquads ?? [1, 2];
    const ordered = [...selected, ...[1, 2, 3, 4].filter((index) => !selected.includes(index))];
    return <><div className="automation-status-grid">{[["automation.allianceGatherAvailable", runtime.available ?? 0], ["automation.dispatched", runtime.dispatched ?? 0], ["automation.lastRun", previewTime(runtime.lastRunAt, language)]].map(([key, value]) => <div className="automation-status-row" key={key}><span>{t(key)}</span><strong>{value}</strong></div>)}</div><p className="muted">{t("automation.allianceGatherSquadPriority")}</p><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.allianceGatherSquadPriority")}>{ordered.map((index) => <div className={`automation-squad-priority-item${selected.includes(index) ? " selected" : ""}`} draggable={selected.includes(index)} key={index} onDragStart={(event) => { if (selected.includes(index)) { event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(index)); setDraggedGatherSquad(index); } }} onDragOver={(event) => { if (selected.includes(index) && draggedGatherSquad !== null && draggedGatherSquad !== index) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); const source = selected.indexOf(draggedGatherSquad), target = selected.indexOf(index); if (source >= 0 && target >= 0 && source !== target) { const next = [...selected]; next.splice(target, 0, next.splice(source, 1)[0]); config.store.edit((draft) => ({ ...draft, allianceGatherSquads: next })); } setDraggedGatherSquad(null); }} onDragEnd={() => setDraggedGatherSquad(null)}><label><input type="checkbox" checked={selected.includes(index)} disabled={!enabled} onChange={(event) => { const next = event.target.checked ? [...selected, index] : selected.filter((entry) => entry !== index); if (config.draft.enabled && next.length === 0) { setGatherSquadError("automation.allianceGatherSquadRequired"); return; } setGatherSquadError(""); config.store.edit((draft) => ({ ...draft, allianceGatherSquads: next })); }} /><span>{t("automation.squad", { index })}</span></label>{selected.includes(index) ? <span className="automation-squad-drag-handle" aria-hidden="true"><AutomationIcon name="drag" /></span> : null}</div>)}</div>{gatherSquadError ? <p className="automation-error" role="alert">{t(gatherSquadError)}</p> : null}</>;
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
    <article className={`automation-card${enabled ? " is-enabled" : ""}`} data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{english(title)}</h3>
          {description ? <p>{["Red Packet", "Fireworks / Egg", "Treasure"].includes(title) ? t("automation.chatClaimDescription", { type: english(title) }) : english(description)}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled || (title === "Automatic Official Application" && Number(config.draft.positionId ?? 0) === 0)} aria-label={`${t(toggleLabel)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
        </div>
      </div>
      <AutomationMeta online={previewEnabled} active={active} state={sourceAutomationState(runtimeState, active)} presentation={presentation} />
      {previewEnabled && previewState === "automation-runtime-error" && !["Weekend Shield", "Attack Shield"].includes(title) ? <p className="automation-error" role="alert">{t("common.actionFailed")}</p> : null}
      {toggleError ? <p role="alert" className="automation-error">{t(toggleError)}</p> : null}
      {(hasSettings || actionLabel) ? <div className="automation-config">
        {(settingsCollapsible || actionLabel) ? <div className="automation-config-actions">
          {hasSettings && settingsCollapsible ? <button className={`automation-config-trigger${expanded ? " is-open" : ""}`} type="button" aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>
            <span className="automation-config-trigger-label">
              <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
              <span>{t("settings.title")}</span>
            </span>
            <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 6l4 4 4-4" /></svg>
          </button> : null}
          {actionLabel ? <button type="button" className="automation-run-action" disabled title={t("status.gameDisconnectedDisabled")}>{t(actionLabel)}</button> : null}
        </div> : null}
        {hasSettings ? <fieldset className="automation-config-body" data-draft-dirty={config.dirty} data-draft-saving={config.saving} onBlur={() => config.store.flush().catch(() => {})} style={settingsCollapsible && !expanded ? { display: "none" } : undefined} disabled={!previewEnabled}><AutomationConfigPreview title={title} enabled={previewEnabled} previewState={previewState} config={config} /></fieldset> : null}
      </div> : null}
    </article>
  );
}

// AutomationCard-LCx_jIi7 c: shared local expansion control. Offline disables
// the configuration fieldset, while its presentation can still be expanded.
function AutomationSettingsTrigger({ expanded, onClick, controls }) {
  const { t } = useI18n();
  return <button type="button" className={`automation-config-trigger${expanded ? " is-open" : ""}`} aria-expanded={expanded} aria-controls={controls} onClick={onClick}>
    <span className="automation-config-trigger-label">
      <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
      <span>{t("settings.title")}</span>
    </span>
    <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="M4 6l4 4 4-4" /></svg>
  </button>;
}

function ResourceGatherCard({ previewEnabled, previewState }) {
  const { t, language } = useI18n();
  const [expanded, setExpanded] = useState(false);
  const settingsId = useId();
  const config = usePreviewConfig(() => previewEnabled ? previewResourceGatherConfig(previewState) : previewResourceGatherConfig("automation-gather-no-squads"), validPreviewResourceGatherConfig, previewState === "automation-gather-save-error", `automation:gather:${previewState}`);
  const runtime = previewResourceGatherRuntime(previewState);
  const enabled = config.draft.enabled === true;
  const configuredSquads = config.draft.squads ?? [];
  const squadIndexes = [...new Set([...configuredSquads.map((squad) => squad.squadIndex), ...(runtime.gatherSquadIndexes ?? [])])].sort((a, b) => a - b);
  const squads = squadIndexes.map((squadIndex) => configuredSquads.find((squad) => squad.squadIndex === squadIndex) ?? { squadIndex, enabled: false, resource: "metal", level: 10 });
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

  return (
    <article className={`automation-card${enabled ? " is-enabled" : ""}`} data-preview-fixture={previewEnabled ? "automation-resource-gather" : "runtime-config-unobserved"} data-draft-dirty={config.dirty} data-draft-saving={config.saving}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{t("automation.category.resourceGather")}</h3>
          <p>{t("automation.resourceGather.description")}</p>
        </div>
        <div className="automation-card-header-actions"><button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} aria-label={`${t("automation.autoExecute")}: ${t(enabled ? "common.enabled" : "common.disabled")}`} title={`${t("automation.autoExecute")}: ${t(enabled ? "common.enabled" : "common.disabled")}`} disabled={!previewEnabled} onClick={() => toggleEnabled(!enabled)}><Switch checked={enabled} /></button></div>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${enabled ? "state-waiting" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.waiting" : "common.disabled") : t("status.disconnected")}</span>
      </div>
      <PreviewConfigError config={config} t={t} />
      <div className="automation-config">
        <div className="automation-config-actions"><AutomationSettingsTrigger expanded={expanded} controls={settingsId} onClick={() => setExpanded((value) => !value)} /></div>
        <fieldset id={settingsId} className="automation-config-body" hidden={!expanded} style={!expanded ? { display: "none" } : undefined} disabled={!previewEnabled}>
          <div className="automation-resource-gather-options">
            <label className="automation-resource-gather-radius">
              <span>{t("automation.resourceGather.scanRadius")}</span>
              <select aria-label={t("automation.resourceGather.scanRadius")} value={config.draft.scanRadius ?? 200} onChange={(event) => save({ ...config.store.getSnapshot().draft, enabled, squads, scanRadius: Number(event.target.value) })}>{[50,100,150,200,250,300,400,500].map((radius) => <option value={radius} key={radius}>{radius}</option>)}</select>
            </label>
            <label>
              <span>{t("automation.resourceGather.manualResumeDelay")}</span>
              <select value={(config.draft.manualResumeDelaySeconds ?? 120) / 60} onChange={(event) => save({ ...config.store.getSnapshot().draft, enabled, squads, manualResumeDelaySeconds: Number(event.target.value) * 60 })}>{Array.from({ length: 30 }, (_, index) => index + 1).map((minutes) => <option value={minutes} key={minutes}>{minutes}</option>)}</select>
            </label>
            <label className="automation-resource-gather-recall">
              <input type="checkbox" checked={config.draft.recallOnDisable ?? false} disabled={!previewEnabled} onChange={(event) => save({ ...config.store.getSnapshot().draft, enabled, squads, recallOnDisable: event.target.checked })} />
              <span>{t("automation.resourceGather.recallOnDisable")}</span>
            </label>
            <small className="automation-resource-gather-radius-hint muted">
              {t("automation.resourceGather.scanRadiusHint", { size: runtime.worldTileCount ?? 1000 })}
            </small>
          </div>
          {squads.length === 0 ? <p className="muted">{t("automation.resourceGather.squadsLoading")}</p> : null}
          {squads.map((squad) => { const resource = runtime.gatherResources?.find((entry) => entry.resource === squad.resource); const squadRuntime = runtime.gatherSquads?.find((entry) => entry.squadIndex === squad.squadIndex); const state = ["recalling", "recall_failed", "state_unconfirmed"].includes(squadRuntime?.step) ? squadRuntime.step : enabled && squad.enabled ? runtime.step === "runtime_wait" ? "runtime_wait" : squadRuntime?.step ?? "idle" : "disabled"; return <div className="automation-resource-gather-squad" role="group" aria-label={t("automation.squad", { index: squad.squadIndex })} key={squad.squadIndex}><label className="automation-resource-gather-enable"><input type="checkbox" checked={squad.enabled} onChange={(event) => updateSquad(squad.squadIndex, { enabled: event.target.checked })} /><span>{t("automation.squad", { index: squad.squadIndex })}</span></label><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.resource")}`} value={squad.resource} onChange={(event) => updateSquad(squad.squadIndex, { resource: event.target.value })}>{["metal","food","gold"].map((resourceKey) => <option value={resourceKey} key={resourceKey}>{t("common.loading")}</option>)}</select><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.level")}`} value={squad.level} onChange={(event) => updateSquad(squad.squadIndex, { level: Number(event.target.value) })}>{Array.from({ length: Math.min(10, resource?.maxLevel ?? 10) }, (_, index) => index + 1).map((level) => <option value={level} disabled={level > (resource?.maxLevel ?? 10)} key={level}>{level}</option>)}</select><span className="automation-resource-gather-state muted">{t(`automation.resourceGather.state.${state}`)}{enabled && squad.enabled && squadRuntime?.pauseReason === "manual_wait" && squadRuntime.manualResumeAt ? <small>{t("automation.resourceGather.manualResumeAt", { time: new Date(squadRuntime.manualResumeAt).toLocaleTimeString(language) })}</small> : null}{enabled && squad.enabled && squadRuntime?.shieldEndAt ? <small>{t("automation.resourceGather.shieldEndAt", { time: new Date(squadRuntime.shieldEndAt).toLocaleString(language) })}</small> : null}</span></div>; })}
          {error ? <p className="automation-error" role="alert">{t(error)}</p> : null}
        </fieldset>
      </div>
    </article>
  );
}

function TradeStationCard({ previewEnabled, previewState = "" }) {
  const { language, t } = useI18n();
  const fixture = previewTradeFixture(previewEnabled ? previewState : "");
  const config = usePreviewConfig(() => ({ enabled: false, crossServerEnabled: false, selectedItemIds: previewState === "automation-trade-positive" || previewState === "automation-trade-history" ? [7001] : [], selectedCurrencyIds: [15, 650053] }), (draft) => typeof draft.enabled === "boolean" && typeof draft.crossServerEnabled === "boolean" && Array.isArray(draft.selectedItemIds) && Array.isArray(draft.selectedCurrencyIds) && draft.selectedCurrencyIds.length > 0, previewState === "automation-trade-save-error", `automation:trade:${previewState}`);
  const [tradeTab, setTradeTab] = useState("goods");
  const [showExclusive, setShowExclusive] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const settingsId = useId();
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
      <article className={`automation-card${enabled ? " is-enabled" : ""}`}>
        <div className="automation-card-header">
          <div className="automation-card-title-group">
            <h3>{t("automation.tradeStation.title")}</h3>
            <p>{t("automation.tradeStation.description")}</p>
          </div>
          <div className="automation-card-header-actions">
            <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} aria-label={`${t("automation.autoExecute")}: ${t(enabled ? "common.enabled" : "common.disabled")}`} title={`${t("automation.autoExecute")}: ${t(enabled ? "common.enabled" : "common.disabled")}`} disabled={!previewEnabled || (!enabled && config.draft.selectedItemIds.length === 0)} onClick={() => save({ ...config.store.getSnapshot().draft, enabled: !enabled })}><Switch checked={enabled} /></button>
          </div>
        </div>
        <div className="automation-card-meta-row" role="status">
          <span className={`automation-state ${enabled ? "state-waiting" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.waiting" : "common.disabled") : t("status.disconnected")}</span>
        </div>
        <div className="automation-config">
          <div className="automation-config-actions"><AutomationSettingsTrigger expanded={expanded} controls={settingsId} onClick={() => setExpanded((value) => !value)} /></div>
          <fieldset id={settingsId} className="automation-config-body" hidden={!expanded} style={!expanded ? { display: "none" } : undefined} disabled={!previewEnabled}>
            <div className="trade-station-warning">
              {t("automation.tradeStation.warning")}
            </div>
            <ToggleRow label={t("automation.tradeStation.crossServer")} checked={config.draft.crossServerEnabled} disabled={!previewEnabled} onChange={(value) => save({ ...config.store.getSnapshot().draft, crossServerEnabled: value })} />
            <fieldset className="trade-station-currencies">
              <legend>{t("automation.tradeStation.currencies")}</legend>
              <div>{currencies.map((currency) => { const currencyName = resolveTradeName(fixture.gameTexts, currency.currencyNameKey, currency.currencyName, currency.currencyId); const selected = config.draft.selectedCurrencyIds.includes(currency.currencyId); return <label className={selected ? "selected" : ""} key={currency.currencyId}><input type="checkbox" checked={selected} disabled={selected && config.draft.selectedCurrencyIds.length === 1} onChange={(event) => toggleCurrency(currency.currencyId, event.target.checked)} />{currency.currencyIconPath ? <GameAssetImage assetPath={currency.currencyIconPath} alt="" className="trade-station-currency-icon" /> : null}<span>{currencyName}</span></label>; })}</div>
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
              <div className="trade-station-good-grid">{visibleGoods.map((item) => { const selected = config.draft.selectedItemIds.includes(item.itemId); const itemName = resolveTradeName(fixture.gameTexts, item.nameKey, item.name, item.itemId); const exclusive = item.offers.every((offer) => offer.exclusive); const possibleCurrencies = [...item.offers.reduce((labels, offer) => labels.has(offer.currencyId) ? labels : labels.set(offer.currencyId, resolveTradeName(fixture.gameTexts, offer.currencyNameKey, offer.currencyName, offer.currencyId)), new Map()).values()]; return <label className={`trade-station-good${selected ? " selected" : ""}${exclusive ? " disabled" : ""}`} key={item.itemId}><input type="checkbox" checked={selected} disabled={exclusive} onChange={(event) => toggleItem(item.itemId, event.target.checked)} /><span className={`trade-station-good-frame quality-${item.quality || 0}`}>{item.quality > 0 ? <GameAssetImage spriteName={`cfm_tongyong_daojukuang_${item.quality}`} alt="" className="trade-station-good-frame-image" deferUntilVisible /> : null}<GameAssetImage assetPath={item.iconPath} alt={itemName} className="trade-station-good-icon" deferUntilVisible /></span><span className="trade-station-good-copy"><b>{itemName}</b><small>{t("automation.tradeStation.buyWheneverAvailable")}</small><small>{t("automation.tradeStation.possibleCurrencies")} {possibleCurrencies.join(" / ") || "-"}</small>{exclusive ? <small className="trade-station-good-exclusive">{t("automation.tradeStation.exclusiveSkipped")}</small> : null}</span>{selected ? <span className="trade-station-good-check" aria-hidden="true">✓</span> : null}</label>; })}</div>
            </div> : <div role="tabpanel">{purchaseDays.length === 0 ? <span className="muted">{t("automation.tradeStation.noPurchases")}</span> : <div className="trade-station-purchase-history">{purchaseDays.map((day) => <section className="trade-station-purchase-day" key={day.dayStartAt}><h4 className="trade-station-purchase-day-heading"><time dateTime={new Date(day.dayStartAt).toISOString()}>{new Date(day.dayStartAt).toLocaleDateString(language, { year: "numeric", month: "long", day: "numeric", weekday: "short" })}</time><span className="trade-station-purchase-day-summary"><span>{t("automation.tradeStation.purchasedDayTotal", { count: day.totalQuantity.toLocaleString(language) })}</span>{[...day.itemCounts.values()].map(({ purchase, quantity }) => <span className="trade-station-purchase-day-item" key={purchase.itemId}>{resolveTradeName(fixture.gameTexts, purchase.itemNameKey, purchase.itemName, purchase.itemId)} ×{quantity.toLocaleString(language)}</span>)}</span></h4><div className="trade-station-purchase-list">{day.purchases.map((purchase, index) => { const itemName = resolveTradeName(fixture.gameTexts, purchase.itemNameKey, purchase.itemName, purchase.itemId); const currencyName = resolveTradeName(fixture.gameTexts, purchase.currencyNameKey, purchase.currencyName, purchase.currencyId); return <article className="trade-station-purchase" key={tradePurchaseRowKey(purchase, index)}><span className={`trade-station-good-frame quality-${purchase.quality || 0}`}>{purchase.quality > 0 ? <GameAssetImage spriteName={`cfm_tongyong_daojukuang_${purchase.quality}`} alt="" className="trade-station-good-frame-image" deferUntilVisible /> : null}<GameAssetImage assetPath={purchase.iconPath} alt={itemName} className="trade-station-good-icon" deferUntilVisible /></span><span className="trade-station-purchase-copy"><b>{itemName}</b><small>{t("automation.tradeStation.quantity", { count: purchase.quantity.toLocaleString(language) })}</small><small className="trade-station-purchase-price">{purchase.currencyIconPath ? <GameAssetImage assetPath={purchase.currencyIconPath} alt="" className="trade-station-currency-icon" deferUntilVisible /> : null}{t("automation.tradeStation.price", { price: purchase.price.toLocaleString(language), currency: currencyName })}</small><small>{t("automation.tradeStation.purchasedAt", { time: new Date(purchase.purchasedAt).toLocaleTimeString(language) })}</small><small>{t("automation.tradeStation.server", { server: purchase.serverId })}</small><small>{t("automation.tradeStation.dailyPurchaseIndex", { count: purchase.dailyPurchaseIndex == null ? "-" : purchase.dailyPurchaseIndex.toLocaleString(language) })}</small>{purchase.confirmedAfterTimeout ? <small>{t("automation.tradeStation.confirmedAfterTimeout")}</small> : null}</span></article>; })}</div></section>)}</div>}</div>}
            {fixture.error ? <div className="automation-error" role="alert">{fixture.error}</div> : null}
          </fieldset>
        </div>
      </article>
    </div>
  );
}

export function AutomationPage({ previewState = "", activeCategory, onActiveCategoryChange }) {
  const { t } = useI18n();
  const [localCategory, setLocalCategory] = useState("daily");
  const category = activeCategory ?? localCategory;
  const [visitedCategories, setVisitedCategories] = useState(() => new Set([category]));
  const selectCategory = (nextCategory) => {
    setVisitedCategories((current) => {
      if (current.has(nextCategory)) return current;
      const next = new Set(current);
      next.add(nextCategory);
      return next;
    });
    if (onActiveCategoryChange) onActiveCategoryChange(nextCategory);
    else setLocalCategory(nextCategory);
  };
  const [configStates, setConfigStates] = useState({});
  const onConfigStatus = (title, state) => setConfigStates((current) => current[title] === state ? current : { ...current, [title]: state });
  const aggregateState = Object.values(configStates).includes("error") ? "error" : Object.values(configStates).includes("saving") ? "saving" : "";
  const previewEnabled = previewState.startsWith("automation-");
  const renderCards = (cards) => cards.map(([title, description]) => <AutomationCard key={title} title={title} description={description} previewEnabled={previewEnabled} previewState={previewState} onConfigStatus={onConfigStatus} />);
  const pageStatus = aggregateState === "error" ? "automation.configSave.error" : aggregateState === "saving" ? "automation.configSave.saving" : previewState === "automation-saving"
    ? "automation.configSave.saving"
    : previewState === "automation-save-error"
      ? "automation.configSave.error"
      : previewState === "automation-saved"
        ? "automation.configSave.saved"
        : "";
  return (
    <section className="panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("nav.automation")} subtitle={previewEnabled ? (pageStatus ? t(pageStatus) : t("status.gameConnected")) : t("status.gameDisconnectedDisabled")} />
      <div className="automation-categories" role="tablist" aria-label={t("nav.automation")}>
        {automationCategories.map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            className={category === key ? "active" : ""}
            aria-selected={category === key}
            onClick={() => selectCategory(key)}
          >
            {t(automationCategoryKeys[key]) || label}
          </button>
        ))}
      </div>
      <div className="automation-grid">
        {visitedCategories.has("resourceGather") ? <Activity mode={category === "resourceGather" ? "visible" : "hidden"}><ResourceGatherCard previewEnabled={previewEnabled} previewState={previewState} /></Activity> : null}
        {visitedCategories.has("trade") ? <Activity mode={category === "trade" ? "visible" : "hidden"}><TradeStationCard previewEnabled={previewEnabled} previewState={previewState} /></Activity> : null}
        {visitedCategories.has("system") ? <Activity mode={category === "system" ? "visible" : "hidden"}>{renderCards(automationCards.system)}</Activity> : null}
        {visitedCategories.has("chat") ? <Activity mode={category === "chat" ? "visible" : "hidden"}>{renderCards(automationCards.chat)}</Activity> : null}
        {visitedCategories.has("resources") ? <Activity mode={category === "resources" ? "visible" : "hidden"}>{renderCards(automationCards.resources)}</Activity> : null}
        {visitedCategories.has("daily") ? <Activity mode={category === "daily" ? "visible" : "hidden"}>{renderCards(automationCards.daily.slice(0, 4))}</Activity> : null}
        {visitedCategories.has("alliance") ? <Activity mode={category === "alliance" ? "visible" : "hidden"}>{renderCards(automationCards.alliance)}</Activity> : null}
        {visitedCategories.has("daily") ? <Activity mode={category === "daily" ? "visible" : "hidden"}>{renderCards(automationCards.daily.slice(4))}</Activity> : null}
      </div>
    </section>
  );
}
