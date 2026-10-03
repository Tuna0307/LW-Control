import * as S from "react/jsx-runtime";
import { useI18n } from "./i18n.jsx";
function b({ className, alt }) {
  return S.jsx("span", { className: className + " game-asset-placeholder", role: alt ? "img" : void 0, "aria-label": alt || void 0 });
}
function e(e2) {
  let t = Number(e2);
  if (!Number.isFinite(t)) return `-`;
  let n = Math.abs(t), r = t < 0 ? `-` : ``;
  return n >= 1e9 ? `${r}${(n / 1e9).toFixed(1)}G` : n >= 1e6 ? `${r}${(n / 1e6).toFixed(1)}M` : n >= 1e3 ? `${r}${(n / 1e3).toFixed(1)}K` : String(t);
}
const se = e;
function DispatchAssistManual({ fixture: Gt, selected: tn, onSelectionChange: nn, onSchedule: qr, onJobAction: Jr }) {
  const { t: M, language: j } = useI18n();
  const Sr = new Map(Gt.jobs.map((job) => [job.uuid, job]));
  const rn = Gt.busy ? "fixture-busy" : "";
  const vr = false;
  function D(e2) {
    return e2 == null || e2 === `` ? `-` : String(e2);
  }
  function T(e2, t) {
    return e2 ? new Date(e2).toLocaleString(t) : `-`;
  }
  function Kr(e2) {
    let t = e2.qualityKey.toUpperCase(), n = e2.isSpecial ? ` \xB7 ${M(`automation.assistQuality.special`)}` : ``;
    return `${e2.ownerName || e2.ownerUid} \xB7 ${t}${n}`;
  }
  function Yr(e2) {
    return e2.starIcons?.length ? (0, S.jsx)(`span`, { className: `automation-assist-stars`, "aria-label": `${e2.star}`, children: e2.starIcons.map((e3, t) => (0, S.jsx)(b, { assetPath: e3, alt: ``, className: `automation-assist-star` }, `${e3}:${t}`)) }) : null;
  }
  function Xr(e2) {
    return (0, S.jsx)(`span`, { className: `automation-assist-rewards`, children: (e2.items ?? []).map((e3, t) => {
      let n = `${e3.name || e3.key} \xD7${D(e3.count)}`;
      return (0, S.jsxs)(`span`, { className: `map-reward-item`, title: n, "aria-label": n, children: [(0, S.jsx)(b, { assetPath: e3.iconPath, alt: e3.name || e3.key, className: `map-reward-icon` }), (0, S.jsxs)(`strong`, { children: [`\xD7`, se(e3.count)] })] }, `${e3.key}:${t}`);
    }) });
  }
  return !vr && (0, S.jsxs)(S.Fragment, { children: [(0, S.jsx)(`strong`, { children: M(`automation.allySecretTasks`) }), (0, S.jsxs)(`div`, { className: `automation-task-list`, children: [(Gt?.tasks ?? []).map((e2) => {
    let t = Sr.get(e2.uuid), n = t && [`scheduled`, `waiting_connection`, `retry_wait`, `running`].includes(t.scheduleStatus);
    return (0, S.jsxs)(`label`, { className: `automation-task-row automation-assist-task-row`, children: [(0, S.jsx)(`input`, { type: `checkbox`, checked: tn.includes(e2.uuid), disabled: !!n || rn !== ``, onChange: (t2) => nn((n2) => t2.target.checked ? [...n2, e2.uuid] : n2.filter((t3) => t3 !== e2.uuid)) }), (0, S.jsxs)(`span`, { className: `automation-assist-task-main`, children: [(0, S.jsxs)(`span`, { className: `automation-assist-task-title`, children: [(0, S.jsx)(`strong`, { children: Kr(e2) }), Yr(e2)] }), Xr(e2)] }), (0, S.jsx)(`span`, { children: e2.helpAvailable ? M(`automation.helpAvailable`) : T(e2.completionTime, j) }), (0, S.jsx)(`span`, { children: M(n ? `automation.assistStatus.${t.scheduleStatus}` : `automation.assistStatus.untracked`) })] }, e2.uuid);
  }), !Gt?.tasks.length && (0, S.jsx)(`span`, { className: `muted`, children: M(`automation.noAllySecretTasks`) })] }), (0, S.jsx)(`button`, { disabled: !tn.length || rn !== ``, onClick: () => void qr(), children: M(`automation.scheduleSelectedHelp`) }), (0, S.jsx)(`div`, { className: `automation-assist-queue`, children: (Gt?.jobs ?? []).filter((e2) => e2.scheduleSource === `manual` && [`scheduled`, `waiting_connection`, `retry_wait`, `running`, `failed`, `expired`].includes(e2.scheduleStatus)).map((e2) => (0, S.jsxs)(`div`, { className: `automation-inline-status`, children: [(0, S.jsxs)(`span`, { children: [Kr(e2), ` \xB7 `, M(`automation.assistStatus.${e2.scheduleStatus}`), ` \xB7 `, T(e2.assistAt, j)] }), [`scheduled`, `waiting_connection`, `retry_wait`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`cancel`, e2.uuid), children: M(`common.cancel`) }), [`failed`, `expired`].includes(e2.scheduleStatus) && (0, S.jsx)(`button`, { disabled: rn !== ``, onClick: () => void Jr(`retry`, e2.uuid), children: M(`common.retry`) })] }, e2.uuid)) })] });
}
export {
  DispatchAssistManual
};
