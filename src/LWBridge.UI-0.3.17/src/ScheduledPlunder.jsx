// Recovered Map "Scheduled Plunder" tab presentation (LWBridge 0.3.17).
// Source: evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js, components `ot`
// (Dispatch / Ghost group) and `st` (Truck group); see evidence/.../LWB317-UI-MAP-INTERACTIONS-001/scheduled/.
// Presentation only: this component never schedules, cancels, shares or claims anything. Row actions are
// forwarded to the caller's handlers, and only when `actionsEnabled` (the canonical runtime availability fence)
// is true; otherwise every action button is additionally disabled and carries no handler.

import { useI18n } from "./i18n.jsx";
import { GameAssetImage } from "./GameAssetImage.jsx";
import { mapDate, mapNumber, mapQuality, mapRewardCount, mapRewardName, truckMaxLootCount, truckState } from "./mapTablePresentation.js";
import {
  dispatchGroupJobs,
  ghostGroupJobs,
  plunderCancelVisible,
  plunderClearDisabled,
  plunderJobKey,
  scheduledPlunderCount,
  secretPlunderStatus,
  truckBusyKey,
  truckInflightKeys,
  truckPlunderAgainVisible,
  truckPlunderResult,
  truckRowKey,
} from "./mapPlunderPresentation.js";

export { scheduledPlunderCount };

const EMPTY_GAME_TEXTS = Object.freeze({});
// Original column widths (+8 cell padding): `[78, 150, 92, 280, 135, 135, 180, 74].map(w => w + 8)` and
// `[78, 150, 86, 110, 200, 280, 92].map(w => w + 8)`.
const SECRET_WIDTHS = [78, 150, 92, 280, 135, 135, 180, 74].map((width) => width + 8);
const TRUCK_WIDTHS = [78, 150, 86, 110, 200, 280, 92].map((width) => width + 8);

// Original reward list cell shared by both groups and canonical Map tables.
function PlunderRewards({ rewards, gameTexts, language }) {
  return rewards?.length ? (
    <span className="map-reward-list map-reward-list--retained">
      {rewards.map((item) => {
        const name = mapRewardName(item, gameTexts);
        const description = `${name} ×${mapNumber(item.count, language)}`;
        return (
          <span className="map-reward-item" key={item.key} title={description} aria-label={description}>
            <GameAssetImage assetPath={item.iconPath} alt={name} className="map-reward-icon" />
            <strong>×{mapRewardCount(item.count)}</strong>
          </span>
        );
      })}
    </span>
  ) : "-";
}

// Original `ot`: Dispatch (kind "dispatch") and Ghost Scout (kind "ghost") scheduled group.
export function SecretPlunderGroup({ kind = "dispatch", jobs, gameTexts = EMPTY_GAME_TEXTS, currentTime, online, busyKey, actionsEnabled = false, onCancel, onClear }) {
  const { language, t } = useI18n();
  const title = t(kind === "ghost" ? "map.ghostScout" : "map.secretTask");
  return (
    <section className="map-scheduled-group map-scheduled-group--secret">
      <header className="map-scheduled-group-title">
        <strong className="map-scheduled-kind map-scheduled-kind--secret">{title}</strong>
        <span>{t("common.itemCount", { count: jobs.length })}</span>
        <button disabled={!actionsEnabled || plunderClearDisabled(jobs, busyKey)} onClick={actionsEnabled ? () => onClear(kind) : undefined}>{t("map.clearPlunderHistory")}</button>
      </header>
      <div className="map-table-scroll">
        <table className="map-table map-table--scheduled-plunder map-table--scheduled-secret" style={{ minWidth: SECRET_WIDTHS.reduce((sum, width) => sum + width, 0) }} aria-label={title}>
          <colgroup>{SECRET_WIDTHS.map((width, index) => <col key={index} style={{ width }} />)}</colgroup>
          <thead>
            <tr className="map-row map-head">
              <th scope="col">{t("map.server")}</th>
              <th scope="col">{t("map.owner")}</th>
              <th scope="col">{t("map.quality")}</th>
              <th scope="col">{t("map.rewards")}</th>
              <th scope="col">{t("map.completionTime")}</th>
              <th scope="col">{t("map.plunderAt")}</th>
              <th scope="col">{t("map.plunderResult")}</th>
              <th scope="col">{t("map.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {jobs.map((job) => {
              const key = plunderJobKey(job);
              const [statusClass, statusText] = secretPlunderStatus(job, { t, gameTexts, online, now: currentTime });
              const cancellable = plunderCancelVisible(job);
              return (
                <tr className="map-row" key={key}>
                  <td>#{job.serverId}</td>
                  <td>{job.ownerName || job.ownerUid || job.uuid}</td>
                  <td>{job.isSpecial ? t("map.specialQuality") : mapQuality(job.quality)}</td>
                  <td className="map-reward-cell"><PlunderRewards rewards={job.rewards} gameTexts={gameTexts} language={language} /></td>
                  <td>{mapDate(job.completionTime, language)}</td>
                  <td>{mapDate(job.plunderAt, language)}</td>
                  <td className="map-plunder-result" title={statusText}><span className={`map-task-status ${statusClass}`}>{statusText}</span></td>
                  <td className="map-plunder-actions">
                    {cancellable && <button disabled={!actionsEnabled || busyKey === key} onClick={actionsEnabled ? () => onCancel(job) : undefined}>{t("common.cancel")}</button>}
                    {!cancellable && "-"}
                  </td>
                </tr>
              );
            })}
            {jobs.length === 0 && <tr><td className="map-empty" colSpan={SECRET_WIDTHS.length}>{t("map.empty")}</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  );
}

