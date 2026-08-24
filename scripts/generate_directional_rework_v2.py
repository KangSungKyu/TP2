import os
import math
import hashlib
from PIL import Image, ImageDraw

TARGET_DIR = r"C:\Users\PC\Projects\TP2\doc\ai_orders\pending\2026-08-24_dummy_attack_effects_rework_v2\assets"

# Colors
CYAN_HEAD = (0, 230, 255, 240)        # Bright Cyan for drawing head
CYAN_BODY = (0, 180, 230, 200)
ACTIVE_RED_HEAD = (255, 60, 60, 240)  # Bright Red Active
ACTIVE_RED_CORE = (255, 255, 255, 240)
ORANGE_TAIL = (255, 140, 30, 220)     # Orange for erasing tail
AMBER_DECAY = (255, 100, 30, 140)

def draw_arc_slice(draw, center, radius, a_start, a_end, width, color, core_color=None):
    if abs(a_end - a_start) < 1.0:
        return
    cx, cy = center
    steps = max(10, int(abs(a_end - a_start) / 3))
    pts = []
    for s in range(steps + 1):
        ang = math.radians(a_start + (a_end - a_start) * (s / steps))
        px = cx + radius * math.cos(ang)
        py = cy + radius * math.sin(ang)
        pts.append((px, py))
    for i in range(len(pts) - 1):
        draw.line([pts[i], pts[i+1]], fill=color, width=width)
    if core_color and width > 2:
        for i in range(len(pts) - 1):
            draw.line([pts[i], pts[i+1]], fill=core_color, width=max(1, width - 2))

def draw_line_slice(draw, p_start, p_end, frac_start, frac_end, width, color, core_color=None):
    if frac_end <= frac_start:
        return
    x1, y1 = p_start
    x2, y2 = p_end
    dx, dy = x2 - x1, y2 - y1
    ps = (x1 + dx * frac_start, y1 + dy * frac_start)
    pe = (x1 + dx * frac_end, y1 + dy * frac_end)
    draw.line([ps, pe], fill=color, width=width)
    if core_color and width > 2:
        draw.line([ps, pe], fill=core_color, width=max(1, width - 2))

# 1. Target 1: U3105 P6006 S7002: lower aim Line (128x128, 8 frames -> 1024x128)
# Name: VFX_DummyAttack_U3105_P6006_S7002_Line.png
def gen_v2_u3105_p6006_line():
    sheet = Image.new("RGBA", (1024, 128), (0, 0, 0, 0))
    p_start = (28, 52)
    p_end = (116, 76)
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 2 if f < 3 else (5 if f == 3 else 2)
            draw_line_slice(d, p_start, p_end, f_s, f_e, w, col, core)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 2. Target 2: U3201 P6100 S7012: OverheadSmash Arc (256x256, 8 frames -> 2048x256)
# Name: VFX_DummyAttack_U3201_P6100_S7012_Arc.png
def gen_v2_u3201_p6100_arc():
    sheet = Image.new("RGBA", (2048, 256), (0, 0, 0, 0))
    center = (115, 120)
    radius = 96
    s_ang, e_ang = -115.0, 45.0
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 6 if f < 3 else (12 if f == 3 else 6)
            draw_arc_slice(d, center, radius, a1, a2, w, col, core)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 3. Target 3: U3201 P6102 S7010: Charge DirectedBox (256x256, 8 frames -> 2048x256)
