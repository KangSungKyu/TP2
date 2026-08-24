import os
import math
import hashlib
from PIL import Image, ImageDraw

TARGET_DIR = r"C:\Users\PC\Projects\TP2\doc\ai_orders\pending\2026-08-24_dummy_attack_effects_rework\assets"

def create_strip(cell_size, num_frames=8):
    return Image.new("RGBA", (cell_size * num_frames, cell_size), (0, 0, 0, 0))

# Standard Colors
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

# 1. Player U3001 S7001: ReverseVerticalUpswing (128px)
# Motion: Rear low (angle +110 deg) -> Head (+40 deg) -> Front high (-40 deg)
# Start: +110 deg -> End: -40 deg (Delta: -150 deg)
def gen_rework_u3001_s7001():
    sheet = create_strip(128)
    center = (60, 68)
    radius = 46
    s_ang, e_ang = 100.0, -45.0
    
    # 8-Frame Directional Draw (f1-f4) then Erase (f5-f8)
    # f1: 0..20%, f2: 0..45%, f3: 0..75%, f4: 0..100%, f5: 20%..100%, f6: 50%..100%, f7: 80%..100%, f8: empty
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 3 if f < 3 else (5 if f == 3 else 3)
            draw_arc_slice(d, center, radius, a1, a2, w, col, core)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 2. Player U3001 S7003: 2-Hit Combo Arc (128px)
# Hit 1: f1 draw (0..50%), f2 impact (0..100%), f3 erase (50%..100%)
# Hit 2: f4 draw (0..50%), f5 impact (0..100%), f6 erase (40%..100%), f7 erase (80%..100%), f8 empty
def gen_rework_u3001_s7003():
    sheet = create_strip(128)
    # Hit 1: Horizontal Down-Slash (s=-60, e=45)
    # Hit 2: Reverse Upswing (s=90, e=-50)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f == 0: # Hit 1 Draw
            draw_arc_slice(d, (58, 64), 42, -60, -10, 3, CYAN_BODY, CYAN_HEAD)
        elif f == 1: # Hit 1 Impact 100%
            draw_arc_slice(d, (58, 64), 42, -60, 45, 5, ACTIVE_RED_HEAD, ACTIVE_RED_CORE)
        elif f == 2: # Hit 1 Erase tail
            draw_arc_slice(d, (58, 64), 42, 0, 45, 3, ORANGE_TAIL)
        elif f == 3: # Hit 2 Draw start
            draw_arc_slice(d, (62, 66), 46, 90, 20, 3, CYAN_BODY, CYAN_HEAD)
        elif f == 4: # Hit 2 Impact 100%
            draw_arc_slice(d, (62, 66), 46, 90, -50, 5, ACTIVE_RED_HEAD, ACTIVE_RED_CORE)
        elif f == 5: # Hit 2 Erase tail 40%
            draw_arc_slice(d, (62, 66), 46, 30, -50, 4, ORANGE_TAIL)
        elif f == 6: # Hit 2 Erase tail 80%
            draw_arc_slice(d, (62, 66), 46, -25, -50, 2, AMBER_DECAY)
        # f == 7 empty
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 3. Monster 3103 P6001 S7001: VerticalDown Heavy Slash (128px)
# Start: Overhead (-110 deg) -> End: Front ground (+40 deg)
def gen_rework_u3103_p6001():
    sheet = create_strip(128)
    center = (56, 60)
    radius = 48
    s_ang, e_ang = -110.0, 40.0
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 4 if f < 3 else (7 if f == 3 else 4)
            draw_arc_slice(d, center, radius, a1, a2, w, col, core)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 4. Monster 3104 P6004 S7001: VerticalDown Weapon Slam (128px)
# Start: Overhead (-100 deg) -> End: Front ground (+35 deg)
def gen_rework_u3104_p6004():
    sheet = create_strip(128)
    center = (58, 62)
    radius = 44
    s_ang, e_ang = -100.0, 35.0
    intervals = [
        (0.0, 0.20), (0.0, 0.45), (0.0, 0.75), (0.0, 1.0),
        (0.20, 1.0), (0.50, 1.0), (0.80, 1.0), (1.0, 1.0)
    ]
    for f, (f_s, f_e) in enumerate(intervals):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f < 7:
            a1 = s_ang + (e_ang - s_ang) * f_s
            a2 = s_ang + (e_ang - s_ang) * f_e
            col = CYAN_BODY if f < 3 else (ACTIVE_RED_HEAD if f == 3 else ORANGE_TAIL)
            core = CYAN_HEAD if f < 3 else (ACTIVE_RED_CORE if f == 3 else None)
            w = 3 if f < 3 else (6 if f == 3 else 3)
            draw_arc_slice(d, center, radius, a1, a2, w, col, core)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 5. Monster 3105 P6005 S7002: Crossbow Aim & Line Shot (128px)
