"""Schedule sheet for Instagram Reels + Facebook Reels, aimed at the UK and US.

Same videos and the same running order as plan_launch.py (gameplay short every day, an episode on Tuesday
and Saturday, story singles next to their episodes), but its own start date and UK/US-friendly times:

  20:00 South Africa  = 18:00 UK, 13:00 New York, 10:00 Los Angeles   (the gameplay short)
  01:00 South Africa  = 23:00 UK, 18:00 New York, 15:00 Los Angeles   (episode / story single, posted
                        just after midnight, so it falls on the NEXT calendar date in South Africa)
(Times are for Nov-Mar; when the UK/US clocks go forward the UK/US times shift by an hour.)

One post goes to both Instagram and Facebook from Meta Business Suite (Planner > Create reel, tick both).
Each post gets an Instagram caption (max 5 hashtags, "link in bio") and a Facebook caption (plain link).

    python plan_meta.py                       # starts Sat 2026-11-21
    python plan_meta.py --start 2026-11-14    # start dates should be a Saturday (keeps the story singles
                                              # next to their episodes)

Writes plans/meta-<start>.html.
"""

import argparse
import datetime as dt
import html

from make_shorts import check_wording
from plan_launch import STAMPEDE_WORLDS, WORLD_ORDER, build_days, label, resolve
from plan_posts import PLANS, SHEET_STYLE

SITE = "farmfurygames.com"
IG_HANDLE = "@farmfurygames"

# Hashtags chosen for UK/US reach; Instagram caps at 5, so keep each list at 5.
IG_TAGS = {
    "Episode": ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#kidsanimation"],
    "Story single": ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#cuteanimals"],
    "Gameplay": ["#farmfury", "#mobilegames", "#indiegame", "#arcadegames", "#familygames"],
    "Stampede": ["#farmfury", "#mobilegames", "#indiegame", "#platformer", "#familygames"],
    "Cosmetics": ["#farmfury", "#mobilegames", "#cuteanimals", "#farmanimals", "#familygames"],
    "Review": ["#farmfury", "#gamereview", "#mobilegames", "#funny", "#familygames"],
    "StampedeReview": ["#farmfury", "#gamereview", "#mobilegames", "#platformer", "#funny"],
    "Carousel": ["#farmfury", "#mobilegames", "#familygames", "#farmanimals", "#gamesforkids"],
}
FB_TAGS = {
    "Episode": ["#FarmFury", "#Animation", "#FamilyFun"],
    "Story single": ["#FarmFury", "#Animation", "#FamilyFun"],
    "Gameplay": ["#FarmFury", "#MobileGames", "#FamilyGames"],
    "Stampede": ["#FarmFury", "#MobileGames", "#FamilyGames"],
    "Cosmetics": ["#FarmFury", "#MobileGames", "#FamilyFun"],
    "Review": ["#FarmFury", "#GameReview", "#FamilyFun"],
    "StampedeReview": ["#FarmFury", "#GameReview", "#FamilyFun"],
    "Carousel": ["#FarmFury", "#FamilyGames", "#MobileGames"],
}
IG_CTA = {
    "Episode": "Play Farm Fury free on the App Store & Google Play 👉 link in bio",
    "Story single": "Play Farm Fury free on the App Store & Google Play 👉 link in bio",
    "Gameplay": "Farm Fury: Arcade is FREE on the App Store & Google Play 👉 link in bio",
    "Stampede": "Farm Fury: Stampede is FREE on the App Store & Google Play 👉 link in bio",
    "Cosmetics": "Farm Fury: Arcade is FREE on the App Store & Google Play 👉 link in bio",
    "Review": "Farm Fury: Arcade is FREE on the App Store & Google Play 👉 link in bio",
    "StampedeReview": "Farm Fury: Stampede is FREE on the App Store & Google Play 👉 link in bio",
    "Carousel": "Play Farm Fury free on the App Store & Google Play 👉 link in bio",
}
FB_CTA = {
    "Stampede": f"Farm Fury: Stampede is free on the App Store and Google Play: {SITE}",
    "Cosmetics": f"Farm Fury: Arcade is free on the App Store and Google Play: {SITE}",
    "Review": f"Farm Fury: Arcade is free on the App Store and Google Play: {SITE}",
    "StampedeReview": f"Farm Fury: Stampede is free on the App Store and Google Play: {SITE}",
    "Episode": f"Play Farm Fury free on the App Store and Google Play: {SITE}",
    "Story single": f"Play Farm Fury free on the App Store and Google Play: {SITE}",
    "Gameplay": f"Farm Fury: Arcade is free on the App Store and Google Play: {SITE}",
    "Carousel": f"Play Farm Fury free on the App Store and Google Play: {SITE}",
}


