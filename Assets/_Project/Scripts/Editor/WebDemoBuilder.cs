using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>Web demo (Corn Field only) build tooling. See WEB_DEMO_PLAN.md at the repo root.
    ///
    /// Unity only puts an asset into a build if the scene or a Resources folder references it, so
    /// trimming the other worlds out of the build is done by removing references here, not by
    /// deleting their source art.
    ///
    /// Do NOT run Phase2ProjectBuilder.BuildAll or Phase3ProjectBuilder.BuildAll in this branch:
    /// they recreate all 175 LevelData assets and every world's art set.</summary>
    public static class WebDemoBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        // Machine-skin hazard prefab fields on the 3 skinned characters' abilities. The prefabs
        // themselves are deleted in this branch; clearing the fields keeps the prefabs clean.
        private static readonly (string prefabPath, string field)[] MachineSkinFields =
        {
            ("Assets/_Project/Prefabs/Characters/Cluck.prefab", "oilHazardPrefab"),
            ("Assets/_Project/Prefabs/Characters/Bessie.prefab", "milkShockwavePrefab"),
            ("Assets/_Project/Prefabs/Characters/Horace.prefab", "hayBaleProjectilePrefab"),
        };

        /// <summary>Rebuilds the UI and art wiring, then trims the scene to the demo's content.
        /// The web demo's equivalent of the main game's Phase 5 -> Wire Uploaded Art chain.</summary>
        [MenuItem("Farm Fury Arcade/Web Demo/Rebuild UI + Trim (run after any UI change)")]
        public static void RebuildAll()
        {
            Phase5ProjectBuilder.BuildAll();
            ArtWiringBuilder.WireAll();
            TrimToDemoContent();
        }

        [MenuItem("Farm Fury Arcade/Web Demo/Trim Scene To Corn Field")]
        public static void TrimToDemoContent()
        {
            // Must run first: it reopens the scene from disk, which would discard any unsaved
            // changes made below.
            SceneCleanupBuilder.RemoveDeletedTestObjects();
            EditorSceneManager.OpenScene(ScenePath);

            int trimmedArtSets = 0, trimmedMusic = 0, missingScripts = 0;

            var managersGO = GameObject.Find("GameManagers");
            if (managersGO != null)
            {
                var tileMap = managersGO.GetComponent<TileMapRenderer>();
                if (tileMap != null)
                {
                    trimmedArtSets = KeepOnlyCornField(new SerializedObject(tileMap), "mazeArtSets");
                }
                var audio = managersGO.GetComponent<AudioManager>();
                if (audio != null)
                {
                    trimmedMusic = KeepOnlyCornField(new SerializedObject(audio), "worldMusicClips");
                }
            }
            else
            {
                Debug.LogWarning("[WebDemoBuilder] GameManagers not found.");
            }

            // Deleted scripts (Ad/IAP/Analytics managers, Phase test harnesses) leave missing-script
            // components behind; remove them everywhere in the scene, and drop the empty test objects.
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missingScripts += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
            }

            ClearMachineSkinFields();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"[WebDemoBuilder] Trimmed {trimmedArtSets} maze art set(s), {trimmedMusic} world music entr(ies), " +
                      $"removed {missingScripts} missing-script component(s).");
        }

        // ---- Boot scene -----------------------------------------------------------------------

        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        /// <summary>Creates Boot.unity (build index 0): a camera and the "YTGameWrapper" GameObject
        /// carrying Google's YTGameWrapper and PlatformBootstrap, which loads the save and then opens
        /// Game.unity. Sets the build scene list to Boot, Game. Safe to re-run (recreates it).</summary>
        [MenuItem("Farm Fury Arcade/Web Demo/Create Boot Scene")]
        public static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGO = new GameObject("Main Camera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.137f, 0.122f, 0.125f); // matches the page's #231F20 canvas colour
            cameraGO.tag = "MainCamera";

            // Name must be exactly "YTGameWrapper": UnityYTGameSDKLib.jslib sends callbacks to it by name.
            var bootGO = new GameObject("YTGameWrapper");
            bootGO.AddComponent<YTGameSDK.YTGameWrapper>();
            bootGO.AddComponent<PlatformBootstrap>();

            EditorSceneManager.SaveScene(scene, BootScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[WebDemoBuilder] Boot.unity created; build scenes = Boot, Game.");
        }

        // ---- Builds -----------------------------------------------------------------------------

        private const string YouTubeDefine = "FF_YOUTUBE";

        /// <summary>YouTube Playables build into Builds/YouTube. Uses the FarmFuryYouTube page
        /// template (loads the YouTube SDK), compression Disabled (YouTube's Unity guide: no
        /// gzip/Brotli; YouTube serves the files itself), and runInBackground so only YouTube's own
        /// onPause/onResume stop the game (Playables forbids using page visibility for that).</summary>
        [MenuItem("Farm Fury Arcade/Web Demo/Build YouTube Playable")]
        public static void BuildYouTube()
        {
            ConfigureCommonWebSettings();
            SetYouTubeDefine(true);
            PlayerSettings.WebGL.template = "PROJECT:FarmFuryYouTube";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.runInBackground = true;
            Build("Builds/YouTube");
        }

        /// <summary>Website build into Builds/Website, for farmfurygames.com/play/. Gzip with
        /// Unity's decompression fallback, so the cPanel server needs no special headers.</summary>
        [MenuItem("Farm Fury Arcade/Web Demo/Build Website")]
        public static void BuildWebsite()
        {
            ConfigureCommonWebSettings();
            SetYouTubeDefine(false);
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.runInBackground = false;
            Build("Builds/Website");
        }

        private static void ConfigureCommonWebSettings()
        {
            if (!File.Exists(BootScenePath))
            {
                CreateBootScene();
            }
            PlayerSettings.productName = "Farm Fury Arcade";
            PlayerSettings.companyName = "Tenbucks Mobile";
            ApplyWebSizeSettings();
        }

        // ---- Download size --------------------------------------------------------------------

        /// <summary>Above this source size (longest side, px) a texture is treated as a full-screen
        /// backdrop and capped at BackdropMaxSize; everything else is capped at SpriteMaxSize.</summary>
        private const int BackdropThreshold = 1500;
        private const int BackdropMaxSize = 2048;
        private const int SpriteMaxSize = 512;
        /// <summary>Characters, robots and maze pieces are drawn about one tile tall (roughly 75-115 px
        /// on screen), so 256 px is still ~2x; UI art (signs, banners, buttons) keeps 512.</summary>
        private const int InWorldSpriteMaxSize = 256;

        private static bool IsInWorldSprite(string path) =>
            path.Contains("/Sprites/Characters/") || path.Contains("/Sprites/Robots/") || path.Contains("/Sprites/Environment/");

        /// <summary>Music files above this size get stronger web compression and load in the
        /// background, so they don't hold up the first playable frame.</summary>
        private const long MusicBytesThreshold = 300 * 1024;

        /// <summary>Web-only size settings (YouTube Playables: each file under 30 MB uncompressed, first
        /// download under 30 MB, playable within ~5 s). Changes only Web/WebGL platform overrides and
        /// player settings, never the mobile builds (which live on main). Safe to re-run.
        ///
        /// The first website build was 36.4 MB gzipped / ~64 MB uncompressed, 90% textures: sprites
        /// went in uncompressed (~1 MB per 500x500 sprite). Crunched DXT5 keeps the files small even
        /// uncompressed (YouTube forbids gzip/Brotli). Note DXT needs width/height in multiples of 4;
        /// the few sprites that aren't stay uncompressed (Unity's own fallback).</summary>
        /// <summary>Crunched DXT5 is the smallest download, but DXT only compresses textures whose
        /// width and height (after the max-size downscale) are multiples of 4 - 87 of our 352 sprites
        /// aren't (e.g. 531x500), and 2720x1536 backdrops become 2048x1157. Unity silently ships those
        /// uncompressed (a 2048 backdrop was 9 MB). ASTC has no size rule, so those use ASTC instead.
        /// Either way the browser decodes on the CPU where the GPU lacks the format (DXT on most phones,
        /// ASTC on most desktops); download size is what the YouTube limits measure.</summary>
        private static TextureImporterFormat ChooseWebFormat(int w, int h, int maxSize, bool isBackdrop)
        {
            float scale = Mathf.Max(w, h) > maxSize ? (float)maxSize / Mathf.Max(w, h) : 1f;
            int rw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int rh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            if (rw % 4 == 0 && rh % 4 == 0)
            {
                return TextureImporterFormat.DXT5Crunched;
            }
            return isBackdrop ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
        }

        [MenuItem("Farm Fury Arcade/Web Demo/Apply Web Size Settings")]
        public static void ApplyWebSizeSettings()
        {
            int textures = 0, audio = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Sprites", "Assets/TextMesh Pro" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                bool isBackdrop = Mathf.Max(w, h) > BackdropThreshold;
                int maxSize = isBackdrop ? BackdropMaxSize : IsInWorldSprite(path) ? InWorldSpriteMaxSize : SpriteMaxSize;
                var format = ChooseWebFormat(w, h, maxSize, isBackdrop);

                var settings = importer.GetPlatformTextureSettings("WebGL");
                if (settings.overridden && settings.maxTextureSize == maxSize &&
                    settings.format == format && settings.compressionQuality == 50)
                {
                    continue;
                }
                settings.overridden = true;
                settings.maxTextureSize = maxSize;
                settings.format = format;
                settings.crunchedCompression = format == TextureImporterFormat.DXT5Crunched;
                settings.compressionQuality = 50;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                textures++;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    continue;
                }
                bool isLong = new FileInfo(path).Length > MusicBytesThreshold;
                var s = importer.GetOverrideSampleSettings("WebGL");
                var wanted = s;
                wanted.compressionFormat = AudioCompressionFormat.AAC;
                wanted.quality = isLong ? 0.4f : 0.6f;
                wanted.loadType = isLong ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                bool loadInBackground = isLong;
                if (importer.ContainsSampleSettingsOverride("WebGL") && s.compressionFormat == wanted.compressionFormat &&
                    Mathf.Approximately(s.quality, wanted.quality) && s.loadType == wanted.loadType &&
                    importer.loadInBackground == loadInBackground)
                {
                    continue;
                }
                importer.SetOverrideSampleSettings("WebGL", wanted);
                importer.loadInBackground = loadInBackground;
                importer.SaveAndReimport();
                audio++;
            }

            var target = NamedBuildTarget.WebGL;
            // Unity 6 lets any plan hide the Unity splash; it cost 2.7 MB in the first build.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;

            AssetDatabase.SaveAssets();
            Debug.Log($"[WebDemoBuilder] Web size settings applied: {textures} texture(s) and {audio} audio clip(s) updated; " +
                      "splash off, managed stripping High, IL2CPP optimise for size.");
        }

        private static void SetYouTubeDefine(bool on)
        {
            var target = NamedBuildTarget.WebGL;
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';').Where(d => !string.IsNullOrWhiteSpace(d) && d != YouTubeDefine).ToList();
            if (on)
            {
                defines.Add(YouTubeDefine);
            }
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        private static void Build(string outputPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootScenePath, ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[WebDemoBuilder] Build {summary.result} -> {outputPath}: " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalErrors} error(s), {summary.totalTime}.");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Removes every element of a serialized array/list whose "mazeType" field isn't
        /// CornField. Returns how many were removed.</summary>
        private static int KeepOnlyCornField(SerializedObject so, string arrayField)
        {
            var arr = so.FindProperty(arrayField);
            if (arr == null || !arr.isArray)
            {
                Debug.LogWarning($"[WebDemoBuilder] {so.targetObject.GetType().Name}.{arrayField} not found.");
                return 0;
            }
            int removed = 0;
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var mazeType = arr.GetArrayElementAtIndex(i).FindPropertyRelative("mazeType");
                if (mazeType != null && mazeType.enumValueIndex != (int)MazeType.CornField)
                {
                    arr.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return removed;
        }

        private static void ClearMachineSkinFields()
        {
            foreach (var (prefabPath, field) in MachineSkinFields)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;
                foreach (var mb in root.GetComponents<MonoBehaviour>().Where(m => m != null))
                {
                    var so = new SerializedObject(mb);
                    var prop = so.FindProperty(field);
                    if (prop != null && prop.objectReferenceValue != null)
                    {
                        prop.objectReferenceValue = null;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
