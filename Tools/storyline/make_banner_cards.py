"""Banner cards for the cosmetics shorts: the Shop's own category banner (Hats & Caps / Trails / Machines) on its
own full-screen scene, centred over a blurred, darkened frame of the short's first clip. make_episode.py plays
each as an "image" shot between the opener and the videos (opening > banner > videos > end card).
Output: <Video>/Cosmetics_Clips/stills/card_<name>.png (1080x1920).

Usage: python make_banner_cards.py
"""

import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
COSMETICS = REPO / "Assets/_Project/Sprites/Cosmetics"
CLIPS = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video\Cosmetics_Clips")
FFMPEG = str(next((REPO / "Tools/content-pipeline/bin").glob("ffmpeg-*/bin")) / "ffmpeg.exe")
W, H = 1080, 1920
BANNER_W = 940
CENTER_Y = 900

CARDS = {   # card name -> (banner art, clip whose first frame is the backdrop)
    "hats": ("Hats&Caps.png", "c1_bessie_cowboy.mp4"),
    "trails": ("Trails.png", "c3_percy_ribbon.mp4"),
    "machines": ("machine.png", "m1_cluck_tractor.mp4"),
}


def main():
    out_dir = CLIPS / "stills"
    with tempfile.TemporaryDirectory() as tmp:
        for name, (banner_file, clip) in CARDS.items():
            frame = Path(tmp) / f"{name}.png"
            subprocess.run([FFMPEG, "-v", "error", "-y", "-ss", "0.5", "-i", str(CLIPS / clip), "-frames:v", "1",
                            str(frame)], check=True)
            bg = Image.open(frame).convert("RGB")
            s = max(W / bg.width, H / bg.height)
            bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
            left, top = (bg.width - W) // 2, (bg.height - H) // 2
            card = bg.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(18)).convert("RGBA")
            card = Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 80)))
            banner = Image.open(COSMETICS / banner_file).convert("RGBA")
            banner = banner.crop(banner.getbbox())
            banner = banner.resize((BANNER_W, round(BANNER_W * banner.height / banner.width)), Image.LANCZOS)
            card.alpha_composite(banner, ((W - banner.width) // 2, CENTER_Y - banner.height // 2))
            out = out_dir / f"card_{name}.png"
            card.convert("RGB").save(out)
            print(out)


if __name__ == "__main__":
    main()
