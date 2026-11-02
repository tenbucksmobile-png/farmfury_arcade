#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Sets the name under the Android home screen icon to HomeScreenName, as IOSPostProcessBuild does for iOS. Only
    /// the label changes: productName stays "FarmFuryArcade" because the editor's save location depends on it.
    /// </summary>
    public class ArcadeAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public const string HomeScreenName = "Arcade";

        public int callbackOrder => 100;

        /// <param name="path">The generated unityLibrary folder; the launcher module sits beside it.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string strings = Path.Combine(path, "..", "launcher", "src", "main", "res", "values", "strings.xml");
            if (!File.Exists(strings))
            {
                Debug.LogError("[ArcadeAndroidBuild] " + strings + " is missing; the home screen name was not set.");
                return;
            }

            string xml = File.ReadAllText(strings);
            string updated = Regex.Replace(xml, "(<string name=\"app_name\">).*?(</string>)",
                "${1}" + HomeScreenName + "${2}");
            if (updated == xml && !xml.Contains(">" + HomeScreenName + "<"))
            {
                Debug.LogError("[ArcadeAndroidBuild] No app_name in " + strings + "; the home screen name was not set.");
                return;
            }
            File.WriteAllText(strings, updated);
        }
    }
}
#endif
