import { Dt as e, Et as t, Ft as n, I as r, It as i, J as a, Mt as o, Nt as s, Ot as c, Pt as l, Q as u, Tt as d, U as f, V as p, W as m, Z as h, a as g, c as _, ct as v, f as y, gt as b, i as x, it as S, j as C, l as w, o as T, ot as E, p as ee, q as te, w as D, wt as ne, yt as re } from "./index-BVfnK1wp.js";
import { n as ie } from "./AutomationCard-LCx_jIi7.js";
import { t as ae } from "./GameAssetImage-Diy9VTIr.js";
var O = i(o());
function oe(e2, t2 = 1, n2 = false) {
  let r2 = e2?.mode === `slotRange`, i2 = e2?.slotRange ?? [t2 + 1, t2 + 1];
  return { enabled: e2?.enabled ?? !n2, mode: r2 ? `slot` : e2?.mode ?? (n2 ? `delay` : `slot`), slotRange: r2 ? [i2[0], i2[0]] : [i2[0], i2[1]], slotDelaySeconds: r2 ? 0 : e2?.slotDelaySeconds ?? 0, delaySeconds: e2?.delaySeconds ?? (n2 ? [1, 3] : [0, 0]), maxWaitMinutes: e2?.maxWaitMinutes ?? (n2 ? 5 : 0), leaderListMode: e2?.leaderListMode ?? `off`, leaders: e2?.leaders ?? [], skipSoloLeader: e2?.skipSoloLeader ?? false, skipKicked: e2?.skipKicked ?? n2 };
}
function se(e2) {
  return typeof e2.enabled == `boolean` && [`slot`, `delay`].includes(e2.mode) && Number.isFinite(e2.slotDelaySeconds) && e2.slotDelaySeconds >= 0 && e2.slotDelaySeconds <= 6e3 && Math.abs(e2.slotDelaySeconds * 100 - Math.round(e2.slotDelaySeconds * 100)) < 1e-6 && Array.isArray(e2.slotRange) && e2.slotRange.length === 2 && e2.slotRange.every((e3) => Number.isSafeInteger(e3) && e3 >= 2 && e3 <= 5) && e2.slotRange[1] >= e2.slotRange[0] && Array.isArray(e2.delaySeconds) && e2.delaySeconds.length === 2 && e2.delaySeconds.every((e3) => Number.isFinite(e3) && e3 >= 0 && e3 <= 6e3 && Math.abs(e3 * 100 - Math.round(e3 * 100)) < 1e-6) && e2.delaySeconds[1] >= e2.delaySeconds[0] && Number.isFinite(e2.maxWaitMinutes) && e2.maxWaitMinutes >= 0 && [`off`, `blacklist`, `whitelist`].includes(e2.leaderListMode) && Array.isArray(e2.leaders) && e2.leaders.every((e3) => typeof e3.uid == `string` && /^[1-9]\d*$/.test(e3.uid) && (e3.name === void 0 || typeof e3.name == `string`)) && new Set(e2.leaders.map((e3) => e3.uid)).size === e2.leaders.length && typeof e2.skipSoloLeader == `boolean` && typeof e2.skipKicked == `boolean`;
}
function ce(e2) {
  let t2 = Math.max(1, Math.floor(Number(e2.repeatCount) || 1)), n2 = Math.max(1, e2.maxLevel - e2.minLevel + 1), r2 = { ...e2 };
  delete r2.loop, delete r2.repeatCount, delete r2.idleTimeSeconds, e2.joinEnabled && (r2.joinRestrictions = oe(e2.joinRestrictions, e2.minMembers ?? 1), delete r2.minMembers);
  let i2 = e2.levelFilterEnabled ?? true;
  return { ...r2, distanceFilterEnabled: e2.distanceFilterEnabled === true, levelFilterEnabled: i2, progressiveLevels: i2 && (e2.progressiveLevels ?? e2.searchable), executionLimit: e2.executionLimit ?? (e2.loop ? 0 : t2 * (e2.searchable ? n2 : 1)), continuousAttack: e2.continuousAttack === true && e2.attackEnabled && !e2.rally };
}
function k(e2) {
  return e2.distanceFilterEnabled !== true || e2.maxDistance > 0;
}
function le(e2, t2) {
  let n2 = /* @__PURE__ */ new Set();
  return e2.map((e3) => {
    let n3 = e3.rally || e3.group === `drill` || e3.monsterType === 8 || e3.monsterType === 22, r2 = t2[e3.monsterNameKey], i2 = (r2 && r2 !== e3.monsterNameKey ? r2 : e3.name || e3.monsterNameKey || e3.key).replace(/\{\d+\}/g, ``).trim();
    return { key: e3.key, group: e3.group, name: i2, monsterNameKey: e3.monsterNameKey || void 0, monsterType: e3.monsterType, monsterIds: e3.monsterIds || [], monsterSpecial: e3.monsterSpecial, searchable: e3.searchable, source: e3.searchable ? `search` : `map`, action: n3 ? `rally` : `attack`, rally: n3, minLevel: e3.minLevel, maxLevel: e3.searchable ? e3.attackMaxLevel ?? e3.maxLevel : e3.maxLevel, attackMinLevel: e3.searchable ? e3.attackMinLevel ?? e3.minLevel : void 0, attackMaxLevel: e3.searchable ? e3.attackMaxLevel ?? e3.maxLevel : void 0 };
  }).filter((e3) => {
    if (e3.group !== `drill`) return true;
    let t3 = `${e3.monsterType}:${e3.monsterSpecial || 0}:${e3.monsterNameKey || ``}`;
    return n2.has(t3) ? false : (n2.add(t3), true);
  });
}
function ue(e2, t2) {
  if (t2.targetKey.startsWith(`query:`) || t2.targetNameQuery) return;
  let n2 = e2.find((e3) => e3.key === t2.targetKey);
  if (n2) return n2;
  let r2 = t2.action || (t2.rally ? `rally` : `attack`), i2 = e2.find((e3) => e3.monsterNameKey === t2.monsterNameKey && e3.monsterType === t2.monsterType && (e3.monsterSpecial || 0) === (t2.monsterSpecial || 0) && e3.rally === t2.rally && e3.action === r2);
  if (i2) return i2;
  if (t2.source === `undiscovered` || t2.targetKey.startsWith(`preset:`) || t2.targetKey.startsWith(`name:`)) return e2.find((e3) => e3.monsterNameKey === t2.monsterNameKey && e3.rally === t2.rally);
}
var A = t(), j = { enabled: false, recallOnDisable: true, squadPriority: [], targets: [] };
function M(e2) {
  return e2.kind === `allianceBuilding` ? `building:${e2.buildId}` : `ally:${e2.uid}`;
}
function de(e2, t2, n2) {
  if (t2 < 0 || n2 < 0 || t2 === n2) return e2;
  let r2 = [...e2], [i2] = r2.splice(t2, 1);
  return r2.splice(n2, 0, i2), r2;
}
function N(e2, t2) {
  if (!t2.length) return e2;
  let n2 = new Map(t2.map((e3) => [e3.uid, e3]));
  return { ...e2, targets: e2.targets.map((e3) => {
    if (e3.kind !== `allyCity`) return e3;
    let t3 = n2.get(e3.uid);
    return t3 ? { kind: `allyCity`, uid: t3.uid, nameSnapshot: t3.name || e3.nameSnapshot, serverIdSnapshot: t3.serverId, pointIdSnapshot: t3.pointId, ...t3.uuid ? { uuidSnapshot: t3.uuid, uuidUpdatedAt: t3.uuidUpdatedAt || 0 } : {} } : e3;
  }) };
}
function fe(e2, t2, n2) {
  let r2 = t2[e2.nameKey], i2 = r2 && r2 !== e2.nameKey ? r2 : e2.name && e2.name !== e2.nameKey ? e2.name : n2;
  return e2.allianceAbbr ? `[${e2.allianceAbbr}] ${i2}` : i2;
}
function pe({ online: t2, serverId: n2, config: r2, open: i2, onOpenChange: a2, onLog: o2 }) {
  let { language: s2, t: l2 } = d(), p2 = c(), h2 = e(`task:allianceGarrison`, { ...j, ...r2 }, { read: async () => ({ ...j, ...(await m(p2)).config?.tasks?.allianceGarrison }), write: async (e2) => (await S(e2, p2), e2), valid: (e2) => !e2.enabled || e2.targets.length > 0 && e2.squadPriority.length > 0 }, p2), g2 = h2.draft, [v2, x2] = (0, O.useState)(null), [E2, ee2] = (0, O.useState)(null), [D2, ie2] = (0, O.useState)([]), [ae2, oe2] = (0, O.useState)({}), [se2, ce2] = (0, O.useState)(false), [k2, le2] = (0, O.useState)(``), [ue2, pe2] = (0, O.useState)(/* @__PURE__ */ new Set()), [me2, he2] = (0, O.useState)(``), [ge2, P2] = (0, O.useState)(``), [_e2, ve2] = (0, O.useState)(``), [ye2, be2] = (0, O.useState)(null), F2 = (0, O.useCallback)(async () => {
    if (t2) try {
      let [e2, t3] = await Promise.all([te(), f()]), n3 = e2.buildings.map((e3) => e3.nameKey).filter(Boolean), r3 = n3.length ? await u(s2, n3) : {};
      x2(e2), ie2(t3.squads.map((e3) => e3.index).sort((e3, t4) => e3 - t4)), oe2(r3), P2(``);
    } catch (e2) {
      P2(_(l2, e2));
    }
  }, [s2, t2, l2]);
  (0, O.useEffect)(() => {
    if (!i2 || !t2) return;
    F2();
    let e2 = window.setInterval(() => {
      F2();
    }, 5e3);
    return () => window.clearInterval(e2);
  }, [i2, F2, n2]), (0, O.useEffect)(() => {
    let e2 = false, n3 = false, r3 = async () => {
      if (n3 || !t2) {
        !t2 && !e2 && ee2(null);
        return;
      }
      n3 = true;
      try {
        let t3 = await C();
        e2 || ee2(t3.tasks.allianceGarrison || null);
      } catch {
      } finally {
        n3 = false;
      }
    };
    r3();
    let i3 = window.setInterval(() => {
      r3();
    }, 2e3);
    return () => {
      e2 = true, window.clearInterval(i3);
    };
  }, [t2]);
  async function xe2(e2) {
    h2.state.edit(N(e2, v2?.allies || []), false), await h2.state.flush(), o2(`alliance garrison config saved`);
  }
  function Se2(e2) {
    h2.state.edit(N(e2, v2?.allies || []));
  }
  async function Ce2(e2) {
    if (e2 && (!g2.targets.length || !g2.squadPriority.length)) {
      P2(l2(g2.targets.length ? `garrison.squadRequired` : `garrison.targetRequired`));
      return;
    }
    try {
      !e2 && t2 ? (he2(`stop`), await h2.state.runAction(() => re(`allianceGarrison`, { recallOwned: g2.recallOnDisable !== false }, p2), (e3) => ({ ...e3, enabled: false }))) : await xe2({ ...h2.state.getSnapshot().draft, enabled: e2 }), P2(``);
    } catch (e3) {
      P2(_(l2, e3));
    } finally {
      he2(``);
    }
  }
  async function we2() {
    if (!g2.targets.length || !g2.squadPriority.length) {
      P2(l2(g2.targets.length ? `garrison.squadRequired` : `garrison.targetRequired`));
      return;
    }
    he2(`run`);
    try {
      h2.state.edit((e2) => ({ ...e2, enabled: true }), false), await h2.state.runAction(() => b(`allianceGarrison`, {}, p2));
    } catch (e2) {
      P2(_(l2, e2));
    } finally {
      he2(``);
    }
  }
  let Te2 = v2?.buildings || [], I2 = v2?.allies || [], Ee2 = (0, O.useMemo)(() => new Map(Te2.map((e2) => [e2.buildId, e2])), [Te2]), De2 = (0, O.useMemo)(() => new Map(I2.map((e2) => [e2.uid, e2])), [I2]), L2 = (0, O.useMemo)(() => new Set(g2.targets.map(M)), [g2.targets]), Oe2 = (0, O.useMemo)(() => new Set(g2.squadPriority), [g2.squadPriority]), R2 = [...g2.squadPriority, ...D2.filter((e2) => !Oe2.has(e2))], ke2 = I2.filter((e2) => e2.name.toLowerCase().includes(k2.trim().toLowerCase()));
  function z2(e2) {
    return fe(e2, ae2, l2(e2.role === `center` ? `garrison.center` : `garrison.attachment`));
  }
  function Ae2(e2) {
    if (e2.kind === `allianceBuilding`) {
      let t3 = Ee2.get(e2.buildId);
      return t3 ? z2(t3) : e2.nameSnapshot;
    }
    return De2.get(e2.uid)?.name || e2.nameSnapshot;
  }
  function je2(e2) {
    let t3 = e2.key, n3 = L2.has(t3) ? g2.targets.filter((e3) => M(e3) !== t3) : [...g2.targets, { kind: `allianceBuilding`, buildId: e2.buildId, nameSnapshot: z2(e2) }];
    Se2({ ...g2, targets: n3 });
  }
  function Me2() {
    pe2(new Set(g2.targets.filter((e2) => e2.kind === `allyCity`).map((e2) => e2.uid))), ce2(true);
  }
  function Ne2() {
    let e2 = g2.targets.filter((e3) => e3.kind !== `allyCity` || ue2.has(e3.uid)), t3 = new Set(e2.filter((e3) => e3.kind === `allyCity`).map((e3) => e3.uid)), n3 = I2.filter((e3) => ue2.has(e3.uid) && !t3.has(e3.uid)).map((e3) => ({ kind: `allyCity`, uid: e3.uid, nameSnapshot: e3.name }));
    Se2({ ...g2, targets: [...e2, ...n3] }), ce2(false);
  }
  function Pe2(e2) {
    let t3 = Oe2.has(e2) ? g2.squadPriority.filter((t4) => t4 !== e2) : [...g2.squadPriority, e2];
    if (g2.enabled && t3.length === 0) {
      P2(l2(`garrison.squadRequired`));
      return;
    }
    Se2({ ...g2, squadPriority: t3 });
  }
  function B2(e2) {
    let t3 = g2.targets.findIndex((e3) => M(e3) === _e2), n3 = g2.targets.findIndex((t4) => M(t4) === M(e2)), r3 = de(g2.targets, t3, n3);
    r3 !== g2.targets && Se2({ ...g2, targets: r3 }), ve2(``);
  }
  function Fe2(e2) {
    if (ye2 == null) return;
    let t3 = de(g2.squadPriority, g2.squadPriority.indexOf(ye2), g2.squadPriority.indexOf(e2));
    t3 !== g2.squadPriority && Se2({ ...g2, squadPriority: t3 }), be2(null);
  }
  let Ie2 = E2?.guardingCount || 0, V2 = v2 ? g2.targets.filter((e2) => e2.kind === `allianceBuilding` ? Ee2.get(e2.buildId)?.available === true : De2.get(e2.uid)?.available === true).length : g2.targets.length, H2 = Math.min(g2.squadPriority.length, V2), U2 = g2.enabled ? l2(`garrison.summary`, { active: Ie2, total: H2 }) : l2(`common.disabled`);
  return (0, A.jsxs)(A.Fragment, { children: [(0, A.jsx)(`article`, { className: `automation-card monster-afk-compact-card is-selectable${i2 ? ` is-selected` : ``}`, title: l2(`garrison.description`), role: `button`, tabIndex: 0, onClick: a2, onKeyDown: (e2) => {
    (e2.key === `Enter` || e2.key === ` `) && (e2.preventDefault(), a2());
  }, children: (0, A.jsxs)(`div`, { className: `automation-card-header`, children: [(0, A.jsxs)(`div`, { children: [(0, A.jsx)(`h3`, { children: l2(`garrison.title`) }), (0, A.jsx)(`p`, { children: U2 })] }), (0, A.jsxs)(`div`, { className: `monster-afk-compact-actions`, children: [(0, A.jsx)(`button`, { type: `button`, className: i2 ? `active` : ``, "aria-label": l2(`nav.settings`), "aria-pressed": i2, onClick: (e2) => {
    e2.stopPropagation(), a2();
  }, children: (0, A.jsx)(T, { name: `settings` }) }), (0, A.jsxs)(`label`, { className: `monster-afk-compact-toggle ${g2.enabled ? `is-enabled` : ``}`, onMouseDown: (e2) => {
    e2.preventDefault(), e2.stopPropagation();
  }, onClick: (e2) => e2.stopPropagation(), children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: g2.enabled, disabled: !!me2, onChange: (e2) => void Ce2(e2.target.checked) }), (0, A.jsx)(`span`, { className: `monster-afk-master-track` })] })] })] }) }), i2 ? (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings garrison-settings`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: [(0, A.jsxs)(`div`, { children: [(0, A.jsx)(`strong`, { children: l2(`garrison.settings`) }), (0, A.jsx)(`span`, { children: l2(`garrison.settingsDescription`) })] }), (0, A.jsx)(`span`, { className: `garrison-count`, children: l2(`garrison.selectedTargets`, { count: g2.targets.length }) })] }), (0, A.jsxs)(`div`, { className: `garrison-settings-grid`, children: [(0, A.jsxs)(`div`, { className: `garrison-target-panel`, children: [(0, A.jsxs)(`div`, { className: `garrison-section-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.buildings`) }), (0, A.jsxs)(`span`, { children: [Te2.filter((e2) => L2.has(e2.key)).length, `/`, Te2.length] })] }), (0, A.jsxs)(`div`, { className: `garrison-building-grid`, children: [Te2.map((e2) => (0, A.jsxs)(`label`, { className: `garrison-building ${e2.role}${e2.available ? `` : ` unavailable`}`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.has(e2.key), disabled: !e2.available, onChange: () => je2(e2) }), (0, A.jsxs)(`span`, { children: [z2(e2), (0, A.jsx)(`small`, { children: e2.unavailableReason === `season_settled` ? l2(`garrison.seasonEnded`) : e2.full ? l2(`garrison.full`) : l2(e2.role === `center` ? `garrison.center` : `garrison.attachment`) })] })] }, e2.key)), Te2.length ? null : (0, A.jsx)(`p`, { className: `muted`, children: l2(t2 ? `garrison.noBuildings` : `garrison.offline`) })] }), (0, A.jsxs)(`div`, { className: `garrison-section-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.allies`) }), (0, A.jsx)(`button`, { type: `button`, onClick: Me2, disabled: !t2, children: l2(`garrison.chooseAllies`) })] }), (0, A.jsxs)(`div`, { className: `garrison-selected-allies`, children: [g2.targets.filter((e2) => e2.kind === `allyCity`).map((e2) => {
    let t3 = De2.get(e2.uid);
    return (0, A.jsx)(`span`, { className: t3?.available ? `` : `unavailable`, children: t3?.name || e2.nameSnapshot }, e2.uid);
  }), g2.targets.some((e2) => e2.kind === `allyCity`) ? null : (0, A.jsx)(`span`, { className: `muted`, children: l2(`garrison.noSelectedAllies`) })] }), (0, A.jsxs)(`div`, { className: `garrison-section-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.targetPriority`) }), (0, A.jsx)(`span`, { children: l2(`garrison.dragHint`) })] }), (0, A.jsxs)(`div`, { className: `garrison-priority-list`, children: [g2.targets.map((e2, t3) => (0, A.jsxs)(`div`, { className: `garrison-priority-item`, draggable: true, onDragStart: () => ve2(M(e2)), onDragOver: (e3) => e3.preventDefault(), onDrop: () => B2(e2), onDragEnd: () => ve2(``), children: [(0, A.jsx)(`span`, { children: t3 + 1 }), (0, A.jsx)(`strong`, { children: Ae2(e2) }), (0, A.jsx)(`i`, { children: (0, A.jsx)(T, { name: `drag` }) })] }, M(e2))), g2.targets.length ? null : (0, A.jsx)(`p`, { className: `muted`, children: l2(`garrison.targetRequired`) })] })] }), (0, A.jsxs)(`div`, { className: `garrison-runtime-panel`, children: [(0, A.jsxs)(`div`, { className: `garrison-section-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.squadPriority`) }), (0, A.jsx)(`span`, { children: g2.squadPriority.join(` \u2192 `) || `-` })] }), (0, A.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, children: R2.map((e2) => {
    let t3 = Oe2.has(e2);
    return (0, A.jsxs)(`div`, { className: `automation-squad-priority-item${t3 ? ` selected` : ``}`, draggable: t3, onDragStart: () => t3 && be2(e2), onDragOver: (e3) => t3 && e3.preventDefault(), onDrop: () => t3 && Fe2(e2), onDragEnd: () => be2(null), children: [(0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: t3, onChange: () => Pe2(e2) }), (0, A.jsx)(`span`, { children: l2(`squad.number`, { number: e2 }) })] }), t3 ? (0, A.jsx)(`span`, { className: `automation-squad-drag-handle`, children: (0, A.jsx)(T, { name: `drag` }) }) : null] }, e2);
  }) }), (0, A.jsx)(`p`, { className: `muted`, children: l2(`garrison.squadHint`) }), (0, A.jsxs)(`div`, { className: `garrison-section-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.current`) }), (0, A.jsxs)(`span`, { children: [Ie2, `/`, H2] })] }), (0, A.jsxs)(`div`, { className: `garrison-assignment-list`, children: [(E2?.assignments || []).map((e2) => (0, A.jsxs)(`div`, { className: `garrison-assignment`, children: [(0, A.jsx)(`b`, { children: e2.squadIndex }), (0, A.jsxs)(`span`, { children: [(0, A.jsx)(`strong`, { children: e2.targetName }), (0, A.jsx)(`small`, { children: l2(`garrison.state.${e2.state}`) })] }), (0, A.jsx)(`em`, { className: e2.state, children: l2(e2.owned ? `garrison.automatic` : `garrison.manual`) })] }, `${e2.squadIndex}:${e2.targetKey}`)), E2?.assignments?.length ? null : (0, A.jsx)(`p`, { className: `muted`, children: l2(`garrison.noAssignments`) })] })] })] }), (0, A.jsx)(ne, { state: h2.state, label: l2(`garrison.title`) }), ge2 ? (0, A.jsx)(`p`, { className: `inline-error`, children: ge2 }) : null, (0, A.jsxs)(`div`, { className: `garrison-actions`, children: [(0, A.jsx)(w, { label: `garrison.recallOnDisable`, checked: g2.recallOnDisable !== false, disabled: !!me2, onChange: (e2) => Se2({ ...g2, recallOnDisable: e2 }) }), (0, A.jsx)(`button`, { type: `button`, className: `primary-action`, disabled: !!me2, onClick: () => void we2(), children: l2(`garrison.runNow`) })] })] }) : null, se2 ? (0, A.jsx)(y, { className: `garrison-modal-backdrop`, label: l2(`garrison.chooseAllies`), onClose: () => ce2(false), children: (0, A.jsxs)(`section`, { className: `garrison-modal`, children: [(0, A.jsxs)(`div`, { className: `garrison-modal-heading`, children: [(0, A.jsx)(`strong`, { children: l2(`garrison.chooseAllies`) }), (0, A.jsx)(`button`, { type: `button`, onClick: () => ce2(false), children: l2(`common.cancel`) })] }), (0, A.jsx)(`input`, { "aria-label": l2(`garrison.searchAlly`), value: k2, onChange: (e2) => le2(e2.target.value), placeholder: l2(`garrison.searchAlly`), autoFocus: true }), (0, A.jsx)(`div`, { className: `garrison-ally-list`, children: ke2.map((e2) => (0, A.jsxs)(`label`, { className: e2.available ? `` : `unavailable`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: ue2.has(e2.uid), disabled: !e2.available, onChange: (t3) => pe2((n3) => {
    let r3 = new Set(n3);
    return t3.target.checked ? r3.add(e2.uid) : r3.delete(e2.uid), r3;
  }) }), (0, A.jsxs)(`span`, { children: [(0, A.jsx)(`strong`, { children: e2.name }), (0, A.jsx)(`small`, { children: l2(`garrison.allyDetail`, { level: e2.level, power: Math.round(e2.power / 1e4) }) })] }), (0, A.jsx)(`em`, { children: e2.available ? l2(e2.online ? `garrison.online` : `garrison.offlineMember`) : l2(e2.unavailableReason === `cross_server` ? `garrison.crossServer` : `garrison.targetLoading`) })] }, e2.uid)) }), (0, A.jsx)(`div`, { className: `garrison-modal-actions`, children: (0, A.jsx)(`button`, { type: `button`, className: `primary-action`, onClick: Ne2, children: l2(`common.completed`) }) })] }) }) : null] });
}
function me({ rows: e2, names: t2 }) {
  let { t: n2 } = d();
  return (0, A.jsx)(`div`, { className: `zombie-bus-assignments`, children: (0, A.jsxs)(`table`, { children: [(0, A.jsx)(`thead`, { children: (0, A.jsx)(`tr`, { children: [`squad`, `ally`, `bus`, `status`].map((e3) => (0, A.jsx)(`th`, { children: n2(`zombieBus.${e3}`) }, e3)) }) }), (0, A.jsx)(`tbody`, { children: e2.map((e3) => (0, A.jsxs)(`tr`, { children: [(0, A.jsx)(`td`, { children: e3.squadIndex }), (0, A.jsx)(`td`, { children: e3.name || e3.uid }), (0, A.jsx)(`td`, { children: e3.nameKey && t2[e3.nameKey] && t2[e3.nameKey] !== e3.nameKey ? t2[e3.nameKey] : n2(e3.gold === true ? `zombieBus.gold` : e3.gold === false ? `zombieBus.normal` : `zombieBus.unknown`) }), (0, A.jsx)(`td`, { children: n2(`zombieBus.state.${e3.state}`) })] }, e3.squadIndex)) })] }) });
}
function he({ online: t2, config: n2, open: r2, onOpenChange: i2, onLog: a2 }) {
  let { language: o2, t: s2 } = d(), l2 = c(), f2 = e(`task:zombieBus`, { enabled: n2?.enabled === true }, { read: async () => ({ enabled: (await m(l2)).config?.tasks?.zombieBus?.enabled === true }), write: async (e2) => (await D(`zombieBus`, e2, l2), e2) }, l2), [p2, h2] = (0, O.useState)(null), [g2, _2] = (0, O.useState)({}), v2 = p2?.busAssignments || [], y2 = v2.some((e2) => [`sending`, `marching`, `guarding`, `recalling`, `returning`].includes(e2.state)), b2 = [...new Set(v2.map((e2) => e2.nameKey).filter((e2) => !!e2))].sort().join(`|`);
  (0, O.useEffect)(() => {
    let e2 = false, n3 = false;
    h2(null);
    let r3 = async () => {
      if (!(!t2 || n3)) {
        n3 = true;
        try {
          let t3 = await C(l2);
          e2 || h2(t3.tasks.zombieBus || null);
        } finally {
          n3 = false;
        }
      }
    };
    r3().catch(() => void 0);
    let i3 = window.setInterval(() => {
      r3().catch(() => void 0);
    }, 1e3);
    return () => {
      e2 = true, window.clearInterval(i3);
    };
  }, [t2, l2]), (0, O.useEffect)(() => {
    let e2 = false;
    return _2({}), t2 && b2 && u(o2, b2.split(`|`)).then((t3) => {
      e2 || _2(t3);
    }).catch(() => void 0), () => {
      e2 = true;
    };
  }, [t2, l2, o2, b2]);
  let x2 = t2 ? p2?.lastError ? `common.failed` : y2 ? `automation.running` : f2.draft.enabled ? `zombieBus.waiting` : `common.disabled` : `status.disconnected`;
  return (0, A.jsxs)(A.Fragment, { children: [(0, A.jsx)(ie, { title: s2(`zombieBus.title`), description: s2(`zombieBus.description`), summary: s2(x2), enabled: f2.draft.enabled, disabled: !t2, settingsOpen: r2, cardSelectable: true, onSettings: i2, settingsLabel: s2(`nav.settings`), onToggle: (e2) => {
    f2.state.edit({ enabled: e2 }, false), f2.state.flush().catch(() => a2(s2(`configSave.failed`)));
  } }), r2 && (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings`, children: [(0, A.jsx)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: (0, A.jsx)(`strong`, { children: s2(`zombieBus.title`) }) }), (0, A.jsx)(`p`, { className: `muted`, children: s2(`zombieBus.description`) }), v2.length > 0 && (0, A.jsx)(me, { rows: v2, names: g2 })] }), (f2.error || p2?.lastError) && (0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings`, children: [(0, A.jsx)(ne, { state: f2.state, disabled: !t2 }), p2?.lastError && (0, A.jsx)(`p`, { className: `automation-error`, role: `alert`, children: s2(p2.lastError) })] })] });
}
var ge = { waiting_join_delay: `squad.join.waiting`, waiting_nearby_target: `squad.afkStep.waitingNearbyTarget`, scanning_nearby: `squad.afkStep.scanningNearby`, scanning_next_target: `squad.afkStep.scanningNextTarget`, scanning_mine_route: `squad.afkStep.scanningMineRoute`, marching_via_mine: `squad.afkStep.marchingViaMine`, redirecting_to_monster: `squad.afkStep.redirectingToMonster`, recovering_from_mine: `squad.afkStep.recoveringFromMine`, scanning_return_mine: `squad.afkStep.scanningReturnMine`, boosting_return: `squad.afkStep.boostingReturn`, returning: `squad.afkStep.returning` };
function P(e2) {
  return e2 === 8 || e2 === 22;
}
var _e = [`normal`, `elite`, `running`, `leader`, `ally`, `drill`, `invader`, `other`], ve = { enabled: false, activeRally: false, squadIndexes: [1] };
function ye({ value: e2, onChange: t2, online: n2, disabled: r2 = false, label: i2 = `squad.afkJoinConditions` }) {
  let { t: o2 } = d(), s2 = c(), [l2, u2] = (0, O.useState)(null), [f2, p2] = (0, O.useState)(false), [m2, h2] = (0, O.useState)(``), [g2, _2] = (0, O.useState)(/* @__PURE__ */ new Set()), [v2, b2] = (0, O.useState)(false);
  (0, O.useEffect)(() => {
    if (u2(null), p2(false), b2(false), !n2 || !e2.enabled || e2.leaderListMode === `off`) return;
    let t3 = false, r3 = false, i3 = async () => {
      if (!r3) {
        r3 = true;
        try {
          let e3 = await a(s2);
          t3 || (u2({ profileId: s2, data: e3 }), b2(false));
        } catch {
          t3 || (u2(null), b2(true));
        } finally {
          r3 = false;
        }
      }
    };
    i3();
    let o3 = window.setInterval(() => {
      i3();
    }, 5e3);
    return () => {
      t3 = true, window.clearInterval(o3);
    };
  }, [n2, s2, e2.enabled, e2.leaderListMode]);
  let x2 = n2 && l2?.profileId === s2 ? l2.data : null, S2 = x2?.membersReady ? x2.members.filter((e3) => e3.uid !== x2.selfUid) : [], C2 = new Map((x2?.membersReady ? x2.members : []).map((e3) => [e3.uid, e3])), T2 = e2.leaders.filter((e3) => !C2.has(e3.uid)), E2 = m2.trim().toLowerCase(), ee2 = S2.filter((e3) => e3.name.toLowerCase().includes(E2) || e3.uid.includes(E2));
  return (0, A.jsxs)(`section`, { className: `rally-join-settings`, children: [(0, A.jsx)(w, { label: i2, checked: e2.enabled, disabled: r2, onChange: (n3) => t2({ ...e2, enabled: n3 }) }), e2.enabled && (0, A.jsxs)(`fieldset`, { disabled: r2, children: [(0, A.jsxs)(`div`, { className: `monster-afk-basic-grid`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-field-wide`, children: [o2(`squad.join.mode`), (0, A.jsxs)(`select`, { "aria-label": o2(`squad.join.mode`), value: e2.mode, onChange: (n3) => t2({ ...e2, mode: n3.target.value }), children: [(0, A.jsx)(`option`, { value: `slot`, children: o2(`squad.join.mode.slot`) }), (0, A.jsx)(`option`, { value: `delay`, children: o2(`squad.join.mode.delay`) })] })] }), [e2.mode === `delay` ? `delaySeconds` : `slotRange`].map((n3) => (0, A.jsx)(`div`, { className: `rally-join-range`, children: [0, 1].map((r3) => (0, A.jsxs)(`label`, { children: [o2(`squad.join.${n3}.${r3 === 0 ? `min` : `max`}`), (0, A.jsx)(`input`, { type: `number`, min: n3 === `slotRange` ? 2 : 0, max: n3 === `slotRange` ? 5 : 6e3, step: n3 === `slotRange` ? 1 : 0.01, value: Number.isNaN(e2[n3][r3]) ? `` : e2[n3][r3], onChange: (i3) => {
    let a2 = [...e2[n3]], o3 = i3.target.valueAsNumber;
    a2[r3] = n3 === `slotRange` ? Math.min(5, Math.max(2, Number.isFinite(o3) ? Math.trunc(o3) : 2)) : o3, t2({ ...e2, [n3]: a2 });
  } })] }, r3)) }, n3)), e2.mode === `slot` && (0, A.jsxs)(`label`, { children: [o2(`squad.join.slotDelay`), (0, A.jsx)(`input`, { type: `number`, min: 0, max: 6e3, step: 0.01, value: Number.isNaN(e2.slotDelaySeconds) ? `` : e2.slotDelaySeconds, onChange: (n3) => t2({ ...e2, slotDelaySeconds: n3.target.valueAsNumber }) })] }), (0, A.jsxs)(`label`, { children: [o2(`squad.join.maxWait`), (0, A.jsx)(`input`, { type: `number`, min: 0, step: 1, value: Number.isNaN(e2.maxWaitMinutes) ? `` : e2.maxWaitMinutes, onChange: (n3) => t2({ ...e2, maxWaitMinutes: n3.target.valueAsNumber }) })] }), (0, A.jsxs)(`label`, { children: [o2(`squad.join.list`), (0, A.jsx)(`select`, { "aria-label": o2(`squad.join.list`), value: e2.leaderListMode, onChange: (n3) => t2({ ...e2, leaderListMode: n3.target.value }), children: [`off`, `blacklist`, `whitelist`].map((e3) => (0, A.jsx)(`option`, { value: e3, children: o2(`squad.join.list.${e3}`) }, e3)) })] })] }), (0, A.jsx)(`p`, { className: `muted`, children: o2(`squad.join.hint`) }), (0, A.jsx)(`p`, { className: `muted`, children: o2(`squad.join.hint.${e2.mode}`) }), e2.leaderListMode !== `off` && (0, A.jsxs)(`div`, { className: `rally-join-leaders`, children: [(0, A.jsx)(`button`, { type: `button`, disabled: !x2?.membersReady, onClick: () => {
    _2(new Set(e2.leaders.map((e3) => e3.uid))), h2(``), p2(true);
  }, children: o2(`squad.join.chooseMembers`) }), !x2?.membersReady && (0, A.jsx)(`span`, { className: `muted`, children: o2(n2 ? v2 ? `squad.join.membersFailed` : `squad.join.membersLoading` : `squad.join.membersOffline`) }), e2.leaders.map((n3, r3) => (0, A.jsxs)(`div`, { className: `rally-join-leader`, children: [(0, A.jsxs)(`span`, { className: `rally-join-member-name`, children: [C2.get(n3.uid)?.name || n3.name || n3.uid, (0, A.jsxs)(`small`, { children: [n3.uid, x2?.membersReady && !C2.has(n3.uid) ? ` \xB7 ${o2(`squad.join.memberLeft`)}` : ``] })] }), (0, A.jsx)(`button`, { type: `button`, className: `danger`, onClick: () => t2({ ...e2, leaders: e2.leaders.filter((e3, t3) => t3 !== r3) }), children: o2(`common.delete`) })] }, n3.uid)), x2?.membersReady && T2.length > 0 && (0, A.jsx)(`span`, { className: `muted`, children: o2(`squad.join.memberLeftHint`) }), e2.leaderListMode === `whitelist` && !e2.leaders.length && (0, A.jsx)(`span`, { className: `muted`, children: o2(`squad.join.emptyWhitelist`) })] }), (0, A.jsx)(w, { label: `squad.join.skipSolo`, checked: e2.skipSoloLeader, onChange: (n3) => t2({ ...e2, skipSoloLeader: n3 }) }), (0, A.jsx)(w, { label: `squad.join.skipKicked`, checked: e2.skipKicked, onChange: (n3) => t2({ ...e2, skipKicked: n3 }) }), !se(e2) && (0, A.jsx)(`p`, { role: `alert`, className: `status-error`, children: o2(`squad.join.invalid`) })] }), e2.enabled && f2 && (0, A.jsx)(y, { className: `garrison-modal-backdrop`, label: o2(`squad.join.chooseMembers`), onClose: () => p2(false), children: (0, A.jsxs)(`section`, { className: `garrison-modal`, children: [(0, A.jsxs)(`div`, { className: `garrison-modal-heading`, children: [(0, A.jsx)(`strong`, { children: o2(`squad.join.chooseMembers`) }), (0, A.jsx)(`button`, { type: `button`, onClick: () => p2(false), children: o2(`common.cancel`) })] }), (0, A.jsx)(`input`, { "aria-label": o2(`squad.join.searchMembers`), placeholder: o2(`squad.join.searchMembers`), value: m2, onChange: (e3) => h2(e3.target.value), autoFocus: true }), (0, A.jsxs)(`div`, { className: `garrison-ally-list`, children: [ee2.map((e3) => (0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: g2.has(e3.uid), onChange: (t3) => _2((n3) => {
    let r3 = new Set(n3);
    return t3.target.checked ? r3.add(e3.uid) : r3.delete(e3.uid), r3;
  }) }), (0, A.jsxs)(`span`, { children: [(0, A.jsx)(`strong`, { children: e3.name || e3.uid }), (0, A.jsx)(`small`, { children: e3.uid })] })] }, e3.uid)), !ee2.length && (0, A.jsx)(`span`, { className: `muted`, children: o2(`squad.join.noMembers`) })] }), (0, A.jsx)(`div`, { className: `garrison-modal-actions`, children: (0, A.jsx)(`button`, { type: `button`, className: `primary-action`, disabled: !x2?.membersReady || r2, onClick: () => {
    if (!x2?.membersReady) return;
    let n3 = e2.leaders.filter((e3) => !C2.has(e3.uid) || e3.uid === x2.selfUid);
    t2({ ...e2, leaders: [...n3, ...S2.filter((e3) => g2.has(e3.uid)).map((e3) => ({ uid: e3.uid, name: e3.name }))] }), p2(false);
  }, children: o2(`squad.join.confirmMembers`) }) })] }) })] });
}
function be(e2, t2, n2) {
  let r2 = e2.indexOf(t2), i2 = e2.indexOf(n2);
  if (r2 < 0 || i2 < 0 || r2 === i2) return e2;
  let a2 = [...e2], [o2] = a2.splice(r2, 1);
  return a2.splice(i2, 0, o2), a2;
}
function F(e2, t2 = e2.kind) {
  let n2 = e2.source || (e2.searchable ? `search` : e2.monsterIds.length ? `map` : `undiscovered`), r2 = t2 === `join` || e2.rally || P(e2.monsterType);
  return { ...ce(e2), kind: t2, source: n2, rally: r2, action: r2 ? `rally` : `attack`, attackEnabled: t2 === `farm`, joinEnabled: t2 === `join`, continuousAttack: t2 === `farm` && e2.continuousAttack === true && !r2, continuousJoin: t2 === `join` && e2.continuousJoin !== false, progressiveLevels: t2 === `farm` && e2.progressiveLevels === true, enabled: e2.enabled !== false, squadIndexes: [...new Set(e2.squadIndexes || [])].sort((e3, t3) => e3 - t3) };
}
function xe(e2) {
  let t2 = { enabled: e2?.enabled === true, allianceDrill: { enabled: e2?.allianceDrill?.enabled === true, activeRally: e2?.allianceDrill?.activeRally === true, squadIndexes: [...new Set(e2?.allianceDrill?.squadIndexes || ve.squadIndexes)], joinRestrictions: oe(e2?.allianceDrill?.joinRestrictions, 1, !e2?.allianceDrill) } };
  if (e2?.strategies) return { ...t2, strategies: e2.strategies.map((e3) => F(e3)) };
  if (e2?.farmStrategies || e2?.joinStrategies) return { ...t2, strategies: [...(e2.farmStrategies || []).map((e3) => F(e3, `farm`)), ...(e2.joinStrategies || []).map((e3) => F(e3, `join`))] };
  let n2 = e2?.strategies?.length ? e2.strategies : (e2?.profiles || []).map((t3) => {
    let n3 = (e2?.assignments || []).filter((e3) => e3.profileId === t3.id).map((e3) => e3.squadIndex);
    return { ...t3, enabled: n3.length > 0, squadIndexes: n3 };
  });
  return { ...t2, strategies: n2.flatMap((e3) => {
    let t3 = [];
    return e3.attackEnabled && t3.push(F(e3, `farm`)), e3.joinEnabled && t3.push(F({ ...e3, id: e3.attackEnabled ? `${e3.id}-join` : e3.id }, `join`)), t3;
  }) };
}
function Se(e2, t2) {
  return { enabled: e2?.enabled ?? t2?.autoUsePotion === true, minStamina: e2?.minStamina ?? t2?.minStamina ?? 50, preferFifty: e2?.preferFifty ?? t2?.preferFifty !== false };
}
function Ce(e2, t2, n2 = `farm`) {
  return { id: `monster-afk-${n2}-${crypto.randomUUID()}`, name: e2.name, targetKey: e2.key, targetNameQuery: void 0, monsterNameKey: e2.monsterNameKey, monsterType: e2.monsterType, monsterIds: e2.monsterIds, monsterSpecial: e2.monsterSpecial, source: e2.source, action: e2.action, searchable: e2.searchable, rally: e2.rally, minLevel: e2.minLevel, maxLevel: e2.maxLevel, maxDistance: 200, distanceFilterEnabled: false, levelFilterEnabled: false, progressiveLevels: false, executionLimit: 0, attackEnabled: n2 === `farm`, continuousAttack: n2 === `farm` && !e2.rally, joinEnabled: n2 === `join`, continuousJoin: n2 === `join`, ...n2 === `join` ? { joinRestrictions: oe(void 0, 1, true) } : {}, enabled: true, kind: n2, squadIndexes: t2.slice(0, 1) };
}
function we(e2, t2) {
  return e2.levelFilterEnabled && t2?.attackMinLevel != null && t2.attackMaxLevel != null && (e2.progressiveLevels ? e2.minLevel < t2.attackMinLevel || e2.minLevel > t2.attackMaxLevel : e2.minLevel < t2.attackMinLevel || e2.maxLevel > t2.attackMaxLevel);
}
function Te(e2) {
  return !!(![e2.executionLimit, e2.minMembers, e2.minLevel, e2.maxLevel, e2.maxDistance].some((e3) => typeof e3 == `number` && !Number.isFinite(e3)) && e2.name.trim() && e2.targetKey.trim() && (!e2.targetKey.startsWith(`query:`) || e2.targetNameQuery?.trim()) && Number.isInteger(e2.monsterType) && e2.monsterType >= 0 && (!e2.levelFilterEnabled || e2.minLevel > 0 && (e2.progressiveLevels || e2.maxLevel >= e2.minLevel)) && k(e2) && Number.isInteger(e2.executionLimit) && e2.executionLimit >= 0 && (e2.kind !== `join` || se(oe(e2.joinRestrictions, e2.minMembers ?? 1))) && e2.squadIndexes.length > 0);
}
function I({ online: t2, serverId: n2, config: r2, staminaPotionConfig: i2, garrisonConfig: a2, zombieBusConfig: o2, onLog: s2 }) {
  let { language: l2, t: h2 } = d(), g2 = c(), y2 = e(`task:monsterSweep`, xe(r2), { read: async () => xe((await m(g2)).config?.tasks?.monsterSweep), write: async (e2) => xe(await v(e2, g2)), valid: (e2) => e2.strategies.every(Te) && se(oe(e2.allianceDrill.joinRestrictions)) }, g2), b2 = e(`task:staminaPotion`, Se(i2, r2), { read: async () => Se((await m(g2)).config?.tasks?.staminaPotion), write: async (e2) => (await D(`staminaPotion`, e2, g2), e2), valid: (e2) => Number.isInteger(e2.minStamina) && e2.minStamina >= 0 && e2.minStamina <= 9999 }, g2), { enabled: x2, strategies: S2, allianceDrill: E2 } = y2.draft, { enabled: ee2, minStamina: te2, preferFifty: re2 } = b2.draft, ae2 = (e2) => b2.state.edit((t3) => ({ ...t3, minStamina: e2 })), ce2 = (e2) => b2.state.edit((t3) => ({ ...t3, preferFifty: e2 })), [k2, j2] = (0, O.useState)(null), [M2, de2] = (0, O.useState)([]), [N2, fe2] = (0, O.useState)(false), me2 = (0, O.useRef)(false), P2 = (e2) => {
    me2.current = e2;
  }, [ve2, F2] = (0, O.useState)(false), [I2, Ee2] = (0, O.useState)(S2[0]?.id ?? null), De2 = (0, O.useRef)(I2);
  De2.current = I2;
  let L2 = S2.find((e2) => e2.id === I2) ?? null, Oe2 = !!L2?.targetKey.startsWith(`query:`);
  function R2(e2) {
    let t3 = y2.state.getSnapshot().draft.strategies.find((e3) => e3.id === De2.current) ?? null, n3 = typeof e2 == `function` ? e2(t3) : e2;
    De2.current = n3?.id ?? null, Ee2(n3?.id ?? null), n3 && y2.state.edit((e3) => ({ ...e3, strategies: e3.strategies.some((e4) => e4.id === n3.id) ? e3.strategies.map((e4) => e4.id === n3.id ? n3 : e4) : [...e3.strategies, n3] }), me2.current ? false : 400);
  }
  let [ke2, z2] = (0, O.useState)([]), [Ae2, je2] = (0, O.useState)({}), Me2 = [...new Set(ke2.map((e2) => e2.joinTargetNameKey).filter((e2) => !!e2))].sort().join(`
`);
  (0, O.useEffect)(() => {
    let e2 = false;
    return je2({}), Me2 && u(l2, Me2.split(`
`)).then((t3) => {
      e2 || je2(t3);
    }).catch(() => void 0), () => {
      e2 = true;
    };
  }, [Me2, l2, g2]);
  let [Ne2, Pe2] = (0, O.useState)([]), [B2, Fe2] = (0, O.useState)(``), [Ie2, V2] = (0, O.useState)(``), H2 = (0, O.useRef)(null), U2 = (0, O.useRef)(null), Le2 = (0, O.useRef)(``), [Re2, ze2] = (0, O.useState)(null), [Be2, Ve2] = (0, O.useState)(null), [He2, Ue2] = (0, O.useState)(``), We2 = !!(L2 && !y2.confirmed.strategies.some((e2) => e2.id === L2.id)), Ge2 = L2?.kind || `farm`;
  (0, O.useEffect)(() => {
    I2 && !S2.some((e2) => e2.id === I2) && Ee2(S2[0]?.id ?? null);
  }, [S2, I2]), (0, O.useEffect)(() => () => {
    y2.state.flush().catch(() => void 0), b2.state.flush().catch(() => void 0);
  }, [y2.state, b2.state]), (0, O.useEffect)(() => {
    if (!t2) {
      fe2(false);
      return;
    }
    fe2(false), Promise.all([p(), f().catch(() => ({ squads: [] }))]).then(async ([e2, t3]) => {
      Pe2((t3.squads || []).map((e3) => e3.index).sort((e3, t4) => e3 - t4));
      let n3 = e2.options.filter((e3) => e3.group !== `drill`), r3 = n3.map((e3) => e3.monsterNameKey).filter(Boolean), i3 = r3.length ? await u(l2, r3) : {};
      de2(le(n3, i3)), fe2(true), V2(``);
    }).catch(() => V2(h2(`common.actionFailed`)));
  }, [l2, t2, n2, h2]), (0, O.useEffect)(() => {
    let e2 = false, n3 = false, r3 = async () => {
      if (!n3) {
        if (!t2) {
          z2([]);
          return;
        }
        n3 = true;
        try {
          let t3 = await C();
          e2 || z2(t3.tasks.monsterSweep?.workers || []);
        } catch {
        } finally {
          n3 = false;
        }
      }
    };
    r3();
    let i3 = window.setInterval(() => {
      r3();
    }, 2e3);
    return () => {
      e2 = true, window.clearInterval(i3);
    };
  }, [t2]), (0, O.useEffect)(() => {
    function e2(e3) {
      U2.current?.contains(e3.target) || F2(false);
    }
    return document.addEventListener(`pointerdown`, e2), () => document.removeEventListener(`pointerdown`, e2);
  }, []);
  let Ke2 = L2 ? ue(M2, L2) : void 0, qe2 = !!(N2 && L2?.targetKey && !Ke2), Je2 = Oe2 ? L2?.lastListTargetKey || `` : Ke2?.key || L2?.targetKey || ``, Ye2 = (0, O.useMemo)(() => M2.filter((e2) => !e2.key.startsWith(`name:`) && (Ge2 === `farm` ? !e2.joinOnly : e2.rally)), [Ge2, M2]), Xe2 = (0, O.useMemo)(() => {
    let e2 = new Map(_e.map((e3) => [e3, []]));
    for (let t3 of Ye2) e2.get(t3.group)?.push(t3);
    return _e.flatMap((t3) => {
      let n3 = e2.get(t3) || [];
      return n3.length ? [{ group: t3, targets: n3 }] : [];
    });
  }, [Ye2]);
  async function Ze2(e2, t3 = y2.state.getSnapshot().draft.enabled, n3 = y2.state.getSnapshot().draft.allianceDrill) {
    return y2.state.edit({ enabled: t3, strategies: e2, allianceDrill: n3 }, false), y2.state.flush();
  }
  async function Qe2() {
    P2(false), !(!L2 || !Te(L2)) && (R2({ ...L2, name: L2.name.trim() }), await y2.state.flush().catch(() => void 0));
  }
  async function $e2(e2) {
    if (e2) y2.state.edit((t3) => ({ ...t3, enabled: e2 }), false), await y2.state.flush().catch(() => void 0);
    else {
      Fe2(`stop`);
      try {
        await y2.state.runAction(() => v({ ...y2.state.getSnapshot().confirmed, enabled: false }, g2), (e3) => ({ ...e3, enabled: false }));
      } catch {
      } finally {
        Fe2(``);
      }
    }
  }
  async function et2(e2) {
    b2.state.edit((t3) => ({ ...t3, enabled: e2 }), false), await b2.state.flush().catch(() => void 0);
  }
  async function tt2(e2) {
    y2.state.edit((t3) => ({ ...t3, allianceDrill: { ...e2, joinRestrictions: oe(e2.joinRestrictions) } }), false), await y2.state.flush().catch(() => void 0);
  }
  async function nt2(e2) {
    if (e2 && E2.squadIndexes.length === 0) {
      j2(`drill`), V2(h2(`squad.afkAllianceDrillSquadRequired`));
      return;
    }
    await tt2({ ...E2, enabled: e2 });
  }
  async function rt2(e2) {
    let t3 = y2.state.getSnapshot().draft.allianceDrill, n3 = t3.squadIndexes.includes(e2) ? t3.squadIndexes.filter((t4) => t4 !== e2) : [...t3.squadIndexes, e2];
    await tt2({ ...t3, enabled: n3.length > 0 && t3.enabled, squadIndexes: n3 });
  }
  async function it2(e2) {
    let t3 = Re2;
    if (ze2(null), Ve2(null), t3 == null) return;
    let n3 = y2.state.getSnapshot().draft.allianceDrill, r3 = be(n3.squadIndexes, t3, e2);
    r3 !== n3.squadIndexes && await tt2({ ...n3, squadIndexes: r3 });
  }
  function at2(e2) {
    let t3 = M2.find((t4) => t4.key === e2);
    !t3 || !L2 || (V2(``), R2({ ...L2, targetKey: t3.key, lastListTargetKey: t3.key, targetNameQuery: void 0, monsterNameKey: t3.monsterNameKey, monsterType: t3.monsterType, monsterIds: t3.monsterIds, monsterSpecial: t3.monsterSpecial, source: t3.source, action: t3.action, searchable: t3.searchable, rally: t3.rally, continuousAttack: t3.rally ? false : L2.continuousAttack, minLevel: t3.minLevel, maxLevel: t3.maxLevel }));
  }
  function ot2(e2) {
    let t3 = M2.find((t4) => !t4.key.startsWith(`name:`) && (e2 === `farm` ? !t4.rally : t4.rally));
    t3 && (R2(Ce(t3, Ne2, e2)), F2(false), V2(``), window.setTimeout(() => H2.current?.scrollIntoView({ behavior: `smooth`, block: `nearest` }), 0));
  }
  function st2(e2) {
    let t3 = ue(M2, e2);
    R2(t3 ? { ...e2, targetKey: t3.key, monsterNameKey: t3.monsterNameKey, monsterIds: t3.monsterIds, monsterSpecial: t3.monsterSpecial, monsterType: t3.monsterType, searchable: t3.searchable, source: t3.source, action: t3.action, rally: t3.rally, continuousAttack: t3.rally ? false : e2.continuousAttack } : { ...e2 }), V2(``);
  }
  function ct2(e2) {
    if (!L2) return;
    let t3 = e2.trim();
    V2(``), R2({ ...L2, lastListTargetKey: L2.targetKey.startsWith(`query:`) ? L2.lastListTargetKey : Ke2?.key || L2.targetKey, targetKey: `query:${t3}`, targetNameQuery: e2, monsterNameKey: void 0, monsterType: 0, monsterIds: [], monsterSpecial: void 0, searchable: false, source: `undiscovered`, action: L2.kind === `join` ? `rally` : `attack`, rally: L2.kind === `join`, minLevel: 1, maxLevel: 999 });
  }
  async function lt2(e2) {
    if (window.confirm(`${h2(`common.delete`)} \u201C${e2.name}\u201D?`)) {
      Fe2(`delete-${e2.id}`);
      try {
        let t3 = S2.filter((t4) => t4.id !== e2.id);
        De2.current === e2.id && (De2.current = t3[0]?.id ?? null, Ee2(t3[0]?.id ?? null)), await Ze2(t3), V2(``);
      } catch {
      } finally {
        Fe2(``);
      }
    }
  }
  async function W2(e2, t3) {
    try {
      await Ze2(S2.map((n3) => n3.id === e2 ? { ...n3, enabled: t3 } : n3)), V2(``);
    } catch {
    }
  }
  async function ut2(e2) {
    let t3 = Le2.current;
    if (Le2.current = ``, Ue2(``), !t3 || t3 === e2 || B2) return;
    let n3 = S2.findIndex((e3) => e3.id === t3), r3 = S2.findIndex((t4) => t4.id === e2);
    if (n3 < 0 || r3 < 0) return;
    let i3 = [...S2], [a3] = i3.splice(n3, 1);
    i3.splice(r3, 0, a3), Fe2(`reorder`);
    try {
      await Ze2(i3), V2(``), s2(`monster afk strategies reordered: ${t3}`);
    } catch {
    } finally {
      Fe2(``);
    }
  }
  function G2(e2) {
    let t3 = h2(e2.source === `search` ? `squad.afkSearchable` : `squad.afkLocalTarget`), n3 = e2.searchable ? ` \xB7 ${dt2(e2)}` : ``, r3 = h2(Ge2 === `join` ? `squad.afkJoin` : e2.action === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`);
    return `${e2.name} \xB7 ${t3} \xB7 ${r3}${n3}`;
  }
  function dt2(e2) {
    return e2?.attackMinLevel != null && e2.attackMaxLevel != null ? h2(`squad.afkAttackableRange`, { min: e2.attackMinLevel, max: e2.attackMaxLevel }) : h2(`squad.afkAttackableRangeUnavailable`);
  }
  let ft2 = !!(L2 && we(L2, Ke2)), pt2 = !!(B2 && B2 !== `save`), K2 = ke2.filter((e2) => e2.activity === `allianceDrill` && e2.running), mt2 = K2.some((e2) => e2.activityRole === `leader`) ? `squad.afkAllianceDrillLeading` : K2.length > 0 ? `squad.afkAllianceDrillJoining` : `squad.afkAllianceDrillWaiting`, ht2 = E2.squadIndexes.join(` \u2192 `) || `-`, gt2 = new Set(E2.squadIndexes), _t2 = [...E2.squadIndexes, ...Ne2.filter((e2) => !gt2.has(e2))], vt2 = `${h2(`squad.afkAllianceDrillOrder`, { order: ht2 })} \xB7 ${h2(E2.activeRally ? `squad.afkAllianceDrillActive` : `squad.afkJoin`)}`, yt2 = k2 === `garrison` || k2 === `drill` || k2 === `zombieBus`;
  return (0, A.jsxs)(`div`, { className: `monster-afk-layout`, children: [(0, A.jsx)(ne, { state: y2.state, label: h2(`squad.afkMaster`) }), (0, A.jsx)(ne, { state: b2.state, label: h2(`automation.autoUsePotion`) }), (0, A.jsxs)(`div`, { className: `monster-afk-toolbar`, children: [(0, A.jsx)(ie, { title: h2(`squad.afkMaster`), description: h2(`squad.afkMasterDescription`), summary: h2(x2 ? `common.enabled` : `common.disabled`), enabled: x2, disabled: !!B2, onToggle: (e2) => void $e2(e2), settingsOpen: k2 === null, cardSelectable: true, onSettings: () => j2(null), settingsLabel: h2(`nav.settings`) }), (0, A.jsx)(ie, { title: h2(`automation.autoUsePotion`), description: h2(`automation.potionMonsterOnly`), summary: `${h2(`automation.minStamina`)} ${te2}`, enabled: ee2, disabled: !!B2, onToggle: (e2) => void et2(e2), settingsOpen: k2 === `potion`, cardSelectable: true, onSettings: () => j2((e2) => e2 === `potion` ? null : `potion`), settingsLabel: h2(`nav.settings`) }), (0, A.jsx)(ie, { title: h2(`squad.afkAllianceDrill`), description: h2(`squad.afkAllianceDrillDescription`), summary: E2.enabled && K2.length > 0 ? `${h2(mt2)} \xB7 ${ht2}` : vt2, enabled: E2.enabled, disabled: !!B2, onToggle: (e2) => void nt2(e2), settingsOpen: k2 === `drill`, cardSelectable: true, onSettings: () => j2((e2) => e2 === `drill` ? null : `drill`), settingsLabel: h2(`nav.settings`) }), (0, A.jsx)(pe, { online: t2, serverId: n2, config: a2, open: k2 === `garrison`, onOpenChange: () => j2((e2) => e2 === `garrison` ? null : `garrison`), onLog: s2 }), (0, A.jsx)(he, { online: t2, config: o2, open: k2 === `zombieBus`, onOpenChange: () => j2((e2) => e2 === `zombieBus` ? null : `zombieBus`), onLog: s2 }), k2 === `potion` && (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: [(0, A.jsx)(`strong`, { children: h2(`automation.autoUsePotion`) }), (0, A.jsx)(`span`, { children: h2(`automation.potionMonsterOnly`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-card-settings`, children: [(0, A.jsxs)(`label`, { children: [(0, A.jsx)(`span`, { children: h2(`automation.minStamina`) }), (0, A.jsx)(`input`, { type: `number`, min: 0, max: 9999, step: 1, value: Number.isNaN(te2) ? `` : te2, disabled: !!B2, onChange: (e2) => ae2(e2.target.valueAsNumber) })] }), (0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: re2, disabled: !!B2, onChange: (e2) => ce2(e2.target.checked) }), (0, A.jsx)(`span`, { children: h2(`automation.preferFifty`) })] })] })] }), k2 === `drill` && (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: [(0, A.jsx)(`strong`, { children: h2(`squad.afkAllianceDrill`) }), (0, A.jsx)(`span`, { children: h2(`squad.afkAllianceDrillDescription`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-drill-inline`, children: [(0, A.jsx)(`span`, { className: `muted`, children: h2(`squad.afkAllianceDrillOrder`, { order: ht2 }) }), (0, A.jsx)(w, { label: `squad.afkAllianceDrillActive`, checked: E2.activeRally, disabled: !!B2, onChange: (e2) => void tt2({ ...E2, activeRally: e2 }) })] }), (0, A.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": h2(`squad.afkAllianceDrillOrder`, { order: ht2 }), children: _t2.map((e2) => {
    let t3 = gt2.has(e2);
    return (0, A.jsxs)(`div`, { className: `automation-squad-priority-item${t3 ? ` selected` : ``}${Re2 === e2 ? ` dragging` : ``}${Be2 === e2 ? ` drag-over` : ``}`, draggable: t3 && !B2, onDragStart: (n3) => {
      t3 && (n3.dataTransfer.effectAllowed = `move`, ze2(e2));
    }, onDragOver: (n3) => {
      !t3 || Re2 == null || Re2 === e2 || (n3.preventDefault(), n3.dataTransfer.dropEffect = `move`, Ve2(e2));
    }, onDrop: (t4) => {
      t4.preventDefault(), it2(e2);
    }, onDragEnd: () => {
      ze2(null), Ve2(null);
    }, children: [(0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: t3, disabled: !!B2, onChange: () => void rt2(e2) }), (0, A.jsx)(`span`, { children: h2(`squad.number`, { number: e2 }) })] }), t3 && (0, A.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, A.jsx)(T, { name: `drag` }) })] }, e2);
  }) }), (0, A.jsx)(ye, { value: E2.joinRestrictions, online: t2, disabled: !!B2, onChange: (e2) => y2.state.edit((t3) => ({ ...t3, allianceDrill: { ...t3.allianceDrill, joinRestrictions: e2 } })) }), K2.filter((e2) => e2.step === `waiting_join_delay`).map((e2) => (0, A.jsxs)(`span`, { className: `muted`, children: [h2(`squad.number`, { number: e2.squadIndex }), ` \xB7 `, h2(`squad.join.waitingDetail`, { target: Ae2[e2.joinTargetNameKey ?? ``] || e2.joinTargetName || ``, seconds: e2.joinWaitSeconds ?? 0 })] }, e2.squadIndex))] })] }), !yt2 && (0, A.jsxs)(A.Fragment, { children: [(0, A.jsxs)(`section`, { className: `monster-afk-profiles`, children: [(0, A.jsxs)(`div`, { className: `monster-section-title`, children: [(0, A.jsx)(`strong`, { children: h2(`squad.afkProfiles`) }), (0, A.jsxs)(`div`, { ref: U2, className: `monster-afk-add-control`, onKeyDown: (e2) => {
    e2.key === `Escape` && F2(false);
  }, children: [(0, A.jsx)(`button`, { type: `button`, "aria-haspopup": `menu`, "aria-expanded": ve2, disabled: !M2.length || !!B2, onClick: () => F2((e2) => !e2), children: h2(`common.add`) }), ve2 && (0, A.jsxs)(`div`, { className: `monster-afk-add-menu`, role: `menu`, children: [(0, A.jsxs)(`button`, { type: `button`, role: `menuitem`, disabled: !M2.some((e2) => !e2.key.startsWith(`name:`) && !e2.rally), onClick: () => ot2(`farm`), children: [(0, A.jsx)(`strong`, { children: h2(`squad.afkActiveAttack`) }), (0, A.jsx)(`span`, { children: h2(`squad.afkActionAttack`) })] }), (0, A.jsxs)(`button`, { type: `button`, role: `menuitem`, disabled: !M2.some((e2) => !e2.key.startsWith(`name:`) && e2.rally), onClick: () => ot2(`join`), children: [(0, A.jsx)(`strong`, { children: h2(`squad.afkJoin`) }), (0, A.jsx)(`span`, { children: h2(`squad.autoJoinRally`) })] })] })] })] }), (0, A.jsxs)(`div`, { className: `monster-afk-profile-list`, children: [S2.map((e2) => {
    let t3 = e2.squadIndexes.map((e3) => ke2.find((t4) => t4.squadIndex === e3)).filter((e3) => !!e3), n3 = M2.find((t4) => t4.key === e2.targetKey) || M2.find((t4) => !!e2.monsterNameKey && t4.monsterNameKey === e2.monsterNameKey && t4.rally === e2.rally), r3 = n3?.source || (N2 ? `undiscovered` : e2.source), i3 = r3 === `search` ? `squad.afkSearchable` : r3 === `map` ? `squad.afkLocalTarget` : `squad.afkUndiscovered`;
    return (0, A.jsxs)(`div`, { className: `monster-afk-profile-card ${L2?.id === e2.id ? `active` : ``} ${e2.enabled ? `` : `disabled`} ${He2 === e2.id ? `drag-over` : ``}`, onDragEnd: () => {
      Le2.current = ``, Ue2(``);
    }, onDragOver: (t4) => {
      !Le2.current || B2 || (t4.preventDefault(), Ue2(e2.id));
    }, onDragLeave: () => Ue2((t4) => t4 === e2.id ? `` : t4), onDrop: (t4) => {
      t4.preventDefault(), ut2(e2.id);
    }, children: [(0, A.jsxs)(`label`, { className: `monster-afk-enabled ${e2.enabled ? `is-enabled` : ``} ${pt2 || e2.squadIndexes.length === 0 ? `is-disabled` : ``}`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: e2.enabled, disabled: !!B2 || e2.squadIndexes.length === 0, onChange: (t4) => void W2(e2.id, t4.target.checked) }), (0, A.jsx)(`span`, { className: `monster-afk-enabled-track`, "aria-hidden": `true` }), (0, A.jsx)(`span`, { children: h2(e2.enabled ? `common.enabled` : `common.disabled`) })] }), (0, A.jsxs)(`button`, { type: `button`, className: `monster-afk-profile-select`, "aria-pressed": L2?.id === e2.id, onClick: () => st2(e2), children: [(0, A.jsxs)(`span`, { className: `monster-afk-profile-heading`, children: [(0, A.jsx)(`strong`, { children: e2.name }), (0, A.jsx)(`span`, { className: `monster-afk-mode-badge ${e2.kind}`, children: h2(e2.kind === `farm` ? `squad.afkActiveAttack` : `squad.afkJoin`) })] }), (0, A.jsxs)(`span`, { children: [e2.levelFilterEnabled ? `${e2.minLevel}-${e2.maxLevel}` : h2(`squad.afkAnyLevel`), ` \xB7 `, e2.distanceFilterEnabled ? e2.maxDistance : h2(`squad.afkAnyDistance`)] }), e2.kind === `farm` && e2.searchable && (0, A.jsxs)(`span`, { className: we(e2, n3) ? `monster-afk-level-warning` : ``, children: [dt2(n3), we(e2, n3) ? ` \xB7 ${h2(`squad.afkLevelOutOfRange`)}` : ``] }), (0, A.jsxs)(`span`, { children: [h2(i3), ` \xB7 `, h2(e2.kind === `join` ? `squad.afkJoin` : (n3?.action || e2.action) === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`), ` \xB7 `, h2(`squad.afkBoundSquads`, { count: e2.squadIndexes.join(`, `) || `-` })] }), t3.map((t4) => {
      let n4 = t4.profileId === e2.id, r4 = t4.completedStrategyIds?.includes(e2.id);
      return (0, A.jsxs)(`span`, { className: n4 && t4.running ? `status-ok` : `muted`, children: [h2(`squad.number`, { number: t4.squadIndex }), `: `, n4 && t4.step ? h2(ge[t4.step] || t4.step) : h2(r4 ? `common.completed` : `squad.status.idle`), n4 && t4.step === `waiting_join_delay` && ` \xB7 ${h2(`squad.join.waitingDetail`, { target: Ae2[t4.joinTargetNameKey ?? ``] || t4.joinTargetName || ``, seconds: t4.joinWaitSeconds ?? 0 })}`, e2.executionLimit > 0 ? ` \xB7 ${t4.strategyProcessed?.[e2.id] || 0}/${e2.executionLimit}` : ``, n4 && t4.lastError ? ` \xB7 ${_(h2, t4.lastError)}` : ``] }, t4.squadIndex);
    })] }), (0, A.jsx)(`button`, { className: `danger`, disabled: !!B2, onClick: () => void lt2(e2), children: h2(`common.delete`) }), (0, A.jsx)(`button`, { type: `button`, className: `monster-afk-profile-drag`, disabled: !!B2, draggable: !B2, "aria-label": h2(`squad.afkReorder`, { name: e2.name }), title: h2(`squad.afkReorder`, { name: e2.name }), onDragStart: (t4) => {
      Le2.current = e2.id, t4.dataTransfer.effectAllowed = `move`, t4.dataTransfer.setData(`text/plain`, e2.id);
    }, onKeyDown: (t4) => {
      if (t4.key !== `ArrowUp` && t4.key !== `ArrowDown`) return;
      t4.preventDefault();
      let n4 = S2.findIndex((t5) => t5.id === e2.id), r4 = S2[n4 + (t4.key === `ArrowUp` ? -1 : 1)];
      !r4 || B2 || (Le2.current = e2.id, ut2(r4.id));
    }, children: (0, A.jsx)(T, { name: `drag` }) })] }, e2.id);
  }), !S2.length && (0, A.jsx)(`span`, { className: `muted`, children: h2(`squad.afkNoProfiles`) })] })] }), L2 && (0, A.jsxs)(`div`, { ref: H2, className: `monster-afk-editor ${We2 ? `is-new` : ``}`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-editor-heading`, children: [(0, A.jsx)(`strong`, { children: h2(We2 ? `squad.afkNewProfile` : `squad.afkEditProfile`) }), (0, A.jsx)(`span`, { className: `monster-afk-mode-badge ${L2.kind}`, children: h2(L2.kind === `farm` ? `squad.afkActiveAttack` : `squad.afkJoin`) })] }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h2(`squad.afkBasicSettings`) }), (0, A.jsxs)(`div`, { className: `monster-afk-basic-grid`, children: [(0, A.jsxs)(`label`, { children: [h2(`squad.afkProfileName`), (0, A.jsx)(`input`, { value: L2.name, onFocus: () => P2(true), onBlur: () => void Qe2(), onChange: (e2) => R2({ ...L2, name: e2.target.value }), onKeyDown: (e2) => {
    e2.key === `Enter` && !e2.nativeEvent.isComposing && e2.currentTarget.blur();
  } })] }), (0, A.jsxs)(`label`, { children: [h2(`squad.afkTarget`), (0, A.jsxs)(`select`, { value: Je2, onChange: (e2) => at2(e2.target.value), children: [Oe2 && !M2.some((e2) => e2.key === Je2) && (0, A.jsx)(`option`, { value: Je2, children: h2(`squad.afkSelectListTarget`) }), !Oe2 && qe2 && (0, A.jsxs)(`option`, { value: L2.targetKey, children: [L2.targetNameQuery || L2.name, ` \xB7 `, h2(`squad.afkUndiscovered`), ` \xB7 `, h2(L2.kind === `join` ? `squad.afkJoin` : L2.action === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`)] }), Xe2.map((e2) => (0, A.jsx)(`optgroup`, { label: h2(`squad.afkGroup.${e2.group}`), children: e2.targets.map((e3) => (0, A.jsx)(`option`, { value: e3.key, children: G2(e3) }, e3.key)) }, e2.group))] })] })] }), (0, A.jsxs)(`div`, { className: `monster-afk-custom-target`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: Oe2, onChange: (e2) => {
    e2.target.checked ? ct2(``) : L2.lastListTargetKey && M2.some((e3) => e3.key === L2.lastListTargetKey) ? at2(L2.lastListTargetKey) : V2(h2(`squad.afkRestoreTargetRequired`));
  } }), h2(`squad.afkCustomTarget`)] }), Oe2 && (0, A.jsxs)(`label`, { className: `monster-afk-custom-name`, children: [h2(`squad.afkCustomName`), (0, A.jsx)(`input`, { value: L2.targetNameQuery || ``, "aria-invalid": !L2.targetNameQuery?.trim(), onChange: (e2) => ct2(e2.target.value) }), (0, A.jsx)(`span`, { className: `muted`, children: h2(`squad.afkCustomTargetHint`) }), !L2.targetNameQuery?.trim() && (0, A.jsx)(`span`, { role: `status`, className: `status-error`, children: h2(`squad.afkCustomTargetRequired`) })] })] })] }), L2.kind === `join` && (0, A.jsx)(`section`, { className: `monster-afk-config-section monster-afk-join-section`, children: (0, A.jsx)(ye, { label: `squad.afkJoinConditions`, value: oe(L2.joinRestrictions, L2.minMembers ?? 1), online: t2, disabled: !!B2, onChange: (e2) => R2({ ...L2, joinRestrictions: e2 }) }) }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h2(`squad.afkExecutionSettings`) }), (0, A.jsxs)(`div`, { className: `monster-afk-basic-grid monster-afk-execution-grid`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-squad-field`, children: [(0, A.jsx)(`span`, { children: h2(`squad.afkAssignments`) }), (0, A.jsx)(`div`, { className: `monster-afk-squads`, role: `group`, "aria-label": h2(`squad.afkAssignments`), children: Ne2.map((e2) => (0, A.jsx)(`button`, { className: `monster-afk-squad-toggle`, type: `button`, "aria-label": h2(`squad.number`, { number: e2 }), "aria-pressed": L2.squadIndexes.includes(e2), title: h2(`squad.number`, { number: e2 }), onClick: () => R2((t3) => t3 && { ...t3, squadIndexes: t3.squadIndexes.includes(e2) ? t3.squadIndexes.filter((t4) => t4 !== e2) : [...t3.squadIndexes, e2].sort((e3, t4) => e3 - t4) }), children: e2 }, e2)) })] }), (0, A.jsxs)(`label`, { title: h2(`squad.afkExecutionLimitHint`), children: [h2(`squad.afkExecutionLimitLabel`), (0, A.jsx)(`input`, { type: `number`, min: 0, step: 1, value: Number.isNaN(L2.executionLimit) ? `` : L2.executionLimit, onChange: (e2) => R2({ ...L2, executionLimit: e2.target.valueAsNumber }) })] })] }), L2.kind === `farm` && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.continuousAttack, disabled: L2.rally, onChange: (e2) => R2({ ...L2, continuousAttack: e2.target.checked }) }), h2(`squad.afkContinuousAttack`), L2.rally && (0, A.jsx)(`span`, { className: `muted`, children: h2(`squad.afkContinuousAttackUnavailable`) })] }), L2.kind === `join` && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.continuousJoin, onChange: (e2) => R2({ ...L2, continuousJoin: e2.target.checked }) }), h2(`squad.afkContinuousJoin`)] })] }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h2(`squad.afkTargetFilters`) }), (0, A.jsxs)(`div`, { className: `monster-afk-filter`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.levelFilterEnabled, onChange: (e2) => R2({ ...L2, levelFilterEnabled: e2.target.checked, progressiveLevels: e2.target.checked && L2.progressiveLevels }) }), h2(`squad.afkLevelFilter`)] }), L2.levelFilterEnabled && (0, A.jsxs)(`div`, { className: `monster-afk-filter-content`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-number-grid`, children: [(0, A.jsxs)(`label`, { children: [h2(`squad.afkMinLevel`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L2.minLevel) ? `` : L2.minLevel, onChange: (e2) => R2({ ...L2, minLevel: e2.target.valueAsNumber }) })] }), !L2.progressiveLevels && (0, A.jsxs)(`label`, { children: [h2(`squad.afkMaxLevel`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L2.maxLevel) ? `` : L2.maxLevel, onChange: (e2) => R2({ ...L2, maxLevel: e2.target.valueAsNumber }) })] })] }), L2.kind === `farm` && L2.searchable && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.progressiveLevels, onChange: (e2) => R2({ ...L2, progressiveLevels: e2.target.checked }) }), h2(`squad.afkProgressiveLevels`)] })] })] }), L2.kind === `farm` && L2.searchable && (0, A.jsxs)(`div`, { className: `monster-afk-attackable-range ${ft2 ? `is-invalid` : ``}`, children: [(0, A.jsx)(`span`, { children: dt2(Ke2) }), ft2 && (0, A.jsx)(`strong`, { children: h2(`squad.afkLevelOutOfRange`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-filter`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L2.distanceFilterEnabled === true, onChange: (e2) => R2({ ...L2, distanceFilterEnabled: e2.target.checked }) }), h2(`squad.afkDistanceFilter`)] }), L2.distanceFilterEnabled && (0, A.jsx)(`div`, { className: `monster-afk-filter-content`, children: (0, A.jsxs)(`label`, { children: [h2(`squad.maxDistance`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L2.maxDistance) ? `` : L2.maxDistance, onChange: (e2) => R2({ ...L2, maxDistance: e2.target.valueAsNumber }) })] }) })] })] })] })] }), Ie2 && (0, A.jsx)(`div`, { className: `automation-error monster-afk-error`, children: Ie2 })] });
}
var Ee = (0, O.createContext)({});
function De(e2) {
  let t2 = (0, O.useRef)(null);
  return t2.current === null && (t2.current = e2()), t2.current;
}
var L = typeof window < `u` ? O.useLayoutEffect : O.useEffect, Oe = (0, O.createContext)(null);
function R(e2, t2) {
  e2.indexOf(t2) === -1 && e2.push(t2);
}
function ke(e2, t2) {
  let n2 = e2.indexOf(t2);
  n2 > -1 && e2.splice(n2, 1);
}
var z = (e2, t2, n2) => n2 > t2 ? t2 : n2 < e2 ? e2 : n2, Ae = {}, je = (e2) => /^-?(?:\d+(?:\.\d+)?|\.\d+)$/u.test(e2), Me = (e2) => typeof e2 == `object` && !!e2, Ne = (e2) => /^0[^.\s]+$/u.test(e2);
function Pe(e2) {
  let t2;
  return () => (t2 === void 0 && (t2 = e2()), t2);
}
var B = (e2) => e2, Fe = (...e2) => e2.reduce((e3, t2) => (n2) => t2(e3(n2))), Ie = (e2, t2, n2) => {
  let r2 = t2 - e2;
  return r2 ? (n2 - e2) / r2 : 1;
}, V = class {
  constructor() {
    this.subscriptions = [];
  }
  add(e2) {
    return R(this.subscriptions, e2), () => ke(this.subscriptions, e2);
  }
  notify(e2, t2, n2) {
    let r2 = this.subscriptions.length;
    if (r2) if (r2 === 1) this.subscriptions[0](e2, t2, n2);
    else for (let i2 = 0; i2 < r2; i2++) {
      let r3 = this.subscriptions[i2];
      r3 && r3(e2, t2, n2);
    }
  }
  getSize() {
    return this.subscriptions.length;
  }
  clear() {
    this.subscriptions.length = 0;
  }
}, H = (e2) => e2 * 1e3, U = (e2) => e2 / 1e3, Le = (e2, t2) => t2 ? 1e3 / t2 * e2 : 0, Re = (e2, t2, n2) => (((1 - 3 * n2 + 3 * t2) * e2 + (3 * n2 - 6 * t2)) * e2 + 3 * t2) * e2, ze = 1e-7, Be = 12;
function Ve(e2, t2, n2, r2, i2) {
  let a2, o2, s2 = 0;
  do
    o2 = t2 + (n2 - t2) / 2, a2 = Re(o2, r2, i2) - e2, a2 > 0 ? n2 = o2 : t2 = o2;
  while (Math.abs(a2) > ze && ++s2 < Be);
  return o2;
}
function He(e2, t2, n2, r2) {
  if (e2 === t2 && n2 === r2) return B;
  let i2 = (t3) => Ve(t3, 0, 1, e2, n2);
  return (e3) => e3 === 0 || e3 === 1 ? e3 : Re(i2(e3), t2, r2);
}
var Ue = (e2) => (t2) => t2 <= 0.5 ? e2(2 * t2) / 2 : (2 - e2(2 * (1 - t2))) / 2, We = (e2) => (t2) => 1 - e2(1 - t2), Ge = He(0.33, 1.53, 0.69, 0.99), Ke = We(Ge), qe = Ue(Ke), Je = (e2) => e2 >= 1 ? 1 : (e2 *= 2) < 1 ? 0.5 * Ke(e2) : 0.5 * (2 - 2 ** (-10 * (e2 - 1))), Ye = (e2) => 1 - Math.sin(Math.acos(e2)), Xe = We(Ye), Ze = Ue(Ye), Qe = He(0.42, 0, 1, 1), $e = He(0, 0, 0.58, 1), et = He(0.42, 0, 0.58, 1), tt = (e2) => Array.isArray(e2) && typeof e2[0] != `number`, nt = (e2) => Array.isArray(e2) && typeof e2[0] == `number`, rt = { linear: B, easeIn: Qe, easeInOut: et, easeOut: $e, circIn: Ye, circInOut: Ze, circOut: Xe, backIn: Ke, backInOut: qe, backOut: Ge, anticipate: Je }, it = (e2) => typeof e2 == `string`, at = (e2) => {
  if (nt(e2)) {
    e2.length;
    let [t2, n2, r2, i2] = e2;
    return He(t2, n2, r2, i2);
  } else if (it(e2)) return rt[e2], `${e2}`, rt[e2];
  return e2;
}, ot = [`setup`, `read`, `resolveKeyframes`, `preUpdate`, `update`, `preRender`, `render`, `postRender`];
function st(e2) {
  let t2 = /* @__PURE__ */ new Set(), n2 = /* @__PURE__ */ new Set(), r2 = false, i2 = false, a2 = /* @__PURE__ */ new WeakSet(), o2 = { delta: 0, timestamp: 0, isProcessing: false };
  function s2(t3) {
    a2.has(t3) && (c2.schedule(t3), e2()), t3(o2);
  }
  let c2 = { schedule: (e3, i3 = false, o3 = false) => {
    let s3 = o3 && r2 ? t2 : n2;
    return i3 && a2.add(e3), s3.add(e3), e3;
  }, cancel: (e3) => {
    n2.delete(e3), a2.delete(e3);
  }, process: (e3) => {
    if (o2 = e3, r2) {
      i2 = true;
      return;
    }
    r2 = true;
    let a3 = t2;
    t2 = n2, n2 = a3, t2.forEach(s2), t2.clear(), r2 = false, i2 && (i2 = false, c2.process(e3));
  } };
  return c2;
}
var ct = 40;
function lt(e2, t2) {
  let n2 = false, r2 = true, i2 = { delta: 0, timestamp: 0, isProcessing: false }, a2 = () => n2 = true, o2 = ot.reduce((e3, t3) => (e3[t3] = st(a2), e3), {}), { setup: s2, read: c2, resolveKeyframes: l2, preUpdate: u2, update: d2, preRender: f2, render: p2, postRender: m2 } = o2, h2 = () => {
    let a3 = Ae.useManualTiming, o3 = a3 ? i2.timestamp : performance.now();
    n2 = false, a3 || (i2.delta = r2 ? 1e3 / 60 : Math.max(Math.min(o3 - i2.timestamp, ct), 1)), i2.timestamp = o3, i2.isProcessing = true, s2.process(i2), c2.process(i2), l2.process(i2), u2.process(i2), d2.process(i2), f2.process(i2), p2.process(i2), m2.process(i2), i2.isProcessing = false, n2 && t2 && (r2 = false, e2(h2));
  }, g2 = () => {
    n2 = true, r2 = true, i2.isProcessing || e2(h2);
  };
  return { schedule: ot.reduce((e3, t3) => {
    let r3 = o2[t3];
    return e3[t3] = (e4, t4 = false, i3 = false) => (n2 || g2(), r3.schedule(e4, t4, i3)), e3;
  }, {}), cancel: (e3) => {
    for (let t3 = 0; t3 < ot.length; t3++) o2[ot[t3]].cancel(e3);
  }, state: i2, steps: o2 };
}
var { schedule: W, cancel: ut, state: G, steps: dt } = lt(typeof requestAnimationFrame < `u` ? requestAnimationFrame : B, true), ft;
function pt() {
  ft = void 0;
}
var K = { now: () => (ft === void 0 && K.set(G.isProcessing || Ae.useManualTiming ? G.timestamp : performance.now()), ft), set: (e2) => {
  ft = e2, queueMicrotask(pt);
} }, mt = (e2) => (t2) => typeof t2 == `string` && t2.startsWith(e2), ht = mt(`--`), gt = mt(`var(--`), _t = (e2) => gt(e2) ? vt.test(e2.split(`/*`)[0].trim()) : false, vt = /var\(--(?:[\w-]+\s*|[\w-]+\s*,(?:\s*[^)(\s]|\s*\((?:[^)(]|\([^)(]*\))*\))+\s*)\)$/iu;
function yt(e2) {
  return typeof e2 == `string` ? e2.split(`/*`)[0].includes(`var(--`) : false;
}
var bt = { test: (e2) => typeof e2 == `number`, parse: parseFloat, transform: (e2) => e2 }, xt = { ...bt, transform: (e2) => z(0, 1, e2) }, St = { ...bt, default: 1 }, Ct = (e2) => Math.round(e2 * 1e5) / 1e5, wt = /-?(?:\d+(?:\.\d+)?|\.\d+)/gu;
function Tt(e2) {
  return e2 == null;
}
var Et = /^(?:#[\da-f]{3,8}|(?:rgb|hsl)a?\((?:-?[\d.]+%?[,\s]+){2}-?[\d.]+%?\s*(?:[,/]\s*)?(?:\b\d+(?:\.\d+)?|\.\d+)?%?\))$/iu, Dt = (e2, t2) => (n2) => !!(typeof n2 == `string` && Et.test(n2) && n2.startsWith(e2) || t2 && !Tt(n2) && Object.prototype.hasOwnProperty.call(n2, t2)), Ot = (e2, t2, n2) => (r2) => {
  if (typeof r2 != `string`) return r2;
  let [i2, a2, o2, s2] = r2.match(wt);
  return { [e2]: parseFloat(i2), [t2]: parseFloat(a2), [n2]: parseFloat(o2), alpha: s2 === void 0 ? 1 : parseFloat(s2) };
}, kt = (e2) => z(0, 255, e2), At = { ...bt, transform: (e2) => Math.round(kt(e2)) }, jt = { test: Dt(`rgb`, `red`), parse: Ot(`red`, `green`, `blue`), transform: ({ red: e2, green: t2, blue: n2, alpha: r2 = 1 }) => `rgba(` + At.transform(e2) + `, ` + At.transform(t2) + `, ` + At.transform(n2) + `, ` + Ct(xt.transform(r2)) + `)` };
function Mt(e2) {
  let t2 = ``, n2 = ``, r2 = ``, i2 = ``;
  return e2.length > 5 ? (t2 = e2.substring(1, 3), n2 = e2.substring(3, 5), r2 = e2.substring(5, 7), i2 = e2.substring(7, 9)) : (t2 = e2.substring(1, 2), n2 = e2.substring(2, 3), r2 = e2.substring(3, 4), i2 = e2.substring(4, 5), t2 += t2, n2 += n2, r2 += r2, i2 += i2), { red: parseInt(t2, 16), green: parseInt(n2, 16), blue: parseInt(r2, 16), alpha: i2 ? parseInt(i2, 16) / 255 : 1 };
}
var Nt = { test: Dt(`#`), parse: Mt, transform: jt.transform }, Pt = (e2) => ({ test: (t2) => typeof t2 == `string` && t2.endsWith(e2) && t2.split(` `).length === 1, parse: parseFloat, transform: (t2) => `${t2}${e2}` }), Ft = Pt(`deg`), It = Pt(`%`), q = Pt(`px`), Lt = Pt(`vh`), Rt = Pt(`vw`), zt = { ...It, parse: (e2) => It.parse(e2) / 100, transform: (e2) => It.transform(e2 * 100) }, Bt = { test: Dt(`hsl`, `hue`), parse: Ot(`hue`, `saturation`, `lightness`), transform: ({ hue: e2, saturation: t2, lightness: n2, alpha: r2 = 1 }) => `hsla(` + Math.round(e2) + `, ` + It.transform(Ct(t2)) + `, ` + It.transform(Ct(n2)) + `, ` + Ct(xt.transform(r2)) + `)` }, J = { test: (e2) => jt.test(e2) || Nt.test(e2) || Bt.test(e2), parse: (e2) => jt.test(e2) ? jt.parse(e2) : Bt.test(e2) ? Bt.parse(e2) : Nt.parse(e2), transform: (e2) => typeof e2 == `string` ? e2 : e2.hasOwnProperty(`red`) ? jt.transform(e2) : Bt.transform(e2), getAnimatableNone: (e2) => {
  let t2 = J.parse(e2);
  return t2.alpha = 0, J.transform(t2);
} }, Vt = /(?:#[\da-f]{3,8}|(?:rgb|hsl)a?\((?:-?[\d.]+%?[,\s]+){2}-?[\d.]+%?\s*(?:[,/]\s*)?(?:\b\d+(?:\.\d+)?|\.\d+)?%?\))/giu;
function Ht(e2) {
  return isNaN(e2) && typeof e2 == `string` && (e2.match(wt)?.length || 0) + (e2.match(Vt)?.length || 0) > 0;
}
var Ut = `number`, Wt = `color`, Gt = `var`, Kt = `var(`, qt = "${}", Jt = /var\s*\(\s*--(?:[\w-]+\s*|[\w-]+\s*,(?:\s*[^)(\s]|\s*\((?:[^)(]|\([^)(]*\))*\))+\s*)\)|#[\da-f]{3,8}|(?:rgb|hsl)a?\((?:-?[\d.]+%?[,\s]+){2}-?[\d.]+%?\s*(?:[,/]\s*)?(?:\b\d+(?:\.\d+)?|\.\d+)?%?\)|-?(?:\d+(?:\.\d+)?|\.\d+)/giu;
function Yt(e2) {
  let t2 = e2.toString(), n2 = [], r2 = { color: [], number: [], var: [] }, i2 = [], a2 = 0;
  return { values: n2, split: t2.replace(Jt, (e3) => (J.test(e3) ? (r2.color.push(a2), i2.push(Wt), n2.push(J.parse(e3))) : e3.startsWith(Kt) ? (r2.var.push(a2), i2.push(Gt), n2.push(e3)) : (r2.number.push(a2), i2.push(Ut), n2.push(parseFloat(e3))), ++a2, qt)).split(qt), indexes: r2, types: i2 };
}
function Xt(e2) {
  return Yt(e2).values;
}
function Zt({ split: e2, types: t2 }) {
  let n2 = e2.length;
  return (r2) => {
    let i2 = ``;
    for (let a2 = 0; a2 < n2; a2++) if (i2 += e2[a2], r2[a2] !== void 0) {
      let e3 = t2[a2];
      e3 === Ut ? i2 += Ct(r2[a2]) : e3 === Wt ? i2 += J.transform(r2[a2]) : i2 += r2[a2];
    }
    return i2;
  };
}
function Qt(e2) {
  return Zt(Yt(e2));
}
var $t = (e2) => typeof e2 == `number` ? 0 : J.test(e2) ? J.getAnimatableNone(e2) : e2, en = (e2, t2) => typeof e2 == `number` ? t2?.trim().endsWith(`/`) ? e2 : 0 : $t(e2);
function tn(e2) {
  let t2 = Yt(e2);
  return Zt(t2)(t2.values.map((e3, n2) => en(e3, t2.split[n2])));
}
var nn = { test: Ht, parse: Xt, createTransformer: Qt, getAnimatableNone: tn };
function rn(e2, t2, n2) {
  return n2 < 0 && (n2 += 1), n2 > 1 && --n2, n2 < 1 / 6 ? e2 + (t2 - e2) * 6 * n2 : n2 < 1 / 2 ? t2 : n2 < 2 / 3 ? e2 + (t2 - e2) * (2 / 3 - n2) * 6 : e2;
}
function an({ hue: e2, saturation: t2, lightness: n2, alpha: r2 }) {
  e2 /= 360, t2 /= 100, n2 /= 100;
  let i2 = 0, a2 = 0, o2 = 0;
  if (!t2) i2 = a2 = o2 = n2;
  else {
    let r3 = n2 < 0.5 ? n2 * (1 + t2) : n2 + t2 - n2 * t2, s2 = 2 * n2 - r3;
    i2 = rn(s2, r3, e2 + 1 / 3), a2 = rn(s2, r3, e2), o2 = rn(s2, r3, e2 - 1 / 3);
  }
  return { red: Math.round(i2 * 255), green: Math.round(a2 * 255), blue: Math.round(o2 * 255), alpha: r2 };
}
function on(e2, t2) {
  return (n2) => n2 > 0 ? t2 : e2;
}
var Y = (e2, t2, n2) => e2 + (t2 - e2) * n2, sn = (e2, t2, n2) => {
  let r2 = e2 * e2, i2 = n2 * (t2 * t2 - r2) + r2;
  return i2 < 0 ? 0 : Math.sqrt(i2);
}, cn = [Nt, jt, Bt], ln = (e2) => cn.find((t2) => t2.test(e2));
function un(e2) {
  let t2 = ln(e2);
  if (`${e2}`, !t2) return false;
  let n2 = t2.parse(e2);
  return t2 === Bt && (n2 = an(n2)), n2;
}
var dn = (e2, t2) => {
  let n2 = un(e2), r2 = un(t2);
  if (!n2 || !r2) return on(e2, t2);
  let i2 = { ...n2 };
  return (e3) => (i2.red = sn(n2.red, r2.red, e3), i2.green = sn(n2.green, r2.green, e3), i2.blue = sn(n2.blue, r2.blue, e3), i2.alpha = Y(n2.alpha, r2.alpha, e3), jt.transform(i2));
}, fn = /* @__PURE__ */ new Set([`none`, `hidden`]);
function pn(e2, t2) {
  return fn.has(e2) ? (n2) => n2 <= 0 ? e2 : t2 : (n2) => n2 >= 1 ? t2 : e2;
}
function mn(e2, t2) {
  return (n2) => Y(e2, t2, n2);
}
function hn(e2) {
  return typeof e2 == `number` ? mn : typeof e2 == `string` ? _t(e2) ? on : J.test(e2) ? dn : yn : Array.isArray(e2) ? gn : typeof e2 == `object` ? J.test(e2) ? dn : _n : on;
}
function gn(e2, t2) {
  let n2 = [...e2], r2 = n2.length, i2 = e2.map((e3, n3) => hn(e3)(e3, t2[n3]));
  return (e3) => {
    for (let t3 = 0; t3 < r2; t3++) n2[t3] = i2[t3](e3);
    return n2;
  };
}
function _n(e2, t2) {
  let n2 = { ...e2, ...t2 }, r2 = {};
  for (let i2 in n2) e2[i2] !== void 0 && t2[i2] !== void 0 && (r2[i2] = hn(e2[i2])(e2[i2], t2[i2]));
  return (e3) => {
    for (let t3 in r2) n2[t3] = r2[t3](e3);
    return n2;
  };
}
function vn(e2, t2) {
  let n2 = [], r2 = { color: 0, var: 0, number: 0 };
  for (let i2 = 0; i2 < t2.values.length; i2++) {
    let a2 = t2.types[i2], o2 = e2.indexes[a2][r2[a2]];
    n2[i2] = e2.values[o2] ?? 0, r2[a2]++;
  }
  return n2;
}
var yn = (e2, t2) => {
  let n2 = nn.createTransformer(t2), r2 = Yt(e2), i2 = Yt(t2);
  return r2.indexes.var.length === i2.indexes.var.length && r2.indexes.color.length === i2.indexes.color.length && r2.indexes.number.length >= i2.indexes.number.length ? fn.has(e2) && !i2.values.length || fn.has(t2) && !r2.values.length ? pn(e2, t2) : Fe(gn(vn(r2, i2), i2.values), n2) : (`${e2}${t2}`, on(e2, t2));
};
function bn(e2, t2, n2) {
  return typeof e2 == `number` && typeof t2 == `number` && typeof n2 == `number` ? Y(e2, t2, n2) : hn(e2)(e2, t2);
}
var xn = (e2) => {
  let t2 = ({ timestamp: t3 }) => e2(t3);
  return { start: (e3 = true) => W.update(t2, e3), stop: () => ut(t2), now: () => G.isProcessing ? G.timestamp : K.now() };
}, Sn = (e2, t2, n2 = 10) => {
  let r2 = ``, i2 = Math.max(Math.round(t2 / n2), 2);
  for (let t3 = 0; t3 < i2; t3++) r2 += Math.round(e2(t3 / (i2 - 1)) * 1e4) / 1e4 + `, `;
  return `linear(${r2.substring(0, r2.length - 2)})`;
}, Cn = 2e4;
function wn(e2) {
  let t2 = 0, n2 = e2.next(t2);
  for (; !n2.done && t2 < 2e4; ) t2 += 50, n2 = e2.next(t2);
  return t2 >= 2e4 ? 1 / 0 : t2;
}
function Tn(e2, t2 = 100, n2) {
  let r2 = n2({ ...e2, keyframes: [0, t2] }), i2 = Math.min(wn(r2), Cn);
  return { type: `keyframes`, ease: (e3) => r2.next(i2 * e3).value / t2, duration: U(i2) };
}
var X = { stiffness: 100, damping: 10, mass: 1, velocity: 0, duration: 800, bounce: 0.3, visualDuration: 0.3, restSpeed: { granular: 0.01, default: 2 }, restDelta: { granular: 5e-3, default: 0.5 }, minDuration: 0.01, maxDuration: 10, minDamping: 0.05, maxDamping: 1 };
function En(e2, t2) {
  return e2 * Math.sqrt(1 - t2 * t2);
}
var Dn = 12;
function On(e2, t2, n2) {
  let r2 = n2;
  for (let n3 = 1; n3 < Dn; n3++) r2 -= e2(r2) / t2(r2);
  return r2;
}
var kn = 1e-3;
function An({ duration: e2 = X.duration, bounce: t2 = X.bounce, velocity: n2 = X.velocity, mass: r2 = X.mass }) {
  let i2, a2;
  X.maxDuration;
  let o2 = 1 - t2;
  o2 = z(X.minDamping, X.maxDamping, o2), e2 = z(X.minDuration, X.maxDuration, U(e2)), o2 < 1 ? (i2 = (t3) => {
    let r3 = t3 * o2, i3 = r3 * e2, a3 = r3 - n2, s3 = En(t3, o2), c3 = Math.exp(-i3);
    return kn - a3 / s3 * c3;
  }, a2 = (t3) => {
    let r3 = t3 * o2 * e2, a3 = r3 * n2 + n2, s3 = o2 ** 2 * t3 ** 2 * e2, c3 = Math.exp(-r3), l2 = En(t3 ** 2, o2);
    return (-i2(t3) + kn > 0 ? -1 : 1) * ((a3 - s3) * c3) / l2;
  }) : (i2 = (t3) => -1e-3 + Math.exp(-t3 * e2) * ((t3 - n2) * e2 + 1), a2 = (t3) => Math.exp(-t3 * e2) * ((n2 - t3) * (e2 * e2)));
  let s2 = 5 / e2, c2 = On(i2, a2, s2);
  if (e2 = H(e2), isNaN(c2)) return { stiffness: X.stiffness, damping: X.damping, duration: e2 };
  {
    let t3 = c2 ** 2 * r2;
    return { stiffness: t3, damping: o2 * 2 * Math.sqrt(r2 * t3), duration: e2 };
  }
}
var jn = [`duration`, `bounce`], Mn = [`stiffness`, `damping`, `mass`];
function Nn(e2, t2) {
  return t2.some((t3) => e2[t3] !== void 0);
}
function Pn(e2) {
  let t2 = { velocity: X.velocity, stiffness: X.stiffness, damping: X.damping, mass: X.mass, isResolvedFromDuration: false, ...e2 };
  if (!Nn(e2, Mn) && Nn(e2, jn)) if (t2.velocity = 0, e2.visualDuration) {
    let n2 = e2.visualDuration, r2 = 2 * Math.PI / (n2 * 1.2), i2 = r2 * r2, a2 = 2 * z(0.05, 1, 1 - (e2.bounce || 0)) * Math.sqrt(i2);
    t2 = { ...t2, mass: X.mass, stiffness: i2, damping: a2 };
  } else {
    let n2 = An({ ...e2, velocity: 0 });
    t2 = { ...t2, ...n2, mass: X.mass }, t2.isResolvedFromDuration = true;
  }
  return t2;
}
function Fn(e2 = X.visualDuration, t2 = X.bounce) {
  let n2 = typeof e2 == `object` ? e2 : { visualDuration: e2, keyframes: [0, 1], bounce: t2 }, { restSpeed: r2, restDelta: i2 } = n2, a2 = n2.keyframes[0], o2 = n2.keyframes[n2.keyframes.length - 1], s2 = { done: false, value: a2 }, { stiffness: c2, damping: l2, mass: u2, duration: d2, velocity: f2, isResolvedFromDuration: p2 } = Pn({ ...n2, velocity: -U(n2.velocity || 0) }), m2 = f2 || 0, h2 = l2 / (2 * Math.sqrt(c2 * u2)), g2 = o2 - a2, _2 = U(Math.sqrt(c2 / u2)), v2 = Math.abs(g2) < 5;
  r2 ||= v2 ? X.restSpeed.granular : X.restSpeed.default, i2 ||= v2 ? X.restDelta.granular : X.restDelta.default;
  let y2, b2, x2, S2, C2, w2;
  if (h2 < 1) x2 = En(_2, h2), S2 = (m2 + h2 * _2 * g2) / x2, y2 = (e3) => {
    let t3 = Math.exp(-h2 * _2 * e3);
    return o2 - t3 * (S2 * Math.sin(x2 * e3) + g2 * Math.cos(x2 * e3));
  }, C2 = h2 * _2 * S2 + g2 * x2, w2 = h2 * _2 * g2 - S2 * x2, b2 = (e3) => Math.exp(-h2 * _2 * e3) * (C2 * Math.sin(x2 * e3) + w2 * Math.cos(x2 * e3));
  else if (h2 === 1) {
    y2 = (e4) => o2 - Math.exp(-_2 * e4) * (g2 + (m2 + _2 * g2) * e4);
    let e3 = m2 + _2 * g2;
    b2 = (t3) => Math.exp(-_2 * t3) * (_2 * e3 * t3 - m2);
  } else {
    let e3 = _2 * Math.sqrt(h2 * h2 - 1);
    y2 = (t4) => {
      let n4 = Math.exp(-h2 * _2 * t4), r4 = Math.min(e3 * t4, 300);
      return o2 - n4 * ((m2 + h2 * _2 * g2) * Math.sinh(r4) + e3 * g2 * Math.cosh(r4)) / e3;
    };
    let t3 = (m2 + h2 * _2 * g2) / e3, n3 = h2 * _2 * t3 - g2 * e3, r3 = h2 * _2 * g2 - t3 * e3;
    b2 = (t4) => {
      let i3 = Math.exp(-h2 * _2 * t4), a3 = Math.min(e3 * t4, 300);
      return i3 * (n3 * Math.sinh(a3) + r3 * Math.cosh(a3));
    };
  }
  let T2 = { calculatedDuration: p2 && d2 || null, velocity: (e3) => H(b2(e3)), next: (e3) => {
    if (!p2 && h2 < 1) {
      let t4 = Math.exp(-h2 * _2 * e3), n3 = Math.sin(x2 * e3), a3 = Math.cos(x2 * e3), c3 = o2 - t4 * (S2 * n3 + g2 * a3), l3 = H(t4 * (C2 * n3 + w2 * a3));
      return s2.done = Math.abs(l3) <= r2 && Math.abs(o2 - c3) <= i2, s2.value = s2.done ? o2 : c3, s2;
    }
    let t3 = y2(e3);
    if (p2) s2.done = e3 >= d2;
    else {
      let n3 = H(b2(e3));
      s2.done = Math.abs(n3) <= r2 && Math.abs(o2 - t3) <= i2;
    }
    return s2.value = s2.done ? o2 : t3, s2;
  }, toString: () => {
    let e3 = Math.min(wn(T2), Cn), t3 = Sn((t4) => T2.next(e3 * t4).value, e3, 30);
    return e3 + `ms ` + t3;
  }, toTransition: () => {
  } };
  return T2;
}
Fn.applyToOptions = (e2) => {
  let t2 = Tn(e2, 100, Fn);
  return e2.ease = t2.ease, e2.duration = H(t2.duration), e2.type = `keyframes`, e2;
};
var In = 5;
function Ln(e2, t2, n2) {
  let r2 = Math.max(t2 - In, 0);
  return Le(n2 - e2(r2), t2 - r2);
}
function Rn({ keyframes: e2, velocity: t2 = 0, power: n2 = 0.8, timeConstant: r2 = 325, bounceDamping: i2 = 10, bounceStiffness: a2 = 500, modifyTarget: o2, min: s2, max: c2, restDelta: l2 = 0.5, restSpeed: u2 }) {
  let d2 = e2[0], f2 = { done: false, value: d2 }, p2 = (e3) => s2 !== void 0 && e3 < s2 || c2 !== void 0 && e3 > c2, m2 = (e3) => s2 === void 0 ? c2 : c2 === void 0 || Math.abs(s2 - e3) < Math.abs(c2 - e3) ? s2 : c2, h2 = n2 * t2, g2 = d2 + h2, _2 = o2 === void 0 ? g2 : o2(g2);
  _2 !== g2 && (h2 = _2 - d2);
  let v2 = (e3) => -h2 * Math.exp(-e3 / r2), y2 = (e3) => _2 + v2(e3), b2 = (e3) => {
    let t3 = v2(e3), n3 = y2(e3);
    f2.done = Math.abs(t3) <= l2, f2.value = f2.done ? _2 : n3;
  }, x2, S2, C2 = (e3) => {
    p2(f2.value) && (x2 = e3, S2 = Fn({ keyframes: [f2.value, m2(f2.value)], velocity: Ln(y2, e3, f2.value), damping: i2, stiffness: a2, restDelta: l2, restSpeed: u2 }));
  };
  return C2(0), { calculatedDuration: null, next: (e3) => {
    let t3 = false;
    return !S2 && x2 === void 0 && (t3 = true, b2(e3), C2(e3)), x2 !== void 0 && e3 >= x2 ? S2.next(e3 - x2) : (!t3 && b2(e3), f2);
  } };
}
function zn(e2, t2, n2) {
  let r2 = [], i2 = n2 || Ae.mix || bn, a2 = e2.length - 1;
  for (let n3 = 0; n3 < a2; n3++) {
    let a3 = i2(e2[n3], e2[n3 + 1]);
    t2 && (a3 = Fe(Array.isArray(t2) ? t2[n3] || B : t2, a3)), r2.push(a3);
  }
  return r2;
}
function Bn(e2, t2, { clamp: n2 = true, ease: r2, mixer: i2 } = {}) {
  let a2 = e2.length;
  if (t2.length, a2 === 1) return () => t2[0];
  if (a2 === 2 && t2[0] === t2[1]) return () => t2[1];
  let o2 = e2[0] === e2[1];
  e2[0] > e2[a2 - 1] && (e2 = [...e2].reverse(), t2 = [...t2].reverse());
  let s2 = zn(t2, r2, i2), c2 = s2.length, l2 = (n3) => {
    if (o2 && n3 < e2[0]) return t2[0];
    let r3 = 0;
    if (c2 > 1) for (; r3 < e2.length - 2 && !(n3 < e2[r3 + 1]); r3++) ;
    let i3 = Ie(e2[r3], e2[r3 + 1], n3);
    return s2[r3](i3);
  };
  return n2 ? (t3) => l2(z(e2[0], e2[a2 - 1], t3)) : l2;
}
function Vn(e2, t2) {
  let n2 = e2[e2.length - 1];
  for (let r2 = 1; r2 <= t2; r2++) {
    let i2 = Ie(0, t2, r2);
    e2.push(Y(n2, 1, i2));
  }
}
function Hn(e2) {
  let t2 = [0];
  return Vn(t2, e2.length - 1), t2;
}
function Un(e2, t2) {
  return e2.map((e3) => e3 * t2);
}
function Wn(e2, t2) {
  return e2.map(() => t2 || et).splice(0, e2.length - 1);
}
function Gn({ duration: e2 = 300, keyframes: t2, times: n2, ease: r2 = `easeInOut` }) {
  let i2 = tt(r2) ? r2.map(at) : at(r2), a2 = { done: false, value: t2[0] }, o2 = Bn(Un(n2 && n2.length === t2.length ? n2 : Hn(t2), e2), t2, { ease: Array.isArray(i2) ? i2 : Wn(t2, i2) });
  return { calculatedDuration: e2, next: (t3) => (a2.value = o2(t3), a2.done = t3 >= e2, a2) };
}
var Kn = (e2) => e2 !== null;
function qn(e2, { repeat: t2, repeatType: n2 = `loop` }, r2, i2 = 1) {
  let a2 = e2.filter(Kn), o2 = i2 < 0 || t2 && n2 !== `loop` && t2 % 2 == 1 ? 0 : a2.length - 1;
  return !o2 || r2 === void 0 ? a2[o2] : r2;
}
var Jn = { decay: Rn, inertia: Rn, tween: Gn, keyframes: Gn, spring: Fn };
function Yn(e2) {
  typeof e2.type == `string` && (e2.type = Jn[e2.type]);
}
var Xn = class {
  constructor() {
    this.updateFinished();
  }
  get finished() {
    return this._finished;
  }
  updateFinished() {
    this._finished = new Promise((e2) => {
      this.resolve = e2;
    });
  }
  notifyFinished() {
    this.resolve();
  }
  then(e2, t2) {
    return this.finished.then(e2, t2);
  }
}, Zn = (e2) => e2 / 100, Qn = class extends Xn {
  constructor(e2) {
    super(), this.state = `idle`, this.startTime = null, this.isStopped = false, this.currentTime = 0, this.holdTime = null, this.playbackSpeed = 1, this.delayState = { done: false, value: void 0 }, this.stop = () => {
      let { motionValue: e3 } = this.options;
      e3 && e3.updatedAt !== K.now() && this.tick(K.now()), this.isStopped = true, this.state !== `idle` && (this.teardown(), this.options.onStop?.());
    }, this.options = e2, this.initAnimation(), this.play(), e2.autoplay === false && this.pause();
  }
  initAnimation() {
    let { options: e2 } = this;
    Yn(e2);
    let { type: t2 = Gn, repeat: n2 = 0, repeatDelay: r2 = 0, repeatType: i2, velocity: a2 = 0 } = e2, { keyframes: o2 } = e2, s2 = t2 || Gn;
    s2 !== Gn && typeof o2[0] != `number` && (this.mixKeyframes = Fe(Zn, bn(o2[0], o2[1])), o2 = [0, 100]);
    let c2 = s2({ ...e2, keyframes: o2 });
    i2 === `mirror` && (this.mirroredGenerator = s2({ ...e2, keyframes: [...o2].reverse(), velocity: -a2 })), c2.calculatedDuration === null && (c2.calculatedDuration = wn(c2));
    let { calculatedDuration: l2 } = c2;
    this.calculatedDuration = l2, this.resolvedDuration = l2 + r2, this.totalDuration = this.resolvedDuration * (n2 + 1) - r2, this.generator = c2;
  }
  updateTime(e2) {
    let t2 = Math.round(e2 - this.startTime) * this.playbackSpeed;
    this.holdTime === null ? this.currentTime = t2 : this.currentTime = this.holdTime;
  }
  tick(e2, t2 = false) {
    let { generator: n2, totalDuration: r2, mixKeyframes: i2, mirroredGenerator: a2, resolvedDuration: o2, calculatedDuration: s2 } = this;
    if (this.startTime === null) return n2.next(0);
    let { delay: c2 = 0, keyframes: l2, repeat: u2, repeatType: d2, repeatDelay: f2, type: p2, onUpdate: m2, finalKeyframe: h2 } = this.options;
    this.speed > 0 ? this.startTime = Math.min(this.startTime, e2) : this.speed < 0 && (this.startTime = Math.min(e2 - r2 / this.speed, this.startTime)), t2 ? this.currentTime = e2 : this.updateTime(e2);
    let g2 = this.currentTime - c2 * (this.playbackSpeed >= 0 ? 1 : -1), _2 = this.playbackSpeed >= 0 ? g2 < 0 : g2 > r2;
    this.currentTime = Math.max(g2, 0), this.state === `finished` && this.holdTime === null && (this.currentTime = r2);
    let v2 = this.currentTime, y2 = n2;
    if (u2) {
      let e3 = Math.min(this.currentTime, r2) / o2, t3 = Math.floor(e3), n3 = e3 % 1;
      !n3 && e3 >= 1 && (n3 = 1), n3 === 1 && t3--, t3 = Math.min(t3, u2 + 1), t3 % 2 && (d2 === `reverse` ? (n3 = 1 - n3, f2 && (n3 -= f2 / o2)) : d2 === `mirror` && (y2 = a2)), v2 = z(0, 1, n3) * o2;
    }
    let b2;
    _2 ? (this.delayState.value = l2[0], b2 = this.delayState) : b2 = y2.next(v2), i2 && !_2 && (b2.value = i2(b2.value));
    let { done: x2 } = b2;
    !_2 && s2 !== null && (x2 = this.playbackSpeed >= 0 ? this.currentTime >= r2 : this.currentTime <= 0);
    let S2 = this.holdTime === null && (this.state === `finished` || this.state === `running` && x2);
    return S2 && p2 !== Rn && (b2.value = qn(l2, this.options, h2, this.speed)), m2 && m2(b2.value), S2 && this.finish(), b2;
  }
  then(e2, t2) {
    return this.finished.then(e2, t2);
  }
  get duration() {
    return U(this.calculatedDuration);
  }
  get iterationDuration() {
    let { delay: e2 = 0 } = this.options || {};
    return this.duration + U(e2);
  }
  get time() {
    return U(this.currentTime);
  }
  set time(e2) {
    e2 = H(e2), this.currentTime = e2, this.startTime === null || this.holdTime !== null || this.playbackSpeed === 0 ? this.holdTime = e2 : this.driver && (this.startTime = this.driver.now() - e2 / this.playbackSpeed), this.driver ? this.driver.start(false) : (this.startTime = 0, this.state = `paused`, this.holdTime = e2, this.tick(e2));
  }
  getGeneratorVelocity() {
    let e2 = this.currentTime;
    if (e2 <= 0) return this.options.velocity || 0;
    if (this.generator.velocity) return this.generator.velocity(e2);
    let t2 = this.generator.next(e2).value;
    return Ln((e3) => this.generator.next(e3).value, e2, t2);
  }
  get speed() {
    return this.playbackSpeed;
  }
  set speed(e2) {
    let t2 = this.playbackSpeed !== e2;
    t2 && this.driver && this.updateTime(K.now()), this.playbackSpeed = e2, t2 && this.driver && (this.time = U(this.currentTime));
  }
  play() {
    if (this.isStopped) return;
    let { driver: e2 = xn, startTime: t2 } = this.options;
    this.driver ||= e2((e3) => this.tick(e3)), this.options.onPlay?.();
    let n2 = this.driver.now();
    this.state === `finished` ? (this.updateFinished(), this.startTime = n2) : this.holdTime === null ? this.startTime ||= t2 ?? n2 : this.startTime = n2 - this.holdTime, this.state === `finished` && this.speed < 0 && (this.startTime += this.calculatedDuration), this.holdTime = null, this.state = `running`, this.driver.start();
  }
  pause() {
    this.state = `paused`, this.updateTime(K.now()), this.holdTime = this.currentTime;
  }
  complete() {
    this.state !== `running` && this.play(), this.state = `finished`, this.holdTime = null;
  }
  finish() {
    this.notifyFinished(), this.teardown(), this.state = `finished`, this.options.onComplete?.();
  }
  cancel() {
    this.holdTime = null, this.startTime = 0, this.tick(0), this.teardown(), this.options.onCancel?.();
  }
  teardown() {
    this.state = `idle`, this.stopDriver(), this.startTime = this.holdTime = null;
  }
  stopDriver() {
    this.driver &&= (this.driver.stop(), void 0);
  }
  sample(e2) {
    return this.startTime = 0, this.tick(e2, true);
  }
  attachTimeline(e2) {
    return this.options.allowFlatten && (this.options.type = `keyframes`, this.options.ease = `linear`, this.initAnimation()), this.driver?.stop(), e2.observe(this);
  }
};
function $n(e2) {
  for (let t2 = 1; t2 < e2.length; t2++) e2[t2] ?? (e2[t2] = e2[t2 - 1]);
}
var er = (e2) => e2 * 180 / Math.PI, tr = (e2) => rr(er(Math.atan2(e2[1], e2[0]))), nr = { x: 4, y: 5, translateX: 4, translateY: 5, scaleX: 0, scaleY: 3, scale: (e2) => (Math.abs(e2[0]) + Math.abs(e2[3])) / 2, rotate: tr, rotateZ: tr, skewX: (e2) => er(Math.atan(e2[1])), skewY: (e2) => er(Math.atan(e2[2])), skew: (e2) => (Math.abs(e2[1]) + Math.abs(e2[2])) / 2 }, rr = (e2) => (e2 %= 360, e2 < 0 && (e2 += 360), e2), ir = tr, ar = (e2) => Math.sqrt(e2[0] * e2[0] + e2[1] * e2[1]), or = (e2) => Math.sqrt(e2[4] * e2[4] + e2[5] * e2[5]), sr = { x: 12, y: 13, z: 14, translateX: 12, translateY: 13, translateZ: 14, scaleX: ar, scaleY: or, scale: (e2) => (ar(e2) + or(e2)) / 2, rotateX: (e2) => rr(er(Math.atan2(e2[6], e2[5]))), rotateY: (e2) => rr(er(Math.atan2(-e2[2], e2[0]))), rotateZ: ir, rotate: ir, skewX: (e2) => er(Math.atan(e2[4])), skewY: (e2) => er(Math.atan(e2[1])), skew: (e2) => (Math.abs(e2[1]) + Math.abs(e2[4])) / 2 };
function cr(e2) {
  return +!!e2.includes(`scale`);
}
function lr(e2, t2) {
  if (!e2 || e2 === `none`) return cr(t2);
  let n2 = e2.match(/^matrix3d\(([-\d.e\s,]+)\)$/u), r2, i2;
  if (n2) r2 = sr, i2 = n2;
  else {
    let t3 = e2.match(/^matrix\(([-\d.e\s,]+)\)$/u);
    r2 = nr, i2 = t3;
  }
  if (!i2) return cr(t2);
  let a2 = r2[t2], o2 = i2[1].split(`,`).map(dr);
  return typeof a2 == `function` ? a2(o2) : o2[a2];
}
var ur = (e2, t2) => {
  let { transform: n2 = `none` } = getComputedStyle(e2);
  return lr(n2, t2);
};
function dr(e2) {
  return parseFloat(e2.trim());
}
var fr = [`transformPerspective`, `x`, `y`, `z`, `translateX`, `translateY`, `translateZ`, `scale`, `scaleX`, `scaleY`, `rotate`, `rotateX`, `rotateY`, `rotateZ`, `skew`, `skewX`, `skewY`], pr = /* @__PURE__ */ new Set([...fr, `pathRotation`]), mr = (e2) => e2 === bt || e2 === q, hr = /* @__PURE__ */ new Set([`x`, `y`, `z`]), gr = fr.filter((e2) => !hr.has(e2));
function _r(e2) {
  let t2 = [];
  return gr.forEach((n2) => {
    let r2 = e2.getValue(n2);
    r2 !== void 0 && (t2.push([n2, r2.get()]), r2.set(+!!n2.startsWith(`scale`)));
  }), t2;
}
var vr = { width: ({ x: e2 }, { paddingLeft: t2 = `0`, paddingRight: n2 = `0`, boxSizing: r2 }) => {
  let i2 = e2.max - e2.min;
  return r2 === `border-box` ? i2 : i2 - parseFloat(t2) - parseFloat(n2);
}, height: ({ y: e2 }, { paddingTop: t2 = `0`, paddingBottom: n2 = `0`, boxSizing: r2 }) => {
  let i2 = e2.max - e2.min;
  return r2 === `border-box` ? i2 : i2 - parseFloat(t2) - parseFloat(n2);
}, top: (e2, { top: t2 }) => parseFloat(t2), left: (e2, { left: t2 }) => parseFloat(t2), bottom: ({ y: e2 }, { top: t2 }) => parseFloat(t2) + (e2.max - e2.min), right: ({ x: e2 }, { left: t2 }) => parseFloat(t2) + (e2.max - e2.min), x: (e2, { transform: t2 }) => lr(t2, `x`), y: (e2, { transform: t2 }) => lr(t2, `y`) };
vr.translateX = vr.x, vr.translateY = vr.y;
var yr = /* @__PURE__ */ new Set(), br = false, xr = false, Sr = false;
function Cr() {
  if (xr) {
    let e2 = Array.from(yr).filter((e3) => e3.needsMeasurement), t2 = new Set(e2.map((e3) => e3.element)), n2 = /* @__PURE__ */ new Map();
    t2.forEach((e3) => {
      let t3 = _r(e3);
      t3.length && (n2.set(e3, t3), e3.render());
    }), e2.forEach((e3) => e3.measureInitialState()), t2.forEach((e3) => {
      e3.render();
      let t3 = n2.get(e3);
      t3 && t3.forEach(([t4, n3]) => {
        e3.getValue(t4)?.set(n3);
      });
    }), e2.forEach((e3) => e3.measureEndState()), e2.forEach((e3) => {
      e3.suspendedScrollY !== void 0 && window.scrollTo(0, e3.suspendedScrollY);
    });
  }
  xr = false, br = false, yr.forEach((e2) => e2.complete(Sr)), yr.clear();
}
function wr() {
  yr.forEach((e2) => {
    e2.readKeyframes(), e2.needsMeasurement && (xr = true);
  });
}
function Tr() {
  Sr = true, wr(), Cr(), Sr = false;
}
var Er = class {
  constructor(e2, t2, n2, r2, i2, a2 = false) {
    this.state = `pending`, this.isAsync = false, this.needsMeasurement = false, this.unresolvedKeyframes = [...e2], this.onComplete = t2, this.name = n2, this.motionValue = r2, this.element = i2, this.isAsync = a2;
  }
  scheduleResolve() {
    this.state = `scheduled`, this.isAsync ? (yr.add(this), br || (br = true, W.read(wr), W.resolveKeyframes(Cr))) : (this.readKeyframes(), this.complete());
  }
  readKeyframes() {
    let { unresolvedKeyframes: e2, name: t2, element: n2, motionValue: r2 } = this;
    if (e2[0] === null) {
      let i2 = r2?.get(), a2 = e2[e2.length - 1];
      if (i2 !== void 0) e2[0] = i2;
      else if (n2 && t2) {
        let r3 = n2.readValue(t2, a2);
        r3 != null && (e2[0] = r3);
      }
      e2[0] === void 0 && (e2[0] = a2), r2 && i2 === void 0 && r2.set(e2[0]);
    }
    $n(e2);
  }
  setFinalKeyframe() {
  }
  measureInitialState() {
  }
  renderEndStyles() {
  }
  measureEndState() {
  }
  complete(e2 = false) {
    this.state = `complete`, this.onComplete(this.unresolvedKeyframes, this.finalKeyframe, e2), yr.delete(this);
  }
  cancel() {
    this.state === `scheduled` && (yr.delete(this), this.state = `pending`);
  }
  resume() {
    this.state === `pending` && this.scheduleResolve();
  }
}, Dr = (e2) => e2.startsWith(`--`);
function Or(e2, t2, n2) {
  Dr(t2) ? e2.style.setProperty(t2, n2) : e2.style[t2] = n2;
}
var kr = {};
function Ar(e2, t2) {
  let n2 = Pe(e2);
  return () => kr[t2] ?? n2();
}
var jr = Ar(() => window.ScrollTimeline !== void 0, `scrollTimeline`), Mr = Ar(() => {
  try {
    document.createElement(`div`).animate({ opacity: 0 }, { easing: `linear(0, 1)` });
  } catch {
    return false;
  }
  return true;
}, `linearEasing`), Nr = ([e2, t2, n2, r2]) => `cubic-bezier(${e2}, ${t2}, ${n2}, ${r2})`, Pr = { linear: `linear`, ease: `ease`, easeIn: `ease-in`, easeOut: `ease-out`, easeInOut: `ease-in-out`, circIn: Nr([0, 0.65, 0.55, 1]), circOut: Nr([0.55, 0, 1, 0.45]), backIn: Nr([0.31, 0.01, 0.66, -0.59]), backOut: Nr([0.33, 1.53, 0.69, 0.99]) };
function Fr(e2, t2) {
  if (e2) return typeof e2 == `function` ? Mr() ? Sn(e2, t2) : `ease-out` : nt(e2) ? Nr(e2) : Array.isArray(e2) ? e2.map((e3) => Fr(e3, t2) || Pr.easeOut) : Pr[e2];
}
function Ir(e2, t2, n2, { delay: r2 = 0, duration: i2 = 300, repeat: a2 = 0, repeatType: o2 = `loop`, ease: s2 = `easeOut`, times: c2 } = {}, l2 = void 0) {
  let u2 = { [t2]: n2 };
  c2 && (u2.offset = c2);
  let d2 = Fr(s2, i2);
  Array.isArray(d2) && (u2.easing = d2);
  let f2 = { delay: r2, duration: i2, easing: Array.isArray(d2) ? `linear` : d2, fill: `both`, iterations: a2 + 1, direction: o2 === `reverse` ? `alternate` : `normal` };
  return l2 && (f2.pseudoElement = l2), e2.animate(u2, f2);
}
function Lr(e2) {
  return typeof e2 == `function` && `applyToOptions` in e2;
}
function Rr({ type: e2, ...t2 }) {
  return Lr(e2) && Mr() ? e2.applyToOptions(t2) : (t2.duration ??= 300, t2.ease ??= `easeOut`, t2);
}
var zr = class extends Xn {
  constructor(e2) {
    if (super(), this.finishedTime = null, this.isStopped = false, this.manualStartTime = null, !e2) return;
    let { element: t2, name: n2, keyframes: r2, pseudoElement: i2, allowFlatten: a2 = false, finalKeyframe: o2, onComplete: s2 } = e2;
    this.isPseudoElement = !!i2, this.allowFlatten = a2, this.options = e2, e2.type;
    let c2 = Rr(e2);
    this.animation = Ir(t2, n2, r2, c2, i2), c2.autoplay === false && this.animation.pause(), this.animation.onfinish = () => {
      if (this.finishedTime = this.time, !i2) {
        let e3 = qn(r2, this.options, o2, this.speed);
        this.updateMotionValue && this.updateMotionValue(e3), Or(t2, n2, e3), this.animation.cancel();
      }
      s2?.(), this.notifyFinished();
    };
  }
  play() {
    this.isStopped || (this.manualStartTime = null, this.animation.play(), this.state === `finished` && this.updateFinished());
  }
  pause() {
    this.animation.pause();
  }
  complete() {
    this.animation.finish?.();
  }
  cancel() {
    try {
      this.animation.cancel();
    } catch {
    }
  }
  stop() {
    if (this.isStopped) return;
    this.isStopped = true;
    let { state: e2 } = this;
    e2 === `idle` || e2 === `finished` || (this.updateMotionValue ? this.updateMotionValue() : this.commitStyles(), this.isPseudoElement || this.cancel());
  }
  commitStyles() {
    let e2 = this.options?.element;
    !this.isPseudoElement && e2?.isConnected && this.animation.commitStyles?.();
  }
  get duration() {
    let e2 = this.animation.effect?.getComputedTiming?.().duration || 0;
    return U(Number(e2));
  }
  get iterationDuration() {
    let { delay: e2 = 0 } = this.options || {};
    return this.duration + U(e2);
  }
  get time() {
    return U(Number(this.animation.currentTime) || 0);
  }
  set time(e2) {
    let t2 = this.finishedTime !== null;
    this.manualStartTime = null, this.finishedTime = null, this.animation.currentTime = H(e2), t2 && this.animation.pause();
  }
  get speed() {
    return this.animation.playbackRate;
  }
  set speed(e2) {
    e2 < 0 && (this.finishedTime = null), this.animation.playbackRate = e2;
  }
  get state() {
    return this.finishedTime === null ? this.animation.playState : `finished`;
  }
  get startTime() {
    return this.manualStartTime ?? Number(this.animation.startTime);
  }
  set startTime(e2) {
    this.manualStartTime = this.animation.startTime = e2;
  }
  attachTimeline({ timeline: e2, rangeStart: t2, rangeEnd: n2, observe: r2 }) {
    return this.allowFlatten && this.animation.effect?.updateTiming({ easing: `linear` }), this.animation.onfinish = null, e2 && jr() ? (this.animation.timeline = e2, t2 && (this.animation.rangeStart = t2), n2 && (this.animation.rangeEnd = n2), B) : r2(this);
  }
}, Br = { anticipate: Je, backInOut: qe, circInOut: Ze };
function Vr(e2) {
  return e2 in Br;
}
function Hr(e2) {
  typeof e2.ease == `string` && Vr(e2.ease) && (e2.ease = Br[e2.ease]);
}
var Ur = 10, Wr = class extends zr {
  constructor(e2) {
    Hr(e2), Yn(e2), super(e2), e2.startTime !== void 0 && e2.autoplay !== false && (this.startTime = e2.startTime), this.options = e2;
  }
  updateMotionValue(e2) {
    let { motionValue: t2, onUpdate: n2, onComplete: r2, element: i2, ...a2 } = this.options;
    if (!t2) return;
    if (e2 !== void 0) {
      t2.set(e2);
      return;
    }
    let o2 = new Qn({ ...a2, autoplay: false }), s2 = Math.max(Ur, K.now() - this.startTime), c2 = z(0, Ur, s2 - Ur), l2 = o2.sample(s2).value, { name: u2 } = this.options;
    i2 && u2 && Or(i2, u2, l2), t2.setWithVelocity(o2.sample(Math.max(0, s2 - c2)).value, l2, c2), o2.stop();
  }
}, Gr = (e2, t2) => t2 === `zIndex` ? false : !!(typeof e2 == `number` || Array.isArray(e2) || typeof e2 == `string` && (nn.test(e2) || e2 === `0`) && !e2.startsWith(`url(`));
function Kr(e2) {
  let t2 = e2[0];
  if (e2.length === 1) return true;
  for (let n2 = 0; n2 < e2.length; n2++) if (e2[n2] !== t2) return true;
}
function qr(e2, t2, n2, r2) {
  let i2 = e2[0];
  if (i2 === null) return false;
  if (t2 === `display` || t2 === `visibility`) return true;
  let a2 = e2[e2.length - 1], o2 = Gr(i2, t2), s2 = Gr(a2, t2);
  return `${t2}${i2}${a2}${o2 ? a2 : i2}`, !o2 || !s2 ? false : Kr(e2) || (n2 === `spring` || Lr(n2)) && r2;
}
function Jr(e2) {
  e2.duration = 0, e2.type = `keyframes`;
}
var Yr = /* @__PURE__ */ new Set([`opacity`, `clipPath`, `filter`, `transform`]), Xr = /^(?:oklch|oklab|lab|lch|color|color-mix|light-dark)\(/;
function Zr(e2) {
  for (let t2 = 0; t2 < e2.length; t2++) if (typeof e2[t2] == `string` && Xr.test(e2[t2])) return true;
  return false;
}
var Qr = /* @__PURE__ */ new Set([`color`, `backgroundColor`, `outlineColor`, `fill`, `stroke`, `borderColor`, `borderTopColor`, `borderRightColor`, `borderBottomColor`, `borderLeftColor`]), $r = Pe(() => Object.hasOwnProperty.call(Element.prototype, `animate`));
function ei(e2) {
  let { motionValue: t2, name: n2, repeatDelay: r2, repeatType: i2, damping: a2, type: o2, keyframes: s2 } = e2;
  if (!(t2?.owner?.current instanceof HTMLElement)) return false;
  let { onUpdate: c2, transformTemplate: l2 } = t2.owner.getProps();
  return $r() && n2 && (Yr.has(n2) || Qr.has(n2) && Zr(s2)) && (n2 !== `transform` || !l2) && !c2 && !r2 && i2 !== `mirror` && a2 !== 0 && o2 !== `inertia`;
}
var ti = 40, ni = class extends Xn {
  constructor({ autoplay: e2 = true, delay: t2 = 0, type: n2 = `keyframes`, repeat: r2 = 0, repeatDelay: i2 = 0, repeatType: a2 = `loop`, keyframes: o2, name: s2, motionValue: c2, element: l2, ...u2 }) {
    super(), this.stop = () => {
      this._animation && (this._animation.stop(), this.stopTimeline?.()), this.keyframeResolver?.cancel();
    }, this.createdAt = K.now();
    let d2 = { autoplay: e2, delay: t2, type: n2, repeat: r2, repeatDelay: i2, repeatType: a2, name: s2, motionValue: c2, element: l2, ...u2 }, f2 = l2?.KeyframeResolver || Er;
    this.keyframeResolver = new f2(o2, (e3, t3, n3) => this.onKeyframesResolved(e3, t3, d2, !n3), s2, c2, l2), this.keyframeResolver?.scheduleResolve();
  }
  onKeyframesResolved(e2, t2, n2, r2) {
    this.keyframeResolver = void 0;
    let { name: i2, type: a2, velocity: o2, delay: s2, isHandoff: c2, onUpdate: l2 } = n2;
    this.resolvedAt = K.now();
    let u2 = true;
    qr(e2, i2, a2, o2) || (u2 = false, (Ae.instantAnimations || !s2) && l2?.(qn(e2, n2, t2)), e2[0] = e2[e2.length - 1], Jr(n2), n2.repeat = 0);
    let d2 = { startTime: r2 ? this.resolvedAt && this.resolvedAt - this.createdAt > ti ? this.resolvedAt : this.createdAt : void 0, finalKeyframe: t2, ...n2, keyframes: e2 }, f2 = u2 && !c2 && ei(d2), p2 = d2.motionValue?.owner?.current, m2;
    if (f2) try {
      m2 = new Wr({ ...d2, element: p2 });
    } catch {
      m2 = new Qn(d2);
    }
    else m2 = new Qn(d2);
    m2.finished.then(() => {
      this.notifyFinished();
    }).catch(B), this.pendingTimeline &&= (this.stopTimeline = m2.attachTimeline(this.pendingTimeline), void 0), this._animation = m2;
  }
  get finished() {
    return this._animation ? this.animation.finished : this._finished;
  }
  then(e2, t2) {
    return this.finished.finally(e2).then(() => {
    });
  }
  get animation() {
    return this._animation || (this.keyframeResolver?.resume(), Tr()), this._animation;
  }
  get duration() {
    return this.animation.duration;
  }
  get iterationDuration() {
    return this.animation.iterationDuration;
  }
  get time() {
    return this.animation.time;
  }
  set time(e2) {
    this.animation.time = e2;
  }
  get speed() {
    return this.animation.speed;
  }
  get state() {
    return this.animation.state;
  }
  set speed(e2) {
    this.animation.speed = e2;
  }
  get startTime() {
    return this.animation.startTime;
  }
  attachTimeline(e2) {
    return this._animation ? this.stopTimeline = this.animation.attachTimeline(e2) : this.pendingTimeline = e2, () => this.stop();
  }
  play() {
    this.animation.play();
  }
  pause() {
    this.animation.pause();
  }
  complete() {
    this.animation.complete();
  }
  cancel() {
    this._animation && this.animation.cancel(), this.keyframeResolver?.cancel();
  }
};
function ri(e2, t2, n2, r2 = 0, i2 = 1) {
  let a2 = Array.from(e2).sort((e3, t3) => e3.sortNodePosition(t3)).indexOf(t2), o2 = e2.size, s2 = (o2 - 1) * r2;
  return typeof n2 == `function` ? n2(a2, o2) : i2 === 1 ? a2 * r2 : s2 - a2 * r2;
}
var ii = 30, ai = (e2) => !isNaN(parseFloat(e2)), oi = { current: void 0 }, si = class {
  constructor(e2, t2 = {}) {
    this.canTrackVelocity = null, this.events = {}, this.updateAndNotify = (e3) => {
      let t3 = K.now();
      if (this.updatedAt !== t3 && this.setPrevFrameValue(), this.prev = this.current, this.setCurrent(e3), this.current !== this.prev && (this.events.change?.notify(this.current), this.dependents)) for (let e4 of this.dependents) e4.dirty();
    }, this.hasAnimated = false, this.setCurrent(e2), this.owner = t2.owner;
  }
  setCurrent(e2) {
    this.current = e2, this.updatedAt = K.now(), this.canTrackVelocity === null && e2 !== void 0 && (this.canTrackVelocity = ai(this.current));
  }
  setPrevFrameValue(e2 = this.current) {
    this.prevFrameValue = e2, this.prevUpdatedAt = this.updatedAt;
  }
  onChange(e2) {
    return this.on(`change`, e2);
  }
  on(e2, t2) {
    this.events[e2] || (this.events[e2] = new V());
    let n2 = this.events[e2].add(t2);
    return e2 === `change` ? () => {
      n2(), W.read(() => {
        this.events.change.getSize() || this.stop();
      });
    } : n2;
  }
  clearListeners() {
    for (let e2 in this.events) this.events[e2].clear();
  }
  attach(e2, t2) {
    this.passiveEffect = e2, this.stopPassiveEffect = t2;
  }
  set(e2) {
    this.passiveEffect ? this.passiveEffect(e2, this.updateAndNotify) : this.updateAndNotify(e2);
  }
  setWithVelocity(e2, t2, n2) {
    this.set(t2), this.prev = void 0, this.prevFrameValue = e2, this.prevUpdatedAt = this.updatedAt - n2;
  }
  jump(e2, t2 = true) {
    this.updateAndNotify(e2), this.prev = e2, this.prevUpdatedAt = this.prevFrameValue = void 0, t2 && this.stop(), this.stopPassiveEffect && this.stopPassiveEffect();
  }
  dirty() {
    this.events.change?.notify(this.current);
  }
  addDependent(e2) {
    this.dependents ||= /* @__PURE__ */ new Set(), this.dependents.add(e2);
  }
  removeDependent(e2) {
    this.dependents && this.dependents.delete(e2);
  }
  get() {
    return oi.current && oi.current.push(this), this.current;
  }
  getPrevious() {
    return this.prev;
  }
  getVelocity() {
    let e2 = K.now();
    if (!this.canTrackVelocity || this.prevFrameValue === void 0 || e2 - this.updatedAt > ii) return 0;
    let t2 = Math.min(this.updatedAt - this.prevUpdatedAt, ii);
    return Le(parseFloat(this.current) - parseFloat(this.prevFrameValue), t2);
  }
  start(e2) {
    return this.stop(), new Promise((t2) => {
      this.hasAnimated = true, this.animation = e2(t2), this.events.animationStart && this.events.animationStart.notify();
    }).then(() => {
      this.events.animationComplete && this.events.animationComplete.notify(), this.clearAnimation();
    });
  }
  stop() {
    this.animation && (this.animation.stop(), this.events.animationCancel && this.events.animationCancel.notify()), this.clearAnimation();
  }
  isAnimating() {
    return !!this.animation;
  }
  clearAnimation() {
    delete this.animation;
  }
  destroy() {
    this.dependents?.clear(), this.events.destroy?.notify(), this.clearListeners(), this.stop(), this.stopPassiveEffect && this.stopPassiveEffect();
  }
};
function ci(e2, t2) {
  return new si(e2, t2);
}
function li(e2, t2) {
  if (e2?.inherit && t2) {
    let { inherit: n2, ...r2 } = e2;
    return { ...t2, ...r2 };
  }
  return e2;
}
function ui(e2, t2) {
  let n2 = e2?.[t2] ?? e2?.default ?? e2;
  return n2 === e2 ? n2 : li(n2, e2);
}
var di = { type: `spring`, stiffness: 500, damping: 25, restSpeed: 10 }, fi = (e2) => ({ type: `spring`, stiffness: 550, damping: e2 === 0 ? 2 * Math.sqrt(550) : 30, restSpeed: 10 }), pi = { type: `keyframes`, duration: 0.8 }, mi = { type: `keyframes`, ease: [0.25, 0.1, 0.35, 1], duration: 0.3 }, hi = (e2, { keyframes: t2 }) => t2.length > 2 ? pi : pr.has(e2) ? e2.startsWith(`scale`) ? fi(t2[1]) : di : mi, gi = /* @__PURE__ */ new Set([`when`, `delay`, `delayChildren`, `staggerChildren`, `staggerDirection`, `repeat`, `repeatType`, `repeatDelay`, `from`, `elapsed`]);
function _i(e2) {
  for (let t2 in e2) if (!gi.has(t2)) return true;
  return false;
}
var vi = (e2, t2, n2, r2 = {}, i2, a2) => (o2) => {
  let s2 = ui(r2, e2) || {}, c2 = s2.delay || r2.delay || 0, { elapsed: l2 = 0 } = r2;
  l2 -= H(c2);
  let u2 = { keyframes: Array.isArray(n2) ? n2 : [null, n2], ease: `easeOut`, velocity: t2.getVelocity(), ...s2, delay: -l2, onUpdate: (e3) => {
    t2.set(e3), s2.onUpdate && s2.onUpdate(e3);
  }, onComplete: () => {
    o2(), s2.onComplete && s2.onComplete();
  }, name: e2, motionValue: t2, element: a2 ? void 0 : i2 };
  _i(s2) || Object.assign(u2, hi(e2, u2)), u2.duration &&= H(u2.duration), u2.repeatDelay &&= H(u2.repeatDelay), u2.from !== void 0 && (u2.keyframes[0] = u2.from);
  let d2 = false;
  if ((u2.type === false || u2.duration === 0 && !u2.repeatDelay) && (Jr(u2), u2.delay === 0 && (d2 = true)), (Ae.instantAnimations || Ae.skipAnimations || i2?.shouldSkipAnimations || s2.skipAnimations) && (d2 = true, Jr(u2), u2.delay = 0), u2.allowFlatten = !s2.type && !s2.ease, d2 && !a2 && t2.get() !== void 0) {
    let e3 = qn(u2.keyframes, s2);
    if (e3 !== void 0) {
      W.update(() => {
        u2.onUpdate(e3), u2.onComplete();
      });
      return;
    }
  }
  return s2.isSync ? new Qn(u2) : new ni(u2);
}, yi = /^var\(--(?:([\w-]+)|([\w-]+), ?([a-zA-Z\d ()%#.,-]+))\)/u;
function bi(e2) {
  let t2 = yi.exec(e2);
  if (!t2) return [,];
  let [, n2, r2, i2] = t2;
  return [`--${n2 ?? r2}`, i2];
}
function xi(e2, t2, n2 = 1) {
  `${e2}`;
  let [r2, i2] = bi(e2);
  if (!r2) return;
  let a2 = window.getComputedStyle(t2).getPropertyValue(r2);
  if (a2) {
    let e3 = a2.trim();
    return je(e3) ? parseFloat(e3) : e3;
  }
  return _t(i2) ? xi(i2, t2, n2 + 1) : i2;
}
function Si(e2) {
  let t2 = [{}, {}];
  return e2?.values.forEach((e3, n2) => {
    t2[0][n2] = e3.get(), t2[1][n2] = e3.getVelocity();
  }), t2;
}
function Ci(e2, t2, n2, r2) {
  if (typeof t2 == `function`) {
    let [i2, a2] = Si(r2);
    t2 = t2(n2 === void 0 ? e2.custom : n2, i2, a2);
  }
  if (typeof t2 == `string` && (t2 = e2.variants && e2.variants[t2]), typeof t2 == `function`) {
    let [i2, a2] = Si(r2);
    t2 = t2(n2 === void 0 ? e2.custom : n2, i2, a2);
  }
  return t2;
}
function wi(e2, t2, n2) {
  let r2 = e2.getProps();
  return Ci(r2, t2, n2 === void 0 ? r2.custom : n2, e2);
}
var Ti = /* @__PURE__ */ new Set([`width`, `height`, `top`, `left`, `right`, `bottom`, ...fr]), Ei = (e2) => Array.isArray(e2);
function Di(e2, t2, n2) {
  e2.hasValue(t2) ? e2.getValue(t2).set(n2) : e2.addValue(t2, ci(n2));
}
function Oi(e2) {
  return Ei(e2) ? e2[e2.length - 1] || 0 : e2;
}
function ki(e2, t2) {
  let { transitionEnd: n2 = {}, transition: r2 = {}, ...i2 } = wi(e2, t2) || {};
  i2 = { ...i2, ...n2 };
  for (let t3 in i2) Di(e2, t3, Oi(i2[t3]));
}
var Z = (e2) => !!(e2 && e2.getVelocity);
function Ai(e2) {
  return !!(Z(e2) && e2.add);
}
function ji(e2, t2) {
  let n2 = e2.getValue(`willChange`);
  if (Ai(n2)) return n2.add(t2);
  if (!n2 && Ae.WillChange) {
    let n3 = new Ae.WillChange(`auto`);
    e2.addValue(`willChange`, n3), n3.add(t2);
  }
}
function Mi(e2) {
  return e2.replace(/([A-Z])/g, (e3) => `-${e3.toLowerCase()}`);
}
var Ni = `data-` + Mi(`framerAppearId`);
function Pi(e2) {
  return e2.props[Ni];
}
function Fi({ protectedKeys: e2, needsAnimating: t2 }, n2) {
  let r2 = e2.hasOwnProperty(n2) && t2[n2] !== true;
  return t2[n2] = false, r2;
}
function Ii(e2, t2, { delay: n2 = 0, transitionOverride: r2, type: i2 } = {}) {
  let { transition: a2, transitionEnd: o2, ...s2 } = t2, c2 = e2.getDefaultTransition();
  a2 = a2 ? li(a2, c2) : c2;
  let l2 = a2?.reduceMotion, u2 = a2?.skipAnimations;
  r2 && (a2 = r2);
  let d2 = [], f2 = i2 && e2.animationState && e2.animationState.getState()[i2], p2 = a2?.path;
  p2 && p2.animateVisualElement(e2, s2, a2, n2, d2);
  for (let t3 in s2) {
    let r3 = e2.getValue(t3, e2.latestValues[t3] ?? null), i3 = s2[t3];
    if (i3 === void 0 || f2 && Fi(f2, t3)) continue;
    let o3 = { delay: n2, ...ui(a2 || {}, t3) };
    u2 && (o3.skipAnimations = true);
    let c3 = r3.get();
    if (c3 !== void 0 && !r3.isAnimating() && !Array.isArray(i3) && i3 === c3 && !o3.velocity) {
      W.update(() => r3.set(i3));
      continue;
    }
    let p3 = false;
    if (window.MotionHandoffAnimation) {
      let n3 = Pi(e2);
      if (n3) {
        let e3 = window.MotionHandoffAnimation(n3, t3, W);
        e3 !== null && (o3.startTime = e3, p3 = true);
      }
    }
    ji(e2, t3);
    let m2 = l2 ?? e2.shouldReduceMotion;
    r3.start(vi(t3, r3, i3, m2 && Ti.has(t3) ? { type: false } : o3, e2, p3));
    let h2 = r3.animation;
    h2 && d2.push(h2);
  }
  if (o2) {
    let t3 = () => W.update(() => {
      o2 && ki(e2, o2);
    });
    d2.length ? Promise.all(d2).then(t3) : t3();
  }
  return d2;
}
function Li(e2, t2, n2 = {}) {
  let r2 = wi(e2, t2, n2.type === `exit` ? e2.presenceContext?.custom : void 0), { transition: i2 = e2.getDefaultTransition() || {} } = r2 || {};
  n2.transitionOverride && (i2 = n2.transitionOverride);
  let a2 = r2 ? () => Promise.all(Ii(e2, r2, n2)) : () => Promise.resolve(), o2 = e2.variantChildren && e2.variantChildren.size ? (r3 = 0) => {
    let { delayChildren: a3 = 0, staggerChildren: o3, staggerDirection: s3 } = i2;
    return Ri(e2, t2, r3, a3, o3, s3, n2);
  } : () => Promise.resolve(), { when: s2 } = i2;
  if (s2) {
    let [e3, t3] = s2 === `beforeChildren` ? [a2, o2] : [o2, a2];
    return e3().then(() => t3());
  } else return Promise.all([a2(), o2(n2.delay)]);
}
function Ri(e2, t2, n2 = 0, r2 = 0, i2 = 0, a2 = 1, o2) {
  let s2 = [];
  for (let c2 of e2.variantChildren) c2.notify(`AnimationStart`, t2), s2.push(Li(c2, t2, { ...o2, delay: n2 + (typeof r2 == `function` ? 0 : r2) + ri(e2.variantChildren, c2, r2, i2, a2) }).then(() => c2.notify(`AnimationComplete`, t2)));
  return Promise.all(s2);
}
function zi(e2, t2, n2 = {}) {
  e2.notify(`AnimationStart`, t2);
  let r2;
  if (Array.isArray(t2)) {
    let i2 = t2.map((t3) => Li(e2, t3, n2));
    r2 = Promise.all(i2);
  } else if (typeof t2 == `string`) r2 = Li(e2, t2, n2);
  else {
    let i2 = typeof t2 == `function` ? wi(e2, t2, n2.custom) : t2;
    r2 = Promise.all(Ii(e2, i2, n2));
  }
  return r2.then(() => {
    e2.notify(`AnimationComplete`, t2);
  });
}
var Bi = { test: (e2) => e2 === `auto`, parse: (e2) => e2 }, Vi = (e2) => (t2) => t2.test(e2), Hi = [bt, q, It, Ft, Rt, Lt, Bi], Ui = (e2) => Hi.find(Vi(e2));
function Wi(e2) {
  return typeof e2 == `number` ? e2 === 0 : e2 === null ? true : e2 === `none` || e2 === `0` || Ne(e2);
}
var Gi = /* @__PURE__ */ new Set([`brightness`, `contrast`, `saturate`, `opacity`]);
function Ki(e2) {
  let [t2, n2] = e2.slice(0, -1).split(`(`);
  if (t2 === `drop-shadow`) return e2;
  let [r2] = n2.match(wt) || [];
  if (!r2) return e2;
  let i2 = n2.replace(r2, ``), a2 = +!!Gi.has(t2);
  return r2 !== n2 && (a2 *= 100), t2 + `(` + a2 + i2 + `)`;
}
var qi = /\b([a-z-]*)\(.*?\)/gu, Ji = { ...nn, getAnimatableNone: (e2) => {
  let t2 = e2.match(qi);
  return t2 ? t2.map(Ki).join(` `) : e2;
} }, Yi = { ...nn, getAnimatableNone: (e2) => {
  let t2 = nn.parse(e2);
  return nn.createTransformer(e2)(t2.map((e3) => typeof e3 == `number` ? 0 : typeof e3 == `object` ? { ...e3, alpha: 1 } : e3));
} }, Xi = { ...bt, transform: Math.round }, Zi = { borderWidth: q, borderTopWidth: q, borderRightWidth: q, borderBottomWidth: q, borderLeftWidth: q, borderRadius: q, borderTopLeftRadius: q, borderTopRightRadius: q, borderBottomRightRadius: q, borderBottomLeftRadius: q, width: q, maxWidth: q, height: q, maxHeight: q, top: q, right: q, bottom: q, left: q, inset: q, insetBlock: q, insetBlockStart: q, insetBlockEnd: q, insetInline: q, insetInlineStart: q, insetInlineEnd: q, padding: q, paddingTop: q, paddingRight: q, paddingBottom: q, paddingLeft: q, paddingBlock: q, paddingBlockStart: q, paddingBlockEnd: q, paddingInline: q, paddingInlineStart: q, paddingInlineEnd: q, margin: q, marginTop: q, marginRight: q, marginBottom: q, marginLeft: q, marginBlock: q, marginBlockStart: q, marginBlockEnd: q, marginInline: q, marginInlineStart: q, marginInlineEnd: q, fontSize: q, backgroundPositionX: q, backgroundPositionY: q, rotate: Ft, pathRotation: Ft, rotateX: Ft, rotateY: Ft, rotateZ: Ft, scale: St, scaleX: St, scaleY: St, scaleZ: St, skew: Ft, skewX: Ft, skewY: Ft, distance: q, translateX: q, translateY: q, translateZ: q, x: q, y: q, z: q, perspective: q, transformPerspective: q, opacity: xt, originX: zt, originY: zt, originZ: q, zIndex: Xi, fillOpacity: xt, strokeOpacity: xt, numOctaves: Xi }, Qi = { ...Zi, color: J, backgroundColor: J, outlineColor: J, fill: J, stroke: J, borderColor: J, borderTopColor: J, borderRightColor: J, borderBottomColor: J, borderLeftColor: J, filter: Ji, WebkitFilter: Ji, mask: Yi, WebkitMask: Yi }, $i = (e2) => Qi[e2], ea = /* @__PURE__ */ new Set([Ji, Yi]);
function ta(e2, t2) {
  let n2 = $i(e2);
  return ea.has(n2) || (n2 = nn), n2.getAnimatableNone ? n2.getAnimatableNone(t2) : void 0;
}
var na = /* @__PURE__ */ new Set([`auto`, `none`, `0`]);
function ra(e2, t2, n2) {
  let r2 = 0, i2;
  for (; r2 < e2.length && !i2; ) {
    let t3 = e2[r2];
    typeof t3 == `string` && !na.has(t3) && Yt(t3).values.length && (i2 = e2[r2]), r2++;
  }
  if (i2 && n2) for (let r3 of t2) e2[r3] = ta(n2, i2);
}
var ia = class extends Er {
  constructor(e2, t2, n2, r2, i2) {
    super(e2, t2, n2, r2, i2, true);
  }
  readKeyframes() {
    let { unresolvedKeyframes: e2, element: t2, name: n2 } = this;
    if (!t2 || !t2.current) return;
    super.readKeyframes();
    for (let n3 = 0; n3 < e2.length; n3++) {
      let r3 = e2[n3];
      if (typeof r3 == `string` && (r3 = r3.trim(), _t(r3))) {
        let i3 = xi(r3, t2.current);
        i3 !== void 0 && (e2[n3] = i3), n3 === e2.length - 1 && (this.finalKeyframe = r3);
      }
    }
    if (this.resolveNoneKeyframes(), !Ti.has(n2) || e2.length !== 2) return;
    let [r2, i2] = e2, a2 = Ui(r2), o2 = Ui(i2);
    if (yt(r2) !== yt(i2) && vr[n2]) {
      this.needsMeasurement = true;
      return;
    }
    if (a2 !== o2) if (mr(a2) && mr(o2)) for (let t3 = 0; t3 < e2.length; t3++) {
      let n3 = e2[t3];
      typeof n3 == `string` && (e2[t3] = parseFloat(n3));
    }
    else vr[n2] && (this.needsMeasurement = true);
  }
  resolveNoneKeyframes() {
    let { unresolvedKeyframes: e2, name: t2 } = this, n2 = [];
    for (let t3 = 0; t3 < e2.length; t3++) (e2[t3] === null || Wi(e2[t3])) && n2.push(t3);
    n2.length && ra(e2, n2, t2);
  }
  measureInitialState() {
    let { element: e2, unresolvedKeyframes: t2, name: n2 } = this;
    if (!e2 || !e2.current) return;
    n2 === `height` && (this.suspendedScrollY = window.pageYOffset), this.measuredOrigin = vr[n2](e2.measureViewportBox(), window.getComputedStyle(e2.current)), t2[0] = this.measuredOrigin;
    let r2 = t2[t2.length - 1];
    r2 !== void 0 && e2.getValue(n2, r2).jump(r2, false);
  }
  measureEndState() {
    let { element: e2, name: t2, unresolvedKeyframes: n2 } = this;
    if (!e2 || !e2.current) return;
    let r2 = e2.getValue(t2);
    r2 && r2.jump(this.measuredOrigin, false);
    let i2 = n2.length - 1, a2 = n2[i2];
    n2[i2] = vr[t2](e2.measureViewportBox(), window.getComputedStyle(e2.current)), a2 !== null && this.finalKeyframe === void 0 && (this.finalKeyframe = a2), this.removedTransforms?.length && this.removedTransforms.forEach(([t3, n3]) => {
      e2.getValue(t3).set(n3);
    }), this.resolveNoneKeyframes();
  }
}, aa = [`borderTopLeftRadius`, `borderTopRightRadius`, `borderBottomRightRadius`, `borderBottomLeftRadius`];
function oa(e2, t2, n2) {
  if (e2 == null) return [];
  if (e2 instanceof EventTarget) return [e2];
  if (typeof e2 == `string`) {
    let r2 = document;
    t2 && (r2 = t2.current);
    let i2 = n2?.[e2] ?? r2.querySelectorAll(e2);
    return i2 ? Array.from(i2) : [];
  }
  return Array.from(e2).filter((e3) => e3 != null);
}
var sa = (e2, t2) => t2 && typeof e2 == `number` ? t2.transform(e2) : e2;
function ca(e2) {
  return Me(e2) && `offsetHeight` in e2 && !(`ownerSVGElement` in e2);
}
var { schedule: la, cancel: ua } = lt(queueMicrotask, false), da = { x: false, y: false };
function fa() {
  return da.x || da.y;
}
function pa(e2) {
  return e2 === `x` || e2 === `y` ? da[e2] ? null : (da[e2] = true, () => {
    da[e2] = false;
  }) : da.x || da.y ? null : (da.x = da.y = true, () => {
    da.x = da.y = false;
  });
}
function ma(e2, t2) {
  let n2 = oa(e2), r2 = new AbortController();
  return [n2, { passive: true, ...t2, signal: r2.signal }, () => r2.abort()];
}
function ha(e2) {
  return !(e2.pointerType === `touch` || fa());
}
function ga(e2, t2, n2 = {}) {
  let [r2, i2, a2] = ma(e2, n2);
  return r2.forEach((e3) => {
    let n3 = false, r3 = false, a3, o2 = () => {
      e3.removeEventListener(`pointerleave`, u2);
    }, s2 = (e4) => {
      a3 &&= (a3(e4), void 0), o2();
    }, c2 = (e4) => {
      n3 = false, window.removeEventListener(`pointerup`, c2), window.removeEventListener(`pointercancel`, c2), r3 && (r3 = false, s2(e4));
    }, l2 = () => {
      n3 = true, window.addEventListener(`pointerup`, c2, i2), window.addEventListener(`pointercancel`, c2, i2);
    }, u2 = (e4) => {
      if (e4.pointerType !== `touch`) {
        if (n3) {
          r3 = true;
          return;
        }
        s2(e4);
      }
    };
    e3.addEventListener(`pointerenter`, (n4) => {
      if (!ha(n4)) return;
      r3 = false;
      let o3 = t2(e3, n4);
      typeof o3 == `function` && (a3 = o3, e3.addEventListener(`pointerleave`, u2, i2));
    }, i2), e3.addEventListener(`pointerdown`, l2, i2);
  }), a2;
}
var _a = (e2, t2) => t2 ? e2 === t2 ? true : _a(e2, t2.parentElement) : false, va = (e2) => e2.pointerType === `mouse` ? typeof e2.button != `number` || e2.button <= 0 : e2.isPrimary !== false, ya = /* @__PURE__ */ new Set([`BUTTON`, `INPUT`, `SELECT`, `TEXTAREA`, `A`]);
function ba(e2) {
  return ya.has(e2.tagName) || e2.isContentEditable === true;
}
var xa = /* @__PURE__ */ new Set([`INPUT`, `SELECT`, `TEXTAREA`]);
function Sa(e2) {
  return xa.has(e2.tagName) || e2.isContentEditable === true;
}
var Ca = /* @__PURE__ */ new WeakSet();
function wa(e2) {
  return (t2) => {
    t2.key === `Enter` && e2(t2);
  };
}
function Ta(e2, t2) {
  e2.dispatchEvent(new PointerEvent(`pointer` + t2, { isPrimary: true, bubbles: true }));
}
var Ea = (e2, t2) => {
  let n2 = e2.currentTarget;
  if (!n2) return;
  let r2 = wa(() => {
    if (Ca.has(n2)) return;
    Ta(n2, `down`);
    let e3 = wa(() => {
      Ta(n2, `up`);
    });
    n2.addEventListener(`keyup`, e3, t2), n2.addEventListener(`blur`, () => Ta(n2, `cancel`), t2);
  });
  n2.addEventListener(`keydown`, r2, t2), n2.addEventListener(`blur`, () => n2.removeEventListener(`keydown`, r2), t2);
};
function Da(e2) {
  return va(e2) && !fa();
}
var Oa = /* @__PURE__ */ new WeakSet();
function ka(e2, t2, n2 = {}) {
  let [r2, i2, a2] = ma(e2, n2), o2 = (e3) => {
    let r3 = e3.currentTarget;
    if (!Da(e3) || Oa.has(e3)) return;
    Ca.add(r3), n2.stopPropagation && Oa.add(e3);
    let a3 = t2(r3, e3), o3 = { ...i2, capture: true }, s2 = (e4, t3) => {
      window.removeEventListener(`pointerup`, c2, o3), window.removeEventListener(`pointercancel`, l2, o3), Ca.has(r3) && Ca.delete(r3), Da(e4) && typeof a3 == `function` && a3(e4, { success: t3 });
    }, c2 = (e4) => {
      s2(e4, r3 === window || r3 === document || n2.useGlobalTarget || _a(r3, e4.target));
    }, l2 = (e4) => {
      s2(e4, false);
    };
    window.addEventListener(`pointerup`, c2, o3), window.addEventListener(`pointercancel`, l2, o3);
  };
  return r2.forEach((e3) => {
    (n2.useGlobalTarget ? window : e3).addEventListener(`pointerdown`, o2, i2), ca(e3) && (e3.addEventListener(`focus`, (e4) => Ea(e4, i2)), !ba(e3) && !e3.hasAttribute(`tabindex`) && (e3.tabIndex = 0));
  }), a2;
}
function Aa(e2) {
  return Me(e2) && `ownerSVGElement` in e2;
}
var ja = /* @__PURE__ */ new WeakMap(), Ma, Na = (e2, t2, n2) => (r2, i2) => i2 && i2[0] ? i2[0][e2 + `Size`] : Aa(r2) && `getBBox` in r2 ? r2.getBBox()[t2] : r2[n2], Pa = Na(`inline`, `width`, `offsetWidth`), Fa = Na(`block`, `height`, `offsetHeight`);
function Ia({ target: e2, borderBoxSize: t2 }) {
  ja.get(e2)?.forEach((n2) => {
    n2(e2, { get width() {
      return Pa(e2, t2);
    }, get height() {
      return Fa(e2, t2);
    } });
  });
}
function La(e2) {
  e2.forEach(Ia);
}
function Ra() {
  typeof ResizeObserver > `u` || (Ma = new ResizeObserver(La));
}
function za(e2, t2) {
  Ma || Ra();
  let n2 = oa(e2);
  return n2.forEach((e3) => {
    let n3 = ja.get(e3);
    n3 || (n3 = /* @__PURE__ */ new Set(), ja.set(e3, n3)), n3.add(t2), Ma?.observe(e3);
  }), () => {
    n2.forEach((e3) => {
      let n3 = ja.get(e3);
      n3?.delete(t2), n3?.size || Ma?.unobserve(e3);
    });
  };
}
var Ba = /* @__PURE__ */ new Set(), Va;
function Ha() {
  Va = () => {
    let e2 = { get width() {
      return window.innerWidth;
    }, get height() {
      return window.innerHeight;
    } };
    Ba.forEach((t2) => t2(e2));
  }, window.addEventListener(`resize`, Va);
}
function Ua(e2) {
  return Ba.add(e2), Va || Ha(), () => {
    Ba.delete(e2), !Ba.size && typeof Va == `function` && (window.removeEventListener(`resize`, Va), Va = void 0);
  };
}
function Wa(e2, t2) {
  return typeof e2 == `function` ? Ua(e2) : za(e2, t2);
}
var Ga = { value: null, addProjectionMetrics: null };
function Ka(e2) {
  return Aa(e2) && e2.tagName === `svg`;
}
var qa = [...Hi, J, nn], Ja = (e2) => qa.find(Vi(e2)), Ya = () => ({ translate: 0, scale: 1, origin: 0, originPoint: 0 }), Xa = () => ({ x: Ya(), y: Ya() }), Za = () => ({ min: 0, max: 0 }), Q = () => ({ x: Za(), y: Za() }), Qa = /* @__PURE__ */ new WeakMap();
function $a(e2) {
  return typeof e2 == `object` && !!e2 && typeof e2.start == `function`;
}
function eo(e2) {
  return typeof e2 == `string` || Array.isArray(e2);
}
var to = [`animate`, `whileInView`, `whileFocus`, `whileHover`, `whileTap`, `whileDrag`, `exit`], no = [`initial`, ...to];
function ro(e2) {
  return $a(e2.animate) || no.some((t2) => eo(e2[t2]));
}
function io(e2) {
  return !!(ro(e2) || e2.variants);
}
function ao(e2, t2, n2) {
  for (let r2 in t2) {
    let i2 = t2[r2], a2 = n2[r2];
    if (Z(i2)) e2.addValue(r2, i2);
    else if (Z(a2)) e2.addValue(r2, ci(i2, { owner: e2 }));
    else if (a2 !== i2) if (e2.hasValue(r2)) {
      let t3 = e2.getValue(r2);
      t3.liveStyle === true ? t3.jump(i2) : t3.hasAnimated || t3.set(i2);
    } else {
      let t3 = e2.getStaticValue(r2);
      e2.addValue(r2, ci(t3 === void 0 ? i2 : t3, { owner: e2 }));
    }
  }
  for (let r2 in n2) t2[r2] === void 0 && e2.removeValue(r2);
  return t2;
}
var oo = { current: null }, so = { current: false }, co = typeof window < `u`;
function lo() {
  if (so.current = true, co) if (window.matchMedia) {
    let e2 = window.matchMedia(`(prefers-reduced-motion)`), t2 = () => oo.current = e2.matches;
    e2.addEventListener(`change`, t2), t2();
  } else oo.current = false;
}
var uo = [`AnimationStart`, `AnimationComplete`, `Update`, `BeforeLayoutMeasure`, `LayoutMeasure`, `LayoutAnimationStart`, `LayoutAnimationComplete`], fo = {};
function po(e2) {
  fo = e2;
}
function mo() {
  return fo;
}
var ho = class {
  scrapeMotionValuesFromProps(e2, t2, n2) {
    return {};
  }
  constructor({ parent: e2, props: t2, presenceContext: n2, reducedMotionConfig: r2, skipAnimations: i2, blockInitialAnimation: a2, visualState: o2 }, s2 = {}) {
    this.current = null, this.children = /* @__PURE__ */ new Set(), this.isVariantNode = false, this.isControllingVariants = false, this.shouldReduceMotion = null, this.shouldSkipAnimations = false, this.values = /* @__PURE__ */ new Map(), this.KeyframeResolver = Er, this.features = {}, this.valueSubscriptions = /* @__PURE__ */ new Map(), this.prevMotionValues = {}, this.hasBeenMounted = false, this.events = {}, this.propEventSubscriptions = {}, this.notifyUpdate = () => this.notify(`Update`, this.latestValues), this.render = () => {
      this.current && (this.triggerBuild(), this.renderInstance(this.current, this.renderState, this.props.style, this.projection));
    }, this.renderScheduledAt = 0, this.scheduleRender = () => {
      let e3 = K.now();
      this.renderScheduledAt < e3 && (this.renderScheduledAt = e3, W.render(this.render, false, true));
    };
    let { latestValues: c2, renderState: l2 } = o2;
    this.latestValues = c2, this.baseTarget = { ...c2 }, this.initialValues = t2.initial ? { ...c2 } : {}, this.renderState = l2, this.parent = e2, this.props = t2, this.presenceContext = n2, this.depth = e2 ? e2.depth + 1 : 0, this.reducedMotionConfig = r2, this.skipAnimationsConfig = i2, this.options = s2, this.blockInitialAnimation = !!a2, this.isControllingVariants = ro(t2), this.isVariantNode = io(t2), this.isVariantNode && (this.variantChildren = /* @__PURE__ */ new Set()), this.manuallyAnimateOnMount = !!(e2 && e2.current);
    let { willChange: u2, ...d2 } = this.scrapeMotionValuesFromProps(t2, {}, this);
    for (let e3 in d2) {
      let t3 = d2[e3];
      c2[e3] !== void 0 && Z(t3) && t3.set(c2[e3]);
    }
  }
  mount(e2) {
    if (this.hasBeenMounted) for (let e3 in this.initialValues) this.values.get(e3)?.jump(this.initialValues[e3]), this.latestValues[e3] = this.initialValues[e3];
    this.current = e2, Qa.set(e2, this), this.projection && !this.projection.instance && this.projection.mount(e2), this.parent && this.isVariantNode && !this.isControllingVariants && (this.removeFromVariantTree = this.parent.addVariantChild(this)), this.values.forEach((e3, t2) => this.bindToMotionValue(t2, e3)), this.reducedMotionConfig === `never` ? this.shouldReduceMotion = false : this.reducedMotionConfig === `always` ? this.shouldReduceMotion = true : (so.current || lo(), this.shouldReduceMotion = oo.current), this.shouldSkipAnimations = this.skipAnimationsConfig ?? false, this.parent?.addChild(this), this.update(this.props, this.presenceContext), this.hasBeenMounted = true;
  }
  unmount() {
    this.projection && this.projection.unmount(), ut(this.notifyUpdate), ut(this.render), this.valueSubscriptions.forEach((e2) => e2()), this.valueSubscriptions.clear(), this.removeFromVariantTree && this.removeFromVariantTree(), this.parent?.removeChild(this);
    for (let e2 in this.events) this.events[e2].clear();
    for (let e2 in this.features) {
      let t2 = this.features[e2];
      t2 && (t2.unmount(), t2.isMounted = false);
    }
    this.current = null;
  }
  addChild(e2) {
    this.children.add(e2), this.enteringChildren ??= /* @__PURE__ */ new Set(), this.enteringChildren.add(e2);
  }
  removeChild(e2) {
    this.children.delete(e2), this.enteringChildren && this.enteringChildren.delete(e2);
  }
  bindToMotionValue(e2, t2) {
    if (this.valueSubscriptions.has(e2) && this.valueSubscriptions.get(e2)(), t2.accelerate && Yr.has(e2) && this.current instanceof HTMLElement) {
      let { factory: n3, keyframes: r3, times: i3, ease: a2, duration: o2 } = t2.accelerate, s2 = new zr({ element: this.current, name: e2, keyframes: r3, times: i3, ease: a2, duration: H(o2) }), c2 = n3(s2);
      this.valueSubscriptions.set(e2, () => {
        c2(), s2.cancel();
      });
      return;
    }
    let n2 = pr.has(e2);
    n2 && this.onBindTransform && this.onBindTransform();
    let r2 = t2.on(`change`, (t3) => {
      this.latestValues[e2] = t3, this.props.onUpdate && W.preRender(this.notifyUpdate), n2 && this.projection && (this.projection.isTransformDirty = true), this.scheduleRender();
    }), i2;
    typeof window < `u` && window.MotionCheckAppearSync && (i2 = window.MotionCheckAppearSync(this, e2, t2)), this.valueSubscriptions.set(e2, () => {
      r2(), i2 && i2();
    });
  }
  sortNodePosition(e2) {
    return !this.current || !this.sortInstanceNodePosition || this.type !== e2.type ? 0 : this.sortInstanceNodePosition(this.current, e2.current);
  }
  updateFeatures() {
    let e2 = `animation`;
    for (e2 in fo) {
      let t2 = fo[e2];
      if (!t2) continue;
      let { isEnabled: n2, Feature: r2 } = t2;
      if (!this.features[e2] && r2 && n2(this.props) && (this.features[e2] = new r2(this)), this.features[e2]) {
        let t3 = this.features[e2];
        t3.isMounted ? t3.update() : (t3.mount(), t3.isMounted = true);
      }
    }
  }
  triggerBuild() {
    this.build(this.renderState, this.latestValues, this.props);
  }
  measureViewportBox() {
    return this.current ? this.measureInstanceViewportBox(this.current, this.props) : Q();
  }
  getStaticValue(e2) {
    return this.latestValues[e2];
  }
  setStaticValue(e2, t2) {
    this.latestValues[e2] = t2;
  }
  update(e2, t2) {
    (e2.transformTemplate || this.props.transformTemplate) && this.scheduleRender(), this.prevProps = this.props, this.props = e2, this.prevPresenceContext = this.presenceContext, this.presenceContext = t2;
    for (let t3 = 0; t3 < uo.length; t3++) {
      let n2 = uo[t3];
      this.propEventSubscriptions[n2] && (this.propEventSubscriptions[n2](), delete this.propEventSubscriptions[n2]);
      let r2 = e2[`on` + n2];
      r2 && (this.propEventSubscriptions[n2] = this.on(n2, r2));
    }
    this.prevMotionValues = ao(this, this.scrapeMotionValuesFromProps(e2, this.prevProps || {}, this), this.prevMotionValues), this.handleChildMotionValue && this.handleChildMotionValue();
  }
  getProps() {
    return this.props;
  }
  getVariant(e2) {
    return this.props.variants ? this.props.variants[e2] : void 0;
  }
  getDefaultTransition() {
    return this.props.transition;
  }
  getTransformPagePoint() {
    return this.props.transformPagePoint;
  }
  getClosestVariantNode() {
    return this.isVariantNode ? this : this.parent ? this.parent.getClosestVariantNode() : void 0;
  }
  addVariantChild(e2) {
    let t2 = this.getClosestVariantNode();
    if (t2) return t2.variantChildren && t2.variantChildren.add(e2), () => t2.variantChildren.delete(e2);
  }
  addValue(e2, t2) {
    let n2 = this.values.get(e2);
    t2 !== n2 && (n2 && this.removeValue(e2), this.bindToMotionValue(e2, t2), this.values.set(e2, t2), this.latestValues[e2] = t2.get());
  }
  removeValue(e2) {
    this.values.delete(e2);
    let t2 = this.valueSubscriptions.get(e2);
    t2 && (t2(), this.valueSubscriptions.delete(e2)), delete this.latestValues[e2], this.removeValueFromRenderState(e2, this.renderState);
  }
  hasValue(e2) {
    return this.values.has(e2);
  }
  getValue(e2, t2) {
    if (this.props.values && this.props.values[e2]) return this.props.values[e2];
    let n2 = this.values.get(e2);
    return n2 === void 0 && t2 !== void 0 && (n2 = ci(t2 === null ? void 0 : t2, { owner: this }), this.addValue(e2, n2)), n2;
  }
  readValue(e2, t2) {
    let n2 = this.latestValues[e2] !== void 0 || !this.current ? this.latestValues[e2] : this.getBaseTargetFromProps(this.props, e2) ?? this.readValueFromInstance(this.current, e2, this.options);
    return n2 != null && (typeof n2 == `string` && (je(n2) || Ne(n2)) ? n2 = parseFloat(n2) : !Ja(n2) && nn.test(t2) && (n2 = ta(e2, t2)), this.setBaseTarget(e2, Z(n2) ? n2.get() : n2)), Z(n2) ? n2.get() : n2;
  }
  setBaseTarget(e2, t2) {
    this.baseTarget[e2] = t2;
  }
  getBaseTarget(e2) {
    let { initial: t2 } = this.props, n2;
    if (typeof t2 == `string` || typeof t2 == `object`) {
      let r3 = Ci(this.props, t2, this.presenceContext?.custom);
      r3 && (n2 = r3[e2]);
    }
    if (t2 && n2 !== void 0) return n2;
    let r2 = this.getBaseTargetFromProps(this.props, e2);
    return r2 !== void 0 && !Z(r2) ? r2 : this.initialValues[e2] !== void 0 && n2 === void 0 ? void 0 : this.baseTarget[e2];
  }
  on(e2, t2) {
    return this.events[e2] || (this.events[e2] = new V()), this.events[e2].add(t2);
  }
  notify(e2, ...t2) {
    this.events[e2] && this.events[e2].notify(...t2);
  }
  scheduleRenderMicrotask() {
    la.render(this.render);
  }
}, go = class extends ho {
  constructor() {
    super(...arguments), this.KeyframeResolver = ia;
  }
  sortInstanceNodePosition(e2, t2) {
    return e2.compareDocumentPosition(t2) & 2 ? 1 : -1;
  }
  getBaseTargetFromProps(e2, t2) {
    let n2 = e2.style;
    return n2 ? n2[t2] : void 0;
  }
  removeValueFromRenderState(e2, { vars: t2, style: n2 }) {
    delete t2[e2], delete n2[e2];
  }
  handleChildMotionValue() {
    this.childSubscription && (this.childSubscription(), delete this.childSubscription);
    let { children: e2 } = this.props;
    Z(e2) && (this.childSubscription = e2.on(`change`, (e3) => {
      this.current && (this.current.textContent = `${e3}`);
    }));
  }
}, _o = class {
  constructor(e2) {
    this.isMounted = false, this.node = e2;
  }
  update() {
  }
};
function vo({ top: e2, left: t2, right: n2, bottom: r2 }) {
  return { x: { min: t2, max: n2 }, y: { min: e2, max: r2 } };
}
function yo({ x: e2, y: t2 }) {
  return { top: t2.min, right: e2.max, bottom: t2.max, left: e2.min };
}
function bo(e2, t2) {
  if (!t2) return e2;
  let n2 = t2({ x: e2.left, y: e2.top }), r2 = t2({ x: e2.right, y: e2.bottom });
  return { top: n2.y, left: n2.x, bottom: r2.y, right: r2.x };
}
function xo(e2) {
  return e2 === void 0 || e2 === 1;
}
function So({ scale: e2, scaleX: t2, scaleY: n2 }) {
  return !xo(e2) || !xo(t2) || !xo(n2);
}
function Co(e2) {
  return So(e2) || wo(e2) || e2.z || e2.rotate || e2.rotateX || e2.rotateY || e2.skewX || e2.skewY;
}
function wo(e2) {
  return To(e2.x) || To(e2.y);
}
function To(e2) {
  return e2 && e2 !== `0%`;
}
function Eo(e2, t2, n2) {
  return n2 + t2 * (e2 - n2);
}
function Do(e2, t2, n2, r2, i2) {
  return i2 !== void 0 && (e2 = Eo(e2, i2, r2)), Eo(e2, n2, r2) + t2;
}
function Oo(e2, t2 = 0, n2 = 1, r2, i2) {
  e2.min = Do(e2.min, t2, n2, r2, i2), e2.max = Do(e2.max, t2, n2, r2, i2);
}
function ko(e2, { x: t2, y: n2 }) {
  Oo(e2.x, t2.translate, t2.scale, t2.originPoint), Oo(e2.y, n2.translate, n2.scale, n2.originPoint);
}
var Ao = 0.999999999999, jo = 1.0000000000001;
function Mo(e2, t2, n2, r2 = false) {
  let i2 = n2.length;
  if (!i2) return;
  t2.x = t2.y = 1;
  let a2, o2;
  for (let s2 = 0; s2 < i2; s2++) {
    a2 = n2[s2], o2 = a2.projectionDelta;
    let { visualElement: i3 } = a2.options;
    i3 && i3.props.style && i3.props.style.display === `contents` || (r2 && a2.options.layoutScroll && a2.scroll && a2 !== a2.root && (No(e2.x, -a2.scroll.offset.x), No(e2.y, -a2.scroll.offset.y)), o2 && (t2.x *= o2.x.scale, t2.y *= o2.y.scale, ko(e2, o2)), r2 && Co(a2.latestValues) && Io(e2, a2.latestValues, a2.layout?.layoutBox));
  }
  t2.x < jo && t2.x > Ao && (t2.x = 1), t2.y < jo && t2.y > Ao && (t2.y = 1);
}
function No(e2, t2) {
  e2.min += t2, e2.max += t2;
}
function Po(e2, t2, n2, r2, i2 = 0.5) {
  Oo(e2, t2, n2, Y(e2.min, e2.max, i2), r2);
}
function Fo(e2, t2) {
  return typeof e2 == `string` ? parseFloat(e2) / 100 * (t2.max - t2.min) : e2;
}
function Io(e2, t2, n2) {
  let r2 = n2 ?? e2;
  Po(e2.x, Fo(t2.x, r2.x), t2.scaleX, t2.scale, t2.originX), Po(e2.y, Fo(t2.y, r2.y), t2.scaleY, t2.scale, t2.originY);
}
function Lo(e2, t2) {
  return vo(bo(e2.getBoundingClientRect(), t2));
}
function Ro(e2, t2, n2) {
  let r2 = Lo(e2, n2), { scroll: i2 } = t2;
  return i2 && (No(r2.x, i2.offset.x), No(r2.y, i2.offset.y)), r2;
}
var zo = { x: `translateX`, y: `translateY`, z: `translateZ`, transformPerspective: `perspective` }, Bo = fr.length;
function Vo(e2, t2, n2) {
  let r2 = ``, i2 = true;
  for (let a3 = 0; a3 < Bo; a3++) {
    let o2 = fr[a3], s2 = e2[o2];
    if (s2 === void 0) continue;
    let c2 = true;
    if (typeof s2 == `number`) c2 = s2 === +!!o2.startsWith(`scale`);
    else {
      let e3 = parseFloat(s2);
      c2 = o2.startsWith(`scale`) ? e3 === 1 : e3 === 0;
    }
    if (!c2 || n2) {
      let e3 = sa(s2, Zi[o2]);
      if (!c2) {
        i2 = false;
        let t3 = zo[o2] || o2;
        r2 += `${t3}(${e3}) `;
      }
      n2 && (t2[o2] = e3);
    }
  }
  let a2 = e2.pathRotation;
  return a2 && (i2 = false, r2 += `rotate(${sa(a2, Zi.pathRotation)}) `), r2 = r2.trim(), n2 ? r2 = n2(t2, i2 ? `` : r2) : i2 && (r2 = `none`), r2;
}
function Ho(e2, t2, n2) {
  let { style: r2, vars: i2, transformOrigin: a2 } = e2, o2 = false, s2 = false;
  for (let e3 in t2) {
    let n3 = t2[e3];
    if (pr.has(e3)) {
      o2 = true;
      continue;
    } else if (ht(e3)) {
      i2[e3] = n3;
      continue;
    } else {
      let t3 = sa(n3, Zi[e3]);
      e3.startsWith(`origin`) ? (s2 = true, a2[e3] = t3) : r2[e3] = t3;
    }
  }
  if (t2.transform || (o2 || n2 ? r2.transform = Vo(t2, e2.transform, n2) : r2.transform &&= `none`), s2) {
    let { originX: e3 = `50%`, originY: t3 = `50%`, originZ: n3 = 0 } = a2;
    r2.transformOrigin = `${e3} ${t3} ${n3}`;
  }
}
function Uo(e2, { style: t2, vars: n2 }, r2, i2) {
  let a2 = e2.style, o2;
  for (o2 in t2) a2[o2] = t2[o2];
  for (o2 in i2?.applyProjectionStyles(a2, r2), n2) a2.setProperty(o2, n2[o2]);
}
function Wo(e2, t2) {
  return t2.max === t2.min ? 0 : e2 / (t2.max - t2.min) * 100;
}
var Go = { correct: (e2, t2) => {
  if (!t2.target) return e2;
  if (typeof e2 == `string`) if (q.test(e2)) e2 = parseFloat(e2);
  else return e2;
  return `${Wo(e2, t2.target.x)}% ${Wo(e2, t2.target.y)}%`;
} }, Ko = { correct: (e2, { treeScale: t2, projectionDelta: n2 }) => {
  let r2 = e2, i2 = nn.parse(e2);
  if (i2.length > 5) return r2;
  let a2 = nn.createTransformer(e2), o2 = typeof i2[0] == `number` ? 0 : 1, s2 = n2.x.scale * t2.x, c2 = n2.y.scale * t2.y;
  i2[0 + o2] /= s2, i2[1 + o2] /= c2;
  let l2 = Y(s2, c2, 0.5);
  return typeof i2[2 + o2] == `number` && (i2[2 + o2] /= l2), typeof i2[3 + o2] == `number` && (i2[3 + o2] /= l2), a2(i2);
} }, qo = { borderRadius: { ...Go, applyTo: [...aa] }, borderTopLeftRadius: Go, borderTopRightRadius: Go, borderBottomLeftRadius: Go, borderBottomRightRadius: Go, boxShadow: Ko };
function Jo(e2, { layout: t2, layoutId: n2 }) {
  return pr.has(e2) || e2.startsWith(`origin`) || (t2 || n2 !== void 0) && (!!qo[e2] || e2 === `opacity`);
}
function Yo(e2, t2, n2) {
  let r2 = e2.style, i2 = t2?.style, a2 = {};
  if (!r2) return a2;
  for (let t3 in r2) (Z(r2[t3]) || i2 && Z(i2[t3]) || Jo(t3, e2) || n2?.getValue(t3)?.liveStyle !== void 0) && (a2[t3] = r2[t3]);
  return a2;
}
function Xo(e2) {
  return window.getComputedStyle(e2);
}
var Zo = class extends go {
  constructor() {
    super(...arguments), this.type = `html`, this.renderInstance = Uo;
  }
  readValueFromInstance(e2, t2) {
    if (pr.has(t2)) return this.projection?.isProjecting ? cr(t2) : ur(e2, t2);
    {
      let n2 = Xo(e2), r2 = (ht(t2) ? n2.getPropertyValue(t2) : n2[t2]) || 0;
      return typeof r2 == `string` ? r2.trim() : r2;
    }
  }
  measureInstanceViewportBox(e2, { transformPagePoint: t2 }) {
    return Lo(e2, t2);
  }
  build(e2, t2, n2) {
    Ho(e2, t2, n2.transformTemplate);
  }
  scrapeMotionValuesFromProps(e2, t2, n2) {
    return Yo(e2, t2, n2);
  }
}, Qo = { offset: `stroke-dashoffset`, array: `stroke-dasharray` }, $o = { offset: `strokeDashoffset`, array: `strokeDasharray` };
function es(e2, t2, n2 = 1, r2 = 0, i2 = true) {
  e2.pathLength = 1;
  let a2 = i2 ? Qo : $o;
  e2[a2.offset] = `${-r2}`, e2[a2.array] = `${t2} ${n2}`;
}
var ts = [`offsetDistance`, `offsetPath`, `offsetRotate`, `offsetAnchor`];
function ns(e2, { attrX: t2, attrY: n2, attrScale: r2, pathLength: i2, pathSpacing: a2 = 1, pathOffset: o2 = 0, ...s2 }, c2, l2, u2) {
  if (Ho(e2, s2, l2), c2) {
    e2.style.viewBox && (e2.attrs.viewBox = e2.style.viewBox);
    return;
  }
  e2.attrs = e2.style, e2.style = {};
  let { attrs: d2, style: f2 } = e2;
  d2.transform && (f2.transform = d2.transform, delete d2.transform), (f2.transform || d2.transformOrigin) && (f2.transformOrigin = d2.transformOrigin ?? `50% 50%`, delete d2.transformOrigin), f2.transform && (f2.transformBox = u2?.transformBox ?? `fill-box`, delete d2.transformBox);
  for (let e3 of ts) d2[e3] !== void 0 && (f2[e3] = d2[e3], delete d2[e3]);
  t2 !== void 0 && (d2.x = t2), n2 !== void 0 && (d2.y = n2), r2 !== void 0 && (d2.scale = r2), i2 !== void 0 && es(d2, i2, a2, o2, false);
}
var rs = /* @__PURE__ */ new Set([`baseFrequency`, `diffuseConstant`, `kernelMatrix`, `kernelUnitLength`, `keySplines`, `keyTimes`, `limitingConeAngle`, `markerHeight`, `markerWidth`, `numOctaves`, `targetX`, `targetY`, `surfaceScale`, `specularConstant`, `specularExponent`, `stdDeviation`, `tableValues`, `viewBox`, `gradientTransform`, `pathLength`, `startOffset`, `textLength`, `lengthAdjust`]), is = (e2) => typeof e2 == `string` && e2.toLowerCase() === `svg`;
function as(e2, t2, n2, r2) {
  Uo(e2, t2, void 0, r2);
  for (let n3 in t2.attrs) e2.setAttribute(rs.has(n3) ? n3 : Mi(n3), t2.attrs[n3]);
}
function os(e2, t2, n2) {
  let r2 = Yo(e2, t2, n2);
  for (let n3 in e2) if (Z(e2[n3]) || Z(t2[n3])) {
    let t3 = fr.indexOf(n3) === -1 ? n3 : `attr` + n3.charAt(0).toUpperCase() + n3.substring(1);
    r2[t3] = e2[n3];
  }
  return r2;
}
var ss = class extends go {
  constructor() {
    super(...arguments), this.type = `svg`, this.isSVGTag = false, this.measureInstanceViewportBox = Q;
  }
  getBaseTargetFromProps(e2, t2) {
    return e2[t2];
  }
  readValueFromInstance(e2, t2) {
    if (pr.has(t2)) {
      let e3 = $i(t2);
      return e3 && e3.default || 0;
    }
    return t2 = rs.has(t2) ? t2 : Mi(t2), e2.getAttribute(t2);
  }
  scrapeMotionValuesFromProps(e2, t2, n2) {
    return os(e2, t2, n2);
  }
  build(e2, t2, n2) {
    ns(e2, t2, this.isSVGTag, n2.transformTemplate, n2.style);
  }
  renderInstance(e2, t2, n2, r2) {
    as(e2, t2, n2, r2);
  }
  mount(e2) {
    this.isSVGTag = is(e2.tagName), super.mount(e2);
  }
}, cs = no.length;
function ls(e2) {
  if (!e2) return;
  if (!e2.isControllingVariants) {
    let t3 = e2.parent && ls(e2.parent) || {};
    return e2.props.initial !== void 0 && (t3.initial = e2.props.initial), t3;
  }
  let t2 = {};
  for (let n2 = 0; n2 < cs; n2++) {
    let r2 = no[n2], i2 = e2.props[r2];
    (eo(i2) || i2 === false) && (t2[r2] = i2);
  }
  return t2;
}
function us(e2, t2) {
  if (!Array.isArray(t2)) return false;
  let n2 = t2.length;
  if (n2 !== e2.length) return false;
  for (let r2 = 0; r2 < n2; r2++) if (t2[r2] !== e2[r2]) return false;
  return true;
}
var ds = [...to].reverse(), fs = to.length;
function ps(e2) {
  return (t2) => Promise.all(t2.map(({ animation: t3, options: n2 }) => zi(e2, t3, n2)));
}
function ms(e2) {
  let t2 = ps(e2), n2 = _s(), r2 = true, i2 = false, a2 = (t3) => (n3, r3) => {
    let i3 = wi(e2, r3, t3 === `exit` ? e2.presenceContext?.custom : void 0);
    if (i3) {
      let { transition: e3, transitionEnd: t4, ...r4 } = i3;
      n3 = { ...n3, ...r4, ...t4 };
    }
    return n3;
  };
  function o2(n3) {
    t2 = n3(e2);
  }
  function s2(o3) {
    let { props: s3 } = e2, c3 = ls(e2.parent) || {}, l2 = [], u2 = /* @__PURE__ */ new Set(), d2 = {}, f2 = 1 / 0;
    for (let t3 = 0; t3 < fs; t3++) {
      let p3 = ds[t3], m2 = n2[p3], h2 = s3[p3] === void 0 ? c3[p3] : s3[p3], g2 = eo(h2), _2 = p3 === o3 ? m2.isActive : null;
      _2 === false && (f2 = t3);
      let v2 = h2 === c3[p3] && h2 !== s3[p3] && g2;
      if (v2 && (r2 || i2) && e2.manuallyAnimateOnMount && (v2 = false), m2.protectedKeys = { ...d2 }, !m2.isActive && _2 === null || !h2 && !m2.prevProp || $a(h2) || typeof h2 == `boolean`) continue;
      if (p3 === `exit` && m2.isActive && _2 !== true) {
        m2.prevResolvedValues && (d2 = { ...d2, ...m2.prevResolvedValues });
        continue;
      }
      let y2 = hs(m2.prevProp, h2), b2 = y2 || p3 === o3 && m2.isActive && !v2 && g2 || t3 > f2 && g2, x2 = false, S2 = Array.isArray(h2) ? h2 : [h2], C2 = S2.reduce(a2(p3), {});
      _2 === false && (C2 = {});
      let { prevResolvedValues: w2 = {} } = m2, T2 = { ...w2, ...C2 }, E2 = (t4) => {
        b2 = true, u2.has(t4) && (x2 = true, u2.delete(t4)), m2.needsAnimating[t4] = true;
        let n3 = e2.getValue(t4);
        n3 && (n3.liveStyle = false);
      };
      for (let e3 in T2) {
        let t4 = C2[e3], n3 = w2[e3];
        if (d2.hasOwnProperty(e3)) continue;
        let r3 = false;
        r3 = Ei(t4) && Ei(n3) ? !us(t4, n3) || y2 : t4 !== n3, r3 ? t4 == null ? u2.add(e3) : E2(e3) : t4 !== void 0 && u2.has(e3) ? E2(e3) : m2.protectedKeys[e3] = true;
      }
      m2.prevProp = h2, m2.prevResolvedValues = C2, m2.isActive && (d2 = { ...d2, ...C2 }), (r2 || i2) && e2.blockInitialAnimation && (b2 = false);
      let ee2 = v2 && y2;
      b2 && (!ee2 || x2) && l2.push(...S2.map((t4) => {
        let n3 = { type: p3 };
        if (typeof t4 == `string` && (r2 || i2) && !ee2 && e2.manuallyAnimateOnMount && e2.parent) {
          let { parent: r3 } = e2, i3 = wi(r3, t4);
          if (r3.enteringChildren && i3) {
            let { delayChildren: t5 } = i3.transition || {};
            n3.delay = ri(r3.enteringChildren, e2, t5);
          }
        }
        return { animation: t4, options: n3 };
      }));
    }
    if (u2.size) {
      let t3 = {};
      if (typeof s3.initial != `boolean`) {
        let n3 = wi(e2, Array.isArray(s3.initial) ? s3.initial[0] : s3.initial);
        n3 && n3.transition && (t3.transition = n3.transition);
      }
      u2.forEach((n3) => {
        let r3 = e2.getBaseTarget(n3), i3 = e2.getValue(n3);
        i3 && (i3.liveStyle = true), t3[n3] = r3 ?? null;
      }), l2.push({ animation: t3 });
    }
    let p2 = !!l2.length;
    return r2 && (s3.initial === false || s3.initial === s3.animate) && !e2.manuallyAnimateOnMount && (p2 = false), r2 = false, i2 = false, p2 ? t2(l2) : Promise.resolve();
  }
  function c2(t3, r3) {
    if (n2[t3].isActive === r3) return Promise.resolve();
    e2.variantChildren?.forEach((e3) => e3.animationState?.setActive(t3, r3)), n2[t3].isActive = r3;
    let i3 = s2(t3);
    for (let e3 in n2) n2[e3].protectedKeys = {};
    return i3;
  }
  return { animateChanges: s2, setActive: c2, setAnimateFunction: o2, getState: () => n2, reset: () => {
    n2 = _s(), i2 = true;
  } };
}
function hs(e2, t2) {
  return typeof t2 == `string` ? t2 !== e2 : Array.isArray(t2) ? !us(t2, e2) : false;
}
function gs(e2 = false) {
  return { isActive: e2, protectedKeys: {}, needsAnimating: {}, prevResolvedValues: {} };
}
function _s() {
  return { animate: gs(true), whileInView: gs(), whileHover: gs(), whileTap: gs(), whileDrag: gs(), whileFocus: gs(), exit: gs() };
}
function vs(e2, t2) {
  e2.min = t2.min, e2.max = t2.max;
}
function ys(e2, t2) {
  vs(e2.x, t2.x), vs(e2.y, t2.y);
}
function bs(e2, t2) {
  e2.translate = t2.translate, e2.scale = t2.scale, e2.originPoint = t2.originPoint, e2.origin = t2.origin;
}
var xs = 0.9999, Ss = 1.0001, Cs = -0.01, ws = 0.01;
function $(e2) {
  return e2.max - e2.min;
}
function Ts(e2, t2, n2) {
  return Math.abs(e2 - t2) <= n2;
}
function Es(e2, t2, n2, r2 = 0.5) {
  e2.origin = r2, e2.originPoint = Y(t2.min, t2.max, e2.origin), e2.scale = $(n2) / $(t2), e2.translate = Y(n2.min, n2.max, e2.origin) - e2.originPoint, (e2.scale >= xs && e2.scale <= Ss || isNaN(e2.scale)) && (e2.scale = 1), (e2.translate >= Cs && e2.translate <= ws || isNaN(e2.translate)) && (e2.translate = 0);
}
function Ds(e2, t2, n2, r2) {
  Es(e2.x, t2.x, n2.x, r2 ? r2.originX : void 0), Es(e2.y, t2.y, n2.y, r2 ? r2.originY : void 0);
}
function Os(e2, t2, n2, r2 = 0) {
  e2.min = (r2 ? Y(n2.min, n2.max, r2) : n2.min) + t2.min, e2.max = e2.min + $(t2);
}
function ks(e2, t2, n2, r2) {
  Os(e2.x, t2.x, n2.x, r2?.x), Os(e2.y, t2.y, n2.y, r2?.y);
}
function As(e2, t2, n2, r2 = 0) {
  let i2 = r2 ? Y(n2.min, n2.max, r2) : n2.min;
  e2.min = t2.min - i2, e2.max = e2.min + $(t2);
}
function js(e2, t2, n2, r2) {
  As(e2.x, t2.x, n2.x, r2?.x), As(e2.y, t2.y, n2.y, r2?.y);
}
function Ms(e2, t2, n2, r2, i2) {
  return e2 -= t2, e2 = Eo(e2, 1 / n2, r2), i2 !== void 0 && (e2 = Eo(e2, 1 / i2, r2)), e2;
}
function Ns(e2, t2 = 0, n2 = 1, r2 = 0.5, i2, a2 = e2, o2 = e2) {
  if (It.test(t2) && (t2 = parseFloat(t2), t2 = Y(o2.min, o2.max, t2 / 100) - o2.min), typeof t2 != `number`) return;
  let s2 = Y(a2.min, a2.max, r2);
  e2 === a2 && (s2 -= t2), e2.min = Ms(e2.min, t2, n2, s2, i2), e2.max = Ms(e2.max, t2, n2, s2, i2);
}
function Ps(e2, t2, [n2, r2, i2], a2, o2) {
  Ns(e2, t2[n2], t2[r2], t2[i2], t2.scale, a2, o2);
}
var Fs = [`x`, `scaleX`, `originX`], Is = [`y`, `scaleY`, `originY`];
function Ls(e2, t2, n2, r2) {
  Ps(e2.x, t2, Fs, n2 ? n2.x : void 0, r2 ? r2.x : void 0), Ps(e2.y, t2, Is, n2 ? n2.y : void 0, r2 ? r2.y : void 0);
}
function Rs(e2) {
  return e2.translate === 0 && e2.scale === 1;
}
function zs(e2) {
  return Rs(e2.x) && Rs(e2.y);
}
function Bs(e2, t2) {
  return e2.min === t2.min && e2.max === t2.max;
}
function Vs(e2, t2) {
  return Bs(e2.x, t2.x) && Bs(e2.y, t2.y);
}
function Hs(e2, t2) {
  return Math.round(e2.min) === Math.round(t2.min) && Math.round(e2.max) === Math.round(t2.max);
}
function Us(e2, t2) {
  return Hs(e2.x, t2.x) && Hs(e2.y, t2.y);
}
function Ws(e2) {
  return $(e2.x) / $(e2.y);
}
function Gs(e2, t2) {
  return e2.translate === t2.translate && e2.scale === t2.scale && e2.originPoint === t2.originPoint;
}
function Ks(e2) {
  return [e2(`x`), e2(`y`)];
}
function qs(e2, t2, n2) {
  let r2 = ``, i2 = e2.x.translate / t2.x, a2 = e2.y.translate / t2.y, o2 = n2?.z || 0;
  if ((i2 || a2 || o2) && (r2 = `translate3d(${i2}px, ${a2}px, ${o2}px) `), (t2.x !== 1 || t2.y !== 1) && (r2 += `scale(${1 / t2.x}, ${1 / t2.y}) `), n2) {
    let { transformPerspective: e3, rotate: t3, pathRotation: i3, rotateX: a3, rotateY: o3, skewX: s3, skewY: c3 } = n2;
    e3 && (r2 = `perspective(${e3}px) ${r2}`), t3 && (r2 += `rotate(${t3}deg) `), i3 && (r2 += `rotate(${i3}deg) `), a3 && (r2 += `rotateX(${a3}deg) `), o3 && (r2 += `rotateY(${o3}deg) `), s3 && (r2 += `skewX(${s3}deg) `), c3 && (r2 += `skewY(${c3}deg) `);
  }
  let s2 = e2.x.scale * t2.x, c2 = e2.y.scale * t2.y;
  return (s2 !== 1 || c2 !== 1) && (r2 += `scale(${s2}, ${c2})`), r2 || `none`;
}
var Js = aa.length, Ys = (e2) => typeof e2 == `string` ? parseFloat(e2) : e2, Xs = (e2) => typeof e2 == `number` || q.test(e2);
function Zs(e2, t2, n2, r2, i2, a2) {
  i2 ? (e2.opacity = Y(0, n2.opacity ?? 1, $s(r2)), e2.opacityExit = Y(t2.opacity ?? 1, 0, ec(r2))) : a2 && (e2.opacity = Y(t2.opacity ?? 1, n2.opacity ?? 1, r2));
  for (let i3 = 0; i3 < Js; i3++) {
    let a3 = aa[i3], o2 = Qs(t2, a3), s2 = Qs(n2, a3);
    o2 === void 0 && s2 === void 0 || (o2 ||= 0, s2 ||= 0, o2 === 0 || s2 === 0 || Xs(o2) === Xs(s2) ? (e2[a3] = Math.max(Y(Ys(o2), Ys(s2), r2), 0), (It.test(s2) || It.test(o2)) && (e2[a3] += `%`)) : e2[a3] = s2);
  }
  (t2.rotate || n2.rotate) && (e2.rotate = Y(t2.rotate || 0, n2.rotate || 0, r2));
}
function Qs(e2, t2) {
  return e2[t2] === void 0 ? e2.borderRadius : e2[t2];
}
var $s = tc(0, 0.5, Xe), ec = tc(0.5, 0.95, B);
function tc(e2, t2, n2) {
  return (r2) => r2 < e2 ? 0 : r2 > t2 ? 1 : n2(Ie(e2, t2, r2));
}
function nc(e2, t2, n2) {
  let r2 = Z(e2) ? e2 : ci(e2);
  return r2.start(vi(``, r2, t2, n2)), r2.animation;
}
function rc(e2, t2, n2, r2 = { passive: true }) {
  return e2.addEventListener(t2, n2, r2), () => e2.removeEventListener(t2, n2, r2);
}
var ic = (e2, t2) => e2.depth - t2.depth, ac = class {
  constructor() {
    this.children = [], this.isDirty = false;
  }
  add(e2) {
    R(this.children, e2), this.isDirty = true;
  }
  remove(e2) {
    ke(this.children, e2), this.isDirty = true;
  }
  forEach(e2) {
    this.isDirty && this.children.sort(ic), this.isDirty = false, this.children.forEach(e2);
  }
};
function oc(e2, t2) {
  let n2 = K.now(), r2 = ({ timestamp: i2 }) => {
    let a2 = i2 - n2;
    a2 >= t2 && (ut(r2), e2(a2 - t2));
  };
  return W.setup(r2, true), () => ut(r2);
}
function sc(e2) {
  return Z(e2) ? e2.get() : e2;
}
var cc = class {
  constructor() {
    this.members = [];
  }
  add(e2) {
    R(this.members, e2);
    for (let t2 = this.members.length - 1; t2 >= 0; t2--) {
      let n2 = this.members[t2];
      if (n2 === e2 || n2 === this.lead || n2 === this.prevLead) continue;
      let r2 = n2.instance;
      (!r2 || r2.isConnected === false) && !n2.snapshot && (ke(this.members, n2), n2.unmount());
    }
    e2.scheduleRender();
  }
  remove(e2) {
    if (ke(this.members, e2), e2 === this.prevLead && (this.prevLead = void 0), e2 === this.lead) {
      let e3 = this.members[this.members.length - 1];
      e3 && this.promote(e3);
    }
  }
  relegate(e2) {
    for (let t2 = this.members.indexOf(e2) - 1; t2 >= 0; t2--) {
      let e3 = this.members[t2];
      if (e3.isPresent !== false && e3.instance?.isConnected !== false) return this.promote(e3), true;
    }
    return false;
  }
  promote(e2, t2) {
    let n2 = this.lead;
    if (e2 !== n2 && (this.prevLead = n2, this.lead = e2, e2.show(), n2)) {
      n2.updateSnapshot(), e2.scheduleRender();
      let { layoutDependency: r2 } = n2.options, { layoutDependency: i2 } = e2.options;
      (r2 === void 0 || r2 !== i2) && (e2.resumeFrom = n2, t2 && (n2.preserveOpacity = true), n2.snapshot && (e2.snapshot = n2.snapshot, e2.snapshot.latestValues = n2.animationValues || n2.latestValues), e2.root?.isUpdating && (e2.isLayoutDirty = true)), e2.options.crossfade === false && n2.hide();
    }
  }
  exitAnimationComplete() {
    this.members.forEach((e2) => {
      e2.options.onExitComplete?.(), e2.resumingFrom?.options.onExitComplete?.();
    });
  }
  scheduleRender() {
    this.members.forEach((e2) => e2.instance && e2.scheduleRender(false));
  }
  removeLeadSnapshot() {
    this.lead?.snapshot && (this.lead.snapshot = void 0);
  }
}, lc = { hasAnimatedSinceResize: true, hasEverUpdated: false }, uc = { nodes: 0, calculatedTargetDeltas: 0, calculatedProjections: 0 }, dc = [``, `X`, `Y`, `Z`], fc = 1e3, pc = 0;
function mc(e2, t2, n2, r2) {
  let { latestValues: i2 } = t2;
  i2[e2] && (n2[e2] = i2[e2], t2.setStaticValue(e2, 0), r2 && (r2[e2] = 0));
}
function hc(e2) {
  if (e2.hasCheckedOptimisedAppear = true, e2.root === e2) return;
  let { visualElement: t2 } = e2.options;
  if (!t2) return;
  let n2 = Pi(t2);
  if (window.MotionHasOptimisedAnimation(n2, `transform`)) {
    let { layout: t3, layoutId: r3 } = e2.options;
    window.MotionCancelOptimisedAnimation(n2, `transform`, W, !(t3 || r3));
  }
  let { parent: r2 } = e2;
  r2 && !r2.hasCheckedOptimisedAppear && hc(r2);
}
function gc({ attachResizeListener: e2, defaultParent: t2, measureScroll: n2, checkIsScrollRoot: r2, resetTransform: i2 }) {
  return class {
    constructor(e3 = {}, n3 = t2?.()) {
      this.id = pc++, this.animationId = 0, this.animationCommitId = 0, this.children = /* @__PURE__ */ new Set(), this.options = {}, this.isTreeAnimating = false, this.isAnimationBlocked = false, this.isLayoutDirty = false, this.isProjectionDirty = false, this.isSharedProjectionDirty = false, this.isTransformDirty = false, this.updateManuallyBlocked = false, this.updateBlockedByResize = false, this.isUpdating = false, this.isSVG = false, this.needsReset = false, this.shouldResetTransform = false, this.hasCheckedOptimisedAppear = false, this.treeScale = { x: 1, y: 1 }, this.eventHandlers = /* @__PURE__ */ new Map(), this.hasTreeAnimated = false, this.layoutVersion = 0, this.updateScheduled = false, this.scheduleUpdate = () => this.update(), this.projectionUpdateScheduled = false, this.checkUpdateFailed = () => {
        this.isUpdating && (this.isUpdating = false, this.clearAllSnapshots());
      }, this.updateProjection = () => {
        this.projectionUpdateScheduled = false, Ga.value && (uc.nodes = uc.calculatedTargetDeltas = uc.calculatedProjections = 0), this.nodes.forEach(yc), this.nodes.forEach(Oc), this.nodes.forEach(kc), this.nodes.forEach(bc), Ga.addProjectionMetrics && Ga.addProjectionMetrics(uc);
      }, this.resolvedRelativeTargetAt = 0, this.linkedParentVersion = 0, this.hasProjected = false, this.isVisible = true, this.animationProgress = 0, this.sharedNodes = /* @__PURE__ */ new Map(), this.latestValues = e3, this.root = n3 ? n3.root || n3 : this, this.path = n3 ? [...n3.path, n3] : [], this.parent = n3, this.depth = n3 ? n3.depth + 1 : 0;
      for (let e4 = 0; e4 < this.path.length; e4++) this.path[e4].shouldResetTransform = true;
      this.root === this && (this.nodes = new ac());
    }
    addEventListener(e3, t3) {
      return this.eventHandlers.has(e3) || this.eventHandlers.set(e3, new V()), this.eventHandlers.get(e3).add(t3);
    }
    notifyListeners(e3, ...t3) {
      let n3 = this.eventHandlers.get(e3);
      n3 && n3.notify(...t3);
    }
    hasListeners(e3) {
      return this.eventHandlers.has(e3);
    }
    mount(t3) {
      if (this.instance) return;
      this.isSVG = Aa(t3) && !Ka(t3), this.instance = t3;
      let { layoutId: n3, layout: r3, visualElement: i3 } = this.options;
      if (i3 && !i3.current && i3.mount(t3), this.root.nodes.add(this), this.parent && this.parent.children.add(this), this.root.hasTreeAnimated && (r3 || n3) && (this.isLayoutDirty = true), e2) {
        let n4, r4 = 0, i4 = () => this.root.updateBlockedByResize = false;
        W.read(() => {
          r4 = window.innerWidth;
        }), e2(t3, () => {
          let e3 = window.innerWidth;
          e3 !== r4 && (r4 = e3, this.root.updateBlockedByResize = true, n4 && n4(), n4 = oc(i4, 250), lc.hasAnimatedSinceResize && (lc.hasAnimatedSinceResize = false, this.nodes.forEach(Dc)));
        });
      }
      n3 && this.root.registerSharedNode(n3, this), this.options.animate !== false && i3 && (n3 || r3) && this.addEventListener(`didUpdate`, ({ delta: e3, hasLayoutChanged: t4, hasRelativeLayoutChanged: n4, layout: r4 }) => {
        if (this.isTreeAnimationBlocked()) {
          this.target = void 0, this.relativeTarget = void 0;
          return;
        }
        let a2 = this.options.transition || i3.getDefaultTransition() || Ic, { onLayoutAnimationStart: o2, onLayoutAnimationComplete: s2 } = i3.getProps(), c2 = !this.targetLayout || !Us(this.targetLayout, r4), l2 = !t4 && n4;
        if (this.options.layoutRoot || this.resumeFrom || l2 || t4 && (c2 || !this.currentAnimation)) {
          this.resumeFrom && (this.resumingFrom = this.resumeFrom, this.resumingFrom.resumingFrom = void 0);
          let t5 = { ...ui(a2, `layout`), onPlay: o2, onComplete: s2 };
          (i3.shouldReduceMotion || this.options.layoutRoot) && (t5.delay = 0, t5.type = false), this.startAnimation(t5), this.setAnimationOrigin(e3, l2, t5.path);
        } else t4 || Dc(this), this.isLead() && this.options.onExitComplete && this.options.onExitComplete();
        this.targetLayout = r4;
      });
    }
    unmount() {
      this.options.layoutId && this.willUpdate(), this.root.nodes.remove(this);
      let e3 = this.getStack();
      e3 && e3.remove(this), this.parent && this.parent.children.delete(this), this.instance = void 0, this.eventHandlers.clear(), ut(this.updateProjection);
    }
    blockUpdate() {
      this.updateManuallyBlocked = true;
    }
    unblockUpdate() {
      this.updateManuallyBlocked = false;
    }
    isUpdateBlocked() {
      return this.updateManuallyBlocked || this.updateBlockedByResize;
    }
    isTreeAnimationBlocked() {
      return this.isAnimationBlocked || this.parent && this.parent.isTreeAnimationBlocked() || false;
    }
    startUpdate() {
      this.isUpdateBlocked() || (this.isUpdating = true, this.nodes && this.nodes.forEach(Ac), this.animationId++);
    }
    getTransformTemplate() {
      let { visualElement: e3 } = this.options;
      return e3 && e3.getProps().transformTemplate;
    }
    willUpdate(e3 = true) {
      if (this.root.hasTreeAnimated = true, this.root.isUpdateBlocked()) {
        this.options.onExitComplete && this.options.onExitComplete();
        return;
      }
      if (window.MotionCancelOptimisedAnimation && !this.hasCheckedOptimisedAppear && hc(this), !this.root.isUpdating && this.root.startUpdate(), this.isLayoutDirty) return;
      this.isLayoutDirty = true;
      for (let e4 = 0; e4 < this.path.length; e4++) {
        let t4 = this.path[e4];
        t4.shouldResetTransform = true, (typeof t4.latestValues.x == `string` || typeof t4.latestValues.y == `string`) && (t4.isLayoutDirty = true), t4.updateScroll(`snapshot`), t4.options.layoutRoot && t4.willUpdate(false);
      }
      let { layoutId: t3, layout: n3 } = this.options;
      if (t3 === void 0 && !n3) return;
      let r3 = this.getTransformTemplate();
      this.prevTransformTemplateValue = r3 ? r3(this.latestValues, ``) : void 0, this.updateSnapshot(), e3 && this.notifyListeners(`willUpdate`);
    }
    update() {
      if (this.updateScheduled = false, this.isUpdateBlocked()) {
        let e4 = this.updateBlockedByResize;
        this.unblockUpdate(), this.updateBlockedByResize = false, this.clearAllSnapshots(), e4 && this.nodes.forEach(Cc), this.nodes.forEach(Sc);
        return;
      }
      if (this.animationId <= this.animationCommitId) {
        this.nodes.forEach(wc);
        return;
      }
      this.animationCommitId = this.animationId, this.isUpdating ? (this.isUpdating = false, this.nodes.forEach(Tc), this.nodes.forEach(Ec), this.nodes.forEach(_c), this.nodes.forEach(vc)) : this.nodes.forEach(wc), this.clearAllSnapshots();
      let e3 = K.now();
      G.delta = z(0, 1e3 / 60, e3 - G.timestamp), G.timestamp = e3, G.isProcessing = true, dt.update.process(G), dt.preRender.process(G), dt.render.process(G), G.isProcessing = false;
    }
    didUpdate() {
      this.updateScheduled || (this.updateScheduled = true, la.read(this.scheduleUpdate));
    }
    clearAllSnapshots() {
      this.nodes.forEach(xc), this.sharedNodes.forEach(jc);
    }
    scheduleUpdateProjection() {
      this.projectionUpdateScheduled || (this.projectionUpdateScheduled = true, W.preRender(this.updateProjection, false, true));
    }
    scheduleCheckAfterUnmount() {
      W.postRender(() => {
        this.isLayoutDirty ? this.root.didUpdate() : this.root.checkUpdateFailed();
      });
    }
    updateSnapshot() {
      this.snapshot || !this.instance || (this.snapshot = this.measure(), this.snapshot && !$(this.snapshot.measuredBox.x) && !$(this.snapshot.measuredBox.y) && (this.snapshot = void 0));
    }
    updateLayout() {
      if (!this.instance || (this.updateScroll(), !(this.options.alwaysMeasureLayout && this.isLead()) && !this.isLayoutDirty)) return;
      if (this.resumeFrom && !this.resumeFrom.instance) for (let e4 = 0; e4 < this.path.length; e4++) this.path[e4].updateScroll();
      let e3 = this.layout;
      this.layout = this.measure(false), this.layoutVersion++, this.layoutCorrected ||= Q(), this.isLayoutDirty = false, this.projectionDelta = void 0, this.notifyListeners(`measure`, this.layout.layoutBox);
      let { visualElement: t3 } = this.options;
      t3 && t3.notify(`LayoutMeasure`, this.layout.layoutBox, e3 ? e3.layoutBox : void 0);
    }
    updateScroll(e3 = `measure`) {
      let t3 = !!(this.options.layoutScroll && this.instance);
      if (this.scroll && this.scroll.animationId === this.root.animationId && this.scroll.phase === e3 && (t3 = false), t3 && this.instance) {
        let t4 = r2(this.instance);
        this.scroll = { animationId: this.root.animationId, phase: e3, isRoot: t4, offset: n2(this.instance), wasRoot: this.scroll ? this.scroll.isRoot : t4 };
      }
    }
    resetTransform() {
      if (!i2) return;
      let e3 = this.isLayoutDirty || this.shouldResetTransform || this.options.alwaysMeasureLayout, t3 = this.projectionDelta && !zs(this.projectionDelta), n3 = this.getTransformTemplate(), r3 = n3 ? n3(this.latestValues, ``) : void 0, a2 = r3 !== this.prevTransformTemplateValue;
      e3 && this.instance && (t3 || Co(this.latestValues) || a2) && (i2(this.instance, r3), this.shouldResetTransform = false, this.scheduleRender());
    }
    measure(e3 = true) {
      let t3 = this.measurePageBox(), n3 = this.removeElementScroll(t3);
      return e3 && (n3 = this.removeTransform(n3)), Bc(n3), { animationId: this.root.animationId, measuredBox: t3, layoutBox: n3, latestValues: {}, source: this.id };
    }
    measurePageBox() {
      let { visualElement: e3 } = this.options;
      if (!e3) return Q();
      let t3 = e3.measureViewportBox();
      if (!(this.scroll?.wasRoot || this.path.some(Hc))) {
        let { scroll: e4 } = this.root;
        e4 && (No(t3.x, e4.offset.x), No(t3.y, e4.offset.y));
      }
      return t3;
    }
    removeElementScroll(e3) {
      let t3 = Q();
      if (ys(t3, e3), this.scroll?.wasRoot) return t3;
      for (let n3 = 0; n3 < this.path.length; n3++) {
        let r3 = this.path[n3], { scroll: i3, options: a2 } = r3;
        r3 !== this.root && i3 && a2.layoutScroll && (i3.wasRoot && ys(t3, e3), No(t3.x, i3.offset.x), No(t3.y, i3.offset.y));
      }
      return t3;
    }
    applyTransform(e3, t3 = false, n3) {
      let r3 = n3 || Q();
      ys(r3, e3);
      for (let e4 = 0; e4 < this.path.length; e4++) {
        let n4 = this.path[e4];
        !t3 && n4.options.layoutScroll && n4.scroll && n4 !== n4.root && (No(r3.x, -n4.scroll.offset.x), No(r3.y, -n4.scroll.offset.y)), Co(n4.latestValues) && Io(r3, n4.latestValues, n4.layout?.layoutBox);
      }
      return Co(this.latestValues) && Io(r3, this.latestValues, this.layout?.layoutBox), r3;
    }
    removeTransform(e3) {
      let t3 = Q();
      ys(t3, e3);
      for (let e4 = 0; e4 < this.path.length; e4++) {
        let n3 = this.path[e4];
        if (!Co(n3.latestValues)) continue;
        let r3;
        n3.instance && (So(n3.latestValues) && n3.updateSnapshot(), r3 = Q(), ys(r3, n3.measurePageBox())), Ls(t3, n3.latestValues, n3.snapshot?.layoutBox, r3);
      }
      return Co(this.latestValues) && Ls(t3, this.latestValues), t3;
    }
    setTargetDelta(e3) {
      this.targetDelta = e3, this.root.scheduleUpdateProjection(), this.isProjectionDirty = true;
    }
    setOptions(e3) {
      this.options = { ...this.options, ...e3, crossfade: e3.crossfade === void 0 ? true : e3.crossfade };
    }
    clearMeasurements() {
      this.scroll = void 0, this.layout = void 0, this.snapshot = void 0, this.prevTransformTemplateValue = void 0, this.targetDelta = void 0, this.target = void 0, this.isLayoutDirty = false;
    }
    forceRelativeParentToResolveTarget() {
      this.relativeParent && this.relativeParent.resolvedRelativeTargetAt !== G.timestamp && this.relativeParent.resolveTargetDelta(true);
    }
    resolveTargetDelta(e3 = false) {
      let t3 = this.getLead();
      this.isProjectionDirty ||= t3.isProjectionDirty, this.isTransformDirty ||= t3.isTransformDirty, this.isSharedProjectionDirty ||= t3.isSharedProjectionDirty;
      let n3 = !!this.resumingFrom || this !== t3;
      if (!(e3 || n3 && this.isSharedProjectionDirty || this.isProjectionDirty || this.parent?.isProjectionDirty || this.attemptToResolveRelativeTarget || this.root.updateBlockedByResize)) return;
      let { layout: r3, layoutId: i3 } = this.options;
      if (!this.layout || !(r3 || i3)) return;
      this.resolvedRelativeTargetAt = G.timestamp;
      let a2 = this.getClosestProjectingParent();
      a2 && this.linkedParentVersion !== a2.layoutVersion && !a2.options.layoutRoot && this.removeRelativeTarget(), !this.targetDelta && !this.relativeTarget && (this.options.layoutAnchor !== false && a2 && a2.layout ? this.createRelativeTarget(a2, this.layout.layoutBox, a2.layout.layoutBox) : this.removeRelativeTarget()), !(!this.relativeTarget && !this.targetDelta) && (this.target || (this.target = Q(), this.targetWithTransforms = Q()), this.relativeTarget && this.relativeTargetOrigin && this.relativeParent && this.relativeParent.target ? (this.forceRelativeParentToResolveTarget(), ks(this.target, this.relativeTarget, this.relativeParent.target, this.options.layoutAnchor || void 0)) : this.targetDelta ? (this.resumingFrom ? this.applyTransform(this.layout.layoutBox, false, this.target) : ys(this.target, this.layout.layoutBox), ko(this.target, this.targetDelta)) : ys(this.target, this.layout.layoutBox), this.attemptToResolveRelativeTarget && (this.attemptToResolveRelativeTarget = false, this.options.layoutAnchor !== false && a2 && !!a2.resumingFrom == !!this.resumingFrom && !a2.options.layoutScroll && a2.target && this.animationProgress !== 1 ? this.createRelativeTarget(a2, this.target, a2.target) : this.relativeParent = this.relativeTarget = void 0), Ga.value && uc.calculatedTargetDeltas++);
    }
    getClosestProjectingParent() {
      if (!(!this.parent || So(this.parent.latestValues) || wo(this.parent.latestValues))) return this.parent.isProjecting() ? this.parent : this.parent.getClosestProjectingParent();
    }
    isProjecting() {
      return !!((this.relativeTarget || this.targetDelta || this.options.layoutRoot) && this.layout);
    }
    createRelativeTarget(e3, t3, n3) {
      this.relativeParent = e3, this.linkedParentVersion = e3.layoutVersion, this.forceRelativeParentToResolveTarget(), this.relativeTarget = Q(), this.relativeTargetOrigin = Q(), js(this.relativeTargetOrigin, t3, n3, this.options.layoutAnchor || void 0), ys(this.relativeTarget, this.relativeTargetOrigin);
    }
    removeRelativeTarget() {
      this.relativeParent = this.relativeTarget = void 0;
    }
    calcProjection() {
      let e3 = this.getLead(), t3 = !!this.resumingFrom || this !== e3, n3 = true;
      if ((this.isProjectionDirty || this.parent?.isProjectionDirty) && (n3 = false), t3 && (this.isSharedProjectionDirty || this.isTransformDirty) && (n3 = false), this.resolvedRelativeTargetAt === G.timestamp && (n3 = false), n3) return;
      let { layout: r3, layoutId: i3 } = this.options;
      if (this.isTreeAnimating = !!(this.parent && this.parent.isTreeAnimating || this.currentAnimation || this.pendingAnimation), this.isTreeAnimating || (this.targetDelta = this.relativeTarget = void 0), !this.layout || !(r3 || i3)) return;
      ys(this.layoutCorrected, this.layout.layoutBox);
      let a2 = this.treeScale.x, o2 = this.treeScale.y;
      Mo(this.layoutCorrected, this.treeScale, this.path, t3), e3.layout && !e3.target && (this.treeScale.x !== 1 || this.treeScale.y !== 1) && (e3.target = e3.layout.layoutBox, e3.targetWithTransforms = Q());
      let { target: s2 } = e3;
      if (!s2) {
        this.prevProjectionDelta && (this.createProjectionDeltas(), this.scheduleRender());
        return;
      }
      !this.projectionDelta || !this.prevProjectionDelta ? this.createProjectionDeltas() : (bs(this.prevProjectionDelta.x, this.projectionDelta.x), bs(this.prevProjectionDelta.y, this.projectionDelta.y)), Ds(this.projectionDelta, this.layoutCorrected, s2, this.latestValues), (this.treeScale.x !== a2 || this.treeScale.y !== o2 || !Gs(this.projectionDelta.x, this.prevProjectionDelta.x) || !Gs(this.projectionDelta.y, this.prevProjectionDelta.y)) && (this.hasProjected = true, this.scheduleRender(), this.notifyListeners(`projectionUpdate`, s2)), Ga.value && uc.calculatedProjections++;
    }
    hide() {
      this.isVisible = false;
    }
    show() {
      this.isVisible = true;
    }
    scheduleRender(e3 = true) {
      if (this.options.visualElement?.scheduleRender(), e3) {
        let e4 = this.getStack();
        e4 && e4.scheduleRender();
      }
      this.resumingFrom && !this.resumingFrom.instance && (this.resumingFrom = void 0);
    }
    createProjectionDeltas() {
      this.prevProjectionDelta = Xa(), this.projectionDelta = Xa(), this.projectionDeltaWithTransform = Xa();
    }
    setAnimationOrigin(e3, t3 = false, n3) {
      let r3 = this.snapshot, i3 = r3 ? r3.latestValues : {}, a2 = { ...this.latestValues }, o2 = Xa();
      (!this.relativeParent || !this.relativeParent.options.layoutRoot) && (this.relativeTarget = this.relativeTargetOrigin = void 0), this.attemptToResolveRelativeTarget = !t3;
      let s2 = Q(), c2 = (r3 ? r3.source : void 0) !== (this.layout ? this.layout.source : void 0), l2 = this.getStack(), u2 = !l2 || l2.members.length <= 1, d2 = !!(c2 && !u2 && this.options.crossfade === true && !this.path.some(Fc));
      this.animationProgress = 0;
      let f2, p2 = n3?.interpolateProjection(e3);
      this.mixTargetDelta = (t4) => {
        let n4 = t4 / 1e3, r4 = p2?.(n4);
        r4 ? (o2.x.translate = r4.x, o2.x.scale = Y(e3.x.scale, 1, n4), o2.x.origin = e3.x.origin, o2.x.originPoint = e3.x.originPoint, o2.y.translate = r4.y, o2.y.scale = Y(e3.y.scale, 1, n4), o2.y.origin = e3.y.origin, o2.y.originPoint = e3.y.originPoint) : (Mc(o2.x, e3.x, n4), Mc(o2.y, e3.y, n4)), this.setTargetDelta(o2), this.relativeTarget && this.relativeTargetOrigin && this.layout && this.relativeParent && this.relativeParent.layout && (js(s2, this.layout.layoutBox, this.relativeParent.layout.layoutBox, this.options.layoutAnchor || void 0), Pc(this.relativeTarget, this.relativeTargetOrigin, s2, n4), f2 && Vs(this.relativeTarget, f2) && (this.isProjectionDirty = false), f2 ||= Q(), ys(f2, this.relativeTarget)), c2 && (this.animationValues = a2, Zs(a2, i3, this.latestValues, n4, d2, u2)), r4 && r4.rotate !== void 0 && (this.animationValues ||= a2, this.animationValues.pathRotation = r4.rotate), this.root.scheduleUpdateProjection(), this.scheduleRender(), this.animationProgress = n4;
      }, this.mixTargetDelta(this.options.layoutRoot ? 1e3 : 0);
    }
    startAnimation(e3) {
      this.notifyListeners(`animationStart`), this.currentAnimation?.stop(), this.resumingFrom?.currentAnimation?.stop(), this.pendingAnimation &&= (ut(this.pendingAnimation), void 0), this.pendingAnimation = W.update(() => {
        lc.hasAnimatedSinceResize = true, this.motionValue ||= ci(0), this.motionValue.jump(0, false), this.currentAnimation = nc(this.motionValue, [0, 1e3], { ...e3, velocity: 0, isSync: true, onUpdate: (t3) => {
          this.mixTargetDelta(t3), e3.onUpdate && e3.onUpdate(t3);
        }, onComplete: () => {
          e3.onComplete && e3.onComplete(), this.completeAnimation();
        } }), this.resumingFrom && (this.resumingFrom.currentAnimation = this.currentAnimation), this.pendingAnimation = void 0;
      });
    }
    completeAnimation() {
      this.resumingFrom && (this.resumingFrom.currentAnimation = void 0, this.resumingFrom.preserveOpacity = void 0);
      let e3 = this.getStack();
      e3 && e3.exitAnimationComplete(), this.resumingFrom = this.currentAnimation = this.animationValues = void 0, this.notifyListeners(`animationComplete`);
    }
    finishAnimation() {
      this.currentAnimation && (this.mixTargetDelta && this.mixTargetDelta(fc), this.currentAnimation.stop()), this.completeAnimation();
    }
    applyTransformsToTarget() {
      let e3 = this.getLead(), { targetWithTransforms: t3, target: n3, layout: r3, latestValues: i3 } = e3;
      if (!(!t3 || !n3 || !r3)) {
        if (this !== e3 && this.layout && r3 && Vc(this.options.animationType, this.layout.layoutBox, r3.layoutBox)) {
          n3 = this.target || Q();
          let t4 = $(this.layout.layoutBox.x);
          n3.x.min = e3.target.x.min, n3.x.max = n3.x.min + t4;
          let r4 = $(this.layout.layoutBox.y);
          n3.y.min = e3.target.y.min, n3.y.max = n3.y.min + r4;
        }
        ys(t3, n3), Io(t3, i3), Ds(this.projectionDeltaWithTransform, this.layoutCorrected, t3, i3);
      }
    }
    registerSharedNode(e3, t3) {
      this.sharedNodes.has(e3) || this.sharedNodes.set(e3, new cc()), this.sharedNodes.get(e3).add(t3);
      let n3 = t3.options.initialPromotionConfig;
      t3.promote({ transition: n3 ? n3.transition : void 0, preserveFollowOpacity: n3 && n3.shouldPreserveFollowOpacity ? n3.shouldPreserveFollowOpacity(t3) : void 0 });
    }
    isLead() {
      let e3 = this.getStack();
      return e3 ? e3.lead === this : true;
    }
    getLead() {
      let { layoutId: e3 } = this.options;
      return e3 && this.getStack()?.lead || this;
    }
    getPrevLead() {
      let { layoutId: e3 } = this.options;
      return e3 ? this.getStack()?.prevLead : void 0;
    }
    getStack() {
      let { layoutId: e3 } = this.options;
      if (e3) return this.root.sharedNodes.get(e3);
    }
    promote({ needsReset: e3, transition: t3, preserveFollowOpacity: n3 } = {}) {
      let r3 = this.getStack();
      r3 && r3.promote(this, n3), e3 && (this.projectionDelta = void 0, this.needsReset = true), t3 && this.setOptions({ transition: t3 });
    }
    relegate() {
      let e3 = this.getStack();
      return e3 ? e3.relegate(this) : false;
    }
    resetSkewAndRotation() {
      let { visualElement: e3 } = this.options;
      if (!e3) return;
      let t3 = false, { latestValues: n3 } = e3;
      if ((n3.z || n3.rotate || n3.rotateX || n3.rotateY || n3.rotateZ || n3.skewX || n3.skewY) && (t3 = true), !t3) return;
      let r3 = {};
      n3.z && mc(`z`, e3, r3, this.animationValues);
      for (let t4 = 0; t4 < dc.length; t4++) mc(`rotate${dc[t4]}`, e3, r3, this.animationValues), mc(`skew${dc[t4]}`, e3, r3, this.animationValues);
      e3.render();
      for (let t4 in r3) e3.setStaticValue(t4, r3[t4]), this.animationValues && (this.animationValues[t4] = r3[t4]);
      e3.scheduleRender();
    }
    applyProjectionStyles(e3, t3) {
      if (!this.instance || this.isSVG) return;
      if (!this.isVisible) {
        e3.visibility = `hidden`;
        return;
      }
      let n3 = this.getTransformTemplate();
      if (this.needsReset) {
        this.needsReset = false, e3.visibility = ``, e3.opacity = ``, e3.pointerEvents = sc(t3?.pointerEvents) || ``, e3.transform = n3 ? n3(this.latestValues, ``) : `none`;
        return;
      }
      let r3 = this.getLead();
      if (!this.projectionDelta || !this.layout || !r3.target) {
        this.options.layoutId && (e3.opacity = this.latestValues.opacity === void 0 ? 1 : this.latestValues.opacity, e3.pointerEvents = sc(t3?.pointerEvents) || ``), this.hasProjected && !Co(this.latestValues) && (e3.transform = n3 ? n3({}, ``) : `none`, this.hasProjected = false);
        return;
      }
      e3.visibility = ``;
      let i3 = r3.animationValues || r3.latestValues;
      this.applyTransformsToTarget();
      let a2 = qs(this.projectionDeltaWithTransform, this.treeScale, i3);
      n3 && (a2 = n3(i3, a2)), e3.transform = a2;
      let { x: o2, y: s2 } = this.projectionDelta;
      e3.transformOrigin = `${o2.origin * 100}% ${s2.origin * 100}% 0`, r3.animationValues ? e3.opacity = r3 === this ? i3.opacity ?? this.latestValues.opacity ?? 1 : this.preserveOpacity ? this.latestValues.opacity : i3.opacityExit : e3.opacity = r3 === this ? i3.opacity === void 0 ? `` : i3.opacity : i3.opacityExit === void 0 ? 0 : i3.opacityExit;
      for (let t4 in qo) {
        if (i3[t4] === void 0) continue;
        let { correct: n4, applyTo: o3, isCSSVariable: s3 } = qo[t4], c2 = a2 === `none` ? i3[t4] : n4(i3[t4], r3);
        if (o3) {
          let t5 = o3.length;
          for (let n5 = 0; n5 < t5; n5++) e3[o3[n5]] = c2;
        } else s3 ? this.options.visualElement.renderState.vars[t4] = c2 : e3[t4] = c2;
      }
      this.options.layoutId && (e3.pointerEvents = r3 === this ? sc(t3?.pointerEvents) || `` : `none`);
    }
    clearSnapshot() {
      this.resumeFrom = this.snapshot = void 0;
    }
    resetTree() {
      this.root.nodes.forEach((e3) => e3.currentAnimation?.stop()), this.root.nodes.forEach(Sc), this.root.sharedNodes.clear();
    }
  };
}
function _c(e2) {
  e2.updateLayout();
}
function vc(e2) {
  let t2 = e2.resumeFrom?.snapshot || e2.snapshot;
  if (e2.isLead() && e2.layout && t2 && e2.hasListeners(`didUpdate`)) {
    let { layoutBox: n2, measuredBox: r2 } = e2.layout, { animationType: i2 } = e2.options, a2 = t2.source !== e2.layout.source;
    if (i2 === `size`) Ks((e3) => {
      let r3 = a2 ? t2.measuredBox[e3] : t2.layoutBox[e3], i3 = $(r3);
      r3.min = n2[e3].min, r3.max = r3.min + i3;
    });
    else if (i2 === `x` || i2 === `y`) {
      let e3 = i2 === `x` ? `y` : `x`;
      vs(a2 ? t2.measuredBox[e3] : t2.layoutBox[e3], n2[e3]);
    } else Vc(i2, t2.layoutBox, n2) && Ks((r3) => {
      let i3 = a2 ? t2.measuredBox[r3] : t2.layoutBox[r3], o3 = $(n2[r3]);
      i3.max = i3.min + o3, e2.relativeTarget && !e2.currentAnimation && (e2.isProjectionDirty = true, e2.relativeTarget[r3].max = e2.relativeTarget[r3].min + o3);
    });
    let o2 = Xa();
    Ds(o2, n2, t2.layoutBox);
    let s2 = Xa();
    a2 ? Ds(s2, e2.applyTransform(r2, true), t2.measuredBox) : Ds(s2, n2, t2.layoutBox);
    let c2 = !zs(o2), l2 = false;
    if (!e2.resumeFrom) {
      let r3 = e2.getClosestProjectingParent();
      if (r3 && !r3.resumeFrom) {
        let { snapshot: i3, layout: a3 } = r3;
        if (i3 && a3) {
          let o3 = e2.options.layoutAnchor || void 0, s3 = Q();
          js(s3, t2.layoutBox, i3.layoutBox, o3);
          let c3 = Q();
          js(c3, n2, a3.layoutBox, o3), Us(s3, c3) || (l2 = true), r3.options.layoutRoot && (e2.relativeTarget = c3, e2.relativeTargetOrigin = s3, e2.relativeParent = r3);
        }
      }
    }
    e2.notifyListeners(`didUpdate`, { layout: n2, snapshot: t2, delta: s2, layoutDelta: o2, hasLayoutChanged: c2, hasRelativeLayoutChanged: l2 });
  } else if (e2.isLead()) {
    let { onExitComplete: t3 } = e2.options;
    t3 && t3();
  }
  e2.options.transition = void 0;
}
function yc(e2) {
  Ga.value && uc.nodes++, e2.parent && (e2.isProjecting() || (e2.isProjectionDirty = e2.parent.isProjectionDirty), e2.isSharedProjectionDirty ||= !!(e2.isProjectionDirty || e2.parent.isProjectionDirty || e2.parent.isSharedProjectionDirty), e2.isTransformDirty ||= e2.parent.isTransformDirty);
}
function bc(e2) {
  e2.isProjectionDirty = e2.isSharedProjectionDirty = e2.isTransformDirty = false;
}
function xc(e2) {
  e2.clearSnapshot();
}
function Sc(e2) {
  e2.clearMeasurements();
}
function Cc(e2) {
  e2.isLayoutDirty = true, e2.updateLayout();
}
function wc(e2) {
  e2.isLayoutDirty = false;
}
function Tc(e2) {
  e2.isAnimationBlocked && e2.layout && !e2.isLayoutDirty && (e2.snapshot = e2.layout, e2.isLayoutDirty = true);
}
function Ec(e2) {
  let { visualElement: t2 } = e2.options;
  t2 && t2.getProps().onBeforeLayoutMeasure && t2.notify(`BeforeLayoutMeasure`), e2.resetTransform();
}
function Dc(e2) {
  e2.finishAnimation(), e2.targetDelta = e2.relativeTarget = e2.target = void 0, e2.isProjectionDirty = true;
}
function Oc(e2) {
  e2.resolveTargetDelta();
}
function kc(e2) {
  e2.calcProjection();
}
function Ac(e2) {
  e2.resetSkewAndRotation();
}
function jc(e2) {
  e2.removeLeadSnapshot();
}
function Mc(e2, t2, n2) {
  e2.translate = Y(t2.translate, 0, n2), e2.scale = Y(t2.scale, 1, n2), e2.origin = t2.origin, e2.originPoint = t2.originPoint;
}
function Nc(e2, t2, n2, r2) {
  e2.min = Y(t2.min, n2.min, r2), e2.max = Y(t2.max, n2.max, r2);
}
function Pc(e2, t2, n2, r2) {
  Nc(e2.x, t2.x, n2.x, r2), Nc(e2.y, t2.y, n2.y, r2);
}
function Fc(e2) {
  return e2.animationValues && e2.animationValues.opacityExit !== void 0;
}
var Ic = { duration: 0.45, ease: [0.4, 0, 0.1, 1] }, Lc = (e2) => typeof navigator < `u` && navigator.userAgent && navigator.userAgent.toLowerCase().includes(e2), Rc = Lc(`applewebkit/`) && !Lc(`chrome/`) ? Math.round : B;
function zc(e2) {
  e2.min = Rc(e2.min), e2.max = Rc(e2.max);
}
function Bc(e2) {
  zc(e2.x), zc(e2.y);
}
function Vc(e2, t2, n2) {
  return e2 === `position` || e2 === `preserve-aspect` && !Ts(Ws(t2), Ws(n2), 0.2);
}
function Hc(e2) {
  return e2 !== e2.root && e2.scroll?.wasRoot;
}
var Uc = gc({ attachResizeListener: (e2, t2) => rc(e2, `resize`, t2), measureScroll: () => ({ x: document.documentElement.scrollLeft || document.body?.scrollLeft || 0, y: document.documentElement.scrollTop || document.body?.scrollTop || 0 }), checkIsScrollRoot: () => true }), Wc = { current: void 0 }, Gc = gc({ measureScroll: (e2) => ({ x: e2.scrollLeft, y: e2.scrollTop }), defaultParent: () => {
  if (!Wc.current) {
    let e2 = new Uc({});
    e2.mount(window), e2.setOptions({ layoutScroll: true }), Wc.current = e2;
  }
  return Wc.current;
}, resetTransform: (e2, t2) => {
  e2.style.transform = t2 === void 0 ? `none` : t2;
}, checkIsScrollRoot: (e2) => window.getComputedStyle(e2).position === `fixed` }), Kc = (0, O.createContext)({ transformPagePoint: (e2) => e2, isStatic: false, reducedMotion: `never` });
function qc(e2, t2) {
  if (typeof e2 == `function`) return e2(t2);
  e2 != null && (e2.current = t2);
}
function Jc(...e2) {
  return (t2) => {
    let n2 = false, r2 = e2.map((e3) => {
      let r3 = qc(e3, t2);
      return !n2 && typeof r3 == `function` && (n2 = true), r3;
    });
    if (n2) return () => {
      for (let t3 = 0; t3 < r2.length; t3++) {
        let n3 = r2[t3];
        typeof n3 == `function` ? n3() : qc(e2[t3], null);
      }
    };
  };
}
function Yc(...e2) {
  return O.useCallback(Jc(...e2), e2);
}
var Xc = class extends O.Component {
  getSnapshotBeforeUpdate(e2) {
    let t2 = this.props.childRef.current;
    if (ca(t2) && e2.isPresent && !this.props.isPresent && this.props.pop !== false) {
      let e3 = t2.offsetParent, n2 = ca(e3) && e3.offsetWidth || 0, r2 = ca(e3) && e3.offsetHeight || 0, i2 = getComputedStyle(t2), a2 = this.props.sizeRef.current;
      a2.height = parseFloat(i2.height), a2.width = parseFloat(i2.width), a2.top = t2.offsetTop, a2.left = t2.offsetLeft, a2.right = n2 - a2.width - a2.left, a2.bottom = r2 - a2.height - a2.top, a2.direction = i2.direction;
    }
    return null;
  }
  componentDidUpdate() {
  }
  render() {
    return this.props.children;
  }
};
function Zc({ children: e2, isPresent: t2, anchorX: n2, anchorY: r2, root: i2, pop: a2 }) {
  let o2 = (0, O.useId)(), s2 = (0, O.useRef)(null), c2 = (0, O.useRef)({ width: 0, height: 0, top: 0, left: 0, right: 0, bottom: 0, direction: `ltr` }), { nonce: l2 } = (0, O.useContext)(Kc), u2 = Yc(s2, e2.props?.ref ?? e2?.ref);
  return (0, O.useInsertionEffect)(() => {
    let { width: e3, height: u3, top: d2, left: f2, right: p2, bottom: m2, direction: h2 } = c2.current;
    if (t2 || a2 === false || !s2.current || !e3 || !u3) return;
    let g2 = h2 === `rtl`, _2 = n2 === `left` ? g2 ? `right: ${p2}` : `left: ${f2}` : g2 ? `left: ${f2}` : `right: ${p2}`, v2 = r2 === `bottom` ? `bottom: ${m2}` : `top: ${d2}`;
    s2.current.dataset.motionPopId = o2;
    let y2 = document.createElement(`style`);
    l2 && (y2.nonce = l2);
    let b2 = i2 ?? document.head;
    return b2.appendChild(y2), y2.sheet && y2.sheet.insertRule(`
          [data-motion-pop-id="${o2}"] {
            position: absolute !important;
            width: ${e3}px !important;
            height: ${u3}px !important;
            ${_2}px !important;
            ${v2}px !important;
          }
        `), () => {
      s2.current?.removeAttribute(`data-motion-pop-id`), b2.contains(y2) && b2.removeChild(y2);
    };
  }, [t2]), (0, A.jsx)(Xc, { isPresent: t2, childRef: s2, sizeRef: c2, pop: a2, children: a2 === false ? e2 : O.cloneElement(e2, { ref: u2 }) });
}
var Qc = ({ children: e2, initial: t2, isPresent: n2, onExitComplete: r2, custom: i2, presenceAffectsLayout: a2, mode: o2, anchorX: s2, anchorY: c2, root: l2 }) => {
  let u2 = De($c), d2 = (0, O.useId)(), f2 = (0, O.useRef)(n2), p2 = (0, O.useRef)(r2);
  L(() => {
    f2.current = n2, p2.current = r2;
  });
  let m2 = true, h2 = (0, O.useMemo)(() => (m2 = false, { id: d2, initial: t2, isPresent: n2, custom: i2, onExitComplete: (e3) => {
    u2.set(e3, true);
    for (let e4 of u2.values()) if (!e4) return;
    r2 && r2();
  }, register: (e3) => (u2.set(e3, false), () => {
    u2.delete(e3), !f2.current && !u2.size && p2.current?.();
  }) }), [n2, u2, r2]);
  return a2 && m2 && (h2 = { ...h2 }), (0, O.useMemo)(() => {
    u2.forEach((e3, t3) => u2.set(t3, false));
  }, [n2]), O.useEffect(() => {
    !n2 && !u2.size && r2 && r2();
  }, [n2]), e2 = (0, A.jsx)(Zc, { pop: o2 === `popLayout`, isPresent: n2, anchorX: s2, anchorY: c2, root: l2, children: e2 }), (0, A.jsx)(Oe.Provider, { value: h2, children: e2 });
};
function $c() {
  return /* @__PURE__ */ new Map();
}
function el(e2 = true) {
  let t2 = (0, O.useContext)(Oe);
  if (t2 === null) return [true, null];
  let { isPresent: n2, onExitComplete: r2, register: i2 } = t2, a2 = (0, O.useId)();
  (0, O.useEffect)(() => {
    if (e2) return i2(a2);
  }, [e2]);
  let o2 = (0, O.useCallback)(() => e2 && r2 && r2(a2), [a2, r2, e2]);
  return !n2 && r2 ? [false, o2] : [true];
}
var tl = (e2) => e2.key || ``;
function nl(e2) {
  let t2 = [];
  return O.Children.forEach(e2, (e3) => {
    (0, O.isValidElement)(e3) && t2.push(e3);
  }), t2;
}
var rl = ({ children: e2, custom: t2, initial: n2 = true, onExitComplete: r2, presenceAffectsLayout: i2 = true, mode: a2 = `sync`, propagate: o2 = false, anchorX: s2 = `left`, anchorY: c2 = `top`, root: l2 }) => {
  let [u2, d2] = el(o2), f2 = (0, O.useMemo)(() => nl(e2), [e2]), p2 = o2 && !u2 ? [] : f2.map(tl), m2 = (0, O.useRef)(true), h2 = (0, O.useRef)(f2), g2 = De(() => /* @__PURE__ */ new Map()), _2 = (0, O.useRef)(/* @__PURE__ */ new Set()), [v2, y2] = (0, O.useState)(f2), [b2, x2] = (0, O.useState)(f2);
  L(() => {
    m2.current = false, h2.current = f2;
    for (let e3 = 0; e3 < b2.length; e3++) {
      let t3 = tl(b2[e3]);
      p2.includes(t3) ? (g2.delete(t3), _2.current.delete(t3)) : g2.get(t3) !== true && g2.set(t3, false);
    }
  }, [b2, p2.length, p2.join(`-`)]);
  let S2 = [];
  if (f2 !== v2) {
    let e3 = [...f2];
    for (let t3 = 0; t3 < b2.length; t3++) {
      let n3 = b2[t3], r3 = tl(n3);
      p2.includes(r3) || (e3.splice(t3, 0, n3), S2.push(n3));
    }
    return a2 === `wait` && S2.length && (e3 = S2), x2(nl(e3)), y2(f2), null;
  }
  let { forceRender: C2 } = (0, O.useContext)(Ee);
  return (0, A.jsx)(A.Fragment, { children: b2.map((e3) => {
    let v3 = tl(e3), y3 = o2 && !u2 ? false : f2 === b2 || p2.includes(v3);
    return (0, A.jsx)(Qc, { isPresent: y3, initial: !m2.current || n2 ? void 0 : false, custom: t2, presenceAffectsLayout: i2, mode: a2, root: l2, onExitComplete: y3 ? void 0 : () => {
      if (_2.current.has(v3)) return;
      if (g2.has(v3)) _2.current.add(v3), g2.set(v3, true);
      else return;
      let e4 = true;
      g2.forEach((t3) => {
        t3 || (e4 = false);
      }), e4 && (C2?.(), x2(h2.current), o2 && d2?.(), r2 && r2());
    }, anchorX: s2, anchorY: c2, children: e3 }, v3);
  }) });
}, il = (0, O.createContext)({ strict: false }), al = { animation: [`animate`, `variants`, `whileHover`, `whileTap`, `exit`, `whileInView`, `whileFocus`, `whileDrag`], exit: [`exit`], drag: [`drag`, `dragControls`], focus: [`whileFocus`], hover: [`whileHover`, `onHoverStart`, `onHoverEnd`], tap: [`whileTap`, `onTap`, `onTapStart`, `onTapCancel`], pan: [`onPan`, `onPanStart`, `onPanSessionStart`, `onPanEnd`], inView: [`whileInView`, `onViewportEnter`, `onViewportLeave`], layout: [`layout`, `layoutId`] }, ol = false;
function sl() {
  if (ol) return;
  let e2 = {};
  for (let t2 in al) e2[t2] = { isEnabled: (e3) => al[t2].some((t3) => !!e3[t3]) };
  po(e2), ol = true;
}
function cl() {
  return sl(), mo();
}
function ll(e2) {
  let t2 = cl();
  for (let n2 in e2) t2[n2] = { ...t2[n2], ...e2[n2] };
  po(t2);
}
var ul = new Set(`animate.exit.variants.initial.style.values.variants.transition.transformTemplate.custom.inherit.onBeforeLayoutMeasure.onAnimationStart.onAnimationComplete.onUpdate.onDragStart.onDrag.onDragEnd.onMeasureDragConstraints.onDirectionLock.onDragTransitionEnd._dragX._dragY.onHoverStart.onHoverEnd.onViewportEnter.onViewportLeave.globalTapTarget.propagate.ignoreStrict.viewport`.split(`.`));
function dl(e2) {
  return e2.startsWith(`while`) || e2.startsWith(`drag`) && e2 !== `draggable` || e2.startsWith(`layout`) || e2.startsWith(`onTap`) || e2.startsWith(`onPan`) || e2.startsWith(`onLayout`) || ul.has(e2);
}
var fl = l({ default: () => pl }), pl, ml = s((() => {
  throw pl = {}, Error(`Could not resolve "@emotion/is-prop-valid" imported by "framer-motion". Is it installed?`);
})), hl = (e2) => !dl(e2);
function gl(e2) {
  typeof e2 == `function` && (hl = (t2) => t2.startsWith(`on`) ? !dl(t2) : e2(t2));
}
try {
  gl((ml(), n(fl)).default);
} catch {
}
function _l(e2, t2, n2) {
  let r2 = {};
  for (let i2 in e2) i2 === `values` && typeof e2.values == `object` || Z(e2[i2]) || (hl(i2) || n2 === true && dl(i2) || !t2 && !dl(i2) || e2.draggable && i2.startsWith(`onDrag`)) && (r2[i2] = e2[i2]);
  return r2;
}
var vl = (0, O.createContext)({});
function yl(e2, t2) {
  if (ro(e2)) {
    let { initial: t3, animate: n2 } = e2;
    return { initial: t3 === false || eo(t3) ? t3 : void 0, animate: eo(n2) ? n2 : void 0 };
  }
  return e2.inherit === false ? {} : t2;
}
function bl(e2) {
  let { initial: t2, animate: n2 } = yl(e2, (0, O.useContext)(vl));
  return (0, O.useMemo)(() => ({ initial: t2, animate: n2 }), [xl(t2), xl(n2)]);
}
function xl(e2) {
  return Array.isArray(e2) ? e2.join(` `) : e2;
}
var Sl = () => ({ style: {}, transform: {}, transformOrigin: {}, vars: {} });
function Cl(e2, t2, n2) {
  for (let r2 in t2) !Z(t2[r2]) && !Jo(r2, n2) && (e2[r2] = t2[r2]);
}
function wl({ transformTemplate: e2 }, t2) {
  return (0, O.useMemo)(() => {
    let n2 = Sl();
    return Ho(n2, t2, e2), Object.assign({}, n2.vars, n2.style);
  }, [t2]);
}
function Tl(e2, t2) {
  let n2 = e2.style || {}, r2 = {};
  return Cl(r2, n2, e2), Object.assign(r2, wl(e2, t2)), r2;
}
function El(e2, t2) {
  let n2 = {}, r2 = Tl(e2, t2);
  return e2.drag && e2.dragListener !== false && (n2.draggable = false, r2.userSelect = r2.WebkitUserSelect = r2.WebkitTouchCallout = `none`, r2.touchAction = e2.drag === true ? `none` : `pan-${e2.drag === `x` ? `y` : `x`}`), e2.tabIndex === void 0 && (e2.onTap || e2.onTapStart || e2.whileTap) && (n2.tabIndex = 0), n2.style = r2, n2;
}
var Dl = () => ({ ...Sl(), attrs: {} });
function Ol(e2, t2, n2, r2) {
  let i2 = (0, O.useMemo)(() => {
    let n3 = Dl();
    return ns(n3, t2, is(r2), e2.transformTemplate, e2.style), { ...n3.attrs, style: { ...n3.style } };
  }, [t2]);
  if (e2.style) {
    let t3 = {};
    Cl(t3, e2.style, e2), i2.style = { ...t3, ...i2.style };
  }
  return i2;
}
var kl = [`animate`, `circle`, `defs`, `desc`, `ellipse`, `g`, `image`, `line`, `filter`, `marker`, `mask`, `metadata`, `path`, `pattern`, `polygon`, `polyline`, `rect`, `stop`, `switch`, `symbol`, `svg`, `text`, `tspan`, `use`, `view`];
function Al(e2) {
  return typeof e2 != `string` || e2.includes(`-`) ? false : !!(kl.indexOf(e2) > -1 || /[A-Z]/u.test(e2));
}
function jl(e2, t2, n2, { latestValues: r2 }, i2, a2 = false, o2) {
  let s2 = (o2 ?? Al(e2) ? Ol : El)(t2, r2, i2, e2), c2 = _l(t2, typeof e2 == `string`, a2), l2 = e2 === O.Fragment ? {} : { ...c2, ...s2, ref: n2 }, { children: u2 } = t2, d2 = (0, O.useMemo)(() => Z(u2) ? u2.get() : u2, [u2]);
  return (0, O.createElement)(e2, { ...l2, children: d2 });
}
function Ml({ scrapeMotionValuesFromProps: e2, createRenderState: t2 }, n2, r2, i2) {
  return { latestValues: Nl(n2, r2, i2, e2), renderState: t2() };
}
function Nl(e2, t2, n2, r2) {
  let i2 = {}, a2 = r2(e2, {});
  for (let e3 in a2) i2[e3] = sc(a2[e3]);
  let { initial: o2, animate: s2 } = e2, c2 = ro(e2), l2 = io(e2);
  t2 && l2 && !c2 && e2.inherit !== false && (o2 === void 0 && (o2 = t2.initial), s2 === void 0 && (s2 = t2.animate));
  let u2 = n2 ? n2.initial === false : false;
  u2 ||= o2 === false;
  let d2 = u2 ? s2 : o2;
  if (d2 && typeof d2 != `boolean` && !$a(d2)) {
    let t3 = Array.isArray(d2) ? d2 : [d2];
    for (let n3 = 0; n3 < t3.length; n3++) {
      let r3 = Ci(e2, t3[n3]);
      if (r3) {
        let { transitionEnd: e3, transition: t4, ...n4 } = r3;
        for (let e4 in n4) {
          let t5 = n4[e4];
          if (Array.isArray(t5)) {
            let e5 = u2 ? t5.length - 1 : 0;
            t5 = t5[e5];
          }
          t5 !== null && (i2[e4] = t5);
        }
        for (let t5 in e3) i2[t5] = e3[t5];
      }
    }
  }
  return i2;
}
var Pl = (e2) => (t2, n2) => {
  let r2 = (0, O.useContext)(vl), i2 = (0, O.useContext)(Oe), a2 = () => Ml(e2, t2, r2, i2);
  return n2 ? a2() : De(a2);
}, Fl = Pl({ scrapeMotionValuesFromProps: Yo, createRenderState: Sl }), Il = Pl({ scrapeMotionValuesFromProps: os, createRenderState: Dl }), Ll = /* @__PURE__ */ Symbol.for(`motionComponentSymbol`);
function Rl(e2, t2, n2) {
  let r2 = (0, O.useRef)(n2);
  (0, O.useInsertionEffect)(() => {
    r2.current = n2;
  });
  let i2 = (0, O.useRef)(null);
  return (0, O.useCallback)((n3) => {
    n3 && e2.onMount?.(n3), t2 && (n3 ? t2.mount(n3) : t2.unmount());
    let a2 = r2.current;
    if (typeof a2 == `function`) if (n3) {
      let e3 = a2(n3);
      typeof e3 == `function` && (i2.current = e3);
    } else i2.current ? (i2.current(), i2.current = null) : a2(n3);
    else a2 && (a2.current = n3);
  }, [t2]);
}
var zl = (0, O.createContext)({});
function Bl(e2) {
  return e2 && typeof e2 == `object` && Object.prototype.hasOwnProperty.call(e2, `current`);
}
function Vl(e2, t2, n2, r2, i2, a2) {
  let { visualElement: o2 } = (0, O.useContext)(vl), s2 = (0, O.useContext)(il), c2 = (0, O.useContext)(Oe), l2 = (0, O.useContext)(Kc), u2 = l2.reducedMotion, d2 = l2.skipAnimations, f2 = (0, O.useRef)(null), p2 = (0, O.useRef)(false);
  r2 ||= s2.renderer, !f2.current && r2 && (f2.current = r2(e2, { visualState: t2, parent: o2, props: n2, presenceContext: c2, blockInitialAnimation: c2 ? c2.initial === false : false, reducedMotionConfig: u2, skipAnimations: d2, isSVG: a2 }), p2.current && f2.current && (f2.current.manuallyAnimateOnMount = true));
  let m2 = f2.current, h2 = (0, O.useContext)(zl);
  m2 && !m2.projection && i2 && (m2.type === `html` || m2.type === `svg`) && Hl(f2.current, n2, i2, h2);
  let g2 = (0, O.useRef)(false);
  (0, O.useInsertionEffect)(() => {
    m2 && g2.current && m2.update(n2, c2);
  });
  let _2 = n2[Ni], v2 = (0, O.useRef)(!!_2 && typeof window < `u` && !window.MotionHandoffIsComplete?.(_2) && window.MotionHasOptimisedAnimation?.(_2));
  return L(() => {
    p2.current = true, m2 && (g2.current = true, window.MotionIsMounted = true, m2.updateFeatures(), m2.scheduleRenderMicrotask(), v2.current && m2.animationState && m2.animationState.animateChanges());
  }), (0, O.useEffect)(() => {
    m2 && (!v2.current && m2.animationState && m2.animationState.animateChanges(), v2.current &&= (queueMicrotask(() => {
      window.MotionHandoffMarkAsComplete?.(_2);
    }), false), m2.enteringChildren = void 0);
  }), m2;
}
function Hl(e2, t2, n2, r2) {
  let { layoutId: i2, layout: a2, drag: o2, dragConstraints: s2, layoutScroll: c2, layoutRoot: l2, layoutAnchor: u2, layoutCrossfade: d2 } = t2;
  e2.projection = new n2(e2.latestValues, t2[`data-framer-portal-id`] ? void 0 : Ul(e2.parent)), e2.projection.setOptions({ layoutId: i2, layout: a2, alwaysMeasureLayout: !!o2 || s2 && Bl(s2), visualElement: e2, animationType: typeof a2 == `string` ? a2 : `both`, initialPromotionConfig: r2, crossfade: d2, layoutScroll: c2, layoutRoot: l2, layoutAnchor: u2 });
}
function Ul(e2) {
  if (e2) return e2.options.allowProjection === false ? Ul(e2.parent) : e2.projection;
}
function Wl(e2, { forwardMotionProps: t2 = false, type: n2 } = {}, r2, i2) {
  r2 && ll(r2);
  let a2 = n2 ? n2 === `svg` : Al(e2), o2 = a2 ? Il : Fl;
  function s2(n3, s3) {
    let c3, l2 = { ...(0, O.useContext)(Kc), ...n3, layoutId: Gl(n3) }, { isStatic: u2 } = l2, d2 = bl(n3), f2 = o2(n3, u2);
    if (!u2 && typeof window < `u`) {
      Kl(l2, r2);
      let t3 = ql(l2);
      c3 = t3.MeasureLayout, d2.visualElement = Vl(e2, f2, l2, i2, t3.ProjectionNode, a2);
    }
    return (0, A.jsxs)(vl.Provider, { value: d2, children: [c3 && d2.visualElement ? (0, A.jsx)(c3, { visualElement: d2.visualElement, ...l2 }) : null, jl(e2, n3, Rl(f2, d2.visualElement, s3), f2, u2, t2, a2)] });
  }
  s2.displayName = `motion.${typeof e2 == `string` ? e2 : `create(${e2.displayName ?? e2.name ?? ``})`}`;
  let c2 = (0, O.forwardRef)(s2);
  return c2[Ll] = e2, c2;
}
function Gl({ layoutId: e2 }) {
  let t2 = (0, O.useContext)(Ee).id;
  return t2 && e2 !== void 0 ? t2 + `-` + e2 : e2;
}
function Kl(e2, t2) {
  (0, O.useContext)(il).strict;
}
function ql(e2) {
  let { drag: t2, layout: n2 } = cl();
  if (!t2 && !n2) return {};
  let r2 = { ...t2, ...n2 };
  return { MeasureLayout: t2?.isEnabled(e2) || n2?.isEnabled(e2) ? r2.MeasureLayout : void 0, ProjectionNode: r2.ProjectionNode };
}
function Jl(e2, t2) {
  if (typeof Proxy > `u`) return Wl;
  let n2 = /* @__PURE__ */ new Map(), r2 = (n3, r3) => Wl(n3, r3, e2, t2);
  return new Proxy((e3, t3) => r2(e3, t3), { get: (i2, a2) => a2 === `create` ? r2 : (n2.has(a2) || n2.set(a2, Wl(a2, void 0, e2, t2)), n2.get(a2)) });
}
var Yl = (e2, t2) => t2.isSVG ?? Al(e2) ? new ss(t2) : new Zo(t2, { allowProjection: e2 !== O.Fragment }), Xl = class extends _o {
  constructor(e2) {
    super(e2), e2.animationState ||= ms(e2);
  }
  updateAnimationControlsSubscription() {
    let { animate: e2 } = this.node.getProps();
    $a(e2) && (this.unmountControls = e2.subscribe(this.node));
  }
  mount() {
    this.updateAnimationControlsSubscription();
  }
  update() {
    let { animate: e2 } = this.node.getProps(), { animate: t2 } = this.node.prevProps || {};
    e2 !== t2 && this.updateAnimationControlsSubscription();
  }
  unmount() {
    this.node.animationState.reset(), this.unmountControls?.();
  }
}, Zl = 0, Ql = { animation: { Feature: Xl }, exit: { Feature: class extends _o {
  constructor() {
    super(...arguments), this.id = Zl++, this.isExitComplete = false;
  }
  update() {
    if (!this.node.presenceContext) return;
    let { isPresent: e2, onExitComplete: t2 } = this.node.presenceContext, { isPresent: n2 } = this.node.prevPresenceContext || {};
    if (!this.node.animationState || e2 === n2) return;
    if (e2 && n2 === false) {
      if (this.isExitComplete) {
        let { initial: e3, custom: t3 } = this.node.getProps();
        if (typeof e3 == `string` || typeof e3 == `object` && e3 && !Array.isArray(e3)) {
          let n3 = wi(this.node, e3, t3);
          if (n3) {
            let { transition: e4, transitionEnd: t4, ...r3 } = n3;
            for (let e5 in r3) this.node.getValue(e5)?.jump(r3[e5]);
          }
        }
        this.node.animationState.reset(), this.node.animationState.animateChanges();
      } else this.node.animationState.setActive(`exit`, false);
      this.isExitComplete = false;
      return;
    }
    let r2 = this.node.animationState.setActive(`exit`, !e2);
    t2 && !e2 && r2.then(() => {
      this.isExitComplete = true, t2(this.id);
    });
  }
  mount() {
    let { register: e2, onExitComplete: t2 } = this.node.presenceContext || {};
    t2 && t2(this.id), e2 && (this.unmount = e2(this.id));
  }
  unmount() {
  }
} } };
function $l(e2) {
  return { point: { x: e2.pageX, y: e2.pageY } };
}
var eu = (e2) => (t2) => va(t2) && e2(t2, $l(t2));
function tu(e2, t2, n2, r2) {
  return rc(e2, t2, eu(n2), r2);
}
var nu = ({ current: e2 }) => e2 ? e2.ownerDocument.defaultView : null, ru = (e2, t2) => Math.abs(e2 - t2);
function iu(e2, t2) {
  let n2 = ru(e2.x, t2.x), r2 = ru(e2.y, t2.y);
  return Math.sqrt(n2 ** 2 + r2 ** 2);
}
var au = /* @__PURE__ */ new Set([`auto`, `scroll`]), ou = class {
  constructor(e2, t2, { transformPagePoint: n2, contextWindow: r2 = window, dragSnapToOrigin: i2 = false, distanceThreshold: a2 = 3, element: o2 } = {}) {
    if (this.startEvent = null, this.lastMoveEvent = null, this.lastMoveEventInfo = null, this.lastRawMoveEventInfo = null, this.handlers = {}, this.contextWindow = window, this.scrollPositions = /* @__PURE__ */ new Map(), this.removeScrollListeners = null, this.onElementScroll = (e3) => {
      this.handleScroll(e3.target);
    }, this.onWindowScroll = () => {
      this.handleScroll(window);
    }, this.updatePoint = () => {
      if (!(this.lastMoveEvent && this.lastMoveEventInfo)) return;
      this.lastRawMoveEventInfo && (this.lastMoveEventInfo = su(this.lastRawMoveEventInfo, this.transformPagePoint));
      let e3 = lu(this.lastMoveEventInfo, this.history), t3 = this.startEvent !== null, n3 = iu(e3.offset, { x: 0, y: 0 }) >= this.distanceThreshold;
      if (!t3 && !n3) return;
      let { point: r3 } = e3, { timestamp: i3 } = G;
      this.history.push({ ...r3, timestamp: i3 });
      let { onStart: a3, onMove: o3 } = this.handlers;
      t3 || (a3 && a3(this.lastMoveEvent, e3), this.startEvent = this.lastMoveEvent), o3 && o3(this.lastMoveEvent, e3);
    }, this.handlePointerMove = (e3, t3) => {
      this.lastMoveEvent = e3, this.lastRawMoveEventInfo = t3, this.lastMoveEventInfo = su(t3, this.transformPagePoint), W.update(this.updatePoint, true);
    }, this.handlePointerUp = (e3, t3) => {
      this.end();
      let { onEnd: n3, onSessionEnd: r3, resumeAnimation: i3 } = this.handlers;
      if ((this.dragSnapToOrigin || !this.startEvent) && i3 && i3(), !(this.lastMoveEvent && this.lastMoveEventInfo)) return;
      let a3 = lu(e3.type === `pointercancel` ? this.lastMoveEventInfo : su(t3, this.transformPagePoint), this.history);
      this.startEvent && n3 && n3(e3, a3), r3 && r3(e3, a3);
    }, !va(e2)) return;
    this.dragSnapToOrigin = i2, this.handlers = t2, this.transformPagePoint = n2, this.distanceThreshold = a2, this.contextWindow = r2 || window;
    let s2 = su($l(e2), this.transformPagePoint), { point: c2 } = s2, { timestamp: l2 } = G;
    this.history = [{ ...c2, timestamp: l2 }];
    let { onSessionStart: u2 } = t2;
    u2 && u2(e2, lu(s2, this.history));
    let d2 = { passive: true, capture: true };
    this.removeListeners = Fe(tu(this.contextWindow, `pointermove`, this.handlePointerMove, d2), tu(this.contextWindow, `pointerup`, this.handlePointerUp, d2), tu(this.contextWindow, `pointercancel`, this.handlePointerUp, d2)), o2 && this.startScrollTracking(o2);
  }
  startScrollTracking(e2) {
    let t2 = e2.parentElement;
    for (; t2; ) {
      let e3 = getComputedStyle(t2);
      (au.has(e3.overflowX) || au.has(e3.overflowY)) && this.scrollPositions.set(t2, { x: t2.scrollLeft, y: t2.scrollTop }), t2 = t2.parentElement;
    }
    this.scrollPositions.set(window, { x: window.scrollX, y: window.scrollY }), window.addEventListener(`scroll`, this.onElementScroll, { capture: true }), window.addEventListener(`scroll`, this.onWindowScroll), this.removeScrollListeners = () => {
      window.removeEventListener(`scroll`, this.onElementScroll, { capture: true }), window.removeEventListener(`scroll`, this.onWindowScroll);
    };
  }
  handleScroll(e2) {
    let t2 = this.scrollPositions.get(e2);
    if (!t2) return;
    let n2 = e2 === window, r2 = n2 ? { x: window.scrollX, y: window.scrollY } : { x: e2.scrollLeft, y: e2.scrollTop }, i2 = { x: r2.x - t2.x, y: r2.y - t2.y };
    i2.x === 0 && i2.y === 0 || (n2 ? this.lastMoveEventInfo && (this.lastMoveEventInfo.point.x += i2.x, this.lastMoveEventInfo.point.y += i2.y) : this.history.length > 0 && (this.history[0].x -= i2.x, this.history[0].y -= i2.y), this.scrollPositions.set(e2, r2), W.update(this.updatePoint, true));
  }
  updateHandlers(e2) {
    this.handlers = e2;
  }
  end() {
    this.removeListeners && this.removeListeners(), this.removeScrollListeners && this.removeScrollListeners(), this.scrollPositions.clear(), ut(this.updatePoint);
  }
};
function su(e2, t2) {
  return t2 ? { point: t2(e2.point) } : e2;
}
function cu(e2, t2) {
  return { x: e2.x - t2.x, y: e2.y - t2.y };
}
function lu({ point: e2 }, t2) {
  return { point: e2, delta: cu(e2, du(t2)), offset: cu(e2, uu(t2)), velocity: fu(t2, 0.1) };
}
function uu(e2) {
  return e2[0];
}
function du(e2) {
  return e2[e2.length - 1];
}
function fu(e2, t2) {
  if (e2.length < 2) return { x: 0, y: 0 };
  let n2 = e2.length - 1, r2 = null, i2 = du(e2);
  for (; n2 >= 0 && (r2 = e2[n2], !(i2.timestamp - r2.timestamp > H(t2))); ) n2--;
  if (!r2) return { x: 0, y: 0 };
  r2 === e2[0] && e2.length > 2 && i2.timestamp - r2.timestamp > H(t2) * 2 && (r2 = e2[1]);
  let a2 = U(i2.timestamp - r2.timestamp);
  if (a2 === 0) return { x: 0, y: 0 };
  let o2 = { x: (i2.x - r2.x) / a2, y: (i2.y - r2.y) / a2 };
  return o2.x === 1 / 0 && (o2.x = 0), o2.y === 1 / 0 && (o2.y = 0), o2;
}
function pu(e2, { min: t2, max: n2 }, r2) {
  return t2 !== void 0 && e2 < t2 ? e2 = r2 ? Y(t2, e2, r2.min) : Math.max(e2, t2) : n2 !== void 0 && e2 > n2 && (e2 = r2 ? Y(n2, e2, r2.max) : Math.min(e2, n2)), e2;
}
function mu(e2, t2, n2) {
  return { min: t2 === void 0 ? void 0 : e2.min + t2, max: n2 === void 0 ? void 0 : e2.max + n2 - (e2.max - e2.min) };
}
function hu(e2, { top: t2, left: n2, bottom: r2, right: i2 }) {
  return { x: mu(e2.x, n2, i2), y: mu(e2.y, t2, r2) };
}
function gu(e2, t2) {
  let n2 = t2.min - e2.min, r2 = t2.max - e2.max;
  return t2.max - t2.min < e2.max - e2.min && ([n2, r2] = [r2, n2]), { min: n2, max: r2 };
}
function _u(e2, t2) {
  return { x: gu(e2.x, t2.x), y: gu(e2.y, t2.y) };
}
function vu(e2, t2) {
  let n2 = 0.5, r2 = $(e2), i2 = $(t2);
  return i2 > r2 ? n2 = Ie(t2.min, t2.max - r2, e2.min) : r2 > i2 && (n2 = Ie(e2.min, e2.max - i2, t2.min)), z(0, 1, n2);
}
function yu(e2, t2) {
  let n2 = {};
  return t2.min !== void 0 && (n2.min = t2.min - e2.min), t2.max !== void 0 && (n2.max = t2.max - e2.min), n2;
}
var bu = 0.35;
function xu(e2 = bu) {
  return e2 === false ? e2 = 0 : e2 === true && (e2 = bu), { x: Su(e2, `left`, `right`), y: Su(e2, `top`, `bottom`) };
}
function Su(e2, t2, n2) {
  return { min: Cu(e2, t2), max: Cu(e2, n2) };
}
function Cu(e2, t2) {
  return typeof e2 == `number` ? e2 : e2[t2] || 0;
}
var wu = /* @__PURE__ */ new WeakMap(), Tu = class {
  constructor(e2) {
    this.openDragLock = null, this.isDragging = false, this.currentDirection = null, this.originPoint = { x: 0, y: 0 }, this.constraints = false, this.hasMutatedConstraints = false, this.elastic = Q(), this.latestPointerEvent = null, this.latestPanInfo = null, this.visualElement = e2;
  }
  start(e2, { snapToCursor: t2 = false, distanceThreshold: n2 } = {}) {
    let { presenceContext: r2 } = this.visualElement;
    if (r2 && r2.isPresent === false) return;
    let i2 = (e3) => {
      t2 && this.snapToCursor($l(e3).point), this.stopAnimation();
    }, a2 = (e3, t3) => {
      let { drag: n3, dragPropagation: r3, onDragStart: i3 } = this.getProps();
      if (n3 && !r3 && (this.openDragLock && this.openDragLock(), this.openDragLock = pa(n3), !this.openDragLock)) return;
      this.latestPointerEvent = e3, this.latestPanInfo = t3, this.isDragging = true, this.currentDirection = null, this.resolveConstraints(), this.visualElement.projection && (this.visualElement.projection.isAnimationBlocked = true, this.visualElement.projection.target = void 0), Ks((e4) => {
        let t4 = this.getAxisMotionValue(e4).get() || 0;
        if (It.test(t4)) {
          let { projection: n4 } = this.visualElement;
          if (n4 && n4.layout) {
            let r4 = n4.layout.layoutBox[e4];
            r4 && (t4 = $(r4) * (parseFloat(t4) / 100));
          }
        }
        this.originPoint[e4] = t4;
      }), i3 && W.update(() => i3(e3, t3), false, true), ji(this.visualElement, `transform`);
      let { animationState: a3 } = this.visualElement;
      a3 && a3.setActive(`whileDrag`, true);
    }, o2 = (e3, t3) => {
      this.latestPointerEvent = e3, this.latestPanInfo = t3;
      let { dragPropagation: n3, dragDirectionLock: r3, onDirectionLock: i3, onDrag: a3 } = this.getProps();
      if (!n3 && !this.openDragLock) return;
      let { offset: o3 } = t3;
      if (r3 && this.currentDirection === null) {
        this.currentDirection = ku(o3), this.currentDirection !== null && i3 && i3(this.currentDirection);
        return;
      }
      this.updateAxis(`x`, t3.point, o3), this.updateAxis(`y`, t3.point, o3), this.visualElement.render(), a3 && W.update(() => a3(e3, t3), false, true);
    }, s2 = (e3, t3) => {
      this.latestPointerEvent = e3, this.latestPanInfo = t3, this.stop(e3, t3), this.latestPointerEvent = null, this.latestPanInfo = null;
    }, c2 = () => {
      let { dragSnapToOrigin: e3 } = this.getProps();
      (e3 || this.constraints) && this.startAnimation({ x: 0, y: 0 });
    }, { dragSnapToOrigin: l2 } = this.getProps();
    this.panSession = new ou(e2, { onSessionStart: i2, onStart: a2, onMove: o2, onSessionEnd: s2, resumeAnimation: c2 }, { transformPagePoint: this.visualElement.getTransformPagePoint(), dragSnapToOrigin: l2, distanceThreshold: n2, contextWindow: nu(this.visualElement), element: this.visualElement.current });
  }
  stop(e2, t2) {
    let n2 = e2 || this.latestPointerEvent, r2 = t2 || this.latestPanInfo, i2 = this.isDragging;
    if (this.cancel(), !i2 || !r2 || !n2) return;
    let { velocity: a2 } = r2;
    this.startAnimation(a2);
    let { onDragEnd: o2 } = this.getProps();
    o2 && W.postRender(() => o2(n2, r2));
  }
  cancel() {
    this.isDragging = false;
    let { projection: e2, animationState: t2 } = this.visualElement;
    e2 && (e2.isAnimationBlocked = false), this.endPanSession();
    let { dragPropagation: n2 } = this.getProps();
    !n2 && this.openDragLock && (this.openDragLock(), this.openDragLock = null), t2 && t2.setActive(`whileDrag`, false);
  }
  endPanSession() {
    this.panSession && this.panSession.end(), this.panSession = void 0;
  }
  updateAxis(e2, t2, n2) {
    let { drag: r2 } = this.getProps();
    if (!n2 || !Ou(e2, r2, this.currentDirection)) return;
    let i2 = this.getAxisMotionValue(e2), a2 = this.originPoint[e2] + n2[e2];
    this.constraints && this.constraints[e2] && (a2 = pu(a2, this.constraints[e2], this.elastic[e2])), i2.set(a2);
  }
  resolveConstraints() {
    let { dragConstraints: e2, dragElastic: t2 } = this.getProps(), n2 = this.visualElement.projection && !this.visualElement.projection.layout ? this.visualElement.projection.measure(false) : this.visualElement.projection?.layout, r2 = this.constraints;
    e2 && Bl(e2) ? this.constraints ||= this.resolveRefConstraints() : e2 && n2 ? this.constraints = hu(n2.layoutBox, e2) : this.constraints = false, this.elastic = xu(t2), r2 !== this.constraints && !Bl(e2) && n2 && this.constraints && !this.hasMutatedConstraints && Ks((e3) => {
      this.constraints !== false && this.getAxisMotionValue(e3) && (this.constraints[e3] = yu(n2.layoutBox[e3], this.constraints[e3]));
    });
  }
  resolveRefConstraints() {
    let { dragConstraints: e2, onMeasureDragConstraints: t2 } = this.getProps();
    if (!e2 || !Bl(e2)) return false;
    let n2 = e2.current, { projection: r2 } = this.visualElement;
    if (!r2 || !r2.layout) return false;
    r2.root && (r2.root.scroll = void 0, r2.root.updateScroll());
    let i2 = Ro(n2, r2.root, this.visualElement.getTransformPagePoint()), a2 = _u(r2.layout.layoutBox, i2);
    if (t2) {
      let e3 = t2(yo(a2));
      this.hasMutatedConstraints = !!e3, e3 && (a2 = vo(e3));
    }
    return a2;
  }
  startAnimation(e2) {
    let { drag: t2, dragMomentum: n2, dragElastic: r2, dragTransition: i2, dragSnapToOrigin: a2, onDragTransitionEnd: o2 } = this.getProps(), s2 = this.constraints || {}, c2 = Ks((o3) => {
      if (!Ou(o3, t2, this.currentDirection)) return;
      let c3 = s2 && s2[o3] || {};
      (a2 === true || a2 === o3) && (c3 = { min: 0, max: 0 });
      let l2 = r2 ? 200 : 1e6, u2 = r2 ? 40 : 1e7, d2 = { type: `inertia`, velocity: n2 ? e2[o3] : 0, bounceStiffness: l2, bounceDamping: u2, timeConstant: 750, restDelta: 1, restSpeed: 10, ...i2, ...c3 };
      return this.startAxisValueAnimation(o3, d2);
    });
    return Promise.all(c2).then(o2);
  }
  startAxisValueAnimation(e2, t2) {
    let n2 = this.getAxisMotionValue(e2);
    return ji(this.visualElement, e2), n2.start(vi(e2, n2, 0, t2, this.visualElement, false));
  }
  stopAnimation() {
    Ks((e2) => this.getAxisMotionValue(e2).stop());
  }
  getAxisMotionValue(e2) {
    let t2 = `_drag${e2.toUpperCase()}`;
    return this.visualElement.getProps()[t2] || this.visualElement.getValue(e2, this.visualElement.latestValues[e2] ?? 0);
  }
  snapToCursor(e2) {
    Ks((t2) => {
      let { drag: n2 } = this.getProps();
      if (!Ou(t2, n2, this.currentDirection)) return;
      let { projection: r2 } = this.visualElement, i2 = this.getAxisMotionValue(t2);
      if (r2 && r2.layout) {
        let { min: n3, max: a2 } = r2.layout.layoutBox[t2], o2 = i2.get() || 0;
        i2.set(e2[t2] - Y(n3, a2, 0.5) + o2);
      }
    });
  }
  scalePositionWithinConstraints() {
    if (!this.visualElement.current) return;
    let { drag: e2, dragConstraints: t2 } = this.getProps(), { projection: n2 } = this.visualElement;
    if (!Bl(t2) || !n2 || !this.constraints) return;
    this.stopAnimation();
    let r2 = { x: 0, y: 0 };
    Ks((e3) => {
      let t3 = this.getAxisMotionValue(e3);
      if (t3 && this.constraints !== false) {
        let n3 = t3.get();
        r2[e3] = vu({ min: n3, max: n3 }, this.constraints[e3]);
      }
    });
    let { transformTemplate: i2 } = this.visualElement.getProps();
    this.visualElement.current.style.transform = i2 ? i2({}, ``) : `none`, n2.root && n2.root.updateScroll(), n2.updateLayout(), this.constraints = false, this.resolveConstraints(), Ks((t3) => {
      if (!Ou(t3, e2, null)) return;
      let n3 = this.getAxisMotionValue(t3), { min: i3, max: a2 } = this.constraints[t3];
      n3.set(Y(i3, a2, r2[t3]));
    }), this.visualElement.render();
  }
  addListeners() {
    if (!this.visualElement.current) return;
    wu.set(this.visualElement, this);
    let e2 = this.visualElement.current, t2 = tu(e2, `pointerdown`, (t3) => {
      let { drag: n3, dragListener: r3 = true } = this.getProps(), i3 = t3.target, a3 = i3 !== e2 && Sa(i3);
      n3 && r3 && !a3 && this.start(t3);
    }), n2, r2 = () => {
      let { dragConstraints: t3 } = this.getProps();
      Bl(t3) && t3.current && (this.constraints = this.resolveRefConstraints(), n2 ||= Du(e2, t3.current, () => this.scalePositionWithinConstraints()));
    }, { projection: i2 } = this.visualElement, a2 = i2.addEventListener(`measure`, r2);
    i2 && !i2.layout && (i2.root && i2.root.updateScroll(), i2.updateLayout()), W.read(r2);
    let o2 = rc(window, `resize`, () => this.scalePositionWithinConstraints()), s2 = i2.addEventListener(`didUpdate`, (({ delta: e3, hasLayoutChanged: t3 }) => {
      this.isDragging && t3 && (Ks((t4) => {
        let n3 = this.getAxisMotionValue(t4);
        n3 && (this.originPoint[t4] += e3[t4].translate, n3.set(n3.get() + e3[t4].translate));
      }), this.visualElement.render());
    }));
    return () => {
      o2(), t2(), a2(), s2 && s2(), n2 && n2();
    };
  }
  getProps() {
    let e2 = this.visualElement.getProps(), { drag: t2 = false, dragDirectionLock: n2 = false, dragPropagation: r2 = false, dragConstraints: i2 = false, dragElastic: a2 = bu, dragMomentum: o2 = true } = e2;
    return { ...e2, drag: t2, dragDirectionLock: n2, dragPropagation: r2, dragConstraints: i2, dragElastic: a2, dragMomentum: o2 };
  }
};
function Eu(e2) {
  let t2 = true;
  return () => {
    if (t2) {
      t2 = false;
      return;
    }
    e2();
  };
}
function Du(e2, t2, n2) {
  let r2 = Wa(e2, Eu(n2)), i2 = Wa(t2, Eu(n2));
  return () => {
    r2(), i2();
  };
}
function Ou(e2, t2, n2) {
  return (t2 === true || t2 === e2) && (n2 === null || n2 === e2);
}
function ku(e2, t2 = 10) {
  let n2 = null;
  return Math.abs(e2.y) > t2 ? n2 = `y` : Math.abs(e2.x) > t2 && (n2 = `x`), n2;
}
var Au = class extends _o {
  constructor(e2) {
    super(e2), this.removeGroupControls = B, this.removeListeners = B, this.controls = new Tu(e2);
  }
  mount() {
    let { dragControls: e2 } = this.node.getProps();
    e2 && (this.removeGroupControls = e2.subscribe(this.controls)), this.removeListeners = this.controls.addListeners() || B;
  }
  update() {
    let { dragControls: e2 } = this.node.getProps(), { dragControls: t2 } = this.node.prevProps || {};
    e2 !== t2 && (this.removeGroupControls(), e2 && (this.removeGroupControls = e2.subscribe(this.controls)));
  }
  unmount() {
    this.removeGroupControls(), this.removeListeners(), this.controls.isDragging || this.controls.endPanSession();
  }
}, ju = (e2) => (t2, n2) => {
  e2 && W.update(() => e2(t2, n2), false, true);
}, Mu = class extends _o {
  constructor() {
    super(...arguments), this.removePointerDownListener = B;
  }
  onPointerDown(e2) {
    this.session = new ou(e2, this.createPanHandlers(), { transformPagePoint: this.node.getTransformPagePoint(), contextWindow: nu(this.node) });
  }
  createPanHandlers() {
    let { onPanSessionStart: e2, onPanStart: t2, onPan: n2, onPanEnd: r2 } = this.node.getProps();
    return { onSessionStart: ju(e2), onStart: ju(t2), onMove: ju(n2), onEnd: (e3, t3) => {
      delete this.session, r2 && W.postRender(() => r2(e3, t3));
    } };
  }
  mount() {
    this.removePointerDownListener = tu(this.node.current, `pointerdown`, (e2) => this.onPointerDown(e2));
  }
  update() {
    this.session && this.session.updateHandlers(this.createPanHandlers());
  }
  unmount() {
    this.removePointerDownListener(), this.session && this.session.end();
  }
}, Nu = false, Pu = class extends O.Component {
  componentDidMount() {
    let { visualElement: e2, layoutGroup: t2, switchLayoutGroup: n2, layoutId: r2 } = this.props, { projection: i2 } = e2;
    i2 && (t2.group && t2.group.add(i2), n2 && n2.register && r2 && n2.register(i2), Nu && i2.root.didUpdate(), i2.addEventListener(`animationComplete`, () => {
      this.safeToRemove();
    }), i2.setOptions({ ...i2.options, layoutDependency: this.props.layoutDependency, onExitComplete: () => this.safeToRemove() })), lc.hasEverUpdated = true;
  }
  getSnapshotBeforeUpdate(e2) {
    let { layoutDependency: t2, visualElement: n2, drag: r2, isPresent: i2 } = this.props, { projection: a2 } = n2;
    return a2 ? (a2.isPresent = i2, e2.layoutDependency !== t2 && a2.setOptions({ ...a2.options, layoutDependency: t2 }), Nu = true, r2 || e2.layoutDependency !== t2 || t2 === void 0 || e2.isPresent !== i2 ? a2.willUpdate() : this.safeToRemove(), e2.isPresent !== i2 && (i2 ? a2.promote() : a2.relegate() || W.postRender(() => {
      let e3 = a2.getStack();
      (!e3 || !e3.members.length) && this.safeToRemove();
    })), null) : null;
  }
  componentDidUpdate() {
    let { visualElement: e2, layoutAnchor: t2 } = this.props, { projection: n2 } = e2;
    n2 && (n2.options.layoutAnchor = t2, n2.root.didUpdate(), la.postRender(() => {
      !n2.currentAnimation && n2.isLead() && this.safeToRemove();
    }));
  }
  componentWillUnmount() {
    let { visualElement: e2, layoutGroup: t2, switchLayoutGroup: n2 } = this.props, { projection: r2 } = e2;
    Nu = true, r2 && (r2.scheduleCheckAfterUnmount(), t2 && t2.group && t2.group.remove(r2), n2 && n2.deregister && n2.deregister(r2));
  }
  safeToRemove() {
    let { safeToRemove: e2 } = this.props;
    e2 && e2();
  }
  render() {
    return null;
  }
};
function Fu(e2) {
  let [t2, n2] = el(), r2 = (0, O.useContext)(Ee);
  return (0, A.jsx)(Pu, { ...e2, layoutGroup: r2, switchLayoutGroup: (0, O.useContext)(zl), isPresent: t2, safeToRemove: n2 });
}
var Iu = { pan: { Feature: Mu }, drag: { Feature: Au, ProjectionNode: Gc, MeasureLayout: Fu } };
function Lu(e2, t2, n2) {
  let { props: r2 } = e2;
  e2.animationState && r2.whileHover && e2.animationState.setActive(`whileHover`, n2 === `Start`);
  let i2 = r2[`onHover` + n2];
  i2 && W.postRender(() => i2(t2, $l(t2)));
}
var Ru = class extends _o {
  mount() {
    let { current: e2 } = this.node;
    e2 && (this.unmount = ga(e2, (e3, t2) => (Lu(this.node, t2, `Start`), (e4) => Lu(this.node, e4, `End`))));
  }
  unmount() {
  }
}, zu = class extends _o {
  constructor() {
    super(...arguments), this.isActive = false;
  }
  onFocus() {
    let e2 = false;
    try {
      e2 = this.node.current.matches(`:focus-visible`);
    } catch {
      e2 = true;
    }
    !e2 || !this.node.animationState || (this.node.animationState.setActive(`whileFocus`, true), this.isActive = true);
  }
  onBlur() {
    !this.isActive || !this.node.animationState || (this.node.animationState.setActive(`whileFocus`, false), this.isActive = false);
  }
  mount() {
    this.unmount = Fe(rc(this.node.current, `focus`, () => this.onFocus()), rc(this.node.current, `blur`, () => this.onBlur()));
  }
  unmount() {
  }
};
function Bu(e2, t2, n2) {
  let { props: r2 } = e2;
  if (e2.current instanceof HTMLButtonElement && e2.current.disabled) return;
  e2.animationState && r2.whileTap && e2.animationState.setActive(`whileTap`, n2 === `Start`);
  let i2 = r2[`onTap` + (n2 === `End` ? `` : n2)];
  i2 && W.postRender(() => i2(t2, $l(t2)));
}
var Vu = class extends _o {
  mount() {
    let { current: e2 } = this.node;
    if (!e2) return;
    let { globalTapTarget: t2, propagate: n2 } = this.node.props;
    this.unmount = ka(e2, (e3, t3) => (Bu(this.node, t3, `Start`), (e4, { success: t4 }) => Bu(this.node, e4, t4 ? `End` : `Cancel`)), { useGlobalTarget: t2, stopPropagation: n2?.tap === false });
  }
  unmount() {
  }
}, Hu = /* @__PURE__ */ new WeakMap(), Uu = /* @__PURE__ */ new WeakMap(), Wu = (e2) => {
  let t2 = Hu.get(e2.target);
  t2 && t2(e2);
}, Gu = (e2) => {
  e2.forEach(Wu);
};
function Ku({ root: e2, ...t2 }) {
  let n2 = e2 || document;
  Uu.has(n2) || Uu.set(n2, {});
  let r2 = Uu.get(n2), i2 = JSON.stringify(t2);
  return r2[i2] || (r2[i2] = new IntersectionObserver(Gu, { root: e2, ...t2 })), r2[i2];
}
function qu(e2, t2, n2) {
  let r2 = Ku(t2);
  return Hu.set(e2, n2), r2.observe(e2), () => {
    Hu.delete(e2), r2.unobserve(e2);
  };
}
var Ju = { some: 0, all: 1 }, Yu = class extends _o {
  constructor() {
    super(...arguments), this.hasEnteredView = false, this.isInView = false;
  }
  startObserver() {
    this.stopObserver?.();
    let { viewport: e2 = {} } = this.node.getProps(), { root: t2, margin: n2, amount: r2 = `some`, once: i2 } = e2, a2 = { root: t2 ? t2.current : void 0, rootMargin: n2, threshold: typeof r2 == `number` ? r2 : Ju[r2] }, o2 = (e3) => {
      let { isIntersecting: t3 } = e3;
      if (this.isInView === t3 || (this.isInView = t3, i2 && !t3 && this.hasEnteredView)) return;
      t3 && (this.hasEnteredView = true), this.node.animationState && this.node.animationState.setActive(`whileInView`, t3);
      let { onViewportEnter: n3, onViewportLeave: r3 } = this.node.getProps(), a3 = t3 ? n3 : r3;
      a3 && a3(e3);
    };
    this.stopObserver = qu(this.node.current, a2, o2);
  }
  mount() {
    this.startObserver();
  }
  update() {
    if (typeof IntersectionObserver > `u`) return;
    let { props: e2, prevProps: t2 } = this.node;
    [`amount`, `margin`, `root`].some(Xu(e2, t2)) && this.startObserver();
  }
  unmount() {
    this.stopObserver?.(), this.hasEnteredView = false, this.isInView = false;
  }
};
function Xu({ viewport: e2 = {} }, { viewport: t2 = {} } = {}) {
  return (n2) => e2[n2] !== t2[n2];
}
var Zu = { inView: { Feature: Yu }, tap: { Feature: Vu }, focus: { Feature: zu }, hover: { Feature: Ru } }, Qu = { layout: { ProjectionNode: Gc, MeasureLayout: Fu } }, $u = Jl({ ...Ql, ...Zu, ...Iu, ...Qu }, Yl);
function ed() {
  !so.current && lo();
  let [e2] = (0, O.useState)(oo.current);
  return e2;
}
var td = $u, nd = [1, 2, 3, 4], rd = 4, id = Array.from({ length: rd }, (e2, t2) => `equipment-preset-fixed-${t2 + 1}`);
function ad(e2) {
  return structuredClone(e2);
}
function od(e2) {
  return [...e2].filter((e3) => e3.index >= 1 && e3.index <= 4).sort((e3, t2) => e3.index - t2.index).map((e3) => ({ squadIndex: e3.index, positions: e3.heroes.slice(0, 5).map((e4, t2) => ({ position: t2 + 1, equips: e4.equips.map((e5) => ({ slot: e5.slot, equipUuid: e5.uuid, name: e5.name, level: e5.level, quality: e5.quality, iconPath: e5.iconPath, promote: e5.promote, heroType: e5.heroType })) })) }));
}
function sd(e2, t2) {
  return e2.find((e3) => e3.squadIndex === t2);
}
function cd(e2, t2, n2) {
  let r2 = sd(e2, t2);
  r2 || (r2 = { squadIndex: t2, positions: [] }, e2.push(r2));
  let i2 = r2.positions.find((e3) => e3.position === n2);
  return i2 || (i2 = { position: n2, equips: [] }, r2.positions.push(i2), r2.positions.sort((e3, t3) => e3.position - t3.position)), i2;
}
function ld(e2) {
  return e2.squads.reduce((e3, t2) => e3 + t2.positions.reduce((e4, t3) => e4 + t3.equips.length, 0), 0);
}
function ud(e2) {
  return e2.squads.reduce((e3, t2) => e3 + t2.positions.filter((e4) => e4.equips.length > 0).length, 0);
}
function dd(e2, t2) {
  return !e2 || !t2.heroes.length ? false : t2.heroes.slice(0, 5).every((t3, n2) => {
    let r2 = e2.positions.find((e3) => e3.position === n2 + 1);
    if (!r2) return false;
    let i2 = new Map(t3.equips.map((e3) => [e3.slot, String(e3.uuid)])), a2 = new Map(r2.equips.map((e3) => [e3.slot, String(e3.equipUuid)]));
    return nd.every((e3) => (i2.get(e3) || ``) === (a2.get(e3) || ``));
  });
}
function fd({ online: t2, squads: n2, reloadSquads: i2, onError: a2 }) {
  let { t: o2 } = d(), s2 = ed(), l2 = c(), u2 = e(`equipment`, null, { read: () => r(l2), write: async (e2) => {
    if (!e2) throw Error(`CONFIG_DRAFT_INVALID`);
    return E(e2, l2);
  }, valid: (e2) => e2 !== null }, l2), f2 = u2.draft, p2 = (e2) => u2.state.edit(e2, false), [m2, g2] = (0, O.useState)(``), [_2, v2] = (0, O.useState)(``), [b2, x2] = (0, O.useState)(null), [S2, C2] = (0, O.useState)(null), [w2, T2] = (0, O.useState)(null), [te2, D2] = (0, O.useState)(``), [re2, ie2] = (0, O.useState)([]), [oe2, se2] = (0, O.useState)(``), [ce2, k2] = (0, O.useState)(false), [le2, ue2] = (0, O.useState)(``), j2 = (f2?.equipmentPresets ?? []).filter((e2) => JSON.stringify(e2) !== JSON.stringify(u2.confirmed?.equipmentPresets.find((t3) => t3.id === e2.id))).map((e2) => e2.id), M2 = (0, O.useRef)(false), de2 = (0, O.useRef)(``), N2 = (0, O.useMemo)(() => f2?.equipmentPresets.find((e2) => e2.id === m2) || f2?.equipmentPresets[0], [f2, m2]), fe2 = !!(N2 && j2.includes(N2.id)), pe2 = (0, O.useMemo)(() => [...new Set(n2.map((e2) => e2.index))].filter((e2) => e2 >= 1 && e2 <= 4).sort((e2, t3) => e2 - t3), [n2]), me2 = (0, O.useMemo)(() => {
    let e2 = /* @__PURE__ */ new Map(), t3 = N2 ? [N2, ...f2?.equipmentPresets.filter((e3) => e3.id !== N2.id) || []] : f2?.equipmentPresets || [];
    for (let r2 of n2) {
      let n3 = t3.find((e3) => dd(sd(e3.squads, r2.index), r2));
      n3 && e2.set(r2.index, n3);
    }
    return e2;
  }, [f2?.equipmentPresets, N2, n2]), he2 = (0, O.useMemo)(() => {
    let e2 = pe2.map((e3) => me2.get(e3)).filter((e3) => !!e3);
    return e2.length ? e2.length !== pe2.length || new Set(e2.map((e3) => e3.id)).size > 1 ? o2(`squad.mixedEquipmentPreset`) : e2[0]?.name || o2(`squad.unmatchedEquipmentPreset`) : o2(`squad.unmatchedEquipmentPreset`);
  }, [me2, o2, pe2]), ge2 = (0, O.useMemo)(() => {
    let e2 = /* @__PURE__ */ new Map();
    for (let t3 of f2?.initialEquipmentConfig?.squads || []) for (let n3 of t3.positions) for (let t4 of n3.equips) e2.set(t4.equipUuid, t4);
    for (let t3 of n2) for (let n3 of t3.heroes) for (let t4 of n3.equips) e2.set(t4.uuid, { slot: t4.slot, equipUuid: t4.uuid, name: t4.name, level: t4.level, quality: t4.quality, iconPath: t4.iconPath, promote: t4.promote, heroType: t4.heroType });
    return e2;
  }, [f2?.initialEquipmentConfig, n2]);
  (0, O.useEffect)(() => {
    u2.state.refresh().then(() => {
      let e2 = u2.state.getSnapshot().draft;
      g2((t3) => t3 || e2?.equipmentPresets[0]?.id || ``);
    }).catch(() => a2(o2(`common.actionFailed`)));
  }, [a2, o2]), (0, O.useEffect)(() => {
    if (!f2 || !t2 || !n2.length || j2.length || M2.current) return;
    let e2 = id.every((e3, t3) => f2.equipmentPresets[t3]?.id === e3), r2 = e2 && f2.equipmentPresets.length === rd, i3 = od(n2), a3;
    if (!e2) a3 = id.map((e3, t3) => ({ id: e3, name: o2(`squad.presetDefaultName`, { number: t3 + 1 }), squads: ad(i3) }));
    else {
      let e3 = !r2;
      if (a3 = f2.equipmentPresets.slice(0, rd).map((t3) => {
        let n3 = false, r3 = ad(t3.squads);
        for (let t4 of i3) {
          let i4 = sd(r3, t4.squadIndex);
          i4?.positions.length || (e3 = true, n3 = true, i4 ? i4.positions = structuredClone(t4.positions) : r3.push(structuredClone(t4)));
        }
        return n3 ? { ...t3, squads: r3 } : t3;
      }), !e3) return;
    }
    M2.current = true;
    let s3 = { ...f2, initialEquipmentConfig: { capturedAt: Date.now(), squads: ad(i3) }, equipmentPresets: a3 };
    u2.state.edit(s3, false), u2.state.flush(false).then(() => {
      g2((e3) => id.includes(e3) ? e3 : id[0]);
    }).catch(() => void 0).finally(() => {
      M2.current = false;
    });
  }, [f2, j2.length, a2, t2, n2, o2]), (0, O.useEffect)(() => {
    if (!oe2) return;
    let e2 = window.setTimeout(() => se2(``), 1800);
    return () => window.clearTimeout(e2);
  }, [oe2]), (0, O.useEffect)(() => h(`bridge://equipment-apply-progress`, (e2) => {
    e2.operationId === de2.current && C2(e2);
  }), []), (0, O.useEffect)(() => {
    let e2 = (e3) => {
      if (!e3.altKey || e3.repeat || !/^[1-4]$/.test(e3.key) || !f2 || _2) return;
      let t3 = e3.target;
      if (t3?.isContentEditable || [`INPUT`, `SELECT`, `TEXTAREA`].includes(t3?.tagName || ``)) return;
      let n3 = f2.equipmentPresets[Number(e3.key) - 1];
      n3 && (e3.preventDefault(), xe2(n3.id));
    };
    return window.addEventListener(`keydown`, e2), () => window.removeEventListener(`keydown`, e2);
  }, [_2, f2, t2]);
  async function P2(e2) {
    u2.state.edit(e2, false);
    try {
      return await u2.state.flush(false);
    } catch {
      return null;
    }
  }
  function _e2() {
    !N2 || _2 || (ue2(N2.name), k2(true));
  }
  function ve2() {
    k2(false), ue2(``);
  }
  async function ye2() {
    let e2 = le2.trim();
    if (!(!f2 || !e2 || _2 || !ce2)) {
      if (!N2 || e2 === N2.name && !j2.includes(N2.id)) {
        ve2();
        return;
      }
      v2(`rename`);
      try {
        await P2({ ...f2, equipmentPresets: f2.equipmentPresets.map((t3) => t3.id === N2.id ? { ...t3, name: e2 } : t3) }) && ve2();
      } finally {
        v2(``);
      }
    }
  }
  function be2() {
    if (!f2 || !N2 || !n2.length || _2) return;
    let e2 = structuredClone(f2), t3 = e2.equipmentPresets.find((e3) => e3.id === N2.id);
    t3 && (t3.squads = od(n2), p2(e2), se2(o2(`squad.currentEquipmentLoaded`)));
  }
  async function F2(e2, n3, r2) {
    if (!f2 || _2 || n3 && !t2) return;
    v2(`${n3 ? `apply` : `save`}-${r2 || `all`}`);
    let a3 = n3 ? `equipment-${Date.now()}-${Math.random().toString(36).slice(2, 8)}` : ``;
    a3 && (de2.current = a3, C2({ operationId: a3, presetId: e2, squadIndex: r2 ?? null, phase: `preparing`, current: 0, total: 0 }));
    try {
      if (!await P2(f2)) return;
      if (!n3) {
        se2(o2(`squad.equipmentConfigSaved`));
        return;
      }
      let t3 = await ee(e2, r2, a3, l2);
      x2(t3), se2(we2(t3)), c() === l2 && await i2();
    } catch (e3) {
      let t3 = { state: `rejected`, applied: 0, requested: 0, reason: String(e3) };
      x2(t3), se2(we2(t3));
    } finally {
      a3 && de2.current === a3 && (de2.current = ``, C2(null)), v2(``);
    }
  }
  async function xe2(e2, t3) {
    await F2(e2, true, t3);
  }
  function Se2(e2) {
    if (!f2 || !N2 || w2?.kind !== `squad` || w2.squadIndex === e2 || _2) return;
    let t3 = structuredClone(f2), r2 = t3.equipmentPresets.find((e3) => e3.id === N2.id);
    if (!r2) return;
    let i3 = sd(r2.squads, w2.squadIndex), a3 = sd(r2.squads, e2), o3 = n2.find((e3) => e3.index === w2.squadIndex)?.heroes.length || 0, s3 = n2.find((t4) => t4.index === e2)?.heroes.length || 0, c2 = Math.min(o3, s3, 5);
    if (!i3 || !a3 || !c2) return;
    let l3 = [];
    for (let e3 = 1; e3 <= c2; e3 += 1) {
      let t4 = cd(r2.squads, i3.squadIndex, e3), n3 = cd(r2.squads, a3.squadIndex, e3), o4 = t4.equips;
      t4.equips = n3.equips, n3.equips = o4, l3.push(`${i3.squadIndex}-${e3}`, `${a3.squadIndex}-${e3}`);
    }
    ie2(l3), window.setTimeout(() => ie2([]), 450), p2(t3);
  }
  async function Ce2(e2) {
    if (!f2 || !N2 || !w2 || w2.kind === `squad` || _2) return;
    let t3 = `${w2.squadIndex}-${w2.position}`, n3 = `${e2.squadIndex}-${e2.position}`;
    if (w2.kind !== e2.kind || t3 === n3) return;
    if (w2.kind === `equip` && e2.kind === `equip` && w2.slot !== e2.slot) {
      se2(o2(`squad.sameSlotRequired`));
      return;
    }
    let r2 = structuredClone(f2), i3 = r2.equipmentPresets.find((e3) => e3.id === N2.id);
    if (!i3) return;
    let a3 = cd(i3.squads, w2.squadIndex, w2.position), s3 = cd(i3.squads, e2.squadIndex, e2.position);
    if (w2.kind === `loadout`) {
      let e3 = a3.equips;
      a3.equips = s3.equips, s3.equips = e3;
    } else {
      if (e2.kind !== `equip`) return;
      let t4 = Number(w2.slot), n4 = a3.equips.findIndex((e3) => e3.slot === t4);
      if (n4 < 0) return;
      let r3 = s3.equips.findIndex((e3) => e3.slot === t4), i4 = a3.equips[n4], o3 = r3 >= 0 ? s3.equips[r3] : null;
      o3 ? a3.equips[n4] = o3 : a3.equips.splice(n4, 1), r3 >= 0 ? s3.equips[r3] = i4 : s3.equips.push(i4), a3.equips.sort((e3, t5) => e3.slot - t5.slot), s3.equips.sort((e3, t5) => e3.slot - t5.slot);
    }
    ie2([t3, n3]), window.setTimeout(() => ie2([]), 450), p2(r2);
  }
  function we2(e2) {
    let t3 = e2.state === `success` ? `squad.equipmentApplySuccess` : e2.state === `partial` ? `squad.equipmentApplyPartial` : `squad.equipmentApplyRejected`;
    return o2(t3, { applied: e2.applied, requested: e2.requested, hero: e2.failedHeroName || `-`, reason: e2.reason || `-` });
  }
  function Te2() {
    return S2 ? S2.phase === `preparing` ? o2(`squad.equipmentApplyPreparing`) : S2.phase === `verifying` ? o2(`squad.equipmentApplyVerifying`, { current: S2.current, total: S2.total }) : o2(`squad.equipmentApplyRunning`, { current: S2.current, total: S2.total, hero: S2.heroName || `-` }) : ``;
  }
  function I2(e2, t3) {
    return e2?.positions.find((e3) => e3.position === t3) || { position: t3, equips: [] };
  }
  function Ee2(e2, t3, n3, r2) {
    let i3 = e2 ? { ...ge2.get(e2.equipUuid), ...e2 } : void 0, a3 = `equip-${t3}-${n3}-${r2}`;
    return (0, A.jsx)(td.div, { className: `preset-equipment-slot quality-${i3?.quality || 0} ${te2 === a3 ? `drop-target` : ``}`, layout: true, layoutId: e2 ? `preset-equip-${N2?.id}-${e2.equipUuid}` : void 0, draggable: !!e2 && !_2, onDragStart: (e3) => {
      e3.stopPropagation(), T2({ kind: `equip`, squadIndex: t3, position: n3, slot: r2 });
    }, onDragOver: (e3) => {
      w2?.kind !== `equip` || w2.slot !== r2 || (e3.preventDefault(), e3.stopPropagation(), D2(a3));
    }, onDragLeave: (e3) => {
      w2?.kind !== `equip` || w2.slot !== r2 || (e3.stopPropagation(), D2((e4) => e4 === a3 ? `` : e4));
    }, onDrop: (e3) => {
      w2?.kind !== `equip` || w2.slot !== r2 || (e3.preventDefault(), e3.stopPropagation(), D2(``), Ce2({ kind: `equip`, squadIndex: t3, position: n3, slot: r2 }));
    }, onDragEnd: () => {
      T2(null), D2(``);
    }, whileHover: s2 ? void 0 : { y: -3, scale: 1.06 }, whileTap: s2 ? void 0 : { scale: 0.97 }, title: i3?.name || o2(`squad.equipmentSlot${r2}`), children: i3 ? (0, A.jsxs)(A.Fragment, { children: [(0, A.jsx)(ae, { assetPath: i3.iconPath, alt: i3.name || String(i3.equipUuid), className: `equipment-icon` }), (0, A.jsxs)(`span`, { children: [`Lv.`, i3.level ?? `-`, i3.promote ? ` +${i3.promote}` : ``] })] }) : (0, A.jsxs)(A.Fragment, { children: [(0, A.jsx)(`span`, { className: `equipment-icon game-asset-placeholder` }), (0, A.jsx)(`span`, { children: o2(`squad.emptyEquipment`) })] }) }, r2);
  }
  return f2 ? (0, A.jsxs)(`div`, { className: `equipment-preset-layout`, children: [!ce2 && (0, A.jsx)(ne, { state: u2.state, label: o2(`squad.equipmentPresets`) }), (0, A.jsxs)(`aside`, { className: `equipment-preset-rail`, children: [(0, A.jsx)(`strong`, { children: o2(`squad.equipmentPresets`) }), (0, A.jsxs)(`div`, { className: `equipment-preset-list`, children: [f2.equipmentPresets.slice(0, rd).map((e2, t3) => (0, A.jsxs)(`button`, { className: e2.id === N2?.id ? `active` : ``, onClick: () => g2(e2.id), children: [(0, A.jsxs)(`span`, { children: [e2.name, j2.includes(e2.id) ? ` *` : ``] }), (0, A.jsxs)(`small`, { children: [t3 < 4 ? `Alt+${t3 + 1} \xB7 ` : ``, o2(`squad.presetSummary`, { positions: ud(e2), equips: ld(e2) })] })] }, e2.id)), !f2.equipmentPresets.length && (0, A.jsx)(`span`, { className: `muted`, children: o2(`squad.noEquipmentPresets`) })] })] }), (0, A.jsxs)(`main`, { className: `equipment-preset-main`, children: [(0, A.jsxs)(`div`, { className: `equipment-preset-toolbar`, children: [(0, A.jsxs)(`div`, { children: [(0, A.jsx)(`strong`, { children: N2?.name || o2(`squad.noEquipmentPresets`) }), N2 && (0, A.jsxs)(`span`, { children: [o2(`squad.allSquadPresetSummary`, { positions: ud(N2), equips: ld(N2) }), fe2 ? ` \xB7 ${o2(`squad.equipmentConfigUnsaved`)}` : ``] }), (0, A.jsx)(`span`, { className: `equipment-current-config`, children: o2(`squad.currentEquipmentPreset`, { name: he2 }) })] }), (0, A.jsxs)(`div`, { className: `equipment-preset-actions`, children: [(0, A.jsx)(`button`, { onClick: _e2, disabled: !N2 || !!_2, children: o2(`common.rename`) }), (0, A.jsx)(`button`, { onClick: be2, disabled: !N2 || !t2 || !!_2, children: o2(`squad.loadCurrentEquipment`) }), (0, A.jsx)(`button`, { onClick: () => N2 && void F2(N2.id, false), disabled: !N2 || !!_2, children: o2(`squad.saveEquipmentConfig`) }), (0, A.jsx)(`button`, { className: `primary`, onClick: () => N2 && void xe2(N2.id), disabled: !N2 || !t2 || !!_2, children: o2(_2 === `apply-all` ? `squad.equipmentApplying` : `squad.saveAndApplyEquipmentConfig`) })] })] }), (0, A.jsx)(rl, { mode: `wait`, children: N2 ? (0, A.jsxs)(td.div, { className: `equipment-preset-squads`, initial: s2 ? false : { opacity: 0, y: 8 }, animate: { opacity: 1, y: 0 }, exit: s2 ? void 0 : { opacity: 0, y: -6 }, transition: { duration: 0.18 }, children: [pe2.map((e2, r2) => {
    let i3 = sd(N2.squads, e2), a3 = n2.find((t3) => t3.index === e2), c2 = `squad-${e2}`;
    return (0, A.jsxs)(td.section, { className: `equipment-preset-squad ${te2 === c2 ? `drop-target` : ``}`, initial: s2 ? false : { opacity: 0, y: 10 }, animate: { opacity: 1, y: 0 }, transition: { delay: r2 * 0.05, duration: 0.2 }, onDragOver: (e3) => {
      w2?.kind === `squad` && (e3.preventDefault(), D2(c2));
    }, onDragLeave: (e3) => {
      e3.currentTarget.contains(e3.relatedTarget) || D2((e4) => e4 === c2 ? `` : e4);
    }, onDrop: (t3) => {
      w2?.kind === `squad` && (t3.preventDefault(), D2(``), Se2(e2));
    }, children: [(0, A.jsxs)(`div`, { className: `equipment-preset-squad-header`, children: [(0, A.jsxs)(`div`, { children: [(0, A.jsx)(`strong`, { children: o2(`squad.number`, { number: e2 }) }), (0, A.jsx)(`span`, { children: o2(`squad.positionEquipmentCount`, { positions: i3?.positions.filter((e3) => e3.equips.length > 0).length || 0, equips: i3?.positions.reduce((e3, t3) => e3 + t3.equips.length, 0) || 0 }) }), (0, A.jsx)(`span`, { className: `equipment-current-config`, children: o2(`squad.currentEquipmentPreset`, { name: me2.get(e2)?.name || o2(`squad.unmatchedEquipmentPreset`) }) })] }), (0, A.jsxs)(`div`, { className: `equipment-preset-actions`, children: [(0, A.jsx)(`div`, { className: `equipment-squad-drag-handle`, draggable: !_2, onDragStart: () => T2({ kind: `squad`, squadIndex: e2 }), onDragEnd: () => {
      T2(null), D2(``);
    }, children: o2(`squad.dragSquadLoadout`) }), (0, A.jsx)(`button`, { onClick: () => void xe2(N2.id, e2), disabled: !t2 || !!_2, children: _2 === `apply-${e2}` ? o2(`squad.equipmentApplying`) : o2(`squad.applySquad`) })] })] }), (0, A.jsx)(`div`, { className: `equipment-preset-positions`, children: (a3?.heroes || []).slice(0, 5).map((t3, n3) => {
      let r3 = n3 + 1, a4 = I2(i3, r3), c3 = `${e2}-${r3}`, l3 = `loadout-${c3}`;
      return (0, A.jsxs)(td.article, { className: `equipment-position-card ${te2 === l3 ? `drop-target` : ``} ${re2.includes(c3) ? `drop-success` : ``}`, layout: true, whileHover: s2 ? void 0 : { y: -2 }, onDragOver: (e3) => {
        w2?.kind === `loadout` && (e3.preventDefault(), D2(l3));
      }, onDragLeave: () => D2((e3) => e3 === l3 ? `` : e3), onDrop: (t4) => {
        w2?.kind === `loadout` && (t4.preventDefault(), D2(``), Ce2({ kind: `loadout`, squadIndex: e2, position: r3 }));
      }, children: [(0, A.jsxs)(`div`, { className: `equipment-position-header`, children: [(0, A.jsx)(`strong`, { children: o2(`squad.position`, { number: r3 }) }), (0, A.jsx)(`span`, { children: o2(`squad.heroFixed`) })] }), (0, A.jsxs)(`div`, { className: `equipment-position-hero`, children: [(0, A.jsx)(ae, { assetPath: t3.iconPath, alt: t3.name, className: `equipment-position-hero-icon` }), (0, A.jsxs)(`div`, { children: [(0, A.jsx)(`strong`, { children: t3.name }), (0, A.jsxs)(`span`, { children: [`Lv.`, t3.level] })] })] }), (0, A.jsx)(`div`, { className: `equipment-loadout-handle`, draggable: !_2, onDragStart: () => T2({ kind: `loadout`, squadIndex: e2, position: r3 }), onDragEnd: () => {
        T2(null), D2(``);
      }, children: (0, A.jsx)(`span`, { children: o2(`squad.dragLoadout`) }) }), (0, A.jsx)(`div`, { className: `equipment-position-items`, children: nd.map((t4) => Ee2(a4.equips.find((e3) => e3.slot === t4), e2, r3, t4)) })] }, r3);
    }) })] }, e2);
  }), !pe2.length && (0, A.jsx)(`div`, { className: `map-empty`, children: o2(`squad.empty`) })] }, N2.id) : (0, A.jsx)(`div`, { className: `map-empty`, children: o2(`squad.createFirstPreset`) }) }), (0, A.jsxs)(`div`, { className: `equipment-preset-hint`, children: [(0, A.jsx)(`span`, { children: o2(`squad.dragEquipmentHint`) }), (0, A.jsx)(`strong`, { children: o2(`squad.quickShortcutHint`) })] }), b2 && (0, A.jsx)(`div`, { className: `equipment-result equipment-result-${b2.state}`, children: we2(b2) })] }), (0, A.jsxs)(rl, { children: [ce2 && (0, A.jsx)(y, { className: `equipment-preset-dialog-backdrop`, labelledBy: `equipment-preset-title`, busy: !!_2, onClose: ve2, children: (0, A.jsxs)(td.div, { className: `equipment-preset-dialog`, initial: s2 ? false : { opacity: 0, y: 8, scale: 0.98 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: s2 ? void 0 : { opacity: 0, y: 6, scale: 0.98 }, children: [(0, A.jsx)(ne, { state: u2.state, label: o2(`squad.equipmentPresets`) }), (0, A.jsx)(`strong`, { id: `equipment-preset-title`, children: o2(`common.rename`) }), (0, A.jsxs)(`label`, { children: [o2(`squad.presetNamePrompt`), (0, A.jsx)(`input`, { autoFocus: true, disabled: !!_2, value: le2, onChange: (e2) => ue2(e2.target.value), onKeyDown: (e2) => {
    e2.key === `Enter` && ye2();
  } })] }), (0, A.jsxs)(`div`, { className: `equipment-preset-actions`, children: [(0, A.jsx)(`button`, { disabled: !!_2, onClick: ve2, children: o2(`common.cancel`) }), (0, A.jsx)(`button`, { className: `primary`, onClick: () => void ye2(), disabled: !le2.trim() || !!_2, children: o2(`common.saveConfig`) })] })] }) }), oe2 && (0, A.jsx)(td.div, { className: `equipment-toast`, initial: s2 ? false : { opacity: 0, y: 10, scale: 0.96 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: s2 ? void 0 : { opacity: 0, y: 8, scale: 0.98 }, children: oe2 }), S2 && (0, A.jsxs)(td.div, { className: `equipment-apply-progress`, initial: s2 ? false : { opacity: 0, y: 10, scale: 0.97 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: s2 ? void 0 : { opacity: 0, y: 8, scale: 0.98 }, children: [(0, A.jsx)(`strong`, { children: Te2() }), (0, A.jsx)(`progress`, { max: Math.max(1, S2.total), value: S2.total > 0 ? S2.current : void 0 })] })] })] }) : (0, A.jsx)(`div`, { className: `map-empty`, children: o2(`common.processing`) });
}
function pd({ activeTab: e2, onActiveTabChange: t2, online: n2, serverId: r2, config: i2, staminaPotionConfig: a2, garrisonConfig: o2, zombieBusConfig: s2, onLog: c2 }) {
  let { t: l2 } = d(), [u2, p2] = (0, O.useState)([]), [m2, h2] = (0, O.useState)(``), [_2, v2] = (0, O.useState)(x), [y2, b2] = (0, O.useState)(() => /* @__PURE__ */ new Set([e2 ?? x])), S2 = (0, O.useRef)(false), C2 = e2 ?? _2, w2 = (0, O.useCallback)(async () => {
    if (!(!n2 || S2.current)) {
      S2.current = true;
      try {
        let e3 = await f();
        p2(e3.squads || []), h2(``);
      } catch {
        h2(l2(`common.actionFailed`));
      } finally {
        S2.current = false;
      }
    }
  }, [n2, l2]);
  return (0, O.useEffect)(() => {
    if (C2 !== `equipment`) return;
    w2();
    let e3 = window.setInterval(w2, 2e3);
    return () => window.clearInterval(e3);
  }, [C2, w2]), (0, A.jsxs)(`section`, { className: `panel squad-panel`, children: [(0, A.jsxs)(`div`, { className: `squad-header`, children: [(0, A.jsx)(`h2`, { children: l2(`squad.title`) }), C2 === `equipment` && (0, A.jsx)(`button`, { onClick: w2, disabled: !n2, children: l2(`squad.refresh`) })] }), (0, A.jsx)(`div`, { className: `squad-tabs`, children: g.map((e3) => (0, A.jsx)(`button`, { className: C2 === e3 ? `active` : ``, onClick: () => {
    b2((t3) => {
      if (t3.has(e3)) return t3;
      let n3 = new Set(t3);
      return n3.add(e3), n3;
    }), t2 ? t2(e3) : v2(e3);
  }, children: l2(e3 === `afk` ? `squad.tabAfk` : `squad.tabEquipment`) }, e3)) }), y2.has(`afk`) && (0, A.jsx)(O.Activity, { mode: C2 === `afk` ? `visible` : `hidden`, children: (0, A.jsx)(I, { online: n2, serverId: r2, config: i2, staminaPotionConfig: a2, garrisonConfig: o2, zombieBusConfig: s2, onLog: c2 }) }), y2.has(`equipment`) && (0, A.jsx)(O.Activity, { mode: C2 === `equipment` ? `visible` : `hidden`, children: (0, A.jsx)(fd, { online: n2, squads: u2, reloadSquads: w2, onError: h2 }) }), m2 && (0, A.jsx)(`div`, { className: `automation-error`, children: m2 })] });
}
export {
  pd as SquadPanel
};
