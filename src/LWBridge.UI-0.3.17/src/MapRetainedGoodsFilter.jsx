import { useRef } from "react";
import { GameAssetImage } from "./GameAssetImage.jsx";

export function MapRetainedGoodsFilter({ items, value, values = [], multiple = false, label, allLabel, selectedLabel = label, clearLabel = allLabel, onChange }) {
  const detailsRef = useRef(null);
  const selected = items.find((item) => item.key === value);
  const choose = (key) => {
    onChange(key);
    if (!multiple) detailsRef.current?.removeAttribute("open");
  };
  const selectedKeys = new Set(values);
  return (
    <details ref={detailsRef} className="map-item-filter">
      <summary aria-label={label} title={label}>
        {!multiple && selected && <GameAssetImage assetPath={selected.iconPath} alt={selected.name} className="map-item-filter-icon" />}
        <span>{multiple ? (values.length ? `${selectedLabel} (${values.length})` : allLabel) : selected?.name || allLabel}</span>
      </summary>
      <div className="map-item-filter-menu">
        <button type="button" className={multiple ? (values.length ? "" : "active") : value ? "" : "active"} onClick={() => choose("")}>{multiple ? clearLabel : allLabel}</button>
        {items.map((item) => (
          <button type="button" data-map-reward-key={item.key} aria-pressed={multiple ? selectedKeys.has(item.key) : item.key === value} className={(multiple ? selectedKeys.has(item.key) : item.key === value) ? "active" : ""} onClick={() => choose(item.key)} key={item.key}>
            <GameAssetImage assetPath={item.iconPath} alt={item.name} className="map-item-filter-icon" />
            <span>{multiple && selectedKeys.has(item.key) ? "✓ " : ""}{item.name}</span>
          </button>
        ))}
      </div>
    </details>
  );
}
