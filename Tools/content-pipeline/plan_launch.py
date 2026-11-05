"""Launch sheet for every marketing video (Nov 2026): storyline Eps 1-9, the six storyline singles and the
numbered gameplay shorts (Episodes/singles/<World>/<World> NN - <title>.mp4).

The pattern agreed 2026-11-05 (revised):
  every morning 07:30       the next gameplay short, world by world in number order
                            (Corn Field 01-10, then Vegetable Patch 01-10, ...)
  Tuesday + Saturday 16:30  the next episode
  six other afternoons      a story single, next to the episode it belongs with
Runs until Ep 9 and the last single are out. After that the mornings carry on through the worlds and the
Tuesday/Saturday afternoons are free for Chapter 2 (Stampede).

A gameplay short is picked up automatically once its file exists in singles/<World>/; until then its slot
shows as TO RECORD. Re-run after rendering new ones (the dates don't move).

    python plan_launch.py                      # starts Sat 2026-11-07
    python plan_launch.py --start 2026-11-10

Writes plans/launch-<start>.html. Doesn't touch ledger.json.
"""

import argparse
import datetime as dt
import html
from pathlib import Path

from make_shorts import check_wording
from plan_posts import APP_STORE_LINK, PLANS, SHEET_STYLE, TIKTOK_HANDLE, YOUTUBE_SHORTS_LINK, YOUTUBE_STEPS

VIDEO = Path.home() / "Desktop/FarmFury_Technical/FarmFury_Artwork/Video/Episodes"
SINGLES = VIDEO / "singles"
SERIES = "Farm Fury: The Story"
CTA = "Play Farm Fury free on the App Store and Google Play, link in bio."
ARCADE_CTA = "Farm Fury: Arcade is free on the App Store and Google Play, link in bio."

# Robots get dodged, outrun, busted or powered down - never anything harsher (check_wording enforces it).
EPISODES = {
    1: ("ep01_the_night_the_robots_came.mp4", "The Night the Robots Came",
        "One quiet night, the Harvest Robots landed on the farm and took every crop 🌽🤖 Who's going to stop them?"),
    2: ("ep02_chaos_on_the_farm.mp4", "Chaos on the Farm",
        "The morning after: eight farm animals, zero crops and a LOT of panic 🐔🐄🐷"),
    3: ("ep03_the_debrief.mp4", "The Debrief",
        "Live from the barn: Barnyard Breakdown replays the robot landing frame by frame 📺"),
    4: ("ep04_the_barn_meeting.mp4", "The Barn Meeting",
        "Ring the bell! All eight animals meet in the barn... and Bessie knows a secret 🔔"),
    5: ("ep05_the_golden_glow.mp4", "The Golden Glow",
        "A golden sunflower gives every animal a special power ✨🌻 Which power is your favourite?"),
    6: ("ep06_meet_the_metal.mp4", "Meet the Metal",
        "WANTED: six Harvest Robots, each with its own trick 🤖 Which one would you dodge first?"),
    7: ("ep07_operation_harvest_home.mp4", "Operation Harvest Home",
        "The plan: get into the maze, collect every crop, dodge every robot 🗺️ Ready?"),
    8: ("ep08_into_the_mazes.mp4", "Into the Mazes",
        "Seven worlds of mazes, from the Corn Field to Harvest Moon 🌽🍎🌾 Which one looks hardest?"),
    9: ("ep09_combo_chaos.mp4", "Combo Chaos",
        "Tag-team two animals in the right order and you get a COMBO 🔁💥 Which pair would you pick?"),
}
EP_TAGS = ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#mobilegame"]
EPISODE_WEEKDAYS = (1, 5)      # Tuesday, Saturday

