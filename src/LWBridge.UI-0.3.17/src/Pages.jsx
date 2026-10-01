import { useEffect, useMemo, useState } from "react";
import { MapDataPage } from "./MapDataPage.jsx";
import { useI18n } from "./i18n.jsx";

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
  return (
    <button
      type="button"
      className="toggle-row"
      role="switch"
      aria-checked={checked}
      aria-label={`${label}: ${checked ? "Enabled" : "Disabled"}`}
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
    error: "",
    production: false,
  };
  switch (name) {
    case "home-checking": return { ...base, rootResolved: false, gameRootStatus: null, proxyStatus: null };
    case "home-missing": return { ...base, gameRootStatus: { valid: false, root: "" } };
    case "home-launching": return { ...base, busy: "launch" };
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
    default: return null;
  }
}

function translatedError(t, value) {
  if (!value) return "";
  const code = typeof value === "string" ? value : value.code || value.message || String(value);
  const translated = t(`error.${code}`);
  return translated === `error.${code}` ? code : translated;
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
  const launching = state.busy === "launch";
  const rootBusy = state.busy === "gameRoot";
  const showRootPicker = rootResolved && !rootValid;
  const repairOnClose = rootResolved && rootValid && gameRunning && repairRequired && !recovering;
  const lifecycleProviderAvailable = false;
  const canStart = lifecycleProviderAvailable && rootResolved && rootValid && !gameRunning && !recovering && !launching;
  const canStop = lifecycleProviderAvailable && rootResolved && rootValid && (gameRunning || recovering) && !launching;

  let status = t("setup.checking");
  if (rootResolved) {
    if (showRootPicker) status = t("setup.gameRootMissing");
    else if (launching) status = t("setup.launchingGame");
    else if (state.busy && state.busy !== "autoLaunchGame" && state.busy !== "autoReconnect") status = t("common.processing");
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
          <span>{state.error ? translatedError(t, state.error) : t("setup.gameRootMissing")}</span>
          <button className="primary" type="button" disabled={rootBusy || !state.production} onClick={onGameRootSelect}>
            {t(rootBusy ? "common.processing" : "setup.gameRootSelect")}
          </button>
        </div>
      ) : (
        <div className="game-controls">
          {!repairOnClose ? (
            <button className="primary" type="button" disabled={!canStart}>{t(launching ? "setup.launchingGame" : "top.launchGame")}</button>
          ) : null}
          <button type="button" disabled={!canStop}>
            {t(repairOnClose ? "setup.updateAndLaunch" : "setup.closeGameAction")}
          </button>
          {repairOnClose ? <span className="muted">{t("setup.updateCloseGame")}</span> : null}
        </div>
      ) : null}
      {state.error && !showRootPicker ? <span className="game-root-error">{translatedError(t, state.error)}</span> : null}
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

function WeeklyQualityPreview({ enabled, defaults }) {
  const { language, t } = useI18n();
  const days = Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat(language, { weekday: "short", timeZone: "UTC" }).format(new Date(Date.UTC(2024, 0, index + 1))));
  return <div className="automation-weekly-quality">{days.map((day, index) => <label key={day}><span>{day}</span><select defaultValue={defaults[index]} disabled={!enabled}><option value="none">{t("automation.noQualityRefresh")}</option><option value="ssr">{t("automation.ssrOrAbove")}</option><option value="ur">UR</option></select></label>)}</div>;
}

