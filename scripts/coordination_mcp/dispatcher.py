#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import re
import subprocess
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AGENTS = ROOT / "AGENTS.md"
MCP_URL = "http://127.0.0.1:8765/mcp"
RULESET = ROOT / "doc" / "ai_order" / "rules" / "main_programmer.md"
UUID_RE = re.compile(r"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", re.I)
ROLE_RE = re.compile(r"^\* \*\*(.+?)\*\*\s*:\s*(.+)$")
CLI_RE = re.compile(r"^\s+\* \*\*Antigravity CLI trajectory \(`[^`]+`\)\*\*\s*:\s*`(" + UUID_RE.pattern[2:-2] + r")`")
WORKER_KEYS = {"status", "summary", "evidence", "applied_ruleset_version", "applied_ruleset_hash"}
SECRET_KEY_RE = re.compile(r"(?i)(?:^|[_-])(authorization|password|secret|token|api[_-]?key)(?:$|[_-])")
SECRET_VALUE_RE = re.compile(r"(?i)(?:bearer\s+\S{12,}|sk-[A-Za-z0-9_-]{12,}|(?:password|secret|token|api[_-]?key)\s*[:=]\s*\S{8,})")
MAX_AGY_STDOUT_BYTES, MAX_WORKER_RESULT_BYTES = 65536, 32768


def load_routes(path=AGENTS):
    routes, seen, current = {}, set(), None
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        match = ROLE_RE.match(line)
        if match:
            ids = UUID_RE.findall(match.group(2))
            if len(ids) < 2:
                continue
            if any(value in seen for value in ids):
                raise ValueError(f"duplicate conversation id in {match.group(1)}")
            seen.update(ids)
            current = match.group(1)
            routes[current] = {"antigravity_ui": tuple(ids[:-1]), "codex": ids[-1], "cli_trajectory": None}
            continue
        cli = CLI_RE.match(line)
        if cli and current:
            value = cli.group(1)
            if routes[current]["cli_trajectory"] is not None:
                raise ValueError(f"multiple CLI trajectories in {current}")
            if any(route["cli_trajectory"] == value for route in routes.values()):
                raise ValueError(f"duplicate CLI trajectory {value}")
            routes[current]["cli_trajectory"] = value
    if len(routes) != 8 or any(not value["antigravity_ui"] or not value["codex"] for value in routes.values()):
        raise ValueError(f"AGENTS.md route mapping incomplete: expected 8 roles, got {len(routes)}")
    return routes


def allowed_ids(routes):
    return ({value["codex"] for value in routes.values()},
            {value["cli_trajectory"] for value in routes.values() if value["cli_trajectory"]})


def validate_target(routes, target):
    ui_ids = {item for value in routes.values() for item in value["antigravity_ui"]}
    cli_ids = allowed_ids(routes)[1]
    if target in ui_ids:
        raise ValueError("UI Conversation UUID is not an Antigravity CLI trajectory")
    if target not in cli_ids:
        raise ValueError("target has no explicit AGENTS.md Antigravity CLI trajectory")


def load_ruleset(path=RULESET):
    raw = Path(path).read_bytes()
    match = re.search(rb"(?m)^ruleset_version:\s*([A-Za-z0-9._-]+)\s*$", raw)
    if not match:
        raise ValueError("ruleset_version missing")
    return match.group(1).decode("ascii"), hashlib.sha256(raw).hexdigest()


def build_prompt(order):
    payload = order["payload"]
    return "\n".join((
        "TP2 Coordination MCP read-only order.",
        f"Order: {order['order_id']} revision {order['revision']}",
        f"Objective: {payload['objective']}",
        f"Allowed files (read-only scope): {json.dumps(payload['allowed_files'], ensure_ascii=False)}",
        f"Forbidden files: {json.dumps(payload['forbidden_files'], ensure_ascii=False)}",
        f"Acceptance: {json.dumps(payload['acceptance'], ensure_ascii=False)}",
        f"Base: {payload['base_branch']} @ {payload['base_sha']}",
        f"Ruleset: {payload['ruleset_version']} sha256:{payload['ruleset_hash']}",
        "Return one JSON object with status, summary, evidence, applied_ruleset_version, and applied_ruleset_hash.",
        "Do not modify files, Git, Unity, settings, sessions, or external state. Do not create sub-agents.",
    ))


def build_agy_args(conversation, prompt, executable="agy.exe"):
    return [executable, "--conversation", conversation, "--print", prompt, "--output-format", "json",
            "--sandbox", "--disable-slash-commands", "--print-timeout", "120s"]


