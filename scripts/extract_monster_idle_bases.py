from PIL import Image
import os

COMFY_OUT = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
DOC_IMG = r"C:\Users\PC\Projects\TP2\doc\images\concepts"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def extract_clean_idle_bases():
    # 1. 3101 SpearSentry: Extract the elegant standing automaton puppet from center-left of the clock
    img1 = Image.open(os.path.join(COMFY_OUT, "Monster_3101_SpearSentry_00001_.png")).convert("RGBA")
    w1, h1 = img1.size
    # Crop central character body
    crop1 = img1.crop((int(w1 * 0.32), int(h1 * 0.08), int(w1 * 0.68), int(h1 * 0.92)))
    # Create 256x256 transparent canvas
    base1 = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    c1_w, c1_h = crop1.size
    scale1 = min(220.0 / c1_w, 220.0 / c1_h)
    resized1 = crop1.resize((int(c1_w * scale1), int(c1_h * scale1)), Image.Resampling.LANCZOS)
    base1.paste(resized1, ((256 - resized1.width) // 2, (256 - resized1.height) // 2))
    base1.save(os.path.join(DOC_IMG, "Monster_3101_SpearSentry_Idle_Base.png"))
    base1.save(os.path.join(ARTIFACT_DIR, "Monster_3101_SpearSentry_Idle_Base.png"))

    # 2. 3102 ShadowStalker: Extract the slender hooded assassin puppet
    img2 = Image.open(os.path.join(COMFY_OUT, "Monster_3102_ShadowStalker_00001_.png")).convert("RGBA")
    w2, h2 = img2.size
    crop2 = img2.crop((int(w2 * 0.16), int(h2 * 0.16), int(w2 * 0.82), int(h2 * 0.88)))
    base2 = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    c2_w, c2_h = crop2.size
    scale2 = min(220.0 / c2_w, 220.0 / c2_h)
    resized2 = crop2.resize((int(c2_w * scale2), int(c2_h * scale2)), Image.Resampling.LANCZOS)
    base2.paste(resized2, ((256 - resized2.width) // 2, (256 - resized2.height) // 2))
    base2.save(os.path.join(DOC_IMG, "Monster_3102_ShadowStalker_Idle_Base.png"))
    base2.save(os.path.join(ARTIFACT_DIR, "Monster_3102_ShadowStalker_Idle_Base.png"))

    # 3. 3103 WaveHeavy: Extract the massive steam golem
    img3 = Image.open(os.path.join(COMFY_OUT, "Monster_3103_WaveHeavy_00001_.png")).convert("RGBA")
    w3, h3 = img3.size
    crop3 = img3.crop((int(w3 * 0.28), int(h3 * 0.13), int(w3 * 0.88), int(h3 * 0.94)))
    base3 = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    c3_w, c3_h = crop3.size
    scale3 = min(220.0 / c3_w, 220.0 / c3_h)
    resized3 = crop3.resize((int(c3_w * scale3), int(c3_h * scale3)), Image.Resampling.LANCZOS)
    base3.paste(resized3, ((256 - resized3.width) // 2, (256 - resized3.height) // 2))
    base3.save(os.path.join(DOC_IMG, "Monster_3103_WaveHeavy_Idle_Base.png"))
    base3.save(os.path.join(ARTIFACT_DIR, "Monster_3103_WaveHeavy_Idle_Base.png"))

    # 4. 3104 ShieldSentinel: Extract the central shield sentinel figure
    img4 = Image.open(os.path.join(COMFY_OUT, "Monster_3104_ShieldSentinel_00001_.png")).convert("RGBA")
    w4, h4 = img4.size
    crop4 = img4.crop((int(w4 * 0.38), int(h4 * 0.22), int(w4 * 0.62), int(h4 * 0.78)))
    base4 = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    c4_w, c4_h = crop4.size
    scale4 = min(220.0 / c4_w, 220.0 / c4_h)
    resized4 = crop4.resize((int(c4_w * scale4), int(c4_h * scale4)), Image.Resampling.LANCZOS)
    base4.paste(resized4, ((256 - resized4.width) // 2, (256 - resized4.height) // 2))
    base4.save(os.path.join(DOC_IMG, "Monster_3104_ShieldSentinel_Idle_Base.png"))
    base4.save(os.path.join(ARTIFACT_DIR, "Monster_3104_ShieldSentinel_Idle_Base.png"))

    # 5. 3105 OrbitalMarksman: Extract the slender marksman puppet with amber steam core
    img5 = Image.open(os.path.join(COMFY_OUT, "Monster_3105_OrbitalMarksman_00001_.png")).convert("RGBA")
    w5, h5 = img5.size
    crop5 = img5.crop((int(w5 * 0.20), int(h5 * 0.08), int(w5 * 0.80), int(h5 * 0.96)))
    base5 = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    c5_w, c5_h = crop5.size
    scale5 = min(220.0 / c5_w, 220.0 / c5_h)
    resized5 = crop5.resize((int(c5_w * scale5), int(c5_h * scale5)), Image.Resampling.LANCZOS)
    base5.paste(resized5, ((256 - resized5.width) // 2, (256 - resized5.height) // 2))
    base5.save(os.path.join(DOC_IMG, "Monster_3105_OrbitalMarksman_Idle_Base.png"))
    base5.save(os.path.join(ARTIFACT_DIR, "Monster_3105_OrbitalMarksman_Idle_Base.png"))

    print("Successfully extracted all 5 clean idle reference bases!")

if __name__ == "__main__":
    extract_clean_idle_bases()
