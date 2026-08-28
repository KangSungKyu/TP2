#!/usr/bin/env python3
import argparse, hashlib, json, os, re, secrets, signal, sqlite3, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HOST, PORT = "127.0.0.1", 8765
ROLES = {"codex", "antigravity"}
TOOLS = ("submit_order", "list_pending", "claim_order", "complete_order", "get_status")
ID_RE = re.compile(r"^[A-Za-z0-9._:-]{1,128}$")
PROJECT_ROOT = Path(__file__).resolve().parents[2]
SECRET_KEY_RE = re.compile(r"(?i)(?:^|[_-])(authorization|password|secret|token|api[_-]?key)(?:$|[_-])")
SECRET_VALUE_RE = re.compile(r"(?i)(?:bearer\s+\S{12,}|sk-[A-Za-z0-9_-]{12,}|(?:password|secret|token|api[_-]?key)\s*[:=]\s*\S{8,})")
STRING = {"type":"string"}; UINT = {"type":"integer","minimum":1}
PAYLOAD_SCHEMA = {"type":"object","additionalProperties":False,"required":["source_conversation","target_conversation","objective","allowed_files","forbidden_files","acceptance","base_branch","base_sha","recommended_max_files","max_revision","ruleset_version","ruleset_hash"],"properties":{"source_conversation":STRING,"target_conversation":STRING,"objective":STRING,"allowed_files":{"type":"array","items":STRING,"minItems":1},"forbidden_files":{"type":"array","items":STRING},"acceptance":{"type":"array","items":STRING,"minItems":1},"base_branch":STRING,"base_sha":STRING,"recommended_max_files":UINT,"max_revision":{"type":"integer","const":1},"ruleset_version":STRING,"ruleset_hash":{"type":"string","pattern":"^[0-9a-f]{64}$"}}}
TOOL_SCHEMAS = {
    "submit_order":{"type":"object","additionalProperties":False,"required":["order_id","idempotency_key","revision","payload"],"properties":{"order_id":STRING,"idempotency_key":STRING,"revision":{"type":"integer","const":1},"payload":PAYLOAD_SCHEMA}},
    "list_pending":{"type":"object","additionalProperties":False,"required":["target_conversation"],"properties":{"target_conversation":STRING,"limit":{"type":"integer","minimum":1,"maximum":100}}},
    "claim_order":{"type":"object","additionalProperties":False,"required":["order_id","worker_id","expected_version","lease_seconds"],"properties":{"order_id":STRING,"worker_id":STRING,"expected_version":UINT,"lease_seconds":{"type":"number","minimum":1,"maximum":3600}}},
    "complete_order":{"type":"object","additionalProperties":False,"required":["order_id","claim_token","expected_version","state","result"],"properties":{"order_id":STRING,"claim_token":STRING,"expected_version":UINT,"state":{"type":"string","enum":["submitted","complete"]},"result":{"type":"object"}}},
    "get_status":{"type":"object","additionalProperties":False,"required":["order_id"],"properties":{"order_id":STRING}},
}


class RpcError(Exception):
    def __init__(self, code, kind, message):
        super().__init__(message); self.code, self.kind, self.message = code, kind, message


class ClosingConnection(sqlite3.Connection):
    def __exit__(self, exc_type, exc, traceback):
        try: return super().__exit__(exc_type, exc, traceback)
        finally: self.close()


def exact(value, keys, where):
    if not isinstance(value, dict): raise RpcError(-32602, "invalid_arguments", f"{where} must be an object")
    unknown, missing = set(value) - set(keys), set(keys) - set(value)
    if unknown or missing: raise RpcError(-32602, "invalid_arguments", f"{where}: missing={sorted(missing)}, unknown={sorted(unknown)}")


def identifier(value, name):
    if not isinstance(value, str) or not ID_RE.fullmatch(value): raise RpcError(-32602, "invalid_arguments", f"invalid {name}")
    return value


def path_list(value, name, empty=False):
    if not isinstance(value, list) or (not value and not empty) or len(value) > 64: raise RpcError(-32602, "invalid_arguments", f"invalid {name}")
    out = []
    for item in value:
        path = item.replace("\\", "/") if isinstance(item, str) else ""
        if not path or len(path) > 260 or path.startswith(("/", "~")) or ":" in path or ".." in path.split("/") or any(x in path for x in "*?[]"):
            raise RpcError(-32602, "invalid_arguments", f"{name} must contain bounded repo-relative paths")
        out.append(path)
    if len(out) != len(set(out)): raise RpcError(-32602, "invalid_arguments", f"duplicate {name}")
    return out