def validate_worker(worker, ruleset_version, ruleset_hash):
    if not isinstance(worker, dict) or set(worker) != WORKER_KEYS:
        raise RuntimeError("antigravity_invalid_worker_schema")
    status = worker["status"]
    if status != "SUCCESS":
        raise RuntimeError("antigravity_worker_" + status.lower() if status in {"BLOCKED", "FAILED"} else "antigravity_invalid_worker_status")
    if not isinstance(worker["summary"], str) or not worker["summary"].strip() or len(worker["summary"]) > 4000 or not isinstance(worker["evidence"], list) or len(worker["evidence"]) > 64:
        raise RuntimeError("antigravity_invalid_worker_schema")
    raw = json.dumps(worker, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    if len(raw) > MAX_WORKER_RESULT_BYTES:
        raise RuntimeError("antigravity_worker_result_oversize")
    def secret(value):
        if isinstance(value, dict): return any(SECRET_KEY_RE.search(str(k)) or secret(v) for k, v in value.items())
        if isinstance(value, list): return any(secret(v) for v in value)
        return isinstance(value, str) and SECRET_VALUE_RE.search(value)
    if secret(worker):
        raise RuntimeError("antigravity_sensitive_result")
    if worker["applied_ruleset_version"] != ruleset_version or worker["applied_ruleset_hash"] != ruleset_hash:
        raise RuntimeError("antigravity_ruleset_mismatch")
    return worker


def run_agy(args, ruleset_version, ruleset_hash, timeout=150):
    try:
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=False, timeout=timeout, shell=False)
    except subprocess.TimeoutExpired as exc:
        raise RuntimeError("antigravity_timeout") from exc
    if result.returncode:
        raise RuntimeError(f"antigravity_nonzero_{result.returncode}")
    if not isinstance(result.stdout, bytes) or len(result.stdout) > MAX_AGY_STDOUT_BYTES:
        raise RuntimeError("antigravity_stdout_oversize")
    try:
        stdout = result.stdout.decode("utf-8", "strict")
        output = json.loads(stdout)
    except UnicodeDecodeError as exc:
        raise RuntimeError("antigravity_decode_error") from exc
    except (json.JSONDecodeError, TypeError) as exc:
        raise RuntimeError("antigravity_invalid_json") from exc
    if not isinstance(output, dict) or output.get("status") != "SUCCESS":
        raise RuntimeError("antigravity_non_success")
    response = output.get("response")
    if not isinstance(response, str) or not response.strip():
        raise RuntimeError("antigravity_empty_response")
    text = response.strip()
    if text.startswith("```") and text.endswith("```"):
        text = re.sub(r"^```(?:json)?\s*|\s*```$", "", text, flags=re.I)
    try:
        worker = json.loads(text)
    except json.JSONDecodeError as exc:
        raise RuntimeError("antigravity_invalid_worker_json") from exc
    return validate_worker(worker, ruleset_version, ruleset_hash)


class Client:
    def __init__(self, token, url=MCP_URL):
        if len(token) < 24:
            raise ValueError("TP2_COORDINATION_TOKEN must contain at least 24 characters")
        self.token, self.url, self.request_id = token, url, 0

    def call(self, name, arguments):
        self.request_id += 1
        body = json.dumps({"jsonrpc": "2.0", "id": self.request_id, "method": "tools/call",
                           "params": {"name": name, "arguments": arguments}}).encode()
        request = urllib.request.Request(self.url, body, headers={"Content-Type": "application/json",
            "Authorization": "Bearer " + self.token, "X-TP2-Role": "codex"})
        response = json.loads(urllib.request.urlopen(request, timeout=10).read())
        if "error" in response:
            raise RuntimeError(f"mcp_{response['error']['data']['type']}:{response['error']['message']}")
        return response["result"]["structuredContent"]


def dispatch(target, execute=False, executable="agy.exe"):
    routes = load_routes()
    ruleset_version, ruleset_hash = load_ruleset()
    codex_ids, antigravity_ids = allowed_ids(routes)
    validate_target(routes, target)
    if not execute:
        sample = {"order_id": "dry-run", "revision": 1, "payload": {"objective": "Read-only status smoke",
            "allowed_files": ["AGENTS.md"], "forbidden_files": ["Assets"], "acceptance": ["Return current status"],
            "base_branch": "portfolio", "base_sha": "c051cdd", "ruleset_version": ruleset_version,
            "ruleset_hash": ruleset_hash}}
        return {"dry_run": True, "args": build_agy_args(target, build_prompt(sample), executable),
                "codex_ids": len(codex_ids), "antigravity_ids": len(antigravity_ids)}
    client = Client(os.environ.get("TP2_COORDINATION_TOKEN", ""))
    pending = client.call("list_pending", {"target_conversation": target, "limit": 1})["orders"]
    if not pending:
        return {"dispatched": False, "reason": "no_pending_order"}
    order = pending[0]
    payload = order["payload"]
    if payload["source_conversation"] not in codex_ids or payload["target_conversation"] != target:
        raise ValueError("order route is outside AGENTS.md allowlist")
    if payload.get("ruleset_version") != ruleset_version or payload.get("ruleset_hash") != ruleset_hash:
        raise RuntimeError("order_ruleset_mismatch")
    claim = client.call("claim_order", {"order_id": order["order_id"], "worker_id": "codex-dispatcher",
        "expected_version": order["version"], "lease_seconds": 300})
    output = run_agy(build_agy_args(target, build_prompt(order), executable), ruleset_version, ruleset_hash)
    complete = client.call("complete_order", {"order_id": order["order_id"], "claim_token": claim["claim_token"],
        "expected_version": claim["order"]["version"], "state": "complete", "result": {"worker_result": output}})
    return {"dispatched": True, "order": complete["order"]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", required=True)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--agy", default="agy.exe")
    args = parser.parse_args()
    print(json.dumps(dispatch(args.target, args.execute, args.agy), ensure_ascii=False))


if __name__ == "__main__":
    main()
