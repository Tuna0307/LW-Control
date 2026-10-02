import { useRef } from "react";

// The native game-asset loader is a separate integration task. This is the
// original GameAssetImage's unavailable-image branch, preserving its slot/label.
function MapFilterAssetPlaceholder({ alt, className }) {
  return <span className={`${className} game-asset-placeholder`} aria-label={alt} />;
}

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
        {selected && <MapFilterAssetPlaceholder assetPath={selected.iconPath} alt={selected.name} className="map-item-filter-icon" />}
        <span>{selected?.name || allLabel}</span>
      </summary>
      <div className="map-item-filter-menu">
        <button type="button" className={value ? "" : "active"} onClick={() => choose("")}>{allLabel}</button>
        {items.map((item) => (
          <button type="button" className={item.key === value ? "active" : ""} onClick={() => choose(item.key)} key={item.key}>
            <MapFilterAssetPlaceholder assetPath={item.iconPath} alt={item.name} className="map-item-filter-icon" />
            <span>{item.name}</span>
          </button>
        ))}
      </div>
    </details>
  );
}