def validate_payload(p):
    keys = ("source_conversation", "target_conversation", "objective", "allowed_files", "forbidden_files", "acceptance", "base_branch", "base_sha", "recommended_max_files", "max_revision", "ruleset_version", "ruleset_hash")
    exact(p, keys, "payload")
    for name in ("source_conversation", "target_conversation"): identifier(p[name], name)
    if not isinstance(p["base_branch"], str) or not re.fullmatch(r"[A-Za-z0-9._/-]{1,200}", p["base_branch"]) or ".." in p["base_branch"].split("/"):
        raise RpcError(-32602, "invalid_arguments", "invalid base_branch")
    if not isinstance(p["base_sha"], str) or not re.fullmatch(r"[0-9a-fA-F]{7,64}", p["base_sha"]): raise RpcError(-32602, "invalid_arguments", "invalid base_sha")
    if not isinstance(p["objective"], str) or not p["objective"].strip() or len(p["objective"]) > 2000: raise RpcError(-32602, "invalid_arguments", "objective must be bounded")
    allowed = path_list(p["allowed_files"], "allowed_files"); path_list(p["forbidden_files"], "forbidden_files", True)
    if len({x.split("/", 1)[0] for x in allowed}) != 1: raise RpcError(-32602, "invalid_arguments", "allowed_files must be one repository domain")
    acceptance = p["acceptance"]
    if not isinstance(acceptance, list) or not 1 <= len(acceptance) <= 16 or any(not isinstance(x, str) or not x.strip() or len(x) > 500 for x in acceptance): raise RpcError(-32602, "invalid_arguments", "acceptance must contain 1..16 bounded assertions")
    if type(p["recommended_max_files"]) is not int or not 1 <= p["recommended_max_files"] <= 64: raise RpcError(-32602, "invalid_arguments", "invalid recommended_max_files")
    if p["max_revision"] != 1: raise RpcError(-32602, "invalid_arguments", "max_revision must be 1")
    identifier(p["ruleset_version"], "ruleset_version")
    if not isinstance(p["ruleset_hash"], str) or not re.fullmatch(r"[0-9a-f]{64}", p["ruleset_hash"]): raise RpcError(-32602, "invalid_arguments", "invalid ruleset_hash")
    return len(allowed) > p["recommended_max_files"]


def validate_result(result):
    raw = json.dumps(result, sort_keys=True, separators=(",", ":"))
    if len(raw.encode("utf-8")) > 32768: raise RpcError(-32602, "result_oversize", "result exceeds byte limit")
    def secret(value):
        if isinstance(value, dict): return any(SECRET_KEY_RE.search(str(k)) or secret(v) for k, v in value.items())
        if isinstance(value, list): return any(secret(v) for v in value)
        return isinstance(value, str) and SECRET_VALUE_RE.search(value)
    if secret(result): raise RpcError(-32602, "sensitive_result", "result contains sensitive material")
    return raw


