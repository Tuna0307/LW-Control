import { useI18n } from "./i18n.jsx";

// AutomationPanel E: next-run pills suppress absent, invalid and elapsed times.
export function previewFutureTime(value, language, now = Date.now()) {
  if (!value || !Number.isFinite(value) || value <= now) return null;
  const date = new Date(value), today = new Date(now);
  const days = Math.round((Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) - Date.UTC(today.getFullYear(), today.getMonth(), today.getDate())) / 86400000);
  const display = days === 0 || days === 1
    ? `${new Intl.RelativeTimeFormat(language, { numeric: "auto" }).format(days, "day")} ${date.toLocaleTimeString(language, { hour: "2-digit", minute: "2-digit" })}`
    : date.toLocaleString(language, { year: date.getFullYear() === today.getFullYear() ? undefined : "numeric", month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" });
  return <time dateTime={date.toISOString()} title={date.toLocaleString(language)}>{display}</time>;
}

// AutomationCard-LCx_jIi7 c: only the first next-run/claim pill is shown.
// Other statusRows are source inputs, not an additional status grid.
export function AutomationMeta({ online, active, state, actionBusy = false, presentation }) {
  const { t } = useI18n();
  const showDetails = online && (active !== false || actionBusy);
  const stateKey = !online ? "status.disconnected" : actionBusy ? "automation.running" : active === false ? "common.disabled" : state;
  const pills = showDetails ? presentation.status?.filter(([key, value]) => ["automation.nextRun", "automation.nextClaim"].includes(key) && value != null && value !== "" && value !== "-").slice(0, 1) : [];
  const summary = showDetails ? presentation.summary?.filter(([key, value]) => value != null && value !== "" && value !== "-" && !(key === "automation.latestResult" && value === stateKey)) : [];
  const stateClass = stateKey === "common.success" ? "success" : stateKey === "common.failed" ? "failed" : stateKey === "automation.running" ? "running" : stateKey === "common.disabled" || !online ? "disabled" : "waiting";
  return <>
    <div className="automation-card-meta-row" role="status">
      <span className={`automation-state state-${stateClass}`}>{t(stateKey)}</span>
      {pills?.length > 0 ? <div className="automation-status-pills">{pills.map(([key, value]) => <span className="automation-status-pill" key={key}><span className="pill-dot" aria-hidden="true" /><span className="pill-label">{t(key)}:</span><strong>{typeof value === "string" ? t(value) : value}</strong></span>)}</div> : null}
    </div>
    {summary?.length > 0 ? <div className="automation-card-summary">{summary.map(([key, value]) => <span key={key}>{t(key)} <strong>{typeof value === "string" ? t(value) : value}</strong></span>)}</div> : null}
  </>;
}
