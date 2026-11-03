"""Ep 7's "seven worlds" shot, built from the game's own world signs instead of a Kling image (Kling kept
putting padlocks on the wrong fields): the four free worlds bright on the chalkboard, the three purchase worlds
dimmed with the game's locked-tile icon on each. Output: <Video>/Ep07_Clips/stills/7.4_map.png (768x1360, the
same size as the Kling stills, so make_episode.py treats it like any other still).

Usage: python make_world_map.py
"""

from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

HERE = Path(__file__).resolve().parent
SPRITES = HERE.parents[1] / "Assets/_Project/Sprites"
VIDEO_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
BOARD = VIDEO_DIR / "Ep07_Clips/stills/7.3b_board.png"   # an empty-ish chalkboard, blurred as the background
OUT = VIDEO_DIR / "Ep07_Clips/stills/7.4_map.png"
W, H = 768, 1360

FREE = ["UI/CornfieldSign.png", "UI/VegetablePatchSign.png", "UI/OrchardSign.png", "UI/WheatfieldSign.png"]
LOCKED = ["Cosmetics/CosmeticType_MazeTheme/FrozenGarden_shield.png",
          "Cosmetics/CosmeticType_MazeTheme/GoldenSunset_shield.png",
          "Cosmetics/CosmeticType_MazeTheme/HarvestMoon_shield.png"]
LOCK_ICON = "UI/LevelTile_Locked.png"


def load(rel, width):
    im = Image.open(SPRITES / rel).convert("RGBA")
    im = im.crop(im.getbbox())
    return im.resize((width, round(width * im.height / im.width)), Image.LANCZOS)


def place(canvas, im, cx, cy):
    canvas.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))


def main():
    bg = Image.open(BOARD).convert("RGB").resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(14))
    canvas = ImageEnhance.Brightness(bg).enhance(0.55).convert("RGBA")

    # Three purchase worlds across the top, dimmed, each with the game's lock tile.
    # Layout keeps clear of the episode's hook text (top ~27% of the frame) and captions (bottom ~28%).
    lock = load(LOCK_ICON, 60)
    for k, rel in enumerate(LOCKED):
        im = load(rel, 200)
        rgb = ImageEnhance.Brightness(im.convert("RGB")).enhance(0.45).convert("RGBA")
        rgb.putalpha(im.getchannel("A"))
        cx, cy = 150 + k * 234, 430
        place(canvas, rgb, cx, cy)
        place(canvas, lock, cx, cy + 68)

    # Four free worlds in a 2x2 grid below, full brightness.
    for k, rel in enumerate(FREE):
        im = load(rel, 250)
        cx, cy = 230 + (k % 2) * 308, 680 + (k // 2) * 245
        place(canvas, im, cx, cy)

    canvas.convert("RGB").save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
