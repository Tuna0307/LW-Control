// Task-owned AFK closeout adapter. Keep previewAfkFixtures.js untouched because it
// is protected pre-existing WIP in this assignment.
import {
  initialAfkToolbarConfig as baseInitialAfkToolbarConfig,
  previewAllianceMembers,
  previewGarrisonBuildings,
  previewGarrisonRuntime as baseGarrisonRuntime,
  previewMemberFixture as baseMemberFixture,
  previewZombieBusRuntime as baseZombieRuntime,
} from "./previewAfkFixtures.js";

export { previewAllianceMembers, previewGarrisonBuildings };

const isAfkPreview = (state) => state.startsWith("squads-profile");
// Synthetic supplied rows use the exact I runtime consumer fields. No executor.
export function previewDrillRuntime(state) {
  if (!['squads-profile-drill-leading', 'squads-profile-drill-joining', 'squads-profile-drill-waiting', 'squads-profile-drill-waiting-translated'].includes(state)) return [];
  return [{ activity: 'allianceDrill', running: true, activityRole: state.endsWith('leading') ? 'leader' : 'member', squadIndex: 1,
    step: state.includes('waiting') ? 'waiting_join_delay' : 'joining', joinTargetNameKey: state.endsWith('translated') ? 'fixture.drillLeader' : '', joinTargetName: 'Fixture Drill rally', joinWaitSeconds: 3 }];
}
export function previewMemberFixture(state) {
  return isAfkPreview(state) ? baseMemberFixture(state)
    : { ready: false, failed: false, online: false, selfUid: "", members: [] };
}
export function previewGarrisonRuntime(state) {
  if (!isAfkPreview(state)) return { buildings: [], assignments: [], guardingCount: 0, lastError: "" };
  if (state === "squads-profile-garrison-discovery-pending") return { discoveryReady: false, discoveryError: "", buildings: [], assignments: [], guardingCount: 0, lastError: "" };
  if (state === "squads-profile-garrison-discovery-failed") return { discoveryReady: false, discoveryError: "common.actionFailed", buildings: [], assignments: [], guardingCount: 0, lastError: "" };
  if (state === "squads-profile-garrison-localized") return {
    discoveryReady: true,
    discoveryError: "",
    gameTexts: { "fixture.garrison.center": "Fixture Translated Center" },
    buildings: [{ key: "building:1", buildId: 1, role: "center", name: "Fixture Fallback Center", nameKey: "fixture.garrison.center", allianceAbbr: "FX", available: true, full: false }],
    assignments: [], guardingCount: 0, lastError: "",
  };
  const runtime = baseGarrisonRuntime(state);
  return { ...runtime, discoveryReady: true, discoveryError: "", buildings: runtime.buildings.map(building => ({ ...building,
    key: `building:${building.buildId}`, full: building.unavailableReason === "full" })) };
}
export function previewZombieBusRuntime(state) {
  return isAfkPreview(state) ? baseZombieRuntime(state)
    : { state: "", assignments: [], lastError: "" };
}

export function previewAfkProfileRuntime(state) {
  if (!["squads-profile-runtime", "squads-profile-runtime-error", "squads-profile-runtime-translated"].includes(state)) return [];
  return [
    {
      squadIndex: 1,
      profileId: "steel",
      activity: "monsterSweep",
      running: true,
      step: "waiting_join_delay",
      joinTargetNameKey: state.endsWith("translated") ? "fixture.runtimeLeader" : "",
      joinTargetName: "Fixture Leader",
      joinWaitSeconds: 4,
      strategyProcessed: { steel: 2 },
      lastError: state.endsWith("-error") ? "GAME_CONNECTION_UNAVAILABLE" : "",
    },
    {
      squadIndex: 2,
      profileId: "gold",
      activity: "monsterSweep",
      running: false,
      completedStrategyIds: ["gold"],
      strategyProcessed: { gold: 3 },
      lastError: "",
    },
  ];
}

export function previewAfkRuntimeNames(state) {
  if (state === "squads-profile-runtime-translated") return { "fixture.runtimeLeader": "Fixture Translated Leader" };
  if (state === "squads-profile-drill-waiting-translated") return { "fixture.drillLeader": "Fixture Translated Drill Leader" };
  return {};
}

export function previewAfkAvailableSquads(state) {
  if (state === "squads-profile-squads-34") return [3, 4];
  if (["squads-profile-squads-empty", "squads-profile-garrison-discovery-pending", "squads-profile-garrison-discovery-failed"].includes(state)) return [];
  return [1, 2, 3, 4];
}

export function previewAfkMasterStopAck(state) {
  return state === "squads-profile-master-stop-busy" ? new Promise(() => {}) : Promise.resolve();
}

export function initialAfkToolbarConfig(previewState) {
  const base = baseInitialAfkToolbarConfig(previewState);
  if (!isAfkPreview(previewState)) return {
    ...base, masterEnabled: false, potionEnabled: false,
    allianceDrill: { ...base.allianceDrill, enabled: false },
    garrison: { enabled: false, recallOnDisable: true, squadPriority: [], targets: [] },
    zombieBus: { enabled: false },
  };
  const drill = previewState.startsWith("squads-profile-drill-");
  const garrison = previewState.startsWith("squads-profile-garrison-");
  const zombie = previewState.startsWith("squads-profile-zombie-");
  const drillEmpty = previewState === "squads-profile-drill-empty";
  const garrisonNoTargets = previewState === "squads-profile-garrison-no-targets";
  const garrisonNoSquads = previewState === "squads-profile-garrison-no-squads";
  return {
    ...base,
    masterEnabled: previewState === "squads-profile-master-enabled" || previewState === "squads-profile-master-stop-busy",
    potionEnabled: previewState.startsWith("squads-profile-potion-"),
    allianceDrill: {
      ...base.allianceDrill,
      enabled: drill && !drillEmpty,
      squadIndexes: drillEmpty ? [] : base.allianceDrill.squadIndexes,
    },
    garrison: {
      ...base.garrison,
      enabled: garrison && !garrisonNoTargets && !garrisonNoSquads,
      targets: garrisonNoTargets ? [] : base.garrison.targets,
      squadPriority: garrisonNoSquads ? [] : base.garrison.squadPriority,
    },
    zombieBus: { ...base.zombieBus, enabled: zombie },
  };
}

export function previewGarrisonMemberFixture(previewState) {
  if (previewState === "squads-profile-garrison-discovery-pending") return { ready: false, failed: false, online: true, selfUid: "10000", members: [] };
  if (previewState === "squads-profile-garrison-discovery-failed") return { ready: false, failed: true, online: true, selfUid: "10000", members: [] };
  const fixture = previewMemberFixture(previewState);
  if (!fixture.ready) return fixture;
  return {
    ...fixture,
    members: fixture.members.map((member) => {
      if (member.uid === fixture.selfUid) return { ...member, available: false, unavailableReason: "self" };
      if (previewState === "squads-profile-garrison-unavailable" && member.uid === "10003") {
        return { ...member, available: false, unavailableReason: "cross_server" };
      }
      return { ...member, available: true, unavailableReason: "" };
    }),
  };
}
