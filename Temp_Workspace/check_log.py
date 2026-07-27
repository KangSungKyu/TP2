import os, time

log_path = r"c:\Users\PC\Projects\TP2\Logs\FullPipelineBatch.log"
print(f"Waiting for log file: {log_path}")

for _ in range(12):
    if os.path.exists(log_path):
        print("Log file created! Reading tail...")
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
            for line in lines[-25:]:
                print(line.strip())
        break
    time.sleep(3)
