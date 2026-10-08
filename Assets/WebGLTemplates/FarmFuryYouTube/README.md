# FarmFuryYouTube WebGL template

Google's `YTGameWrapperTemplate` from `Google-WebGLTemplate-only.unitypackage`
(https://github.com/google/web-game-samples, Unity folder, downloaded 2026-10-08), with only the
page `<title>` changed. It loads the YouTube Playables SDK (`https://www.youtube.com/game_api/v1`)
before any game code and exposes `unityGameInstance`, which `UnityYTGameSDKLib.jslib` needs.

`unarchiver.min.js` is MIT (Xenova, see `LICENSE`); `lib/jszip.min.js` is MIT/GPLv3 dual
(see `lib/LICENSE.markdown`). They are only used if the build files are zipped (Path 2 in
index.html); our build uses Path 1 (uncompressed, as YouTube requires).
