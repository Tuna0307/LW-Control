const keys = ["newRewards", "alreadyClaimed", "exhausted", "expired", "refused", "unconfirmed"];
const rows = {
  en: ["New rewards confirmed", "Already claimed", "No rewards left", "Expired", "Refused", "Unconfirmed"],
  "zh-CN": ["已确认的新奖励", "已领取", "奖励已领完", "已过期", "未获准领取", "结果未确认"],
  "zh-TW": ["已確認的新獎勵", "已領取", "獎勵已領完", "已過期", "未獲准領取", "結果未確認"],
  ja: ["新たに確認された報酬", "受取済み", "残り報酬なし", "期限切れ", "受取拒否", "未確認"],
  ko: ["새 보상 확인", "이미 수령함", "남은 보상 없음", "기간 만료", "수령 거부됨", "결과 미확인"],
  vi: ["Phần thưởng mới đã xác nhận", "Đã nhận trước đó", "Đã hết phần thưởng", "Đã hết hạn", "Bị từ chối", "Chưa xác nhận"],
  id: ["Hadiah baru terkonfirmasi", "Sudah diklaim", "Hadiah habis", "Kedaluwarsa", "Ditolak", "Belum terkonfirmasi"],
  ru: ["Новые подтверждённые награды", "Уже получено", "Награды закончились", "Срок истёк", "Отказано", "Не подтверждено"],
  pt: ["Novas recompensas confirmadas", "Já recolhida", "Sem recompensas restantes", "Expirada", "Recusada", "Não confirmada"],
};
export const chatRewardOutcomes = Object.fromEntries(Object.entries(rows).map(([language, values]) =>
  [language, Object.fromEntries(keys.map((key, index) => [`chatRewards.${key}`, values[index]]))]));
