"""Store videos (2026-11-25): App Store app previews and Google Play promo trailers for Farm Fury: Arcade and
Farm Fury: Stampede, cut from the recorded gameplay sessions (and, for Google Play only, the Kling story clips).

    python make_store_videos.py                    # all four
    python make_store_videos.py arcade_preview     # one

Output: Video/Store_Videos/<name>.mp4
  *_preview.mp4  App Store app preview: 1920x886 (6.9"/6.7" iPhone landscape), 30 fps, H.264, AAC, <= 30 s.
                 Gameplay only (Apple: previews must show footage captured from the app), with a short caption per
                 clip and the game's own sound.
  *_trailer.mp4  Google Play promo video (upload to YouTube, unlisted, paste the link in Play Console):
                 1920x1080, logo opener, story clips, gameplay with captions, end card, Theme music under the game.

Windows are the ones already picked for the gameplay/ability shorts (frames checked then). Check the output before
uploading; nudge a window's start here if a power fires too late in its clip.
"""

import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
FFMPEG = HERE / "bin/ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe"
SESSIONS = HERE / "sessions"
ORCHARD = SESSIONS / "20261106-192320"       # Arcade, Orchard world
FROZEN = SESSIONS / "20261122-172342"        # Stampede, Meadow Ruins boss + Frozen Tundra
VIDEO = Path.home() / "Desktop/FarmFury_Technical/FarmFury_Artwork/Video"
OUT = VIDEO / "Store_Videos"
ASSETS = HERE.parent / "storyline/assets"
THEME = REPO / "Assets/_Project/Audio/Music/Theme.mp3"
BANGERS = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
FPS = 30
YELLOW, DARK = (255, 214, 64), (35, 20, 8)

# (session, start s, seconds, caption)
ARCADE = [
    (ORCHARD, 644.5, 5.4, "BILLY'S MUD SLING"),
    (ORCHARD, 305.5, 5.4, "WOOLLY'S CLONES"),
    (ORCHARD, 333.0, 5.4, "DUCKY'S WATER SPOUT"),
    (ORCHARD, 238.0, 5.4, "PERCY'S BOUNCE ROLL"),
    (ORCHARD, 145.0, 5.4, "BESSIE'S GROUND SLAM"),
]
STAMPEDE = [
    (FROZEN, 236.9, 5.4, "CLUCK'S EGG LAUNCH"),
    (FROZEN, 316.6, 5.4, "BESSIE FINDS A SECRET ROOM"),
    (FROZEN, 424.6, 5.4, "PERCY'S ROLL DASH"),
    (FROZEN, 444.8, 5.4, "WOOLLY'S CLOUD STEP"),
    (FROZEN, 489.2, 5.4, "HORACE'S HORSESHOE THROW"),
]
STORY = [  # Kling story clips for the Google Play trailers: (file, skip s, seconds, caption)
    (VIDEO / "Ep01_Clips/ep01_C_eye.mp4", 1.0, 2.6, "THE HARVEST ROBOTS TOOK THE CROPS..."),
    (VIDEO / "Ep05_Clips/ep05_6.2_circle.mp4", 1.0, 2.8, "...SO THE ANIMALS GOT POWERS!"),
]


def run(args):
    subprocess.run([str(FFMPEG), "-hide_banner", "-loglevel", "error", "-y", *map(str, args)], check=True)


def font(size):
    return ImageFont.truetype(str(BANGERS), size)


