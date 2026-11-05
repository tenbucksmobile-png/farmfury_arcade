"""Storyline launch sheet: one episode each morning, one gameplay short later the same day.

Episodes come from the artwork folder's Video/Episodes (rendered by storyline/make_episode.py);
gameplay shorts from sessions/*/shorts_franchise/ (make_shorts.py --franchise), ordered the same way
plan_posts.py orders them. Writes plans/launch-<start>.html. Doesn't touch ledger.json.

    python plan_launch.py --start 2026-11-06
    python plan_launch.py --start 2026-11-06 --episode-time 07:00 --weekend-episode-time 08:30 --short-time 17:30
"""

import argparse
import datetime as dt
import html
from pathlib import Path

from make_shorts import check_wording
from plan_posts import (APP_STORE_LINK, HERE, PLANS, SHEET_STYLE, TIKTOK_HANDLE, YOUTUBE_SHORTS_LINK,
                        YOUTUBE_STEPS, dedupe_titles, describe, order, youtube_block)

EPISODE_DIR = Path.home() / "Desktop/FarmFury_Technical/FarmFury_Artwork/Video/Episodes"
SERIES = "Farm Fury: The Story"

# (file, title, caption body). Same wording rule as the gameplay shorts: robots get dodged, outrun,
# busted or powered down, never anything harsher.
EPISODES = [
    ("ep01_the_night_the_robots_came.mp4", "The Night the Robots Came",
     "One quiet night, the Harvest Robots landed on the farm and took every crop 🌽🤖 Who's going to stop them?"),
    ("ep02_chaos_on_the_farm.mp4", "Chaos on the Farm",
     "The morning after: eight farm animals, zero crops and a LOT of panic 🐔🐄🐷"),
    ("ep03_the_debrief.mp4", "The Debrief",
     "Live from the barn: Barnyard Breakdown replays the robot landing frame by frame 📺"),
    ("ep04_the_barn_meeting.mp4", "The Barn Meeting",
     "Ring the bell! All eight animals meet in the barn... and Bessie knows a secret 🔔"),
    ("ep05_the_golden_glow.mp4", "The Golden Glow",
     "A golden sunflower gives every animal a special power ✨🌻 Which power is your favourite?"),
    ("ep06_meet_the_metal.mp4", "Meet the Metal",
     "WANTED: six Harvest Robots, each with its own trick 🤖 Which one would you dodge first?"),
    ("ep07_operation_harvest_home.mp4", "Operation Harvest Home",
     "The plan: get into the maze, collect every crop, dodge every robot 🗺️ Ready?"),
    ("ep08_into_the_mazes.mp4", "Into the Mazes",
     "Seven worlds of mazes, from the Corn Field to Harvest Moon 🌽🍎🌾 Which one looks hardest?"),
    ("ep09_combo_chaos.mp4", "Combo Chaos",
     "Tag-team two animals in the right order and you get a COMBO 🔁💥 Which pair would you pick?"),
]
EP_TAGS = ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#mobilegame"]
EP_CTA = "Play Farm Fury free on the App Store and Google Play, link in bio."


def episode_post(n, file, title, body):
    caption = f"EP {n} | {title}\n{body}\n\n{EP_CTA}\n\n{' '.join(EP_TAGS)}"
    yt_title = f"Ep {n}: {title} | {SERIES}"
    yt_description = (f"{body}\n\nEpisode {n} of {SERIES}.\n\nPlay Farm Fury: Arcade free:\n"
                      f"App Store: {APP_STORE_LINK}\nGoogle Play: {YOUTUBE_SHORTS_LINK}\n\n"
                      f"{' '.join(['#shorts'] + EP_TAGS)}")
    for text in (caption, yt_title, yt_description):
        check_wording(text.replace("#", " "))
    return {"kind": f"episode {n}", "video": str(EPISODE_DIR / file), "caption": caption,
            "yt_title": yt_title, "yt_description": yt_description, "seconds": None}


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--start", default=(dt.date.today() + dt.timedelta(days=1)).isoformat())
    p.add_argument("--episode-time", default="07:00")
    p.add_argument("--weekend-episode-time", default="08:30")
    p.add_argument("--short-time", default="17:30")
    args = p.parse_args()

    shorts = [describe(m) for m in sorted(HERE.glob("sessions/*/shorts_franchise/*.json"))
              if m.with_suffix(".mp4").exists()]
    shorts = order(dedupe_titles(shorts, taken=[]))
    start = dt.date.fromisoformat(args.start)
    rows, i = [], 0
    for d, (file, title, body) in enumerate(EPISODES):
        day = start + dt.timedelta(days=d)
        ep_time = args.weekend_episode_time if day.weekday() >= 5 else args.episode_time
        posts = [(ep_time, episode_post(d + 1, file, title, body))]
        if d < len(shorts):
            posts.append((args.short_time, shorts[d]))
        rows.append(f"<h2>{day.strftime('%A %d %B')}</h2>")
        for t, post in posts:
            secs = f" &middot; {post['seconds']}s" if post.get("seconds") else ""
            rows.append(f"""
<div class="post">
  <div class="meta"><b>{t}</b> &middot; {html.escape(post['kind'])}{secs}</div>
  <div class="file"><a href="file:///{html.escape(post['video'].replace(chr(92), '/'))}">{html.escape(post['video'])}</a></div>
  <div class="label">TikTok caption</div>
  <textarea id="c{i}" rows="7" readonly>{html.escape(post['caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('c{i}').value)">Copy caption</button>
{youtube_block(i, post)}
</div>""")
            i += 1
    left = shorts[len(EPISODES):]
    if left:
        rows.append(f"<h2>Gameplay shorts left after Ep 9 ({len(left)})</h2><ul>"
                    + "".join(f"<li>{html.escape(s['video'])}</li>" for s in left) + "</ul>")
    PLANS.mkdir(exist_ok=True)
    out = PLANS / f"launch-{start.isoformat()}.html"
    out.write_text(f"""<!doctype html><meta charset="utf-8"><title>Farm Fury launch plan</title>
<style>{SHEET_STYLE}</style>
<h1>Farm Fury: The Story launch ({TIKTOK_HANDLE})</h1>
<p><b>TikTok Studio:</b> Upload &gt; Schedule at the time shown (South Africa time), paste the caption,
switch on <i>Disclose post content &gt; Your brand</i> and the <i>AI-generated content</i> label on episodes.
Add each episode to the "{SERIES}" playlist; pin Ep 1.</p>
{YOUTUBE_STEPS}
{''.join(rows)}
""", encoding="utf-8")
    print(f"{out}  ({len(EPISODES)} episodes, {min(len(shorts), len(EPISODES))} gameplay shorts paired,"
          f" {len(left)} left over)")


if __name__ == "__main__":
    main()
