import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"
DOC_IMG_DIR = r"C:\Users\PC\Projects\TP2\doc\images\concepts"
PROJECT_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"

MONSTERS = [
    {
        "id": "3101",
        "name": "Monster_3101_SpearSentry_Concept",
        "positive": "masterpiece, best quality, single isolated full body character sprite, 2d side-view pixel art automaton spear guard, standing idle pose, facing right, wearing brass mask and tattered dark military frock uniform, holding long pneumatic piston spear resting on ground, clockwork gears, clean crisp pixel outlines, simple solid neutral gray background, no border, no emblem, no circle",
        "negative": "circle, emblem, decorative frame, border, clock background, multiple views, multiple characters, text, ui, blurry, 3d render, photorealistic, deformed"
    },
    {
        "id": "3102",
        "name": "Monster_3102_ShadowStalker_Concept",
        "positive": "masterpiece, best quality, single isolated full body character sprite, 2d side-view pixel art clockwork assassin puppet, standing idle pose, facing right, slender automaton, dark leather hood and coat, brass gears on joints, holding dual serrated clockwork daggers, mechanical wire tail, clean crisp pixel outlines, simple solid neutral gray background, no border, no emblem, no circle",
        "negative": "circle, emblem, decorative frame, border, clock background, magic circle, multiple views, multiple characters, text, ui, blurry, 3d render, photorealistic, deformed"
    },
    {
        "id": "3103",
        "name": "Monster_3103_WaveHeavy_Concept",
        "positive": "masterpiece, best quality, single isolated full body character sprite, 2d side-view pixel art heavy steam golem, standing idle pose, facing right, massive bulky brass boiler chest venting white steam, heavy dark iron plate armor, holding gigantic steam pulverizing hammer, imposing heavy silhouette, clean crisp pixel outlines, simple solid neutral gray background, no border, no emblem, no circle",
        "negative": "naked, human skin, circle, emblem, decorative frame, border, clock background, multiple views, multiple characters, text, ui, blurry, 3d render, photorealistic, deformed"
    },
    {
        "id": "3104",
        "name": "Monster_3104_ShieldSentinel_Concept",
        "positive": "masterpiece, best quality, single isolated full body character sprite, 2d side-view pixel art automaton shield sentinel, standing idle guard pose, facing right, ornate brass helmet and heavy plate armor, holding massive steam tower shield with iron rivets in front and gear mace in other hand, clean crisp pixel outlines, simple solid neutral gray background, no border, no emblem, no circle",
        "negative": "circle, emblem, decorative frame, border, clock background, shrine, fire frame, multiple views, multiple characters, text, ui, blurry, 3d render, photorealistic, deformed"
    },
    {
        "id": "3105",
        "name": "Monster_3105_OrbitalMarksman_Concept",
        "positive": "masterpiece, best quality, single isolated full body character sprite, 2d side-view pixel art clockwork sniper automaton, standing idle pose, facing right, multi-lens brass mechanical eye, dark longcoat, holding long-barrel clockwork repeating crossbow rifle, glowing amber core, clean crisp pixel outlines, simple solid neutral gray background, no border, no emblem, no circle",
        "negative": "circle, emblem, decorative frame, border, clock background, floating parts, multiple views, multiple characters, text, ui, blurry, 3d render, photorealistic, deformed"
    }
]

def queue_prompt(m_def, seed=777):
    prompt_workflow = {
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8.0,
                "denoise": 1.0,
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
                "text": m_def["positive"]
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": m_def["negative"]
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
                "filename_prefix": m_def["name"],
                "images": ["8", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Queued {m_def['name']} ID: {prompt_id}")
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
                        print(f"Generated: {filename}")
                        return filename
        except Exception as e:
            pass
    return None

def main():
    os.makedirs(DOC_IMG_DIR, exist_ok=True)
    os.makedirs(PROJECT_DIR, exist_ok=True)
    for m in MONSTERS:
        pid = queue_prompt(m, seed=int(m["id"]) * 13 + 333)
        fname = wait_for_prompt(pid)
        if fname:
            src = os.path.join(OUTPUT_DIR, fname)
            art_dst = os.path.join(ARTIFACT_DIR, f"{m['name']}.png")
            doc_dst = os.path.join(DOC_IMG_DIR, f"{m['name']}.png")
            proj_dst = os.path.join(PROJECT_DIR, f"{m['name']}.png")
            import shutil
            shutil.copy2(src, art_dst)
            shutil.copy2(src, doc_dst)
            shutil.copy2(src, proj_dst)
            print(f"Updated concept: {doc_dst}")

if __name__ == "__main__":
    main()
