"""Record a gameplay session from the Android phone for the shorts pipeline.

Records the phone screen (with game audio) using scrcpy, captures the game's
[Highlight] log lines from logcat, and measures the phone-to-PC clock offset so
the markers can be placed on the video's timeline. Stop with Enter (or Ctrl+C).

Needs: a Development Build of the game installed (markers are compiled out of
release builds), USB debugging on, scrcpy and adb available.

Usage:
    python record_session.py            # record, then parse markers
    python record_session.py --show     # also show the mirror window on the PC
"""

import argparse
import json
import os
import shutil
import signal
import subprocess
import sys
import threading
import time
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
SESSIONS_DIR = HERE / "sessions"
PACKAGE = "com.farmfury.arcade"

ADB_FALLBACKS = [
    Path(os.environ.get("LOCALAPPDATA", "")) / "Android/Sdk/platform-tools/adb.exe",
    Path("C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"),
]


def now_ms():
    return int(time.time() * 1000)


def find_adb():
    found = shutil.which("adb")
    if found:
        return found
    for candidate in ADB_FALLBACKS:
        if candidate.exists():
            return str(candidate)
    sys.exit("adb not found. Install Android platform-tools or put adb on PATH.")


def find_scrcpy():
    """Prefers the portable copy in bin/ (downloaded there because winget isn't available)."""
    bundled = sorted(HERE.glob("bin/scrcpy-*/scrcpy.exe"))
    if bundled:
        return str(bundled[-1])
    found = shutil.which("scrcpy")
    if found:
        return found
    sys.exit("scrcpy not found. Unzip scrcpy-win64 into Tools/content-pipeline/bin/ (see README).")


def run(cmd, **kwargs):
    return subprocess.run(cmd, capture_output=True, text=True, **kwargs)


def check_device(adb):
    out = run([adb, "devices"]).stdout.strip().splitlines()[1:]
    devices = [line.split()[0] for line in out if line.strip().endswith("device")]
    if len(devices) != 1:
        sys.exit(f"Expected exactly one connected device, found {len(devices)}. Check `adb devices`.")
    if PACKAGE not in run([adb, "shell", "pm", "list", "packages", PACKAGE]).stdout:
        print(f"WARNING: {PACKAGE} is not installed on the device.")
    model = run([adb, "shell", "getprop", "ro.product.model"]).stdout.strip()
    return devices[0], model


def screen_size(adb):
    """Physical screen size, rounded down to a multiple of 16 like scrcpy's encoder output."""
    text = run([adb, "shell", "wm", "size"]).stdout
    try:
        w, h = (int(v) for v in text.split(":")[-1].strip().split("x"))
    except ValueError:
        return [1600, 720]
    return [w - w % 16, h - h % 16]


def launch_game(adb):
    """Open the game before recording starts, so scrcpy's first frame is already landscape."""
    run([adb, "shell", "monkey", "-p", PACKAGE, "-c", "android.intent.category.LAUNCHER", "1"])
    time.sleep(4)


def phone_time_ms(adb):
    """Phone Unix time in ms. Tries sub-second sources first."""
    for shell_cmd in ("echo $EPOCHREALTIME", "date +%s.%N"):
        text = run([adb, "shell", shell_cmd]).stdout.strip()
        try:
            value = float(text)
            if "." in text:
                return int(value * 1000)
        except ValueError:
            continue
    return int(run([adb, "shell", "date", "+%s"]).stdout.strip()) * 1000