def caption_png(text, w, path, size=64):
    """A rounded dark pill with yellow Bangers text, centred, for overlaying at the top of a clip."""
    f = font(size)
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    tw = d.textlength(text, font=f)
    img = Image.new("RGBA", (w, size + 50), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0 = (w - tw) / 2 - 36
    d.rounded_rectangle((x0, 4, x0 + tw + 72, size + 40), 30, fill=(20, 10, 30, 200))
    d.text((w / 2, 4 + (size + 36) / 2), text, font=f, fill=YELLOW, anchor="mm", stroke_width=4, stroke_fill=DARK)
    img.save(path)


def card_png(w, h, lines, path, still=None):
    """Logo card: the Farm Fury Games logo and lines of text on a blurred game backdrop."""
    bg = Image.open(still or REPO / "Assets/_Project/Sprites/UI/landing.png").convert("RGB")
    s = max(w / bg.width, h / bg.height)
    bg = bg.resize((int(bg.width * s) + 1, int(bg.height * s) + 1))
    x0, y0 = (bg.width - w) // 2, (bg.height - h) // 2
    bg = bg.crop((x0, y0, x0 + w, y0 + h)).filter(ImageFilter.GaussianBlur(16))
    img = Image.alpha_composite(bg.convert("RGBA"), Image.new("RGBA", (w, h), (20, 10, 30, 150)))
    logo = Image.open(ASSETS / "farmfury-games-logo.png").convert("RGBA")
    lh = int(h * 0.42)
    logo = logo.resize((int(logo.width * lh / logo.height), lh))
    img.alpha_composite(logo, ((w - logo.width) // 2, int(h * 0.08)))
    d = ImageDraw.Draw(img)
    y = int(h * 0.56)
    for text, size, color in lines:
        d.text((w / 2, y), text, font=font(size), fill=color, anchor="ma", stroke_width=5, stroke_fill=DARK)
        y += int(size * 1.25)
    img.convert("RGB").save(path)


def gameplay_seg(work, i, src, start, secs, cap, w, h, music_trailer):
    """One gameplay clip scaled to w x h (crop for the 2.17:1 preview, blurred bars for 16:9) with its caption."""
    cap_png = work / f"cap{i}.png"
    caption_png(cap, w, cap_png)
    if h / w > 720 / 1600:   # 16:9: fit the whole 2.22:1 frame, blurred copy behind
        vf = (f"[0:v]split[a][b];[a]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},boxblur=24:2,"
              f"eq=brightness=-0.2[bg];[b]scale={w}:-2[fg];[bg][fg]overlay=0:(H-h)/2[v0]")
    else:                    # preview: fill the height, trim the sides a little
        vf = f"[0:v]scale=-2:{h},crop={w}:{h}[v0]"
    vf += f";[v0][1:v]overlay=0:24,fps={FPS},format=yuv420p,setsar=1[v]"
    out = work / f"seg{i:02d}.mp4"
    run(["-ss", f"{start:.2f}", "-t", f"{secs:.2f}", "-i", src / "video.mp4", "-i", cap_png,
         "-filter_complex", vf, "-map", "[v]", "-map", "0:a",
         "-af", f"aresample=48000,afade=t=in:d=0.08,afade=t=out:st={secs - 0.12:.2f}:d=0.12",
         "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-c:a", "aac", "-b:a", "192k", "-ac", "2", out])
    return out


def still_seg(work, i, png, secs, w, h):
    out = work / f"seg{i:02d}.mp4"
    frames = int(secs * FPS)
    run(["-loop", "1", "-t", f"{secs:.2f}", "-i", png, "-f", "lavfi", "-t", f"{secs:.2f}",
         "-i", "anullsrc=r=48000:cl=stereo",
         "-filter_complex", f"[0:v]scale={w * 2}:{h * 2},zoompan=z='1+0.05*on/{frames}':x='iw/2-(iw/zoom/2)':"
                            f"y='ih/2-(ih/zoom/2)':d=1:s={w}x{h}:fps={FPS},format=yuv420p,setsar=1[v]",
         "-map", "[v]", "-map", "1:a", "-c:v", "libx264", "-crf", "18", "-c:a", "aac", "-b:a", "192k", out])
    return out


def story_seg(work, i, clip, skip, secs, cap, w, h):
    """A 9:16 Kling story clip on a blurred 16:9 copy of itself, with a caption."""
    cap_png = work / f"cap{i}.png"
    caption_png(cap, w, cap_png, 60)
    out = work / f"seg{i:02d}.mp4"
    run(["-ss", f"{skip:.2f}", "-t", f"{secs:.2f}", "-i", clip, "-i", cap_png,
         "-f", "lavfi", "-t", f"{secs:.2f}", "-i", "anullsrc=r=48000:cl=stereo",
         "-filter_complex", f"[0:v]split[a][b];[a]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},"
                            f"boxblur=30:2,eq=brightness=-0.25[bg];[b]scale=-2:{h}[fg];[bg][fg]overlay=(W-w)/2:0[v0];"
                            f"[v0][1:v]overlay=0:24,fps={FPS},format=yuv420p,setsar=1[v]",
         "-map", "[v]", "-map", "2:a", "-c:v", "libx264", "-crf", "18", "-c:a", "aac", "-b:a", "192k", out])
    return out


def join(work, segs, out, music=None, game_gain=1.0):
    listing = work / "list.txt"
    listing.write_text("".join(f"file '{s.as_posix()}'\n" for s in segs), encoding="utf-8")
    joined = work / "joined.mp4"
    run(["-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", joined])
    if not music:
        run(["-i", joined, "-c", "copy", "-movflags", "+faststart", out])
        return
    dur = float(subprocess.run([str(FFMPEG), "-i", str(joined)], capture_output=True, text=True).stderr
                .split("Duration: ")[1].split(",")[0].split(":")[-1]) + 0  # seconds part only (videos are < 1 min)
    run(["-i", joined, "-stream_loop", "-1", "-t", f"{dur:.2f}", "-i", music,
         "-filter_complex", f"[0:a]volume={game_gain}[g];[1:a]volume=0.55,afade=t=out:st={dur - 2.0:.2f}:d=2.0[m];"
                            f"[g][m]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.9[a]",
         "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
         "-t", f"{dur:.2f}", out])


def preview(name, clips, end_line):
    w, h = 1920, 886
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        segs = [gameplay_seg(work, i, *c, w, h, False) for i, c in enumerate(clips)]
        out = OUT / f"{name}.mp4"
        join(work, segs, out)
    return out


def trailer(name, clips, game_title, backdrop):
    w, h = 1920, 1080
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        opener, end = work / "opener.png", work / "end.png"
        card_png(w, h, [(game_title, 96, (255, 255, 255))], opener, backdrop)
        card_png(w, h, [("FREE TO PLAY", 110, YELLOW), ("FARMFURYGAMES.COM", 64, (255, 255, 255))], end, backdrop)
        segs = [still_seg(work, 0, opener, 2.0, w, h)]
        segs += [story_seg(work, 1 + i, *s, w, h) for i, s in enumerate(STORY)]
        segs += [gameplay_seg(work, 10 + i, src, start, 3.6, cap, w, h, True)
                 for i, (src, start, _, cap) in enumerate(clips)]
        segs.append(still_seg(work, 30, end, 3.0, w, h))
        out = OUT / f"{name}.mp4"
        join(work, segs, out, music=THEME, game_gain=0.45)
    return out


JOBS = {
    "arcade_preview": lambda: preview("arcade_preview", ARCADE, None),
    "stampede_preview": lambda: preview("stampede_preview", STAMPEDE, None),
    "arcade_trailer": lambda: trailer("arcade_trailer", ARCADE, "FARM FURY: ARCADE",
                                      VIDEO / "Ep04_Clips/stills/4.1_meeting.png"),
    "stampede_trailer": lambda: trailer("stampede_trailer", STAMPEDE, "FARM FURY: STAMPEDE",
                                        VIDEO / "Ep10_Clips/stills/10.7_stampede.png"),
}

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for key in sys.argv[1:] or list(JOBS):
        print(JOBS[key]())
