import { useEffect, useId, useState } from "react";
import { backendBridge } from "./backendBridge.js";
import { useI18n } from "./i18n.jsx";
import { Switch } from "./sharedPageUI.jsx";
import { PreviewConfigError, usePreviewConfigAdapter } from "./previewConfigHook.jsx";
import { createAutomationAdapter, validAutomationDraft } from "./automationRuntime.js";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";
import { subscribeAutomationStatus } from "./automationStatusHub.js";

// R30 native Resource gathering. RECOVERED original settings; the engine only sends
// configured idle squads to empty public resource tiles (see feature018_gather_native.lua).
const KIND = "resourceGather";
const RADII = [50, 100, 150, 200, 250, 300, 400, 500];
const RESOURCES = [["metal", "squad.afkResourceMetal"], ["food", "squad.afkResourceFood"], ["gold", "squad.afkResourceGold"]];
const STEP_LABELS = { idle: "idle", searching: "searching", busy: "squad_busy", paused: "manual_wait", nothing_available: "no_resource", unavailable: "task_paused" };

export function defaultGatherSquads(squads) {
  const byIndex = new Map((Array.isArray(squads) ? squads : []).map(squad => [Number(squad.squadIndex), squad]));
  return [1, 2, 3, 4].map(index => ({ squadIndex: index, enabled: false, resource: "metal", level: 10, ...(byIndex.get(index) ?? {}) }));
}

