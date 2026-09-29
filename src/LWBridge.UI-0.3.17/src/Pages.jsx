import { useState } from "react";

function Switch({ checked = false, disabled = false, label, onChange }) {
  return (
    <span
      className={`ui-switch${checked ? " is-on" : ""}`}
      aria-hidden="true"
    />
  );
}

function ToggleRow({ label, checked = false, disabled = false, onChange }) {
  return (
    <button
      type="button"
      className="toggle-row"
      role="switch"
      aria-checked={checked}
      aria-label={`${label}: ${checked ? "Enabled" : "Disabled"}`}
      disabled={disabled}
      onClick={() => onChange?.(!checked)}
    >
      <span>{label}</span>
      <Switch checked={checked} />
    </button>
  );
}

function PanelTitle({ title, subtitle }) {
  return (
    <div className="panel-title">
      <h2>{title}</h2>
      {subtitle ? <span className="muted">{subtitle}</span> : null}
    </div>
  );
}

export function HomePage() {
  return (
    <section className="panel quick-actions-panel">
      <div className="panel-title">
        <h2>Game Setup</h2>
        <span className="muted">Checking game setup…</span>
      </div>
      <ToggleRow label="Open games at startup" disabled />
      <ToggleRow label="Automatic Reconnection" disabled />
    </section>
  );
}

const automationCategories = [
  ["daily", "Daily Tasks"],
  ["alliance", "Alliance"],
  ["resourceGather", "Resource gathering"],
  ["resources", "Resource Claims"],
  ["chat", "Chat"],
  ["trade", "Trade Station"],
  ["system", "Protection & System"],
];

const automationCards = {
  daily: [
    ["Auto Training", "Train and promote in large batches, then finish the remaining troops."],
    ["Automatic Construction", "Upgrade the lowest-level building when the queue is idle."],
    ["Free Stamina", "Claim daily free Stamina when available."],
    ["Automatic Treatment", "Treat wounded soldiers, request Alliance help, and collect completed treatment every 2 seconds without using Gold."],
    ["Trucks", "Claim arrived Truck rewards, refresh to the target quality, and continue dispatching available Trucks."],
    ["Secret Task", "Claim completed rewards, then continue refreshing and dispatching eligible Secret Tasks."],
    ["Automatically assist alliance Secret Tasks", "Automatically assist alliance Secret Tasks"],
    ["Ghost Ops", "Start and share your Ghost Ops, join eligible alliance missions, and claim rewards on time."],
  ],
  alliance: [
    ["Alliance Tech Donations", "Automatically donate standard resources to Alliance Tech."],
    ["Automatic Official Application", "Automatically apply for one selected official position."],
    ["Automatic Alliance Train Boarding", "Select and rank target rewards, or let reward quantity override the ranking."],
    ["Alliance Help", "Help Alliance members automatically, with a check every minute."],
    ["Alliance Gifts", "Claim normal and advanced Alliance Gifts at the configured interval."],
    ["Excavation Stronghold Resources", "Claim resources from occupied excavation strongholds at the configured interval."],
    ["Alliance Center Resources", "Claim production resources from Alliance Center buildings at the configured interval."],
    ["Alliance Gathering Dispatch", "Automatically send selected idle squads to Alliance Gathering Points."],
  ],
  resources: [
    ["Building Resource Collection", "Collect resources produced by city buildings on a schedule."],
    ["Armed Truck", "Claim Armed Truck idle rewards on a schedule."],
  ],
  chat: [
    ["Red Packet", "Listen for chat messages and automatically claim Red Packet."],
    ["Fireworks / Egg", "Listen for chat messages and automatically claim Fireworks / Egg."],
    ["Treasure", "Listen for chat messages and automatically claim Treasure."],
  ],
  system: [
    ["Weekend Shield", "Maintain a Shield throughout Saturday server time."],
    ["Attack Shield", "Try 8, 12, and 24-hour Shields in order when attacked."],
  ],
};

function AutomationCard({ title, description }) {
  return (
    <article className="automation-card" data-preview-fixture="runtime-config-unobserved">
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>{title}</h3>
          {description ? <p>{description}</p> : null}
        </div>
        <div className="automation-card-header-actions">
          <button className="automation-header-switch" type="button" disabled aria-label={`${title}: Disabled`}><Switch checked={false} /></button>
        </div>
      </div>
      <div className="automation-card-meta-row">
        <span className="automation-state">Disabled</span>
      </div>
    </article>
  );
}

