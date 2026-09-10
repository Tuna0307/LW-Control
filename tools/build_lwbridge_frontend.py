"""Reproduce the recovered frontend with a login-free local host adapter.

The evidence directory is immutable. Every transformation is anchored to the
verified 0.3.1 bundle and fails if that bundle changes.
"""
import hashlib
import argparse
import json
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
            # PM13-01b: preserve the recovered error formatter, but teach it
            # rebuild-only bounded resource/search errors. These strings are
            # IMPLEMENTATION POLICY, not recovered original LWBridge wording.
            feedback_errors = {
                'LIVE_RESOURCE_TYPES_UNSUPPORTED': [
                    'Live map scan currently supports Resource Point only. Select only Resource Point, then start the scan.',
                    '当前实机地图扫描仅支持“资源点”。请只勾选“资源点”，然后开始扫描。',
                    '目前實機地圖掃描僅支援「資源點」。請只勾選「資源點」，然後開始掃描。',
                    '現在のライブマップスキャンは「資源ポイント」のみ対応しています。「資源ポイント」だけを選択してからスキャンを開始してください。',
                    '현재 라이브 지도 스캔은 자원 지점만 지원합니다. 자원 지점만 선택한 뒤 스캔을 시작하세요.',
                    'Quét bản đồ trực tiếp hiện chỉ hỗ trợ Điểm tài nguyên. Chỉ chọn Điểm tài nguyên rồi bắt đầu quét.',
                    'Pemindaian peta langsung saat ini hanya mendukung Titik Sumber Daya. Pilih hanya Titik Sumber Daya, lalu mulai pemindaian.',
                    'Сейчас сканирование карты поддерживает только точки ресурсов. Выберите только «Точка ресурсов», затем запустите сканирование.',
                    'No momento, a varredura ao vivo do mapa aceita apenas Pontos de Recurso. Selecione somente Ponto de Recurso e inicie a varredura.',
                ],
                'MAP_SAVED_CONTEXT_UNAVAILABLE': [
                    'No saved map server is available for this profile. Run a Resource Point scan first, then search again.',
                    '此账号没有可用的已保存地图区服。请先运行“资源点”扫描，然后再次搜索。',
                    '此帳號沒有可用的已儲存地圖伺服器。請先執行「資源點」掃描，然後再次搜尋。',
                    'このプロファイルには保存済みのマップサーバー情報がありません。先に「資源ポイント」をスキャンしてから、もう一度検索してください。',
                    '이 프로필에는 저장된 지도 서버 정보가 없습니다. 먼저 자원 지점 스캔을 실행한 뒤 다시 검색하세요.',
                    'Hồ sơ này chưa có máy chủ bản đồ đã lưu. Hãy quét Điểm tài nguyên trước rồi tìm kiếm lại.',
                    'Profil ini belum memiliki server peta tersimpan. Jalankan pemindaian Titik Sumber Daya terlebih dahulu, lalu cari lagi.',
                    'Для этого профиля нет сохранённого сервера карты. Сначала выполните сканирование точек ресурсов, затем повторите поиск.',
                    'Este perfil não tem um servidor de mapa salvo. Primeiro execute uma varredura de Pontos de Recurso e depois pesquise novamente.',
                ],
                'MAP_SAVED_CONTEXT_AMBIGUOUS': [
                    'Saved map data for this profile spans multiple servers, so one cannot be chosen safely. Establish the current server with a Resource Point scan, then search again.',
                    '此账号保存了多个区服的地图数据，程序无法安全选择其中一个。请先通过“资源点”扫描建立当前区服，然后再次搜索。',
                    '此帳號儲存了多個伺服器的地圖資料，程式無法安全選擇其中一個。請先透過「資源點」掃描建立目前伺服器，然後再次搜尋。',
                    'このプロファイルには複数サーバーの保存済みマップデータがあるため、安全に1つを選択できません。「資源ポイント」をスキャンして現在のサーバーを確立してから、もう一度検索してください。',
                    '이 프로필에는 여러 서버의 저장된 지도 데이터가 있어 하나를 안전하게 선택할 수 없습니다. 자원 지점 스캔으로 현재 서버를 확인한 뒤 다시 검색하세요.',
                    'Hồ sơ này có dữ liệu bản đồ đã lưu từ nhiều máy chủ nên không thể chọn an toàn một máy chủ. Hãy quét Điểm tài nguyên để xác định máy chủ hiện tại rồi tìm kiếm lại.',
                    'Profil ini memiliki data peta tersimpan dari beberapa server sehingga satu server tidak dapat dipilih dengan aman. Jalankan pemindaian Titik Sumber Daya untuk menetapkan server saat ini, lalu cari lagi.',
                    'В этом профиле сохранены данные карты с нескольких серверов, поэтому нельзя безопасно выбрать один. Выполните сканирование точек ресурсов, чтобы определить текущий сервер, затем повторите поиск.',
                    'Este perfil contém dados de mapa salvos de vários servidores, então não é seguro escolher um automaticamente. Execute uma varredura de Pontos de Recurso para definir o servidor atual e pesquise novamente.',
                ],
                'MAP_QUERY_FAILED': [
                    'Saved map search failed. Try Search again. Technical details remain in the application log.',
                    '已保存地图数据搜索失败。请再次点击“搜索”。技术错误详情仍保留在程序日志中。',
                    '已儲存地圖資料搜尋失敗。請再次按「搜尋」。技術錯誤詳情仍保留在程式記錄中。',
                    '保存済みマップデータの検索に失敗しました。もう一度「検索」を実行してください。技術的な詳細はアプリのログに保持されています。',
                    '저장된 지도 데이터 검색에 실패했습니다. 검색을 다시 시도하세요. 기술 세부 정보는 앱 로그에 남아 있습니다.',
                    'Tìm kiếm dữ liệu bản đồ đã lưu thất bại. Hãy thử Tìm kiếm lại. Chi tiết kỹ thuật vẫn được giữ trong nhật ký ứng dụng.',
                    'Pencarian data peta tersimpan gagal. Coba Cari lagi. Detail teknis tetap disimpan di log aplikasi.',
                    'Не удалось выполнить поиск по сохранённым данным карты. Повторите поиск. Технические сведения сохранены в журнале приложения.',
                    'A pesquisa nos dados de mapa salvos falhou. Tente Pesquisar novamente. Os detalhes técnicos permanecem no log do aplicativo.',
                ],
            }
            feedback_error_js = json.dumps(feedback_errors, ensure_ascii=True, separators=(',', ':'))[1:-1] + ','
            s = replace_once(s, 'SERVER_JUMP_TIMEOUT:[', feedback_error_js + 'SERVER_JUMP_TIMEOUT:[')
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
            start_error = 'catch(e){let t=String(e);yn(t),x(`map scan start error `+t)}'
            s = replace_once(s, start_error,
                'catch(e){let t=String(e);yn(e),x(`map scan start error `+t)}')
            # PM13-01b: reuse the recovered visible scan-error surface for
            # search/context failures, without changing the idle layout.
            search_start = 'async function er(e=z){if(F===`scheduledPlunder`)return;let t=E.current+1;E.current=t,Yt(!0);try{'
            s = replace_once(s, search_start,
                'async function er(e=z){if(F===`scheduledPlunder`)return;let t=E.current+1;E.current=t,Yt(!0),yn(``);try{')
            search_catch = 'catch(e){t===E.current&&(F===`treasure`&&q(!1),H([]),U(0),x(`map search error `+String(e)))}finally{t===E.current&&Yt(!1)}'
            s = replace_once(s, search_catch,
                'catch(e){t===E.current&&(F===`treasure`&&q(!1),yn({code:`MAP_QUERY_FAILED`}),H([]),U(0),x(`map search error `+String(e)))}finally{t===E.current&&Yt(!1)}')
            search_button = '(0,D.jsx)(`button`,{onClick:()=>{z===1?er(1):B(1)},children:C(`common.search`)})'
            s = replace_once(s, search_button,
                '(0,D.jsx)(`button`,{onClick:()=>{if(L<=0){yn({code:`MAP_SAVED_CONTEXT_UNAVAILABLE`});return}z===1?er(1):B(1)},children:C(`common.search`)})')
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
