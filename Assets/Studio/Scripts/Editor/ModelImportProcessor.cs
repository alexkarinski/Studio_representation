using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Studio.EditorTools
{
    /// <summary>
    /// Импорт моделей Poly Haven из Assets/Studio/Resources/Models:
    /// нормали помечаются как Normal Map, у карты металл/гладкость отключается sRGB,
    /// а материалы FBX собираются в Standard с альбедо, нормалью и metallic/smoothness.
    /// Текстуры подготовлены скриптом tools/prepare_models.py: &lt;set&gt;_albedo, &lt;set&gt;_normal, &lt;set&gt;_ms.
    /// </summary>
    public class ModelImportProcessor : AssetPostprocessor
    {
        const string Root = "Assets/Studio/Resources/Models/";

        static bool InModels(string path) => path.Replace('\\', '/').StartsWith(Root);

        void OnPreprocessTexture()
        {
            if (!InModels(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            if (name.EndsWith("_normal")) ti.textureType = TextureImporterType.NormalMap;
            else if (name.EndsWith("_ms")) { ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; }
            else if (name.EndsWith("_albedo") && assetPath.EndsWith(".png")) ti.alphaIsTransparency = true;
        }

        void OnPreprocessModel()
        {
            if (!InModels(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.bakeAxisConversion = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            // текстуры должны быть импортированы раньше модели
            foreach (var tex in TexturePaths(assetPath)) context.DependsOnArtifact(tex);
        }

        static string[] TexturePaths(string modelPath)
        {
            var dir = Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/Textures";
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')).ToArray();
        }

        void OnPostprocessMaterial(Material material)
        {
            if (!InModels(assetPath)) return;
            var texs = TexturePaths(assetPath);
            var sets = texs.Where(t => Path.GetFileNameWithoutExtension(t).EndsWith("_albedo"))
                .Select(t => Path.GetFileNameWithoutExtension(t).Replace("_albedo", "")).ToArray();

            string matName = material.name.Trim().ToLowerInvariant().Replace(' ', '_');
            material.shader = Shader.Find("Standard");

            if (matName.Contains("glass"))
            {
                MakeTransparent(material, new Color(0.9f, 0.95f, 1f, 0.12f));
                return;
            }

            string set = sets.FirstOrDefault(s => s == matName);
            if (set == null && sets.Length == 1) set = sets[0];
            if (set == null) set = sets.Where(s => matName.StartsWith(s) || s.StartsWith(matName)).OrderByDescending(s => s.Length).FirstOrDefault();
            if (set == null) return;

            Texture2D Find(string suffix)
            {
                var p = texs.FirstOrDefault(t => Path.GetFileNameWithoutExtension(t) == set + suffix);
                return p != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(p) : null;
            }

            material.color = Color.white;
            var albedo = Find("_albedo");
            if (albedo != null) material.SetTexture("_MainTex", albedo);

            var normal = Find("_normal");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            var ms = Find("_ms");
            if (ms != null)
            {
                material.SetTexture("_MetallicGlossMap", ms);
                material.SetFloat("_GlossMapScale", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Glossiness", 0.3f);
            }

            // листья растений — альфа в альбедо (png) → режим Cutout
            var albedoPath = texs.FirstOrDefault(t => Path.GetFileNameWithoutExtension(t) == set + "_albedo");
            if (albedoPath != null && albedoPath.EndsWith(".png"))
            {
                material.SetFloat("_Mode", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.doubleSidedGI = true;
            }
        }

        static void MakeTransparent(Material m, Color c)
        {
            m.color = c;
            m.SetFloat("_Mode", 3f);
            m.SetFloat("_Glossiness", 0.95f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
