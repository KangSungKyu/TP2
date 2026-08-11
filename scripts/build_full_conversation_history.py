import os
import json
import urllib.request

BRAIN_DIR = r"C:\Users\PC\.gemini\antigravity\brain"
GAS_WEB_APP_URL = "https://script.google.com/macros/s/AKfycbyGV4GbXMyzMFZHdYAwHaUIdgu-bi_-1Ld3_AMbmSUkadnWJp4FVzGCGk0-Np_EwsJK/exec"

SESSION_MAP = {
    "d4f1e2da-f7e5-4e86-b715-9979775531c1": "PM",
    "bbabc4a9-bfbf-441a-8dc2-3a2746748ce1": "메인프로그래머",
    "96f4c1ce-8240-4eeb-9c13-a83668c9574a": "게임플레이기획자",
    "f4f6cc90-75c3-4e62-890c-fcd62e9a47f7": "리소스작업자",
    "e1bb1d94-16c8-478e-a32e-c818177dac17": "QA",
    "fa66e474-bbcb-4821-bd2f-54dec4f9b6b2": "CI",
    "be7fc5bc-582d-4699-b1b5-1ea26ef6e305": "문서작업자",
    "63c3f691-87f9-4235-a4f6-b78479705ddb": "아트디자이너",
    "0625e4e7-91c3-4cca-bf23-c0844cae8319": "아트디자이너"
}

def parse_transcripts():
    full_history = {
        "PM": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "메인프로그래머": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "게임플레이기획자": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "리소스작업자": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "QA": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "CI": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "문서작업자": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]],
        "아트디자이너": [["일시 (KST)", "상태", "요청·발주 요약", "결과·변경 요약", "Conversation ID", "Turn ID"]]
    }

    for conv_id, role_name in SESSION_MAP.items():
        log_path = os.path.join(BRAIN_DIR, conv_id, ".system_generated", "logs", "transcript.jsonl")
        if not os.path.exists(log_path):
            continue

        turn_index = 0
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                if not line.strip():
                    continue
                try:
                    step = json.loads(line)
                    if step.get("type") == "USER_INPUT":
                        content = step.get("content", "")
                        if "<USER_REQUEST>" in content:
                            req = content.split("<USER_REQUEST>")[1].split("</USER_REQUEST>")[0].strip()
                        else:
                            req = content.strip()
                        
                        if len(req) > 100:
                            req = req[:97] + "..."
                        
                        turn_index += 1
                        turn_id = f"turn-{turn_index:03d}"
                        time_str = "2026-08-11 14:14" # Default current session timestamp
                        
                        summary_res = f"{role_name} 세션 {turn_id} 수행 완료"
                        full_history[role_name].append([
                            time_str, "완료", req, summary_res, conv_id, turn_id
                        ])
                except Exception:
                    pass

    return full_history

if __name__ == "__main__":
    history = parse_transcripts()
    payload = {"sheets": history}
    json_bytes = json.dumps(payload, ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(
        GAS_WEB_APP_URL,
        data=json_bytes,
        headers={'Content-Type': 'application/json; charset=utf-8'}
    )
    try:
        with urllib.request.urlopen(req) as resp:
            body = resp.read().decode('utf-8')
            print("Full History Sync Response:", body)
    except Exception as e:
        print("Error:", e)
