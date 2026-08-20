import urllib.request
import json
import os
import time
import shutil

COMFY_URL = "http://127.0.0.1:8188"
PROJECT_MONSTER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"
DOC_CONCEPT_DIR = r"c:\Users\PC\Projects\TP2\doc\images\concepts"
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

def generate_monster(prompt_text, filename_prefix, seed=700):
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
                "text": "blurry, low quality, deformed, photorealistic, 3d render, bad anatomy, cropped, multiple characters"
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
    print("=== Generating 5 Monster Dot Pixel Art Concepts (Unified Style) ===")
    os.makedirs(PROJECT_MONSTER_DIR, exist_ok=True)
    os.makedirs(DOC_CONCEPT_DIR, exist_ok=True)

    style_suffix = ", 128x128 pixel art single character full body side view facing right, crisp black outline, gothic brass leather dot shading, dark clockwork steampunk, solid gray background, clean 2D pixel sprite"

    monsters = [
        ("Monster_3101_SpearSentry_Concept", "brass mask automaton spear sentry in old military uniform holding mechanical piston spear" + style_suffix, 701),
        ("Monster_3102_ShadowStalker_Concept", "agile lightweight steampunk assassin automaton with clockwork wings and dual saw-daggers" + style_suffix, 702),
        ("Monster_3103_WaveHeavy_Concept", "hulking brass heavy armor steam golem with boiler chest carrying giant smash hammer" + style_suffix, 703),
        ("Monster_3104_ShieldSentinel_Concept", "clockwork shield sentinel automaton holding giant brass steam iron gate tower shield" + style_suffix, 704),
        ("Monster_3105_OrbitalMarksman_Concept", "clockwork sniper automaton with lens eye and scope holding steam repeating crossbow" + style_suffix, 705)
    ]

    comfy_out_dir = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"

    for prefix, prompt, seed in monsters:
        fn = generate_monster(prompt, prefix, seed)
        if fn:
            src_path = os.path.join(comfy_out_dir, fn)
            proj_dest = os.path.join(PROJECT_MONSTER_DIR, f"{prefix}.png")
            doc_dest = os.path.join(DOC_CONCEPT_DIR, f"{prefix}.png")
            art_dest = os.path.join(ARTIFACT_DIR, f"{prefix}.png")
            shutil.copy(src_path, proj_dest)
            shutil.copy(src_path, doc_dest)
            shutil.copy(src_path, art_dest)
            print(f"Saved: {prefix}.png to Monsters, doc/concepts & artifact dir")

    print("=== All 5 Monster Dot Pixel Art Concepts Completed ===")
