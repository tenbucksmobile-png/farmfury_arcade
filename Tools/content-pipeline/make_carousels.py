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


def footer(canvas, draw, n, total):
    draw.text((W - 40, H - 40), f"{n}/{total}", font=font(34), fill=WHITE, anchor="rb", stroke_width=3,
              stroke_fill=DARK)
    if n < total:
        draw.text((40, H - 40), "SWIPE  >", font=font(34), fill=YELLOW, anchor="lb", stroke_width=3, stroke_fill=DARK)


def cover(title, subtitle, art):
    c = background()
    d = ImageDraw.Draw(c)
    paste_center(c, fit(load(ASSETS / "farmfury-logo.png"), 640, 330), 230)
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
    paste_center(c, fit(load(ASSETS / "farmfury-logo.png"), 660, 360), 270)
    arcade, stampede = fit(load(ASSETS / "arcade-logo.png"), 360, 300), fit(load(ASSETS / "stampede-logo.png"), 420, 300)
    c.alpha_composite(arcade, (W // 2 - 20 - arcade.width, 560 - arcade.height // 2))
    c.alpha_composite(stampede, (W // 2 + 20, 560 - stampede.height // 2))
    y = text(d, 760, line, 72, YELLOW)
    y = text(d, y + 10, "FREE ON THE APP STORE & GOOGLE PLAY", 50)
    apple, google = fit(load(ASSETS / "app-store-badge.png"), 440, 150), fit(load(ASSETS / "google-play-badge.png"), 460, 150)
    c.alpha_composite(apple, (W // 2 - 20 - apple.width, y + 40))
    c.alpha_composite(google, (W // 2 + 20, y + 40 + (apple.height - google.height) // 2))
    text(d, y + 230, "Link in bio  •  farmfurygames.com", 42, body=True)
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


BUILDERS = {"which_animal": which_animal, "parents": parents, "worlds": worlds, "robots": robots, "combos": combos}
WORDING = [*(w for c in CHARACTERS for w in c[1:]), *(w for r in ROBOT_INFO for w in r[1:]),
           *(w for c in COMBOS for w in c[1:])]


def vertical(slide):
    bg = slide.resize((int(W * VH / H), VH), Image.LANCZOS)
    left = (bg.width - W) // 2
    bg = bg.crop((left, 0, left + W, VH)).filter(ImageFilter.GaussianBlur(24))
    bg = Image.alpha_composite(bg, Image.new("RGBA", (W, VH), (0, 0, 0, 90)))
    bg.alpha_composite(slide, (0, (VH - H) // 2))
    return bg


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
            footer(slide, ImageDraw.Draw(slide), n, len(slides))
            slide.convert("RGB").save(folder / f"{n:02d}.png")
            vertical(slide).convert("RGB").save(folder / "vertical" / f"{n:02d}.png")
        print(f"{folder}  {len(slides)} slides")


if __name__ == "__main__":
    main()
