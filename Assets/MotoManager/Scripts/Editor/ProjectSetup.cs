using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotoManager.EditorTools
{
    /// <summary>
    /// One-click project configuration, so ProjectSettings never need to be edited by hand:
    /// Android player settings, the bootstrap scene and the build settings scene list.
    /// Runs automatically the first time the project is opened.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/MotoManager/Scenes/Main.unity";
        public const string BundleId = "com.adridominguez.motomanager";
        private const string DoneKey = "MotoManager.ProjectSetup.v2";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(DoneKey + "|" + Application.dataPath, false)) return;
                ConfigureAll();
                EditorPrefs.SetBool(DoneKey + "|" + Application.dataPath, true);
            };
        }

        [MenuItem("MotoManager/Configure project (Android + scene)")]
        public static void ConfigureAll()
        {
            ConfigureAndroid();
            EnsureScene();
            Debug.Log("MotoManager: project configured for Android.");
        }

        public static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "Adrian Dominguez";
            PlayerSettings.productName = "Motorcycle Motorsport Manager";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // IL2CPP + ARM64: required by Google Play and much faster than Mono for the simulation.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.stripEngineCode = true;
#if UNITY_2019_1_OR_NEWER
            // (Guarded only so the out-of-Unity compile check, which uses 2018 reference assemblies, passes.)
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
#endif

            // Races are viewed in landscape on phones and tablets.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

#if UNITY_2019_1_OR_NEWER
            PlayerSettings.Android.startInFullscreen = true;
            // The game draws behind the notch and rounded corners; the interface keeps its content inside the
            // safe area itself (UI/UiScale.cs, SafeArea), so backgrounds fill the whole screen.
            PlayerSettings.Android.renderOutsideSafeArea = true;
#endif
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.gpuSkinning = false;

            // Light visuals: no need for HDR or high MSAA on mobile.
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 120;
        }

        /// <summary>The scene is intentionally almost empty: everything is created by GameBootstrap at runtime.</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                var camera = cam.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(14, 17, 22, 255);
                cam.AddComponent<AudioListener>();
                var light = new GameObject("Sun");
                var l = light.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(55, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// <summary>Command-line and menu builds. CLI: Unity -batchmode -quit -projectPath . -executeMethod MotoManager.EditorTools.BuildScript.BuildAndroid</summary>
    public static class BuildScript
    {
        [MenuItem("MotoManager/Build Android APK")]
        public static void BuildAndroid()
        {
            ProjectSetup.ConfigureAll();
#if UNITY_2019_1_OR_NEWER
            EditorUserBuildSettings.buildAppBundle = false;
#endif
            Build("Builds/Android/MotoManager.apk");
        }

        [MenuItem("MotoManager/Build Android App Bundle (Play Store)")]
        public static void BuildAndroidBundle()
        {
            ProjectSetup.ConfigureAll();
#if UNITY_2019_1_OR_NEWER
            EditorUserBuildSettings.buildAppBundle = true;
#endif
            Build("Builds/Android/MotoManager.aab");
        }

        /// <summary>
        /// The value after <paramref name="name"/> on the command line ("-mmVersion 0.2.0"), or null. The CI build
        /// (.github/workflows/android.yml) passes the version and, when the repository has them, the signing key.
        /// </summary>
        private static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1]) && !args[i + 1].StartsWith("-")) return args[i + 1];
            return null;
        }

        /// <summary>
        /// Version and signing from the command line: -mmVersion 0.2.0 -mmVersionCode 7, and -mmKeystore path
        /// -mmKeystorePass ... -mmKeyAlias ... -mmKeyPass ... to sign with the project's own key (so every
        /// downloaded APK installs over the previous one). Without a key Unity signs with its debug key.
        /// </summary>
        private static void ApplyCommandLine()
        {
            string version = Arg("-mmVersion");
            if (version != null) PlayerSettings.bundleVersion = version.TrimStart('v');
            int code;
            if (int.TryParse(Arg("-mmVersionCode"), out code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
            string keystore = Arg("-mmKeystore");
            if (keystore != null && File.Exists(keystore))
            {
#if UNITY_2019_1_OR_NEWER
                PlayerSettings.Android.useCustomKeystore = true;
#endif
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = Arg("-mmKeystorePass") ?? "";
                PlayerSettings.Android.keyaliasName = Arg("-mmKeyAlias") ?? "";
                PlayerSettings.Android.keyaliasPass = Arg("-mmKeyPass") ?? "";
            }
        }

        private static void Build(string path)
        {
            ApplyCommandLine();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("MotoManager build: " + report.summary.result + " -> " + path + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
