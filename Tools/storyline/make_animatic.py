"""Rough animatic of storyline Episodes 0-1: the recorded ElevenLabs voice lines over still game art
with slow zooms, captions and music, in 9:16 (1080x1920). It is a pacing/voice test and a stand-in
until the Kling clips exist - not the finished episode. No end card (that waits for the App Store).

Voices come from <Video>/Eleven_Audio/<character>_<clip>.mp3 (see FarmFury_Voice_Script.md); robot
lines from <Video>/Robot_Voices (run robot_fx.py first).
Output: <Video>/Storyline_Animatic/ep00-01_animatic.mp4

Usage:
    python make_animatic.py
"""

import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFont

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from make_shorts import (ACCENT_FILL, AUDIO_OUT, FONT, FPS, H, LOGO, REPO, STROKE_FILL,  # noqa: E402
                         TEXT_FILL, VIDEO_OUT, W, check_wording)
from normalize_video import find_ffmpeg  # noqa: E402

VIDEO_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
VOICE_DIR = VIDEO_DIR / "Eleven_Audio"
ROBOT_VOICE_DIR = VIDEO_DIR / "Robot_Voices"     # robot lines with effects, from robot_fx.py
OUT = VIDEO_DIR / "Storyline_Animatic" / "ep00-01_animatic.mp4"
UI = REPO / "Assets/_Project/Sprites/UI"
CHARS = REPO / "Assets/_Project/Sprites/Characters"
ROBOTS = REPO / "Assets/_Project/Sprites/Robots"
SFX = REPO / "Assets/_Project/Audio/SFX"
MUSIC = REPO / "Assets/_Project/Audio/Music/Theme.mp3"
# Top-down maze from a real Android recording (level start, all crops still there); crop = the board.
MAZE_VIDEO = HERE.parent / "content-pipeline/sessions/20261001-150832/video.mp4"
MAZE_TIME = 20.5
MAZE_CROP = (350, 15, 1250, 700)

SCALE = 2                 # stills are drawn at 2x so the slow zoom stays smooth
LEAD, GAP, TAIL = 0.5, 0.35, 0.7
SCENE_FADE = 0.3
MUSIC_GAIN = 0.16         # music sits well under the voices
SPEAKER_FILL = {"CLUCK": ACCENT_FILL, "BESSIE": (255, 190, 210), "THE COMBINE": (255, 90, 70)}

# Each scene: background, sprites (file, centre x, centre y, height - in 1080x1920 units), zoom
# start/end, optional red tint ("red": seconds after scene start when the red light starts, or
# "always"), voice lines (file stem, caption) and sound effects (file, seconds after scene start).
SCENES = [
    {"name": "0.1", "bg": UI / "World1_Cornfield.png", "bg_focus": 0.82, "zoom": (1.0, 1.18),
     "lines": [("cluck_0.1", "Bessie… Bessie, are you awake?")], "red": "end", "sfx": [("RobotSpawn.mp3", "end")]},
    {"name": "0.2", "bg": UI / "Bg_LevelSelect.png", "bg_focus": 0.2, "dim": 0.55, "zoom": (1.06, 1.0),
     "sprites": [(CHARS / "Cluck_front.png", 330, 1180, 520), (CHARS / "Bessie_front.png", 760, 1130, 700)],
     "lines": [("bessie_0.2", "I am now, Clucky."),
               ("cluck_0.2", "Don't call me— THERE'S A SPACESHIP IN THE CORN!")], "red": "line2"},
    {"name": "0.3", "bg": UI / "World1_Cornfield.png", "bg_focus": 0.3, "dim": 0.35, "zoom": (1.1, 1.0),
     "red": "always",
     "sprites": [(ROBOTS / "Drone.png", 540, 620, 240),
                 (ROBOTS / "ScoutRobot_front.png", 250, 1090, 320),
                 (ROBOTS / "HarvestorRobot_front.png", 760, 1050, 440)],
     "lines": [("robot_combine_0.3", "COLLECT. EVERY. CROP.")], "sfx": [("RobotSpawn.mp3", 0.1)]},
    {"name": "title", "title": True, "seconds": 3.0},
    {"name": "1.1", "bg": UI / "World1_Cornfield.png", "bg_focus": 0.55, "zoom": (1.12, 1.0),
     "lines": [("cluck_1.1", "It was harvest night. The corn was golden, the carrots were crunchy, "
                               "and everybody was fast asleep…"),
               ("bessie_1.1", "…except one chicken who'd had too much corn before bed.")]},
    {"name": "1.4", "bg": UI / "World1_Cornfield.png", "bg_focus": 0.5, "dim": 0.25, "zoom": (1.0, 1.25),
     "red": "always", "sprites": [(ROBOTS / "RobotEyes.png", 540, 860, 700)],
     "lines": [("robot_combine_1.4", "Target: farm. Instruction: collect every crop. Every. Single. One.")]},
    {"name": "1.5", "bg": UI / "World1_Cornfield.png", "bg_focus": 0.75, "dim": 0.45, "zoom": (1.0, 1.12),
     "red": "always",
     "sprites": [(ROBOTS / "Drone.png", 540, 640, 260),
                 (ROBOTS / "ScoutRobot_front.png", 170, 1080, 300),
                 (ROBOTS / "HarvestorRobot_front.png", 400, 1040, 420),
                 (ROBOTS / "PatrolRobot_Front.png", 680, 1060, 360),
                 (ROBOTS / "DriftRobot_front.png", 910, 1090, 300)],
     "lines": [("cluck_1.5_a", "And out came… them."), ("bessie_1.5", "The Harvest Robots.")],
     "sfx": [("RobotSpawn.mp3", 0.2)]},
    {"name": "1.6", "bg": "maze", "zoom": (1.35, 1.0),
     "lines": [("bessie_1.6", "In one night they turned our fields into mazes. "
                                "Harvest corridors, they called them."),
               ("cluck_1.6_a", "Rude, is what I called them.")]},
    {"name": "tbc", "tbc": True, "seconds": 2.8},
]


