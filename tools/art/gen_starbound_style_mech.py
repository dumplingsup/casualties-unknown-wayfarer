"""Generate original Starbound-inspired modular mech art for Wayfarer v0.3.

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


def body_layers(palette: str, pal: dict, style: dict | None = None):
    # Extra width on the right is the Avian beak; the pivot stays at x=28 so the
    # existing seat, legs and cockpit coordinates keep their world positions.
    W, H, PX, PY = 64, 60, 28, 30
    back = arr(W, H)
    front = arr(W, H)
    ivory = IVORY
    # New Starbound-catalogue bodies use a themed rust/accent ramp while keeping
    # the same ivory beak/crest. This is an original recolour/shape variant.
    if style:
        themed = dict(pal)
        themed["secondary"] = ramp_palette(style.get("theme_secondary", (90, 52, 38)))
        themed["accent"] = ramp_palette(style.get("theme_accent", (44, 94, 86)))
        pal = themed

    # ---- rear engine / fuel tank (back layer) ----
    eng = poly_mask(W, H, [(2, 28), (5, 16), (13, 11), (25, 14), (29, 25), (25, 41), (16, 48), (6, 44), (2, 37)])
    paint(back, eng, pal["dark"], seed=11, bottom_darken=0.1)
    eng_band = rect_mask(W, H, (4, 25, 28, 32)) & eng
    paint(back, eng_band, pal["primary"], seed=12, noise=0.02)
    for y in (23, 27, 31):
        line(back, [(3, y), (28, y + 1)], (10, 14, 18, 255))
    # circular reactor and vent
    reactor = ellipse_mask(W, H, 13, 36, 6, 6)
    paint(back, reactor, pal["secondary"], seed=13, noise=0.015)
    reactor_core = ellipse_mask(W, H, 13, 36, 2, 2)
    back[reactor_core] = pal["glow"]
    back[reactor & ~erode(reactor)] = OUTLINE
    for i in range(3):
        line(back, [(6 + i * 6, 18), (7 + i * 6, 25)], (8, 15, 17, 255))
    # exhaust stacks
    for x in (4, 9):
        stack = rect_mask(W, H, (x, 7, x + 4, 16)) & eng
        paint(back, stack, pal["secondary"], seed=20 + x, noise=0.02)
        line(back, [(x + 1, 8), (x + 1, 15)], (7, 10, 12, 255))
        line(back, [(x + 3, 8), (x + 3, 15)], (32, 44, 45, 255))

    # ---- seat back / rear cockpit rim ----
    seat = poly_mask(W, H, [(12, 2), (23, 3), (25, 11), (23, 28), (17, 33), (12, 29), (10, 17)])
    paint(back, seat, pal["primary"], seed=31, noise=0.025)
    seat_inner = poly_mask(W, H, [(15, 8), (22, 8), (22, 24), (18, 27), (15, 24)])
    paint(back, seat_inner, pal["secondary"], seed=32, noise=0.015)
    for x in (17, 21):
        line(back, [(x, 10), (x, 24)], (8, 12, 14, 255))
    # roll bar behind the real head
    line(back, [(10, 4), (9, 18), (12, 31)], (9, 12, 14, 255), width=3)
    line(back, [(11, 4), (10, 18), (13, 30)], pal["primary"][4], width=1)
    line(back, [(10, 4), (23, 4), (24, 12)], (9, 12, 14, 255), width=2)
    line(back, [(11, 5), (22, 5), (23, 12)], pal["primary"][5], width=1)
    # antenna
    line(back, [(8, 19), (4, 5), (2, 0)], (8, 12, 16, 255))
    dot(back, 2, 0, pal["glow"])

    # ---- swept avian wing / backpack fin (back layer) ----
    # A single readable Avian silhouette element. It sits behind the seat and the
    # real head, so it frames the cockpit instead of covering the pilot.
    wing = poly_mask(W, H, [(1, 21), (2, 12), (7, 4), (14, 0), (22, 1), (25, 6), (20, 10), (14, 10), (9, 15), (6, 22)])
    paint(back, wing, ivory, seed=80, noise=0.03, bottom_darken=0.18)
    for a, b in [((4, 18), (11, 5)), ((8, 19), (16, 4)), ((12, 19), (20, 6)), ((16, 18), (22, 9))]:
        line(back, [a, b], ivory[3])
    wing_edge = wing & ~erode(wing)
    back[wing_edge & (back[:, :, 3] > 0)] = OUTLINE
    # A connected fan of further crest feathers behind the pilot. The real head
    # covers the roots; the pale tips stay visible above the cockpit.
    for idx, pts in enumerate([
        [(19, 20), (30, 2), (37, 1), (35, 9), (27, 17)],
        [(24, 19), (36, 5), (44, 6), (40, 14), (31, 19)],
        [(28, 19), (41, 10), (47, 13), (42, 19), (34, 21)],
    ]):
        crest = poly_mask(W, H, pts)
        paint(back, crest, ivory, seed=82 + idx, noise=0.03, bottom_darken=0.16)
        base = (min(p[0] for p in pts) + 4, max(p[1] for p in pts) - 2)
        tip = (max(p[0] for p in pts) - 2, min(p[1] for p in pts) + 2)
        line(back, [base, tip], ivory[2 if idx < 2 else 3])
        back[crest & ~erode(crest)] = OUTLINE

    # ---- lower back skirt / hip frame ----
    hipback = poly_mask(W, H, [(3, 42), (16, 45), (24, 40), (26, 49), (17, 54), (5, 50)])
    paint(back, hipback, pal["secondary"], seed=41, noise=0.02)
    add_outline_edges(back, hipback)

    # ---- front chest cowl and fender (front layer) ----
    # The top is deliberately low: the real player's head and a slice of upper torso
    # must remain unobstructed. The raised prow sits to the right of the head.
    cowl = poly_mask(W, H, [(8, 34), (13, 28), (22, 26), (35, 27), (45, 32), (50, 40), (46, 50), (37, 55), (21, 53), (11, 47), (8, 41)])
    paint(front, cowl, pal["primary"], seed=51, noise=0.03, bottom_darken=0.12)
    # low cockpit rim and crash bar in front of the seat, below the pilot's chest
    rim = poly_mask(W, H, [(9, 35), (15, 30), (23, 29), (36, 30), (45, 35), (47, 39), (39, 41), (24, 40), (14, 39)])
    paint(front, rim, pal["secondary"], seed=52, noise=0.02)
    line(front, [(10, 36), (23, 32), (36, 32), (45, 36)], (7, 11, 14, 255), width=2)
    line(front, [(11, 35), (23, 31), (36, 31), (44, 35)], pal["primary"][5])
    # ---- console / control area in front of the pilot's lower torso ----
    console = poly_mask(W, H, [(25, 40), (39, 37), (43, 42), (42, 48), (29, 51), (24, 46)])
    paint(front, console, pal["dark"], seed=61, noise=0.02)
    screen = rect_mask(W, H, (30, 39, 38, 43)) & console
    window(front, console, 30, 39, 38, 43, pal)
    for x in (27, 33, 39):
        line(front, [(x, 44), (x - 1, 48)], pal["primary"][3])
        dot(front, x - 1, 48, pal["glow"])
    # seat base / lap shield
    lap = poly_mask(W, H, [(20, 46), (30, 45), (42, 48), (46, 55), (36, 58), (19, 54)])
    paint(front, lap, pal["primary"], seed=62, noise=0.025)
    hazard(front, lap, 22, 29, 49, 54, pal["accent"], phase=0)
    line(front, [(30, 47), (43, 53)], (8, 12, 15, 255), width=2)
    # front hip mount and shoulder mount
    shoulder = ellipse_mask(W, H, 42, 42, 7, 8)
    paint(front, shoulder, pal["secondary"], seed=71, noise=0.02)
    shoulder_hole = ellipse_mask(W, H, 42, 42, 3, 3)
    front[shoulder_hole] = (9, 15, 17, 255)
    front[shoulder & ~erode(shoulder)] = OUTLINE
    hip = ellipse_mask(W, H, 32, 54, 5, 5)
    paint(front, hip, pal["dark"], seed=72)
    # rivets and wear
    rivets(front, [(17, 38), (24, 34), (35, 35), (44, 42), (22, 51), (39, 55)], pal["primary"][5])
    # bottom step
    step = poly_mask(W, H, [(24, 54), (34, 54), (39, 59), (27, 59)])
    paint(front, step, pal["secondary"], seed=73, noise=0.02)

    # Open-cockpit cutout: the game's real upper torso sits behind this opening.
    # It is punched after the cowl so the front layer never paints over the chest.
    cockpit_hole = poly_mask(W, H, [(17, 17), (37, 16), (43, 24), (39, 31), (27, 33), (17, 28), (13, 22)])
    front[cockpit_hole] = (0, 0, 0, 0)
    hole_edge = cockpit_hole & ~erode(cockpit_hole)
    front[hole_edge] = OUTLINE
    # small harness bar across the opening, below the chin, not across the face
    line(front, [(18, 25), (28, 28), (39, 25)], pal["secondary"][4], width=2)
    line(front, [(18, 24), (28, 27), (39, 24)], (8, 12, 14, 255))
    dot(front, 28, 28, pal["glow"])

    # ---- Avian shell: rust cockpit dome, sensor eye and a large pale beak ----
    # These are original shapes inspired by the reference silhouette, not copied
    # Starbound pixels. The pale beak and crest are the two strongest reads.
    dome = poly_mask(W, H, [(41, 23), (47, 20), (53, 23), (56, 30), (54, 38), (48, 42), (42, 38), (39, 31)])
    paint(front, dome, pal["secondary"], seed=91, noise=0.035, bottom_darken=0.12)
    line(front, [(44, 22), (53, 25), (55, 33)], pal["secondary"][4])
    line(front, [(42, 36), (48, 40)], pal["dark"][3])
    add_outline_edges(front, dome)
    # upper beak
    upper = poly_mask(W, H, [(44, 27), (53, 25), (61, 29), (63, 33), (55, 35), (48, 34), (44, 32)])
    paint(front, upper, ivory, seed=92, noise=0.025, bottom_darken=0.1)
    for y, x0, x1 in ((29, 47, 60), (32, 46, 61)):
        line(front, [(x0, y), (x1, y + 1)], ivory[3])
    add_outline_edges(front, upper)
    # lower beak / jaw
    lower = poly_mask(W, H, [(45, 34), (56, 35), (62, 38), (58, 43), (50, 42), (44, 38)])
    paint(front, lower, ivory, seed=93, noise=0.02, bottom_darken=0.24)
    line(front, [(45, 35), (57, 36), (62, 38)], (8, 12, 14, 255), width=2)
    line(front, [(47, 39), (57, 40)], ivory[2])
    add_outline_edges(front, lower)
    dot(front, 54, 29, (8, 12, 14, 255))
    # sensor eye above the beak root
    socket = ellipse_mask(W, H, 47, 25, 5, 4)
    front[socket] = (8, 12, 14, 255)
    iris = ellipse_mask(W, H, 47, 25, 3, 3)
    front[iris] = rgba(pal["secondary"][4])
    pupil = ellipse_mask(W, H, 48, 25, 1, 2)
    front[pupil] = rgba(pal["glow"])
    dot(front, 46, 24, (255, 255, 255, 255))
    brow = poly_mask(W, H, [(41, 20), (51, 19), (54, 23), (47, 22), (41, 23)])
    paint(front, brow, ivory, seed=94, noise=0.02)
    add_outline_edges(front, brow)

    # pale cockpit rails frame the real head without covering the face
    rail_l = poly_mask(W, H, [(8, 13), (13, 10), (16, 14), (15, 27), (11, 31), (7, 24)])
    rail_r = poly_mask(W, H, [(43, 12), (47, 14), (48, 21), (44, 25), (41, 21), (40, 15)])
    for rail in (rail_l, rail_r):
        paint(front, rail, ivory, seed=95, noise=0.02)
        add_outline_edges(front, rail)
    # pale chest plate below the cockpit, so the open head sits in a bone cradle
    plate = poly_mask(W, H, [(16, 41), (29, 37), (43, 42), (45, 52), (36, 58), (21, 56), (13, 49)])
    paint(front, plate, ivory, seed=96, noise=0.03, bottom_darken=0.12)
    line(front, [(18, 45), (31, 41), (43, 46)], ivory[2])
    hazard(front, plate, 21, 27, 48, 54, pal["accent"], phase=2)
    add_outline_edges(front, plate)

    if style:
        deco = style.get("deco", "wing")
        if deco in ("wing", "crown"):
            extra = poly_mask(W, H, [(29, 21), (40, 2), (47, 1), (44, 9), (36, 19)])
            paint(back, extra, ivory, seed=700, noise=0.03, bottom_darken=0.15)
            line(back, [(31, 19), (43, 5)], ivory[2])
            add_outline_edges(back, extra)
        if deco == "fin":
            fin = poly_mask(W, H, [(1, 33), (4, 18), (12, 9), (22, 13), (18, 22), (20, 31), (12, 39)])
            paint(back, fin, pal["secondary"], seed=701, noise=0.03)
            line(back, [(5, 20), (14, 13), (18, 22)], pal["accent"][4])
            add_outline_edges(back, fin)
        if deco == "leaf":
            leaf = poly_mask(W, H, [(2, 28), (8, 15), (18, 6), (28, 10), (24, 18), (14, 24), (8, 34)])
            paint(back, leaf, pal["accent"], seed=702, noise=0.035)
            for a, b in (((6, 26), (22, 11)), ((10, 28), (25, 16))):
                line(back, [a, b], pal["secondary"][4])
            add_outline_edges(back, leaf)
        if deco == "blocky":
            plate2 = poly_mask(W, H, [(24, 12), (36, 9), (42, 12), (40, 20), (29, 22), (23, 18)])
            paint(back, plate2, pal["primary"], seed=703, noise=0.02)
            line(back, [(26, 14), (39, 12)], pal["secondary"][3])
            add_outline_edges(back, plate2)
        if deco == "reactor":
            core = ellipse_mask(W, H, 14, 34, 7, 7)
            paint(back, core, pal["dark"], seed=704, noise=0.01)
            ring = ellipse_mask(W, H, 14, 34, 4, 4)
            back[ring] = rgba(pal["glow"])
            dot(back, 14, 34, pal["accent"][4])
            add_outline_edges(back, core)
        if deco == "visor":
            visor = poly_mask(W, H, [(43, 22), (53, 21), (57, 24), (54, 27), (45, 26), (42, 24)])
            paint(front, visor, pal["dark"], seed=705, noise=0.01)
            line(front, [(45, 24), (55, 23)], pal["glow"])
            add_outline_edges(front, visor)
        if deco in ("horns", "crown"):
            horn_l = poly_mask(W, H, [(41, 19), (43, 7), (47, 3), (46, 12), (45, 19)])
            horn_r = poly_mask(W, H, [(50, 20), (53, 8), (57, 5), (55, 14), (53, 21)])
            for horn in (horn_l, horn_r):
                paint(front, horn, ivory, seed=706, noise=0.02, bottom_darken=0.2)
                add_outline_edges(front, horn)

    return back, front, (PX, PY)


def body_icons(palette: str, pal: dict, body_back, body_front):
    # Use the actual front layer as the icon source, with a small transparent border.
    src = Image.fromarray(body_front, "RGBA")
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
    if kind == "strider":
        TW, TH, TPX, TPY = 16, 14, 8, 2
        SW, SH, SPX, SPY = 14, 14, 7, 2
        FW, FH, FPX, FPY = 24, 8, 10, 2
        thigh = arr(TW, TH)
        shin = arr(SW, SH)
        foot = arr(FW, FH)
        # thigh: hip disc at top, knee at bottom
        t = poly_mask(TW, TH, [(3, 2), (11, 2), (14, 7), (12, 12), (8, 13), (3, 10), (1, 6)])
        paint(thigh, t, pal["primary"], seed=101, noise=0.02)
        disc = ellipse_mask(TW, TH, 7, 5, 4, 4)
        paint(thigh, disc, pal["secondary"], seed=102, noise=0.01)
        thigh[ellipse_mask(TW, TH, 7, 5, 1, 1)] = (8, 12, 14, 255)
        line(thigh, [(4, 9), (11, 10)], (8, 12, 14, 255))
        # shin: reverse joint
        s = poly_mask(SW, SH, [(4, 1), (10, 2), (12, 7), (9, 12), (4, 11), (2, 7)])
        paint(shin, s, pal["secondary"], seed=111, noise=0.02)
        line(shin, [(4, 3), (5, 10)], pal["primary"][4])
        line(shin, [(9, 3), (8, 9)], (8, 12, 14, 255))
        # foot: broad engineering pad
        f = poly_mask(FW, FH, [(2, 3), (16, 2), (22, 4), (22, 6), (17, 7), (3, 7)])
        paint(foot, f, pal["dark"], seed=121, noise=0.02)
        tread = rect_mask(FW, FH, (4, 5, 20, 7)) & f
        paint(foot, tread, pal["secondary"], seed=122, noise=0.01)
        return {
            "thigh": (thigh, (TPX, TPY)), "shin": (shin, (SPX, SPY)), "foot": (foot, (FPX, FPY)),
            "thigh_len": 10, "shin_len": 10,
        }
    else:  # hopper
        TW, TH, TPX, TPY = 18, 12, 9, 2
        SW, SH, SPX, SPY = 16, 12, 8, 2
        FW, FH, FPX, FPY = 26, 9, 11, 2
        thigh = arr(TW, TH)
        shin = arr(SW, SH)
        foot = arr(FW, FH)
        t = poly_mask(TW, TH, [(4, 1), (12, 2), (16, 6), (13, 10), (7, 11), (2, 7)])
        paint(thigh, t, pal["secondary"], seed=151, noise=0.02)
        coil = ellipse_mask(TW, TH, 9, 6, 4, 3)
        paint(thigh, coil, pal["accent"], seed=152, noise=0.01)
        thigh[ellipse_mask(TW, TH, 9, 6, 1, 1)] = (8, 12, 14, 255)
        s = poly_mask(SW, SH, [(3, 1), (11, 1), (14, 5), (12, 10), (4, 10), (1, 6)])
        paint(shin, s, pal["primary"], seed=161, noise=0.02)
        line(shin, [(4, 3), (7, 9)], (8, 12, 14, 255), width=2)
        f = poly_mask(FW, FH, [(1, 4), (18, 2), (24, 5), (24, 7), (18, 8), (2, 8)])
        paint(foot, f, pal["dark"], seed=171, noise=0.02)
        paint(foot, rect_mask(FW, FH, (3, 6, 23, 8)) & f, pal["accent"], seed=172, noise=0.01)
        return {
            "thigh": (thigh, (TPX, TPY)), "shin": (shin, (SPX, SPY)), "foot": (foot, (FPX, FPY)),
            "thigh_len": 8, "shin_len": 8,
        }


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
    paint(body, upper, pal["primary"], seed=202, noise=0.02)
    elbow = ellipse_mask(W, H, 18, 8, 4, 4)
    paint(body, elbow, pal["dark"], seed=203, noise=0.01)
    forearm = poly_mask(W, H, [(18, 4), (27, 5), (30, 8), (27, 11), (18, 12), (16, 8)])
    paint(body, forearm, pal["secondary"], seed=204, noise=0.02)
    hazard(body, forearm, 20, 26, 8, 11, pal["accent"], phase=2)
    line(body, [(8, 4), (8, 12)], (7, 11, 13, 255), width=2)
    extra = {}
    if kind == "drill":
        bit0 = arr(14, 12)
        bit1 = arr(14, 12)
        for b, phase in ((bit0, 0), (bit1, 1)):
            tip = poly_mask(14, 12, [(1, 2), (8, 3), (13, 6), (8, 9), (1, 10)])
            paint(b, tip, pal["primary"], seed=210 + phase, noise=0.015)
            for i in range(3):
                x = 4 + i * 3 + phase
                line(b, [(x, 3), (x + 1, 9)], (9, 13, 15, 255), width=2)
                line(b, [(x + 1, 3), (x + 2, 9)], pal["secondary"][4])
            b[ellipse_mask(14, 12, 12, 6, 1, 2)] = pal["glow"]
        extra["bit0"] = (bit0, (1, 6))
        extra["bit1"] = (bit1, (1, 6))
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
    body_specs = [("champion_open", None), ("venturer_open", None)]
    body_specs += [(b["id"], b) for b in CATALOG.get("bodies", [])]
    preview_ids = {"champion_open", "venturer_open"} | {
        b["id"] for b in CATALOG.get("bodies", [])[:8]
    }
    for palette, pal in PALETTES.items():
        # body
        for body_id, style in body_specs:
            back, front, (px, py) = body_layers(palette, pal, style)
            # A small but real silhouette difference for the second body.
            if body_id == "venturer_open":
                # Recolour/widen the front cowl by adding a grille and twin exhausts.
                W, H = front.shape[1], front.shape[0]
                grille = rect_mask(W, H, (22, 33, 39, 40))
                paint(front, grille, pal["dark"], seed=501, noise=0.01)
                for x in range(24, 39, 4):
                    line(front, [(x, 34), (x, 39)], (8, 12, 14, 255))
                for x in (3, 9):
                    line(back, [(x, 2), (x, 12)], (8, 12, 14, 255), width=2)
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