export function NativeGatherCard({ profileId, online, bridge = backendBridge }) {
  const { t, language } = useI18n();
  const settingsId = useId();
  const config = usePreviewConfigAdapter({ enabled: false }, createAutomationAdapter(bridge, profileId, KIND, online, draft => validAutomationDraft(KIND, draft)), `automation-native:${profileId}:${KIND}`);
  const [ready, setReady] = useState(false), [error, setError] = useState(""), [runtime, setRuntime] = useState(null), [busy, setBusy] = useState(false), [expanded, setExpanded] = useState(false);
  useEffect(() => {
    let closed = false;
    setReady(false);setRuntime(null);
    void config.store.refresh().then(() => { if (!closed) setReady(true); }).catch(e => { if (!closed) setError(e.message); });
    const unsubscribe = online ? subscribeAutomationStatus(bridge, profileId, services => { if (!closed) setRuntime(services?.[KIND] ?? null); }, e => { if (!closed) setError(e.message); }) : () => {};
    return () => { closed = true;unsubscribe(); };
  }, [config.store, online, profileId]);
  const draft = config.draft;
  const squads = defaultGatherSquads(draft.squads);
  const disabled = !ready || !online || busy || config.saving;
  const save = async patch => {
    setBusy(true);setError("");
    try {
      if (patch.enabled === false) { assertBridgeProfileOwner(bridge, profileId);await bridge.invokeProfileScoped("feature018_automation_stop", { kind: KIND }); }
      config.store.edit(current => ({ ...current, squads: defaultGatherSquads(current.squads), ...patch }), false);
      await config.store.flush();
    } catch (e) { setError(e.message === "CONFIG_DRAFT_INVALID" ? t("automation.resourceGather.squadRequired") : e.message); }
    finally { setBusy(false); }
  };
  const updateSquad = (index, patch) => {
    const next = squads.map(squad => squad.squadIndex === index ? { ...squad, ...patch } : squad);
    if (draft.enabled && !next.some(squad => squad.enabled)) { setError(t("automation.resourceGather.squadRequired"));return; }
    void save({ squads: next });
  };
  const toggle = () => {
    if (!draft.enabled && !squads.some(squad => squad.enabled)) { setError(t("automation.resourceGather.squadRequired"));setExpanded(true);return; }
    void save({ enabled: !draft.enabled });
  };
  const rows = runtime?.before?.gatherSquads ?? [];
  const state = !online ? t("status.disconnected") : !draft.enabled ? t("common.disabled") : runtime?.state === "unconfirmed" ? t("automation.resultUnconfirmed") : t("common.waiting");
  return <article className={`automation-card${draft.enabled ? " is-enabled" : ""}`} data-native-automation={KIND} data-automation-state={runtime?.state ?? "unobserved"} data-automation-confirmed={runtime?.confirmed ?? 0}>
    <div className="automation-card-header">
      <div className="automation-card-title-group"><h3>{t("automation.category.resourceGather")}</h3><p>{t("automation.resourceGather.description")}</p></div>
      <button type="button" role="switch" className="automation-header-switch" aria-label={t("automation.category.resourceGather")} aria-checked={draft.enabled === true} disabled={disabled} onClick={toggle}><Switch checked={draft.enabled === true} /></button>
    </div>
    <p role="status">{state}</p>
    {runtime?.confirmed > 0 ? <p>{t("automation.confirmedActions", { count: runtime.confirmed })}</p> : null}
    {runtime?.error ? <p role="alert" className="error-text">{runtime.error}</p> : null}
    {error ? <p role="alert" className="error-text">{error}</p> : null}
    <PreviewConfigError config={config} t={t} />
    <div className="automation-config-actions"><button type="button" className={`automation-config-trigger${expanded ? " is-open" : ""}`} aria-expanded={expanded} aria-controls={settingsId} onClick={() => setExpanded(value => !value)}>
      <span className="automation-config-trigger-label"><span>{t("settings.title")}</span></span>
      <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="M4 6l4 4 4-4" /></svg>
    </button></div>
    <fieldset id={settingsId} className="automation-config-body" hidden={!expanded} disabled={disabled}>
      <div className="automation-form-grid">
        <label><span>{t("automation.resourceGather.scanRadius")}</span><select value={draft.scanRadius ?? 200} onChange={event => { void save({ scanRadius: Number(event.target.value) }); }}>{RADII.map(radius => <option key={radius} value={radius}>{radius}</option>)}</select></label>
        <label><span>{t("automation.resourceGather.manualResumeDelay")}</span><select value={Math.round((draft.manualResumeDelaySeconds ?? 120) / 60)} onChange={event => { void save({ manualResumeDelaySeconds: Number(event.target.value) * 60 }); }}>{Array.from({ length: 30 }, (_, i) => i + 1).map(minutes => <option key={minutes} value={minutes}>{minutes}</option>)}</select></label>
        <label className="automation-inline-checkbox"><input type="checkbox" checked={draft.recallOnDisable === true} onChange={event => { void save({ recallOnDisable: event.target.checked }); }} />{t("automation.resourceGather.recallOnDisable")}</label>
      </div>
      <p className="muted">{t("automation.resourceGather.scanRadiusHint", { size: 1000 })}</p>
      {squads.map(squad => {
        const row = rows.find(entry => entry.squadIndex === squad.squadIndex);
        const step = !draft.enabled || !squad.enabled ? "disabled" : STEP_LABELS[row?.step] ?? "idle";
        return <div className="automation-resource-gather-squad" role="group" aria-label={t("automation.squad", { index: squad.squadIndex })} key={squad.squadIndex}>
          <label className="automation-resource-gather-enable"><input type="checkbox" checked={squad.enabled} onChange={event => updateSquad(squad.squadIndex, { enabled: event.target.checked })} /><span>{t("automation.squad", { index: squad.squadIndex })}</span></label>
          <select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.resource")}`} value={squad.resource} onChange={event => updateSquad(squad.squadIndex, { resource: event.target.value })}>{RESOURCES.map(([value, label]) => <option key={value} value={value}>{t(label)}</option>)}</select>
          <select aria-label={`${t("automation.squad", { index: squad.squadIndex })} ${t("automation.resourceGather.level")}`} value={squad.level} onChange={event => updateSquad(squad.squadIndex, { level: Number(event.target.value) })}>{Array.from({ length: 10 }, (_, i) => i + 1).map(level => <option key={level} value={level}>{level}</option>)}</select>
          <span className="automation-resource-gather-state muted">{t(`automation.resourceGather.state.${step}`)}{row?.manualResumeAt ? <small>{t("automation.resourceGather.manualResumeAt", { time: new Date(row.manualResumeAt).toLocaleTimeString(language) })}</small> : null}{row?.reason ? <small>{row.reason}</small> : null}</span>
        </div>;
      })}
    </fieldset>
  </article>;
}
