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
    /// public submission, not just before the first internal TestFlight build.
    ///
    /// Also injects GADApplicationIdentifier into Info.plist (2026-09-14) — closes the crash-on-
    /// launch found in the first real TestFlight install. Google Mobile Ads (bundled here as
    /// LevelPlay's AdMob mediation adapter, see Assets/LevelPlay/Editor/ISAdMobAdapterDependencies.xml)
    /// verifies this key exists at process start via GADApplicationVerifyPublisherInitializedCorrectly
    /// and throws an uncaught NSException if it's missing — the crash log's own backtrace names that
    /// exact method, on the main thread, before the app ever reaches Unity's own code. This key is
    /// Google's AdMob "App ID" (from apps.admob.com > Apps > this app > App settings), a different
    /// value from the LevelPlay app key/ad unit IDs already stored on the AdManager component in the
    /// scene — Unity's iOS export has no built-in field for it, so nothing was ever writing it into
    /// Info.plist until now.</summary>
    public static class IOSPostProcessBuild
    {
        /// <summary>AdMob App ID for iOS, from apps.admob.com > Apps > FarmFury Arcade (iOS) >
        /// App settings > App ID. Update here (not per-build) if the AdMob app is ever
        /// recreated/relinked to a different bundle ID.</summary>
        private const string AdMobAppId = "ca-app-pub-1264425755955045~9222731930";

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

            AddGADApplicationIdentifier(pathToBuiltProject);
        }

        private static void AddGADApplicationIdentifier(string pathToBuiltProject)
        {
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            plist.root.SetString("GADApplicationIdentifier", AdMobAppId);

            // This app uses no encryption beyond the OS's standard HTTPS, so it's exempt from
            // Apple's Export Compliance requirement — Export Compliance was already answered
            // manually for the first build ("None of the algorithms mentioned above"), but that
            // answer doesn't carry forward automatically to future builds without this key. Set
            // it so every future build skips the "Missing Compliance" hold in App Store Connect
            // instead of needing the same manual answer re-entered every time.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);
            Debug.Log("[IOSPostProcessBuild] Set GADApplicationIdentifier in Info.plist to " +
                      AdMobAppId + " (fixes the GADApplicationVerifyPublisherInitializedCorrectly " +
                      "crash-on-launch — Google Mobile Ads throws an uncaught exception at process " +
                      "start if this key is missing). Also set ITSAppUsesNonExemptEncryption=false " +
                      "to skip the per-build Export Compliance prompt in App Store Connect.");
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
