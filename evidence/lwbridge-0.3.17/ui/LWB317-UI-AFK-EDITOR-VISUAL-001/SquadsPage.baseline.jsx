import { Activity, useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "./i18n.jsx";
import { RallyJoinSettings } from "./RallyJoinSettings.jsx";
import { applyAfkTarget, makePreviewAfkProfile, normalizeJoinRestrictions, previewAfkLevelOutOfRange, previewAfkProfileValid, previewAfkTargets, refreshGarrisonTargets, resolvePreviewAfkTarget, validJoinRestrictions } from "./previewAfkContracts.js";
import { initialAfkToolbarConfig, previewDrillRuntime, previewGarrisonMemberFixture, previewGarrisonRuntime, previewZombieBusRuntime } from "./previewAfkCloseoutFixtures.js";
import { PreviewConfigError, usePreviewConfig } from "./previewConfigHook.jsx";
import { cloneEquipmentValue, currentEquipmentPresetLabel, currentEquipmentPresetMatches, EQUIPMENT_PRESET_LIMIT, EQUIPMENT_SLOTS, equipmentCatalog, equipmentDirtyPresetIds, equipmentItemCount, equipmentPositionCount, findEquipmentSquad, previewEquipmentFixture, swapEquipmentSquads, swapEquipmentTarget } from "./previewEquipmentContracts.js";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { ToggleRow } from "./sharedPageUI.jsx";
import { motion, AnimatePresence, useReducedMotion, equipmentMotionProps } from "./EquipmentMotion.jsx";

export function SquadsPage({ previewState = "", activeTab, onActiveTabChange }) {
  const { t } = useI18n();
  const equipmentPreview = previewState.startsWith("squads-equipment");
  const previewEnabled = previewState.startsWith("squads-profile") || equipmentPreview;
  const [localTab, setLocalTab] = useState(equipmentPreview ? "equipment" : "afk");
  const tab = activeTab ?? localTab;
  const [visitedTabs, setVisitedTabs] = useState(() => new Set([tab]));
  const [equipmentRefreshCount, setEquipmentRefreshCount] = useState(0);
  const equipmentOnline = equipmentPreview && previewState !== "squads-equipment-offline";
  const selectTab = (nextTab) => {
    setVisitedTabs((current) => {
      if (current.has(nextTab)) return current;
      const next = new Set(current);
      next.add(nextTab);
      return next;
    });
    if (onActiveTabChange) onActiveTabChange(nextTab);
    else setLocalTab(nextTab);
  };
  return (
    <section className="panel squad-panel" data-preview-fixture={previewEnabled ? previewState : undefined} data-equipment-refresh-count={equipmentRefreshCount}>
      <div className="squad-header">
        <h2>{t("squad.title")}</h2>
        {tab === "equipment" ? <button type="button" disabled={!equipmentOnline} title={!equipmentOnline ? t("status.gameDisconnectedDisabled") : undefined} onClick={equipmentOnline ? () => setEquipmentRefreshCount((count) => count + 1) : undefined}>{t("squad.refresh")}</button> : null}
      </div>
      <div className="squad-tabs" role="tablist" aria-label={t("squad.title")}>
        <button type="button" role="tab" className={tab === "afk" ? "active" : ""} aria-selected={tab === "afk"} onClick={() => selectTab("afk")}>{t("squad.tabAfk")}</button>
        <button type="button" role="tab" className={tab === "equipment" ? "active" : ""} aria-selected={tab === "equipment"} onClick={() => selectTab("equipment")}>{t("squad.tabEquipment")}</button>
      </div>
      {visitedTabs.has("afk") ? <Activity mode={tab === "afk" ? "visible" : "hidden"}><AfkContent previewEnabled={previewEnabled} previewState={previewState} /></Activity> : null}
      {visitedTabs.has("equipment") ? <Activity mode={tab === "equipment" ? "visible" : "hidden"}><EquipmentContent previewEnabled={previewEnabled} previewState={previewState} /></Activity> : null}
    </section>
  );
}

function AfkProfileEditor({ enabled, isNew = false, profile, onProfileChange, previewState = "" }) {
  const { t } = useI18n();
  const [targetError, setTargetError] = useState("");
  const targetDiscoveryReady = !["squads-profile-target-loading", "squads-profile-target-failed"].includes(previewState);
  const sourceTargets = !targetDiscoveryReady ? [] : previewState === "squads-profile-target-undiscovered" ? previewAfkTargets.filter((target) => target.key !== profile.targetKey) : previewAfkTargets;
  const availableTargets = sourceTargets.filter((target) => !target.key.startsWith("name:") && target.group !== "drill" && (profile.kind === "join" ? target.rally : !target.joinOnly));
  const selectedTarget = resolvePreviewAfkTarget(sourceTargets, profile);
  const targetMissing = targetDiscoveryReady && !profile.customTarget && !selectedTarget;
  const attackRangeWarning = profile.kind === "farm" && profile.searchable && previewAfkLevelOutOfRange(profile, selectedTarget);
  const patch = (changes) => onProfileChange({ ...profile, ...changes });
  useEffect(() => { setTargetError(""); }, [profile.id]);
  return (
    <section className={`monster-afk-editor ${isNew ? "is-new" : "is-editing"}`} data-preview-fixture="afk-profile-editor">
      <div className="monster-afk-editor-heading"><strong>{t(isNew ? "squad.afkNewProfile" : "squad.afkEditProfile")}</strong><span className={`monster-afk-mode-badge ${profile.kind}`}>{t(profile.kind === "farm" ? "squad.afkActiveAttack" : "squad.afkJoin")}</span></div>
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkBasicSettings")}</strong>
        <div className="monster-afk-basic-grid">
          <label><span>{t("squad.afkProfileName")}</span><input value={profile.name} disabled={!enabled} onChange={(event) => patch({ name: event.target.value })} onBlur={() => { if (previewAfkProfileValid(profile)) patch({ name: profile.name.trim() }); }} onKeyDown={(event) => { if (event.key === "Enter" && !event.nativeEvent.isComposing) event.currentTarget.blur(); }} /></label>
          <label><span>{t("squad.afkTarget")}</span><select value={profile.customTarget ? profile.lastListTargetKey : selectedTarget?.key || profile.targetKey} disabled={!enabled} onChange={(event) => { const target = sourceTargets.find((entry) => entry.key === event.target.value); if (!target) return; setTargetError(""); onProfileChange(applyAfkTarget(profile, target)); }}>{targetMissing ? <option value={profile.targetKey}>{profile.targetNameQuery || profile.name} · {t("squad.afkUndiscovered")} · {t(profile.kind === "join" ? "squad.afkJoin" : profile.action === "rally" ? "squad.afkActionRally" : "squad.afkActionAttack")}</option> : null}{profile.customTarget && !availableTargets.some((target) => target.key === profile.lastListTargetKey) ? <option value={profile.lastListTargetKey}>{t("squad.afkSelectListTarget")}</option> : null}{["normal", "elite", "running", "leader", "ally", "drill", "invader", "other"].filter((group) => availableTargets.some((target) => target.group === group)).map((group) => <optgroup label={t(`squad.afkGroup.${group}`)} key={group}>{availableTargets.filter((target) => target.group === group).map((target) => <option value={target.key} key={target.key}>{target.name || (target.labelKey ? t(target.labelKey) : target.key)} · {t(target.source === "search" ? "squad.afkSearchable" : "squad.afkLocalTarget")} · {t(profile.kind === "join" ? "squad.afkJoin" : target.action === "rally" ? "squad.afkActionRally" : "squad.afkActionAttack")}{target.searchable ? ` · ${target.attackMinLevel != null && target.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: target.attackMinLevel, max: target.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}` : ""}</option>)}</optgroup>)}</select>{previewState === "squads-profile-target-loading" ? <span className="muted">{t("status.checking")}</span> : null}{previewState === "squads-profile-target-failed" ? <span className="status-error">{t("common.actionFailed")}</span> : null}</label>
        </div>
        <div className="monster-afk-custom-target"><label className="monster-afk-check-row"><input type="checkbox" checked={profile.customTarget} disabled={!enabled} onChange={(event) => { if (event.target.checked) { setTargetError(""); patch({ customTarget: true, lastListTargetKey: selectedTarget?.key || profile.targetKey, targetKey: "query:", targetNameQuery: "", monsterType: 0, monsterNameKey: undefined, monsterSpecial: undefined, monsterIds: [], source: "undiscovered", action: profile.kind === "join" ? "rally" : "attack", rally: profile.kind === "join", searchable: false, minLevel: 1, maxLevel: 999 }); return; } const restored = availableTargets.find((target) => target.key === profile.lastListTargetKey); if (!restored) { setTargetError("squad.afkRestoreTargetRequired"); return; } setTargetError(""); onProfileChange(applyAfkTarget(profile, restored)); }} />{t("squad.afkCustomTarget")}</label>{profile.customTarget ? <label className="monster-afk-custom-name"><span>{t("squad.afkCustomName")}</span><input value={profile.targetNameQuery} aria-invalid={!profile.targetNameQuery.trim()} disabled={!enabled} onChange={(event) => patch({ targetKey: `query:${event.target.value.trim()}`, targetNameQuery: event.target.value })} /><span className="muted">{t("squad.afkCustomTargetHint")}</span>{!profile.targetNameQuery.trim() ? <span role="status" className="status-error">{t("squad.afkCustomTargetRequired")}</span> : null}</label> : null}{targetError ? <span role="status" className="status-error">{t(targetError)}</span> : null}</div>
      </div>
      {profile.kind === "join" ? <RallyJoinSettings key={profile.id} value={profile.joinRestrictions || normalizeJoinRestrictions(undefined, 1, true)} onChange={(joinRestrictions) => patch({ joinRestrictions })} disabled={!enabled} previewState={previewState} ToggleRow={ToggleRow} /> : null}
      <div className="monster-afk-config-section">
        <strong>{t("squad.afkExecutionSettings")}</strong>
        <div className="monster-afk-basic-grid monster-afk-execution-grid">
          <div className="monster-afk-squad-field"><span>{t("squad.afkAssignments")}</span><div className="monster-afk-squads" role="group" aria-label={t("squad.afkAssignments")}>{[1,2,3,4].map((number) => <button className="monster-afk-squad-toggle" type="button" aria-label={t("squad.number", { number })} aria-pressed={profile.squadIndexes.includes(number)} disabled={!enabled} key={number} onClick={() => patch({ squadIndexes: profile.squadIndexes.includes(number) ? profile.squadIndexes.filter((entry) => entry !== number) : [...profile.squadIndexes, number].sort((a, b) => a - b) })}>{number}</button>)}</div></div>
          <label title={t("squad.afkExecutionLimitHint")}><span>{t("squad.afkExecutionLimitLabel")}</span><input type="number" min="0" step="1" value={Number.isNaN(profile.executionLimit) ? "" : profile.executionLimit} disabled={!enabled} onChange={(event) => patch({ executionLimit: event.target.valueAsNumber })} /></label>
        </div>
        {profile.kind === "farm" ? <label className="monster-afk-check-row"><input type="checkbox" checked={profile.continuousAttack} disabled={!enabled || profile.rally} onChange={(event) => patch({ continuousAttack: event.target.checked })} />{t("squad.afkContinuousAttack")}{profile.rally ? <span className="muted">{t("squad.afkContinuousAttackUnavailable")}</span> : null}</label> : <label className="monster-afk-check-row"><input type="checkbox" checked={profile.continuousJoin} disabled={!enabled} onChange={(event) => patch({ continuousJoin: event.target.checked })} />{t("squad.afkContinuousJoin")}</label>}
      </div>
      <div className="monster-afk-config-section monster-afk-filter">
        <strong>{t("squad.afkTargetFilters")}</strong>
        {profile.kind === "farm" && profile.searchable ? <p className={ `monster-afk-attackable-range${attackRangeWarning ? " is-invalid" : ""}`}>{selectedTarget?.attackMinLevel != null && selectedTarget?.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: selectedTarget.attackMinLevel, max: selectedTarget.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}{attackRangeWarning ? ` · ${t("squad.afkLevelOutOfRange")}` : ""}</p> : null}
        <label className="monster-afk-check-row"><input type="checkbox" checked={profile.levelFilterEnabled} disabled={!enabled} onChange={(event) => patch({ levelFilterEnabled: event.target.checked, progressiveLevels: event.target.checked && profile.progressiveLevels })} />{t("squad.afkLevelFilter")}</label>
        {profile.levelFilterEnabled ? <div className="monster-afk-filter-content"><div className="monster-afk-number-grid"><label><span>{t("squad.afkMinLevel")}</span><input type="number" min="1" value={Number.isNaN(profile.minLevel) ? "" : profile.minLevel} disabled={!enabled} onChange={(event) => patch({ minLevel: event.target.valueAsNumber })} /></label>{!profile.progressiveLevels ? <label><span>{t("squad.afkMaxLevel")}</span><input type="number" min="1" value={Number.isNaN(profile.maxLevel) ? "" : profile.maxLevel} disabled={!enabled} onChange={(event) => patch({ maxLevel: event.target.valueAsNumber })} /></label> : null}</div>{profile.kind === "farm" && profile.searchable ? <label className="monster-afk-check-row"><input type="checkbox" checked={profile.progressiveLevels} disabled={!enabled} onChange={(event) => patch({ progressiveLevels: event.target.checked })} />{t("squad.afkProgressiveLevels")}</label> : null}</div> : null}
        <label className="monster-afk-check-row"><input type="checkbox" checked={profile.distanceFilterEnabled} disabled={!enabled} onChange={(event) => patch({ distanceFilterEnabled: event.target.checked })} />{t("squad.afkDistanceFilter")}</label>
        {profile.distanceFilterEnabled ? <div className="monster-afk-filter-content"><label><span>{t("squad.maxDistance")}</span><input type="number" min="1" value={Number.isNaN(profile.maxDistance) ? "" : profile.maxDistance} disabled={!enabled} onChange={(event) => patch({ maxDistance: event.target.valueAsNumber })} /></label></div> : null}
      </div>


    </section>
  );
}

function AllianceDrillPreviewSettings({ disabled, value, onChange, previewState = "" }) {
  const { t } = useI18n();
  const [draggedSquad, setDraggedSquad] = useState(null);
  const [dragOverSquad, setDragOverSquad] = useState(null);
  const runtime = previewDrillRuntime(previewState);
  const patch = (changes) => onChange({ ...value, ...changes });
  const orderedSquads = [...value.squadIndexes, ...[1,2,3,4].filter((number) => !value.squadIndexes.includes(number))];
  const reorderSquad = (target) => {
    if (draggedSquad == null || draggedSquad === target) return;
    const next = [...value.squadIndexes];
    const sourceIndex = next.indexOf(draggedSquad);
    const targetIndex = next.indexOf(target);
    if (sourceIndex < 0 || targetIndex < 0) return;
    const [moved] = next.splice(sourceIndex, 1);
    next.splice(targetIndex, 0, moved);
    patch({ squadIndexes: next });
  };
  return <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("squad.afkAllianceDrill")}</strong><span>{t("squad.afkAllianceDrillDescription")}</span></div><div className="monster-afk-drill-inline"><span className="muted">{t("squad.afkAllianceDrillOrder", { order: value.squadIndexes.join(" → ") || "-" })}</span><ToggleRow label={t("squad.afkAllianceDrillActive")} checked={value.activeRally} disabled={disabled} onChange={(activeRally) => patch({ activeRally })} /></div><div className="automation-compact-choice-group automation-squad-priority" role="group" aria-label={t("squad.afkAllianceDrillOrder", { order: value.squadIndexes.join(" → ") || "-" })}>{orderedSquads.map((number) => { const selected = value.squadIndexes.includes(number); return <div className={`automation-squad-priority-item${selected ? " selected" : ""}${draggedSquad === number ? " dragging" : ""}${dragOverSquad === number ? " drag-over" : ""}`} draggable={selected && !disabled} onDragStart={() => selected && setDraggedSquad(number)} onDragOver={(event) => { if (selected && draggedSquad != null && draggedSquad !== number) { event.preventDefault(); setDragOverSquad(number); } }} onDrop={(event) => { event.preventDefault(); reorderSquad(number); setDraggedSquad(null); setDragOverSquad(null); }} onDragEnd={() => { setDraggedSquad(null); setDragOverSquad(null); }} key={number}><label><input type="checkbox" checked={selected} disabled={disabled} onChange={() => { const squadIndexes = selected ? value.squadIndexes.filter((item) => item !== number) : [...value.squadIndexes, number]; patch({ enabled: value.enabled && squadIndexes.length > 0, squadIndexes }); }} /><span>{t("squad.number", { number })}</span></label>{selected ? <span className="automation-squad-drag-handle" aria-hidden="true">↕</span> : null}</div>; })}</div>{value.squadIndexes.length === 0 ? <span className="status-error">{t("squad.afkAllianceDrillSquadRequired")}</span> : null}<RallyJoinSettings value={value.joinRestrictions || normalizeJoinRestrictions(undefined, 1, true)} onChange={(joinRestrictions) => patch({ joinRestrictions })} disabled={disabled} previewState={previewState} ToggleRow={ToggleRow} />{runtime.filter((row) => row.step === "waiting_join_delay").map((row) => <span className="muted" key={row.squadIndex}>{t("squad.number", { number: row.squadIndex })} · {t("squad.join.waitingDetail", { target: row.joinTargetName || "", seconds: row.joinWaitSeconds ?? 0 })}</span>)}</section>;
}

