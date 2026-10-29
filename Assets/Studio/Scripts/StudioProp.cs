using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Редактируемый объект оформления: можно выбрать, двигать, вращать, перекрашивать,
    /// а на экраны/панели — загружать картинку (логотип клиента, заставку).
    /// </summary>
    public class StudioProp : MonoBehaviour
    {
        public string typeId;
        public string displayName;
        public Color color = Color.white;
        public string imagePath = "";

        /// <summary>Материалы, которые перекрашиваются при смене цвета.</summary>
        public List<Material> tintMaterials = new List<Material>();
        /// <summary>true — цвет меняет свечение (неон), а не albedo.</summary>
        public bool tintIsEmission;
        /// <summary>Материал экрана/панели, на который можно повесить картинку.</summary>
        public Material imageMaterial;

        public bool HasColor => tintMaterials.Count > 0;

        public bool castShadows = true;

        /// <summary>Schattenwurf des Objekts an/aus (z. B. Stativ, das keinen Schatten werfen soll).</summary>
        public void SetCastShadows(bool on)
        {
            castShadows = on;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (!(r is LineRenderer)) r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public bool HasImage => imageMaterial != null;

        public void SetColor(Color c)
        {
            color = c;
            foreach (var m in tintMaterials)
            {
                if (m == null) continue;
                if (tintIsEmission)
                {
                    m.color = c;
                    m.SetColor("_EmissionColor", c * 2.5f);
                }
                else m.color = c;
            }
            foreach (var l in GetComponentsInChildren<Light>())
                if (tintIsEmission) l.color = c;
        }

        public bool SetImage(string path)
        {
            if (imageMaterial == null) return false;
            if (string.IsNullOrEmpty(path))
            {
                imagePath = "";
                ApplyTexture(imageMaterial, null);
                return true;
            }
            var tex = LoadTexture(path);
            if (tex == null) return false;
            imagePath = path;
            ApplyTexture(imageMaterial, tex);
            return true;
        }

        public static Texture2D LoadTexture(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileName(path) };
                if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 4;
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Не удалось загрузить картинку: " + e.Message);
                return null;
            }
        }

        public static void ApplyTexture(Material m, Texture2D tex)
        {
            m.mainTexture = tex;
            if (m.IsKeywordEnabled("_EMISSION"))
            {
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", tex != null ? Color.white * 0.9f : m.color * 0.8f);
            }
            if (tex != null) m.color = Color.white;
        }
    }
}
