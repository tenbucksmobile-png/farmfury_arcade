"""Convert a session's raw scrcpy recording into a clean, easy-to-cut MP4.

scrcpy's raw .mkv can change frame size mid-file (if the phone rotated while
recording) and has very few keyframes, so jumping to a time in it decodes to
garbage. This re-encodes it once, from the start, to a fixed landscape size
with a keyframe every second. Timestamps are kept, so markers still line up.

Usage:
    python normalize_video.py sessions/20260924-170129
    python normalize_video.py            # newest session
"""

import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def find_ffmpeg(name="ffmpeg"):
    bundled = sorted(HERE.glob(f"bin/ffmpeg-*/bin/{name}.exe"))
    if bundled:
        return str(bundled[-1])
    sys.exit(f"{name} not found. Unzip the gyan.dev ffmpeg essentials build into Tools/content-pipeline/bin/.")


def landscape_size(session):
    """Width x height of the phone screen held sideways, from `adb shell wm size`."""
    w, h = session.get("screen_size", [1600, 720])
    return max(w, h), min(w, h)


def normalize(session_dir):
    session_path = session_dir / "session.json"
    session = json.loads(session_path.read_text(encoding="utf-8"))
    raw = session_dir / session.get("raw_video", session["video"])
    out = session_dir / "video.mp4"
    width, height = landscape_size(session)
    # Even dimensions are required by libx264.
    width, height = width - width % 2, height - height % 2

    print(f"Converting {raw.name} -> {out.name} ({width}x{height}, keyframe every second)...")
    cmd = [
        find_ffmpeg(), "-v", "error", "-y",
        "-i", str(raw),
        "-vf", f"scale={width}:{height}",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
        "-force_key_frames", "expr:gte(t,n_forced*1)",
        "-c:a", "aac", "-b:a", "160k",
        "-movflags", "+faststart",
        str(out),
    ]
    result = subprocess.run(cmd)
    if result.returncode != 0 or not out.exists():
        sys.exit("ffmpeg failed; the raw recording is still in the session folder.")

    session["raw_video"] = raw.name
    session["video"] = out.name
    session_path.write_text(json.dumps(session, indent=2), encoding="utf-8")
    print(f"Done: {out}")


def main():
    if len(sys.argv) > 1:
        session_dir = Path(sys.argv[1]).resolve()
    else:
        sessions = sorted((HERE / "sessions").glob("*/session.json"))
        if not sessions:
            sys.exit("No sessions found.")
        session_dir = sessions[-1].parent
    normalize(session_dir)


if __name__ == "__main__":
    main()