function GarrisonPreviewSettings({ disabled, value, onChange, previewState = "" }) {
  const { t } = useI18n();
  const runtime = previewGarrisonRuntime(previewState);
  const save = (next) => onChange(refreshGarrisonTargets(next, memberFixture.members));
  const memberFixture = previewGarrisonMemberFixture(previewState);
  const members = memberFixture.members.filter((member) => member.uid !== memberFixture.selfUid);
  const memberMap = new Map(memberFixture.members.map((member) => [member.uid, member]));
  const buildingMap = new Map(runtime.buildings.map((building) => [building.buildId, building]));
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pickerSearch, setPickerSearch] = useState("");
  const [draftAllies, setDraftAllies] = useState(new Set());
  const [draggedTargetKey, setDraggedTargetKey] = useState("");
  const [draggedSquad, setDraggedSquad] = useState(null);
  const filtered = members.filter((member) => !pickerSearch.trim() || `${member.name} ${member.uid}`.toLowerCase().includes(pickerSearch.trim().toLowerCase()));
  const [selectionError, setSelectionError] = useState("");
  const selected = new Set(value.targets.map((target) => target.kind === "allianceBuilding" ? `building:${target.buildId}` : `ally:${target.uid}`));
  const selectedAllies = value.targets.filter((target) => target.kind === "allyCity");
  const availableTargetCount = value.targets.filter((target) => target.kind === "allianceBuilding"
    ? buildingMap.get(target.buildId)?.available === true
    : memberMap.get(target.uid)?.available === true).length;
  const orderedSquads = [...value.squadPriority, ...[1,2,3,4].filter((number) => !value.squadPriority.includes(number))];
  const targetKey = (target) => target.kind === "allianceBuilding" ? `building:${target.buildId}` : `ally:${target.uid}`;
  const targetName = (target) => target.kind === "allianceBuilding"
    ? buildingMap.get(target.buildId)?.name || target.nameSnapshot
    : memberMap.get(target.uid)?.name || target.nameSnapshot;
  const reorderTarget = (target) => {
    if (!draggedTargetKey || draggedTargetKey === targetKey(target)) return;
    const next = [...value.targets];
    const sourceIndex = next.findIndex((entry) => targetKey(entry) === draggedTargetKey);
    const targetIndex = next.findIndex((entry) => targetKey(entry) === targetKey(target));
    if (sourceIndex < 0 || targetIndex < 0) return;
    const [moved] = next.splice(sourceIndex, 1);
    next.splice(targetIndex, 0, moved);
    save({ ...value, targets: next });
  };
  const reorderSquad = (target) => {
    if (draggedSquad == null || draggedSquad === target) return;
    const next = [...value.squadPriority];
    const sourceIndex = next.indexOf(draggedSquad);
    const targetIndex = next.indexOf(target);
    if (sourceIndex < 0 || targetIndex < 0) return;
    const [moved] = next.splice(sourceIndex, 1);
    next.splice(targetIndex, 0, moved);
    save({ ...value, squadPriority: next });
  };
  const toggleBuilding = (building) => {
    const key = `building:${building.buildId}`;
    const targets = selected.has(key)
      ? value.targets.filter((target) => !(target.kind === "allianceBuilding" && target.buildId === building.buildId))
      : [...value.targets, { kind: "allianceBuilding", buildId: building.buildId, nameSnapshot: building.name }];
    save({ ...value, targets });
  };
  useEffect(() => {
    if (!pickerOpen) return;
    const closeOnEscape = (event) => { if (event.key === "Escape") setPickerOpen(false); };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [pickerOpen]);
  return (
    <section className="automation-card monster-afk-toolbar-settings garrison-settings">
      <div className="monster-afk-toolbar-settings-heading"><div><strong>{t("garrison.settings")}</strong><span>{t("garrison.settingsDescription")}</span></div><span className="garrison-count">{t("garrison.selectedTargets", { count: value.targets.length })}</span></div>
      <div className="garrison-settings-grid">
        <div className="garrison-target-panel">
          <div className="garrison-section-heading"><strong>{t("garrison.buildings")}</strong><span>{runtime.buildings.filter((building) => selected.has(`building:${building.buildId}`)).length}/{runtime.buildings.length}</span></div>
          <div className="garrison-building-grid">{runtime.buildings.map((building) => <label className={`garrison-building ${building.role}${building.available ? "" : " unavailable"}`} key={building.key}><input type="checkbox" checked={selected.has(`building:${building.buildId}`)} disabled={disabled || !building.available} onChange={() => toggleBuilding(building)} /><span>{building.name}<small>{building.unavailableReason === "season_settled" ? t("garrison.seasonEnded") : building.full ? t("garrison.full") : t(building.role === "center" ? "garrison.center" : "garrison.attachment")}</small></span></label>)}{runtime.buildings.length === 0 ? <p className="muted">{t(disabled ? "garrison.offline" : "garrison.noBuildings")}</p> : null}</div>
          <div className="garrison-section-heading"><strong>{t("garrison.allies")}</strong><button type="button" disabled={disabled || !memberFixture.ready} onClick={() => { setDraftAllies(new Set(selectedAllies.map((target) => target.uid))); setPickerSearch(""); setPickerOpen(true); }}>{t("garrison.chooseAllies")}</button></div>
          {!memberFixture.ready ? <span className="muted">{t(!memberFixture.online ? "squad.join.membersOffline" : memberFixture.failed ? "squad.join.membersFailed" : "squad.join.membersLoading")}</span> : null}
          <div className="garrison-selected-allies">{selectedAllies.length ? selectedAllies.map((target) => { const member = memberMap.get(target.uid); return <span className={member?.available === true ? "" : "unavailable"} key={target.uid}>{member?.name || target.nameSnapshot}</span>; }) : <p className="muted">{t("garrison.noSelectedAllies")}</p>}</div>
          <div className="garrison-section-heading"><strong>{t("garrison.targetPriority")}</strong><span>{t("garrison.dragHint")}</span></div>
          <div className="garrison-priority-list">{value.targets.map((target, index) => <div className="garrison-priority-item" draggable={!disabled} onDragStart={() => setDraggedTargetKey(targetKey(target))} onDragOver={(event) => { if (draggedTargetKey && draggedTargetKey !== targetKey(target)) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); reorderTarget(target); setDraggedTargetKey(""); }} onDragEnd={() => setDraggedTargetKey("")} key={targetKey(target)}><span>{index + 1}</span><strong>{targetName(target)}</strong><i aria-hidden="true">↕</i></div>)}</div>
          {value.targets.length === 0 ? <p className="muted">{t("garrison.targetRequired")}</p> : null}
        </div>
        <div className="garrison-runtime-panel">
          <div className="garrison-section-heading"><strong>{t("garrison.squadPriority")}</strong><span>{value.squadPriority.join(" → ") || "-"}</span></div>
          <div className="automation-compact-choice-group automation-squad-priority">{orderedSquads.map((number) => { const isSelected = value.squadPriority.includes(number); return <div className={`automation-squad-priority-item${isSelected ? " selected" : ""}`} draggable={isSelected && !disabled} onDragStart={() => isSelected && setDraggedSquad(number)} onDragOver={(event) => { if (isSelected && draggedSquad != null && draggedSquad !== number) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); reorderSquad(number); setDraggedSquad(null); }} onDragEnd={() => setDraggedSquad(null)} key={number}><label><input type="checkbox" checked={isSelected} disabled={disabled} onChange={() => { if (isSelected && value.enabled && value.squadPriority.length === 1) { setSelectionError("garrison.squadRequired"); return; } setSelectionError(""); const squadPriority = isSelected ? value.squadPriority.filter((item) => item !== number) : [...value.squadPriority, number]; save({ ...value, squadPriority }); }} /><span>{t("squad.number", { number })}</span></label>{isSelected ? <span className="automation-squad-drag-handle" aria-hidden="true">↕</span> : null}</div>; })}</div>
          {value.squadPriority.length === 0 ? <p className="muted">{t("garrison.squadRequired")}</p> : <p className="muted">{t("garrison.squadHint")}</p>}
          <div className="garrison-section-heading"><strong>{t("garrison.current")}</strong><span>{runtime.guardingCount}/{Math.min(value.squadPriority.length, availableTargetCount)}</span></div>
          <div className="garrison-assignment-list">{runtime.assignments.length ? runtime.assignments.map((assignment) => <div className="garrison-assignment" key={`${assignment.squadIndex}:${assignment.targetKey}`}><b>{assignment.squadIndex}</b><span><strong>{assignment.targetName}</strong><small>{t(`garrison.state.${assignment.state}`)}</small></span><em className={assignment.state}>{t(assignment.owned ? "garrison.automatic" : "garrison.manual")}</em></div>) : <p className="muted">{t("garrison.noAssignments")}</p>}</div>
        </div>
      </div>
      {selectionError ? <p className="inline-error" role="alert">{t(selectionError)}</p> : null}
      {runtime.lastError ? <p className="automation-error" role="alert">{t(runtime.lastError)}</p> : null}
      <div className="garrison-actions"><ToggleRow label={t("garrison.recallOnDisable")} checked={value.recallOnDisable !== false} disabled={disabled} onChange={(recallOnDisable) => save({ ...value, recallOnDisable })} /><button type="button" className="primary-action" disabled data-preview-action="presentation-only" title={t("status.gameDisconnectedDisabled")}>{t("garrison.runNow")}</button></div>
      {pickerOpen ? <div className="garrison-modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setPickerOpen(false); }}><section className="garrison-modal" role="dialog" aria-modal="true" aria-label={t("garrison.chooseAllies")}><div className="garrison-modal-heading"><strong>{t("garrison.chooseAllies")}</strong><button type="button" onClick={() => setPickerOpen(false)}>{t("common.cancel")}</button></div><input aria-label={t("garrison.searchAlly")} value={pickerSearch} onChange={(event) => setPickerSearch(event.target.value)} placeholder={t("garrison.searchAlly")} autoFocus /><div className="garrison-ally-list">{filtered.map((member) => <label className={member.available ? "" : "unavailable"} key={member.uid}><input type="checkbox" checked={draftAllies.has(member.uid)} disabled={!member.available} onChange={(event) => setDraftAllies((current) => { const next = new Set(current); if (event.target.checked) next.add(member.uid); else next.delete(member.uid); return next; })} /><span><strong>{member.name}</strong><small>{t("garrison.allyDetail", { level: member.level, power: Math.round(member.power / 10000) })} · {t(member.available ? member.online ? "garrison.online" : "garrison.offlineMember" : member.unavailableReason === "cross_server" ? "garrison.crossServer" : "garrison.targetLoading")}</small></span></label>)}</div><div className="garrison-modal-actions"><button type="button" className="primary-action" onClick={() => { const retained = value.targets.filter((target) => target.kind !== "allyCity" || draftAllies.has(target.uid)); const retainedAllies = new Set(retained.filter((target) => target.kind === "allyCity").map((target) => target.uid)); const added = members.filter((member) => member.available && draftAllies.has(member.uid) && !retainedAllies.has(member.uid)).map((member) => ({ kind: "allyCity", uid: member.uid, nameSnapshot: member.name })); save({ ...value, targets: [...retained, ...added] }); setPickerOpen(false); }}>{t("common.completed")}</button></div></section></div> : null}
    </section>
  );
}