function AutomationConfigPreview({ title, enabled }) {
  const { t } = useI18n();
  const [constructionTargetEnabled, setConstructionTargetEnabled] = useState(true);
  const [replyEnabled, setReplyEnabled] = useState(false);
  const [treasureDispatchEnabled, setTreasureDispatchEnabled] = useState(false);
  const [dispatchAssistEnabled, setDispatchAssistEnabled] = useState(false);
  const [ghostJoinEnabled, setGhostJoinEnabled] = useState(false);
  const [trainMode, setTrainMode] = useState("reward");
  const [vipTrainMode, setVipTrainMode] = useState("reward");
  const [thanksMode, setThanksMode] = useState("like");
  if (title === "Auto Training") {
    return <div className="automation-form-grid"><label><span>{t("automation.soldierTraining.totalCount")}</span><input type="number" min="1" max="1000000" defaultValue="1000" disabled={!enabled} /></label><label><span>{t("automation.soldierTraining.target")}</span><select defaultValue="highest" disabled={!enabled}><option value="highest">{t("automation.soldierTraining.highest")}</option></select></label></div>;
  }
  if (title === "Automatic Construction") {
    return <><label className="automation-checkbox-row"><input type="checkbox" checked={constructionTargetEnabled} disabled={!enabled} onChange={(event) => setConstructionTargetEnabled(event.target.checked)} /><span>{t("automation.construction.targetEnabled")}</span></label>{constructionTargetEnabled ? <><div className="automation-actions"><label><span>{t("automation.construction.targetLevel")}</span><input type="number" min="1" max="100" step="1" defaultValue="30" disabled={!enabled} /></label></div><details className="construction-type-select"><summary><span>{t("automation.construction.buildingTypes")}</span><strong>{t("automation.construction.selectTypes")}</strong><span aria-hidden="true">⌄</span></summary><p className="muted">{t("automation.construction.selectTypes")}</p></details></> : null}<label className="automation-checkbox-row"><input type="checkbox" defaultChecked disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label><span>{t("automation.maxBuilders")}</span><input type="number" min="1" max="20" step="1" defaultValue="1" disabled={!enabled} /></label></div></>;
  }
  if (title === "Automatic Treatment") {
    return <div className="automation-actions"><label><span>{t("automation.treatmentAmountPerArmy")}</span><input type="number" min="1" max="1000000" step="1" defaultValue="1" disabled={!enabled} /></label></div>;
  }
  if (title === "Automatic Official Application") {
    return <div className="automation-actions"><label><span>{t("automation.targetPosition")}</span><select defaultValue="0" disabled={!enabled}><option value="0">{t("automation.position.none")}</option><option value="10002">{t("automation.position.vicePresident")}</option><option value="10003">{t("automation.position.strategyMinister")}</option><option value="10004">{t("automation.position.defenseMinister")}</option><option value="10005">{t("automation.position.constructionMinister")}</option><option value="10006">{t("automation.position.scienceMinister")}</option><option value="10007">{t("automation.position.internalAffairsMinister")}</option></select></label></div>;
  }
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) {
    const treasure = title === "Treasure";
    return <><section className="automation-settings-section"><h3>{t("automation.section.claim")}</h3>{treasure ? <p className="hint">{t("automation.treasureTargetDelayHint")}</p> : null}<div className="automation-form-grid"><label><span>{t("automation.minDelaySeconds")}</span><input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" defaultValue="0" disabled={!enabled} /></label><label><span>{t("automation.maxDelaySeconds")}</span><input type="number" min="0" max={treasure ? "600" : "60"} step="0.01" defaultValue="0" disabled={!enabled} /></label></div></section><section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" checked={replyEnabled} disabled={!enabled} onChange={(event) => setReplyEnabled(event.target.checked)} /><span>{t("automation.autoReply")}</span></label>{replyEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label><span>{t("automation.replyDelayMin")}</span><input type="number" min="0.1" max="600" step="0.1" defaultValue="2" /></label><label><span>{t("automation.replyDelayMax")}</span><input type="number" min="0.1" max="600" step="0.1" defaultValue="5" /></label></div><label className="automation-replies"><span>{t("automation.replyPhrases")}</span><textarea rows="4" defaultValue="" /></label></div> : null}</section>{treasure ? <section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.treasureAutoSearch")}</span></label><p className="muted">{t("automation.treasureAutoSearchHint")}</p><label className="automation-checkbox-row"><input type="checkbox" checked={treasureDispatchEnabled} disabled={!enabled} onChange={(event) => setTreasureDispatchEnabled(event.target.checked)} /><span>{t("automation.treasureAutoDispatch")}</span></label>{treasureDispatchEnabled ? <div className="automation-subsettings"><div className="automation-form-grid"><label><span>{t("automation.dispatchDelayMin")}</span><input type="number" min="0.1" max="600" step="0.1" defaultValue="2" /></label><label><span>{t("automation.dispatchDelayMax")}</span><input type="number" min="0.1" max="600" step="0.1" defaultValue="5" /></label></div><label><span>{t("automation.treasureDispatchRetrySeconds")}</span><input type="number" min="1" max="300" step="1" defaultValue="30" /></label><div className="automation-squad-choices">{[1,2,3,4].map((index) => <label key={index}><input type="checkbox" defaultChecked={index === 1} /><span>{t("automation.squad", { index })}</span></label>)}</div><p className="muted">{t("automation.treasureDispatchPriorityHint")}</p></div> : null}</section> : null}</>;
  }
  if (title === "Trucks") {
    return <><section className="automation-settings-section"><h3>{t("automation.section.schedule")}</h3><div className="automation-actions"><label><span>{t("automation.delayAfterResetMinutes")}</span><input type="number" min="0" max="1440" step="1" defaultValue="2" disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} defaults={["ssr","ur","ssr","ssr","ssr","ur","ssr"]} /></section><section className="automation-settings-section"><h3>{t("automation.section.departure")}</h3><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.railwayDepartWhenTicketsInsufficient")}</span></label></section></>;
  }
  if (title === "Secret Task") {
    return <><section className="automation-settings-section"><h3>{t("squad.afkExecutionSettings")}</h3><label className="automation-checkbox-row"><input type="checkbox" defaultChecked disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label><div className="automation-actions"><label><span>{t("automation.delayAfterResetMinutes")}</span><input type="number" min="0" max="1440" step="1" defaultValue="3" disabled={!enabled} /></label></div></section><section className="automation-settings-section"><h3>{t("automation.section.quality")}</h3><WeeklyQualityPreview enabled={enabled} defaults={["none","ur","none","none","none","ur","none"]} /></section><section className="automation-settings-section"><label className="automation-checkbox-row"><input type="checkbox" checked={dispatchAssistEnabled} disabled={!enabled} onChange={(event) => setDispatchAssistEnabled(event.target.checked)} /><span>{t("automation.dispatchAssist")}</span></label>{dispatchAssistEnabled ? <><div className="automation-squad-choices">{["n","r","sr","ssr","ur","special"].map((quality) => <label key={quality}><input type="checkbox" defaultChecked={quality === "ur"} /><span>{quality === "special" ? t("automation.assistQuality.special") : quality.toUpperCase()}</span></label>)}</div><div className="automation-form-grid"><label><span>{t("automation.minDelaySeconds")}</span><input type="number" min="0" max="86400" step="1" defaultValue="0" /></label><label><span>{t("automation.maxDelaySeconds")}</span><input type="number" min="0" max="86400" step="1" defaultValue="0" /></label><label><span>{t("automation.assistIntervalSeconds")}</span><input type="number" min="5" max="300" step="1" defaultValue="30" /></label></div></> : <><strong>{t("automation.allySecretTasks")}</strong><span className="muted">{t("automation.noAllySecretTasks")}</span></>}</section></>;
  }
  if (title === "Ghost Ops") {
    return <><label className="automation-checkbox-row"><input type="checkbox" checked={ghostJoinEnabled} disabled={!enabled} onChange={(event) => setGhostJoinEnabled(event.target.checked)} /><span>{t("automation.ghost.autoJoinAlliance")}</span></label>{ghostJoinEnabled ? <div className="automation-section"><strong>{t("automation.ghost.allianceFilter")}</strong><div className="automation-compact-choice-group">{["sr","ur","special"].map((filter) => <label className="automation-compact-choice" key={filter}><input type="radio" name="preview-ghost-filter" value={filter} defaultChecked={filter === "special"} disabled={!enabled} /><span>{t(`automation.ghost.filter.${filter}`)}</span></label>)}</div></div> : null}<label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.ghost.autoClaimRewards")}</span></label><div className="automation-inline-status"><span>{t("automation.ghost.ownPending")}: 0</span><span>{t("automation.ghost.allianceCandidates")}: 0</span><span>{t("automation.claimed")}: 0</span></div></>;
  }
  if (title === "Automatic Alliance Train Boarding") {
    return <><section className="automation-settings-section"><strong>{t("automation.normalCarriageSelection")}</strong><span className="muted">{t("automation.normalCarriageHint")}</span><div className="automation-compact-choice-group">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-train-mode" checked={trainMode === mode} onChange={() => setTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</div>{trainMode === "fixed" ? <div className="automation-compact-choice-group">{[1,2,3,4].map((carriage) => <label className="automation-compact-choice" key={carriage}><input type="radio" name="preview-normal-carriage" defaultChecked={carriage === 1} /><span>{t(`automation.carriage${carriage}`)}</span></label>)}</div> : <p className="muted">{t("automation.noPreferredRewards")}</p>}</section><section className="automation-settings-section"><strong>{t("automation.vipCarriageSelection")}</strong><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.autoAcceptTrainVip")}</span></label><span className="muted">{t("automation.vipCarriageHint")}</span><div className="automation-compact-choice-group">{["reward","fixed"].map((mode) => <label className="automation-compact-choice" key={mode}><input type="radio" name="preview-vip-train-mode" checked={vipTrainMode === mode} onChange={() => setVipTrainMode(mode)} /><span>{t(mode === "reward" ? "automation.rewardSelectionMode" : "automation.fixedSelectionMode")}</span></label>)}</div>{vipTrainMode === "fixed" ? <div className="automation-compact-choice-group">{[1,2,3,4].map((carriage) => <label className="automation-compact-choice" key={carriage}><input type="checkbox" defaultChecked={carriage < 3} /><span>{t(`automation.carriage${carriage}`)}</span></label>)}</div> : null}</section><section className="automation-settings-section"><h3>{t("automation.section.additional")}</h3><div className="automation-actions"><label><span>{t("automation.thanksMode")}</span><select value={thanksMode} onChange={(event) => setThanksMode(event.target.value)}><option value="like">{t("automation.thanksLike")}</option><option value="tickets">{t("automation.thanksTickets")}</option></select></label>{thanksMode === "tickets" ? <label><span>{t("automation.ticketCount")}</span><select defaultValue="1"><option>1</option><option>2</option><option>3</option></select></label> : null}</div><p className="muted">{t("automation.ticketFallbackLike")}</p></section></>;
  }
  if (title === "Alliance Tech Donations") {
    return <div className="automation-actions"><label><span>{t("automation.donateThreshold")}</span><input type="number" min="1" max="30" step="1" defaultValue="15" disabled={!enabled} /></label></div>;
  }
  if (["Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources", "Building Resource Collection", "Armed Truck"].includes(title)) {
    const defaultInterval = title === "Alliance Gifts" ? 120 : 60;
    return <div className="automation-actions"><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" max="1440" step="1" defaultValue={defaultInterval} disabled={!enabled} /></label></div>;
  }
  if (title === "Alliance Gathering Dispatch") {
    return <><p className="muted">{t("automation.allianceGatherSquadPriority")}</p><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("automation.allianceGatherSquadPriority")}>{[1,2,3,4].map((index) => <label key={index}><input type="checkbox" defaultChecked={index < 3} disabled={!enabled} /><span>{t("automation.squad", { index })}</span></label>)}</div></>;
  }
  return null;
}

