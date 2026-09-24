# Farm Fury - TikTok and YouTube setup

Files in this folder:
- `profile_picture.png` - 800x800, use on both platforms (they crop it to a circle)
- `youtube_banner.png` - 2560x1440 YouTube banner (logo + tagline sit in the area every device shows)
- `youtube_banner_preview_safe_area.png` - preview only, red box = the always-visible area. Don't upload.

## Before you start
- Sign up with a **studio email** (e.g. `social@farmfurygames.com` or the studio Gmail), not a personal
  one, so the accounts belong to the business and can be handed over.
- Turn on **2-step verification** on both.
- Use the **same handle on both**. Suggested: `@farmfurygames` (matches farmfurygames.com and covers
  the sibling games planned later). Alternative: `@farmfuryarcade`.

## YouTube (channel for Shorts)
1. Sign in to the studio Google account, go to youtube.com > profile icon > **Create a channel**.
   Choose to use a **custom name** ("Farm Fury Games"); this makes a Brand Account, which other
   people can be given access to later.
2. Handle: `@farmfurygames`.
3. YouTube Studio > **Customisation**:
   - Profile picture: `profile_picture.png`
   - Banner: `youtube_banner.png`
   - Description: copy from "YouTube channel description" below
   - Links: Google Play link (YouTube version below) titled "Get the game - free", and
     `https://www.farmfurygames.com` titled "Website"
4. YouTube Studio > Settings > **Channel > Advanced settings > Audience** - **your decision, see below.**
5. Settings > Channel > Feature eligibility: verify with your phone number.

## TikTok
1. Download TikTok on the phone, sign up with the studio email, username `farmfurygames`, name
   "Farm Fury Games".
2. Profile > menu > Settings and privacy > **Account > Switch to Business Account**, category "Games".
   A business account can add a website link to the bio.
3. Edit profile: photo `profile_picture.png`, bio from below, website = the TikTok Google Play link below.

## Texts to paste

**TikTok bio** (80 characters max):
```
Dodge the robots, save the crops! 🌽🐔 Free on Google Play ⬇️
```

**YouTube channel description:**
```
Farm Fury: Arcade - farm animals vs Harvest Robots!

Race through the maze, dodge the robots and collect every crop to save the farm. Grab a power crop and the robots run from YOU. Every animal has its own special move: Cluck's Egg Drop, Bessie's Ground Slam, Percy's Bounce Roll and more.

Free on Google Play: https://play.google.com/store/apps/details?id=com.farmfury.arcade&referrer=utm_source%3Dyoutube%26utm_medium%3Dsocial%26utm_campaign%3Dchannel
Website: https://www.farmfurygames.com
```

## Store links (each one is tracked separately in Play Console)
| Where | Link |
|---|---|
| TikTok bio | `https://play.google.com/store/apps/details?id=com.farmfury.arcade&referrer=utm_source%3Dtiktok%26utm_medium%3Dsocial%26utm_campaign%3Dbio` |
| YouTube channel | `https://play.google.com/store/apps/details?id=com.farmfury.arcade&referrer=utm_source%3Dyoutube%26utm_medium%3Dsocial%26utm_campaign%3Dchannel` |

Installs from these show in Play Console's acquisition reports under tracked channels (UTM).

## Decision: "made for kids" on YouTube
YouTube requires you to say whether the channel/videos are **made for kids** (US COPPA law). This is
a legal judgement for you, not a setting to guess:
- **Yes:** comments, the notification bell and personalised ads are switched off; the videos can
  appear in YouTube Kids. Reach is usually lower.
- **No:** normal features, but only honest if the videos are aimed at a general audience (teens,
  adults, families), not primarily at children.
- Cartoon animals, simple language and a child-friendly game are the kind of things YouTube and the
  FTC weigh towards "made for kids". The game itself is treated as child-directed for ads.
You can set it per video instead of per channel. If unsure, ask a lawyer; it can't be automated.

TikTok is 13+ only and has no equivalent setting; aim the TikTok videos at teens and adults.

## After the accounts exist - connecting automatic uploads
**YouTube** (free):
1. console.cloud.google.com > new project "farmfury-shorts".
2. APIs & Services > Library > enable **YouTube Data API v3**.
3. OAuth consent screen: External, app name "Farm Fury Shorts Uploader", add your studio Google
   account as a **test user**.
4. Credentials > Create credentials > OAuth client ID > **Desktop app** > download the JSON.
5. Save it as `Tools/content-pipeline/channel-kit/client_secret.json` (it's gitignored; never commit it).
Uploads through the API stay **private** until Google audits the app (form: "YouTube API Services -
Audit and Quota Extension"). Until then, videos can be uploaded private automatically and made
public in YouTube Studio with one click, or scheduled there directly.

**TikTok:**
1. developers.tiktok.com > log in with the TikTok account > Manage apps > Connect an app.
2. Add the **Content Posting API** product. Needs: website `https://www.farmfurygames.com`,
   Privacy Policy `https://www.farmfurygames.com/privacy/`, Terms `https://www.farmfurygames.com/terms/`.
3. Until TikTok audits the app, API posts are private-only. Meanwhile use TikTok's own web
   scheduler (tiktok.com > Upload > Schedule) with the finished shorts from `sessions/<id>/shorts/`.