def group(kind):
    if kind == "Carousel":
        return "Carousel"
    if kind.startswith("Episode"):
        return "Episode"
    if kind.startswith("Story"):
        return "Story single"
    if kind.startswith("Cosmetics"):
        return "Cosmetics"
    if kind.startswith("The Scout Reviews: Stampede"):
        return "StampedeReview"
    if kind.startswith("The Scout Reviews"):
        return "Review"
    if kind.startswith("Stampede") or any(w in kind for w in STAMPEDE_WORLDS):
        return "Stampede"
    return "Gameplay"


def captions(p):
    body = p["caption"].split("\n\n")[0]        # "EP n | title\nbody" or the plain body
    g = group(p["kind"])
    ig = f"{body}\n\n{IG_CTA[g]}\n\n{' '.join(IG_TAGS[g])}"
    fb = f"{body}\n\n{FB_CTA[g]}\n\n{' '.join(FB_TAGS[g])}"
    for text in (ig, fb):
        check_wording(text.replace("#", " "))
    return ig, fb


def post_html(i, when, slot):
    when_txt = when.strftime("%a %d %b %H:%M")
    p = resolve(slot)
    if p is None:
        return (f'<div class="post todo"><div class="meta"><b>{when_txt}</b> &middot; TO MAKE: '
                f'{html.escape(label(slot))}</div></div>')
    ig, fb = captions(p)
    missing = "" if p["video"].exists() else ' <b style="color:#c00">FILE MISSING</b>'
    ai = ('<div class="note">AI-made animation: turn on <i>Add AI label</i> (Instagram: Advanced settings; '
          'Facebook: the AI info toggle).</div>' if group(p["kind"]) not in ("Gameplay", "Stampede") else "")
    cls = "post ep" if p["kind"].startswith("Episode") else "post"
    return f"""
<div class="{cls}">
  <div class="meta"><b>{when_txt}</b> (South Africa time) &middot; {html.escape(p['kind'])}{missing}</div>
  <div class="file"><a href="file:///{html.escape(str(p['video']).replace(chr(92), '/'))}">{html.escape(str(p['video']))}</a></div>
  {ai}
  <div class="label">Instagram caption</div>
  <textarea id="i{i}" rows="7" readonly>{html.escape(ig)}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('i{i}').value)">Copy Instagram caption</button>
  <div class="label">Facebook caption</div>
  <textarea id="f{i}" rows="6" readonly>{html.escape(fb)}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('f{i}').value)">Copy Facebook caption</button>
</div>"""


