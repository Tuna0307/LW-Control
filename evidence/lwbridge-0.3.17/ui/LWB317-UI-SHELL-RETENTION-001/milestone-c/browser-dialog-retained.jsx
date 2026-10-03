import { Activity, Fragment } from "react";
import { PageForRoute } from "/src/Pages.jsx";
import { routes } from "/src/routes.js";

function RetainedPages({ activeRoute, visitedRoutes, selectedProfileId, pageProps }) {
  return (
    <Fragment key={selectedProfileId}>
      {routes.map((route) => visitedRoutes.has(route.key) ? (
        <Activity key={route.key} mode={route.key === activeRoute ? "visible" : "hidden"}>
          <PageForRoute routeKey={route.key} {...pageProps} />
        </Activity>
      ) : null)}
    </Fragment>
  );
}

export { RetainedPages as BrowserDialogRetainedPages };
