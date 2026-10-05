import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { createConfigDraft } from "./previewConfig.js";

// Recovered config stores survive view changes. This registry is page-memory
// only; it has no transport, disk persistence or native provider fallback.
const previewStores = new Map();
export function usePreviewConfigAdapter(initial, adapter, scope = "") {
  const adapterRef = useRef(adapter);
  adapterRef.current = adapter;
  const stableAdapterRef = useRef(null);
  if (!stableAdapterRef.current) {
    stableAdapterRef.current = {
      valid: (draft) => adapterRef.current.valid?.(draft),
      read: () => adapterRef.current.read(),
      write: (draft) => adapterRef.current.write(draft),
    };
  }
  const [store] = useState(() => {
    if (scope && previewStores.has(scope)) {
      const cached = previewStores.get(scope);
      cached.store.setAdapter(stableAdapterRef.current);
      return cached.store;
    }
    const confirmed = structuredClone(typeof initial === "function" ? initial() : initial);
    const draftStore = createConfigDraft(confirmed, stableAdapterRef.current);
    if (scope) previewStores.set(scope, { store: draftStore });
    return draftStore;
  });
  const state = useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
  return { ...state, store };
}

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

export function PreviewConfigError({ config, t, label = "", disabled = false }) {
  if (!config.error) return null;
  return <div className="automation-error" role="alert">
    {label ? <strong>{label} </strong> : null}
    <span>{t("configSave.failed")}</span>{" "}
    <button type="button" disabled={disabled || config.saving} onClick={() => Promise.resolve(config.store.flush()).catch(() => {})}>{t("common.retry")}</button>{" "}
    <button type="button" disabled={disabled || config.saving} onClick={() => Promise.resolve(config.store.refresh(true)).catch(() => {})}>{t("configSave.discard")}</button>
  </div>;
}
