"""Turn a session's best moments into vertical 9:16 shorts (1080x1920).

Makes three kinds of short, each opening with the Farm Fury poster (1.5 s):
  mix_01.mp4       highlight mix: the best 3 moments, strongest first (~25-30 s)
  short_01.mp4...  one moment each, lengthened to ~12-18 s
  unlock_<name>.mp4  a character/world unlock: the level finishing, then the
                   unlock card held on screen for a few seconds

Layout of every frame, top to bottom:
  headline           what's happening ("EAT ALL THE ROBOTS!")
  picture            the centre of the game screen (the maze), so the on-screen
                     D-pad/buttons and the "Development Build" label are cut away
  call to action     game logo + "FREE ON GOOGLE PLAY", above the bottom ~350px
                     that TikTok/Shorts cover with their own buttons
A blurred, darkened copy of the picture fills the background.

Usage:
    python make_shorts.py                      # newest session
    python make_shorts.py sessions/<id> --count 5
    python make_shorts.py --include-deaths     # also use clips where you died
"""

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from normalize_video import find_ffmpeg

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
FONT = REPO / "Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf"
LOGO = REPO / "Assets/_Project/Sprites/UI/Logo.png"
POSTER = REPO / "Assets/_Project/Sprites/UI/landing.png"
THEME_MUSIC = REPO / "Assets/_Project/Audio/Music/Theme.mp3"

W, H = 1080, 1920
FPS = 30
# Width of the centre crop as a multiple of the landscape frame's height. The maze is centred and
# about 1.25x as wide as tall; 1.3 keeps a little scenery either side.
CROP_ASPECT = 1.3
PICTURE_CENTER_Y = 960
HOOK_BOX = (60, 190, W - 60, 520)      # left, top, right, bottom
CTA_TOP = 1420

OPENER_SECONDS = 1.5
SINGLE_MIN_SECONDS = 12.0
SINGLE_MAX_SECONDS = 18.0
SINGLE_LEAD_SHARE = 0.6                # extra length goes 60% before the moment, 40% after
MIX_CLIPS = 3
MIX_SEGMENT_MAX_SECONDS = 8.5
# The unlock card appears after the Level Complete stars/score reveal, roughly this long after the
# character_unlock marker (measured on the first recording: marker 238.0 s, card fully in at 241 s).
UNLOCK_CARD_DELAY_SECONDS = 3.3
UNLOCK_LEAD_SECONDS = 8.0
UNLOCK_HOLD_SECONDS = 2.5

TEXT_FILL = (255, 255, 255)
ACCENT_FILL = (255, 214, 64)
STROKE_FILL = (58, 32, 12)

VIDEO_OUT = ["-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-r", str(FPS)]
AUDIO_OUT = ["-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-ac", "2"]


# ---------------------------------------------------------------------------------------------
# Headlines and the text overlay

# Child-friendly wording: the game is marketed to kids and families. Robots get zapped, bonked,
# busted or powered down (the game's own How to Play text says "zap"); never killed or eaten.
# check_wording() refuses any headline containing these, including AI-written ones later.
AVOID_WORDS = {"kill", "kills", "killed", "killing", "eat", "eats", "eaten", "eating", "die", "dies",
               "died", "dead", "death", "murder", "destroy", "destroyed", "blood", "gun", "shoot", "shot"}


def check_wording(text):
    words = {w.strip(".,!?:;'\"").lower() for w in text.split()}
    bad = sorted(words & AVOID_WORDS)
    if bad:
        raise ValueError(f"Headline uses words we don't use in kids' marketing: {bad} in {text!r}")
    return text