# Storyline singles: key -> (file in singles/, title, caption body)
STORY = {
    "robots": ("robot_celebration.mp4", "The Robots Are Celebrating",
               "The Harvest Robots think they've won... 🤖🎉 Not for long."),
    "bell": ("bessie_bell_boogie.mp4", "Bessie's Bell Boogie",
             "When Bessie rings the bell, the whole farm dances 🔔🐄"),
    "slam": ("bessie_ground_slam.mp4", "Bessie's Ground Slam",
             "GROUND... SLAM! 💥🐄 Bessie shakes the whole farm."),
    "egg": ("cluck_egg_drop.mp4", "Cluck's Egg Drop",
            "Cluck drops an egg, and the robot never saw it coming 🥚🤖"),
    "dance": ("cluck_victory_dance.mp4", "Cluck's Victory Dance",
              "Every crop collected? Time for Cluck's victory dance 🐔🕺"),
    "cluckday": ("cluck_celebration.mp4", "Cluck's Big Day",
                 "We did it! Every crop's home safe 🌽🐔"),
}
STORY_TAGS = ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#cute"]
# Afternoon of day N (0 = the first day) -> story single. With a Saturday start the episodes land on days
# 0,3,7,10,14,17,21,24,28; these sit next to the episode each one belongs with.
SINGLE_DAYS = {2: "robots",      # Mon, after Ep 1: the robots gloating
               9: "bell",        # Mon, before Ep 4 "Ring the bell"
               12: "slam",       # Thu, before Ep 5's powers
               16: "egg",        # Mon, after Ep 5
               26: "dance",      # Thu, before the finale
               29: "cluckday"}   # Sun, after Ep 9: "every crop's home safe"

# Gameplay: world by world, numbered 01-10 each. Add hand-written captions as shorts get made; one without
# gets a plain caption from its file title.
WORLD_ORDER = ["Corn Field", "Vegetable Patch", "Orchard", "Wheat Field",
               "Frozen Garden", "Golden Sunset", "Harvest Moon"]
PER_WORLD = 10
CAPTIONS = {
    ("Corn Field", 1): "Cluck leaves an egg behind, and the robot walks right into it 🥚",
    ("Corn Field", 2): "Bessie's Ground Slam powers down every robot nearby 💥🐄",
    ("Corn Field", 3): "Swap animals mid-maze! Percy rolls in 🐷",
    ("Corn Field", 4): "Three Woollys are better than one 🐑🐑🐑",
    ("Corn Field", 5): "Only Ducky can skip across the water 🦆💦",
    ("Corn Field", 6): "That was close! Percy dodges the robots 🐷🤖",
    ("Corn Field", 7): "Stay one step ahead of the robots 🐄🌽",
    ("Corn Field", 8): "Collect every crop to clear the maze 🌽",
    ("Corn Field", 9): "Can you outrun the Harvest Robots? 🤖💨",
    ("Corn Field", 10): "Ducky takes the water shortcut 🦆💦",
}
GP_TAGS = ["#farmfury", "#mobilegame", "#arcadegame", "#mazegame", "#retrogaming"]


def store_links(tags):
    return (f"App Store: {APP_STORE_LINK}\nGoogle Play: {YOUTUBE_SHORTS_LINK}\n\n"
            f"{' '.join(['#shorts'] + tags)}")


def episode(n):
    file, title, body = EPISODES[n]
    note = ("TikTok: AI-generated label on, add to the playlist" + (", PIN this one" if n == 1 else "")
            + f". YouTube: add to the \"{SERIES}\" playlist"
            + (f", set Related video to Ep {n - 1}" if n > 1 else "") + ".")
    return {"kind": f"Episode {n}", "video": VIDEO / file,
            "caption": f"EP {n} | {title}\n{body}\n\n{CTA}\n\n{' '.join(EP_TAGS)}",
            "yt_title": f"Ep {n}: {title} | {SERIES}",
            "yt_desc": f"{body}\n\nEpisode {n} of {SERIES}.\n\nPlay Farm Fury free:\n{store_links(EP_TAGS)}",
            "note": note, "short": f"Ep {n}: {title}"}


