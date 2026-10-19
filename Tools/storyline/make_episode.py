"""Storyline Episode 1 ("The Night the Robots Came") from the Kling clips: each clip trimmed to its
slot, joined in order, voice lines dropped in at set times with burned-in captions, the clips' own
Kling sound kept as quiet ambience, and a music bed that dips under the voices. 9:16, 1080x1920.
Ends on a card with the Farm Fury Arcade logo and the App Store / Google Play badges. No title card.

Clips: <Video>/Ep01_Clips/ep01_<letter>_*.mp4. Voices: <Video>/Eleven_Audio, robot lines from
<Video>/Robot_Voices (run robot_fx.py first).
Output: <Video>/Episodes/ep01_the_night_the_robots_came.mp4

Usage:
    python make_episode.py              # App Store + Google Play badges
    python make_episode.py --play-only  # Google Play badge only (to post before the iOS app is live)
"""

import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from PIL import Image, ImageDraw, ImageFilter, ImageFont  # noqa: E402

from make_shorts import (ACCENT_FILL, AUDIO_OUT, FONT, FPS, H, LOGO, POSTER, REPO, STROKE_FILL,  # noqa: E402
                         TEXT_FILL, VIDEO_OUT, W)
from make_animatic import VIDEO_DIR, build_caption, prepare_voice, run  # noqa: E402
from normalize_video import find_ffmpeg  # noqa: E402

CLIP_DIR = VIDEO_DIR / "Ep01_Clips"
OUT = VIDEO_DIR / "Episodes" / "ep01_the_night_the_robots_came.mp4"
OUT_PLAY_ONLY = VIDEO_DIR / "Episodes" / "ep01_the_night_the_robots_came_play_only.mp4"
MUSIC = REPO / "Assets/_Project/Audio/Music/Theme.mp3"

XFADE = 0.4               # crossfade between clips
AMBIENCE = 0.45           # Kling's own clip sound, under everything
MUSIC_GAIN = 0.16
FADE_OUT = 0.6
END_CARD_SECONDS = 4.5
END_CARD_XFADE = 0.8
APP_STORE_BADGE = HERE / "assets" / "app-store-badge.png"
PLAY_BADGE = HERE / "assets" / "google-play-badge.png"

# The story-intro clip (Kling, 2026-10-15): Cluck and Bessie on the farm at night, thumbs up. Reused as the
# closing shot so the heroes are actually seen, instead of generating a new clip. Has no audio track.
HEROES_CLIP = VIDEO_DIR / "kling_20261015_VIDEO___Style_lo_5296_0.mp4"

# Each shot: clip file (in CLIP_DIR, or "path" for one elsewhere), seconds cut off its start, seconds used after
# that, extra seconds to hold the last frame, and voice lines: (file stem, caption, seconds after the shot starts),
# optionally with (from, to) seconds to use only part of the recording.
SHOTS = [
    # Cold open (2026-10-19): a flash-forward to the Combine's eye and the end of its line, so the first two
    # seconds hook the viewer before the slow farm intro.
    {"file": "ep01_C_eye.mp4", "skip": 3.6, "use": 2.4, "lines": [
        ("robot_combine_1.4", "Every. Single. One.", 0.05, (4.6, 6.9))]},
    {"file": "ep01_A_farm.mp4", "use": 14.0, "lines": [
        ("cluck_1.1", "It was harvest night. The corn was golden, the carrots were crunchy, "
                      "and everybody was fast asleep…", 0.6),
        ("bessie_1.1", "…except one chicken who'd had too much corn before bed.", 9.2)]},
    {"file": "ep01_B_landing.mp4", "use": 10.0, "lines": [
        ("robot_combine_1.4", "Target: farm. Instruction: collect every crop. Every. Single. One.", 7.0)]},
    {"file": "ep01_C_eye.mp4", "skip": 1.4, "use": 4.6, "lines": []},
    # The clip opens on the scarecrow shot again (it was started from B's last frame) and only cuts to the hatch at
    # ~1.9s; starting there avoids a jump back after the eye close-up. The last frame is held to fit Bessie's line.
    {"file": "ep01_D_robots.mp4", "skip": 1.9, "use": 3.1, "hold": 1.3, "lines": [
        ("cluck_1.5_a", "And out came… them.", 0.6),
        ("bessie_1.5", "The Harvest Robots.", 3.3)]},
    {"file": "ep01_E_maze.mp4", "use": 8.0, "hold": 3.0, "lines": [
        ("bessie_1.6", "In one night they turned our fields into mazes. Harvest corridors, they called them.", 1.2),
        ("cluck_1.6_a", "Rude, is what I called them.", 7.6)]},
    {"path": HEROES_CLIP, "skip": 5.5, "use": 4.5, "lines": []},
]


