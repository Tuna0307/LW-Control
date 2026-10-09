import { useEffect, useMemo, useRef, useState } from "react";
import * as A from "react/jsx-runtime";
import { useI18n } from "./i18n.jsx";
import { validJoinRestrictions as se } from "./previewAfkContracts.js";
import { previewMemberFixture } from "./previewAfkCloseoutFixtures.js";
function JoinModal({ className, label, onClose, children }) {
  const ref = useRef(null);
  const pointerStartedOnBackdrop = useRef(false);
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog.showModal();
    return () => {
      dialog.close();
      if (previous?.isConnected) previous.focus({ preventScroll: true });
    };
  }, []);
  return A.jsx("dialog", {
    ref,
    className: `app-dialog ${className}`,
    role: "dialog",
    "aria-modal": true,
    "aria-label": label,
    "aria-busy": false,
    onCancel: (event) => {
      event.preventDefault();
      onClose();
    },
    onKeyDown: (event) => {
      event.stopPropagation();
      if (event.key !== "Tab") return;
      const controls = [...event.currentTarget.querySelectorAll("button, [href], input, select, textarea, [tabindex]")]
        .filter((element) => element.tabIndex >= 0 && !element.matches(":disabled") && element.getClientRects().length > 0);
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (!first) event.preventDefault();
      else if (event.shiftKey && (document.activeElement === first || !controls.includes(document.activeElement))) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && (document.activeElement === last || !controls.includes(document.activeElement))) {
        event.preventDefault();
        first.focus();
      }
    },
    onPointerDown: (event) => {
      pointerStartedOnBackdrop.current = event.target === event.currentTarget;
    },
    onClick: () => {
      pointerStartedOnBackdrop.current = false;
    },
    children,
  });
}
function RallyJoinSettings({
  value: e,
  onChange: t,
  disabled: r = false,
  label: i = "squad.afkJoinConditions",
  previewState = "",
  ToggleRow
}) {
  const { t: o } = useI18n();
  const fixture = previewMemberFixture(previewState);
  const n = fixture.online;
  const x = fixture.ready ? { membersReady: true, members: fixture.members, selfUid: fixture.selfUid } : null;
  const v = fixture.failed;
  const [f, p] = useState(false);
  const [m, h] = useState("");
  const [g, _] = useState(/* @__PURE__ */ new Set());
  const S = x?.membersReady ? x.members.filter((member) => member.uid !== x.selfUid) : [];
  const C = new Map((x?.membersReady ? x.members : []).map((member) => [member.uid, member]));
  const T = e.leaders.filter((member) => !C.has(member.uid));
  const E = m.trim().toLowerCase();
  const ee = S.filter((member) => member.name.toLowerCase().includes(E) || member.uid.includes(E));
  const w = useMemo(() => ({ label, ...props }) => {
    const { t: t2 } = useI18n();
    return A.jsx(ToggleRow, { label: t2(label), ...props });
  }, [ToggleRow]);
  const y = JoinModal;
  return (0, A.jsxs)(`section`, { className: `rally-join-settings`, children: [(0, A.jsx)(w, { label: i, checked: e.enabled, disabled: r, onChange: (n2) => t({ ...e, enabled: n2 }) }), e.enabled && (0, A.jsxs)(`fieldset`, { disabled: r, children: [(0, A.jsxs)(`div`, { className: `monster-afk-basic-grid`, children: [(0, A.jsxs)(`label`, { className: `monster-afk-field-wide`, children: [o(`squad.join.mode`), (0, A.jsxs)(`select`, { "aria-label": o(`squad.join.mode`), value: e.mode, onChange: (n2) => t({ ...e, mode: n2.target.value }), children: [(0, A.jsx)(`option`, { value: `slot`, children: o(`squad.join.mode.slot`) }), (0, A.jsx)(`option`, { value: `delay`, children: o(`squad.join.mode.delay`) })] })] }), [e.mode === `delay` ? `delaySeconds` : `slotRange`].map((n2) => (0, A.jsx)(`div`, { className: `rally-join-range`, children: [0, 1].map((r2) => (0, A.jsxs)(`label`, { children: [o(`squad.join.${n2}.${r2 === 0 ? `min` : `max`}`), (0, A.jsx)(`input`, { type: `number`, min: n2 === `slotRange` ? 2 : 0, max: n2 === `slotRange` ? 5 : 6e3, step: n2 === `slotRange` ? 1 : 0.01, value: Number.isNaN(e[n2][r2]) ? `` : e[n2][r2], onChange: (i2) => {
    let a = [...e[n2]], o2 = i2.target.valueAsNumber;
    a[r2] = n2 === `slotRange` ? Math.min(5, Math.max(2, Number.isFinite(o2) ? Math.trunc(o2) : 2)) : o2, t({ ...e, [n2]: a });
  } })] }, r2)) }, n2)), e.mode === `slot` && (0, A.jsxs)(`label`, { children: [o(`squad.join.slotDelay`), (0, A.jsx)(`input`, { type: `number`, min: 0, max: 6e3, step: 0.01, value: Number.isNaN(e.slotDelaySeconds) ? `` : e.slotDelaySeconds, onChange: (n2) => t({ ...e, slotDelaySeconds: n2.target.valueAsNumber }) })] }), (0, A.jsxs)(`label`, { children: [o(`squad.join.maxWait`), (0, A.jsx)(`input`, { type: `number`, min: 0, step: 1, value: Number.isNaN(e.maxWaitMinutes) ? `` : e.maxWaitMinutes, onChange: (n2) => t({ ...e, maxWaitMinutes: n2.target.valueAsNumber }) })] }), (0, A.jsxs)(`label`, { children: [o(`squad.join.list`), (0, A.jsx)(`select`, { "aria-label": o(`squad.join.list`), value: e.leaderListMode, onChange: (n2) => t({ ...e, leaderListMode: n2.target.value }), children: [`off`, `blacklist`, `whitelist`].map((e2) => (0, A.jsx)(`option`, { value: e2, children: o(`squad.join.list.${e2}`) }, e2)) })] })] }), (0, A.jsx)(`p`, { className: `muted`, children: o(`squad.join.hint`) }), (0, A.jsx)(`p`, { className: `muted`, children: o(`squad.join.hint.${e.mode}`) }), e.leaderListMode !== `off` && (0, A.jsxs)(`div`, { className: `rally-join-leaders`, children: [(0, A.jsx)(`button`, { type: `button`, disabled: !x?.membersReady, onClick: () => {
    _(new Set(e.leaders.map((e2) => e2.uid))), h(``), p(true);
  }, children: o(`squad.join.chooseMembers`) }), !x?.membersReady && (0, A.jsx)(`span`, { className: `muted`, children: o(n ? v ? `squad.join.membersFailed` : `squad.join.membersLoading` : `squad.join.membersOffline`) }), e.leaders.map((n2, r2) => (0, A.jsxs)(`div`, { className: `rally-join-leader`, children: [(0, A.jsxs)(`span`, { className: `rally-join-member-name`, children: [C.get(n2.uid)?.name || n2.name || n2.uid, (0, A.jsxs)(`small`, { children: [n2.uid, x?.membersReady && !C.has(n2.uid) ? ` \xB7 ${o(`squad.join.memberLeft`)}` : ``] })] }), (0, A.jsx)(`button`, { type: `button`, className: `danger`, onClick: () => t({ ...e, leaders: e.leaders.filter((e2, t2) => t2 !== r2) }), children: o(`common.delete`) })] }, n2.uid)), x?.membersReady && T.length > 0 && (0, A.jsx)(`span`, { className: `muted`, children: o(`squad.join.memberLeftHint`) }), e.leaderListMode === `whitelist` && !e.leaders.length && (0, A.jsx)(`span`, { className: `muted`, children: o(`squad.join.emptyWhitelist`) })] }), (0, A.jsx)(w, { label: `squad.join.skipSolo`, checked: e.skipSoloLeader, onChange: (n2) => t({ ...e, skipSoloLeader: n2 }) }), (0, A.jsx)(w, { label: `squad.join.skipKicked`, checked: e.skipKicked, onChange: (n2) => t({ ...e, skipKicked: n2 }) }), !se(e) && (0, A.jsx)(`p`, { role: `alert`, className: `status-error`, children: o(`squad.join.invalid`) })] }), e.enabled && f && (0, A.jsx)(y, { className: `garrison-modal-backdrop`, label: o(`squad.join.chooseMembers`), onClose: () => p(false), children: (0, A.jsxs)(`section`, { className: `garrison-modal`, children: [(0, A.jsxs)(`div`, { className: `garrison-modal-heading`, children: [(0, A.jsx)(`strong`, { children: o(`squad.join.chooseMembers`) }), (0, A.jsx)(`button`, { type: `button`, onClick: () => p(false), children: o(`common.cancel`) })] }), (0, A.jsx)(`input`, { "aria-label": o(`squad.join.searchMembers`), placeholder: o(`squad.join.searchMembers`), value: m, onChange: (e2) => h(e2.target.value), autoFocus: true }), (0, A.jsxs)(`div`, { className: `garrison-ally-list`, children: [ee.map((e2) => (0, A.jsxs)(`label`, { children: [(0, A.jsx)(`input`, { type: `checkbox`, checked: g.has(e2.uid), onChange: (t2) => _((n2) => {
    let r2 = new Set(n2);
    return t2.target.checked ? r2.add(e2.uid) : r2.delete(e2.uid), r2;
  }) }), (0, A.jsxs)(`span`, { children: [(0, A.jsx)(`strong`, { children: e2.name || e2.uid }), (0, A.jsx)(`small`, { children: e2.uid })] })] }, e2.uid)), !ee.length && (0, A.jsx)(`span`, { className: `muted`, children: o(`squad.join.noMembers`) })] }), (0, A.jsx)(`div`, { className: `garrison-modal-actions`, children: (0, A.jsx)(`button`, { type: `button`, className: `primary-action`, disabled: !x?.membersReady || r, onClick: () => {
    if (!x?.membersReady) return;
    let n2 = e.leaders.filter((e2) => !C.has(e2.uid) || e2.uid === x.selfUid);
    t({ ...e, leaders: [...n2, ...S.filter((e2) => g.has(e2.uid)).map((e2) => ({ uid: e2.uid, name: e2.name }))] }), p(false);
  }, children: o(`squad.join.confirmMembers`) }) })] }) })] });
}
export {
  RallyJoinSettings
};
