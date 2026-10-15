// Ghost reward filter state helpers. Rewards are matched by the authoritative
// stored reward identity (`reward:<type>:<itemId>`), never by name, quality or
// list position. AND ("all") is the default; OR ("any") is an explicit choice.
export const GHOST_REWARD_MAX_SELECTION = 24;
export const GHOST_REWARD_MATCH_DEFAULT = "all";

// A minimum is optional. When present it must be a plain positive whole number.
export function parseGhostRewardMinimum(raw) {
  const text = String(raw ?? "").trim();
  if (text === "") return { valid: true, value: null };
  if (!/^[1-9]\d{0,15}$/.test(text)) return { valid: false, value: null };
  const value = Number(text);
  return Number.isSafeInteger(value) ? { valid: true, value } : { valid: false, value: null };
}

export function toggleGhostReward(filters, key) {
  const current = Array.isArray(filters) ? filters : [];
  if (!key) return [];
  if (current.some((filter) => filter.key === key)) return current.filter((filter) => filter.key !== key);
  return current.length >= GHOST_REWARD_MAX_SELECTION ? current : [...current, { key, min: "" }];
}

export function setGhostRewardMinimum(filters, key, raw) {
  return (Array.isArray(filters) ? filters : []).map((filter) => filter.key === key ? { ...filter, min: String(raw ?? "") } : filter);
}

// Drop selections that the selected server no longer offers, so a choice made on
// one saved server can never silently constrain another server's tasks.
export function pruneGhostRewards(filters, items) {
  const offered = new Set((Array.isArray(items) ? items : []).map((item) => item?.key));
  const current = Array.isArray(filters) ? filters : [];
  const kept = current.filter((filter) => offered.has(filter.key));
  return kept.length === current.length ? current : kept;
}

// Returns { valid:false } for an unusable minimum, otherwise the query fragment
// ({} when nothing is selected).
export function buildGhostRewardQuery(filters, match) {
  const current = Array.isArray(filters) ? filters : [];
  if (current.length === 0) return { valid: true, fragment: {} };
  const rewardFilters = [];
  for (const filter of current) {
    const minimum = parseGhostRewardMinimum(filter.min);
    if (!minimum.valid) return { valid: false, fragment: {} };
    rewardFilters.push(minimum.value === null ? { key: filter.key } : { key: filter.key, minCount: minimum.value });
  }
  return { valid: true, fragment: { rewardFilters, rewardMatch: match === "any" ? "any" : GHOST_REWARD_MATCH_DEFAULT } };
}

// Unknown is not empty: only a non-empty list of keyed, positive-amount entries is authoritative.
export function ghostRewardsKnown(row) {
  return Array.isArray(row?.rewards) && row.rewards.some((item) => item && typeof item.key === "string" && item.key && Number(item.count) > 0);
}
