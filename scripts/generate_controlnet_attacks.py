import urllib.request
import json
import os
import time
from PIL import Image

COMFY_URL = "http://127.0.0.1:8188"
COMFY_INPUT = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\input"
COMFY_OUTPUT = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
POSE_DIR = r"C:\Users\PC\Projects\TP2\doc\images\pose_skeletons"
TARGET_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def queue_controlnet_frame(pose_img_name, motion_name, frame_idx, seed=1234):
    prompt_workflow = {
        "1": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
                "ckpt_name": "Anything-v5.0-PRT-RE.safetensors"
            }
        },
        "2": {
            "class_type": "ControlNetLoader",
            "inputs": {
                "control_net_name": "control_v11p_sd15_openpose_fp16.safetensors"
            }
        },
        "3": {
            "class_type": "LoadImage",
            "inputs": {
                "image": pose_img_name
            }
        },
        "4": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["1", 1],
                "text": "masterpiece, pixel art, 32-bit pixel sprite, full body character sprite, side view facing right, dark fantasy gothic automaton puppet hunter, black leather trenchcoat, golden brass mechanical prosthetic left arm, holding clockwork saw-blade katana in combat slash pose, clean crisp pixel outlines, retro game sprite style, solid neutral gray background"
            }
        },
        "5": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["1", 1],
                "text": "blurry, smooth digital painting, vector, 3d, photorealistic, anime face closeup, multiple characters, extra limbs, broken sword, floating sword, frame, circle, UI, watermark"
            }
        },
        "6": {
            "class_type": "ControlNetApply",
            "inputs": {
                "conditioning": ["4", 0],
                "control_net": ["2", 0],
                "image": ["3", 0],
                "strength": 0.95
            }
        },
        "7": {
            "class_type": "EmptyLatentImage",
            "inputs": {
                "batch_size": 1,
                "height": 512,
                "width": 512
            }
        },
        "8": {
            "class_type": "KSampler",
            "inputs": {
                "cfg": 8.0,
                "denoise": 1.0,
                "latent_image": ["7", 0],
                "model": ["1", 0],
                "negative": ["5", 0],
                "positive": ["6", 0],
                "sampler_name": "euler",
                "scheduler": "normal",
                "seed": seed + frame_idx * 7,
                "steps": 25
            }
        },
        "9": {
            "class_type": "VAEDecode",
            "inputs": {
                "samples": ["8", 0],
                "vae": ["1", 2]
            }
        },
        "10": {
            "class_type": "SaveImage",
            "inputs": {
                "filename_prefix": f"Player_CN_{motion_name}_F{frame_idx:02d}",
                "images": ["9", 0]
            }
        }
    }

    data = json.dumps({"prompt": prompt_workflow}).encode('utf-8')
    req = urllib.request.Request(f"{COMFY_URL}/prompt", data=data, headers={'Content-Type': 'application/json'})
    resp = urllib.request.urlopen(req)
    prompt_res = json.loads(resp.read().decode('utf-8'))
    return prompt_res.get("prompt_id")

def wait_for_prompt(prompt_id):
    for _ in range(40):
        time.sleep(2)
        try:
            hist_req = urllib.request.urlopen(f"{COMFY_URL}/history/{prompt_id}")
            hist_data = json.loads(hist_req.read().decode('utf-8'))
            if prompt_id in hist_data:
                outputs = hist_data[prompt_id].get("outputs", {})
                for node_id, out in outputs.items():
                    if "images" in out:
                        return out["images"][0]["filename"]
        except Exception:
            pass
    return None

def build_attack_motion(motion_name, frame_count):
    print(f"\n--- Generating ControlNet Motion: {motion_name} ({frame_count} frames) ---")
    frame_images = []
    
    for f in range(1, frame_count + 1):
        pose_filename = f"Pose_{f:02d}.png"
        src_pose = os.path.join(POSE_DIR, motion_name, pose_filename)
        dst_pose = os.path.join(COMFY_INPUT, f"CN_{motion_name}_{pose_filename}")
        
        import shutil
        shutil.copy2(src_pose, dst_pose)
        
        pid = queue_controlnet_frame(f"CN_{motion_name}_{pose_filename}", motion_name, f, seed=999)
        out_name = wait_for_prompt(pid)
        if out_name:
            out_img_path = os.path.join(COMFY_OUTPUT, out_name)
            img = Image.open(out_img_path).convert("RGBA")
            img_256 = img.resize((256, 256), Image.Resampling.LANCZOS)
            frame_images.append(img_256)
            print(f"Generated frame {f}/{frame_count}: {out_name}")
        else:
            print(f"Error generating frame {f}")

    if len(frame_images) == frame_count:
        # Stitch into sprite sheet
        sheet_w = 256 * frame_count
        sheet_h = 256
        sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))
        for idx, fimg in enumerate(frame_images):
            sheet.paste(fimg, (idx * 256, 0))
            
        out_sheet_name = f"{motion_name}.png"
        target_path = os.path.join(TARGET_DIR, out_sheet_name)
        art_path = os.path.join(ARTIFACT_DIR, out_sheet_name)
        doc_path = os.path.join(r"C:\Users\PC\Projects\TP2\doc\images\player_required", out_sheet_name)
        
        sheet.save(target_path)
        sheet.save(art_path)
        sheet.save(doc_path)
        print(f"[SUCCESS] Saved ControlNet sprite sheet: {target_path} ({sheet_w}x{sheet_h})")

def main():
    os.makedirs(COMFY_INPUT, exist_ok=True)
    os.makedirs(TARGET_DIR, exist_ok=True)
    build_attack_motion("Attack_01", 8)
    build_attack_motion("Attack_02", 10)

if __name__ == "__main__":
    main()
