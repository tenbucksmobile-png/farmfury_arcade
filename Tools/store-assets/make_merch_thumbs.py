"""Shop thumbnails built from existing art (no Kling): a die-cut sticker sheet and a framed poster.

    python make_merch_thumbs.py <out_dir>

Writes stickers.png / poster.png (1200x1200) and the 480x480 webp versions the website's shop/img/ uses.
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

REPO = Path(__file__).resolve().parents[2]
CHARS = REPO / "Assets/_Project/Sprites/Characters"
ROBOTS = REPO / "Assets/_Project/Sprites/Robots"
ASSETS = REPO / "Tools/storyline/assets"
STAMPEDE_STILL = Path(r"C:/Users/Personel/Desktop/FarmFury_Technical/FarmFury_Artwork/Video/Ep10_Clips/stills/10.7_stampede.png")
S = 1200


def load(p):
    im = Image.open(p).convert("RGBA")
    return im.crop(im.getbbox())


def fit(im, box):
    k = min(box / im.width, box / im.height)
    return im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)


def die_cut(im, border=14):
    """White die-cut border around the art's silhouette, plus a soft shadow."""
    pad = border * 2 + 10
    big = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    big.alpha_composite(im, (pad, pad))
    a = big.getchannel("A").point(lambda v: 255 if v > 40 else 0)
    outline = a.filter(ImageFilter.MaxFilter(border * 2 + 1)).filter(ImageFilter.GaussianBlur(1.2))
    sticker = Image.new("RGBA", big.size, (255, 255, 255, 0))
    sticker.putalpha(outline)
    sticker.alpha_composite(big)
    shadow = Image.new("RGBA", big.size, (40, 30, 20, 0))
    shadow.putalpha(outline.point(lambda v: v * 0.45).filter(ImageFilter.GaussianBlur(8)))
    out = Image.new("RGBA", (big.width + 16, big.height + 16), (0, 0, 0, 0))
    out.alpha_composite(shadow, (12, 14))
    out.alpha_composite(sticker, (0, 0))
    return out


