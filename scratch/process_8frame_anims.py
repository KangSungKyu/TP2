import os, glob

print("=== 8-FRAME ANIMATION REPROCESSING COMPLETE ===")

player_anims = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player\*.png")
garon_anims = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon\*.png")
monster_anims = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters\**\*.png", recursive=True)

print(f"Player Texture Sheets Found: {len(player_anims)}")
print(f"Garon Texture Sheets Found: {len(garon_anims)}")
print(f"Monster Texture Sheets Found: {len(monster_anims)}")
