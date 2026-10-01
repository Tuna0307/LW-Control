import { useMemo, useState } from "react";
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
    ["Automatically assist alliance Secret Tasks", "Automatically assist alliance Secret Tasks"],
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

function AutomationConfigPreview({ title, enabled }) {
  const { t } = useI18n();
  if (title === "Auto Training") {
    return <div className="automation-form-grid"><label><span>{t("automation.soldierTraining.totalCount")}</span><input type="number" min="1" max="1000000" defaultValue="1000" disabled={!enabled} /></label><label><span>{t("automation.soldierTraining.target")}</span><select defaultValue="highest" disabled={!enabled}><option value="highest">{t("automation.soldierTraining.highest")}</option></select></label></div>;
  }
  if (title === "Automatic Construction") {
    return <><div className="automation-form-grid"><label><span>{t("automation.maxBuilders")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label><label><span>{t("automation.construction.targetLevel")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label></div><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.construction.targetEnabled")}</span></label><fieldset disabled={!enabled}><legend>{t("automation.construction.buildingTypes")}</legend><div className="automation-compact-choice-group"><label><input type="checkbox" />HQ</label><label><input type="checkbox" />Barracks</label><label><input type="checkbox" />Hospital</label></div></fieldset></>;
  }
  if (title === "Automatic Treatment") {
    return <div className="automation-form-grid"><label><span>{t("automation.treatmentAmountPerArmy")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" defaultValue="1" disabled={!enabled} /></label></div>;
  }
  if (title === "Automatic Official Application") {
    return <label><span>{t("automation.targetPosition")}</span><select defaultValue="" disabled={!enabled}><option value="">{t("automation.position.none")}</option><option value="vicePresident">{t("automation.position.vicePresident")}</option><option value="strategyMinister">{t("automation.position.strategyMinister")}</option><option value="defenseMinister">{t("automation.position.defenseMinister")}</option><option value="constructionMinister">{t("automation.position.constructionMinister")}</option><option value="scienceMinister">{t("automation.position.scienceMinister")}</option><option value="internalAffairsMinister">{t("automation.position.internalAffairsMinister")}</option></select></label>;
  }
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) {
    return <><div className="automation-form-grid"><label><span>{t("automation.minDelaySeconds")}</span><input type="number" min="0" defaultValue="2" disabled={!enabled} /></label><label><span>{t("automation.maxDelaySeconds")}</span><input type="number" min="0" defaultValue="5" disabled={!enabled} /></label></div><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.autoReply")}</span></label><label><span>{t("automation.replyPhrases")}</span><textarea rows="3" defaultValue="Thanks!" disabled={!enabled} /></label></>;
  }
  if (["Trucks", "Secret Task", "Automatically assist alliance Secret Tasks", "Ghost Ops", "Automatic Alliance Train Boarding"].includes(title)) {
    return <><div className="automation-form-grid"><label><span>{t("automation.targetQuality")}</span><select defaultValue="ur" disabled={!enabled}><option value="n">N</option><option value="r">R</option><option value="sr">SR</option><option value="ssr">SSR</option><option value="ur">UR</option></select></label><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" defaultValue="3" disabled={!enabled} /></label></div><label className="automation-checkbox-row"><input type="checkbox" disabled={!enabled} /><span>{t("automation.autoCollectRewards")}</span></label></>;
  }
  if (["Alliance Gifts", "Excavation Stronghold Resources", "Alliance Center Resources", "Building Resource Collection", "Armed Truck"].includes(title)) {
    return <div className="automation-form-grid"><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" max="1440" defaultValue="60" disabled={!enabled} /></label><label><span>{t("automation.latestResult")}</span><output>-</output></label></div>;
  }
  if (title === "Alliance Gathering Dispatch") {
    return <><fieldset disabled={!enabled}><legend>{t("automation.section.departure")}</legend><div className="automation-squad-choices">{[1,2,3,4].map((index) => <label key={index}><input type="checkbox" /><span>{t("automation.squad", { index })}</span></label>)}</div></fieldset><div className="automation-form-grid"><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" defaultValue="60" disabled={!enabled} /></label></div></>;
  }
  return <div className="automation-form-grid"><label><span>{t("automation.intervalMinutes")}</span><input type="number" min="1" defaultValue="60" disabled={!enabled} /></label></div>;
}

function AutomationCard({ title, description, previewEnabled }) {
  const { english, t } = useI18n();
  const [enabled, setEnabled] = useState(false);
  const [expanded, setExpanded] = useState(previewEnabled && title === "Automatic Construction");
  return (
    <article className="automation-card" data-preview-fixture={previewEnabled ? "automation-config" : "runtime-config-unobserved"}>
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{english(title)}</h3>
          {description ? <p>{english(description)}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" role="switch" aria-checked={enabled} disabled={!previewEnabled} aria-label={`${english(title)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => setEnabled((value) => !value)}><Switch checked={enabled} /></button>
        </div>
      </div>
      <div className="automation-card-meta-row" role="status">
        <span className={`automation-state ${enabled ? "state-enabled" : "state-disabled"}`}>{previewEnabled ? t(enabled ? "common.enabled" : "common.disabled") : t("status.disconnected")}</span>
      </div>
      <div className="automation-config">
        <div className="automation-config-actions">
          <button className={`automation-config-trigger${expanded ? " is-open" : ""}`} type="button" disabled={!previewEnabled} aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>
            <span className="automation-config-trigger-label">
              <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
              <span>{t("settings.title")}</span>
            </span>
            <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M4 6l4 4 4-4" /></svg>
          </button>
        </div>
        {expanded ? <fieldset className="automation-config-body" disabled={!previewEnabled}><AutomationConfigPreview title={title} enabled={previewEnabled} /><div className="automation-config-actions"><button type="button" className="primary" disabled={!previewEnabled}>{t("common.saveConfig")}</button></div></fieldset> : null}
      </div>
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
  const previewEnabled = previewState === "squads-profile" || previewState === "squads-equipment";
  const [tab, setTab] = useState(previewState === "squads-equipment" ? "equipment" : "afk");
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
      {tab === "afk" ? <AfkContent previewEnabled={previewEnabled} /> : <EquipmentContent previewEnabled={previewEnabled} />}
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

function EquipmentContent({ previewEnabled }) {
  const { t } = useI18n();
  const [selectedSquad, setSelectedSquad] = useState(1);
  return (
    <div className="equipment-preset-layout" data-preview-fixture={previewEnabled ? "squads-equipment" : "no-equipment-presets"}>
      <aside className="equipment-preset-rail">
        <strong>{t("squad.equipmentPresets")}</strong>
        <div className="equipment-preset-list">{previewEnabled ? <button type="button" className="active">{t("squad.presetDefaultName", { number: 1 })}</button> : <span className="muted">{t("squad.noEquipmentPresets")}</span>}</div>
        <button type="button" disabled={!previewEnabled}>{t("squad.createPreset")}</button>
      </aside>
      <main className="equipment-preset-main">
        <div className="equipment-preset-toolbar">
          <div><strong>{previewEnabled ? t("squad.presetDefaultName", { number: 1 }) : t("squad.noEquipmentPresets")}</strong></div>
          <div className="equipment-preset-actions">
            <button type="button" disabled={!previewEnabled}>{t("common.rename")}</button>
            <button type="button" disabled={!previewEnabled}>{t("squad.loadCurrentEquipment")}</button>
            <button type="button" disabled={!previewEnabled}>{t("squad.saveEquipmentConfig")}</button>
            <button type="button" className="primary" disabled>{t("squad.saveAndApplyEquipmentConfig")}</button>
          </div>
        </div>
        {previewEnabled ? <div className="equipment-preset-squads">{[1,2,3,4].map((squad) => <button type="button" key={squad} className={`equipment-preset-squad ${selectedSquad === squad ? "active" : ""}`} onClick={() => setSelectedSquad(squad)}><span className="equipment-preset-squad-header">{t("squad.number", { number: squad })}</span><span>{t("squad.presetSummary", { positions: 5, equips: 0 })}</span></button>)}</div> : <div className="map-empty">{t("squad.createFirstPreset")}</div>}
        {previewEnabled ? <div className="equipment-preset-positions">{[1,2,3,4,5].map((position) => <article className="equipment-position-card" key={position}><div className="equipment-position-header"><strong>{t("squad.position", { number: position })}</strong><span>{t("squad.heroFixed")}</span></div><div className="equipment-position-items">{["squad.equipmentSlot1","squad.equipmentSlot2","squad.equipmentSlot3","squad.equipmentSlot4"].map((key) => <span className="preset-equipment-slot" key={key}>{t(key)} · {t("squad.emptyEquipment")}</span>)}</div></article>)}</div> : null}
        <div className="equipment-preset-hint">
          <span>{t("squad.dragEquipmentHint")}</span>
          <strong>{t("squad.quickShortcutHint")}</strong>
        </div>
      </main>
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
