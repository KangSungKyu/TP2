import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"

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

def generate_image(prompt_text, filename_prefix, seed=100):
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
                "height": 512,
                "width": 512
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
                "text": "blurry, low quality, deformed, photorealistic, 3d render, bad anatomy, cropped, noisy"
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
    print("=== Generating Gothic Clockwork Steampunk Pixel Art Concept Assets ===")

    # 1. Player: Puppet Hunter
    player_prompt = "128x128 pixel art full body side view facing right, dark gothic steampunk puppet hunter warrior, wearing black leather long trench coat, scarf, brass mechanical automaton prosthetic arm on left side, holding clockwork saw-blade katana sword, Lies of P Blasphemous style, solid gray background, clean pixel lines"
    generate_image(player_prompt, "Player_PuppetHunter_Concept", seed=101)

    # 2. Boss: Clockwork Commander Garon
    garon_prompt = "128x128 pixel art full body side view facing right, massive brass heavy armor steampunk knight commander, 3-barrel steam boiler on back, holding giant steam greatsword, dark gothic clockwork boss, Lies of P style, solid gray background, detailed 2D pixel sprite"
    generate_image(garon_prompt, "Garon_ClockworkCommander_Concept", seed=201)

    # 3. Monster 3101: SpearSentry
    spear_prompt = "128x128 pixel art full body side view facing right, brass mask automaton guard in old military uniform, holding mechanical piston spear, dark gothic steampunk enemy, solid gray background"
    generate_image(spear_prompt, "Monster_3101_SpearSentry_Concept", seed=301)

    print("=== Concept Generation Batch Completed ===")
