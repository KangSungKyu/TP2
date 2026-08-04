import glob
import os
import struct

png_files = glob.glob(r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters\*\*.png")

for p in sorted(png_files):
    with open(p, 'rb') as f:
        data = f.read(24)
        if data[:8] == b'\x89PNG\r\n\x1a\n':
            w, h = struct.unpack('>II', data[16:24])
            mname = os.path.basename(os.path.dirname(p))
            fname = os.path.basename(p)
            print(f"{mname}/{fname}: {w}x{h}")
