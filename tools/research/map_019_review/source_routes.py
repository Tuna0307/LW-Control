"""MAP-019 offline shipped-source route inspection, never executes the game.

Run: python tools/research/map_019_review/source_routes.py
Outputs current artifact digests, focused decoded IL and Lua bytecode under the
ignored artifacts/map019/source-routes directory. No installed file is written.
"""
from __future__ import annotations
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path[:0] = [str(ROOT / 'src/LWBridge.GameScripts'), str(ROOT / 'tools/research/map_009_offline'),
                str(ROOT / 'tools/research/map_014_il_decode')]
from rgmd_disasm import DEFAULT, Resolver, disasm
from rgmd_il import MetadataImage
import run_live_resource_probe as probe
import compact_lua53 as lua
from inspect_current import root_methods, walk, disassemble

OUT = ROOT / 'artifacts/map019/source-routes'
METHODS = {
    'WorldMarchDataManager': ['HandleWorldMarchGet', 'WorldGetMarchInfos',
        'HandleWorldMarchGetImpl', 'ParseDataWorldMarchGet',
        'HandleWorldGetRectMarchInfos', 'HandlePushWorldMarchAddImpl',
        'HandlePushWorldMarchDelImpl'],
    'WorldGetBlockMessage': ['CSSetData'],
    'WorldGetRectMarchInfosMessage': ['CSSetData', 'CSOnResponse'],
    'WorldGetRectMarchInfosMessage+Request': ['.ctor'],
    'WorldPointManager': [
        'ParseWorldGetBlock', 'ParseWorldPointFoldUp', 'GetServerLod',
        'UpdateLWAoi_Big3000', 'SendAoiRequest', 'SendViewRequest',
        'HandleViewPointsReply', 'ParseWorldPointRemove', 'ParseWorldPointUpdate',
        'HandleViewUpdateNotify', 'RecalculateBlockArg', 'get_lwAoiBlockSizeArray'],
}
METHOD_NAME_SEARCH = re.compile(r'(Lod|LWAoi|FoldUp|WorldMarchGet|WorldMarchInfo|FindResource|FindMonster|PointRemove|PointsUpdate|GetNew)', re.I)
MODULE_SEARCH = re.compile(r'(Net/Msgs/.*(FindMonster|FindResource|DispatchFindNearest|WorldGet|WorldMarch|RectMarch|Train|Ghostrecon|Treasure)|DataCenter/.*(TrainDataManager|WorldMarchDataManager|RadarCenterDataManager|Doomsday|Ghostrecon|Treasure)|WorldPointManager|MiniMap|Minimap)', re.I)
LUA_METHOD_SEARCH = re.compile(r'(OnCreate|HandleMessage|HandleWorldMarchGet|FindMonster|FindResource|Get.*List|Handle.*(All|Self|Info)|GetAllTrain|Update.*Info|OnTrainListGet|GhostReconGetAllianceTaskList|OnGet.*Info)', re.I)
ALL_METHOD_MODULES = ('UIMainMiniMap/Controller/UIMainMiniMapCtrl',
                     'UIMainMiniMap/Component/WorldMiniMapComp',
                     'ActGhostreconAllianceManager', 'ActGhostreconManager',
                     'LWDoomsdayManager', 'RadarCenterDataManager')

def digest(data): return hashlib.sha256(data).hexdigest()

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {'staticOnly': True, 'assembly': str(DEFAULT),
                'assemblySha256': digest(DEFAULT.read_bytes()),
                'il': [], 'lua': []}
    if manifest['assemblySha256'] != 'bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e':
        raise ValueError('Unsupported current assembly digest; decoder must be qualified anew')
    img = MetadataImage.load(DEFAULT)
    resolver = Resolver(img)
    inventory = []
    for ti, td in enumerate(img.tables.TypeDef.rows, 1):
        type_name = img.type_name('TypeDef', ti)
        for mr in td.MethodList:
            name = str(mr.row.Name)
            if type_name in METHODS or METHOD_NAME_SEARCH.search(name):
                inventory.append({'type': type_name, 'method': name,
                                  'token': f'0x{0x06000000|mr.row_index:08x}'})
            if type_name in METHODS and name in METHODS[type_name]:
                token = f'0x{0x06000000|mr.row_index:08x}'
                lines = [f'##### {token} {resolver.method_def(mr.row_index)} rva=0x{mr.row.Rva:x}']
                lines += disasm(img, resolver, mr.row_index)
                filename = f'{type_name}.{name}.il.txt'
                payload = ('\n'.join(lines)+'\n').encode()
                (OUT / filename).write_bytes(payload)
                manifest['il'].append({'type': type_name, 'method': name, 'token': token,
                    'file': filename, 'sha256': digest(payload)})
    (OUT / 'method_inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    package = probe.paths()['data']
    manifest['luaPackage'] = str(package)
    manifest['luaPackageSha256'] = digest(package.read_bytes())
    fv, cv, raw = probe.read_lwlf(package)
    entries = dict(raw)
    original = entries.get(probe.ORIGINAL_LUA_ENTRY, entries.get(probe.LUA_ENTRY))
    if (fv, cv) not in ((3, 26), (3, 27)) or original is None or digest(original) != '50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137':
        raise ValueError('Unsupported current Lua content/original entry')
    manifest['originalEntrySha256'] = digest(original)
    manifest['luaVersion'] = [fv, cv]
    for name, encoded in raw:
        if 'LWBridge' in name or not MODULE_SEARCH.search(name): continue
        decoded = probe.decode_lenc(encoded)
        chunk = lua.parse_chunk(decoded)
        methods = root_methods(chunk)
        item = {'module': name, 'encodedSha256': digest(encoded),
                'decodedSha256': digest(decoded), 'methods': methods, 'selected': []}
        lines = []
        for method, locator in methods.items():
            if not LUA_METHOD_SEARCH.search(method) and not any(p in name for p in ALL_METHOD_MODULES): continue
            for p, proto in walk(chunk):
                if p != locator: continue
                lines += [f'##### {method} {p} sourceLines={proto["line"]}-{proto["lastLine"]}']
                lines += disassemble(proto)
                item['selected'].append({'method': method, 'path': p, 'sourceLines': [proto['line'],proto['lastLine']]})
        filename = re.sub(r'[^A-Za-z0-9_.-]', '_', name) + '.txt'
        payload = ('\n'.join(lines)+'\n').encode()
        (OUT / filename).write_bytes(payload)
        item['file'] = filename
        item['receiptSha256'] = digest(payload)
        manifest['lua'].append(item)
    (OUT/'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps({'out': str(OUT), 'assemblySha256': manifest['assemblySha256'],
                      'luaVersion': manifest['luaVersion'], 'ilMethods': len(manifest['il']),
                      'luaModules': len(manifest['lua'])}))

if __name__ == '__main__': main()