# Name: VFX_DummyAttack_U3201_P6102_S7010_DirectedBox.png
def gen_v2_u3201_p6102_directedbox():
    sheet = Image.new("RGBA", (2048, 256), (0, 0, 0, 0))
    p_start = (65, 128)
    p_end = (185, 128)
    intervals = [
        (0.0, 0.25), (0.0, 0.50), (0.0, 0.80), (0.0, 1.0),
        (0.25, 1.0), (0.55, 1.0), (0.85, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            x1, y1 = p_start
            x2, y2 = p_end
            cx = x1 + (x2 - x1) * ((f_s + f_e) / 2)
            cur_w = (x2 - x1) * (f_e - f_s) + 20
            cur_h = 45 + 15 * (1.0 - abs(f - 3) / 4)
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            
            hw, hh = cur_w / 2, cur_h / 2
            bbox = [cx - hw, y1 - hh, cx + hw, y1 + hh]
            d.rectangle(bbox, fill=(col[0], col[1], col[2], 60), outline=col, width=4)
            if core:
                d.line([(cx - hw + 10, y1), (cx + hw - 10, y1)], fill=core, width=3)
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 4. Target 4: U3001 S7003: 2-Hit Combo Arc (128x128, 16 frames -> 2048x128)
# Name: VFX_DummyAttack_U3001_PNA_S7003_Arc.png
# Hit 1 (f0-f7): Complete 8-frame Draw 1-4 (s=-65, e=45) / Erase 5-8
# Hit 2 (f8-f15): Complete 8-frame Draw 9-12 (s=95, e=-45) / Erase 13-16
def gen_v2_u3001_s7003_16f():
    sheet = Image.new("RGBA", (2048, 128), (0, 0, 0, 0))
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    # Hit 1 (Horizontal Down-Slash)
    center1 = (58, 64)
    radius1 = 44
    s1, e1 = -65.0, 45.0
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s1 + (e1 - s1) * f_s
            a2 = s1 + (e1 - s1) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 3 if f < 3 else (5 if f == 3 else 3)
            draw_arc_slice(d, center1, radius1, a1, a2, w, col, core)
        sheet.paste(frame, (f * 128, 0))

    # Hit 2 (Reverse Vertical Upswing)
    center2 = (62, 66)
    radius2 = 46
    s2, e2 = 95.0, -45.0
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s2 + (e2 - s2) * f_s
            a2 = s2 + (e2 - s2) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 3 if f < 3 else (5 if f == 3 else 3)
            draw_arc_slice(d, center2, radius2, a1, a2, w, col, core)
        sheet.paste(frame, ((8 + f) * 128, 0))

    return sheet

# 5. Target 5: U3201 P6101 S7011: Down->Upswing Arc Combo (256x256, 16 frames -> 4096x256)
# Name: VFX_DummyAttack_U3201_P6101_S7011_Arc.png
# Hit 1 (f0-f7): Complete 8-frame Draw 1-4 (s=-105, e=35) / Erase 5-8
# Hit 2 (f8-f15): Complete 8-frame Draw 9-12 (s=90, e=-40) / Erase 13-16
def gen_v2_u3201_p6101_16f():
    sheet = Image.new("RGBA", (4096, 256), (0, 0, 0, 0))
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    # Hit 1: Downward Cleave
    center1 = (115, 125)
    radius1 = 92
    s1, e1 = -105.0, 35.0
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s1 + (e1 - s1) * f_s
            a2 = s1 + (e1 - s1) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 6 if f < 3 else (12 if f == 3 else 6)
            draw_arc_slice(d, center1, radius1, a1, a2, w, col, core)
        sheet.paste(frame, (f * 256, 0))

    # Hit 2: Upswing Cleave
    center2 = (120, 130)
    radius2 = 94
    s2, e2 = 90.0, -40.0
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s2 + (e2 - s2) * f_s
            a2 = s2 + (e2 - s2) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 6 if f < 3 else (12 if f == 3 else 6)
            draw_arc_slice(d, center2, radius2, a1, a2, w, col, core)
        sheet.paste(frame, ((8 + f) * 256, 0))

    return sheet

def main():
    os.makedirs(TARGET_DIR, exist_ok=True)
    v2_targets = {
        "VFX_DummyAttack_U3105_P6006_S7002_Line.png": gen_v2_u3105_p6006_line,
        "VFX_DummyAttack_U3201_P6100_S7012_Arc.png": gen_v2_u3201_p6100_arc,
        "VFX_DummyAttack_U3201_P6102_S7010_DirectedBox.png": gen_v2_u3201_p6102_directedbox,
        "VFX_DummyAttack_U3001_PNA_S7003_Arc.png": gen_v2_u3001_s7003_16f,
        "VFX_DummyAttack_U3201_P6101_S7011_Arc.png": gen_v2_u3201_p6101_16f,
    }

    print("=== Generating 5 Rework V2 Dummy Attack Effect Sheets ===")
    for filename, fn in v2_targets.items():
        img = fn()
        out_path = os.path.join(TARGET_DIR, filename)
        img.save(out_path, format="PNG")
        with open(out_path, "rb") as f:
            data = f.read()
            sha = hashlib.sha256(data).hexdigest()
        print(f"Generated V2: {filename} ({img.size[0]}x{img.size[1]}) - Size: {len(data)} bytes - SHA256: {sha}")

if __name__ == "__main__":
    main()