def run(cmd):
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError("command failed: " + " ".join(map(str, cmd[:6])))


def probe_duration(ffmpeg, path):
    ffprobe = str(Path(ffmpeg).with_name("ffprobe.exe"))
    out = subprocess.run([ffprobe, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0",
                          str(path)], capture_output=True, text=True, check=True).stdout
    return float(out.strip())


def prepare_voice(ffmpeg, stem, work):
    """Trim silence off both ends (pauses inside the line stay) and level each line."""
    src = (ROBOT_VOICE_DIR / f"{stem}.wav") if stem.startswith("robot_") else VOICE_DIR / f"{stem}.mp3"
    if not src.exists():
        raise FileNotFoundError(src)
    level = -19 if stem == "cluck_0.1" else -16      # the whisper stays a bit quieter
    trim = "silenceremove=start_periods=1:start_threshold=-45dB"
    out = work / f"{stem}.wav"
    run([ffmpeg, "-v", "error", "-y", "-i", str(src), "-af",
         f"{trim},areverse,{trim},areverse,loudnorm=I={level}:TP=-2:LRA=11,"
         "aformat=sample_rates=48000:channel_layouts=stereo", str(out)])
    return out, probe_duration(ffmpeg, out)


def cover(img, w, h, focus_x=0.5):
    """Scale to fill w x h, cropping the overflow; focus_x picks which part of a wide image stays."""
    s = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    left = round((img.width - w) * focus_x)
    top = (img.height - h) // 2
    return img.crop((left, top, left + w, top + h))


