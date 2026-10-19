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
      if (typeof next?.reason === "string" && typeof next?.status === "string") setOutcome(next.status === "Confirmed" ? null : next);
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
  if (!outcome || outcome.status === "Confirmed") return null;
  const keys = outcome.kind === "AttackSquad" ? "QWER" : outcome.kind === "RecallSquad" ? "ASDF" : null;
  const key = keys?.[outcome.slot - 1] || outcome.kind;
  const reason = outcome.reason.replace(/^current_client_game_action_rejected \[[^\]]+\]: /, "");
  return <div className="hotkey-feedback automation-error" role="status" aria-live="polite"
    data-hotkey-status={outcome.status} style={{ position: "fixed", top: 66, left: 24,
      right: 24, zIndex: 1000, padding: "10px 14px", background: "var(--panel-bg, #fff)",
      border: "1px solid currentColor", borderRadius: 8 }}>
    <button type="button" aria-label={t("common.close")} title={t("common.close")}
      onClick={() => setOutcome(null)} style={{ float: "right", marginLeft: 12 }}>×</button>
    <strong>{key}</strong>: {reason}
  </div>;
}
