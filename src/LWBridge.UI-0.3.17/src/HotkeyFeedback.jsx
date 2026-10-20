import { useEffect, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { backendBridge } from "./backendBridge.js";

export function HotkeyFeedback({ profileId, activeRoute }) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(null);
  useEffect(() => {
    setOutcome(null);
    if (!backendBridge.available || !profileId) return undefined;
    const owner = backendBridge.currentProfileOwner();
    return backendBridge.listen("bridge://hotkey-outcome", (event) => {
      if (!backendBridge.isCurrentProfileOwner(owner) || event?.profileId !== profileId) return;
      const next = event.payload;
      const relocation = next?.kind === "RandomRelocate" || next?.kind === "AllianceRelocate";
      if (typeof next?.reason === "string" && typeof next?.status === "string") {
        setOutcome(next.status === "Confirmed" && !relocation ? null : next);
      }
    });
  }, [profileId, activeRoute]);
  useEffect(() => {
    if (!outcome) return undefined;
    const timer = window.setTimeout(() => setOutcome(null), 6000);
    return () => window.clearTimeout(timer);
  }, [outcome]);
  useEffect(() => {
    const clearOnTabChange = (event) => {
      const tab = event.target?.closest?.('[role="tab"]');
      if (tab && tab.getAttribute("aria-selected") !== "true") setOutcome(null);
    };
    document.addEventListener("click", clearOnTabChange, true);
    return () => document.removeEventListener("click", clearOnTabChange, true);
  }, []);
  if (!outcome) return null;
  const keys = outcome.kind === "AttackSquad" ? "QWER" : outcome.kind === "RecallSquad" ? "ASDF" : null;
  const relocation = outcome.kind === "RandomRelocate" || outcome.kind === "AllianceRelocate";
  const displayKey = outcome.kind === "RandomRelocate" ? "F9" :
    outcome.kind === "AllianceRelocate" ? "F10" :
    keys?.[outcome.slot - 1] ||
    (outcome.kind === "UseShield" ? (outcome.slot === 8 ? "F6" : outcome.slot === 12 ? "F7" : outcome.slot === 24 ? "F8" : t("hotkeys.feedback.hotkey")) :
    outcome.kind === "ApplyEquipmentPreset" && outcome.slot >= 1 && outcome.slot <= 4 ? `Alt+${outcome.slot}` :
    outcome.kind === "ReinforceFrontline" ? "G" :
    t("hotkeys.feedback.hotkey"));
  const commonReason = outcome.status === "NotFocused" ? t("hotkeys.feedback.notFocused") :
    outcome.status === "SessionChanged" ? t("hotkeys.feedback.sessionChanged") :
    outcome.status === "Unsupported" ? t("hotkeys.feedback.unsupported") :
    outcome.status === "Busy" ? t("hotkeys.feedback.busy") :
    outcome.status === "Submitted" ? t("hotkeys.feedback.submitted") :
    t("hotkeys.feedback.actionFailed");
  const reason = relocation && outcome.status === "Confirmed" ? t("hotkeys.feedback.relocationConfirmed") :
    relocation && outcome.status === "Submitted" ? t("hotkeys.feedback.relocationSubmitted") :
    relocation && outcome.status === "Unsupported" ? t("hotkeys.feedback.relocationUnsupported") :
    relocation && !["NotFocused", "SessionChanged", "Busy"].includes(outcome.status) ? t("hotkeys.feedback.relocationFailed") :
    commonReason;
  return <div className={`hotkey-feedback ${outcome.status === "Confirmed" ? "automation-success" : "automation-error"}`} role="status" aria-live="polite"
    data-hotkey-status={outcome.status} style={{ position: "fixed", top: 66, left: 24,
      right: 24, zIndex: 1000, padding: "10px 14px", background: "var(--panel-bg, #fff)",
      border: "1px solid currentColor", borderRadius: 8 }}>
    <button type="button" aria-label={t("common.close")} title={t("common.close")}
      onClick={() => setOutcome(null)} style={{ float: "right", marginLeft: 12 }}>×</button>
    <strong>{displayKey}</strong>: {reason}
  </div>;
}
