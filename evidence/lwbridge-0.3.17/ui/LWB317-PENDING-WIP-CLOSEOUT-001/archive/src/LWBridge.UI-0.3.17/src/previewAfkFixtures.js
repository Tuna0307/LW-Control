// Browser-only deterministic fixtures for recovered AFK presentation branches.
// Names/IDs/counts are synthetic and must not be treated as current game data.

export const previewAllianceMembers = [
  { uid: "10000", name: "Fixture Self", level: 30, power: 19000, online: true, self: true },
  { uid: "10001", name: "Fixture Avery", level: 30, power: 18240, online: true },
  { uid: "10002", name: "Fixture Blair", level: 29, power: 16480, online: false },
  { uid: "10003", name: "Fixture Casey", level: 28, power: 15120, online: true },
];

export function previewMemberFixture(previewState) {
  if (previewState === "squads-profile-members-loading") return { ready: false, failed: false, online: true, selfUid: "10000", members: [] };
  if (previewState === "squads-profile-members-failed") return { ready: false, failed: true, online: true, selfUid: "10000", members: [] };
  if (previewState === "squads-profile-members-offline") return { ready: false, failed: false, online: false, selfUid: "10000", members: [] };
  if (previewState === "squads-profile-members-empty") return { ready: true, failed: false, online: true, selfUid: "10000", members: [previewAllianceMembers[0]] };
  return { ready: true, failed: false, online: true, selfUid: "10000", members: previewAllianceMembers };
}

export const previewGarrisonBuildings = [
  { key: "building:center", buildId: 1, role: "center", name: "Fixture Alliance Center", available: true },
  { key: "building:attachment", buildId: 2, role: "attachment", name: "Fixture Adjacent Building", available: true },
];

export function previewGarrisonRuntime(previewState) {
  if (previewState === "squads-profile-garrison-unavailable") {
    return {
      buildings: previewGarrisonBuildings.map((entry, index) => ({ ...entry, available: false, unavailableReason: index === 0 ? "season_settled" : "full" })),
      assignments: [],
      guardingCount: 0,
      lastError: "",
    };
  }
  if (previewState === "squads-profile-garrison-running") {
    return {
      buildings: previewGarrisonBuildings,
      assignments: [{ squadIndex: 1, targetKey: "building:center", targetName: "Fixture Alliance Center", state: "guarding", owned: true }],
      guardingCount: 1,
      lastError: "",
    };
  }
  return { buildings: previewGarrisonBuildings, assignments: [], guardingCount: 0, lastError: previewState === "squads-profile-garrison-error" ? "common.actionFailed" : "" };
}

export function previewZombieBusRuntime(previewState) {
  if (previewState === "squads-profile-zombie-error") return { state: "error", lastError: "common.actionFailed", assignments: [] };
  if (previewState === "squads-profile-zombie-running") return {
    state: "running",
    lastError: "",
    assignments: [
      { squadIndex: 1, uid: "10001", name: "Fixture Avery", nameKey: "", gold: true, state: "guarding" },
      { squadIndex: 2, uid: "10003", name: "Fixture Casey", nameKey: "", gold: false, state: "marching" },
    ],
  };
  return { state: "waiting", lastError: "", assignments: [] };
}

export function initialAfkToolbarConfig(previewState) {
  const drillRuntime = previewState.startsWith("squads-profile-drill-");
  const garrisonRuntime = previewState.startsWith("squads-profile-garrison-");
  const zombieRuntime = previewState.startsWith("squads-profile-zombie-");
  const potionFixture = previewState.startsWith("squads-profile-potion-");
  return {
    masterEnabled: false,
    potionEnabled: potionFixture,
    minStamina: previewState === "squads-profile-potion-invalid" ? 10000 : 50,
    preferFifty: true,
    allianceDrill: {
      enabled: drillRuntime,
      activeRally: false,
      squadIndexes: [1],
      joinRestrictions: {
        enabled: false,
        mode: "delay",
        slotRange: [2, 2],
        slotDelaySeconds: 0,
        delaySeconds: [1, 3],
        maxWaitMinutes: 5,
        leaderListMode: "off",
        leaders: [],
        skipSoloLeader: false,
        skipKicked: true,
      },
    },
    garrison: {
      enabled: garrisonRuntime,
      recallOnDisable: true,
      targets: [{ kind: "allianceBuilding", buildId: 1, nameSnapshot: "Fixture Alliance Center" }],
      squadPriority: [1, 2],
    },
    zombieBus: { enabled: zombieRuntime },
  };
}
