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
            results.append({'view': reference.stem.removesuffix('-reference'), 'mae': mean, 'highDeltaRate': changed, 'identical': diff.getbbox() is None})
            if diff.getbbox():
                diff.save(reference.with_name(reference.name.replace('-reference.png', '-diff.png')))
    assert len(results) == 32, f'Expected 32 paired states, got {len(results)}'
    (OUTPUT / 'pixel-diff.json').write_text(json.dumps(results, indent=2))
    print(json.dumps({'pairs': len(results), 'identical': sum(r['identical'] for r in results), 'maxMae': max(r['mae'] for r in results), 'maxHighDeltaRate': max(r['highDeltaRate'] for r in results)}, indent=2))
    assert all(r['mae'] <= 0.5 and r['highDeltaRate'] <= 0.005 for r in results), 'Visual mismatch; inspect diff PNGs'


if __name__ == '__main__':
    main()
