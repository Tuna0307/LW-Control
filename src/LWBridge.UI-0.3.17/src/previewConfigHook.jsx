import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { createConfigDraft } from "./previewConfig.js";

// Recovered config stores survive view changes. This registry is page-memory
// only; it has no transport, disk persistence or native provider fallback.
const previewStores = new Map();
export function usePreviewConfig(initial, valid, failFirstSave = false, scope = "") {
  const validRef = useRef(valid);
  validRef.current = valid;
  const [store] = useState(() => {
    if (scope && previewStores.has(scope)) {
      const cached = previewStores.get(scope);
      cached.validRef.current = valid;
      return cached.store;
    }
    let confirmed = structuredClone(typeof initial === "function" ? initial() : initial);
    let failurePending = failFirstSave;
    const draftStore = createConfigDraft(confirmed, {
      valid: (draft) => validRef.current(draft),
      read: async () => structuredClone(confirmed),
      write: async (draft) => {
        if (failurePending) {
          failurePending = false;
          throw new Error("PREVIEW_CONFIG_SAVE_FAILED");
        }
        confirmed = structuredClone(draft);
        return structuredClone(confirmed);
      },
    });
    if (scope) previewStores.set(scope, { store: draftStore, validRef });
    return draftStore;
  });
  const state = useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
  useEffect(() => () => { store.flush().catch(() => {}); }, [store]);
  return { ...state, store };
}

export function PreviewConfigError({ config, t }) {
  if (!config.error) return null;
  return <div className="automation-error" role="alert">
    <span>{t("configSave.failed")}</span>{" "}
    <button type="button" disabled={config.saving} onClick={() => config.store.flush().catch(() => {})}>{t("common.retry")}</button>{" "}
    <button type="button" disabled={config.saving} onClick={() => config.store.refresh(true).catch(() => {})}>{t("configSave.discard")}</button>
  </div>;
}
