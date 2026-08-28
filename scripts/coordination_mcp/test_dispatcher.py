import json
import os
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
        version, digest = dispatcher.load_ruleset()
        worker = {"status":"SUCCESS","summary":"reviewed","evidence":[],"applied_ruleset_version":version,"applied_ruleset_hash":digest}
        success = lambda value: json.dumps({"status":"SUCCESS","response":value},ensure_ascii=False).encode("utf-8")
        self.assertLessEqual(len(dispatcher.RULESET.read_text(encoding="utf-8").splitlines()),100)
        self.assertEqual((version,digest),dispatcher.load_ruleset())
        with patch("dispatcher.subprocess.run", side_effect=subprocess.TimeoutExpired(args, 1)):
            with self.assertRaisesRegex(RuntimeError, "antigravity_timeout"): dispatcher.run_agy(args,version,digest)
        marker=b"RAW_SECRET_MARKER"
        with patch("dispatcher.subprocess.run", return_value=subprocess.CompletedProcess(args,2,marker,marker)):
            with self.assertRaisesRegex(RuntimeError,"antigravity_nonzero_2") as error: dispatcher.run_agy(args,version,digest)
        self.assertNotIn(marker.decode(),str(error.exception))
        failures = (
            (b"\xff", "antigravity_decode_error"),
            (b"not-json", "antigravity_invalid_json"),
            (success(""), "antigravity_empty_response"),
            (json.dumps({"status":"FAILED","response":"x"}).encode(), "antigravity_non_success"),
            (success("not-json"), "antigravity_invalid_worker_json"),
            (success(json.dumps({**worker,"status":"BLOCKED"})), "antigravity_worker_blocked"),
            (success(json.dumps({**worker,"status":"FAILED"})), "antigravity_worker_failed"),
            (success(json.dumps({**worker,"status":"UNKNOWN"})), "antigravity_invalid_worker_status"),
            (success(json.dumps({**worker,"extra":True})), "antigravity_invalid_worker_schema"),
            (success(json.dumps({**worker,"evidence":["token=RAW_SECRET_MARKER_123456"]})), "antigravity_sensitive_result"),
            (success(json.dumps({**worker,"evidence":["x"*33000]})), "antigravity_worker_result_oversize"),
            (success(json.dumps({**worker,"applied_ruleset_hash":"b"*64})), "antigravity_ruleset_mismatch"),
            (success(json.dumps({k:v for k,v in worker.items() if k!="applied_ruleset_version"})), "antigravity_invalid_worker_schema"),
        )
        for stdout, category in failures:
            with self.subTest(category=category), patch("dispatcher.subprocess.run",return_value=subprocess.CompletedProcess(args,0,stdout,b"")):
                with self.assertRaisesRegex(RuntimeError,category): dispatcher.run_agy(args,version,digest)
        with patch.dict(os.environ,{"PYTHONIOENCODING":"cp949"}), patch("dispatcher.subprocess.run",return_value=subprocess.CompletedProcess(args,0,success(json.dumps(worker,ensure_ascii=False)),b"")):
            self.assertEqual(dispatcher.run_agy(args,version,digest),worker)

    def test_dispatch_completes_only_valid_worker_result(self):
        routes=dispatcher.load_routes(); source=next(iter(dispatcher.allowed_ids(routes)[0])); target=next(iter(dispatcher.allowed_ids(routes)[1])); version,digest=dispatcher.load_ruleset()
        order={"order_id":"synthetic","revision":1,"version":1,"payload":{"source_conversation":source,"target_conversation":target,"objective":"review","allowed_files":["scripts/coordination_mcp/dispatcher.py"],"forbidden_files":[],"acceptance":["report"],"base_branch":"portfolio","base_sha":"c051cdd","ruleset_version":version,"ruleset_hash":digest}}
        worker={"status":"SUCCESS","summary":"ok","evidence":[],"applied_ruleset_version":version,"applied_ruleset_hash":digest}
        class FakeClient:
            def __init__(self): self.calls=[]
            def call(self,name,args):
                self.calls.append((name,args))
                if name=="list_pending": return {"orders":[order]}
                if name=="claim_order": return {"claim_token":"claim","order":{"version":2}}
                if name=="complete_order": return {"order":{"state":"complete"}}
        for category in ("antigravity_empty_response","antigravity_worker_blocked","antigravity_worker_failed","antigravity_invalid_worker_status","antigravity_worker_result_oversize","antigravity_invalid_worker_schema","antigravity_sensitive_result"):
            failed=FakeClient()
            with self.subTest(category=category), patch("dispatcher.Client",return_value=failed), patch("dispatcher.run_agy",side_effect=RuntimeError(category)):
                with self.assertRaisesRegex(RuntimeError,category): dispatcher.dispatch(target,True)
            self.assertNotIn("complete_order",[name for name,_ in failed.calls])
        passed=FakeClient()
        with patch("dispatcher.Client",return_value=passed), patch("dispatcher.run_agy",return_value=worker): self.assertTrue(dispatcher.dispatch(target,True)["dispatched"])
        completions=[args for name,args in passed.calls if name=="complete_order"]
        self.assertEqual(len(completions),1); self.assertEqual(completions[0]["result"],{"worker_result":worker})

    def test_run_script_is_repo_relative(self):
        text=(dispatcher.ROOT/"scripts"/"coordination_mcp"/"run.ps1").read_text(encoding="utf-8")
        self.assertIn("$PSScriptRoot",text); self.assertIn(".venv",text); self.assertNotIn("C:\\Users\\",text)

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
