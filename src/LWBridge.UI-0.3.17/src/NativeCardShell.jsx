import { useState } from "react";
import { Switch } from "./sharedPageUI.jsx";

// Shared frame for the live (native) Automation cards, using the RECOVERED 0.3.17 card
// structure: header switch, one status pill, compact details, and a Settings section.

export function NativeCardHeader({ title, description, checked, disabled, onToggle, label }) {
  return <div className="automation-card-header">
    <div className="automation-card-title-group"><h3>{title}</h3>{description ? <p>{description}</p> : null}</div>
    <div className="automation-card-header-actions">
      <button type="button" role="switch" className="automation-header-switch" aria-label={label ?? title} aria-checked={checked === true} disabled={disabled} onClick={onToggle}><Switch checked={checked === true} /></button>
    </div>
  </div>;
}

const STATE_KEYS = { disabled: "common.disabled", waiting: "common.waiting", success: "common.success",
  nothing_available: "automation.state.nothingToDo", unconfirmed: "automation.state.unconfirmed", error: "common.failed" };

// One status pill, then label/value rows, a muted note and any error.
export function NativeCardStatus({ t, online, enabled, state, rows = [], note, errors = [] }) {
  const key = !online ? "status.disconnected" : !enabled && state !== "success" ? "common.disabled" : STATE_KEYS[state] ?? "common.waiting";
  const tone = key === "common.success" ? "success" : key === "automation.state.unconfirmed" || key === "common.failed" ? "failed" : key === "status.disconnected" || key === "common.disabled" ? "disabled" : "waiting";
  const visible = rows.filter(([, value]) => value != null && value !== "");
  return <>
    <div className="automation-card-meta-row" role="status"><span className={`automation-state state-${tone}`}>{t(key)}</span></div>
    {visible.length ? <div className="automation-status-grid">{visible.map(([label, value]) => <div className="automation-status-row" key={label}><span>{label}</span><strong>{value}</strong></div>)}</div> : null}
    {note ? <p className="automation-card-note">{note}</p> : null}
    {errors.filter(Boolean).map((error, index) => <div role="alert" className="automation-error" key={index}>{error}</div>)}
  </>;
}

export function useRememberedExpansion(storageKey) {
  const [expanded, setExpanded] = useState(() => { try { return localStorage.getItem(storageKey) === "true"; } catch { return false; } });
  const toggle = () => { const next = !expanded;setExpanded(next);try { localStorage.setItem(storageKey, String(next)); } catch { /* Storage may be unavailable. */ } };
  return [expanded, toggle, setExpanded];
}

export function SettingsTrigger({ t, expanded, onToggle }) {
  return <button type="button" className={`automation-config-trigger${expanded ? " is-open" : ""}`} aria-expanded={expanded} onClick={onToggle}>
    <span className="automation-config-trigger-label">
      <svg className="automation-config-gear" viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><circle cx="8" cy="8" r="2.5" /><path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" /></svg>
      <span>{t("settings.title")}</span>
    </span>
    <svg className="trigger-chevron" viewBox="0 0 16 16" width="11" height="11" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false"><path d="M4 6l4 4 4-4" /></svg>
  </button>;
}

// Settings under a divider. Collapsible cards show the Settings trigger; small cards show the
// fields directly, like the original single-setting cards.
export function NativeCardSettings({ t, collapsible = false, expanded = true, onToggle, disabled, onBlur, children }) {
  return <div className="automation-config">
    {collapsible ? <div className="automation-config-actions"><SettingsTrigger t={t} expanded={expanded} onToggle={onToggle} /></div> : null}
    <fieldset className="automation-config-body" disabled={disabled} onBlur={onBlur} style={collapsible && !expanded ? { display: "none" } : undefined}>{children}</fieldset>
  </div>;
}
