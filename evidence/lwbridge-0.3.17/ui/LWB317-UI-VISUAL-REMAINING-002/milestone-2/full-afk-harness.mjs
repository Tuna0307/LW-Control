import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {
  read, squad, compile, h, Fragment, hooks, flatten, text, stable,
} from '../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs';
import * as afk from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js';
import * as fixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js';
import en from '../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';
import ja from '../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js';

export const here = path.dirname(fileURLToPath(import.meta.url));
export const currentSource = read('evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-2/baseline/pre-fix-SquadsPage.jsx');
export const catalogs = {en, ja};
export const tFor = (catalog) => (key, vars = {}) => String(catalog[key] || key || '')
  .replace(/\{(\w+)\}/g, (match, name) => vars[name] ?? match);

export function profileFixture() {
  return {
    ...afk.makePreviewAfkProfile('runtime-profile', 'Runtime Profile', 'join', 'boss'),
    enabled: true,
    squadIndexes: [1, 2],
    executionLimit: 3,
  };
}

export function runtimeRows() {
  return [
    {
      squadIndex: 1,
      profileId: 'runtime-profile',
      activity: 'monsterSweep',
      running: true,
      step: 'waiting_join_delay',
      joinTargetName: 'Fixture Leader',
      joinWaitSeconds: 4,
      strategyProcessed: {'runtime-profile': 2},
      lastError: 'common.actionFailed',
    },
    {
      squadIndex: 2,
      profileId: 'other-profile',
      activity: 'monsterSweep',
      running: false,
      completedStrategyIds: ['runtime-profile'],
      strategyProcessed: {'runtime-profile': 3},
    },
  ];
}

function stateStore(draft, confirmed = structuredClone(draft)) {
  let current = draft;
  return {
    get draft() { return current; },
    confirmed,
    dirty: false,
    saving: false,
    error: null,
    state: {
      getSnapshot: () => ({draft: current, confirmed}),
      edit: (next) => { current = typeof next === 'function' ? next(current) : next; },
      flush: async () => current,
      refresh: async () => current,
      runAction: async (action) => action(),
    },
  };
}

export function renderOriginalProfile({language = 'en', busy = '', rows = runtimeRows()} = {}) {
  const t = tFor(catalogs[language]);
  const profile = profileFixture();
  const drill = {
    enabled: false,
    squadIndexes: [1, 2],
    activeRally: false,
    joinRestrictions: afk.normalizeJoinRestrictions(undefined, 1, true),
  };
  const monster = stateStore({enabled: true, strategies: [profile], allianceDrill: drill});
  const stamina = stateStore({enabled: false, minStamina: 50, preferFifty: true});
  const targetOptions = afk.previewAfkTargets.filter((entry) => entry.group !== 'drill');
  const sourceHooks = hooks([
    null,
    targetOptions,
    true,
    undefined,
    false,
    profile.id,
    undefined,
    rows,
    {},
    [1, 2, 3, 4],
    busy,
    '',
  ]);
  const O = {
    useState: sourceHooks.useState,
    useEffect: sourceHooks.useEffect,
    useMemo: sourceHooks.useMemo,
    useRef: sourceHooks.useRef,
  };
  let configCalls = 0;
  const configHook = () => (configCalls++ === 0 ? monster : stamina);
  const Compact = (props) => h('SourceCompact', props);
  const Original = compile(squad, 'I', {
    A: {jsx: (type, props) => ({type, props}), jsxs: (type, props) => ({type, props}), Fragment},
    O,
    d: () => ({language, t}),
    c: () => ({profileId: 1}),
    e: configHook,
    xe: (value) => value,
    Se: (value) => value,
    Te: afk.previewAfkProfileValid,
    se: afk.validJoinRestrictions,
    oe: afk.normalizeJoinRestrictions,
    m: async () => ({}),
    v: async (value) => value,
    D: async () => {},
    p: async () => ({options: []}),
    f: async () => ({squads: []}),
    u: async () => ({}),
    le: (items) => items,
    C: async () => ({tasks: {}}),
    ue: (items, value) => afk.resolvePreviewAfkTarget(items, value),
    _e: ['normal', 'elite', 'running', 'leader', 'ally', 'drill', 'invader', 'other'],
    be: (items, from, to) => {
      const next = [...items];
      const source = next.indexOf(from);
      const target = next.indexOf(to);
      if (source < 0 || target < 0 || source === target) return items;
      const [moved] = next.splice(source, 1);
      next.splice(target, 0, moved);
      return next;
    },
    Ce: (_target, squads, kind) => afk.makePreviewAfkProfile('source-new', 'Source new', kind, kind === 'join' ? 'boss' : 'steel'),
    we: (value, target) => afk.previewAfkLevelOutOfRange(value, target),
    ne: () => null,
    ie: Compact,
    pe: (props) => h('SourceGarrison', props),
    he: (props) => h('SourceZombie', props),
    w: (props) => h('SourceToggle', props),
    T: (props) => h('SourceIcon', props),
    ye: (props) => h('SourceJoinSettings', props),
    _: (translate, error) => translate(error),
    ge: {
      waiting_join_delay: 'squad.join.waiting',
      waiting_nearby_target: 'squad.afkStep.waitingNearbyTarget',
      scanning_nearby: 'squad.afkStep.scanningNearby',
      scanning_next_target: 'squad.afkStep.scanningNextTarget',
      scanning_mine_route: 'squad.afkStep.scanningMineRoute',
      marching_via_mine: 'squad.afkStep.marchingViaMine',
      redirecting_to_monster: 'squad.afkStep.redirectingToMonster',
      recovering_from_mine: 'squad.afkStep.recoveringFromMine',
      scanning_return_mine: 'squad.afkStep.scanningReturnMine',
      boosting_return: 'squad.afkStep.boostingReturn',
      returning: 'squad.afkStep.returning',
    },
    window: {setInterval: () => 0, clearInterval() {}, setTimeout: (fn) => fn(), confirm: () => true},
    document: {addEventListener() {}, removeEventListener() {}},
  });
  sourceHooks.begin();
  return Original({online: true, serverId: 321, config: monster.draft, staminaPotionConfig: stamina.draft, garrisonConfig: {}, zombieBusConfig: {}, onLog() {}});
}

