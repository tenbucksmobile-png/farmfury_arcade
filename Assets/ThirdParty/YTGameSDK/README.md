# YouTube Playables Unity wrapper (Google)

`YTGameWrapper.cs` and `UnityYTGameSDKLib.jslib` are copied unchanged from Google's
`GoogleYTGameWrapper.unitypackage` in https://github.com/google/web-game-samples (Unity folder),
downloaded 2026-10-08. Licensed under the Apache License 2.0 (see each file's header).

Do not edit these files; game-specific logic lives in
`Assets/_Project/Scripts/Core/Platform/`. The wrapper needs a GameObject named exactly
`YTGameWrapper` (the jslib sends callbacks to it by name) - the Boot scene provides it.
