"""PM audit: reproduce partial-install rollback gaps on disposable dummy files only.

Run from repository root: python evidence/lwbridge-implementation/2026-09-10-pm12-partial-install-repro.py
No installed game files, package construction, launcher or game process is used.
The output describes the defect at the audited revision; it is not a passing
restoration regression test and is intentionally retained as historical evidence.
"""
from pathlib import Path
import importlib.util
import json
import shutil
import uuid
from unittest.mock import patch

repo = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('pm12_helper', repo/'tools/run_live_resource_probe.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
workspace = (repo/'.codex-live/pm-review-12').resolve()
workspace.mkdir(parents=True, exist_ok=True)
results = []
for fail_at in (2, 3):
    root = workspace/('dummy-install-'+uuid.uuid4().hex)
    root.mkdir()
    candidate = root/'candidate'
    candidate.mkdir()
    p = {'runtime':root/'runtime', 'backup_root':root/'backups'}
    originals = {}
    for key, name in [('data','LWScripts.data'),('metadata','LWScripts.txt'),('version','version.txt')]:
        p[key] = root/name
        originals[key] = ('DUMMY ORIGINAL '+key).encode()
        p[key].write_bytes(originals[key])
        (candidate/name).write_text('DUMMY CANDIDATE '+key)
    calls = {'copy':0, 'restore':0}
    def dummy_copy(source, destination):
        assert Path(source).resolve().is_relative_to(root)
        assert Path(destination).resolve().is_relative_to(root)
        calls['copy'] += 1
        if calls['copy'] == fail_at:
            raise OSError('PM12 injected dummy install failure')
        shutil.copyfile(source, destination)
    def dummy_restore(paths, backup):
        calls['restore'] += 1
        for key in originals:
            paths[key].write_bytes(originals[key])
        return {'restored':True}
    def forbidden(*args, **kwargs):
        raise AssertionError('PM12 must never launch or stop a real process')
    with patch.object(helper,'paths',return_value=p), \
         patch.object(helper,'process_running',return_value=False), \
         patch.object(helper,'verify_current',return_value={'dummy':True}), \
         patch.object(helper,'make_candidate',return_value={'dummy':True}), \
         patch.object(helper.tempfile,'mkdtemp',return_value=str(candidate)), \
         patch.object(helper,'copy_atomic',side_effect=dummy_copy), \
         patch.object(helper,'restore_backup',side_effect=dummy_restore), \
         patch.object(helper,'stop_game',side_effect=forbidden), \
         patch.object(helper.subprocess,'Popen',side_effect=forbidden):
        try:
            helper.run('pm12-dummy',10,False)
            error = None
        except OSError as exc:
            error = str(exc)
    changed = [key for key in originals if p[key].read_bytes()!=originals[key]]
    results.append({'failureAtReplacement':fail_at,'error':error,
                    'restoreCalls':calls['restore'],'dummyFilesLeftChanged':changed})
report = {'scope':'isolated dummy files; no live operations',
          'auditedRevision':'749b8e3bdca0ed92f7d22aae0f1ab0c1d562a4cd',
          'cases':results,
          'partialInstallDefectReproduced':all(x['restoreCalls']==0 and x['dummyFilesLeftChanged'] for x in results)}
print(json.dumps(report,indent=2))
