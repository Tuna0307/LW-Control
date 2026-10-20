import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import englishMessages from "./locales/en.js";
import { withMapNameFeedback } from "./locales/map010-feedback.js";
import { withMapOtherTexts } from "./locales/map014-other.js";
import { withGhostRewardTexts } from "./locales/map014-ghost-rewards.js";
import { withZombieBossTexts } from "./locales/map014-zombie-boss.js";
import { withHomeLaunchFeedback } from "./locales/home-launch-feedback.js";
import { withMapScanControlTexts } from "./locales/map017-scan-controls.js";
import { withFrontlineCountTexts } from "./locales/feature018-g-count.js";
import { withAfkRuntimeTexts } from "./locales/feature018-afk-runtime.js";
import { withR23Feedback } from "./locales/feature018-r23-feedback.js";
import { withEquipmentPlanTexts } from "./locales/feature018-equipment-plan.js";

export const LANGUAGES = Object.freeze([
  { code: "en", name: "English" },
  { code: "zh-CN", name: "Chinese (Simplified)" },
  { code: "zh-TW", name: "Chinese (Traditional)" },
  { code: "ja", name: "Japanese" },
  { code: "ko", name: "Korean" },
  { code: "vi", name: "Vietnamese" },
  { code: "id", name: "Indonesian" },
  { code: "ru", name: "Русский" },
  { code: "pt", name: "Português" },
]);

const withMapExtras = (language, catalog) => withR23Feedback(language, withAfkRuntimeTexts(language,withFrontlineCountTexts(language, withMapScanControlTexts(language, withHomeLaunchFeedback(language, withZombieBossTexts(language, withGhostRewardTexts(language, withMapOtherTexts(language, withMapNameFeedback(language, catalog)))))))));
const LOADERS = {
  en: () => Promise.resolve(withEquipmentPlanTexts("en", withMapExtras("en", englishMessages))),
  "zh-CN": () => import("./locales/zh-CN.js").then((module) => withEquipmentPlanTexts("zh-CN", withMapExtras("zh-CN", module.default))),
  "zh-TW": () => import("./locales/zh-TW.js").then((module) => withEquipmentPlanTexts("zh-TW", withMapExtras("zh-TW", module.default))),
  ja: () => import("./locales/ja.js").then((module) => withEquipmentPlanTexts("ja", withMapExtras("ja", module.default))),
  ko: () => import("./locales/ko.js").then((module) => withEquipmentPlanTexts("ko", withMapExtras("ko", module.default))),
  vi: () => import("./locales/vi.js").then((module) => withEquipmentPlanTexts("vi", withMapExtras("vi", module.default))),
  id: () => import("./locales/id.js").then((module) => withEquipmentPlanTexts("id", withMapExtras("id", module.default))),
  ru: () => import("./locales/ru.js").then((module) => withEquipmentPlanTexts("ru", withMapExtras("ru", module.default))),
  pt: () => import("./locales/pt.js").then((module) => withEquipmentPlanTexts("pt", withMapExtras("pt", module.default))),
};

const LANGUAGE_KEY = "lwbridge.language";
const I18nContext = createContext(null);
const ENGLISH_KEY_BY_VALUE = new Map();
for (const [key, value] of Object.entries(englishMessages)) {
  if (typeof value === "string" && !ENGLISH_KEY_BY_VALUE.has(value)) ENGLISH_KEY_BY_VALUE.set(value, key);
}

function initialLanguage() {
  try {
    const saved = localStorage.getItem(LANGUAGE_KEY);
    return LANGUAGES.some(({ code }) => code === saved) ? saved : "zh-CN";
  } catch {
    return "zh-CN";
  }
}

export function I18nProvider({ children }) {
  const [catalog, setCatalog] = useState(null);
  const request = useRef(0);

  const setLanguage = useCallback((language) => {
    const current = ++request.current;
    const loader = LOADERS[language] || LOADERS["zh-CN"];
    loader().then((messages) => {
      if (current === request.current) setCatalog({ language, messages });
    }).catch((error) => {
      if (current === request.current) console.error("Failed to load language catalog", error);
    });
  }, []);

  useEffect(() => {
    setLanguage(initialLanguage());
    return () => { request.current += 1; };
  }, [setLanguage]);

  useEffect(() => {
    if (!catalog) return;
    try { localStorage.setItem(LANGUAGE_KEY, catalog.language); } catch {}
    document.documentElement.lang = catalog.language;
  }, [catalog]);

  const value = useMemo(() => catalog ? {
    language: catalog.language,
    setLanguage,
    t(key, values = {}) {
      return (catalog.messages[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
    },
    english(text, values = {}) {
      const key = ENGLISH_KEY_BY_VALUE.get(text);
      if (!key) return text;
      return (catalog.messages[key] || text).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
    },
  } : null, [catalog, setLanguage]);

  if (!value) {
    return <main className="auth-screen"><div className="auth-loading" role="status" aria-busy="true" /></main>;
  }
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const value = useContext(I18nContext);
  if (!value) throw new Error("useI18n must be used inside I18nProvider");
  return value;
}
