#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace MToolKit.Template.Editor
{
  public static class MTKBuildScript
  {

        private static void ConfigureBackend(ScriptingImplementation backend, NamedBuildTarget namedTarget)
        {
            PlayerSettings.SetScriptingBackend(namedTarget, backend);
            PlayerSettings.SetArchitecture(namedTarget, 1); // 1 = x86_64
        }


        private static void PerformBuild(BuildTarget target, NamedBuildTarget namedTarget, string outputPath, bool development, bool il2cpp)
        {
            var backend = il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;

            ConfigureBackend(backend, namedTarget);
            AddressableAssetSettings.BuildPlayerContent();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MToolKit/Template/Data/Scenes/Bootstrapper.unity" },
                locationPathName = outputPath,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Build failed: {report.summary.result}");
        }

        [MenuItem("Tools/MToolKit/Build/Windows Dev (Mono)")]
        public static void PerformWindowsBuild() =>
            PerformBuild(BuildTarget.StandaloneWindows64, NamedBuildTarget.Standalone, "Builds/Windows-Dev/MToolKitTemplate_Dev.exe", true, false);

        [MenuItem("Tools/MToolKit/Build/Windows Prod (IL2CPP)")]
        public static void PerformWindowsBuildProd() =>
            PerformBuild(BuildTarget.StandaloneWindows64, NamedBuildTarget.Standalone, "Builds/Windows/MToolKitTemplate.exe", false, true);

        [MenuItem("Tools/MToolKit/Build/Linux Dev (Mono)")]
        public static void PerformLinuxBuild() =>
            PerformBuild(BuildTarget.StandaloneLinux64, NamedBuildTarget.Standalone, "Builds/Linux-Dev/MToolKitTemplate_Dev.x86_64", true, false);

        [MenuItem("Tools/MToolKit/Build/Linux Prod (IL2CPP)")]
        public static void PerformLinuxBuildProd() =>
            PerformBuild(BuildTarget.StandaloneLinux64, NamedBuildTarget.Standalone, "Builds/Linux/MToolKitTemplate.x86_64", false, true);
  }

}
#endif