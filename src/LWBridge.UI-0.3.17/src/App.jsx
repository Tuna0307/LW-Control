import { useState } from "react";
import { initialRouteKey, routes } from "./routes.js";

export function App() {
  const [activeRoute, setActiveRoute] = useState(initialRouteKey);
  const current = routes.find((route) => route.key === activeRoute) ?? routes[0];

  return (
    <div className="ui-scaffold" data-reference-version="0.3.17">
      <header className="ui-scaffold-header">
        <strong>LWBridge</strong>
        <span>0.3.17 static UI preview</span>
      </header>

      <div className="ui-scaffold-layout">
        <nav aria-label="LWBridge sections">
          {routes.map((route, index) => (
            <button
              key={route.key}
              type="button"
              aria-current={route.key === activeRoute ? "page" : undefined}
              onClick={() => setActiveRoute(route.key)}
            >
              <span>{String(index + 1).padStart(2, "0")}</span>
              {route.label}
            </button>
          ))}
        </nav>

        <main>
          <section>
            <h1>{current.label}</h1>
            <p>Static page reproduction is added in the following campaign stages.</p>
          </section>
        </main>
      </div>
    </div>
  );
}
