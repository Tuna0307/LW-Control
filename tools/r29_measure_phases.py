"""Passive process-tree sampling for an explicitly owned packaged R29 proof."""
from pathlib import Path
import argparse
import json
import subprocess
import time
import r29_part_c_sampler as sampler


def process_tree(host_pid):
    result = subprocess.run(['powershell', '-NoProfile', '-Command',
        'Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId,Name | ConvertTo-Json -Compress'],
        capture_output=True, text=True, check=True)
    rows = json.loads(result.stdout)
    selected = {host_pid}
    for _ in range(10):
        more = {r['ProcessId'] for r in rows if r['ParentProcessId'] in selected}
        if more <= selected:
            break
        selected |= more
    return [sampler.ProcessTargetSpec(f"app-{r['Name']}-{r['ProcessId']}", r['ProcessId'])
            for r in rows if r['ProcessId'] in selected]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    owned = json.loads((root / 'owned-process.json').read_text())
    admission = json.loads((root / 'feature018-account-admission.json').read_text())
    # No account is privileged by the sampler: match the exact captured host
    # and authenticated game session; all measurements are read-only.
    host_pid, game_pid = owned['pid'], admission['gamePid']
    args.output.mkdir(parents=True, exist_ok=True)
    seen = set()
    until = time.monotonic() + 2800
    metrics = root / 'overview-bridge/r29-perf.json'
    def read_metrics():
        try:
            return json.loads(metrics.read_text())
        except (OSError, ValueError):
            return None
    while time.monotonic() < until:
        try:
            phase = json.loads((root / 'r29-perf-phase.json').read_text())
        except (OSError, ValueError):
            time.sleep(.25)
            continue
        name = phase['phase']
        if name == 'finished':
            break
        if name in seen:
            time.sleep(.25)
            continue
        seen.add(name)
        before = read_metrics()
        specs = process_tree(host_pid) + [sampler.ProcessTargetSpec('game', game_pid)]
        report = sampler.run_sampler(specs, [sampler.FileRootSpec('runtime', root / 'overview-bridge')],
            duration_seconds=phase['seconds'], interval_seconds=1 if phase['seconds'] > 300 else .25)
        report.update(phase=phase, native_metrics_before=before, native_metrics_after=read_metrics(),
            process_tree_scope='captured host and its current descendants; exact authenticated game separate')
        path = args.output / (name + '.json')
        path.write_text(json.dumps(report), encoding='utf-8')
        print('R29_PHASE_SAMPLED', name, path, flush=True)


if __name__ == '__main__':
    main()
