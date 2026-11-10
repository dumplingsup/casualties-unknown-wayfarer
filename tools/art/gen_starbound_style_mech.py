"""Generate original modular mech art for Wayfarer v0.4.2.

This is original pixel art: the shapes, palettes, outlines and pixels were drawn by
this script. The Starbound Champion-00 asset extracted from the user's local install
was used only as a silhouette/colour reference; no Starbound pixels are copied here.
See docs/STARBOUND-MECH-PORT-EXECUTION.md and docs/ART-ASSETS.md.

Run from D:\\testGround\\furry:
  python tools/art/gen_starbound_style_mech.py
"""
from __future__ import annotations

import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "WayfarerMech" / "Assets"
PREVIEW = ROOT / "tools" / "art" / "preview"
ASSETS.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)

CATALOG_PATH = ROOT / "tools" / "art" / "starbound_catalog.json"
CATALOG = json.loads(CATALOG_PATH.read_text(encoding="utf-8")) if CATALOG_PATH.exists() else {
    "bodies": [], "arms": [], "legs": [], "boosters": [], "horns": []
}

PPU = 11.0
OUTLINE = (8, 12, 18, 255)
RNG = np.random.default_rng(0x5B0A2)

# Deliberately low-saturation industrial palettes, with one warm/cold accent.
PALETTES = {
    "bone": {
        "name": "Bone / Teal",
        "dark": [(18, 22, 25), (28, 33, 35), (42, 49, 50), (61, 70, 69)],
        "primary": [(36, 32, 25), (58, 52, 40), (86, 78, 58), (116, 105, 78), (151, 135, 100), (190, 172, 132), (222, 207, 168)],
        "secondary": [(42, 23, 18), (67, 35, 25), (98, 50, 32), (138, 70, 42), (182, 99, 58), (220, 136, 83)],
        "accent": [(18, 44, 43), (27, 66, 62), (42, 94, 86), (65, 128, 116), (98, 168, 150)],
        "glow": (125, 236, 218, 255),
        "glass": (12, 27, 30, 255),
    },
    "olive": {
        "name": "Olive / Amber",
        "dark": [(17, 20, 17), (27, 31, 25), (40, 45, 35), (58, 63, 50)],
        "primary": [(29, 31, 22), (46, 49, 33), (67, 70, 47), (93, 96, 64), (123, 125, 84), (158, 157, 108), (197, 193, 143)],
        "secondary": [(50, 37, 20), (77, 55, 27), (111, 79, 36), (153, 110, 48), (198, 148, 65)],
        "accent": [(72, 38, 17), (111, 60, 24), (159, 89, 34), (211, 126, 45), (241, 171, 77)],
        "glow": (255, 204, 101, 255),
        "glass": (21, 26, 19, 255),
    },
    "steel": {
        "name": "Steel / Red",
        "dark": [(14, 18, 23), (23, 29, 36), (34, 42, 51), (49, 59, 70)],
        "primary": [(31, 37, 43), (49, 58, 66), (72, 83, 94), (100, 113, 125), (132, 146, 158), (169, 182, 193), (208, 218, 225)],
        "secondary": [(55, 27, 27), (86, 38, 36), (123, 48, 44), (165, 61, 54), (211, 84, 70)],
        "accent": [(74, 32, 23), (116, 48, 31), (165, 67, 40), (215, 98, 54), (246, 154, 86)],
        "glow": (255, 107, 87, 255),
        "glass": (14, 20, 26, 255),
    },
    "violet": {
        "name": "Violet / Lime",
        "dark": [(18, 16, 25), (29, 25, 38), (43, 36, 55), (61, 51, 75)],
        "primary": [(35, 29, 44), (54, 44, 65), (78, 64, 93), (107, 88, 126), (140, 116, 160), (177, 150, 196), (214, 190, 227)],
        "secondary": [(36, 55, 24), (55, 82, 32), (80, 117, 43), (112, 158, 55), (152, 200, 76)],
        "accent": [(83, 36, 74), (128, 52, 111), (176, 71, 151), (224, 99, 190), (250, 154, 223)],
        "glow": (190, 255, 112, 255),
        "glass": (23, 17, 27, 255),
    },
}

RAMPS = ("primary", "secondary", "dark", "accent")


def arr(w: int, h: int) -> np.ndarray:
    return np.zeros((h, w, 4), np.uint8)


def poly_mask(w: int, h: int, pts) -> np.ndarray:
    im = Image.new("1", (w, h), 0)
    ImageDraw.Draw(im).polygon([(int(round(x)), int(round(y))) for x, y in pts], fill=1)
    return np.asarray(im, dtype=bool)


def rect_mask(w: int, h: int, box) -> np.ndarray:
    m = np.zeros((h, w), bool)
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    x0, x1 = max(0, min(x0, x1)), min(w, max(x0, x1) + 1)
    y0, y1 = max(0, min(y0, y1)), min(h, max(y0, y1) + 1)
    m[y0:y1, x0:x1] = True
    return m


