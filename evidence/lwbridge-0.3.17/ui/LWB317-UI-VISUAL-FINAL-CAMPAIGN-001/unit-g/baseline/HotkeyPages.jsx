import { useI18n } from "./i18n.jsx";
import { formatSheepElapsed, HOTKEY_CARDS, mergeHotkeyField, MINI_GAME_HOTKEY_CARDS, previewHotkeyConfig, previewSheepStatus, sheepStatusKey } from "./previewRemainingPagesContracts.js";
import { useEffect, useRef, useState } from "react";
import { Switch, ToggleRow, PanelTitle } from "./sharedPageUI.jsx";

function HotkeyCard({ card, config, pendingField, onSaveField, previewEnabled = false }) {
  const { t } = useI18n();
  const enabled = config[card.key] === true;
  const cardWarning = card.warning ? t(card.warning) : "";
  const sourceAttack = card.key === "attack";
  return (
    <article className={`hotkey-card${cardWarning ? " hotkey-card-danger" : ""}`} data-preview-fixture={previewEnabled ? "hotkeys-config" : "runtime-config-unobserved"}>
      <div className="hotkey-card-header">
        <kbd className="hotkey-binding">{card.binding}</kbd>
        <button className="hotkey-switch" type="button" disabled={pendingField !== null} aria-label={`${t(card.title)}: ${t(enabled ? "common.enabled" : "common.disabled")}`} onClick={() => onSaveField({ ...config, [card.key]: !enabled }, card.key)}><Switch checked={enabled} /></button>
      </div>
      <h3>{t(card.title)}</h3>
      <p>{t(card.description)}</p>
      {sourceAttack ? (
        <div className="hotkey-attack-settings">
          <label className="hotkey-attack-toggle"><input type="checkbox" checked={config.attackMarchSpeedupItem === true} disabled={pendingField !== null} onChange={() => onSaveField({ ...config, attackMarchSpeedupItem: config.attackMarchSpeedupItem !== true }, "attackMarchSpeedupItem")} /><span>{t("hotkeys.attackSpeedupItem")}</span></label>
          <label className="hotkey-attack-toggle"><input type="checkbox" checked={config.attackMarchSpeedupDiamond === true} disabled={pendingField !== null} onChange={() => onSaveField({ ...config, attackMarchSpeedupDiamond: config.attackMarchSpeedupDiamond !== true }, "attackMarchSpeedupDiamond")} /><span>{t("hotkeys.attackSpeedupDiamond")}</span></label>
          <small>{t("hotkeys.attackSpeedupWarning")}</small>
        </div>
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
  const [sheepBusy, setSheepBusy] = useState(previewState === "mini-games-sheep-pending");
  const [sheepActionError, setSheepActionError] = useState(() => previewState === "mini-games-start-failed" ? t("miniGames.sheep.failed") : "");
  const [sheepStatus] = useState(() => previewEnabled ? previewSheepStatus(previewState, Date.now()) : null);
  const [now, setNow] = useState(() => Date.now());
  const sheepRunning = sheepStatus?.running === true;

  async function saveHotkeyField(nextConfig, field) {
    if (!hotkeyConfig || pendingField !== null) return;
    const previous = hotkeyConfig;
    setHotkeyConfig(nextConfig);
    setPendingField(field);
    setHotkeyError("");
    try {
      await Promise.resolve();
      if (previewState === saveErrorState) throw new Error("preview hotkey save failure");
      const saved = mergeHotkeyField(sourceConfigRef.current, nextConfig, field);
      sourceConfigRef.current = saved;
      setHotkeyConfig(saved);
    } catch {
      setHotkeyConfig(previous);
      setHotkeyError(t("hotkeys.saveFailed"));
    } finally {
      setPendingField(null);
    }
  }

  useEffect(() => {
    if (!isMiniGames || !sheepRunning) return undefined;
    setNow(Date.now());
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, [isMiniGames, sheepRunning]);

  async function openLandCell() {
    if (!previewEnabled || !online || landBusy) return;
    setLandBusy(true);
    setLandResult("");
    setHotkeyError("");
    try {
      await Promise.resolve();
      if (previewState === "mini-games-land-error") throw new Error("preview land cell failure");
      setLandResult(t("miniGames.landCellSent", { id: 17 }));
    } catch {
      setHotkeyError(t("miniGames.landCellFailed"));
    } finally {
      setLandBusy(false);
    }
  }

  async function toggleSheepGame() {
    if (!previewEnabled || !online || sheepBusy) return;
    setSheepBusy(true);
    setSheepActionError("");
    try {
      await Promise.resolve();
      if (previewState === "mini-games-start-failed") throw new Error("preview sheep failure");
    } catch {
      setSheepActionError(t("miniGames.sheep.failed"));
    } finally {
      setSheepBusy(false);
    }
  }

  const sheepStartedAt = Number(sheepStatus?.startedAt) || 0;
  const sheepDurationMs = sheepStatus?.durationMs ?? (sheepRunning && sheepStartedAt > 0 ? Math.max(0, now - sheepStartedAt) : 0);
  const showSheepElapsed = (sheepRunning && sheepStartedAt > 0) || (sheepStatus?.step === "completed" && sheepStatus?.durationMs != null);
  const sheepStatusText = sheepStatus?.plannedMoves
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
          <ToggleRow label={t("miniGames.treasureChest.enabled")} checked={hotkeyConfig?.treasureChestHint === true} disabled={!online || !hotkeyConfig || pendingField !== null} onChange={(enabled) => { if (hotkeyConfig) saveHotkeyField({ ...hotkeyConfig, treasureChestHint: enabled }, "treasureChestHint"); }} />
        </article> : null}
        {isMiniGames ? <article className="hotkey-card">
          <h3>{t("miniGames.landCell.title")}</h3>
          <p>{t("miniGames.landCell.description")}</p>
          <div className="mini-game-actions"><button className="primary" type="button" disabled={!previewEnabled || !online || landBusy} onClick={() => void openLandCell()}>{t(landBusy ? "miniGames.landCellOpening" : "miniGames.landCellAction")}</button></div>
          {landResult ? <span className="hotkey-state enabled">{landResult}</span> : null}
        </article> : null}
        {isMiniGames ? <article className="hotkey-card">
          <h3>{t("miniGames.sheep.title")}</h3>
          <p>{t("miniGames.sheep.description")}</p>
          {sheepStatus?.currentLevel ? <span className="hotkey-state">{t("miniGames.sheep.level", { level: sheepStatus.currentLevel })}</span> : null}
          {showSheepElapsed ? <span className="hotkey-state">{t("miniGames.sheep.elapsed", { time: formatSheepElapsed(sheepDurationMs) })}</span> : null}
          <span className={sheepStatus?.step === "completed" ? "hotkey-state enabled" : "hotkey-state"}>{sheepStatusText}</span>
          <div className="mini-game-actions"><button className={sheepRunning ? "" : "primary"} type="button" disabled={!previewEnabled || !online || sheepBusy} onClick={() => void toggleSheepGame()}>{t(sheepBusy ? "common.processing" : sheepRunning ? "common.stop" : "common.start")}</button></div>
          {sheepActionError ? <div className="automation-error" role="alert">{sheepActionError}</div> : null}
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
