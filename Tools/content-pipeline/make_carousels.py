"""Photo carousels for the 12:00 slot (added 2026-11-25 after the reach check): swipe-through image posts built from
the game's own art, no Kling. One folder per carousel in Video/Carousels/<key>/:

    NN.png             1080x1350 (4:5)  - Instagram carousel, Facebook photo post, YouTube Post
    vertical/NN.png    1080x1920 (9:16) - TikTok photo mode (same slide on a taller blurred background)

    python make_carousels.py                 # all of them
    python make_carousels.py parents robots  # just these

Facts on the slides follow the shipped games (CLAUDE.md ability/robot/combo tables, the Scout Reviews fact sheets).
Robots get dodged or powered down, never anything harsher (check_wording); the parents' slides say "ads" and
"optional purchases" plainly.
"""

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from make_shorts import check_wording

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
UI = REPO / "Assets/_Project/Sprites/UI"
ROBOTS = REPO / "Assets/_Project/Sprites/Robots"
SHIELDS = REPO / "Assets/_Project/Sprites/Cosmetics/CosmeticType_MazeTheme"
ST_UI = Path("C:/Users/Personel/Desktop/FarmFury_Stampede/Assets/_Project/Sprites/UI")
ASSETS = HERE.parent / "storyline/assets"
OUT = Path.home() / "Desktop/FarmFury_Technical/FarmFury_Artwork/Video/Carousels"

W, H, VH = 1080, 1350, 1920
BANGERS = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
BODY_FONT = Path("C:/Windows/Fonts/segoeuib.ttf")
YELLOW, WHITE, DARK = (255, 214, 64), (255, 255, 255), (35, 20, 8)

CHARACTERS = [  # (card art, name, power, what it does)
    ("Cluck_Chicken.png", "CLUCK", "EGG DROP", "Drops an egg behind her. Any robot that walks into it is out of the game."),
    ("Bessie_Cow.png", "BESSIE", "GROUND SLAM", "Slams the ground and powers down every robot close by."),
    ("Percy_Pig.png", "PERCY", "BOUNCE ROLL", "Curls into a ball and rolls straight through the robots."),
    ("Wooly_Sheep.png", "WOOLLY", "TRIPLE CLONE", "Two fluffy clones pop out and help collect the crops."),
    ("Ducky_Duck.png", "DUCKY", "WATER SKIP", "Skips across the water to the other side. Nobody else can!"),
    ("Horace_Horse.png", "HORACE", "HORSESHOE THROW", "Throws a horseshoe that knocks the first robot it hits out of the game."),
    ("Gerald_Turkey.png", "GERALD", "FEATHER BURST", "Fires a burst of feathers that sends a robot packing."),
    ("Billy_Goat.png", "BILLY", "MUD SLING", "Flings mud. Miss, and it leaves a sticky puddle for the next robot."),
]
ROBOT_INFO = [  # (sprite, name, how it moves, how to dodge it)
    ("HarvestorRobot_front.png", "THE HARVESTER", "Comes straight for you. Always.",
     "Keep moving and use the warp tunnels to lose it."),
    ("ScoutRobot_front.png", "THE SCOUT", "Aims for where you're GOING, not where you are.",
     "Change direction at the last moment."),
    ("PatrolRobot_Front.png", "THE PATROL", "Works with the Harvester to cut you off.",
     "Don't get stuck between the two of them."),
    ("DriftRobot_front.png", "THE DRIFTER", "Chases you from far away, then wanders off when it gets close.",
     "Stay close and it loses interest."),
    ("Drone.png", "THE DRONE", "Flies straight over the walls, but slowly.",
     "Walls won't stop it, so outrun it instead."),
]
COMBOS = [  # (banner, name, trigger, effect)
    ("Combo_Featherstorm.png", "FEATHER STORM", "Cluck, then Woolly", "Woolly's clones drop eggs as they walk."),
    ("Combo_EarthquakeRoll.png", "EARTHQUAKE ROLL", "Bessie, then Percy", "Percy's next roll goes three times as far."),
    ("Combo_SkipShatter.png", "SKIP SHATTER", "Ducky, then Woolly", "Ducky's next water skip brings two wool clones."),
    ("Combo_DoubleSlam.png", "DOUBLE SLAM", "Bessie, swap, Bessie again", "Ground Slam covers twice the area."),
    ("Combo_CrossFire.png", "CROSSFIRE", "Billy, then Horace", "Horace's horseshoe flies twice as far."),
    ("Combo_IronStampede.png", "IRON STAMPEDE", "Bessie, then Gerald", "Gerald's Feather Burst fires in all four directions."),
    ("Combo_KicknRoll.png", "KICK AND ROLL", "Horace, then Percy", "Percy's next roll goes three times as far."),
    ("Combo_FullFury.png", "FULL FURY", "Use 5 different animals in one maze", "Every robot freezes for 5 seconds."),
]


def font(size, body=False):
    return ImageFont.truetype(str(BODY_FONT if body else BANGERS), size)


