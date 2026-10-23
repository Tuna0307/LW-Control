import { useState } from "react";
import { useI18n } from "./i18n.jsx";

// RECOVERED original AutomationPanel contracts. These are native profile settings;
// no preview fixture or runtime state supplies a setting or an action identity.
export const MISSION_KINDS = ["dispatch", "dispatchAssist"];
const weeklyDefaults = ["none", "ur", "none", "none", "none", "ur", "none"];
const assistQualities = ["n", "r", "sr", "ssr", "ur", "special"];
const switches = { dispatch: ["autoExecute", "collectRewards"], dispatchAssist: ["autoHelp"] };

export function defaultMissionConfig(kind) {
  if (kind === "dispatch") return { enabled: false, autoExecute: false, collectRewards: false, delayMinutes: 3, weeklyQualities: [...weeklyDefaults] };
  if (kind === "dispatchAssist") return { enabled: false, autoHelp: false, qualities: [], delaySeconds: [0, 0], intervalSeconds: 30 };
  return null;
}

export function normalizeMissionDraft(kind, draft) {
  if (!MISSION_KINDS.includes(kind)) return draft;
  const next = { ...draft };
  next.enabled = switches[kind].some(key => next[key] === true);
  if (kind === "dispatch" && String(next.delayMinutes ?? "").trim() !== "") next.delayMinutes = Number(next.delayMinutes);
  if (kind === "dispatchAssist") {
    if (String(next.intervalSeconds ?? "").trim() !== "") next.intervalSeconds = Number(next.intervalSeconds);
    if (Array.isArray(next.delaySeconds)) next.delaySeconds = next.delaySeconds.map(value => String(value ?? "").trim() === "" ? value : Number(value));
  }
  return next;
}

export function toggleMissionMaster(kind, draft, enabled) {
  if (!MISSION_KINDS.includes(kind)) return { ...draft, enabled };
  const next = { ...defaultMissionConfig(kind), ...draft };
  if (enabled) next[switches[kind][0]] = true;
  else for (const key of switches[kind]) next[key] = false;
  return normalizeMissionDraft(kind, next);
}

const integer = (value, min, max) => String(value ?? "").trim() !== "" && Number.isInteger(Number(value)) && Number(value) >= min && Number(value) <= max;
export function validMissionDraft(kind, draft) {
  const defaults = defaultMissionConfig(kind);
  if (!defaults || typeof draft.enabled !== "boolean" || Object.keys(draft).some(key => !(key in defaults)) || switches[kind].some(key => typeof draft[key] !== "boolean")) return false;
  if (draft.enabled !== switches[kind].some(key => draft[key])) return false;
  if (kind === "dispatch") return integer(draft.delayMinutes, 0, 1440) && Array.isArray(draft.weeklyQualities) && draft.weeklyQualities.length === 7 && draft.weeklyQualities.every(value => ["none", "ssr", "ur"].includes(value));
  return Array.isArray(draft.qualities) && draft.qualities.length <= 6 && new Set(draft.qualities).size === draft.qualities.length && draft.qualities.every(value => assistQualities.includes(value)) &&
    (!draft.autoHelp || draft.qualities.length > 0) && Array.isArray(draft.delaySeconds) && draft.delaySeconds.length === 2 && draft.delaySeconds.every(value => integer(value, 0, 86400)) &&
    Number(draft.delaySeconds[0]) <= Number(draft.delaySeconds[1]) && integer(draft.intervalSeconds, 5, 300);
}

