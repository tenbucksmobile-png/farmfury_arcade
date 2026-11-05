"""The gameplay-short template (Nov 2026): the storyline opener, the world badge, ~10 s of gameplay,
the Arcade end card.

  1. opener      every animal shouts "Farm Fury!" over the all-characters line-up; FARM FURY, Arcade and
                 Stampede logos appear (storyline/make_episode.py build_opener, same as the episodes)
  2. world badge the world's Level Select sign pops in over its own blurred backdrop; its music is the
                 same world track the gameplay plays, lined up so it runs straight into it (and on
                 under the end card). Pick a window without a power crop: that switches the music.
  3. gameplay    a window of a recorded session, centre crop of the maze, headline on top, small
                 "free on App Store & Google Play" bar at the bottom (make_shorts.py layout)
  4. end card    Arcade logo, PLAY FREE NOW!, both store badges, website, "Farm Fury!" chorus

    python make_gameplay_short.py sessions/20261001-150832 --start 446.5 --seconds 10.5 \
        --world 0 --hook "DODGE THE ROBOTS!" --name cornfield_test

Output: <session>/shorts_v3/<name>.mp4 (+ .json for plan_posts/plan_launch).
"""

import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

import numpy as np

import make_shorts as ms
from normalize_video import find_ffmpeg

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "storyline"))
import make_episode  # noqa: E402

REPO = HERE.parent.parent
UI = REPO / "Assets/_Project/Sprites/UI"
MUSIC = REPO / "Assets/_Project/Audio/Music"
SFX = REPO / "Assets/_Project/Audio/SFX"

# world index -> (badge, backdrop, music, label). Purchased worlds: add their shield art when needed.
WORLDS = {
    0: ("CornfieldSign.png", "World1_Cornfield.png", "CornField.mp3", "WORLD 1"),
    1: ("VegetablePatchSign.png", None, "VegetablePatch.mp3", "WORLD 2"),
    2: ("OrchardSign.png", None, "Orchard.mp3", "WORLD 3"),
    3: ("WheatfieldSign.png", None, "Wheatfield.mp3", "WORLD 4"),
}
BADGE_SECONDS = 1.8
ALIGN_SR = 4000
BADGE_W = 820
W, H, FPS = ms.W, ms.H, ms.FPS


def ease_out_back(x):
    x = max(0.0, min(1.0, x))
    c = 1.7
    return 1 + (c + 1) * (x - 1) ** 3 + c * (x - 1) ** 2


def decode_mono(ffmpeg, args):
    raw = subprocess.run([ffmpeg, "-v", "error", *args, "-ac", "1", "-ar", str(ALIGN_SR), "-f", "f32le", "-"],
                         capture_output=True).stdout
    return np.frombuffer(raw, np.float32)


def find_music_position(ffmpeg, video, start, track):
    """Where the world track is at `start` seconds into the recording (it loops in the game), and how loud
    it is there relative to the track file, by cross-correlating 6 s of the recording with the track.
    None if it can't be found (e.g. the power-crop music is playing instead)."""
    rec = decode_mono(ffmpeg, ["-ss", f"{start:.2f}", "-t", "6", "-i", str(video)])
    mus = decode_mono(ffmpeg, ["-i", str(track)])
    rec = rec - rec.mean()
    looped = np.concatenate([mus, mus[:len(rec)]])
    n = 1 << int(np.ceil(np.log2(len(looped) + len(rec))))
    corr = np.fft.irfft(np.fft.rfft(looped, n) * np.conj(np.fft.rfft(rec, n)), n)[:len(mus)]
    energy = np.sqrt(np.convolve(looped ** 2, np.ones(len(rec)), "valid")[:len(mus)]) + 1e-9
    score = corr / (energy * np.linalg.norm(rec))
    k = int(np.argmax(score))
    if score[k] < 6 * np.median(np.abs(score)):
        return None
    ref = looped[k:k + len(rec)]
    gain = float(np.dot(rec, ref) / np.dot(ref, ref))
    return k / ALIGN_SR, len(mus) / ALIGN_SR, gain


