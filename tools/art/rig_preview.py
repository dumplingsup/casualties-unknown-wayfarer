"""Offline assembly using the runtime rig constants and Unity rotation convention."""
import math
import re
from pathlib import Path
from PIL import Image, ImageDraw


def constants(root):
    text = (root/'src/WayfarerMech/MechRig.cs').read_text(encoding='utf-8')
    return {k:float(v) for k,v in re.findall(r'(\w+)\s*=\s*(-?[\d.]+)f',text)}


def render(g, scale=1, jet=False, facing=1, aim=0, overlay=False):
    R=constants(g['ROOT'])
    canvas=Image.new('RGBA',(128,96),(0,0,0,0))
    root=(54,34)
    parts=[]
    def add(name,x,y,ang=0,sort=None):
        maker=g['make_'+name]
        part=maker()
        parts.append((part.sort if sort is None else sort,part,(root[0]+x,root[1]+y),ang))
    def vec(x,y,angle): return g['rotate_vec'](x,y,angle)
    add('torso',0,0)
    add('cockpit',R['CabinX'],R['CabinY'])
    add('backpack',R['PackX'],R['PackY'])
    add('pelvis',0,3)
    add('cannon_mount',R['MountX'],R['MountY'])
    add('cannon',R['GunX'],R['GunY'],max(-70,min(70,aim)))
    da=max(-82,min(82,aim))
    add('drill_arm',R['DrillX'],R['DrillY'],da)
    bit=g['make_drill_bit'](0)
    bx,by=vec(R['BitX'],0,da)
    parts.append((37,bit,(root[0]+R['DrillX']+bx,root[1]+R['DrillY']+by),da))
    for front in (False,True):
        suffix='Front' if front else 'Back'
        x,y=R['Hip'+suffix+'X'],R['HipY']
        a=R['Thigh'+suffix+'Rest']+(10 if front else -8)*(1 if jet else 0)
        s=R['Shin'+suffix+'Rest']+(24 if jet else 0)
        for kind,angle in [('thigh',a),('shin',a+s),('foot',0)]:
            part=g['make_'+kind](front)
            parts.append((part.sort,part,(root[0]+x,root[1]+y),angle))
            if kind!='foot':
                dx,dy=vec(0,R['ThighLength' if kind=='thigh' else 'ShinLength'],angle)
                x+=dx; y+=dy
    if not jet: add('shadow',0,R['GroundY'])
    else:
        for key in ('NozzleAX','NozzleBX'):
            part=g['make_flame'](0)
            parts.append((8,part,(root[0]+R['PackX']+R[key],root[1]+R['PackY']+R['NozzleY']),0))
    for _,part,target,angle in sorted(parts,key=lambda p:p[0]):
        g['paste_rotated'](canvas,part,target,angle)
    if overlay:
        d=ImageDraw.Draw(canvas)
        d.rectangle((root[0]-16.8,root[1]-26.4,root[0]+16.8,root[1]+26.4),outline=(222,174,79,255))
        for x,y in ((R['GunX'],R['GunY']),(R['DrillX'],R['DrillY']),
                    (R['HipFrontX'],R['HipY']),(R['HipBackX'],R['HipY'])):
            d.ellipse((root[0]+x-1,root[1]+y-1,root[0]+x+1,root[1]+y+1),fill=(255,85,85,255))
    if facing<0: canvas=canvas.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return canvas.resize((128*scale,96*scale),Image.Resampling.NEAREST)


def matrix(g,out):
    cells=[('RIGHT / IDLE',False,1,0),('LEFT / IDLE',False,-1,0),
           ('RIGHT / AIM UP',False,1,65),('LEFT / AIM DOWN',False,-1,-50),
           ('JET / RIG POSE',True,1,0),('HULL / JOINTS',False,1,0)]
    sheet=Image.new('RGB',(128*4*3,96*4*2+64),(31,36,37))
    d=ImageDraw.Draw(sheet)
    for i,(label,jet,facing,aim) in enumerate(cells):
        x=(i%3)*512; y=(i//3)*416
        image=render(g,4,jet,facing,aim,overlay=i==5)
        sheet.paste(image,(x,y+24),image)
        d.text((x+16,y+8),label,fill=(220,225,220))
    sheet.save(out/'rig-matrix.png')
