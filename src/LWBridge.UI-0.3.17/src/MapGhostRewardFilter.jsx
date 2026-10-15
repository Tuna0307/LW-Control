import { GameAssetImage } from "./GameAssetImage.jsx";
import { useI18n } from "./i18n.jsx";
import { mapRewardName } from "./mapTablePresentation.js";
import { parseGhostRewardMinimum } from "./mapGhostRewards.js";

// Reward filter for Ghost tasks. Items come from the selected server's stored
// authoritative reward lists (real game names and icons). Selecting several
// rewards means "contains ALL of them" unless the labelled "ANY" mode is chosen.
export function MapGhostRewardFilter({ items, filters, match, gameTexts, onToggle, onClear, onMinimum, onMatch }) {
  const { t } = useI18n();
  const selectedKeys = new Set(filters.map((filter) => filter.key));
  const itemByKey = new Map(items.map((item) => [item.key, item]));
  const displayName = (item, key) => (item ? mapRewardName(item, gameTexts) : key);
  return (
    <>
      <details className="map-item-filter map-ghost-reward-filter">
        <summary aria-label={t("map.ghostRewardFilter")} title={t("map.ghostRewardFilter")}>
          <span>{filters.length ? t("map.ghostRewardSelected", { count: filters.length }) : t("map.ghostRewardAll")}</span>
        </summary>
        <div className="map-item-filter-menu">
          <button type="button" className={filters.length ? "" : "active"} onClick={onClear}>{filters.length ? t("map.ghostRewardClear") : t("map.ghostRewardAll")}</button>
          {items.map((item) => {
            const selected = selectedKeys.has(item.key);
            const name = displayName(item, item.key);
            return (
              <button type="button" data-map-reward-key={item.key} aria-pressed={selected} className={selected ? "active" : ""} onClick={() => onToggle(item.key)} key={item.key}>
                <GameAssetImage assetPath={item.iconPath} alt={name} className="map-item-filter-icon" />
                <span>{selected ? "✓ " : ""}{name}</span>
                {Number.isFinite(Number(item.taskCount)) ? <small>{t("map.ghostRewardTaskCount", { count: item.taskCount })}</small> : null}
              </button>
            );
          })}
        </div>
      </details>
      {filters.length ? (
        <span className="map-ghost-reward-selected">
          {filters.map((filter) => {
            const item = itemByKey.get(filter.key);
            const name = displayName(item, filter.key);
            const minimum = parseGhostRewardMinimum(filter.min);
            return (
              <span className="map-ghost-reward-chip" data-map-ghost-reward-chip={filter.key} key={filter.key}>
                <GameAssetImage assetPath={item?.iconPath} alt={name} className="map-item-filter-icon" />
                <span>{name}</span>
                <input type="text" inputMode="numeric" aria-label={t("map.ghostRewardMinimumFor", { name })} aria-invalid={!minimum.valid}
                  placeholder={t("map.ghostRewardMinimumPlaceholder")} value={filter.min} onChange={(event) => onMinimum(filter.key, event.target.value.trim())} />
                <button type="button" aria-label={`${t("map.ghostRewardClear")}: ${name}`} onClick={() => onToggle(filter.key)}>×</button>
              </span>
            );
          })}
          {filters.length > 1 ? (
            <select aria-label={t("map.ghostRewardMatch")} value={match} onChange={(event) => onMatch(event.target.value === "any" ? "any" : "all")}>
              <option value="all">{t("map.ghostRewardMatchAll")}</option>
              <option value="any">{t("map.ghostRewardMatchAny")}</option>
            </select>
          ) : null}
        </span>
      ) : null}
    </>
  );
}
