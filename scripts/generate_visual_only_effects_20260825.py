import os
import math
import hashlib
from PIL import Image, ImageDraw

TARGET_DIR = r"C:\Users\PC\Projects\TP2\doc\ai_orders\pending\2026-08-25_skill_attack_effect_visual_only_rework\assets"

def create_strip(cell_size, num_frames=8):
    return Image.new("RGBA", (cell_size * num_frames, cell_size), (0, 0, 0, 0))

# Visual Color Palettes (Organic Pure Visuals - No Debug Colliders)
# Palette 1: Sharp Cyan/Steel Blade Trail (Player, Spear, Fast slashes)
BLADE_CYAN_CORE = (230, 255, 255, 255)
BLADE_CYAN_GLOW = (0, 220, 255, 220)
BLADE_CYAN_EDGE = (0, 140, 220, 160)

# Palette 2: Fiery / Crimson Active Strike (Impacts, Boss Greatsword, Heavy Slashes)
FIRE_CORE = (255, 255, 240, 255)
FIRE_GOLD = (255, 210, 40, 230)
FIRE_RED = (255, 60, 40, 220)
FIRE_TRAIL = (200, 40, 20, 150)

# Palette 3: Shadow / Violet Energy (Shadow Stalker, Piercing)
SHADOW_CORE = (255, 240, 255, 255)
SHADOW_VIOLET = (190, 60, 255, 220)
SHADOW_DARK = (100, 20, 180, 160)

# Palette 4: Amber / Shockwave Shock (Torso Ram, Shield Slam, Ground Fissure)
AMBER_CORE = (255, 250, 220, 255)
AMBER_BRIGHT = (255, 180, 30, 230)
AMBER_DECAY = (220, 90, 20, 140)

def draw_tapered_arc(draw, center, radius, a_start, a_end, base_w, c_core, c_glow, c_edge):
    if abs(a_end - a_start) < 0.5:
        return
    cx, cy = center
    steps = max(12, int(abs(a_end - a_start) / 2.5))
    pts = []
    for s in range(steps + 1):
        t = s / steps
        ang = math.radians(a_start + (a_end - a_start) * t)
        px = cx + radius * math.cos(ang)
        py = cy + radius * math.sin(ang)
        pts.append((px, py, t))
    
    # Outer glow
    for i in range(len(pts) - 1):
        w = max(1, int(base_w * (1.2 - 0.5 * pts[i][2])))
        draw.line([pts[i][:2], pts[i+1][:2]], fill=c_edge, width=w + 3)
    # Inner glow
    for i in range(len(pts) - 1):
        w = max(1, int(base_w * (1.0 - 0.4 * pts[i][2])))
        draw.line([pts[i][:2], pts[i+1][:2]], fill=c_glow, width=w + 1)
    # Bright core
    for i in range(len(pts) - 1):
        w = max(1, int(base_w * 0.5 * (1.0 - 0.3 * pts[i][2])))
        draw.line([pts[i][:2], pts[i+1][:2]], fill=c_core, width=w)

def draw_tapered_line(draw, p_start, p_end, frac_start, frac_end, base_w, c_core, c_glow, c_edge):
    if frac_end <= frac_start:
        return
    x1, y1 = p_start
    x2, y2 = p_end
    dx, dy = x2 - x1, y2 - y1
    ps = (x1 + dx * frac_start, y1 + dy * frac_start)
    pe = (x1 + dx * frac_end, y1 + dy * frac_end)
    # Outer
    draw.line([ps, pe], fill=c_edge, width=base_w + 3)
    # Inner
    draw.line([ps, pe], fill=c_glow, width=base_w + 1)
    # Core
    draw.line([ps, pe], fill=c_core, width=max(1, base_w - 1))

def draw_shock_ring(draw, center, radius, base_w, c_core, c_glow, c_edge):
    if radius <= 1:
        return
    cx, cy = center
    bbox_out = [cx - radius - 1, cy - radius - 1, cx + radius + 1, cy + radius + 1]
    bbox_mid = [cx - radius, cy - radius, cx + radius, cy + radius]
    draw.ellipse(bbox_out, outline=c_edge, width=base_w + 2)
    draw.ellipse(bbox_mid, outline=c_glow, width=base_w)
    if base_w > 2:
        draw.ellipse(bbox_mid, outline=c_core, width=max(1, base_w - 2))

