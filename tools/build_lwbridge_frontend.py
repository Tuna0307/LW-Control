"""Reproduce the recovered frontend with a login-free local host adapter.

The evidence directory is immutable. Every transformation is anchored to the
verified 0.3.1 bundle and fails if that bundle changes.
"""
import hashlib
import argparse
import json
import re
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


def remove_locale_template_entry(text, key):
    pattern = re.compile(re.escape(json.dumps(key, ensure_ascii=False)) + r':`[^`]*`,')
    text, count = pattern.subn('', text, count=1)
    if count != 1:
        raise ValueError(f'Expected exactly one locale entry: {key}')
    return text


def apply_hash_locked_delta(text, recipe_path):
    recipe = json.loads(recipe_path.read_text(encoding='utf-8'))
    if recipe.get('schemaVersion') != 1:
        raise ValueError(f'Unsupported frontend delta schema: {recipe_path}')
    base_hash = hashlib.sha256(text.encode('utf-8')).hexdigest()
    if base_hash != recipe['baseSha256']:
        raise ValueError(
            f'Frontend delta base hash mismatch for {recipe_path.name}: '
            f'{base_hash} != {recipe["baseSha256"]}')
    result = text
    edits = recipe.get('edits')
    if not isinstance(edits, list):
        raise ValueError(f'Frontend delta edits are invalid: {recipe_path}')
    last_start = len(result) + 1
    for edit in reversed(edits):
        start = int(edit['start'])
        delete_length = int(edit['deleteLength'])
        insert = str(edit['insert'])
        if start > last_start:
            raise ValueError(f'Frontend delta edits are not ordered: {recipe_path}')
        if start < 0 or delete_length < 0 or start + delete_length > len(result):
            raise ValueError(f'Frontend delta edit is outside the base text: {recipe_path}')
        result = result[:start] + insert + result[start + delete_length:]
        last_start = start
    result_hash = hashlib.sha256(result.encode('utf-8')).hexdigest()
    if result_hash != recipe['resultSha256']:
        raise ValueError(
            f'Frontend delta result hash mismatch for {recipe_path.name}: '
            f'{result_hash} != {recipe["resultSha256"]}')
    return result


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
            # LWB-R7-066 owner override: City Excel export is retired from the
            # shipped API surface. Keep this transform anchored to the immutable
            # recovered bundle so regeneration cannot resurrect the command.
            s = replace_once(s, 'function Vt(e,t){return U(`map_city_export`,{query:e,...t})}', '')
            s = replace_once(s, ',Vt as T,', ',')
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
            # LWB-R7-038: Zombie Boss is a dedicated ninth Map Data kind. The
            # Map Data Auto Scan controls already make it mutually exclusive,
            # so the parent scheduler sanitizer must preserve it too. Keep the
            # recovered Auto Scan default list unchanged.
            s = replace_once(
                s,
                'var Un=new Set([`city`,`resource`,`monster`,`truck`,`railway`,`dispatch`,`ghost`,`treasure`]),Wn=',
                'var Un=new Set([`city`,`resource`,`monster`,`zombie_boss`,`truck`,`railway`,`dispatch`,`ghost`,`treasure`]),Wn=')
            # LWB-R7-067 owner override: Auto Scan no longer owns/persists a
            # Normal/Fast choice. The backend planner selects the effective strategy.
            s = replace_once(s, ',scanMode:`fast`', '')
            s = replace_once(s, ',scanMode:e?.scanMode===`normal`?`normal`:`fast`', '')
            s = replace_once(s, ',scanMode:i.scanMode', '')
            # LWB-R7-130: one failed target server must not abort the rest of an
            # Auto Scan cycle. Keep return-to-origin in the outer finally, but
            # isolate jump/scan/wait errors per target and continue the list.
            s = replace_once(
                s,
                'F(`automatic map scan cycle started servers=${n.join(`,`)}`);for(let t of n){if(e||!Je.current.enabled)break;let n=await Se(t);F(n.changed?`automatic map scan switched ${n.previousServerId} -> ${t}`:`automatic map scan already on server ${t}`),Mt(await Te({selectedTypes:i.selectedTypes,resume:!1}));let a=await r();a?.lastError?F(`automatic map scan server=${t} error=${a.lastError}`):a&&F(`automatic map scan server=${t} completed`)}',
                'F(`automatic map scan cycle started servers=${n.join(`,`)}`);let o=[],s=[];for(let t of n){if(e||!Je.current.enabled)break;try{let n=await Se(t);F(n.changed?`automatic map scan switched ${n.previousServerId} -> ${t}`:`automatic map scan already on server ${t}`),Mt(await Te({selectedTypes:i.selectedTypes,resume:!1}));let a=await r();a?.lastError?(s.push(t),F(`automatic map scan server=${t} error=${a.lastError}`)):(o.push(t),a&&F(`automatic map scan server=${t} completed`))}catch(n){s.push(t),F(`automatic map scan server=${t} error=`+String(n))}}F(`automatic map scan cycle finished completed=${o.join(`,`)} failed=${s.join(`,`)}`)')
            # Give Map Data per-profile preference keys without moving scheduler
            # ownership out of the top-level app.
            s = replace_once(
                s,
                'children:(0,M.jsx)(dr,{activeTab:Et?bt:void 0,',
                'children:(0,M.jsx)(dr,{profileId:u.selectedProfileId,activeTab:Et?bt:void 0,')
            # LWB-R7-127 IMPLEMENTATION POLICY: Home's account rail represents
            # active LWBridge-owned game instances, not every configured profile.
            # Ownership requires both the live PID and instanceId returned by
            # profile_instance_status; an unmanaged game has no instanceId.
            s = replace_once(
                s,
                '},[ae]),!ae)return null;let oe=ae.profiles,se=oe.length>=ae.maxProfiles',
                '},[ae]),(0,j.useEffect)(()=>{if(!ae)return;let e=ae.profiles.filter(e=>{let t=s[e.id];return t?.pid!=null&&typeof t.instanceId==`string`&&t.instanceId.length>0});e.length>0&&!e.some(e=>e.id===ae.selectedProfileId)&&r.select(e[0].id,!1)},[ae,s]),!ae)return null;let oe=ae.profiles,Pe=oe.filter(e=>{let t=s[e.id];return t?.pid!=null&&typeof t.instanceId==`string`&&t.instanceId.length>0}),se=oe.length>=ae.maxProfiles')
            s = replace_once(
                s,
                'children:oe.map(t=>{let n=O(t,o)',
                'children:Pe.map(t=>{let n=O(t,o)')
            s = replace_once(
                s,
                'children:oe.map(t=>{let n=t.id===ae.selectedProfileId',
                'children:Pe.map(t=>{let n=t.id===ae.selectedProfileId')
            s = replace_once(
                s,
                'f=s[t.id],p=f?.phase===`error`&&f.pid==null,m=',
                'f=s[t.id],p=!(f?.pid!=null&&typeof f.instanceId==`string`&&f.instanceId.length>0),m=')
            # PM13-01b: preserve the recovered error formatter, but teach it
            # rebuild-only bounded resource/search errors. These strings are
            # IMPLEMENTATION POLICY, not recovered original LWBridge wording.
            feedback_errors = {
                'GAME_UPDATE_UNSUPPORTED': [
                    'Last War updated, but this version changed a bridge-critical component. LWBridge stopped safely instead of using an unverified game build.',
                    'Last War 已更新，但此版本修改了桥接关键组件。LWBridge 已安全停止，不会使用未经验证的游戏版本。',
                    'Last War 已更新，但此版本修改了橋接關鍵元件。LWBridge 已安全停止，不會使用未經驗證的遊戲版本。',
                    'Last War が更新され、ブリッジに重要なコンポーネントが変更されました。未検証のゲーム版を使用せず、LWBridge は安全に停止しました。',
                    'Last War가 업데이트되었고 브리지 핵심 구성 요소가 변경되었습니다. LWBridge는 검증되지 않은 게임 빌드를 사용하지 않고 안전하게 중지했습니다.',
                    'Last War đã cập nhật và thay đổi một thành phần quan trọng của cầu nối. LWBridge đã dừng an toàn thay vì dùng bản game chưa được xác minh.',
                    'Last War telah diperbarui dan mengubah komponen penting bridge. LWBridge berhenti dengan aman daripada memakai build game yang belum diverifikasi.',
                    'Last War обновилась и изменила критичный для моста компонент. LWBridge безопасно остановилась вместо использования непроверенной версии игры.',
                    'Last War foi atualizado e alterou um componente crítico da ponte. O LWBridge parou com segurança em vez de usar uma versão do jogo não verificada.',
                ],
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
        elif re.match(r'^(en|id|ja|ko|pt|ru|vi|zh-CN|zh-TW)-.*\.js$', path.name):
            s = data.decode('utf-8')
            # LWB-R7-066 / R7-067 owner overrides: retired export and scan-speed
            # labels must not reappear in any shipped locale bundle.
            for key in (
                'map.exportExcel', 'map.exportingExcel', 'map.exportExcelSuccess',
                'map.speed', 'map.normalSpeed', 'map.fastSpeed',
            ):
                s = remove_locale_template_entry(s, key)
            data = s.encode('utf-8')
        elif path.name == 'MapDataPanel-C1HVeNHr.js':
            s = data.decode('utf-8')
            # LWB-R7-066 owner override: remove City Excel export from the
            # generated Map Data panel, including import/state/handler/button.
            s = replace_once(s, ',T as o,', ',')
            s = replace_once(s, ',[Tn,En]=(0,b.useState)(!1)', '')
            s = replace_between(s, 'async function lr(){', 'async function ur(){', '')
            s = replace_once(s, 'F===`city`&&(0,D.jsx)(`button`,{disabled:Tn||L<=0||w.isReading,onClick:lr,children:C(Tn?`map.exportingExcel`:`map.exportExcel`)}),', '')
            # LWB-R7-067 owner override: Manual/Auto Scan expose no Normal/Fast
            # user setting; map_scan_start omits scanMode and backend planning owns it.
            s = replace_once(s, 'ke=`lwbridge.mapScanMode`,', '')
            s = replace_once(s, ',[P,Xe]=(0,b.useState)(()=>{let e=localStorage.getItem(ke);return e===`normal`||e===`fast`?e:w.scanMode||`normal`})', '')
            s = replace_once(s, '(0,b.useEffect)(()=>{localStorage.setItem(ke,P)},[P]),', '')
            s = replace_once(s, 'ae({selectedTypes:e,scanMode:P})', 'ae({selectedTypes:e})')
            s = replace_between(s, '(0,D.jsxs)(`fieldset`,{className:`map-speed-toggle', '(0,D.jsx)(`button`,{className:w.isReading?``:`primary`', '')
            s = replace_once(s, '(0,D.jsxs)(`label`,{children:[(0,D.jsx)(`span`,{children:C(`map.speed`)}),(0,D.jsxs)(`select`,{value:S.scanMode,onChange:e=>$({scanMode:e.target.value===`fast`?`fast`:`normal`}),children:[(0,D.jsx)(`option`,{value:`normal`,children:C(`map.normalSpeed`)}),(0,D.jsx)(`option`,{value:`fast`,children:C(`map.fastSpeed`)})]})]})]}),', '')
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
                'catch(e){t===E.current&&(F===`treasure`&&q(!1),yn({code:`MAP_QUERY_FAILED`}),x(`map search error `+String(e)))}finally{t===E.current&&Yt(!1)}')
            search_button = '(0,D.jsx)(`button`,{onClick:()=>{z===1?er(1):B(1)},children:C(`common.search`)})'
            s = replace_once(s, search_button,
                '(0,D.jsx)(`button`,{onClick:()=>{if(L<=0){yn({code:`MAP_SAVED_CONTEXT_UNAVAILABLE`});return}z===1?er(1):B(1)},children:C(`common.search`)})')
            # LWB-R7-009: preserve owner scan-content selection while stopped.
            # The backend's stopped-state default must not overwrite the local
            # checkbox choice; also keep at least one selected Manual type.
            s = replace_once(s, 'selectedTypes:[...O],totalBlocks:0',
                'selectedTypes:[`city`],totalBlocks:0')
            s = replace_once(s,
                '(0,b.useEffect)(()=>{Ge(tt(w.selectedTypes))},[w.selectedTypes])',
                '(0,b.useEffect)(()=>{w.isReading&&Ge(tt(w.selectedTypes))},[w.isReading,w.selectedTypes])')
            s = replace_once(s, 'disabled:!e.enabled,checked:We.includes(e.key)',
                'disabled:!e.enabled||We.length===1&&We[0]===e.key,checked:We.includes(e.key)')
            # LWB-R7-014 / R7-017 IMPLEMENTATION POLICY: owner-reviewed Monster usability pass.
            # The rebuild adds an inclusive maximum-level selector, localized-name keyword
            # resolution, and a shield-only Remaining countdown from ZMBossInfo.shieldEndTime.
            # R7-017 presents the maximum-level choices in source-bounded increments of five
            # (5, 10, 15, ...) while keeping the backend predicate as level <= selected max.
            # These additions are anchored to the immutable 0.3.1 panel; the original
            # recovered Monster table did not itself expose a Remaining column/level filter.
            s = replace_once(s,
                'function Ge(e){return[`gatherMarchUuid`,`gatherUid`].some(t=>{let n=String(k(e,t)||``).trim();return n!==``&&n!==`0`})}',
                'function Ge(e){return[`gatherMarchUuid`,`gatherUid`].some(t=>{let n=String(k(e,t)||``).trim();return n!==``&&n!==`0`})}function monsterLevelSteps(e){let t=(Array.isArray(e)?e:[]).map(Number).filter(Number.isFinite),n=t.length?Math.max(...t):0;return n>0?Array.from({length:Math.ceil(n/5)},(e,t)=>(t+1)*5):[]}')
            s = replace_once(s,
                '[Wt,Gt]=(0,b.useState)(``),[Kt,qt]=(0,b.useState)(Re)',
                '[Wt,Gt]=(0,b.useState)(``),[monsterLevels,setMonsterLevels]=(0,b.useState)([]),[monsterLevel,setMonsterLevel]=(0,b.useState)(``),[Kt,qt]=(0,b.useState)(Re)')
            s = replace_once(s,
                'if(!w.isReading&&F!==`dispatch`&&F!==`ghost`&&F!==`truck`&&F!==`scheduledPlunder`)return;nn(Date.now())',
                'if(!w.isReading&&F!==`dispatch`&&F!==`ghost`&&F!==`truck`&&F!==`monster`&&F!==`scheduledPlunder`)return;nn(Date.now())')
            s = replace_once(s,
                'ut(t.alliances),ft(t.names),mt(t.dispatchLevels),gt(t.counts)',
                'ut(t.alliances),ft(t.names),mt(t.dispatchLevels),setMonsterLevels(Array.isArray(t.monsterLevels)?t.monsterLevels:[]),gt(t.counts)')
            s = replace_once(s,
                'Gt(e=>!e||t.dispatchLevels.includes(Number(e))?e:``),zt(e=>!e||t.treasureTypes.some(t=>t.key===e)?e:``)',
                'Gt(e=>!e||t.dispatchLevels.includes(Number(e))?e:``),setMonsterLevel(e=>!e||monsterLevelSteps(t.monsterLevels).includes(Number(e))?e:``),zt(e=>!e||t.treasureTypes.some(t=>t.key===e)?e:``)')
            s = replace_once(s,
                'mt([]),gt(ve),vt(!0)',
                'mt([]),setMonsterLevels([]),setMonsterLevel(``),gt(ve),vt(!0)')
            s = replace_once(s,
                'F===`city`&&(0,D.jsxs)(`select`,{"aria-label":C(`map.allianceFilter`)',
                'F===`monster`&&(0,D.jsxs)(`select`,{"aria-label":C(`map.level`),value:monsterLevel,onChange:e=>{setMonsterLevel(e.target.value),B(1)},children:[(0,D.jsx)(`option`,{value:``,children:C(`squad.afkAnyLevel`)}),monsterLevelSteps(monsterLevels).map(e=>(0,D.jsx)(`option`,{value:e,children:e},e))]}),F===`city`&&(0,D.jsxs)(`select`,{"aria-label":C(`map.allianceFilter`)')
            s = replace_once(s,
                'minLevel:n===`dispatch`&&Wt?Number(Wt):void 0,maxLevel:n===`dispatch`&&Wt?Number(Wt):void 0',
                'monsterNameKeys:n===`monster`&&I.trim()?dt.monster.filter(e=>String(j(Xt,e.key,e.key)).toLocaleLowerCase().includes(I.trim().toLocaleLowerCase())).map(e=>e.key):void 0,minLevel:n===`dispatch`&&Wt?Number(Wt):void 0,maxLevel:n===`dispatch`&&Wt?Number(Wt):n===`monster`&&monsterLevel?Number(monsterLevel):void 0')
            s = replace_once(s,
                '[F,Et,L,z,Pt,It,Rt,Bt,Ht,Wt,Ot,Kt,At,Mt,mn,An,h,J,Nn]',
                '[F,Et,L,z,Pt,It,Rt,Bt,Ht,Wt,monsterLevel,Ot,Kt,At,Mt,mn,An,h,J,Nn]')
            s = replace_once(s,
                'function L(e,t,n,r,i,a){',
                'function L(e,t,n,r,i,a,c){')
            s = replace_once(s,
                'e===`monster`?[o,{label:t(`common.name`),width:`minmax(150px, 1fr)`,value:e=>j(r,k(e,`monsterNameKey`),t(`map.unknownMonster`))},{label:t(`map.level`),width:`70px`,sortBy:`level`,value:e=>F(k(e,`level`),n)},{label:t(`map.distance`),width:`90px`,sortBy:`distance`,value:e=>F(k(e,`distanceFromHome`),n)},s]',
                'e===`monster`?[o,{label:t(`common.name`),width:`minmax(150px, 1fr)`,value:e=>j(r,k(e,`monsterNameKey`),t(`map.unknownMonster`))},{label:t(`map.level`),width:`70px`,sortBy:`level`,value:e=>F(k(e,`level`),n)},{label:t(`automation.remaining`),width:`110px`,value:e=>{let t=P(k(e,`shieldEndTime`)??k(e,`zMBossShieldEndTime`));return t&&t>c?Ke(t-c):`-`}},{label:t(`map.distance`),width:`90px`,sortBy:`distance`,value:e=>F(k(e,`distanceFromHome`),n)},s]')
            s = replace_once(s,
                '()=>L(e,g,h,r,i,d),[r,i,e,h,g,d]',
                '()=>L(e,g,h,r,i,d,a),[r,i,e,h,g,d,a]')
            # The six source-attributed Map Data panel checkpoints below predate
            # generator maintenance and were committed directly to the generated
            # panel. Apply their exact text delta only after the known generator
            # base is reproduced; both base and result are SHA-256 locked.
            # Source commits are recorded in the recipe itself.
            s = apply_hash_locked_delta(
                s,
                ROOT / 'tools' / 'frontend_overrides' / 'map-data-panel.delta.json')
            # LWB-R7-130: durable Map Data preferences, saved-server browsing,
            # stale-query invalidation during Clear, and an Auto Scan Stop control.
            s = replace_once(s,
                'filterStoreKey=`lwbridge.mapResultFilters.v1`;function readResultFilters(){try{let e=JSON.parse(localStorage.getItem(filterStoreKey)||`{}`);return e&&typeof e===`object`&&!Array.isArray(e)?e:{}}catch{return{}}}',
                'filterStoreKey=`lwbridge.mapResultFilters.v1`;function readResultFilters(){try{let e=JSON.parse(localStorage.getItem(filterStoreKey)||`{}`);return e&&typeof e===`object`&&!Array.isArray(e)?e:{}}catch{return{}}}function mapPrefKey(e,t){return `lwbridge.${e}.${t||`default`}`}function readManualTypes(e,t){try{let n=JSON.parse(localStorage.getItem(mapPrefKey(`mapManualScanTypes`,e))||`null`),r=Array.isArray(n)?n.filter(e=>Pe.has(e)):[];return r.length?r:tt(t)}catch{return tt(t)}}function readScanTab(e){let t=localStorage.getItem(mapPrefKey(`mapScanTab`,e));return t===`auto`?`auto`:`manual`}function readResultTab(e){let t=localStorage.getItem(mapPrefKey(`mapResultTab`,e));return t&&Pe.has(t)?t:`city`}function readBrowseServer(e,t){let n=Number(localStorage.getItem(mapPrefKey(`mapBrowseServer`,e)));return Number.isInteger(n)&&n>0?n:Number(t)||0}')
            s = replace_once(s, 'function ct({activeTab:s,onActiveTabChange:d,scanState:ee,', 'function ct({profileId:profileId,activeTab:s,onActiveTabChange:d,scanState:ee,')
            s = replace_once(s,
                'savedResultFilters=readResultFilters(),[We,Ge]=(0,b.useState)(()=>tt(w.selectedTypes)),[Ze,Qe]=(0,b.useState)(`city`),F=s??Ze,[I,$e]',
                'savedResultFilters=readResultFilters(),[We,Ge]=(0,b.useState)(()=>readManualTypes(profileId,w.selectedTypes)),[Ze,Qe]=(0,b.useState)(()=>readResultTab(profileId)),F=s??Ze,[I,$e]')
            s = replace_once(s,
                '[I,$e]=(0,b.useState)(()=>typeof savedResultFilters.keyword===`string`?savedResultFilters.keyword:``),[L,ct]=(0,b.useState)(()=>w.serverId),',
                '[I,$e]=(0,b.useState)(()=>typeof savedResultFilters.keyword===`string`?savedResultFilters.keyword:``),[L,ct]=(0,b.useState)(()=>readBrowseServer(profileId,w.serverId)),')
            s = replace_once(s, '[Y,Fn]=(0,b.useState)(`manual`),[In,Ln]', '[Y,Fn]=(0,b.useState)(()=>readScanTab(profileId)),[In,Ln]')
            s = replace_once(s,
                '(0,b.useEffect)(()=>{localStorage.setItem(Ae,String(An))},[An]),',
                '(0,b.useEffect)(()=>{localStorage.setItem(mapPrefKey(`mapManualScanTypes`,profileId),JSON.stringify(We))},[profileId,We]),(0,b.useEffect)(()=>{localStorage.setItem(mapPrefKey(`mapScanTab`,profileId),Y)},[profileId,Y]),(0,b.useEffect)(()=>{localStorage.setItem(mapPrefKey(`mapResultTab`,profileId),F)},[profileId,F]),(0,b.useEffect)(()=>{L>0&&localStorage.setItem(mapPrefKey(`mapBrowseServer`,profileId),String(L))},[profileId,L]),(0,b.useEffect)(()=>{let e=readResultTab(profileId);d?d(e):Qe(e)},[profileId]),(0,b.useEffect)(()=>{localStorage.setItem(Ae,String(An))},[An]),')
            s = replace_once(s,
                '(0,b.useEffect)(()=>{if(w.serverId!==L){if(E.current+=1,O.current.clear(),w.serverId<=0){ct(0),B(1),H([]),U(0),Yt(!1);return}ct(w.serverId),B(1),H([]),U(0),Yt(!0)}},[L,w.serverId]),',
                '(0,b.useEffect)(()=>{if(w.isReading&&w.serverId!==L){E.current+=1,Pe.current+=1,O.current.clear(),ct(w.serverId),B(1),H([]),U(0),Yt(!0);return}!w.isReading&&L<=0&&w.serverId>0&&ct(w.serverId)},[L,w.serverId,w.isReading]),')
            s = replace_once(s,
                'async function Zn(){try{v(await l()),x(`full map scan stopped`)}catch(e){x(`map scan stop error `+String(e))}}async function Qn(){yn(``);try{',
                'async function Zn(){try{v(await l()),x(`full map scan stopped`)}catch(e){x(`map scan stop error `+String(e))}}async function stopAutoScan(){$({enabled:!1});await Zn()}async function Qn(){E.current+=1,Pe.current+=1,Yt(!1),q(!1),yn(``);try{')
            s = replace_once(s,
                '(0,D.jsx)(`button`,{type:`button`,className:`primary`,disabled:!h||!S.enabled||be||w.isReading,onClick:()=>$({nextRunAt:Date.now()}),children:C(`map.runAutoScanNow`)})',
                '(0,D.jsx)(`button`,{type:`button`,className:`primary`,disabled:!h||!S.enabled||be||w.isReading,onClick:()=>$({nextRunAt:Date.now()}),children:C(`map.runAutoScanNow`)}),(0,D.jsx)(`button`,{type:`button`,className:be?`danger`:`` ,disabled:!be,onClick:stopAutoScan,children:C(`common.stop`)})')
            s = replace_once(s,
                'F!==`scheduledPlunder`&&(0,D.jsxs)(`div`,{className:`map-searchbar`,children:[(0,D.jsx)(`input`,{value:I,',
                'F!==`scheduledPlunder`&&(0,D.jsxs)(`div`,{className:`map-searchbar`,children:[Array.isArray(p?.savedServerIds)&&p.savedServerIds.length>1&&(0,D.jsx)(`select`,{\"aria-label\":C(`map.server`),value:L,onChange:e=>{E.current+=1,Pe.current+=1,O.current.clear(),ct(Number(e.target.value)),B(1),H([]),U(0),Yt(!0)},children:p.savedServerIds.map(e=>(0,D.jsx)(`option`,{value:e,children:`${C(`map.server`)} ${e}`},e))}),(0,D.jsx)(`input`,{value:I,')
            # LWB-R7-131: Clear already invalidates the in-flight saved-search
            # generation. It must also suppress the one automatic search effect
            # caused by Clear's own state resets, otherwise the cleared server is
            # queried again immediately and can surface the same transient SQLite
            # error that R7-130 intended to make stale.
            s = replace_once(s,
                'we=(0,b.useRef)(v),Ee=(0,b.useRef)(y),T=(0,b.useRef)(x),E=(0,b.useRef)(0),O=(0,b.useRef)(new Map),Pe=(0,b.useRef)(0),',
                'we=(0,b.useRef)(v),Ee=(0,b.useRef)(y),T=(0,b.useRef)(x),E=(0,b.useRef)(0),O=(0,b.useRef)(new Map),Pe=(0,b.useRef)(0),clearAutoSearchOnce=(0,b.useRef)(!1),')
            s = replace_once(s,
                '(0,b.useEffect)(()=>{if(F!==`scheduledPlunder`&&!(F===`treasure`&&h&&J&&!Nn.playerUid)){if(!L){E.current+=1,B(1),H([]),U(0);return}er(z)}},[F,Et,L,z,Pt,It,Rt,Bt,Ht,Wt,monsterLevel,resourceLevel,resourceIdleOnly,resourceFullOnly,excludeBlackTile,Ot,Kt,At,Mt,mn,An,h,J,Nn]),',
                '(0,b.useEffect)(()=>{if(clearAutoSearchOnce.current){clearAutoSearchOnce.current=!1;return}if(F!==`scheduledPlunder`&&!(F===`treasure`&&h&&J&&!Nn.playerUid)){if(!L){E.current+=1,B(1),H([]),U(0);return}er(z)}},[F,Et,L,z,Pt,It,Rt,Bt,Ht,Wt,monsterLevel,resourceLevel,resourceIdleOnly,resourceFullOnly,excludeBlackTile,Ot,Kt,At,Mt,mn,An,h,J,Nn]),')
            s = replace_once(s,
                'async function Qn(){E.current+=1,Pe.current+=1,Yt(!1),q(!1),yn(``);try{let e=await ne(L);Ie.current+=1,',
                'async function Qn(){E.current+=1,Pe.current+=1,Yt(!1),q(!1),yn(``);try{let e=await ne(L);clearAutoSearchOnce.current=!0,Ie.current+=1,')
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
