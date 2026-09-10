"""Reproduce the recovered frontend with a login-free local host adapter.

The evidence directory is immutable. Every transformation is anchored to the
verified 0.3.1 bundle and fails if that bundle changes.
"""
import hashlib
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'evidence/lwbridge-0.3.1/frontend'
OUTPUT = ROOT / 'src/LWBridge.Desktop/WebUi'


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError(f'Expected exactly one anchor: {old[:90]}')
    return text.replace(old, new, 1)


def replace_between(text, start, end, replacement):
    if text.count(start) != 1:
        raise ValueError(f'Ambiguous function: {start}')
    a = text.index(start)
    b = text.index(end, a)
    return text[:a] + replacement + text[b:]


def build(check=False):
    def emit(path, data):
        if check:
            if not path.exists() or path.read_bytes() != data:
                raise ValueError(f'Generated frontend is stale: {path}. Run this tool without --check.')
        else:
            path.write_bytes(data)

    for line in (SOURCE / 'manifest.sha256.txt').read_text().splitlines():
        digest, name = line.split(maxsplit=1)
        path = SOURCE / name.strip().lstrip('*')
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest.lower():
            raise ValueError(f'Recovered asset hash mismatch: {name}')
    if not check:
        (OUTPUT / 'assets').mkdir(parents=True, exist_ok=True)
    for path in (SOURCE / 'assets').iterdir():
        data = path.read_bytes()
        if path.name == 'api-ClPPi2JT.js':
            s = data.decode('utf-8')
            s = replace_between(s, 'function U(e,t){', 'function W(e,t){',
                'function U(e,t){let n=T(),r=t&&typeof t==`object`&&!Array.isArray(t)?{...t}:t==null?{}:{value:t};return n&&!(`profileId`in r)&&(r.profileId=n),window.LWBridgePreview.invoke(e,r)}')
            s = replace_between(s, 'function W(e,t){', 'function G(e){',
                'function W(e,t){return window.LWBridgePreview.listen(e,e=>{let n=e;if(n&&typeof n==`object`&&`profileId`in n&&`payload`in n){if(n.profileId!==T())return;t(n.payload);return}t(n)})}')
            data = s.encode('utf-8')
        elif path.name == 'index-sfL2sT3K.js':
            s = data.decode('utf-8')
            # Remove login/register/unbind UI and its provider, not just CSS-hide it.
            s = replace_between(s, 'function pt({auth:e}){', 'function mt(e,t)', '')
            provider = (OUTPUT / 'local-providers.js').read_text(encoding='utf-8')
            s = replace_between(s, 'function Xt({children:e}){', 'function Zt(){', provider)
            s = replace_between(s, 'function sn({children:e}){', 'function cn(){', '')
            s = replace_between(s, 'function fn({open:e,onClose:t}){', 'function pn(e,t)',
                                'function fn(){return null}')
            account = ',(0,M.jsxs)(`button`,{className:`auth-account-button`,type:`button`,onClick:f,children:[(0,M.jsx)(`strong`,{children:y(`auth.account`)}),(0,M.jsx)(`span`,{children:g.state.expiresAt?new Date(g.state.expiresAt).toLocaleDateString():`-`})]})'
            s = replace_once(s, account, '')
            # Advanced is compiled into 0.3.1 but hard-hidden by An(!1,...).
            # Preserve the original eight-item navigation. Explicit --view
            # advanced exposes the recovered hidden page for future research.
            s = replace_once(s, 'Tt=An(!1,o.state.accessRole)', 'Tt=window.LWBridgePreview.view===`advanced`')
            s = replace_once(s, '[p,m]=(0,j.useState)(`overview`)',
                            '[p,m]=(0,j.useState)(window.LWBridgePreview.view)')
            s = replace_once(s, 'new Set([`overview`])',
                            'new Set([window.LWBridgePreview.view])')
            data = s.encode('utf-8')
        elif path.name == 'MapDataPanel-C1HVeNHr.js':
            s = data.decode('utf-8')
            # IMPLEMENTATION POLICY: bounded current-view rows use the source-backed
            # occupancy boolean only after the importer proves both recovered gather
            # fields were readable. Preserve the recovered formatter for original
            # rows and show an unknown marker for incomplete bounded captures.
            original_status = 'value:e=>{let n=Ge(e);return j(r,n?`300039`:`372138`,t(n?`map.resourceGathering`:`map.resourceIdle`))}'
            bounded_status = 'value:e=>{let a=k(e,`rebuildGatherOccupancyKnown`);if(a===!1)return`—`;let n=a===!0?k(e,`rebuildGatherOccupied`)===!0:Ge(e);return j(r,n?`300039`:`372138`,t(n?`map.resourceGathering`:`map.resourceIdle`))}'
            s = replace_once(s, original_status, bounded_status)
            data = s.encode('utf-8')
        emit(OUTPUT / 'assets' / path.name, data)
    html = (SOURCE / 'index.html').read_text(encoding='utf-8')
    html = replace_once(html, '<script type="module"', '<script src="./preview-host.js"></script>\n    <script type="module"')
    # Browser network access stays local-only. Production desktop IPC is the
    # explicit WebView2 message adapter, while capture/browser mode remains a
    # deterministic fixture with no live fallback.
    html = html.replace("connect-src 'self' ws://127.0.0.1:1420", "connect-src 'self'")
    html = html.replace('</head>', '<link rel="stylesheet" href="./presentation.css">\n</head>')
    emit(OUTPUT / 'index.html', html.encode('utf-8'))
    print('Verified recovered hashes; generated login-free original React frontend.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Verify outputs without modifying them')
    build(parser.parse_args().check)
