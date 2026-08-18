import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"

def test_generate():
    # 1. Get object_info to find checkpoint loader models
    try:
        req = urllib.request.urlopen(f"{COMFY_URL}/object_info")
        info = json.loads(req.read().decode('utf-8'))
        ckpt_list = info.get("CheckpointLoaderSimple", {}).get("input", {}).get("required", {}).get("ckpt_name", [[]])[0]
        print("Available Checkpoints:", ckpt_list)
    except Exception as e:
        print("Error getting object_info:", e)
        return

    if not ckpt_list:
        print("No checkpoint models found in ComfyUI!")
        return

    chosen_ckpt = ckpt_list[0]
    print(f"Using Checkpoint: {chosen_ckpt}")

    # 2. Build default Text-to-Image workflow prompt
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
                "seed": 42,
                "steps": 20
            }
        },
        "4": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
                "ckpt_name": chosen_ckpt
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
                "text": "pixel art fantasy knight with sword, side view, dark fantasy, solid black background, 128x128 pixel style"
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": "blurry, low quality, deformed, photorealistic"
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
                "filename_prefix": "TestWarrior_TP2",
                "images": ["8", 0]
            }
        }
    }

    # 3. Queue prompt
    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Prompt queued successfully! Prompt ID: {prompt_id}")

    # 4. Wait for generation to complete
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
                    subfolder = img_info.get("subfolder", "")
                    img_type = img_info.get("type", "output")
                    print(f"Generated Image: {filename} (subfolder: {subfolder}, type: {img_type})")
                    return filename
    print("Timed out waiting for image output.")

if __name__ == "__main__":
    test_generate()
