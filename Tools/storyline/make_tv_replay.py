"""Episode 3, clip 3.2: the Combine's landing (Ep 1 clip B) replayed in slow motion on the old TV in the
Barnyard Breakdown studio, with chalk telestrator marks appearing over it. Built locally instead of as a
Kling job: the TV still (Ep03_Clips/stills/3.2_tv.png) has a flat green screen that is keyed out and the
footage placed behind it.

Output: <Video>/Ep03_Clips/ep03_3.2_tv.mp4 (1080x1920, silent; make_episode.py adds the audio).

Usage:
    python make_tv_replay.py
"""

import math
import random
import sys
import tempfile
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from PIL import Image, ImageDraw, ImageFilter  # noqa: E402

from make_shorts import FPS, H, W  # noqa: E402
from make_animatic import VIDEO_DIR, run  # noqa: E402
from normalize_video import find_ffmpeg  # noqa: E402

CLIP_DIR = VIDEO_DIR / "Ep03_Clips"
TV_STILL = CLIP_DIR / "stills" / "3.2_tv.png"
LANDING = VIDEO_DIR / "Ep01_Clips" / "ep01_B_landing.mp4"
OUT = CLIP_DIR / "ep03_3.2_tv.mp4"

SOURCE_FROM, SOURCE_TO = 0.5, 9.5     # seconds of the landing clip used
SLOW = 1.2                            # replay runs at 1/SLOW speed
SOURCE_CROP_TOP = 0.20                # fraction of the clip's height above the crop (keeps the Combine in view)
CHALK_RGBA = (245, 245, 235, 235)


def green_mask(still):
    """Screen mask from the flat green: (mask image at full frame size, bounding box)."""
    a = np.asarray(still.convert("RGB")).astype(int)
    m = (a[:, :, 1] > 150) & (a[:, :, 0] < 140) & (a[:, :, 2] < 140)
    mask = Image.fromarray((m * 255).astype("uint8")).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(1))
    ys, xs = np.where(m)
    return mask, (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)


def chalk_line(draw, points, width=10, seed=0):
    """A slightly wobbly, doubled stroke so it reads as chalk rather than a vector line."""
    rnd = random.Random(seed)
    for _ in range(3):
        jittered = [(x + rnd.uniform(-3, 3), y + rnd.uniform(-3, 3)) for x, y in points]
        draw.line(jittered, fill=CHALK_RGBA, width=width - rnd.randint(0, 4), joint="curve")


def chalk_layer(box, kind):
    """Transparent full-frame PNG with one telestrator mark over the screen box."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    cx, cy, rw, rh = (x0 + x1) / 2, y0 + (y1 - y0) * 0.55, (x1 - x0) * 0.33, (y1 - y0) * 0.30
    if kind == "circle":
        pts = [(cx + rw * math.cos(t), cy + rh * math.sin(t)) for t in np.linspace(-0.3, 2 * math.pi + 0.4, 60)]
        chalk_line(d, pts, seed=1)
    elif kind == "arrow":
        tip = (cx - rw * 0.9, cy - rh * 0.4)
        tail = (x0 - 60, y0 - 140)
        chalk_line(d, [tail, ((tail[0] + tip[0]) / 2 - 30, (tail[1] + tip[1]) / 2), tip], seed=2)
        chalk_line(d, [(tip[0] - 50, tip[1] - 10), tip, (tip[0] - 5, tip[1] - 55)], seed=3)
    elif kind == "rows":     # strikes across the flattened rows
        for k in range(4):
            yy = y1 - (y1 - y0) * (0.12 + 0.07 * k)
            chalk_line(d, [(x0 + 40, yy), (x1 - 40, yy - 12)], width=8, seed=10 + k)
    return img


def main():
    ffmpeg = find_ffmpeg()
    still = Image.open(TV_STILL).convert("RGB")
    sx, sy = W / still.width, H / still.height
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        bg = still.resize((W, H), Image.LANCZOS)
        mask_small, box_small = green_mask(still)
        mask = mask_small.resize((W, H), Image.LANCZOS)
        box = (round(box_small[0] * sx), round(box_small[1] * sy), round(box_small[2] * sx), round(box_small[3] * sy))
        bw, bh = box[2] - box[0], box[3] - box[1]
        bw, bh = bw + bw % 2, bh + bh % 2
        bg.save(work / "bg.png")
        mask.crop((box[0], box[1], box[0] + bw, box[1] + bh)).save(work / "mask.png")
        for kind in ("circle", "arrow", "rows"):
            chalk_layer(box, kind).save(work / f"chalk_{kind}.png")

        dur = (SOURCE_TO - SOURCE_FROM) * SLOW
        crop_h = f"iw*{bh}/{bw}"
        screen = (f"[1:v]trim={SOURCE_FROM}:{SOURCE_TO},setpts={SLOW}*(PTS-STARTPTS),fps={FPS},"
                  f"crop=iw:{crop_h}:0:ih*{SOURCE_CROP_TOP},scale={bw}:{bh},"
                  "eq=saturation=0.85:contrast=1.08,noise=alls=8:allf=t,vignette=PI/5,format=rgba[scr];"
                  f"[2:v]format=gray,scale={bw}:{bh}[m];[scr][m]alphamerge[scrm];"
                  f"[0:v][scrm]overlay={box[0]}:{box[1]}:shortest=1[tv]")
        # Chalk marks: the circle with Cluck's "Look at that landing!", then the arrow, then the flattened-row
        # strikes with Bessie's "Flattened forty rows" (times match the episode's line placement).
        marks = [("circle", 0.9), ("arrow", 2.6), ("rows", 5.6)]
        chain, cur = [], "tv"
        for k, (kind, at) in enumerate(marks):
            chain.append(f"[{cur}][{3 + k}:v]overlay=0:0:enable='gte(t,{at})'[c{k}]")
            cur = f"c{k}"
        graph = screen + ";" + ";".join(chain) + f";[{cur}]format=yuv420p[v]"
        run([ffmpeg, "-v", "error", "-y",
             "-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.2f}", "-i", str(work / "bg.png"),
             "-i", str(LANDING),
             "-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.2f}", "-i", str(work / "mask.png"),
             *[x for kind, _ in marks for x in ("-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.2f}",
                                                "-i", str(work / f"chalk_{kind}.png"))],
             "-filter_complex", graph, "-map", "[v]", "-t", f"{dur:.2f}",
             "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "-r", str(FPS), str(OUT)])
    print(f"{OUT}  {dur:.1f}s")


if __name__ == "__main__":
    main()
