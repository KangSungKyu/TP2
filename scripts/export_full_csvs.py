import os
import csv
import sys

sys.path.append(r"c:\Users\PC\Projects\TP2\scripts")
import build_full_conversation_history

def export_csvs():
    history = build_full_conversation_history.parse_all_history()
    csv_dir = r"c:\Users\PC\Projects\TP2\doc\reports\csv_exports"
    os.makedirs(csv_dir, exist_ok=True)

    for role, rows in history.items():
        csv_path = os.path.join(csv_dir, f"{role}.csv")
        with open(csv_path, "w", encoding="utf-8-sig", newline="") as f:
            writer = csv.writer(f)
            writer.writerows(rows)
        print(f"Exported {role}.csv with {len(rows)-1} turns")

if __name__ == "__main__":
    export_csvs()