# Several headlines per kind of moment, so shorts made from one session don't repeat. The game is
# about dodging the robots and collecting every crop; lead with that, not with chasing robots.
HEADLINES = {
    "near_miss": ["DODGE! THAT ROBOT WAS SO CLOSE!", "HOW DID I DODGE THAT?!", "ONE TILE AWAY FROM TROUBLE",
                  "THE ROBOTS ALMOST HAD ME...", "SLIPPED PAST THE HARVEST ROBOTS!"],
    "level_complete": ["EVERY LAST CROP COLLECTED!", "FARM SAVED! NEXT LEVEL!", "CROPS SAFE. ROBOTS DODGED.",
                       "CLEARED THE WHOLE FIELD!", "NOT ONE CROP LEFT FOR THE ROBOTS"],
    "power_pellet": ["GRAB THE POWER CROP!", "POWER CROP = ROBOTS ON THE RUN!", "NOW THE ROBOTS RUN FROM ME!"],
    "full_chain": ["POWER CROP = ROBOTS ON THE RUN!", "THE ROBOTS SCATTERED!", "SENT THE ROBOTS PACKING!"],
    "combo": ["COMBO: {name}!"],
    "character_unlock": ["NEW CHARACTER UNLOCKED: {character}!"],
    "world_unlock": ["A NEW WORLD JUST OPENED UP!"],
    "default": ["DODGE THE ROBOTS. SAVE THE CROPS.", "FARM ANIMALS vs HARVEST ROBOTS"],
}
# Which moment a headline is about, in priority order.
HEADLINE_PRIORITY = ["character_unlock", "world_unlock", "combo", "near_miss", "level_complete",
                     "power_pellet", "full_chain"]


def hook_text(moments, variant=0):
    """Headline for the most important moment in the list; variant picks among the options."""
    by_type = {m["type"]: m for m in moments}
    kind = next((k for k in HEADLINE_PRIORITY if k in by_type), "default")
    options = HEADLINES[kind]
    marker = by_type.get(kind, {})
    return options[variant % len(options)].format(
        name=marker.get("name", "").replace("_", " ").upper(),
        character=marker.get("character", "").upper())


def fit_font(draw, text, box, max_size=120, min_size=56):
    """Largest font size (and wrapped lines) that fits text inside box."""
    width, height = box[2] - box[0], box[3] - box[1]
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
    return ImageFont.truetype(str(FONT), min_size), [text], int(min_size * 1.05)


