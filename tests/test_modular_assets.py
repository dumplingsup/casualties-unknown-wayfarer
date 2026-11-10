"""Validate the current embedded asset manifest, not only the legacy art rig."""
import json
from pathlib import Path
import unittest
from PIL import Image

ASSETS = Path(__file__).resolve().parents[1] / "src/WayfarerMech/Assets"


class ModularAssets(unittest.TestCase):
    def test_every_manifest_entry_matches_its_png(self):
        parts = json.loads((ASSETS / "parts.json").read_text())
        self.assertGreater(len(parts), 300)
        self.assertEqual(set(parts), {p.stem for p in ASSETS.glob("*.png")})
        for key, part in parts.items():
            with self.subTest(key=key), Image.open(ASSETS / (key + ".png")) as im:
                self.assertEqual(im.size, (part["w"], part["h"]))
                self.assertGreater(part["ppu"], 0)
                x, y = part["pivot"]
                self.assertTrue(0 <= x <= im.width and 0 <= y <= im.height)
                self.assertEqual(im.mode, "RGBA")

    def test_body_layers_and_palettes_are_complete(self):
        parts = json.loads((ASSETS / "parts.json").read_text())
        bodies = {k.removeprefix("body_").removesuffix("_front_bone")
                  for k in parts if k.startswith("body_") and k.endswith("_front_bone")}
        self.assertGreaterEqual(len(bodies), 31)
        for body in bodies:
            for side in ("front", "back"):
                for palette in ("bone", "olive", "steel", "violet"):
                    self.assertIn(f"body_{body}_{side}_{palette}", parts)