function ResourceGatherCard() {
  return (
    <article className="automation-card automation-card-wide">
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>Resource gathering</h3>
          <p>
            Continuously gather ordinary resources. AFK and activity tasks take priority. Pause
            under a shield and resume after the configured manual-action delay.
          </p>
        </div>
        <button className="automation-header-switch" type="button" disabled aria-label="Resource gathering: Disabled"><Switch checked={false} /></button>
      </div>
      <div className="automation-config-body">
        <p className="muted">Enable at least one gathering squad</p>
        <div className="automation-form-grid">
          <label>
            <span>Resource</span>
            <select disabled><option>—</option></select>
          </label>
          <label>
            <span>Search level</span>
            <input disabled value="" readOnly />
          </label>
          <label>
            <span>Wait after manual actions (minutes)</span>
            <input disabled value="" readOnly />
          </label>
          <label>
            <span>Scan radius (tiles)</span>
            <input disabled value="" readOnly />
          </label>
        </div>
        <ToggleRow label="Recall squads when gathering is disabled" disabled />
        <span className="muted">Loading squad settings</span>
      </div>
    </article>
  );
}

function TradeStationCard() {
  return (
    <article className="automation-card trade-station-panel">
      <div className="automation-card-header">
        <div className="automation-card-title-group">
          <h3>Trade Station Auto Purchase</h3>
          <p>Listen for trade station broadcasts and buy every selected available good serially.</p>
        </div>
        <button className="automation-header-switch" type="button" disabled aria-label="Trade Station Auto Purchase: Disabled"><Switch checked={false} /></button>
      </div>
      <p className="muted">
        Prices are not compared; selected goods are purchased only with the checked currencies.
      </p>
      <ToggleRow label="Scan cross-server trade stations" disabled />
      <div className="automation-form-grid">
        <label><span>Purchase currencies</span><select disabled><option>—</option></select></label>
        <label><span>Goods to buy</span><select disabled><option>Select goods</option></select></label>
      </div>
      <div className="automation-card-summary">
        <span>Detected <strong>—</strong></span>
        <span>Attempted <strong>—</strong></span>
        <span>Succeeded <strong>—</strong></span>
        <span>Last result <strong>—</strong></span>
      </div>
      <span className="muted">Loading goods...</span>
    </article>
  );
}