def _centered(draw, text, font, y, fill, stroke):
    draw.text(((W - draw.textlength(text, font=font)) / 2, y), text, font=font, fill=fill,
              stroke_width=stroke, stroke_fill=STROKE_FILL)


def _badge(path, width):
    """Badge scaled to a width, with Google's built-in clear-space padding trimmed so both badges
    line up by their visible edges."""
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getchannel("A").getbbox())
    return im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)


def has_audio(ffmpeg, clip):
    probe = Path(ffmpeg).with_name("ffprobe.exe" if ffmpeg.lower().endswith(".exe") else "ffprobe")
    out = subprocess.run([str(probe), "-v", "error", "-select_streams", "a", "-show_entries", "stream=index",
                          "-of", "csv=p=0", str(clip)], capture_output=True, text=True).stdout
    return bool(out.strip())


def build_end_card(path, play_only=False):
    """Blurred, darkened game poster; logo, 'play free', both store badges, the website."""
    card = Image.open(POSTER).convert("RGBA")
    scale = max(W / card.width, H / card.height)
    card = card.resize((round(card.width * scale), round(card.height * scale)), Image.LANCZOS)
    left, top = (card.width - W) // 2, (card.height - H) // 2
    card = card.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(40))
    card = Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 155)))
    draw = ImageDraw.Draw(card)

    logo_size = 640
    logo = Image.open(LOGO).convert("RGBA").resize((logo_size, logo_size), Image.LANCZOS)
    card.alpha_composite(logo, ((W - logo_size) // 2, 230))
    _centered(draw, "PLAY FREE NOW!", ImageFont.truetype(str(FONT), 124), 930, TEXT_FILL, 8)

    badge_w = 600
    y = 1130
    for badge_path in ((PLAY_BADGE,) if play_only else (APP_STORE_BADGE, PLAY_BADGE)):
        badge = _badge(badge_path, badge_w)
        card.alpha_composite(badge, ((W - badge_w) // 2, y))
        y += badge.height + 50
    _centered(draw, "FARMFURYGAMES.COM", ImageFont.truetype(str(FONT), 80), y + 40, ACCENT_FILL, 6)
    card.convert("RGB").save(path)


def main():
    play_only = "--play-only" in sys.argv[1:]
    out_path = OUT_PLAY_ONLY if play_only else OUT
    ffmpeg = find_ffmpeg()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        inputs, vgraph, agraph = [], [], []
        starts, t = [], 0.0
        for i, shot in enumerate(SHOTS):
            clip = Path(shot["path"]) if "path" in shot else CLIP_DIR / shot["file"]
            if not clip.exists():
                raise FileNotFoundError(clip)
            inputs += ["-i", str(clip)]
            skip = shot.get("skip", 0.0)
            dur = shot["use"] + shot.get("hold", 0.0)
            vgraph.append(f"[{i}:v]trim={skip}:{skip + shot['use']},setpts=PTS-STARTPTS,fps={FPS},"
                          f"scale={W}:{H}:flags=lanczos,setsar=1,"
                          f"tpad=stop_mode=clone:stop_duration={shot.get('hold', 0.0)}[v{i}]")
            if has_audio(ffmpeg, clip):
                agraph.append(f"[{i}:a]atrim={skip}:{skip + shot['use']},asetpts=PTS-STARTPTS,"
                              "aformat=sample_rates=48000:channel_layouts=stereo,"
                              f"apad=whole_dur={dur}[k{i}]")
            else:
                agraph.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{dur}[k{i}]")
            starts.append(t)
            t += dur - XFADE
        clips_end = t + XFADE

        # End card: a still with a slow push-in, crossfaded in after the last clip.
        end_png = work / "end_card.png"
        build_end_card(end_png, play_only)
        ei = len(SHOTS)
        inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{END_CARD_SECONDS:.2f}", "-i", str(end_png)]
        frames = int(END_CARD_SECONDS * FPS)
        vgraph.append(f"[{ei}:v]scale={W * 2}:{H * 2},zoompan=z='1+0.04*on/{frames}':x='iw/2-(iw/zoom/2)':"
                      f"y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS},setsar=1[vend]")
        end_start = clips_end - END_CARD_XFADE
        total = end_start + END_CARD_SECONDS

        # Join with crossfades.
        cur, acur = "v0", "k0"
        for i in range(1, len(SHOTS)):
            off = starts[i]
            vgraph.append(f"[{cur}][v{i}]xfade=transition=fade:duration={XFADE}:offset={off:.2f}[x{i}]")
            agraph.append(f"[{acur}][k{i}]acrossfade=d={XFADE}[ka{i}]")
            cur, acur = f"x{i}", f"ka{i}"
        vgraph.append(f"[{cur}][vend]xfade=transition=fade:duration={END_CARD_XFADE}:offset={end_start:.2f}[xend]")
        cur = "xend"

        # Voices and captions at absolute times.
        voice_labels = []
        for si, shot in enumerate(SHOTS):
            for li, (stem, text, at, *part) in enumerate(shot["lines"]):
                path, d = prepare_voice(ffmpeg, stem, work)
                if part:
                    lo, hi = part[0]
                    cut = work / f"{stem}_{si}_{li}_part.wav"
                    run([ffmpeg, "-v", "error", "-y", "-i", str(path), "-af",
                         f"atrim={lo}:{hi},asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st={hi - lo - 0.08:.2f}:d=0.08",
                         str(cut)])
                    path, d = cut, hi - lo
                a = starts[si] + at
                cap = work / f"{stem}_{si}_{li}_cap.png"
                build_caption(stem, text, cap)
                vi = inputs.count("-i")
                inputs += ["-i", str(cap)]
                vgraph.append(f"[{cur}][{vi}:v]overlay=0:0:enable='between(t,{a:.2f},{a + d + 0.25:.2f})'[o{vi}]")
                cur = f"o{vi}"
                ai = inputs.count("-i")
                inputs += ["-i", str(path)]
                ms = int(a * 1000)
                agraph.append(f"[{ai}:a]adelay={ms}|{ms}[vo{ai}]")
                voice_labels.append(f"[vo{ai}]")
                print(f"  {a:5.1f}-{a + d:5.1f}s  {stem}")

        mi = inputs.count("-i")
        inputs += ["-stream_loop", "-1", "-i", str(MUSIC)]
        agraph.append("".join(voice_labels) + f"amix=inputs={len(voice_labels)}:normalize=0,apad,"
                      f"atrim=0:{total:.2f},asplit=3[vox][key][key2]")
        agraph.append(f"[{mi}:a]aformat=sample_rates=48000:channel_layouts=stereo,loudnorm=I=-14:TP=-1.5:LRA=11,"
                      f"volume={MUSIC_GAIN},afade=t=in:d=1,atrim=0:{total:.2f}[mus]")
        agraph.append(f"[{acur}]volume={AMBIENCE},apad,afade=t=out:st={end_start:.2f}:d={END_CARD_XFADE},atrim=0:{total:.2f}[amb]")
        agraph.append("[mus][key]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[mbed]")
        agraph.append("[amb][key2]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[abed]")
        agraph.append("[mbed][abed][vox]amix=inputs=3:normalize=0:duration=first,"
                      f"loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT}[a]")
        vgraph.append(f"[{cur}]fade=t=in:d=0.4,fade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT},format=yuv420p[v]")

        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(vgraph + agraph),
             "-map", "[v]", "-map", "[a]", "-t", f"{total:.2f}", *VIDEO_OUT, *AUDIO_OUT,
             "-movflags", "+faststart", str(out_path)])
    print(f"{out_path}  {total:.1f}s")


if __name__ == "__main__":
    main()
