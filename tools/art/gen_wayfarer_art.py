"""Generate original Wayfarer v0.2.1 layered pixel art (native PPU=8).

This script is the editable source. Re-run it after changing shapes or palette.
It does not read/copy game files or atlas pixels.

Reference-only provenance (no atlas pixels are copied into these assets):
  Angais/pixel-art-atlas-1.4m @ bf78f8c8cbafa6d07718111a94d41e2dd79ddaa7
  sample row_ids inspected: 34050, 15282, 24216 (power armor chestplate),
  18400, 23759 (pauldron), 16547, 11149 (boots), 9746, 10178 (jetpack),
  3141, 6468 (power drill), 11879 (robot arm), 195, 1674 (generator).
See docs/ART-ASSETS.md for notes and license caveats.
"""
from __future__ import annotations

import json
import math
import random
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / "src" / "WayfarerMech" / "Assets"
PREVIEW_DIR = ROOT / "tools" / "art" / "preview"
ASSET_DIR.mkdir(parents=True, exist_ok=True)
PREVIEW_DIR.mkdir(parents=True, exist_ok=True)

random.seed(0x57A7)
np.random.seed(0x57A7)

# Low-saturation steel/olive, rust and amber accents, dim glass.
OUTLINE = (9, 15, 24, 255)
STEEL = [
    (17, 23, 31, 255), (28, 36, 46, 255), (41, 52, 64, 255),
    (58, 71, 84, 255), (80, 95, 109, 255), (108, 124, 137, 255), (150, 165, 175, 255),
]
OLIVE = [
    (27, 31, 26, 255), (39, 45, 36, 255), (53, 60, 47, 255),
    (71, 79, 63, 255), (93, 102, 83, 255), (123, 132, 109, 255),
]
RUST = [
    (48, 29, 20, 255), (70, 43, 29, 255), (97, 61, 40, 255),
    (132, 88, 54, 255), (170, 121, 75, 255),
]
AMBER = [(84, 51, 20, 255), (129, 80, 29, 255), (187, 118, 45, 255), (224, 162, 83, 255), (245, 208, 148, 255)]
GLASS = [(7, 20, 25, 255), (11, 30, 36, 255), (19, 52, 57, 255), (36, 88, 86, 255), (86, 148, 140, 255)]
GLINT = (177, 222, 214, 255)
SOOT = (11, 11, 11, 255)
WHITE = (235, 240, 235, 255)


def array(w: int, h: int) -> np.ndarray:
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


def paint(arr: np.ndarray, mask: np.ndarray, ramp, seed: int = 0, noise: float = 0.045,
          outline: bool = True, rim: bool = True, bottom_darken: float = 0.0):
    """Coherent top-left lighting with a little dither; 1px coloured outline."""
    if not mask.any():
        return
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    nx = (xs - x0) / max(1, x1 - x0)
    ny = (ys - y0) / max(1, y1 - y0)
    rng = np.random.default_rng(seed)
    # Broad light bands plus a small amount of pixelscale dither.
    light = 0.28 + 0.46 * (1.0 - ny) + 0.18 * (1.0 - nx) - bottom_darken * ny
    light += rng.normal(0.0, noise, len(xs))
    idx = np.clip(np.rint(light * (len(ramp) - 1)), 0, len(ramp) - 1).astype(int)
    arr[ys, xs] = np.asarray(ramp, dtype=np.uint8)[idx]
    if rim:
        top = mask & ~np.roll(mask, 1, axis=0)
        left = mask & ~np.roll(mask, 1, axis=1)
        for y, x in zip(*np.nonzero(top | left)):
            arr[y, x] = ramp[min(len(ramp) - 1, 4)]
    if outline:
        arr[mask & ~erode(mask)] = OUTLINE


def line(arr: np.ndarray, pts, color, width=1):
    im = Image.fromarray(arr, "RGBA")
    ImageDraw.Draw(im).line([(int(round(x)), int(round(y))) for x, y in pts], fill=color, width=width)
    arr[:] = np.asarray(im)


def dotted(arr: np.ndarray, pts, color):
    h, w = arr.shape[:2]
    for x, y in pts:
        if 0 <= int(x) < w and 0 <= int(y) < h:
            arr[int(y), int(x)] = color


