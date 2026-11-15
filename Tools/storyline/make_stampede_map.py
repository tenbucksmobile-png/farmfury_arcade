"""Ep 10's "six lands" map (shot 10.3), built from Farm Fury: Stampede's own world badges instead of a Kling image,
the same way make_world_map.py builds Ep 7's Arcade map: the six free territories snake up the chalkboard in play
order, joined by a dotted chalk trail that ends at the Mothership, which glows red. Output:
<Video>/Ep10_Clips/stills/10.3_map.png (768x1360, like the Kling stills, so make_episode.py treats it as a still).

Usage: python make_stampede_map.py
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

VIDEO_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
STAMPEDE_UI = Path(r"C:\Users\Personel\Desktop\FarmFury_Stampede\Assets\_Project\Sprites\UI")
BOARD = VIDEO_DIR / "Ep07_Clips/stills/7.3b_board.png"   # same chalkboard as Ep 7's map
OUT = VIDEO_DIR / "Ep10_Clips/stills/10.3_map.png"
W, H = 768, 1360

# Play order, with each badge's centre. Snakes up from bottom-left so the trail reads as a march; the layout keeps
# clear of the hook text (top ~27%) and the captions (bottom ~28%).
WORLDS = [
    ("Badge_Meadowruins.png", 210, 935),
    ("Badge_FrozenTundra.png", 558, 935),
    ("Badge_WatermillVillage.png", 558, 705),
    ("Badge_SkyWorld.png", 210, 705),
    ("Badge_SunkenCity.png", 210, 475),
    ("Badge_Mothership.png", 558, 460),
]
BADGE = 165
MOTHERSHIP = 215


def load(name, width):
    im = Image.open(STAMPEDE_UI / name).convert("RGBA")
    im = im.crop(im.getbbox())
    return im.resize((width, round(width * im.height / im.width)), Image.LANCZOS)


def dotted(draw, a, b, step=22, r=5):
    (x0, y0), (x1, y1) = a, b
    n = max(1, int(((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5 // step))
    for i in range(n + 1):
        x, y = x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(240, 236, 220, 230))


def main():
    bg = Image.open(BOARD).convert("RGB").resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(14))
    canvas = ImageEnhance.Brightness(bg).enhance(0.55).convert("RGBA")

    trail = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(trail)
    for (_, ax, ay), (_, bx, by) in zip(WORLDS, WORLDS[1:]):
        dotted(d, (ax, ay), (bx, by))
    canvas.alpha_composite(trail)

    # A red glow behind the Mothership: the goal.
    _, mx, my = WORLDS[-1]
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((mx - 170, my - 170, mx + 170, my + 170), fill=(255, 40, 30, 150))
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(40)))

    for i, (name, cx, cy) in enumerate(WORLDS):
        im = load(name, MOTHERSHIP if i == len(WORLDS) - 1 else BADGE)
        canvas.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))

    OUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
