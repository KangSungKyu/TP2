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
        self.assertEqual((len(routes), len(codex), len(antigravity)), (8, 8, 1))
        target = "edb1a3dd-9480-440f-9d90-282e1ec134d4"
        self.assertIn(target, antigravity)
        with self.assertRaisesRegex(ValueError, "UI Conversation UUID"):
            dispatcher.validate_target(routes, "bbabc4a9-bfbf-441a-8dc2-3a2746748ce1")
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
            lines=[]
            for i in range(8):
                ui=f"00000000-0000-0000-0000-{i+1:012d}"; codex=f"10000000-0000-0000-0000-{i+1:012d}"
                lines.append(f"* **Role{i}** : {ui} / {codex}")
                if i < 2: lines.append("  * **Antigravity CLI trajectory (`cli`)** : `20000000-0000-0000-0000-000000000001`")
            path.write_text("\n".join(lines),encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "duplicate CLI trajectory"): dispatcher.load_routes(path)
            path.write_text("\n".join(line for line in lines if "CLI trajectory" not in line),encoding="utf-8")
            routes=dispatcher.load_routes(path)
            with self.assertRaisesRegex(ValueError, "no explicit"):
                dispatcher.validate_target(routes,"20000000-0000-0000-0000-000000000001")
        finally:
            shutil.rmtree(folder, ignore_errors=True)
            root = folder.parent
            if root.exists() and not any(root.iterdir()): root.rmdir()


if __name__ == "__main__": unittest.main()
