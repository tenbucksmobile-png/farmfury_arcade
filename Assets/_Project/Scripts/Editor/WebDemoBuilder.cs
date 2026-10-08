using System.Linq;
using UnityEditor;
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
