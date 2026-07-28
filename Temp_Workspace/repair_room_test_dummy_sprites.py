import os, glob, re

env_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
prefab_p = r"c:\Users\PC\Projects\TP2\Assets\prefabs\Rooms\Room_TestDummy.prefab"

def get_sprite_ref(file_name):
    meta_p = os.path.join(env_dir, file_name + ".png.meta")
    guid, file_id = "", "21300000"
    if os.path.exists(meta_p):
        with open(meta_p, "r", encoding="utf-8") as f:
            txt = f.read()
        m_guid = re.search(r"guid: ([a-f0-9]+)", txt)
        if m_guid: guid = m_guid.group(1)
        m_id = re.search(r"internalID: (\d+)", txt)
        if m_id: file_id = m_id.group(1)
    return f"{{fileID: {file_id}, guid: {guid}, type: 3}}"

ground_ref = get_sprite_ref("Tile_Terrain_Ground")
plat_ref = get_sprite_ref("Tile_Platform_OneWay")
hazard_ref = get_sprite_ref("Tile_Hazard_SpikesLava")
struct_ref = get_sprite_ref("Sprite_Structures_Interactive")

print(f"Ground Sprite Ref: {ground_ref}")
print(f"Platform Sprite Ref: {plat_ref}")
print(f"Hazard Sprite Ref: {hazard_ref}")
print(f"Struct Sprite Ref: {struct_ref}")

with open(prefab_p, "r", encoding="utf-8") as f:
    yaml_txt = f.read()

# Replace m_Sprite: {fileID: 0} in SpriteRenderer blocks
# 21200001 (Ground_Base)
yaml_txt = re.sub(
    r"(--- !u!212 &21200001\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + ground_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200004 (Platform_Low)
yaml_txt = re.sub(
    r"(--- !u!212 &21200004\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + plat_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200005 (Platform_Mid)
yaml_txt = re.sub(
    r"(--- !u!212 &21200005\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + plat_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200006 (Platform_High)
yaml_txt = re.sub(
    r"(--- !u!212 &21200006\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + plat_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200007 (Hazard_Spikes)
yaml_txt = re.sub(
    r"(--- !u!212 &21200007\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + hazard_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200008 (Door_Exit)
yaml_txt = re.sub(
    r"(--- !u!212 &21200008\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + struct_ref,
    yaml_txt,
    flags=re.DOTALL
)

# 21200009 (Chest_Treasure)
yaml_txt = re.sub(
    r"(--- !u!212 &21200009\nSpriteRenderer:.*?\n  m_Sprite: )\{fileID: 0\}",
    r"\g<1>" + struct_ref,
    yaml_txt,
    flags=re.DOTALL
)

with open(prefab_p, "w", encoding="utf-8") as f:
    f.write(yaml_txt)

print("Successfully injected all Sprite GUIDs into Room_TestDummy.prefab!")
