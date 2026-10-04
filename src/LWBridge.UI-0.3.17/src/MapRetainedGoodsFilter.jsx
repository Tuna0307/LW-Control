import { useRef } from "react";
import { GameAssetImage } from "./GameAssetImage.jsx";

export function MapRetainedGoodsFilter({ items, value, label, allLabel, onChange }) {
  const detailsRef = useRef(null);
  const selected = items.find((item) => item.key === value);
  const choose = (key) => {
    onChange(key);
    detailsRef.current?.removeAttribute("open");
  };
  return (
    <details ref={detailsRef} className="map-item-filter">
      <summary aria-label={label} title={label}>
        {selected && <GameAssetImage assetPath={selected.iconPath} alt={selected.name} className="map-item-filter-icon" />}
        <span>{selected?.name || allLabel}</span>
      </summary>
      <div className="map-item-filter-menu">
        <button type="button" className={value ? "" : "active"} onClick={() => choose("")}>{allLabel}</button>
        {items.map((item) => (
          <button type="button" className={item.key === value ? "active" : ""} onClick={() => choose(item.key)} key={item.key}>
            <GameAssetImage assetPath={item.iconPath} alt={item.name} className="map-item-filter-icon" />
            <span>{item.name}</span>
          </button>
        ))}
      </div>
    </details>
  );
}