def ellipse_mask(w: int, h: int, cx, cy, rx, ry) -> np.ndarray:
    yy, xx = np.mgrid[0:h, 0:w]
    return ((xx - cx) / max(rx, 0.1)) ** 2 + ((yy - cy) / max(ry, 0.1)) ** 2 <= 1.0


def erode(mask: np.ndarray) -> np.ndarray:
    m = mask.copy()
    m[1:, :] &= mask[:-1, :]
    m[:-1, :] &= mask[1:, :]
    m[:, 1:] &= mask[:, :-1]
    m[:, :-1] &= mask[:, 1:]
    return m


def ramp_rgba(ramp):
    return [tuple(c) + (255,) if len(c) == 3 else tuple(c) for c in ramp]


def paint(arr: np.ndarray, mask: np.ndarray, ramp, seed=0, noise=0.035, outline=True, rim=True, bottom_darken=0.0):
    if not mask.any():
        return
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    nx = (xs - x0) / max(1, x1 - x0)
    ny = (ys - y0) / max(1, y1 - y0)
    rng = np.random.default_rng(seed)
    light = 0.24 + 0.47 * (1.0 - ny) + 0.16 * (1.0 - nx) - bottom_darken * ny
    light += rng.normal(0.0, noise, len(xs))
    idx = np.clip(np.rint(light * (len(ramp) - 1)), 0, len(ramp) - 1).astype(int)
    arr[ys, xs] = np.asarray(ramp_rgba(ramp), dtype=np.uint8)[idx]
    if rim:
        top = mask & ~np.roll(mask, 1, axis=0)
        left = mask & ~np.roll(mask, 1, axis=1)
        for y, x in zip(*np.nonzero(top | left)):
            arr[y, x] = ramp_rgba(ramp)[min(len(ramp) - 1, 4)]
    if outline:
        arr[mask & ~erode(mask)] = OUTLINE


def rgba(color):
    return color if len(color) == 4 else tuple(color) + (255,)


def line(arr, pts, color, width=1):
    im = Image.fromarray(arr, "RGBA")
    ImageDraw.Draw(im).line([(int(round(x)), int(round(y))) for x, y in pts], fill=rgba(color), width=width)
    arr[:] = np.asarray(im)


def dot(arr, x, y, color):
    h, w = arr.shape[:2]
    if 0 <= int(x) < w and 0 <= int(y) < h:
        arr[int(y), int(x)] = rgba(color)