export function AutomationPage() {
  const [category, setCategory] = useState("daily");
  return (
    <section className="panel">
      <PanelTitle title="Automation" subtitle="Game disconnected. Actions are disabled." />
      <div className="automation-categories" role="tablist" aria-label="Automation categories">
        {automationCategories.map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            className={category === key ? "active" : ""}
            aria-selected={category === key}
            onClick={() => setCategory(key)}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="automation-grid">
        {category === "resourceGather" ? <ResourceGatherCard /> : null}
        {category === "trade" ? <TradeStationCard /> : null}
        {(automationCards[category] ?? []).map(([title, description]) => (
          <AutomationCard key={title} title={title} description={description} />
        ))}
      </div>
    </section>
  );
}

const scanTypes = [
  "Player City",
  "Resource Point",
  "Monster",
  "Truck",
  "Train",
  "Secret Task",
  "Ghost Ops",
  "Treasure",
];

const mapTabs = [
  "City",
  "Resource",
  "Monster",
  "Truck",
  "Train",
  "Secret Task",
  "Ghost Ops",
  "Treasure",
  "Scheduled Plunder",
];

export function MapDataPage() {
  const [scanMode, setScanMode] = useState("Manual Scan");
  const [tab, setTab] = useState("City");
  return (
    <section className="panel map-panel">
      <div className="map-scan-tabs" role="tablist" aria-label="Map scan mode">
        {["Manual Scan", "Auto Scan"].map((label) => (
          <button
            key={label}
            type="button"
            role="tab"
            className={scanMode === label ? "active" : ""}
            aria-selected={scanMode === label}
            onClick={() => setScanMode(label)}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="map-header">
        <h2>World Map Data</h2>
        <div className="map-actions">
          {scanMode === "Manual Scan" ? (
            <>
              <fieldset className="map-speed-toggle" disabled>
                <span className="map-speed-slider" />
                <label><input type="radio" checked readOnly /><span>Normal</span></label>
                <label><input type="radio" readOnly /><span>Fast</span></label>
              </fieldset>
              <button type="button" className="primary" disabled>Start Scan</button>
              <button type="button" disabled>Stop</button>
              <button type="button" disabled>Clear Map Data</button>
            </>
          ) : null}
          <button type="button" disabled>Export Excel</button>
        </div>
      </div>

      {scanMode === "Manual Scan" ? (
        <div className="map-auto-scan-card">
          <div className="map-controls">
            <span className="map-controls-label">Scan contents</span>
            <div className="map-types map-types--compact">
              {scanTypes.map((label) => (
                <label key={label} className="disabled">
                  <input type="checkbox" disabled />
                  <span>{label}</span>
                </label>
              ))}
            </div>
          </div>
          <div className="map-auto-scan-grid">
            <label><span>Server</span><input disabled value="" readOnly /></label>
          </div>
          <div className="map-scan-summary">
            <span className="map-status-pill">Stopped</span>
            <span>Server <strong>—</strong></span>
            <strong>Scan progress</strong>
            <div className="map-progress low"><progress className="map-progress-bar" max="100" value="0" /><span>—</span></div>
          </div>
        </div>
      ) : (
        <div className="map-auto-scan-card">
          <div className="map-auto-scan-master">
            <Switch checked={false} />
            <strong>Enable automatic scanning</strong>
            <span>Disabled</span>
          </div>
          <div className="map-auto-scan-grid">
            <div className="map-auto-scan-server-field">
              <span>Target servers</span>
              <div className="map-auto-scan-server-input"><input disabled /><button type="button" disabled>Add</button></div>
              <small>Enter server IDs and click Add. Commas add several at once; × removes one. No entries scans the current server.</small>
            </div>
            <label><span>Interval (minutes)</span><input disabled /></label>
          </div>
          <div className="map-auto-scan-options">
            <label><input type="checkbox" disabled />Return to the original server after scanning</label>
            <button type="button" disabled>Run now</button>
          </div>
          <small>Each target server is entered before scanning; a server ID alone cannot scan another server.</small>
        </div>
      )}

      <div className="map-tabs" role="tablist" aria-label="Map data types">
        {mapTabs.map((label, index) => (
          <button
            key={label}
            type="button"
            role="tab"
            className={tab === label ? "active" : ""}
            aria-selected={tab === label}
            onClick={() => setTab(label)}
          >
            <span className="map-tab-label">{label}</span>
            {index < 8 ? <span className="map-tab-count">—</span> : null}
          </button>
        ))}
      </div>

      <div className="map-searchbar">
        <input aria-label="Search map data" placeholder="Search name, Alliance, or UUID" />
        {tab === "City" ? (
          <>
            <select aria-label="Filter by Alliance"><option>All Alliances</option><option>No Alliance</option></select>
            <label className="map-filter-field"><input type="checkbox" /> <span>Marked only</span></label>
          </>
        ) : null}
        <span className="map-result-count">—</span>
      </div>

      <div className="map-table-scroll">
        <table className="map-table">
          <tbody>
            <tr className="map-row"><td className="map-empty muted">No saved data for this type.</td></tr>
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function SquadsPage() {
  const [tab, setTab] = useState("afk");
  return (
    <section className="panel squad-panel">
      <div className="squad-header">
        <PanelTitle title="Squads / AFK" />
        {tab === "equipment" ? <button type="button" disabled>Refresh</button> : null}
      </div>
      <div className="squad-tabs" role="tablist" aria-label="Squads tabs">
        <button type="button" role="tab" className={tab === "afk" ? "active" : ""} aria-selected={tab === "afk"} onClick={() => setTab("afk")}>AFK Tasks</button>
        <button type="button" role="tab" className={tab === "equipment" ? "active" : ""} aria-selected={tab === "equipment"} onClick={() => setTab("equipment")}>Equipment Schemes</button>
      </div>
      {tab === "afk" ? <AfkContent /> : <EquipmentContent />}
    </section>
  );
}

function AfkContent() {
  return (
    <div className="afk-layout">
      <section className="afk-profile-panel">
        <h3>Shared AFK Profiles</h3>
        <ToggleRow label="Monster AFK master switch" disabled />
        <p className="muted">Enable or pause all monster AFK profiles below. Alliance Drill and Auto Garrison are controlled separately.</p>
        <button type="button" disabled>New AFK Profile</button>
        <div className="afk-empty-state muted">No AFK profiles</div>
      </section>
      <section className="afk-editor-panel">
        <div className="clone-card-stack">
          <article className="automation-card">
            <div className="automation-card-header"><div className="automation-card-title-group"><h3>Auto Alliance Drill</h3><p>Participate in squad order; the first available squad launches rallies when enabled.</p></div><button className="automation-header-switch" type="button" disabled aria-label="Auto Alliance Drill: Disabled"><Switch /></button></div>
          </article>
          <article className="automation-card">
            <div className="automation-card-header"><div className="automation-card-title-group"><h3>Auto Garrison</h3><p>Keep selected alliance buildings and allies reinforced.</p></div><button className="automation-header-switch" type="button" disabled aria-label="Auto Garrison: Disabled"><Switch /></button></div>
          </article>
          <article className="automation-card">
            <div className="automation-card-header"><div className="automation-card-title-group"><h3>Zombie Bus Garrison</h3><p>Keep the strongest squad home; prioritize gold buses. Recall after confirmed battle completion, or after 90 seconds.</p></div><button className="automation-header-switch" type="button" disabled aria-label="Zombie Bus Garrison: Disabled"><Switch /></button></div>
          </article>
          <article className="automation-card">
            <div className="automation-card-header"><div className="automation-card-title-group"><h3>Stamina</h3><p>Uses stamina items whenever current stamina falls below the limit.</p></div></div>
            <ToggleRow label="Use Stamina items automatically" disabled />
            <div className="automation-form-grid">
              <label><span>Use below Stamina</span><input disabled /></label>
              <label><span>Prefer 50-point item</span><input type="checkbox" disabled /></label>
            </div>
          </article>
        </div>
      </section>
    </div>
  );
}

function EquipmentContent() {
  return (
    <div className="equipment-layout clone-equipment-layout">
      <aside className="equipment-preset-list">
        <strong>Equipment presets</strong>
        <p className="muted">No equipment presets</p>
      </aside>
      <section className="equipment-main">
        <h3>Create an equipment preset first.</h3>
        <div className="map-actions clone-left-actions">
          <button type="button" disabled>Save configuration</button>
          <button type="button" disabled>Save and apply to squads</button>
          <button type="button" disabled>Read current equipment</button>
        </div>
        <p className="muted">Alt+1–4 applies the matching scheme across all squads.</p>
      </section>
    </div>
  );
}

export function CityLayoutPage() {
  return (
    <div className="panel city-layout-empty">
      The game is disconnected, so the city layout cannot be loaded.
    </div>
  );
}

const hotkeyCards = [
  ["Q / W / E / R", "Attack target", "Q/W/E/R each send one march at whatever is under the cursor: enemy city, ally city reinforcement, monster, resource node, or city ruin. World bosses and rally monsters are skipped. A/S/D/F recalls."],
  ["A / S / D / F", "Recall squad", "Recall squads 1–4 to the Headquarters."],
  ["Space", "Shield countdown", "Hold Space to display remaining Shield time above protected cities."],
  ["F6 / F7 / F8", "Use Shield", "F6 uses an 8-hour Shield, F7 a 12-hour Shield, and F8 a 24-hour Shield.", "This shortcut consumes the matching Shield item immediately."],
  ["Alt + 1～4", "Equipment preset", "Apply equipment presets 1–4 to all configured squads."],
  ["F9", "Random relocation", "Press F9 to use a Random Relocator.", "Relocation may consume an item. This shortcut is disabled by default."],
  ["F10", "Alliance relocation", "Press F10 to relocate to the Alliance rally point.", "Relocation may consume an item. This shortcut is disabled by default."],
];

function HotkeyCard({ binding, title, description, warning, attack = false }) {
  return (
    <article className={`hotkey-card${warning ? " hotkey-card-danger" : ""}`} data-preview-fixture="runtime-config-unobserved">
      <div className="hotkey-card-header">
        <kbd className="hotkey-binding">{binding}</kbd>
        <button className="hotkey-switch" type="button" disabled aria-label={`${title}: Disabled`}><Switch checked={false} /></button>
      </div>
      <h3>{title}</h3>
      <p>{description}</p>
      {attack ? (
        <div className="hotkey-attack-settings">
          <label className="hotkey-attack-toggle"><input type="checkbox" disabled /><span>Use speedup items from the inventory</span></label>
          <label className="hotkey-attack-toggle"><input type="checkbox" disabled /><span>Buy speedups with diamonds when items run out</span></label>
          <small>Items are spent first, cheapest tier first. Diamond purchases pick the lowest-priced tier and spend real diamonds.</small>
        </div>
      ) : null}
      <span className="hotkey-state">Disabled</span>
      {warning ? <div className="hotkey-danger">{warning}</div> : null}
    </article>
  );
}

export function HotkeysPage() {
  return (
    <section className="panel hotkey-panel">
      <PanelTitle title="Game Hotkeys" subtitle="Fixed shortcuts that respond while the Last War game window is active." />
      <div className="hotkey-status">Game disconnected. Settings are saved and will apply after connection.</div>
      <div className="hotkey-grid">
        {hotkeyCards.map(([binding, title, description, warning], index) => (
          <HotkeyCard key={title} binding={binding} title={title} description={description} warning={warning} attack={index === 0} />
        ))}
      </div>
    </section>
  );
}

export function MiniGamesPage() {
  return (
    <section className="panel hotkey-panel">
      <PanelTitle title="Mini Games" subtitle="Optional helpers for in-game mini games." />
      <div className="hotkey-status">Game disconnected. Settings are saved and will apply after connection.</div>
      <div className="hotkey-grid">
        <HotkeyCard binding="G" title="Frontline reinforcement" description="Press G after a Frontline Breakthrough battle starts to add 5 soldiers each time." />
        <article className="hotkey-card">
          <h3>Black Market Chests</h3>
          <p>Mark the grand prize chest in the game after the shuffle ends.</p>
          <ToggleRow label="Mark grand prize chest" disabled />
        </article>
        <article className="hotkey-card">
          <h3>Unlock Land Cell</h3>
          <p>Unlock the next unopened city land cell. Each click processes one cell.</p>
          <div className="mini-game-actions"><button className="primary" type="button" disabled>Unlock One Cell</button></div>
        </article>
        <article className="hotkey-card">
          <h3>Food House Auto Clear</h3>
          <p>Open the in-game board and visibly clear the current S4 Food House level without items or entering the next level.</p>
          <span className="hotkey-state">Stopped</span>
          <div className="mini-game-actions"><button className="primary" type="button" disabled>Start</button></div>
        </article>
      </div>
    </section>
  );
}

export function SettingsPage() {
  return (
    <section className="panel settings-panel">
      <PanelTitle title="Settings" subtitle="In-game display, application information, and client updates." />
      <section className="update-panel">
        <div className="update-heading">
          <div>
            <strong>In-game performance</strong>
            <span>Show real-time performance and network latency in the top-left corner of the game.</span>
          </div>
        </div>
        <p>Processing</p>
      </section>
      <section className="update-panel feedback-panel">
        <div className="update-heading"><div><strong>Issue feedback</strong><span>Export recent local diagnostic logs to help investigate issues.</span></div></div>
        <p className="feedback-privacy">The archive automatically masks player names, UIDs, authorization data, and local paths. It is saved only to the location you choose and is never uploaded automatically.</p>
        <div className="update-actions"><button type="button" disabled>Export diagnostic archive</button></div>
      </section>
      <section className="update-panel">
        <div className="update-heading">
          <div><strong>Client Update</strong><span>Updates are checked automatically with the authorization heartbeat.</span></div>
          <span className="update-version">Current version: 0.3.17</span>
        </div>
        <div className="update-actions"><button type="button" disabled>Check for updates</button></div>
      </section>
    </section>
  );
}

export function PageForRoute({ routeKey }) {
  switch (routeKey) {
    case "overview": return <HomePage />;
    case "automation": return <AutomationPage />;
    case "map-data": return <MapDataPage />;
    case "march": return <SquadsPage />;
    case "city-layout": return <CityLayoutPage />;
    case "hotkeys": return <HotkeysPage />;
    case "mini-games": return <MiniGamesPage />;
    case "settings": return <SettingsPage />;
    default: return <HomePage />;
  }
}
