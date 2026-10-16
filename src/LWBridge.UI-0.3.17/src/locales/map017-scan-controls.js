const hints = Object.freeze({
  en: "Select at least one scan category.",
  "zh-CN": "请至少选择一个扫描类别。",
  "zh-TW": "請至少選擇一個掃描類別。",
  ja: "スキャンする種類を1つ以上選択してください。",
  ko: "스캔할 종류를 하나 이상 선택하세요.",
  vi: "Chọn ít nhất một loại để quét.",
  id: "Pilih setidaknya satu kategori pemindaian.",
  ru: "Выберите хотя бы одну категорию сканирования.",
  pt: "Selecione pelo menos uma categoria para verificar.",
});

export function withMapScanControlTexts(language, catalog) {
  return { ...catalog, "map.scanCategoryRequired": hints[language] || hints.en };
}
