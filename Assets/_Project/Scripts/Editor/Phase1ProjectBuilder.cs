using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// One-off Phase 1 scaffolding: builds Game.unity and LevelData_01 programmatically so the
    /// architecture can be verified without hand-authoring scene YAML. Safe to re-run — it
    /// overwrites the scene and level asset each time.
    /// </summary>
    public static class Phase1ProjectBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Farm Fury Arcade/Phase 1/Build Game Scene (resets to Phase 1 baseline)")]
        public static void BuildAll()
        {
            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath);
                var managers = GameObject.Find("GameManagers");
                bool hasPhase2Content = managers != null && managers.GetComponent("ScoreManager") != null;
                if (hasPhase2Content && !EditorUtility.DisplayDialog(
                        "Rebuild Game.unity from scratch?",
                        "This scene already has Phase 2+ content (ScoreManager, TileMapRenderer, InputController, " +
                        "Cluck prefab wiring). Rebuilding from scratch wipes all of that back to the bare Phase 1 " +
                        "baseline (GameManager/DataManager/SaveManager/SceneController only, no Cluck prefab).\n\n" +
                        "Run 'Farm Fury Arcade > Phase 2 > Build All' afterward if you do this, or Cancel and use " +
                        "that instead.",
                        "Rebuild anyway", "Cancel"))
                {
                    Debug.Log("[Phase1ProjectBuilder] Rebuild cancelled — existing Phase 2+ scene left untouched.");
                    return;
                }
            }

            BuildGameScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase1ProjectBuilder] Game.unity built successfully. Run Phase 2 Project Builder for LevelData_01, prefabs, and Phase 2 component wiring.");
        }

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            cameraGO.transform.position = new Vector3(14f, 15f, -10f);
            cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraGO.AddComponent<AudioListener>();

            // Global Light 2D
            var lightGO = new GameObject("Global Light 2D");
            var light2D = lightGO.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;

            // Canvas + EventSystem
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Project uses the Input System package exclusively (activeInputHandler = 1),
            // so the EventSystem needs InputSystemUIInputModule rather than the legacy
            // StandaloneInputModule (which throws when UnityEngine.Input is disabled).
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<InputSystemUIInputModule>();

            // Content parents
            new GameObject("MazeParent");
            var characterParent = new GameObject("CharacterParent").transform;
            var robotParent = new GameObject("RobotParent").transform;

            // GameManagers
            var managersGO = new GameObject("GameManagers");
            managersGO.AddComponent<GameManager>();
            managersGO.AddComponent<DataManager>();
            managersGO.AddComponent<SaveManager>();
            var sceneController = managersGO.AddComponent<SceneController>();

            var so = new SerializedObject(sceneController);
            so.FindProperty("characterParent").objectReferenceValue = characterParent;
            so.FindProperty("robotParent").objectReferenceValue = robotParent;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Phase 1 verification harness - not gameplay/UI, see Phase1Test.cs.
            new GameObject("Phase1Test").AddComponent<Phase1Test>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static float _playModeStartTime;

        /// <summary>
        /// Batch-mode play test: opens Game.unity, enters Play mode, lets several in-game
        /// seconds pass (enough for Phase1Test's and Phase2Test's coroutine-driven checks, which
        /// include a few real-time waits), then exits Play mode and the Editor. Intended to be
        /// invoked with -executeMethod and WITHOUT -quit, since play mode needs the Editor's
        /// update loop to keep ticking. Uses Time.time (in-game seconds) rather than a raw frame
        /// count so it isn't sensitive to how fast/slow batch mode happens to tick.
        /// </summary>
        [MenuItem("Farm Fury Arcade/Phase 1/Run Play Mode Verification")]
        public static void RunPlayModeVerification()
        {
            EditorSceneManager.OpenScene(ScenePath);
            _playModeStartTime = -1f;
            EditorApplication.update += OnUpdateWhilePlaying;
            EditorApplication.isPlaying = true;
        }

        private const float PlayModeDurationSeconds = 3f;

        private static void OnUpdateWhilePlaying()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_playModeStartTime < 0f)
            {
                _playModeStartTime = Time.time;
            }

            if (Time.time - _playModeStartTime > PlayModeDurationSeconds)
            {
                EditorApplication.update -= OnUpdateWhilePlaying;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }
    }
}