def load(path):
    return Image.open(path).convert("RGBA")


def fit(img, box_w, box_h):
    s = min(box_w / img.width, box_h / img.height)
    return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS)


def background():
    bg = load(UI / "landing.png")
    bg = bg.resize((int(bg.width * H / bg.height), H), Image.LANCZOS)
    left = (bg.width - W) // 2
    bg = bg.crop((left, 0, left + W, H)).filter(ImageFilter.GaussianBlur(14))
    return Image.alpha_composite(bg, Image.new("RGBA", (W, H), (20, 10, 30, 150)))


def wrap(draw, text, fnt, width):
    lines, line = [], ""
    for word in text.split():
        trial = f"{line} {word}".strip()
        if draw.textlength(trial, font=fnt) <= width:
            line = trial
        else:
            lines.append(line)
            line = word
    return lines + [line]


def text(draw, y, txt, size, color=WHITE, body=False, width=W - 140, stroke=None, spacing=1.15):
    """Centred, wrapped text with a dark outline; returns the y below it."""
    fnt = font(size, body)
    stroke = stroke if stroke is not None else (3 if body else 6)
    for line in wrap(draw, txt, fnt, width):
        w = draw.textlength(line, font=fnt)
        draw.text(((W - w) / 2, y), line, font=fnt, fill=color, stroke_width=stroke, stroke_fill=DARK)
        y += int(size * spacing)
    return y


