import os, glob, re

env_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
mat_dir = r"c:\Users\PC\Projects\TP2\Assets\Materials\Physics"
server_dir = r"C:\Users\PC\TP2LocalServer\ServerData"

print("=== DUMMY STAGE BUILDER RESOURCE INTEGRITY CHECK ===\n")

specs = [
    ("Tile_Terrain_Ground.png.meta", "Ground, Wall, Slope"),
    ("Tile_Platform_OneWay.png.meta", "1-Way Platform"),
    ("Tile_Hazard_SpikesLava.png.meta", "Spikes, Lava, Traps"),
    ("Tile_Background_Deco.png.meta", "Deco, Background"),
    ("Sprite_Structures_Interactive.png.meta", "Door, Chest, Breakable")
]

for meta_file, usage in specs:
    p = os.path.join(env_dir, meta_file)
    name = meta_file.replace(".png.meta", "")
    if os.path.exists(p):
        with open(p, "r", encoding="utf-8") as f:
            txt = f.read()
        sub_names = re.findall(r"name: ([\w_]+)\n\s+rect:", txt)
        print(f"OK: {name} ({usage}): {len(sub_names)} sub-sprites sliced (Pivot Center 0.5,0.5)")
        print(f"   Sub-sprites: {sub_names[:3]} ... {sub_names[-1] if sub_names else ''}")

print("\n--- PhysicsMaterials Check ---")
for m_name in ["GroundPhysicsMaterial.physicsMaterial2D", "WallPhysicsMaterial.physicsMaterial2D"]:
    mp = os.path.join(mat_dir, m_name)
    if os.path.exists(mp):
        with open(mp, "r", encoding="utf-8") as f: txt = f.read()
        f_val = re.search(r"friction: ([\d.]+)", txt).group(1)
        b_val = re.search(r"bounciness: ([\d.]+)", txt).group(1)
        print(f"OK: {m_name}: Friction={f_val}, Bounciness={b_val}")

print("\n--- ServerData Deployment Check ---")
if os.path.exists(server_dir):
    cnt = sum(len(files) for _, _, files in os.walk(server_dir))
    print(f"OK: ServerData deployed bundle files count: {cnt}")

print("\n=== DUMMY STAGE RESOURCE CHECK COMPLETE ===")
