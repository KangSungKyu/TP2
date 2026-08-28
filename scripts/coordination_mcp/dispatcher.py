#!/usr/bin/env python3
import argparse
import json
import os
import re
import subprocess
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AGENTS = ROOT / "AGENTS.md"
MCP_URL = "http://127.0.0.1:8765/mcp"
UUID_RE = re.compile(r"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", re.I)
ROLE_RE = re.compile(r"^\* \*\*(.+?)\*\*\s*:\s*(.+)$")


def load_routes(path=AGENTS):
    routes, seen = {}, set()
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        match = ROLE_RE.match(line)
        if not match:
            continue
        ids = UUID_RE.findall(match.group(2))
        if len(ids) < 2:
            continue
        if any(value in seen for value in ids):
            raise ValueError(f"duplicate conversation id in {match.group(1)}")
        seen.update(ids)
        routes[match.group(1)] = {"antigravity": tuple(ids[:-1]), "codex": ids[-1]}
    if len(routes) != 8 or any(not value["antigravity"] or not value["codex"] for value in routes.values()):
        raise ValueError(f"AGENTS.md route mapping incomplete: expected 8 roles, got {len(routes)}")
    return routes


def allowed_ids(routes):
    return ({value["codex"] for value in routes.values()},
            {item for value in routes.values() for item in value["antigravity"]})


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
        "Return one JSON object with status, summary, and evidence.",
        "Do not modify files, Git, Unity, settings, sessions, or external state. Do not create sub-agents.",
    ))


def build_agy_args(conversation, prompt, executable="agy.exe"):
    return [executable, "--conversation", conversation, "--print", prompt, "--output-format", "json",
            "--sandbox", "--disable-slash-commands", "--print-timeout", "120s"]


def run_agy(args, timeout=150):
    try:
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=timeout, shell=False)
    except subprocess.TimeoutExpired as exc:
        raise RuntimeError("antigravity_timeout") from exc
    if result.returncode:
        detail = (result.stderr.strip() or result.stdout.strip())[:500]
        raise RuntimeError(f"antigravity_nonzero:{result.returncode}:{detail}")
    try:
        output = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise RuntimeError("antigravity_invalid_json") from exc
    if not isinstance(output, (dict, list)):
        raise RuntimeError("antigravity_invalid_json_type")
    return output


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
    codex_ids, antigravity_ids = allowed_ids(routes)
    if target not in antigravity_ids:
        raise ValueError("target conversation is not in AGENTS.md Antigravity allowlist")
    if not execute:
        sample = {"order_id": "dry-run", "revision": 1, "payload": {"objective": "Read-only status smoke",
            "allowed_files": ["AGENTS.md"], "forbidden_files": ["Assets"], "acceptance": ["Return current status"],
            "base_branch": "portfolio", "base_sha": "c051cdd"}}
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
    claim = client.call("claim_order", {"order_id": order["order_id"], "worker_id": "codex-dispatcher",
        "expected_version": order["version"], "lease_seconds": 300})
    output = run_agy(build_agy_args(target, build_prompt(order), executable))
    complete = client.call("complete_order", {"order_id": order["order_id"], "claim_token": claim["claim_token"],
        "expected_version": claim["order"]["version"], "state": "complete", "result": {"antigravity": output}})
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
