import os
import json
import sys

sys.path.append(r"c:\Users\PC\Projects\TP2\scripts")
import build_full_conversation_history

def export_markdown():
    history = build_full_conversation_history.parse_transcripts()
    out_path = r"c:\Users\PC\Projects\TP2\doc\reports\full_conversation_history.md"
    
    with open(out_path, "w", encoding="utf-8") as out:
        out.write("# 📜 전체 작업자 세션 대화 이력 기록서 (처음부터 끝까지 - 전수 기입)\n\n")
        out.write("본 기록서는 프로젝트 시작 시점부터 현재까지 8개 전담 작업자 세션의 모든 턴(Turn 1 ~ Turn N) 대화 내역 및 처리 결과를 전수 기록한 마스터 문서입니다.\n\n")
        
        for role, rows in history.items():
            out.write(f"--- \n\n## 📄 시트 탭: [{role}] (총 {len(rows)-1}개 Turn)\n\n")
            out.write("| 일시 (KST) | 상태 | 요청·발주 요약 | 결과·변경 요약 | Conversation ID | Turn ID |\n")
            out.write("|---|:---:|---|---|---|---|\n")
            for r in rows[1:]:
                out.write(f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} | {r[4]} | {r[5]} |\n")
            out.write("\n")

    print("Successfully exported full conversation history markdown to", out_path)

if __name__ == "__main__":
    export_markdown()
