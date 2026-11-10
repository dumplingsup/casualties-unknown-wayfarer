"""Make an unretouched, real-time GIF/contact sheet from Probe motion captures."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw

p = argparse.ArgumentParser()
p.add_argument('before', type=Path)
p.add_argument('after', type=Path)
p.add_argument('out', type=Path)
a = p.parse_args()
a.out.mkdir(parents=True, exist_ok=True)
records = [[json.loads(line) for line in (r/'motion.jsonl').read_text().splitlines()]
           for r in (a.before, a.after)]
for rows in records:
    start = rows[0]['time']
    for row in rows:
        row['elapsed'] = row['time'] - start
duration = min(rows[-1]['elapsed'] for rows in records)
frames = []
for i in range(int(duration * 15)):
    time = i / 15
    canvas = Image.new('RGB', (960, 470), (26, 31, 35))
    draw = ImageDraw.Draw(canvas)
    for side, (run, rows) in enumerate(zip((a.before, a.after), records)):
        row = min(rows, key=lambda r: abs(r['elapsed'] - time))
        im = Image.open(run/'motion'/f"frame-{row['frame']:03}.png")
        # Fixed camera-space crop, identical nearest-neighbor scale. No relighting.
        im = im.crop((560, 300, 720, 440)).resize((480, 420), Image.Resampling.NEAREST)
        canvas.paste(im, (side * 480, 32))
        draw.text((side * 480 + 16, 10), ('BEFORE / 0.4.5' if side == 0 else 'AFTER / 0.4.6') +
                  f"    {time:.2f}s", fill=(230, 225, 210))
    frames.append(canvas)
frames[0].save(a.out/'walk-before-after.gif', save_all=True, append_images=frames[1:],
               duration=[67 if i % 3 else 66 for i in range(len(frames))], loop=0)
walking = frames[65:78]  # Both runs travel freely left in this measured interval.
walking[0].save(a.out/'walk-loop.gif', save_all=True, append_images=walking[1:], duration=67, loop=0)
sheet = Image.new('RGB', (960, 470 * 4), (26, 31, 35))
for row, time in enumerate((4.35, 4.55, 4.75, 4.95)):
    sheet.paste(frames[min(len(frames)-1, round(time*15))], (0, row*470))
sheet.save(a.out/'walk-frames.png')
print(f'{len(frames)} frames, {duration:.2f}s; original timestamps, 15fps sampled playback')
