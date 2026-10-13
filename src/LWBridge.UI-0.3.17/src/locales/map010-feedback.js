// MAP-010 clone-specific additions. Keep recovered 0.3.17 catalogs generated
// verbatim; new messages are explicitly translated for all nine app languages.
export const MAP_NAME_FEEDBACK = Object.freeze({
  en: {
    "map.namesMissingRoot": "Game names unavailable: current game installation is not resolved.",
    "map.namesUnavailable": "Game names unavailable: {detail}",
    "map.namesRetry": "Retry names",
    "map.namesLoading": "Loading game names…",
    "map.savedServer": "Saved server",
  },
  "zh-CN": {
    "map.namesMissingRoot": "游戏名称不可用：尚未确定当前游戏安装位置。",
    "map.namesUnavailable": "游戏名称不可用：{detail}",
    "map.namesRetry": "重试加载名称",
    "map.namesLoading": "正在加载游戏名称…",
    "map.savedServer": "已保存的服务器",
  },
  "zh-TW": {
    "map.namesMissingRoot": "遊戲名稱無法使用：尚未確認目前遊戲安裝位置。",
    "map.namesUnavailable": "遊戲名稱無法使用：{detail}",
    "map.namesRetry": "重試載入名稱",
    "map.namesLoading": "正在載入遊戲名稱…",
    "map.savedServer": "已儲存的伺服器",
  },
  ja: {
    "map.namesMissingRoot": "ゲーム内名称を取得できません：現在のゲームのインストール先が確認できません。",
    "map.namesUnavailable": "ゲーム内名称を取得できません：{detail}",
    "map.namesRetry": "名称を再取得",
    "map.namesLoading": "ゲーム内名称を読み込み中…",
    "map.savedServer": "保存済みサーバー",
  },
  ko: {
    "map.namesMissingRoot": "게임 이름을 사용할 수 없습니다: 현재 게임 설치 경로를 확인할 수 없습니다.",
    "map.namesUnavailable": "게임 이름을 사용할 수 없습니다: {detail}",
    "map.namesRetry": "이름 다시 불러오기",
    "map.namesLoading": "게임 이름을 불러오는 중…",
    "map.savedServer": "저장된 서버",
  },
  vi: {
    "map.namesMissingRoot": "Không thể tải tên trong trò chơi: chưa xác định được thư mục cài đặt trò chơi hiện tại.",
    "map.namesUnavailable": "Không thể tải tên trong trò chơi: {detail}",
    "map.namesRetry": "Tải lại tên",
    "map.namesLoading": "Đang tải tên trong trò chơi…",
    "map.savedServer": "Máy chủ đã lưu",
  },
  id: {
    "map.namesMissingRoot": "Nama game tidak tersedia: lokasi pemasangan game saat ini belum diketahui.",
    "map.namesUnavailable": "Nama game tidak tersedia: {detail}",
    "map.namesRetry": "Muat ulang nama",
    "map.namesLoading": "Memuat nama dalam game…",
    "map.savedServer": "Server tersimpan",
  },
  ru: {
    "map.namesMissingRoot": "Игровые названия недоступны: расположение текущей установки игры не определено.",
    "map.namesUnavailable": "Игровые названия недоступны: {detail}",
    "map.namesRetry": "Повторить загрузку названий",
    "map.namesLoading": "Загрузка игровых названий…",
    "map.savedServer": "Сохранённый сервер",
  },
  pt: {
    "map.namesMissingRoot": "Nomes do jogo indisponíveis: a instalação atual do jogo não foi localizada.",
    "map.namesUnavailable": "Nomes do jogo indisponíveis: {detail}",
    "map.namesRetry": "Carregar nomes novamente",
    "map.namesLoading": "Carregando nomes do jogo…",
    "map.savedServer": "Servidor salvo",
  },
});

export function withMapNameFeedback(language, catalog) {
  return { ...catalog, ...MAP_NAME_FEEDBACK[language] };
}
