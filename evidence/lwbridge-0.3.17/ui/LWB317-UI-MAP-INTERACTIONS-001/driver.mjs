// Implementation-agnostic interaction driver.
//
// It operates ONLY on what a user could touch: the rendered element tree (inputs, selects, buttons, the
// table/pagination elements and their production callbacks) and on the search requests the provider
// received. The same driver therefore runs the original component (original/original-runtime.mjs), the
// canonical page and the immutable pre-campaign baseline, and scenario results can be compared like for like.
import assert from "node:assert/strict";
import { nodeText, treeNodes as treeNodesOf } from "./harness.mjs";

const ROLES = {
  scheduleDispatch: ["scheduleDispatchPlunder", "dispatchPlunderSchedule"],
  scheduleTruck: ["scheduleTruckPlunder", "truckPlunderSchedule"],
  share: ["shareDispatchToAlliance", "dispatchShareAlliance"],
  cancelDispatch: ["cancelDispatchPlunder", "dispatchPlunderCancel"],
  cancelTruck: ["cancelTruckPlunder", "truckPlunderCancel"],
  clearDispatch: ["clearDispatchPlunderHistory", "dispatchPlunderClear"],
  clearTruck: ["clearTruckPlunderHistory", "truckPlunderClear"],
  list: ["listPlunderJobs", "plunderJobsList"],
};
const NAME_LABELS = new Set(["common.name", "Resource name", "Monster name"]);
const DEFAULT_IGNORED_QUERY_KEYS = new Set(["pageSize"]);

export function normalizeQuery(query) {
  const out = {};
  for (const [key, value] of Object.entries(query)) {
    if (DEFAULT_IGNORED_QUERY_KEYS.has(key) || value === undefined || value === null || value === "") continue;
    out[key] = value;
  }
  return out;
}

