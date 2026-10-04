import { useI18n } from "./i18n.jsx";

export function Switch({ checked = false, disabled = false, label, onChange }) {
  return (
    <span
      className={`ui-switch${checked ? " is-on" : ""}`}
      aria-hidden="true"
    />
  );
}

export function ToggleRow({ label, checked = false, disabled = false, onChange }) {
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

export function PanelTitle({ title, subtitle }) {
  return (
    <div className="panel-title">
      <h2>{title}</h2>
      {subtitle ? <span className="muted">{subtitle}</span> : null}
    </div>
  );
}
