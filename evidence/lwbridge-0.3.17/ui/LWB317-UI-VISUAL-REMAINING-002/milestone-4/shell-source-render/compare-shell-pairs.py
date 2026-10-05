from pathlib import Path
import json, sys
from PIL import Image, ImageChops
import numpy as np

root = Path(__file__).resolve().parent / 'browser'
packet = json.loads((root / 'pairs.json').read_text(encoding='utf-8'))
results = []
for pair in packet['pairs']:
    original = Image.open(root / pair['original']['screenshot']).convert('RGB')
    current = Image.open(root / pair['current']['screenshot']).convert('RGB')
    size = (max(original.width, current.width), max(original.height, current.height))
    a = Image.new('RGB', size, (255, 0, 255)); a.paste(original, (0, 0))
    b = Image.new('RGB', size, (0, 255, 255)); b.paste(current, (0, 0))
    changed = int(np.any(np.asarray(ImageChops.difference(a, b)), axis=2).sum())
    om = json.loads((root / pair['original']['measurement']).read_text(encoding='utf-8'))
    cm = json.loads((root / pair['current']['measurement']).read_text(encoding='utf-8'))
    geometry = []
    selectors = sorted(set(om['keyGeometry']) | set(cm['keyGeometry']))
    for selector in selectors:
        if om['keyGeometry'].get(selector) != cm['keyGeometry'].get(selector):
            geometry.append({'selector': selector, 'original': om['keyGeometry'].get(selector), 'current': cm['keyGeometry'].get(selector)})
    element_differences = sum(1 for i in range(max(len(om['elements']), len(cm['elements']))) if (om['elements'][i] if i < len(om['elements']) else None) != (cm['elements'][i] if i < len(cm['elements']) else None))
    results.append({'id': pair['id'], 'language': pair['language'], 'theme': pair['theme'], 'width': pair['width'], 'originalSize': original.size, 'currentSize': current.size, 'changedPixels': changed, 'outsideMasks': changed, 'masks': [], 'rawDomIdentical': om['html'] == cm['html'], 'elementDifferences': element_differences, 'keyGeometryDifferences': geometry, 'originalAncestry': om['ancestry'], 'currentAncestry': cm['ancestry'], 'originalExitAfterShell': om['exitAfterShell'], 'currentExitAfterShell': cm['exitAfterShell'], 'focusedOriginal': om['focused'], 'focusedCurrent': cm['focused']})
output = {'pairs': len(results), 'exactPixels': sum(item['changedPixels'] == 0 for item in results), 'totalChangedPixels': sum(item['changedPixels'] for item in results), 'cases': results, 'classification': 'Raw unmasked comparison. Expected product-scope/provider fences (account/upgrade/current unavailable actions and supplemental current aria/data attributes) remain visible differences; no masks convert them to parity.'}
encoded = json.dumps(output, ensure_ascii=False, indent=2) + '\n'
target = root / 'comparison-results.json'
if '--verify' in sys.argv:
    assert target.read_text(encoding='utf-8') == encoded, 'fresh pixel/measurement comparison differs from pinned result'
else:
    target.write_text(encoded, encoding='utf-8')
print(json.dumps({'result': 'SHELL_PIXEL_COMPARE_OK', 'pairs': len(results), 'exactPixels': output['exactPixels'], 'totalChangedPixels': output['totalChangedPixels']}))
