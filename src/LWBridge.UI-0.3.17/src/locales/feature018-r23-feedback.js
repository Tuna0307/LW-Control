// Clone-owned R23 feedback. Recovered locale catalogs remain unchanged.
const messages = {
  "en": {
    "squad.equipmentCaptureHint": "Press Save current squads to copy your current gear into this preset.",
    "squad.currentEquipmentSlot": "Wearing now: {name}",
    "automation.chatObserved": "Chat messages observed: {count}"
  },
  "zh-CN": {
    "squad.equipmentCaptureHint": "点击“保存当前小队”将当前装备复制到此预设。",
    "squad.currentEquipmentSlot": "当前穿戴：{name}",
    "automation.chatObserved": "已观察到聊天消息：{count}"
  },
  "zh-TW": {
    "squad.equipmentCaptureHint": "點擊「儲存目前小隊」將目前裝備複製到此預設。",
    "squad.currentEquipmentSlot": "目前穿戴：{name}",
    "automation.chatObserved": "已觀察到聊天訊息：{count}"
  },
  "ja": {
    "squad.equipmentCaptureHint": "「現在の部隊を保存」で現在の装備をこのプリセットにコピーします。",
    "squad.currentEquipmentSlot": "現在の装備：{name}",
    "automation.chatObserved": "確認したチャットメッセージ：{count}"
  },
  "ko": {
    "squad.equipmentCaptureHint": "현재 부대 저장을 눌러 현재 장비를 이 프리셋에 복사하세요.",
    "squad.currentEquipmentSlot": "현재 착용: {name}",
    "automation.chatObserved": "확인한 채팅 메시지: {count}"
  },
  "vi": {
    "squad.equipmentCaptureHint": "Nhấn Lưu đội hiện tại để sao chép trang bị hiện tại vào bộ cài sẵn này.",
    "squad.currentEquipmentSlot": "Đang mặc: {name}",
    "automation.chatObserved": "Tin nhắn trò chuyện đã quan sát: {count}"
  },
  "id": {
    "squad.equipmentCaptureHint": "Tekan Simpan regu saat ini untuk menyalin perlengkapan saat ini ke preset ini.",
    "squad.currentEquipmentSlot": "Sedang dipakai: {name}",
    "automation.chatObserved": "Pesan obrolan teramati: {count}"
  },
  "ru": {
    "squad.equipmentCaptureHint": "Нажмите «Сохранить текущие отряды», чтобы скопировать их снаряжение в этот набор.",
    "squad.currentEquipmentSlot": "Сейчас надето: {name}",
    "automation.chatObserved": "Обнаружено сообщений чата: {count}"
  },
  "pt": {
    "squad.equipmentCaptureHint": "Pressione Salvar esquadrões atuais para copiar o equipamento atual para esta configuração.",
    "squad.currentEquipmentSlot": "Usando agora: {name}",
    "automation.chatObserved": "Mensagens de chat observadas: {count}"
  }
};
export function withR23Feedback(language, catalog) { return { ...catalog, ...(messages[language] || messages.en) }; }
