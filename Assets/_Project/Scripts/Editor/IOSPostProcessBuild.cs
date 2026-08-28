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
    /// no-op in that case, not an error.</summary>
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

            File.WriteAllText(projectPath, project.WriteToString());
        }

        private static void RemoveLd64Flag(PBXProject project, string targetGuid, string targetLabel)
        {
            project.UpdateBuildProperty(targetGuid, "OTHER_LDFLAGS", new string[0], new[] { "-ld64" });
            Debug.Log($"[IOSPostProcessBuild] Removed -ld64 from Other Linker Flags on the {targetLabel} " +
                      "(Xcode 26 linker workaround — see CLAUDE.md's iOS build toolchain note).");
        }
    }
}
#endif