def story(key):
    file, title, body = STORY[key]
    return {"kind": "Story single", "video": SINGLES / file,
            "caption": f"{body}\n\n{CTA}\n\n{' '.join(STORY_TAGS)}",
            "yt_title": f"{title} | {SERIES}",
            "yt_desc": f"{body}\n\nFrom {SERIES}.\n\nPlay Farm Fury free:\n{store_links(STORY_TAGS)}",
            "note": "TikTok: AI-generated label on.", "short": f"{title} (story single)"}


def gameplay(world, n):
    folder = SINGLES / world
    found = sorted(folder.glob(f"{world} {n:02d} - *.mp4")) if folder.exists() else []
    if not found:
        return None
    video = found[0]
    title = video.stem.split(" - ", 1)[1]
    body = CAPTIONS.get((world, n), f"{title}! 🌽🤖")
    return {"kind": f"Gameplay - {world} {n:02d}", "video": video,
            "caption": f"{body}\n\n{ARCADE_CTA}\n\n{' '.join(GP_TAGS)}",
            "yt_title": f"{title} | Farm Fury: Arcade",
            "yt_desc": (f"{body}\n\nReal gameplay from Farm Fury: Arcade, {world}.\n\nPlay free:\n"
                        f"{store_links(GP_TAGS)}"),
            "note": "", "short": f"{world} {n:02d} {title}"}


def resolve(slot):
    """slot: ("ep", n) | ("story", key) | ("gp", world, n). None = gameplay not rendered yet."""
    if slot[0] == "ep":
        return episode(slot[1])
    if slot[0] == "story":
        return story(slot[1])
    return gameplay(slot[1], slot[2])


def label(slot):
    return f"{slot[1]} {slot[2]:02d}"


def build_days(start):
    queue = [("gp", w, n) for w in WORLD_ORDER for n in range(1, PER_WORLD + 1)]
    days, ep, d = [], 1, 0
    while ep <= len(EPISODES) or any(k >= d for k in SINGLE_DAYS):
        day = start + dt.timedelta(days=d)
        afternoon = None
        if day.weekday() in EPISODE_WEEKDAYS and ep <= len(EPISODES):
            afternoon, ep = ("ep", ep), ep + 1
        elif d in SINGLE_DAYS:
            afternoon = ("story", SINGLE_DAYS[d])
        days.append((day, queue.pop(0), afternoon))
        d += 1
    return days, queue


