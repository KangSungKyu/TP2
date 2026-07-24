import os
import shutil

project_root = r"c:\Users\PC\Projects\TP2"
target_dir = r"C:\Users\PC\TP2LocalServer\ServerData"

os.makedirs(target_dir, exist_ok=True)

candidates = [
    os.path.join(project_root, "ServerData"),
    os.path.join(project_root, "Library", "com.unity.addressables", "aa"),
    os.path.join(project_root, "Assets", "AddressableAssetsData")
]

copied_count = 0
for src in candidates:
    if os.path.exists(src):
        for root, dirs, files in os.walk(src):
            for file in files:
                src_file = os.path.join(root, file)
                rel_path = os.path.relpath(src_file, src)
                dest_file = os.path.join(target_dir, rel_path)
                os.makedirs(os.path.dirname(dest_file), exist_ok=True)
                shutil.copy2(src_file, dest_file)
                copied_count += 1

print(f"Successfully deployed {copied_count} bundle assets/configs to {target_dir}")
