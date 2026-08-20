import urllib.request
import json
import os
import time
from PIL import Image

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"
DOC_IMG_DIR = r"C:\Users\PC\Projects\TP2\doc\images\concepts"
PROJECT_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"

# Matching Player_Concept_Gothic and Garon_Concept_Gothic style:
# Sharp pixel art, crisp black outline, metallic brass & dark iron palette, Castlevania/Blasphemous sprite aesthetic
PIXEL_MONSTERS = [
    {
        "id": "3101",
        "name": "Monster_3101_SpearSentry_Concept",
        "positive": "pixel art, 32-bit pixel sprite, full body character sprite, side view, dark fantasy gothic automaton spear soldier, wearing brass face mask, dark military trenchcoat with golden brass epaulets and trim, dark trousers and boots, holding long pneumatic piston spear resting at side, crisp black pixel outline, clean pixel clusters, retro game sprite style, solid plain light gray background",
        "negative": "blurry, smooth digital painting, vector, 3d, photorealistic, modern, anime, multiple characters, extra limbs, circular frame, emblem, background detail"
    },
    {
        "id": "3102",
        "name": "Monster_3102_ShadowStalker_Concept",
        "positive": "pixel art, 32-bit pixel sprite, full body character sprite, side view, dark fantasy gothic automaton shadow assassin, slender mechanical puppet, dark leather hooded cloak, golden brass gear joints, holding dual serrated clockwork saw daggers, crisp black pixel outline, clean pixel clusters, retro game sprite style, solid plain light gray background",
        "negative": "blurry, smooth digital painting, vector, 3d, photorealistic, modern, anime, multiple characters, extra limbs, circular frame, emblem, background detail"
    },
    {
        "id": "3103",
        "name": "Monster_3103_WaveHeavy_Concept",
        "positive": "pixel art, 32-bit pixel sprite, full body character sprite, side view, imposing bulky dark fantasy steam golem, heavy dark iron plate armor with brass trims, glowing brass steam boiler chest venting white steam puffs, heavy iron boots, holding gigantic steam pulverizer warhammer resting on shoulder, crisp black pixel outline, clean pixel clusters, retro game sprite style, solid plain light gray background",
        "negative": "blurry, smooth digital painting, vector, 3d, photorealistic, modern, anime, naked, human flesh, multiple characters, extra limbs, circular frame, emblem"
    },
    {
        "id": "3104",
        "name": "Monster_3104_ShieldSentinel_Concept",
        "positive": "pixel art, 32-bit pixel sprite, full body character sprite, side view, dark fantasy gothic automaton shield knight, heavy dark iron and brass full plate armor, ornate brass visor helmet, holding massive fortress tower shield with brass rivets and steam vents, holding heavy gear mace in other hand, crisp black pixel outline, clean pixel clusters, retro game sprite style, solid plain light gray background",
        "negative": "blurry, smooth digital painting, vector, 3d, photorealistic, modern, anime, multiple characters, extra limbs, circular frame, emblem, background detail"
    },
    {
        "id": "3105",
        "name": "Monster_3105_OrbitalMarksman_Concept",
        "positive": "pixel art, 32-bit pixel sprite, full body character sprite, side view, dark fantasy gothic automaton marksman sniper, dark long coat with brass gears, multi-lens brass optical scope head, holding long-barrel clockwork repeating crossbow rifle, glowing amber core, crisp black pixel outline, clean pixel clusters, retro game sprite style, solid plain light gray background",
        "negative": "blurry, smooth digital painting, vector, 3d, photorealistic, modern, anime, multiple characters, extra limbs, circular frame, emblem, background detail"
    }
]

def queue_prompt(m_def, seed=42):
    prompt_workflow = {
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8.5,
                "denoise": 1.0,
                "latent_image": ["5", 0],
                "model": ["4", 0],
                "negative": ["7", 0],
                "positive": ["6", 0],
                "sampler_name": "euler",
                "scheduler": "normal",
                "seed": seed,
                "steps": 28
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
                        print(f"Generated Pixel Sprite: {filename}")
                        return filename
        except Exception as e:
            pass
    return None

def main():
    os.makedirs(DOC_IMG_DIR, exist_ok=True)
    os.makedirs(PROJECT_DIR, exist_ok=True)
    for m in PIXEL_MONSTERS:
        pid = queue_prompt(m, seed=int(m["id"]) * 19 + 888)
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
            print(f"Saved pixel sprite: {doc_dst}")

if __name__ == "__main__":
    main()
