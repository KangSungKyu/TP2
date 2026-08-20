import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Shared\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

PROMPTS = [
    {
        "name": "Concept_Player_PuppetHunter",
        "positive": "masterpiece, best quality, 2d pixel art sprite sheet concept, side view, gothic puppet hunter, dark leather longcoat, brass mechanical prosthetic left arm, clockwork saw-blade sword, glowing steam exhaust on back, dark fantasy, solid neutral background, full body 128x128 pixel style, crisp silhouette",
        "negative": "blurry, low quality, photorealistic, 3d render, modern clothes, deformed, extra limbs"
    },
    {
        "name": "Concept_Boss_ClockworkGaron",
        "positive": "masterpiece, best quality, 2d pixel art concept, side view, huge clockwork commander boss, heavy brass armor, glowing red steam boiler on back, massive clockwork steam greatsword, steam bursting out, dark fantasy, solid neutral background, 128x128 pixel style, imposing silhouette",
        "negative": "blurry, low quality, photorealistic, 3d render, cute, modern, deformed"
    }
]

def queue_prompt(prompt_def, seed=100):
    prompt_workflow = {
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 7.5,
                "denoise": 1,
                "latent_image": ["5", 0],
                "model": ["4", 0],
                "negative": ["7", 0],
                "positive": ["6", 0],
                "sampler_name": "euler_ancestral",
                "scheduler": "karras",
                "seed": seed,
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
                "text": prompt_def["positive"]
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": prompt_def["negative"]
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
                "filename_prefix": prompt_def["name"],
                "images": ["8", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Queued {prompt_def['name']} with ID: {prompt_id}")
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

def main():
    generated_files = []
    for p in PROMPTS:
        pid = queue_prompt(p, seed=int(time.time()) % 10000)
        fname = wait_for_prompt(pid)
        if fname:
            src = os.path.join(OUTPUT_DIR, fname)
            dst = os.path.join(ARTIFACT_DIR, fname)
            if os.path.exists(src):
                import shutil
                shutil.copy2(src, dst)
                print(f"Copied to artifact: {dst}")
                generated_files.append(fname)
    print("All concept arts generated:", generated_files)

if __name__ == "__main__":
    main()
