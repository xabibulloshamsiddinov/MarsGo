using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MarsGo.EditorTools
{
    /// <summary>
    /// One-click project setup: materials, Mars sky, post-processing, the Main scene and build
    /// settings. Menu: MarsGo/Setup Project. Batch: -executeMethod MarsGo.EditorTools.MarsGoSetup.Run
    /// </summary>
    public static class MarsGoSetup
    {
        const string Root = "Assets/Scenes/MarsGo";
        const string MaterialDir = Root + "/Resources/" + MarsMaterials.ResourceFolder;
        const string ArtDir = Root + "/Art";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("MarsGo/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();

            CreateMaterials();
            CreateParticleMaterials();
            ConfigureAudioImport();
            var sky = CreateSkybox();
            var profile = CreateVolumeProfile();
            CreateScene(sky, profile);
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("MarsGo setup complete: " + ScenePath);
        }

        /// <summary>Soft-dot texture + additive / alpha particle materials (URP Particles/Unlit).</summary>
        static void CreateParticleMaterials()
        {
            string texPath = $"{MaterialDir}/{FxAssets.SoftDotName}.png";
            File.WriteAllBytes(texPath, FxAssets.MakeSoftDot(64).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            foreach (var (name, additive) in new[] { (FxAssets.AdditiveName, true), (FxAssets.AlphaName, false) })
            {
                string path = $"{MaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find(FxAssets.ParticleShader)) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                FxAssets.Configure(mat, additive, tex);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>Music / ambience stream from disk (small memory); short SFX are decompressed on load.</summary>
        static void ConfigureAudioImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Resources/MarsGo/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool stream = path.Contains("/Music/") || path.Contains("/Ambience/") && path.Contains("_loop");
                var settings = importer.defaultSampleSettings;
                settings.loadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = stream ? 0.6f : 0.8f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        static void CreateMaterials()
        {
            foreach (var spec in MarsMaterials.Specs)
            {
                string path = $"{MaterialDir}/{spec.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find(MarsMaterials.LitShader)) { name = spec.Name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                MarsMaterials.Apply(mat, spec);
                EditorUtility.SetDirty(mat);
            }
        }

        static Material CreateSkybox()
        {
            string path = ArtDir + "/MarsSky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("MarsGo/MarsSky");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            // Small sun high on the left, behind the level, so its blue halo is in view.
            mat.SetVector("_SunDir", new Vector4(-0.45f, 0.32f, 0.84f, 0f));
            mat.SetColor("_Zenith", new Color(0.58f, 0.36f, 0.28f));
            mat.SetColor("_Horizon", new Color(0.93f, 0.70f, 0.52f));
            mat.SetColor("_Ground", new Color(0.55f, 0.30f, 0.20f));
            mat.SetFloat("_Exponent", 0.55f);
            mat.SetColor("_SunColor", new Color(1f, 0.97f, 0.92f));
            mat.SetFloat("_SunSize", 0.9993f);
            mat.SetColor("_Halo", new Color(0.62f, 0.72f, 0.9f));
            mat.SetFloat("_HaloPower", 40f);
            mat.SetFloat("_HaloStrength", 0.55f);
            mat.SetVector("_Moon1Dir", new Vector4(0.36f, 0.34f, 0.87f, 0f));
            mat.SetFloat("_Moon1Size", 0.013f);
            mat.SetVector("_Moon2Dir", new Vector4(0.02f, 0.33f, 0.94f, 0f));
            mat.SetFloat("_Moon2Size", 0.007f);
            // Pale and low-contrast: seen through Mars' dusty air.
            mat.SetColor("_MoonLit", new Color(0.9f, 0.76f, 0.64f));
            mat.SetColor("_MoonDark", new Color(0.7f, 0.5f, 0.4f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile CreateVolumeProfile()
        {
            string path = ArtDir + "/MarsPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) AssetDatabase.DeleteAsset(path);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.saturation.Override(8f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.45f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void CreateScene(Material sky, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1200f;
            camGo.transform.position = new Vector3(0f, 3f, -22f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<GameCamera>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.72f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var volGo = new GameObject("PostProcess");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var game = new GameObject("Game");
            game.AddComponent<GameManager>();
            game.AddComponent<DebugHud>();
            game.AddComponent<AutoScreenshot>();

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.6f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.38f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.13f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.88f, 0.64f, 0.48f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 420f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Urmonov";
            PlayerSettings.productName = "MarsGo";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.urmonov.marsgo");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.urmonov.marsgo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.bundleVersion = "0.1.0";
        }

        /// <summary>Batch: -executeMethod MarsGo.EditorTools.MarsGoSetup.BuildWindows</summary>
        [MenuItem("MarsGo/Build Windows")]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/MarsGo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"MarsGo Windows build: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.outputPath}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
