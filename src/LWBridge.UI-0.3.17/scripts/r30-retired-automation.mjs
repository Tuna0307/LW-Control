// Owner-directed R30 removal. Applied to every regenerated locale.
export function retiredAutomationKey(key) {
  return /^automation\.(soldierTraining|construction|autoTraining|autoConstruction|railway|buildingResources|armedTruckReward|weekendShield|attackShield|freeStamina|stamina|allianceTrainRide|truck)\./.test(key)
    || /^automation\.(train|weekendWindow)/.test(key)
    || ["automation.category.resources", "automation.category.system", "automation.currentShield", "automation.railwayDepartWhenTicketsInsufficient"].includes(key);
}
