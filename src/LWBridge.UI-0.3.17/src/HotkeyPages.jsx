import { useI18n } from "./i18n.jsx";
import { formatSheepElapsed, HOTKEY_CARDS, mergeHotkeyField, MINI_GAME_HOTKEY_CARDS, previewHotkeyConfig, previewSheepStatus, sheepStatusKey } from "./previewRemainingPagesContracts.js";
import { useEffect, useRef, useState } from "react";
import { Switch, ToggleRow, PanelTitle } from "./sharedPageUI.jsx";
import { backendBridge } from "./backendBridge.js";

function HotkeyCard({ card, config, pendingField, onSaveField, previewEnabled = false }) {
  const { t } = useI18n();
  const enabled = config[card.key] === true;
  const cardWarning = card.warning ? t(card.warning) : "";
  const sourceAttack = card.key === "attack";
  return (
    <article className={`hotkey-card${cardWarning ? " hotkey-card-danger" : ""}`} data-preview-fixture={previewEnabled ? "hotkeys-config" : undefined}>
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

// Original speedup switches are only *preferences* for a target that has not
// yet been source-verified. Keep their intent separate from this explicit,
// owned ATTACK-march item action: never convert a preference into a purchase.
// Native item-use returns a submission; two independent game data changes
// must be observed before reporting completion.
function OwnedRapidItemPanel({ online, previewEnabled }) {
  const { t, language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "当前行军道具加速", inspect: "读取当前编队与拥有的加速道具",
    slot: "编队", item: "拥有的道具", empty: "该编队没有已核实的可用加速道具",
    consent: "仅同意消耗所选的一件库存道具，不使用钻石",
    send: "使用一件道具", pending: "已提交，等待游戏行军时间和库存确认",
    confirmed: "已由游戏行军时间及道具库存共同确认",
    notConfirmed: "未获得权威确认；请在游戏中检查，不要重复使用",
    check: "只读取前一次请求的状态",
    hint: "只处理当前账号已拥有的攻击行军和库存道具。不会创建攻击，也不会购买钻石。",
  } : {
    title: "Current-march item speedup", inspect: "Inspect owned attack march and items",
    slot: "Squad", item: "Owned item", empty: "No verified owned speedup item for this squad",
    consent: "I approve consuming exactly one selected owned item. Never spend diamonds.",
    send: "Use one owned item", pending: "Request sent; waiting for both march time and inventory confirmation",
    confirmed: "Confirmed by game march time and owned inventory",
    notConfirmed: "Unconfirmed. Inspect the game state before attempting any other item use.",
    check: "Read previous request status only",
    hint: "Requires an existing owned ATTACK march. This does not start attacks or buy diamonds.",
  };
  const [slot, setSlot] = useState(1);
  const [items, setItems] = useState([]);
  const [itemId, setItemId] = useState("");
  const [marchUuid, setMarchUuid] = useState("");
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [pending, setPending] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const live = online && !previewEnabled;

  useEffect(() => {
    // Never reuse a selected consumable, old consent or pending appearance
    // after the profile owner, slot or authenticated connection changes.
    setItems([]); setItemId(""); setMarchUuid(""); setConsent(false);
    setPending(false); setStatus(""); setError(""); setBusy(false);
  }, [slot, live, backendBridge.profileGeneration]);

  async function inspect() {
    if (!live || busy || pending) return;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setError(""); setStatus(""); setItems([]); setItemId(""); setConsent(false);
    try {
      const response = await backendBridge.invokeProfileScoped(
        "feature018_rapid_action", { mode: "inventory", slot }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (response?.observation !== "native_owned_rapid_item_candidates" ||
          Number(response?.slot) !== slot || typeof response?.marchUuid !== "string" ||
          !Array.isArray(response?.items)) {
        throw new Error("Native rapid inventory provenance is unverified");
      }
      const options = response.items.filter((entry) =>
        /^\d+$/.test(String(entry?.itemId || "")) &&
        Number.isSafeInteger(entry?.count) && entry.count > 0);
      setMarchUuid(response.marchUuid);
      setItems(options);
      setItemId(options.length ? String(options[0].itemId) : "");
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }

  async function useOne() {
    if (!live || busy || pending || !consent || !marchUuid ||
        !items.some((entry) => String(entry.itemId) === itemId && entry.count > 0)) return;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setConsent(false); setError(""); setStatus("");
    try {
      const response = await backendBridge.invokeProfileScoped(
        "feature018_rapid_action",
        { mode: "itemUse", slot, itemId, itemOptIn: true }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (response?.requestDispatched !== true ||
          response?.awaitingGameConfirmation !== true ||
          Number(response?.slot) !== slot ||
          response?.marchUuid !== marchUuid ||
          String(response?.itemId) !== itemId) {
        throw new Error("Original march rapid sender gave no correlated dispatch receipt");
      }
      setPending(true);
      setStatus("pending");
      setItems([]); setItemId("");
    } catch (cause) {
      // A timeout or dropped response could still follow a server send.
      // Never auto-retry or claim the selected item was not used.
      if (backendBridge.isCurrentProfileOwner(owner)) {
        setError((cause.code || cause.message) + " — " + words.notConfirmed);
        setItems([]); setItemId("");
      }
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }

  async function readPrevious() {
    if (!live || busy) return;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setError("");
    try {
      const result = await backendBridge.invokeProfileScoped(
        "feature018_rapid_action", { mode: "status", slot }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.state === "confirmed" &&
          result.observation === "native_march_endTime_and_item_count_transition" &&
          result.marchUuid === marchUuid) {
        setStatus("confirmed"); setPending(false);
      } else if (result?.state === "pending" && result.requestDispatched === false) {
        setStatus("pending");
      } else {
        throw new Error("Native march/item status has no authoritative completion receipt");
      }
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError((cause.code || cause.message) + " — " + words.notConfirmed);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }

  useEffect(() => {
    if (!live || !pending) return undefined;
    let active = true;
    let checking = false;
    let tries = 0;
    const owner = backendBridge.currentProfileOwner();
    const timer = window.setInterval(async () => {
      if (!active || checking) return;
      checking = true;
      tries++;
      try {
        const result = await backendBridge.invokeProfileScoped(
          "feature018_rapid_action", { mode: "status", slot }, 10000);
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        if (result?.state === "confirmed" &&
            result.observation === "native_march_endTime_and_item_count_transition" &&
            result.marchUuid === marchUuid) {
          setStatus("confirmed"); setPending(false);
        } else if (result?.state !== "pending" || result.requestDispatched !== false) {
          setStatus("unconfirmed"); setPending(false);
        }
      } catch {
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setStatus("unconfirmed"); setPending(false);
        }
      } finally {
        checking = false;
        if (active && tries >= 12 && backendBridge.isCurrentProfileOwner(owner)) {
          setStatus((old) => old === "confirmed" ? old : "unconfirmed");
          setPending(false);
        }
      }
    }, 1500);
    return () => { active = false; window.clearInterval(timer); };
  }, [live, pending, slot, marchUuid, backendBridge.profileGeneration]);

  return (
    <article className="hotkey-card hotkey-rapid-card" data-feature018-rapid>
      <h3>{words.title}</h3>
      <p>{words.hint}</p>
      <label>{words.slot} <select aria-label={words.slot} value={slot} disabled={busy || pending} onChange={(event) => setSlot(Number(event.target.value))}>
        {[1, 2, 3, 4].map((number) => <option key={number} value={number}>{t("automation.squad", { index: number })}</option>)}
      </select></label>
      <div className="mini-game-actions"><button type="button" disabled={!live || busy || pending} onClick={() => void inspect()}>{words.inspect}</button></div>
      {marchUuid && items.length === 0 && status !== "pending" ? <small>{words.empty}</small> : null}
      {items.length > 0 ? <>
        <label>{words.item} <select aria-label={words.item} value={itemId} disabled={busy || pending}
          onChange={(event) => { setItemId(event.target.value); setConsent(false); }}>
          {items.map((item) => <option key={item.itemId} value={String(item.itemId)}>#{item.itemId} × {item.count}</option>)}
        </select></label>
        <label><input type="checkbox" checked={consent} disabled={busy || pending || !live}
          onChange={(event) => setConsent(event.target.checked)} /> {words.consent}</label>
        <div className="mini-game-actions"><button type="button" className="primary" disabled={!live || busy || pending || !consent || !itemId}
          onClick={() => void useOne()}>{words.send}</button></div>
      </> : null}
      <div className="mini-game-actions"><button type="button" disabled={!live || busy} onClick={() => void readPrevious()}>{words.check}</button></div>
      {status ? <span role="status" className={status === "confirmed" ? "hotkey-state enabled" : "hotkey-state"}>
        {status === "confirmed" ? words.confirmed : status === "pending" ? words.pending : words.notConfirmed}
      </span> : null}
      {error ? <div className="automation-error" role="alert">{error}</div> : null}
    </article>
  );
}

// Inspect the exact game's shield items and their original 8/12/24-hour
// StatusTab durations before deciding whether F6/F7/F8 can legitimately act.
// This card has no item sender, no diamond fallback, and no browser UID input.
function ShieldOwnedInventoryPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "保护罩道具库存（只读）", description: "读取当前账号已拥有的保护罩及原生持续时间，不消耗道具。",
    inspect: "检查当前保护罩道具", empty: "暂无已核实的保护罩道具",
    duration: "小时", count: "数量", ambiguous: "有多个同持续时间道具，不能自动选择",
  } : {
    title: "Owned shields (read-only)",
    description: "Inspect this account's actual shield items and their native duration without consuming anything.",
    inspect: "Inspect native shield inventory", empty: "No verified owned shields",
    duration: "hours", count: "Count", ambiguous: "Multiple items share the same duration; do not auto-select",
  };
  const live = online && !previewEnabled;
  const [busy, setBusy] = useState(false);
  const [items, setItems] = useState(null);
  const [error, setError] = useState("");
  useEffect(() => { setBusy(false); setItems(null); setError(""); },
    [live, backendBridge.profileGeneration]);
  async function inspect() {
    if (!live || busy) return;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setItems(null); setError("");
    try {
      const result = await backendBridge.invokeProfileScoped(
        "feature018_shield_inventory_get", {}, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.observation !== "native_MAIN_SHIELD_StatusTab_owned_item_duration_join" ||
          result?.durationMappingVerified !== true || result?.readOnly !== true ||
          result?.requestDispatched !== false || !Array.isArray(result?.items))
        throw new Error("Current game's native shield source was not verified");
      const validated = result.items.map((item) => {
        if (!/^\d+$/.test(String(item?.itemId || "")) ||
            !/^\d+$/.test(String(item?.uuid || "")) ||
            !Number.isSafeInteger(item?.count) || item.count <= 0 ||
            !Number.isFinite(item?.durationHours) || item.durationHours <= 0)
          throw new Error("Current game's shield stock has invalid native fields");
        return item;
      });
      setItems(validated);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-shield-inventory>
    <h3>{words.title}</h3><p>{words.description}</p>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {items !== null ? <div role="status">{items.length === 0 ? <small>{words.empty}</small> :
      items.map((item) => <div key={item.uuid}>
        <small>#{item.itemId} · {item.durationHours} {words.duration} · {words.count}: {item.count}</small>
        {item.uniqueDuration !== true ? <small>{words.ambiguous}</small> : null}
      </div>)}</div> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
}

// Alt+1–4 retain their original intent, but the current v26 install reply
// contains no per-equipment correlated server ACK. Never present a preview
// as an applied preset, or accept a browser-supplied equipment UUID.
function EquipmentNativePreflightPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "装备预设原生检查（只读）", description: "查看已保存预设与游戏当前英雄装备的差异。不装备、不拆卸任何道具。",
    slot: "预设编号", inspect: "检查当前游戏装备", missing: "没有可用的游戏检查结果。",
    ready: "已核对当前装备；尚未执行预设应用。", unsupported: "预设应用仍需可靠的服务器装备确认。",
    changed: "需要变更的位置", worn: "由其他英雄装备", uninstall: "需要卸下",
  } : {
    title: "Native equipment preset inspection (read-only)",
    description: "Compare a saved preset against current game hero slots without equipping or removing an item.",
    slot: "Preset number", inspect: "Inspect current game equipment",
    missing: "No verified native equipment result.",
    ready: "Current equipment inspected. The preset has not been applied.",
    unsupported: "Preset application still requires correlated server equipment confirmation.",
    changed: "Slots to change", worn: "Worn by other heroes", uninstall: "Slots needing removal",
  };
  const [slot, setSlot] = useState(1);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const live = online && !previewEnabled;
  useEffect(() => {
    setResult(null); setError(""); setBusy(false);
  }, [slot, live, backendBridge.profileGeneration]);
  async function inspect() {
    if (!live || busy) return;
    const owner = backendBridge.currentProfileOwner();
    setResult(null); setError(""); setBusy(true);
    try {
      // Profile runtime derives these slots from its *persisted confirmed*
      // equipment_config_get. The browser sends neither UID nor preset data.
      const data = await backendBridge.invokeProfileScoped(
        "feature018_equipment_preflight", { slot }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (data?.observation !== "native_HeroEquip_slot_inventory_preflight" ||
          data?.readOnly !== true || data?.readyToApply !== false ||
          data?.requestDispatched !== false || Number(data?.slot) !== slot ||
          !Array.isArray(data?.slots) || !Number.isInteger(data?.changedSlots)) {
        throw new Error("Native equipment source did not provide a read-only owner-confirmed observation");
      }
      setResult(data);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-equipment-preflight>
    <h3>{words.title}</h3><p>{words.description}</p>
    <label>{words.slot} <select aria-label={words.slot} value={slot} disabled={busy}
      onChange={(event) => setSlot(Number(event.target.value))}>
      {[1, 2, 3, 4].map((value) => <option key={value} value={value}>{value}</option>)}
    </select></label>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {result ? <div role="status"><span className="hotkey-state">{words.ready}</span>
      <small>{words.changed}: {result.changedSlots} · {words.worn}: {result.wornByOtherHero ?? 0} · {words.uninstall}: {result.uninstallSlots ?? 0}</small>
      <small>{words.unsupported}</small>
    </div> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
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
        const response = await backendBridge.invokeProfileScoped("mini_land_status", {}, 5000);
        if (!active || !backendBridge.isCurrentProfileOwner(owner)) return;
        if (response?.cellId !== landPendingId) {
          setHotkeyError(`${t("miniGames.landCellFailed")} (Land cell identity changed)`);
          setLandPendingId(null);
        } else if (response?.state === "unlocked") {
          setLandResult(`${t("miniGames.landCellSent", { id: landPendingId })} · ${t("common.completed")}`);
          setLandPendingId(null);
        } else if (response?.state !== "locked") {
          setHotkeyError(`${t("miniGames.landCellFailed")} (Unconfirmed native land state)`);
          setLandPendingId(null);
        }
      } catch (error) {
        if (active && backendBridge.isCurrentProfileOwner(owner)) {
          setHotkeyError(`${t("miniGames.landCellFailed")} (${error.code || error.message})`);
          setLandPendingId(null);
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
    setLandBusy(true);
    setLandResult("");
    setHotkeyError("");
    try {
      if (previewEnabled) {
        await Promise.resolve();
        if (previewState === "mini-games-land-error") throw new Error("preview land cell failure");
        setLandResult(t("miniGames.landCellSent", { id: 17 }));
      } else {
        const result = await backendBridge.invokeProfileScoped("monopoly_cell_open");
        if (!Number.isInteger(result?.cellId) || result.cellId <= 0 || result.requestDispatched !== true) {
          throw new Error("monopoly_cell_open did not confirm a cell ID");
        }
        setLandResult(t("miniGames.landCellSent", { id: result.cellId }));
        setLandPendingId(result.cellId);
      }
    } catch (error) {
      setHotkeyError(`${t("miniGames.landCellFailed")}${previewEnabled ? "" : ` (${error.code || error.message})`}`);
    } finally {
      setLandBusy(false);
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
        {!isMiniGames && hotkeyConfig ? <OwnedRapidItemPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <ShieldOwnedInventoryPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <EquipmentNativePreflightPanel online={online} previewEnabled={previewEnabled} /> : null}
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