def measure_clock_offset(adb, samples=7):
    """Returns (phone - PC) in ms, from the sample with the shortest round trip."""
    best = None
    for _ in range(samples):
        t0 = now_ms()
        phone = phone_time_ms(adb)
        t1 = now_ms()
        round_trip = t1 - t0
        offset = phone - (t0 + t1) // 2
        if best is None or round_trip < best[0]:
            best = (round_trip, offset)
    return best[1], best[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--show", action="store_true", help="show the scrcpy mirror window")
    args = parser.parse_args()

    adb = find_adb()
    scrcpy = find_scrcpy()

    serial, model = check_device(adb)
    offset_ms, round_trip_ms = measure_clock_offset(adb)
    print(f"Device: {model} ({serial})  clock offset phone-PC: {offset_ms} ms (round trip {round_trip_ms} ms)")

    session_dir = SESSIONS_DIR / datetime.now().strftime("%Y%m%d-%H%M%S")
    session_dir.mkdir(parents=True)
    video_path = session_dir / "raw.mkv"  # mkv stays playable even if recording is cut off abruptly
    logcat_path = session_dir / "logcat.txt"

    # scrcpy ships its own adb; pointing it at ours avoids two adb servers restarting each other.
    env = dict(os.environ, ADB=adb)
    creation = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0

    # Some phones (seen on the Honor test phone) set persist.log.tag=S, which silences every app
    # log tag that isn't whitelisted - including Unity, so no markers reach logcat. log.tag.Unity
    # overrides that per tag. It resets on reboot, so set it every session.
    run([adb, "shell", "setprop", "log.tag.Unity", "V"])
    print("Opening the game...")
    launch_game(adb)
    run([adb, "logcat", "-c"])
    logcat_file = open(logcat_path, "w", encoding="utf-8", errors="replace")
    logcat = subprocess.Popen([adb, "logcat", "-v", "epoch", "Unity:V", "*:S"],
                              stdout=logcat_file, stderr=subprocess.STDOUT)

    scrcpy_cmd = [
        scrcpy, f"--record={video_path}", "--stay-awake",
        # Lock to the orientation at start (landscape, since the game is already open). Without
        # this, a rotation mid-recording changes the frame size inside the file.
        "--capture-orientation=@",
        # A keyframe every second instead of scrcpy's default 10, so the file can be cut accurately.
        "--video-codec-options=i-frame-interval:int=1",
    ]
    if not args.show:
        scrcpy_cmd.append("--no-window")  # scrcpy 4.x; replaces the older --no-playback
    launch_ms = now_ms()
    recorder = subprocess.Popen(scrcpy_cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, env=env, creationflags=creation)

    recording_started_ms = {"value": None}
    scrcpy_log = []

    def watch_scrcpy():
        for line in recorder.stdout:
            scrcpy_log.append(line.rstrip())
            if recording_started_ms["value"] is None and "Recording started" in line:
                recording_started_ms["value"] = now_ms()

    threading.Thread(target=watch_scrcpy, daemon=True).start()

    print(f"Recording to {session_dir}")
    print("Play the game on the phone (keep it sideways). Press Enter here to stop.")
    try:
        input()
    except KeyboardInterrupt:
        pass

    if recorder.poll() is None:
        if os.name == "nt":
            recorder.send_signal(signal.CTRL_BREAK_EVENT)
        else:
            recorder.send_signal(signal.SIGINT)
        try:
            recorder.wait(timeout=10)
        except subprocess.TimeoutExpired:
            recorder.kill()
    logcat.terminate()
    logcat.wait(timeout=5)
    logcat_file.close()

    start_ms = recording_started_ms["value"]
    if start_ms is None:
        print("WARNING: scrcpy never printed 'Recording started'; using its launch time. Markers may be off by ~1s.")
        start_ms = launch_ms

    (session_dir / "scrcpy.log").write_text("\n".join(scrcpy_log), encoding="utf-8")
    session = {
        "device_model": model,
        "video": video_path.name,
        "screen_size": screen_size(adb),
        "logcat": logcat_path.name,
        "recording_start_pc_ms": start_ms,
        "clock_offset_ms": offset_ms,
        "clock_round_trip_ms": round_trip_ms,
    }
    (session_dir / "session.json").write_text(json.dumps(session, indent=2), encoding="utf-8")

    if not video_path.exists():
        print("ERROR: no video was written. See scrcpy.log in the session folder.")
        return

    print("Recording saved.")
    subprocess.run([sys.executable, str(HERE / "normalize_video.py"), str(session_dir)])
    print("Parsing markers...")
    subprocess.run([sys.executable, str(HERE / "parse_markers.py"), str(session_dir)])


if __name__ == "__main__":
    main()
