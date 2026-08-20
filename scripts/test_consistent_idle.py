import urllib.request
import json
import os
import time

COMFY_URL = "http://127.0.0.1:8188"
OUTPUT_DIR = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def test_img2img_idle():
    prompt_workflow = {
        "10": {
            "class_type": "LoadImage",
            "inputs": {
                "image": "Player_Concept_Gothic.png"
            }
        },
        "12": {
            "class_type": "VAEEncode",
            "inputs": {
                "pixels": ["10", 0],
                "vae": ["4", 2]
            }
        },
        "3": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8.0,
                "denoise": 0.35,  # Low denoise to preserve 100% of the original character features, face, clothes, arm, sword
                "latent_image": ["12", 0],
                "model": ["4", 0],
                "negative": ["7", 0],
                "positive": ["6", 0],
                "sampler_name": "euler_ancestral",
                "scheduler": "karras",
                "seed": 100,
                "steps": 25
            }
        },
        "4": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
                "ckpt_name": "Anything-v5.0-PRT-RE.safetensors"
            }
        },
        "6": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": "masterpiece, best quality, single full body character sprite, pixel art dark fantasy puppet hunter, black leather long coat, golden brass prosthetic arm holding clockwork saw-blade katana, idle breathing pose, clean crisp pixel edges, 256x256 pixel art style, neutral solid background"
            }
        },
        "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["4", 1],
                "text": "blurry, low quality, photorealistic, 3d, different character, blonde hair, green hair, deformed, extra limbs, multiple characters"
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
                "filename_prefix": "Player_256_Consistent_Idle_F1",
                "images": ["8", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    prompt_id = prompt_res.get("prompt_id")
    print(f"Queued Img2Img Idle Prompt ID: {prompt_id}")

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
                        print(f"Generated Consistent Frame: {filename}")
                        src = os.path.join(OUTPUT_DIR, filename)
                        dst = os.path.join(ARTIFACT_DIR, filename)
                        import shutil
                        shutil.copy2(src, dst)
                        print(f"Copied to artifact: {dst}")
                        return filename
        except Exception as e:
            pass
    return None

if __name__ == "__main__":
    test_img2img_idle()
