import os, shutil, glob

old_dir = r"c:\Users\PC\Projects\TP2\Assets\prefabs"
new_dir = r"c:\Users\PC\Projects\TP2\Assets\Prefabs"

# If operating on Windows, rename folder via temp directory to guarantee case change in git/fs
temp_dir = r"c:\Users\PC\Projects\TP2\Assets\prefabs_temp_rename"

if os.path.exists(old_dir):
    os.rename(old_dir, temp_dir)
    os.rename(temp_dir, new_dir)
    print(f"Renamed {old_dir} -> {new_dir} successfully!")

rooms_dir = os.path.join(new_dir, "Rooms")
os.makedirs(rooms_dir, exist_ok=True)

print("Check files in Assets/Prefabs:")
for p in glob.glob(os.path.join(new_dir, "**", "*"), recursive=True):
    print(f"  {p}")
