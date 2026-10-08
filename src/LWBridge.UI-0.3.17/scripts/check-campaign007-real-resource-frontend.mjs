import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { createMapApi, buildSearchPayload, MAP_PAGE_SIZE } from "../src/mapBackend.js";

if (process.argv.length < 4) {
  throw new Error("Usage: check-campaign007-real-resource-frontend.mjs <native-real-payload.json> <output.json>");
}
const payloadPath = resolve(process.argv[2]);
const resultPath = resolve(process.argv[3]);
const real = JSON.parse(readFileSync(payloadPath, "utf8"));
if (!real.proofType.includes("REAL_V22_CAPTURED")) throw new Error("not actual native command data");
const response = real.profileA;
const commands = [];
const listeners = [];
let generation = 1;
let profileId = "A";
let deferredEventCount = 0;
const bridge = {
  get profileId() { return profileId; },
  currentProfileOwner: () => ({ profileId, generation }),
  isCurrentProfileOwner: (owned) => owned.profileId === profileId && owned.generation === generation,
  invokeProfileScoped: async (command, args) => {
    commands.push({ profileId, generation, command, args });
    if (command === "map_search") {
      if (args.kind !== "resource" || args.query.serverId !== 2212 ||
          args.query.pageSize !== MAP_PAGE_SIZE) throw new Error("not production Resource envelope");
      if (profileId === "B") return real.profileB.firstPage;
      if (args.query.resourceNameKey === "__campaign007_no_such_resource_key__")
        return response.impossibleResourceFilter;
      if (args.query.resourceNameKey) {
        if (args.query.resourceNameKey !== "129027") throw new Error("unexpected real filter");
        return response.positiveResourceFilter;
      }
      return args.query.page === 2 ? response.secondPage : response.firstPage;
    }
    if (command === "map_data_options") return profileId === "B"
      ? { serverId:2212, counts:{resource:0}, names:{resource:[],monster:[]} }
      : response.options;
    if (command === "map_summary") return response.offlineSummary;
    throw new Error("unpermitted native command in frontend replay: "+command);
  },
  listen: (eventName, callback) => {
    const lease = { eventName, callback, isActive: true };
    listeners.push(lease);
    return () => { lease.isActive = false; };
  },
};
const require = (condition, message) => { if (!condition) throw new Error("CAMPAIGN007 FRONTEND: "+message); };
const keys = (rows) => rows.map(row => row.recordKey);
const apiA = createMapApi(bridge);
const first = await apiA.search("resource", { serverId: 2212, page: 1 });
const second = await apiA.search("resource", { serverId: 2212, page: 2 });
require(first.total === 8008 && first.rows.length === 50 &&
  second.total === 8008 && second.rows.length === 50, "real page1/page2 consumption");
require(keys(first.rows).every(k => !keys(second.rows).includes(k)), "real native pages overlap");
const filt = await apiA.search("resource", {serverId:2212, resourceNameKey:"129027"});
require(filt.total === 2608 && filt.rows.every(r=>r.resourceNameKey==="129027"),
  "real native positive filter normalization");
const absent = await apiA.search("resource", {
  serverId: 2212, resourceNameKey:"__campaign007_no_such_resource_key__",
});
require(absent.total === 0 && absent.rows.length === 0,"real negative filter");
const options = await apiA.dataOptions(2212);
const summary = await apiA.summary();
require(options.counts.resource === 8008 && options.names.resource.length === 4,
  "native options normalized for Resource");
require(summary.serverId === 0 && summary.counts.resource === 0,
  "offline summary must not fake live context");
let eventCountA = 0;
const unlistenA = apiA.listenScanStatus?.(()=>{eventCountA++;}) || (()=>{});
const savedListeners = [...listeners];
profileId = "B"; generation++;
const apiB = createMapApi(bridge);
const b = await apiB.search("resource", {serverId:2212,page:1});
require(b.total===0 && b.rows.length===0, "B inherited A");
for(const ev of savedListeners) if(ev.eventName==="bridge://map-scan-status") ev.callback({
  profileId:"A", payload:{isReading:true,scanRunId:"obsolete-A"}
});
require(eventCountA===0,"obsolete A event delivered after B owner");
profileId="A";generation++;
const apiANew = createMapApi(bridge);
const reopened = await apiANew.search("resource", {serverId:2212,page:1});
require(reopened.total===8008 && keys(reopened.rows).join("|")===keys(first.rows).join("|"),"A/B/A retained rows");
const expected = buildSearchPayload("resource", {serverId:2212,page:2});
require(expected.kind==="resource" && expected.query.page===2 &&
  expected.query.pageSize===50 && expected.query.sorts?.[0]?.sortOrder==="desc",
  "actual frontend builder envelope mismatch");
require(commands.every(call=>["map_search","map_data_options","map_summary"].includes(call.command)),
  "unexpected action command");
const report = {
  proofType:"actual 0.3.17 canonical mapBackend createMapApi code consuming replayed ACTUAL native Resource command responses; NO mounted DOM/game/original runtime",
  originalParity:"NOT_PROVEN",
  payloadPath, total:first.total, page1Count:first.rows.length,page2Count:second.rows.length,
  positiveFilterCount:filt.total,negativeFilterCount:absent.total,
  nativeResourceOptions:options.names.resource.length, profileBTotal:b.total,
  reopenedATotal:reopened.total, rejectedOldProfileEvent:true, offlineSummaryServerId:summary.serverId,
  commandCount:commands.length, commands, externalGameActions:0
};
writeFileSync(resultPath,JSON.stringify(report,null,2)+"\n");
unlistenA();
console.log("CAMPAIGN007_REAL_RESOURCE_FRONTEND_ADAPTER_PASS page2 filter options A/B/A stale-event; 0 game actions");
