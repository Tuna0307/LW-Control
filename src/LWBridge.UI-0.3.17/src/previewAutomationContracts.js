// AutomationPanel-BJ0gIqFh.js: task adapter valid(), Vr (chat), Wr/Gr (assist).
export function initialAutomationDraft(title, previewState = "") {
  const fixture = previewState.startsWith("automation-");
  if (title === "Automatic Treatment") return { enabled: false, amountPerArmy: "1" };
  if (title === "Automatic Official Application") return { enabled: false, positionId: "0" };
  if (title === "Alliance Tech Donations") return { enabled: false, threshold: "15" };
  if (title === "Alliance Gifts") return { enabled: false, intervalMinutes: "120" };
  if (["Excavation Stronghold Resources", "Alliance Center Resources"].includes(title)) return { enabled: false, intervalMinutes: "60" };
  if (title === "Alliance Gathering Dispatch") return { enabled: false, allianceGatherSquads: previewState === "automation-squads-34" ? [3] : previewState === "automation-squads-empty" ? [] : [1, 2] };
  if (title === "Secret Task") return { enabled: previewState === "automation-runtime-running", collectRewards: false, delayMinutes: "3", weeklyQualities: ["none", "ur", "none", "none", "none", "ur", "none"] };
  if (title === "Dispatch Assist") return { autoHelp: previewState === "automation-assist-auto", qualities: ["ssr", "ur"], delaySeconds: ["0", "0"], intervalSeconds: "30" };
  if (["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title)) return { enabled: false, claimMin: "0", claimMax: "0", replyEnabled: false, replyMin: "2", replyMax: "5", replies: "", ...(title === "Treasure" ? { treasureSearchEnabled: false, treasureDispatchEnabled: previewState === "automation-squads-34", dispatchMin: "2", dispatchMax: "5", dispatchRetry: "30", dispatchSquads: previewState === "automation-squads-34" ? [3] : previewState === "automation-squads-empty" ? [] : [1] } : {}) };
  return { enabled: false };
}

export function automationDraftError(title, draft, maximumBuilders = 20) {
  const ranges = {
    amountPerArmy: [1, 1000000],
    threshold: [1, 30], intervalMinutes: [1, 1440], delayMinutes: [0, 1440],
    assistInterval: [5, 300], positionId: [0, 10007],
  };
  for (const [key, [min, max]] of Object.entries(ranges)) {
    if (!(key in draft)) continue;
    const value = Number(draft[key]);
    if (String(draft[key]).trim() === "" || !Number.isInteger(value) || !(value >= min && value <= max)) {
      return key === "amountPerArmy" ? "automation.treatmentAmountError" : key === "intervalMinutes" ? "automation.intervalError" : key === "delayMinutes" ? "automation.dailyDelayError" : "configSave.failed";
    }
  }
  if (title === "Automatic Official Application" && ![0, 10002, 10003, 10004, 10005, 10006, 10007].includes(Number(draft.positionId ?? 0))) return "automation.positionError";
  if (["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"].includes(title)) {
    const pairValid = (minKey, maxKey, defaults, limitMin, limitMax) => {
      const min = String(draft[minKey] ?? defaults[0]), max = String(draft[maxKey] ?? defaults[1]);
      return min.trim() !== "" && max.trim() !== "" && Number.isFinite(Number(min)) && Number.isFinite(Number(max)) && Number(min) >= limitMin && Number(max) <= limitMax && Number(min) <= Number(max);
    };
    if (!pairValid("claimMin", "claimMax", [0, 0], 0, title === "Treasure" ? 600 : 60)) return title === "Treasure" ? "automation.treasureDelayError" : title === "Red Packet" ? "automation.redPacketDelayError" : "automation.fireworksDelayError";
    if (!pairValid("replyMin", "replyMax", [2, 5], 0.1, 600) || (title === "Treasure" && !pairValid("dispatchMin", "dispatchMax", [2, 5], 0.1, 600))) return "automation.interactionDelayError";
    if (title === "Treasure" && draft.treasureDispatchEnabled && !(draft.dispatchSquads ?? [1]).length) return "automation.treasureDispatchSquadRequired";
    if (title === "Treasure" && Array.isArray(draft.claimSchedule) && draft.claimSchedule.some((row) => !/^\d\d:\d\d$/.test(row.start ?? "") || !/^\d\d:\d\d$/.test(row.end ?? "") || String(row.min).trim() === "" || String(row.max).trim() === "" || !(Number(row.min) >= 0) || !(Number(row.max) <= 600) || Number(row.min) > Number(row.max))) return "automation.treasureScheduleError";
    if (draft.activeHoursEnabled && (!/^\d\d:\d\d$/.test(draft.activeStart ?? "") || !/^\d\d:\d\d$/.test(draft.activeEnd ?? "") || draft.activeStart === draft.activeEnd)) return "automation.activeHoursError";
    const retry = Number(draft.dispatchRetry ?? 30);
    if (title === "Treasure" && (!Number.isInteger(retry) || retry < 1 || retry > 300)) return "automation.treasureDispatchRetryError";
  }
  if (title === "Dispatch Assist") {
    const min = Number(draft.delaySeconds?.[0] ?? 0), max = Number(draft.delaySeconds?.[1] ?? 0), interval = Number(draft.intervalSeconds ?? 30);
    if (!Number.isInteger(min) || !Number.isInteger(max) || min < 0 || max > 86400 || min > max || !Number.isInteger(interval) || interval < 5 || interval > 300 || (draft.autoHelp && !(draft.qualities ?? []).length)) return "automation.dispatchAssistConfigError";
  }
  if (["Secret Task"].includes(title)) {
    const weekly = draft.weeklyQualities;
    if (!Array.isArray(weekly) || weekly.length !== 7 || weekly.some((quality) => !["none", "ssr", "ur"].includes(quality))) return "configSave.failed";
  }
  return "";
}