class Store:
    def __init__(self, path):
        self.path = str(Path(path).resolve()); Path(self.path).parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db: db.executescript("""PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS orders(order_id TEXT PRIMARY KEY,idempotency_key TEXT UNIQUE NOT NULL,payload_json TEXT NOT NULL,payload_hash TEXT NOT NULL,target_conversation TEXT NOT NULL,state TEXT NOT NULL,version INTEGER NOT NULL,revision INTEGER NOT NULL,base_sha TEXT NOT NULL,claimed_by TEXT,claim_token TEXT,lease_expires REAL,result_json TEXT,created_at REAL NOT NULL,updated_at REAL NOT NULL,CHECK(state IN('pending','claimed','submitted','complete')));""")

    def connect(self):
        db = sqlite3.connect(self.path, timeout=5, factory=ClosingConnection); db.row_factory = sqlite3.Row; db.execute("PRAGMA busy_timeout=5000"); return db

    @staticmethod
    def recover(db, now):
        db.execute("UPDATE orders SET state='pending',version=version+1,claimed_by=NULL,claim_token=NULL,lease_expires=NULL,updated_at=? WHERE state='claimed' AND lease_expires<=?", (now, now))

    @staticmethod
    def public(row):
        d = dict(row); d["payload"] = json.loads(d.pop("payload_json")); raw = d.pop("result_json"); d["result"] = json.loads(raw) if raw else None
        d.pop("payload_hash"); d.pop("claim_token"); return d

    def submit(self, a):
        exact(a, ("order_id", "idempotency_key", "revision", "payload"), "submit_order")
        oid, idem = identifier(a["order_id"], "order_id"), identifier(a["idempotency_key"], "idempotency_key")
        if a["revision"] != 1: raise RpcError(-32602, "invalid_arguments", "revision must be 1")
        warning = validate_payload(a["payload"]); raw = json.dumps(a["payload"], sort_keys=True, separators=(",", ":")); digest = hashlib.sha256((oid+"\0"+raw).encode()).hexdigest(); now = time.time()
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE"); row = db.execute("SELECT * FROM orders WHERE idempotency_key=? OR order_id=?", (idem, oid)).fetchone()
            if row:
                if row["idempotency_key"] != idem or row["payload_hash"] != digest: raise RpcError(-32010, "duplicate_conflict", "duplicate has different content")
                return {"order": self.public(row), "duplicate": True, "scope_warning": warning}
            db.execute("INSERT INTO orders VALUES(?,?,?,?,?,'pending',1,1,?,NULL,NULL,NULL,NULL,?,?)", (oid, idem, raw, digest, a["payload"]["target_conversation"], a["payload"]["base_sha"], now, now)); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
        return {"order": self.public(row), "duplicate": False, "scope_warning": warning}

    def pending(self, a):
        if not isinstance(a, dict) or set(a)-{"target_conversation","limit"} or "target_conversation" not in a: raise RpcError(-32602, "invalid_arguments", "target_conversation and optional limit required")
        target, limit = identifier(a["target_conversation"], "target_conversation"), a.get("limit", 50)
        if type(limit) is not int or not 1 <= limit <= 100: raise RpcError(-32602, "invalid_arguments", "limit must be 1..100")
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE"); self.recover(db, time.time()); rows = db.execute("SELECT * FROM orders WHERE state='pending' AND target_conversation=? ORDER BY created_at LIMIT ?", (target, limit)).fetchall()
        return {"orders": [self.public(x) for x in rows]}

    def claim(self, a):
        exact(a, ("order_id", "worker_id", "expected_version", "lease_seconds"), "claim_order"); oid, worker = identifier(a["order_id"], "order_id"), identifier(a["worker_id"], "worker_id"); version, lease = a["expected_version"], a["lease_seconds"]
        if type(version) is not int or version < 1 or not isinstance(lease, (int,float)) or not 1 <= lease <= 3600: raise RpcError(-32602, "invalid_arguments", "invalid version or lease")
        now, token = time.time(), secrets.token_urlsafe(24)
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE"); self.recover(db, now); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
            if not row: raise RpcError(-32004, "not_found", "order not found")
            if row["version"] != version: raise RpcError(-32011, "version_conflict", f"current version {row['version']}")
            if row["state"] != "pending": raise RpcError(-32012, "invalid_state", f"cannot claim {row['state']}")
            db.execute("UPDATE orders SET state='claimed',version=version+1,claimed_by=?,claim_token=?,lease_expires=?,updated_at=? WHERE order_id=?", (worker, token, now+lease, now, oid)); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
        return {"order": self.public(row), "claim_token": token}

    def complete(self, a):
        exact(a, ("order_id", "claim_token", "expected_version", "state", "result"), "complete_order"); oid, token = identifier(a["order_id"], "order_id"), identifier(a["claim_token"], "claim_token"); version, state, result = a["expected_version"], a["state"], a["result"]
        if type(version) is not int or version < 1 or state not in {"submitted","complete"} or not isinstance(result, dict): raise RpcError(-32602, "invalid_arguments", "invalid completion")
        raw, now = validate_result(result), time.time()
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE"); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
            if not row: raise RpcError(-32004, "not_found", "order not found")
            if row["state"] in {"submitted","complete"}:
                if row["claim_token"] == token and row["state"] == state and row["result_json"] == raw: return {"order": self.public(row), "duplicate": True}
                raise RpcError(-32010, "duplicate_conflict", "completion differs")
            if row["state"] != "claimed": raise RpcError(-32012, "invalid_state", f"cannot complete {row['state']}")
            if row["lease_expires"] <= now:
                self.recover(db, now); db.commit()
                raise RpcError(-32013, "expired_lease", "claim expired")
            if row["claim_token"] != token: raise RpcError(-32014, "invalid_claim_token", "claim token mismatch")
            if row["version"] != version: raise RpcError(-32011, "version_conflict", f"current version {row['version']}")
            db.execute("UPDATE orders SET state=?,version=version+1,result_json=?,updated_at=? WHERE order_id=?", (state, raw, now, oid)); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
        return {"order": self.public(row), "duplicate": False}

    def status(self, a):
        exact(a, ("order_id",), "get_status"); oid = identifier(a["order_id"], "order_id")
        with self.connect() as db: db.execute("BEGIN IMMEDIATE"); self.recover(db, time.time()); row = db.execute("SELECT * FROM orders WHERE order_id=?", (oid,)).fetchone()
        if not row: raise RpcError(-32004, "not_found", "order not found")
        return {"order": self.public(row)}