const controlKeys = ["fireworks", "goldEgg", "fireworksHelp", "goldEggHelp", "crossServer", "ignoreBirthday", "legacy", "crossHint", "eventUnavailable"];
const controls = {
  en: ["Fireworks", "Gold Egg", "Claim eligible Fireworks gifts from game chat.", "Claim eligible tax, trade, lucky and birthday eggs.", "Cross-server Gold Egg", "Skip birthday eggs", "Uses your previous Red Packet and Fireworks rules. Changing Gold Egg settings separates this control.", "Uses eligible chat rooms supplied by the game. Availability depends on the current season.", "Special activity egg claims are not supported yet."],
  "zh-CN": ["烟花", "金蛋", "领取游戏聊天中符合条件的烟花礼物。", "领取符合条件的税收、贸易、幸运和生日金蛋。", "跨服金蛋", "跳过生日金蛋", "沿用之前的红包和烟花规则。修改金蛋设置后将独立控制。", "使用游戏提供的可用聊天室，具体范围取决于当前赛季。", "暂不支持特殊活动蛋的自动领取。"],
  "zh-TW": ["煙花", "金蛋", "領取遊戲聊天中符合條件的煙花禮物。", "領取符合條件的稅收、貿易、幸運和生日金蛋。", "跨服金蛋", "跳過生日金蛋", "沿用之前的紅包和煙花規則。修改金蛋設定後將獨立控制。", "使用遊戲提供的可用聊天室，具體範圍取決於目前賽季。", "暫不支援特殊活動蛋的自動領取。"],
  ja: ["花火", "金の卵", "ゲームチャットから受取可能な花火ギフトを受け取ります。", "受取可能な税・交易・幸運・誕生日の卵を受け取ります。", "サーバー間の金の卵", "誕生日の卵を除外", "以前の赤い封筒と花火のルールを使用中です。金の卵の設定を変更すると独立します。", "ゲームが提供する利用可能なチャットルームを使用します。範囲は現在のシーズンによります。", "特別イベントの卵の自動受取は未対応です。"],
  ko: ["불꽃놀이", "황금 알", "게임 채팅의 수령 가능한 불꽃놀이 선물을 받습니다.", "수령 가능한 세금, 교역, 행운, 생일 알을 받습니다.", "서버 간 황금 알", "생일 알 제외", "기존 빨간 봉투 및 불꽃놀이 규칙을 사용합니다. 황금 알 설정을 바꾸면 별도로 제어합니다.", "게임이 제공하는 채팅방을 사용합니다. 범위는 현재 시즌에 따라 달라집니다.", "특별 이벤트 알 자동 수령은 아직 지원하지 않습니다."],
  vi: ["Pháo hoa", "Trứng vàng", "Nhận quà pháo hoa đủ điều kiện từ chat trò chơi.", "Nhận trứng thuế, giao dịch, may mắn và sinh nhật đủ điều kiện.", "Trứng vàng liên máy chủ", "Bỏ qua trứng sinh nhật", "Đang dùng quy tắc lì xì và pháo hoa cũ. Sửa cài đặt trứng vàng để điều khiển riêng.", "Dùng phòng chat hợp lệ do trò chơi cung cấp. Phạm vi tùy mùa hiện tại.", "Chưa hỗ trợ tự nhận trứng sự kiện đặc biệt."],
  id: ["Kembang api", "Telur emas", "Klaim hadiah kembang api yang memenuhi syarat dari chat game.", "Klaim telur pajak, dagang, keberuntungan, dan ulang tahun yang memenuhi syarat.", "Telur emas lintas server", "Lewati telur ulang tahun", "Mengikuti aturan amplop merah dan kembang api sebelumnya. Mengubah pengaturan telur emas memisahkannya.", "Menggunakan ruang chat yang disediakan game. Cakupan bergantung pada musim saat ini.", "Klaim telur acara khusus belum didukung."],
  ru: ["Фейерверки", "Золотое яйцо", "Получать доступные подарки-фейерверки из чата игры.", "Получать доступные налоговые, торговые, счастливые и праздничные яйца.", "Межсерверное золотое яйцо", "Пропускать яйца дня рождения", "Используются прежние правила красных пакетов и фейерверков. Изменение настроек золотых яиц отделит управление.", "Используются доступные чаты, предоставленные игрой. Охват зависит от текущего сезона.", "Получение яиц особых событий пока не поддерживается."],
  pt: ["Fogos de artifício", "Ovo dourado", "Recolher presentes elegíveis de fogos de artifício do chat do jogo.", "Recolher ovos elegíveis de impostos, comércio, sorte e aniversário.", "Ovo dourado entre servidores", "Ignorar ovos de aniversário", "Usa as regras anteriores de envelopes vermelhos e fogos. Alterar as opções dos ovos dourados separa este controlo.", "Usa salas de chat disponibilizadas pelo jogo. A cobertura depende da temporada atual.", "A recolha de ovos de eventos especiais ainda não é suportada."],
};
for (const [language, values] of Object.entries(controls))
  Object.assign(chatRewardOutcomes[language], Object.fromEntries(controlKeys.map((key, index) => [`chatRewards.${key}`, values[index]])));
const separate = { en: "Use separate Gold Egg settings", "zh-CN": "独立设置金蛋", "zh-TW": "獨立設定金蛋", ja: "金の卵を個別に設定", ko: "황금 알 별도 설정", vi: "Dùng cài đặt trứng vàng riêng", id: "Gunakan pengaturan telur emas terpisah", ru: "Отделить настройки золотых яиц", pt: "Usar opções separadas de ovos dourados" };
for (const [language, text] of Object.entries(separate)) chatRewardOutcomes[language]["chatRewards.separate"] = text;
const rooms = { en: "Available cross-server rooms", "zh-CN": "可用跨服聊天室", "zh-TW": "可用跨服聊天室", ja: "利用可能なサーバー間チャット", ko: "사용 가능한 서버 간 채팅방", vi: "Phòng liên máy chủ khả dụng", id: "Ruang lintas server tersedia", ru: "Доступные межсерверные чаты", pt: "Salas entre servidores disponíveis" };
for (const [language, text] of Object.entries(rooms)) chatRewardOutcomes[language]["chatRewards.remoteRooms"] = text;
export const withChatRewardOutcomes = (language, catalog) => ({ ...catalog, ...chatRewardOutcomes[language],
  "automation.fireworks.title": chatRewardOutcomes[language]["chatRewards.fireworks"] });
