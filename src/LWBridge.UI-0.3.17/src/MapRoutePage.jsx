import { MapDataPage } from "./MapDataPage.jsx";
import { getMapPreviewProvider } from "./mapPreviewApi.js";
import { useState } from "react";

export function MapRoutePage(pageProps) {
  const previewProvider = getMapPreviewProvider(pageProps.bridgeMode, pageProps.previewState);
  // Preserve the selected kind across owner retirement when the page is
  // mounted on its own (App already owns this tab externally).
  const [localTab, setLocalTab] = useState(undefined);
  // App retains its module-scoped mapApi when the selected native profile
  // changes. Key the actual route by App's profile ID + generation, not by
  // provider object identity or connected server (stopped owners have server=0).
  // Remounting also retires page-local rows, filters, response generations and
  // cached options *before* any replacement owner's saved-server read settles.
  const owner = pageProps.mapOwner || pageProps.gameTextOwner || pageProps.mapApi?.profileId || "";
  return <MapDataPage key={owner} {...pageProps} {...(previewProvider || {})}
    activeTab={pageProps.activeTab ?? localTab}
    onActiveTabChange={pageProps.onActiveTabChange ?? setLocalTab} />;
}
