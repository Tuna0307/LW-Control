// Current-client session persistence feedback; original catalogs stay verbatim.
export const HOME_LAUNCH_FEEDBACK = Object.freeze({
  en: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "The game started, but its recovery information could not be saved.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "Game exited. Choose Close game before starting again.",
  },
  "zh-CN": {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "游戏已启动，但无法保存游戏会话的恢复信息。",
    "error.GAME_EXITED_RESTORE_REQUIRED": "游戏已退出。请先点击“关闭游戏”，再重新启动。",
  },
  "zh-TW": {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "遊戲已啟動，但無法儲存遊戲工作階段的恢復資訊。",
    "error.GAME_EXITED_RESTORE_REQUIRED": "遊戲已結束。請先點擊「關閉遊戲」，再重新啟動。",
  },
  ja: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "ゲームは起動しましたが、セッションの復元情報を保存できませんでした。",
    "error.GAME_EXITED_RESTORE_REQUIRED": "ゲームが終了しました。再起動前に「ゲームを閉じる」を選択してください。",
  },
  ko: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "게임이 시작되었지만 세션 복구 정보를 저장하지 못했습니다.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "게임이 종료되었습니다. 다시 시작하기 전에 게임 종료를 누르세요.",
  },
  vi: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "Trò chơi đã khởi động nhưng không thể lưu thông tin khôi phục phiên.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "Trò chơi đã thoát. Chọn Đóng trò chơi trước khi khởi động lại.",
  },
  id: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "Game dimulai, tetapi informasi pemulihan sesi tidak dapat disimpan.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "Game telah keluar. Pilih Tutup game sebelum memulai lagi.",
  },
  ru: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "Игра запущена, но не удалось сохранить данные восстановления сеанса.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "Игра завершилась. Перед запуском нажмите «Закрыть игру».",
  },
  pt: {
    "error.RECOVERY_RECORD_COMMIT_FAILED": "O jogo iniciou, mas não foi possível salvar as informações de recuperação da sessão.",
    "error.GAME_EXITED_RESTORE_REQUIRED": "O jogo foi encerrado. Escolha Fechar jogo antes de iniciar novamente.",
  },
});

export function withHomeLaunchFeedback(language, catalog) {
  return { ...catalog, ...HOME_LAUNCH_FEEDBACK[language] };
}