def weather(arr: np.ndarray, mask: np.ndarray, seed: int, strength=0.35):
    """Sparse rust chips/streaks biased to the lower half; never a full repaint."""
    rng = np.random.default_rng(seed)
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return
    y0, y1 = ys.min(), ys.max()
    p = (0.010 + 0.075 * np.clip((ys - y0) / max(1, y1 - y0), 0, 1)) * (0.6 + strength)
    for y, x, prob in zip(ys, xs, p):
        if rng.random() < prob:
            arr[y, x] = RUST[min(len(RUST) - 1, int(rng.random() * 3))]
    # A few short vertical streaks from the lower edge.
    for _ in range(max(1, int(strength * 3))):
        x = int(rng.integers(0, mask.shape[1]))
        col = np.nonzero(mask[:, x])[0]
        if len(col) < 5:
            continue
        start = int(rng.integers(col.min() + 2, max(col.min() + 3, col.max() - 2)))
        for y in range(start, min(mask.shape[0], start + int(rng.integers(2, 4)))):
            if mask[y, x]:
                arr[y, x] = RUST[1 + int(rng.random() * 2)]


def hazard(arr: np.ndarray, mask: np.ndarray, x0, x1, y0, y1, phase=0):
    for y in range(y0, y1):
        for x in range(x0, x1):
            if 0 <= y < mask.shape[0] and 0 <= x < mask.shape[1] and mask[y, x]:
                arr[y, x] = AMBER[2] if ((x + y + phase) // 3) % 2 == 0 else (34, 33, 27, 255)


def rivets(arr, pts):
    for x, y in pts:
        dotted(arr, [(x, y)], STEEL[5])
        dotted(arr, [(x + 1, y + 1)], OUTLINE)


def glass(arr: np.ndarray, mask: np.ndarray, w, h, box):
    x0, y0, x1, y1 = box
    m = rect_mask(w, h, box) & mask
    ys, xs = np.nonzero(m)
    if len(xs) == 0:
        return
    for y, x in zip(ys, xs):
        t = (x - x0) / max(1, x1 - x0)
        u = (y - y0) / max(1, y1 - y0)
        idx = int(np.clip(round((1.0 - u) * 2 + t * 1.5), 0, len(GLASS) - 1))
        arr[y, x] = GLASS[idx]
    arr[m & ~erode(m)] = (47, 35, 25, 255)
    for i in range(2):
        if y0 + 2 + i < h and x0 + 2 + i < w:
            arr[y0 + 2 + i, x0 + 2 + i] = GLINT
    if y0 + 2 < h and x1 - 2 >= 0:
        arr[y0 + 2, x1 - 2] = GLASS[3]


@dataclass
class Part:
    name: str
    img: np.ndarray
    pivot: tuple[int, int]
    sort: int
    offset: tuple[int, int] = (0, 0)


def make_backpack() -> Part:
    w, h = 18, 28
    a = array(w, h)
    pack = poly_mask(w, h, [(2, 6), (8, 1), (15, 2), (17, 9), (16, 22), (12, 26), (5, 25), (2, 19)])
    paint(a, pack, STEEL[1:], seed=31, bottom_darken=0.08)
    tank = poly_mask(w, h, [(3, 2), (9, 1), (12, 3), (12, 9), (8, 11), (4, 9)])
    paint(a, tank, OLIVE, seed=32, noise=0.025)
    line(a, [(3, 5), (12, 5)], OUTLINE)
    line(a, [(3, 8), (12, 8)], (145, 152, 134, 255))
    line(a, [(3, 3), (1, 9), (3, 14)], (5, 9, 13, 255))
    line(a, [(4, 3), (2, 9), (4, 14)], STEEL[4])
    for x in (4, 9):
        n = rect_mask(w, h, (x, 19, x + 4, 26))
        paint(a, n, STEEL[:4], seed=33 + x)
        line(a, [(x + 1, 21), (x + 1, 25)], SOOT)
        line(a, [(x + 3, 21), (x + 3, 25)], (38, 38, 36, 255))
        a[26, x + 1:x + 4] = SOOT
    hazard(a, pack, 12, 16, 14, 19, phase=0)
    rivets(a, [(4, 14), (13, 13), (7, 22)])
    weather(a, pack, seed=34, strength=0.45)
    return Part("backpack", a, (14, 24), 6, (-17, -1))


def make_pelvis() -> Part:
    w, h = 26, 13
    a = array(w, h)
    m = poly_mask(w, h, [(2, 3), (20, 1), (25, 5), (22, 11), (5, 12), (1, 8)])
    paint(a, m, STEEL, seed=41, bottom_darken=0.06)
    plate = poly_mask(w, h, [(4, 2), (16, 2), (20, 6), (16, 10), (5, 10)])
    paint(a, plate, OLIVE, seed=42)
    line(a, [(2, 6), (8, 7), (16, 6)], OUTLINE)
    for cx in (6, 20):
        disc = ellipse_mask(w, h, cx, 6, 2.3, 2.3)
        paint(a, disc, STEEL[3:], seed=43 + cx)
        dotted(a, [(cx, 6)], STEEL[6])
    hazard(a, plate, 11, 19, 4, 8, phase=2)
    rivets(a, [(4, 4), (12, 8), (22, 9)])
    weather(a, m, seed=44, strength=0.35)
    return Part("pelvis", a, (13, 5), 22, (0, 3))


def make_thigh(front: bool) -> Part:
    w, h = 14, 14
    a = array(w, h)
    m = poly_mask(w, h, [(2, 2), (12, 2), (13, 6), (10, 13), (4, 13), (1, 7)])
    ramp = STEEL if front else STEEL[:5]
    paint(a, m, ramp, seed=51 + (1 if front else 0), bottom_darken=0.06)
    plate = poly_mask(w, h, [(3, 5), (11, 5), (11, 11), (3, 11)])
    paint(a, plate, OLIVE if front else OLIVE[:4], seed=52 + (1 if front else 0), noise=0.025)
    paint(a, ellipse_mask(w, h, 7, 2.2, 2.6, 2.0), STEEL[3:], seed=53, rim=False)
    paint(a, ellipse_mask(w, h, 7, 12, 2.4, 1.8), STEEL[2:], seed=54, rim=False)
    line(a, [(2, 7), (2, 12)], (7, 13, 17, 255))
    line(a, [(3, 7), (3, 12)], (128, 142, 150, 255))
    rivets(a, [(4, 4), (10, 8)])
    if front:
        dotted(a, [(12, 4), (12, 5)], AMBER[2])
    weather(a, m, seed=55 + (1 if front else 0), strength=0.3)
    return Part("thigh_front" if front else "thigh_back", a, (7, 2), 30 if front else 10)


def make_shin(front: bool) -> Part:
    w, h = 12, 14
    a = array(w, h)
    m = poly_mask(w, h, [(2, 2), (10, 2), (11, 7), (8, 13), (4, 13), (1, 8)])
    ramp = STEEL if front else STEEL[:5]
    paint(a, m, ramp, seed=61 + (1 if front else 0), bottom_darken=0.06)
    plate = poly_mask(w, h, [(3, 5), (9, 5), (9, 11), (3, 11)])
    paint(a, plate, OLIVE if front else OLIVE[:4], seed=62 + (1 if front else 0), noise=0.025)
    paint(a, ellipse_mask(w, h, 6, 2.2, 2.2, 1.6), STEEL[3:], seed=63, rim=False)
    paint(a, ellipse_mask(w, h, 6, 12.2, 2.1, 1.5), STEEL[2:], seed=64, rim=False)
    line(a, [(8, 4), (9, 8)], (124, 137, 145, 255))
    line(a, [(9, 4), (10, 8)], (16, 21, 27, 255))
    weather(a, m, seed=65 + (1 if front else 0), strength=0.3)
    return Part("shin_front" if front else "shin_back", a, (6, 2), 31 if front else 11)


def make_foot(front: bool) -> Part:
    w, h = 20, 7
    a = array(w, h)
    m = poly_mask(w, h, [(1, 2), (16, 1), (19, 4), (18, 6), (3, 6), (0, 4)])
    ramp = STEEL if front else STEEL[:5]
    paint(a, m, ramp, seed=71 + (1 if front else 0), bottom_darken=0.1)
    top = poly_mask(w, h, [(3, 1), (15, 1), (17, 3), (4, 3)])
    paint(a, top, OLIVE if front else OLIVE[:4], seed=72 + (1 if front else 0), noise=0.025)
    line(a, [(2, 6), (18, 6)], (5, 8, 12, 255))
    for x in range(4, 18, 3):
        dotted(a, [(x, 5)], (84, 92, 90, 255))
    paint(a, ellipse_mask(w, h, 6, 2, 2.3, 1.4), STEEL[3:], seed=73, rim=False)
    weather(a, m, seed=74 + (1 if front else 0), strength=0.3)
    return Part("foot_front" if front else "foot_back", a, (6, 2), 32 if front else 12)


def make_muzzle_flash(frame=0) -> Part:
    w, h = 18, 16
    a = array(w, h)
    n, cx, cy = 9, 4, 8
    rng = random.Random(300 + frame)
    pts = []
    for i in range(n):
        ang = i / n * math.tau
        r = 7.0 if i % 2 == 0 else 3.0
        r += rng.uniform(-0.8, 0.8)
        pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r * 0.7))
    paint(a, poly_mask(w, h, pts), AMBER, seed=301 + frame, outline=False, rim=False)
    paint(a, poly_mask(w, h, [(2, 6), (6, 4), (10, 8), (6, 12)]), [AMBER[3], AMBER[4], WHITE], seed=302 + frame, outline=False, rim=False)
    paint(a, poly_mask(w, h, [(0, 6), (4, 7), (2, 10)]), AMBER[:3], seed=303 + frame, outline=False, rim=False)
    return Part(f"muzzle_flash_{frame}", a, (4, 8), 50, (0, 0))


