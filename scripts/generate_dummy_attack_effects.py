import os
import math
from PIL import Image, ImageDraw

OUTPUT_DIR = r"C:\Users\PC\Projects\TP2\doc\ai_orders\completed\2026-08-24_dummy_attack_effects\assets"

def create_strip(cell_size, num_frames=8):
    w = cell_size * num_frames
    h = cell_size
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))

# Colors
TELEGRAPH_COLOR = (255, 204, 25, 75)     # Yellow low alpha (1.0, 0.8, 0.1, 0.25)
ACTIVE_CORE = (255, 255, 255, 240)       # White core for crisp pixel art
ACTIVE_RED = (255, 50, 50, 200)          # Red active (1.0, 0.2, 0.2, 0.8)
ACTIVE_ORANGE = (255, 140, 30, 180)      # Orange accent
RECOVERY_COLOR = (255, 100, 30, 80)      # Decaying amber (low alpha)

def draw_arc(draw, center, radius, start_angle, end_angle, width, color):
    cx, cy = center
    # Draw arc as connected lines or thick pie arc
    steps = 30
    points = []
    for s in range(steps + 1):
        ang = math.radians(start_angle + (end_angle - start_angle) * (s / steps))
        px = cx + radius * math.cos(ang)
        py = cy + radius * math.sin(ang)
        points.append((px, py))
    for i in range(len(points) - 1):
        draw.line([points[i], points[i+1]], fill=color, width=width)

def draw_ring(draw, center, radius, width, color):
    cx, cy = center
    bbox = [cx - radius, cy - radius, cx + radius, cy + radius]
    draw.ellipse(bbox, outline=color, width=width)

def draw_directed_box(draw, center, size, angle_deg, color, fill_color=None):
    cx, cy = center
    w, h = size
    # Create rotated polygon points
    hw, hh = w / 2, h / 2
    local_pts = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
    rad = math.radians(angle_deg)
    cos_a, sin_a = math.cos(rad), math.sin(rad)
    world_pts = []
    for lx, ly in local_pts:
        wx = cx + (lx * cos_a - ly * sin_a)
        wy = cy + (lx * sin_a + ly * cos_a)
        world_pts.append((wx, wy))
    if fill_color:
        draw.polygon(world_pts, fill=fill_color, outline=color)
    else:
        draw.polygon(world_pts, outline=color)

def draw_line(draw, start, end, width, color):
    draw.line([start, end], fill=color, width=width)

# 1. Player U3001 S7001 Arc (Single Hit 128px)
def gen_u3001_s7001():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 64, 64
        if f == 0: # Telegraph
            draw_arc(d, (cx - 10, cy), 35, -45, 45, 2, TELEGRAPH_COLOR)
        elif f == 1: # Pre-swing
            draw_arc(d, (cx - 5, cy), 40, -55, 55, 3, (255, 180, 50, 140))
        elif f == 2: # Active Impact Start
            draw_arc(d, (cx + 5, cy), 45, -60, 60, 5, ACTIVE_RED)
            draw_arc(d, (cx + 5, cy), 45, -50, 50, 2, ACTIVE_CORE)
        elif f == 3: # Active Peak
            draw_arc(d, (cx + 10, cy), 48, -70, 70, 6, ACTIVE_RED)
            draw_arc(d, (cx + 10, cy), 48, -60, 60, 3, ACTIVE_CORE)
        elif f == 4: # Active Followthrough
            draw_arc(d, (cx + 12, cy), 50, -75, 75, 4, ACTIVE_ORANGE)
            draw_arc(d, (cx + 12, cy), 50, -65, 65, 2, ACTIVE_CORE)
        elif f == 5: # Recovery 1
            draw_arc(d, (cx + 14, cy), 50, -60, 60, 3, RECOVERY_COLOR)
        elif f == 6: # Recovery 2
            draw_arc(d, (cx + 15, cy), 50, -40, 40, 2, (255, 100, 30, 40))
        # f == 7 empty / dissipate
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 2. Player U3001 S7003 Arc (2-Hit Combo 128px)
def gen_u3001_s7003():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 64, 64
        if f == 0: # Hit 1 Telegraph
            draw_arc(d, (cx - 5, cy), 38, -40, 40, 2, TELEGRAPH_COLOR)
        elif f == 1: # Hit 1 Peak
            draw_arc(d, (cx + 8, cy), 44, -60, 60, 5, ACTIVE_RED)
            draw_arc(d, (cx + 8, cy), 44, -50, 50, 2, ACTIVE_CORE)
        elif f == 2: # Hit 1 Followthrough
            draw_arc(d, (cx + 10, cy), 46, -65, 65, 3, ACTIVE_ORANGE)
        elif f == 3: # Hit 2 Telegraph / Transition
            draw_arc(d, (cx, cy), 40, -30, 70, 2, (255, 200, 30, 100))
        elif f == 4: # Hit 2 Impact (Upward Slash)
            draw_arc(d, (cx + 10, cy - 5), 48, -80, 50, 6, ACTIVE_RED)
            draw_arc(d, (cx + 10, cy - 5), 48, -70, 40, 3, ACTIVE_CORE)
        elif f == 5: # Hit 2 Peak Extension
            draw_arc(d, (cx + 12, cy - 8), 52, -85, 45, 5, ACTIVE_ORANGE)
            draw_arc(d, (cx + 12, cy - 8), 52, -75, 35, 2, ACTIVE_CORE)
        elif f == 6: # Recovery
            draw_arc(d, (cx + 14, cy - 8), 52, -60, 30, 2, RECOVERY_COLOR)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 3. Monster 3101 P6001 S7001 Line (Spear Thrust 128px)
