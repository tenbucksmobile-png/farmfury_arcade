"""YouTube banner + Facebook cover with all three Farm Fury games (Arcade, Stampede, Cannonfire).

Usage: python make_banners_v3.py <folder containing backdrop-main.png>
       (backdrop-main.png is in the website zip: assets/brand/backdrop-main.png)

Writes youtube_banner_v3.png (2560x1440) and facebook_cover_v3.png (1640x624) next to this
script, plus *_preview.png copies with the platform safe areas outlined.

v3 vs v2: adds the Cannonfire logo, and strips the near-invisible background pixels
(alpha < 40) from every logo before use - in v2 those pixels picked up the drop shadow
and drew a faint dark box behind the Arcade and Stampede logos.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

SRC = sys.argv[1]
OUT = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(OUT, '..', '..', 'storyline', 'assets')
GAMES_LOGO = 'C:/Users/Personel/Desktop/FarmFury_Technical/FarmFury_Artwork/Rough_Icons/Logo_game.png'
FONT = os.path.join(OUT, '..', '..', '..', 'Assets', 'TextMesh Pro', 'Examples & Extras', 'Fonts', 'Bangers.ttf')
GOLD = (255, 196, 40)
INK = (40, 18, 6)


def clean_logo(path, h):
    im = Image.open(path).convert('RGBA')
    a = im.split()[3].point(lambda v: 0 if v < 40 else v)
    im.putalpha(a)
    im = im.crop(a.getbbox())
    return im.resize((round(im.width * h / im.height), h), Image.LANCZOS)


def backdrop(w, h, oval):
    bg = Image.open(os.path.join(SRC, 'backdrop-main.png')).convert('RGB')
    scale = max(w / bg.width, h / bg.height)
    bg = bg.resize((round(bg.width * scale), round(bg.height * scale)), Image.LANCZOS)
    x0, y0 = (bg.width - w) // 2, (bg.height - h) // 2
    bg = bg.crop((x0, y0, x0 + w, y0 + h))
    bg = ImageEnhance.Color(bg).enhance(0.85)
    bg = ImageEnhance.Brightness(bg).enhance(0.42)
    m = Image.new('L', (w, h), 0)
    ImageDraw.Draw(m).ellipse(oval, fill=120)
    m = m.filter(ImageFilter.GaussianBlur(max(w, h) // 21))
    return Image.composite(Image.new('RGB', (w, h), (10, 6, 4)), bg, m).convert('RGBA')


def paste(bg, im, x, y, blur):
    sh = Image.new('RGBA', im.size, (0, 0, 0, 0))
    sh.putalpha(im.split()[3].point(lambda v: int(v * 0.7)))
    pad = blur * 3
    big = Image.new('RGBA', (im.width + 2 * pad, im.height + 2 * pad), (0, 0, 0, 0))
    big.alpha_composite(sh, (pad, pad))
    big = big.filter(ImageFilter.GaussianBlur(blur))
    bg.alpha_composite(big, (x - pad + blur // 2, y - pad + blur * 2 // 3))
    bg.alpha_composite(im, (round(x), round(y)))


def compose(w, h, oval, cy, ff_h, game_h, gap, div_gap, f1, f2, line_gap):
    """FF Games logo | divider | three game logos in a row with store text centred below them."""
    bg = backdrop(w, h, oval)
    dr = ImageDraw.Draw(bg)
    ff = clean_logo(GAMES_LOGO, ff_h)
    games = [clean_logo(os.path.join(ASSETS, f), game_h)
             for f in ('arcade-logo.png', 'stampede-logo.png', 'Logo_Cannonfire.png')]
    row_w = sum(g.width for g in games) + gap * (len(games) - 1)
    t1, t2 = 'FREE ON THE APP STORE & GOOGLE PLAY', 'FARMFURYGAMES.COM'
    right_w = max(row_w, dr.textlength(t1, font=f1))
    total = ff.width + 2 * div_gap + right_w
    x = (w - total) / 2
    paste(bg, ff, round(x), cy - ff.height // 2, max(6, ff_h // 26))
    dx = round(x + ff.width + div_gap)
    dr.line((dx, cy - ff_h // 2, dx, cy + ff_h // 2), fill=GOLD + (220,), width=max(3, ff_h // 70))
    rl = dx + div_gap
    block_h = game_h + line_gap + f1.size + line_gap // 2 + f2.size
    top = cy - block_h // 2
    gx = rl + (right_w - row_w) / 2
    for g in games:
        paste(bg, g, round(gx), top, max(5, game_h // 16))
        gx += g.width + gap
    sw = max(3, f1.size // 16)
    for s, f, y, col in ((t1, f1, top + game_h + line_gap, (255, 255, 255)),
                         (t2, f2, top + game_h + line_gap + f1.size + line_gap // 2, GOLD)):
        tw = dr.textlength(s, font=f)
        dr.text((rl + (right_w - tw) / 2, y), s, font=f, fill=col, stroke_width=sw, stroke_fill=INK)
    return bg.convert('RGB')


def youtube():
    w, h = 2560, 1440
    out = compose(w, h, (300, 400, 2260, 1040), 720, 360, 190, 55, 60,
                  ImageFont.truetype(FONT, 74), ImageFont.truetype(FONT, 52), 34)
    out.save(os.path.join(OUT, 'youtube_banner_v3.png'))
    p = out.copy()
    d = ImageDraw.Draw(p)
    d.rectangle((0, 508, w, 931), outline=(0, 255, 0), width=4)          # desktop strip
    d.rectangle((507, 508, 2053, 931), outline=(255, 0, 0), width=4)     # safe on all devices
    p.resize((1280, 720)).save(os.path.join(OUT, 'youtube_banner_v3_preview.png'))


def facebook():
    w, h = 1640, 624
    out = compose(w, h, (180, 60, 1460, 580), 300, 250, 132, 36, 40,
                  ImageFont.truetype(FONT, 52), ImageFont.truetype(FONT, 38), 22)
    out.save(os.path.join(OUT, 'facebook_cover_v3.png'))
    p = out.copy()
    d = ImageDraw.Draw(p)
    mw = round(h * 16 / 9)                                                # phone shows the centre 16:9
    d.rectangle(((w - mw) // 2, 0, (w + mw) // 2, h - 1), outline=(255, 0, 0), width=3)
    d.rectangle((0, h - 160, 360, h - 1), outline=(0, 255, 0), width=3)  # approx. profile-picture overlap (desktop)
    p.save(os.path.join(OUT, 'facebook_cover_v3_preview.png'))


if __name__ == '__main__':
    youtube()
    facebook()
