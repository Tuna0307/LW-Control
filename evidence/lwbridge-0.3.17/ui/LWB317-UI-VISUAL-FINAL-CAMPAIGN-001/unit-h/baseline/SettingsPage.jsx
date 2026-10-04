import { formatDiagnosticBytes, previewUpdateStatus, updateDownloadVisible, updateManualCooldownSeconds, updateStatusBusy } from "./previewRemainingPagesContracts.js";
import { useEffect, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { ToggleRow, PanelTitle } from "./sharedPageUI.jsx";

const SETTINGS_PREVIEW_STATES = new Set([
  "settings-multiprofile",
  "settings-complete",
  "settings-visual-loading",
  "settings-visual-error",
  "settings-visual-saving",
  "settings-visual-save-error",
  "settings-feedback",
  "settings-feedback-preparing",
  "settings-feedback-exporting",
  "settings-feedback-finalizing",
  "settings-feedback-success",
  "settings-feedback-error",
  "settings-feedback-canceled",
  "settings-update-idle",
  "settings-update-checking",
  "settings-update-available",
  "settings-update-downloading",
  "settings-update-opening",
  "settings-update-error",
  "settings-update-check-error",
  "settings-update-download-error",
  "settings-update-cooldown",
]);

function initialFeedbackState(previewState) {
  if (previewState === "settings-feedback-success") {
    return { phase: "success", result: { archiveBytes: 4_404_019, path: "Preview/diagnostics.zip" }, progress: null };
  }
  if (previewState === "settings-feedback-error") return { phase: "error", result: null, progress: null };
  if (previewState === "settings-feedback-canceled") return { phase: "idle", result: null, progress: null };
  const state = previewState === "settings-feedback-preparing"
    ? "preparing"
    : previewState === "settings-feedback-finalizing"
      ? "finalizing"
      : "exporting";
  if (previewState === "settings-feedback" || previewState === "settings-feedback-preparing" || previewState === "settings-feedback-exporting" || previewState === "settings-feedback-finalizing") {
    const percent = state === "preparing" ? 8 : state === "finalizing" ? 92 : 45;
    return { phase: "exporting", result: null, progress: { exportId: "preview-feedback", state, processedBytes: percent, totalBytes: 100, percent } };
  }
  return { phase: "idle", result: null, progress: null };
}

export function SettingsPage({ previewState = "", showProfileFocus: runtimeShowProfileFocus = false, focusGameOnProfileSelect = true, onFocusGameOnProfileSelectChange = null }) {
  const { language, t } = useI18n();
  const previewEnabled = SETTINGS_PREVIEW_STATES.has(previewState);
  const showProfileFocus = previewEnabled
    ? previewState === "settings-multiprofile" || previewState === "settings-complete"
    : runtimeShowProfileFocus;
  const [visualPreferences, setVisualPreferences] = useState(() => previewState === "settings-visual-error" || previewState === "settings-visual-loading" || !previewEnabled ? null : { showFps: false, showPing: false, previewUnrelatedField: "preserve-me" });
  const [visualBusy, setVisualBusy] = useState(previewState === "settings-visual-saving");
  const [visualErrorText, setVisualErrorText] = useState(() => previewState === "settings-visual-error" ? t("settings.visualMetrics.loadFailed") : previewState === "settings-visual-save-error" ? t("settings.visualMetrics.saveFailed") : "");
  const visualSourceRef = useRef(visualPreferences ?? { showFps: false, showPing: false, previewUnrelatedField: "preserve-me" });
  const [focusGamePreview, setFocusGamePreview] = useState(focusGameOnProfileSelect);
  const [feedbackState, setFeedbackState] = useState(() => initialFeedbackState(previewState));
  const feedbackProgress = feedbackState.progress;
  const [updateStatus, setUpdateStatus] = useState(() => previewUpdateStatus(previewEnabled ? previewState : "", Date.now()));
  const [updateNow, setUpdateNow] = useState(() => Date.now());
  const updateCooldown = updateManualCooldownSeconds(updateStatus, updateNow);
  const updateBusy = updateStatusBusy(updateStatus);
  const showUpdateDownload = updateDownloadVisible(updateStatus);
  const updateErrorKey = updateStatus.message ? `update.error.${updateStatus.message}` : "";
  const translatedUpdateError = updateErrorKey && t(updateErrorKey) !== updateErrorKey
    ? t(updateErrorKey)
    : updateStatus.message
      ? t("update.error.default")
      : "";

  useEffect(() => {
    if (updateCooldown <= 0) return undefined;
    const interval = window.setInterval(() => setUpdateNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, [updateCooldown > 0]);

  async function saveVisualPreference(field, value) {
    if (!visualPreferences || visualBusy) return;
    const previousVisual = visualPreferences;
    const optimistic = { ...visualPreferences, [field]: value };
    setVisualPreferences(optimistic);
    setVisualBusy(true);
    setVisualErrorText("");
    try {
      await Promise.resolve();
      if (previewState === "settings-visual-save-error") throw new Error("preview visual metrics save failure");
      const saved = { ...visualSourceRef.current, [field]: value };
      visualSourceRef.current = saved;
      setVisualPreferences(saved);
    } catch {
      setVisualPreferences(previousVisual);
      setVisualErrorText(t("settings.visualMetrics.saveFailed"));
    } finally {
      setVisualBusy(false);
    }
  }

  function changeProfileFocus(value) {
    if (previewEnabled) setFocusGamePreview(value);
    else onFocusGameOnProfileSelectChange?.(value);
  }

  async function exportFeedback() {
    if (!previewEnabled || feedbackState.phase === "exporting") return;
    setFeedbackState({ phase: "exporting", result: null, progress: { exportId: "preview-feedback-click", state: "preparing", processedBytes: 0, totalBytes: 0, percent: 0 } });
    await Promise.resolve();
    if (previewState === "settings-feedback-canceled") {
      setFeedbackState({ phase: "idle", result: null, progress: null });
      return;
    }
    if (previewState === "settings-feedback-error") {
      setFeedbackState({ phase: "error", result: null, progress: null });
      return;
    }
    setFeedbackState({ phase: "success", result: { archiveBytes: 4_404_019, path: "Preview/diagnostics.zip" }, progress: null });
  }

  async function checkForUpdates() {
    if (!previewEnabled || updateBusy || updateCooldown > 0) return;
    setUpdateStatus((current) => ({ ...current, phase: "checking", message: null }));
    await Promise.resolve();
    if (previewState === "settings-update-check-error") {
      setUpdateStatus((current) => ({ ...current, phase: "error", message: "UPDATE_CHECK_FAILED" }));
      return;
    }
    setUpdateStatus(previewState === "settings-update-available" ? previewUpdateStatus("settings-update-available", Date.now()) : previewUpdateStatus("settings-complete", Date.now()));
  }

  async function downloadAndOpenUpdate() {
    if (!previewEnabled || !showUpdateDownload) return;
    setUpdateStatus((current) => ({ ...current, phase: "downloading", progress: 0, message: null }));
    await Promise.resolve();
    if (previewState === "settings-update-download-error") {
      setUpdateStatus((current) => ({ ...current, phase: "error", message: "UPDATE_DOWNLOAD_FAILED" }));
      return;
    }
    setUpdateStatus((current) => ({ ...current, phase: "opening", progress: 100 }));
  }

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
        {visualPreferences === null && !visualErrorText ? <p>{t("common.processing")}</p> : null}
        {visualPreferences ? <div className="settings-stack"><ToggleRow label={t("settings.visualMetrics.showFps")} checked={visualPreferences.showFps} disabled={visualBusy} onChange={(value) => void saveVisualPreference("showFps", value)} /><ToggleRow label={t("settings.visualMetrics.showPing")} checked={visualPreferences.showPing} disabled={visualBusy} onChange={(value) => void saveVisualPreference("showPing", value)} /></div> : null}
        {visualErrorText ? <p className="update-error" role="alert">{visualErrorText}</p> : null}
      </section>
      {showProfileFocus ? <section className="update-panel"><div className="update-heading"><div><strong>{t("settings.accountInteraction.title")}</strong><span>{t("settings.accountInteraction.description")}</span></div></div><div className="settings-stack"><ToggleRow label={t("settings.accountInteraction.focusGameOnProfileSelect")} checked={previewEnabled ? focusGamePreview : focusGameOnProfileSelect} onChange={changeProfileFocus} /></div></section> : null}
      <section className="update-panel feedback-panel">
        <div className="update-heading"><div><strong>{t("feedback.title")}</strong><span>{t("feedback.description")}</span></div></div>
        <p className="feedback-privacy">{t("feedback.privacyNotice")}</p>
        {feedbackState.phase === "success" && feedbackState.result ? <p className="update-success feedback-result">{t("feedback.success", { size: formatDiagnosticBytes(feedbackState.result.archiveBytes), path: feedbackState.result.path })}</p> : null}
        {feedbackState.phase === "error" ? <p className="update-error" role="alert">{t("feedback.failed")}</p> : null}
        <div className="update-actions">
          {feedbackState.phase === "exporting" && feedbackProgress ? <div className="feedback-export-progress"><span>{t(`feedback.progress.${feedbackProgress.state}`)}</span><progress aria-label={t("feedback.progress.label")} max="100" value={feedbackProgress.percent} /><strong>{feedbackProgress.percent}%</strong></div> : null}
          <button type="button" disabled={!previewEnabled || feedbackState.phase === "exporting"} onClick={() => void exportFeedback()}>{t(feedbackState.phase === "exporting" ? "feedback.exporting" : "feedback.export")}</button>
        </div>
      </section>
      <section className="update-panel">
        <div className="update-heading">
          <div><strong>{t("update.title")}</strong><span>{t("update.currentVersion", { version: updateStatus.currentVersion || "-" })}</span></div>
          {updateStatus.latestVersion ? <span className="update-version">{t("update.latestVersion", { version: updateStatus.latestVersion })}</span> : null}
        </div>
        {updateStatus.phase === "idle" ? <p>{t("update.idle")}</p> : null}
        {updateStatus.phase === "checking" ? <p>{t("update.checking")}</p> : null}
        {updateStatus.phase === "upToDate" ? <p className="update-success">{t("update.upToDate")}</p> : null}
        {updateStatus.phase === "available" ? <p className="update-available">{t("update.available")}</p> : null}
        {updateStatus.phase === "opening" ? <p className="update-success">{t("update.opening")}</p> : null}
        {translatedUpdateError ? <p className="update-error" role="alert">{translatedUpdateError}</p> : null}
        {updateStatus.publishedAt ? <span className="update-date">{t("update.publishedAt", { date: new Date(updateStatus.publishedAt).toLocaleString(language) })}</span> : null}
        {updateStatus.releaseNotes ? <div className="update-notes">{updateStatus.releaseNotes}</div> : null}
        {updateStatus.downloadDirectory ? <p className="update-directory">{t("update.downloadDirectory", { path: updateStatus.downloadDirectory })}</p> : null}
        {updateStatus.phase === "downloading" ? <div className="update-progress"><progress max="100" value={updateStatus.progress || 0} /><span>{t("update.downloading", { progress: updateStatus.progress || 0 })}</span></div> : null}
        <div className="update-actions">
          <button type="button" disabled={!previewEnabled || updateBusy || updateCooldown > 0} onClick={() => void checkForUpdates()}>{updateCooldown > 0 ? t("update.checkCooldown", { seconds: updateCooldown }) : t("update.check")}</button>
          {showUpdateDownload ? <button className="primary" type="button" onClick={() => void downloadAndOpenUpdate()}>{t("update.downloadAndOpen")}</button> : null}
        </div>
      </section>
    </section>
  );
}