def make_flame(frame=0) -> Part:
    w, h = 12, 28
    a = array(w, h)
    rng = random.Random(400 + frame)
    pts = [(6, 0), (10, 4), (9, 9), (11, 14), (8, 19), (8, 24), (6, 27), (4, 23), (3, 18), (1, 13), (3, 8), (2, 4)]
    pts = [(x + rng.uniform(-1, 1), y + rng.uniform(-0.7, 0.7)) for x, y in pts]
    paint(a, poly_mask(w, h, pts), AMBER[:4], seed=401 + frame, outline=False, rim=False)
    paint(a, poly_mask(w, h, [(6, 3), (8, 8), (7, 14), (6, 22), (5, 15), (4, 9)]), [AMBER[2], AMBER[3], AMBER[4], WHITE], seed=402 + frame, outline=False, rim=False)
    paint(a, poly_mask(w, h, [(6, 6), (7, 11), (6, 19), (5, 12)]), [AMBER[4], WHITE], seed=403 + frame, outline=False, rim=False)
    return Part(f"flame_{frame}", a, (6, 1), 8, (-17, 1))


def make_shadow() -> Part:
    w, h = 34, 8
    a = array(w, h)
    m = ellipse_mask(w, h, 17, 4, 16, 3.2)
    ys, xs = np.nonzero(m)
    for y, x in zip(ys, xs):
        a[y, x] = (3, 6, 9, 115)
    return Part("shadow", a, (17, 4), 1, (0, 28))


