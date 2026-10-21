"""Copy a freshly built production UI into an owner's running test copy (screen-only live update).

The running app watches its own ProductionUi folder and reloads when the build identity file
changes. Assets and index.html are copied first; lwbridge-ui-build.json is written last so the
app never reloads a half-copied build. Never touches the game, bridge scripts or app binaries.
"""
from pathlib import Path
import argparse
import json
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BUILT = ROOT / 'src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0/ProductionUi'
IDENTITY = 'lwbridge-ui-build.json'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--target', type=Path, required=True, help='the owner copy LW-Control-UI folder')
    parser.add_argument('--skip-build', action='store_true')
    args = parser.parse_args()
    target = (args.target.resolve(strict=True) / 'ProductionUi')
    if not (target / IDENTITY).is_file() or not (args.target / 'LWBridge.Desktop.exe').is_file():
        raise SystemExit('Target is not an LW-Control-UI folder with a production UI')
    if not args.skip_build:
        subprocess.run(['dotnet', 'build', str(ROOT / 'src/LWBridge.Desktop'), '-c', 'Release', '-v', 'q'], check=True)
    identity = json.loads((BUILT / IDENTITY).read_text(encoding='utf-8'))
    for source in sorted(BUILT.rglob('*')):
        if source.is_file() and source.name != IDENTITY:
            destination = target / source.relative_to(BUILT)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
    temporary = target / (IDENTITY + '.tmp')
    shutil.copy2(BUILT / IDENTITY, temporary)
    temporary.replace(target / IDENTITY)
    print('LIVE_UI_DEPLOYED ' + json.dumps({'target': str(target), 'artifactFingerprint': identity.get('artifactFingerprint')}))


if __name__ == '__main__':
    main()
