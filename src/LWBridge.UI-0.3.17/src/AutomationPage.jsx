import { useI18n } from "./i18n.jsx";
import { PreviewConfigError, usePreviewConfig } from "./previewConfigHook.jsx";
import { automationDraftError, initialAutomationDraft } from "./previewAutomationContracts.js";
import { AutomationMeta, previewFutureTime } from "./AutomationMeta.jsx";
import { DispatchAssistManual } from "./DispatchAssistManual.jsx";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { NativeChatAutomationCard } from "./NativeChatAutomationCard.jsx";
import { NativeAutomationCard } from "./NativeAutomationCard.jsx";
import { NativeGatherCard } from "./NativeGatherCard.jsx";
import { AutomationCardBoundary } from "./AutomationCardBoundary.jsx";
import { NativeTradeCard } from "./NativeTradeCard.jsx";
import { AUTOMATION_TASKS } from "./automationRuntime.js";
import { dispatchWeeklyQualities, previewAssistFixture, previewAutomationAvailableSquads, previewAutomationRuntime, previewResourceGatherConfig, previewResourceGatherRuntime, previewTradeFixture, validPreviewResourceGatherConfig } from "./previewAutomationFixtures.js";
import { Activity, useEffect, useId, useState } from "react";
import { buildTradePurchaseDays, resolveTradeName, tradePurchaseRowKey } from "./tradePurchaseHistory.js";
import { Switch, ToggleRow, PanelTitle } from "./sharedPageUI.jsx";

const automationCategories = [
  ["daily", "Daily Tasks"],
  ["alliance", "Alliance"],
  ["resourceGather", "Resource gathering"],
  ["chat", "Chat"],
  ["trade", "Trade Station"],
];

const automationCategoryKeys = {
  daily: "automation.category.daily",
  alliance: "automation.category.alliance",
  resourceGather: "automation.category.resourceGather",
  chat: "automation.category.chat",
  trade: "automation.category.trade",
};

const automationCards = {
  daily: [
    ["Automatic Treatment", "Treat wounded soldiers, request Alliance help, and collect completed treatment every 2 seconds without using Gold."],
    ["Secret Task", "Claim completed rewards, then continue refreshing and dispatching eligible Secret Tasks."],
  ],
  alliance: [
    ["Alliance Tech Donations", "Automatically donate standard resources to Alliance Tech."],
    ["Automatic Official Application", "Automatically apply for one selected official position."],
    ["Alliance Help", "Help Alliance members automatically, with a check every 3 seconds."],
    ["Alliance Gifts", "Claim normal and advanced Alliance Gifts at the configured interval."],
    ["Excavation Stronghold Resources", "Claim resources from occupied excavation strongholds at the configured interval."],
    ["Alliance Center Resources", "Claim production resources from Alliance Center buildings at the configured interval."],
    ["Alliance Gathering Dispatch", "Automatically send selected idle squads to Alliance Gathering Points."],
  ],
  chat: [
    ["Red Packet", "Listen for chat messages and automatically claim Red Packet."],
    ["Fireworks / Egg", "Listen for chat messages and automatically claim Fireworks / Egg."],
    ["Gold Egg", "Claim eligible tax, trade, lucky and birthday eggs."],
    ["Treasure", "Listen for chat messages and automatically claim Treasure."],
  ],
};

const nonCollapsibleAutomationSettings = new Set([
  "Automatic Treatment",
  "Alliance Tech Donations",
  "Automatic Official Application",
  "Alliance Gifts",
  "Excavation Stronghold Resources",
  "Alliance Center Resources",
]);

const automationCardsWithoutSettings = new Set([
  "Alliance Help",
]);

const automationActionLabels = {
  "Secret Task": "automation.dispatchAll",
  "Alliance Help": "common.runNow",
  "Alliance Gifts": "common.runNow",
  "Excavation Stronghold Resources": "common.runNow",
  "Alliance Center Resources": "common.runNow",
  "Red Packet": "common.runNow",
  "Fireworks / Egg": "common.runNow",
  Treasure: "common.runNow",
  "Alliance Gathering Dispatch": "common.runNow",
};

function WeeklyQualityPreview({ enabled, values, onChange, running = false }) {
  const { language, t } = useI18n();
  const days = Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat(language, { weekday: "short", timeZone: "UTC" }).format(new Date(Date.UTC(2024, 0, index + 1))));
  return <div className="automation-weekly-quality">{days.map((day, index) => <label key={day}><span>{day}</span><select value={values[index]} disabled={!enabled || running} onChange={(event) => { const next = [...values]; next[index] = event.target.value; onChange(next); }}><option value="none">{t("automation.noQualityRefresh")}</option><option value="ssr">{t("automation.ssrOrAbove")}</option><option value="ur">UR</option></select></label>)}</div>;
}

export function AutomationConfigPreview({ title, enabled, previewState = "", config, assistConfig, availableSquads, onLocalError }) {
  return <AutomationFields title={title} enabled={enabled} previewState={previewState} config={config} assistConfig={assistConfig} availableSquads={availableSquads} onLocalError={onLocalError} />;
}