function ZombieBusPreviewSettings({ previewState = "" }) {
  const { t } = useI18n();
  const runtime = previewZombieBusRuntime(previewState);
  return <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("zombieBus.title")}</strong></div><p className="muted">{t("zombieBus.description")}</p>{runtime.assignments.length ? <div className="zombie-bus-assignments"><table><thead><tr>{["squad","ally","bus","status"].map((key) => <th key={key}>{t(`zombieBus.${key}`)}</th>)}</tr></thead><tbody>{runtime.assignments.map((row) => <tr key={row.squadIndex}><td>{row.squadIndex}</td><td>{row.name || row.uid}</td><td>{row.nameKey && runtime.gameTexts?.[row.nameKey] && runtime.gameTexts[row.nameKey] !== row.nameKey ? runtime.gameTexts[row.nameKey] : t(row.gold === true ? "zombieBus.gold" : row.gold === false ? "zombieBus.normal" : "zombieBus.unknown")}</td><td>{t(`zombieBus.state.${row.state}`)}</td></tr>)}</tbody></table></div> : null}{runtime.lastError ? <p className="automation-error" role="alert">{t(runtime.lastError)}</p> : null}</section>;
}

function validAfkToolbarConfig(config) {
  return Number.isInteger(config.minStamina) && config.minStamina >= 0 && config.minStamina <= 9999
    && typeof config.preferFifty === "boolean"
    && (!config.allianceDrill.enabled || config.allianceDrill.squadIndexes.length > 0)
    && validJoinRestrictions(config.allianceDrill.joinRestrictions)
    && (!config.garrison.enabled || (config.garrison.targets.length > 0 && config.garrison.squadPriority.length > 0));
}