# Motion: Muzzle (x=30, y=64) -> Target (x=115, y=64)
def gen_rework_u3105_p6005():
    sheet = create_strip(128)
    p_start = (30, 64)
    p_end = (115, 64)
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
            w = 2 if f < 3 else (4 if f == 3 else 2)
            draw_line_slice(d, p_start, p_end, f_s, f_e, w, col, core)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 6. Monster 3105 P6006 S7002: Lower Aim Line (128px)
# Motion: Lower aim trajectory (x=30, y=55) -> (x=115, y=75)
def gen_rework_u3105_p6006():
    sheet = create_strip(128)
    p_start = (30, 55)
    p_end = (115, 75)
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

# 7. Boss 3201 P6100 S7012: OverheadSmash Arc (256px)
# Start: High overhead (-115 deg) -> End: Front ground (+45 deg)
def gen_rework_u3201_p6100():
    sheet = create_strip(256)
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

# 8. Boss 3201 P6101 S7011: Down->Upswing Combo Arc (256px)
# Hit 1: Downward (s=-100, e=30) / Hit 2: Upswing (s=90, e=-40)
def gen_rework_u3201_p6101():
    sheet = create_strip(256)
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        if f == 0: # Hit 1 Draw start
            draw_arc_slice(d, (115, 125), 88, -100, -30, 6, CYAN_BODY, CYAN_HEAD)
        elif f == 1: # Hit 1 Impact
            draw_arc_slice(d, (115, 125), 88, -100, 30, 10, ACTIVE_RED_HEAD, ACTIVE_RED_CORE)
        elif f == 2: # Hit 1 Erase tail
            draw_arc_slice(d, (115, 125), 88, -20, 30, 5, ORANGE_TAIL)
        elif f == 3: # Hit 2 Draw start
            draw_arc_slice(d, (120, 130), 92, 90, 20, 6, CYAN_BODY, CYAN_HEAD)
        elif f == 4: # Hit 2 Impact
            draw_arc_slice(d, (120, 130), 92, 90, -40, 12, ACTIVE_RED_HEAD, ACTIVE_RED_CORE)
        elif f == 5: # Hit 2 Erase 40%
            draw_arc_slice(d, (120, 130), 92, 35, -40, 7, ORANGE_TAIL)
        elif f == 6: # Hit 2 Erase 80%
            draw_arc_slice(d, (120, 130), 92, -15, -40, 4, AMBER_DECAY)
        # f == 7 empty
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 9. Boss 3201 P6102 S7010: Charge DirectedBox (256px)
# Motion: Greatsword Charge advance from (70, 128) -> (175, 128), head expands then tail erases
def gen_rework_u3201_p6102():
    sheet = create_strip(256)
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
            
            # Draw directed box slice
            hw, hh = cur_w / 2, cur_h / 2
            bbox = [cx - hw, y1 - hh, cx + hw, y1 + hh]
            d.rectangle(bbox, fill=(col[0], col[1], col[2], 60), outline=col, width=4)
            if core:
                d.line([(cx - hw + 10, y1), (cx + hw - 10, y1)], fill=core, width=3)
        sheet.paste(frame, (f * 256, 0))
    return sheet

def main():
    os.makedirs(TARGET_DIR, exist_ok=True)
    reworks = {
        "VFX_DummyAttack_U3001_PNA_S7001_Arc.png": gen_rework_u3001_s7001,
        "VFX_DummyAttack_U3001_PNA_S7003_Arc.png": gen_rework_u3001_s7003,
        "VFX_DummyAttack_U3103_P6001_S7001_Arc.png": gen_rework_u3103_p6001,
        "VFX_DummyAttack_U3104_P6004_S7001_Arc.png": gen_rework_u3104_p6004,
        "VFX_DummyAttack_U3105_P6005_S7002_Line.png": gen_rework_u3105_p6005,
        "VFX_DummyAttack_U3105_P6006_S7002_Ring.png": gen_rework_u3105_p6006,
        "VFX_DummyAttack_U3201_P6100_S7012_DirectedBox.png": gen_rework_u3201_p6100,
        "VFX_DummyAttack_U3201_P6101_S7011_Arc.png": gen_rework_u3201_p6101,
        "VFX_DummyAttack_U3201_P6102_S7010_Arc.png": gen_rework_u3201_p6102,
    }

    print("=== Generating 9 Directional Draw-then-Erase Rework Attack Effect Sheets ===")
    for filename, fn in reworks.items():
        img = fn()
        out_path = os.path.join(TARGET_DIR, filename)
        img.save(out_path, format="PNG")
        with open(out_path, "rb") as f:
            data = f.read()
            sha = hashlib.sha256(data).hexdigest()
        print(f"Generated: {filename} ({img.size[0]}x{img.size[1]}) - Size: {len(data)} bytes - SHA256: {sha}")

if __name__ == "__main__":
    main()
