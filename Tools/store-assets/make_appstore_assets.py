"""Builds the App Store Connect creative assets (Header + Search Results) for Farm Fury: Arcade.

Header:          3840 x 1646 (21:9), from the 2720x1418 key art (FarmFury/unity/.../LandingPage.png).
                 Scaled 1.2x (keeps the FARM FURY wordmark and every character's feet), then the
                 left/right edges are extended with mirrored, softened scenery to reach 21:9.
Search Results:  3840 x 2560 (3:2, Apple's max), gameplay screenshot on the cornfield backdrop with
                 a short headline (Apple: "make the app's purpose obvious at a glance").
Both are RGB (no alpha - Apple rejects transparency), saved as PNG and high-quality JPEG.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

DESK = Path(r"C:\Users\Personel\Desktop")
KEY_ART = DESK / r"FarmFury\unity\Assets\Sprites\UI\LandingPage.png"
BACKDROP = DESK / r"FarmFury_Arcade\Assets\_Project\Sprites\UI\World1_Cornfield.png"
GAMEPLAY = DESK / r"FarmFury_Technical\AppleScreenshots\IMG_2463.png"
FONT = DESK / r"FarmFury_Arcade\Assets\TextMesh Pro\Examples & Extras\Fonts\Bangers.ttf"
OUT = DESK / r"FarmFury_Technical\AppStore_CreativeAssets"
OUT.mkdir(exist_ok=True)


def save(img, name):
    img = img.convert("RGB")
    img.save(OUT / f"{name}.png", optimize=True)
    img.save(OUT / f"{name}.jpg", quality=93, subsampling=0)
    print(name, img.size)


def extend_sides(img, target_w):
    """Widen by mirroring each edge strip outward, softening it with distance from the seam."""
    w, h = img.size
    pad = (target_w - w) // 2
    pad_r = target_w - w - pad
    canvas = Image.new("RGB", (target_w, h))
    canvas.paste(img, (pad, 0))
    for side, p in (("L", pad), ("R", pad_r)):
        strip = img.crop((0, 0, p, h)) if side == "L" else img.crop((w - p, 0, w, h))
        mirror = strip.transpose(Image.FLIP_LEFT_RIGHT)
        blurred = mirror.filter(ImageFilter.GaussianBlur(18))
        darker = ImageEnhance.Brightness(blurred).enhance(0.85)
        # Blend sharp mirror near the seam -> blurred/darker towards the outer edge.
        mask = Image.new("L", (p, h))
        md = ImageDraw.Draw(mask)
        for x in range(p):
            t = x / max(1, p - 1)          # 0 at outer edge .. 1 at seam (for the left side)
            if side == "R":
                t = 1 - t
            md.line([(x, 0), (x, h)], fill=int(255 * t ** 1.5))
        piece = Image.composite(mirror, darker, mask)
        canvas.paste(piece, (0, 0) if side == "L" else (pad + w, 0))
    return canvas


def header():
    art = Image.open(KEY_ART).convert("RGB")
    s = 1.2
    art = art.resize((round(art.width * s), round(art.height * s)), Image.LANCZOS)  # 3264 x 1702
    top, bottom = 40, art.height - 16
    art = art.crop((0, top, art.width, bottom))                                    # 3264 x 1646
    assert art.height == 1646, art.size
    img = extend_sides(art, 3840)
    save(img, "header_3840x1646")


def outlined_text(draw, xy, text, font, fill, stroke, anchor="mm"):
    draw.text(xy, text, font=font, fill=fill, stroke_width=stroke, stroke_fill=(45, 25, 10), anchor=anchor)


def search_results():
    W, H = 3840, 2560
    bg = Image.open(BACKDROP).convert("RGB")
    scale = max(W / bg.width, H / bg.height)
    bg = bg.resize((round(bg.width * scale), round(bg.height * scale)), Image.LANCZOS)
    bg = bg.crop(((bg.width - W) // 2, (bg.height - H) // 2, (bg.width - W) // 2 + W, (bg.height - H) // 2 + H))
    bg = ImageEnhance.Brightness(bg.filter(ImageFilter.GaussianBlur(10))).enhance(0.8)

    shot = Image.open(GAMEPLAY).convert("RGB")             # 2688 x 1242 iPhone gameplay
    sw = 3560
    shot = shot.resize((sw, round(shot.height * sw / shot.width)), Image.LANCZOS)  # 3560 x 1645
    # rounded frame + shadow
    frame_pad = 22
    framed = Image.new("RGB", (shot.width + 2 * frame_pad, shot.height + 2 * frame_pad), (60, 34, 14))
    framed.paste(shot, (frame_pad, frame_pad))
    mask = Image.new("L", framed.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, framed.width - 1, framed.height - 1], radius=70, fill=255)
    shadow = Image.new("L", (framed.width + 120, framed.height + 120), 0)
    ImageDraw.Draw(shadow).rounded_rectangle([60, 70, framed.width + 60, framed.height + 70], radius=70, fill=160)
    shadow = shadow.filter(ImageFilter.GaussianBlur(30))
    x = (W - framed.width) // 2
    y = H - framed.height - 150
    bg.paste((0, 0, 0), (x - 60, y - 60), shadow)
    bg.paste(framed, (x, y), mask)

    draw = ImageDraw.Draw(bg)
    font = ImageFont.truetype(str(FONT), 250)
    outlined_text(draw, (W // 2, (y - 30) // 2 + 20), "DODGE THE ROBOTS. SAVE THE CROPS!", font,
                  fill=(255, 196, 40), stroke=16)
    save(bg, "search_results_3840x2560")


header()
search_results()
