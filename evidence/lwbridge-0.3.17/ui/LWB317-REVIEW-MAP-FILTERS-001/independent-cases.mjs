import assert from "node:assert/strict";
import { getMapPreviewProvider } from "../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js";
import { buildMapColumns } from "../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js";

const t = (key) => key;

const checking = getMapPreviewProvider("preview", "map-treasure-checking");
assert(checking);
assert.equal(checking.previewTreasureStatesRefreshing, true);

const checkingResult = await checking.mapApi.search("treasure", { serverId: 321, page: 1, pageSize: 100 });
assert(checkingResult.rows.length >= 4);

const treasureColumns = buildMapColumns("treasure", t, "en-US", checking.gameTexts, "", true);
const worldColumn = treasureColumns.find((column) => column.label === "map.treasureWorldState");
const playerColumn = treasureColumns.find((column) => column.label === "map.treasurePlayerState");
assert(worldColumn && playerColumn);

// Missing states become Checking/Verifying while the explicit preview flag is on.
assert.equal(worldColumn.value(checkingResult.rows[0]), "map.treasureStateVerifying");
assert.equal(playerColumn.value(checkingResult.rows[0]), "map.treasurePlayerVerifying");

// A blocking reason still outranks the synthetic missing-state fallback.
assert.equal(worldColumn.value(checkingResult.rows[2]), "map.treasureStateVerifying");
assert.equal(playerColumn.value(checkingResult.rows[2]), "map.treasurePlayerOtherAlliance");

// Known states are preserved by the same columns while refresh is active.
const known = checkingResult.rows.find((row) => row.worldClaimState && row.playerClaimState);
assert(known);
assert.notEqual(worldColumn.value(known), "map.treasureStateVerifying");
assert.notEqual(playerColumn.value(known), "map.treasurePlayerVerifying");

// The checking input is fenced from other fixtures and non-preview bridge modes.
const tableStates = getMapPreviewProvider("preview", "map-table-states");
assert(tableStates);
assert.equal(tableStates.previewTreasureStatesRefreshing, false);
assert.equal(getMapPreviewProvider("native", "map-treasure-checking"), null);
assert.equal(getMapPreviewProvider("native-unavailable", "map-treasure-checking"), null);
assert.equal(getMapPreviewProvider("preview", "home"), null);

console.log(JSON.stringify({
  status: "PASS",
  checkingRows: checkingResult.rows.length,
  missingState: {
    world: worldColumn.value(checkingResult.rows[0]),
    player: playerColumn.value(checkingResult.rows[0]),
  },
  blockedMissingState: {
    world: worldColumn.value(checkingResult.rows[2]),
    player: playerColumn.value(checkingResult.rows[2]),
  },
  knownState: {
    worldInput: known.worldClaimState,
    playerInput: known.playerClaimState,
    worldOutput: worldColumn.value(known),
    playerOutput: playerColumn.value(known),
  },
  fences: {
    mapTableStatesRefreshing: tableStates.previewTreasureStatesRefreshing,
    nativeProvider: null,
    nativeUnavailableProvider: null,
    nonMapPreviewProvider: null,
  },
}, null, 2));
