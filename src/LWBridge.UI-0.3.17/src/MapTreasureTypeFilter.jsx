import { useRef } from "react";
import { useI18n } from "./i18n.jsx";
import { treasureName } from "./mapTablePresentation.js";

export function MapTreasureTypeFilter({ items, value, gameTexts, onChange }) {
  const { t } = useI18n();
  const detailsRef = useRef(null);
  const selected = items.find((item) => item.key === value);
  const name = (item) => treasureName(t, gameTexts, item.treasureType, item.suppliesType, item.treasureNameKey);
  const choose = (key) => {
    onChange(key);
    detailsRef.current?.removeAttribute("open");
  };
  return (
    <details ref={detailsRef} className="map-item-filter">
      <summary aria-label={t("map.treasureType")} title={t("map.treasureType")}>
        <span>{selected ? name(selected) : t("map.allTreasureTypes")}</span>
      </summary>
      <div className="map-item-filter-menu">
        <button type="button" className={value ? "" : "active"} onClick={() => choose("")}>{t("map.allTreasureTypes")}</button>
        {items.map((item) => (
          <button type="button" className={item.key === value ? "active" : ""} onClick={() => choose(item.key)} key={item.key}>
            <span>{name(item)} ({item.count})</span>
          </button>
        ))}
      </div>
    </details>
  );
}
