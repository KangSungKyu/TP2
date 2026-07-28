import os, shutil, glob

src_dir = r"c:\Users\PC\Projects\TP2\Assets\prefabs"
dst_dir = r"c:\Users\PC\Projects\TP2\Assets\Prefabs"

# If src_dir exists, copy files to dst_dir
if os.path.exists(src_dir):
    for root, dirs, files in os.walk(src_dir):
        rel_path = os.path.relpath(root, src_dir)
        target_root = os.path.join(dst_dir, rel_path)
        os.makedirs(target_root, exist_ok=True)
        for f in files:
            s_file = os.path.join(root, f)
            d_file = os.path.join(target_root, f)
            shutil.copy2(s_file, d_file)

print("Successfully copied all assets to Assets/Prefabs!")
for p in glob.glob(os.path.join(dst_dir, "*.prefab")):
    print(f"  {p}")
for p in glob.glob(os.path.join(dst_dir, "Rooms", "*.prefab")):
    print(f"  {p}")
