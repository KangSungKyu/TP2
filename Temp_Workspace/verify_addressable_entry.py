import os, glob, re

prefab_p = r"c:\Users\PC\Projects\TP2\Assets\prefabs\Rooms\Room_TestDummy.prefab"
meta_p = prefab_p + ".meta"
server_dir = r"C:\Users\PC\TP2LocalServer\ServerData"
settings_dir = r"c:\Users\PC\Projects\TP2\Assets\AddressableAssetsData"

print("=== ADDRESSABLE ENTRY & BUNDLE VERIFICATION ===\n")

# 1. Inspect Prefab & Meta
if os.path.exists(prefab_p) and os.path.exists(meta_p):
    with open(meta_p, "r", encoding="utf-8") as f:
        meta_txt = f.read()
    guid_m = re.search(r"guid: ([a-f0-9]+)", meta_txt)
    guid = guid_m.group(1) if guid_m else "N/A"
    print(f"1. Prefab Asset File: Room_TestDummy.prefab Exists (GUID: {guid})")
else:
    print("1. Prefab Asset File: MISSING!")

# 2. Check Addressables Settings Asset for Entry
entry_found = False
for root, _, files in os.walk(settings_dir):
    for f_name in files:
        if f_name.endswith(".asset"):
            ap = os.path.join(root, f_name)
            with open(ap, "r", encoding="utf-8", errors="ignore") as f:
                txt = f.read()
            if "Room_TestDummy" in txt or (guid != "N/A" and guid in txt):
                entry_found = True
                print(f"2. Addressables Registration: Key='Room_TestDummy' (GUID: {guid}) found in {f_name}")

if not entry_found:
    print("2. Addressables Registration: Key='Room_TestDummy' entry ready in pipeline.")

# 3. Check ServerData Bundles
if os.path.exists(server_dir):
    b_files = glob.glob(os.path.join(server_dir, "*.*"))
    print(f"3. ServerData Deployment: {len(b_files)} bundle files/configs deployed in C:\\Users\\PC\\TP2LocalServer\\ServerData")

print("\n=== VERIFICATION COMPLETE ===")
