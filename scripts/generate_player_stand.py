import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

PROMPT_DEF = {
    "name": "Concept_Player_Hunter_Stand",
    "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy puppet hunter, wearing black trench coat, brass prosthetic arm holding clockwork sword, glowing steam valve on back, neutral gray background, crisp clean pixel edges, high detail 2d game asset",
    "negative": "multiple characters, collage, grid, blurry, 3d, photorealistic, deformed"
}

def queue_prompt():
    prompt_workflow = {
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8.0,
                "denoise": 1,
                "latent_image": ["5", 0],
                "model": ["4", 0],
                "negative": ["7", 0],
                "positive": ["6", 0],
                "sampler_name": "euler_ancestral",
                "scheduler": "karras",
                "seed": 777,
                "steps": 25
            }
        },
        "4": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
                "ckpt_name": "Anything-v5.0-PRT-RE.safetensors"
            }
        },
        "5": {
            "class_type": "EmptyLatentImage",
            "inputs": {
                "batch_size": 1,
                "height": 512,
                "width": 512
            }
        },
        "6": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": PROMPT_DEF["positive"]
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": PROMPT_DEF["negative"]
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
                "filename_prefix": PROMPT_DEF["name"],
                "images": ["8", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Queued ID: {prompt_id}")
    return prompt_id

def wait_for_prompt(prompt_id):
    for _ in range(30):
        time.sleep(2)
        try:
            hist_req = urllib.request.urlopen(f"{COMFY_URL}/history/{prompt_id}")
            hist_data = json.loads(hist_req.read().decode('utf-8'))
            if prompt_id in hist_data:
                outputs = hist_data[prompt_id].get("outputs", {})
                for node_id, out in outputs.items():
                    if "images" in out:
                        img_info = out["images"][0]
                        filename = img_info["filename"]
                        print(f"Finished: {filename}")
                        return filename
        except Exception as e:
            pass
    return None

if __name__ == "__main__":
    pid = queue_prompt()
    fname = wait_for_prompt(pid)
    if fname:
        src = os.path.join(OUTPUT_DIR, fname)
        dst = os.path.join(ARTIFACT_DIR, fname)
        import shutil
        shutil.copy2(src, dst)
        print(f"Copied to: {dst}")
