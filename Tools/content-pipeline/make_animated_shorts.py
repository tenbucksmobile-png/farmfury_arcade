"""Turn Kling AI character clips into vertical 9:16 shorts (1080x1920), same look as make_shorts.py:
logo intro, headline at the top, the clip in the middle, "FREE ON GOOGLE PLAY" below, end card.

Kling clips are square, silent, and open on the reference sprite against black before the scene
fades in; each entry's `skip` cuts that off. Music comes from the game's own soundtrack.

Output goes to sessions/<--session>/shorts/animated_<slug>.mp4 (+ .json with the caption), so
plan_posts.py picks them up like any other short.

Usage:
    python make_animated_shorts.py
    python make_animated_shorts.py --only groundslam
"""

import argparse
import json
import subprocess
import tempfile
from pathlib import Path

from make_shorts import (AUDIO_OUT, FPS, H, PICTURE_CENTER_Y, REPO, VIDEO_OUT, W, Builder,
                         build_overlay, check_wording, video_duration)
from normalize_video import find_ffmpeg

HERE = Path(__file__).resolve().parent
KLING_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
MUSIC = REPO / "Assets/_Project/Audio/Music"
MUSIC_FADE_IN_SECONDS = 0.5
MUSIC_FADE_OUT_SECONDS = 3.0   # the whole end card fades out
PICTURE_SIZE = 880  # square clip; fits between the headline box (ends y=520) and the CTA (starts y=1420)

# skip: seconds of black start-frame to cut. caption: TikTok caption body (CTA + hashtags are added
# by plan_posts.py). Same messaging rule as everywhere: robots slip/scatter, never anything harsher.
CLIPS = [
    {"file": "ClucksVictoryDance.mp4", "slug": "cluck_victory_dance", "skip": 1.0,
     "hook": "CLUCK'S VICTORY DANCE!", "music": "Theme.mp3",
     "caption": "Cluck after collecting every last crop 🐔🌽 Rate her victory dance from 1 to 10!"},
    {"file": "EggDropDodge.mp4", "slug": "cluck_egg_drop", "skip": 0.85,
     "hook": "CLUCK'S EGG DROP: SLIP AND SLIDE!", "music": "Theme.mp3",
     "caption": "Robot on your tail? Drop an egg and let it slip 🥚🤖 Cluck's Egg Drop in action!"},
    {"file": "GroundSlam.mp4", "slug": "bessie_ground_slam", "skip": 1.45,
     "hook": "BESSIE'S GROUND SLAM!", "music": "Theme.mp3",
     "caption": "One stomp from Bessie and the whole veg patch jumps 🐄💥🥕 Who else wants a turn?"},
    {"file": "BessieBellBoogie.mp4", "slug": "bessie_bell_boogie", "skip": 0.8,
     "hook": "BESSIE'S BELL BOOGIE!", "music": "Theme.mp3",
     "caption": "Bessie has moves 🐄🔔 Field cleared, time to dance!"},
]


def render_square_clip(ffmpeg, out, video, start, duration, overlay, music):
    """The square clip centred on a blurred copy of itself, headline/CTA overlay on top, music under."""
    inputs = ["-ss", f"{start:.2f}", "-t", f"{duration:.2f}", "-i", str(video),
              "-loop", "1", "-framerate", str(FPS), "-t", f"{duration:.2f}", "-i", str(overlay),
              "-t", f"{duration:.2f}", "-i", str(music)]
    filters = (
        "[0:v]setsar=1,split[a][b];"
        f"[a]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,eq=brightness=-0.18[bg];"
        f"[b]scale={PICTURE_SIZE}:{PICTURE_SIZE}[fg];"
        f"[bg][fg]overlay=(W-w)/2:{PICTURE_CENTER_Y}-h/2,fps={FPS}[pic];"
        "[pic][1:v]overlay=0:0,setsar=1,format=yuv420p[v];"
        "[2:a]aformat=sample_rates=48000:channel_layouts=stereo,apad[a]"
    )
    cmd = [ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", filters,
           "-map", "[v]", "-map", "[a]", "-t", f"{duration:.2f}", *VIDEO_OUT, *AUDIO_OUT, str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed rendering {out.name}")


def replace_music(ffmpeg, short, music, work):
    """Swap the joined short's audio for one track played straight through, so the music doesn't
    restart at each segment (intro / clip / end card), and fade it out over the last few seconds.
    The Kling clips have no sound of their own, so nothing else is lost."""
    total = video_duration(ffmpeg, short)
    tmp = work / f"music_{short.name}"
    fade_in = f"afade=t=in:st=0:d={MUSIC_FADE_IN_SECONDS}"
    fade_out = f"afade=t=out:st={total - MUSIC_FADE_OUT_SECONDS:.2f}:d={MUSIC_FADE_OUT_SECONDS}"
    cmd = [ffmpeg, "-v", "error", "-y", "-i", str(short), "-stream_loop", "-1", "-i", str(music),
           "-map", "0:v", "-map", "1:a", "-t", f"{total:.2f}", "-c:v", "copy",
           "-af", f"aformat=sample_rates=48000:channel_layouts=stereo,loudnorm=I=-14:TP=-1.5:LRA=11,{fade_in},{fade_out}",
           *AUDIO_OUT, "-movflags", "+faststart", str(tmp)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed replacing music on {short.name}")
    tmp.replace(short)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--session", default="kling-20261006", help="folder name under sessions/")
    parser.add_argument("--only", help="render only clips whose slug contains this text")
    args = parser.parse_args()

    ffmpeg = find_ffmpeg()
    out_dir = HERE / "sessions" / args.session / "shorts"
    out_dir.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as tmp:
        b = Builder(ffmpeg, None, out_dir, Path(tmp))
        for clip in CLIPS:
            if args.only and args.only not in clip["slug"]:
                continue
            check_wording(clip["caption"])
            video = KLING_DIR / clip["file"]
            duration = video_duration(ffmpeg, video) - clip["skip"]
            overlay = b._temp(".png")
            build_overlay(clip["hook"], overlay)
            body = b._temp(".mp4")
            render_square_clip(ffmpeg, body, video, clip["skip"], duration, overlay, MUSIC / clip["music"])
            name = f"animated_{clip['slug']}"
            b.finish(name, [b.opener(clip["hook"]), (body, duration)],
                     {"hook": clip["hook"], "source": str(video), "caption_body": clip["caption"]})
            replace_music(ffmpeg, out_dir / f"{name}.mp4", MUSIC / clip["music"], Path(tmp))

    print(f"Shorts written to {out_dir}")


if __name__ == "__main__":
    main()