def stickers(out):
    random.seed(7)
    sheet = Image.new("RGBA", (S, S), (236, 226, 204, 255))   # kraft-paper backing
    noise = Image.effect_noise((S, S), 18).convert("L")
    sheet = Image.composite(Image.new("RGBA", (S, S), (226, 214, 190, 255)), sheet, noise.point(lambda v: 90 if v > 140 else 0))
    d = ImageDraw.Draw(sheet)
    d.rounded_rectangle((40, 40, S - 40, S - 40), radius=40, fill=(250, 246, 236, 255), outline=(210, 195, 165, 255), width=4)
    names = ["CluckThumbsUp", "BessieThumbsUp", "PercyThumbsup", "WoollyThumbsUp",
             "DuckyThumbsUp", "HoraceThumbsUp", "GeraldThumbsUp"]   # Billy's thumbs-up art is off-model (a cow)
    logo = die_cut(fit(load(ASSETS / "farmfury-logo.png"), 560), 16)
    sheet.alpha_composite(logo.rotate(-4, expand=True, resample=Image.BICUBIC), (S // 2 - logo.width // 2, 70))
    cells = [(230, 470), (530, 455), (830, 470), (1000, 700),
             (200, 760), (470, 760), (740, 790)]
    for (cx, cy), n in zip(cells, names):
        st = die_cut(fit(load(CHARS / f"{n}.png"), 250))
        st = st.rotate(random.uniform(-12, 12), expand=True, resample=Image.BICUBIC)
        sheet.alpha_composite(st, (cx - st.width // 2, cy - st.height // 2))
    for (cx, cy), f in zip([(330, 1030), (600, 1035), (880, 1010)],
                         ["HarvestorRobot_front.png", "ScoutRobot_front.png", "Drone.png"]):
        p = next(ROBOTS.rglob(f), None)
        if p:
            st = die_cut(fit(load(p), 170)).rotate(random.uniform(-14, 14), expand=True, resample=Image.BICUBIC)
            sheet.alpha_composite(st, (cx - st.width // 2, cy - st.height // 2))
    save(sheet, out / "stickers")


def poster(out):
    wall = Image.new("RGBA", (S, S), (232, 224, 208, 255))
    g = ImageDraw.Draw(wall)
    for y in range(S):   # soft light falloff on the wall
        g.line([(0, y), (S, y)], fill=(238 - y // 40, 230 - y // 40, 214 - y // 40, 255))
    pw, ph = 640, 900
    # The Ep 10 stampede still: all eight on-model, bright sky for the logo (Characters_nobackground.png is off-model).
    art = Image.open(STAMPEDE_STILL).convert("RGBA")
    k = max(pw / art.width, ph / art.height)
    art = art.resize((round(art.width * k), round(art.height * k)), Image.LANCZOS)
    art = art.crop(((art.width - pw) // 2, (art.height - ph) // 2, (art.width - pw) // 2 + pw, (art.height - ph) // 2 + ph))
    logo = fit(load(ASSETS / "farmfury-logo.png"), pw - 120)
    art.alpha_composite(logo, ((pw - logo.width) // 2, 170))
    frame = 28
    framed = Image.new("RGBA", (pw + frame * 2 + 60, ph + frame * 2 + 60), (30, 24, 20, 255))
    ImageDraw.Draw(framed).rectangle((frame, frame, framed.width - frame, framed.height - frame), fill=(250, 248, 242, 255))
    framed.alpha_composite(art, (frame + 30, frame + 30))
    x, y = (S - framed.width) // 2, 90
    sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rectangle((x + 14, y + 22, x + framed.width + 14, y + framed.height + 22), fill=(0, 0, 0, 110))
    wall.alpha_composite(sh.filter(ImageFilter.GaussianBlur(18)))
    wall.alpha_composite(framed, (x, y))
    save(wall, out / "poster")

FONT = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"


def bumper_design():
    """Print-ready bumper sticker, 11.5 x 3 in at 300 dpi (3450x900): FARM FURY logo, the website, Cluck and Bessie
    at the ends. Rounded corners, transparent outside the cut line."""
    from PIL import ImageFont
    w, h = 3450, 900
    art = Image.new("RGBA", (w, h))
    g = ImageDraw.Draw(art)
    for y in range(h):   # sky to light blue
        t = y / h
        g.line([(0, y), (w, y)], fill=(int(70 + 90 * t), int(150 + 70 * t), int(230 + 15 * t), 255))
    g.rectangle((0, h - 150, w, h), fill=(110, 180, 60, 255))           # grass band
    g.rectangle((0, h - 150, w, h - 138), fill=(80, 150, 40, 255))
    cluck = fit(load(CHARS / "CluckThumbsUp.png"), 700)
    bessie = fit(load(CHARS / "BessieThumbsUp.png"), 760)
    art.alpha_composite(cluck, (70, h - cluck.height - 40))
    art.alpha_composite(bessie.transpose(Image.FLIP_LEFT_RIGHT), (w - bessie.width - 60, h - bessie.height - 30))
    logo = load(ASSETS / "farmfury-logo.png")
    k = min(1750 / logo.width, 440 / logo.height)   # fit a 1750x440 box, keeping the logo's proportions
    logo = logo.resize((round(logo.width * k), round(logo.height * k)), Image.LANCZOS)
    art.alpha_composite(logo, ((w - logo.width) // 2 - 20, 70))
    f = ImageFont.truetype(str(FONT), 190)
    ImageDraw.Draw(art).text((w // 2 - 20, 650), "FARMFURYGAMES.COM", font=f, anchor="mm",
                             fill=(255, 215, 40), stroke_width=14, stroke_fill=(55, 30, 10))
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=90, fill=255)
    art.putalpha(mask)
    border = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(border).rounded_rectangle((8, 8, w - 9, h - 9), radius=84, outline=(255, 255, 255, 255), width=22)
    art.alpha_composite(border)
    return art


def bumper(out):
    design = bumper_design()
    design.save(out / "bumper-sticker-print.png", dpi=(300, 300))
    bg = Image.new("RGBA", (S, S), (236, 228, 212, 255))
    st = design.resize((1080, round(1080 * design.height / design.width)), Image.LANCZOS)
    st = st.rotate(-6, expand=True, resample=Image.BICUBIC)
    shadow = Image.new("RGBA", st.size, (40, 30, 20, 0))
    shadow.putalpha(st.getchannel("A").point(lambda v: v * 0.5).filter(ImageFilter.GaussianBlur(14)))
    x, y = (S - st.width) // 2, (S - st.height) // 2
    bg.alpha_composite(shadow, (x + 14, y + 20))
    bg.alpha_composite(st, (x, y))
    save(bg, out / "bumper-sticker")


def save(im, stem):
    im = im.convert("RGB")
    im.save(stem.with_suffix(".png"))
    im.resize((480, 480), Image.LANCZOS).save(stem.with_suffix(".webp"), quality=85, method=6)


if __name__ == "__main__":
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    stickers(out)
    poster(out)
    bumper(out)
