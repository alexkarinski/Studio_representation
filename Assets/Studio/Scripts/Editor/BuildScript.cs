using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.EditorTools
{
    /// <summary>
    /// Настройки проекта и сборка WebGL. Используется GitHub Actions (game-ci/unity-builder, buildMethod)
    /// и меню Studio → Собрать WebGL.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildScript
    {
        public const string ScenePath = "Assets/Studio/Studio.unity";
        const string KeepDir = "Assets/Studio/Resources/ShaderKeep";

        static BuildScript()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                ApplyPlayerSettings();
                EnsureShaderKeepers();
            };
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Studio Representation";
            PlayerSettings.companyName = "Studio";
            PlayerSettings.runInBackground = true;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            // GitHub Pages не отдаёт заголовки Content-Encoding для .gz — включаем распаковку в JS
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "PROJECT:Fullscreen";
            // объекты собираются из кода (CreatePrimitive и т.п.) — движок не должен вырезать «неиспользуемые» компоненты
            PlayerSettings.stripEngineCode = false;
        }

        /// <summary>
        /// Материалы создаются из кода, а Unity в билде вырезает неиспользуемые варианты шейдеров.
        /// Эти материалы-«якоря» в Resources гарантируют, что нужные варианты Standard попадут в сборку.
        /// </summary>
        public static void EnsureShaderKeepers()
        {
            var standard = Shader.Find("Standard");
            var sprites = Shader.Find("Sprites/Default");
            if (standard == null) { Debug.LogError("Shader Standard не найден"); return; }
            Directory.CreateDirectory(KeepDir);

            void Keep(string name, Shader shader, params string[] keywords)
            {
                var path = $"{KeepDir}/{name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
                var m = new Material(shader);
                foreach (var k in keywords) m.EnableKeyword(k);
                if (keywords.Contains("_EMISSION"))
                {
                    m.SetColor("_EmissionColor", Color.white);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                if (keywords.Contains("_ALPHATEST_ON")) { m.SetFloat("_Mode", 1); m.renderQueue = 2450; }
                AssetDatabase.CreateAsset(m, path);
            }

            Keep("Standard_Base", standard);
            Keep("Standard_Emission", standard, "_EMISSION");
            Keep("Standard_Normal", standard, "_NORMALMAP");
            Keep("Standard_NormalMS", standard, "_NORMALMAP", "_METALLICGLOSSMAP");
            Keep("Standard_NormalMSCutout", standard, "_NORMALMAP", "_METALLICGLOSSMAP", "_ALPHATEST_ON");
            Keep("Standard_Glass", standard, "_ALPHAPREMULTIPLY_ON");
            if (sprites != null) Keep("Sprites_Default", sprites);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Пустая сцена с камерой; студию строит StudioBootstrap при запуске.</summary>
        public static string EnsureScene()
        {
            if (File.Exists(ScenePath)) return ScenePath;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            return ScenePath;
        }

        [MenuItem("Studio/Собрать WebGL (в папку build)")]
        static void BuildFromMenu() => Build("build/WebGL/WebGL");

        /// <summary>Точка входа для CI: -executeMethod Studio.EditorTools.BuildScript.BuildWebGL [-customBuildPath путь].</summary>
        public static void BuildWebGL()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-customBuildPath");
            var path = i >= 0 && i + 1 < args.Length ? args[i + 1] : "build/WebGL/WebGL";
            Build(path);
        }

        static void Build(string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            ApplyPlayerSettings();
            EnsureShaderKeepers();
            var scene = EnsureScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };

            Debug.Log("Сборка WebGL → " + path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = path,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            Debug.Log($"Результат: {report.summary.result}, ошибок: {report.summary.totalErrors}, размер: {report.summary.totalSize / 1048576} МБ");
            if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
