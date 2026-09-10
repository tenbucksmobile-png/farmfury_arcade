#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>Runs automatically after Unity generates the Xcode project for every iOS build —
    /// local or Unity Cloud Build, since both go through the same BuildTarget.iOS export path.
    ///
    /// Fixes the Xcode 26 linker bug documented in CLAUDE.md's "iOS build toolchain" section:
    /// Unity's iOS export still adds `-ld64` to Other Linker Flags on both the main app target and
    /// the UnityFramework target, which forces Xcode's old linker — Xcode 26 asserts on it
    /// (`Assertion failed: (it != _dylibToOrdinal.end())`, function dylibToOrdinal, OutputFile.cpp
    /// line 5196) and the archive fails outright. That note was written as a manual "if you hit
    /// this, here's the fix" — this closes it proactively so the very first archive attempt (the
    /// literal gate to every downstream audit phase, per both the iOS Submission Audit and the
    /// cross-platform code audit) doesn't lose a build cycle to a bug that's already
    /// known and already has a known fix.
    ///
    /// Safe to leave in permanently: if a future Xcode/Unity version stops injecting `-ld64` (or
    /// never had it), UpdateBuildProperty's removal list simply has nothing to remove — this is a
    /// no-op in that case, not an error.
    ///
    /// Also injects the app-level Privacy Manifest (PrivacyInfo.xcprivacy, 2026-09-10) — Apple's
    /// binary-validation step at upload rejects an app that uses a "required-reason API"
    /// (PlayerPrefs -> NSUserDefaults, which every progress/economy value in this project goes
    /// through — see SaveManager.cs) with no manifest declaring a reason code, independent of human
    /// review. The source file lives at Assets/_Project/iOS/PrivacyInfo.xcprivacy (kept outside
    /// Assets/Plugins/iOS deliberately — that folder's importer only recognizes standard native-
    /// plugin extensions, and a plain .xcprivacy file dropped there has no guarantee of being
    /// copied into the app bundle with correct target membership); this method copies it into the
    /// generated Xcode project directly and registers it on the main app target itself, the same
    /// way third-party SDKs bundle their own manifests (see the several PrivacyInfo.xcprivacy files
    /// already present under Library/PackageCache for LevelPlay/AdMob/IAP — those are per-SDK and
    /// unrelated to this app-level one). This is a first-pass declaration covering the one
    /// required-reason API this app's own code actually uses (UserDefaults, reason CA92.1) with
    /// NSPrivacyTracking/NSPrivacyCollectedDataTypes left empty (no first-party tracking or data
    /// collection) — review against whatever the ad/IAP SDKs' own bundled manifests declare before
    /// public submission, not just before the first internal TestFlight build.</summary>
    public static class IOSPostProcessBuild
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            RemoveLd64Flag(project, mainTargetGuid, "main app target");
            RemoveLd64Flag(project, frameworkTargetGuid, "UnityFramework target");
            AddPrivacyManifest(project, mainTargetGuid, pathToBuiltProject);

            File.WriteAllText(projectPath, project.WriteToString());
        }

        private static void RemoveLd64Flag(PBXProject project, string targetGuid, string targetLabel)
        {
            project.UpdateBuildProperty(targetGuid, "OTHER_LDFLAGS", new string[0], new[] { "-ld64" });
            Debug.Log($"[IOSPostProcessBuild] Removed -ld64 from Other Linker Flags on the {targetLabel} " +
                      "(Xcode 26 linker workaround — see CLAUDE.md's iOS build toolchain note).");
        }

        private static void AddPrivacyManifest(PBXProject project, string mainTargetGuid, string pathToBuiltProject)
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project", "iOS", "PrivacyInfo.xcprivacy");
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning("[IOSPostProcessBuild] PrivacyInfo.xcprivacy not found at " + sourcePath +
                                  " — app-level Privacy Manifest was NOT added to this build.");
                return;
            }

            const string destRelativePath = "PrivacyInfo.xcprivacy";
            string destPath = Path.Combine(pathToBuiltProject, destRelativePath);
            File.Copy(sourcePath, destPath, true);

            string fileGuid = project.AddFile(destRelativePath, destRelativePath, PBXSourceTree.Source);
            project.AddFileToBuild(mainTargetGuid, fileGuid);
            Debug.Log("[IOSPostProcessBuild] Added PrivacyInfo.xcprivacy to the main app target's build resources.");
        }
    }
}
#endif
