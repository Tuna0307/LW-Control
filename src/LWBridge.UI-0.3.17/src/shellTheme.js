// index-BVfnK1wp.js Et, UTF-8 byte 364550. Native window preference
// persistence is a separate provider; this reproduces the browser lifecycle.
export function toggleShellTheme(theme, commit, document, window, storage, flushSync) {
  const next = theme === "light" ? "dark" : "light";
  const root = document.documentElement;
  const update = () => {
    root.dataset.theme = next;
    try { storage.setItem("lwbridge.theme", next); } catch {}
    flushSync(() => commit(next));
  };
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (!reduced && document.startViewTransition) {
    const transition = document.startViewTransition(update);
    transition?.finished?.catch(() => {});
    return;
  }
  if (!reduced) root.classList.add("theme-transitioning");
  update();
  if (!reduced) window.setTimeout(() => root.classList.remove("theme-transitioning"), 240);
}
