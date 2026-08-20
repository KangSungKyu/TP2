import os
import urllib.request
import time
import sys

BASE_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\models"

MODELS_TO_DOWNLOAD = [
    {
        "name": "control_v11p_sd15_openpose_fp16.safetensors",
        "url": "https://huggingface.co/comfyanonymous/ControlNet-v1-1_fp16_safetensors/resolve/main/control_v11p_sd15_openpose_fp16.safetensors",
        "dest_dir": os.path.join(BASE_DIR, "controlnet")
    },
    {
        "name": "control_v11p_sd15_lineart_fp16.safetensors",
        "url": "https://huggingface.co/comfyanonymous/ControlNet-v1-1_fp16_safetensors/resolve/main/control_v11p_sd15_lineart_fp16.safetensors",
        "dest_dir": os.path.join(BASE_DIR, "controlnet")
    },
    {
        "name": "ip-adapter_sd15.safetensors",
        "url": "https://huggingface.co/h94/IP-Adapter/resolve/main/models/ip-adapter_sd15.safetensors",
        "dest_dir": os.path.join(BASE_DIR, "ipadapter")
    },
    {
        "name": "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors",
        "url": "https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors",
        "dest_dir": os.path.join(BASE_DIR, "clip_vision")
    }
]

def download_file(url, dest_path):
    print(f"\n[Starting Download] {os.path.basename(dest_path)}")
    print(f"URL: {url}")
    
    if os.path.exists(dest_path) and os.path.getsize(dest_path) > 1000000:
        print(f"File already exists with size {os.path.getsize(dest_path)} bytes. Skipping.")
        return True

    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    temp_path = dest_path + ".tmp"
    
    headers = {'User-Agent': 'Mozilla/5.0'}
    req = urllib.request.Request(url, headers=headers)
    
    start_time = time.time()
    last_print = start_time
    
    try:
        with urllib.request.urlopen(req) as response, open(temp_path, 'wb') as out_file:
            total_size = int(response.headers.get('content-length', 0))
            downloaded = 0
            block_size = 1024 * 1024  # 1MB
            
            while True:
                buffer = response.read(block_size)
                if not buffer:
                    break
                out_file.write(buffer)
                downloaded += len(buffer)
                
                curr_time = time.time()
                if curr_time - last_print > 3.0:
                    percent = (downloaded / total_size * 100) if total_size > 0 else 0
                    speed = (downloaded / (curr_time - start_time)) / (1024 * 1024)
                    print(f"Progress: {downloaded / (1024*1024):.1f} MB / {total_size / (1024*1024):.1f} MB ({percent:.1f}%) - {speed:.2f} MB/s")
                    last_print = curr_time
                    
        if os.path.exists(dest_path):
            os.remove(dest_path)
        os.rename(temp_path, dest_path)
        print(f"[Completed] {os.path.basename(dest_path)} ({downloaded / (1024*1024):.1f} MB)")
        return True
    except Exception as e:
        print(f"[Error] Failed to download {url}: {e}")
        if os.path.exists(temp_path):
            os.remove(temp_path)
        return False

def main():
    print("=== ComfyUI Motion Control & Consistency Models Setup ===")
    for item in MODELS_TO_DOWNLOAD:
        dest_path = os.path.join(item["dest_dir"], item["name"])
        success = download_file(item["url"], dest_path)
        if not success:
            print(f"Warning: Failed to download {item['name']}")
    print("\n=== All Setup Tasks Finished! ===")

if __name__ == "__main__":
    main()