export function createDriver(h) {
  const find = (predicate) => h.findNodes(predicate);
  const one = (predicate, what) => {
    const nodes = find(predicate);
    assert.ok(nodes.length >= 1, `${h.label}: ${what} not rendered`);
    return nodes[0];
  };
  const maybe = (predicate) => find(predicate)[0] || null;

  const keywordNode = () => maybe((node) => node.type === "input" && node.props?.["aria-label"] === "map.searchLabel");
  const nameNode = () => maybe((node) => node.type === "select" && NAME_LABELS.has(node.props?.["aria-label"]));
  const searchButton = () => maybe((node) => node.type === "button" && nodeText(node) === "common.search");
  const scheduleButtons = () => find((node) => node.type === "button" && String(node.props?.className || "").includes("map-schedule-button"));
  const delayNode = () => maybe((node) => node.type === "input" && node.props?.type === "number" && node.props?.min === "0");
  const tableNode = () => maybe((node) => typeof node.type === "function" && node.props && ("selectedKeys" in node.props || "selectedDispatchKeys" in node.props));
  const paginationNode = () => maybe((node) => typeof node.type === "function" && node.type.name === "Pagination")
    || maybe((node) => typeof node.type === "function" && node.props && "totalPages" in node.props && "page" in node.props);

  const driver = {
    h,
    async typeKeyword(text) {
      const node = keywordNode();
      assert.ok(node, `${h.label}: keyword input`);
      node.props.onChange({ target: { value: text } });
      await h.settle();
    },
    async clickSearch() {
      const node = searchButton();
      assert.ok(node, `${h.label}: search button`);
      assert.ok(!node.props.disabled, `${h.label}: search button is disabled`);
      node.props.onClick();
      await h.settle();
    },
    searchDisabled() { return Boolean(searchButton()?.props.disabled); },
    async selectName(value) {
      const node = nameNode();
      assert.ok(node, `${h.label}: name select`);
      node.props.onChange({ target: { value } });
      await h.settle();
    },
    nameOptions() {
      const node = nameNode();
      if (!node) return null;
      return find((child) => child.type === "option" && node.props.children !== undefined)
        .filter((option) => option.props?.value !== undefined)
        .map((option) => ({ value: option.props.value, text: nodeText(option) }));
    },
    async setDelay(text) {
      const node = delayNode();
      assert.ok(node, `${h.label}: random delay input`);
      node.props.onChange({ target: { value: text } });
      await h.settle();
    },
    delayState() {
      const node = delayNode();
      return node ? { value: node.props.value, disabled: Boolean(node.props.disabled), label: node.props["aria-label"] ?? null } : null;
    },
    scheduleButtonStates() {
      return scheduleButtons().map((node) => ({ text: nodeText(node), disabled: Boolean(node.props.disabled) }));
    },
    // Calls the production selection callback exactly as the table child does.
    async toggleRow(kind, row) {
      const table = tableNode();
      assert.ok(table, `${h.label}: table element`);
      const props = table.props;
      if ("onToggleDispatch" in props) {
        if (kind === "truck") props.onToggleTruck(row);
        else props.onToggleDispatch({ ...row, taskKind: kind === "ghost" ? "ghost" : "dispatch" });
      } else {
        props.onSelect(row);
      }
      await h.settle();
    },
    // Keys of the selection collection the ACTIVE table kind reads (Dispatch/Ghost map or Truck map).
    selectionKeys() {
      const table = tableNode();
      if (!table) return null;
      const props = table.props;
      const keys = "onToggleDispatch" in props ? (props.kind === "truck" ? props.selectedTruckKeys : props.selectedDispatchKeys) : props.selectedKeys;
      return [...keys].sort();
    },
    keywordValue() { return keywordNode()?.props.value ?? null; },
    nameValue() { return nameNode()?.props.value ?? null; },
    tableRows() { return tableNode()?.props.rows ?? null; },
    tableProps() { return tableNode()?.props ?? null; },
    // Map tab buttons: the ones that contain a `map-tab-label` span (the original omits role="tab").
    tabTexts() { return find((node) => node.type === "button" && treeNodesOf(node).some((child) => child.props?.className === "map-tab-label")).map((node) => nodeText(node)); },
    activeTabText() { return find((node) => node.type === "button" && node.props?.className === "active" && treeNodesOf(node).some((child) => child.props?.className === "map-tab-label")).map((node) => nodeText(node)); },
    messageText() { return find((node) => node.type === "span" && node.props?.className === "map-claim-result").map((node) => nodeText(node)); },
    async setMarkedOnly(checked) {
      const label = one((node) => node.type === "label" && String(node.props?.className || "").includes("map-filter-field"), "marked-only label");
      const input = treeNodesOf(label).find((node) => node.type === "input" && node.props?.type === "checkbox");
      assert.ok(input, `${h.label}: marked-only checkbox`);
      input.props.onChange({ target: { checked } });
      await h.settle();
    },
    async clickButtonByText(text) {
      const node = one((candidate) => candidate.type === "button" && nodeText(candidate) === text, `button ${text}`);
      assert.ok(!node.props.disabled, `${h.label}: button ${text} disabled`);
      node.props.onClick();
      await h.settle();
    },
    page() { return paginationNode()?.props.page ?? null; },
    async setPage(n) {
      const node = paginationNode();
      assert.ok(node, `${h.label}: pagination`);
      node.props.onPage(n);
      await h.settle();
    },
    requests() { return h.requests.map((request) => ({ id: request.id, kind: request.kind, query: normalizeQuery(request.query) })); },
    lastQuery() { return normalizeQuery(h.currentRequest()?.query || {}); },
    // ---- provider-call and Scheduled Plunder helpers (canonical method names and original stub names) -------------
    callsOf(role) { return h.calls.filter((call) => ROLES[role].includes(call.name)); },
    async settleFlow() {
      for (let i = 0; i < 4; i += 1) { await new Promise((resolve) => setImmediate(resolve)); await h.settle(); }
    },
    async resolveCall(call, value) { call.resolve(value); await driver.settleFlow(); },
    async rejectCall(call, error) { call.reject(error); await driver.settleFlow(); },
    setJobs(jobs) { if (h.setJobs) h.setJobs(jobs); else h.jobs.value = jobs; },
    async emitJobsChanged() {
      if (h.emitEvent) await h.emitEvent("bridge://dispatch-plunder-changed");
      else await h.ctx.jobsChanged();
      await driver.settleFlow();
    },
    // Normalised view of the Scheduled Plunder groups: job lists, shared busy key and the production callbacks.
    scheduled() {
      const nodes = find((node) => typeof node.type === "function" && node.props);
      const canonical = nodes.find((node) => node.type.name === "ScheduledPlunder");
      if (canonical) {
        const p = canonical.props;
        return {
          dispatchJobs: p.dispatchJobs.filter((job) => job.taskKind !== "ghost"), ghostJobs: p.dispatchJobs.filter((job) => job.taskKind === "ghost"), truckJobs: p.truckJobs,
          busyKey: p.busyKey, online: p.online, actionsEnabled: p.actionsEnabled,
          cancelDispatch: (job) => p.onCancelDispatch(job), cancelTruck: (job) => p.onCancelTruck(job), again: (job) => p.onPlunderAgain(job), clear: (kind) => p.onClear(kind),
        };
      }
      const groups = nodes.filter((node) => "jobs" in node.props && "onCancel" in node.props);
      if (!groups.length) return null;
      const secret = groups.filter((node) => !("onPlunderAgain" in node.props));
      const truck = groups.find((node) => "onPlunderAgain" in node.props);
      const dispatch = secret.find((node) => node.props.kind !== "ghost");
      const ghost = secret.find((node) => node.props.kind === "ghost");
      return {
        dispatchJobs: dispatch.props.jobs, ghostJobs: ghost.props.jobs, truckJobs: truck.props.jobs,
        busyKey: dispatch.props.busyKey, online: dispatch.props.online, actionsEnabled: true,
        cancelDispatch: (job) => dispatch.props.onCancel(job), cancelTruck: (job) => truck.props.onCancel(job), again: (job) => truck.props.onPlunderAgain(job),
        clear: (kind) => (kind === "truck" ? truck.props.onClear() : dispatch.props.onClear(kind)),
      };
    },
  };
  return driver;
}
