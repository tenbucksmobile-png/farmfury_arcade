using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>One-off scene-hygiene fixes that aren't "wire uploaded art" (ArtWiringBuilder) or
    /// "rebuild a whole phase's content" (PhaseNProjectBuilder). Each entry point here is a small,
    /// targeted, idempotent edit to the existing Game.unity scene.</summary>
    public static class SceneCleanupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>Phase1Test/Phase2Test/Phase3Test/Phase4Test each draw an always-on OnGUI debug
        /// overlay (manual test buttons) in the top-left/top area of the screen — independent of
        /// their runOnStart flag, since OnGUI doesn't check it. Every PhaseNProjectBuilder leaves
        /// these GameObjects active after disabling only runOnStart on the previous phase's test,
        /// so all 4 overlays stack and render simultaneously in every Play session, looking like
        /// rows of black call-to-action buttons over whatever screen is actually showing.
        /// Deactivates all 5 Phase*Test GameObjects (including Phase5Test, which has no OnGUI but
        /// is the same kind of debug-only harness) so a normal Play session is clean. They aren't
        /// deleted — re-activate a specific one in the Inspector (or via its ContextMenu) to run
        /// its manual test battery again.
        ///
        /// Also de-duplicates: Phase5ProjectBuilder.BuildAll used to look up "Phase5Test" via
        /// GameObject.Find, which only matches active objects — once this method deactivates it,
        /// a later BuildAll re-run couldn't find it and spawned a second active Phase5Test every
        /// time. Phase5ProjectBuilder now looks up inactive instances too, but this method still
        /// collapses any duplicates a prior run already created before that fix landed.</summary>
        [MenuItem("Farm Fury Arcade/Disable Debug Test Overlays")]
        public static void DisableDebugTestOverlays()
        {
            EditorSceneManager.OpenScene(ScenePath);

            DedupeAndDisable<Phase1Test>();
            DedupeAndDisable<Phase2Test>();
            DedupeAndDisable<Phase3Test>();
            DedupeAndDisable<Phase4Test>();
            DedupeAndDisable<Phase5Test>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SceneCleanupBuilder] Debug test overlay GameObjects deduplicated and disabled.");
        }

        /// <summary>The Main Camera was static (no follow script) at orthographic size 16 —
        /// sized to keep the whole 28x31 maze on screen at once, classic-arcade style, but that
        /// reads as "very far and small" per feedback. Zooms in considerably (16 -> 5, roughly a
        /// 3x zoom) and adds CameraFollow so the camera tracks the active character instead of
        /// showing empty maze the player isn't near. Safe to re-run — just resets orthographicSize
        /// and ensures exactly one CameraFollow component.</summary>
        [MenuItem("Farm Fury Arcade/Zoom In Gameplay Camera")]
        public static void ZoomInGameplayCamera()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var cameraGO = GameObject.Find("Main Camera");
            var camera = cameraGO != null ? cameraGO.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogWarning("[SceneCleanupBuilder] Could not find Main Camera in the scene.");
                return;
            }

            camera.orthographicSize = 5f;

            if (cameraGO.GetComponent<CameraFollow>() == null)
            {
                cameraGO.AddComponent<CameraFollow>();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SceneCleanupBuilder] Gameplay camera zoomed in (orthographic size 16 -> 5) with CameraFollow added.");
        }

        private static void DedupeAndDisable<T>() where T : MonoBehaviour
        {
            var instances = Resources.FindObjectsOfTypeAll<T>()
                .Where(t => !EditorUtility.IsPersistent(t.gameObject) && t.gameObject.scene.IsValid())
                .ToList();

            if (instances.Count == 0)
            {
                Debug.LogWarning($"[SceneCleanupBuilder] Could not find any {typeof(T).Name} in the scene.");
                return;
            }

            // Keep the first, destroy any extras (duplicates from the Find()-only-finds-active bug).
            for (int i = 1; i < instances.Count; i++)
            {
                Object.DestroyImmediate(instances[i].gameObject);
            }

            instances[0].gameObject.SetActive(false);
        }
    }
}
