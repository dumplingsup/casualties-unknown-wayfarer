"""Original v0.2.1 structural parts, using the existing procedural drawing primitives.

Installed by gen_wayfarer_art.py; no external images are read or repainted.
"""

def install(g):
    # Reuse the established palette, primitive renderer and Part format.
    globals().update({k: v for k, v in g.items() if not k.startswith('__')})
    return dict(torso=torso, cockpit=cockpit, cannon=cannon,
                cannon_mount=cannon_mount, drill_arm=drill_arm, drill_bit=drill_bit)


def torso():
    w, h = 34, 28
    a = array(w, h)
    m = poly_mask(w,h,[(2,9),(6,2),(23,1),(31,8),(33,20),(28,26),(8,27),(1,21)])
    paint(a,m,STEEL,seed=601,noise=.018)
    rear = poly_mask(w,h,[(3,10),(7,3),(14,3),(16,9),(14,21),(4,22)])
    paint(a,rear,OLIVE,seed=602,noise=.018)
    # Exposed load-bearing ring under the cabin, rather than a humanoid chest.
    line(a,[(5,23),(13,25),(26,25),(31,21)],STEEL[5],2)
    line(a,[(6,25),(13,27),(26,27)],STEEL[1])
    for y in (12,15,18):
        line(a,[(5,y),(10,y)],OUTLINE)
        line(a,[(5,y+1),(10,y+1)],STEEL[4])
    rivets(a,[(5,8),(12,5),(5,21),(28,23)])
    weather(a,m,seed=603,strength=.12)
    return Part('torso',a,(17,24),20)


def cockpit():
    w,h=24,27
    a=array(w,h)
    m=poly_mask(w,h,[(3,3),(15,1),(21,5),(23,14),(21,23),(16,26),(4,25),(1,18),(1,8)])
    paint(a,m,STEEL,seed=611,noise=.012)
    shell=poly_mask(w,h,[(4,4),(14,2),(19,5),(20,19),(17,24),(5,23),(3,17)])
    paint(a,shell,OLIVE,seed=612,noise=.015)
    # One coherent seated cabin: brow, window, door, step; no chin/face vents.
    glass(a,m,w,h,(10,6,19,14))
    line(a,[(10,5),(18,4),(21,7)],STEEL[5])
    line(a,[(9,6),(8,16),(8,22)],OUTLINE)
    line(a,[(10,17),(19,17),(18,22),(10,23)],STEEL[2])
    line(a,[(11,18),(14,18)],STEEL[5])
    line(a,[(6,24),(17,24)],STEEL[5])
    line(a,[(4,9),(4,13)],STEEL[5])
    line(a,[(4,17),(4,20)],STEEL[4])
    dotted(a,[(20,16),(20,17)],AMBER[3])
    rivets(a,[(6,5),(6,21),(19,22)])
    weather(a,m,seed=613,strength=.10)
    return Part('cockpit',a,(12,24),26,(3,-2))


def cannon_mount():
    a=array(12,12)
    m=poly_mask(12,12,[(1,11),(2,7),(4,2),(8,1),(10,6),(11,11)])
    paint(a,m,STEEL,seed=621,noise=.01)
    paint(a,ellipse_mask(12,12,6,5,3,3),STEEL[2:],seed=622,rim=False)
    dotted(a,[(6,5)],STEEL[6])
    line(a,[(3,10),(9,10)],STEEL[5])
    return Part('cannon_mount',a,(6,10),18,(-3,-19))


def cannon():
    w,h=26,10
    a=array(w,h)
    m=poly_mask(w,h,[(1,3),(6,1),(14,2),(16,3),(24,3),(25,7),(15,8),(5,9),(1,7)])
    paint(a,m,STEEL,seed=631,noise=.015)
    paint(a,rect_mask(w,h,(7,2,13,6)),OLIVE,seed=632,noise=.01)
    paint(a,ellipse_mask(w,h,4,5,2.8,2.7),STEEL[2:],seed=633,rim=False)
    line(a,[(15,4),(23,4)],STEEL[5])
    line(a,[(15,7),(24,7)],STEEL[1])
    line(a,[(24,4),(24,6)],OUTLINE)
    rivets(a,[(9,7),(13,3)])
    return Part('cannon',a,(4,5),16,(-3,-24))


def drill_arm():
    w,h=24,14
    a=array(w,h)
    m=poly_mask(w,h,[(1,4),(5,1),(11,3),(15,4),(22,3),(23,10),(16,11),(11,12),(4,12),(1,9)])
    paint(a,m,STEEL,seed=641,noise=.01)
    paint(a,poly_mask(w,h,[(5,3),(12,4),(13,9),(6,10)]),OLIVE,seed=642,noise=.01)
    paint(a,ellipse_mask(w,h,4,7,3.2,3.2),STEEL[2:],seed=643,rim=False)
    dotted(a,[(4,7)],STEEL[6])
    line(a,[(8,11),(16,10)],STEEL[5])
    line(a,[(12,4),(19,4)],STEEL[5])
    for x in (16,19): line(a,[(x,6),(x,9)],OUTLINE)
    paint(a,rect_mask(w,h,(21,3,23,11)),STEEL[2:],seed=644,rim=False)
    dotted(a,[(14,4),(15,4)],AMBER[2])
    return Part('drill_arm',a,(4,7),36,(6,-1))


def drill_bit(frame):
    w,h=14,12
    a=array(w,h)
    m=poly_mask(w,h,[(1,2),(3,1),(12,5),(13,6),(12,7),(3,11),(1,10)])
    paint(a,m,STEEL[1:],seed=651,noise=.01)
    # Helical steel cutting edges, not orange rust bands across the entire tool.
    for x in range(3-frame,12,4):
        for y in range(2,10):
            xx=x+(y-2)//3
            if 0<=xx<w and m[y,xx]: a[y,xx]=STEEL[5 if y<6 else 2]
    paint(a,rect_mask(w,h,(0,3,2,9)),STEEL[2:],seed=652,rim=False)
    dotted(a,[(13,6)],STEEL[6])
    return Part(f'drill_bit_{frame}',a,(1,6),37)
