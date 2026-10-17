// OWN_DESIGN (owner request 2026-10-17): configurable soldiers per G press.
const texts = Object.freeze({
  en: ["Soldiers per G press", "Press G during a stage run (Frontline Breakthrough stages or the Sunday challenge) to add soldiers. Set how many below."],
  "zh-CN": ["每按一次 G 增加士兵", "关卡闯关（前线突破关卡或周日挑战）进行中按 G 增加士兵，数量可在下方设置。"],
  "zh-TW": ["每按一次 G 增加士兵", "關卡闖關（前線突破關卡或週日挑戰）進行中按 G 增加士兵，數量可在下方設定。"],
  ja: ["G 1回あたりの兵士数", "ステージ進行中（前線突破ステージまたは日曜チャレンジ）に G を押すと兵士を追加します。数は下で設定できます。"],
  ko: ["G 1회당 병사 수", "스테이지 진행 중(전선 돌파 스테이지 또는 일요일 챌린지) G를 누르면 병사가 추가됩니다. 아래에서 수를 설정하세요."],
  vi: ["Số lính mỗi lần nhấn G", "Nhấn G khi đang vượt màn (màn Đột phá tiền tuyến hoặc thử thách Chủ nhật) để thêm lính. Đặt số lượng bên dưới."],
  id: ["Prajurit per tekan G", "Tekan G saat menjalankan tahap (tahap Frontline Breakthrough atau tantangan Minggu) untuk menambah prajurit. Atur jumlahnya di bawah."],
  ru: ["Солдат за нажатие G", "Нажимайте G во время прохождения этапа (этапы прорыва фронта или воскресное испытание), чтобы добавить солдат. Количество задаётся ниже."],
  pt: ["Soldados por toque em G", "Pressione G durante uma fase (fases do Avanço na Linha de Frente ou desafio de domingo) para adicionar soldados. Defina a quantidade abaixo."],
});

export function withFrontlineCountTexts(language, catalog) {
  const [label, description] = texts[language] || texts.en;
  return { ...catalog, "hotkeys.frontlineReinforce.countLabel": label, "hotkeys.frontlineReinforce.description": description };
}