def gen_u3101_p6001():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Telegraph Aim Line
            draw_line(d, (30, cy), (80, cy), 2, TELEGRAPH_COLOR)
        elif f == 1: # Pre-thrust
            draw_line(d, (35, cy), (95, cy), 3, (255, 180, 50, 140))
        elif f == 2: # Full Thrust Peak
            draw_line(d, (20, cy), (115, cy), 6, ACTIVE_RED)
            draw_line(d, (40, cy), (115, cy), 2, ACTIVE_CORE)
            # Spear tip spark
            draw_directed_box(d, (115, cy), (8, 8), 45, ACTIVE_CORE, ACTIVE_RED)
        elif f == 3: # Extension Hold
            draw_line(d, (25, cy), (120, cy), 5, ACTIVE_RED)
            draw_line(d, (50, cy), (120, cy), 2, ACTIVE_CORE)
        elif f == 4: # Piercing shockwave ring
            draw_line(d, (35, cy), (118, cy), 3, ACTIVE_ORANGE)
            draw_ring(d, (118, cy), 6, 2, (255, 200, 50, 150))
        elif f == 5: # Retraction 1
            draw_line(d, (30, cy), (95, cy), 2, RECOVERY_COLOR)
        elif f == 6: # Retraction 2
            draw_line(d, (25, cy), (70, cy), 1, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 4. Monster 3102 P6008 S7005 Line (Charging Thrust 128px)
def gen_u3102_p6008():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Charge telegraph
            draw_directed_box(d, (50, cy), (40, 20), 0, TELEGRAPH_COLOR)
        elif f == 1: # Surge forward
            draw_directed_box(d, (60, cy), (50, 22), 0, (255, 180, 50, 140))
        elif f == 2: # Impact Thrust Peak
            draw_line(d, (20, cy), (115, cy), 6, ACTIVE_RED)
            draw_directed_box(d, (80, cy), (60, 26), 0, ACTIVE_RED, (255, 50, 50, 80))
            draw_line(d, (50, cy), (115, cy), 2, ACTIVE_CORE)
        elif f == 3: # Peak Extension
            draw_line(d, (30, cy), (118, cy), 5, ACTIVE_ORANGE)
            draw_directed_box(d, (85, cy), (55, 22), 0, ACTIVE_ORANGE)
        elif f == 4: # Residual trail
            draw_line(d, (40, cy), (110, cy), 3, RECOVERY_COLOR)
        elif f == 5: # Recovery
            draw_line(d, (50, cy), (95, cy), 2, (255, 100, 30, 50))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 5. Monster 3102 P6009 S7006 DirectedBox (Barrage 2-Hit 128px)
def gen_u3102_p6009():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Hit 1 Telegraph
            draw_directed_box(d, (50, cy - 10), (35, 16), -10, TELEGRAPH_COLOR)
        elif f == 1: # Hit 1 Active Strike
            draw_directed_box(d, (75, cy - 10), (50, 22), -10, ACTIVE_RED, (255, 50, 50, 100))
            draw_line(d, (40, cy - 10), (105, cy - 15), 2, ACTIVE_CORE)
        elif f == 2: # Hit 1 Followthrough / Hit 2 Prep
            draw_directed_box(d, (80, cy - 10), (45, 18), -10, ACTIVE_ORANGE)
            draw_directed_box(d, (50, cy + 10), (35, 16), 10, TELEGRAPH_COLOR)
        elif f == 3: # Hit 2 Active Strike
            draw_directed_box(d, (80, cy + 10), (55, 24), 10, ACTIVE_RED, (255, 50, 50, 120))
            draw_line(d, (45, cy + 10), (115, cy + 15), 3, ACTIVE_CORE)
        elif f == 4: # Hit 2 Peak Hold
            draw_directed_box(d, (85, cy + 10), (50, 20), 10, ACTIVE_ORANGE)
            draw_line(d, (50, cy + 10), (115, cy + 15), 2, ACTIVE_CORE)
        elif f == 5: # Recovery 1
            draw_directed_box(d, (85, cy + 5), (40, 16), 5, RECOVERY_COLOR)
        elif f == 6: # Recovery 2
            draw_directed_box(d, (80, cy), (30, 12), 0, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 6. Monster 3103 P6001 S7001 Arc (Heavy Slash 128px)
def gen_u3103_p6001():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 60, 64
        if f == 0: # Windup Telegraph
            draw_arc(d, (cx - 15, cy), 40, -50, 50, 3, TELEGRAPH_COLOR)
        elif f == 1: # Pre-swing
            draw_arc(d, (cx - 5, cy), 46, -60, 60, 4, (255, 180, 50, 140))
        elif f == 2: # Heavy Impact Peak
            draw_arc(d, (cx + 10, cy), 52, -75, 75, 8, ACTIVE_RED)
            draw_arc(d, (cx + 10, cy), 52, -65, 65, 4, ACTIVE_CORE)
        elif f == 3: # Peak Extension
            draw_arc(d, (cx + 14, cy), 54, -80, 80, 7, ACTIVE_RED)
            draw_arc(d, (cx + 14, cy), 54, -70, 70, 3, ACTIVE_CORE)
        elif f == 4: # Heavy followthrough
            draw_arc(d, (cx + 16, cy), 54, -80, 80, 5, ACTIVE_ORANGE)
        elif f == 5: # Recovery
            draw_arc(d, (cx + 18, cy), 54, -60, 60, 3, RECOVERY_COLOR)
        elif f == 6: # Dissipate
            draw_arc(d, (cx + 18, cy), 54, -40, 40, 2, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 7. Monster 3103 P6010 S7007 DirectedBox (Torso Ram 128px)
def gen_u3103_p6010():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Charge telegraph
            draw_directed_box(d, (45, cy), (40, 34), 0, TELEGRAPH_COLOR)
        elif f == 1: # Pre-ram
            draw_directed_box(d, (55, cy), (50, 38), 0, (255, 180, 50, 140))
        elif f == 2: # Heavy Body Ram Peak
            draw_directed_box(d, (75, cy), (65, 44), 0, ACTIVE_RED, (255, 50, 50, 120))
            draw_directed_box(d, (80, cy), (55, 34), 0, ACTIVE_CORE)
        elif f == 3: # Ram Extension
            draw_directed_box(d, (85, cy), (65, 42), 0, ACTIVE_RED, (255, 100, 30, 100))
        elif f == 4: # Impact shockwave
            draw_directed_box(d, (90, cy), (60, 38), 0, ACTIVE_ORANGE)
            draw_ring(d, (105, cy), 12, 2, (255, 200, 50, 150))
        elif f == 5: # Recovery
            draw_directed_box(d, (90, cy), (45, 30), 0, RECOVERY_COLOR)
        elif f == 6: # Dissipate
            draw_directed_box(d, (90, cy), (30, 20), 0, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 8. Monster 3104 P6003 S7001 DirectedBox (Shield Bash 128px)
def gen_u3104_p6003():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Shield brace telegraph
            draw_directed_box(d, (45, cy), (20, 50), 0, TELEGRAPH_COLOR)
        elif f == 1: # Push forward
            draw_directed_box(d, (55, cy), (24, 54), 0, (255, 180, 50, 140))
        elif f == 2: # Shield Bash Peak Impact
            draw_directed_box(d, (75, cy), (30, 60), 0, ACTIVE_RED, (255, 50, 50, 120))
            draw_line(d, (85, cy - 25), (85, cy + 25), 4, ACTIVE_CORE)
        elif f == 3: # Impact Hold & Shockwave
            draw_directed_box(d, (82, cy), (28, 58), 0, ACTIVE_ORANGE)
            draw_arc(d, (85, cy), 20, -60, 60, 3, (255, 220, 50, 160))
        elif f == 4: # Followthrough
            draw_directed_box(d, (85, cy), (24, 52), 0, ACTIVE_ORANGE)
        elif f == 5: # Recovery
            draw_directed_box(d, (80, cy), (18, 44), 0, RECOVERY_COLOR)
        elif f == 6: # Dissipate
            draw_directed_box(d, (75, cy), (14, 35), 0, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 9. Monster 3104 P6004 S7001 Arc (Weapon Slam 128px)
def gen_u3104_p6004():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 64, 64
        if f == 0: # Overhead telegraph
            draw_arc(d, (cx, cy), 38, -120, -40, 2, TELEGRAPH_COLOR)
        elif f == 1: # Downward swing start
            draw_arc(d, (cx + 5, cy), 42, -90, 0, 4, (255, 180, 50, 140))
        elif f == 2: # Ground Slam Impact Peak
            draw_arc(d, (cx + 10, cy + 5), 48, -45, 45, 7, ACTIVE_RED)
            draw_arc(d, (cx + 10, cy + 5), 48, -30, 30, 3, ACTIVE_CORE)
            # Ground impact line
            draw_line(d, (cx - 10, cy + 40), (cx + 35, cy + 40), 4, ACTIVE_CORE)
        elif f == 3: # Ground Slam Burst
            draw_arc(d, (cx + 12, cy + 6), 50, -35, 35, 5, ACTIVE_ORANGE)
            draw_line(d, (cx - 15, cy + 40), (cx + 45, cy + 40), 3, ACTIVE_RED)
        elif f == 4: # Shockwave dissipate
            draw_line(d, (cx - 20, cy + 40), (cx + 50, cy + 40), 2, RECOVERY_COLOR)
        elif f == 5: # Recovery
            draw_line(d, (cx - 10, cy + 40), (cx + 35, cy + 40), 1, (255, 100, 30, 50))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 10. Monster 3105 P6005 S7002 Line (Crossbow Aim & Shot 128px)
def gen_u3105_p6005():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Aim telegraph line
            draw_line(d, (20, cy), (120, cy), 1, TELEGRAPH_COLOR)
        elif f == 1: # Target locked
            draw_line(d, (20, cy), (120, cy), 2, (255, 180, 50, 160))
        elif f == 2: # Muzzle Flash & Bolt Fire
            draw_line(d, (25, cy), (125, cy), 5, ACTIVE_RED)
            draw_line(d, (35, cy), (125, cy), 2, ACTIVE_CORE)
            # Small muzzle spark
            draw_ring(d, (35, cy), 5, 2, ACTIVE_CORE)
        elif f == 3: # Bolt trajectory peak
            draw_line(d, (45, cy), (128, cy), 4, ACTIVE_RED)
            draw_line(d, (65, cy), (128, cy), 2, ACTIVE_CORE)
        elif f == 4: # Trail fade
            draw_line(d, (65, cy), (125, cy), 3, ACTIVE_ORANGE)
        elif f == 5: # Recovery
            draw_line(d, (85, cy), (120, cy), 2, RECOVERY_COLOR)
        elif f == 6: # Dissipate
            draw_line(d, (100, cy), (120, cy), 1, (255, 100, 30, 40))
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 11. Monster 3105 P6006 S7002 Ring (Charged Aim & Muzzle Blast 128px)
def gen_u3105_p6006():
    sheet = create_strip(128)
    for f in range(8):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 64
        if f == 0: # Charged aim line + small ring
            draw_line(d, (20, cy), (120, cy), 1, TELEGRAPH_COLOR)
            draw_ring(d, (35, cy), 6, 1, TELEGRAPH_COLOR)
        elif f == 1: # Charge concentration
            draw_line(d, (20, cy), (120, cy), 2, (255, 200, 50, 160))
            draw_ring(d, (35, cy), 8, 2, (255, 200, 50, 160))
        elif f == 2: # Muzzle Blast Peak Impact
            draw_line(d, (25, cy), (125, cy), 6, ACTIVE_RED)
            draw_line(d, (40, cy), (125, cy), 2, ACTIVE_CORE)
            draw_ring(d, (38, cy), 12, 3, ACTIVE_RED)
            draw_ring(d, (38, cy), 12, 1, ACTIVE_CORE)
        elif f == 3: # Blast Wave Expanding
            draw_line(d, (45, cy), (128, cy), 4, ACTIVE_ORANGE)
            draw_ring(d, (42, cy), 16, 2, ACTIVE_ORANGE)
        elif f == 4: # Radial trail fade
            draw_line(d, (65, cy), (125, cy), 3, ACTIVE_ORANGE)
            draw_ring(d, (45, cy), 20, 1, RECOVERY_COLOR)
        elif f == 5: # Recovery
            draw_line(d, (85, cy), (120, cy), 2, RECOVERY_COLOR)
        sheet.paste(frame, (f * 128, 0))
    return sheet

# 12. Boss 3201 P6100 S7012 DirectedBox (Greatsword Charge 256px)
def gen_u3201_p6100():
    sheet = create_strip(256)
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cy = 128
        if f == 0: # Charge telegraph area
            draw_directed_box(d, (90, cy), (80, 70), 0, TELEGRAPH_COLOR)
        elif f == 1: # Surge forward
            draw_directed_box(d, (110, cy), (100, 80), 0, (255, 180, 50, 140))
        elif f == 2: # Greatsword Charge Impact Peak
            draw_directed_box(d, (145, cy), (135, 95), 0, ACTIVE_RED, (255, 50, 50, 120))
            draw_line(d, (80, cy), (225, cy), 4, ACTIVE_CORE)
        elif f == 3: # Peak Extension Hold
            draw_directed_box(d, (160, cy), (140, 90), 0, ACTIVE_RED, (255, 100, 30, 100))
            draw_line(d, (90, cy), (235, cy), 3, ACTIVE_CORE)
        elif f == 4: # Followthrough
            draw_directed_box(d, (170, cy), (130, 80), 0, ACTIVE_ORANGE)
        elif f == 5: # Deceleration
            draw_directed_box(d, (175, cy), (110, 65), 0, RECOVERY_COLOR)
        elif f == 6: # Recovery
            draw_directed_box(d, (175, cy), (80, 45), 0, (255, 100, 30, 40))
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 13. Boss 3201 P6101 S7011 Arc (ComboSlash 2-Hit 256px)
def gen_u3201_p6101():
    sheet = create_strip(256)
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 120, 128
        if f == 0: # Hit 1 Telegraph
            draw_arc(d, (cx - 20, cy), 75, -50, 50, 4, TELEGRAPH_COLOR)
        elif f == 1: # Hit 1 Peak (Horizontal Cleave)
            draw_arc(d, (cx + 15, cy), 95, -70, 70, 10, ACTIVE_RED)
            draw_arc(d, (cx + 15, cy), 95, -60, 60, 4, ACTIVE_CORE)
        elif f == 2: # Hit 1 Followthrough / Hit 2 Prep
            draw_arc(d, (cx + 20, cy), 98, -75, 75, 6, ACTIVE_ORANGE)
            draw_arc(d, (cx - 10, cy), 80, -90, 20, 3, TELEGRAPH_COLOR)
        elif f == 3: # Hit 2 Peak (Upward Cleave)
            draw_arc(d, (cx + 20, cy - 10), 105, -85, 45, 12, ACTIVE_RED)
            draw_arc(d, (cx + 20, cy - 10), 105, -75, 35, 5, ACTIVE_CORE)
        elif f == 4: # Hit 2 Peak Extension
            draw_arc(d, (cx + 25, cy - 15), 110, -90, 40, 8, ACTIVE_ORANGE)
            draw_arc(d, (cx + 25, cy - 15), 110, -80, 30, 3, ACTIVE_CORE)
        elif f == 5: # Recovery 1
            draw_arc(d, (cx + 28, cy - 15), 110, -70, 25, 4, RECOVERY_COLOR)
        elif f == 6: # Recovery 2
            draw_arc(d, (cx + 30, cy - 15), 110, -50, 15, 2, (255, 100, 30, 40))
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 14. Boss 3201 P6102 S7010 Arc (OverheadSmash 256px)
def gen_u3201_p6102():
    sheet = create_strip(256)
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 128, 128
        if f == 0: # Overhead telegraph
            draw_arc(d, (cx, cy), 75, -130, -30, 4, TELEGRAPH_COLOR)
        elif f == 1: # Downward acceleration
            draw_arc(d, (cx + 10, cy), 85, -100, 0, 6, (255, 180, 50, 140))
        elif f == 2: # Ground Smash Peak
            draw_arc(d, (cx + 20, cy + 10), 98, -50, 50, 12, ACTIVE_RED)
            draw_arc(d, (cx + 20, cy + 10), 98, -35, 35, 5, ACTIVE_CORE)
            # Ground fissure line
            draw_line(d, (cx - 30, cy + 85), (cx + 80, cy + 85), 8, ACTIVE_RED)
            draw_line(d, (cx - 20, cy + 85), (cx + 70, cy + 85), 3, ACTIVE_CORE)
        elif f == 3: # Ground Fissure Burst
            draw_arc(d, (cx + 22, cy + 12), 100, -40, 40, 8, ACTIVE_ORANGE)
            draw_line(d, (cx - 45, cy + 85), (cx + 95, cy + 85), 6, ACTIVE_ORANGE)
        elif f == 4: # Shockwave dissipate
            draw_line(d, (cx - 55, cy + 85), (cx + 105, cy + 85), 4, RECOVERY_COLOR)
        elif f == 5: # Recovery
            draw_line(d, (cx - 35, cy + 85), (cx + 85, cy + 85), 2, (255, 100, 30, 40))
        sheet.paste(frame, (f * 256, 0))
    return sheet

# 15. Boss 3201 P6103 S7013 Ring (Shockwave Burst 256px)
def gen_u3201_p6103():
    sheet = create_strip(256)
    for f in range(8):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        cx, cy = 128, 128
        if f == 0: # Radial telegraph
            draw_ring(d, (cx, cy), 30, 3, TELEGRAPH_COLOR)
        elif f == 1: # Pre-burst
            draw_ring(d, (cx, cy), 45, 4, (255, 180, 50, 140))
        elif f == 2: # Shockwave Blast Peak
            draw_ring(d, (cx, cy), 70, 10, ACTIVE_RED)
            draw_ring(d, (cx, cy), 70, 4, ACTIVE_CORE)
        elif f == 3: # Expanding Shockwave Ring
            draw_ring(d, (cx, cy), 95, 8, ACTIVE_RED)
            draw_ring(d, (cx, cy), 95, 3, ACTIVE_CORE)
        elif f == 4: # Massive Radial Wave
            draw_ring(d, (cx, cy), 115, 6, ACTIVE_ORANGE)
        elif f == 5: # Wave Dissipation
            draw_ring(d, (cx, cy), 122, 4, RECOVERY_COLOR)
        elif f == 6: # Fading Ring
            draw_ring(d, (cx, cy), 125, 2, (255, 100, 30, 30))
        sheet.paste(frame, (f * 256, 0))
    return sheet

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    generators = {
        "VFX_DummyAttack_U3001_PNA_S7001_Arc.png": gen_u3001_s7001,
        "VFX_DummyAttack_U3001_PNA_S7003_Arc.png": gen_u3001_s7003,
        "VFX_DummyAttack_U3101_P6001_S7001_Line.png": gen_u3101_p6001,
        "VFX_DummyAttack_U3102_P6008_S7005_Line.png": gen_u3102_p6008,
        "VFX_DummyAttack_U3102_P6009_S7006_DirectedBox.png": gen_u3102_p6009,
        "VFX_DummyAttack_U3103_P6001_S7001_Arc.png": gen_u3103_p6001,
        "VFX_DummyAttack_U3103_P6010_S7007_DirectedBox.png": gen_u3103_p6010,
        "VFX_DummyAttack_U3104_P6003_S7001_DirectedBox.png": gen_u3104_p6003,
        "VFX_DummyAttack_U3104_P6004_S7001_Arc.png": gen_u3104_p6004,
        "VFX_DummyAttack_U3105_P6005_S7002_Line.png": gen_u3105_p6005,
        "VFX_DummyAttack_U3105_P6006_S7002_Ring.png": gen_u3105_p6006,
        "VFX_DummyAttack_U3201_P6100_S7012_DirectedBox.png": gen_u3201_p6100,
        "VFX_DummyAttack_U3201_P6101_S7011_Arc.png": gen_u3201_p6101,
        "VFX_DummyAttack_U3201_P6102_S7010_Arc.png": gen_u3201_p6102,
        "VFX_DummyAttack_U3201_P6103_S7013_Ring.png": gen_u3201_p6103,
    }

    import hashlib
    print("=== Generating 15 Dummy Attack Effect Sprite Sheets ===")
    for filename, fn in generators.items():
        img = fn()
        out_path = os.path.join(OUTPUT_DIR, filename)
        img.save(out_path, format="PNG")
        with open(out_path, "rb") as f:
            data = f.read()
            sha = hashlib.sha256(data).hexdigest()
        print(f"Generated: {filename} ({img.size[0]}x{img.size[1]}) - Size: {len(data)} bytes - SHA256: {sha[:12]}...")

if __name__ == "__main__":
    main()
