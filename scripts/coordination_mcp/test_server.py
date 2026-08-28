import json, shutil, sqlite3, sys, threading, unittest, urllib.request, uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import server as coordination_server

HOST, TOOLS = coordination_server.HOST, coordination_server.TOOLS


class CoordinationMcpTest(unittest.TestCase):
    token = "synthetic-test-token-123456789"
    def setUp(self):
        self.real_project_root = coordination_server.PROJECT_ROOT
        with self.assertRaisesRegex(ValueError, "outside the repository"):
            coordination_server.create_server(self.real_project_root/"rejected-test.sqlite3",self.token,0)
        self.test_root=Path(__file__).resolve().parent/".test_runtime"/uuid.uuid4().hex
        self.fake_project_root=self.test_root/"fake_project"; self.fake_project_root.mkdir(parents=True)
        self.db=self.test_root/"database"/"orders.sqlite3"
        coordination_server.PROJECT_ROOT=self.fake_project_root
        self.server=None; self.start()
    def tearDown(self):
        try:
            if self.server: self.stop()
        finally:
            coordination_server.PROJECT_ROOT=self.real_project_root
            shutil.rmtree(self.test_root,ignore_errors=True)
            runtime_root=self.test_root.parent
            if runtime_root.exists() and not any(runtime_root.iterdir()): runtime_root.rmdir()
    def start(self):
        self.server=coordination_server.create_server(self.db,self.token,0); self.thread=threading.Thread(target=self.server.serve_forever,daemon=True); self.thread.start(); self.url=f"http://{HOST}:{self.server.server_port}"
    def stop(self): self.server.shutdown(); self.server.server_close(); self.thread.join(2); self.server=None
    def request(self,method,params,role="codex"):
        body=json.dumps({"jsonrpc":"2.0","id":1,"method":method,"params":params}).encode(); req=urllib.request.Request(self.url+"/mcp",body,headers={"Content-Type":"application/json","Authorization":"Bearer "+self.token,"X-TP2-Role":role}); return json.loads(urllib.request.urlopen(req,timeout=3).read())
    def call(self,name,args,role="codex"): return self.request("tools/call",{"name":name,"arguments":args},role)
    @staticmethod
    def payload(files=None,limit=2):
        return {"source_conversation":"source:synthetic","target_conversation":"target:synthetic","objective":"Synthetic bounded coordination test","allowed_files":files or ["Assets/A.cs"],"forbidden_files":["Assets/B.cs"],"acceptance":["synthetic pass"],"base_branch":"codex/synthetic","base_sha":"c051cdd","recommended_max_files":limit,"max_revision":1,"ruleset_version":"test-v1","ruleset_hash":"a"*64}
    def test_full_contract(self):
        health=urllib.request.Request(self.url+"/health",headers={"Authorization":"Bearer "+self.token,"X-TP2-Role":"codex"}); self.assertEqual(json.loads(urllib.request.urlopen(health).read())["host"],HOST)
        self.assertEqual(self.request("initialize",{},"codex")["result"]["serverInfo"]["name"],"tp2-coordination"); self.assertEqual(self.request("initialize",{},"antigravity")["result"]["serverInfo"]["name"],"tp2-coordination")
        schemas=self.request("tools/list",{})["result"]["tools"]; self.assertEqual(tuple(x["name"] for x in schemas),TOOLS)
        for tool in schemas:
            self.assertFalse(tool["inputSchema"]["additionalProperties"]); self.assertTrue(tool["inputSchema"]["required"]); self.assertTrue(tool["inputSchema"]["properties"])
        submit={"order_id":"synthetic-1","idempotency_key":"idem-1","revision":1,"payload":self.payload()}
        self.assertFalse(self.call("submit_order",submit)["result"]["structuredContent"]["duplicate"]); self.assertTrue(self.call("submit_order",submit)["result"]["structuredContent"]["duplicate"])
        self.assertEqual(len(self.call("list_pending",{"target_conversation":"target:synthetic"})["result"]["structuredContent"]["orders"]),1)
        claim={"order_id":"synthetic-1","worker_id":"worker","expected_version":1,"lease_seconds":30}
        with ThreadPoolExecutor(max_workers=2) as pool: results=list(pool.map(lambda _:self.call("claim_order",claim),range(2)))
        success=[x for x in results if "result" in x]; self.assertEqual(len(success),1); token=success[0]["result"]["structuredContent"]["claim_token"]
        done={"order_id":"synthetic-1","claim_token":token,"expected_version":2,"state":"complete","result":{"ok":True}}
        self.assertFalse(self.call("complete_order",done)["result"]["structuredContent"]["duplicate"]); self.assertTrue(self.call("complete_order",done)["result"]["structuredContent"]["duplicate"])
        self.assertEqual(self.call("get_status",{"order_id":"synthetic-1"})["result"]["structuredContent"]["order"]["state"],"complete")
        warning=self.call("submit_order",{"order_id":"synthetic-2","idempotency_key":"idem-2","revision":1,"payload":self.payload(["Assets/A.cs","Assets/B.cs"],1)}); self.assertTrue(warning["result"]["structuredContent"]["scope_warning"])
        self.assertEqual(self.call("get_status",{"order_id":"synthetic-1","unknown":1})["error"]["data"]["type"],"invalid_arguments")
        third={"order_id":"synthetic-3","idempotency_key":"idem-3","revision":1,"payload":self.payload()}; self.call("submit_order",third)
        stale=self.call("claim_order",{"order_id":"synthetic-3","worker_id":"worker","expected_version":1,"lease_seconds":30})["result"]["structuredContent"]
        db=sqlite3.connect(self.db)
        try: db.execute("UPDATE orders SET lease_expires=0 WHERE order_id='synthetic-3'"); db.commit()
        finally: db.close()
        expired=self.call("complete_order",{"order_id":"synthetic-3","claim_token":stale["claim_token"],"expected_version":2,"state":"complete","result":{}}); self.assertEqual(expired["error"]["data"]["type"],"expired_lease")
        db=sqlite3.connect(self.db)
        try: self.assertEqual(db.execute("SELECT state,version,claim_token,lease_expires FROM orders WHERE order_id='synthetic-3'").fetchone(),("pending",3,None,None))
        finally: db.close()
        self.assertEqual(self.call("get_status",{"order_id":"synthetic-3"})["result"]["structuredContent"]["order"]["state"],"pending")
        fourth={"order_id":"synthetic-4","idempotency_key":"idem-4","revision":1,"payload":self.payload()}; self.call("submit_order",fourth)
        fourth_claim=self.call("claim_order",{"order_id":"synthetic-4","worker_id":"worker","expected_version":1,"lease_seconds":30})["result"]["structuredContent"]
        rejected=self.call("complete_order",{"order_id":"synthetic-4","claim_token":fourth_claim["claim_token"],"expected_version":2,"state":"complete","result":{"summary":"token=RAW_SECRET_MARKER_123456"}}); self.assertEqual(rejected["error"]["data"]["type"],"sensitive_result")
        db=sqlite3.connect(self.db)
        try: self.assertEqual(db.execute("SELECT state,result_json FROM orders WHERE order_id='synthetic-4'").fetchone(),("claimed",None))
        finally: db.close()
        self.stop(); self.start(); self.assertEqual(self.call("get_status",{"order_id":"synthetic-1"})["result"]["structuredContent"]["order"]["state"],"complete"); self.assertEqual(self.server.server_address[0],HOST)


if __name__=="__main__": unittest.main()
