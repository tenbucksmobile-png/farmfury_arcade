# Farm Fury Arcade: Web Demo (YouTube Playables + website)

Branch `web-demo`, worktree folder `Desktop\FarmFury_Arcade_Web` (created 2026-10-08 from `main` at
`ac45f71`). The mobile game stays on `main` in `Desktop\FarmFury_Arcade`; nothing here is merged back.
A main-game fix can be brought over with `git cherry-pick <sha>`.

Goal: a free, Corn Field-only (levels 1-25) demo used for marketing, not monetisation. One code base,
two builds, chosen by scripting define:
- `FF_YOUTUBE` - YouTube Playables (YouTube SDK for saves/pause/audio, no external links).
- `FF_WEBSITE` - farmfurygames.com/play/ (browser storage, store buttons allowed).

## Phase 0 findings (checked 2026-10-08 against developers.google.com/youtube/gaming/playables)

| Rule | Effect on the plan |
|---|---|
| Must NOT specifically target kids / be "made for kids"; content suitable for 13+ general audience | **Open risk.** Play Store lists the game with children in the target audience, and the Shorts are aimed at kids and families. The Playable must be positioned as a general/family audience game, not a kids' game. User to decide whether to apply. |
| Must NOT display external links, sharing prompts, extra user agreements, or an exit/quit button | YouTube build: no store buttons, QR or URLs; no Exit button; no Privacy/Terms screen. |
| `ytgame.engagement.openYTContent()` can open a YouTube video or another Playable | YouTube build's call to action = open the Farm Fury trailer / channel video, plus text "Full game on Google Play". |
| Must be playable at every aspect ratio from 9:32 to 32:9; must NOT lock orientation | **New work:** portrait layout (camera fit by width, HUD/D-pad below the maze). Also benefits phone browsers on the website. |
| Must support touch AND mouse for everything; keyboard and Esc-to-close recommended | D-pad/swipe already pointer-based; add Esc closes overlays. |
| Must say when no more content is available | Demo Complete screen after level 25. |
| Must call `firstFrameReady()` at the loading/title screen and `gameReady()` only when interactive | Title screen: firstFrameReady on show, gameReady when "Press Start" accepts a tap. |
| Saves: `saveData()` on real progress, no other save mechanism, await `loadData()` first, handle older save versions, final flush max 64 KiB | SaveManager routed through a platform layer; YouTube build must not touch PlayerPrefs. Save JSON gets a version field. |
| Audio: must obey `isAudioEnabled()` / `onAudioEnabledChange()`, no overall mute button (music/SFX toggles OK) | Wire to AudioManager; Settings keeps the Music toggle only. |
| Pause: must stop everything on `onPause()`, resume only on `onResume()`, no Page Visibility API | Same freeze as Pause menu (timeScale 0 + AudioListener.pause). Save on pause. |
| `sendScore()` optional; if used, must match best score in the save | Optional: send total score. |
| Unity: Compression Format must be **Disabled** (no gzip/Brotli); every file < 30 MiB; initial download < 30 MiB (15 recommended); total < 250 MiB; JS heap < 512 MB; playable in ~5 s | `.data` file (all Resources assets) must fit under 30 MiB uncompressed - texture/audio budget is the main risk. |
| Thumbnails/title/description must not contain logos/branding | Store-listing assets made separately. |
| Unity wrapper: Google's `YTGameWrapper.cs` + `UnityYTGameSDKLib.jslib` + WebGL template (unitypackage in the Playables sample repo) | Use it instead of writing our own bridge; wrap it in `YouTubePlatform`. |

Sources: certification requirements (design, integration, trust & safety, monetization), SDK
reference, Unity wrapper page - all under developers.google.com/youtube/gaming/playables.

Still to do in Phase 0 (user): submit the Playables interest form; decide the kids-audience question.

## Defaults (agreed 2026-10-08)
Daily Challenge removed; Leaderboards removed; Character Story kept (Cosmetics tab removed);
coins + coin Revive + coin Skip kept; combos kept.

## Phases

1. **Setup** - worktree (done), switch platform to Web, add `FF_YOUTUBE`/`FF_WEBSITE` build profiles.
2. **Delete** - packages (purchasing, analytics, levelplay, collab-proxy, visualscripting,
   multiplayer.center, unused modules), folders (LevelPlay, MobileDependencyResolver, Plugins/Android,
   CloudBuildScripts, _Project/iOS, cosmetics data+sprites, LevelData_26-175 + RobotTest, other-world
   art/music), scripts (Ad/IAP/Analytics managers, shop/cosmetic/locker/legal/merch/parental-gate/
   menu-hub screens, cosmetic renderers, daily challenge, leaderboards, roster, Android back handler,
   SafeArea, Phase tests, iOS/cosmetic/machine editor tools); edit GameManager, GameplayHUD,
   RevivePrompt, LevelComplete/Failed, Pause, Settings, MainMenu, CharacterStory, UnlockProgression
   (25 levels), LevelSelect, SaveManager, the 3 machine-skin abilities; trim Phase2/3/5 builders and
   ArtWiringBuilder; rerun Phase 2 -> 3 -> 4 -> 5 -> Wire Uploaded Art. Checkpoint: compiles, a level plays.
3. **Platform layer** - `IPlatformServices` (save get/set/flush + async load, firstFrame/gameReady,
   pause/resume, audio enabled, links allowed, open store / open YouTube video), `WebsitePlatform`,
   `YouTubePlatform` (on Google's wrapper), boot waits for save load, SaveManager -> platform.
4. **Marketing screens** - locked "Full game" world shields + panel, Gerald/Billy "Full game",
   Demo Complete screen after level 25. Website: Play badge + QR + UTM. YouTube: text + openYTContent.
5. **Responsive layout** (new, required by YouTube) - portrait/narrow aspect: camera fits maze
   width, HUD and D-pad reflow below the board; Esc closes overlays.
6. **Size & speed** - compression Disabled (YouTube) / gzip+fallback (website), strip engine code,
   IL2CPP size, texture caps, Vorbis music streaming. Measure .data/.wasm against 30 MiB.
7. **Test** - all 25 levels, unlocks, saves across reload, pause/mute, every aspect ratio, touch+mouse.
8. **Ship** - YouTube test suite + certification; website upload to public_html/play/, home page link,
   privacy line.