# 8-Frame Directional Progress Intervals: Draw 1-4, Erase 5-8
STD_INTERVALS = [
    (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
    (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
]

# 1. 2026-08-25_U3001_E8014_skill_effect.png: Player S7001 ReverseVerticalUpswing Arc (128x128)
def gen_e8014():
    sheet = create_strip(128)
    center = (60, 68)
    radius = 45
    s_ang, e_ang = 100.0, -45.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (5 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 2. 2026-08-25_U3101_E8015_skill_effect.png: Monster 3101 P6001 S7001 Spear Thrust Line (128x128)
def gen_e8015():
    sheet = create_strip(128)
    p_start = (25, 64)
    p_end = (115, 64)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 3 if f < 3 else (5 if f == 3 else 2)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 3. 2026-08-25_U3102_E8016_skill_effect.png: Monster 3102 P6008 S7005 Shadow Charging Thrust Line (128x128)
def gen_e8016():
    sheet = create_strip(128)
    p_start = (30, 64)
    p_end = (118, 64)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 3 if f < 3 else (6 if f == 3 else 3)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, SHADOW_CORE, SHADOW_VIOLET, SHADOW_DARK)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 4. 2026-08-25_U3102_E8017_skill_effect.png: Monster 3102 P6009 S7006 Shadow Barrage Slash Arc (128x128)
def gen_e8017():
    sheet = create_strip(128)
    center = (60, 64)
    radius = 44
    s_ang, e_ang = -70.0, 50.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (5 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, SHADOW_CORE, SHADOW_VIOLET, SHADOW_DARK)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 5. 2026-08-25_U3103_E8018_skill_effect.png: Monster 3103 P6001 S7001 VerticalDown Heavy Slash Arc (128x128)
def gen_e8018():
    sheet = create_strip(128)
    center = (56, 60)
    radius = 48
    s_ang, e_ang = -110.0, 40.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 4 if f < 3 else (7 if f == 3 else 4)
            draw_tapered_arc(d, center, radius, a1, a2, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 6. 2026-08-25_U3103_E8019_skill_effect.png: Monster 3103 P6010 S7007 Torso Ram Surge (128x128)
def gen_e8019():
    sheet = create_strip(128)
    p_start = (35, 64)
    p_end = (112, 64)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 5 if f < 3 else (9 if f == 3 else 4)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, AMBER_CORE, AMBER_BRIGHT, AMBER_DECAY)
            # Side energy wisps
            if f in (2, 3, 4):
                draw_tapered_arc(d, (75, 64), 26, -50, 50, 3, AMBER_CORE, AMBER_BRIGHT, AMBER_DECAY)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 7. 2026-08-25_U3104_E8020_skill_effect.png: Monster 3104 P6003 S7001 Shield Bash Impact Burst (128x128)
def gen_e8020():
    sheet = create_strip(128)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            # Vertical blunt impact crest
            cx = 55 + int(35 * f_e)
            rad = 18 + int(12 * (1.0 - abs(f - 3)/4))
            draw_tapered_arc(d, (cx - 15, 64), rad, -55, 55, 4, AMBER_CORE, AMBER_BRIGHT, AMBER_DECAY)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 8. 2026-08-25_U3104_E8021_skill_effect.png: Monster 3104 P6004 S7001 Vertical Downward Slam Arc (128x128)
def gen_e8021():
    sheet = create_strip(128)
    center = (58, 62)
    radius = 44
    s_ang, e_ang = -100.0, 35.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (6 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, AMBER_CORE, AMBER_BRIGHT, AMBER_DECAY)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 9. 2026-08-25_U3105_E8022_skill_effect.png: Monster 3105 P6005 S7002 Single Crossbow Shot Line (128x128)
def gen_e8022():
    sheet = create_strip(128)
    p_start = (28, 64)
    p_end = (116, 64)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 2 if f < 3 else (4 if f == 3 else 2)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 10. 2026-08-25_U3201_E8023_skill_effect.png: Boss 3201 P6103 S7013 Shockwave Radial Ring (256x256)
def gen_e8023():
    sheet = create_strip(256)
    center = (128, 128)
    radii = [18, 40, 70, 98, 114, 120, 124, 128]
    widths = [3, 5, 8, 10, 7, 4, 2, 0]
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            draw_shock_ring(d, center, radii[f], widths[f], FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 11. 2026-08-25_U3105_E8024_skill_effect.png: Monster 3105 P6006 S7002 Lower Aim Shot Line (128x128)
def gen_e8024():
    sheet = create_strip(128)
    p_start = (28, 52)
    p_end = (116, 76)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 2 if f < 3 else (5 if f == 3 else 2)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 12. 2026-08-25_U3201_E8025_skill_effect.png: Boss 3201 P6100 S7012 OverheadSmash Greatsword Arc (256x256)
def gen_e8025():
    sheet = create_strip(256)
    center = (115, 120)
    radius = 96
    s_ang, e_ang = -115.0, 45.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 6 if f < 3 else (12 if f == 3 else 6)
            draw_tapered_arc(d, center, radius, a1, a2, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 13. 2026-08-25_U3201_E8026_skill_effect.png: Boss 3201 P6102 S7010 Greatsword Charge Surge Line/Burst (256x256)
def gen_e8026():
    sheet = create_strip(256)
    p_start = (65, 128)
    p_end = (195, 128)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 7 if f < 3 else (14 if f == 3 else 7)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
            # Frontal slash wisps
            if f in (2, 3, 4):
                draw_tapered_arc(d, (135, 128), 50, -55, 55, 5, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 14. 2026-08-25_U3001_E8027_skill_effect.png: Player S7003 Hit 1 Downward Cleave Arc (128x128, 8 frames)
def gen_e8027():
    sheet = create_strip(128)
    center = (58, 64)
    radius = 44
    s_ang, e_ang = -65.0, 45.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (5 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 15. 2026-08-25_U3001_E8028_skill_effect.png: Player S7003 Hit 2 Reverse Upswing Arc (128x128, 8 frames)
def gen_e8028():
    sheet = create_strip(128)
    center = (62, 66)
    radius = 46
    s_ang, e_ang = 95.0, -45.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (5 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 16. 2026-08-25_U3201_E8029_skill_effect.png: Boss 3201 P6101 S7011 Hit 1 Downward Cleave Arc (256x256, 8 frames)
def gen_e8029():
    sheet = create_strip(256)
    center = (115, 125)
    radius = 92
    s_ang, e_ang = -105.0, 35.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 6 if f < 3 else (12 if f == 3 else 6)
            draw_tapered_arc(d, center, radius, a1, a2, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 17. 2026-08-25_U3201_E8030_skill_effect.png: Boss 3201 P6101 S7011 Hit 2 Upswing Cleave Arc (256x256, 8 frames)
def gen_e8030():
    sheet = create_strip(256)
    center = (120, 130)
    radius = 94
    s_ang, e_ang = 90.0, -40.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 6 if f < 3 else (12 if f == 3 else 6)
            draw_tapered_arc(d, center, radius, a1, a2, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 18. 2026-08-25_U3106_E8031_skill_effect.png: Monster 3106 P6007 S7003 Special Slash Arc (128x128, 8 frames)
def gen_e8031():
    sheet = create_strip(128)
    center = (60, 64)
    radius = 45
    s_ang, e_ang = -80.0, 45.0
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            bw = 3 if f < 3 else (6 if f == 3 else 3)
            draw_tapered_arc(d, center, radius, a1, a2, bw, FIRE_CORE, FIRE_GOLD, FIRE_RED)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 19. 2026-08-25_U3001_E8032_skill_effect.png: Player S7002 Piercing Thrust / Heavy Slash (128x128, 8 frames)
def gen_e8032():
    sheet = create_strip(128)
    p_start = (24, 64)
    p_end = (118, 64)
    for f, (f_s, f_e) in enumerate(STD_INTERVALS):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            bw = 3 if f < 3 else (6 if f == 3 else 3)
            draw_tapered_line(d, p_start, p_end, f_s, f_e, bw, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
            # Subtle radial spark at tip
            if f in (2, 3, 4):
                draw_tapered_arc(d, (100, 64), 16, -45, 45, 2, BLADE_CYAN_CORE, BLADE_CYAN_GLOW, BLADE_CYAN_EDGE)
        sheet.paste(frame, (f * 128, 0))
    return sheet

def main():
    os.makedirs(TARGET_DIR, exist_ok=True)
    effects = {
        "2026-08-25_U3001_E8014_skill_effect.png": (3001, 8014, gen_e8014),
        "2026-08-25_U3101_E8015_skill_effect.png": (3101, 8015, gen_e8015),
        "2026-08-25_U3102_E8016_skill_effect.png": (3102, 8016, gen_e8016),
        "2026-08-25_U3102_E8017_skill_effect.png": (3102, 8017, gen_e8017),
        "2026-08-25_U3103_E8018_skill_effect.png": (3103, 8018, gen_e8018),
        "2026-08-25_U3103_E8019_skill_effect.png": (3103, 8019, gen_e8019),
        "2026-08-25_U3104_E8020_skill_effect.png": (3104, 8020, gen_e8020),
        "2026-08-25_U3104_E8021_skill_effect.png": (3104, 8021, gen_e8021),
        "2026-08-25_U3105_E8022_skill_effect.png": (3105, 8022, gen_e8022),
        "2026-08-25_U3201_E8023_skill_effect.png": (3201, 8023, gen_e8023),
        "2026-08-25_U3105_E8024_skill_effect.png": (3105, 8024, gen_e8024),
        "2026-08-25_U3201_E8025_skill_effect.png": (3201, 8025, gen_e8025),
        "2026-08-25_U3201_E8026_skill_effect.png": (3201, 8026, gen_e8026),
        "2026-08-25_U3001_E8027_skill_effect.png": (3001, 8027, gen_e8027),
        "2026-08-25_U3001_E8028_skill_effect.png": (3001, 8028, gen_e8028),
        "2026-08-25_U3201_E8029_skill_effect.png": (3201, 8029, gen_e8029),
        "2026-08-25_U3201_E8030_skill_effect.png": (3201, 8030, gen_e8030),
        "2026-08-25_U3106_E8031_skill_effect.png": (3106, 8031, gen_e8031),
        "2026-08-25_U3001_E8032_skill_effect.png": (3001, 8032, gen_e8032),
    }

    print("=== Generating 19 Visual-Only Skill Attack Effect PNG Sheets ===")
    for filename, (uid, eid, fn) in effects.items():
        img = fn()
        out_path = os.path.join(TARGET_DIR, filename)
        img.save(out_path, format="PNG")
        with open(out_path, "rb") as f:
            data = f.read()
            sha = hashlib.sha256(data).hexdigest()
        print(f"Generated: {filename} ({img.size[0]}x{img.size[1]}) - Size: {len(data)} bytes - SHA256: {sha}")

if __name__ == "__main__":
    main()
