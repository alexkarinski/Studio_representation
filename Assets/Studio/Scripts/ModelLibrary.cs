using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// 3D-модели мебели и декора (Poly Haven, CC0) из Resources/Models/&lt;id&gt;/&lt;id&gt;.fbx.
    /// Материалы настраивает ModelImportProcessor при импорте; здесь — подстраховка,
    /// нормализация (опора на пол, центр) и подготовка к перекраске.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public static GameObject Load(string id)
        {
            if (Prefabs.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>($"Models/{id}/{id}");
            Prefabs[id] = p;
            if (p == null) Debug.LogWarning("Модель не найдена: " + id);
            return p;
        }

        /// <summary>Ставит модель внутрь root: низ на y=0, центр по XZ, поворот yaw. Возвращает false, если модели нет.</summary>
        public static bool Build(string id, Transform root, StudioProp prop, float yaw)
        {
            var prefab = Load(id);
            if (prefab == null) return false;

            var pivot = new GameObject("Model").transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(0, yaw, 0);
            var go = Object.Instantiate(prefab, pivot, false);
            go.name = id;

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return true;

            // единицы: Poly Haven — метры, но подстрахуемся от FBX в сантиметрах
            var b = Bounds(rends);
            if (b.size.y > 6f) { go.transform.localScale *= 0.01f; b = Bounds(rends); }
            else if (b.size.y < 0.05f) { go.transform.localScale *= 100f; b = Bounds(rends); }

            var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z) + root.position;
            go.transform.position += offset;

            foreach (var r in rends)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var mats = r.materials; // экземпляры — для независимой перекраски
                foreach (var m in mats)
                {
                    FixMaterial(id, m);
                    string n = m.name.ToLowerInvariant();
                    if (n.Contains("glass")) continue;
                    if (n.Contains("artwork")) { prop.imageMaterial = m; continue; }
                    prop.tintMaterials.Add(m);
                }
                r.materials = mats;
            }
            return true;
        }

        static Bounds Bounds(Renderer[] rends)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Если импорт не подхватил текстуры — догружаем альбедо из Resources.</summary>
        static void FixMaterial(string id, Material m)
        {
            if (m.mainTexture != null) return;
            string set = CleanName(m.name);
            var tex = Resources.Load<Texture2D>($"Models/{id}/Textures/{set}_albedo");
            if (tex == null)
            {
                var all = Resources.LoadAll<Texture2D>($"Models/{id}/Textures");
                int count = 0;
                foreach (var t in all)
                    if (t.name.EndsWith("_albedo")) { tex = t; count++; }
                if (count != 1) tex = null;
            }
            if (tex != null) { m.mainTexture = tex; m.color = Color.white; }
        }

        public static string CleanName(string materialName)
        {
            var n = materialName.Replace(" (Instance)", "").Trim().ToLowerInvariant().Replace(' ', '_');
            return n;
        }
    }
}
