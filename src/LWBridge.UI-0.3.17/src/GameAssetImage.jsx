import { createContext, useContext, useEffect, useRef, useState } from "react";

const GameAssetReaderContext = createContext(null);
export function GameAssetImageProvider({ readImage = null, children }) {
  return <GameAssetReaderContext.Provider value={readImage}>{children}</GameAssetReaderContext.Provider>;
}

// Exact GameAssetImage-Diy9VTIr.js b: exactly one nonblank source.
export function normalizeGameAssetSource(assetPath, spriteName) {
  const asset = assetPath?.trim();
  const sprite = spriteName?.trim();
  return !!asset === !!sprite ? null : asset
    ? { sourceKey: `asset:${asset}`, assetPath: asset }
    : { sourceKey: `sprite:${sprite}`, spriteName: sprite };
}

// Original g/_/v/y: two concurrent reads, insertion-ordered 32 Mi character
// cache, 60 s failed-source suppression, coalescing and queued-listener disposal.
export function createGameAssetImageCache(readImage, { schedule = (callback) => window.setTimeout(callback, 0), now = () => Date.now() } = {}) {
  const limit = 32 * 1024 * 1024;
  const images = new Map();
  const failures = new Map();
  const requests = new Map();
  const queue = [];
  let running = 0;
  let scheduled = false;
  let characters = 0;

  function store(sourceKey, value) {
    failures.delete(sourceKey);
    const previous = images.get(sourceKey);
    if (previous) { images.delete(sourceKey); characters -= previous.length; }
    if (value.length > limit) return;
    images.set(sourceKey, value); characters += value.length;
    while (characters > limit) {
      const oldest = images.keys().next().value;
      if (!oldest) break;
      const old = images.get(oldest);
      images.delete(oldest); characters -= old?.length || 0;
    }
  }
  function enqueueDrain() {
    if (scheduled) return;
    scheduled = true;
    schedule(() => { scheduled = false; drain(); });
  }
  function drain() {
    while (running < 2 && queue.length > 0) {
      const request = queue.shift();
      if (!request || request.listeners.size === 0 || requests.get(request.sourceKey) !== request) continue;
      request.state = "running"; running++;
      readImage({ assetPath: request.assetPath, spriteName: request.spriteName })
        .then((result) => {
          store(request.sourceKey, result.dataUrl);
          request.listeners.forEach((listener) => listener.load(result.dataUrl));
        })
        .catch(() => { failures.set(request.sourceKey, now() + 60000); })
        .finally(() => {
          if (requests.get(request.sourceKey) === request) requests.delete(request.sourceKey);
          request.listeners.clear(); running--; enqueueDrain();
        });
    }
  }
  return {
    lookup(sourceKey) { return images.get(sourceKey); },
    subscribe(source, listener) {
      const cached = images.get(source.sourceKey);
      if (cached) { listener.load(cached); return () => {}; }
      const retryAt = failures.get(source.sourceKey) ?? 0;
      if (retryAt > now()) return () => {};
      if (retryAt) failures.delete(source.sourceKey);
      let request = requests.get(source.sourceKey);
      if (!request) {
        request = { ...source, listeners: new Set(), state: "queued" };
        requests.set(source.sourceKey, request); queue.push(request); enqueueDrain();
      }
      request.listeners.add(listener);
      return () => {
        request.listeners.delete(listener);
        if (request.state === "queued" && request.listeners.size === 0) requests.delete(source.sourceKey);
      };
    },
  };
}

// Source has one immutable reader. Per-reader cache identity prevents an inert
// preview reader from supplying its data to a different/native reader.
const READER_CACHES = new WeakMap();
function cacheFor(readImage) {
  if (!readImage) return null;
  if (!READER_CACHES.has(readImage)) READER_CACHES.set(readImage, createGameAssetImageCache(readImage));
  return READER_CACHES.get(readImage);
}

// Original x byte 1307. Caller supplies a verified existing/local reader; absent
// readers retain placeholders and never dispatch native or service work.
export function GameAssetImage({ assetPath, spriteName, alt, className, deferUntilVisible = false, readImage: explicitReader }) {
  const inheritedReader = useContext(GameAssetReaderContext);
  const readImage = explicitReader === undefined ? inheritedReader : explicitReader;
  const source = normalizeGameAssetSource(assetPath, spriteName);
  const sourceKey = source?.sourceKey || "";
  const cache = cacheFor(readImage);
  const ref = useRef(null);
  const [loaded, setLoaded] = useState({ sourceKey, cache, src: "" });
  const [visibility, setVisibility] = useState({ sourceKey, ready: !deferUntilVisible });
  const cached = sourceKey && cache?.lookup(sourceKey) || "";
  const src = loaded.sourceKey === sourceKey && loaded.cache === cache ? loaded.src : cached;
  const ready = visibility.sourceKey === sourceKey && visibility.ready;

  useEffect(() => {
    const value = sourceKey && cache?.lookup(sourceKey) || "";
    setLoaded({ sourceKey, cache, src: value });
    setVisibility({ sourceKey, ready: !deferUntilVisible || !!value });
  }, [sourceKey, deferUntilVisible, cache]);
  useEffect(() => {
    if (!sourceKey || !deferUntilVisible || src || ready) return undefined;
    const element = ref.current;
    if (!element || typeof IntersectionObserver === "undefined") { setVisibility({ sourceKey, ready: true }); return undefined; }
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) { setVisibility({ sourceKey, ready: true }); observer.disconnect(); }
    }, { rootMargin: "160px 0px" });
    observer.observe(element);
    return () => observer.disconnect();
  }, [sourceKey, deferUntilVisible, ready, src]);
  useEffect(() => {
    if (!sourceKey || !ready || src || !cache) return undefined;
    return cache.subscribe(source, { load: (value) => setLoaded({ sourceKey, cache, src: value }) });
  }, [sourceKey, source?.assetPath, source?.spriteName, ready, src, cache]);
  return src ? <img className={className} src={src} alt={alt} loading="lazy" decoding="async" />
    : <span ref={ref} className={`${className} game-asset-placeholder`} aria-label={alt} />;
}