function AutomationCard({ title, description, previewEnabled }) {
  const { english, t } = useI18n();
  const [enabled, setEnabled] = useState(false);
  const settingsCollapsible = !nonCollapsibleAutomationSettings.has(title);
  const hasSettings = !automationCardsWithoutSettings.has(title);
  const [expanded, setExpanded] = useState(previewEnabled && title === "Automatic Construction");
  const actionLabel = automationActionLabels[title];
  const toggleLabel = title === "Ghost Ops" ? "automation.ghost.autoStartOwn" : "automation.autoExecute";
  return (
    <article className="automation-card" data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{english(title)}</h3>
          {description ? <p>{english(description)}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled} aria-label={`${t(toggleLabel)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
        </div>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.enabled" : "common.disabled") : t("status.disconnected")}</span>
      </div>
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
        {hasSettings && (!settingsCollapsible || expanded) ? <fieldset className="automation-config-body" disabled={!previewEnabled}><AutomationConfigPreview title={title} enabled={previewEnabled} /></fieldset> : null}
      </div> : null}
    </article>
  );
}

function ResourceGatherCard({ previewEnabled }) {
  const { t } = useI18n();
  const [enabled, setEnabled] = useState(false);
  return (
    <article className="automation-card" data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{t("automation.category.resourceGather")}</h3>
          <p>{t("automation.resourceGather.description")}</p>
        </div>
        <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.enabled" : "common.disabled") : t("status.disconnected")}</span>
      </div>
      <div className="automation-config">
        <fieldset className="automation-config-body" disabled={!previewEnabled}>
          <div className="automation-resource-gather-options">
            <label className="automation-resource-gather-radius">
              <span>{t("automation.resourceGather.scanRadius")}</span>
              <select value="200" readOnly><option value="200">200</option></select>
            </label>
            <label>
              <span>{t("automation.resourceGather.manualResumeDelay")}</span>
              <select value="2" readOnly><option value="2">2</option></select>
            </label>
            <label className="automation-resource-gather-recall">
              <input type="checkbox" readOnly />
              <span>{t("automation.resourceGather.recallOnDisable")}</span>
            </label>
            <small className="automation-resource-gather-radius-hint muted">
              {t("automation.resourceGather.scanRadiusHint", { size: 1000 })}
            </small>
          </div>
          <p className="muted">{t("automation.resourceGather.squadsLoading")}</p>
        </fieldset>
      </div>
    </article>
  );
}

function TradeStationCard({ previewEnabled }) {
  const { t } = useI18n();
  const [enabled, setEnabled] = useState(false);
  const [tradeTab, setTradeTab] = useState("goods");
  return (
    <div className="trade-station-panel" data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <article className="automation-card">
        <div className="automation-card-header">
          <div className="automation-card-title-group">
            <h3>{t("automation.tradeStation.title")}</h3>
            <p>{t("automation.tradeStation.description")}</p>
          </div>
          <div className="automation-card-header-actions">
            <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
          </div>
        </div>
        <div className="automation-card-meta-row" role="status">
          <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.enabled" : "common.disabled") : t("status.disconnected")}</span>
        </div>
        <div className="automation-config">
          <fieldset className="automation-config-body" disabled={!previewEnabled}>
            <div className="trade-station-warning">
              {t("automation.tradeStation.warning")}
            </div>
            <ToggleRow label={t("automation.tradeStation.crossServer")} disabled={!previewEnabled} />
            <fieldset className="trade-station-currencies">
              <legend>{t("automation.tradeStation.currencies")}</legend>
              <div />
            </fieldset>
            <div className="trade-station-stats">
              <span>{t("automation.tradeStation.detected")}: 0</span>
              <span>{t("automation.tradeStation.attempted")}: 0</span>
              <span>{t("automation.tradeStation.succeeded")}: 0</span>
              <span>{t("automation.tradeStation.lastResult")}: -</span>
            </div>
            <div className="trade-station-tabs" role="tablist">
              <button type="button" role="tab" aria-selected={tradeTab === "goods"} className={tradeTab === "goods" ? "active" : ""} onClick={() => setTradeTab("goods")}>{t("automation.tradeStation.goods")}</button>
              <button type="button" role="tab" aria-selected={tradeTab === "history"} className={tradeTab === "history" ? "active" : ""} onClick={() => setTradeTab("history")}>{t("automation.tradeStation.purchasedItems", { count: 0 })}</button>
            </div>
            {tradeTab === "goods" ? <div className="trade-station-goods" role="tabpanel">
              <div className="trade-station-goods-heading">
                <strong>{t("automation.tradeStation.goodsToBuy")}</strong>
                <label><input type="checkbox" disabled={!previewEnabled} />{t("automation.tradeStation.showExclusive")}</label>
              </div>
              <span className="muted">{t("automation.tradeStation.noGoods")}</span>
            </div> : <div className="trade-station-purchase-history" role="tabpanel"><span className="muted">{t("automation.tradeStation.noPurchases")}</span></div>}
          </fieldset>
        </div>
      </article>
    </div>
  );
}

export function AutomationPage({ previewState = "" }) {
  const { t } = useI18n();
  const [category, setCategory] = useState("daily");
  const previewEnabled = previewState === "automation-config";
  return (
    <section className="panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("nav.automation")} subtitle={previewEnabled ? t("common.available") : t("status.gameDisconnected")} />
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
      <div className="automation-grid">
        {category === "resourceGather" ? <ResourceGatherCard previewEnabled={previewEnabled} /> : null}
        {category === "trade" ? <TradeStationCard previewEnabled={previewEnabled} /> : null}
        {(automationCards[category] ?? []).map(([title, description]) => (
          <AutomationCard key={title} title={title} description={description} previewEnabled={previewEnabled} />
        ))}
      </div>
    </section>
  );
}

export function SquadsPage({ previewState = "" }) {
  const { t } = useI18n();
  const equipmentPreview = previewState.startsWith("squads-equipment");
  const previewEnabled = previewState === "squads-profile" || equipmentPreview;
  const [tab, setTab] = useState(equipmentPreview ? "equipment" : "afk");
  return (
    <section className="panel squad-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <div className="squad-header">
        <h2>{t("squad.title")}</h2>
        {tab === "equipment" ? <button type="button" disabled={!previewEnabled}>{t("squad.refresh")}</button> : null}
      </div>
      <div className="squad-tabs" role="tablist" aria-label={t("squad.title")}>
        <button type="button" role="tab" className={tab === "afk" ? "active" : ""} aria-selected={tab === "afk"} onClick={() => setTab("afk")}>{t("squad.tabAfk")}</button>
        <button type="button" role="tab" className={tab === "equipment" ? "active" : ""} aria-selected={tab === "equipment"} onClick={() => setTab("equipment")}>{t("squad.tabEquipment")}</button>
      </div>
      {tab === "afk" ? <AfkContent previewEnabled={previewEnabled} /> : <EquipmentContent previewEnabled={previewEnabled} previewState={previewState} />}
    </section>
  );
}

function AfkProfileEditor({ enabled }) {
  const { t } = useI18n();
  const [joinMode, setJoinMode] = useState("slot");
  return (
    <section className="monster-afk-editor is-editing" data-preview-fixture="afk-profile-editor">
      <div className="monster-afk-editor-heading"><strong>{t("squad.afkNewProfile")}</strong><span className="monster-afk-mode-badge attack">{t("squad.afkFarmStrategies")}</span></div>
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkBasicSettings")}</strong>
        <div className="monster-afk-basic-grid">
          <label><span>{t("squad.afkProfileName")}</span><input defaultValue="Steel Hunt" disabled={!enabled} /></label>
          <label><span>{t("squad.afkTarget")}</span><select defaultValue="steel" disabled={!enabled}><option value="steel">{t("squad.afkResourceMetal")}</option><option value="food">{t("squad.afkResourceFood")}</option><option value="gold">{t("squad.afkResourceGold")}</option><option value="boss">{t("squad.afkResourceBoss")}</option></select></label>
          <label className="monster-afk-field-wide"><span>{t("squad.afkCustomName")}</span><input placeholder={t("squad.afkCustomTargetHint")} disabled={!enabled} /></label>
        </div>
        <div className="monster-afk-check-row"><label><input type="checkbox" defaultChecked disabled={!enabled} />{t("squad.afkActiveAttack")}</label><label><input type="checkbox" disabled={!enabled} />{t("squad.afkContinuousAttack")}</label></div>
      </div>
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkAssignments")}</strong>
        <div className="monster-afk-squads">{[1,2,3,4].map((number) => <label className="monster-afk-squad-toggle" key={number}><input type="checkbox" defaultChecked={number === 1} disabled={!enabled} /><span>{t("squad.number", { number })}</span></label>)}</div>
      </div>
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkExecutionSettings")}</strong>
        <div className="monster-afk-basic-grid monster-afk-execution-grid">
          <label><span>{t("squad.afkExecutionLimitLabel")}</span><input type="number" min="0" defaultValue="0" disabled={!enabled} /></label>
          <label><span>{t("squad.afkRepeatPerLevel")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label>
          <label><span>{t("squad.maxDistance")}</span><input type="number" min="0" defaultValue="0" disabled={!enabled} /></label>
          <label><input type="checkbox" defaultChecked disabled={!enabled} />{t("squad.loop")}</label>
        </div>
      </div>
      <div className="monster-afk-config-section monster-afk-filter">
        <strong>{t("squad.afkTargetFilters")}</strong>
        <div className="monster-afk-number-grid"><label><span>{t("squad.afkMinLevel")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label><label><span>{t("squad.afkMaxLevel")}</span><input type="number" min="1" defaultValue="99" disabled={!enabled} /></label></div>
      </div>
      <div className="monster-afk-config-section monster-afk-join-section">
        <strong>{t("squad.afkJoinConditions")}</strong>
        <label className="monster-afk-check-row"><input type="checkbox" disabled={!enabled} />{t("squad.afkJoin")}</label>
        <div className="rally-join-settings">
          <label><span>{t("squad.join.mode")}</span><select value={joinMode} disabled={!enabled} onChange={(event) => setJoinMode(event.target.value)}><option value="slot">{t("squad.join.mode.slot")}</option><option value="delay">{t("squad.join.mode.delay")}</option></select></label>
          {joinMode === "slot" ? <div className="rally-join-range"><label><span>{t("squad.join.slotRange.min")}</span><input type="number" min="2" max="5" defaultValue="2" disabled={!enabled} /></label><label><span>{t("squad.join.slotRange.max")}</span><input type="number" min="2" max="5" defaultValue="5" disabled={!enabled} /></label></div> : <div className="rally-join-range"><label><span>{t("squad.join.delaySeconds.min")}</span><input type="number" min="0" max="6000" defaultValue="0" disabled={!enabled} /></label><label><span>{t("squad.join.delaySeconds.max")}</span><input type="number" min="0" max="6000" defaultValue="5" disabled={!enabled} /></label></div>}
          <label><span>{t("squad.join.list")}</span><select defaultValue="off" disabled={!enabled}><option value="off">{t("squad.join.list.off")}</option><option value="blacklist">{t("squad.join.list.blacklist")}</option><option value="whitelist">{t("squad.join.list.whitelist")}</option></select></label>
          <label><input type="checkbox" disabled={!enabled} />{t("squad.join.skipSolo")}</label><label><input type="checkbox" disabled={!enabled} />{t("squad.join.skipKicked")}</label>
        </div>
      </div>
      <div className="automation-config-actions"><button type="button" className="primary" disabled={!enabled}>{t("common.saveConfig")}</button><button type="button" disabled={!enabled}>{t("common.cancel")}</button></div>
    </section>
  );
}

function AfkContent({ previewEnabled }) {
  const { t } = useI18n();
  const [showEditor, setShowEditor] = useState(previewEnabled);
  return (
    <div className="monster-afk-layout" data-preview-fixture={previewEnabled ? "squads-profile" : "runtime-config-unobserved"}>
      <div className="monster-afk-toolbar">
        <CompactAfkCard title={t("squad.afkMaster")} summary={t("common.disabled")} previewEnabled={previewEnabled} details={<p className="muted">{t("squad.afkMasterDescription")}</p>} />
        <CompactAfkCard title={t("squad.afkAllianceDrill")} summary={t("squad.afkAllianceDrillWaiting")} previewEnabled={previewEnabled} details={<><p>{t("squad.afkAllianceDrillDescription")}</p><fieldset disabled={!previewEnabled}><legend>{t("squad.afkAllianceDrillSquads")}</legend>{[1,2,3,4].map((number) => <label key={number}><input type="checkbox" defaultChecked={number === 1} />{t("squad.number", { number })}</label>)}</fieldset></>} />
        <CompactAfkCard title={t("garrison.title")} summary={t("common.disabled")} previewEnabled={previewEnabled} details={<div className="garrison-settings-grid"><label><input type="checkbox" disabled={!previewEnabled} />{t("garrison.center")}</label><label><input type="checkbox" disabled={!previewEnabled} />{t("garrison.attachment")}</label><label><input type="checkbox" disabled={!previewEnabled} />{t("garrison.recallOnDisable")}</label><button type="button" disabled={!previewEnabled}>{t("garrison.chooseAllies")}</button></div>} />
        <CompactAfkCard title={t("zombieBus.title")} summary={previewEnabled ? t("zombieBus.waiting") : t("status.disconnected")} previewEnabled={previewEnabled} details={<p className="muted">{t("zombieBus.description")}</p>} />
      </div>
      <section className="monster-afk-profiles">
        <div className="monster-section-title">
          <strong>{t("squad.afkProfiles")}</strong>
          <div className="monster-afk-add-control"><button type="button" disabled={!previewEnabled} onClick={() => setShowEditor(true)}>{t("common.add")}</button></div>
        </div>
        <div className="monster-afk-profile-list">
          {previewEnabled ? <article className="monster-afk-profile-card"><div className="monster-afk-profile-heading"><strong>Steel Hunt</strong><span className="monster-afk-mode-badge attack">{t("squad.afkActionAttack")}</span></div><span>{t("squad.afkBoundSquads", { count: 1 })}</span><button type="button" onClick={() => setShowEditor(true)}>{t("squad.afkEditProfile")}</button></article> : <span className="muted">{t("squad.afkNoProfiles")}</span>}
        </div>
        {showEditor ? <AfkProfileEditor enabled={previewEnabled} /> : null}
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

function CompactAfkCard({ title, summary, previewEnabled, details }) {
  const [enabled, setEnabled] = useState(false);
  const [expanded, setExpanded] = useState(false);
  return (
    <article className="automation-card monster-afk-compact-card is-selectable" title={title}>
      <div className="automation-card-header">
        <div><h3>{title}</h3><p>{summary}</p></div>
        <div className="monster-afk-compact-actions">
          <button type="button" disabled={!previewEnabled} aria-label="Settings" aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}><SettingsGlyph /></button>
          <label className={`monster-afk-compact-toggle ${enabled ? "enabled" : ""}`}>
            <input type="checkbox" disabled={!previewEnabled} checked={enabled} onChange={(event) => setEnabled(event.target.checked)} aria-label={title} />
            <span className="monster-afk-master-track" aria-hidden="true" />
          </label>
        </div>
      </div>
      {expanded && details ? <div className="monster-afk-toolbar-settings">{details}</div> : null}
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

export function CityLayoutPage({ previewState = "" }) {
  const { t } = useI18n();
  const previewEnabled = previewState === "city-layout-populated";
  const [zoom, setZoom] = useState(24);
  const [selectedBuilding, setSelectedBuilding] = useState(previewEnabled ? 1 : null);
  const [hasChanges, setHasChanges] = useState(previewEnabled);
  if (!previewEnabled) return <div className="panel city-layout-empty">{t("cityLayout.offline")}</div>;

  const cells = Array.from({ length: 64 }, (_, index) => index);
  const buildings = [
    { id: 1, name: "HQ", level: 30, x: 2, y: 2, width: 2, height: 2, movable: false },
    { id: 2, name: "Barracks", level: 28, x: 5, y: 2, width: 2, height: 2, movable: true },
    { id: 3, name: "Hospital", level: 27, x: 3, y: 5, width: 2, height: 1, movable: true },
  ];
  const selected = buildings.find((building) => building.id === selectedBuilding) || null;
  return (
    <section className="panel city-layout-panel" data-preview-fixture="city-layout-populated">
      <div className="city-layout-header">
        <div><h2>{t("cityLayout.title")}</h2><span>{t("cityLayout.stats", { cells: 64, buildings: buildings.length, movable: buildings.filter((item) => item.movable).length })}</span></div>
        <div className="city-layout-actions">
          <button type="button">{t("common.refresh")}</button>
          <button type="button" disabled={!hasChanges} onClick={() => setHasChanges(false)}>{t("cityLayout.undo")}</button>
          <button type="button" disabled>{t("cityLayout.redo")}</button>
          <button type="button" onClick={() => setHasChanges(false)}>{t("cityLayout.restoreInitial")}</button>
          <button type="button">{t("cityLayout.saveDraft")}</button>
          <button type="button" className="primary" disabled>{t("cityLayout.apply")}</button>
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
            <div className="city-layout-grid" style={{ gridTemplateColumns: `repeat(8, ${zoom}px)`, gridAutoRows: `${zoom}px` }}>
              {cells.map((index) => <span key={index} className={`city-layout-cell${index % 11 === 0 ? " road" : index > 55 ? " locked" : index % 13 === 0 ? " flag-only" : " available"}`} />)}
              {buildings.map((building) => <button key={building.id} type="button" className={`city-layout-building${selectedBuilding === building.id ? " selected" : ""}`} style={{ gridColumn: `${building.x} / span ${building.width}`, gridRow: `${building.y} / span ${building.height}` }} onClick={() => setSelectedBuilding(building.id)}><span className="city-layout-building-name">{building.name}</span><span className="city-layout-building-level">{t("cityLayout.level", { level: building.level })}</span></button>)}
            </div>
          </div>
          <div className="city-layout-legend"><span className="available">{t("cityLayout.cell.available")}</span><span className="road">{t("cityLayout.cell.road")}</span><span className="flag-only">{t("cityLayout.cell.flagOnly")}</span><span className="locked">{t("cityLayout.cell.locked")}</span></div>
        </main>
        <aside className="city-layout-inspector">
          <strong>{t("cityLayout.properties")}</strong>
          {selected ? <div className="city-layout-selected"><div><strong>{selected.name}</strong><span>{t("cityLayout.level", { level: selected.level })}</span></div><dl><dt>{t("cityLayout.footprint")}</dt><dd>{selected.width}×{selected.height}</dd><dt>{t("cityLayout.current")}</dt><dd>{selected.x},{selected.y}</dd><dt>{t("cityLayout.target")}</dt><dd>{hasChanges ? `${selected.x + 1},${selected.y}` : `${selected.x},${selected.y}`}</dd><dt>{t("cityLayout.movable")}</dt><dd>{t(selected.movable ? "common.yes" : "common.no")}</dd><dt>{t("cityLayout.rule")}</dt><dd>{t(selected.movable ? "cityLayout.rule.normal" : "cityLayout.rule.flag")}</dd></dl></div> : <span className="muted">{t("cityLayout.selectBuilding")}</span>}
          <div className="city-layout-validation valid">{t("cityLayout.validation.valid")}</div>
          <div className="city-layout-changes"><strong>{hasChanges ? t("cityLayout.changes", { count: 1 }) : t("cityLayout.noChanges")}</strong>{hasChanges ? <button type="button" onClick={() => setHasChanges(false)}>{t("cityLayout.undo")}</button> : null}</div>
        </aside>
      </div>
      <footer className="city-layout-footer"><span>{t("cityLayout.draftRevision", { revision: 1 })}</span><span>{t("cityLayout.conflicts", { count: 0 })}</span></footer>
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
  const previewEnabled = previewState === "hotkeys-connected";
  return (
    <section className="panel hotkey-panel" data-preview-fixture={previewEnabled ? previewState : undefined}>
      <PanelTitle title={t("hotkeys.title")} subtitle={t("hotkeys.description")} />
      {!previewEnabled ? <div className="hotkey-status">{t("hotkeys.offlineHint")}</div> : null}
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
  const previewEnabled = previewState === "mini-games-active" || previewState === "mini-games-complete";
  const [chestEnabled, setChestEnabled] = useState(false);
  const [foodRunning, setFoodRunning] = useState(previewState === "mini-games-active");
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
          <div className="mini-game-actions"><button className="primary" type="button" disabled>{t("miniGames.landCellAction")}</button></div>
        </article>
        <article className="hotkey-card">
          <h3>{t("miniGames.sheep.title")}</h3>
          <p>{t("miniGames.sheep.description")}</p>
          {previewEnabled ? <span className="hotkey-state">{t("miniGames.sheep.level", { level: 4 })}</span> : null}
          {previewEnabled ? <span className="hotkey-state">{t("miniGames.sheep.elapsed", { time: foodRunning ? "00:31" : "00:00" })}</span> : null}
          <span className={`hotkey-state${previewState === "mini-games-complete" ? " enabled" : ""}`}>{foodRunning ? t("miniGames.sheep.progress", { confirmed: 18, total: 42 }) : previewState === "mini-games-complete" ? t("miniGames.sheep.dailyLimit") : t("common.stopped")}</span>
          <div className="mini-game-actions">{foodRunning ? <button className="danger" type="button" onClick={() => setFoodRunning(false)}>{t("common.stop")}</button> : <button className="primary" type="button" disabled={!previewEnabled || previewState === "mini-games-complete"} onClick={() => setFoodRunning(true)}>{t("common.start")}</button>}</div>
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
        {previewEnabled ? <div className="settings-stack"><ToggleRow label={t("settings.visualMetrics.showFps")} checked={showFps} onChange={setShowFps} /><ToggleRow label={t("settings.visualMetrics.showPing")} checked={showPing} onChange={setShowPing} /></div> : <p>{t("common.processing")}</p>}
      </section>
      {showProfileFocus ? <section className="update-panel"><div className="update-heading"><div><strong>{t("settings.accountInteraction.title")}</strong><span>{t("settings.accountInteraction.description")}</span></div></div><ToggleRow label={t("settings.accountInteraction.focusGameOnProfileSelect")} checked={focusGame} onChange={setFocusGame} /></section> : null}
      <section className="update-panel feedback-panel">
        <div className="update-heading"><div><strong>{t("feedback.title")}</strong><span>{t("feedback.description")}</span></div></div>
        <p className="feedback-privacy">{t("feedback.privacyNotice")}</p>
        {previewState === "settings-feedback" ? <div className="feedback-export-progress" role="progressbar" aria-label={t("feedback.progress.label")} aria-valuenow="45"><span>{t("feedback.progress.preparing")}</span><span>{t("feedback.progress.exporting")}</span><span>{t("feedback.progress.finalizing")}</span></div> : null}
        <div className="update-actions"><button type="button" disabled>{t(previewState === "settings-feedback" ? "feedback.exporting" : "feedback.export")}</button></div>
      </section>
      <section className="update-panel">
        <div className="update-heading">
          <div><strong>{t("update.title")}</strong><span>{t(`update.${updatePhase}`)}</span></div>
          <span className="update-version">{t("update.currentVersion", { version: "0.3.17" })}</span>
        </div>
        {updatePhase === "available" ? <><span>{t("update.latestVersion", { version: "0.3.18" })}</span><div className="update-actions"><button type="button" disabled>{t("update.downloadAndOpen")}</button></div></> : updatePhase === "downloading" ? <div className="update-progress"><progress max="100" value="42" /><span>{t("update.downloading", { progress: 42 })}</span></div> : <div className="update-actions"><button type="button" disabled={!previewEnabled || updatePhase === "checking"}>{t("update.check")}</button></div>}
      </section>
    </section>
  );
}

export function PageForRoute({ routeKey, ...pageProps }) {
  switch (routeKey) {
    case "overview": return <HomePage {...pageProps} />;
    case "automation": return <AutomationPage {...pageProps} />;
    case "map-data": return <MapDataPage {...pageProps} />;
    case "march": return <SquadsPage {...pageProps} />;
    case "city-layout": return <CityLayoutPage {...pageProps} />;
    case "hotkeys": return <HotkeysPage {...pageProps} />;
    case "mini-games": return <MiniGamesPage {...pageProps} />;
    case "settings": return <SettingsPage {...pageProps} />;
    default: return <HomePage {...pageProps} />;
  }
}
