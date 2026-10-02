import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import englishMessages from "./locales/en.js";

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

const LOADERS = {
  en: () => Promise.resolve(englishMessages),
  "zh-CN": () => import("./locales/zh-CN.js").then((module) => module.default),
  "zh-TW": () => import("./locales/zh-TW.js").then((module) => module.default),
  ja: () => import("./locales/ja.js").then((module) => module.default),
  ko: () => import("./locales/ko.js").then((module) => module.default),
  vi: () => import("./locales/vi.js").then((module) => module.default),
  id: () => import("./locales/id.js").then((module) => module.default),
  ru: () => import("./locales/ru.js").then((module) => module.default),
  pt: () => import("./locales/pt.js").then((module) => module.default),
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
    }).catch(() => {
      if (language !== "en") {
        LOADERS.en().then((messages) => {
          if (current === request.current) setCatalog({ language: "en", messages });
        });
      }
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