function initialAfkProfiles(previewState) {
  const steel = makePreviewAfkProfile("steel", "Fixture Steel Hunt", "farm", "steel");
  const gold = { ...makePreviewAfkProfile("gold", "Fixture Gold Hunt", "farm", "gold"), squadIndexes: [2], minLevel: 3, maxLevel: 8, levelFilterEnabled: true };
  if (previewState === "squads-profile-range-warning") return [{ ...makePreviewAfkProfile("steel", "Fixture Range Warning", "farm", "steel"), minLevel: 11, maxLevel: 12, levelFilterEnabled: true }];
  if (previewState === "squads-profile-target-undiscovered") return [{ ...steel, monsterNameKey: "fixture-missing-target" }];
  if (previewState.startsWith("squads-profile-members-")) {
    const join = makePreviewAfkProfile("join", "Fixture Rally Join", "join", "boss");
    const target = previewState === "squads-profile-members-missing-target" ? { ...join, targetKey: "fixture-missing-rally", monsterNameKey: "fixture-missing-rally", source: "map" } : join;
    const leaders = previewState === "squads-profile-members-left" ? [{ uid: "19999", name: "Fixture Departed" }]
      : previewState === "squads-profile-members-self" ? [{ uid: "10000", name: "Fixture Self" }]
      : [{ uid: "10001", name: "Fixture Avery" }];
    return [{ ...target, joinRestrictions: { ...join.joinRestrictions, enabled: true, leaderListMode: "whitelist", leaders } }];
  }
  return [steel, gold];
}

