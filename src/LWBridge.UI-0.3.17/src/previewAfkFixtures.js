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

export function initialAfkToolbarConfig(previewState) {
  return {
    masterEnabled: false,
    potionEnabled: false,
    minStamina: previewState === "squads-profile-potion-invalid" ? 10000 : 50,
    preferFifty: true,
    allianceDrill: {
      enabled: false,
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
  };
}
