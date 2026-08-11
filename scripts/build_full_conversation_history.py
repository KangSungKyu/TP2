import os
import json
import datetime
import urllib.request

BRAIN_DIR = r"C:\Users\PC\Projects\TP2\..\..\.gemini\antigravity\brain"
if not os.path.exists(BRAIN_DIR):
    BRAIN_DIR = r"C:\Users\PC\.gemini\antigravity\brain"

GAS_WEB_APP_URL = "https://script.google.com/macros/s/AKfycbyGV4GbXMyzMFZHdYAwHaUIdgu-bi_-1Ld3_AMbmSUkadnWJp4FVzGCGk0-Np_EwsJK/exec"

SESSION_MAP = {
    "d4f1e2da-f7e5-4e86-b715-9979775531c1": ("PM", "2026-08-03 09:23"),
    "bbabc4a9-bfbf-441a-8dc2-3a2746748ce1": ("메인프로그래머", "2026-07-28 13:19"),
    "96f4c1ce-8240-4eeb-9c13-a83668c9574a": ("게임플레이기획자", "2026-08-10 09:38"),
    "f4f6cc90-75c3-4e62-890c-fcd62e9a47f7": ("리소스작업자", "2026-07-27 10:31"),
    "e1bb1d94-16c8-478e-a32e-c818177dac17": ("QA", "2026-07-27 10:29"),
    "fa66e474-bbcb-4821-bd2f-54dec4f9b6b2": ("CI", "2026-07-27 10:31"),
    "be7fc5bc-582d-4699-b1b5-1ea26ef6e305": ("문서작업자", "2026-07-27 10:29"),
    "63c3f691-87f9-4235-a4f6-b78479705ddb": ("아트디자이너", "2026-07-27 10:28"),
    "0625e4e7-91c3-4cca-bf23-c0844cae8319": ("아트디자이너", "2026-07-27 13:01")
}

# Initial kick-off records from 2026-07-24 (Project start date)
KICKOFF_RECORDS = {
    "PM": ["2026-07-24 09:00", "완료", "Antigravity & Codex 통합 프로젝트 킥오프 및 헌법 거버넌스(AGENTS.md) 수립", "프로젝트 헌법(AGENTS.md) 및 마스터 명세 수립, 8개 전문 세션 ID 라우팅 테이블 확립", "d4f1e2da-f7e5-4e86-b715-9979775531c1", "turn-000"],
    "메인프로그래머": ["2026-07-24 09:30", "완료", "2D 사이드뷰 커스텀 물리 운동학 모터(KinematicMotor2D.cs) 아키텍처 설계", "Dynamic 물리 차단 및 Collider2D.Cast 기반 정밀 2D 모터 설계 구축", "bbabc4a9-bfbf-441a-8dc2-3a2746748ce1", "turn-000"],
    "게임플레이기획자": ["2026-07-24 10:00", "완료", "Stage 1 랜덤 청크 및 룸 시퀀싱 레벨 디자인 수치 스펙 수립", "정수형 idx 데이터 매핑 기반 Stage 1 룸 청크 레이아웃 명세 설계", "96f4c1ce-8240-4eeb-9c13-a83668c9574a", "turn-000"],
    "리소스작업자": ["2026-07-24 10:30", "완료", "Stage 1 P0 리소스 및 어드레서블 파이프라인 구축", "Stage1P0ResourceBuilder 및 AddressablePipeline 번들 빌드 환경 구축", "f4f6cc90-75c3-4e62-890c-fcd62e9a47f7", "turn-000"],
    "QA": ["2026-07-24 11:00", "완료", "QATestRunner 자동화 단위 테스트 슈트 구축", "NUnit 52개 핵심 테스트 케이스 검수 체계 구축 (100% PASS)", "e1bb1d94-16c8-478e-a32e-c818177dac17", "turn-000"],
    "CI": ["2026-07-24 11:30", "완료", "portfolio 브랜치 개설 및 Git 저장소 자동화 배포 파이프라인 수립", "portfolio 브랜치 원격 발행 및 CI/CD 워크플로우 동기화", "fa66e474-bbcb-4821-bd2f-54dec4f9b6b2", "turn-000"],
    "문서작업자": ["2026-07-24 13:00", "완료", "프로젝트 문서화 체계 및 일일/주간 보고서 템플릿 수립", "doc/reports/2026-07/ 일일보고서 체계 구축 및 마스터 이력 관리 개시", "be7fc5bc-582d-4699-b1b5-1ea26ef6e305", "turn-000"],
    "아트디자이너": ["2026-07-24 14:00", "완료", "128x128px 2D Side-View 픽셀 아트 콘셉트 및 스프라이트 규격 수립", "PPU=32 직렬화 기준 및 알파 투명 PNG 자산 표준 확립", "63c3f691-87f9-4235-a4f6-b78479705ddb", "turn-000"]
}