def make_beacon() -> Part:
    w, h = 20, 24
    a = array(w, h)
    body = poly_mask(w, h, [(3, 5), (14, 3), (17, 6), (17, 17), (14, 21), (4, 21), (1, 17), (1, 8)])
    paint(a, body, STEEL[1:], seed=501)
    top = poly_mask(w, h, [(4, 3), (13, 2), (15, 5), (4, 6)])
    paint(a, top, OLIVE, seed=502, noise=0.025)
    screen = poly_mask(w, h, [(5, 8), (13, 7), (13, 14), (5, 15)])
    paint(a, screen, GLASS, seed=503, outline=True, rim=False)
    line(a, [(8, 13), (8, 9), (11, 9), (11, 13)], GLINT)
    line(a, [(9, 9), (9, 7)], GLINT)
    dotted(a, [(10, 7)], GLINT)
    line(a, [(6, 17), (12, 17)], OUTLINE)
    for x in (7, 10):
        dotted(a, [(x, 18)], AMBER[3])
    line(a, [(15, 6), (18, 2)], STEEL[5])
    dotted(a, [(19, 1)], AMBER[4])
    rivets(a, [(3, 7), (15, 17)])
    weather(a, body, seed=504, strength=0.3)
    return Part("beacon", a, (10, 12), 0)


