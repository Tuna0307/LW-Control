"""Compare feature screenshots from check_lwbridge_frontend.cjs (Pillow)."""
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageStat

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / '.codex-live/lwbridge-ui-verification'


def main():
    results = []
    for reference in sorted(OUTPUT.glob('*-reference.png')):
        candidate = reference.with_name(reference.name.replace('-reference.png', '-rebuilt.png'))
        with Image.open(reference) as a, Image.open(candidate) as b:
            assert a.size == b.size
            diff = ImageChops.difference(a.convert('RGB'), b.convert('RGB'))
            mean = sum(ImageStat.Stat(diff).mean) / 3
            channels = diff.split()
            maximum = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
            changed = sum(maximum.histogram()[31:]) / (a.width * a.height)
            view = reference.stem.removesuffix('-reference')
            intentional = view.startswith('map-data-')
            results.append({'view': view, 'mae': mean, 'highDeltaRate': changed, 'identical': diff.getbbox() is None, 'intentionalMapDataOverride': intentional})
            if diff.getbbox():
                diff.save(reference.with_name(reference.name.replace('-reference.png', '-diff.png')))
    assert len(results) == 32, f'Expected 32 paired states, got {len(results)}'
    (OUTPUT / 'pixel-diff.json').write_text(json.dumps(results, indent=2))
    strict = [r for r in results if not r['intentionalMapDataOverride']]
    map_data = [r for r in results if r['intentionalMapDataOverride']]
    print(json.dumps({
        'pairs': len(results),
        'strictPairs': len(strict),
        'intentionalMapDataPairs': len(map_data),
        'identical': sum(r['identical'] for r in results),
        'strictMaxMae': max(r['mae'] for r in strict),
        'strictMaxHighDeltaRate': max(r['highDeltaRate'] for r in strict),
        'mapDataMaxMae': max(r['mae'] for r in map_data),
        'mapDataMaxHighDeltaRate': max(r['highDeltaRate'] for r in map_data),
    }, indent=2))
    assert len(map_data) == 4, f'Expected four documented Map Data override pairs, got {len(map_data)}'
    assert all(r['mae'] <= 0.5 and r['highDeltaRate'] <= 0.005 for r in strict), 'Unexpected visual mismatch outside documented Map Data overrides; inspect diff PNGs'
    # R7-066/R7-067 plus dedicated Zombie Boss intentionally change Map Data
    # geometry relative to immutable 0.3.1. Keep that delta bounded so a broad
    # rendering regression cannot hide behind the documented exemption.
    assert all(r['mae'] <= 8.0 and r['highDeltaRate'] <= 0.04 for r in map_data), 'Map Data override visual delta exceeded the audited bound; inspect diff PNGs'


if __name__ == '__main__':
    main()
