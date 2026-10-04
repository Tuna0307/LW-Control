"""Read-only independent decoding. Only source-evidenced individual native controls can be masked."""
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

HERE = Path(__file__).resolve().parent
CAMPAIGN = HERE.parent.parent
REPO = CAMPAIGN.parents[3]

def box(rect):
    return [math.floor(rect['x']), math.floor(rect['y']), math.ceil(rect['x'] + rect['width']), math.ceil(rect['y'] + rect['height'])]

def compare(unit, subfolder):
    folder = CAMPAIGN / unit / subfolder
    measured = json.loads((folder / 'measurements.json').read_text(encoding='utf8'))
    pairs = {}
    for record in measured['records']:
        pairs.setdefault(record['pairId'], {})[record['side']] = record
    results = []
    for pair_id, records in pairs.items():
        left, right = records['original'], records['current']
        a, b = [Image.open(REPO / r['screenshot']).convert('RGB') for r in [left, right]]
        assert a.size == b.size
        rgb = ImageChops.difference(a, b)
        red, green, blue = rgb.split()
        changed = ImageChops.lighter(ImageChops.lighter(red, green), blue).point(lambda v: 255 if v else 0)
        allowed = Image.new('L', a.size)
        draw = ImageDraw.Draw(allowed)
        controls = []
        # Native availability fences only: exact same original/current button box,
        # source-side enabled/current-side disabled, and exact known native label.
        permitted = {'Claim Boxes', 'Claim Season Treasures', '宝箱を一括受取', 'シーズン宝物を一括受取'} if unit == 'unit-a' else {'Start Scan', 'スキャン開始'}
        for name, current in right['measurement']['anchors'].items():
            original = left['measurement']['anchors'].get(name)
            if not current or not original or current['tag'] != 'BUTTON':
                continue
            ca, oa = dict(current['attrs']), dict(original['attrs'])
            if current['text'] in permitted and 'disabled' in ca and 'disabled' not in oa:
                assert current['rect'] == original['rect'], (pair_id, name, 'native control geometry')
                bounds = box(current['rect'])
                # Pillow rectangle includes final pixel: subtract one for exact half-open bounds.
                draw.rectangle([bounds[0], bounds[1], bounds[2]-1, bounds[3]-1], fill=255)
                controls.append({'anchor': name, 'label': current['text'], 'box': bounds, 'reason': 'unavailable native action'})
        outside = ImageChops.subtract(changed, allowed)
        count = lambda image: sum(v != 0 for v in image.getdata())
        results.append({'unit': unit, 'pair': pair_id, 'changedPixels': count(changed), 'outsideNativeControlPixels': count(outside), 'nativeMasks': controls, 'outsideBBox': outside.getbbox()})
    return results

results = compare('unit-a', 'browser') + compare('unit-a', 'action-message-browser/browser') + compare('unit-b', 'browser')
blocked = [r for r in results if r['outsideNativeControlPixels']]
print(json.dumps({'result': 'CHANGES_REQUIRED' if blocked else 'PASS', 'pairs': len(results), 'results': results, 'blocked': blocked, 'scope': 'Recomputed decoded submitted PNG pixels; no query-error banner, table, asset or panel masks.'}, indent=2))
raise SystemExit(1 if blocked else 0)
