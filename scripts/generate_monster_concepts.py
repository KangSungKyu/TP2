import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"
TARGET_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"

MONSTER_PROMPTS = [
    {
        "id": "3101",
        "name": "Monster_3101_SpearSentry",
        "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy automaton spear guard, wearing brass mask, tattered military frock uniform, holding high-pressure pneumatic piston spear, clockwork gears visible, clean crisp pixel edges, neutral solid background, 256x256 pixel art style",
        "negative": "multiple characters, 3d render, photorealistic, modern, blurry, deformed"
    },
    {
        "id": "3102",
        "name": "Monster_3102_ShadowStalker",
        "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy clockwork assassin, slender agile puppet, dark hood, brass gears, dual serrated clockwork daggers, mechanical wire tail, glowing amber eye, clean crisp pixel edges, neutral solid background, 256x256 pixel art style",
        "negative": "multiple characters, 3d render, photorealistic, modern, blurry, deformed"
    },
    {
        "id": "3103",
        "name": "Monster_3103_WaveHeavy",
        "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy heavy steam golem, massive brass boiler on chest venting white steam, heavy iron armor plating, wielding gigantic steam-powered pulverizing hammer, imposing bulky silhouette, clean crisp pixel edges, neutral solid background, 256x256 pixel art style",
        "negative": "multiple characters, 3d render, photorealistic, modern, blurry, deformed"
    },
    {
        "id": "3104",
        "name": "Monster_3104_ShieldSentinel",
        "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy automaton shield sentinel, wearing ornate brass helmet, holding massive fortress gate steam tower shield in left hand and heavy gear mace in right hand, steam venting from shield vents, clean crisp pixel edges, neutral solid background, 256x256 pixel art style",
        "negative": "multiple characters, 3d render, photorealistic, modern, blurry, deformed"
    },
    {
        "id": "3105",
        "name": "Monster_3105_OrbitalMarksman",
        "positive": "masterpiece, best quality, single full body 2d side-view character sprite, pixel art dark fantasy clockwork sniper automaton, multi-lens mechanical scope eye, long coat, holding long-barrel clockwork repeating crossbow rifle, glowing amber steam core, clean crisp pixel edges, neutral solid background, 256x256 pixel art style",
        "negative": "multiple characters, 3d render, photorealistic, modern, blurry, deformed"
    }
]

def queue_prompt(m_def, seed=555):
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
    print(f"Queued {m_def['name']} with ID: {prompt_id}")
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
    os.makedirs(TARGET_DIR, exist_ok=True)
    generated = []
    for m in MONSTER_PROMPTS:
        pid = queue_prompt(m, seed=int(m["id"]) * 7 + 101)
        fname = wait_for_prompt(pid)
        if fname:
            src = os.path.join(OUTPUT_DIR, fname)
            art_dst = os.path.join(ARTIFACT_DIR, f"{m['name']}_Concept.png")
            proj_dst = os.path.join(TARGET_DIR, f"{m['name']}_Concept.png")
            import shutil
            shutil.copy2(src, art_dst)
            shutil.copy2(src, proj_dst)
            print(f"Copied to: {art_dst} and {proj_dst}")
            generated.append(f"{m['name']}_Concept.png")
    print("All monster concepts successfully generated:", generated)

if __name__ == "__main__":
    main()