// Original `st`: Truck scheduled group.
export function TruckPlunderGroup({ jobs, gameTexts = EMPTY_GAME_TEXTS, currentTime, online, busyKey, actionsEnabled = false, onCancel, onPlunderAgain, onClear }) {
  const { language, t } = useI18n();
  const inflight = truckInflightKeys(jobs);
  return (
    <section className="map-scheduled-group map-scheduled-group--truck">
      <header className="map-scheduled-group-title">
        <strong className="map-scheduled-kind map-scheduled-kind--truck">{t("map.truck")}</strong>
        <span>{t("common.itemCount", { count: jobs.length })}</span>
        <button disabled={!actionsEnabled || plunderClearDisabled(jobs, busyKey)} onClick={actionsEnabled ? () => onClear("truck") : undefined}>{t("map.clearPlunderHistory")}</button>
      </header>
      <div className="map-table-scroll">
        <table className="map-table map-table--scheduled-plunder map-table--scheduled-truck" style={{ minWidth: TRUCK_WIDTHS.reduce((sum, width) => sum + width, 0) }} aria-label={t("map.truck")}>
          <colgroup>{TRUCK_WIDTHS.map((width, index) => <col key={index} style={{ width }} />)}</colgroup>
          <thead>
            <tr className="map-row map-head">
              <th scope="col">{t("map.server")}</th>
              <th scope="col">{t("map.playerAlliance")}</th>
              <th scope="col">{t("map.quality")}</th>
              <th scope="col">{t("map.plunderCount")}</th>
              <th scope="col">{t("map.plunderResult")}</th>
              <th scope="col">{t("map.plunderRewards")}</th>
              <th scope="col">{t("map.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {jobs.map((job) => {
              const busy = truckBusyKey(job);
              const maximum = truckMaxLootCount(job);
              const robbed = Math.max(0, Number(job.robTimes) || 0);
              const state = truckState(job, currentTime);
              const result = truckPlunderResult(job, { t, online, now: currentTime, state });
              const cancellable = plunderCancelVisible(job);
              const again = truckPlunderAgainVisible(job, online, state, inflight);
              return (
                <tr className="map-row" key={truckRowKey(job)}>
                  <td>#{job.serverId}</td>
                  <td>{job.ownerName || job.allianceName || job.uuid}</td>
                  <td>{job.isSpecialURQuality ? t("map.reindeerQuality") : mapQuality(job.quality)}</td>
                  <td>{robbed}/{maximum}</td>
                  <td className="map-plunder-result" title={result === "-" ? "" : result}>{result}</td>
                  <td className="map-reward-cell"><PlunderRewards rewards={job.plunderRewards} gameTexts={gameTexts} language={language} /></td>
                  <td className="map-plunder-actions">
                    {cancellable && <button disabled={!actionsEnabled || busyKey === busy} onClick={actionsEnabled ? () => onCancel(job) : undefined}>{t("common.cancel")}</button>}
                    {again && <button disabled={!actionsEnabled || busyKey === busy} onClick={actionsEnabled ? () => onPlunderAgain(job) : undefined}>{t("map.plunderAgain")}</button>}
                    {!cancellable && !again && "-"}
                  </td>
                </tr>
              );
            })}
            {jobs.length === 0 && <tr><td className="map-empty" colSpan={TRUCK_WIDTHS.length}>{t("map.empty")}</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  );
}

// The three original sibling groups, in original order: Dispatch, Ghost Scout, Truck.
export function ScheduledPlunder({ dispatchJobs, truckJobs, gameTexts = EMPTY_GAME_TEXTS, currentTime, online, busyKey, actionsEnabled = false, onCancelDispatch, onCancelTruck, onPlunderAgain, onClear }) {
  return (
    <>
      <SecretPlunderGroup jobs={dispatchGroupJobs(dispatchJobs)} gameTexts={gameTexts} currentTime={currentTime} online={online} busyKey={busyKey} actionsEnabled={actionsEnabled} onCancel={onCancelDispatch} onClear={onClear} />
      <SecretPlunderGroup kind="ghost" jobs={ghostGroupJobs(dispatchJobs)} gameTexts={gameTexts} currentTime={currentTime} online={online} busyKey={busyKey} actionsEnabled={actionsEnabled} onCancel={onCancelDispatch} onClear={onClear} />
      <TruckPlunderGroup jobs={truckJobs} gameTexts={gameTexts} currentTime={currentTime} online={online} busyKey={busyKey} actionsEnabled={actionsEnabled} onCancel={onCancelTruck} onPlunderAgain={onPlunderAgain} onClear={onClear} />
    </>
  );
}