export function NativeMissionFields({ kind, config, disabled, runtime }) {
  const { t, language } = useI18n();
  const [error, setError] = useState("");
  if (!MISSION_KINDS.includes(kind)) return null;
  const draft = { ...defaultMissionConfig(kind), ...config.draft };
  const valid = validMissionDraft(kind, draft);
  const commit = patch => {
    setError("");
    config.store.edit(value => normalizeMissionDraft(kind, { ...defaultMissionConfig(kind), ...value, ...patch }), false);
    void config.store.flush().catch(e => setError(e.message === "CONFIG_DRAFT_INVALID" ? t(kind === "dispatchAssist" ? "automation.dispatchAssistConfigError" : "configSave.failed") : e.message));
  };
  const editNumber = (key, value) => config.store.edit(current => ({ ...current, [key]: value }));
  const flush = () => {
    config.store.edit(value => normalizeMissionDraft(kind, value), false);
    void config.store.flush().catch(e => setError(e.message === "CONFIG_DRAFT_INVALID" ? t(kind === "dispatchAssist" ? "automation.dispatchAssistConfigError" : "configSave.failed") : e.message));
  };
  const checkbox = (key, label) => <label className="automation-inline-checkbox"><input type="checkbox" checked={draft[key] === true} disabled={disabled} onChange={event => commit({ [key]: event.target.checked })} />{t(label)}</label>;
  const weekdays = Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat(language, { weekday: "short", timeZone: "UTC" }).format(new Date(Date.UTC(2024, 0, index + 1))));
  const observed = runtime?.after ?? runtime?.before;
  return <div className="automation-mission-fields" data-native-mission-fields={kind}>
    {kind === "dispatch" ? <>
      <section className="automation-settings-section"><h4>{t("squad.afkExecutionSettings")}</h4>
        {checkbox("autoExecute", "automation.autoExecute")}
        {checkbox("collectRewards", "automation.autoCollectRewards")}
        <label>{t("automation.delayAfterResetMinutes")}<input type="number" min="0" max="1440" step="1" value={draft.delayMinutes} disabled={disabled} aria-invalid={!integer(draft.delayMinutes, 0, 1440)} onChange={event => editNumber("delayMinutes", event.target.value)} onBlur={flush} /></label>
      </section>
      <section className="automation-settings-section"><h4>{t("automation.section.quality")}</h4>
        <div className="automation-weekly-quality">{weekdays.map((day, index) => <label key={index}><span>{day}</span><select value={draft.weeklyQualities[index]} disabled={disabled} aria-label={`${t("automation.section.quality")} ${day}`} onChange={event => commit({ weeklyQualities: draft.weeklyQualities.map((value, i) => i === index ? event.target.value : value) })}>
          <option value="none">{t("automation.noQualityRefresh")}</option><option value="ssr">{t("automation.ssrOrAbove")}</option><option value="ur">UR</option>
        </select></label>)}</div>
      </section>
    </> : null}
    {kind === "dispatchAssist" ? <section className="automation-settings-section">
      {checkbox("autoHelp", "automation.dispatchAssist")}
      <div className="automation-squad-choices" role="group" aria-label={t("automation.section.quality")}>{assistQualities.map(quality => <label key={quality}><input type="checkbox" checked={draft.qualities.includes(quality)} disabled={disabled} onChange={event => commit({ qualities: event.target.checked ? [...draft.qualities, quality] : draft.qualities.filter(value => value !== quality) })} />{quality === "special" ? t("automation.assistQuality.special") : quality.toUpperCase()}</label>)}</div>
      <div className="automation-form-grid">{[[0, "automation.minDelaySeconds"], [1, "automation.maxDelaySeconds"]].map(([index, label]) => <label key={index}>{t(label)}<input type="number" min="0" max="86400" step="1" value={draft.delaySeconds[index]} disabled={disabled} aria-invalid={!valid} onChange={event => config.store.edit(value => ({ ...value, delaySeconds: draft.delaySeconds.map((delay, i) => i === index ? event.target.value : delay) }))} onBlur={flush} /></label>)}
        <label>{t("automation.assistIntervalSeconds")}<input type="number" min="5" max="300" step="1" value={draft.intervalSeconds} disabled={disabled} aria-invalid={!integer(draft.intervalSeconds, 5, 300)} onChange={event => editNumber("intervalSeconds", event.target.value)} onBlur={flush} /></label>
      </div>
      {!valid ? <p className="error-text" role="alert">{t("automation.dispatchAssistConfigError")}</p> : null}
    </section> : null}
    {observed?.available != null ? <div className="automation-status-row"><span>{t("automation.available")}</span><strong>{observed.available}</strong></div> : null}
    {error ? <p className="error-text" role="alert">{error}</p> : null}
  </div>;
}
