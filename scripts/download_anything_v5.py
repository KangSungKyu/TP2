import urllib.request
import os
import sys
import time

URL = "https://huggingface.co/swl-models/Anything-v5.0-PRT/resolve/main/Anything-v5.0-PRT-RE.safetensors"
DEST_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\models\checkpoints"
DEST_FILE = os.path.join(DEST_DIR, "Anything-v5.0-PRT-RE.safetensors")

def download():
    os.makedirs(DEST_DIR, exist_ok=True)
    if os.path.exists(DEST_FILE) and os.path.getsize(DEST_FILE) > 2000000000:
        print(f"File already downloaded: {DEST_FILE} ({os.path.getsize(DEST_FILE):,} bytes)")
        return

    print(f"Starting download: {URL}")
    print(f"Target location: {DEST_FILE}")

    start_time = time.time()
    last_print = 0

    def reporthook(count, block_size, total_size):
        nonlocal last_print
        now = time.time()
        if now - last_print >= 3.0 or count * block_size >= total_size:
            last_print = now
            downloaded = count * block_size
            pct = (downloaded / total_size) * 100 if total_size > 0 else 0
            mb = downloaded / (1024 * 1024)
            total_mb = total_size / (1024 * 1024) if total_size > 0 else 0
            elapsed = now - start_time
            speed = mb / elapsed if elapsed > 0 else 0
            print(f"Progress: {mb:.1f} MB / {total_mb:.1f} MB ({pct:.1f}%) @ {speed:.2f} MB/s", flush=True)

    opener = urllib.request.build_opener()
    opener.addheaders = [('User-Agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)')]
    urllib.request.install_opener(opener)

    urllib.request.urlretrieve(URL, DEST_FILE, reporthook)
    print(f"\nDownload completed successfully! Saved to: {DEST_FILE}")

if __name__ == "__main__":
    download()