def build_overlay(hook, path):
    check_wording(hook)
    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    font, lines, line_height = fit_font(draw, hook, HOOK_BOX)
    top = HOOK_BOX[1] + (HOOK_BOX[3] - HOOK_BOX[1] - len(lines) * line_height) // 2
    stroke = max(4, font.size // 12)
    for i, line in enumerate(lines):
        x = (W - draw.textlength(line, font=font)) / 2
        draw.text((x, top + i * line_height), line, font=font, fill=TEXT_FILL,
                  stroke_width=stroke, stroke_fill=STROKE_FILL)

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


# ---------------------------------------------------------------------------------------------
# Rendering. Every segment is rendered to the same format, then segments are joined.

def vertical_frame_filter(crop_centre):
    """[0:v] -> [pic] laid out 1080x1920 with blurred background. crop_centre=False keeps the full
    width (used for the poster, whose title spans the whole image)."""
    crop = f"crop=ih*{CROP_ASPECT}:ih:(iw-ih*{CROP_ASPECT})/2:0," if crop_centre else ""
    return (
        f"[0:v]{crop}setsar=1,split[a][b];"
        f"[a]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,eq=brightness=-0.18[bg];"
        f"[b]scale={W}:-2[fg];"
        f"[bg][fg]overlay=0:{PICTURE_CENTER_Y}-h/2,fps={FPS}[pic];"
    )


def render_segment(ffmpeg, out, duration, overlay, visual, audio):
    """visual: ("video", path, start) or ("image", path, zoom).  audio: ("file", path, start) or None."""
    kind, vpath, vopt = visual
    if kind == "video":
        inputs = ["-ss", f"{vopt:.2f}", "-t", f"{duration:.2f}", "-i", str(vpath)]
        picture = vertical_frame_filter(crop_centre=True)
    else:
        inputs = ["-loop", "1", "-framerate", str(FPS), "-t", f"{duration:.2f}", "-i", str(vpath)]
        picture = vertical_frame_filter(crop_centre=(vopt != "poster"))
    inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{duration:.2f}", "-i", str(overlay)]

    if audio is None:
        inputs += ["-f", "lavfi", "-t", f"{duration:.2f}", "-i", "anullsrc=r=48000:cl=stereo"]
    else:
        _, apath, astart = audio
        inputs += ["-ss", f"{astart:.2f}", "-t", f"{duration:.2f}", "-i", str(apath)]

    # A slow push-in on stills so they don't look frozen solid.
    zoom, pic = "", "[pic]"
    if kind == "image":
        frames = max(1, int(duration * FPS))
        zoom = (f"[pic]scale={W * 2}:{H * 2},zoompan=z='1+0.06*on/{frames}':x='iw/2-(iw/zoom/2)':"
                f"y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS}[zoomed];")
        pic = "[zoomed]"
    filters = (
        picture + zoom +
        f"{pic}[1:v]overlay=0:0,setsar=1,format=yuv420p[v];"
        "[2:a]aformat=sample_rates=48000:channel_layouts=stereo,apad[a]"
    )
    cmd = [ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", filters,
           "-map", "[v]", "-map", "[a]", "-t", f"{duration:.2f}", *VIDEO_OUT, *AUDIO_OUT, str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed rendering {out.name}")


def join_segments(ffmpeg, parts, out):
    """Concatenate segments, balance loudness for social apps (-14 LUFS), fade in/out."""
    total = sum(d for _, d in parts)
    inputs = []
    for path, _ in parts:
        inputs += ["-i", str(path)]
    n = len(parts)
    # setsar=1 on every input: zoompan can emit a near-1 pixel aspect that concat refuses to mix.
    prep = "".join(f"[{i}:v]setsar=1[sv{i}];" for i in range(n))
    streams = "".join(f"[sv{i}][{i}:a]" for i in range(n))
    fade = 0.3
    filters = (
        f"{prep}{streams}concat=n={n}:v=1:a=1[cv][ca];"
        f"[cv]fade=t=in:st=0:d={fade},fade=t=out:st={total - fade:.2f}:d={fade}[v];"
        f"[ca]loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=in:st=0:d={fade},afade=t=out:st={total - fade:.2f}:d={fade}[a]"
    )
    cmd = [ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", filters, "-map", "[v]", "-map", "[a]",
           *VIDEO_OUT, *AUDIO_OUT, "-movflags", "+faststart", str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed joining {out.name}")
    return total


def extract_frame(ffmpeg, video, t, out):
    cmd = [ffmpeg, "-v", "error", "-y", "-ss", f"{t:.2f}", "-i", str(video), "-frames:v", "1", str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError("ffmpeg failed extracting a still")


def video_duration(ffmpeg, video):
    ffprobe = str(Path(ffmpeg).with_name("ffprobe.exe"))
    out = subprocess.run([ffprobe, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(video)],
                         capture_output=True, text=True).stdout
    return float(out.strip())


# ---------------------------------------------------------------------------------------------
# Choosing what goes into each short

def lengthen(start, end, target, video_len):
    """Grow [start, end] to at least target seconds, mostly by adding lead-in, within the video."""
    extra = max(0.0, target - (end - start))
    start -= extra * SINGLE_LEAD_SHARE
    end += extra * (1 - SINGLE_LEAD_SHARE)
    if start < 0:
        end, start = end - start, 0.0
    if end > video_len:
        start, end = max(0.0, start - (end - video_len)), video_len
    return start, end


def best_moment_time(clip):
    return max(clip["moments"], key=lambda m: m.get("_score", 0))["t"]


def trim_for_mix(clip):
    """Cut a clip down to MIX_SEGMENT_MAX_SECONDS, keeping its best moment ~60% of the way in."""
    start, end = clip["start"], clip["end"]
    if end - start <= MIX_SEGMENT_MAX_SECONDS:
        return start, end
    centre = best_moment_time(clip)
    start = max(start, centre - MIX_SEGMENT_MAX_SECONDS * 0.6)
    return start, min(end, start + MIX_SEGMENT_MAX_SECONDS)


class Builder:
    def __init__(self, ffmpeg, video, out_dir, work_dir):
        self.ffmpeg, self.video, self.out_dir, self.work = ffmpeg, video, out_dir, work_dir
        self.count = 0

    def _temp(self, suffix):
        self.count += 1
        return self.work / f"part_{self.count:03}{suffix}"

    def overlay(self, hook):
        path = self._temp(".png")
        build_overlay(hook, path)
        return path

    def opener(self, hook):
        out = self._temp(".mp4")
        render_segment(self.ffmpeg, out, OPENER_SECONDS, self.overlay(hook),
                       ("image", POSTER, "poster"), ("file", THEME_MUSIC, 0.0))
        return out, OPENER_SECONDS

    def gameplay(self, hook, start, end):
        out = self._temp(".mp4")
        render_segment(self.ffmpeg, out, end - start, self.overlay(hook),
                       ("video", self.video, start), ("file", self.video, start))
        return out, end - start

    def hold(self, hook, t, duration):
        """A still frame from the video at t, held for duration, with the video's own audio."""
        still = self._temp(".png")
        extract_frame(self.ffmpeg, self.video, t, still)
        out = self._temp(".mp4")
        render_segment(self.ffmpeg, out, duration, self.overlay(hook),
                       ("image", still, "frame"), ("file", self.video, t))
        return out, duration

    def finish(self, name, parts, info):
        out = self.out_dir / f"{name}.mp4"
        total = join_segments(self.ffmpeg, parts, out)
        info = dict(info, file=out.name, seconds=round(total, 1))
        (self.out_dir / f"{name}.json").write_text(json.dumps(info, indent=2), encoding="utf-8")
        print(f"  {out.name}  {total:.1f}s  \"{info['hook']}\"")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("session", nargs="?", help="session folder (default: newest)")
    parser.add_argument("--count", type=int, default=3, help="how many single-moment shorts (default 3)")
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
    markers = json.loads((session_dir / "markers.json").read_text(encoding="utf-8"))

    from parse_markers import score, WEIGHTS  # reuse the same moment scoring
    for clip in clips:
        for m in clip["moments"]:
            m["_score"] = score(m) if m["type"] in WEIGHTS else 0

    usable = [c for c in clips if args.include_deaths or not c["has_death"]]
    # Unlock moments get their own short, so keep them out of the singles/mix.
    gameplay_clips = [c for c in usable if not any(m["type"] in ("character_unlock", "world_unlock") for m in c["moments"])]

    ffmpeg = find_ffmpeg()
    video_len = video_duration(ffmpeg, video)
    out_dir = session_dir / "shorts"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir()

    with tempfile.TemporaryDirectory() as tmp:
        b = Builder(ffmpeg, video, out_dir, Path(tmp))

        mix = gameplay_clips[:MIX_CLIPS]
        if len(mix) >= 2:
            print("Highlight mix:")
            first_hook = hook_text(mix[0]["moments"])
            parts = [b.opener(first_hook)]
            for j, clip in enumerate(mix):
                start, end = trim_for_mix(clip)
                parts.append(b.gameplay(hook_text(clip["moments"], variant=j), start, end))
            b.finish("mix_01", parts, {"hook": first_hook, "clips": [[c["start"], c["end"]] for c in mix]})

        print("Single moments:")
        for i, clip in enumerate(gameplay_clips[: args.count], 1):
            hook = hook_text(clip["moments"], variant=i)
            start, end = lengthen(clip["start"], clip["end"], SINGLE_MIN_SECONDS, video_len)
            end = min(end, start + SINGLE_MAX_SECONDS)
            parts = [b.opener(hook), b.gameplay(hook, start, end)]
            b.finish(f"short_{i:02}", parts, {"hook": hook, "clip": [start, end]})

        unlocks = [m for m in markers if m["type"] in ("character_unlock", "world_unlock")]
        if unlocks:
            print("Unlocks:")
        for m in unlocks:
            hook = hook_text([m])
            card_t = min(m["t"] + UNLOCK_CARD_DELAY_SECONDS, video_len - 0.1)
            start = max(0.0, card_t - UNLOCK_LEAD_SECONDS)
            parts = [b.opener(hook), b.gameplay(hook, start, card_t), b.hold(hook, card_t, UNLOCK_HOLD_SECONDS)]
            name = m.get("character") or f"world{m.get('world', '')}"
            b.finish(f"unlock_{name.lower()}", parts, {"hook": hook, "clip": [start, card_t], "hold_at": card_t})

    print(f"Shorts written to {out_dir}")


if __name__ == "__main__":
    main()