def handler(store, token):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_): pass
        def auth(self): return self.client_address[0] in {"127.0.0.1","::1"} and self.headers.get("X-TP2-Role") in ROLES and secrets.compare_digest(self.headers.get("Authorization", ""), "Bearer "+token)
        def send(self, status, body):
            raw=json.dumps(body,separators=(",",":")).encode(); self.send_response(status); self.send_header("Content-Type","application/json"); self.send_header("Content-Length",str(len(raw))); self.end_headers(); self.wfile.write(raw)
        def do_GET(self):
            if self.path != "/health": return self.send(404,{"error":"not_found"})
            return self.send(200,{"ok":True,"host":HOST}) if self.auth() else self.send(401,{"error":"unauthorized"})
        def do_POST(self):
            if self.path != "/mcp": return self.send(404,{"error":"not_found"})
            if not self.auth(): return self.send(401,{"error":"unauthorized"})
            request = {}
            try:
                length=int(self.headers.get("Content-Length","0"));
                if not 0 < length <= 1_000_000: raise RpcError(-32700,"parse_error","invalid content length")
                request=json.loads(self.rfile.read(length))
                if set(request)=={"jsonrpc","method","params"} and request.get("jsonrpc")=="2.0" and request.get("method")=="notifications/initialized":
                    return self.send(202,{})
                exact(request,("jsonrpc","id","method","params"),"request")
                if request["jsonrpc"]!="2.0" or not isinstance(request["params"],dict): raise RpcError(-32600,"invalid_request","invalid JSON-RPC envelope")
                method, params=request["method"],request["params"]
                if method=="initialize": result={"protocolVersion":"2025-03-26","capabilities":{"tools":{}},"serverInfo":{"name":"tp2-coordination","version":"1"}}
                elif method=="tools/list": result={"tools":[{"name":x,"description":x.replace("_"," "),"inputSchema":TOOL_SCHEMAS[x]} for x in TOOLS]}
                elif method=="tools/call":
                    exact(params,("name","arguments"),"tools/call"); name,args=params["name"],params["arguments"]
                    if name not in TOOLS or not isinstance(args,dict): raise RpcError(-32601,"unknown_tool",f"unknown tool {name}")
                    value={"submit_order":store.submit,"list_pending":store.pending,"claim_order":store.claim,"complete_order":store.complete,"get_status":store.status}[name](args); result={"content":[{"type":"text","text":json.dumps(value,separators=(",",":"))}],"structuredContent":value}
                else: raise RpcError(-32601,"method_not_found",f"unknown method {method}")
                self.send(200,{"jsonrpc":"2.0","id":request["id"],"result":result})
            except RpcError as e: self.send(200,{"jsonrpc":"2.0","id":request.get("id"),"error":{"code":e.code,"message":e.message,"data":{"type":e.kind}}})
            except (ValueError,TypeError,json.JSONDecodeError) as e: self.send(200,{"jsonrpc":"2.0","id":None,"error":{"code":-32700,"message":str(e),"data":{"type":"parse_error"}}})
            except Exception: self.send(500,{"error":"internal_error"})
    return Handler


def create_server(db_path, token, port=PORT):
    if not isinstance(token,str) or len(token)<24: raise ValueError("TP2_COORDINATION_TOKEN must contain at least 24 characters")
    db=Path(db_path).resolve()
    if db==PROJECT_ROOT or PROJECT_ROOT in db.parents: raise ValueError("coordination DB must be outside the repository")
    return ThreadingHTTPServer((HOST,port),handler(Store(db),token))


def main():
    default=Path(os.environ.get("LOCALAPPDATA",Path.home()))/"TP2"/"coordination_mcp"/"orders.sqlite3"; parser=argparse.ArgumentParser(); parser.add_argument("--port",type=int,default=PORT); parser.add_argument("--db",default=os.environ.get("TP2_COORDINATION_DB",str(default))); args=parser.parse_args(); server=create_server(args.db,os.environ.get("TP2_COORDINATION_TOKEN",""),args.port)
    def stop(*_): threading.Thread(target=server.shutdown,daemon=True).start()
    signal.signal(signal.SIGINT,stop); signal.signal(signal.SIGTERM,stop)
    try: server.serve_forever()
    finally: server.server_close()


if __name__ == "__main__": main()
