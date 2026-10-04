function I({ online: t, serverId: n, config: r, staminaPotionConfig: i, garrisonConfig: a, zombieBusConfig: o, onLog: s }) {
  let { language: l, t: h } = d(), g = c(), y = e(`task:monsterSweep`, xe(r), { read: async () => xe((await m(g)).config?.tasks?.monsterSweep), write: async (e2) => xe(await v(e2, g)), valid: (e2) => e2.strategies.every(Te) && se(oe(e2.allianceDrill.joinRestrictions)) }, g), b = e(`task:staminaPotion`, Se(i, r), { read: async () => Se((await m(g)).config?.tasks?.staminaPotion), write: async (e2) => (await D(`staminaPotion`, e2, g), e2), valid: (e2) => Number.isInteger(e2.minStamina) && e2.minStamina >= 0 && e2.minStamina <= 9999 }, g), { enabled: x, strategies: S, allianceDrill: E } = y.draft, { enabled: ee, minStamina: te, preferFifty: re } = b.draft, ae = (e2) => b.state.edit((t2) => ({ ...t2, minStamina: e2 })), ce = (e2) => b.state.edit((t2) => ({ ...t2, preferFifty: e2 })), [k, j] = (0, O.useState)(null), [M, de] = (0, O.useState)([]), [N, fe] = (0, O.useState)(false), me = (0, O.useRef)(false), P = (e2) => {
    me.current = e2;
  }, [ve, F] = (0, O.useState)(false), [I2, Ee] = (0, O.useState)(S[0]?.id ?? null), De = (0, O.useRef)(I2);
  De.current = I2;
  let L = S.find((e2) => e2.id === I2) ?? null, Oe = !!L?.targetKey.startsWith(`query:`);
  function R(e2) {
    let t2 = y.state.getSnapshot().draft.strategies.find((e3) => e3.id === De.current) ?? null, n2 = typeof e2 == `function` ? e2(t2) : e2;
    De.current = n2?.id ?? null, Ee(n2?.id ?? null), n2 && y.state.edit((e3) => ({ ...e3, strategies: e3.strategies.some((e4) => e4.id === n2.id) ? e3.strategies.map((e4) => e4.id === n2.id ? n2 : e4) : [...e3.strategies, n2] }), me.current ? false : 400);
  }
  let [ke, z] = (0, O.useState)([]), [Ae, je] = (0, O.useState)({}), Me = [...new Set(ke.map((e2) => e2.joinTargetNameKey).filter((e2) => !!e2))].sort().join(`
`);
  (0, O.useEffect)(() => {
    let e2 = false;
    return je({}), Me && u(l, Me.split(`
`)).then((t2) => {
      e2 || je(t2);
    }).catch(() => void 0), () => {
      e2 = true;
    };
  }, [Me, l, g]);
  let [Ne, Pe] = (0, O.useState)([]), [B, Fe] = (0, O.useState)(``), [Ie, V] = (0, O.useState)(``), H = (0, O.useRef)(null), U = (0, O.useRef)(null), Le = (0, O.useRef)(``), [Re, ze] = (0, O.useState)(null), [Be, Ve] = (0, O.useState)(null), [He, Ue] = (0, O.useState)(``), We = !!(L && !y.confirmed.strategies.some((e2) => e2.id === L.id)), Ge = L?.kind || `farm`;
  (0, O.useEffect)(() => {
    I2 && !S.some((e2) => e2.id === I2) && Ee(S[0]?.id ?? null);
  }, [S, I2]), (0, O.useEffect)(() => () => {
    y.state.flush().catch(() => void 0), b.state.flush().catch(() => void 0);
  }, [y.state, b.state]), (0, O.useEffect)(() => {
    if (!t) {
      fe(false);
      return;
    }
    fe(false), Promise.all([p(), f().catch(() => ({ squads: [] }))]).then(async ([e2, t2]) => {
      Pe((t2.squads || []).map((e3) => e3.index).sort((e3, t3) => e3 - t3));
      let n2 = e2.options.filter((e3) => e3.group !== `drill`), r2 = n2.map((e3) => e3.monsterNameKey).filter(Boolean), i2 = r2.length ? await u(l, r2) : {};
      de(le(n2, i2)), fe(true), V(``);
    }).catch(() => V(h(`common.actionFailed`)));
  }, [l, t, n, h]), (0, O.useEffect)(() => {
    let e2 = false, n2 = false, r2 = async () => {
      if (!n2) {
        if (!t) {
          z([]);
          return;
        }
        n2 = true;
        try {
          let t2 = await C();
          e2 || z(t2.tasks.monsterSweep?.workers || []);
        } catch {
        } finally {
          n2 = false;
        }
      }
    };
    r2();
    let i2 = window.setInterval(() => {
      r2();
    }, 2e3);
    return () => {
      e2 = true, window.clearInterval(i2);
    };
  }, [t]), (0, O.useEffect)(() => {
    function e2(e3) {
      U.current?.contains(e3.target) || F(false);
    }
    return document.addEventListener(`pointerdown`, e2), () => document.removeEventListener(`pointerdown`, e2);
  }, []);
  let Ke = L ? ue(M, L) : void 0, qe = !!(N && L?.targetKey && !Ke), Je = Oe ? L?.lastListTargetKey || `` : Ke?.key || L?.targetKey || ``, Ye = (0, O.useMemo)(() => M.filter((e2) => !e2.key.startsWith(`name:`) && (Ge === `farm` ? !e2.joinOnly : e2.rally)), [Ge, M]), Xe = (0, O.useMemo)(() => {
    let e2 = new Map(_e.map((e3) => [e3, []]));
    for (let t2 of Ye) e2.get(t2.group)?.push(t2);
    return _e.flatMap((t2) => {
      let n2 = e2.get(t2) || [];
      return n2.length ? [{ group: t2, targets: n2 }] : [];
    });
  }, [Ye]);
  async function Ze(e2, t2 = y.state.getSnapshot().draft.enabled, n2 = y.state.getSnapshot().draft.allianceDrill) {
    return y.state.edit({ enabled: t2, strategies: e2, allianceDrill: n2 }, false), y.state.flush();
  }
  async function Qe() {
    P(false), !(!L || !Te(L)) && (R({ ...L, name: L.name.trim() }), await y.state.flush().catch(() => void 0));
  }
  async function $e(e2) {
    if (e2) y.state.edit((t2) => ({ ...t2, enabled: e2 }), false), await y.state.flush().catch(() => void 0);
    else {
      Fe(`stop`);
      try {
        await y.state.runAction(() => v({ ...y.state.getSnapshot().confirmed, enabled: false }, g), (e3) => ({ ...e3, enabled: false }));
      } catch {
      } finally {
        Fe(``);
      }
    }
  }
  async function et(e2) {
    b.state.edit((t2) => ({ ...t2, enabled: e2 }), false), await b.state.flush().catch(() => void 0);
  }
  async function tt(e2) {
    y.state.edit((t2) => ({ ...t2, allianceDrill: { ...e2, joinRestrictions: oe(e2.joinRestrictions) } }), false), await y.state.flush().catch(() => void 0);
  }
  async function nt(e2) {
    if (e2 && E.squadIndexes.length === 0) {
      j(`drill`), V(h(`squad.afkAllianceDrillSquadRequired`));
      return;
    }
    await tt({ ...E, enabled: e2 });
  }
  async function rt(e2) {
    let t2 = y.state.getSnapshot().draft.allianceDrill, n2 = t2.squadIndexes.includes(e2) ? t2.squadIndexes.filter((t3) => t3 !== e2) : [...t2.squadIndexes, e2];
    await tt({ ...t2, enabled: n2.length > 0 && t2.enabled, squadIndexes: n2 });
  }
  async function it(e2) {
    let t2 = Re;
    if (ze(null), Ve(null), t2 == null) return;
    let n2 = y.state.getSnapshot().draft.allianceDrill, r2 = be(n2.squadIndexes, t2, e2);
    r2 !== n2.squadIndexes && await tt({ ...n2, squadIndexes: r2 });
  }
  function at(e2) {
    let t2 = M.find((t3) => t3.key === e2);
    !t2 || !L || (V(``), R({ ...L, targetKey: t2.key, lastListTargetKey: t2.key, targetNameQuery: void 0, monsterNameKey: t2.monsterNameKey, monsterType: t2.monsterType, monsterIds: t2.monsterIds, monsterSpecial: t2.monsterSpecial, source: t2.source, action: t2.action, searchable: t2.searchable, rally: t2.rally, continuousAttack: t2.rally ? false : L.continuousAttack, minLevel: t2.minLevel, maxLevel: t2.maxLevel }));
  }
  function ot(e2) {
    let t2 = M.find((t3) => !t3.key.startsWith(`name:`) && (e2 === `farm` ? !t3.rally : t3.rally));
    t2 && (R(Ce(t2, Ne, e2)), F(false), V(``), window.setTimeout(() => H.current?.scrollIntoView({ behavior: `smooth`, block: `nearest` }), 0));
  }
  function st(e2) {
    let t2 = ue(M, e2);
    R(t2 ? { ...e2, targetKey: t2.key, monsterNameKey: t2.monsterNameKey, monsterIds: t2.monsterIds, monsterSpecial: t2.monsterSpecial, monsterType: t2.monsterType, searchable: t2.searchable, source: t2.source, action: t2.action, rally: t2.rally, continuousAttack: t2.rally ? false : e2.continuousAttack } : { ...e2 }), V(``);
  }
  function ct(e2) {
    if (!L) return;
    let t2 = e2.trim();
    V(``), R({ ...L, lastListTargetKey: L.targetKey.startsWith(`query:`) ? L.lastListTargetKey : Ke?.key || L.targetKey, targetKey: `query:${t2}`, targetNameQuery: e2, monsterNameKey: void 0, monsterType: 0, monsterIds: [], monsterSpecial: void 0, searchable: false, source: `undiscovered`, action: L.kind === `join` ? `rally` : `attack`, rally: L.kind === `join`, minLevel: 1, maxLevel: 999 });
  }
  async function lt(e2) {
    if (window.confirm(`${h(`common.delete`)} \u201C${e2.name}\u201D?`)) {
      Fe(`delete-${e2.id}`);
      try {
        let t2 = S.filter((t3) => t3.id !== e2.id);
        De.current === e2.id && (De.current = t2[0]?.id ?? null, Ee(t2[0]?.id ?? null)), await Ze(t2), V(``);
      } catch {
      } finally {
        Fe(``);
      }
    }
  }
  async function W(e2, t2) {
    try {
      await Ze(S.map((n2) => n2.id === e2 ? { ...n2, enabled: t2 } : n2)), V(``);
    } catch {
    }
  }
  async function ut(e2) {
    let t2 = Le.current;
    if (Le.current = ``, Ue(``), !t2 || t2 === e2 || B) return;
    let n2 = S.findIndex((e3) => e3.id === t2), r2 = S.findIndex((t3) => t3.id === e2);
    if (n2 < 0 || r2 < 0) return;
    let i2 = [...S], [a2] = i2.splice(n2, 1);
    i2.splice(r2, 0, a2), Fe(`reorder`);
    try {
      await Ze(i2), V(``), s(`monster afk strategies reordered: ${t2}`);
    } catch {
    } finally {
      Fe(``);
    }
  }
  function G(e2) {
    let t2 = h(e2.source === `search` ? `squad.afkSearchable` : `squad.afkLocalTarget`), n2 = e2.searchable ? ` \xB7 ${dt(e2)}` : ``, r2 = h(Ge === `join` ? `squad.afkJoin` : e2.action === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`);
    return `${e2.name} \xB7 ${t2} \xB7 ${r2}${n2}`;
  }
  function dt(e2) {
    return e2?.attackMinLevel != null && e2.attackMaxLevel != null ? h(`squad.afkAttackableRange`, { min: e2.attackMinLevel, max: e2.attackMaxLevel }) : h(`squad.afkAttackableRangeUnavailable`);
  }
  let ft = !!(L && we(L, Ke)), pt = !!(B && B !== `save`), K = ke.filter((e2) => e2.activity === `allianceDrill` && e2.running), mt = K.some((e2) => e2.activityRole === `leader`) ? `squad.afkAllianceDrillLeading` : K.length > 0 ? `squad.afkAllianceDrillJoining` : `squad.afkAllianceDrillWaiting`, ht = E.squadIndexes.join(` \u2192 `) || `-`, gt = new Set(E.squadIndexes), _t = [...E.squadIndexes, ...Ne.filter((e2) => !gt.has(e2))], vt = `${h(`squad.afkAllianceDrillOrder`, { order: ht })} \xB7 ${h(E.activeRally ? `squad.afkAllianceDrillActive` : `squad.afkJoin`)}`, yt = k === `garrison` || k === `drill` || k === `zombieBus`;
  return (0, A.jsxs)(`div`, { className: `monster-afk-layout`, children: [(0, A.jsx)(ne, { state: y.state, label: h(`squad.afkMaster`) }), (0, A.jsx)(ne, { state: b.state, label: h(`automation.autoUsePotion`) }), (0, A.jsxs)(`div`, { className: `monster-afk-toolbar`, children: [(0, A.jsx)(ie, { title: h(`squad.afkMaster`), description: h(`squad.afkMasterDescription`), summary: h(x ? `common.enabled` : `common.disabled`), enabled: x, disabled: !!B, onToggle: (e2) => void $e(e2), settingsOpen: k === null, cardSelectable: true, onSettings: () => j(null), settingsLabel: h(`nav.settings`) }), (0, A.jsx)(ie, { title: h(`automation.autoUsePotion`), description: h(`automation.potionMonsterOnly`), summary: `${h(`automation.minStamina`)} ${te}`, enabled: ee, disabled: !!B, onToggle: (e2) => void et(e2), settingsOpen: k === `potion`, cardSelectable: true, onSettings: () => j((e2) => e2 === `potion` ? null : `potion`), settingsLabel: h(`nav.settings`) }), (0, A.jsx)(ie, { title: h(`squad.afkAllianceDrill`), description: h(`squad.afkAllianceDrillDescription`), summary: E.enabled && K.length > 0 ? `${h(mt)} \xB7 ${ht}` : vt, enabled: E.enabled, disabled: !!B, onToggle: (e2) => void nt(e2), settingsOpen: k === `drill`, cardSelectable: true, onSettings: () => j((e2) => e2 === `drill` ? null : `drill`), settingsLabel: h(`nav.settings`) }), (0, A.jsx)(pe, { online: t, serverId: n, config: a, open: k === `garrison`, onOpenChange: () => j((e2) => e2 === `garrison` ? null : `garrison`), onLog: s }), (0, A.jsx)(he, { online: t, config: o, open: k === `zombieBus`, onOpenChange: () => j((e2) => e2 === `zombieBus` ? null : `zombieBus`), onLog: s }), k === `potion` && (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: [(0, A.jsx)(`strong`, { children: h(`automation.autoUsePotion`) }), (0, A.jsx)(`span`, { children: h(`automation.potionMonsterOnly`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-card-settings`, children: [(0, A.jsxs)(`label`, { children: [(0, A.jsx)(`span`, { children: h(`automation.minStamina`) }), (0, A.jsx)(`input`, { type: `number`, min: 0, max: 9999, step: 1, value: Number.isNaN(te) ? `` : te, disabled: !!B, onChange: (e2) => ae(e2.target.valueAsNumber) })] }), (0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: re, disabled: !!B, onChange: (e2) => ce(e2.target.checked) }), (0, A.jsx)(`span`, { children: h(`automation.preferFifty`) })] })] })] }), k === `drill` && (0, A.jsxs)(`section`, { className: `automation-card monster-afk-toolbar-settings`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-toolbar-settings-heading`, children: [(0, A.jsx)(`strong`, { children: h(`squad.afkAllianceDrill`) }), (0, A.jsx)(`span`, { children: h(`squad.afkAllianceDrillDescription`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-drill-inline`, children: [(0, A.jsx)(`span`, { className: `muted`, children: h(`squad.afkAllianceDrillOrder`, { order: ht }) }), (0, A.jsx)(w, { label: `squad.afkAllianceDrillActive`, checked: E.activeRally, disabled: !!B, onChange: (e2) => void tt({ ...E, activeRally: e2 }) })] }), (0, A.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": h(`squad.afkAllianceDrillOrder`, { order: ht }), children: _t.map((e2) => {
    let t2 = gt.has(e2);
    return (0, A.jsxs)(`div`, { className: `automation-squad-priority-item${t2 ? ` selected` : ``}${Re === e2 ? ` dragging` : ``}${Be === e2 ? ` drag-over` : ``}`, draggable: t2 && !B, onDragStart: (n2) => {
      t2 && (n2.dataTransfer.effectAllowed = `move`, ze(e2));
    }, onDragOver: (n2) => {
      !t2 || Re == null || Re === e2 || (n2.preventDefault(), n2.dataTransfer.dropEffect = `move`, Ve(e2));
    }, onDrop: (t3) => {
      t3.preventDefault(), it(e2);
    }, onDragEnd: () => {
      ze(null), Ve(null);
    }, children: [(0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: t2, disabled: !!B, onChange: () => void rt(e2) }), (0, A.jsx)(`span`, { children: h(`squad.number`, { number: e2 }) })] }), t2 && (0, A.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, A.jsx)(T, { name: `drag` }) })] }, e2);
  }) }), (0, A.jsx)(ye, { value: E.joinRestrictions, online: t, disabled: !!B, onChange: (e2) => y.state.edit((t2) => ({ ...t2, allianceDrill: { ...t2.allianceDrill, joinRestrictions: e2 } })) }), K.filter((e2) => e2.step === `waiting_join_delay`).map((e2) => (0, A.jsxs)(`span`, { className: `muted`, children: [h(`squad.number`, { number: e2.squadIndex }), ` \xB7 `, h(`squad.join.waitingDetail`, { target: Ae[e2.joinTargetNameKey ?? ``] || e2.joinTargetName || ``, seconds: e2.joinWaitSeconds ?? 0 })] }, e2.squadIndex))] })] }), !yt && (0, A.jsxs)(A.Fragment, { children: [(0, A.jsxs)(`section`, { className: `monster-afk-profiles`, children: [(0, A.jsxs)(`div`, { className: `monster-section-title`, children: [(0, A.jsx)(`strong`, { children: h(`squad.afkProfiles`) }), (0, A.jsxs)(`div`, { ref: U, className: `monster-afk-add-control`, onKeyDown: (e2) => {
    e2.key === `Escape` && F(false);
  }, children: [(0, A.jsx)(`button`, { type: `button`, "aria-haspopup": `menu`, "aria-expanded": ve, disabled: !M.length || !!B, onClick: () => F((e2) => !e2), children: h(`common.add`) }), ve && (0, A.jsxs)(`div`, { className: `monster-afk-add-menu`, role: `menu`, children: [(0, A.jsxs)(`button`, { type: `button`, role: `menuitem`, disabled: !M.some((e2) => !e2.key.startsWith(`name:`) && !e2.rally), onClick: () => ot(`farm`), children: [(0, A.jsx)(`strong`, { children: h(`squad.afkActiveAttack`) }), (0, A.jsx)(`span`, { children: h(`squad.afkActionAttack`) })] }), (0, A.jsxs)(`button`, { type: `button`, role: `menuitem`, disabled: !M.some((e2) => !e2.key.startsWith(`name:`) && e2.rally), onClick: () => ot(`join`), children: [(0, A.jsx)(`strong`, { children: h(`squad.afkJoin`) }), (0, A.jsx)(`span`, { children: h(`squad.autoJoinRally`) })] })] })] })] }), (0, A.jsxs)(`div`, { className: `monster-afk-profile-list`, children: [S.map((e2) => {
    let t2 = e2.squadIndexes.map((e3) => ke.find((t3) => t3.squadIndex === e3)).filter((e3) => !!e3), n2 = M.find((t3) => t3.key === e2.targetKey) || M.find((t3) => !!e2.monsterNameKey && t3.monsterNameKey === e2.monsterNameKey && t3.rally === e2.rally), r2 = n2?.source || (N ? `undiscovered` : e2.source), i2 = r2 === `search` ? `squad.afkSearchable` : r2 === `map` ? `squad.afkLocalTarget` : `squad.afkUndiscovered`;
    return (0, A.jsxs)(`div`, { className: `monster-afk-profile-card ${L?.id === e2.id ? `active` : ``} ${e2.enabled ? `` : `disabled`} ${He === e2.id ? `drag-over` : ``}`, onDragEnd: () => {
      Le.current = ``, Ue(``);
    }, onDragOver: (t3) => {
      !Le.current || B || (t3.preventDefault(), Ue(e2.id));
    }, onDragLeave: () => Ue((t3) => t3 === e2.id ? `` : t3), onDrop: (t3) => {
      t3.preventDefault(), ut(e2.id);
    }, children: [(0, A.jsxs)(`label`, { className: `monster-afk-enabled ${e2.enabled ? `is-enabled` : ``} ${pt || e2.squadIndexes.length === 0 ? `is-disabled` : ``}`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: e2.enabled, disabled: !!B || e2.squadIndexes.length === 0, onChange: (t3) => void W(e2.id, t3.target.checked) }), (0, A.jsx)(`span`, { className: `monster-afk-enabled-track`, "aria-hidden": `true` }), (0, A.jsx)(`span`, { children: h(e2.enabled ? `common.enabled` : `common.disabled`) })] }), (0, A.jsxs)(`button`, { type: `button`, className: `monster-afk-profile-select`, "aria-pressed": L?.id === e2.id, onClick: () => st(e2), children: [(0, A.jsxs)(`span`, { className: `monster-afk-profile-heading`, children: [(0, A.jsx)(`strong`, { children: e2.name }), (0, A.jsx)(`span`, { className: `monster-afk-mode-badge ${e2.kind}`, children: h(e2.kind === `farm` ? `squad.afkActiveAttack` : `squad.afkJoin`) })] }), (0, A.jsxs)(`span`, { children: [e2.levelFilterEnabled ? `${e2.minLevel}-${e2.maxLevel}` : h(`squad.afkAnyLevel`), ` \xB7 `, e2.distanceFilterEnabled ? e2.maxDistance : h(`squad.afkAnyDistance`)] }), e2.kind === `farm` && e2.searchable && (0, A.jsxs)(`span`, { className: we(e2, n2) ? `monster-afk-level-warning` : ``, children: [dt(n2), we(e2, n2) ? ` \xB7 ${h(`squad.afkLevelOutOfRange`)}` : ``] }), (0, A.jsxs)(`span`, { children: [h(i2), ` \xB7 `, h(e2.kind === `join` ? `squad.afkJoin` : (n2?.action || e2.action) === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`), ` \xB7 `, h(`squad.afkBoundSquads`, { count: e2.squadIndexes.join(`, `) || `-` })] }), t2.map((t3) => {
      let n3 = t3.profileId === e2.id, r3 = t3.completedStrategyIds?.includes(e2.id);
      return (0, A.jsxs)(`span`, { className: n3 && t3.running ? `status-ok` : `muted`, children: [h(`squad.number`, { number: t3.squadIndex }), `: `, n3 && t3.step ? h(ge[t3.step] || t3.step) : h(r3 ? `common.completed` : `squad.status.idle`), n3 && t3.step === `waiting_join_delay` && ` \xB7 ${h(`squad.join.waitingDetail`, { target: Ae[t3.joinTargetNameKey ?? ``] || t3.joinTargetName || ``, seconds: t3.joinWaitSeconds ?? 0 })}`, e2.executionLimit > 0 ? ` \xB7 ${t3.strategyProcessed?.[e2.id] || 0}/${e2.executionLimit}` : ``, n3 && t3.lastError ? ` \xB7 ${_(h, t3.lastError)}` : ``] }, t3.squadIndex);
    })] }), (0, A.jsx)(`button`, { className: `danger`, disabled: !!B, onClick: () => void lt(e2), children: h(`common.delete`) }), (0, A.jsx)(`button`, { type: `button`, className: `monster-afk-profile-drag`, disabled: !!B, draggable: !B, "aria-label": h(`squad.afkReorder`, { name: e2.name }), title: h(`squad.afkReorder`, { name: e2.name }), onDragStart: (t3) => {
      Le.current = e2.id, t3.dataTransfer.effectAllowed = `move`, t3.dataTransfer.setData(`text/plain`, e2.id);
    }, onKeyDown: (t3) => {
      if (t3.key !== `ArrowUp` && t3.key !== `ArrowDown`) return;
      t3.preventDefault();
      let n3 = S.findIndex((t4) => t4.id === e2.id), r3 = S[n3 + (t3.key === `ArrowUp` ? -1 : 1)];
      !r3 || B || (Le.current = e2.id, ut(r3.id));
    }, children: (0, A.jsx)(T, { name: `drag` }) })] }, e2.id);
  }), !S.length && (0, A.jsx)(`span`, { className: `muted`, children: h(`squad.afkNoProfiles`) })] })] }), L && (0, A.jsxs)(`div`, { ref: H, className: `monster-afk-editor ${We ? `is-new` : ``}`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-editor-heading`, children: [(0, A.jsx)(`strong`, { children: h(We ? `squad.afkNewProfile` : `squad.afkEditProfile`) }), (0, A.jsx)(`span`, { className: `monster-afk-mode-badge ${L.kind}`, children: h(L.kind === `farm` ? `squad.afkActiveAttack` : `squad.afkJoin`) })] }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h(`squad.afkBasicSettings`) }), (0, A.jsxs)(`div`, { className: `monster-afk-basic-grid`, children: [(0, A.jsxs)(`label`, { children: [h(`squad.afkProfileName`), (0, A.jsx)(`input`, { value: L.name, onFocus: () => P(true), onBlur: () => void Qe(), onChange: (e2) => R({ ...L, name: e2.target.value }), onKeyDown: (e2) => {
    e2.key === `Enter` && !e2.nativeEvent.isComposing && e2.currentTarget.blur();
  } })] }), (0, A.jsxs)(`label`, { children: [h(`squad.afkTarget`), (0, A.jsxs)(`select`, { value: Je, onChange: (e2) => at(e2.target.value), children: [Oe && !M.some((e2) => e2.key === Je) && (0, A.jsx)(`option`, { value: Je, children: h(`squad.afkSelectListTarget`) }), !Oe && qe && (0, A.jsxs)(`option`, { value: L.targetKey, children: [L.targetNameQuery || L.name, ` \xB7 `, h(`squad.afkUndiscovered`), ` \xB7 `, h(L.kind === `join` ? `squad.afkJoin` : L.action === `rally` ? `squad.afkActionRally` : `squad.afkActionAttack`)] }), Xe.map((e2) => (0, A.jsx)(`optgroup`, { label: h(`squad.afkGroup.${e2.group}`), children: e2.targets.map((e3) => (0, A.jsx)(`option`, { value: e3.key, children: G(e3) }, e3.key)) }, e2.group))] })] })] }), (0, A.jsxs)(`div`, { className: `monster-afk-custom-target`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: Oe, onChange: (e2) => {
    e2.target.checked ? ct(``) : L.lastListTargetKey && M.some((e3) => e3.key === L.lastListTargetKey) ? at(L.lastListTargetKey) : V(h(`squad.afkRestoreTargetRequired`));
  } }), h(`squad.afkCustomTarget`)] }), Oe && (0, A.jsxs)(`label`, { className: `monster-afk-custom-name`, children: [h(`squad.afkCustomName`), (0, A.jsx)(`input`, { value: L.targetNameQuery || ``, "aria-invalid": !L.targetNameQuery?.trim(), onChange: (e2) => ct(e2.target.value) }), (0, A.jsx)(`span`, { className: `muted`, children: h(`squad.afkCustomTargetHint`) }), !L.targetNameQuery?.trim() && (0, A.jsx)(`span`, { role: `status`, className: `status-error`, children: h(`squad.afkCustomTargetRequired`) })] })] })] }), L.kind === `join` && (0, A.jsx)(`section`, { className: `monster-afk-config-section monster-afk-join-section`, children: (0, A.jsx)(ye, { label: `squad.afkJoinConditions`, value: oe(L.joinRestrictions, L.minMembers ?? 1), online: t, disabled: !!B, onChange: (e2) => R({ ...L, joinRestrictions: e2 }) }) }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h(`squad.afkExecutionSettings`) }), (0, A.jsxs)(`div`, { className: `monster-afk-basic-grid monster-afk-execution-grid`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-squad-field`, children: [(0, A.jsx)(`span`, { children: h(`squad.afkAssignments`) }), (0, A.jsx)(`div`, { className: `monster-afk-squads`, role: `group`, "aria-label": h(`squad.afkAssignments`), children: Ne.map((e2) => (0, A.jsx)(`button`, { className: `monster-afk-squad-toggle`, type: `button`, "aria-label": h(`squad.number`, { number: e2 }), "aria-pressed": L.squadIndexes.includes(e2), title: h(`squad.number`, { number: e2 }), onClick: () => R((t2) => t2 && { ...t2, squadIndexes: t2.squadIndexes.includes(e2) ? t2.squadIndexes.filter((t3) => t3 !== e2) : [...t2.squadIndexes, e2].sort((e3, t3) => e3 - t3) }), children: e2 }, e2)) })] }), (0, A.jsxs)(`label`, { title: h(`squad.afkExecutionLimitHint`), children: [h(`squad.afkExecutionLimitLabel`), (0, A.jsx)(`input`, { type: `number`, min: 0, step: 1, value: Number.isNaN(L.executionLimit) ? `` : L.executionLimit, onChange: (e2) => R({ ...L, executionLimit: e2.target.valueAsNumber }) })] })] }), L.kind === `farm` && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L.continuousAttack, disabled: L.rally, onChange: (e2) => R({ ...L, continuousAttack: e2.target.checked }) }), h(`squad.afkContinuousAttack`), L.rally && (0, A.jsx)(`span`, { className: `muted`, children: h(`squad.afkContinuousAttackUnavailable`) })] }), L.kind === `join` && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L.continuousJoin, onChange: (e2) => R({ ...L, continuousJoin: e2.target.checked }) }), h(`squad.afkContinuousJoin`)] })] }), (0, A.jsxs)(`section`, { className: `monster-afk-config-section`, children: [(0, A.jsx)(`h3`, { children: h(`squad.afkTargetFilters`) }), (0, A.jsxs)(`div`, { className: `monster-afk-filter`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L.levelFilterEnabled, onChange: (e2) => R({ ...L, levelFilterEnabled: e2.target.checked, progressiveLevels: e2.target.checked && L.progressiveLevels }) }), h(`squad.afkLevelFilter`)] }), L.levelFilterEnabled && (0, A.jsxs)(`div`, { className: `monster-afk-filter-content`, children: [(0, A.jsxs)(`div`, { className: `monster-afk-number-grid`, children: [(0, A.jsxs)(`label`, { children: [h(`squad.afkMinLevel`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L.minLevel) ? `` : L.minLevel, onChange: (e2) => R({ ...L, minLevel: e2.target.valueAsNumber }) })] }), !L.progressiveLevels && (0, A.jsxs)(`label`, { children: [h(`squad.afkMaxLevel`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L.maxLevel) ? `` : L.maxLevel, onChange: (e2) => R({ ...L, maxLevel: e2.target.valueAsNumber }) })] })] }), L.kind === `farm` && L.searchable && (0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L.progressiveLevels, onChange: (e2) => R({ ...L, progressiveLevels: e2.target.checked }) }), h(`squad.afkProgressiveLevels`)] })] })] }), L.kind === `farm` && L.searchable && (0, A.jsxs)(`div`, { className: `monster-afk-attackable-range ${ft ? `is-invalid` : ``}`, children: [(0, A.jsx)(`span`, { children: dt(Ke) }), ft && (0, A.jsx)(`strong`, { children: h(`squad.afkLevelOutOfRange`) })] }), (0, A.jsxs)(`div`, { className: `monster-afk-filter`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-check-row`, children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: L.distanceFilterEnabled === true, onChange: (e2) => R({ ...L, distanceFilterEnabled: e2.target.checked }) }), h(`squad.afkDistanceFilter`)] }), L.distanceFilterEnabled && (0, A.jsx)(`div`, { className: `monster-afk-filter-content`, children: (0, A.jsxs)(`label`, { children: [h(`squad.maxDistance`), (0, A.jsx)(`input`, { type: `number`, min: 1, value: Number.isNaN(L.maxDistance) ? `` : L.maxDistance, onChange: (e2) => R({ ...L, maxDistance: e2.target.valueAsNumber }) })] }) })] })] })] })] }), Ie && (0, A.jsx)(`div`, { className: `automation-error monster-afk-error`, children: Ie })] });
}
