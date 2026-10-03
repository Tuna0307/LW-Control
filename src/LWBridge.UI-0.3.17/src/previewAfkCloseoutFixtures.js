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
  if (!['squads-profile-drill-leading', 'squads-profile-drill-joining', 'squads-profile-drill-waiting'].includes(state)) return [];
  return [{ activity: 'allianceDrill', running: true, activityRole: state.endsWith('leading') ? 'leader' : 'member', squadIndex: 1,
    step: state.endsWith('waiting') ? 'waiting_join_delay' : 'joining', joinTargetName: 'Fixture Drill rally', joinWaitSeconds: 3 }];
}
export function previewMemberFixture(state) {
  return isAfkPreview(state) ? baseMemberFixture(state)
    : { ready: false, failed: false, online: false, selfUid: "", members: [] };
}
export function previewGarrisonRuntime(state) {
  if (!isAfkPreview(state)) return { buildings: [], assignments: [], guardingCount: 0, lastError: "" };
  const runtime = baseGarrisonRuntime(state);
  return { ...runtime, buildings: runtime.buildings.map(building => ({ ...building,
    key: `building:${building.buildId}`, full: building.unavailableReason === "full" })) };
}
export function previewZombieBusRuntime(state) {
  return isAfkPreview(state) ? baseZombieRuntime(state)
    : { state: "", assignments: [], lastError: "" };
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
    masterEnabled: previewState === "squads-profile-master-enabled",
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