def build_still(scene, ffmpeg, work):
    w, h = W * SCALE, H * SCALE
    if scene["bg"] == "maze":
        frame = work / "maze.png"
        run([ffmpeg, "-v", "error", "-y", "-ss", str(MAZE_TIME), "-i", str(MAZE_VIDEO), "-frames:v", "1", str(frame)])
        board = Image.open(frame).convert("RGB").crop(MAZE_CROP)
        bg = cover(Image.open(UI / "World1_Cornfield.png").convert("RGB"), w, h, 0.5)
        bg = ImageEnhance.Brightness(bg).enhance(0.45)
        bw = round(w * 0.96)
        board = board.resize((bw, round(board.height * bw / board.width)), Image.LANCZOS)
        bg.paste(board, ((w - bw) // 2, round(h * 0.40) - board.height // 2))
        img = bg
    else:
        img = cover(Image.open(scene["bg"]).convert("RGB"), w, h, scene.get("bg_focus", 0.5))
        if "dim" in scene:
            img = ImageEnhance.Brightness(img).enhance(scene["dim"])
    img = img.convert("RGBA")
    for path, cx, cy, sh in scene.get("sprites", []):
        sp = Image.open(path).convert("RGBA")
        sp = sp.crop(sp.getbbox())
        sh *= SCALE
        sp = sp.resize((round(sp.width * sh / sp.height), sh), Image.LANCZOS)
        img.alpha_composite(sp, (cx * SCALE - sp.width // 2, cy * SCALE - sp.height // 2))
    img = img.convert("RGB")
    plain = work / f"{scene['name']}_still.png"
    img.save(plain)
    red = None
    if scene.get("red"):
        tint = Image.new("RGB", img.size, (255, 30, 20))
        red_img = Image.blend(img, tint, 0.38)
        red_img = ImageEnhance.Brightness(red_img).enhance(0.9)
        red = work / f"{scene['name']}_red.png"
        red_img.save(red)
    return plain, red


def text_lines(draw, text, font, max_w):
    words, lines, cur = text.split(), [], ""
    for wd in words:
        test = (cur + " " + wd).strip()
        if draw.textlength(test, font=font) <= max_w:
            cur = test
        else:
            lines.append(cur)
            cur = wd
    return lines + [cur]


def build_caption(stem, text, path):
    check_wording(text)
    text = text.replace("…", "...")   # Bangers has no ellipsis glyph
    speaker = "THE COMBINE" if stem.startswith("robot_combine") else stem.split("_")[0].upper()
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    name_font = ImageFont.truetype(str(FONT), 64)
    font = ImageFont.truetype(str(FONT), 76)
    lines = text_lines(d, text, font, W - 160)
    line_h = 84
    box_h = 100 + line_h * len(lines) + 40
    top = 1500 - box_h // 2
    d.rounded_rectangle((50, top, W - 50, top + box_h), radius=36, fill=(20, 12, 30, 175))
    d.text((W // 2, top + 58), speaker, font=name_font, fill=SPEAKER_FILL.get(speaker, TEXT_FILL),
           anchor="mm", stroke_width=5, stroke_fill=STROKE_FILL)
    for i, ln in enumerate(lines):
        d.text((W // 2, top + 130 + i * line_h), ln, font=font, fill=TEXT_FILL, anchor="mm",
               stroke_width=6, stroke_fill=STROKE_FILL)
    img.save(path)


def build_card(scene, work):
    """Title card or 'to be continued' card on the dimmed poster."""
    img = cover(Image.open(UI / "Landing_Opacity.png").convert("RGB"), W * SCALE, H * SCALE, 0.5)
    img = ImageEnhance.Brightness(img).enhance(0.4).convert("RGBA")
    d = ImageDraw.Draw(img)
    if scene.get("title"):
        logo = Image.open(LOGO).convert("RGBA")
        size = 760 * SCALE
        logo = logo.resize((size, round(logo.height * size / logo.width)), Image.LANCZOS)
        img.alpha_composite(logo, ((img.width - size) // 2, 520 * SCALE))
        f1 = ImageFont.truetype(str(FONT), 70 * SCALE)
        f2 = ImageFont.truetype(str(FONT), 112 * SCALE)
        d.text((img.width // 2, 1360 * SCALE), "EPISODE 1", font=f1, fill=ACCENT_FILL, anchor="mm",
               stroke_width=6 * SCALE, stroke_fill=STROKE_FILL)
        for i, ln in enumerate(["THE NIGHT THE", "ROBOTS CAME"]):
            d.text((img.width // 2, (1490 + i * 125) * SCALE), ln, font=f2, fill=TEXT_FILL, anchor="mm",
                   stroke_width=8 * SCALE, stroke_fill=STROKE_FILL)
    else:
        f = ImageFont.truetype(str(FONT), 120 * SCALE)
        d.text((img.width // 2, img.height // 2), "TO BE CONTINUED...", font=f, fill=ACCENT_FILL,
               anchor="mm", stroke_width=8 * SCALE, stroke_fill=STROKE_FILL)
    path = work / f"{scene['name']}_card.png"
    img.convert("RGB").save(path)
    return path


def zoom_filter(label_in, label_out, z0, z1, frames):
    z = f"{z0}+({z1}-{z0})*on/{frames}"
    return (f"[{label_in}]zoompan=z='{z}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}"
            f":s={W}x{H}:fps={FPS},setsar=1[{label_out}]")


def render_scene(ffmpeg, scene, work, voices):
    if scene.get("title") or scene.get("tbc"):
        dur = scene["seconds"]
        still, red, captions = build_card(scene, work), None, []
        zoom = (1.0, 1.05)
    else:
        still, red = build_still(scene, ffmpeg, work)
        t, captions = LEAD, []
        for i, (stem, text) in enumerate(scene["lines"]):
            cap = work / f"{scene['name']}_cap{i}.png"
            build_caption(stem, text, cap)
            d = voices[stem][1]
            captions.append((cap, t, t + d, stem))
            t += d + GAP
        dur = t - GAP + TAIL
        zoom = scene["zoom"]
    frames = round(dur * FPS)

    inputs = ["-i", str(still)]
    graph = [zoom_filter("0:v", "base", zoom[0], zoom[1], frames)]
    cur = "base"
    if red:
        inputs += ["-i", str(red)]
        graph.append(zoom_filter("1:v", "redz", zoom[0], zoom[1], frames))
        if scene["red"] == "always":
            ramp = "1"
        elif scene["red"] == "end":
            start = dur - 1.3
            ramp = f"clip((T-{start:.2f})/0.6,0,1)"
        else:  # red light flickers in as the second line starts
            start = captions[1][1]
            ramp = f"clip((T-{start:.2f})/0.25,0,1)*(0.75+0.25*sin(T*18))"
        graph.append(f"[base][redz]blend=all_expr='A+(B-A)*{ramp}'[tinted]")
        cur = "tinted"
    for i, (cap, a, b, _) in enumerate(captions):
        idx = len(inputs) // 2
        inputs += ["-i", str(cap)]
        graph.append(f"[{cur}][{idx}:v]overlay=0:0:enable='between(t,{a:.2f},{b + 0.25:.2f})'[c{i}]")
        cur = f"c{i}"
    graph.append(f"[{cur}]fade=t=in:st=0:d={SCENE_FADE},fade=t=out:st={dur - SCENE_FADE:.2f}:d={SCENE_FADE},"
                 "format=yuv420p[v]")
    out = work / f"scene_{scene['name']}.mp4"
    run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(graph), "-map", "[v]",
         "-frames:v", str(frames), *VIDEO_OUT, str(out)])

    events = [(voices[stem][0], a, 1.0) for _, a, _, stem in captions]
    for name, when in scene.get("sfx", []):
        when = dur - 1.3 if when == "end" else when
        events.append((SFX / name, when, 0.55))
    return out, dur, events


def main():
    ffmpeg = find_ffmpeg()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        stems = {stem for s in SCENES for stem, _ in s.get("lines", [])}
        voices = {stem: prepare_voice(ffmpeg, stem, work) for stem in sorted(stems)}

        parts, events, t = [], [], 0.0
        for scene in SCENES:
            path, dur, ev = render_scene(ffmpeg, scene, work, voices)
            parts.append(path)
            events += [(f, t + a, g) for f, a, g in ev]
            t += dur
            print(f"  {scene['name']}: {dur:.1f}s")
        total = t

        concat = work / "parts.txt"
        concat.write_text("".join(f"file '{p.as_posix()}'\n" for p in parts))
        video = work / "video.mp4"
        run([ffmpeg, "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(concat), "-c", "copy", str(video)])

        inputs = ["-i", str(video), "-stream_loop", "-1", "-i", str(MUSIC)]
        graph = [f"[1:a]aformat=sample_rates=48000:channel_layouts=stereo,volume={MUSIC_GAIN},"
                 f"afade=t=in:st=0:d=1,afade=t=out:st={total - 2.5:.2f}:d=2.5,atrim=0:{total:.2f}[m]"]
        labels = ["[m]"]
        for i, (f, when, gain) in enumerate(events):
            inputs += ["-i", str(f)]
            ms = int(when * 1000)
            graph.append(f"[{i + 2}:a]aformat=sample_rates=48000:channel_layouts=stereo,volume={gain},"
                         f"adelay={ms}|{ms}[e{i}]")
            labels.append(f"[e{i}]")
        graph.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=first,"
                     "loudnorm=I=-14:TP=-1.5:LRA=11[a]")
        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(graph), "-map", "0:v",
             "-map", "[a]", "-t", f"{total:.2f}", "-c:v", "copy", *AUDIO_OUT, "-movflags", "+faststart",
             str(OUT)])
    print(f"Wrote {OUT} ({total:.1f}s)")


if __name__ == "__main__":
    main()
