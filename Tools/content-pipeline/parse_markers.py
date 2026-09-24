"""Turn a recorded session's [Highlight] log lines into ranked clip candidates.

Reads session.json + logcat.txt from a session folder (written by
record_session.py) and writes:
  markers.json  every marker, with its time in seconds from the start of the video
  clips.json    candidate clips (start/end seconds), best first

Usage:
    python parse_markers.py sessions/20260924-153000
    python parse_markers.py                # newest session
"""

import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent

LINE = re.compile(r"\[Highlight\] type=(\S+) ms=(\d+)(.*)")
DETAIL = re.compile(r"(\w+)=(\S+)")

# How interesting each moment is, and how much video to keep before/after it (seconds).
# Tune these after watching a few sessions.
WEIGHTS = {
    # The game is about dodging robots and collecting every crop, so those moments rank highest.
    "near_miss":        (9, 4.0, 3.0),
    "level_complete":   (8, 6.0, 3.0),
    "world_unlock":     (7, 1.0, 5.0),
    "character_unlock": (6, 1.0, 5.0),
    "combo":            (6, 4.0, 4.0),
    "power_pellet":     (5, 2.0, 4.0),
    "full_chain":       (3, 5.0, 3.0),
    "revive":           (2, 2.0, 3.0),
    "robot_defeated":   (1, 3.0, 2.0),
    "ability":          (1, 1.0, 3.0),
}
# Context only: never the reason for a clip.
CONTEXT_TYPES = {"level_start", "level_failed", "player_death"}

NEAR_MISS_SURVIVE_SECONDS = 2.0   # a near miss followed by a death this soon wasn't a near miss
NEAR_MISS_RESOLVED_SECONDS = 1.5  # ...nor one where an ability/robot defeat happened this close
MAX_CLIP_SECONDS = 25.0
MIN_CLIP_SECONDS = 6.0
DEATH_PENALTY = 4                 # clips showing a death rank lower (still kept, sometimes they're funny)


def load_markers(session_dir):
    session = json.loads((session_dir / "session.json").read_text(encoding="utf-8"))
    start_pc_ms = session["recording_start_pc_ms"]
    offset_ms = session["clock_offset_ms"]

    markers = []
    for raw in (session_dir / session["logcat"]).read_text(encoding="utf-8", errors="replace").splitlines():
        match = LINE.search(raw)
        if not match:
            continue
        kind, phone_ms, rest = match.group(1), int(match.group(2)), match.group(3)
        t = (phone_ms - offset_ms - start_pc_ms) / 1000.0
        if t < 0:
            continue  # happened before the recording started
        # Builds made before the invariant-culture fix wrote decimals with the phone's locale ("1,39").
        details = {k: re.sub(r"^(-?\d+),(\d+)$", r"\1.\2", v) for k, v in DETAIL.findall(rest)}
        markers.append({"t": round(t, 2), "type": kind, **details})
    markers.sort(key=lambda m: m["t"])
    return markers


def drop_failed_near_misses(markers):
    """A near miss only counts as a dodge if the player got away cleanly: no death shortly after,
    and no ability used / robot beaten around it (on the first recording, every "near miss" was
    Bessie's Ground Slam taking the robot out, which isn't a dodge)."""
    deaths = [m["t"] for m in markers if m["type"] == "player_death"]
    resolved = [m["t"] for m in markers if m["type"] in ("ability", "robot_defeated")]
    kept = []
    for m in markers:
        if m["type"] == "near_miss":
            if any(0 <= d - m["t"] <= NEAR_MISS_SURVIVE_SECONDS for d in deaths):
                continue
            if any(abs(r - m["t"]) <= NEAR_MISS_RESOLVED_SECONDS for r in resolved):
                continue
        kept.append(m)
    return kept


def score(marker):
    weight = WEIGHTS[marker["type"]][0]
    if marker["type"] == "near_miss":
        # The closer the robot got, the better the dodge.
        cells = float(marker.get("cells", 1.4))
        weight += 3 if cells < 1.0 else (1 if cells < 1.2 else 0)
    if marker["type"] == "level_complete":
        weight += int(marker.get("stars", 0))
        if marker.get("deaths") == "0":
            weight += 2   # cleared the farm without getting caught
    return weight


def build_clips(markers):
    windows = []
    for m in markers:
        if m["type"] not in WEIGHTS:
            continue
        _, pre, post = WEIGHTS[m["type"]]
        windows.append({"start": max(0.0, m["t"] - pre), "end": m["t"] + post, "score": score(m), "moments": [m]})

    # Merge overlapping windows so one exciting stretch becomes one clip, not five.
    windows.sort(key=lambda w: w["start"])
    merged = []
    for w in windows:
        if merged and w["start"] <= merged[-1]["end"] and w["end"] - merged[-1]["start"] <= MAX_CLIP_SECONDS:
            last = merged[-1]
            last["end"] = max(last["end"], w["end"])
            last["score"] += w["score"]
            last["moments"].extend(w["moments"])
        else:
            merged.append(dict(w, moments=list(w["moments"])))

    deaths = [m["t"] for m in markers if m["type"] == "player_death"]
    clips = []
    for c in merged:
        length = c["end"] - c["start"]
        if length < MIN_CLIP_SECONDS:
            pad = (MIN_CLIP_SECONDS - length) / 2
            c["start"], c["end"] = max(0.0, c["start"] - pad), c["end"] + pad
        c["has_death"] = any(c["start"] <= d <= c["end"] for d in deaths)
        if c["has_death"]:
            c["score"] -= DEATH_PENALTY
        c["start"], c["end"] = round(c["start"], 2), round(c["end"], 2)
        c["summary"] = ", ".join(sorted({m["type"] for m in c["moments"]}))
        clips.append(c)
    clips.sort(key=lambda c: -c["score"])
    return clips


def main():
    if len(sys.argv) > 1:
        session_dir = Path(sys.argv[1]).resolve()
    else:
        sessions = sorted((HERE / "sessions").glob("*/session.json"))
        if not sessions:
            sys.exit("No sessions found. Run record_session.py first.")
        session_dir = sessions[-1].parent

    markers = load_markers(session_dir)
    if not markers:
        print("No [Highlight] markers found. Is the phone running a Development Build?")
        print(f"Check {session_dir / 'logcat.txt'} for Unity log lines.")
        return

    markers = drop_failed_near_misses(markers)
    clips = build_clips(markers)
    (session_dir / "markers.json").write_text(json.dumps(markers, indent=2), encoding="utf-8")
    (session_dir / "clips.json").write_text(json.dumps(clips, indent=2), encoding="utf-8")

    counts = {}
    for m in markers:
        counts[m["type"]] = counts.get(m["type"], 0) + 1
    print(f"{len(markers)} markers: " + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
    print(f"{len(clips)} candidate clips (best first):")
    for i, c in enumerate(clips[:15], 1):
        flag = "  [death]" if c["has_death"] else ""
        print(f"  {i:2}. {c['start']:7.1f}s - {c['end']:7.1f}s  score {c['score']:3}  {c['summary']}{flag}")
    print(f"Written: {session_dir / 'markers.json'}, {session_dir / 'clips.json'}")


if __name__ == "__main__":
    main()
