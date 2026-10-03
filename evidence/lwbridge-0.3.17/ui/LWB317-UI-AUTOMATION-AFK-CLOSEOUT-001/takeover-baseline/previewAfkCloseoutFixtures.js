// Task-owned AFK closeout adapter. Keep previewAfkFixtures.js untouched because it
// is protected pre-existing WIP in this assignment.
import {
  initialAfkToolbarConfig as baseInitialAfkToolbarConfig,
  previewAllianceMembers,
  previewGarrisonBuildings,
  previewGarrisonRuntime,
  previewMemberFixture,
  previewZombieBusRuntime,
} from "./previewAfkFixtures.js";

export { previewAllianceMembers, previewGarrisonBuildings, previewGarrisonRuntime, previewMemberFixture, previewZombieBusRuntime };

export function initialAfkToolbarConfig(previewState) {
  const base = baseInitialAfkToolbarConfig(previewState);
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
