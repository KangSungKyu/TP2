import os, glob, re

env_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
mat_dir = r"c:\Users\PC\Projects\TP2\Assets\Materials\Physics"
server_dir = r"C:\Users\PC\TP2LocalServer\ServerData"

print("=== 2D STAGE ENVIRONMENT FINAL INTEGRITY INSPECTION ===\n")

# 1. Inspect Sprite Slicing & Pivot (0.5, 0.5) in Environment Metas
metas = glob.glob(os.path.join(env_dir, "*.meta"))
print(f"1. Environment Meta Files ({len(metas)}):")
for m_path in metas:
    name = os.path.basename(m_path).replace(".png.meta", "")
    with open(m_path, "r", encoding="utf-8") as f:
        txt = f.read()
    
    slices = re.findall(r"name: ([\w_]+)\n\s+rect:", txt)
    pivots = re.findall(r"pivot: \{x: ([\d.]+), y: ([\d.]+)\}", txt)
    print(f"   - {name}: Slices={len(slices)}, Pivots={set(pivots)}")

# 2. Inspect PhysicsMaterial2D
print(f"\n2. PhysicsMaterial2D Assets ({len(glob.glob(os.path.join(mat_dir, '*.physicsMaterial2D')))}):")
for mat_p in glob.glob(os.path.join(mat_dir, "*.physicsMaterial2D")):
    m_name = os.path.basename(mat_p)
    with open(mat_p, "r", encoding="utf-8") as f:
        txt = f.read()
    friction = re.search(r"friction: ([\d.]+)", txt)
    bounce = re.search(r"bounciness: ([\d.]+)", txt)
    f_val = friction.group(1) if friction else "N/A"
    b_val = bounce.group(1) if bounce else "N/A"
    print(f"   - {m_name}: Friction={f_val}, Bounciness={b_val}")

# 3. Inspect Addressables ServerData Bundles
print(f"\n3. ServerData Bundles Count in {server_dir}:")
if os.path.exists(server_dir):
    files_count = sum(len(files) for _, _, files in os.walk(server_dir))
    print(f"   - Total Bundle Files/Configs Deployed: {files_count}")

print("\n=== STAGE INSPECTION COMPLETE ===")
