const assert = require('assert/strict');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const panel = fs.readFileSync(
  path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'MapDataPanel-C1HVeNHr.js'),
  'utf8');
const api = fs.readFileSync(
  path.join(root, 'src', 'LWBridge.Desktop', 'WebUi', 'assets', 'api-ClPPi2JT.js'),
  'utf8');

assert.ok(
  panel.includes('await ne(L);clearAutoSearchOnce.current=!0'),
  'Clear must pass the current positive live server ID through the original Qn handler');
assert.equal(
  panel.includes('await ne(0);clearAutoSearchOnce.current=!0'),
  false,
  'rebuild-only serverId=0 clear-all must remain removed');
assert.equal(
  (panel.match(/onClick:Qn/g) || []).length,
  1,
  'original Clear must remain Manual-only with exactly one Qn button binding');
assert.ok(api.includes('map_scan_clear'), 'generated API must retain map_scan_clear');

console.log('R8-008 strict map_scan_clear frontend contract check passed.');
