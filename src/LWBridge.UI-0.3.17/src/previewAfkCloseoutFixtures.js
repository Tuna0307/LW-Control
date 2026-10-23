// Task-owned AFK closeout adapter. Keep previewAfkFixtures.js untouched because it
// is protected pre-existing WIP in this assignment.
import {
  initialAfkToolbarConfig as baseInitialAfkToolbarConfig,
  previewAllianceMembers,
  previewMemberFixture as baseMemberFixture,
} from "./previewAfkFixtures.js";

export { previewAllianceMembers };

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
  if (["squads-profile-squads-empty"].includes(state)) return [];
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
  };
  const drill = previewState.startsWith("squads-profile-drill-");
  const drillEmpty = previewState === "squads-profile-drill-empty";
  return {
    ...base,
    masterEnabled: previewState === "squads-profile-master-enabled" || previewState === "squads-profile-master-stop-busy",
    potionEnabled: previewState.startsWith("squads-profile-potion-"),
    allianceDrill: {
      ...base.allianceDrill,
      enabled: drill && !drillEmpty,
      squadIndexes: drillEmpty ? [] : base.allianceDrill.squadIndexes,
    },
  };
}
