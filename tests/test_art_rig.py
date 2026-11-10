"""Check source/preview/runtime contracts, rather than subjective art quality."""
import re
import sys
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools/art'))
import gen_wayfarer_art as art
from rig_preview import constants
from PIL import Image
import numpy as np


class RigContracts(unittest.TestCase):
    def test_pivots_match_runtime(self):
        table=(ROOT/'src/WayfarerMech/MechArt.cs').read_text(encoding='utf-8')
        for name,x,y in re.findall(r'\{"(\w+)", new Vector2Int\((\d+), (\d+)\)\}',table):
            if name.endswith(('_front','_back')):
                kind,side=name.rsplit('_',1)
                part=getattr(art,'make_'+kind)(side=='front')
            elif name.endswith(('_0','_1')):
                kind,frame=name.rsplit('_',1)
                part=getattr(art,'make_'+kind)(int(frame))
            else: part=getattr(art,'make_'+name)()
            self.assertEqual(part.pivot,(int(x),int(y)),name)
            self.assertTrue((ROOT/'src/WayfarerMech/Assets'/f'wayfarer_{name}.png').exists(),name)

    def test_positive_rotation_points_up_in_preview(self):
        # A horizontal line from a pivot must rotate upward for Unity's +90 degrees.
        pixels=np.zeros((5,9,4),dtype=np.uint8)
        pixels[2,2:8]=[255,0,0,255]
        part=art.Part('marker',pixels,(2,2),0)
        canvas=Image.new('RGBA',(40,40))
        art.paste_rotated(canvas,part,(20,20),90)
        ys,xs=np.nonzero(np.asarray(canvas)[:,:,3])
        self.assertLess(float(ys.mean()),20)
        self.assertLessEqual(abs(float(xs.mean())-20),1)

    def test_tool_anchors_touch_art(self):
        r=constants(ROOT)
        cannon=art.make_cannon()
        bit=art.make_drill_bit(0)
        self.assertEqual(r['MuzzleX'],cannon.img.shape[1]-1-cannon.pivot[0])
        self.assertEqual(r['TipX'],bit.img.shape[1]-1-bit.pivot[0])
        self.assertLessEqual(r['BitX'],art.make_drill_arm().img.shape[1]-art.make_drill_arm().pivot[0])


if __name__=='__main__': unittest.main()