function AfkContent({ previewEnabled, previewState }) {
  const { t } = useI18n();
  const [showEditor, setShowEditor] = useState(previewEnabled);
  const config = usePreviewConfig(() => previewEnabled ? initialAfkProfiles(previewState) : [], (profiles) => profiles.every(previewAfkProfileValid), previewState === "squads-profile-save-error", `afk:${previewState}`);
  const toolbar = usePreviewConfig(() => initialAfkToolbarConfig(previewState), validAfkToolbarConfig, previewState === "squads-profile-toolbar-save-error", `afk-toolbar:${previewState}`);
  const profiles = config.draft;
  const setProfiles = (value, delay = 400) => config.store.edit(value, delay);
  const [editingId, setEditingId] = useState(() => previewEnabled ? initialAfkProfiles(previewState)[0]?.id || "" : "");
  const [draggedProfileId, setDraggedProfileId] = useState("");
  const [addMenuOpen, setAddMenuOpen] = useState(false);
  const [toolbarPanel, setToolbarPanel] = useState(() => previewState.includes("potion") ? "potion" : previewState.includes("drill") ? "drill" : previewState.includes("garrison") ? "garrison" : previewState.includes("zombie") ? "zombieBus" : null);
  const newProfileCounter = useRef(1);
  const editingProfile = profiles.find((profile) => profile.id === editingId) || null;
  const updateProfile = (nextProfile) => setProfiles((current) => current.map((profile) => profile.id === nextProfile.id ? nextProfile : profile));
  const addProfile = (kind) => { const target = previewAfkTargets.find((entry) => !entry.key.startsWith("name:") && entry.group !== "drill" && (kind === "join" ? entry.rally : !entry.rally)); if (!target) return; const id = `preview-new-${newProfileCounter.current++}`; const profile = makePreviewAfkProfile(id, target.name, kind, target.key); setProfiles((current) => [...current, profile]); setEditingId(id); setShowEditor(true); setAddMenuOpen(false); };
  const moveProfile = (sourceId, targetId) => { setProfiles((current) => {
    const sourceIndex = current.findIndex((profile) => profile.id === sourceId);
    const targetIndex = current.findIndex((profile) => profile.id === targetId);
    if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) return current;
    const next = [...current];
    const [moved] = next.splice(sourceIndex, 1);
    next.splice(targetIndex, 0, moved);
    return next;
  }, false); config.store.flush().catch(() => {}); };
  const flushToolbar = (next) => { toolbar.store.edit(next, false); toolbar.store.flush().catch(() => {}); };
  const updateToolbar = (mutator, immediate = true) => {
    const current = toolbar.store.getSnapshot().draft;
    const next = typeof mutator === "function" ? mutator(current) : mutator;
    if (immediate) flushToolbar(next); else toolbar.store.edit(next);
  };
  const garrisonRuntime = previewGarrisonRuntime(previewState);
  const garrisonMembers = previewGarrisonMemberFixture(previewState);
  const zombieRuntime = previewZombieBusRuntime(previewState);
  const targetDiscoveryReady = !["squads-profile-target-loading", "squads-profile-target-failed"].includes(previewState);
  const profilePanelHidden = ["drill", "garrison", "zombieBus"].includes(toolbarPanel);
  const garrisonBuildingMap = new Map(garrisonRuntime.buildings.map((building) => [building.buildId, building]));
  const garrisonMemberMap = new Map(garrisonMembers.members.map((member) => [member.uid, member]));
  const garrisonAvailableTargetCount = toolbar.draft.garrison.targets.filter((target) => target.kind === "allianceBuilding"
    ? garrisonBuildingMap.get(target.buildId)?.available === true
    : garrisonMemberMap.get(target.uid)?.available === true).length;
  const zombieRunning = zombieRuntime.assignments.some((assignment) => ["sending", "marching", "guarding", "recalling", "returning"].includes(assignment.state));
  const drillRuntime = previewDrillRuntime(previewState);
  const drillState = drillRuntime.some((row) => row.activityRole === "leader") ? "squad.afkAllianceDrillLeading" : drillRuntime.length > 0 ? "squad.afkAllianceDrillJoining" : "squad.afkAllianceDrillWaiting";
  const drillOrder = toolbar.draft.allianceDrill.squadIndexes.join(" → ") || "-";
  const toggleToolbarPanel = (panel) => setToolbarPanel((current) => current === panel ? null : panel);
  return (
    <div className="monster-afk-layout" data-draft-dirty={config.dirty || toolbar.dirty} data-draft-saving={config.saving || toolbar.saving} data-preview-fixture={previewEnabled ? "squads-profile" : "runtime-config-unobserved"}>
      <PreviewConfigError config={config} t={t} />
      <PreviewConfigError config={toolbar} t={t} />
      <div className="monster-afk-toolbar">
        <CompactAfkCard title={t("squad.afkMaster")} description={t("squad.afkMasterDescription")} summary={t(toolbar.draft.masterEnabled ? "common.enabled" : "common.disabled")} enabled={toolbar.draft.masterEnabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === null} onSettings={() => setToolbarPanel(null)} onToggle={(masterEnabled) => updateToolbar((draft) => ({ ...draft, masterEnabled }))} />
        <CompactAfkCard title={t("automation.autoUsePotion")} description={t("automation.potionMonsterOnly")} summary={`${t("automation.minStamina")} ${toolbar.draft.minStamina}`} enabled={toolbar.draft.potionEnabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "potion"} onSettings={() => toggleToolbarPanel("potion")} onToggle={(potionEnabled) => updateToolbar((draft) => ({ ...draft, potionEnabled }))} />
        <CompactAfkCard title={t("squad.afkAllianceDrill")} description={t("squad.afkAllianceDrillDescription")} summary={toolbar.draft.allianceDrill.enabled && drillRuntime.length > 0 ? `${t(drillState)} · ${drillOrder}` : `${t("squad.afkAllianceDrillOrder", { order: drillOrder })} · ${t(toolbar.draft.allianceDrill.activeRally ? "squad.afkAllianceDrillActive" : "squad.afkJoin")}`} enabled={toolbar.draft.allianceDrill.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "drill"} onSettings={() => toggleToolbarPanel("drill")} onToggle={(enabled) => { if (enabled && toolbar.draft.allianceDrill.squadIndexes.length === 0) { setToolbarPanel("drill"); return; } updateToolbar((draft) => ({ ...draft, allianceDrill: { ...draft.allianceDrill, enabled } })); }} />
        <CompactAfkCard title={t("garrison.title")} description={t("garrison.description")} summary={toolbar.draft.garrison.enabled ? t("garrison.summary", { active: garrisonRuntime.guardingCount, total: Math.min(toolbar.draft.garrison.squadPriority.length, garrisonAvailableTargetCount) }) : t("common.disabled")} enabled={toolbar.draft.garrison.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "garrison"} onSettings={() => toggleToolbarPanel("garrison")} onToggle={(enabled) => { if (enabled && (!toolbar.draft.garrison.targets.length || !toolbar.draft.garrison.squadPriority.length)) { setToolbarPanel("garrison"); return; } updateToolbar((draft) => ({ ...draft, garrison: { ...draft.garrison, enabled } })); }} />
        <CompactAfkCard title={t("zombieBus.title")} description={t("zombieBus.description")} summary={previewEnabled ? t(zombieRuntime.lastError ? "common.failed" : zombieRunning ? "automation.running" : toolbar.draft.zombieBus.enabled ? "zombieBus.waiting" : "common.disabled") : t("status.disconnected")} enabled={toolbar.draft.zombieBus.enabled} disabled={!previewEnabled} settingsOpen={toolbarPanel === "zombieBus"} onSettings={() => toggleToolbarPanel("zombieBus")} onToggle={(enabled) => updateToolbar((draft) => ({ ...draft, zombieBus: { ...draft.zombieBus, enabled } }))} />
      </div>
      {toolbarPanel === "potion" ? <section className="automation-card monster-afk-toolbar-settings"><div className="monster-afk-toolbar-settings-heading"><strong>{t("automation.autoUsePotion")}</strong><span>{t("automation.potionMonsterOnly")}</span></div><div className="monster-afk-card-settings"><label><span>{t("automation.minStamina")}</span><input type="number" min="0" max="9999" step="1" value={Number.isNaN(toolbar.draft.minStamina) ? "" : toolbar.draft.minStamina} disabled={!previewEnabled} onChange={(event) => updateToolbar((draft) => ({ ...draft, minStamina: event.target.valueAsNumber }), false)} onBlur={() => toolbar.store.flush().catch(() => {})} /></label><label><input type="checkbox" checked={toolbar.draft.preferFifty} disabled={!previewEnabled} onChange={(event) => updateToolbar((draft) => ({ ...draft, preferFifty: event.target.checked }))} /><span>{t("automation.preferFifty")}</span></label></div></section> : null}
      {toolbarPanel === "drill" ? <AllianceDrillPreviewSettings disabled={!previewEnabled} value={toolbar.draft.allianceDrill} previewState={previewState} onChange={(allianceDrill) => updateToolbar((draft) => ({ ...draft, allianceDrill }))} /> : null}
      {toolbarPanel === "garrison" ? <GarrisonPreviewSettings disabled={!previewEnabled} value={toolbar.draft.garrison} previewState={previewState} onChange={(garrison) => updateToolbar((draft) => ({ ...draft, garrison }))} /> : null}
      {toolbarPanel === "zombieBus" ? <ZombieBusPreviewSettings previewState={previewState} /> : null}
      {zombieRuntime.lastError && toolbarPanel !== "zombieBus" ? <div className="monster-afk-toolbar-settings"><p className="automation-error" role="alert">{t(zombieRuntime.lastError)}</p></div> : null}
      {!profilePanelHidden ? <section className="monster-afk-profiles">
        <div className="monster-section-title">
          <strong>{t("squad.afkProfiles")}</strong>
          <div className="monster-afk-add-control" onKeyDown={(event) => { if (event.key === "Escape") setAddMenuOpen(false); }}><button type="button" aria-haspopup="menu" aria-expanded={addMenuOpen} disabled={!previewEnabled || !targetDiscoveryReady || previewAfkTargets.length === 0} onClick={() => setAddMenuOpen((open) => !open)}>{t("common.add")}</button>{addMenuOpen ? <div className="monster-afk-add-menu" role="menu"><button type="button" role="menuitem" disabled={!previewAfkTargets.some((target) => !target.rally)} onClick={() => addProfile("farm")}><strong>{t("squad.afkActiveAttack")}</strong><span>{t("squad.afkActionAttack")}</span></button><button type="button" role="menuitem" disabled={!previewAfkTargets.some((target) => target.rally)} onClick={() => addProfile("join")}><strong>{t("squad.afkJoin")}</strong><span>{t("squad.autoJoinRally")}</span></button></div> : null}</div>
        </div>
        <div className="monster-afk-profile-list">
          {profiles.length ? profiles.map((profile) => { const profileTargets = !targetDiscoveryReady ? [] : previewState === "squads-profile-target-undiscovered" ? previewAfkTargets.filter((entry) => entry.key !== profile.targetKey) : previewAfkTargets; const target = resolvePreviewAfkTarget(profileTargets, profile); const source = target?.source || (targetDiscoveryReady ? "undiscovered" : profile.source); const action = target?.action || profile.action; const rangeWarning = profile.kind === "farm" && profile.searchable && previewAfkLevelOutOfRange(profile, target); return <article className={`monster-afk-profile-card ${editingId === profile.id ? "active" : ""} ${profile.enabled ? "" : "disabled"}`} key={profile.id} onDragOver={(event) => { if (draggedProfileId) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); moveProfile(draggedProfileId, profile.id); setDraggedProfileId(""); }}><label className={`monster-afk-enabled ${profile.enabled ? "is-enabled" : ""} ${profile.squadIndexes.length === 0 ? "is-disabled" : ""}`}><input type="checkbox" checked={profile.enabled} disabled={!previewEnabled || profile.squadIndexes.length === 0} onChange={(event) => { setProfiles((current) => current.map((entry) => entry.id === profile.id ? { ...entry, enabled: event.target.checked } : entry), false); config.store.flush().catch(() => {}); }} /><span className="monster-afk-enabled-track" aria-hidden="true" /><span>{t(profile.enabled ? "common.enabled" : "common.disabled")}</span></label><button type="button" className="monster-afk-profile-select" aria-pressed={editingId === profile.id} onClick={() => { setEditingId(profile.id); setShowEditor(true); }}><span className="monster-afk-profile-heading"><strong>{profile.name}</strong><span className={`monster-afk-mode-badge ${profile.kind}`}>{t(profile.kind === "farm" ? "squad.afkActiveAttack" : "squad.afkJoin")}</span></span><span>{profile.levelFilterEnabled ? `${profile.minLevel}-${profile.maxLevel}` : t("squad.afkAnyLevel")} · {profile.distanceFilterEnabled ? profile.maxDistance : t("squad.afkAnyDistance")}</span>{profile.kind === "farm" && profile.searchable ? <span className={rangeWarning ? "monster-afk-level-warning" : ""}>{target?.attackMinLevel != null && target?.attackMaxLevel != null ? t("squad.afkAttackableRange", { min: target.attackMinLevel, max: target.attackMaxLevel }) : t("squad.afkAttackableRangeUnavailable")}{rangeWarning ? ` · ${t("squad.afkLevelOutOfRange")}` : ""}</span> : null}<span>{`${t(source === "search" ? "squad.afkSearchable" : source === "map" ? "squad.afkLocalTarget" : "squad.afkUndiscovered")} · ${t(profile.kind === "join" ? "squad.afkJoin" : action === "rally" ? "squad.afkActionRally" : "squad.afkActionAttack")} · ${t("squad.afkBoundSquads", { count: profile.squadIndexes.join(", ") || "-" })}`}</span></button><button type="button" className="danger" onClick={() => { if (!window.confirm(`${t("common.delete")} “${profile.name}”?`)) return; const remaining = config.store.getSnapshot().draft.filter((entry) => entry.id !== profile.id); setProfiles(remaining, false); config.store.flush().catch(() => {}); if (editingId === profile.id) { setEditingId(remaining[0]?.id || ""); setShowEditor(remaining.length > 0); } }}>{t("common.delete")}</button><button type="button" className="monster-afk-profile-drag" draggable aria-label={t("squad.afkReorder", { name: profile.name })} title={t("squad.afkReorder", { name: profile.name })} onDragStart={() => setDraggedProfileId(profile.id)} onDragEnd={() => setDraggedProfileId("")} onKeyDown={(event) => { if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return; event.preventDefault(); const index = profiles.findIndex((entry) => entry.id === profile.id); const moveTarget = profiles[index + (event.key === "ArrowUp" ? -1 : 1)]; if (moveTarget) moveProfile(profile.id, moveTarget.id); }}>↕</button></article>; }) : <span className="muted">{t("squad.afkNoProfiles")}</span>}
        </div>
        {showEditor && editingProfile ? <AfkProfileEditor enabled={previewEnabled} isNew={!config.confirmed.some((profile) => profile.id === editingProfile.id)} profile={editingProfile} onProfileChange={updateProfile} previewState={previewState} /> : null}
      </section> : null}
    </div>
  );
}

