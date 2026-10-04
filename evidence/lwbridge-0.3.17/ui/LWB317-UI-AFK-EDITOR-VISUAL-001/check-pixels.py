import json, sys
from pathlib import Path
from PIL import Image
import numpy as np
root = Path(__file__).resolve().parent
results = []
for phase in ['baseline', 'current']:
    data = json.loads((root / phase / 'pairs.json').read_text())
    for pair in data['pairs']:
        a = np.array(Image.open(root / phase / pair['original']['screenshot']).convert('RGBA'))
        b = np.array(Image.open(root / phase / pair['current']['screenshot']).convert('RGBA'))
        differences = int(np.any(a != b, axis=2).sum()) if a.shape == b.shape else None
        results.append({'phase': phase, 'id': pair['id'], 'originalShape': list(a.shape), 'currentShape': list(b.shape), 'differentPixels': differences})
        if phase == 'current':
            assert differences == 0, pair['id']
        else:
            assert differences != 0, pair['id']
record = {'pairs': results, 'pillow': Image.__version__, 'numpy': np.__version__, 'masks': []}
if '--record' in sys.argv:
    (root / 'pixel-results.json').write_text(json.dumps(record, indent=2) + '\n')
else:
    assert record == json.loads((root / 'pixel-results.json').read_text())
print('LWB317_AFK_EDITOR_PIXELS_OK current=12 baselineFailures=12 masks=0')
