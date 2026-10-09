import { useEffect, useRef, useState } from "react";
import warningIcon from "./assets/icon-warning.png";
import { useI18n } from "./i18n.jsx";

// Exact original In dialog contract, index-BVfnK1wp.js byte 209017.
function ExitModal({ busy, onClose, children }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog.showModal();
    return () => {
      dialog.close();
      if (previous?.isConnected) previous.focus({ preventScroll: true });
    };
  }, []);
  return <dialog ref={ref} className="app-dialog app-exit-backdrop" role="dialog" aria-modal="true" aria-labelledby="app-exit-title" aria-describedby="app-exit-description" aria-busy={busy}
    onCancel={(event) => { event.preventDefault(); if (!busy) onClose?.(); }}
    onKeyDown={(event) => {
      event.stopPropagation();
      if (event.key !== "Tab") return;
      const controls = [...event.currentTarget.querySelectorAll("button, [href], input, select, textarea, [tabindex]")]
        .filter((element) => element.tabIndex >= 0 && !element.matches(":disabled") && element.getClientRects().length > 0);
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (!first) event.preventDefault();
      else if (event.shiftKey && (document.activeElement === first || !controls.includes(document.activeElement))) {
        event.preventDefault(); last.focus();
      } else if (!event.shiftKey && (document.activeElement === last || !controls.includes(document.activeElement))) {
        event.preventDefault(); first.focus();
      }
    }}>{children}</dialog>;
}

// Ji at byte 376111. Count is supplied instance count, not a timer/countdown.
export function AppExitDialog({ instanceCount = null, busy = false, onCancel = null, onConfirm = null }) {
  const { t } = useI18n();
  if (instanceCount === null) return null;
  return <ExitModal busy={busy} onClose={onCancel}><section className="app-exit-dialog">
    <img src={warningIcon} alt="" width="40" height="40" aria-hidden="true" />
    <div className="app-exit-content"><h2 id="app-exit-title">{t("appExit.title")}</h2><p id="app-exit-description">{t("appExit.description", { count: instanceCount })}</p>
      <div className="app-exit-actions">
        <button type="button" disabled={busy || !onCancel} autoFocus onClick={() => onCancel?.()}>{t("common.cancel")}</button>
        <button type="button" className="app-exit-confirm" disabled={busy || !onConfirm} onClick={() => onConfirm?.()}>{busy ? t("appExit.closing") : t("appExit.confirm", { count: instanceCount })}</button>
      </div>
    </div>
  </section></ExitModal>;
}

// qi at byte 375835. A supplied event/provider may be bound separately; absent
// providers never register native listeners or invoke the original exit command.
export function AppExitPrompt({ subscribeCloseRequests = null, confirmExit = null }) {
  const [instanceCount, setInstanceCount] = useState(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!subscribeCloseRequests) return undefined;
    return subscribeCloseRequests((event) => { setBusy(false); setInstanceCount(event.instanceCount); });
  }, [subscribeCloseRequests]);
  return <AppExitDialog instanceCount={instanceCount} busy={busy} onCancel={() => setInstanceCount(null)} onConfirm={confirmExit ? () => {
    setBusy(true);
    try { Promise.resolve(confirmExit()).catch(() => setBusy(false)); }
    catch { setBusy(false); }
  } : null} />;
}
