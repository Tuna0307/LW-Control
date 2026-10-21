import { useState } from "react";
import { useI18n } from "./i18n.jsx";
import { assertBridgeProfileOwner } from "./profileConfigDraft.js";

// Owner 2026-10-21: read-only game emoji list (feature018_chat_emoji_list). Clicking one appends its
// in-text tag (<lwEmoji:ID:>) to the end of the last reply line; nothing is sent to the game.
export function GameEmojiPicker({ bridge, profileId, disabled, onPick }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false), [emojis, setEmojis] = useState(null), [error, setError] = useState(""), [pictures, setPictures] = useState({});
  // Pictures are read one at a time (read-only) so the game is never flooded.
  const loadPictures = async list => {
    for (const emoji of list) {
      if (!emoji.name) continue;
      for (let attempt = 0; attempt < 3; attempt += 1) {
        try {
          const result = await bridge.invokeProfileScoped("feature018_chat_emoji_image", { name: String(emoji.name) });
          if (result?.png) { setPictures(current => ({ ...current, [emoji.id]: `data:image/png;base64,${result.png}` }));break; }
        } catch { await new Promise(resolve => setTimeout(resolve, 1000)); }
      }
    }
  };
  const load = async () => {
    const next = !open;setOpen(next);
    if (!next || emojis) return;
    try {
      assertBridgeProfileOwner(bridge, profileId);
      const result = await bridge.invokeProfileScoped("feature018_chat_emoji_list");
      const list = Array.isArray(result?.emojis) ? result.emojis : result?.emojis && typeof result.emojis === "object" ? Object.values(result.emojis) : [];
      setEmojis(list);setError("");void loadPictures(list);
    } catch (e) { setError(e.message); }
  };
  return <div className="game-emoji-picker">
    <button type="button" className="automation-config-trigger" aria-expanded={open} disabled={disabled} onClick={() => { void load(); }}>{t("automation.gameEmoji")}</button>
    {open ? <div className="game-emoji-list" role="group" aria-label={t("automation.gameEmoji")}>
      <p className="muted">{t("automation.gameEmojiHint")}</p>
      {error ? <p role="alert" className="error-text">{error}</p> : null}
      {emojis ? emojis.map(emoji => <button type="button" key={emoji.id} data-emoji-id={emoji.id} disabled={disabled} title={emoji.tag} onClick={() => onPick(emoji.tag)}>{pictures[emoji.id] ? <img src={pictures[emoji.id]} alt={emoji.name ?? `#${emoji.id}`} width="32" height="32" /> : emoji.name ? `${emoji.name} (#${emoji.id})` : `#${emoji.id}`}</button>) : null}
    </div> : null}
  </div>;
}
