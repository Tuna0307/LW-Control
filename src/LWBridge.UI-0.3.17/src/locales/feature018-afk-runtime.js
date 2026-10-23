// OWN_DESIGN runtime feedback for the recovered AFK controls.
const keys = ["waitingStamina", "waitingSquad", "accountPaused", "confirmingItem", "noItems", "usedItems", "waitingProfile", "waitingSearch"];
const rows = {
  en: ["Waiting for Stamina", "Waiting for an available squad", "Paused: account changed", "Confirming Stamina item", "No owned Stamina items", "Stamina items used: {count}", "Waiting for an enabled profile", "Waiting for monster search"],
  "zh-CN": ["等待体力", "等待空闲编队", "已暂停：账号已变更", "正在确认体力道具", "没有已拥有的体力道具", "已使用体力道具：{count}", "等待已启用的方案", "等待搜索怪物"],
  "zh-TW": ["等待體力", "等待空閒編隊", "已暫停：帳號已變更", "正在確認體力道具", "沒有已擁有的體力道具", "已使用體力道具：{count}", "等待已啟用的方案", "等待搜尋怪物"],
  ja: ["スタミナ待ち", "空き部隊待ち", "一時停止：アカウントが変更されました", "スタミナアイテムを確認中", "所持スタミナアイテムなし", "使用したスタミナアイテム：{count}", "有効なプロファイル待ち", "モンスター検索待ち"],
  ko: ["스태미나 대기", "사용 가능한 부대 대기", "일시 정지: 계정이 변경됨", "스태미나 아이템 확인 중", "보유한 스태미나 아이템 없음", "사용한 스태미나 아이템: {count}", "활성 프로필 대기", "몬스터 검색 대기"],
  vi: ["Đang chờ thể lực", "Đang chờ đội rảnh", "Đã tạm dừng: tài khoản đã đổi", "Đang xác nhận vật phẩm thể lực", "Không có vật phẩm thể lực", "Vật phẩm thể lực đã dùng: {count}", "Đang chờ hồ sơ được bật", "Đang chờ tìm quái vật"],
  id: ["Menunggu Stamina", "Menunggu skuad yang tersedia", "Dijeda: akun berubah", "Mengonfirmasi item Stamina", "Tidak memiliki item Stamina", "Item Stamina digunakan: {count}", "Menunggu profil yang diaktifkan", "Menunggu pencarian monster"],
  ru: ["Ожидание выносливости", "Ожидание свободного отряда", "Пауза: аккаунт изменён", "Подтверждение предмета выносливости", "Нет предметов выносливости", "Использовано предметов выносливости: {count}", "Ожидание включённого профиля", "Ожидание поиска монстра"],
  pt: ["Aguardando Energia", "Aguardando um esquadrão disponível", "Pausado: a conta mudou", "Confirmando item de Energia", "Sem itens de Energia no inventário", "Itens de Energia usados: {count}", "Aguardando um perfil ativado", "Aguardando busca de monstros"],
};
export function withAfkRuntimeTexts(language, catalog) {
  const values = rows[language] || rows.en;
  return { ...catalog, ...Object.fromEntries(keys.map((key, i) => [`squad.runtime.${key}`, values[i]])) };
}
