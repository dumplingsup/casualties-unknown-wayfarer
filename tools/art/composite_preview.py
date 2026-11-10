from PIL import Image, ImageEnhance, ImageDraw
from pathlib import Path
root=Path(r"D:\testGround\furry")
bg=Image.open(root/"runs/ui-recon-v01/shot-08.png").convert("RGBA")
mech=Image.open(root/"tools/art/preview/assembly_1x.png").convert("RGBA")
bbox=mech.getbbox(); mech=mech.crop(bbox); print("mech bbox",bbox,mech.size)
# screen scale estimate: ~24 px per world unit, PPU 8 -> 3 px per art px
scale=3
m=mech.resize((mech.width*scale, mech.height*scale), Image.NEAREST)
# right-facing on the dark floor left of pod
out=bg.copy()
x1,y1=110,600-m.height  # feet y=600
out.alpha_composite(m,(x1,y1))
# left-facing at right side
mf=m.transpose(Image.FLIP_LEFT_RIGHT)
x2,y2=930,650-m.height
out.alpha_composite(mf,(x2,y2))
# dark cave copy: tinted down, placed on dark background upper-left
dark=m.copy(); d=ImageDraw.Draw(dark); d.rectangle([0,0,dark.width,dark.height],fill=(10,16,24,110),outline=None)
out.alpha_composite(dark,(80,230))
# 1x annotation crop
ann=out.convert("RGB")
d=ImageDraw.Draw(ann)
d.text((10,10),"screen composite, est. 3px per art px (PPU8, ~24px/world unit)",fill=(240,220,150))
out_path=root/"tools/art/preview/composite_game_shot08.png"
ann.save(out_path); print(out_path)
# also a crop around the two mechs at 2x
crop=ann.crop((0,200,1280,720)); crop=crop.resize((crop.width,crop.height),Image.NEAREST)
crop.save(root/"tools/art/preview/composite_game_shot08_crop.png")
