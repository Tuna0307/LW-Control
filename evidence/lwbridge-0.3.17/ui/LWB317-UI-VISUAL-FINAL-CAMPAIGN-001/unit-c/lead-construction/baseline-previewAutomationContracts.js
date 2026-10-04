// AutomationPanel-BJ0gIqFh.js: task adapter valid(), Vr (chat), Wr/Gr (assist).
export function initialAutomationDraft(title, previewState = "") {
  const fixture = previewState.startsWith("automation-");
  if (title === "Automatic Construction") return fixture ? { enabled: false, constructionTargetEnabled: true, constructionBuildingTypeIds: [1101, 1201], constructionTargetLevel: "30", maxBuilders: "1", autoClaimCompleted: true } : {};
  if (title === "Auto Training") return { enabled: false, trainingTotalCount: fixture ? previewState === "automation-validation-error" ? 1000001 : 1000 : 0, trainingTargetLevel: previewState === "automation-training-unavailable-level" ? 9 : 0 };
  if (title === "Automatic Treatment") return { enabled: false, amountPerArmy: "1" };
  if (title === "Automatic Official Application") return { enabled: false, positionId: "0" };
  if (title === "Alliance Tech Donations") return { enabled: false, threshold: "15" };
  if (title === "Alliance Gifts") return { enabled: false, intervalMinutes: "120" };
  if (["Excavation Stronghold Resources", "Alliance Center Resources", "Building Resource Collection", "Armed Truck"].includes(title)) return { enabled: false, intervalMinutes: "60" };
  if (title === "Alliance Gathering Dispatch") return { enabled: false, allianceGatherSquads: [1, 2] };
  if (title === "Automatic Alliance Train Boarding") return { enabled: false, trainMode: "reward", vipTrainMode: "reward", normalFixedCarriageIds: [1], vipFixedCarriageIds: [1, 2], preferredRewardKeys: ["fixture-medal"], preferRewardQuantity: false, autoAcceptVip: false, thanksMode: "like", ticketCount: "1" };
  if (title === "Trucks") return { enabled: false, delayMinutes: "2", weeklyQualities: ["ssr", "ur", "ssr", "ssr", "ssr", "ur", "ssr"], departWhenTicketsInsufficient: false };
  if (title === "Secret Task") return { enabled: false, collectRewards: false, delayMinutes: "3", weeklyQualities: ["none", "ur", "none", "none", "none", "ur", "none"], dispatchAssistEnabled: previewState === "automation-assist-auto", assistQualities: ["ssr", "ur"], assistMin: "0", assistMax: "0", assistInterval: "30" };
  if (title === "Ghost Ops") return { enabled: false, ghostJoinEnabled: false, ghostFilter: "special", ghostClaimRewards: false };
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) return { enabled: false, claimMin: "0", claimMax: "0", replyEnabled: false, replyMin: "2", replyMax: "5", replies: "", ...(title === "Treasure" ? { treasureSearchEnabled: false, treasureDispatchEnabled: false, dispatchMin: "2", dispatchMax: "5", dispatchRetry: "30", dispatchSquads: [1] } : {}) };
  return { enabled: false };
}

export function automationDraftError(title, draft) {
  const ranges = {
    constructionTargetLevel: [1, 100], maxBuilders: [1, 20], amountPerArmy: [1, 1000000],
    threshold: [1, 30], intervalMinutes: [1, 1440], delayMinutes: [0, 1440],
    ticketCount: [1, 9999], assistInterval: [5, 300], positionId: [0, 10007],
  };
  for (const [key, [min, max]] of Object.entries(ranges)) {
    if (!(key in draft)) continue;
    const value = Number(draft[key]);
    if (String(draft[key]).trim() === "" || !Number.isInteger(value) || value < min || value > max) {
      return key === "maxBuilders" ? "automation.builderLimitError" : key === "amountPerArmy" ? "automation.treatmentAmountError" : key === "intervalMinutes" ? "automation.intervalError" : "configSave.failed";
    }
  }
  if ("trainingTotalCount" in draft && (!Number.isInteger(Number(draft.trainingTotalCount)) || Number(draft.trainingTotalCount) < 0 || Number(draft.trainingTotalCount) > 1000000 || (draft.trainEnabled && Number(draft.trainingTotalCount) === 0))) return "automation.soldierTraining.quantityError";
  if (["Red Packet", "Fireworks / Egg", "Treasure"].includes(title)) {
    const pairValid = (minKey, maxKey, defaults, limitMin, limitMax) => {
      const min = String(draft[minKey] ?? defaults[0]), max = String(draft[maxKey] ?? defaults[1]);
      return min.trim() !== "" && max.trim() !== "" && Number.isFinite(Number(min)) && Number.isFinite(Number(max)) && Number(min) >= limitMin && Number(max) <= limitMax && Number(min) <= Number(max);
    };
    if (!pairValid("claimMin", "claimMax", [0, 0], 0, title === "Treasure" ? 600 : 60)) return title === "Treasure" ? "automation.treasureDelayError" : title === "Red Packet" ? "automation.redPacketDelayError" : "automation.fireworksDelayError";
    if (!pairValid("replyMin", "replyMax", [2, 5], 0.1, 600) || (title === "Treasure" && !pairValid("dispatchMin", "dispatchMax", [2, 5], 0.1, 600))) return "automation.interactionDelayError";
    if (draft.replyEnabled && !String(draft.replies ?? "").split(/\r?\n/).some((line) => line.trim())) return "automation.replyRequired";
    if (title === "Treasure" && draft.treasureDispatchEnabled && !(draft.dispatchSquads ?? [1]).length) return "automation.treasureDispatchSquadRequired";
    const retry = Number(draft.dispatchRetry ?? 30);
    if (title === "Treasure" && (!Number.isInteger(retry) || retry < 1 || retry > 300)) return "automation.treasureDispatchRetryError";
  }
  if (title === "Secret Task") {
    const min = Number(draft.assistMin ?? 0), max = Number(draft.assistMax ?? 0), interval = Number(draft.assistInterval ?? 30);
    if (!Number.isInteger(min) || !Number.isInteger(max) || min < 0 || max > 86400 || min > max || !Number.isInteger(interval) || interval < 5 || interval > 300 || (draft.dispatchAssistEnabled && !(draft.assistQualities ?? []).length)) return "automation.dispatchAssistConfigError";
  }
  if (["Trucks", "Secret Task"].includes(title)) {
    const weekly = draft.weeklyQualities;
    if (!Array.isArray(weekly) || weekly.length !== 7 || weekly.some((quality) => !["none", "ssr", "ur"].includes(quality))) return "configSave.failed";
  }
  return "";
}

// Synthetic order data, consumed through he()'s recovered rendering contract.
export function previewTrainingOrder(state) {
  if (state === "automation-training-no-order") return null;
  return { orderId: "fixture-order", activationId: "fixture-activation", completed: 360, total: 1000, producing: 240, pending: 40, reason: state === "automation-training-error" ? "unconfirmed" : "working" };
}

export function activateTraining(draft, enabled, order, newId = () => crypto.randomUUID()) {
  if (!enabled) return { ...draft, enabled: false };
  const total = draft.trainingTotalCount;
  if (!Number.isInteger(total) || total <= 0 || total > 1000000) return draft;
  const reusable = order && !["identity_changed", "unconfirmed"].includes(order.reason) && order.total === total && order.completed < order.total && order.orderId;
  return { ...draft, enabled: true, trainEnabled: true, promoteEnabled: true, collectEnabled: true, orderId: reusable ? order.orderId : newId(), activationId: newId() };
}
