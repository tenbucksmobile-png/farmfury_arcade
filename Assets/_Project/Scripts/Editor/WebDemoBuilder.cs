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
