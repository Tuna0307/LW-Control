import { useI18n } from "./i18n.jsx";
import { formatSheepElapsed, HOTKEY_CARDS, mergeHotkeyField, MINI_GAME_HOTKEY_CARDS, previewHotkeyConfig, previewSheepStatus, sheepStatusKey } from "./previewRemainingPagesContracts.js";
import { useEffect, useRef, useState } from "react";
import { Switch, ToggleRow, PanelTitle } from "./sharedPageUI.jsx";
import { backendBridge } from "./backendBridge.js";

function HotkeyCard({ card, config, pendingField, onSaveField, previewEnabled = false }) {
  const { t } = useI18n();
  const enabled = config[card.key] === true;
  const cardWarning = card.warning ? t(card.warning) : "";
  return (
    <article className={`hotkey-card${cardWarning ? " hotkey-card-danger" : ""}`} data-preview-fixture={previewEnabled ? "hotkeys-config" : undefined}>
      <div className="hotkey-card-header">
        <kbd className="hotkey-binding">{card.binding}</kbd>
        <button className="hotkey-switch" type="button" disabled={pendingField !== null} aria-label={`${t(card.title)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => onSaveField({ ...config, [card.key]: !enabled }, card.key)}><Switch checked={enabled} /></button>
      </div>
      <h3>{t(card.title)}</h3>
      <p>{t(card.description)}</p>
      {card.key === "frontlineReinforce" ? (
        <label className="hotkey-attack-toggle">
          {t("hotkeys.frontlineReinforce.countLabel")}
          <input type="number" min={1} max={50} step={1} style={{ width: "4.5em", marginLeft: "0.5em" }}
            value={Number.isInteger(config.frontlineReinforceCount) ? config.frontlineReinforceCount : 5}
            disabled={pendingField !== null}
            onChange={(event) => {
              const count = Math.round(Number(event.target.value));
              if (Number.isFinite(count) && count >= 1 && count <= 50 && count !== config.frontlineReinforceCount)
                onSaveField({ ...config, frontlineReinforceCount: count }, "frontlineReinforceCount");
            }} />
        </label>
      ) : null}
      <span className={`hotkey-state${enabled ? " enabled" : ""}`}>{t(enabled ? "common.enabled" : "common.disabled")}</span>
      {cardWarning ? <div className="hotkey-danger">{cardWarning}</div> : null}
    </article>
  );
}

const MINI_GAMES_PREVIEW_STATES = new Set([
  "mini-games-active",
  "mini-games-solving",
  "mini-games-executing",
  "mini-games-completed",
  "mini-games-complete",
  "mini-games-all-complete",
  "mini-games-activity-ended",
  "mini-games-ui-open",
  "mini-games-conflict",
  "mini-games-solve-failed",
  "mini-games-unsupported",
  "mini-games-start-failed",
  "mini-games-land-success",
  "mini-games-land-error",
  "mini-games-land-opening",
  "mini-games-offline",
  "mini-games-config-loading",
  "mini-games-load-error",
  "mini-games-config-saving",
  "mini-games-save-error",
  "mini-games-sheep-pending",
  "mini-games-refreshing",
  "mini-games-starting",
  "mini-games-opening",
  "mini-games-initial-delay",
  "mini-games-state-error",
]);

export function RecoveredHotkeyPanel({ category = "hotkeys", previewState = "", online: runtimeOnline = false }) {
  const { t } = useI18n();
  const isMiniGames = category === "miniGames";
  const previewPrefix = isMiniGames ? "mini-games-" : "hotkeys-";
  const previewEnabled = isMiniGames ? MINI_GAMES_PREVIEW_STATES.has(previewState) : previewState.startsWith(previewPrefix);
  const online = previewEnabled
    ? isMiniGames
      ? previewState !== "mini-games-offline"
      : previewState === "hotkeys-connected" || previewState === "hotkeys-saving"
    : runtimeOnline;
  const loadErrorState = isMiniGames ? "mini-games-load-error" : "hotkeys-load-error";
  const loadingState = isMiniGames ? "mini-games-config-loading" : "hotkeys-loading";
  const saveErrorState = isMiniGames ? "mini-games-save-error" : "hotkeys-save-error";
  const savingState = isMiniGames ? "mini-games-config-saving" : "hotkeys-saving";
  const [hotkeyConfig, setHotkeyConfig] = useState(() => previewState === loadErrorState || previewState === loadingState || !previewEnabled ? null : previewHotkeyConfig());
  const [pendingField, setPendingField] = useState(() => previewState === savingState ? (isMiniGames ? "treasureChestHint" : "attack") : null);
  const [hotkeyError, setHotkeyError] = useState(() => previewState === loadErrorState ? t("hotkeys.loadFailed") : previewState === saveErrorState ? t("hotkeys.saveFailed") : previewState === "mini-games-land-error" ? t("miniGames.landCellFailed") : "");
  const sourceConfigRef = useRef(hotkeyConfig ?? previewHotkeyConfig());
  const [landBusy, setLandBusy] = useState(previewState === "mini-games-land-opening");
  const [landResult, setLandResult] = useState(() => previewState === "mini-games-land-success" ? t("miniGames.landCellSent", { id: 17 }) : "");
  const [landPendingId, setLandPendingId] = useState(null);
  const [chestMarker, setChestMarker] = useState({ visible: false, reason: "" });
  const [sheepBusy, setSheepBusy] = useState(previewState === "mini-games-sheep-pending");
  const [sheepActionError, setSheepActionError] = useState(() => previewState === "mini-games-start-failed" ? t("miniGames.sheep.failed") : "");
  const [previewSheepSnapshot] = useState(() => previewEnabled ? previewSheepStatus(previewState, Date.now()) : null);
  const [liveSheepStatus, setLiveSheepStatus] = useState(null);
  const sheepStatus = previewEnabled ? previewSheepSnapshot : liveSheepStatus;
  const [now, setNow] = useState(() => Date.now());
  const sheepRunning = sheepStatus?.running === true;

  useEffect(() => {
    if (previewEnabled) return undefined;
    let active = true;
    const owner = backendBridge.currentProfileOwner();
    setHotkeyConfig(null);
    setHotkeyError("");
    const read = async () => {
      try {
        const config = await backendBridge.invokeProfileScoped("hotkey_config_get");
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        sourceConfigRef.current = config;
        setHotkeyConfig(config);
      } catch (error) {
        if (active) setHotkeyError(`${t("hotkeys.loadFailed")} (${error.code || error.message})`);
      }
    };
    void read();
    return () => { active = false; };
  }, [previewEnabled, category, backendBridge.profileGeneration]);

  async function saveHotkeyField(nextConfig, field) {
    if (!hotkeyConfig || pendingField !== null) return;
    const previous = hotkeyConfig;
    setHotkeyConfig(nextConfig);
    setPendingField(field);
    setHotkeyError("");
    const owner = backendBridge.currentProfileOwner();
    try {
      let saved;
      if (previewEnabled) {
        await Promise.resolve();
        if (previewState === saveErrorState) throw new Error("preview hotkey save failure");
        saved = mergeHotkeyField(sourceConfigRef.current, nextConfig, field);
      } else {
        // Original 0.3.17 performs a fresh get before every whole-object save,
        // so another tab's updated fields are retained.
        const fresh = await backendBridge.invokeProfileScoped("hotkey_config_get");
        saved = await backendBridge.invokeProfileScoped("hotkey_config_save", { ...fresh, [field]: nextConfig[field] });
      }
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      sourceConfigRef.current = saved;
      setHotkeyConfig(saved);
    } catch (error) {
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      setHotkeyConfig(previous);
      setHotkeyError(`${t("hotkeys.saveFailed")}${previewEnabled ? "" : ` (${error.code || error.message})`}`);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setPendingField(null);
    }
  }

  useEffect(() => {
    if (!isMiniGames || !sheepRunning) return undefined;
    setNow(Date.now());
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, [isMiniGames, sheepRunning]);

  useEffect(() => {
    if (!isMiniGames || previewEnabled || !online) {
      setLiveSheepStatus(null);
      return undefined;
    }
    let active = true;
    let busy = false;
    const owner = backendBridge.currentProfileOwner();
    const refresh = async () => {
      if (!active || busy) return;
      busy = true;
      try {
        const [managerResult, solverResult] = await Promise.allSettled([
          backendBridge.invokeProfileScoped("mini_sheep_status", {}, 5000),
          backendBridge.invokeProfileScoped("feature018_sheep_automation_status", {}, 5000),
        ]);
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        // Manager may return unsupported after the event closes. The owned
        // solver's terminal receipt is independently authoritative; keep it.
        const response = managerResult.status === "fulfilled" ? managerResult.value : null;
        const solver = solverResult.status === "fulfilled" ? solverResult.value : null;
        if (!solver) {
          setLiveSheepStatus({ state: "error", step: "unsupported_client", running: false });
          return;
        }
        // The live manager gives the currently available level; the solver
        // owns the original level of a terminal run. Native send-only
        // acknowledgements cannot set completed or confirmedMoves.
        const managerLevelValid = response?.game === "LWSheep" && Number.isInteger(response?.level) && response.level >= 0;
        // A completed or failed run may outlive Season 4 manager availability;
        // retain its authoritative server-confirmed terminal status and the
        // run's original level instead of calling completion unsupported.
        const terminal = solver?.running === false && ["completed", "solve_failed", "stopped"].includes(solver?.status);
        const runLevelValid = Number.isInteger(solver?.currentLevel) && solver.currentLevel >= 1;
        const level = terminal && runLevelValid ? solver.currentLevel : managerLevelValid ? response.level
          : runLevelValid ? solver.currentLevel : null;
        if (managerLevelValid || (terminal && level !== null)) {
          const stage = typeof solver?.status === "string" ? solver.status : "idle";
          const running = solver?.running === true;
          const elapsed = Number(solver?.elapsedSeconds);
          setLiveSheepStatus({
            running, state: stage === "solve_failed" ? "error" : running ? "running" : "idle",
            step: stage, currentLevel: level,
            confirmedMoves: Number.isInteger(solver?.confirmedMoves) ? solver.confirmedMoves : 0,
            plannedMoves: Number.isInteger(solver?.totalMoves) && solver.totalMoves > 0 ? solver.totalMoves : 0,
            startedAt: running && Number.isFinite(elapsed) && elapsed >= 0 ? Date.now() - elapsed * 1000 : null,
            durationMs: Number.isFinite(elapsed) && elapsed >= 0 ? elapsed * 1000 : null,
            error: typeof solver?.error === "string" ? solver.error : "",
          });
        } else {
          setLiveSheepStatus({ state: "error", step: "unsupported_client", running: false });
        }
      } catch {
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setLiveSheepStatus({ state: "error", step: "unsupported_client", running: false });
        }
      } finally {
        busy = false;
      }
    };
    void refresh();
    const interval = window.setInterval(() => void refresh(), 2000);
    return () => { active = false; window.clearInterval(interval); };
  }, [isMiniGames, previewEnabled, online, backendBridge.profileGeneration]);

  useEffect(() => {
    if (!isMiniGames || previewEnabled || !online || !hotkeyConfig) {
      setChestMarker({ visible: false, reason: "" });
      return undefined;
    }
    let active = true;
    let busy = false;
    const owner = backendBridge.currentProfileOwner();
    const enabled = hotkeyConfig.treasureChestHint === true;
    const synchronize = async () => {
      if (!active || busy) return;
      busy = true;
      try {
        const result = await backendBridge.invokeProfileScoped(
          enabled ? "mini_chest_mark" : "mini_chest_unmark", {}, 5000);
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setChestMarker({ visible: result?.markerVisible === true && enabled,
            reason: result?.observation || "" });
        }
      } catch (error) {
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setChestMarker({ visible: false, reason: enabled ? error.code || error.message : "" });
        }
      } finally { busy = false; }
    };
    void synchronize();
    // The game opens chest views independently of the app. Retry only while
    // the persisted preference is enabled; a marker is never reported without
    // the game's actual visible effect confirmation.
    const interval = enabled ? window.setInterval(() => void synchronize(), 2000) : null;
    return () => { active = false; if (interval !== null) window.clearInterval(interval); };
  }, [isMiniGames, previewEnabled, online, hotkeyConfig?.treasureChestHint, backendBridge.profileGeneration]);

  useEffect(() => {
    if (!isMiniGames || previewEnabled) return;
    // Native requests are owner-scoped. A previous profile's local pending
    // cell ID and confirmation text must never carry over to the new owner.
    setLandPendingId(null);
    setLandResult("");
  }, [isMiniGames, previewEnabled, backendBridge.profileGeneration]);

  useEffect(() => {
    if (previewEnabled || !isMiniGames || !online) return undefined;
    let active = true;
    const owner = backendBridge.currentProfileOwner();
    // The game-side one-cell request stays pending when this tab unmounts.
    // Restore that pending indicator from the native state after a remount.
    void backendBridge.invokeProfileScoped("mini_land_status", {}, 5000).then((response) => {
      if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
      if (response?.state === "locked" && response?.awaitingGameConfirmation === true &&
          Number.isInteger(response.cellId) && response.cellId > 0) {
        setLandPendingId(response.cellId);
        setLandResult(t("miniGames.landCellSent", { id: response.cellId }));
      }
    }).catch(() => { /* An unavailable land feature has no pending request to restore. */ });
    return () => { active = false; };
  }, [previewEnabled, isMiniGames, online, backendBridge.profileGeneration]);

  useEffect(() => {
    if (previewEnabled || !isMiniGames || !online || landPendingId === null) return undefined;
    let active = true;
    let busy = false;
    const owner = backendBridge.currentProfileOwner();
    const refreshLand = async () => {
      if (!active || busy) return;
      busy = true;
      try {
        // Read the EXACT cell of the one previously sent packet. The default
        // "next locked cell" changes after success and cannot identify the
        // request/reply pair; an unrelated push can also unlock it.
        const response = await backendBridge.invokeProfileScoped(
          "mini_land_status", { expectedCellId: landPendingId }, 5000);
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        if (response?.cellId !== landPendingId || response?.exactCellLookup !== true) {
          setHotkeyError(`${t("miniGames.landCellFailed")} (Land cell identity changed)`);
        } else if (response?.state === "unlocked" &&
            response?.requestProof?.requestCausalityConfirmed === true &&
            response.requestProof.replyLandId === landPendingId &&
            response.requestProof.targetRequestCount === 1 &&
            response.requestProof.replyCount === 1 &&
            response.requestProof.accountFenceIntact === true) {
          setLandResult(`${t("miniGames.landCellSent", { id: landPendingId })} · ${t("common.completed")}`);
          setLandPendingId(null);
        } else if (response?.state === "unlocked") {
          // No second send: the change may be a push or an unrelated user
          // action. Keep the pending latch and expose the missing proof.
          setHotkeyError(`${t("miniGames.landCellFailed")} (Native reply correlation unconfirmed)`);
        } else if (response?.state !== "locked") {
          setHotkeyError(`${t("miniGames.landCellFailed")} (Unconfirmed native land state)`);
        }
      } catch (error) {
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setHotkeyError(`${t("miniGames.landCellFailed")} (${error.code || error.message})`);
          // A status timeout may follow a successful send. Never enable the
          // action button to retransmit while this owner remains pending.
        }
      } finally {
        busy = false;
      }
    };
    void refreshLand();
    const interval = window.setInterval(() => void refreshLand(), 1000);
    return () => { active = false; window.clearInterval(interval); };
  }, [previewEnabled, isMiniGames, online, landPendingId, backendBridge.profileGeneration]);

  async function openLandCell() {
    if (!online || landBusy || landPendingId !== null) return;
    const owner = backendBridge.currentProfileOwner();
    setLandBusy(true);
    setLandResult("");
    setHotkeyError("");
    try {
      if (previewEnabled) {
        await Promise.resolve();
        if (previewState === "mini-games-land-error") throw new Error("preview land cell failure");
        setLandResult(t("miniGames.landCellSent", { id: 17 }));
      } else {
        const current = await backendBridge.invokeProfileScoped("mini_land_status", {}, 5000);
        if (!backendBridge.isCurrentProfileOwner(owner)) return;
        if (!Number.isInteger(current?.cellId) || current.cellId <= 0 ||
            current.cellId > 10000 || current.state !== "locked") {
          throw new Error("No exact current locked native Land Cell to unlock");
        }
        const expectedCellId = current.cellId;
        // Latch before dispatch: even an RPC timeout can follow a genuine
        // UnlockUserLand send. Native and UI must never retry an uncertain
        // action with an inferred next locked ID.
        setLandPendingId(expectedCellId);
        const result = await backendBridge.invokeProfileScoped(
          "monopoly_cell_open", { expectedCellId }, 10000);
        if (!backendBridge.isCurrentProfileOwner(owner)) return;
        if (result?.cellId !== expectedCellId || result.requestDispatched !== true ||
            result.awaitingGameConfirmation !== true) {
          throw new Error("monopoly_cell_open did not confirm the exact one-shot packet submission");
        }
        setLandResult(t("miniGames.landCellSent", { id: result.cellId }));
      }
    } catch (error) {
      if (previewEnabled || backendBridge.isCurrentProfileOwner(owner))
        setHotkeyError(`${t("miniGames.landCellFailed")}${previewEnabled ? "" : ` (${error.code || error.message})`}`);
    } finally {
      if (previewEnabled || backendBridge.isCurrentProfileOwner(owner)) setLandBusy(false);
    }
  }

  async function toggleSheepGame() {
    if (!online || sheepBusy) return;
    setSheepBusy(true);
    setSheepActionError("");
    try {
      if (previewEnabled) {
        await Promise.resolve();
        if (previewState === "mini-games-start-failed") throw new Error("preview sheep failure");
      } else {
        // The exact original frontend calls startAutomation/stopAutomation
        // with the task name sheepGame. A successful dispatch is only an
        // acknowledgement; the next real automation_status decides running.
        await backendBridge.invokeProfileScoped(sheepRunning ? "automation_stop" : "automation_start", { taskName: "sheepGame" });
      }
    } catch (error) {
      setSheepActionError(`${t("miniGames.sheep.failed")}${previewEnabled ? "" : ` (${error.code || error.message})`}`);
    } finally {
      setSheepBusy(false);
    }
  }

  const sheepStartedAt = Number(sheepStatus?.startedAt) || 0;
  const sheepDurationMs = sheepStatus?.durationMs ?? (sheepRunning && sheepStartedAt > 0 ? Math.max(0, now - sheepStartedAt) : 0);
  const showSheepElapsed = (sheepRunning && sheepStartedAt > 0) || (sheepStatus?.step === "completed" && sheepStatus?.durationMs != null);
  const sheepStatusText = sheepRunning && sheepStatus?.step === "executing" && sheepStatus?.plannedMoves > 0
    ? t("miniGames.sheep.progress", { confirmed: sheepStatus.confirmedMoves ?? 0, total: sheepStatus.plannedMoves })
    : t(sheepStatusKey(sheepStatus));
  const cards = isMiniGames ? MINI_GAME_HOTKEY_CARDS : HOTKEY_CARDS;

  return (
    <section className="panel hotkey-panel" data-preview-fixture={previewEnabled ? previewState : undefined} data-hotkey-category={category}>
      <PanelTitle title={t(`${category}.title`)} subtitle={t(`${category}.description`)} />
      <div className="hotkey-status">{t(online ? "status.gameConnected" : "hotkeys.offlineHint")}</div>
      {hotkeyConfig === null && !hotkeyError ? <div className="muted">{t("common.processing")}</div> : null}
      {(hotkeyConfig || isMiniGames) ? <div className="hotkey-grid">
        {hotkeyConfig ? cards.map((card) => <HotkeyCard key={card.key} card={card} config={hotkeyConfig} pendingField={pendingField} onSaveField={saveHotkeyField} previewEnabled={previewEnabled} />) : null}
        {isMiniGames ? <article className="hotkey-card">
          <h3>{t("miniGames.treasureChest.title")}</h3>
          <p>{t("miniGames.treasureChest.description")}</p>
          <ToggleRow label={t("miniGames.treasureChest.enabled")} checked={hotkeyConfig?.treasureChestHint === true} disabled={!hotkeyConfig || pendingField !== null} onChange={(enabled) => { if (hotkeyConfig) saveHotkeyField({ ...hotkeyConfig, treasureChestHint: enabled }, "treasureChestHint"); }} />
          {!previewEnabled && chestMarker.visible ? <span className="hotkey-state enabled">{t("miniGames.treasureChest.marker")}</span> : null}
          {!previewEnabled && hotkeyConfig?.treasureChestHint === true && chestMarker.reason && !chestMarker.visible ? <small>{chestMarker.reason}</small> : null}
        </article> : null}
        {isMiniGames ? <article className="hotkey-card">
          <h3>{t("miniGames.landCell.title")}</h3>
          <p>{t("miniGames.landCell.description")}</p>
          <div className="mini-game-actions"><button className="primary" type="button" disabled={!online || landBusy || landPendingId !== null} onClick={() => void openLandCell()}>{t(landBusy || landPendingId !== null ? "miniGames.landCellOpening" : "miniGames.landCellAction")}</button></div>
          {landResult ? <span className="hotkey-state enabled">{landResult}</span> : null}
        </article> : null}
        {isMiniGames ? <article className="hotkey-card">
          <h3>{t("miniGames.sheep.title")}</h3>
          <p>{t("miniGames.sheep.description")}</p>
          {sheepStatus?.currentLevel ? <span className="hotkey-state">{t("miniGames.sheep.level", { level: sheepStatus.currentLevel })}</span> : null}
          {showSheepElapsed ? <span className="hotkey-state">{t("miniGames.sheep.elapsed", { time: formatSheepElapsed(sheepDurationMs) })}</span> : null}
          <span className={sheepStatus?.step === "completed" ? "hotkey-state enabled" : "hotkey-state"}>{sheepStatusText}</span>
          <div className="mini-game-actions"><button className={sheepRunning ? "" : "primary"} type="button" disabled={!online || sheepBusy} onClick={() => void toggleSheepGame()}>{t(sheepBusy ? "common.processing" : sheepRunning ? "common.stop" : "common.start")}</button></div>
          {sheepActionError ? <div className="automation-error" role="alert">{sheepActionError}</div> : null}
          {!previewEnabled && sheepStatus?.error && sheepStatus?.step === "solve_failed" ? <div className="automation-error" role="alert">{sheepStatus.error}</div> : null}
        </article> : null}
      </div> : null}
      {hotkeyError ? <div className="automation-error" role="alert">{hotkeyError}</div> : null}
    </section>
  );
}

export function HotkeysPage({ previewState = "", online = false }) {
  return <RecoveredHotkeyPanel category="hotkeys" previewState={previewState} online={online} />;
}

export function MiniGamesPage({ previewState = "", online = false }) {
  return <RecoveredHotkeyPanel category="miniGames" previewState={previewState} online={online} />;
}