def post_html(i, time, slot):
    p = resolve(slot)
    if p is None:
        return (f'<div class="post todo"><div class="meta"><b>{time}</b> &middot; TO RECORD: '
                f'{html.escape(label(slot))} gameplay short</div></div>')
    for text in (p["caption"], p["yt_title"], p["yt_desc"]):
        check_wording(text.replace("#", " "))
    missing = "" if p["video"].exists() else ' <b style="color:#c00">FILE MISSING</b>'
    note = f'<div class="note">{html.escape(p["note"])}</div>' if p["note"] else ""
    cls = "post ep" if p["kind"].startswith("Episode") else "post"
    return f"""
<div class="{cls}">
  <div class="meta"><b>{time}</b> &middot; {html.escape(p['kind'])}{missing}</div>
  <div class="file"><a href="file:///{html.escape(str(p['video']).replace(chr(92), '/'))}">{html.escape(str(p['video']))}</a></div>
  {note}
  <div class="label">TikTok caption</div>
  <textarea id="c{i}" rows="6" readonly>{html.escape(p['caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('c{i}').value)">Copy caption</button>
  <div class="label">YouTube title ({len(p['yt_title'])}/100)</div>
  <input id="t{i}" readonly value="{html.escape(p['yt_title'], quote=True)}">
  <button onclick="navigator.clipboard.writeText(document.getElementById('t{i}').value)">Copy title</button>
  <div class="label">YouTube description</div>
  <textarea id="d{i}" rows="6" readonly>{html.escape(p['yt_desc'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('d{i}').value)">Copy description</button>
</div>"""


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--start", default="2026-11-07")
    p.add_argument("--morning", default="07:30")
    p.add_argument("--afternoon", default="16:30")
    args = p.parse_args()
    start = dt.date.fromisoformat(args.start)
    days, left = build_days(start)

    rows, overview, first_todo, i = [], [], {}, 0
    for n, (day, morning, afternoon) in enumerate(days, 1):
        kind = "Episode day" if afternoon and afternoon[0] == "ep" else "Gameplay day"
        rows.append(f"<h2>Day {n} &middot; {day.strftime('%a %d %b')} &middot; {kind}</h2>")
        cells = []
        for time, slot in ((args.morning, morning), (args.afternoon, afternoon)):
            if slot is None:
                cells.append("")
                continue
            rows.append(post_html(i, time, slot))
            i += 1
            r = resolve(slot)
            if r is None:
                first_todo.setdefault(slot[1], day)
                cells.append(f"<i>to record: {html.escape(label(slot))}</i>")
            else:
                text = html.escape(r["short"])
                cells.append(f"<b>{text}</b>" if r["kind"].startswith("Episode") else text)
        overview.append(f"<tr><td>{n}</td><td>{day.strftime('%a %d %b')}</td>"
                        + "".join(f"<td>{c}</td>" for c in cells) + "</tr>")
    todo_count = sum(1 for _, m, _ in days if resolve(m) is None)
    need = "".join(f"<li><b>{html.escape(w)}</b> (10 shorts): first one posts {d.strftime('%a %d %b')}, so have "
                   f"them rendered by {(d - dt.timedelta(days=3)).strftime('%a %d %b')}</li>"
                   for w, d in first_todo.items())
    after = sorted({s[1] for s in left}, key=WORLD_ORDER.index)
    window = start + dt.timedelta(days=9)

    PLANS.mkdir(exist_ok=True)
    out = PLANS / f"launch-{start.isoformat()}.html"
    out.write_text(f"""<!doctype html><meta charset="utf-8"><title>Farm Fury launch schedule</title>
<style>{SHEET_STYLE}
table{{border-collapse:collapse;width:100%;font-size:14px}} td,th{{border:1px solid #ddd;padding:4px 6px;text-align:left}}
.ep{{border-color:#e6b800;background:#fffbea}} .todo{{background:#f4f4f4;color:#777}} .note{{font-size:13px;color:#555}}
</style>
<h1>Farm Fury launch schedule ({TIKTOK_HANDLE} + YouTube Shorts)</h1>
<p>{len(days)} days, {start.strftime('%a %d %b')} to {days[-1][0].strftime('%a %d %b')}. Every morning
{args.morning}: the next gameplay short, world by world in number order. Tuesday and Saturday {args.afternoon}:
the next episode. Six other afternoons: a story single next to its episode. Same file, same time on TikTok and
YouTube.</p>
<p><b>Recordings needed:</b></p><ul>{need or "<li>none</li>"}</ul>
<p><b>After this sheet:</b> the mornings carry on through {html.escape(', '.join(after))} ({len(left)} more
shorts, one a day); Tuesday and Saturday afternoons are free for Chapter 2 (Stampede) once it's made.</p>
<p><b>TikTok Studio</b> schedules at most 10 days ahead: schedule Days 1-10 now, then top up every few days
(next on or after {window.strftime('%a %d %b')}). Every post: <i>Disclose post content &gt; Your brand</i> on.
Episodes and story singles: also the <i>AI-generated content</i> label. Make a "{SERIES}" playlist.</p>
{YOUTUBE_STEPS}
<table><tr><th>Day</th><th>Date</th><th>{args.morning}</th><th>{args.afternoon}</th></tr>{''.join(overview)}</table>
{''.join(rows)}
""", encoding="utf-8")
    print(f"{out}  ({len(days)} days to {days[-1][0]}, {todo_count} gameplay slots still to record)")


if __name__ == "__main__":
    main()
