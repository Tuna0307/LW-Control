import { useState } from "react";

// Every offered item/currency is supplied by a read-only native season-template
// observation. Saved IDs stay saved even when the current season is unavailable.
export function NativeTradeFields({ config, runtime, observation, disabled, t }) {
  const [error, setError] = useState("");
  const draft = config.draft;
  const data = observation?.data ?? observation ?? runtime?.observation ?? runtime?.after ?? runtime?.before;
  const catalog = data?.catalog?.nativeTemplate === true ? data.catalog : null;
  // A native empty Lua table is serialized as {} rather than []: accept both shapes.
  const list = value => Array.isArray(value) ? value : value && typeof value === "object" ? Object.values(value) : [];
  const items = list(catalog?.items), currencies = list(catalog?.currencies);
  const save = async patch => {
    setError("");
    try {
      config.store.edit(previous => ({ ...previous, ...patch }), false);
      await config.store.flush();
    } catch (failure) { setError(failure.message); }
  };
  const toggle = (field, id, checked) => {
    const ids = new Set(draft[field] ?? []);
    if (checked) ids.add(id); else ids.delete(id);
    void save({ [field]: [...ids].sort((a, b) => a - b) });
  };
  return <div className="trade-station-panel" data-native-trade-fields="true">
    <p className="trade-station-warning">{t("automation.tradeStation.warning")}</p>
    <label><input type="checkbox" checked={draft.crossServerEnabled === true} disabled={disabled}
      onChange={event => { void save({ crossServerEnabled: event.target.checked }); }} />{t("automation.tradeStation.crossServer")}</label>
    <fieldset className="trade-station-currencies" disabled={disabled}>
      <legend>{t("automation.tradeStation.currencies")}</legend>
      {currencies.map(currency => <label key={currency.id}>
        <input type="checkbox" checked={(draft.selectedCurrencyIds ?? []).includes(currency.id)}
          onChange={event => toggle("selectedCurrencyIds", currency.id, event.target.checked)} />
        {currency.name || `#${currency.id}`}{currency.balance != null ? ` (${currency.balance.toLocaleString()})` : ""}
      </label>)}
    </fieldset>
    <fieldset disabled={disabled}>
      <legend>{t("automation.tradeStation.goodsToBuy")}</legend>
      {!catalog || items.length === 0 ? <p className="muted">{t("automation.tradeStation.noGoods")}</p> : null}
      <div className="trade-station-good-grid">{items.map(item => {
        const offers = list(item.offers);
        const exclusive = offers.length > 0 && offers.every(offer => offer.exclusive === true);
        const possible = [...new Set(offers.map(offer => offer.currencyId))]
          .map(id => currencies.find(currency => currency.id === id)?.name || `#${id}`);
        return <label className="trade-station-good" key={item.itemId}>
          <input type="checkbox" checked={(draft.selectedItemIds ?? []).includes(item.itemId)} disabled={exclusive}
            onChange={event => toggle("selectedItemIds", item.itemId, event.target.checked)} />
          <span><b>{item.name || `#${item.itemId}`}</b><small>{t("automation.tradeStation.possibleCurrencies")}{possible.join(" / ")}</small>
            {exclusive ? <small>{t("automation.tradeStation.exclusiveSkipped")}</small> : null}</span>
        </label>;
      })}</div>
    </fieldset>
    {data?.reason ? <p className="muted">{data.reason}</p> : null}
    {error ? <p role="alert" className="error-text">{error}</p> : null}
  </div>;
}
