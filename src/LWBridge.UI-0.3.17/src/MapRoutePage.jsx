import { MapDataPage } from "./MapDataPage.jsx";
import { getMapPreviewProvider } from "./mapPreviewApi.js";

export function MapRoutePage(pageProps) {
  const previewProvider = getMapPreviewProvider(pageProps.bridgeMode, pageProps.previewState);
  return <MapDataPage {...pageProps} {...(previewProvider || {})} />;
}
