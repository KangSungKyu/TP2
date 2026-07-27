import subprocess

try:
    output = subprocess.check_output("tasklist", shell=True).decode('cp949', errors='ignore')
    is_running = "Unity.exe" in output
    print(f"UNITY_RUNNING={is_running}")
except Exception as e:
    print(f"ERROR: {e}")