function sourceVisibleDraftError(title, draft, maximumBuilders) {
  const error = automationDraftError(title, draft, maximumBuilders);
  if (["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title)) return error;
  if (["Automatic Treatment", "Automatic Official Application", "Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources"].includes(title)) return error;
  if (["Secret Task"].includes(title) && error === "automation.dailyDelayError") return error;
  return "";
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
    case "Automatic Treatment":
      return { summary: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)]), status: rows(["automation.wounded", text(runtime.wounded)], ["automation.treating", text(runtime.treating)], ["automation.treatmentBatches", text(runtime.batchesStarted)], ["automation.soldiersQueued", text(runtime.soldiersQueued)], ["automation.helpsRequested", text(runtime.helpsRequested)], ["automation.collected", text(runtime.collected)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Secret Task":
      return { summary: rows(["automation.dispatched", runtime.dispatched], ["automation.pendingClaims", runtime.pendingClaims]), status: rows(["automation.nextRun", (enabled || configDraft.collectRewards) ? previewFutureTime(runtime.nextScheduledAt, language) : "-"], ["automation.qualifiedTotal", `${text(runtime.qualified ?? runtime.details?.qualified ?? 0)} / ${text(runtime.available ?? runtime.details?.available ?? 0)}`], ["automation.dispatched", text(runtime.dispatched ?? 0)], ["automation.refreshed", text(runtime.refreshed ?? 0)], ["automation.claimed", text(runtime.claimed ?? 0)], ["automation.pendingClaims", text(runtime.pendingClaims ?? 0)], ["automation.nextClaim", previewFutureTime(runtime.nextContinuationAt, language)], ["automation.superRefresh", t(runtime.capabilities?.superRefresh === true ? "common.available" : runtime.capabilities?.superRefresh === false ? "automation.normalRefresh" : "common.notChecked")]) };
    case "Alliance Tech Donations":
      return { summary: rows(["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)]), status: rows(["automation.currentTech", text(runtime.scienceId)], ["automation.remaining", text(runtime.remaining)], ["automation.donated", text(runtime.donated)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Automatic Official Application":
      return { status: rows(["automation.targetPosition", text(runtime.positionId || configDraft.positionId)], ["automation.currentPosition", text(runtime.currentPositionId)], ["automation.applicationQueue", text(runtime.applyQueueLength)], ["automation.nextCheck", previewTime(runtime.nextRunAt, language)]) };
    case "Alliance Help":
      return { status: rows(["automation.available", text(runtime.available ?? 0)], ["automation.processed", text(runtime.processed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gifts":
      return { status: rows(["automation.normalClaimed", text(runtime.normalClaimed ?? 0)], ["automation.advancedClaimed", text(runtime.advancedClaimed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Excavation Stronghold Resources":
    case "Alliance Center Resources":
      return { status: rows(["automation.available", text(runtime.available ?? 0)], ["automation.claimed", text(runtime.claimed ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)], ["automation.nextRun", enabled ? previewFutureTime(runtime.nextRunAt, language) : "-"]) };
    case "Alliance Gathering Dispatch":
      return { status: rows(["automation.allianceGatherAvailable", text(runtime.available ?? 0)], ["automation.dispatched", text(runtime.dispatched ?? 0)], ["automation.lastRun", previewTime(runtime.lastRunAt, language)]) };
    case "Red Packet":
    case "Fireworks / Egg":
    case "Gold Egg":
      return { summary: rows(["automation.pendingClaims", text(runtime.pendingCount)], ["automation.latestResult", t(runtime.lastResult === "success" ? "common.success" : runtime.lastResult === "failed" ? "common.failed" : "common.waiting")]) };
    case "Treasure":
      return { summary: rows(["automation.pendingClaims", text(runtime.pendingCount)], ["automation.treasureDispatchPending", text(runtime.dispatchPendingCount)]) };
    default:
      return {};
  }
}

// Recovered index-BVfnK1wp.js Vr: source glyphs used by Automation ordering controls.
export function AutomationIcon({ name }) {
  return <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
    {name === "arrow-up" ? <path d="M8 13.5v-11M3.5 7 8 2.5 12.5 7" /> : null}
    {name === "arrow-down" ? <path d="M8 2.5v11M3.5 9 8 13.5 12.5 9" /> : null}
    {name === "drag" ? <path d="M5 4h.01M11 4h.01M5 8h.01M11 8h.01M5 12h.01M11 12h.01" /> : null}
  </svg>;
}

function AutomationFields({ title, enabled, previewState, config, assistConfig = config, availableSquads = [], onLocalError = () => {} }) {
  const { t, language } = useI18n();
  const setNow = (key, value) => {
    config.store.edit((draft) => ({ ...draft, [key]: typeof value === "function" ? value(draft[key]) : value }), false);
    config.store.flush().catch(() => {});
  };
  const fieldState = (key, initial, immediate = false) => [config.draft[key] ?? initial, (value) => immediate ? setNow(key, value) : config.store.edit((draft) => ({ ...draft, [key]: typeof value === "function" ? value(draft[key] ?? initial) : value }))];
  // Owner 2026-10-21: a Min/Max pair keeps itself valid when you leave a box (Min 10 with Max 0 becomes 10-10).
  const delayPairs = { claimMin: ["claimMax", true], claimMax: ["claimMin", false], replyMin: ["replyMax", true], replyMax: ["replyMin", false], dispatchMin: ["dispatchMax", true], dispatchMax: ["dispatchMin", false] };
  const settlePair = (key) => {
    const pair = delayPairs[key];
    if (!pair) return;
    config.store.edit((draft) => {
      const [other, isMin] = pair;
      const mine = Number(draft[key]), theirs = Number(draft[other]);
      if (String(draft[key] ?? "").trim() === "" || String(draft[other] ?? "").trim() === "" || !Number.isFinite(mine) || !Number.isFinite(theirs)) return draft;
      if (isMin ? theirs < mine : theirs > mine) return { ...draft, [other]: draft[key] };
      return draft;
    });
  };
  const field = (key, initial) => ({ value: config.draft[key] ?? initial, onChange: (event) => config.store.edit((draft) => ({ ...draft, [key]: event.target.value })), onBlur: () => settlePair(key) });
  const check = (key, initial = false) => ({ checked: config.draft[key] ?? initial, onChange: (event) => config.store.edit((draft) => ({ ...draft, [key]: event.target.checked })) });
  const checkNow = (key, initial = false) => ({ checked: config.draft[key] ?? initial, onChange: (event) => setNow(key, event.target.checked) });
  const [replyEnabled] = fieldState("replyEnabled", false);
  const [treasureDispatchEnabled] = fieldState("treasureDispatchEnabled", false);
  const dispatchAssistEnabled = assistConfig.draft.autoHelp ?? false;
  const [draggedTreasureSquad, setDraggedTreasureSquad] = useState(null);
  const [treasureDragOverSquad, setTreasureDragOverSquad] = useState(null);
  const [draggedGatherSquad, setDraggedGatherSquad] = useState(null);
  const [gatherDragOverSquad, setGatherDragOverSquad] = useState(null);
  const [assistSelected, setAssistSelected] = useState([]);
  const [assistValidationError, setAssistValidationError] = useState("");
  const runtime = previewAutomationRuntime(title, previewState);
  const assistFixture = previewAssistFixture(previewState);
  const [assistJobs, setAssistJobs] = useState(() => assistFixture.jobs);
  const assistTasks = assistFixture.tasks;
  const assistJobByTask = new Map(assistJobs.map((job) => [job.uuid, job]));
  const assistTitle = (task) => `${task.ownerName || task.ownerUid} · ${String(task.qualityKey).toUpperCase()}${task.isSpecial ? ` · ${t("automation.assistQuality.special")}` : ""}`;
  const commitAssistNow = (patch) => {
    const next = { ...assistConfig.store.getSnapshot().draft, ...patch };
    const error = automationDraftError("Dispatch Assist", next);
    if (error) {
      setAssistValidationError(error);
      return false;
    }
    setAssistValidationError("");
    assistConfig.store.edit(next, false);
    assistConfig.store.flush().catch(() => {});
    return true;
  };
  const editAssistSetting = (patch) => {
    assistConfig.store.edit((draft) => ({ ...draft, ...patch }), false);
    const next = assistConfig.store.getSnapshot().draft;
    const error = automationDraftError("Dispatch Assist", next);
    setAssistValidationError(error);
    if (error) assistConfig.store.pause();
    else assistConfig.store.edit(next);
  };
  if (title === "Automatic Treatment") {
    return <div className="automation-actions"><label>{t("automation.treatmentAmountPerArmy")}<input type="number" min="1" max="1000000" step="1" {...field("amountPerArmy", "1")} disabled={!enabled} /></label></div>;
  }
  if (title === "Automatic Official Application") {
    return <div className="automation-actions"><label>{t("automation.targetPosition")}<select value={config.draft.positionId ?? "0"} onChange={(event) => setNow("positionId", event.target.value)} disabled={!enabled}><option value="0">{t("automation.position.none")}</option><option value="10002">{t("automation.position.vicePresident")}</option><option value="10003">{t("automation.position.strategyMinister")}</option><option value="10004">{t("automation.position.defenseMinister")}</option><option value="10005">{t("automation.position.constructionMinister")}</option><option value="10006">{t("automation.position.scienceMinister")}</option><option value="10007">{t("automation.position.internalAffairsMinister")}</option></select></label></div>;
  }
  if (["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title)) {
    const treasure = title === "Treasure";
    const selectedDispatchSquads = config.draft.dispatchSquads ?? [];
    const dispatchSquads = [...selectedDispatchSquads, ...availableSquads.filter((index) => !selectedDispatchSquads.includes(index))];
    const updateDispatchSquads = (next) => {
      config.store.edit((draft) => ({ ...draft, dispatchSquads: next }), false);
      config.store.flush().catch(() => {});
    };
    const hoursOn = config.draft.activeHoursEnabled === true;
    const hours = <section className="automation-settings-section"><ToggleRow label={t("automation.activeHours")} checked={hoursOn} disabled={!enabled} onChange={(value) => setNow("activeHoursEnabled", value)} />{hoursOn ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.activeHoursStart")}<input type="time" {...field("activeStart", "08:00")} disabled={!enabled} /></label><label>{t("automation.activeHoursEnd")}<input type="time" {...field("activeEnd", "02:00")} disabled={!enabled} /></label></div><p className="muted">{t("automation.activeHoursHint")}</p></div> : null}</section>;
    // Owner 2026-10-26: LwTool "Dig Treasure" settings first (claim on completion, hunt, unshared, dig dispatch).
    if (treasure) {
      const order = config.draft.dispatchOrder === "strong" ? "strong" : "weak";
      const toggleSquad = (index, on) => updateDispatchSquads(on ? [...selectedDispatchSquads, index].sort((a, b) => a - b) : selectedDispatchSquads.filter((entry) => entry !== index));
      return <>{hours}
        <section className="automation-settings-section"><h3>{t("automation.section.claim")}</h3>
          <p className="hint">{t("automation.treasureShareHint")}</p>
          <div className="automation-form-grid"><label>{t("automation.treasureClickBefore")}<input type="number" min="-5000" max="300" step="1" {...field("clickBeforeMs", "0")} disabled={!enabled} /></label></div>
          <p className="muted">{t("automation.treasureClickBeforeHint")}</p>
          <ToggleRow label={t("automation.treasureRandomClaim")} checked={config.draft.randomClaimEnabled === true} disabled={!enabled} onChange={(value) => setNow("randomClaimEnabled", value)} />
          <p className="muted">{t("automation.treasureRandomClaimHint")}</p>
          <ToggleRow label={t("automation.treasureSkipOwnRadar")} checked={config.draft.skipOwnRadar !== false} disabled={!enabled} onChange={(value) => setNow("skipOwnRadar", value)} />
        </section>
        <section className="automation-settings-section"><ToggleRow label={t("automation.autoReply")} checked={replyEnabled} disabled={!enabled} onChange={(value) => setNow("replyEnabled", value)} />{replyEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.replyDelayMin")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMin", "2")} /></label><label>{t("automation.replyDelayMax")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMax", "5")} /></label></div><label className="automation-replies">{t("automation.replyPhrases")}<textarea rows="4" {...field("replies", "")} /></label></div> : null}</section>
        <section className="automation-settings-section"><h3>{t("automation.section.treasureFind")}</h3>
          <ToggleRow label={t("automation.treasureHunt")} checked={config.draft.huntEnabled !== false} disabled={!enabled} onChange={(value) => setNow("huntEnabled", value)} />
          <p className="muted">{t("automation.treasureHuntHint")}</p>
          <ToggleRow label={t("automation.treasureUnshared")} checked={config.draft.unsharedEnabled === true} disabled={!enabled} onChange={(value) => setNow("unsharedEnabled", value)} />
          <p className="muted">{t("automation.treasureUnsharedHint")}</p>
        </section>
        <section className="automation-settings-section"><ToggleRow label={t("automation.treasureAutoDispatch")} checked={treasureDispatchEnabled} disabled={!enabled} onChange={(value) => setNow("treasureDispatchEnabled", value)} />
          {treasureDispatchEnabled ? <div className="automation-subsettings">
            <div className="automation-form-grid">
              <label>{t("automation.treasureDigDelay")}<input type="number" min="0" max="60000" step="1" {...field("digDelayMs", "300")} /></label>
              <label>{t("automation.treasureDigOrder")}<select value={order} onChange={(event) => setNow("dispatchOrder", event.target.value)}><option value="weak">{t("automation.treasureDigOrderWeak")}</option><option value="strong">{t("automation.treasureDigOrderStrong")}</option></select></label>
            </div>
            <p className="muted">{t("automation.treasureDigDelayHint")}</p>
            <ToggleRow label={t("automation.treasureRedirect")} checked={config.draft.redirectGathering !== false} onChange={(value) => setNow("redirectGathering", value)} />
            <p className="muted">{t("automation.treasureRedirectHint")}</p>
            <label>{t("automation.treasureDispatchRetrySeconds")}<input type="number" min="1" max="300" step="1" {...field("dispatchRetry", "30")} /></label>
            <div className="automation-compact-choice-group" role="group" aria-label={t("automation.treasureSquads")}>{availableSquads.map((index) => <label key={index}><input type="checkbox" checked={selectedDispatchSquads.includes(index)} onChange={(event) => toggleSquad(index, event.target.checked)} /><span>{t("automation.squad", { index })}</span></label>)}</div>
            <p className="muted">{t("automation.treasureSquads")}</p>
          </div> : null}
        </section></>;
    }
    return <>{hours}<section className="automation-settings-section"><h3>{t("automation.section.claim")}</h3>{treasure && Array.isArray(config.draft.claimSchedule) ? <TreasureClaimSchedule rows={config.draft.claimSchedule} disabled={!enabled} onChange={(claimSchedule) => config.store.edit((draft) => ({ ...draft, claimSchedule }))} /> : <>{treasure ? <p className="hint">{t("automation.treasureTargetDelayHint")}</p> : null}<div className="automation-form-grid"><label>{t("automation.minDelaySeconds")}<input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMin", "0")} disabled={!enabled} /></label><label>{t("automation.maxDelaySeconds")}<input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" {...field("claimMax", "0")} disabled={!enabled} /></label></div></>}</section><section className="automation-settings-section"><ToggleRow label={t("automation.autoReply")} checked={replyEnabled} disabled={!enabled} onChange={(value) => setNow("replyEnabled", value)} />{replyEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.replyDelayMin")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMin", "2")} /></label><label>{t("automation.replyDelayMax")}<input type="number" min="0.1" max="600" step="0.1" {...field("replyMax", "5")} /></label></div><label className="automation-replies">{t("automation.replyPhrases")}<textarea rows="4" {...field("replies", "")} /></label></div> : null}</section>{treasure ? <section className="automation-settings-section"><p className="muted">{t("automation.treasureShareHint")}</p><ToggleRow label={t("automation.treasureAutoDispatch")} checked={treasureDispatchEnabled} disabled={!enabled} onChange={(value) => setNow("treasureDispatchEnabled", value)} />{treasureDispatchEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label>{t("automation.dispatchDelayMin")}<input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMin", "2")} /></label><label>{t("automation.dispatchDelayMax")}<input type="number" min="0.1" max="600" step="0.1" {...field("dispatchMax", "5")} /></label></div><label>{t("automation.treasureDispatchRetrySeconds")}<input type="number" min="1" max="300" step="1" {...field("dispatchRetry", "30")} /></label><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.treasureDispatchPriorityHint")}>{dispatchSquads.map((index) => { const selected = selectedDispatchSquads.includes(index); return <div className={`automation-squad-priority-item${selected ? " selected" : ""}${draggedTreasureSquad === index ? " dragging" : ""}${treasureDragOverSquad === index ? " drag-over" : ""}`} draggable={selected} key={index} onDragStart={(event) => { if (!selected) return; event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(index)); setDraggedTreasureSquad(index); }} onDragOver={(event) => { if (!selected || draggedTreasureSquad === null || draggedTreasureSquad === index) return; event.preventDefault(); event.dataTransfer.dropEffect = "move"; setTreasureDragOverSquad(index); }} onDrop={(event) => { event.preventDefault(); const source = selectedDispatchSquads.indexOf(draggedTreasureSquad), target = selectedDispatchSquads.indexOf(index); setDraggedTreasureSquad(null); setTreasureDragOverSquad(null); if (source < 0 || target < 0 || source === target) return; const next = [...selectedDispatchSquads]; next.splice(target, 0, next.splice(source, 1)[0]); updateDispatchSquads(next); }} onDragEnd={() => { setDraggedTreasureSquad(null); setTreasureDragOverSquad(null); }}><label><input type="checkbox" checked={selected} onChange={(event) => updateDispatchSquads(event.target.checked ? [...selectedDispatchSquads, index] : selectedDispatchSquads.filter((entry) => entry !== index))} /><span>{t("automation.squad", { index })}</span></label>{selected ? <span className="automation-squad-drag-handle" aria-hidden="true"><AutomationIcon name="drag" /></span> : null}</div>; })}</div><p className="muted">{t("automation.treasureDispatchPriorityHint")}</p></div> : null}</section> : null}</>;
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
    const assistQualities = assistConfig.draft.qualities ?? [];
    const assistDelaySeconds = assistConfig.draft.delaySeconds ?? ["0", "0"];
    const assistError = assistValidationError || automationDraftError("Dispatch Assist", assistConfig.draft);
    const updateAssistQuality = (quality, checked) => {
      const qualities = checked ? [...assistQualities, quality] : assistQualities.filter((entry) => entry !== quality);
      commitAssistNow({ qualities });
    };
    return <>
      <section className="automation-settings-section">
        <h3>{t("squad.afkExecutionSettings")}</h3>
        <label className="automation-checkbox-row"><input type="checkbox" {...checkNow("collectRewards", false)} disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label>
        <div className="automation-actions"><label>{t("automation.delayAfterResetMinutes")}<input type="number" min="0" max="1440" step="1" {...field("delayMinutes", "3")} disabled={!enabled} /></label></div>
      </section>
      <section className="automation-settings-section">
        <h3>{t("automation.section.quality")}</h3>
        <WeeklyQualityPreview enabled={enabled} values={weekly} running={runtime.running === true} onChange={async (values) => { config.store.edit((draft) => ({ ...draft, weeklyQualities: values }), false); await config.store.flush().catch(() => {}); }} />
      </section>
      <section className="automation-settings-section">
        <ToggleRow label={t("automation.dispatchAssist")} checked={dispatchAssistEnabled} disabled={!enabled || assistFixture.busy} onChange={(value) => commitAssistNow({ autoHelp: value })} />
        {dispatchAssistEnabled ? <>
          <div className="automation-squad-choices">{["n", "r", "sr", "ssr", "ur", "special"].map((quality) => <label key={quality}><input type="checkbox" disabled={assistFixture.busy} checked={assistQualities.includes(quality)} onChange={(event) => updateAssistQuality(quality, event.target.checked)} /><span>{quality === "special" ? t("automation.assistQuality.special") : quality.toUpperCase()}</span></label>)}</div>
          <div className="automation-form-grid">
            <label>{t("automation.minDelaySeconds")}<input type="number" min="0" max="86400" step="1" disabled={assistFixture.busy} value={assistDelaySeconds[0] ?? "0"} onChange={(event) => editAssistSetting({ delaySeconds: [event.target.value, assistDelaySeconds[1] ?? "0"] })} onBlur={() => assistConfig.store.flush().catch(() => {})} /></label>
            <label>{t("automation.maxDelaySeconds")}<input type="number" min="0" max="86400" step="1" disabled={assistFixture.busy} value={assistDelaySeconds[1] ?? "0"} onChange={(event) => editAssistSetting({ delaySeconds: [assistDelaySeconds[0] ?? "0", event.target.value] })} onBlur={() => assistConfig.store.flush().catch(() => {})} /></label>
            <label>{t("automation.assistIntervalSeconds")}<input type="number" min="5" max="300" step="1" disabled={assistFixture.busy} value={assistConfig.draft.intervalSeconds ?? "30"} onChange={(event) => editAssistSetting({ intervalSeconds: event.target.value })} onBlur={() => assistConfig.store.flush().catch(() => {})} /></label>
          </div>
        </> : <DispatchAssistManual fixture={{ ...assistFixture, jobs: assistJobs }} selected={assistSelected} onSelectionChange={setAssistSelected} onSchedule={scheduleSelected} onJobAction={(action, uuid) => changeJobStatus(uuid, action === "cancel" ? "cancelled" : "scheduled")} />}
        {dispatchAssistEnabled && assistError ? <p className="error-text">{t(assistError)}</p> : null}
      </section>
    </>;
  }
  if (title === "Alliance Tech Donations") {
    return <div className="automation-actions"><label>{t("automation.donateThreshold")}<input type="number" min="1" max="30" step="1" {...field("threshold", "15")} disabled={!enabled} /></label></div>;
  }
  if (["Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources"].includes(title)) {
    const defaultInterval = title === "Alliance Gifts" ? 120 : 60;
    return <div className="automation-actions"><label>{t("automation.intervalMinutes")}<input type="number" min="1" max="1440" step="1" {...field("intervalMinutes", defaultInterval)} disabled={!enabled} /></label></div>;
  }
  if (title === "Alliance Gathering Dispatch") {
    const selected = config.draft.allianceGatherSquads ?? [1, 2];
    const ordered = [...selected, ...availableSquads.filter((index) => !selected.includes(index))];
    return <><div className="automation-status-grid">{[["automation.allianceGatherAvailable", runtime.available ?? 0], ["automation.dispatched", runtime.dispatched ?? 0], ["automation.lastRun", previewTime(runtime.lastRunAt, language)]].map(([key, value]) => <div className="automation-status-row" key={key}><span>{t(key)}</span><strong>{value}</strong></div>)}</div><p className="muted">{t("automation.allianceGatherSquadPriority")}</p><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.allianceGatherSquadPriority")}>{ordered.map((index) => <div className={`automation-squad-priority-item${selected.includes(index) ? " selected" : ""}${draggedGatherSquad === index ? " dragging" : ""}${gatherDragOverSquad === index ? " drag-over" : ""}`} draggable={selected.includes(index)} key={index} onDragStart={(event) => { if (selected.includes(index)) { event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", String(index)); setDraggedGatherSquad(index); } }} onDragOver={(event) => { if (selected.includes(index) && draggedGatherSquad !== null && draggedGatherSquad !== index) { event.preventDefault(); event.dataTransfer.dropEffect = "move"; setGatherDragOverSquad(index); } }} onDrop={(event) => { event.preventDefault(); const source = selected.indexOf(draggedGatherSquad), target = selected.indexOf(index); setDraggedGatherSquad(null); setGatherDragOverSquad(null); if (source >= 0 && target >= 0 && source !== target) { const next = [...selected]; next.splice(target, 0, next.splice(source, 1)[0]); setNow("allianceGatherSquads", next); } }} onDragEnd={() => { setDraggedGatherSquad(null); setGatherDragOverSquad(null); }}><label><input type="checkbox" checked={selected.includes(index)} disabled={!enabled} onChange={(event) => { const next = event.target.checked ? [...selected, index] : selected.filter((entry) => entry !== index); if (config.draft.enabled && next.length === 0) { onLocalError("automation.allianceGatherSquadRequired"); return; } onLocalError(""); setNow("allianceGatherSquads", next); }} /><span>{t("automation.squad", { index })}</span></label>{selected.includes(index) ? <span className="automation-squad-drag-handle" aria-hidden="true"><AutomationIcon name="drag" /></span> : null}</div>)}</div></>;
  }
  return null;
}

// Owner 2026-10-22: Treasure claim delay by time of day (this computer's clock), random between Min and Max.
function TreasureClaimSchedule({ rows, disabled, onChange }) {
  const { t } = useI18n();
  const update = (index, patch) => onChange(rows.map((row, i) => i === index ? { ...row, ...patch } : row));
  return <div className="treasure-schedule">
    <p className="muted">{t("automation.treasureScheduleHint")}</p>
    <div className="treasure-schedule-head" aria-hidden="true"><span>{t("automation.scheduleFrom")}</span><span>{t("automation.scheduleTo")}</span><span>{t("automation.minDelaySeconds")}</span><span>{t("automation.maxDelaySeconds")}</span><span /></div>
    {rows.map((row, index) => <div className="treasure-schedule-row" key={index}>
      <input type="time" aria-label={t("automation.scheduleFrom")} value={row.start} disabled={disabled} onChange={(event) => update(index, { start: event.target.value })} />
      <input type="time" aria-label={t("automation.scheduleTo")} value={row.end} disabled={disabled} onChange={(event) => update(index, { end: event.target.value })} />
      <input type="number" min="0" max="600" step="0.01" aria-label={t("automation.minDelaySeconds")} value={row.min} disabled={disabled} onChange={(event) => update(index, { min: event.target.value })} />
      <input type="number" min="0" max="600" step="0.01" aria-label={t("automation.maxDelaySeconds")} value={row.max} disabled={disabled} onChange={(event) => update(index, { max: event.target.value })} />
      <button type="button" className="treasure-schedule-remove" disabled={disabled || rows.length <= 1} aria-label={t("automation.scheduleRemove")} onClick={() => onChange(rows.filter((_, i) => i !== index))}>×</button>
    </div>)}
    {rows.length < 12 ? <button type="button" className="treasure-schedule-add" disabled={disabled} onClick={() => onChange([...rows, { start: "00:00", end: "08:00", min: 1, max: 2 }])}>{t("automation.scheduleAdd")}</button> : null}
  </div>;
}

function AutomationCard({ title, description, online = false, previewEnabled, previewState = "", profileId = "", onConfigStatus, availableSquads }) {
  const { english, language, t } = useI18n();
  const failFirstSave = previewState === "automation-save-error" || (previewState === "automation-secret-weekly-save-error" && title === "Secret Task");
  const failFirstAssistSave = title === "Secret Task" && previewState === "automation-assist-save-error";
  const runtime = previewAutomationRuntime(title, previewState);
  const maximumBuilders = Math.max(1, runtime.totalBuilders ?? 20);
  const profileScope = profileId || "default";
  const config = usePreviewConfig(() => initialAutomationDraft(title, previewState), (draft) => !automationDraftError(title, draft, maximumBuilders), failFirstSave, `automation:${profileScope}:${previewState}:${title}`);
  const assistConfig = usePreviewConfig(() => initialAutomationDraft("Dispatch Assist", previewState), (draft) => !automationDraftError("Dispatch Assist", draft), failFirstAssistSave, `automation:${profileScope}:${previewState}:${title}:dispatchAssist`);
  const visibleDraftError = sourceVisibleDraftError(title, config.draft, maximumBuilders);
  const enabled = config.draft.enabled === true;
  const active = title === "Secret Task" ? enabled || config.draft.collectRewards === true : enabled;
  const runtimeState = runtime.state;
  const presentation = automationRuntimePresentation(title, runtime, active, config.draft, t, language);
  const [fieldError, setFieldError] = useState("");
  const setEnabled = (next) => {
    const value = typeof next === "function" ? next(enabled) : next;
    if (title === "Alliance Gathering Dispatch" && value && !(config.draft.allianceGatherSquads ?? [1, 2]).length) { setFieldError("automation.allianceGatherSquadRequired"); return; }
    setFieldError("");
    const nextDraft = (draft) => ({ ...draft, enabled: value });
    if (["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure", "Secret Task"].includes(title)) {
      config.store.edit(nextDraft, false);
      config.store.flush().catch(() => {});
    } else config.store.edit(nextDraft);
  };
  useEffect(() => { onConfigStatus?.(title, config.error ? "error" : config.saving ? "saving" : "", config.store); }, [title, config.error, config.saving, config.store, onConfigStatus]);
  useEffect(() => {
    if (title === "Secret Task") onConfigStatus?.("Dispatch Assist", assistConfig.error ? "error" : assistConfig.saving ? "saving" : "", assistConfig.store);
  }, [title, assistConfig.error, assistConfig.saving, assistConfig.store, onConfigStatus]);
  const settingsCollapsible = !nonCollapsibleAutomationSettings.has(title);
  const hasSettings = !automationCardsWithoutSettings.has(title);
  const [expanded, setExpanded] = useState(false);
  const actionBusy = false;
  const actionLabel = (title === "Secret Task") && runtime.running ? "common.stop" : automationActionLabels[title];
  const toggleLabel = "automation.autoExecute";
  return (
    <article className={`automation-card${enabled ? " is-enabled" : ""}`} data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{english(title)}</h3>
          {description ? <p>{["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title) ? t("automation.chatClaimDescription", { type: english(title) }) : english(description)}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled || actionBusy || (title === "Automatic Official Application" && Number(config.draft.positionId ?? 0) === 0)} aria-label={`${t(toggleLabel)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
        </div>
      </div>
      <AutomationMeta online={previewEnabled || online} active={active} state={sourceAutomationState(runtimeState, active)} actionBusy={actionBusy} presentation={presentation} />
      {fieldError ? <div role="alert" className="automation-error">{t(fieldError)}</div> : visibleDraftError ? <div role="alert" className="automation-error">{t(visibleDraftError)}</div> : null}
      {(hasSettings || actionLabel) ? <div className="automation-config">
        {(settingsCollapsible || actionLabel) ? <div className="automation-config-actions">
          {hasSettings && settingsCollapsible ? <button className={`automation-config-trigger${expanded ? " is-open" : ""}`} type="button" aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>
            <span className="automation-config-trigger-label">
              <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
              <span>{t("settings.title")}</span>
            </span>
            <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 6l4 4 4-4" /></svg>
          </button> : null}
          {actionLabel ? <button type="button" className="automation-run-action" disabled title={actionBusy ? undefined : t("status.gameDisconnectedDisabled")}>{t(actionBusy ? "automation.runningEllipsis" : actionLabel)}</button> : null}
        </div> : null}
        {hasSettings ? <fieldset className="automation-config-body" data-draft-dirty={config.dirty || (title === "Secret Task" && assistConfig.dirty)} data-draft-saving={config.saving || (title === "Secret Task" && assistConfig.saving)} onBlur={() => config.store.flush().catch(() => {})} style={settingsCollapsible && !expanded ? { display: "none" } : undefined} disabled={!previewEnabled || actionBusy}><AutomationConfigPreview title={title} enabled={previewEnabled && !actionBusy} previewState={previewState} config={config} assistConfig={assistConfig} availableSquads={availableSquads} onLocalError={setFieldError} /></fieldset> : null}
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

function ResourceGatherCard({ previewEnabled, previewState, profileId = "", availableSquads, onConfigStatus }) {
  const { t, language } = useI18n();
  const [expanded, setExpanded] = useState(false);
  const settingsId = useId();
  const config = usePreviewConfig(() => previewEnabled ? previewResourceGatherConfig(previewState) : previewResourceGatherConfig("automation-gather-no-squads"), validPreviewResourceGatherConfig, previewState === "automation-gather-save-error", `automation:${profileId || "default"}:gather:${previewState}`);
  useEffect(() => { onConfigStatus?.("__resourceGather", config.error ? "error" : config.saving ? "saving" : "", config.store); }, [config.error, config.saving, config.store, onConfigStatus]);
  const runtime = previewResourceGatherRuntime(previewState);
  const enabled = config.draft.enabled === true;
  const configuredSquads = config.draft.squads ?? [];
  const squadIndexes = [...new Set([...availableSquads, ...configuredSquads.map((squad) => squad.squadIndex), ...(runtime.gatherSquadIndexes ?? [])])].sort((a, b) => a - b);
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
      {error ? <div className="automation-error" role="alert">{t(error)}</div> : null}
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
          {squads.map((squad) => { const resource = runtime.gatherResources?.find((entry) => entry.resource === squad.resource); const squadRuntime = runtime.gatherSquads?.find((entry) => entry.squadIndex === squad.squadIndex); const state = ["recalling", "recall_failed", "state_unconfirmed"].includes(squadRuntime?.step) ? squadRuntime.step : enabled && squad.enabled ? runtime.step === "runtime_wait" ? "runtime_wait" : squadRuntime?.step ?? "idle" : "disabled"; return <div className="automation-resource-gather-squad" role="group" aria-label={t("automation.squad", { index: squad.squadIndex })} key={squad.squadIndex}><label className="automation-resource-gather-enable"><input type="checkbox" checked={squad.enabled} onChange={(event) => updateSquad(squad.squadIndex, { enabled: event.target.checked })} /><span>{t("automation.squad", { index: squad.squadIndex })}</span></label><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.resource")}`} value={squad.resource} onChange={(event) => updateSquad(squad.squadIndex, { resource: event.target.value })}>{["metal","food","gold"].map((resourceKey) => <option value={resourceKey} key={resourceKey}>{t("common.loading")}</option>)}</select><select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.level")}`} value={squad.level} onChange={(event) => updateSquad(squad.squadIndex, { level: Number(event.target.value) })}>{Array.from({ length: Math.min(10, resource?.maxLevel ?? 10) }, (_, index) => index + 1).map((level) => <option value={level} disabled={level > (resource?.maxLevel ?? 10)} key={level}>{level}</option>)}</select><span className="automation-resource-gather-state muted">{t(`automation.resourceGather.state.${state}`)}{enabled && squad.enabled && squadRuntime?.pauseReason === "manual_wait" && squadRuntime.manualResumeAt ? <small>{t("automation.resourceGather.manualResumeAt", { time: new Date(squadRuntime.manualResumeAt).toLocaleTimeString(language) })}</small> : null}{enabled && squad.enabled && squadRuntime?.pauseReason === "treasure" ? <small>{t("automation.resourceGather.treasureBorrowed")}</small> : null}{enabled && squad.enabled && squadRuntime?.shieldEndAt ? <small>{t("automation.resourceGather.shieldEndAt", { time: new Date(squadRuntime.shieldEndAt).toLocaleString(language) })}</small> : null}</span></div>; })}
        </fieldset>
      </div>
    </article>
  );
}

function TradeStationCard({ previewEnabled, previewState = "", profileId = "" }) {
  const { language, t } = useI18n();
  const fixture = previewTradeFixture(previewEnabled ? previewState : "");
  const config = usePreviewConfig(() => ({ enabled: false, crossServerEnabled: false, selectedItemIds: previewState === "automation-trade-positive" || previewState === "automation-trade-history" ? [7001] : [], selectedCurrencyIds: [15, 650053] }), (draft) => typeof draft.enabled === "boolean" && typeof draft.crossServerEnabled === "boolean" && Array.isArray(draft.selectedItemIds) && Array.isArray(draft.selectedCurrencyIds) && draft.selectedCurrencyIds.length > 0, previewState === "automation-trade-save-error", `automation:${profileId || "default"}:trade:${previewState}`);
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
            {fixture.error ? <div className="automation-error">{fixture.error}</div> : null}
          </fieldset>
        </div>
      </article>
    </div>
  );
}

export function AutomationPage({ previewState = "", profileId = "", online = false, activeCategory, onActiveCategoryChange }) {
  const { english, t } = useI18n();
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
  const onConfigStatus = (title, state, store) => setConfigStates((current) => current[title]?.state === state && current[title]?.store === store ? current : { ...current, [title]: { state, store } });
  const aggregateState = Object.values(configStates).some((entry) => entry.state === "error") ? "error" : Object.values(configStates).some((entry) => entry.state === "saving") ? "saving" : "";
  const previewEnabled = previewState.startsWith("automation-");
  const discoveredSquads = previewAutomationAvailableSquads(previewState);
  const [availableSquads, setAvailableSquads] = useState([]);
  useEffect(() => {
    if (previewEnabled && discoveredSquads.length > 0) setAvailableSquads(discoveredSquads);
  }, [previewEnabled, previewState, discoveredSquads.join(",")]);
  const renderCards = (cards) => cards.map(([title, description]) => !previewEnabled && ["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title)
    ? <AutomationCardBoundary key={`${profileId}:${title}`} name={title} title={english(title)} message={t("common.actionFailed")} retryLabel={t("common.retry")}><NativeChatAutomationCard title={title} description={description} online={online} profileId={profileId} availableSquads={[1, 2, 3, 4]} /></AutomationCardBoundary>
    : !previewEnabled && AUTOMATION_TASKS[title] ? <AutomationCardBoundary key={`${profileId}:${title}`} name={AUTOMATION_TASKS[title]} title={english(title)} message={t("common.actionFailed")} retryLabel={t("common.retry")}><NativeAutomationCard title={title} description={description} profileId={profileId} online={online} /></AutomationCardBoundary>
    : !previewEnabled ? <article key={title} className="automation-card"><h3>{english(title)}</h3><p>{english(description)}</p><p role="status">{t("common.notChecked")}</p></article>
    : <AutomationCard key={title} title={title} description={description} online={online} previewEnabled={previewEnabled} previewState={previewState} profileId={profileId} onConfigStatus={onConfigStatus} availableSquads={availableSquads} />);
  const pageStatus = aggregateState === "error" ? "automation.configSave.error" : aggregateState === "saving" ? "automation.configSave.saving" : previewState === "automation-saving"
    ? "automation.configSave.saving"
    : previewState === "automation-save-error"
      ? "automation.configSave.error"
      : previewState === "automation-saved"
        ? "automation.configSave.saved"
        : "";
  return (
    <section className="panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      {Object.entries(configStates).map(([title, entry]) => entry.state === "error" && entry.store ? <PreviewConfigError key={title} config={{ error: true, saving: false, store: entry.store }} t={t} label={title === "__resourceGather" ? t("automation.category.resourceGather") : title === "Dispatch Assist" ? t("automation.dispatchAssist") : english(title)} /> : null)}
      <PanelTitle title={t("nav.automation")} subtitle={previewEnabled || online ? (pageStatus ? t(pageStatus) : t("status.gameConnected")) : t("status.gameDisconnectedDisabled")} subtitleClassName={pageStatus === "automation.configSave.error" ? "error-text" : "muted"} />
      <div className="automation-categories" role="tablist" aria-label={t("nav.automation")}>
        {automationCategories.map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            data-automation-category={key}
            className={category === key ? "active" : ""}
            aria-selected={category === key}
            onClick={() => selectCategory(key)}
          >
            {t(automationCategoryKeys[key]) || label}
          </button>
        ))}
      </div>
      <div className="automation-grid">
        {visitedCategories.has("resourceGather") ? <Activity mode={category === "resourceGather" ? "visible" : "hidden"}>{previewEnabled ? <ResourceGatherCard previewEnabled={previewEnabled} previewState={previewState} profileId={profileId} availableSquads={availableSquads} onConfigStatus={onConfigStatus} /> : <AutomationCardBoundary key={`${profileId}:gather`} name="resourceGather" title={t("automation.category.resourceGather")} message={t("common.actionFailed")} retryLabel={t("common.retry")}><NativeGatherCard profileId={profileId} online={online} /></AutomationCardBoundary>}</Activity> : null}
        {visitedCategories.has("trade") ? <Activity mode={category === "trade" ? "visible" : "hidden"}>{previewEnabled ? <TradeStationCard previewEnabled={previewEnabled} previewState={previewState} profileId={profileId} /> : <AutomationCardBoundary key={`${profileId}:trade`} name="tradeStation" title={t("automation.tradeStation.title")} message={t("common.actionFailed")} retryLabel={t("common.retry")}><NativeTradeCard profileId={profileId} online={online} /></AutomationCardBoundary>}</Activity> : null}
        {visitedCategories.has("chat") ? <Activity mode={category === "chat" ? "visible" : "hidden"}>{renderCards(automationCards.chat)}</Activity> : null}
        {visitedCategories.has("daily") ? <Activity mode={category === "daily" ? "visible" : "hidden"}>{renderCards(automationCards.daily)}</Activity> : null}
        {visitedCategories.has("alliance") ? <Activity mode={category === "alliance" ? "visible" : "hidden"}>{renderCards(automationCards.alliance)}</Activity> : null}
      </div>
    </section>
  );
}
