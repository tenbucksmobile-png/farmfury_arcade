"""Turn a session's best clips into vertical 9:16 shorts (1080x1920).

Layout, top to bottom:
  hook text          what's happening ("EAT ALL THE ROBOTS!")
  gameplay           the centre of the screen (the maze), cropped from the
                     landscape video, so the on-screen D-pad/buttons and the
                     "Development Build" label are cut away
  call to action     game logo + "FREE ON GOOGLE PLAY"
A blurred, darkened copy of the gameplay fills the background. The call to
action sits above the bottom ~350px, which TikTok/Shorts cover with their own UI.

Output: sessions/<id>/shorts/short_01.mp4 (+ .json with the clip and text used)

Usage:
    python make_shorts.py                      # newest session, top 3 clips
    python make_shorts.py sessions/<id> --count 5
    python make_shorts.py --include-deaths     # also use clips where you died
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from normalize_video import find_ffmpeg

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
FONT = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
LOGO = REPO / "Assets/_Project/Sprites/UI/Logo.png"

W, H = 1080, 1920
# Width of the centre crop as a fraction of the landscape frame's height. The maze is centred and
# about 1.25x as wide as tall; 1.3 keeps a little of the scenery either side.
CROP_ASPECT = 1.3
GAMEPLAY_CENTER_Y = 960
HOOK_BOX = (60, 190, W - 60, 520)      # left, top, right, bottom
CTA_TOP = 1420

TEXT_FILL = (255, 255, 255)
ACCENT_FILL = (255, 214, 64)
STROKE_FILL = (58, 32, 12)


def hook_text(clip):
    """Headline for a clip, picked from its most exciting moment."""
    moments = clip["moments"]
    by_type = {m["type"]: m for m in moments}
    if "character_unlock" in by_type:
        return f"NEW CHARACTER UNLOCKED: {by_type['character_unlock'].get('character', '').upper()}!"
    if "world_unlock" in by_type:
        return "A NEW WORLD JUST OPENED UP!"
    if "combo" in by_type:
        return f"COMBO: {by_type['combo'].get('name', '').replace('_', ' ').upper()}!"
    if "full_chain" in by_type:
        robots = int(by_type["full_chain"].get("robots", 2))
        return f"1 PELLET. {robots} ROBOTS. GONE." if robots >= 3 else "EAT ALL THE ROBOTS!"
    if "near_miss" in by_type:
        return "THAT WAS WAY TOO CLOSE..."
    if sum(1 for m in moments if m["type"] == "robot_defeated") >= 2:
        return "THE ROBOTS HAD NO CHANCE"
    if "level_complete" in by_type:
        return "FARM SAVED. NEXT LEVEL!"
    return "FARM ANIMALS vs HARVEST ROBOTS"


def fit_font(draw, text, box, max_size=120, min_size=56):
    """Largest font size (and wrapped lines) that fits text inside box."""
    width = box[2] - box[0]
    height = box[3] - box[1]
    for size in range(max_size, min_size - 1, -4):
        font = ImageFont.truetype(str(FONT), size)
        lines, line = [], ""
        for word in text.split():
            trial = f"{line} {word}".strip()
            if draw.textlength(trial, font=font) <= width:
                line = trial
            else:
                if line:
                    lines.append(line)
                line = word
        lines.append(line)
        line_height = int(size * 1.05)
        if len(lines) * line_height <= height and all(draw.textlength(l, font=font) <= width for l in lines):
            return font, lines, line_height
    font = ImageFont.truetype(str(FONT), min_size)
    return font, [text], int(min_size * 1.05)


def draw_centered(draw, lines, font, line_height, top, fill):
    stroke = max(4, font.size // 12)
    for i, line in enumerate(lines):
        x = (W - draw.textlength(line, font=font)) / 2
        draw.text((x, top + i * line_height), line, font=font, fill=fill,
                  stroke_width=stroke, stroke_fill=STROKE_FILL)


def build_overlay(hook, path):
    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    font, lines, line_height = fit_font(draw, hook, HOOK_BOX)
    block = len(lines) * line_height
    top = HOOK_BOX[1] + (HOOK_BOX[3] - HOOK_BOX[1] - block) // 2
    draw_centered(draw, lines, font, line_height, top, TEXT_FILL)

    logo_size = 170
    logo = Image.open(LOGO).convert("RGBA").resize((logo_size, logo_size), Image.LANCZOS)
    cta_font = ImageFont.truetype(str(FONT), 78)
    small_font = ImageFont.truetype(str(FONT), 52)
    title, subtitle = "FREE ON GOOGLE PLAY", "FARM FURY: ARCADE"
    text_w = max(draw.textlength(title, font=cta_font), draw.textlength(subtitle, font=small_font))
    gap = 24
    left = int((W - (logo_size + gap + text_w)) / 2)
    overlay.alpha_composite(logo, (left, CTA_TOP))
    text_x = left + logo_size + gap
    draw.text((text_x, CTA_TOP + 18), title, font=cta_font, fill=ACCENT_FILL, stroke_width=6, stroke_fill=STROKE_FILL)
    draw.text((text_x, CTA_TOP + 104), subtitle, font=small_font, fill=TEXT_FILL, stroke_width=5, stroke_fill=STROKE_FILL)

    overlay.save(path)


def render(ffmpeg, video, clip, overlay_path, out_path):
    start, end = clip["start"], clip["end"]
    duration = end - start
    fade = min(0.4, duration / 4)
    filters = (
        f"[0:v]crop=ih*{CROP_ASPECT}:ih:(iw-ih*{CROP_ASPECT})/2:0,split[a][b];"
        f"[a]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,eq=brightness=-0.18[bg];"
        f"[b]scale={W}:-2[fg];"
        f"[bg][fg]overlay=0:{GAMEPLAY_CENTER_Y}-h/2[base];"
        f"[base][1:v]overlay=0:0,format=yuv420p,"
        f"fade=t=in:st=0:d={fade},fade=t=out:st={duration - fade:.2f}:d={fade}[v];"
        f"[0:a]loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=in:st=0:d={fade},afade=t=out:st={duration - fade:.2f}:d={fade}[a]"
    )
    cmd = [
        ffmpeg, "-v", "error", "-y",
        "-ss", f"{start:.2f}", "-t", f"{duration:.2f}", "-i", str(video),
        "-i", str(overlay_path),
        "-filter_complex", filters, "-map", "[v]", "-map", "[a]",
        "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-r", "30",
        "-c:a", "aac", "-b:a", "160k", "-ar", "48000",
        "-movflags", "+faststart",
        str(out_path),
    ]
    return subprocess.run(cmd).returncode == 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("session", nargs="?", help="session folder (default: newest)")
    parser.add_argument("--count", type=int, default=3, help="how many clips to render (default 3)")
    parser.add_argument("--include-deaths", action="store_true", help="also use clips that show a death")
    args = parser.parse_args()

    if args.session:
        session_dir = Path(args.session).resolve()
    else:
        sessions = sorted((HERE / "sessions").glob("*/clips.json"))
        if not sessions:
            sys.exit("No parsed sessions found. Run record_session.py first.")
        session_dir = sessions[-1].parent

    session = json.loads((session_dir / "session.json").read_text(encoding="utf-8"))
    video = session_dir / session["video"]
    if video.suffix != ".mp4":
        sys.exit(f"{video.name} isn't normalised yet. Run: python normalize_video.py {session_dir}")
    clips = json.loads((session_dir / "clips.json").read_text(encoding="utf-8"))
    if not args.include_deaths:
        clips = [c for c in clips if not c["has_death"]]
    clips = clips[: args.count]
    if not clips:
        sys.exit("No clips to render.")

    ffmpeg = find_ffmpeg()
    out_dir = session_dir / "shorts"
    out_dir.mkdir(exist_ok=True)
    for i, clip in enumerate(clips, 1):
        hook = hook_text(clip)
        overlay = out_dir / f"short_{i:02}_overlay.png"
        out = out_dir / f"short_{i:02}.mp4"
        build_overlay(hook, overlay)
        print(f"[{i}/{len(clips)}] {clip['start']:.1f}-{clip['end']:.1f}s  \"{hook}\"")
        if render(ffmpeg, video, clip, overlay, out):
            info = {"clip": clip, "hook": hook, "file": out.name}
            (out_dir / f"short_{i:02}.json").write_text(json.dumps(info, indent=2), encoding="utf-8")
        else:
            print(f"    ffmpeg failed for clip {i}")
    print(f"Shorts written to {out_dir}")


if __name__ == "__main__":
    main()