SETUP = f"""
<details open><summary><b>One-time setup (about 30 minutes)</b></summary><ol>
<li><b>Facebook Page.</b> Facebook &gt; Pages &gt; Create new Page. Name: <i>Farm Fury Games</i>, category
<i>Video Game</i>. Profile picture: the Farm Fury Games logo; cover photo: the YouTube banner
(<code>channel-kit/youtube_banner_franchise_v2.png</code> crops well). Website: https://{SITE}.
Then <i>Edit action button</i> &gt; <i>Play Game</i> (or <i>Visit website</i>) &gt; https://{SITE}.</li>
<li><b>Instagram.</b> Create <i>{IG_HANDLE}</i> (same name as TikTok/YouTube if it's free), same logo.
Settings &gt; Account type &gt; switch to a <b>Professional account</b>, category <i>Video Game</i> or
<i>Gaming Video Creator</i>. Bio: "Farm animals vs Harvest Robots 🐔🤖 Free on App Store &amp; Google Play 👇",
link: https://{SITE}.</li>
<li><b>Link them.</b> On the Facebook Page: Settings &gt; Linked accounts &gt; Instagram &gt; Connect.</li>
<li><b>Meta Business Suite</b> (business.facebook.com, or the Meta Business Suite app). Check its time zone
is South Africa so the times on this sheet line up (it uses your computer's time zone when scheduling).</li>
<li>Optional: post 3 of the gameplay shorts straight away (not scheduled) so the profiles aren't empty when the
first scheduled reel goes out. Use any from Corn Field 01-03; they post again on their day anyway.</li>
</ol></details>
<details open><summary><b>Scheduling each reel</b></summary><ol>
<li>Meta Business Suite &gt; Planner (or <i>Create reel</i>) &gt; tick <b>both</b> the Facebook Page and
Instagram.</li>
<li>Upload the file from the sheet. Paste the <b>Instagram caption</b>; switch to <i>Customise post for
Facebook</i> and paste the <b>Facebook caption</b> there.</li>
<li>Cover: pick a bright frame (or upload the episode thumbnail from
<code>Episodes/thumbnails/</code> for episodes).</li>
<li>Instagram: <i>Share to Feed</i> on. Episodes/story singles: <i>Add AI label</i> on.</li>
<li>Schedule &gt; set the date and time from the sheet. Meta Business Suite lets you schedule weeks ahead, so
you can load a whole week (or the whole sheet) in one sitting.</li>
</ol></details>
<p><b>Paid boost (optional, later):</b> when a reel does well, <i>Boost</i> it with Audience = custom,
Locations = United States + United Kingdom, Age 25-44 (parents). Never target under-18s. Start with a few
dollars a day for 3 days and compare installs by country in Play Console / App Store Connect.</p>
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--start", default="2026-11-21")
    ap.add_argument("--first", default="20:00", help="gameplay short, same SA date")
    ap.add_argument("--second", default="01:00", help="episode/single, the next SA date")
    ap.add_argument("--review", default="23:00", help="the Scout Reviews (21:00 UK, 16:00 New York), same SA date")
    args = ap.parse_args()
    start = dt.date.fromisoformat(args.start)
    if start.weekday() != 5:
        print("Note: start isn't a Saturday; some story singles may land on episode days and be skipped.")
    t1 = dt.time.fromisoformat(args.first)
    t2 = dt.time.fromisoformat(args.second)
    days, left = build_days(start)

    rows, overview, i, todo = [], [], 0, 0
    t3 = dt.time.fromisoformat(args.review)
    for n, (day, gp, review, second) in enumerate(days, 1):
        first_at = dt.datetime.combine(day, t1)
        review_at = dt.datetime.combine(day, t3)
        second_at = dt.datetime.combine(day + dt.timedelta(days=1), t2)
        kind = "Episode day" if second and second[0] == "ep" else "Gameplay day"
        rows.append(f"<h2>Day {n} &middot; {day.strftime('%a %d %b')} &middot; {kind}</h2>")
        cells = []
        for when, slot in ((first_at, gp), (review_at, review), (second_at, second)):
            if slot is None:
                cells.append("")
                continue
            rows.append(post_html(i, when, slot))
            i += 1
            r = resolve(slot)
            if r is None:
                todo += 1
                cells.append(f"<i>to record: {html.escape(label(slot))}</i>")
            else:
                t = html.escape(r["short"])
                cells.append((f"<b>{t}</b>" if r["kind"].startswith("Episode") else t)
                             + f"<br><small>{when.strftime('%a %d %b %H:%M')}</small>")
        overview.append(f"<tr><td>{n}</td><td>{day.strftime('%a %d %b')}</td>"
                        + "".join(f"<td>{c}</td>" for c in cells) + "</tr>")
    after = sorted({s[1] for s in left}, key=WORLD_ORDER.index)

    PLANS.mkdir(exist_ok=True)
    out = PLANS / f"meta-{start.isoformat()}.html"
    out.write_text(f"""<!doctype html><meta charset="utf-8"><title>Farm Fury Instagram + Facebook Reels</title>
<style>{SHEET_STYLE}
table{{border-collapse:collapse;width:100%;font-size:14px}} td,th{{border:1px solid #ddd;padding:4px 6px;text-align:left;vertical-align:top}}
.ep{{border-color:#e6b800;background:#fffbea}} .todo{{background:#f4f4f4;color:#777}} .note{{font-size:13px;color:#555}}
details{{margin:12px 0}} code{{font-size:13px}}
</style>
<h1>Farm Fury: Instagram + Facebook Reels (UK/US)</h1>
<p>{len(days)} days from {start.strftime('%a %d %b')}. Same videos and order as the TikTok/YouTube sheet, at
times aimed at the UK and US. All times are <b>South Africa time</b>:</p>
<ul>
<li><b>{t1.strftime('%H:%M')}</b> every day: the next gameplay short (18:00 UK, 13:00 New York).</li>
<li><b>{t2.strftime('%H:%M')} the following night</b>: the episode (Tue/Sat) or story single
(23:00 UK, 18:00 New York, 15:00 Los Angeles). Watch the date on each post: it's the next calendar day.</li>
<li><b>{t3.strftime('%H:%M')}</b> on five days: The Scout Reviews (21:00 UK, 16:00 New York).</li>
</ul>
{SETUP}
<table><tr><th>Day</th><th>Date</th><th>{t1.strftime('%H:%M')}</th><th>{t3.strftime('%H:%M')} (Scout Reviews)</th><th>{t2.strftime('%H:%M')} (next day)</th></tr>{''.join(overview)}</table>
<p>After this sheet the gameplay shorts carry on through {html.escape(', '.join(after))}.</p>
{''.join(rows)}
""", encoding="utf-8")
    print(f"{out}  ({len(days)} days, {i} posts, {todo} still to record)")


if __name__ == "__main__":
    main()