function SettingsGlyph() {
  return (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false">
      <circle cx="8" cy="8" r="2.5" />
      <path d="M8 1.5v1.8M8 12.7v1.8M1.5 8h1.8M12.7 8h1.8M3.4 3.4l1.3 1.3M11.3 11.3l1.3 1.3M3.4 12.6l1.3-1.3M11.3 4.7l1.3-1.3" />
    </svg>
  );
}

function CompactAfkCard({ title, description, summary, enabled, disabled = false, settingsOpen = false, onToggle, onSettings }) {
  const { t } = useI18n();
  const selectable = !!onSettings;
  return (
    <article className={`automation-card monster-afk-compact-card${selectable ? " is-selectable" : ""}${settingsOpen ? " is-selected" : ""}`} title={description || title} role={selectable ? "button" : undefined} tabIndex={selectable ? 0 : undefined} onClick={selectable ? onSettings : undefined} onKeyDown={(event) => { if (selectable && (event.key === "Enter" || event.key === " ")) { event.preventDefault(); onSettings(); } }}>
      <div className="automation-card-header">
        <div><h3>{title}</h3><p>{summary}</p></div>
        <div className="monster-afk-compact-actions">
          {onSettings ? <button type="button" className={settingsOpen ? "active" : ""} aria-label={t("nav.settings")} aria-pressed={settingsOpen} onClick={(event) => { event.stopPropagation(); onSettings(); }}><SettingsGlyph /></button> : null}
          <label className={`monster-afk-compact-toggle ${enabled ? "is-enabled" : ""}`} onMouseDown={(event) => { event.preventDefault(); event.stopPropagation(); }} onClick={(event) => event.stopPropagation()}>
            <input type="checkbox" disabled={disabled} checked={enabled} onClick={(event) => event.stopPropagation()} onChange={(event) => onToggle?.(event.target.checked)} aria-label={title} />
            <span className="monster-afk-master-track" aria-hidden="true" />
          </label>
        </div>
      </div>
    </article>
  );
}

function EquipmentDialog({ busy, onClose, children }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    if (!dialog) return undefined;
    if (typeof dialog.showModal === "function") dialog.showModal();
    else dialog.setAttribute("open", "");
    return () => {
      if (dialog.open && typeof dialog.close === "function") dialog.close();
      else dialog.removeAttribute("open");
      if (previousFocus?.isConnected) previousFocus.focus({ preventScroll: true });
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className="app-dialog equipment-preset-dialog-backdrop"
      role="dialog"
      aria-modal="true"
      aria-labelledby="equipment-preset-title"
      aria-busy={busy}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
      onKeyDown={(event) => {
        event.stopPropagation();
        if (event.key !== "Tab") return;
        const focusable = [...event.currentTarget.querySelectorAll("button, [href], input, select, textarea, [tabindex]")]
          .filter((element) => element.tabIndex >= 0 && !element.matches(":disabled") && element.getClientRects().length > 0);
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (!first) {
          event.preventDefault();
          return;
        }
        if (event.shiftKey && (document.activeElement === first || !focusable.includes(document.activeElement))) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !focusable.includes(document.activeElement))) {
          event.preventDefault();
          first.focus();
        }
      }}
    >
      {children}
    </dialog>
  );
}