def paste_center(canvas, img, cy):
    canvas.alpha_composite(img, ((W - img.width) // 2, int(cy - img.height / 2)))


def footer(canvas, draw, n, total, h=H):
    draw.text((W - 40, h - 40), f"{n}/{total}", font=font(34), fill=WHITE, anchor="rb", stroke_width=3,
              stroke_fill=DARK)
    if n < total:
        draw.text((40, h - 40), "SWIPE  >", font=font(34), fill=YELLOW, anchor="lb", stroke_width=3, stroke_fill=DARK)


def cover(title, subtitle, art):
    c = background()
    d = ImageDraw.Draw(c)
    paste_center(c, fit(load(ASSETS / "farmfury-games-logo.png"), 640, 330), 230)
    y = text(d, 430, title, 110, YELLOW)
    if subtitle:
        y = text(d, y + 10, subtitle, 46, body=True)
    if art:   # fills whatever space the title leaves, so a two-line title never runs into the picture
        img = fit(art, 900, H - 110 - (y + 30))
        paste_center(c, img, (y + 30 + H - 110) / 2)
    return c


def end_slide(line):
    c = background()
    d = ImageDraw.Draw(c)
    # The studio logo only, no per-game logos: the list of games keeps growing.
    paste_center(c, fit(load(ASSETS / "farmfury-games-logo.png"), 640, 480), 330)
    y = text(d, 640, line, 72, YELLOW)
    y = text(d, y + 10, "FREE GAMES FOR IPHONE & ANDROID", 50)
    # No store badges: there are several games, so the website (which links to each one) is the single call to action.
    url = "farmfurygames.com"
    fnt = font(96)
    w = d.textlength(url, font=fnt)
    box = (W / 2 - w / 2 - 50, y + 50, W / 2 + w / 2 + 50, y + 50 + 150)
    d.rounded_rectangle(box, 40, fill=YELLOW, outline=DARK, width=6)
    d.text((W / 2, box[1] + 75), url, font=fnt, fill=DARK, anchor="mm")
    text(d, box[3] + 40, "Link in bio", 46, body=True)
    return c


def card_slide(art, art_h, title, lines):
    """One big picture, a yellow title under it, then short white lines; the whole block centred top to bottom."""
    scratch = ImageDraw.Draw(Image.new("RGBA", (W, H)))
    block = art_h + 30 + (text(scratch, 0, title, 96) + sum(text(scratch, 0, t, size, body=True) + 18
                                                            for size, t in lines))
    top = max(60, (H - 90 - block) // 2)
    c = background()
    d = ImageDraw.Draw(c)
    paste_center(c, fit(art, 860, art_h), top + art_h / 2)
    y = text(d, top + art_h + 30, title, 96, YELLOW)
    for size, line in lines:
        y = text(d, y + 18, line, size, body=True)
    return c


def side_by_side(*imgs, gap=40):
    """Several images on one transparent strip (e.g. both store badges as one picture)."""
    h = max(i.height for i in imgs)
    strip = Image.new("RGBA", (sum(i.width for i in imgs) + gap * (len(imgs) - 1), h))
    x = 0
    for i in imgs:
        strip.alpha_composite(i, (x, (h - i.height) // 2))
        x += i.width + gap
    return strip


def grid_slide(title, images, cols, cell_h, caption=None):
    c = background()
    d = ImageDraw.Draw(c)
    y = text(d, 70, title, 88, YELLOW)
    rows = (len(images) + cols - 1) // cols
    cell_w = (W - 80) // cols
    top = y + 30
    for i, img in enumerate(images):
        r, col = divmod(i, cols)
        n_in_row = min(cols, len(images) - r * cols)
        x0 = (W - n_in_row * cell_w) // 2 + col * cell_w
        im = fit(img, cell_w - 30, cell_h - 30)
        c.alpha_composite(im, (x0 + (cell_w - im.width) // 2, top + r * cell_h + (cell_h - im.height) // 2))
    if caption:
        text(d, top + rows * cell_h + 20, caption, 44, body=True)
    return c


def which_animal():
    slides = [cover("WHICH ANIMAL ARE YOU?", "8 farm animals. 8 powers. Swipe and pick yours!",
                    load(UI / "Cluck_Chicken.png"))]
    for art, name, power, what in CHARACTERS:
        slides.append(card_slide(load(UI / art), 640, name, [(58, power), (44, what)]))
    slides.append(end_slide("WHICH ONE ARE YOU? TELL US IN THE COMMENTS!"))
    return slides


def parents():
    s = [cover("A QUICK GUIDE FOR PARENTS", "What is Farm Fury, and is it a good fit for your kids?",
               load(UI / "Bessie_Cow.png"))]
    cast = load(UI / "landing.png")
    s.append(card_slide(cast.crop((0, int(cast.height * 0.5), cast.width, cast.height)), 480, "FARM ANIMALS VS ROBOTS", [
        (46, "Cartoon farm animals collect crops and dodge Harvest Robots."),
        (42, "Farm Fury: Arcade is a maze game. Farm Fury: Stampede is a run-and-jump adventure."),
        (42, "No blood, no scary stuff: robots just get dodged or powered down.")]))
    badges = side_by_side(fit(load(ASSETS / "app-store-badge.png"), 600, 200), fit(load(ASSETS / "google-play-badge.png"), 640, 200))
    s.append(card_slide(badges, 200, "FREE TO PLAY", [
        (46, "Free to download on iPhone and Android."),
        (42, "There are ads: a short ad between some levels, plus optional \"watch an ad\" rewards."),
        (42, "A one-off \"Remove Ads\" purchase turns off the ads between levels.")]))
    s.append(card_slide(load(UI / "Shop.png"), 420, "OPTIONAL PURCHASES", [
        (46, "Coins, extra worlds and dress-up items can be bought, but you never need them to play."),
        (42, "In Farm Fury: Arcade, every real-money purchase asks a grown-up maths question first.")]))
    s.append(card_slide(load(UI / "Wooly_Sheep.png"), 520, "NO SIGN-UP. NO CHAT.", [
        (46, "No account to create and no chat with strangers."),
        (42, "In Farm Fury: Arcade, ads are requested as child-directed, so they aren't personalised."),
        (40, "Privacy policy: farmfurygames.com/privacy")]))
    s.append(end_slide("TRY IT TOGETHER"))
    return s


def worlds():
    arcade_free = [load(UI / f) for f in ("CornfieldSign.png", "VegetablePatchSign.png", "OrchardSign.png",
                                          "WheatfieldSign.png")]
    arcade_extra = [load(SHIELDS / f) for f in ("FrozenGarden_shield.png", "GoldenSunset_shield.png",
                                                "HarvestMoon_shield.png")]
    stampede = [load(ST_UI / f) for f in ("Badge_Meadowruins.png", "Badge_FrozenTundra.png",
                                          "Badge_WatermillVillage.png", "Badge_SkyWorld.png", "Badge_SunkenCity.png",
                                          "Badge_Mothership.png")]
    return [
        cover("EVERY FARM FURY WORLD", "Which one would you visit first?", load(UI / "OrchardSign.png")),
        grid_slide("FARM FURY: ARCADE", arcade_free, 2, 420, "4 free worlds, 25 mazes each"),
        grid_slide("ARCADE BONUS WORLDS", arcade_extra, 3, 420, "3 extra worlds to unlock, 25 mazes each"),
        grid_slide("FARM FURY: STAMPEDE", stampede, 3, 380, "6 lands, all the way to the Robot Mothership"),
        end_slide("WHICH WORLD IS FIRST?"),
    ]


def robots():
    s = [cover("MEET THE HARVEST ROBOTS", "How each one moves, and how to dodge it",
               load(ROBOTS / "HarvestorRobot_front.png"))]
    for art, name, moves, tip in ROBOT_INFO:
        s.append(card_slide(load(ROBOTS / art), 560, name, [(46, moves), (44, "TIP: " + tip)]))
    s.append(end_slide("WHICH ROBOT IS THE SNEAKIEST?"))
    return s


def combos():
    s = [cover("COMBO GUIDE", "Swap animals in the right order for a COMBO. Save this for your next run!",
               load(UI / "Combo_EarthquakeRoll.png"))]
    for i in range(0, len(COMBOS), 2):
        c = background()
        d = ImageDraw.Draw(c)
        y = 50
        for banner, name, trigger, effect in COMBOS[i:i + 2]:
            img = fit(load(UI / banner), 620, 280)
            paste_center(c, img, y + 150)
            y = text(d, y + 310, name, 74, YELLOW)
            y = text(d, y + 4, trigger, 44, body=True)
            y = text(d, y + 4, effect, 40, (230, 230, 230), body=True) + 40
        s.append(c)
    s.append(end_slide("WHICH COMBO IS YOUR FAVOURITE?"))
    return s


# Trading cards (2026-11-25, user's idea; also meant as merch). Stats are made-up flavour for the cards, not game
# numbers (in the game every animal moves at the same speed); each animal's four stats add up to 26 so no card is
# simply "the best". Masters are 1500x2100 (2.5x3.5 in, the size of a real trading card, at 600 dpi) in
# Carousels/trading_cards/cards/. The art is the 500x500 thumbs-up sprites, upscaled: fine on a phone, but print
# merch from higher-resolution art.
CHARS = REPO / "Assets/_Project/Sprites/Characters"
STAT_NAMES = ("POWER", "SPEED", "AGILITY", "SMARTS")
# One signature power per animal, worded so it holds for every Farm Fury game (each game has its own version:
# Arcade Egg Drop, Stampede Egg Launch, Cannonfire Cluster Bomb, ...). No game logos: more games are coming.
# Catchphrases from the saga's Power Up shorts.
CARDS = [  # (art, name, species, colour, (power, speed, agility, smarts), signature power, what it does, catchphrase)
    ("CluckThumbsUp.png", "CLUCK", "CHICKEN", (245, 140, 25), (4, 7, 7, 8), "EGGS",
     "Drops them, launches them, rains them down by the dozen.", "\"Cluck. Chase. Collect. CHAOS!\""),
    ("BessieThumbsUp.png", "BESSIE", "COW", (215, 55, 55), (10, 4, 4, 8), "GROUND SLAM",
     "One stomp and everything nearby shakes.", "\"Now that's what I call a ground rule.\""),
    ("PercyThumbsup.png", "PERCY", "PIG", (235, 105, 150), (7, 8, 7, 4), "ROLLING",
     "Curls into a ball and bowls straight through.", "\"Roll with it!\""),
    ("WoollyThumbsUp.png", "WOOLLY", "SHEEP", (130, 110, 200), (4, 5, 7, 10), "WOOL",
     "Clones, clouds and balls of fluff. Never just one Woolly.", "\"Ewe can never have too many of me.\""),
    ("DuckyThumbsUp.png", "DUCKY", "DUCK", (40, 145, 225), (4, 7, 9, 6), "WATER",
     "Skips it, sprays it, swirls it. The pond is his.", "\"Just go with the flow, dude.\""),
    ("HoraceThumbsUp.png", "HORACE", "HORSE", (165, 95, 45), (7, 9, 6, 4), "HORSESHOES",
     "Throws a horseshoe and never misses the target.", "\"Ringer!\""),
    ("GeraldThumbsUp.png", "GERALD", "TURKEY", (150, 55, 40), (7, 5, 5, 9), "FEATHERS",
     "Bursts, blows and bombs of feathers. Don't ruffle him.", "\"You want feathers? I'll GIVE you feathers!\""),
    ("BillyThumbsUp.png", "BILLY", "GOAT", (105, 140, 55), (9, 6, 8, 3), "MUD",
     "Slings it, throws it, leaves it for the next robot.", "\"Head first. Questions later.\""),
]
CW, CH = 1500, 2100


def _shade(color, f):
    return tuple(max(0, min(255, int(c * f))) for c in color)


def _ctext(draw, cx, y, txt, fnt, fill, stroke=0, stroke_fill=DARK):
    w = draw.textlength(txt, font=fnt)
    draw.text((cx - w / 2, y), txt, font=fnt, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


def fit_size_body(draw, txt, size, width):
    while size > 30 and draw.textlength(txt, font=font(size, body=True)) > width:
        size -= 2
    return font(size, body=True)


def fit_size(draw, txt, size, width):
    while size > 40 and draw.textlength(txt, font=font(size)) > width:
        size -= 4
    return font(size)


def solid(img):
    """Fill see-through holes inside the character (some sprites have semi-transparent faces) with the colour of
    the surrounding pixels, leaving the outside transparent."""
    alpha = img.split()[3]
    inside = alpha.point(lambda a: 255 if a > 40 else 0)
    # Flood the outside from a 2px transparent border, so gaps touching the picture's edge count as outside too.
    outside = Image.new("L", (img.width + 4, img.height + 4), 0)
    outside.paste(inside, (2, 2))
    ImageDraw.floodfill(outside, (0, 0), 128)
    outside = outside.crop((2, 2, img.width + 2, img.height + 2))
    mask = outside.point(lambda v: 0 if v == 128 else 255)   # silhouette with its holes closed
    rgb = img.convert("RGB")
    blur_rgb = Image.composite(rgb, Image.new("RGB", img.size), inside).filter(ImageFilter.GaussianBlur(25))
    blur_a = inside.filter(ImageFilter.GaussianBlur(25))
    import numpy as np
    fill = np.array(blur_rgb, dtype=float) / np.maximum(np.array(blur_a, dtype=float)[..., None] / 255, 1e-3)
    under = Image.fromarray(np.clip(fill, 0, 255).astype("uint8"), "RGB").convert("RGBA")
    under.putalpha(mask)
    return Image.alpha_composite(under, img)


def trading_card(n, art, name, species, color, stats, power, what, quote):
    card = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    # Outer frame: rounded card in the animal's colour with a gold edge.
    d.rounded_rectangle((0, 0, CW - 1, CH - 1), 90, fill=(250, 205, 70))
    d.rounded_rectangle((28, 28, CW - 29, CH - 29), 70, fill=color)
    d.rounded_rectangle((70, 70, CW - 71, CH - 71), 50, fill=_shade(color, 0.55))
    # Name bar.
    d.rounded_rectangle((110, 110, CW - 111, 300), 40, fill=(255, 250, 235), outline=DARK, width=8)
    d.text((160, 120), name, font=font(150), fill=DARK)
    sp = font(64, body=True)
    d.text((CW - 160 - d.textlength(species, font=sp), 175), species, font=sp, fill=_shade(color, 0.8))
    # Art window: sunburst in the animal's colour behind the thumbs-up pose.
    win = (110, 330, CW - 111, 1240)
    burst = Image.new("RGBA", (win[2] - win[0], win[3] - win[1]), _shade(color, 1.25))
    bd = ImageDraw.Draw(burst)
    cx, cy = burst.width / 2, burst.height / 2
    import math
    for k in range(24):
        a0, a1 = 2 * math.pi * k / 24, 2 * math.pi * (k + 0.5) / 24
        bd.polygon([(cx, cy), (cx + 2000 * math.cos(a0), cy + 2000 * math.sin(a0)),
                    (cx + 2000 * math.cos(a1), cy + 2000 * math.sin(a1))], fill=_shade(color, 1.45) + (255,))
    mask = Image.new("L", burst.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, burst.width - 1, burst.height - 1), 40, fill=255)
    card.paste(burst, win[:2], mask)
    img = solid(fit(load(CHARS / art), win[2] - win[0] - 60, win[3] - win[1] - 40))
    card.alpha_composite(img, (int(win[0] + (win[2] - win[0] - img.width) / 2), int(win[3] - 20 - img.height)))
    d.rounded_rectangle(win, 40, outline=DARK, width=8)
    # Card number badge on the art.
    d.ellipse((CW - 330, 360, CW - 140, 550), fill=(250, 205, 70), outline=DARK, width=8)
    _ctext(d, CW - 235, 395, f"#{n:02d}", font(86), DARK)
    # Signature power panel.
    panel = (110, 1262, CW - 111, 1578)
    d.rounded_rectangle(panel, 36, fill=(255, 250, 235), outline=DARK, width=8)
    _ctext(d, CW / 2, 1280, "SIGNATURE POWER: " + power,
           fit_size(d, "SIGNATURE POWER: " + power, 84, panel[2] - panel[0] - 80), _shade(color, 0.75))
    yy = 1385
    for line in wrap(d, what, font(50, body=True), panel[2] - panel[0] - 90):
        _ctext(d, CW / 2, yy, line, font(50, body=True), DARK)
        yy += 62
    _ctext(d, CW / 2, yy + 6, quote, fit_size_body(d, quote, 46, panel[2] - panel[0] - 80), _shade(color, 0.7))
    # Stats: label, ten pips, number.
    sy = 1606
    for label, value in zip(STAT_NAMES, stats):
        d.text((130, sy), label, font=font(70), fill=WHITE, stroke_width=5, stroke_fill=DARK)
        for p in range(10):
            x0 = 520 + p * 74
            d.rounded_rectangle((x0, sy + 10, x0 + 60, sy + 66), 14,
                                fill=(250, 205, 70) if p < value else _shade(color, 0.35), outline=DARK, width=4)
        d.text((CW - 140, sy), str(value), font=font(70), fill=WHITE, anchor="ra", stroke_width=5, stroke_fill=DARK)
        sy += 84
    # Footer.
    d.text((130, CH - 122), "FARM FURY GAMES  •  SERIES 1", font=font(52), fill=(250, 205, 70), stroke_width=4,
           stroke_fill=DARK)
    d.text((CW - 130, CH - 122), f"{n} / {len(CARDS)}", font=font(52), fill=(250, 205, 70), anchor="ra",
           stroke_width=4, stroke_fill=DARK)
    return card


def trading_cards():
    cards = [trading_card(i, *c) for i, c in enumerate(CARDS, 1)]
    masters = OUT / "trading_cards" / "cards"
    masters.mkdir(parents=True, exist_ok=True)
    for i, (c, info) in enumerate(zip(cards, CARDS), 1):
        c.save(masters / f"{i:02d}_{info[1].lower()}.png")
    # Cover: a fan of four cards.
    cov = background()
    d = ImageDraw.Draw(cov)
    paste_center(cov, fit(load(ASSETS / "farmfury-games-logo.png"), 600, 300), 180)
    y = text(d, 360, "COLLECT ALL 8!", 120, YELLOW)
    text(d, y, "Farm Fury trading cards, Series 1. Swipe to see them all.", 44, body=True)
    for k, idx in enumerate((1, 3, 0, 2)):
        small = cards[idx].resize((350, 490), Image.LANCZOS).rotate((k - 1.5) * -9, expand=True,
                                                                     resample=Image.BICUBIC)
        cov.alpha_composite(small, (int(W / 2 - small.width / 2 + (k - 1.5) * 200), 660 + int(abs(k - 1.5) * 30)))
    slides = [cov]
    for c in cards:
        s = background()
        paste_center(s, c.resize((858, 1201), Image.LANCZOS), H / 2 - 20)
        slides.append(s)
    slides.append(end_slide("WHICH CARD WOULD YOU PULL FIRST?"))
    return slides


BUILDERS = {"trading_cards": trading_cards, "which_animal": which_animal, "parents": parents, "worlds": worlds, "robots": robots, "combos": combos}
WORDING = [*(w for c in CHARACTERS for w in c[1:]), *(w for r in ROBOT_INFO for w in r[1:]),
           *(w for c in COMBOS for w in c[1:])]


def vertical(slide):
    bg = slide.resize((int(W * VH / H), VH), Image.LANCZOS)
    left = (bg.width - W) // 2
    bg = bg.crop((left, 0, left + W, VH)).filter(ImageFilter.GaussianBlur(24))
    bg = Image.alpha_composite(bg, Image.new("RGBA", (W, VH), (0, 0, 0, 90)))
    bg.alpha_composite(slide, (0, (VH - H) // 2))
    return bg


# ---------------------------------------------------------------------------------------------------------------
# Photo carousels from the episode / cosmetics Kling stills (2026-11-25). Stills are 768x1360 (9:16), so each photo
# slide is drawn twice, natively: 1080x1350 (cropped) and 1080x1920 (full frame), instead of padding the 4:5 one.
STILLS = OUT.parent
EP = {n: STILLS / f"Ep{n:02d}_Clips/stills" for n in (2, 3, 4, 5, 6, 7, 8, 10, 11)}
COS = STILLS / "Cosmetics_Clips/stills"


def cover_fill(img, w, h, focus=0.5):
    """Scale to cover w x h and crop; focus = where to centre the crop vertically (0 top .. 1 bottom)."""
    s = max(w / img.width, h / img.height)
    img = img.resize((int(img.width * s + 0.5), int(img.height * s + 0.5)), Image.LANCZOS)
    x = (img.width - w) // 2
    y = int((img.height - h) * focus)
    return img.crop((x, y, x + w, y + h))


def shade_bottom(canvas, start, strength=230):
    """Dark gradient from y=start to the bottom so white text stays readable on any photo."""
    h = canvas.height - start
    grad = Image.new("L", (1, h))
    for y in range(h):
        grad.putpixel((0, y), int(strength * min(1, (y / h) * 1.6)))
    overlay = Image.new("RGBA", (canvas.width, h), (15, 8, 20, 255))
    overlay.putalpha(grad.resize((canvas.width, h)))
    canvas.alpha_composite(overlay, (0, start))


def photo(still, kicker, title, lines, focus=0.45, text_top=False):
    """One still, full bleed, with a kicker (small yellow), big title and short lines at the bottom.
    Returns (4:5 slide, 9:16 slide)."""
    out = []
    for h in (H, VH):
        c = cover_fill(load(still), W, h, focus).convert("RGBA")
        scratch = ImageDraw.Draw(Image.new("RGBA", (W, h)))
        block = (text(scratch, 0, kicker, 46) if kicker else 0) + text(scratch, 0, title, 92) + \
            sum(text(scratch, 0, t, 44, body=True) + 10 for t in lines)
        if text_top:   # subject sits low in the picture: put the words at the top instead
            top = 190
            grad = c.transpose(Image.FLIP_TOP_BOTTOM)
            shade_bottom(grad, max(0, h - (top + block + 220)))
            c = grad.transpose(Image.FLIP_TOP_BOTTOM)
        else:
            top = h - 150 - block
            shade_bottom(c, max(0, top - 220))
        d = ImageDraw.Draw(c)
        y = top
        if kicker:
            y = text(d, y, kicker, 46, YELLOW)
        y = text(d, y, title, 92, WHITE)
        for t in lines:
            y = text(d, y + 10, t, 44, body=True)
        logo = fit(load(ASSETS / "farmfury-games-logo.png"), 150, 130)
        c.alpha_composite(logo, (W - logo.width - 30, 30))
        out.append(c)
    return tuple(out)


def bookend(first, title, subtitle, art_still):
    """Cover (first=True) or end slide for a photo carousel, both formats."""
    if not first:
        e = end_slide(title)
        return e, vertical(e)
    out = []
    for h in (H, VH):
        c = cover_fill(load(art_still), W, h, 0.45).convert("RGBA")
        c = Image.alpha_composite(c, Image.new("RGBA", c.size, (20, 10, 30, 120)))
        d = ImageDraw.Draw(c)
        paste_center(c, fit(load(ASSETS / "farmfury-games-logo.png"), 520, 420), h * 0.2)
        y = text(d, int(h * 0.2) + 240, title, 112, YELLOW)
        text(d, y + 10, subtitle, 48, body=True)
        out.append(c)
    return tuple(out)


STORY_BEATS = [  # (still, episode label, title, line); wording follows the episode captions in plan_launch.py
    (EP[10] / "ref_eye.png", "EPISODE 1", "THE NIGHT THE ROBOTS CAME", "One quiet night, the Harvest Robots landed and took every crop."),
    (EP[2] / "2.7_chaos.png", "EPISODE 2", "CHAOS ON THE FARM", "The morning after: eight animals, zero crops, a LOT of panic."),
    (EP[3] / "3.1_studio.png", "EPISODE 3", "THE DEBRIEF", "Live from the barn, Barnyard Breakdown replays the landing."),
    (EP[4] / "4.1_meeting.png", "EPISODE 4", "THE BARN MEETING", "Bessie rings the bell. Everyone meets in the barn..."),
    (EP[5] / "6.2_circle.png", "EPISODE 5", "THE GOLDEN GLOW", "...and a golden sunflower gives every animal a power."),
    (EP[6] / "4.6_lineup.png", "EPISODE 6", "MEET THE METAL", "Every Harvest Robot has its own trick."),
    (EP[7] / "7.5_huddle.png", "EPISODE 7", "OPERATION HARVEST HOME", "The plan: into the mazes, every crop back home."),
    (EP[8] / "8.9_dawn.png", "EPISODE 8", "INTO THE MAZES", "Maze after maze, until the robots ran for home."),
    (EP[10] / "10.7_stampede.png", "EPISODE 10", "THE MARCH", "So the whole farm went after them. Chapter 2: STAMPEDE!"),
]


def story_so_far():
    s = [bookend(True, "THE STORY SO FAR", "Farm animals vs robots, in 9 pictures. Swipe!", EP[5] / "6.2_circle.png")]
    for still, ep, title, line in STORY_BEATS:
        s.append(photo(still, ep, title, [line]))
    s.append(bookend(False, "WATCH EVERY EPISODE ON OUR PAGE", None, None))
    return s


POWER_STILLS = ["6.3_cluck.png", "6.4_bessie.png", "6.5_percy.png", "6.6_woolly.png", "6.7_ducky.png",
                "6.8_horace.png", "6.9_gerald.png", "6.10_billy.png"]


LOW_SUBJECT = {"6.7_ducky.png"}


def power_reveals():
    s = [bookend(True, "8 ANIMALS. 8 POWERS.", "The night the golden sunflower glowed. Swipe!", EP[5] / "6.1_sunflower.png")]
    for still, card in zip(POWER_STILLS, CARDS):
        low = still in LOW_SUBJECT   # the animal sits at the bottom of the frame
        s.append(photo(EP[5] / still, card[1], "SIGNATURE POWER: " + card[5], [card[7]],
                       focus=0.9 if low else 0.45, text_top=low))
    s.append(bookend(False, "WHICH POWER WOULD YOU PICK?", None, None))
    return s


WANTED = [  # (still, name, known trick); generic across games, no "how to beat it" game mechanics
    ("4.1_harvester.png", "THE HARVESTER", "Never stops chasing."),
    ("4.2_scout.png", "THE SCOUT", "Guesses where you're going next."),
    ("4.3_patrol.png", "THE PATROL", "Never works alone."),
    ("4.4_drifter.png", "THE DRIFTER", "Comes close... then wanders off."),
    ("4.5_drone.png", "THE DRONE", "Flies right over the walls."),
]
PARCHMENT, INK = (236, 214, 170), (78, 44, 20)


def wanted_poster(still, name, trick, h):
    c = background() if h == H else vertical(background())
    d = ImageDraw.Draw(c)
    pw, ph = 860, int(h * 0.86)
    x0, y0 = (W - pw) // 2, (h - ph) // 2
    d.rounded_rectangle((x0 + 14, y0 + 18, x0 + pw + 14, y0 + ph + 18), 20, fill=(0, 0, 0, 120))
    d.rounded_rectangle((x0, y0, x0 + pw, y0 + ph), 20, fill=PARCHMENT, outline=INK, width=10)
    _ctext(d, W / 2, y0 + 30, "WANTED", font(170), INK)
    _ctext(d, W / 2, y0 + 205, "FOR CROP THEFT", font(56), (160, 40, 30))
    pic_h = ph - 560
    pic = cover_fill(load(EP[6] / still), pw - 120, pic_h, 0.55).convert("RGBA")
    c.alpha_composite(pic, (x0 + 60, y0 + 290))
    d.rectangle((x0 + 60, y0 + 290, x0 + pw - 60, y0 + 290 + pic_h), outline=INK, width=8)
    y = y0 + 310 + pic_h
    _ctext(d, W / 2, y, name, font(96), INK)
    _ctext(d, W / 2, y + 105, "Known trick: " + trick, fit_size_body(d, "Known trick: " + trick, 44, pw - 100), INK)
    _ctext(d, W / 2, y + 170, "REWARD: EVERY CROP BACK", font(58), (160, 40, 30))
    return c


def wanted():
    s = [bookend(True, "WANTED!", "Have you seen these robots? Swipe!", EP[6] / "4.6_lineup.png")]
    for still, name, trick in WANTED:
        s.append((wanted_poster(still, name, trick, H), wanted_poster(still, name, trick, VH)))
    s.append(bookend(False, "WHICH ROBOT IS THE SNEAKIEST?", None, None))
    return s


LOOKS = [  # (still, kicker, title, line); items from the in-game Shop
    ("c1_bessie_cowboy.png", "HATS", "BESSIE IN A COWBOY HAT", "Yee-haw. Moo-haw?"),
    ("c2_cluck_crown.png", "HATS", "CLUCK IN THE CROWN", "Queen of the coop."),
    ("c3_percy_ribbon.png", "TRAILS", "PERCY'S RAINBOW RIBBON", "Leave a little sparkle behind you."),
    ("c4_ducky_bubbles.png", "TRAILS", "DUCKY'S BUBBLE TRAIL", "Pop, pop, pop."),
    ("m1_cluck_tractor.png", "MACHINES", "CLUCK'S TRACTOR", "Cluck behind the wheel. Clear the road."),
    ("m2_bessie_tanker.png", "MACHINES", "BESSIE'S MILK TANKER", "Fresh milk, coming through!"),
    ("m3_horace_baler.png", "MACHINES", "HORACE'S HAY BALER", "Neigh-bours, stand back."),
]


def lookbook():
    s = [bookend(True, "THE FARM FURY LOOKBOOK", "Hats, trails and machines from the in-game Shop. Swipe!", COS / "m1_cluck_tractor.png")]
    for still, kicker, title, line in LOOKS:
        s.append(photo(COS / still, kicker, title, [line]))
    s.append(bookend(False, "WHICH LOOK IS YOUR FAVOURITE?", None, None))
    return s


WALLPAPERS = [EP[5] / "6.2_circle.png", EP[4] / "4.1_meeting.png", EP[6] / "4.6_lineup.png", EP[8] / "8.9_dawn.png",
              EP[10] / "10.7_stampede.png", EP[8] / "8.5_ducky.png", EP[5] / "6.1_sunflower.png", EP[8] / "8.3_horace.png"]


def wallpapers():
    s = [bookend(True, "FREE PHONE WALLPAPERS", "8 Farm Fury wallpapers. Save your favourite!", EP[8] / "8.7_gerald_bessie.png")]
    for still in WALLPAPERS:
        pair = []
        for h in (H, VH):
            c = cover_fill(load(still), W, h, 0.45).convert("RGBA")
            logo = fit(load(ASSETS / "farmfury-games-logo.png"), 170, 150)
            c.alpha_composite(logo, (W - logo.width - 36, h - logo.height - 120))
            pair.append(c)
        s.append(tuple(pair))
    s.append(bookend(False, "WHICH ONE IS ON YOUR PHONE?", None, None))
    return s


BUILDERS.update({"story_so_far": story_so_far, "power_reveals": power_reveals, "wanted": wanted,
                 "lookbook": lookbook, "wallpapers": wallpapers})
WORDING += [w for b in STORY_BEATS for w in b[1:]] + [w for wt in WANTED for w in wt[1:]] + \
    [w for lk in LOOKS for w in lk[1:]]


def main():
    for w in WORDING:
        check_wording(w)
    keys = sys.argv[1:] or list(BUILDERS)
    for key in keys:
        slides = BUILDERS[key]()
        folder = OUT / key
        (folder / "vertical").mkdir(parents=True, exist_ok=True)
        for old in list(folder.glob("*.png")) + list((folder / "vertical").glob("*.png")):
            old.unlink()
        for n, slide in enumerate(slides, 1):
            # A builder may hand back (4:5, 9:16) pairs drawn natively; otherwise the 9:16 is the 4:5 on a taller blur.
            square, tall = slide if isinstance(slide, tuple) else (slide, None)
            footer(square, ImageDraw.Draw(square), n, len(slides))
            square.convert("RGB").save(folder / f"{n:02d}.png")
            if tall is None:
                tall = vertical(square)
            else:
                footer(tall, ImageDraw.Draw(tall), n, len(slides), tall.height)
            tall.convert("RGB").save(folder / "vertical" / f"{n:02d}.png")
        print(f"{folder}  {len(slides)} slides")


if __name__ == "__main__":
    main()