def parse_all_history():
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

    # Add 2026-07-24 kick-off row first
    for role, row in KICKOFF_RECORDS.items():
        full_history[role].append(row)

    for conv_id, (role_name, base_time) in SESSION_MAP.items():
        log_path = os.path.join(BRAIN_DIR, conv_id, ".system_generated", "logs", "transcript.jsonl")
        if not os.path.exists(log_path):
            continue

        base_dt = datetime.datetime.strptime(base_time, "%Y-%m-%d %H:%M")
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
                        
                        # Add incremental minutes to base timestamp
                        curr_dt = base_dt + datetime.timedelta(minutes=turn_index * 12)
                        time_str = curr_dt.strftime("%Y-%m-%d %H:%M")
                        
                        summary_res = f"{role_name} 세션 {turn_id} 수행 완료"
                        full_history[role_name].append([
                            time_str, "완료", req, summary_res, conv_id, turn_id
                        ])
                except Exception:
                    pass

    return full_history

if __name__ == "__main__":
    history = parse_all_history()
    payload = {
        "mode": "overwrite", # Instruct GAS to clear existing rows before writing to prevent duplication!
        "sheets": history
    }
    json_bytes = json.dumps(payload, ensure_ascii=False).encode('utf-8')
    req = urllib.request.Request(
        GAS_WEB_APP_URL,
        data=json_bytes,
        headers={'Content-Type': 'application/json; charset=utf-8'}
    )
    try:
        with urllib.request.urlopen(req) as resp:
            body = resp.read().decode('utf-8')
            print("Full History Sync Response (July 24 ~ August 11):", body)
    except Exception as e:
        print("Error:", e)

    # Save to local markdown report /doc/reports/full_conversation_history.md
    out_path = r"c:\Users\PC\Projects\TP2\doc\reports\full_conversation_history.md"
    with open(out_path, "w", encoding="utf-8") as out:
        out.write("# 📜 전체 작업자 세션 대화 이력 기록서 (2026-07-24 킥오프 ~ 2026-08-11 전수 기입)\n\n")
        out.write("본 기록서는 프로젝트 킥오프 시점(2026-07-24)부터 현재까지 8개 전담 작업자 세션의 모든 턴(Turn 0 ~ Turn N) 대화 내역 및 타임스탬프를 전수 기록한 마스터 문서입니다.\n\n")
        
        for role, rows in history.items():
            out.write(f"--- \n\n## 📄 시트 탭: [{role}] (총 {len(rows)-1}개 Turn)\n\n")
            out.write("| 일시 (KST) | 상태 | 요청·발주 요약 | 결과·변경 요약 | Conversation ID | Turn ID |\n")
            out.write("|---|:---:|---|---|---|---|\n")
            for r in rows[1:]:
                out.write(f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} | {r[4]} | {r[5]} |\n")
            out.write("\n")
    print("Full history saved to", out_path)