def hazard(arr, mask, x0, x1, y0, y1, accent, phase=0):
    for y in range(y0, y1):
        for x in range(x0, x1):
            if 0 <= y < mask.shape[0] and 0 <= x < mask.shape[1] and mask[y, x]:
                ac = accent[3] if len(accent[3]) == 4 else tuple(accent[3]) + (255,)
                arr[y, x] = ac if ((x + y + phase) // 3) % 2 == 0 else (33, 34, 30, 255)


def rivets(arr, pts, light, dark=OUTLINE):
    for x, y in pts:
        dot(arr, x, y, light)
        dot(arr, x + 1, y + 1, dark)


def window(arr, mask, x0, y0, x1, y1, pal):
    m = rect_mask(arr.shape[1], arr.shape[0], (x0, y0, x1, y1)) & mask
    ys, xs = np.nonzero(m)
    if len(xs) == 0:
        return
    g = pal["glass"]
    for y, x in zip(ys, xs):
        t = (x - x0) / max(1, x1 - x0)
        u = (y - y0) / max(1, y1 - y0)
        if u < 0.32 and t < 0.7:
            arr[y, x] = (29, 48, 49, 255)
        elif u > 0.72:
            arr[y, x] = (7, 13, 16, 255)
        else:
            arr[y, x] = g
    arr[m & ~erode(m)] = (4, 7, 9, 255)
    if y1 - y0 > 4 and x1 - x0 > 5:
        dot(arr, x0 + 2, y0 + 2, pal["glow"])


def add_outline_edges(arr, mask):
    if mask.any():
        arr[mask & ~erode(mask)] = OUTLINE


def save(arr: np.ndarray, key: str, pivot, manifest: dict, w: int, h: int):
    path = ASSETS / (key + ".png")
    Image.fromarray(arr, "RGBA").save(path)
    manifest[key] = {"w": w, "h": h, "pivot": [int(pivot[0]), int(pivot[1])], "ppu": PPU}


# ---------------------------------------------------------------------------
# Body: original open-cockpit silhouette. Back layer = engine, seat, roll bar.
# Front layer = low chest cowl, console, lap armour, shoulder and hip mounts.
# ---------------------------------------------------------------------------

# Natural beak / crest / bone armour stays ivory across all body colours. This keeps
# the Avian silhouette legible instead of tinting the beak olive or violet with the hull.
IVORY = [(29, 27, 23), (47, 44, 36), (72, 67, 55), (104, 97, 79),
         (143, 134, 110), (185, 174, 145), (223, 213, 184)]


def ramp_palette(rgb):
    """Build an original six-step metal/cloth ramp from one theme colour."""
    r, g, b = rgb
    factors = (0.28, 0.46, 0.68, 0.92, 1.12, 1.35)
    return [tuple(min(255, max(0, int(c * f))) for c in (r, g, b)) for f in factors]


def champion_cockpit(pal):
    """Bird canopy is a structural roll cage over a seated pilot, not a nose sticker.
    Pixel contract: pivot 32,34; head 30,24; shoulders 46,48; hips 24/40,60.
    """
    W,H = 76,64
    back,front = arr(W,H),arr(W,H)
    steel = [(15,22,26),(31,42,46),(55,67,70),(90,103,104),(141,153,145)]
    ivory = [(44,43,39),(77,73,60),(121,114,92),(170,160,126),(217,203,165),(239,225,192)]
    ochre = [(45,31,20),(83,54,26),(132,87,34),(181,127,49),(217,171,76),(242,207,123)]
    armor = ochre if pal is PALETTES["bone"] else pal["primary"]
    def plate(dst, pts, ramp):
        m=poly_mask(W,H,pts); paint(dst,m,ramp,noise=0,bottom_darken=0.08); return m
    # Rear load-bearing mast, engine block and seat. The mast carries the canopy.
    plate(back,[(6,23),(16,19),(23,23),(24,47),(17,53),(5,47)],steel)
    plate(back,[(5,30),(14,28),(16,44),(7,46),(3,40)],armor)
    for y in (32,36,40): line(back,[(7,y),(12,y)],steel[0])
    plate(back,[(15,16),(20,12),(25,18),(24,36),(19,42),(14,37)],steel)
    plate(back,[(20,22),(25,22),(25,35),(31,39),(28,43),(20,39)],pal["secondary"])
    line(back,[(21,24),(21,35),(29,40)],pal["secondary"][4],width=2)
    # Broad wing plates sweep backwards from the roof mount.
    for pts in (
        [(29,9),(16,4),(2,3),(10,11),(23,15)],
        [(25,13),(13,10),(0,12),(9,19),(22,21)],
        [(22,19),(11,18),(2,23),(11,27),(22,25)]):
        plate(back,pts,ivory)
    line(back,[(8,7),(22,12)],ochre[3],width=2)
    line(back,[(6,16),(19,20)],ivory[1])
    plate(back,[(14,26),(16,14),(24,7),(31,7),(28,13),(22,17),(20,29)],armor)
    line(back,[(17,24),(19,16),(26,10)],steel[4])
    # The bird skull is the roof. Its lower edge stays ABOVE the real face window.
    plate(front,[(22,9),(30,3),(44,2),(55,6),(63,12),(59,17),(49,14),(34,13),(26,16),(20,15)],ivory)
    plate(front,[(29,5),(32,1),(41,2),(47,5),(39,7)],ivory)
    # A hooked beak hangs ahead of the console, never over the pilot.
    plate(front,[(57,10),(66,12),(72,18),(74,25),(70,29),(68,21),(61,18),(57,16)],ochre)
    line(front,[(62,14),(68,17),(71,23)],ochre[4])
    plate(front,[(43,8),(53,8),(56,11),(51,14),(45,12)],steel)
    front[ellipse_mask(W,H,50,10,2,2)] = rgba(ochre[4])
    dot(front,50,10,OUTLINE)
    line(front,[(41,6),(52,7)],ivory[4])
    # Console behind hands, with an angled lit instrument display.
    plate(back,[(43,28),(51,28),(54,36),(50,41),(41,38)],steel)
    plate(back,[(44,29),(49,29),(50,33),(44,33)],pal["accent"])
    line(back,[(45,31),(48,31)],pal["glow"])
    # Tub chassis below the seat; the pilot's torso remains visible above the rim.
    plate(front,[(15,41),(22,39),(37,40),(48,36),(54,39),(54,50),(45,56),(25,55),(16,50)],armor)
    line(front,[(19,42),(35,43),(47,39),(51,40)],ochre[4],width=2)
    plate(front,[(25,46),(39,46),(41,52),(29,53)],armor)
    for x in (29,33,37): line(front,[(x,47),(x,51)],steel[0])
    # Guard rail and two mechanical control sticks: clearly belong to the cockpit.
    line(front,[(21,36),(40,36),(43,33)],steel[0],width=3)
    line(front,[(22,35),(39,35),(42,32)],steel[4])
    line(front,[(23,36),(23,42)],steel[2],width=2)
    line(front,[(38,35),(38,41)],steel[2],width=2)
    for x in (33,40):
        line(back,[(x,35),(x+1,31)],steel[3],width=2)
        line(back,[(x,30),(x+3,30)],steel[0],width=2)
    # Explicit hip beam, readable bearings and mounted tool axis.
    plate(back,[(19,51),(43,51),(47,57),(43,63),(20,63),(15,57)],steel)
    for x in (24,40):
        paint(back,ellipse_mask(W,H,x,59,5,4),steel,noise=0)
        back[ellipse_mask(W,H,x,59,2,2)] = rgba(ochre[2])
    paint(front,ellipse_mask(W,H,46,48,7,7),steel,noise=0)
    paint(front,ellipse_mask(W,H,46,48,4,4),ivory,noise=0)
    front[ellipse_mask(W,H,46,48,1,1)] = OUTLINE
    rivets(front,[(19,46),(25,43),(43,42),(46,54)],ivory[4])
    return back,front,(32,34)


def body_layers(palette: str, pal: dict, style: dict | None = None):
    """Open industrial cockpit, with explicit load-bearing frame and low cowl.

    Native coordinates are the contract with MechCatalog: head (26,15), near
    shoulder (42,44), hips (20/36,56). Ornament must not occupy the face window.
    """
    W, H, PX, PY = 64, 60, 28, 30
    back, front = arr(W, H), arr(W, H)
    style = style or {"id": "champion_open"}
    ident = style.get("id", "champion_open")
    if ident in ("champion_open", "sb_avian", "sb_avianproto"):
        return champion_cockpit(pal)
    avian = ident == "champion_open" or ident in ("sb_avian", "sb_avianproto")
    human = ident == "venturer_open" or ident in ("sb_human", "sb_humanproto", "sb_military")
    primary = pal["primary"]
    accent = ramp_palette(style.get("theme_secondary", pal["secondary"][3]))
    steel = [(18, 25, 29), (33, 44, 48), (56, 69, 71), (90, 103, 100), (137, 147, 135)]
    shell = IVORY if avian else primary

    def panel(target, pts, ramp, seed=0):
        mask = poly_mask(W, H, pts)
        paint(target, mask, ramp, seed=seed, noise=0, bottom_darken=0.08)
        return mask

    # Rear power spine, seat rails and a single paired exhaust. No decorative dome.
    panel(back, [(5,22),(10,17),(18,18),(20,40),(15,47),(6,43)], steel)
    panel(back, [(6,26),(13,24),(15,37),(7,38)], primary)
    for y in (28,32,36): line(back, [(7,y),(12,y)], OUTLINE)
    panel(back, [(13,8),(18,6),(21,12),(20,29),(16,34),(12,29)], steel)
    panel(back, [(16,13),(20,13),(20,27),(17,29)], accent)
    line(back, [(13,10),(13,31),(22,38)], steel[3], width=2)
    # Dorsal feathers are short plates behind the seat, not a crown over the pilot.
    if avian:
        panel(back, [(7,22),(8,12),(13,3),(17,4),(14,14),(13,23)], shell)
        panel(back, [(3,26),(4,18),(8,11),(11,12),(9,22)], shell)
        line(back, [(9,18),(13,8)], shell[2])
    elif human:
        panel(back, [(6,14),(6,6),(10,6),(12,16)], steel)
        line(back, [(7,7),(10,7)], steel[3])
    else:
        # Other catalogue families retain their theme, without inheriting a bird head.
        deco = style.get("deco", "blocky")
        tip = 5 if deco in ("fin", "leaf", "horns") else 11
        panel(back, [(5,27),(5,14),(12,tip),(17,12),(14,28)], primary)
        line(back, [(7,20),(12,12)], accent[4])
    # Seat and low interior console behind the visible real torso.
    panel(back, [(17,29),(33,29),(39,34),(34,41),(18,40),(13,35)], steel)
    panel(back, [(19,30),(31,30),(31,34),(19,35)], accent)
    panel(back, [(36,24),(42,24),(45,29),(41,34),(35,31)], steel)
    line(back, [(38,26),(41,26)], pal["glow"])

    # Narrow, tapered hull. Broad clean planes instead of round nested plates.
    panel(front, [(13,32),(19,29),(35,29),(44,34),(46,43),(39,49),(23,49),(14,43)], primary)
    panel(front, [(14,32),(20,30),(35,30),(43,34),(41,37),(19,35)], accent)
    line(front, [(17,32),(34,32),(41,35)], shell[4])
    panel(front, [(17,38),(23,36),(36,37),(40,42),(35,47),(22,46)], shell)
    line(front, [(21,39),(33,39)], shell[2])
    line(front, [(22,45),(34,46)], OUTLINE)
    # A small asymmetric service hatch supplies scale without pixel speckle.
    panel(front, [(23,40),(29,40),(29,44),(23,44)], steel)
    dot(front, 25, 41, pal["glow"])
    rivets(front, [(18,36),(35,35),(37,44)], steel[4])

    # Integrated prow below the pilot's head, never a second giant animal head.
    if avian:
        panel(front, [(39,27),(46,27),(55,32),(52,35),(43,34),(39,32)], shell)
        line(front, [(43,32),(52,33)], shell[2])
        panel(front, [(39,25),(44,25),(46,28),(40,29)], steel)
        dot(front, 42, 27, pal["glow"])
    else:
        panel(front, [(39,27),(49,27),(51,34),(44,37),(39,33)], primary)
        for y in (29,32): line(front, [(43,y),(48,y)], steel[0])
        dot(front, 47, 28, pal["glow"])

    # Pelvic crossmember connects the body to both animated hips. Armor ends above it.
    panel(back, [(17,45),(38,45),(42,52),(38,57),(17,57),(13,52)], steel)
    panel(front, [(25,47),(32,47),(33,53),(28,56),(24,52)], accent)
    for cx in (20,36):
        disc = ellipse_mask(W,H,cx,55,5,4)
        paint(back, disc, steel, noise=0)
        back[ellipse_mask(W,H,cx,55,2,2)] = rgba(steel[1])
    # The tool axis is visible at the real rig pivot, rather than floating at the beak.
    mount = ellipse_mask(W,H,42,44,6,6)
    paint(front,mount,steel,noise=0)
    front[ellipse_mask(W,H,42,44,3,3)] = rgba(accent[3])
    front[ellipse_mask(W,H,42,44,1,1)] = OUTLINE
    return back, front, (PX,PY)


def body_icons(palette: str, pal: dict, body_back, body_front):
    # Use the actual front layer as the icon source, with a small transparent border.
    src = Image.alpha_composite(Image.fromarray(body_back, "RGBA"), Image.fromarray(body_front, "RGBA"))
    bbox = src.getbbox()
    if bbox is None:
        return Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    crop = src.crop(bbox)
    crop.thumbnail((22, 22), Image.NEAREST)
    icon = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    icon.alpha_composite(crop, ((24 - crop.width) // 2, (24 - crop.height) // 2))
    return icon


# ---------------------------------------------------------------------------
# Legs
# ---------------------------------------------------------------------------

def leg_layers(palette: str, pal: dict, kind: str):
    # Actual joint axes are 9 native pixels apart, matching the runtime legs.
    steel = [(16,23,27),(32,42,46),(54,67,70),(87,102,104),(137,152,149)]
    thigh, shin, foot = arr(18,17), arr(16,17), arr(24,10)
    def plate(target, points, ramp):
        mask = poly_mask(target.shape[1],target.shape[0],points)
        paint(target,mask,ramp,noise=0)
    # Hip socket, thigh box section, visible knee bearing.
    plate(thigh, [(3,3),(6,0),(12,0),(16,4),(15,11),(12,15),(5,14),(2,9)], steel)
    plate(thigh, [(5,4),(12,3),(14,6),(12,11),(5,10)], pal["primary"])
    line(thigh,[(5,5),(11,5)],pal["primary"][4])
    for cy in (3,12):
        paint(thigh,ellipse_mask(18,17,9,cy,3,3),steel,noise=0)
        thigh[ellipse_mask(18,17,9,cy,1,1)] = rgba(steel[1])
    # Two-tone greave and one exposed hydraulic rod, rather than a chain of circles.
    plate(shin,[(3,2),(10,0),(14,4),(13,11),(10,15),(4,15),(2,10)],steel)
    plate(shin,[(7,4),(12,4),(12,11),(9,13),(6,10)],pal["primary"])
    line(shin,[(4,5),(4,11)],steel[4],width=2)
    line(shin,[(8,5),(11,5)],pal["accent"][3])
    paint(shin,ellipse_mask(16,17,8,3,3,3),steel,noise=0)
    shin[ellipse_mask(16,17,8,3,1,1)] = OUTLINE
    if kind == "hopper":
        for y in (6,8,10): line(shin,[(5,y),(10,y)],pal["accent"][3])
    # Raised ankle and short chamfered toe; the sole is visibly load-bearing.
    plate(foot,[(4,3),(8,1),(13,1),(16,4),(21,5),(23,7),(21,9),(2,9),(1,6)],steel)
    plate(foot,[(5,4),(13,3),(17,5),(19,6),(4,6)],pal["primary"])
    line(foot,[(3,8),(21,8)],steel[0],width=2)
    for x in (5,10,15,20): dot(foot,x,8,steel[2])
    return {"thigh":(thigh,(9,3)),"shin":(shin,(8,3)),"foot":(foot,(10,3)),
            "thigh_len":9,"shin_len":9}


# ---------------------------------------------------------------------------
# Arms
# ---------------------------------------------------------------------------

def arm_layers(palette: str, pal: dict, kind: str):
    W, H, PX, PY = 34, 16, 6, 8
    body = arr(W, H)
    # shared mount + upper arm + elbow
    mount = ellipse_mask(W, H, 7, 8, 6, 6)
    paint(body, mount, pal["secondary"], seed=201, noise=0.015)
    body[ellipse_mask(W, H, 7, 8, 2, 2)] = (8, 12, 14, 255)
    upper = poly_mask(W, H, [(6, 5), (16, 5), (19, 8), (16, 11), (6, 11), (4, 8)])
    paint(body, upper, pal["dark"], seed=202, noise=0)
    elbow = ellipse_mask(W, H, 18, 8, 4, 4)
    paint(body, elbow, pal["dark"], seed=203, noise=0.01)
    forearm = poly_mask(W, H, [(18, 4), (27, 5), (30, 8), (27, 11), (18, 12), (16, 8)])
    paint(body, forearm, pal["primary"], seed=204, noise=0)
    hazard(body, forearm, 20, 26, 8, 11, pal["accent"], phase=2)
    line(body, [(8, 4), (8, 12)], (7, 11, 13, 255), width=2)
    extra = {}
    if kind == "drill":
        bit0 = arr(20, 20)
        bit1 = arr(20, 20)
        for b, phase in ((bit0, 0), (bit1, 1)):
            tip = poly_mask(20, 20, [(1, 1), (11, 4), (19, 10), (11, 16), (1, 19)])
            paint(b, tip, [(25,31,36),(48,60,65),(79,93,98),(120,135,137),(171,182,175),(209,215,198)], seed=210 + phase, noise=0)
            for i in range(3):
                x = 4 + i * 5 + phase
                line(b, [(x, 3), (x + 1, 17)], (9, 13, 15, 255), width=2)
                line(b, [(x + 1, 4), (x + 2, 16)], (159,173,175,255))
            b[~tip] = (0,0,0,0)
            b[tip & ~erode(tip)] = OUTLINE
        extra["bit0"] = (bit0, (1, 10))
        extra["bit1"] = (bit1, (1, 10))
        tip_x, muzzle_x = 30, 30
    elif kind == "cannon":
        barrel = poly_mask(W, H, [(26, 6), (34, 6), (34, 10), (26, 10)])
        paint(body, barrel, pal["primary"], seed=221, noise=0.01)
        line(body, [(27, 7), (33, 7)], pal["secondary"][4])
        line(body, [(27, 9), (32, 9)], (8, 12, 14, 255))
        brake = rect_mask(W, H, (30, 5, 34, 11)) & barrel
        paint(body, brake, pal["dark"], seed=222, noise=0.01)
        flash0 = arr(18, 18)
        flash1 = arr(18, 18)
        for f, seed in ((flash0, 231), (flash1, 232)):
            for i, (rx, ry) in enumerate(((8, 2), (5, 5), (2, 8))):
                m = poly_mask(18, 18, [(0, 8), (2, 6 - i), (10, 7), (18, 8), (10, 10 + i), (2, 11)])
                paint(f, m, pal["accent"], seed=seed + i, outline=False, noise=0.01)
            f[ellipse_mask(18, 18, 2, 8, 2, 3)] = pal["glow"]
        extra["flash0"] = (flash0, (3, 9))
        extra["flash1"] = (flash1, (3, 9))
        tip_x, muzzle_x = 34, 33
    else:  # beam
        emitter = poly_mask(W, H, [(26, 5), (31, 5), (33, 8), (31, 11), (26, 11), (24, 8)])
        paint(body, emitter, pal["secondary"], seed=241, noise=0.01)
        line(body, [(27, 7), (31, 7)], pal["glow"])
        line(body, [(27, 9), (31, 9)], (8, 12, 14, 255))
        glow = arr(72, 8)
        # tapered energy beam sprite, left pivot
        for x in range(72):
            a = max(0.0, 1.0 - x / 72.0)
            hh = 1 + int(round(2.2 * a))
            for y in range(4 - hh, 5 + hh):
                if 0 <= y < 8:
                    glow[y, x] = rgba(pal["glow"] if abs(y - 4) <= 1 else pal["accent"][4])
        extra["glow"] = (glow, (1, 4))
        tip_x, muzzle_x = 36, 33
    return body, (PX, PY), extra, tip_x, muzzle_x


# ---------------------------------------------------------------------------
# Booster and horn
# ---------------------------------------------------------------------------

def booster_layers(palette: str, pal: dict, kind: str):
    W, H, PX, PY = 24, 28, 12, 24
    b = arr(W, H)
    shell = poly_mask(W, H, [(3, 3), (14, 2), (21, 6), (22, 18), (17, 24), (8, 25), (3, 20), (1, 9)])
    paint(b, shell, pal["primary"], seed=301, noise=0.025)
    for x in (5, 11, 17):
        line(b, [(x, 5), (x, 18)], (8, 12, 14, 255), width=2)
        line(b, [(x + 1, 6), (x + 1, 17)], pal["secondary"][4])
    tank = rect_mask(W, H, (4, 8, 10, 16)) & shell
    paint(b, tank, pal["secondary"], seed=302, noise=0.01)
    if kind == "twinjet":
        for x in (5, 16):
            nozzle = poly_mask(W, H, [(x - 2, 22), (x + 5, 22), (x + 6, 28), (x - 3, 28)])
            paint(b, nozzle, pal["dark"], seed=311 + x, noise=0.01)
            b[rect_mask(W, H, (x, 24, x + 3, 28))] = (8, 12, 14, 255)
    else:
        for x in (5, 15):
            nozzle = poly_mask(W, H, [(x - 2, 22), (x + 4, 22), (x + 5, 28), (x - 3, 28)])
            paint(b, nozzle, pal["dark"], seed=321 + x, noise=0.01)
            b[rect_mask(W, H, (x, 24, x + 2, 28))] = (8, 12, 14, 255)
    # warning chevron
    hazard(b, shell, 14, 21, 8, 13, pal["accent"], phase=1)
    flame0 = arr(14, 26)
    flame1 = arr(14, 26)
    for f, seed in ((flame0, 331), (flame1, 332)):
        m = poly_mask(14, 26, [(3, 1), (10, 1), (12, 8), (9, 25), (5, 17), (2, 8)])
        paint(f, m, pal["accent"], seed=seed, outline=False, noise=0.01)
        f[rect_mask(14, 26, (5, 2, 8, 12)) & m] = pal["glow"]
    return b, (PX, PY), flame0, flame1


def horn_layers(pal: dict):
    W, H, PX, PY = 14, 10, 2, 9
    h = arr(W, H)
    m = poly_mask(W, H, [(1, 3), (7, 2), (12, 5), (13, 8), (10, 9), (3, 8), (0, 6)])
    paint(h, m, pal["accent"], seed=401, noise=0.02)
    line(h, [(4, 5), (11, 6)], (8, 12, 14, 255), width=2)
    return h, (PX, PY)


# ---------------------------------------------------------------------------
# Main generation
# ---------------------------------------------------------------------------

def main():
    manifest = {}
    previews = []
    body_specs = [("champion_open", {"id": "champion_open"}), ("venturer_open", {"id": "venturer_open"})]
    body_specs += [(b["id"], b) for b in CATALOG.get("bodies", [])]
    preview_ids = {"champion_open", "venturer_open"} | {
        b["id"] for b in CATALOG.get("bodies", [])[:8]
    }
    for palette, pal in PALETTES.items():
        # body
        for body_id, style in body_specs:
            back, front, (px, py) = body_layers(palette, pal, style)
            save(back, f"body_{body_id}_back_{palette}", (px, py), manifest, *back.shape[1::-1])
            save(front, f"body_{body_id}_front_{palette}", (px, py), manifest, *front.shape[1::-1])
            if palette == "bone":
                icon = body_icons(palette, pal, back, front)
                icon.save(ASSETS / f"icon_body_{body_id}.png")
                manifest[f"icon_body_{body_id}"] = {"w": 24, "h": 24, "pivot": [12, 12], "ppu": PPU}
            # preview (local contact sheet only; not part of release)
            if body_id in preview_ids:
                canvas = Image.new("RGBA", (back.shape[1] * 3, back.shape[0] * 3), (24, 26, 30, 255))
                im = Image.alpha_composite(Image.fromarray(back, "RGBA"), Image.fromarray(front, "RGBA"))
                im = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
                canvas.alpha_composite(im, (0, 0))
                previews.append((f"{body_id}_{palette}", canvas))

        # legs: four art families cover the eight Starbound leg IDs.
        for leg_id in ("strider", "hopper", "arc", "sleek"):
            base_kind = "hopper" if leg_id in ("hopper", "sleek") else "strider"
            data = leg_layers(palette, pal, base_kind)
            for layer in ("thigh", "shin", "foot"):
                img, pivot = data[layer]
                save(img, f"leg_{leg_id}_{layer}_{palette}", pivot, manifest, *img.shape[1::-1])
            if palette == "bone":
                # compose an icon from the three layers
                icon = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
                t, (tx, ty) = data["thigh"]
                s, (sx, sy) = data["shin"]
                f, (fx, fy) = data["foot"]
                # rough stacked icon
                def paste_layer(im, px, py):
                    x = (24 - im.width) // 2 + px
                    y = (24 - im.height) // 2 + py
                    icon.alpha_composite(im, (x, y))
                paste_layer(Image.fromarray(f, "RGBA"), 0, 8)
                paste_layer(Image.fromarray(s, "RGBA"), 0, 3)
                paste_layer(Image.fromarray(t, "RGBA"), 0, -2)
                icon.save(ASSETS / f"icon_legs_{leg_id}.png")
                manifest[f"icon_legs_{leg_id}"] = {"w": 24, "h": 24, "pivot": [12, 12], "ppu": PPU}

        # arms
        for arm_id in ("drill", "cannon", "beam"):
            body, pivot, extra, tipx, muzzlex = arm_layers(palette, pal, arm_id)
            save(body, f"arm_{arm_id}_{palette}", pivot, manifest, *body.shape[1::-1])
            for k, (img, pv) in extra.items():
                save(img, f"arm_{arm_id}_{k}_{palette}", pv, manifest, *img.shape[1::-1])
            if palette == "bone":
                icon = Image.fromarray(body, "RGBA").crop(Image.fromarray(body, "RGBA").getbbox())
                icon.thumbnail((22, 16), Image.NEAREST)
                ic = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
                ic.alpha_composite(icon, ((24 - icon.width) // 2, (24 - icon.height) // 2))
                ic.save(ASSETS / f"icon_{arm_id}.png")
                manifest[f"icon_{arm_id}"] = {"w": 24, "h": 24, "pivot": [12, 12], "ppu": PPU}

        # boosters
        for booster_id in ("vector", "twinjet"):
            b, pivot, f0, f1 = booster_layers(palette, pal, booster_id)
            save(b, f"booster_{booster_id}_{palette}", pivot, manifest, *b.shape[1::-1])
            save(f0, f"booster_{booster_id}_flame0_{palette}", (7, 1), manifest, *f0.shape[1::-1])
            save(f1, f"booster_{booster_id}_flame1_{palette}", (7, 1), manifest, *f1.shape[1::-1])
            if palette == "bone":
                icon = Image.fromarray(b, "RGBA").crop(Image.fromarray(b, "RGBA").getbbox())
                icon.thumbnail((20, 22), Image.NEAREST)
                ic = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
                ic.alpha_composite(icon, ((24 - icon.width) // 2, (24 - icon.height) // 2))
                ic.save(ASSETS / f"icon_booster_{booster_id}.png")
                manifest[f"icon_booster_{booster_id}"] = {"w": 24, "h": 24, "pivot": [12, 12], "ppu": PPU}

        # horn
        h, pivot = horn_layers(pal)
        save(h, f"horn_siren_{palette}", pivot, manifest, *h.shape[1::-1])
        if palette == "bone":
            ic = Image.fromarray(h, "RGBA").crop(Image.fromarray(h, "RGBA").getbbox())
            ic.thumbnail((20, 16), Image.NEAREST)
            icon = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
            icon.alpha_composite(ic, ((24 - ic.width) // 2, (24 - ic.height) // 2))
            icon.save(ASSETS / "icon_siren.png")
            manifest["icon_siren"] = {"w": 24, "h": 24, "pivot": [12, 12], "ppu": PPU}

    # soft grounding shadow (original)
    W, H, PX, PY = 44, 9, 22, 4
    shadow = arr(W, H)
    yy, xx = np.mgrid[0:H, 0:W]
    m = (((xx - 22) / 21.0) ** 2 + ((yy - 4) / 3.6) ** 2) <= 1.0
    for y, x in zip(*np.nonzero(m)):
        a = int(120 * (1.0 - ((x - 22) / 21.0) ** 2) * (1.0 - ((y - 4) / 3.6) ** 2))
        shadow[y, x] = (4, 8, 10, max(0, a))
    save(shadow, "shadow", (PX, PY), manifest, W, H)

    # beacon: original, not copied from Starbound
    W, H, PX, PY = 20, 24, 10, 12
    beacon = arr(W, H)
    body = poly_mask(W, H, [(4, 5), (16, 5), (18, 12), (15, 20), (5, 20), (2, 12)])
    paint(beacon, body, PALETTES["steel"]["primary"], seed=601, noise=0.02)
    screen = rect_mask(W, H, (6, 8, 14, 16)) & body
    window(beacon, body, 6, 8, 14, 16, PALETTES["steel"])
    line(beacon, [(5, 21), (15, 21)], (8, 12, 14, 255), width=2)
    dot(beacon, 10, 3, PALETTES["steel"]["glow"])
    save(beacon, "beacon", (PX, PY), manifest, W, H)

    # manifest
    (ASSETS / "parts.json").write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")

    # preview sheet: bodies only, 3x
    big = Image.new("RGBA", (max(c.width for _, c in previews) * 2 + 30,
                             (max(c.height for _, c in previews) + 30) * ((len(previews) + 1) // 2) + 30),
                    (24, 26, 30, 255))
    d = ImageDraw.Draw(big)
    for i, (name, im) in enumerate(previews):
        x = 10 + (i % 2) * (max(c.width for _, c in previews) + 20)
        y = 20 + (i // 2) * (max(c.height for _, c in previews) + 30)
        big.alpha_composite(im, (x, y))
        d.text((x, y - 12), name, fill=(220, 220, 220, 255))
    big.save(PREVIEW / "starbound_style_bodies.png")
    print(f"generated {len(manifest)} art entries; preview {PREVIEW / 'starbound_style_bodies.png'}")


if __name__ == "__main__":
    main()