def render_badge(ffmpeg, world, out, work, music_pos=None):
    badge_file, backdrop_file, music_file, label = WORLDS[world]
    backdrop = Image.open(UI / (backdrop_file or "Bg_LevelSelect.png")).convert("RGB")
    s = max(W / backdrop.width, H / backdrop.height)
    backdrop = backdrop.resize((round(backdrop.width * s), round(backdrop.height * s)), Image.LANCZOS)
    l, t = (backdrop.width - W) // 2, (backdrop.height - H) // 2
    bg = backdrop.crop((l, t, l + W, t + H)).filter(ImageFilter.GaussianBlur(14)).convert("RGBA")
    bg = Image.alpha_composite(bg, Image.new("RGBA", (W, H), (0, 0, 0, 90)))
    badge = Image.open(UI / badge_file).convert("RGBA")
    badge = badge.resize((BADGE_W, round(BADGE_W * badge.height / badge.width)), Image.LANCZOS)

    frames = work / "badge_frames"
    frames.mkdir()
    for i in range(int(BADGE_SECONDS * FPS)):
        t = i / FPS
        zoom = 1.0 + 0.05 * t / BADGE_SECONDS            # slow push on the backdrop
        cw, ch = W / zoom, H / zoom
        frame = bg.crop((int((W - cw) / 2), int((H - ch) / 2), int((W + cw) / 2), int((H + ch) / 2)))
        frame = frame.resize((W, H), Image.BILINEAR)
        pop = 0.55 + 0.45 * ease_out_back(t / 0.35)
        alpha = min(1.0, t / 0.12, (BADGE_SECONDS - t) / 0.15)
        b = badge.resize((max(1, round(badge.width * pop)), max(1, round(badge.height * pop))), Image.BILINEAR)
        if alpha < 1:
            b.putalpha(b.getchannel("A").point(lambda a: int(a * alpha)))
        frame.alpha_composite(b, (round(W / 2 - b.width / 2), round(900 - b.height / 2)))
        frame.convert("RGB").save(frames / f"{i:04d}.png")

    # The badge plays the same world track the gameplay is about to play, timed so it runs straight into it.
    if music_pos:
        pos, length, gain = music_pos
        music_in = ["-stream_loop", "-1", "-ss", f"{(pos - BADGE_SECONDS) % length:.3f}"]
        vol = gain                     # same level as the recording; the whole body is loudness-normalised after
    else:
        music_in, vol = [], 0.7
    cmd = [ffmpeg, "-v", "error", "-y", "-framerate", str(FPS), "-i", str(frames / "%04d.png"),
           *music_in, "-t", f"{BADGE_SECONDS}", "-i", str(MUSIC / music_file),
           "-i", str(SFX / "RarePellet_pickup.mp3"),
           "-filter_complex",
           f"[1:a]aformat=sample_rates=48000:channel_layouts=stereo,volume={vol:.3f},afade=t=in:d=0.3[m];"
           f"[2:a]aformat=sample_rates=48000:channel_layouts=stereo,adelay=60|60,volume={vol * 3:.4f}[p];"
           f"[m][p]amix=inputs=2:normalize=0,apad=whole_dur={BADGE_SECONDS},atrim=0:{BADGE_SECONDS}[a]",
           "-map", "0:v", "-map", "[a]", "-t", f"{BADGE_SECONDS}", *ms.VIDEO_OUT, *ms.AUDIO_OUT, str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError("ffmpeg failed rendering the world badge")
    return out, BADGE_SECONDS


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("session")
    p.add_argument("--start", type=float, required=True, help="gameplay start, seconds into video.mp4")
    p.add_argument("--seconds", type=float, default=10.0)
    p.add_argument("--world", type=int, default=0)
    p.add_argument("--hook", required=True, help="on-screen headline during gameplay")
    p.add_argument("--name", required=True)
    args = p.parse_args()
    ms.check_wording(args.hook)

    session_dir = Path(args.session).resolve()
    video = session_dir / "video.mp4"
    out_dir = session_dir / "shorts_v3"
    body_dir = out_dir / "body"
    body_dir.mkdir(parents=True, exist_ok=True)
    ms.FRANCHISE = True                      # no fades/end card in the body: the wrapper adds them
    ms.CTA_TITLE, ms.CTA_SUBTITLE = "FARM FURY: ARCADE", "FREE ON APP STORE & GOOGLE PLAY"
    ffmpeg = find_ffmpeg()

    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        b = ms.Builder(ffmpeg, video, body_dir, work)
        track = MUSIC / WORLDS[args.world][2]
        music_pos = find_music_position(ffmpeg, video, args.start, track)
        if music_pos:
            print(f"  world music found in the recording at {music_pos[0]:.2f}s of {track.name} (gain {music_pos[2]:.2f})")
        else:
            print(f"  world music not found at {args.start}s (power-crop music?); badge uses {track.name} from the top")
        badge = render_badge(ffmpeg, args.world, work / "badge.mp4", work, music_pos)
        play = b.gameplay(args.hook, args.start, args.start + args.seconds)
        body = body_dir / f"{args.name}.mp4"
        total = ms.join_segments(ffmpeg, [badge, play], body)
        end_kw = {}
        if music_pos:   # the end card carries the same track on from where the gameplay leaves it
            end_at = BADGE_SECONDS + args.seconds - make_episode.END_CARD_XFADE
            end_kw = {"end_music": track, "end_music_start": (music_pos[0] - BADGE_SECONDS + end_at) % music_pos[1]}
        make_episode.wrap_video(ffmpeg, body, out_dir=out_dir, arcade_only=True, **end_kw)

    info = {"hook": args.hook, "clip": [args.start, args.start + args.seconds], "world": args.world,
            "file": f"{args.name}.mp4", "seconds": round(ms.video_duration(ffmpeg, out_dir / f"{args.name}.mp4"), 1)}
    (out_dir / f"{args.name}.json").write_text(json.dumps(info, indent=2), encoding="utf-8")
    print(f"body {total:.1f}s -> {out_dir / (args.name + '.mp4')}  {info['seconds']}s")


if __name__ == "__main__":
    main()
