import os
import base64
import requests
import json

API_KEY = "b8910d80-048e-42f7-8ff9-8c347b4d36bb"
HEADERS = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

BASE_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures"

def save_base64_image(base64_str, output_path):
    if "," in base64_str:
        base64_str = base64_str.split(",")[1]
    img_data = base64.b64decode(base64_str)
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "wb") as f:
        f.write(img_data)
    print(f"[PixelLab API] Successfully saved: {output_path}")

def generate_pixen_image(description, width, height, output_path, no_bg=True):
    print(f"[PixelLab API] Generating Pixen image ({width}x{height}): {description}...")
    url = "https://api.pixellab.ai/v2/create-image-pixen"
    payload = {
        "description": description,
        "image_size": {"width": width, "height": height},
        "no_background": no_bg
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 200:
        data = r.json()
        b64 = data.get("image", {}).get("base64")
        if b64:
            save_base64_image(b64, output_path)
            return True
    print(f"Error {r.status_code}: {r.text}")
    return False

def generate_pixflux_image(description, width, height, output_path, no_bg=True):
    print(f"[PixelLab API] Generating Pixflux image ({width}x{height}): {description}...")
    url = "https://api.pixellab.ai/v2/create-image-pixflux"
    payload = {
        "description": description,
        "image_size": {"width": width, "height": height},
        "transparent_background": no_bg
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 200:
        data = r.json()
        b64 = data.get("image", {}).get("base64")
        if b64:
            save_base64_image(b64, output_path)
            return True
    print(f"Error {r.status_code}: {r.text}")
    return False

def main():
    print("=== Starting PixelLab Asset Generation Pipeline ===")
    
    # 1. Boss 1: Iron Guard Garon (철위병 가론)
    garon_dir = os.path.join(BASE_DIR, "Characters", "Bosses", "Garon")
    
    generate_pixen_image(
        "heavy armored knight boss Iron Guard Garon, holding a massive shield and dark broadsword, glowing red eye visor, dark oriental sci-fi pixel art style, side view",
        128, 128,
        os.path.join(garon_dir, "Garon_Concept.png")
    )
    
    generate_pixen_image(
        "heavy armored knight boss Iron Guard Garon performing heavy downward sword slash smash with red energy effect, pixel art side view",
        128, 128,
        os.path.join(garon_dir, "Garon_Pattern_Slash.png")
    )
    
    generate_pixen_image(
        "heavy armored knight boss Iron Guard Garon charging forward at high speed with spear thrust, pixel art side view",
        128, 128,
        os.path.join(garon_dir, "Garon_Pattern_Charge.png")
    )
    
    generate_pixen_image(
        "heavy armored knight boss Iron Guard Garon smashing ground creating glowing shockwave wave on floor, pixel art side view",
        128, 128,
        os.path.join(garon_dir, "Garon_Pattern_Shockwave.png")
    )

    # 2. Monsters / Enemies
    monsters_dir = os.path.join(BASE_DIR, "Characters", "Monsters")
    
    generate_pixen_image(
        "cybernetic spear guard enemy, dark armor, glowing cyan visor, 2d platformer pixel art sprite, side view",
        64, 64,
        os.path.join(monsters_dir, "Monster_CyberGuard.png")
    )
    
    generate_pixen_image(
        "shadow phantom assassin enemy with dual glowing daggers, dark ghost aura, pixel art sprite, side view",
        64, 64,
        os.path.join(monsters_dir, "Monster_ShadowPhantom.png")
    )

    # 3. Tileset
    tileset_dir = os.path.join(BASE_DIR, "Tilesets")
    
    generate_pixflux_image(
        "2d platformer sidescroller tileset, dark ruined ancient temple stone ground platforms, glowing cyan rune circuit pattern, spikes hazard, pixel art 256x256 sheet",
        256, 256,
        os.path.join(tileset_dir, "DarkTemple_Tileset.png"),
        no_bg=False
    )

    # 4. Backgrounds
    bg_dir = os.path.join(BASE_DIR, "Backgrounds")
    
    generate_pixflux_image(
        "2d side-scroller parallax background, dark night sky with giant crimson moon and temporal dimensional rift rift, dark oriental scifi atmosphere, pixel art",
        384, 216,
        os.path.join(bg_dir, "Background_FarSky.png"),
        no_bg=False
    )
    
    generate_pixflux_image(
        "2d side-scroller parallax background midground layer, ruined ancient sci-fi temple pillars, floating broken stones, cyan glow energy, pixel art",
        384, 216,
        os.path.join(bg_dir, "Background_RuinedPillars.png"),
        no_bg=False
    )

    print("=== Asset Generation Completed ===")

if __name__ == "__main__":
    main()
