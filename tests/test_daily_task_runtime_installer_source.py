import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from tools.install_daily_task_runtime import _atomic_copy


class DailyTaskRuntimeInstallerChecks(unittest.TestCase):
    def test_atomic_copy_replaces_destination_with_exact_source(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.bin"
            destination = root / "destination.bin"
            source.write_bytes(b"new-runtime")
            destination.write_bytes(b"official")
            _atomic_copy(source, destination)
            self.assertEqual(destination.read_bytes(), b"new-runtime")


if __name__ == "__main__":
    unittest.main()
