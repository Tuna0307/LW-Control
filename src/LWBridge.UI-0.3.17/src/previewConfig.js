// Local UI draft engine recovered from index-BVfnK1wp.js, function T (config store).
// It receives an explicit adapter; browser preview supplies local memory only.
const w = (value) => JSON.stringify(value);
export function createConfigDraft(e2, t2) {
  let n2 = { draft: e2, confirmed: e2, dirty: false, saving: false, error: null }, r2 = 0, i2, a2, o2, s2, c2 = false, l2 = false, u2 = false, d2 = false, f2 = w(e2), p2 = /* @__PURE__ */ new Set();
  function m2(e3) {
    n2 = { ...n2, ...e3 }, n2.dirty = w(n2.draft) !== w(n2.confirmed);
    for (let e4 of p2) e4();
  }
  function h2() {
    i2 !== void 0 && clearTimeout(i2), i2 = void 0;
  }
  function g2() {
    return t2.valid?.(n2.draft) !== false;
  }
  function _2(e3, t3 = 400) {
    let a3 = typeof e3 == `function` ? e3(n2.draft) : e3;
    r2++, d2 = false, h2(), m2({ draft: a3, error: null }), !u2 && t3 !== false && g2() && n2.dirty && !c2 && (i2 = setTimeout(() => {
      i2 = void 0, v2().catch(() => void 0);
    }, t3));
  }
  function v2(e3 = true) {
    if (h2(), u2) return Promise.reject(Error(`CONFIG_SAVE_DISPOSED`));
    if (a2) return !e3 && g2() && (o2 = { value: structuredClone(n2.draft), generation: r2 }), a2;
    if (c2) return Promise.reject(Error(`CONFIG_ACTION_PENDING`));
    if (!n2.dirty) return Promise.resolve(n2.confirmed);
    if (d2 || !g2()) {
      let e4 = Error(`CONFIG_DRAFT_INVALID`);
      return m2({ error: e4 }), Promise.reject(e4);
    }
    return m2({ saving: true, error: null }), a2 = (async () => {
      try {
        for (; (n2.dirty || o2) && !u2 && !c2 && !d2 && g2(); ) {
          let i3 = o2?.value ?? structuredClone(n2.draft), a3 = o2?.generation ?? r2;
          o2 = void 0;
          let s3 = await t2.write(i3);
          if (u2 || (m2({ confirmed: s3, ...r2 === a3 ? { draft: s3 } : {} }), !e3 && !o2)) break;
        }
        return n2.confirmed;
      } catch (e4) {
        throw u2 || m2({ error: e4 }), h2(), o2 = void 0, e4;
      } finally {
        a2 = void 0, u2 || m2({ saving: false });
      }
    })(), a2;
  }
  async function y2(e3 = false) {
    if (u2 || n2.saving || c2 || !e3 && n2.dirty) return;
    if (s2 && !e3) return s2;
    let i3 = r2, a3 = (async () => {
      let e4 = await t2.read();
      u2 || i3 !== r2 || n2.saving || c2 || (r2++, d2 = false, m2({ draft: e4, confirmed: e4, error: null }));
    })();
    s2 = a3;
    try {
      await a3;
    } finally {
      s2 === a3 && (s2 = void 0);
    }
  }
  function b2(e3) {
    let t3 = w(e3);
    t3 !== f2 && (f2 = t3, !n2.dirty && !n2.saving && !c2 && t3 !== w(n2.confirmed) && y2().catch(() => void 0));
  }
  async function x2(e3, s3, f3) {
    if (u2) throw Error(`CONFIG_SAVE_DISPOSED`);
    if (l2) throw Error(`CONFIG_ACTION_PENDING`);
    l2 = true;
    try {
      if (s3) c2 = true, o2 = void 0, _2(s3, false), await a2?.catch(() => void 0);
      else {
        if (await v2(), n2.dirty) throw Error(`CONFIG_DRAFT_INVALID`);
        c2 = true, f3 && _2(f3, false);
      }
      if (h2(), u2) throw Error(`CONFIG_SAVE_DISPOSED`);
      m2({ saving: true });
      let i3 = r2, l3 = await e3();
      if (u2) return l3;
      let d3 = await t2.read();
      if (u2) return l3;
      let p3 = s3 ? s3(n2.draft) : n2.draft, g3 = r2 !== i3;
      return r2++, m2({ confirmed: d3, draft: s3 || g3 ? p3 : d3, error: null }), l3;
    } catch (e4) {
      throw u2 || m2({ error: e4 }), e4;
    } finally {
      l2 = false, c2 = false, u2 || m2({ saving: false }), !u2 && n2.dirty && !n2.error && g2() && !d2 && (i2 = setTimeout(() => {
        i2 = void 0, v2().catch(() => void 0);
      }, 400));
    }
  }
  return { getSnapshot: () => n2, subscribe(e3) {
    return p2.add(e3), () => {
      p2.delete(e3);
    };
  }, edit: _2, flush: v2, refresh: y2, receive: b2, runAction: x2, pause() {
    h2(), d2 = true;
  }, setAdapter(e3) {
    t2 = e3;
  }, dispose() {
    u2 = true, h2(), p2.clear();
  } };
}