function EquipmentContent({ previewEnabled, previewState = "" }) {
  const { t } = useI18n();
  const reducedMotion = useReducedMotion();
  const fixture = useMemo(() => previewEquipmentFixture(previewState, t), [previewState, t]);
  const [presets, setPresets] = useState(() => cloneEquipmentValue(fixture.presets));
  const [confirmedPresets, setConfirmedPresets] = useState(() => cloneEquipmentValue(fixture.confirmedPresets));
  const [selectedPresetId, setSelectedPresetId] = useState(() => presets[0]?.id || "");
  const [renameOpen, setRenameOpen] = useState(fixture.renameOpen);
  const [renameValue, setRenameValue] = useState(() => presets[0]?.name || "");
  const [dragged, setDragged] = useState(null);
  const [dropTarget, setDropTarget] = useState("");
  const [dropSuccess, setDropSuccess] = useState([]);
  const [toast, setToast] = useState("");
  const [lastPreviewAction, setLastPreviewAction] = useState("");
  const [actionBusyKey, setActionBusyKey] = useState("");
  const [configSaving, setConfigSaving] = useState(false);
  const [configError, setConfigError] = useState(null);
  const acknowledgementState = useRef({ previewState, rejectionConsumed: false, pendingPromise: null });
  if (acknowledgementState.current.previewState !== previewState) {
    acknowledgementState.current = { previewState, rejectionConsumed: false, pendingPromise: null };
  }
  const busyKey = fixture.busyKey || actionBusyKey;
  const busy = Boolean(busyKey);
  const online = fixture.online;
  const selectedPreset = presets.find((preset) => preset.id === selectedPresetId) || presets[0];
  const dirtyPresetIds = useMemo(() => new Set(equipmentDirtyPresetIds(presets, confirmedPresets)), [presets, confirmedPresets]);
  const availableSquadIndexes = useMemo(() => [...new Set(fixture.squads.map((squad) => squad.index))].filter((index) => index >= 1 && index <= 4).sort((left, right) => left - right), [fixture.squads]);
  const currentMatches = useMemo(() => currentEquipmentPresetMatches(presets, selectedPreset, fixture.squads), [presets, selectedPreset, fixture.squads]);
  const currentEquipmentLabel = useMemo(() => currentEquipmentPresetLabel(currentMatches, availableSquadIndexes, t), [currentMatches, availableSquadIndexes, t]);
  const catalog = useMemo(() => equipmentCatalog(fixture.initialEquipmentConfig, fixture.squads), [fixture.initialEquipmentConfig, fixture.squads]);
  const positionCount = selectedPreset ? equipmentPositionCount(selectedPreset) : 0;
  const equipmentCount = selectedPreset ? equipmentItemCount(selectedPreset) : 0;

  const editPresets = (next) => {
    setPresets(next);
    setConfigError(null);
  };

  const acknowledgePreviewConfig = () => {
    if (previewState === "squads-equipment-rename-pending") {
      acknowledgementState.current.pendingPromise ||= new Promise(() => {});
      return acknowledgementState.current.pendingPromise;
    }
    if (previewState === "squads-equipment-rename-error" && !acknowledgementState.current.rejectionConsumed) {
      acknowledgementState.current.rejectionConsumed = true;
      return Promise.reject(new Error("PREVIEW_EQUIPMENT_SAVE_FAILED"));
    }
    return true;
  };

  const flushPreviewConfig = (nextPresets = presets) => {
    setConfigError(null);
    const acknowledgement = acknowledgePreviewConfig();
    const confirm = () => {
      const confirmed = cloneEquipmentValue(nextPresets);
      setConfirmedPresets(confirmed);
      return confirmed;
    };
    if (!acknowledgement || typeof acknowledgement.then !== "function") return acknowledgement ? confirm() : null;
    setConfigSaving(true);
    return acknowledgement
      .then((value) => value ? confirm() : null)
      .catch((error) => {
        setConfigError(error instanceof Error ? error : new Error(String(error)));
        return null;
      })
      .finally(() => setConfigSaving(false));
  };

  const discardPreviewConfig = () => {
    setPresets(cloneEquipmentValue(confirmedPresets));
    setConfigError(null);
    return cloneEquipmentValue(confirmedPresets);
  };

  const configFeedback = {
    error: configError,
    saving: configSaving,
    store: {
      flush: () => flushPreviewConfig(presets),
      refresh: () => discardPreviewConfig(),
    },
  };

  const markDropSuccess = (keys) => {
    setDropSuccess(keys);
    window.setTimeout(() => setDropSuccess([]), 450);
  };

  const swapPositions = (target) => {
    if (!dragged || !selectedPreset || busy) return;
    const result = swapEquipmentTarget(presets, selectedPreset.id, dragged, target);
    if (result.error) {
      setToast(t(result.error));
      return;
    }
    if (!result.handled) return;
    editPresets(result.presets);
    markDropSuccess(result.successKeys);
  };

  const swapSquads = (targetSquadIndex) => {
    if (dragged?.kind !== "squad" || !selectedPreset || busy) return;
    const result = swapEquipmentSquads(presets, selectedPreset.id, dragged.squadIndex, targetSquadIndex, fixture.squads);
    if (!result.handled) return;
    editPresets(result.presets);
    markDropSuccess(result.successKeys);
  };

  const openRename = () => {
    if (!selectedPreset || busy) return;
    setRenameValue(selectedPreset.name);
    setRenameOpen(true);
  };

  const closeRename = () => {
    if (busy) return;
    setRenameOpen(false);
    setRenameValue("");
  };

  const saveRename = () => {
    const name = renameValue.trim();
    if (!name || !selectedPreset || busy || !renameOpen) return;
    if (name === selectedPreset.name && !dirtyPresetIds.has(selectedPreset.id)) {
      setRenameOpen(false);
      setRenameValue("");
      return;
    }
    const nextPresets = presets.map((preset) => preset.id === selectedPreset.id ? { ...preset, name } : preset);
    editPresets(nextPresets);
    setActionBusyKey("rename");
    const acknowledgement = flushPreviewConfig(nextPresets);
    const finish = (value) => {
      if (value) {
        setRenameOpen(false);
        setRenameValue("");
      }
      setActionBusyKey("");
    };
    if (acknowledgement && typeof acknowledgement.then === "function") acknowledgement.then(finish, () => finish(null));
    else finish(acknowledgement);
  };

  const savePreviewConfig = () => {
    if (!selectedPreset || busy) return;
    setActionBusyKey("save-all");
    const acknowledgement = flushPreviewConfig(presets);
    const finish = (value) => {
      if (value) setToast(t("squad.equipmentConfigSaved"));
      setActionBusyKey("");
    };
    if (acknowledgement && typeof acknowledgement.then === "function") acknowledgement.then(finish, () => finish(null));
    else finish(acknowledgement);
  };

  const resultText = fixture.result ? t(
    fixture.result.state === "success" ? "squad.equipmentApplySuccess" : fixture.result.state === "partial" ? "squad.equipmentApplyPartial" : "squad.equipmentApplyRejected",
    { applied: fixture.result.applied, requested: fixture.result.requested, hero: fixture.result.failedHeroName || "-", reason: fixture.result.reason || "-" },
  ) : "";

  const progressText = fixture.progress
    ? fixture.progress.phase === "preparing"
      ? t("squad.equipmentApplyPreparing")
      : fixture.progress.phase === "verifying"
        ? t("squad.equipmentApplyVerifying", { current: fixture.progress.current, total: fixture.progress.total })
        : t("squad.equipmentApplyRunning", { current: fixture.progress.current, total: fixture.progress.total, hero: fixture.progress.heroName || "-" })
    : "";

  useEffect(() => {
    const onKeyDown = (event) => {
      if (!event.altKey || event.repeat || !/^[1-4]$/.test(event.key) || !presets.length || busy) return;
      const target = event.target;
      if (target?.isContentEditable || ["INPUT", "SELECT", "TEXTAREA"].includes(target?.tagName || "")) return;
      const preset = presets[Number(event.key) - 1];
      if (!preset) return;
      event.preventDefault();
      if (previewEnabled) setLastPreviewAction(`apply-all:${preset.id}`);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [busy, presets, previewEnabled]);

  useEffect(() => {
    if (!toast) return undefined;
    const timer = window.setTimeout(() => setToast(""), 1800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  return (
    <div className="equipment-preset-layout" data-preview-fixture={previewEnabled ? previewState || "squads-equipment" : "no-equipment-presets"} data-preview-action={lastPreviewAction || undefined} data-equipment-busy={busyKey || undefined}>
      {!renameOpen ? <PreviewConfigError config={configFeedback} t={t} label={t("squad.equipmentPresets")} /> : null}
      <aside className="equipment-preset-rail">
        <strong>{t("squad.equipmentPresets")}</strong>
        <div className="equipment-preset-list">{presets.length ? presets.slice(0, EQUIPMENT_PRESET_LIMIT).map((preset, index) => <button type="button" key={preset.id} className={preset.id === selectedPreset?.id ? "active" : ""} onClick={() => setSelectedPresetId(preset.id)}><span>{preset.name}{dirtyPresetIds.has(preset.id) ? " *" : ""}</span><small>{index < 4 ? `Alt+${index + 1} · ` : ""}{t("squad.presetSummary", { positions: equipmentPositionCount(preset), equips: equipmentItemCount(preset) })}</small></button>) : <span className="muted">{t("squad.noEquipmentPresets")}</span>}</div>
      </aside>
      <main className="equipment-preset-main">
        <div className="equipment-preset-toolbar">
          <div><strong>{selectedPreset?.name || t("squad.noEquipmentPresets")}</strong>{selectedPreset ? <span>{t("squad.allSquadPresetSummary", { positions: positionCount, equips: equipmentCount })}{dirtyPresetIds.has(selectedPreset.id) ? ` · ${t("squad.equipmentConfigUnsaved")}` : ""}</span> : null}<span className="equipment-current-config">{t("squad.currentEquipmentPreset", { name: currentEquipmentLabel })}</span></div>
          <div className="equipment-preset-actions">
            <button type="button" disabled={!selectedPreset || busy} onClick={openRename}>{t("common.rename")}</button>
            <button type="button" disabled={!selectedPreset || !online || busy} title={!online ? t("status.gameDisconnectedDisabled") : undefined} onClick={selectedPreset && online && !busy ? () => setLastPreviewAction(`load-current:${selectedPreset.id}`) : undefined}>{t("squad.loadCurrentEquipment")}</button>
            <button type="button" disabled={!selectedPreset || busy} onClick={savePreviewConfig}>{t("squad.saveEquipmentConfig")}</button>
            <button type="button" className="primary" disabled={!selectedPreset || !online || busy} title={!online ? t("status.gameDisconnectedDisabled") : undefined} onClick={selectedPreset && online && !busy ? () => setLastPreviewAction(`apply-all:${selectedPreset.id}`) : undefined}>{t(busyKey === "apply-all" ? "squad.equipmentApplying" : "squad.saveAndApplyEquipmentConfig")}</button>
          </div>
        </div>
        <AnimatePresence mode="wait">{selectedPreset ? <motion.div className="equipment-preset-squads" {...equipmentMotionProps("presets", reducedMotion)} key={selectedPreset.id}>{availableSquadIndexes.map((squadIndex, squadArrayIndex) => {
          const presetSquad = findEquipmentSquad(selectedPreset.squads, squadIndex);
          const liveSquad = fixture.squads.find((squad) => squad.index === squadIndex);
          const squadKey = `squad-${squadIndex}`;
          const squadPositions = presetSquad?.positions.filter((position) => position.equips.length > 0).length || 0;
          const squadEquips = presetSquad?.positions.reduce((sum, position) => sum + position.equips.length, 0) || 0;
          return <motion.section className={`equipment-preset-squad ${dropTarget === squadKey ? "drop-target" : ""}`} {...equipmentMotionProps("squad", reducedMotion, { index: squadArrayIndex })} key={squadIndex} onDragOver={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); setDropTarget(squadKey); } }} onDragLeave={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setDropTarget((current) => current === squadKey ? "" : current); }} onDrop={(event) => { if (dragged?.kind === "squad") { event.preventDefault(); setDropTarget(""); swapSquads(squadIndex); } }}>
            <div className="equipment-preset-squad-header"><div><strong>{t("squad.number", { number: squadIndex })}</strong><span>{t("squad.positionEquipmentCount", { positions: squadPositions, equips: squadEquips })}</span><span className="equipment-current-config">{t("squad.currentEquipmentPreset", { name: currentMatches.get(squadIndex)?.name || t("squad.unmatchedEquipmentPreset") })}</span></div><div className="equipment-preset-actions"><div className="equipment-squad-drag-handle" draggable={!busy} onDragStart={() => setDragged({ kind: "squad", squadIndex })} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>{t("squad.dragSquadLoadout")}</div><button type="button" disabled={!online || busy} title={!online ? t("status.gameDisconnectedDisabled") : undefined} onClick={online && !busy ? () => setLastPreviewAction(`apply-${squadIndex}:${selectedPreset.id}`) : undefined}>{t(busyKey === `apply-${squadIndex}` ? "squad.equipmentApplying" : "squad.applySquad")}</button></div></div>
            <div className="equipment-preset-positions">{(liveSquad?.heroes || []).slice(0, 5).map((hero, heroIndex) => {
              const positionNumber = heroIndex + 1;
              const position = presetSquad?.positions.find((entry) => entry.position === positionNumber) || { position: positionNumber, equips: [] };
              const positionKey = `${squadIndex}-${positionNumber}`;
              const loadoutKey = `loadout-${positionKey}`;
              return <motion.article className={`equipment-position-card ${dropTarget === loadoutKey ? "drop-target" : ""} ${dropSuccess.includes(positionKey) ? "drop-success" : ""}`} {...equipmentMotionProps("position", reducedMotion)} key={positionNumber} onDragOver={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); setDropTarget(loadoutKey); } }} onDragLeave={() => setDropTarget((current) => current === loadoutKey ? "" : current)} onDrop={(event) => { if (dragged?.kind === "loadout") { event.preventDefault(); setDropTarget(""); swapPositions({ kind: "loadout", squadIndex, position: positionNumber }); } }}>
                <div className="equipment-position-header"><strong>{t("squad.position", { number: positionNumber })}</strong><span>{t("squad.heroFixed")}</span></div>
                <div className="equipment-position-hero"><GameAssetImage assetPath={hero.iconPath} alt={hero.name} className="equipment-position-hero-icon" /><div><strong>{hero.name}</strong><span>Lv.{hero.level}</span></div></div>
                <div className="equipment-loadout-handle" draggable={!busy} onDragStart={() => setDragged({ kind: "loadout", squadIndex, position: positionNumber })} onDragEnd={() => { setDragged(null); setDropTarget(""); }}><span>{t("squad.dragLoadout")}</span></div>
                <div className="equipment-position-items">{EQUIPMENT_SLOTS.map((slot) => {
                  const savedEquip = position.equips.find((item) => item.slot === slot);
                  const equip = savedEquip ? { ...catalog.get(savedEquip.equipUuid), ...savedEquip } : undefined;
                  const equipTarget = `equip-${squadIndex}-${positionNumber}-${slot}`;
                  return <motion.div className={`preset-equipment-slot quality-${equip?.quality || 0} ${dropTarget === equipTarget ? "drop-target" : ""}`} {...equipmentMotionProps("slot", reducedMotion, { presetId: selectedPreset?.id, equipUuid: savedEquip?.equipUuid, hasSavedEquip: !!savedEquip })} key={slot} draggable={!!savedEquip && !busy} title={equip?.name || t(`squad.equipmentSlot${slot}`)} onDragStart={(event) => { if (!savedEquip) return; event.stopPropagation(); setDragged({ kind: "equip", squadIndex, position: positionNumber, slot }); }} onDragOver={(event) => { if (dragged?.kind === "equip" && dragged.slot === slot) { event.preventDefault(); event.stopPropagation(); setDropTarget(equipTarget); } }} onDragLeave={(event) => { if (dragged?.kind === "equip" && dragged.slot === slot) { event.stopPropagation(); setDropTarget((current) => current === equipTarget ? "" : current); } }} onDrop={(event) => { if (dragged?.kind === "equip" && dragged.slot === slot) { event.preventDefault(); event.stopPropagation(); setDropTarget(""); swapPositions({ kind: "equip", squadIndex, position: positionNumber, slot }); } }} onDragEnd={() => { setDragged(null); setDropTarget(""); }}>{equip ? <><GameAssetImage assetPath={equip.iconPath} alt={equip.name || String(equip.equipUuid)} className="equipment-icon" /><span>Lv.{equip.level ?? "-"}{equip.promote ? ` +${equip.promote}` : ""}</span></> : <><span className="equipment-icon game-asset-placeholder" /><span>{t("squad.emptyEquipment")}</span></>}</motion.div>;
                })}</div>
              </motion.article>;
            })}</div>
          </motion.section>;
        })}{!availableSquadIndexes.length ? <div className="map-empty">{t("squad.empty")}</div> : null}</motion.div> : <div className="map-empty">{t("squad.createFirstPreset")}</div>}</AnimatePresence>
        <div className="equipment-preset-hint">
          <span>{t("squad.dragEquipmentHint")}</span>
          <strong>{t("squad.quickShortcutHint")}</strong>
        </div>
        {fixture.result ? <div className={`equipment-result equipment-result-${fixture.result.state}`}>{resultText}</div> : null}
      </main>
      <AnimatePresence>
        {renameOpen ? <EquipmentDialog busy={busy} onClose={closeRename}><motion.div className="equipment-preset-dialog" {...equipmentMotionProps("rename", reducedMotion)}><PreviewConfigError config={configFeedback} t={t} label={t("squad.equipmentPresets")} /><strong id="equipment-preset-title">{t("common.rename")}</strong><label>{t("squad.presetNamePrompt")}<input autoFocus disabled={busy} value={renameValue} onChange={(event) => setRenameValue(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") { event.preventDefault(); saveRename(); } }} /></label><div className="equipment-preset-actions"><button type="button" disabled={busy} onClick={closeRename}>{t("common.cancel")}</button><button type="button" className="primary" disabled={!renameValue.trim() || busy} onClick={saveRename}>{t("common.saveConfig")}</button></div></motion.div></EquipmentDialog> : null}
        {toast ? <motion.div className="equipment-toast" {...equipmentMotionProps("toast", reducedMotion)} role="status">{toast}</motion.div> : null}
        {fixture.progress ? <motion.div className="equipment-apply-progress" {...equipmentMotionProps("progress", reducedMotion)} role="status"><strong>{progressText}</strong><progress max={Math.max(1, fixture.progress.total)} value={fixture.progress.total > 0 ? fixture.progress.current : undefined} /></motion.div> : null}
      </AnimatePresence>
    </div>
  );
}
