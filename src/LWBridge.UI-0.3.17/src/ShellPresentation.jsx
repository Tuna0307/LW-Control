import { useSyncExternalStore } from "react";
import { useI18n } from "./i18n.jsx";

// Original si at index-BVfnK1wp.js byte 354710. Live updater actions
// remain fenced until an independently verified provider is connected.
export function TopVersion({ status, onDownload = null }) {
  const { t } = useI18n();
  const visible = status.phase === "available" || status.phase === "downloading"
    || status.phase === "opening" || status.phase === "error"
      && status.latestVersion !== null && status.latestVersion !== status.currentVersion;
  const busy = status.phase === "downloading" || status.phase === "opening";
  return <div className="top-version-row">
    <span>{t("top.version", { version: status.currentVersion || "-" })}</span>
    {visible ? <button className="top-update-button" type="button"
      disabled={busy || !onDownload}
      title={t("top.updateAvailable", { version: status.latestVersion || "-" })}
      aria-label={t("top.updateAvailable", { version: status.latestVersion || "-" })}
      onClick={() => Promise.resolve(onDownload?.()).catch(() => {})}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 15V5M7.5 9.5 12 5l4.5 4.5" />
        <path className="top-update-tray" d="M5 19h14" />
      </svg>
      {status.phase === "downloading" ? <span>{status.progress || 0}%</span> : null}
    </button> : null}
  </div>;
}

// Original Oe byte 199047 and shell binding byte 372793. The store
// is supplied explicitly; absent native stores cannot fabricate save outcomes.
export function ShellConfigSaveError({ state, label, disabled = false }) {
  const { t } = useI18n();
  const snapshot = useSyncExternalStore(state.subscribe, state.getSnapshot, state.getSnapshot);
  if (!snapshot.error) return null;
  return <div className="automation-error" role="alert">
    {label ? <strong>{label} </strong> : null}
    <span>{t("configSave.failed")}</span>{" "}
    <button type="button" disabled={disabled || snapshot.saving}
      onClick={() => state.flush().catch(() => {})}>{t("common.retry")}</button>{" "}
    <button type="button" disabled={disabled || snapshot.saving}
      onClick={() => state.refresh(true).catch(() => {})}>{t("configSave.discard")}</button>
  </div>;
}

const FLAG_ERROR_LABELS = ["automation.weekendShield.title", "automation.attackShield.title",
  "automation.autoReconnect.title", "automation.autoClosePopup.title"];
export function ShellConfigSaveErrors({ states = [] }) {
  const { t } = useI18n();
  return states.map((state, index) => state ? <ShellConfigSaveError key={index}
    state={state} label={t(FLAG_ERROR_LABELS[index])} /> : null);
}
