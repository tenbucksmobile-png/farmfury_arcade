"""Graphics for The Scout Reviews (Arcade): transparent overlays shown with make_episode.py's "banner" key, plus
two full-frame cards shown as "image" shots. Output: <Video>/FarmFury_Arcade/Robot_Reviews/clips/cards/.

  title_rv<n>.png     THE SCOUT REVIEWS / REVIEW n / "A Farm Fury team production" (the honesty line)
  stamp_1bolt.png     rubber-stamp rating, the running gag
  stamp_1half.png     1.5 BOLTS (Review 2)
  stamp_5stars.png    Bessie's 5 STARS (Review 5)
  facts.png           FREE · ADS · OPTIONAL PURCHASES (Review 5, for grown-ups)
  card_daily.png      full frame: the game's Daily Challenge shield over a blurred Corn Field backdrop
  card_combos.png     full frame: all 8 combo banners in a grid

Usage: python make_review_cards.py
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
UI = REPO / "Assets/_Project/Sprites/UI"
FONT = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
OUT = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video\FarmFury_Arcade\Robot_Reviews\clips\cards")
W, H = 1080, 1920
PINK, INK, CREAM, RED, GOLD = (236, 72, 153), (24, 18, 32), (255, 248, 235), (205, 38, 38), (245, 190, 30)
COMBOS = ["Combo_EarthquakeRoll.png", "Combo_Featherstorm.png", "Combo_DoubleSlam.png", "Combo_CrossFire.png",
          "Combo_KicknRoll.png", "Combo_SkipShatter.png", "Combo_IronStampede.png", "Combo_FullFury.png"]


def font(size):
    return ImageFont.truetype(str(FONT), size)


def centered(draw, text, f, y, fill, stroke=0, stroke_fill=INK, width=W):
    x = (width - draw.textlength(text, font=f)) / 2
    draw.text((x, y), text, font=f, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


def title(n):
    img = Image.new("RGBA", (980, 420), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((20, 20, 960, 330), radius=36, fill=INK + (235,), outline=PINK, width=10)
    centered(d, "THE SCOUT REVIEWS", font(118), 50, CREAM, 0, width=980)
    label = "THE VERDICT" if n == 5 else f"REVIEW {n}"
    centered(d, label, font(84), 190, PINK, 0, width=980)
    centered(d, "A FARM FURY TEAM PRODUCTION", font(42), 352, CREAM, 6, width=980)
    return img


def stamp(top, bottom, color, angle=-8):
    img = Image.new("RGBA", (900, 520), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((30, 30, 870, 490), radius=40, fill=CREAM + (230,), outline=color, width=22)
    d.rounded_rectangle((62, 62, 838, 458), radius=26, outline=color, width=6)
    centered(d, "RATING", font(80), 80, color, width=900)
    centered(d, top, font(170), 160, color, width=900)
    centered(d, bottom, font(66), 360, color, width=900)
    return img.rotate(angle, expand=True, resample=Image.BICUBIC)


def facts():
    img = Image.new("RGBA", (1000, 520), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((10, 10, 990, 510), radius=30, fill=INK + (230,), outline=GOLD, width=8)
    for k, (word, col) in enumerate((("FREE TO DOWNLOAD", GOLD), ("HAS ADS", CREAM),
                                     ("OPTIONAL PURCHASES", CREAM))):
        centered(d, word, font(104), 40 + k * 150, col, width=1000)
    return img


def backdrop():
    bg = Image.open(UI / "World1_Cornfield.png").convert("RGB")
    s = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
    left, top = (bg.width - W) // 2, (bg.height - H) // 2
    card = bg.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(16)).convert("RGBA")
    return Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 110)))


def fit(path, width):
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getbbox())
    return im.resize((width, round(width * im.height / im.width)), Image.LANCZOS)


def daily_card():
    card = backdrop()
    shield = fit(UI / "DailyChallenge.png", 700)
    card.alpha_composite(shield, ((W - shield.width) // 2, 900 - shield.height // 2))
    return card


def combos_card():
    card = backdrop()
    cell_w, gap_x, gap_y = 470, 40, 34
    banners = [fit(UI / f, cell_w) for f in COMBOS]
    rows = [banners[i:i + 2] for i in range(0, 8, 2)]
    row_h = [max(b.height for b in r) for r in rows]
    y = 900 - (sum(row_h) + gap_y * 3) // 2
    for r, h in zip(rows, row_h):
        x = (W - (2 * cell_w + gap_x)) // 2
        for b in r:
            card.alpha_composite(b, (x, y + (h - b.height) // 2))
            x += cell_w + gap_x
        y += h + gap_y
    return card


# --- Season 2: The Scout Reviews Farm Fury: Stampede (2026-11-22). `python make_review_cards.py --stampede` ---------
ST = Path(r"C:\Users\Personel\Desktop\FarmFury_Stampede\Assets\_Project\Sprites")
ST_OUT = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video\FarmFury_Stampede\Robot_Reviews\cards")
ST_LANDS = ["Badge_Meadowruins.png", "Badge_FrozenTundra.png", "Badge_WatermillVillage.png", "Badge_SkyWorld.png",
            "Badge_SunkenCity.png", "Badge_Mothership.png"]
OVERLORD = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video\Ep10_Clips\stills\10.2_overlord.png")


def st_title(n):
    img = Image.new("RGBA", (980, 470), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((20, 20, 960, 380), radius=36, fill=INK + (235,), outline=GOLD, width=10)
    centered(d, "THE SCOUT REVIEWS", font(118), 45, CREAM, 0, width=980)
    centered(d, "FARM FURY: STAMPEDE", font(70), 175, GOLD, 0, width=980)
    centered(d, "THE VERDICT" if n == 5 else f"REVIEW {n}", font(76), 262, PINK, 0, width=980)
    centered(d, "A FARM FURY TEAM PRODUCTION", font(42), 402, CREAM, 6, width=980)
    return img


def st_facts():
    img = Image.new("RGBA", (1000, 670), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((10, 10, 990, 660), radius=30, fill=INK + (230,), outline=GOLD, width=8)
    for k, (word, col) in enumerate((("FREE TO DOWNLOAD", GOLD), ("6 FREE LANDS", GOLD), ("HAS ADS", CREAM),
                                     ("OPTIONAL PURCHASES", CREAM))):
        centered(d, word, font(100), 40 + k * 150, col, width=1000)
    return img


def st_backdrop(name="FT_Paralax_Far.png"):
    bg = Image.open(ST / "Environment" / name).convert("RGB")
    s = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
    left, top = (bg.width - W) // 2, (bg.height - H) // 2
    card = bg.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(16)).convert("RGBA")
    return Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 110)))


def st_lands_card():
    """The six free lands' shields, 2 across x 3 down, in order."""
    card = st_backdrop()
    cell, gap = 440, 50
    shields = [fit(ST / "UI" / f, cell) for f in ST_LANDS]
    y = 900 - (3 * cell + 2 * gap) // 2
    for r in range(3):
        x = (W - (2 * cell + gap)) // 2
        for b in shields[r * 2:r * 2 + 2]:
            card.alpha_composite(b, (x + (cell - b.width) // 2, y + (cell - b.height) // 2))
            x += cell + gap
        y += cell + gap
    return card


def st_bosses_card():
    """Three bosses with their names (the Commander is the game's own boss art, StampedePhase5aSetup.BossArtRight), then the Overlord as a red-eyed silhouette ('???')."""
    card = st_backdrop("RM_Parallax_Far.png")
    d = ImageDraw.Draw(card)
    bosses = [(ST / "Robots" / "Robot_right.png", "THE COMMANDER"),
              (ST / "UI/SkyIsland/StormBaron.png", "STORM BARON"),
              (ST / "UI/SunkenCity/DeepDredger.png", "DEEP DREDGER")]
    y = 260
    for path, name in bosses:
        im = Image.open(path).convert("RGBA")
        im = im.crop(im.getbbox())
        im.thumbnail((360, 300), Image.LANCZOS)
        card.alpha_composite(im, (110, y + (300 - im.height) // 2))
        d.text((520, y + 110), name, font=font(84), fill=CREAM, stroke_width=6, stroke_fill=INK)
        y += 330
    ov = Image.open(OVERLORD).convert("RGB")
    ov.thumbnail((360, 300), Image.LANCZOS)
    ov = Image.eval(ov, lambda p: int(p * 0.55)).convert("RGBA")
    card.alpha_composite(ov, (110, y + (300 - ov.height) // 2))
    d.text((520, y + 110), "THE OVERLORD ???", font=font(84), fill=(255, 90, 70), stroke_width=6, stroke_fill=INK)
    return card


def main_stampede():
    ST_OUT.mkdir(parents=True, exist_ok=True)
    for n in range(1, 6):
        st_title(n).save(ST_OUT / f"title_srv{n}.png")
    stamp("1 BOLT", "OUT OF 5", RED).save(ST_OUT / "stamp_1bolt.png")
    stamp("1.5 BOLTS", "OUT OF 5", RED).save(ST_OUT / "stamp_1half.png")
    stamp("5 STARS", "BY CLUCK & BESSIE", (34, 139, 34), angle=6).save(ST_OUT / "stamp_5stars.png")
    st_facts().save(ST_OUT / "facts.png")
    st_lands_card().convert("RGB").save(ST_OUT / "card_lands.png")
    st_bosses_card().convert("RGB").save(ST_OUT / "card_bosses.png")
    print(ST_OUT)


def main():
    import sys
    if "--stampede" in sys.argv:
        main_stampede()
        return
    OUT.mkdir(parents=True, exist_ok=True)
    for n in range(1, 6):
        title(n).save(OUT / f"title_rv{n}.png")
    stamp("1 BOLT", "OUT OF 5", RED).save(OUT / "stamp_1bolt.png")
    stamp("1.5 BOLTS", "OUT OF 5", RED).save(OUT / "stamp_1half.png")
    stamp("5 STARS", "BY BESSIE", (34, 139, 34), angle=6).save(OUT / "stamp_5stars.png")
    facts().save(OUT / "facts.png")
    daily_card().convert("RGB").save(OUT / "card_daily.png")
    combos_card().convert("RGB").save(OUT / "card_combos.png")
    print(OUT)


if __name__ == "__main__":
    main()
