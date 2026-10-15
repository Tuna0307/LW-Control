// Current-client session persistence feedback; original catalogs stay verbatim.
export const HOME_LAUNCH_FEEDBACK = Object.freeze({
  en: { "error.RECOVERY_RECORD_COMMIT_FAILED": "The game started, but its recovery information could not be saved." },
  "zh-CN": { "error.RECOVERY_RECORD_COMMIT_FAILED": "游戏已启动，但无法保存游戏会话的恢复信息。" },
  "zh-TW": { "error.RECOVERY_RECORD_COMMIT_FAILED": "遊戲已啟動，但無法儲存遊戲工作階段的恢復資訊。" },
  ja: { "error.RECOVERY_RECORD_COMMIT_FAILED": "ゲームは起動しましたが、セッションの復元情報を保存できませんでした。" },
  ko: { "error.RECOVERY_RECORD_COMMIT_FAILED": "게임이 시작되었지만 세션 복구 정보를 저장하지 못했습니다." },
  vi: { "error.RECOVERY_RECORD_COMMIT_FAILED": "Trò chơi đã khởi động nhưng không thể lưu thông tin khôi phục phiên." },
  id: { "error.RECOVERY_RECORD_COMMIT_FAILED": "Game dimulai, tetapi informasi pemulihan sesi tidak dapat disimpan." },
  ru: { "error.RECOVERY_RECORD_COMMIT_FAILED": "Игра запущена, но не удалось сохранить данные восстановления сеанса." },
  pt: { "error.RECOVERY_RECORD_COMMIT_FAILED": "O jogo iniciou, mas não foi possível salvar as informações de recuperação da sessão." },
});

export function withHomeLaunchFeedback(language, catalog) {
  return { ...catalog, ...HOME_LAUNCH_FEEDBACK[language] };
}