# ---------------------------------------------------------------------------
# Assembly preview
# ---------------------------------------------------------------------------
def rotate_vec(dx, dy, angle_deg):
    t = math.radians(angle_deg)
    c, s = math.cos(t), math.sin(t)
    return dx * c + dy * s, -dx * s + dy * c


def paste_rotated(canvas: Image.Image, part: Part, target, angle_deg, scale=1, alpha=1.0):
    img = Image.fromarray(part.img, "RGBA")
    if scale != 1:
        img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    pad = Image.new("RGBA", (img.width * 3, img.height * 3), (0, 0, 0, 0))
    ox, oy = img.width, img.height
    pad.alpha_composite(img, (ox, oy))
    px, py = part.pivot[0] * scale + ox, part.pivot[1] * scale + oy
    rot = pad.rotate(angle_deg, resample=Image.NEAREST, center=(px, py))
    if alpha < 1.0:
        arr = np.array(rot)
        arr[:, :, 3] = (arr[:, :, 3].astype(np.float32) * alpha).astype(np.uint8)
        rot = Image.fromarray(arr, "RGBA")
    canvas.alpha_composite(rot, (int(round(target[0] - px)), int(round(target[1] - py))))


# Structural revision keeps source primitives, replacing only authored parts.
import rig_parts
import rig_preview
for _name, _maker in rig_parts.install(globals()).items():
    globals()['make_' + _name] = _maker


def assemble(scale=1, jet=False):
    return rig_preview.render(globals(), scale, jet)


def main():
    parts = {}
    for part in [
        make_torso(), make_cockpit(), make_backpack(), make_pelvis(), make_cannon_mount(),
        make_thigh(False), make_thigh(True), make_shin(False), make_shin(True),
        make_foot(False), make_foot(True), make_drill_arm(),
        make_drill_bit(0), make_drill_bit(1), make_cannon(),
        make_muzzle_flash(0), make_muzzle_flash(1), make_flame(0), make_flame(1),
        make_shadow(), make_beacon(),
    ]:
        parts[part.name] = part
        Image.fromarray(part.img, "RGBA").save(ASSET_DIR / f"wayfarer_{part.name}.png")

    for scale, name in [(1, "assembly_1x.png"), (6, "assembly_6x.png")]:
        im = assemble(scale)
        im.save(PREVIEW_DIR / name)
        for bg_name, bg_color in [("dark", (17, 24, 34, 255)), ("light", (70, 74, 70, 255))]:
            bg = Image.new("RGBA", im.size, bg_color)
            bg.alpha_composite(im)
            bg.convert("RGB").save(PREVIEW_DIR / f"assembly_{bg_name}_{name}")
    jet = assemble(6, jet=True)
    bg = Image.new("RGBA", jet.size, (12, 18, 26, 255))
    bg.alpha_composite(jet)
    bg.convert("RGB").save(PREVIEW_DIR / "assembly_jet_6x.png")

    rig_preview.matrix(globals(), PREVIEW_DIR)

    manifest = {
        "ppu": 8,
        "source": "Original, generated by tools/art/gen_wayfarer_art.py",
        "atlas_reference_revision": "bf78f8c8cbafa6d07718111a94d41e2dd79ddaa7",
        "atlas_sample_row_ids": [34050, 15282, 24216, 18400, 23759, 16547, 11149, 9746, 10178, 3141, 6468, 11879, 195, 1674],
        "parts": {name: {"pixels": [p.img.shape[1], p.img.shape[0]], "pivot": list(p.pivot), "sort": p.sort}
                  for name, p in parts.items()},
    }
    (PREVIEW_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"wrote {len(parts)} parts to {ASSET_DIR}")
    print(f"previews in {PREVIEW_DIR}")


if __name__ == "__main__":
    main()
