const keys = ["liveEquipmentLevel", "liveEquipmentLevelUnknown", "livePresetHelp", "livePresetCapture", "livePresetEquip", "livePresetSaved", "livePresetEmpty", "livePresetCaptured", "livePresetEquipped", "livePresetSaveFailed"];
const messages = {
  en: ["Lv.{level}", "Level unavailable", "Gear edits below change the game immediately. Save current gear to a preset to recall it with Alt+1–4. Saving a preset does not change gear.", "Save current gear to preset", "Equip preset", "Saved", "Not saved", "Current gear saved to preset {number} (Alt+{number}). Game gear unchanged.", "Preset {number} equipped in the game", "Could not save the preset. Current gear is unchanged. Try saving again."],
  "zh-CN": ["等级 {level}", "等级不可用", "下方装备编辑会立即更改游戏。将当前装备保存到预设后，可用 Alt+1–4 切换。保存预设不会更改装备。", "保存当前装备到预设", "穿戴预设", "已保存", "未保存", "当前装备已保存到预设 {number}（Alt+{number}）。游戏装备未更改。", "已在游戏中穿戴预设 {number}", "无法保存预设。当前装备未更改，请重试保存。"],
  "zh-TW": ["等級 {level}", "等級無法取得", "下方裝備編輯會立即變更遊戲。將目前裝備儲存到預設後，可用 Alt+1–4 切換。儲存預設不會變更裝備。", "儲存目前裝備到預設", "穿戴預設", "已儲存", "未儲存", "目前裝備已儲存到預設 {number}（Alt+{number}）。遊戲裝備未變更。", "已在遊戲中穿戴預設 {number}", "無法儲存預設。目前裝備未變更，請重試儲存。"],
  ja: ["Lv.{level}", "レベル取得不可", "下の装備編集はすぐゲームに反映されます。現在の装備をプリセットに保存すると Alt+1–4 で呼び出せます。保存だけでは装備は変わりません。", "現在の装備をプリセットに保存", "プリセットを装着", "保存済み", "未保存", "現在の装備をプリセット {number}（Alt+{number}）に保存しました。ゲーム内の装備は変わりません。", "ゲーム内でプリセット {number} を装着しました", "プリセットを保存できませんでした。現在の装備は変わりません。保存を再試行してください。"],
  ko: ["Lv.{level}", "레벨 확인 불가", "아래 장비 편집은 게임에 즉시 적용됩니다. 현재 장비를 프리셋에 저장하면 Alt+1–4로 불러올 수 있습니다. 저장만으로 장비가 바뀌지는 않습니다.", "현재 장비를 프리셋에 저장", "프리셋 착용", "저장됨", "저장되지 않음", "현재 장비를 프리셋 {number}(Alt+{number})에 저장했습니다. 게임 장비는 변경되지 않았습니다.", "게임에서 프리셋 {number} 착용 완료", "프리셋을 저장하지 못했습니다. 현재 장비는 변경되지 않았습니다. 다시 저장하세요."],
  vi: ["Cấp {level}", "Không có cấp độ", "Chỉnh sửa bên dưới áp dụng ngay vào trò chơi. Lưu trang bị hiện tại vào bộ để gọi bằng Alt+1–4. Lưu bộ không thay đổi trang bị.", "Lưu trang bị hiện tại vào bộ", "Mặc bộ trang bị", "Đã lưu", "Chưa lưu", "Đã lưu trang bị hiện tại vào bộ {number} (Alt+{number}). Trang bị trong trò chơi không thay đổi.", "Đã mặc bộ {number} trong trò chơi", "Không thể lưu bộ. Trang bị hiện tại không thay đổi. Hãy thử lưu lại."],
  id: ["Lv.{level}", "Level tidak tersedia", "Edit di bawah langsung mengubah game. Simpan perlengkapan saat ini ke preset untuk dipanggil dengan Alt+1–4. Menyimpan preset tidak mengubah perlengkapan.", "Simpan perlengkapan saat ini ke preset", "Pakai preset", "Tersimpan", "Belum disimpan", "Perlengkapan saat ini disimpan ke preset {number} (Alt+{number}). Perlengkapan dalam game tidak berubah.", "Preset {number} dipakai dalam game", "Preset tidak dapat disimpan. Perlengkapan saat ini tidak berubah. Coba simpan lagi."],
  ru: ["Ур.{level}", "Уровень недоступен", "Изменения ниже сразу применяются в игре. Сохраните текущее снаряжение в набор для вызова через Alt+1–4. Сохранение набора не меняет снаряжение.", "Сохранить текущее снаряжение в набор", "Надеть набор", "Сохранён", "Не сохранён", "Текущее снаряжение сохранено в набор {number} (Alt+{number}). Снаряжение в игре не изменилось.", "Набор {number} надет в игре", "Не удалось сохранить набор. Текущее снаряжение не изменилось. Повторите сохранение."],
  pt: ["Nv.{level}", "Nível indisponível", "As edições abaixo alteram o jogo imediatamente. Guarde o equipamento atual numa predefinição para usar Alt+1–4. Guardar a predefinição não altera o equipamento.", "Guardar equipamento atual na predefinição", "Equipar predefinição", "Guardada", "Não guardada", "Equipamento atual guardado na predefinição {number} (Alt+{number}). O equipamento no jogo não mudou.", "Predefinição {number} equipada no jogo", "Não foi possível guardar a predefinição. O equipamento atual não mudou. Tente guardar novamente."],
};
const renameMessages = {
  "en": [
    "Rename preset",
    "Preset {number} renamed. Saved gear unchanged."
  ],
  "zh-CN": [
    "重命名预设",
    "已重命名预设 {number}。已保存的装备未更改。"
  ],
  "zh-TW": [
    "重新命名預設",
    "已重新命名預設 {number}。已儲存的裝備未變更。"
  ],
  "ja": [
    "プリセット名を変更",
    "プリセット {number} の名前を変更しました。保存済みの装備は変更されません。"
  ],
  "ko": [
    "프리셋 이름 변경",
    "프리셋 {number}의 이름을 변경했습니다. 저장된 장비는 그대로 유지됩니다."
  ],
  "vi": [
    "Đổi tên bộ trang bị",
    "Đã đổi tên bộ {number}. Trang bị đã lưu không thay đổi."
  ],
  "id": [
    "Ganti nama preset",
    "Nama preset {number} diubah. Perlengkapan tersimpan tidak berubah."
  ],
  "ru": [
    "Переименовать набор",
    "Набор {number} переименован. Сохранённое снаряжение не изменено."
  ],
  "pt": [
    "Renomear predefinição",
    "Predefinição {number} renomeada. Equipamento guardado inalterado."
  ]
};
keys.push("livePresetRename", "livePresetRenamed");
for (const [language, values] of Object.entries(messages)) values.push(...renameMessages[language]);
export const equipmentPresetTexts = Object.fromEntries(Object.entries(messages).map(([language, values]) => [language, Object.fromEntries(keys.map((key, index) => [`squad.${key}`, values[index]]))]));
export const withEquipmentPresetTexts = (language, catalog) => ({ ...catalog, ...equipmentPresetTexts[language] });
