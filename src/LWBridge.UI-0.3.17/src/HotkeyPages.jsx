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

// This native observation is deliberately NOT a virtual Q/W/E/R press. The
// original current-main-world mouse raycast is still unproven. Formation
// readiness is useful only as a current owner-scoped, read-only diagnosis.
function NativeAttackReadinessPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "Q/W/E/R 原生出征条件", description: "只读取当前队伍兵力、空闲士兵与目标。不会编队或出征。",
    inspect: "检查原生出征准备", total: "空闲士兵", target: "已核实普通怪物目标",
    squad: "队伍", locked: "未解锁", soldier: "已编入士兵", hero: "英雄",
    capacity: "队伍容量", canStaff: "可以在游戏中手动编入空闲士兵",
    missing: "还没有可核实的出征编队", guard: "真实鼠标光标目标、服务器确认和游戏窗口按键仍须单独核验",
    editorConsent: "确认仅打开游戏原生编队编辑器；我会在游戏中手动保存，不自动出征",
    editor: "打开原生编队编辑器", editorOpened: "原生编辑器已打开；未保存编队、未派遣攻击，请在游戏中操作",
    needSelector: "请先在游戏中打开原生出征队伍选择界面，再重新检查",
    inspectSelector: "只读核对游戏当前已选择的目标",
    selectedTarget: "原生已选择的目标",
    selectorGuard: "该结果不能证明当前物理鼠标指向此目标；未出征，未调用攻击。",
    inspectReadiness: "核对选定队伍及目标的全部原生出征条件（只读）",
    sourceReadiness: "原生编队和已选择目标的来源条件",
    remainingReasons: "仍缺少的原生条件",
    cursorUnverified: "即使来源条件全部满足，当前物理鼠标目标、原生服务器回复及行军后状态仍未核实，不能声称已出征。",
  } : {
    title: "Q/W/E/R native attack readiness",
    description: "Inspect current squads, unassigned soldiers and game-owned targets. Does not assign troops or attack.",
    inspect: "Inspect native attack readiness", total: "Unassigned soldiers",
    target: "Verified ordinary monster candidates", squad: "Squad", locked: "Locked",
    soldier: "Staffed soldiers", hero: "Heroes", capacity: "Squad capacity",
    canStaff: "Troops may be manually assigned through the game's own formation UI",
    missing: "No eligible native attack formation yet",
    guard: "Physical cursor target, server ACK and game-window shortcut still require separate proof",
    editorConsent: "Open only the original game formation editor; I will save there myself. Never auto-attack.",
    editor: "Open native formation editor", editorOpened: "Original editor opened. No troop save or attack dispatched; continue in the game.",
    needSelector: "First open the game's original march selector, then inspect again.",
    inspectSelector: "Read currently selected native target",
    selectedTarget: "Game-selected native target",
    selectorGuard: "This does not prove the current physical mouse target. No attack was dispatched.",
    inspectReadiness: "Inspect all original selected squad/target prerequisites (read-only)",
    sourceReadiness: "Original selected squad and target source prerequisites",
    remainingReasons: "Remaining native prerequisites",
    cursorUnverified: "Even when source conditions pass, physical cursor target, server reply and fresh owned march still lack proof. No attack was dispatched.",
  };
  const [data, setData] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [editorSlot, setEditorSlot] = useState(1);
  const [editorConsent, setEditorConsent] = useState(false);
  const [editorOpened, setEditorOpened] = useState(false);
  const [nativeTarget, setNativeTarget] = useState(null);
  const [sourceReadiness, setSourceReadiness] = useState(null);
  const editorInFlight = useRef(false);
  const live = online && !previewEnabled;
  useEffect(() => { setData(null); setBusy(false); setError("");
    setEditorConsent(false); setEditorOpened(false); setNativeTarget(null);
    setSourceReadiness(null);
    editorInFlight.current = false; },
    [live, backendBridge.profileGeneration]);
  async function inspect() {
    if (!live || busy) return;
    const owner = backendBridge.currentProfileOwner();
    setData(null); setError(""); setBusy(true); setEditorOpened(false);
    setEditorConsent(false); setNativeTarget(null); setSourceReadiness(null);
    try {
      const result = await backendBridge.invokeProfileScoped(
        "feature018_attack_prepare", { mode: "inventory" }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.observation !== "native_ordinary_monsters_and_owned_squads" ||
          result?.requestDispatched !== false ||
          result?.staffingPreparationPerformed !== false ||
          !Array.isArray(result?.candidates) || result?.squads == null ||
          typeof result.squads !== "object" ||
          ![1, 2, 3, 4].every((slot) => result.squads[String(slot)] &&
            typeof result.squads[String(slot)].unlocked === "boolean"))
        throw Error("Native attack preparation did not prove a read-only owner snapshot");
      setData(result);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  async function inspectSelectedTarget() {
    if (!live || busy || !data?.currentSelector || editorInFlight.current) return;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setError(""); setNativeTarget(null);
    try {
      const result = await backendBridge.invokeProfileScoped("feature018_attack_prepare",
        { mode: "selectorTarget" }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.observation !== "native_typed_formation_selector_readonly" ||
          result.requestDispatched !== false ||
          result.safeToDispatchFromPhysicalCursor !== false ||
          result.target?.requestDispatched !== false ||
          result.target.currentCursorVerified !== false ||
          result.target.physicalCursorTargetVerified !== false ||
          !/^[1-9]\d{0,19}$/.test(String(result.target.uuid || "")))
        throw Error("Original game selector target could not be read independently");
      setNativeTarget(result.target);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  async function inspectAttackReadiness() {
    if (!live || busy || !data || editorInFlight.current) return;
    const owner = backendBridge.currentProfileOwner();
    const selectedSlot = editorSlot;
    setBusy(true); setError(""); setSourceReadiness(null);
    try {
      const result = await backendBridge.invokeProfileScoped(
        "feature018_attack_prepare",
        { mode: "attackReadiness", slot: selectedSlot }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.observation !== "native_attack_readonly_prerequisite_inventory" ||
          result.slot !== selectedSlot || !Array.isArray(result.outstandingReasons) ||
          result.outstandingReasons.some((reason) => typeof reason !== "string" ||
            !/^[a-z_]+$/.test(reason)) ||
          result.sourcePrerequisitesObserved !== (result.outstandingReasons.length === 0) ||
          result.physicalCursorTargetVerified !== false ||
          result.safeToDispatchFromPhysicalCursor !== false ||
          result.serverAcknowledgementCorrelated !== false ||
          result.serverRequestTokenEchoVerified !== false ||
          result.nativeMarchPostStateVerified !== false ||
          result.attackDispatched !== false ||
          result.requestDispatched !== false ||
          result.staffingMutationPerformed !== false)
        throw Error("Source-native attack readiness cannot establish a safe no-send observation");
      setSourceReadiness(result);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  const editable = data?.currentSelector &&
    data?.squads?.[String(editorSlot)]?.unlocked === true &&
    data.squads[String(editorSlot)].canPrepareWithUnassignedSoldiers === true &&
    !data.squads[String(editorSlot)].marchUuid &&
    /^[1-9]\d{0,19}$/.test(String(data.squads[String(editorSlot)].formationUuid || ""));
  async function openEditor() {
    if (!live || busy || editorInFlight.current || !editable || !editorConsent || editorOpened) return;
    editorInFlight.current = true;
    setBusy(true); setError(""); setEditorConsent(false);
    const owner = backendBridge.currentProfileOwner();
    const expectedUuid = String(data.squads[String(editorSlot)].formationUuid);
    try {
      const opened = await backendBridge.invokeProfileScoped(
        "feature018_attack_prepare", { mode: "staffingEditor", slot: editorSlot,
          editorOptIn: true }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (opened?.observation !== "native_manual_formation_editor_open" ||
          opened.slot !== editorSlot || opened.formationUuid !== expectedUuid ||
          opened.operatorMustSaveInOriginalGame !== true ||
          opened.formationSaveSent !== false || opened.attackDispatched !== false ||
          opened.requestDispatched !== false || opened.staffingPreparationPerformed !== false)
        throw Error("Native formation editor did not return an exact no-attack receipt");
      setEditorOpened(true);
      setData(null);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError(cause.code || cause.message);
      // A timed-out open may have succeeded. Re-observe the game; do not resend.
      setData(null);
    } finally {
      editorInFlight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-attack-readiness>
    <h3>{words.title}</h3><p>{words.description}</p>
    <div className="mini-game-actions"><button type="button" disabled={busy || !live}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {data ? <div role="status">
      <small>{words.total}: {data.unassignedSoldiers?.observed === true ?
        data.unassignedSoldiers.total : "—"} · {words.target}: {data.candidates.length}</small>
      {[1, 2, 3, 4].map((slot) => {
        const squad = data.squads[String(slot)];
        return <div key={slot}><small>{words.squad} {slot}: {squad.unlocked ?
          `${words.soldier}: ${squad.soldierCount ?? "—"} · ${words.hero}: ${squad.heroCount ?? "—"} · ${words.capacity}: ${squad.formationCapacity ?? "—"}`
          : words.locked}</small>
          {squad.canPrepareWithUnassignedSoldiers === true ? <small>{words.canStaff}</small> : null}
        </div>;
      })}
      {!Object.values(data.squads).some((row) => row.staffedForAttack === true) ?
        <small>{words.missing}</small> : null}
      <small>{words.guard}</small>
      {data.currentSelector ? <div>
        <div className="mini-game-actions"><button type="button" disabled={busy || editorOpened}
          onClick={() => void inspectAttackReadiness()}>{words.inspectReadiness}</button></div>
        {sourceReadiness ? <div role="status">
          <small>{words.sourceReadiness}: {sourceReadiness.sourcePrerequisitesObserved ? "✓" : "—"}</small>
          {sourceReadiness.outstandingReasons.length > 0 ?
            <small>{words.remainingReasons}: {sourceReadiness.outstandingReasons.join(", ")}</small> : null}
          <small>{words.cursorUnverified}</small>
        </div> : null}
        <div className="mini-game-actions"><button type="button" disabled={busy || editorOpened}
          onClick={() => void inspectSelectedTarget()}>{words.inspectSelector}</button></div>
        {nativeTarget ? <div role="status">
          <small>{words.selectedTarget}: {String(nativeTarget.uuid)} · {String(nativeTarget.point ?? "—")}</small>
          <small>{words.selectorGuard}</small>
        </div> : null}
        <label>{words.squad} <select value={editorSlot} disabled={busy || editorOpened}
          onChange={(event) => { setEditorSlot(Number(event.target.value));
            setEditorConsent(false); setSourceReadiness(null); }}>
          {[1, 2, 3, 4].map((slot) => <option key={slot} value={slot}>{slot}</option>)}
        </select></label>
        <label><input type="checkbox" checked={editorConsent} disabled={!editable || busy || editorOpened}
          onChange={(event) => setEditorConsent(event.target.checked)} /> {words.editorConsent}</label>
        <div className="mini-game-actions"><button type="button" disabled={!editable || !editorConsent || busy || editorOpened}
          onClick={() => void openEditor()}>{words.editor}</button></div>
      </div> : <small>{words.needSelector}</small>}
    </div> : null}
    {editorOpened ? <span className="hotkey-state enabled" role="status">{words.editorOpened}</span> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
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
        // Current v26 ItemUse serializes an opaque item-stack UUID via
        // PutUtfString; only its *template itemId* must be numeric. This
        // exact native contract was proven on the approved 2288 account.
        if (!/^[1-9]\d{0,19}$/.test(String(item?.itemId || "")) ||
            !/^(?!0$)[A-Za-z0-9_-]{1,128}$/.test(String(item?.uuid || "")) ||
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

// A purchase only provisions a game-owned shield in inventory. An ItemUse is
// deliberately separate, with its own native cooldown/status/inventory proof.
// Never infer ownership or affordability from the browser, and never retry a
// purchase or consumption whose network outcome is uncertain.
function ShieldNativeActionPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "保护罩原生购买及使用", hint: "仅当前账号原生商店、库存和保护罩状态；购买不会自动使用。确认后只发送一次。",
    duration: "时长", inspect: "读取游戏价格与库存", balance: "当前游戏金币",
    price: "原生价格", owned: "拥有数量", missing: "还需金币",
    buyConsent: "确认花费上方显示的游戏金币，购买一件此时长保护罩，不使用真钱",
    useConsent: "确认消耗一件已核实拥有的保护罩，不花费金币",
    buy: "购买一件至库存", use: "使用一件保护罩", check: "只读核对前次请求",
    awaiting: "请求可能已发送；等待原生库存和状态核对，不得重复发送",
    purchased: "游戏库存+1及金币扣费确认；请重新读取库存后再使用",
    shopReply: "观察到一次原生商店回复，但协议没有请求 nonce，无法唯一证明这次购买的服务器归属。",
    shopStackReply: "已核对原生商店回复的准确道具 UUID、数量与库存及金币变化；协议仍无请求 nonce。",
    shopCache: "只核实游戏本地库存和金币变化；没有唯一匹配的服务器回复。请在游戏中确认。",
    shopAmbiguous: "原生商店回复观察不确定；禁止重复发送。请核对游戏后手动处理。",
    itemReply: "观察到一次原生道具使用回复，但没有请求 nonce；服务器归属不能唯一确认。",
    itemCache: "仅核实原生库存减少及保护罩生效；没有唯一匹配的道具使用服务器回复。",
    itemAmbiguous: "道具使用原生回复归属不明，请勿再次消耗。",
    used: "道具减少及 MAIN_SHIELD 生效均已确认",
    unavailable: "当前没有可用库存或金币。此操作不会自动赚取或购买金币。",
    daily: "当前任务可领取", goldTasks: "奖励类型含金币的任务", signIn: "可领取签到活动",
    claimHint: "以上仅为游戏本地只读资格。金币数量、实际领取及服务器入账均未核实；请使用游戏原生领取界面。",
    error: "原生回复尚未获得所需确认；请在游戏中检查，不要重试",
  } : {
    title: "Native shield purchase and use",
    hint: "Authenticated game shop, inventory and shield effect only. Buying does not auto-use an item. Each confirmed action sends once.",
    duration: "Duration", inspect: "Inspect native price and inventory",
    balance: "Current in-game gold", price: "Native price", owned: "Owned count",
    missing: "Additional gold needed",
    buyConsent: "I approve spending the displayed amount of in-game gold to buy exactly one shield. No real-money purchase.",
    useConsent: "I approve consuming exactly one verified owned shield, with no gold spending.",
    buy: "Buy one owned shield", use: "Use one owned shield", check: "Read previous action status",
    awaiting: "The request may have been sent. Await native inventory/effect confirmation; do not send again.",
    purchased: "Native item count +1 and gold debit confirmed; inspect inventory again before use.",
    shopReply: "One matching original shop reply was observed. The protocol has no request nonce, so server causality is unproven.",
    shopStackReply: "The original shop reply's exact stack UUID/count, inventory and gold changes match, but there is still no request nonce.",
    shopCache: "Only cached game inventory/gold changes verified. No uniquely correlated server reply; inspect in the game.",
    shopAmbiguous: "Original shop reply observer became ambiguous. No retry; reconcile in the game.",
    itemReply: "One matching original ItemUse reply was observed, but the protocol has no request nonce.",
    itemCache: "Native owned-item debit and shield effect were verified, but no uniquely matched ItemUse reply exists.",
    itemAmbiguous: "The original ItemUse reply observer is ambiguous. Do not consume another item.",
    used: "Owned item debit and MAIN_SHIELD effect both confirmed.",
    unavailable: "No eligible inventory or in-game gold. This does not earn or purchase gold automatically.",
    daily: "Claimable daily tasks", goldTasks: "Tasks tagged with gold reward type",
    signIn: "Claimable sign-in events",
    claimHint: "Only read-only cached game eligibility. Reward amount, actual claim and server credit remain unverified. Claim through the game's own UI.",
    error: "Game receipt is unconfirmed. Check the game before any further action.",
  };
  const [duration, setDuration] = useState(8);
  const [shop, setShop] = useState(null);
  const [owned, setOwned] = useState(false);
  const [consent, setConsent] = useState("");
  const [pending, setPending] = useState(null);
  const [status, setStatus] = useState("");
  const [purchaseProof, setPurchaseProof] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const nativeInFlight = useRef(false);
  const nativePending = useRef(null);
  const live = online && !previewEnabled;
  useEffect(() => {
    nativeInFlight.current = false;
    nativePending.current = null;
    setShop(null); setOwned(false); setConsent(""); setPending(null);
    setStatus(""); setPurchaseProof(""); setError(""); setBusy(false);
  }, [duration, live, backendBridge.profileGeneration]);

  async function inspect() {
    if (!live || busy || pending || nativeInFlight.current || nativePending.current) return;
    nativeInFlight.current = true;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setError(""); setStatus(""); setPurchaseProof("");
    setShop(null); setOwned(false); setConsent("");
    try {
      const [price, inventory] = await Promise.all([
        backendBridge.invokeProfileScoped("feature018_shield_shop",
          { mode: "inspect", slot: duration }, 10000),
        backendBridge.invokeProfileScoped("feature018_shield_inventory_get", {}, 10000),
      ]);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (price?.observation !== "native_shield_shop_inventory" ||
          price.readOnly !== true || price.requestDispatched !== false ||
          price.durationHours !== duration || !/^[1-9]\d{0,19}$/.test(String(price.itemId || "")) ||
          !Number.isSafeInteger(price.nativePrice) || price.nativePrice <= 0 ||
          !Number.isSafeInteger(price.gold) || price.gold < 0 ||
          !Number.isSafeInteger(price.count) || price.count < 0 ||
          price.additionalGoldNeeded !== Math.max(0, price.nativePrice - price.gold) ||
          price.canAffordNativePrice !== (price.gold >= price.nativePrice) ||
          inventory?.observation !== "native_MAIN_SHIELD_StatusTab_owned_item_duration_join" ||
          inventory.readOnly !== true || inventory.requestDispatched !== false ||
          inventory.durationMappingVerified !== true || !Array.isArray(inventory.items))
        throw Error("Exact game shield shop/owned duration evidence unavailable");
      const sameDuration = inventory.items.filter((item) =>
        item.durationHours === duration && String(item.itemId) === String(price.itemId) &&
        item.uniqueDuration === true && item.count > 0 &&
        /^(?!0$)[A-Za-z0-9_-]{1,128}$/.test(String(item.uuid || "")));
      // Native source can have ambiguous stacks. Never choose a stack or
      // authorize ItemUse without one unique identity for this duration.
      const nativeOwned = price.count > 0 && sameDuration.length === 1 &&
        sameDuration[0].count === price.count;
      setShop(price);
      setOwned(nativeOwned);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      nativeInFlight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  async function act(kind) {
    if (!live || busy || pending || nativeInFlight.current || nativePending.current ||
        !shop || consent !== kind ||
        (kind === "buy" ? shop.count !== 0 || !shop.canAffordNativePrice : !owned)) return;
    const owner = backendBridge.currentProfileOwner();
    const baseline = { kind, duration, itemId: String(shop.itemId),
      count: shop.count, gold: shop.gold, price: shop.nativePrice };
    // Tombstone before any mutating mailbox call. A timeout can follow a send.
    // React state updates are batched. The synchronous refs stop two browser
    // click events in the same task from launching two native item sends.
    nativePending.current = baseline;
    nativeInFlight.current = true;
    setPending(baseline); setBusy(true); setConsent(""); setError("");
    setStatus("pending"); setPurchaseProof("");
    try {
      const result = await backendBridge.invokeProfileScoped(
        kind === "buy" ? "feature018_shield_shop" : "feature018_hotkey_action",
        kind === "buy"
          ? { mode: "buy", slot: duration, gemOptIn: true }
          : { action: "shieldUse", slot: duration }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (result?.slot !== duration || (kind === "buy" &&
          (result.itemId !== baseline.itemId ||
            result.requestDispatched !== true ||
            result.awaitingGameConfirmation !== true)) ||
          (kind === "use" &&
            (result.requestDispatched !== true ||
              result.awaitingGameConfirmation !== true)))
        throw Error("Native shield sender did not produce an attributable submission");
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError((cause.code || cause.message) + " — " + words.error);
    } finally {
      nativeInFlight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  async function check() {
    if (!live || busy || !pending || nativeInFlight.current ||
        nativePending.current !== pending) return;
    nativeInFlight.current = true;
    const owner = backendBridge.currentProfileOwner();
    const baseline = pending;
    setBusy(true); setError("");
    try {
      const result = await backendBridge.invokeProfileScoped(
        baseline.kind === "buy" ? "feature018_shield_shop" : "feature018_hotkey_action",
        baseline.kind === "buy"
          ? { mode: "status", slot: baseline.duration }
          : { action: "shieldUse", slot: baseline.duration, queryOnly: true }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      const bought = baseline.kind === "buy" &&
        result?.observation === "native_shield_purchase_inventory_and_gold" &&
        result?.itemId === baseline.itemId &&
        result.durationHours === baseline.duration &&
        result.spentGems === baseline.price &&
        result.beforeGold === baseline.gold &&
        result.beforeCount === baseline.count &&
        result.count === baseline.count + 1 &&
        result.gold === baseline.gold - baseline.price &&
        result.purchasedCount === 1 &&
        result.nativeReplyCausalityConfirmed === false &&
        (result.purchaseProofKind === "matching_original_reply_owned_stack_and_cached_poststate_no_nonce"
          ? result.nativeShopReplyObserved === true &&
            result.nativeShopReplyMatched === true && result.nativeShopReplyCount === 1 &&
            result.nativeShopReplyStackMatched === true
          : result.purchaseProofKind === "matching_original_reply_and_cached_poststate_no_nonce"
          ? result.nativeShopReplyObserved === true &&
            result.nativeShopReplyMatched === true && result.nativeShopReplyCount === 1 &&
            result.nativeShopReplyStackMatched !== true
          : result.purchaseProofKind === "native_cached_inventory_and_gold_only" &&
            result.nativeShopReplyMatched !== true);
      const used = baseline.kind === "use" &&
        result?.observation === "native_owned_shield_consumption_and_MAIN_SHIELD" &&
        result.slot === baseline.duration && result.itemId === baseline.itemId &&
        result.consumedCount === 1 &&
        result.durationMilliseconds === baseline.duration * 3600000 &&
        result.remainingMilliseconds > 0 &&
        result.remainingMilliseconds <= result.durationMilliseconds + 15000 &&
        result.nativeReplyCausalityConfirmed === false &&
        (result.itemUseProofKind === "matching_original_itemuse_reply_and_shield_poststate_no_nonce"
          ? result.nativeItemUseReplyObserved === true &&
            result.nativeItemUseReplyMatched === true && result.nativeItemUseReplyCount === 1
          : result.itemUseProofKind === "native_cached_shield_inventory_and_MAIN_SHIELD_only" &&
            result.nativeItemUseReplyMatched !== true);
      if ((bought || used) && result.nativeReplyAmbiguous === true) {
        // Exact cache differences cannot overcome an interrupted/foreign
        // native handler. Retain the one-shot browser tombstone.
        setStatus(bought ? "shopAmbiguous" : "itemAmbiguous");
      } else if (bought || used) {
        nativePending.current = null;
        setPending(null); setShop(null); setOwned(false);
        setStatus(bought ? "purchased" : "used");
        setPurchaseProof(bought ? result.purchaseProofKind : result.itemUseProofKind);
      } else if (result?.awaitingGameConfirmation === true &&
          result?.requestDispatched === false &&
          (baseline.kind !== "buy" || result.itemId === baseline.itemId)) {
        setStatus("pending");
      } else {
        throw Error("Native receipt has no correlated inventory and effect transition");
      }
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError((cause.code || cause.message) + " — " + words.error);
    } finally {
      nativeInFlight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-shield-shop>
    <h3>{words.title}</h3><p>{words.hint}</p>
    <label>{words.duration} <select value={duration} disabled={busy || !!pending}
      onChange={(event) => setDuration(Number(event.target.value))}>
      {[8, 12, 24].map((hours) => <option key={hours} value={hours}>{hours} h</option>)}
    </select></label>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy || !!pending}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {shop ? <div role="status">
      <small>{words.balance}: {shop.gold} · {words.price}: {shop.nativePrice} · {words.owned}: {shop.count}</small>
      {shop.additionalGoldNeeded > 0 ? <small>{words.missing}: {shop.additionalGoldNeeded}</small> : null}
      {shop.nativeGoldClaimRoutes?.readOnly === true &&
       shop.nativeGoldClaimRoutes?.requestDispatched === false ? <div>
        {shop.nativeGoldClaimRoutes.availability === "native_observed" ? <small>
          {words.daily}: {shop.nativeGoldClaimRoutes.claimableDailyTaskCount ?? "—"} ·
          {words.goldTasks}: {shop.nativeGoldClaimRoutes.goldTypeMatchedDailyTaskCount ?? "—"}
        </small> : null}
        {shop.nativeGoldClaimRoutes.signIn?.availability === "native_cached" ? <small>
          {words.signIn}: {shop.nativeGoldClaimRoutes.signIn.claimableEventCount ?? "—"}
        </small> : null}
        <small>{words.claimHint}</small>
      </div> : null}
      {shop.count === 0 && shop.canAffordNativePrice ? <label>
        <input type="checkbox" checked={consent === "buy"} disabled={busy || !live}
          onChange={(event) => setConsent(event.target.checked ? "buy" : "")} /> {words.buyConsent}
      </label> : null}
      {owned ? <label><input type="checkbox" checked={consent === "use"}
        disabled={busy || !live}
        onChange={(event) => setConsent(event.target.checked ? "use" : "")} /> {words.useConsent}</label> : null}
      <div className="mini-game-actions">
        {shop.count === 0 && shop.canAffordNativePrice ? <button type="button" className="primary"
          disabled={busy || !live || consent !== "buy"} onClick={() => void act("buy")}>{words.buy}</button> : null}
        {owned ? <button type="button" className="primary" disabled={busy || !live || consent !== "use"}
          onClick={() => void act("use")}>{words.use}</button> : null}
      </div>
      {!(owned || (shop.count === 0 && shop.canAffordNativePrice)) ? <small>{words.unavailable}</small> : null}
    </div> : null}
    {pending ? <div role="status"><small>{words.awaiting}</small><div className="mini-game-actions">
      <button type="button" disabled={!live || busy} onClick={() => void check()}>{words.check}</button>
    </div></div> : null}
    {status === "purchased" || status === "used" ?
      <span role="status" className="hotkey-state enabled">
        {status === "purchased" ? words.purchased : words.used}</span> : null}
    {status === "purchased" && purchaseProof ? <small role="status">
      {purchaseProof === "matching_original_reply_owned_stack_and_cached_poststate_no_nonce"
        ? words.shopStackReply : purchaseProof === "matching_original_reply_and_cached_poststate_no_nonce"
          ? words.shopReply : words.shopCache}
    </small> : null}
    {status === "used" && purchaseProof ? <small role="status">
      {purchaseProof === "matching_original_itemuse_reply_and_shield_poststate_no_nonce"
        ? words.itemReply : words.itemCache}
    </small> : null}
    {status === "shopAmbiguous" ? <small role="alert">{words.shopAmbiguous}</small> : null}
    {status === "itemAmbiguous" ? <small role="alert">{words.itemAmbiguous}</small> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
}

// The v26 owner-persisted preset bridge has a sequential install/uninstall
// transaction. User consent covers all its steps; each native queryOnly call
// verifies the prior step's exact hero/slot before advancing. The browser
// never chooses an equipment UUID or supplies preset contents.
function EquipmentNativePreflightPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const words = chinese ? {
    title: "装备预设原生检查", description: "检查当前英雄装备；确认后可应用已保存的原生预设。",
    slot: "预设编号", inspect: "检查当前游戏装备", missing: "没有可用的游戏检查结果。",
    ready: "已核对当前装备；尚未执行预设应用。", unsupported: "每步必须等待游戏装备槽位更新确认。",
    changed: "需要变更的位置", worn: "由其他英雄装备", uninstall: "需要卸下",
    consent: "确认对当前账号应用此已保存预设（逐件安装和卸下）", apply: "应用已保存预设",
    advance: "验证并继续下一步", pending: "正在等待游戏确认；不要重新提交预设",
    success: "已核对所有英雄装备槽位，预设应用完成", uncertain: "状态未确认；请检查游戏，勿重复应用",
  } : {
    title: "Native equipment preset", description: "Inspect current hero gear, then explicitly apply a saved preset through the authenticated game.",
    slot: "Preset number", inspect: "Inspect current game equipment",
    missing: "No verified native equipment result.",
    ready: "Current equipment inspected. The preset has not been applied.",
    unsupported: "Each step requires authoritative hero-slot state confirmation.",
    changed: "Slots to change", worn: "Worn by other heroes", uninstall: "Slots needing removal",
    consent: "I approve applying all gear changes in this saved preset to the current account.",
    apply: "Apply saved preset", advance: "Verify and advance next step",
    pending: "Waiting for native equipment confirmation; do not submit again",
    success: "Applied and verified all hero equipment slots", uncertain: "Unconfirmed. Check the game; do not submit again.",
  };
  const [slot, setSlot] = useState(1);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [consent, setConsent] = useState(false);
  const [pending, setPending] = useState(null);
  const [confirmed, setConfirmed] = useState(false);
  const live = online && !previewEnabled;
  useEffect(() => {
    setResult(null); setError(""); setBusy(false); setConsent(false);
    setPending(null); setConfirmed(false);
  }, [slot, live, backendBridge.profileGeneration]);
  async function inspect() {
    if (!live || busy || pending) return;
    const owner = backendBridge.currentProfileOwner();
    setResult(null); setError(""); setBusy(true); setConsent(false); setConfirmed(false);
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
  function recordApplyReceipt(receipt, expectedId) {
    if (receipt?.slot !== slot || receipt?.presetId !== expectedId ||
        !Number.isInteger(receipt?.completedSteps) ||
        !Number.isInteger(receipt?.plannedSteps) ||
        receipt.completedSteps < 0 || receipt.completedSteps > receipt.plannedSteps)
      throw new Error("Native equipment transaction identity or progress is unverified");
    if (receipt.observation === "native_HeroEquip_preset_slots_applied" &&
        receipt.equipmentApplied === true &&
        receipt.completedSteps === receipt.plannedSteps &&
        Array.isArray(receipt.slots) && receipt.slots.length > 0 &&
        receipt.slots.every((entry) => typeof entry.currentEquipUuid === "string" &&
          entry.currentEquipUuid === entry.targetEquipUuid)) {
      setConfirmed(true); setPending(null); return;
    }
    if (receipt.observation === "native_HeroEquip_preset_pending" &&
        receipt.awaitingGameConfirmation === true &&
        receipt.equipmentApplied === false) {
      setPending({ presetId: expectedId, completed: receipt.completedSteps, total: receipt.plannedSteps });
      return;
    }
    throw new Error("Native equipment result has no confirmed poststate");
  }
  async function advancePreset(initial) {
    if (!live || busy || (initial ? (!result || !consent || pending || confirmed ||
        result.changedSlots <= 0) : !pending)) return;
    const owner = backendBridge.currentProfileOwner();
    const expectedId = initial ? result.presetId : pending.presetId;
    setBusy(true); setError(""); setConsent(false);
    // A native timeout can follow one dispatched HeroEquipInstall. Latch the
    // chosen transaction before the first mutating call; do not resubmit it.
    if (initial) setPending({ presetId: expectedId, completed: 0, total: result.changedSlots });
    try {
      const receipt = await backendBridge.invokeProfileScoped(
        "feature018_hotkey_action", { action: "equipment", slot,
          ...(initial ? {} : { queryOnly: true }) }, 10000);
      if (backendBridge.isCurrentProfileOwner(owner)) recordApplyReceipt(receipt, expectedId);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError(`${cause.code || cause.message} — ${words.uncertain}`);
    } finally {
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-equipment-preflight>
    <h3>{words.title}</h3><p>{words.description}</p>
    <label>{words.slot} <select aria-label={words.slot} value={slot} disabled={busy || !!pending}
      onChange={(event) => setSlot(Number(event.target.value))}>
      {[1, 2, 3, 4].map((value) => <option key={value} value={value}>{value}</option>)}
    </select></label>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy || !!pending}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {result ? <div role="status"><span className="hotkey-state">{words.ready}</span>
      <small>{words.changed}: {result.changedSlots} · {words.worn}: {result.wornByOtherHero ?? 0} · {words.uninstall}: {result.uninstallSlots ?? 0}</small>
      <small>{words.unsupported}</small>
    </div> : null}
    {result && !pending && !confirmed && result.changedSlots > 0 ? <>
      <label><input type="checkbox" checked={consent} disabled={busy || !live}
        onChange={(event) => setConsent(event.target.checked)} /> {words.consent}</label>
      <div className="mini-game-actions"><button type="button" className="primary" disabled={!consent || busy || !live}
        onClick={() => void advancePreset(true)}>{words.apply}</button></div>
    </> : null}
    {pending ? <div role="status"><small>{words.pending} ({pending.completed}/{pending.total})</small>
      <div className="mini-game-actions"><button type="button" disabled={busy || !live}
        onClick={() => void advancePreset(false)}>{words.advance}</button></div></div> : null}
    {confirmed ? <span className="hotkey-state enabled" role="status">{words.success}</span> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
}

// F9/F10 item and destination eligibility are verified by the original
// installed game, not browser-supplied bag IDs or world coordinates. This
// control only reads that provenance; source-home F10 currently routes via
// MoveCrossServer and must not be represented as a successful relocation.
function RelocationNativeReadinessPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const chinese = language?.startsWith("zh");
  const [action, setAction] = useState("randomRelocate");
  const [receipt, setReceipt] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const inflight = useRef(false);
  const live = online && !previewEnabled;
  useEffect(() => {
    inflight.current = false;
    setReceipt(null); setError(""); setBusy(false);
  }, [live, backendBridge.profileGeneration]);
  async function inspect() {
    if (!live || busy || inflight.current) return;
    inflight.current = true;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setReceipt(null); setError("");
    try {
      const native = await backendBridge.invokeProfileScoped(
        "feature018_relocation_preflight", { action }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (native?.observation !== "original_v26_authenticated_relocation_readiness" ||
          native.action !== action || native.readOnly !== true ||
          native.requestDispatched !== false ||
          native.originalPacketSend !== false ||
          native.serverReplyObserved !== false ||
          native.relocationConfirmed !== false ||
          native.destinationVerified !== false ||
          native.serverAckAttributable !== false ||
          native.sourceServerId !== 2288 ||
          native.selfServerId !== 2288 || native.currentServerId !== 2288 ||
          String(native.ownerUid) !== "2553945035002288" ||
          (action === "allianceRelocate" &&
            (native.actionable !== false ||
             native.homeDirectRelocationEnabled !== false)))
        throw Error("Current original F9/F10 source did not provide a read-only verified owner receipt");
      setReceipt(native);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner))
        setError(cause.code || cause.message);
    } finally {
      inflight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-relocation-readiness>
    <h3>{chinese ? "F9/F10 原生迁城条件" : "F9/F10 native relocation prerequisites"}</h3>
    <p>{chinese ? "读取账号、原生背包道具、游戏提醒和迁城目标来源；不会使用道具或发起迁城。" :
      "Inspect account, original bag item, game warnings and destination source. Never spends an item or relocates."}</p>
    <label>{chinese ? "原生快捷键" : "Original shortcut"}
      <select value={action} disabled={busy}
        onChange={(event) => { setAction(event.target.value); setReceipt(null); setError(""); }}>
        <option value="randomRelocate">F9</option>
        <option value="allianceRelocate">F10</option>
      </select>
    </label>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy}
      onClick={() => void inspect()}>{chinese ? "只读核对原生迁城条件" :
      "Inspect native relocation readiness"}</button></div>
    {receipt ? <div role="status">
      <small>{chinese ? "已核实道具" : "Owned native item"}:
        {" "}{receipt.ownerItemPresent === true ? String(receipt.itemId) : "—"}</small>
      <small>{chinese ? "原始游戏路径" : "Original game route"}:
        {" "}{receipt.originalNativeRoute || "—"}</small>
      {receipt.currentBlocker ? <small>{chinese ? "阻碍" : "Blocker"}:
        {" "}{receipt.currentBlocker}</small> : null}
      {receipt.chooserRequestEligible === true ?
        <small>{chinese ? "原生集结点选择器可能可用，但尚未打开、选择或迁城。" :
          "Original rally chooser may be eligible. It has not been opened, selected or dispatched."}</small> : null}
      <small>{chinese ? "只完成来源核对；目标、服务器回复和迁城后位置尚未证明。" :
        "Source prerequisites only. Destination, server acknowledgement and city poststate are unproven."}</small>
    </div> : null}
    {error ? <div className="automation-error" role="alert">{error}</div> : null}
  </article>;
}

// Normal stage and Sunday Frontline G adds five local Parkour units. The original
// per-G operation does not send a battle-score RPC. Inspect the independent
// current battle before allowing one deliberate UI action per owner lifetime.
// A timeout may happen after units were created: NEVER retry that UI intent.
function FrontlineNativeActionPanel({ online, previewEnabled }) {
  const { language } = useI18n();
  const zh = language?.startsWith("zh");
  const words = zh ? {
    title: "前线突破：原生 G 五名士兵",
    description: "开启 G 后，在普通关卡或周日前线的战斗中，每次按 G 增加五名士兵。离开战斗后无效。",
    inspect: "检查正在进行的原生战斗", ready: "原生前线可操作",
    count: "当前士兵（含预备）", event: "活动", hero: "英雄",
    consent: "确认当前这一场战斗只增加五名原生士兵；若等待超时，我会在游戏中核对，不重复发送",
    execute: "原生增援五名士兵（G）", pending: "本次动作已锁定；无法重新派遣，请在游戏中核对。",
    result: "本地战斗士兵总数增加五名（包含预备士兵）；未收到独立服务器计分回复。",
    changed: "前线战斗已变化；本次动作不能按原计划确认",
  } : {
    title: "Frontline: original G adds five units",
    description: "With G enabled, press G in a running normal stage or Sunday challenge to add five soldiers. G does nothing outside a run.",
    inspect: "Inspect active original battle", ready: "Native Frontline ready",
    count: "Current members (including reserves)", event: "Event", hero: "Hero",
    consent: "I approve adding five native units in this battle once. If the outcome times out, I will inspect the game without retrying.",
    execute: "Add five original units (G)", pending: "This intent is locked against replay; inspect the game.",
    result: "Battle total increased by five soldiers, including reserves. No independent server scoring reply.",
    changed: "Original battle changed; this action cannot be confirmed from this snapshot",
  };
  const live = online && !previewEnabled;
  const [ready, setReady] = useState(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [latched, setLatched] = useState(false);
  const [outcome, setOutcome] = useState(null);
  const [error, setError] = useState("");
  const inflight = useRef(false);
  const actionLatch = useRef(false);
  useEffect(() => {
    setReady(null); setConsent(false); setBusy(false); setLatched(false);
    setOutcome(null); setError("");
    inflight.current = false; actionLatch.current = false;
  }, [live, backendBridge.profileGeneration]);
  const validRead = (data) =>
    data?.observation === "native_parkour_battle_readonly" &&
    data?.readOnly === true && data?.requestDispatched === false &&
    data?.ownerUid === "2553945035002288" && data?.sourceServerId === 2288 &&
    data?.observationScope === "native_local_battle" &&
    data?.serverAckObserved === false &&
    (data?.stageMode === "normal" || data?.stageMode === "sunday") &&
    typeof data?.stageRunId === "string" && /^[1-9]\d{0,19}$/.test(data.stageRunId) &&
    Number.isInteger(data?.memberCount) && data.memberCount > 0 &&
    Number.isInteger(data?.activeMemberCount) && data.activeMemberCount >= 0 &&
    Number.isInteger(data?.reserveMemberCount) && data.reserveMemberCount >= 0 &&
    data.memberCount === data.activeMemberCount + data.reserveMemberCount &&
    Number.isInteger(data?.guidAllocator) && data.guidAllocator > 0 &&
    Number.isInteger(data?.heroId) && data.heroId > 0;
  async function inspect() {
    if (!live || inflight.current) return;
    inflight.current = true;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setReady(null); setConsent(false); setError("");
    try {
      const result = await backendBridge.invokeProfileScoped("mini_frontline_status", {}, 8000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (!validRead(result)) throw Error(words.changed);
      setReady(result);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      inflight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  async function reinforce() {
    if (!live || inflight.current || actionLatch.current || busy || !consent ||
        !ready || ready.actionEligible !== true || ready.uncertainPriorAction !== false) return;
    // An error or a timed-out reply may follow some original AddMember calls.
    // Latch synchronously BEFORE invoking the bridge, including across focus
    // changes, server timeouts and later read-only refreshes.
    actionLatch.current = true;
    inflight.current = true;
    const before = ready;
    const owner = backendBridge.currentProfileOwner();
    setBusy(true); setLatched(true); setConsent(false); setOutcome(null); setError("");
    try {
      const action = await backendBridge.invokeProfileScoped(
        "feature018_frontline_action", { frontlineOptIn: true }, 10000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (action?.observation !== "native_ParkourTeam_GetRemainUnitCount" ||
          action?.action !== "frontlineReinforce" || action?.slot !== 5 ||
          action?.frontBreakSundayActId !== before.frontBreakSundayActId ||
          action?.stageMode !== before.stageMode || action?.levelId !== before.levelId ||
          action?.stageRunId !== before.stageRunId ||
          action?.heroId !== before.heroId ||
          action?.beforeCount !== before.memberCount ||
          action?.guidAllocatorBefore !== before.guidAllocator ||
          action?.addedMembers !== 5 ||
          !Number.isInteger(action?.observedCreatedUnits) ||
          action.observedCreatedUnits < 0 || action.observedCreatedUnits > 5 ||
          !Number.isInteger(action?.reservedMembers) ||
          action.reservedMembers < 0 || action.reservedMembers > 5 ||
          action.observedCreatedUnits + action.reservedMembers !== 5 ||
          action?.activeMemberCountBefore !== before.activeMemberCount ||
          action?.reserveMemberCountBefore !== before.reserveMemberCount ||
          action?.activeMemberCountAfter !== before.activeMemberCount + action.observedCreatedUnits ||
          action?.reserveMemberCountAfter !== before.reserveMemberCount + action.reservedMembers ||
          action?.afterCount !== action.activeMemberCountAfter + action.reserveMemberCountAfter ||
          action?.nativeReserveVerified !== true ||
          action?.reserveIdentitySource !== "native_ParkourTeam_overflowUnitCount" ||
          action?.unitIdentitySource !== "native_ParkourTeam_teamUnits_guid" ||
          action?.unitIdentityVerified !== true ||
          action?.afterCount !== before.memberCount + 5 ||
          action?.guidAllocatorAfter !== before.guidAllocator + action.observedCreatedUnits ||
          action?.serverAckObserved !== false ||
          action?.observationScope !== "native_local_battle")
        throw Error(words.changed);
      // This is a SECOND independent read of the same original battle.
      // No second AddMember call occurs on this path.
      const after = await backendBridge.invokeProfileScoped("mini_frontline_status", {}, 8000);
      if (!backendBridge.isCurrentProfileOwner(owner)) return;
      if (!validRead(after) || after.frontBreakSundayActId !== before.frontBreakSundayActId ||
          after.heroId !== before.heroId || after.stageRunId !== before.stageRunId ||
          after.memberCount !== action.afterCount ||
          after.activeMemberCount !== action.activeMemberCountAfter ||
          after.reserveMemberCount !== action.reserveMemberCountAfter ||
          after.guidAllocator !== action.guidAllocatorAfter)
        throw Error(words.changed);
      setOutcome(action); setReady(after);
    } catch (cause) {
      if (backendBridge.isCurrentProfileOwner(owner)) setError(cause.code || cause.message);
    } finally {
      inflight.current = false;
      if (backendBridge.isCurrentProfileOwner(owner)) setBusy(false);
    }
  }
  return <article className="hotkey-card" data-feature018-frontline-native>
    <h3>{words.title}</h3><p>{words.description}</p>
    <div className="mini-game-actions"><button type="button" disabled={!live || busy}
      onClick={() => void inspect()}>{words.inspect}</button></div>
    {ready ? <div role="status"><span className="hotkey-state">{words.ready}</span>
      <small>{words.event}: {ready.levelId ?? ready.frontBreakSundayActId ?? ready.stageMode} · {words.hero}: {ready.heroId} · {words.count}: {ready.memberCount}</small>
    </div> : null}
    {ready?.actionEligible === true && !latched ? <>
      <label><input type="checkbox" checked={consent} disabled={!live || busy}
        onChange={(event) => setConsent(event.target.checked)} /> {words.consent}</label>
      <div className="mini-game-actions"><button className="primary" type="button"
        disabled={!live || busy || !consent} onClick={() => void reinforce()}>{words.execute}</button></div>
    </> : null}
    {latched ? <small role="status">{words.pending}</small> : null}
    {outcome ? <div role="status" className="hotkey-state enabled">{words.result}
      {" "}{words.count}: {outcome.beforeCount} → {outcome.afterCount}</div> : null}
    {ready?.uncertainPriorAction ? <small role="alert">{words.pending}</small> : null}
    {error ? <div role="alert" className="automation-error">{error}</div> : null}
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
        {!isMiniGames && hotkeyConfig ? <NativeAttackReadinessPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <OwnedRapidItemPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <ShieldOwnedInventoryPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <ShieldNativeActionPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <EquipmentNativePreflightPanel online={online} previewEnabled={previewEnabled} /> : null}
        {!isMiniGames && hotkeyConfig ? <RelocationNativeReadinessPanel online={online} previewEnabled={previewEnabled} /> : null}
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
        {isMiniGames ? <FrontlineNativeActionPanel online={online} previewEnabled={previewEnabled} /> : null}
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
