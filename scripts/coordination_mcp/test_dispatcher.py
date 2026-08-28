import json
import shutil
import subprocess
import sys
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import dispatcher


class DispatcherTest(unittest.TestCase):
    def test_routes_args_and_failures(self):
        routes = dispatcher.load_routes()
        codex, antigravity = dispatcher.allowed_ids(routes)
        self.assertEqual((len(routes), len(codex), len(antigravity)), (8, 8, 9))
        target = next(iter(antigravity))
        args = dispatcher.build_agy_args(target, "x; Remove-Item *")
        self.assertFalse(isinstance(args, str)); self.assertEqual(args[1:3], ["--conversation", target]); self.assertIn("x; Remove-Item *", args)
        with patch("dispatcher.subprocess.run", side_effect=subprocess.TimeoutExpired(args, 1)):
            with self.assertRaisesRegex(RuntimeError, "antigravity_timeout"): dispatcher.run_agy(args)
        with patch("dispatcher.subprocess.run", return_value=subprocess.CompletedProcess(args, 2, "", "bad")):
            with self.assertRaisesRegex(RuntimeError, "antigravity_nonzero"): dispatcher.run_agy(args)
        with patch("dispatcher.subprocess.run", return_value=subprocess.CompletedProcess(args, 0, "not-json", "")):
            with self.assertRaisesRegex(RuntimeError, "antigravity_invalid_json"): dispatcher.run_agy(args)
        with patch("dispatcher.subprocess.run", return_value=subprocess.CompletedProcess(args, 0, json.dumps({"status":"ok"}), "")):
            self.assertEqual(dispatcher.run_agy(args)["status"], "ok")

    def test_duplicate_and_missing_routes_rejected(self):
        folder = dispatcher.ROOT / "scripts" / "coordination_mcp" / ".test_routes" / uuid.uuid4().hex
        folder.mkdir(parents=True)
        try:
            path = folder / "AGENTS.md"
            path.write_text("* **A** : 11111111-1111-1111-1111-111111111111 / 22222222-2222-2222-2222-222222222222\n* **B** : 11111111-1111-1111-1111-111111111111 / 33333333-3333-3333-3333-333333333333", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "duplicate"): dispatcher.load_routes(path)
        finally:
            shutil.rmtree(folder, ignore_errors=True)
            root = folder.parent
            if root.exists() and not any(root.iterdir()): root.rmdir()


if __name__ == "__main__": unittest.main()
