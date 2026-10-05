function Ae({ activeCategory: t, onActiveCategoryChange: n, profileId: i, autoWeekendShield: o, autoAttackShield: c, online: l, busy: _, resourceBusy: ce, chatBusy: le, config: C, redPacketConfig: ue, treasureConfig: de, fireworksConfig: fe, tradeStationConfig: Ae2, status: k, resourceStatus: je, onToggle: Me, onBackgroundToggle: A, onBackgroundRun: Ne, onConstructionClaimAll: Pe, onInspect: Fe, onRunTask: Ie, onResourceRun: Le, onChatRun: Re }) {
  let { language: j, t: M } = te(), N = i ?? u(), ze = (0, x.useRef)(l);
  ze.current = l, (0, x.useSyncExternalStore)(m, e, e);
  let Be = JSON.stringify(C ?? {}), P = Object.fromEntries(Ee.map((e2) => [e2, h(N, `task:${e2}`, C?.[e2] ?? {}, { read: async () => (await f(N)).config?.tasks?.[e2] ?? {}, write: async (t2) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n2 = Oe(t2);
    return await ae(e2, n2, N), n2;
  }, valid: (t2) => {
    if (e2 === `soldierTraining`) {
      let e3 = Number(t2.totalCount ?? 0);
      return Number.isInteger(e3) && e3 >= 0 && e3 <= 1e6 && (!t2.trainEnabled || e3 > 0);
    }
    let n2 = { targetLevel: [1, 100], maxBuilders: [1, Math.max(1, k?.tasks.construction?.totalBuilders ?? 20)], amountPerArmy: [1, 1e6], threshold: [1, 30], intervalMinutes: [1, 1440], delayMinutes: [0, 1440], ticketCount: [1, 9999], intervalSeconds: [5, 300], positionId: [0, 10007] };
    if (!Object.entries(t2).every(([e3, t3]) => !De.has(e3) || String(t3).trim() !== `` && Number.isInteger(Number(t3)) && Number(t3) >= n2[e3][0] && Number(t3) <= n2[e3][1])) return false;
    if (Array.isArray(t2.delaySeconds)) {
      let [e3, n3] = t2.delaySeconds;
      if ([e3, n3].some((e4) => String(e4).trim() === `` || !Number.isInteger(Number(e4))) || Number(e3) < 0 || Number(n3) > 86400 || Number(e3) > Number(n3)) return false;
    }
    return true;
  } })])), F = Object.fromEntries(Ee.map((e2) => [e2, P[e2].getSnapshot().draft])), I = Object.fromEntries([`redPacket`, `treasure`, `fireworks`].map((e2) => [e2, h(N, `chat:${e2}`, ke(e2 === `redPacket` ? ue : e2 === `treasure` ? de : fe), { read: async () => {
    let t2 = (await f(N)).config?.chat_automation;
    return ke(e2 === `redPacket` ? t2?.red_packet : e2 === `treasure` ? t2?.treasure : t2?.fireworks);
  }, write: async (t2) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n2 = Vr(e2, t2);
    if (!n2) throw Error(`CONFIG_DRAFT_INVALID`);
    return await d(e2, n2, N), t2;
  }, valid: (t2) => Vr(e2, t2) !== null })])), L = Object.fromEntries(w.map(({ name: e2 }) => [e2, h(N, `resource:${e2}`, { enabled: je?.tasks[e2].enabled ?? false, intervalMinutes: je?.tasks[e2].intervalMinutes ?? 60 }, { read: async () => {
    let t2 = (await s(N)).tasks[e2];
    return { enabled: t2.enabled, intervalMinutes: t2.intervalMinutes };
  }, write: async (t2) => {
    if (!ze.current) throw Error(`GAME_DISCONNECTED`);
    let n2 = { ...t2, intervalMinutes: Number(t2.intervalMinutes) };
    return await r(e2, n2, N), n2;
  }, valid: (e3) => Number.isInteger(Number(e3.intervalMinutes)) && Number(e3.intervalMinutes) >= 1 && Number(e3.intervalMinutes) <= 1440 })])), Ve = Object.fromEntries(w.map(({ name: e2 }) => [e2, String(L[e2].getSnapshot().draft.intervalMinutes)])), He = Object.fromEntries(w.map(({ name: e2 }) => [e2, L[e2].getSnapshot().draft.enabled])), Ue = Object.fromEntries([...Ee.map((e2) => [e2, P[e2].getSnapshot()]), ...Object.entries(I).map(([e2, t2]) => [`chat:${e2}`, t2.getSnapshot()]), ...Object.entries(L).map(([e2, t2]) => [`resource:${e2}`, t2.getSnapshot()])].map(([e2, t2]) => [e2, t2.error ? `error` : t2.saving ? `saving` : ``]));
  function R(e2) {
    let [t2, n2] = e2.split(`:`);
    return t2 === `chat` ? I[n2] : t2 === `resource` ? L[n2] : P[t2];
  }
  function z(e2) {
    R(e2)?.flush().catch(() => void 0);
  }
  let We = (0, x.useRef)(false), Ge = I.redPacket.getSnapshot().draft, Ke = I.treasure.getSnapshot().draft, qe = I.fireworks.getSnapshot().draft, Je = (e2) => I.redPacket.edit(e2, false), Ye = (e2) => I.treasure.edit(e2, false), Xe = (e2) => I.fireworks.edit(e2, false), Ze = String(P.allianceDonate.getSnapshot().draft.threshold ?? 15), Qe = (e2) => P.allianceDonate.edit((t2) => ({ ...t2, threshold: e2 }), false), $e = String(P.treatment.getSnapshot().draft.amountPerArmy ?? 1), et = (e2) => P.treatment.edit((t2) => ({ ...t2, amountPerArmy: e2 }), false), [tt, nt] = (0, x.useState)({}), [rt, it] = (0, x.useState)({}), [at, ot] = (0, x.useState)(`daily`), B = t ?? at;
  (0, x.useEffect)(() => {
    l && B === `resourceGather` && Fe(`resourceGather`), l && B === `daily` && (Fe(`soldierTraining`), Fe(`construction`));
  }, [l, B, i]);
  let [V, st] = (0, x.useState)(() => /* @__PURE__ */ new Set([B])), ct = String(P.railway.getSnapshot().draft.delayMinutes ?? 2), lt = (e2) => P.railway.edit((t2) => ({ ...t2, delayMinutes: e2 }), false), ut = String(P.dispatch.getSnapshot().draft.delayMinutes ?? 3), dt = (e2) => P.dispatch.edit((t2) => ({ ...t2, delayMinutes: e2 }), false), ft = String(P.allianceGift.getSnapshot().draft.intervalMinutes ?? 120), pt = (e2) => P.allianceGift.edit((t2) => ({ ...t2, intervalMinutes: e2 }), false), mt = String(P.strongholdResource.getSnapshot().draft.intervalMinutes ?? 60), ht = (e2) => P.strongholdResource.edit((t2) => ({ ...t2, intervalMinutes: e2 }), false), gt = String(P.allianceCenterResource.getSnapshot().draft.intervalMinutes ?? 60), _t = (e2) => P.allianceCenterResource.edit((t2) => ({ ...t2, intervalMinutes: e2 }), false), vt = String(P.construction.getSnapshot().draft.maxBuilders ?? 1), yt = (e2) => P.construction.edit((t2) => ({ ...t2, maxBuilders: e2 }), false), bt = String(P.officialPosition.getSnapshot().draft.positionId ?? 0), xt = (e2) => P.officialPosition.edit((t2) => ({ ...t2, positionId: e2 }), false), [H, St] = (0, x.useState)([]), [Ct, wt] = (0, x.useState)(null), [Tt, Et] = (0, x.useState)(null), [Dt, Ot] = (0, x.useState)(null), [kt, At] = (0, x.useState)(null), [jt, Mt] = (0, x.useState)(null), [Nt, Pt] = (0, x.useState)(null), [Ft, It] = (0, x.useState)(``), [Lt, Rt] = (0, x.useState)(false), [zt, Bt] = (0, x.useState)(`like`), Vt = String(P.allianceTrainRide.getSnapshot().draft.ticketCount ?? 1), Ht = (e2) => P.allianceTrainRide.edit((t2) => ({ ...t2, ticketCount: e2 }), false), [Ut, Wt] = (0, x.useState)([]), [Gt, Kt] = (0, x.useState)(null), qt = P.dispatchAssist.getSnapshot().draft.qualities ?? [], Jt = (e2) => P.dispatchAssist.edit((t2) => ({ ...t2, qualities: e2 }), false), Yt = String(P.dispatchAssist.getSnapshot().draft.delaySeconds?.[0] ?? 0), Xt = (e2) => P.dispatchAssist.edit((t2) => ({ ...t2, delaySeconds: [e2, t2.delaySeconds?.[1] ?? 0] }), false), Zt = String(P.dispatchAssist.getSnapshot().draft.delaySeconds?.[1] ?? 0), Qt = (e2) => P.dispatchAssist.edit((t2) => ({ ...t2, delaySeconds: [t2.delaySeconds?.[0] ?? 0, e2] }), false), $t = String(P.dispatchAssist.getSnapshot().draft.intervalSeconds ?? 30), en = (e2) => P.dispatchAssist.edit((t2) => ({ ...t2, intervalSeconds: e2 }), false), [tn, nn] = (0, x.useState)([]), [rn, an] = (0, x.useState)(``), [on, U] = (0, x.useState)(``);
  (0, x.useEffect)(() => {
    for (let e2 of Ee) P[e2].receive(C?.[e2] ?? {});
  }, [Be]), (0, x.useEffect)(() => {
    I.redPacket.receive(ke(ue)), I.treasure.receive(ke(de)), I.fireworks.receive(ke(fe));
    for (let { name: e2 } of w) {
      let t2 = je?.tasks[e2];
      t2 && L[e2].receive({ enabled: t2.enabled, intervalMinutes: t2.intervalMinutes });
    }
  }, [JSON.stringify([ue, de, fe, je?.tasks])]);
  async function W(e2, t2) {
    P[e2].edit((e3) => ({ ...e3, ...t2 }), false), await P[e2].flush().catch(() => void 0);
  }
  function G(e2, t2, n2) {
    P[t2].edit((e3) => ({ ...e3, ...n2 }));
  }
  (0, x.useEffect)(() => {
    if (!l) return;
    let e2 = false;
    return ne().then((t2) => {
      if (e2) return;
      let n2 = [...new Set((t2.squads || []).map((e3) => e3.index))].sort((e3, t3) => e3 - t3);
      n2.length > 0 && Wt(n2);
    }).catch(() => {
    }), () => {
      e2 = true;
    };
  }, [l, B]), (0, x.useEffect)(() => {
    if (!l || B !== `daily`) return;
    let e2 = true, t2 = () => a().then((t3) => {
      e2 && Kt(t3);
    }).catch(() => void 0);
    t2();
    let n2 = Math.min(300, Math.max(5, Number(F?.dispatchAssist?.intervalSeconds) || 30)), r2 = window.setInterval(t2, n2 * 1e3);
    return () => {
      e2 = false, window.clearInterval(r2);
    };
  }, [B, F?.dispatchAssist?.intervalSeconds, l, M]);
  function sn(e2, t2) {
    let n2 = H.indexOf(e2), r2 = n2 + t2;
    if (n2 < 0 || r2 < 0 || r2 >= H.length) return;
    let i2 = [...H];
    [i2[n2], i2[r2]] = [i2[r2], i2[n2]], St(i2), W(`allianceTrainRide`, { preferredRewardKeys: i2 });
  }
  (0, x.useEffect)(() => {
    St(F?.allianceTrainRide?.preferredRewardKeys ?? []), Rt(F?.allianceTrainRide?.preferRewardQuantity ?? false), Bt(F?.allianceTrainRide?.thanksMode ?? `like`);
  }, [(F?.allianceTrainRide?.preferredRewardKeys ?? []).join(`\0`), F?.allianceTrainRide?.preferRewardQuantity, F?.allianceTrainRide?.thanksMode, F?.allianceTrainRide?.ticketCount]), (0, x.useEffect)(() => {
    if (!l) {
      We.current = false;
      return;
    }
    B === `alliance` && !We.current && (We.current = true, Fe(`allianceTrainRide`));
  }, [B, l, Fe]);
  let K = k?.tasks.construction, cn = Array.isArray(K?.buildingTypes) ? K.buildingTypes : [], [ln, un] = (0, x.useState)({}), dn = JSON.stringify([...new Set(cn.map((e2) => e2.nameKey).filter(Boolean))].sort());
  (0, x.useEffect)(() => {
    let e2 = true;
    return un({}), dn !== `[]` && ee(j, JSON.parse(dn)).then((t2) => {
      e2 && un(t2);
    }).catch(() => {
    }), () => {
      e2 = false;
    };
  }, [j, dn, N]);
  let fn = (0, x.useId)(), [pn, mn] = (0, x.useState)(`all`);
  (0, x.useEffect)(() => {
    mn(`all`);
  }, [N]);
  let q = [`all`, `economy`, `military`, `decoration`, `season`, `other`].filter((e2) => e2 === `all` || cn.some((t2) => (t2.category ?? `other`) === e2)), hn = q.includes(pn) ? pn : `all`, gn = cn.filter((e2) => hn === `all` || (e2.category ?? `other`) === hn), _n = F?.construction?.targetEnabled ?? false, vn = F?.construction?.buildingTypeIds ?? [], yn = F?.construction?.targetLevel ?? 30, bn = (e2) => {
    let t2 = ln[e2.nameKey];
    return t2 && t2 !== e2.nameKey ? t2 : e2.name || M(`automation.currentBuilding`);
  }, xn = k?.tasks.stamina, J = k?.tasks.treatment, Sn = k?.tasks.allianceDonate, Cn = k?.tasks.officialPosition, Y = k?.tasks.allianceTrainRide, wn = k?.tasks.allianceHelp, Tn = k?.tasks.allianceGift, En = k?.tasks.strongholdResource, Dn = k?.tasks.allianceCenterResource, On = k?.tasks.allianceGather, X = k?.tasks.railway, Z = k?.tasks.dispatch, kn = k?.tasks.ghostRecon, An = F?.construction?.enabled ?? K?.enabled ?? false, jn = F?.construction?.autoClaimCompleted ?? true, Mn = Number(vt), Nn = Math.max(1, K?.totalBuilders ?? 20), Pn = Number.isInteger(Mn) && Mn >= 1 && Mn <= Nn, Fn = F?.stamina?.enabled ?? xn?.enabled ?? false, In = F?.treatment?.enabled ?? J?.enabled ?? false, Ln = Number($e), Rn = Number.isInteger(Ln) && Ln >= 1 && Ln <= 1e6, zn = F?.allianceDonate?.enabled ?? Sn?.enabled ?? false, Bn = F?.officialPosition?.enabled ?? Cn?.enabled ?? false, Vn = Number(bt), Hn = Vn === 0 || ve.some((e2) => e2.id === Vn), Un = F?.allianceTrainRide?.enabled ?? Y?.enabled ?? false, Wn = F?.allianceTrainRide?.autoAcceptVip ?? false, Gn = (F?.allianceTrainRide?.fixedCarriageIds ?? []).filter((e2) => e2 >= 1 && e2 <= 4), Kn = (F?.allianceTrainRide?.vipFixedCarriageIds ?? []).filter((e2) => e2 >= 1 && e2 <= 4), qn = F?.allianceTrainRide?.selectionMode ?? (Gn.length === 1 ? `fixed` : `reward`), Jn = F?.allianceTrainRide?.vipSelectionMode ?? (Kn.length === 2 ? `fixed` : `reward`), Yn = Te(qn, Jn), Xn = Y?.rewardOptions ?? [], Zn = new Map(Xn.map((e2) => [e2.key, e2])), Qn = new Set(H), $n = [...H.map((e2) => Zn.get(e2)).filter((e2) => e2 !== void 0), ...Xn.filter((e2) => !Qn.has(e2.key))];
  Number(Ze);
  let er = F?.allianceHelp?.enabled ?? wn?.enabled ?? false, tr = F?.allianceGift?.enabled ?? Tn?.enabled ?? false, nr = F?.strongholdResource?.enabled ?? En?.enabled ?? false, rr = F?.allianceCenterResource?.enabled ?? Dn?.enabled ?? false, ir = F?.allianceGather?.enabled ?? On?.enabled ?? false, ar = F?.allianceGather?.squadPriority ?? [], or = new Set(ar), sr = [...ar, ...Ut.filter((e2) => !or.has(e2))], cr = F?.railway?.enabled ?? X?.enabled ?? false, lr = F?.railway?.departWhenTicketsInsufficient ?? false, ur = F?.dispatch?.autoExecute ?? F?.dispatch?.enabled ?? false, dr = F?.dispatch?.collectRewards ?? F?.dispatch?.enabled ?? false, fr = ur || dr, pr = F?.ghostRecon?.autoStartOwn ?? false, mr = F?.ghostRecon?.autoJoinAlliance ?? false, hr = F?.ghostRecon?.autoClaimRewards ?? false, gr = F?.ghostRecon?.allianceFilter ?? `special`, _r = pr || mr || hr, vr = F?.dispatchAssist?.autoHelp ?? F?.dispatchAssist?.enabled ?? false, yr = Number(Yt), br = Number(Zt), xr = Number($t), Sr = new Map((Gt?.jobs ?? []).map((e2) => [e2.uuid, e2])), Cr = F?.railway?.weeklyQualities ?? ge, wr = F?.dispatch?.weeklyQualities ?? _e, Tr = Number(ct), Er = Number(ut), Dr = Number(ft), Or = Number.isInteger(Tr) && Tr >= 0 && Tr <= 1440, kr = Number.isInteger(Er) && Er >= 0 && Er <= 1440, Ar = Number.isInteger(Dr) && Dr >= 1 && Dr <= 1440, jr = Number(mt), Mr = Number(gt), Nr = Number.isInteger(jr) && jr >= 1 && jr <= 1440, Pr = Number.isInteger(Mr) && Mr >= 1 && Mr <= 1440, Fr = Z?.details, Q = k?.services?.shield, $ = (e2) => !l || _ === e2, Ir = Object.values(Ue), Lr = Ir.includes(`error`) ? `error` : Ir.includes(`saving`) ? `saving` : Ir.includes(`saved`) ? `saved` : ``, Rr = Array.from({ length: 7 }, (e2, t2) => new Intl.DateTimeFormat(j, { weekday: `short`, timeZone: `UTC` }).format(new Date(Date.UTC(2024, 0, t2 + 1))));
  function zr(e2, t2, n2) {
    return (0, S.jsx)(`div`, { className: `automation-weekly-quality`, children: Rr.map((r2, i2) => (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`span`, { children: r2 }), (0, S.jsxs)(`select`, { value: t2[i2], disabled: $(e2) || n2, onChange: (n3) => {
      let r3 = [...t2];
      r3[i2] = n3.target.value, W(e2, { weeklyQualities: r3 });
    }, children: [(0, S.jsx)(`option`, { value: `none`, children: M(`automation.noQualityRefresh`) }), (0, S.jsx)(`option`, { value: `ssr`, children: M(`automation.ssrOrAbove`) }), (0, S.jsx)(`option`, { value: `ur`, children: `UR` })] })] }, r2)) });
  }
  async function Br(e2, t2, n2 = Number(Ve[e2])) {
    L[e2].edit({ enabled: t2, intervalMinutes: n2 }, false), await L[e2].flush().catch(() => void 0);
  }
  function Vr(e2, t2) {
    let n2 = Number(t2.min), r2 = Number(t2.max), i2 = we(t2.replies), a2 = e2 === `treasure` ? 600 : 60, o2 = t2.min.trim() !== `` && t2.max.trim() !== `` && Number.isFinite(n2) && Number.isFinite(r2) && n2 >= 0 && r2 <= a2 && n2 <= r2, s2 = ``;
    if (o2 ? [[t2.replyMin, t2.replyMax], ...e2 === `treasure` ? [[t2.dispatchMin, t2.dispatchMax]] : []].some(([e3, t3]) => !e3.trim() || !t3.trim() || !Number.isFinite(Number(e3)) || !Number.isFinite(Number(t3)) || Number(e3) < 0.1 || Number(t3) > 600 || Number(e3) > Number(t3)) ? s2 = M(`automation.interactionDelayError`) : t2.replyEnabled && i2.length === 0 ? s2 = M(`automation.replyRequired`) : e2 === `treasure` && t2.dispatchEnabled && !t2.dispatchSquads?.length ? s2 = M(`automation.treasureDispatchSquadRequired`) : e2 === `treasure` && (!Number.isInteger(Number(t2.dispatchRetrySeconds)) || Number(t2.dispatchRetrySeconds) < 1 || Number(t2.dispatchRetrySeconds) > 300) && (s2 = M(`automation.treasureDispatchRetryError`)) : s2 = M(e2 === `treasure` ? `automation.treasureDelayError` : e2 === `fireworks` ? `automation.fireworksDelayError` : `automation.redPacketDelayError`), nt((t3) => ({ ...t3, [e2]: s2 })), s2) return null;
    let c2 = { enabled: t2.enabled, claimDelaySeconds: [n2, r2], replyEnabled: t2.replyEnabled, replyPhrases: i2, replyDelaySeconds: [Number(t2.replyMin), Number(t2.replyMax)] };
    return e2 === `treasure` && (c2.searchEnabled = t2.searchEnabled === true, c2.dispatchDelaySeconds = [Number(t2.dispatchMin), Number(t2.dispatchMax)], c2.dispatchEnabled = t2.dispatchEnabled === true, c2.dispatchSquadPriority = t2.dispatchSquads ?? [], c2.dispatchRetrySeconds = Number(t2.dispatchRetrySeconds)), c2;
  }
  async function Hr(e2, t2) {
    I[e2].edit(t2, false), await I[e2].flush().catch(() => void 0);
  }
  async function Ur(e2) {
    try {
      let t2 = await I[e2].runAction(() => Re(e2));
      it((n2) => ({ ...n2, [e2]: t2 > 0 ? M(`automation.queued`, { count: t2 }) : M(`automation.nothingToClaim`) }));
    } catch {
      it((t2) => ({ ...t2, [e2]: M(`automation.runFailed`) }));
    }
  }
  function Wr(e2, t2 = qt, n2 = yr, r2 = br, i2 = xr) {
    if (!Number.isInteger(n2) || !Number.isInteger(r2) || n2 < 0 || r2 > 86400 || n2 > r2 || !Number.isInteger(i2) || i2 < 5 || i2 > 300 || e2 && !t2.length) {
      U(M(`automation.dispatchAssistConfigError`));
      return;
    }
    U(``), W(`dispatchAssist`, { autoHelp: e2, qualities: t2, delaySeconds: [n2, r2], intervalSeconds: i2 });
  }
  function Gr(e2, t2, n2) {
    if (!(Number.isInteger(e2) && Number.isInteger(t2) && e2 >= 0 && t2 <= 86400 && e2 <= t2 && Number.isInteger(n2) && n2 >= 5 && n2 <= 300)) {
      R(`dispatchAssist:settings`)?.pause(), U(M(`automation.dispatchAssistConfigError`));
      return;
    }
    U(``), P.dispatchAssist.edit((r2) => ({ ...r2, autoHelp: vr, qualities: qt, delaySeconds: [e2, t2], intervalSeconds: n2 }));
  }
  function Kr(e2) {
    let t2 = e2.qualityKey.toUpperCase(), n2 = e2.isSpecial ? ` \xB7 ${M(`automation.assistQuality.special`)}` : ``;
    return `${e2.ownerName || e2.ownerUid} \xB7 ${t2}${n2}`;
  }
  async function qr() {
    if (tn.length) {
      an(`schedule`);
      try {
        await ie(tn), nn([]), Kt(await a()), U(``);
      } catch {
        U(``);
      } finally {
        an(``);
      }
    }
  }
  async function Jr(e2, t2) {
    an(`${e2}:${t2}`);
    try {
      e2 === `cancel` ? await p(t2) : await re(t2), Kt(await a()), U(``);
    } catch {
      U(``);
    } finally {
      an(``);
    }
  }
  function Yr(e2) {
    return e2.starIcons?.length ? (0, S.jsx)(`span`, { className: `automation-assist-stars`, "aria-label": `${e2.star}`, children: e2.starIcons.map((e3, t2) => (0, S.jsx)(b, { assetPath: e3, alt: ``, className: `automation-assist-star` }, `${e3}:${t2}`)) }) : null;
  }
  function Xr(e2) {
    return (0, S.jsx)(`span`, { className: `automation-assist-rewards`, children: (e2.items ?? []).map((e3, t2) => {
      let n2 = `${e3.name || e3.key} \xD7${D(e3.count)}`;
      return (0, S.jsxs)(`span`, { className: `map-reward-item`, title: n2, "aria-label": n2, children: [(0, S.jsx)(b, { assetPath: e3.iconPath, alt: e3.name || e3.key, className: `map-reward-icon` }), (0, S.jsxs)(`strong`, { children: [`\xD7`, se(e3.count)] })] }, `${e3.key}:${t2}`);
    }) });
  }
  function Zr(e2) {
    let t2 = e2 === `redPacket`, n2 = e2 === `treasure`, r2 = t2 ? k?.services?.redPacket : n2 ? k?.services?.treasure : k?.services?.fireworks, i2 = t2 ? Ge : n2 ? Ke : qe, a2 = t2 ? Je : n2 ? Ye : Xe, o2 = `chat:${e2}`, s2 = (t3, n3 = false) => {
      let r3 = { ...i2, ...t3 };
      a2(r3), n3 ? (R(o2)?.pause(), Hr(e2, r3)) : I[e2].edit(r3);
    }, c2 = t2 ? `automation.redPacket.title` : n2 ? `automation.treasure.title` : `automation.fireworks.title`, u2 = i2.dispatchSquads ?? [], ee2 = new Set(u2), d2 = [...u2, ...Ut.filter((e3) => !ee2.has(e3))];
    return (0, S.jsxs)(y, { online: l, title: c2, description: M(`automation.chatClaimDescription`, { type: M(c2) }), state: O(r2?.lastResult?.state, i2.enabled), enabled: i2.enabled, disabled: !l || le === e2, onToggle: (e3) => s2({ enabled: e3 }, true), actionLabel: `common.runNow`, actionBusy: le === e2, onAction: () => void Ur(e2), summaryRows: [[`automation.pendingClaims`, r2?.pendingCount], n2 ? [`automation.treasureDispatchPending`, r2?.dispatchPendingCount] : [`automation.latestResult`, rt[e2] || (r2?.lastResult?.state === `success` ? `common.success` : r2?.lastResult?.state === `failed` ? `common.failed` : null)]], error: tt[e2] || void 0, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.claim`) }), n2 && (0, S.jsx)(`p`, { className: `hint`, children: M(`automation.treasureTargetDelayHint`) }), (0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.minDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: n2 ? 600 : 60, step: 0.01, value: i2.min, onChange: (e3) => s2({ min: e3.target.value }), onBlur: () => z(o2) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.maxDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: n2 ? 600 : 60, step: 0.01, value: i2.max, onChange: (e3) => s2({ max: e3.target.value }), onBlur: () => z(o2) })] })] })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.autoReply`, checked: i2.replyEnabled, disabled: !l || le === e2, onChange: (e3) => s2({ replyEnabled: e3 }, true) }), i2.replyEnabled && (0, S.jsxs)(`div`, { className: `automation-subsettings`, children: [(0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.replyDelayMin`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i2.replyMin, onChange: (e3) => s2({ replyMin: e3.target.value }), onBlur: () => z(o2) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.replyDelayMax`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i2.replyMax, onChange: (e3) => s2({ replyMax: e3.target.value }), onBlur: () => z(o2) })] })] }), (0, S.jsxs)(`label`, { className: `automation-replies`, children: [M(`automation.replyPhrases`), (0, S.jsx)(`textarea`, { rows: 4, value: i2.replies, onChange: (e3) => s2({ replies: e3.target.value }), onBlur: () => z(o2) })] })] })] }), n2 && (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.treasureAutoSearch`, checked: i2.searchEnabled === true, disabled: !l || le === e2, onChange: (e3) => s2({ searchEnabled: e3 }, true) }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.treasureAutoSearchHint`) }), (0, S.jsx)(g, { label: `automation.treasureAutoDispatch`, checked: i2.dispatchEnabled === true, disabled: !l || le === e2, onChange: (e3) => s2({ dispatchEnabled: e3 }, true) }), i2.dispatchEnabled === true && (0, S.jsxs)(`div`, { className: `automation-subsettings`, children: [(0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.dispatchDelayMin`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i2.dispatchMin, onChange: (e3) => s2({ dispatchMin: e3.target.value }), onBlur: () => z(o2) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.dispatchDelayMax`), (0, S.jsx)(`input`, { type: `number`, min: 0.1, max: 600, step: 0.1, value: i2.dispatchMax, onChange: (e3) => s2({ dispatchMax: e3.target.value }), onBlur: () => z(o2) })] })] }), (0, S.jsxs)(`label`, { children: [M(`automation.treasureDispatchRetrySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 300, step: 1, value: i2.dispatchRetrySeconds, onChange: (e3) => s2({ dispatchRetrySeconds: e3.target.value }), onBlur: () => z(o2) })] }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": M(`automation.treasureDispatchPriorityHint`), children: d2.map((e3) => {
      let t3 = u2.indexOf(e3) >= 0;
      return (0, S.jsxs)(`div`, { className: `automation-squad-priority-item${t3 ? ` selected` : ``}${Dt === e3 ? ` dragging` : ``}${kt === e3 ? ` drag-over` : ``}`, draggable: t3, onDragStart: (n3) => {
        t3 && (n3.dataTransfer.effectAllowed = `move`, n3.dataTransfer.setData(`text/plain`, String(e3)), Ot(e3));
      }, onDragOver: (n3) => {
        !t3 || Dt === null || Dt === e3 || (n3.preventDefault(), n3.dataTransfer.dropEffect = `move`, At(e3));
      }, onDrop: (n3) => {
        n3.preventDefault();
        let r3 = Dt;
        if (Ot(null), At(null), r3 === null || !t3) return;
        let i3 = Se(u2, r3, e3);
        i3 !== u2 && s2({ dispatchSquads: i3 }, true);
      }, onDragEnd: () => {
        Ot(null), At(null);
      }, children: [(0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t3, onChange: (t4) => s2({ dispatchSquads: t4.target.checked ? [...u2, e3] : u2.filter((t5) => t5 !== e3) }, true) }), (0, S.jsx)(`span`, { children: M(`automation.squad`, { index: e3 }) })] }), t3 && (0, S.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) })] }, e3);
    }) }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.treasureDispatchPriorityHint`) })] })] })] }, e2);
  }
  return (0, S.jsxs)(`section`, { className: `panel`, children: [Object.entries(P).map(([e2, t2]) => (0, S.jsx)(oe, { disabled: !l, state: t2, label: M(e2 === `dispatch` ? `automation.secretTask.title` : e2 === `dispatchAssist` ? `automation.dispatchAssist` : e2 === `ghostRecon` ? `automation.ghost.title` : `automation.${e2}.title`) }, e2)), Object.entries(I).map(([e2, t2]) => (0, S.jsx)(oe, { disabled: !l, state: t2, label: M(`automation.${e2}.title`) }, e2)), w.map(({ name: e2, title: t2 }) => (0, S.jsx)(oe, { disabled: !l, state: L[e2], label: M(t2) }, e2)), (0, S.jsxs)(`div`, { className: `panel-title`, children: [(0, S.jsx)(`h2`, { children: M(`nav.automation`) }), (0, S.jsx)(`span`, { className: Lr === `error` ? `error-text` : `muted`, children: M(Lr ? `automation.configSave.${Lr}` : l ? `status.gameConnected` : `status.gameDisconnectedDisabled`) })] }), (0, S.jsx)(`div`, { className: `automation-categories`, role: `tablist`, children: [`daily`, `alliance`, `resourceGather`, `resources`, `chat`, `trade`, `system`].map((e2) => (0, S.jsx)(`button`, { className: B === e2 ? `active` : ``, role: `tab`, "aria-selected": B === e2, onClick: () => {
    st((t2) => {
      if (t2.has(e2)) return t2;
      let n2 = new Set(t2);
      return n2.add(e2), n2;
    }), n ? n(e2) : ot(e2);
  }, children: M(`automation.category.${e2}`) }, e2)) }), (0, S.jsxs)(`div`, { className: `automation-grid`, children: [V.has(`resourceGather`) && (0, S.jsx)(x.Activity, { mode: B === `resourceGather` ? `visible` : `hidden`, children: (0, S.jsx)(me, { online: l, config: P.resourceGather.getSnapshot().draft, status: k?.tasks.resourceGather, squadIndexes: Ut, onSave: (e2) => {
    W(`resourceGather`, e2);
  } }) }), V.has(`trade`) && (0, S.jsx)(x.Activity, { mode: B === `trade` ? `visible` : `hidden`, children: (0, S.jsx)(pe, { profileId: i, online: l, config: Ae2, status: k?.services?.tradeStation }) }), V.has(`system`) && (0, S.jsx)(x.Activity, { mode: B === `system` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(y, { online: l, title: `automation.weekendShield.title`, description: `automation.weekendShield.description`, state: o ? `common.waiting` : `common.disabled`, enabled: o, disabled: !l, onToggle: (e2) => Me(`autoWeekendShield`, e2), statusRows: [[`automation.currentShield`, Q?.shielded ? `common.on` : `common.off`], [`automation.shieldEnds`, T(Q?.shieldEndAt, j)], [`automation.weekendWindow`, Q?.inWeekendWindow ? `common.yes` : `common.no`], [`automation.weekendWindowLocal`, Q?.weekendStartAt && Q?.weekendEndAt ? `${T(Q.weekendStartAt, j)} \u2013 ${T(Q.weekendEndAt, j)}` : `-`]] }), (0, S.jsx)(y, { online: l, title: `automation.attackShield.title`, description: `automation.attackShield.description`, state: c ? `common.waiting` : `common.disabled`, enabled: c, disabled: !l, onToggle: (e2) => Me(`autoAttackShield`, e2), statusRows: [[`automation.currentShield`, Q?.shielded ? `common.on` : `common.off`], [`automation.pendingReason`, Q?.pendingReason || `-`]] })] }) }), V.has(`chat`) && (0, S.jsx)(x.Activity, { mode: B === `chat` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [Zr(`redPacket`), Zr(`fireworks`), Zr(`treasure`)] }) }), V.has(`resources`) && (0, S.jsx)(x.Activity, { mode: B === `resources` ? `visible` : `hidden`, children: w.map((e2) => {
    let t2 = je?.tasks[e2.name], n2 = He[e2.name], r2 = Number(Ve[e2.name]), i2 = Number.isInteger(r2) && r2 >= 1 && r2 <= 1440;
    return (0, S.jsx)(y, { online: l, title: e2.title, settingsCollapsible: false, description: e2.description, state: O(t2?.state, n2), enabled: n2, disabled: !l || ce === e2.name, onToggle: (t3) => {
      Br(e2.name, t3);
    }, actionLabel: `common.runNow`, actionBusy: ce === e2.name, onAction: () => {
      L[e2.name].runAction(() => Le(e2.name)).catch(() => void 0);
    }, statusRows: [[`automation.lastRun`, T(t2?.lastRunAt, j)], [`automation.nextRun`, n2 ? E(t2?.nextRunAt, j) : `-`]], error: i2 ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: Ve[e2.name], onBlur: () => {
      z(`resource:${e2.name}`);
    }, onChange: (t3) => {
      let n3 = t3.target.value;
      L[e2.name].edit((e3) => ({ ...e3, intervalMinutes: n3 }));
    } })] }) }) }, e2.name);
  }) }), V.has(`daily`) && (0, S.jsx)(x.Activity, { mode: B === `daily` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(he, { online: l, profileId: N, config: F.soldierTraining, status: k?.tasks.soldierTraining, disabled: $(`soldierTraining`), onPatch: (e2) => P.soldierTraining.edit((t2) => ({ ...t2, ...e2 })), onFlush: () => {
    P.soldierTraining.flush().catch(() => void 0);
  } }, N), (0, S.jsxs)(y, { online: l, title: `automation.construction.title`, description: `automation.construction.description`, state: O(K?.state, An), enabled: An, disabled: $(`construction`), onToggle: (e2) => A(`construction`, e2), actionLabel: `automation.claimAllBuildingRewards`, actionBusy: _ === `construction`, onAction: Pe, summaryRows: [[`automation.automaticBuilders`, K?.automaticBuilders == null ? null : `${K.automaticBuilders} / ${K.maxBuilders ?? Mn}`], [`automation.buildersTotal`, K?.occupiedBuilders == null || K.totalBuilders == null ? null : `${K.occupiedBuilders} / ${K.totalBuilders}`]], statusRows: [[`automation.currentBuilding`, ln[cn.find((e2) => e2.itemId === K?.candidate?.itemId)?.nameKey ?? ``] || K?.candidate?.name || K?.candidate?.uuid || `-`], [`automation.currentLevel`, D(K?.candidate?.level)], [`automation.automaticBuilders`, `${D(K?.automaticBuilders ?? 0)} / ${D(K?.maxBuilders ?? Mn)}`], [`automation.buildersTotal`, `${D(K?.occupiedBuilders ?? 0)} / ${D(K?.totalBuilders ?? 0)}`], [`automation.processed`, D(K?.processed ?? 0)], [`automation.nextCheck`, T(K?.nextRunAt, j)]], error: Pn ? void 0 : `automation.builderLimitError`, children: [(0, S.jsx)(g, { variant: `checkbox`, label: `automation.construction.targetEnabled`, checked: _n, disabled: $(`construction`), onChange: (e2) => {
    W(`construction`, { targetEnabled: e2 });
  } }), _n && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.construction.targetLevel`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 100, step: 1, value: yn, disabled: $(`construction`), onChange: (e2) => P.construction.edit((t2) => ({ ...t2, targetLevel: e2.target.value })), onBlur: () => z(`construction:targetLevel`) })] }) }), (0, S.jsxs)(`details`, { className: `construction-type-select`, children: [(0, S.jsxs)(`summary`, { children: [(0, S.jsx)(`span`, { children: M(`automation.construction.buildingTypes`) }), (0, S.jsx)(`strong`, { children: vn.length ? cn.filter((e2) => vn.includes(e2.itemId)).map(bn).join(`\u3001`) : M(`automation.construction.selectTypes`) }), (0, S.jsx)(`span`, { className: `construction-select-arrow`, "aria-hidden": `true`, children: `\u2304` })] }), (0, S.jsx)(`div`, { className: `construction-category-tabs`, role: `tablist`, "aria-label": M(`automation.construction.buildingTypes`), children: q.map((e2) => (0, S.jsx)(`button`, { type: `button`, role: `tab`, id: `${fn}-${e2}`, "aria-controls": fn, "aria-selected": hn === e2, tabIndex: hn === e2 ? 0 : -1, onClick: () => mn(e2), onKeyDown: (t2) => {
    let n2 = q.indexOf(e2), r2 = t2.key === `ArrowRight` ? (n2 + 1) % q.length : t2.key === `ArrowLeft` ? (n2 + q.length - 1) % q.length : t2.key === `Home` ? 0 : t2.key === `End` ? q.length - 1 : -1;
    r2 < 0 || (t2.preventDefault(), mn(q[r2]), t2.currentTarget.parentElement?.querySelectorAll(`[role="tab"]`)[r2]?.focus());
  }, children: M(`automation.construction.category.${e2}`) }, e2)) }), (0, S.jsx)(`fieldset`, { className: `construction-target-types`, role: `tabpanel`, id: fn, "aria-labelledby": `${fn}-${hn}`, disabled: $(`construction`), children: gn.map((e2) => (0, S.jsxs)(`label`, { className: `automation-checkbox-row`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: vn.includes(e2.itemId), onChange: (t2) => {
    W(`construction`, { buildingTypeIds: t2.target.checked ? [...vn, e2.itemId] : vn.filter((t3) => t3 !== e2.itemId) });
  } }), (0, S.jsx)(`span`, { children: bn(e2) })] }, e2.itemId)) })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(vn.length ? `automation.construction.targetHint` : `automation.construction.selectTypes`) })] }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoCollectRewards`, checked: jn, disabled: $(`construction`), onChange: (e2) => {
    W(`construction`, { autoClaimCompleted: e2 });
  } }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.maxBuilders`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: Nn, step: 1, value: vt, disabled: $(`construction`), onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    yt(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= Nn ? G(`construction:maxBuilders`, `construction`, { maxBuilders: n2 }) : R(`construction:maxBuilders`)?.pause();
  }, onBlur: () => z(`construction:maxBuilders`) })] }) })] }), (0, S.jsx)(y, { online: l, title: `automation.stamina.title`, description: `automation.stamina.description`, state: O(xn?.state, Fn), enabled: Fn, disabled: $(`stamina`), onToggle: (e2) => A(`stamina`, e2), statusRows: [[`automation.claimedToday`, `${D(xn?.todayCount)} / ${D(xn?.dailyLimit)}`], [`automation.processed`, D(xn?.processed ?? 0)], [`automation.nextClaim`, E(xn?.nextClaimAt ?? xn?.nextRunAt, j)]] }), (0, S.jsx)(y, { online: l, title: `automation.treatment.title`, settingsCollapsible: false, description: `automation.treatment.description`, state: O(J?.state, In), enabled: In, disabled: $(`treatment`), onToggle: (e2) => A(`treatment`, e2), summaryRows: [[`automation.wounded`, J?.wounded], [`automation.treating`, J?.treating]], statusRows: [[`automation.wounded`, D(J?.wounded ?? 0)], [`automation.treating`, D(J?.treating ?? 0)], [`automation.treatmentBatches`, D(J?.batchesStarted ?? 0)], [`automation.soldiersQueued`, D(J?.soldiersQueued ?? 0)], [`automation.helpsRequested`, D(J?.helpsRequested ?? 0)], [`automation.collected`, D(J?.collected ?? 0)], [`automation.nextCheck`, T(J?.nextRunAt, j)]], error: Rn ? void 0 : `automation.treatmentAmountError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.treatmentAmountPerArmy`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1e6, step: 1, value: $e, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    et(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= 1e6 ? G(`treatment:amountPerArmy`, `treatment`, { amountPerArmy: n2 }) : R(`treatment:amountPerArmy`)?.pause();
  }, onBlur: () => z(`treatment:amountPerArmy`) })] }) }) })] }) }), V.has(`alliance`) && (0, S.jsx)(x.Activity, { mode: B === `alliance` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(y, { online: l, title: `automation.allianceDonate.title`, settingsCollapsible: false, description: `automation.allianceDonate.description`, state: O(Sn?.state, zn), enabled: zn, disabled: $(`allianceDonate`), onToggle: (e2) => A(`allianceDonate`, e2), summaryRows: [[`automation.remaining`, Sn?.remaining], [`automation.donated`, Sn?.donated]], statusRows: [[`automation.currentTech`, D(Sn?.scienceId)], [`automation.remaining`, D(Sn?.remaining)], [`automation.donated`, D(Sn?.donated ?? 0)], [`automation.nextCheck`, T(Sn?.nextRunAt, j)]], children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.donateThreshold`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 30, step: 1, value: Ze, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    Qe(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= 30 ? G(`allianceDonate:threshold`, `allianceDonate`, { threshold: n2 }) : R(`allianceDonate:threshold`)?.pause();
  }, onBlur: () => z(`allianceDonate:threshold`) })] }) }) }), (0, S.jsx)(y, { online: l, title: `automation.officialPosition.title`, settingsCollapsible: false, description: `automation.officialPosition.description`, state: O(Cn?.state, Bn), enabled: Bn, disabled: $(`officialPosition`) || Vn === 0, configDisabled: $(`officialPosition`), onToggle: (e2) => A(`officialPosition`, e2), statusRows: [[`automation.targetPosition`, D(Cn?.positionId || Vn)], [`automation.currentPosition`, D(Cn?.currentPositionId)], [`automation.applicationQueue`, D(Cn?.applyQueueLength ?? 0)], [`automation.nextCheck`, T(Cn?.nextRunAt, j)]], error: Hn ? void 0 : `automation.positionError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.targetPosition`), (0, S.jsxs)(`select`, { value: bt, disabled: $(`officialPosition`), onChange: (e2) => {
    let t2 = Number(e2.target.value);
    xt(e2.target.value), W(`officialPosition`, { positionId: t2 });
  }, children: [(0, S.jsx)(`option`, { value: `0`, children: M(`automation.position.none`) }), ve.map((e2) => (0, S.jsx)(`option`, { value: e2.id, children: M(e2.label) }, e2.id))] })] }) }) }), (0, S.jsxs)(y, { online: l, title: `automation.allianceTrainRide.title`, description: `automation.allianceTrainRide.description`, state: O(Y?.state, Un), enabled: Un, disabled: $(`allianceTrainRide`), onToggle: (e2) => A(`allianceTrainRide`, e2), summaryRows: [[`automation.currentCarriage`, Y?.currentCarriage], [`automation.queueCapacity`, Y?.queueLength == null || Y.maxPassenger == null ? null : `${Y.queueLength} / ${Y.maxPassenger}`]], statusRows: [[`automation.currentCarriage`, D(Y?.currentCarriage)], [`automation.queueCapacity`, `${D(Y?.queueLength ?? 0)} / ${D(Y?.maxPassenger)}`], [`automation.currentReward`, D(Y?.currentReward)], [`automation.trainVipStatus`, Y?.isVip ? M(`common.yes`) : M(`common.no`)], [`automation.trainVipCarriages`, Y?.vipCarriages?.join(`, `) || `\u2014`], [`automation.nextCheck`, T(Y?.nextRunAt, j)]], children: [(0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.normalCarriageSelection`) }), (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.normalCarriageHint`) }), (0, S.jsx)(`fieldset`, { className: `automation-compact-choice-group automation-selection-mode-choices`, "aria-label": M(`automation.normalCarriageSelection`), children: [`reward`, `fixed`].map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `train-normal-selection-mode`, checked: qn === e2, disabled: $(`allianceTrainRide`), onChange: () => {
    W(`allianceTrainRide`, { selectionMode: e2 });
  } }), (0, S.jsx)(`span`, { children: M(e2 === `reward` ? `automation.rewardSelectionMode` : `automation.fixedSelectionMode`) })] }, `normal-mode-${e2}`)) }), qn === `fixed` && (0, S.jsxs)(`fieldset`, { className: `automation-compact-choice-group automation-carriage-choices`, "aria-label": M(`automation.normalCarriageSelection`), children: [(0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, disabled: true }), (0, S.jsx)(`span`, { children: M(`automation.driver`) })] }), xe.map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: Gn.includes(e2), disabled: $(`allianceTrainRide`), onChange: (t2) => {
    W(`allianceTrainRide`, { fixedCarriageIds: t2.target.checked ? [e2] : [] });
  } }), (0, S.jsx)(`span`, { children: M(`automation.carriage${e2}`) })] }, `normal-${e2}`))] })] }), Yn && (0, S.jsxs)(`div`, { className: `automation-train-rewards automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.rewardPreferences`) }), (0, S.jsxs)(`div`, { className: `automation-preference-list`, children: [H.length === 0 && (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.noPreferredRewards`) }), $n.map((e2) => {
    let t2 = H.indexOf(e2.key), n2 = t2 >= 0;
    return (0, S.jsxs)(`div`, { className: `automation-preference-item${n2 ? ` selected` : ``}${Ct === e2.key ? ` dragging` : ``}${Tt === e2.key ? ` drag-over` : ``}`, draggable: n2, onDragStart: (t3) => {
      n2 && (t3.dataTransfer.effectAllowed = `move`, wt(e2.key));
    }, onDragOver: (t3) => {
      !n2 || !Ct || Ct === e2.key || (t3.preventDefault(), t3.dataTransfer.dropEffect = `move`, Et(e2.key));
    }, onDrop: (t3) => {
      t3.preventDefault();
      let r2 = Ct;
      if (wt(null), Et(null), !r2 || !n2) return;
      let i2 = H.indexOf(r2), a2 = H.indexOf(e2.key);
      if (i2 < 0 || a2 < 0 || i2 === a2) return;
      let o2 = [...H];
      o2.splice(a2, 0, o2.splice(i2, 1)[0]), St(o2), W(`allianceTrainRide`, { preferredRewardKeys: o2 });
    }, onDragEnd: () => {
      wt(null), Et(null);
    }, children: [(0, S.jsxs)(`label`, { className: `automation-reward-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: n2, onChange: (t3) => {
      let n3 = t3.target.checked ? [...H, e2.key] : H.filter((t4) => t4 !== e2.key);
      St(n3), W(`allianceTrainRide`, { preferredRewardKeys: n3 });
    } }), (0, S.jsx)(b, { assetPath: e2.iconPath, alt: e2.name || e2.key, className: `automation-reward-icon` }), (0, S.jsx)(`span`, { children: e2.name || e2.key })] }), (0, S.jsx)(`strong`, { className: `automation-reward-count`, children: e2.count === void 0 ? `\u2014` : `\xD7${se(e2.count)}` }), n2 && (0, S.jsxs)(`span`, { className: `automation-reward-order`, children: [(0, S.jsx)(`button`, { type: `button`, title: M(`automation.moveUp`), "aria-label": M(`automation.moveUp`), disabled: t2 === 0, onClick: () => sn(e2.key, -1), children: (0, S.jsx)(v, { name: `arrow-up` }) }), (0, S.jsx)(`button`, { type: `button`, title: M(`automation.moveDown`), "aria-label": M(`automation.moveDown`), disabled: t2 === H.length - 1, onClick: () => sn(e2.key, 1), children: (0, S.jsx)(v, { name: `arrow-down` }) }), (0, S.jsx)(`span`, { className: `automation-reward-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) })] })] }, e2.key);
  })] })] }), (0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.vipCarriageSelection`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoAcceptTrainVip`, checked: Wn, disabled: $(`allianceTrainRide`), onChange: (e2) => {
    W(`allianceTrainRide`, { autoAcceptVip: e2 });
  } }), (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.vipCarriageHint`) }), (0, S.jsx)(`fieldset`, { className: `automation-compact-choice-group automation-selection-mode-choices`, "aria-label": M(`automation.vipCarriageSelection`), children: [`reward`, `fixed`].map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `train-vip-selection-mode`, checked: Jn === e2, disabled: $(`allianceTrainRide`), onChange: () => {
    W(`allianceTrainRide`, { vipSelectionMode: e2 });
  } }), (0, S.jsx)(`span`, { children: M(e2 === `reward` ? `automation.rewardSelectionMode` : `automation.fixedSelectionMode`) })] }, `vip-mode-${e2}`)) }), Jn === `fixed` && (0, S.jsxs)(`fieldset`, { className: `automation-compact-choice-group automation-carriage-choices`, "aria-label": M(`automation.vipCarriageSelection`), children: [(0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, disabled: true }), (0, S.jsx)(`span`, { children: M(`automation.driver`) })] }), xe.map((e2) => {
    let t2 = Kn.includes(e2);
    return (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t2, disabled: $(`allianceTrainRide`) || !t2 && Kn.length >= 2, onChange: (t3) => {
      W(`allianceTrainRide`, { vipFixedCarriageIds: t3.target.checked ? [...Kn, e2] : Kn.filter((t4) => t4 !== e2) });
    } }), (0, S.jsx)(`span`, { children: M(`automation.carriage${e2}`) })] }, `vip-${e2}`);
  })] })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.additional`) }), Yn && (0, S.jsx)(g, { variant: `checkbox`, label: `automation.preferRewardQuantity`, checked: Lt, disabled: $(`allianceTrainRide`), onChange: (e2) => {
    Rt(e2), W(`allianceTrainRide`, { preferRewardQuantity: e2 });
  } }), (0, S.jsxs)(`div`, { className: `automation-actions`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.thanksMode`), (0, S.jsxs)(`select`, { value: zt, onChange: (e2) => {
    let t2 = e2.target.value;
    Bt(t2), W(`allianceTrainRide`, { thanksMode: t2 });
  }, children: [(0, S.jsx)(`option`, { value: `like`, children: M(`automation.thanksLike`) }), (0, S.jsx)(`option`, { value: `tickets`, children: M(`automation.thanksTickets`) })] })] }), zt === `tickets` && (0, S.jsxs)(`label`, { children: [M(`automation.ticketCount`), (0, S.jsxs)(`select`, { value: Vt, onChange: (e2) => {
    Ht(e2.target.value), W(`allianceTrainRide`, { ticketCount: Number(e2.target.value) });
  }, children: [(0, S.jsx)(`option`, { value: `1`, children: `1` }), (0, S.jsx)(`option`, { value: `2`, children: `2` }), (0, S.jsx)(`option`, { value: `3`, children: `3` })] })] })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.ticketFallbackLike`) })] })] }), (0, S.jsx)(y, { online: l, title: `automation.allianceHelp.title`, description: `automation.allianceHelp.description`, state: O(wn?.state, er), enabled: er, disabled: $(`allianceHelp`), onToggle: (e2) => A(`allianceHelp`, e2), actionLabel: `common.runNow`, actionBusy: _ === `allianceHelp`, onAction: () => Ne(`allianceHelp`), statusRows: [[`automation.available`, D(wn?.available ?? 0)], [`automation.processed`, D(wn?.processed ?? 0)], [`automation.lastRun`, T(wn?.lastRunAt, j)], [`automation.nextRun`, er ? E(wn?.nextRunAt, j) : `-`]] }), (0, S.jsx)(y, { online: l, title: `automation.allianceGift.title`, settingsCollapsible: false, description: `automation.allianceGift.description`, state: O(Tn?.state, tr), enabled: tr, disabled: $(`allianceGift`), onToggle: (e2) => A(`allianceGift`, e2), actionLabel: `common.runNow`, actionBusy: _ === `allianceGift`, onAction: () => Ne(`allianceGift`), statusRows: [[`automation.normalClaimed`, D(Tn?.normalClaimed ?? 0)], [`automation.advancedClaimed`, D(Tn?.advancedClaimed ?? 0)], [`automation.lastRun`, T(Tn?.lastRunAt, j)], [`automation.nextRun`, tr ? E(Tn?.nextRunAt, j) : `-`]], error: Ar ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: ft, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    pt(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= 1440 ? G(`allianceGift:intervalMinutes`, `allianceGift`, { intervalMinutes: n2 }) : R(`allianceGift:intervalMinutes`)?.pause();
  }, onBlur: () => z(`allianceGift:intervalMinutes`) })] }) }) }), (0, S.jsx)(y, { online: l, title: `automation.strongholdResource.title`, settingsCollapsible: false, description: `automation.strongholdResource.description`, state: O(En?.state, nr), enabled: nr, disabled: $(`strongholdResource`), onToggle: (e2) => A(`strongholdResource`, e2), actionLabel: `common.runNow`, actionBusy: _ === `strongholdResource`, onAction: () => Ne(`strongholdResource`), statusRows: [[`automation.available`, D(En?.available ?? 0)], [`automation.claimed`, D(En?.claimed ?? 0)], [`automation.lastRun`, T(En?.lastRunAt, j)], [`automation.nextRun`, nr ? E(En?.nextRunAt, j) : `-`]], error: Nr ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: mt, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    ht(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= 1440 ? G(`strongholdResource:intervalMinutes`, `strongholdResource`, { intervalMinutes: n2 }) : R(`strongholdResource:intervalMinutes`)?.pause();
  }, onBlur: () => z(`strongholdResource:intervalMinutes`) })] }) }) }), (0, S.jsx)(y, { online: l, title: `automation.allianceCenterResource.title`, settingsCollapsible: false, description: `automation.allianceCenterResource.description`, state: O(Dn?.state, rr), enabled: rr, disabled: $(`allianceCenterResource`), onToggle: (e2) => A(`allianceCenterResource`, e2), actionLabel: `common.runNow`, actionBusy: _ === `allianceCenterResource`, onAction: () => Ne(`allianceCenterResource`), statusRows: [[`automation.available`, D(Dn?.available ?? 0)], [`automation.claimed`, D(Dn?.claimed ?? 0)], [`automation.lastRun`, T(Dn?.lastRunAt, j)], [`automation.nextRun`, rr ? E(Dn?.nextRunAt, j) : `-`]], error: Pr ? void 0 : `automation.intervalError`, children: (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.intervalMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 1, max: 1440, step: 1, value: gt, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    _t(t2), Number.isInteger(n2) && n2 >= 1 && n2 <= 1440 ? G(`allianceCenterResource:intervalMinutes`, `allianceCenterResource`, { intervalMinutes: n2 }) : R(`allianceCenterResource:intervalMinutes`)?.pause();
  }, onBlur: () => z(`allianceCenterResource:intervalMinutes`) })] }) }) }), (0, S.jsxs)(y, { online: l, title: `automation.allianceGather.title`, description: `automation.allianceGather.description`, state: O(On?.state, ir), enabled: ir, disabled: $(`allianceGather`), onToggle: (e2) => {
    if (e2 && ar.length === 0) {
      It(`automation.allianceGatherSquadRequired`);
      return;
    }
    It(``), A(`allianceGather`, e2);
  }, actionLabel: `common.runNow`, actionBusy: _ === `allianceGather`, onAction: () => {
    if (ar.length === 0) {
      It(`automation.allianceGatherSquadRequired`);
      return;
    }
    It(``), Ne(`allianceGather`);
  }, error: Ft || void 0, children: [(0, S.jsxs)(`div`, { className: `automation-status-grid`, children: [(0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.allianceGatherAvailable`) }), (0, S.jsx)(`strong`, { children: D(On?.available ?? 0) })] }), (0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.dispatched`) }), (0, S.jsx)(`strong`, { children: D(On?.dispatched ?? 0) })] }), (0, S.jsxs)(`div`, { className: `automation-status-row`, children: [(0, S.jsx)(`span`, { children: M(`automation.lastRun`) }), (0, S.jsx)(`strong`, { children: T(On?.lastRunAt, j) })] })] }), (0, S.jsx)(`p`, { className: `muted`, children: M(`automation.allianceGatherSquadPriority`) }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group automation-squad-priority`, role: `group`, "aria-label": M(`automation.allianceGatherSquadPriority`), children: sr.map((e2) => {
    let t2 = or.has(e2);
    return (0, S.jsxs)(`div`, { className: `automation-squad-priority-item${t2 ? ` selected` : ``}${jt === e2 ? ` dragging` : ``}${Nt === e2 ? ` drag-over` : ``}`, draggable: t2, onDragStart: (n2) => {
      t2 && (n2.dataTransfer.effectAllowed = `move`, n2.dataTransfer.setData(`text/plain`, String(e2)), Mt(e2));
    }, onDragOver: (n2) => {
      !t2 || jt === null || jt === e2 || (n2.preventDefault(), n2.dataTransfer.dropEffect = `move`, Pt(e2));
    }, onDrop: (n2) => {
      n2.preventDefault();
      let r2 = jt;
      if (Mt(null), Pt(null), r2 === null || !t2) return;
      let i2 = Se(ar, r2, e2);
      i2 !== ar && W(`allianceGather`, { squadPriority: i2 });
    }, onDragEnd: () => {
      Mt(null), Pt(null);
    }, children: [(0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: t2, onChange: (t3) => {
      let n2 = t3.target.checked ? [...ar, e2] : ar.filter((t4) => t4 !== e2);
      if (ir && n2.length === 0) {
        It(`automation.allianceGatherSquadRequired`);
        return;
      }
      It(``), W(`allianceGather`, { squadPriority: n2 });
    } }), (0, S.jsx)(`span`, { children: M(`automation.squad`, { index: e2 }) })] }), t2 ? (0, S.jsx)(`span`, { className: `automation-squad-drag-handle`, "aria-hidden": `true`, children: (0, S.jsx)(v, { name: `drag` }) }) : null] }, e2);
  }) })] })] }) }), V.has(`daily`) && (0, S.jsx)(x.Activity, { mode: B === `daily` ? `visible` : `hidden`, children: (0, S.jsxs)(S.Fragment, { children: [(0, S.jsxs)(y, { online: l, title: `automation.railway.title`, description: `automation.railway.description`, state: O(X?.state, cr), enabled: cr, disabled: $(`railway`), onToggle: (e2) => {
    W(`railway`, { enabled: e2 });
  }, actionLabel: X?.running ? `common.stop` : `automation.departAll`, actionBusy: _ === `railway`, onAction: () => Ie(`railway`, X?.running === true, { weeklyQualities: Cr, departWhenTicketsInsufficient: lr }), summaryRows: [[`automation.departed`, X?.departed], [`automation.pendingClaims`, X?.pendingClaims]], statusRows: [[`automation.nextRun`, cr ? E(X?.nextScheduledAt, j) : `-`], [`automation.qualifiedTotal`, `${D(X?.qualified ?? 0)} / ${D(X?.total ?? 0)}`], [`automation.refreshed`, D(X?.refreshed ?? 0)], [`automation.departed`, D(X?.departed ?? 0)], [`automation.claimed`, D(X?.claimed ?? 0)], [`automation.pendingClaims`, D(X?.pendingClaims ?? 0)], [`automation.nextClaim`, E(X?.nextContinuationAt, j)], [`automation.batchDeparture`, Ce(X?.capabilities?.batchDeparture, `common.degraded`)]], error: Or ? void 0 : `automation.dailyDelayError`, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.schedule`) }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.delayAfterResetMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 1440, step: 1, value: ct, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    lt(t2), Number.isInteger(n2) && n2 >= 0 && n2 <= 1440 ? G(`railway:delayMinutes`, `railway`, { delayMinutes: n2 }) : R(`railway:delayMinutes`)?.pause();
  }, onBlur: () => z(`railway:delayMinutes`) })] }) })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.quality`) }), zr(`railway`, Cr, X?.running)] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.departure`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.railwayDepartWhenTicketsInsufficient`, checked: lr, disabled: $(`railway`) || X?.running, onChange: (e2) => {
    W(`railway`, { departWhenTicketsInsufficient: e2 });
  } })] })] }), (0, S.jsxs)(y, { online: l, title: `automation.secretTask.title`, description: `automation.secretTask.description`, state: O(Z?.state, fr), enabled: ur, active: fr, disabled: $(`dispatch`), onToggle: (e2) => {
    W(`dispatch`, { autoExecute: e2 });
  }, actionLabel: Z?.running ? `common.stop` : `automation.dispatchAll`, actionBusy: _ === `dispatch`, onAction: () => Ie(`dispatch`, Z?.running === true, { runNow: true, weeklyQualities: wr }), summaryRows: [[`automation.dispatched`, Z?.dispatched], [`automation.pendingClaims`, Z?.pendingClaims]], statusRows: [[`automation.nextRun`, fr ? E(Z?.nextScheduledAt, j) : `-`], [`automation.qualifiedTotal`, `${D(Z?.qualified ?? Fr?.qualified ?? 0)} / ${D(Z?.available ?? Fr?.available ?? 0)}`], [`automation.dispatched`, D(Z?.dispatched ?? 0)], [`automation.refreshed`, D(Z?.refreshed ?? 0)], [`automation.claimed`, D(Z?.claimed ?? 0)], [`automation.pendingClaims`, D(Z?.pendingClaims ?? 0)], [`automation.nextClaim`, E(Z?.nextContinuationAt, j)], [`automation.superRefresh`, Ce(Z?.capabilities?.superRefresh, `automation.normalRefresh`)]], error: kr ? void 0 : `automation.dailyDelayError`, children: [(0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`squad.afkExecutionSettings`) }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.autoCollectRewards`, checked: dr, disabled: $(`dispatch`), onChange: (e2) => {
    W(`dispatch`, { collectRewards: e2 });
  } }), (0, S.jsx)(`div`, { className: `automation-actions`, children: (0, S.jsxs)(`label`, { children: [M(`automation.delayAfterResetMinutes`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 1440, step: 1, value: ut, onChange: (e2) => {
    let t2 = e2.target.value, n2 = Number(t2);
    dt(t2), Number.isInteger(n2) && n2 >= 0 && n2 <= 1440 ? G(`dispatch:delayMinutes`, `dispatch`, { delayMinutes: n2 }) : R(`dispatch:delayMinutes`)?.pause();
  }, onBlur: () => z(`dispatch:delayMinutes`) })] }) })] }), (0, S.jsxs)(`section`, { className: `automation-settings-section`, children: [(0, S.jsx)(`h3`, { children: M(`automation.section.quality`) }), zr(`dispatch`, wr, Z?.running)] }), (0, S.jsxs)(`div`, { className: `automation-settings-section`, children: [(0, S.jsx)(g, { label: `automation.dispatchAssist`, checked: vr, disabled: !l || rn !== ``, onChange: Wr }), vr && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`div`, { className: `automation-squad-choices`, children: ye.map((e2) => (0, S.jsxs)(`label`, { children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: qt.includes(e2), onChange: (t2) => {
    let n2 = t2.target.checked ? [...qt, e2] : qt.filter((t3) => t3 !== e2);
    Jt(n2), Wr(vr, n2);
  } }), e2 === `special` ? M(`automation.assistQuality.special`) : e2.toUpperCase()] }, e2)) }), (0, S.jsxs)(`div`, { className: `automation-form-grid`, children: [(0, S.jsxs)(`label`, { children: [M(`automation.minDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 86400, step: 1, value: Yt, onChange: (e2) => {
    let t2 = e2.target.value;
    Xt(t2), Gr(Number(t2), br, xr);
  }, onBlur: () => z(`dispatchAssist:settings`) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.maxDelaySeconds`), (0, S.jsx)(`input`, { type: `number`, min: 0, max: 86400, step: 1, value: Zt, onChange: (e2) => {
    let t2 = e2.target.value;
    Qt(t2), Gr(yr, Number(t2), xr);
  }, onBlur: () => z(`dispatchAssist:settings`) })] }), (0, S.jsxs)(`label`, { children: [M(`automation.assistIntervalSeconds`), (0, S.jsx)(`input`, { type: `number`, min: 5, max: 300, step: 1, value: $t, onChange: (e2) => {
    let t2 = e2.target.value;
    en(t2), Gr(yr, br, Number(t2));
  }, onBlur: () => z(`dispatchAssist:settings`) })] })] })] }), !vr && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`strong`, { children: M(`automation.allySecretTasks`) }), (0, S.jsxs)(`div`, { className: `automation-task-list`, children: [(Gt?.tasks ?? []).map((e2) => {
    let t2 = Sr.get(e2.uuid), n2 = t2 && [`scheduled`, `waiting_connection`, `retry_wait`, `running`].includes(t2.scheduleStatus);
    return (0, S.jsxs)(`label`, { className: `automation-task-row automation-assist-task-row`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: tn.includes(e2.uuid), disabled: !!n2 || rn !== ``, onChange: (t3) => nn((n3) => t3.target.checked ? [...n3, e2.uuid] : n3.filter((t4) => t4 !== e2.uuid)) }), (0, S.jsxs)(`span`, { className: `automation-assist-task-main`, children: [(0, S.jsxs)(`span`, { className: `automation-assist-task-title`, children: [(0, S.jsx)(`strong`, { children: Kr(e2) }), Yr(e2)] }), Xr(e2)] }), (0, S.jsx)(`span`, { children: e2.helpAvailable ? M(`automation.helpAvailable`) : T(e2.completionTime, j) }), (0, S.jsx)(`span`, { children: M(n2 ? `automation.assistStatus.${t2.scheduleStatus}` : `automation.assistStatus.untracked`) })] }, e2.uuid);
  }), !Gt?.tasks.length && (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.noAllySecretTasks`) })] }), (0, S.jsx)(`button`, { disabled: !tn.length || rn !== ``, onClick: () => void qr(), children: M(`automation.scheduleSelectedHelp`) }), (0, S.jsx)(`div`, { className: `automation-assist-queue`, children: (Gt?.jobs ?? []).filter((e2) => e2.scheduleSource === `manual` && [`scheduled`, `waiting_connection`, `retry_wait`, `running`, `failed`, `expired`].includes(e2.scheduleStatus)).map((e2) => (0, S.jsxs)(`div`, { className: `automation-inline-status`, children: [(0, S.jsxs)(`span`, { children: [Kr(e2), ` \xB7 `, M(`automation.assistStatus.${e2.scheduleStatus}`), ` \xB7 `, T(e2.assistAt, j)] }), [`scheduled`, `waiting_connection`, `retry_wait`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`cancel`, e2.uuid), children: M(`common.cancel`) }), [`failed`, `expired`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`retry`, e2.uuid), children: M(`common.retry`) })] }, e2.uuid)) })] }), vr && on && (0, S.jsx)(`p`, { className: `error-text`, children: on })] })] }), (0, S.jsxs)(y, { online: l, title: `automation.ghost.title`, description: `automation.ghost.description`, state: O(kn?.state, _r), enabled: pr, active: _r, toggleLabel: `automation.ghost.autoStartOwn`, disabled: $(`ghostRecon`), onToggle: (e2) => {
    W(`ghostRecon`, { autoStartOwn: e2 });
  }, statusRows: [[`automation.nextClaim`, hr ? E(kn?.nextClaimAt, j) : `-`]], children: [(0, S.jsx)(g, { label: `automation.ghost.autoJoinAlliance`, checked: mr, disabled: $(`ghostRecon`), onChange: (e2) => {
    W(`ghostRecon`, { autoJoinAlliance: e2 });
  } }), mr && (0, S.jsxs)(`div`, { className: `automation-section`, children: [(0, S.jsx)(`strong`, { children: M(`automation.ghost.allianceFilter`) }), (0, S.jsx)(`div`, { className: `automation-compact-choice-group`, children: be.map((e2) => (0, S.jsxs)(`label`, { className: `automation-compact-choice`, children: [(0, S.jsx)(`input`, { type: `radio`, name: `ghost-alliance-filter`, value: e2, checked: gr === e2, disabled: $(`ghostRecon`), onChange: () => {
    W(`ghostRecon`, { allianceFilter: e2 });
  } }), (0, S.jsx)(`span`, { children: M(`automation.ghost.filter.${e2}`) })] }, e2)) })] }), (0, S.jsx)(g, { variant: `checkbox`, label: `automation.ghost.autoClaimRewards`, checked: hr, disabled: $(`ghostRecon`), onChange: (e2) => {
    W(`ghostRecon`, { autoClaimRewards: e2 });
  } }), (0, S.jsxs)(`div`, { className: `automation-inline-status`, children: [(0, S.jsxs)(`span`, { children: [M(`automation.ghost.ownPending`), `: `, D(kn?.ownPending ?? 0)] }), (0, S.jsxs)(`span`, { children: [M(`automation.ghost.allianceCandidates`), `: `, D(kn?.allianceCandidates ?? 0)] }), (0, S.jsxs)(`span`, { children: [M(`automation.claimed`), `: `, D(kn?.claimed ?? 0)] })] })] })] }) })] })] });
}
