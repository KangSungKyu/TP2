import urllib.request
import json
import os
import time
import shutil
from PIL import Image, ImageDraw

COMFY_URL = "http://127.0.0.1:8188"
PROJECT_PLAYER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\0625e4e7-91c3-4cca-bf23-c0844cae8319"

def get_checkpoint():
    try:
        req = urllib.request.urlopen(f"{COMFY_URL}/object_info")
        info = json.loads(req.read().decode('utf-8'))
        ckpt_list = info.get("CheckpointLoaderSimple", {}).get("input", {}).get("required", {}).get("ckpt_name", [[]])[0]
        if ckpt_list:
            return ckpt_list[0]
    except Exception as e:
        print("Error getting checkpoint:", e)
    return None

def generate_comfy_sprite(prompt_text, filename_prefix, seed=500):
    ckpt = get_checkpoint()
    if not ckpt:
        print("Checkpoint not found!")
        return None

    prompt_workflow = {
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8,
                "denoise": 1,
                "latent_image": ["5", 0],
                "model": ["4", 0],
                "negative": ["7", 0],
                "positive": ["6", 0],
                "sampler_name": "euler",
                "scheduler": "normal",
                "seed": seed,
                "steps": 20
            }
        },
        "4": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
                "ckpt_name": ckpt
            }
        },
        "5": {
            "class_type": "EmptyLatentImage",
            "inputs": {
                "batch_size": 1,
                "height": 256,
                "width": 1536  # 6 frames of 256x256
            }
        },
        "6": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": prompt_text
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": "blurry, low quality, deformed, photorealistic, 3d render, bad anatomy, cropped"
            }
        },
        "8": {
            "class_type": "VAEDecode",
            "inputs": {
                "samples": ["3", 0],
                "vae": ["4", 2]
            }
        },
        "9": {
            "class_type": "SaveImage",
            "inputs": {
                "filename_prefix": filename_prefix,
                "images": ["8", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Queued {filename_prefix}... ID: {prompt_id}")

    for _ in range(30):
        time.sleep(2)
        hist_req = urllib.request.urlopen(f"{COMFY_URL}/history/{prompt_id}")
        hist_data = json.loads(hist_req.read().decode('utf-8'))
        if prompt_id in hist_data:
            outputs = hist_data[prompt_id].get("outputs", {})
            for node_id, out in outputs.items():
                if "images" in out:
                    img_info = out["images"][0]
                    filename = img_info["filename"]
                    print(f"Finished {filename_prefix}: {filename}")
                    return filename
    return None

if __name__ == "__main__":
    print("=== Generating Group 1 Required 256x256 Player Animation Sprite Sheets ===")
    os.makedirs(PROJECT_PLAYER_DIR, exist_ok=True)

    required_tasks = [
        ("Player_256_Idle", "256x256 pixel art sprite sheet 6 frames horizontal row side view facing right, gothic steampunk puppet hunter breathing idle stance, black trench coat, brass automaton arm, detailed 2D pixel art, solid gray background", 601),
        ("Player_256_Run", "256x256 pixel art sprite sheet 8 frames horizontal row side view facing right, gothic steampunk puppet hunter athletic sprint run cycle, coat fluttering, solid gray background", 602),
        ("Player_256_Attack_01", "256x256 pixel art sprite sheet 6 frames horizontal row side view facing right, gothic steampunk puppet hunter fast horizontal katana slash, cyan clockwork blade trail, solid gray background", 607),
        ("Player_256_Attack_02", "256x256 pixel art sprite sheet 6 frames horizontal row side view facing right, gothic steampunk puppet hunter rising diagonal katana slash strike, brass arm spark, solid gray background", 608),
    ]

    comfy_out_dir = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"

    for prefix, prompt, seed in required_tasks:
        fn = generate_comfy_sprite(prompt, prefix, seed)
        if fn:
            src_path = os.path.join(comfy_out_dir, fn)
            proj_dest = os.path.join(PROJECT_PLAYER_DIR, f"{prefix}.png")
            art_dest = os.path.join(ARTIFACT_DIR, f"{prefix}.png")
            shutil.copy(src_path, proj_dest)
            shutil.copy(src_path, art_dest)
            print(f"Saved: {proj_dest} & {art_dest}")

    print("=== Group 1 Required Generation Complete ===")
