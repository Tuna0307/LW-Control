// New Map controls. The official game remains in Chinese; these labels belong
// solely to the clone. Other clone languages use the clear English fallback.
const labels = {
  en: { minimum: "Min level", maximum: "Max level", amount: "Resource amount", terrain: "Tile terrain", all: "All", full: "Full", partial: "Partially depleted", unknown: "Unknown", black: "Black tile", normal: "Non-black tile", escort: "Max escort power (millions)", invalidPower: "Enter a non-negative escort power (millions), up to 1 billion.", itemsAll: "Match ALL selected items", clearItems: "Clear selected items" },
  ja: { minimum: "最低レベル", maximum: "最高レベル", amount: "資源残量", terrain: "タイル地形", all: "すべて", full: "満量", partial: "一部消費", unknown: "不明", black: "黒タイル", normal: "通常タイル", escort: "護衛戦力上限（百万）", invalidPower: "0〜10億の数値を入力してください。", itemsAll: "選択したすべてのアイテムを含む", clearItems: "選択解除" },
  "zh-CN": { minimum: "最低等级", maximum: "最高等级", amount: "资源余量", terrain: "地块类型", all: "全部", full: "满量", partial: "部分消耗", unknown: "未知", black: "黑土地", normal: "非黑土地", escort: "护送战力上限（百万）", invalidPower: "请输入0至10亿之间的有效数值。", itemsAll: "包含全部已选物品", clearItems: "清除已选" },
  "zh-TW": { minimum: "最低等級", maximum: "最高等級", amount: "資源餘量", terrain: "地塊類型", all: "全部", full: "滿量", partial: "部分消耗", unknown: "未知", black: "黑土地", normal: "非黑土地", escort: "護送戰力上限（百萬）", invalidPower: "請輸入0至10億之間的有效數值。", itemsAll: "包含全部已選物品", clearItems: "清除已選" },
};

export const mapOwnerFilterLabel = (language, key) => (labels[language] || labels.en)[key];

export function parseEscortMillions(raw) {
  if (raw === "") return { valid: true, value: null };
  // Plain positive decimal only; no hex, exponent, negative, Infinity or NaN.
  if (!/^\d+(?:\.\d+)?$/.test(raw)) return { valid: false, value: null };
  const value = Number(raw);
  return { valid: Number.isFinite(value) && value <= 1_000_000_000, value };
}