export function renderCurrentProfile({language = 'en', saving = false} = {}) {
  const t = tFor(catalogs[language]);
  const profile = profileFixture();
  const profiles = [profile];
  const toolbarDraft = fixtures.initialAfkToolbarConfig('squads-profile-positive');
  const profileConfig = {
    draft: profiles,
    confirmed: structuredClone(profiles),
    dirty: false,
    saving,
    error: null,
    store: {
      getSnapshot: () => ({draft: profiles, confirmed: profiles}),
      edit() {}, flush: async () => profiles, refresh: async () => profiles,
    },
  };
  const toolbarConfig = {
    draft: toolbarDraft,
    confirmed: structuredClone(toolbarDraft),
    dirty: false,
    saving: false,
    error: null,
    store: {
      getSnapshot: () => ({draft: toolbarDraft, confirmed: toolbarDraft}),
      edit() {}, flush: async () => toolbarDraft, refresh: async () => toolbarDraft,
    },
  };
  let configCalls = 0;
  const currentHooks = hooks();
  const Current = compile(currentSource, 'AfkContent', {
    h, Fragment,
    ...currentHooks,
    useI18n: () => ({t}),
    usePreviewConfig: () => (configCalls++ === 0 ? profileConfig : toolbarConfig),
    initialAfkProfiles: () => profiles,
    validAfkToolbarConfig: () => true,
    ...afk,
    ...fixtures,
    PreviewConfigError: () => null,
    CompactAfkCard: (props) => h('CurrentCompact', props),
    AfkProfileEditor: () => null,
    AllianceDrillPreviewSettings: (props) => h('CurrentDrill', props),
    GarrisonPreviewSettings: (props) => h('CurrentGarrison', props),
    ZombieBusPreviewSettings: (props) => h('CurrentZombie', props),
    window: {confirm: () => true},
  });
  currentHooks.begin();
  return Current({previewEnabled: true, previewState: 'squads-profile-positive'});
}

export function profileCard(tree) {
  return flatten(tree).find((node) => String(node.props?.className || '').includes('monster-afk-profile-card'));
}

export function profileSummary(tree) {
  const card = profileCard(tree);
  if (!card) return null;
  const buttons = flatten(card).filter((node) => node.type === 'button');
  const inputs = flatten(card).filter((node) => node.type === 'input');
  return {
    tag: card.type,
    className: card.props.className,
    text: text(card),
    statusRows: flatten(card).filter((node) => node.type === 'span' && ['status-ok', 'muted'].includes(node.props?.className)).map((node) => ({className: node.props.className, text: text(node)})),
    buttons: buttons.map((node) => ({className: node.props.className || '', disabled: node.props.disabled === true, text: text(node), draggable: node.props.draggable})),
    inputs: inputs.map((node) => ({type: node.props.type, checked: node.props.checked, disabled: node.props.disabled === true})),
    stable: stable(card),
  };
}

export function writeJson(name, value) {
  fs.writeFileSync(path.join(here, name), JSON.stringify(value, null, 2) + '\n');
}
