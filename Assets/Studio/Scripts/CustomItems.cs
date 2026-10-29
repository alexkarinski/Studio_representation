using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Eigene Objekte: 3D-Modelle (.glb) und Bilder als freigestellte Aufsteller (.png/.jpg).
    /// Dateien liegen in persistentDataPath/Custom (im Browser: IndexedDB), Typ-ID „custom:dateiname“.
    /// </summary>
    public static class CustomItems
    {
        public const string Prefix = "custom:";
        public const string ImageHint =
            "Bild als Aufsteller: PNG (mit Transparenz → freigestellt) oder JPG, z. B. Logo, Pappaufsteller, Roll-up. " +
            "Höhe 1,8 m (Querformat: 2 m breit); danach frei verschieb- und drehbar.";

        public static string Dir => Path.Combine(Application.persistentDataPath, "Custom");

        public static bool IsCustom(string id) => id != null && id.StartsWith(Prefix);
        public static string FileOf(string id) => Path.Combine(Dir, id.Substring(Prefix.Length));
        public static string DisplayName(string id) => Path.GetFileNameWithoutExtension(id.Substring(Prefix.Length));

        static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>();

        /// <summary>Datei ablegen und Typ-ID zurückgeben.</summary>
        public static string Store(string fileName, byte[] bytes)
        {
            Directory.CreateDirectory(Dir);
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            File.WriteAllBytes(Path.Combine(Dir, fileName), bytes);
            Cache.Remove(fileName);
            WebBridge.SyncFS();
            return Prefix + fileName;
        }

        public static List<string> All()
        {
            if (!Directory.Exists(Dir)) return new List<string>();
            return Directory.GetFiles(Dir)
                .Where(f => f.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase) || IsImage(f))
                .OrderBy(f => f).Select(f => Prefix + Path.GetFileName(f)).ToList();
        }

        public static void Delete(string id)
        {
            var f = FileOf(id);
            if (File.Exists(f)) File.Delete(f);
            Cache.Remove(id.Substring(Prefix.Length));
            WebBridge.SyncFS();
        }

        static bool IsImage(string f) => f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Baut das Objekt in root. Wirft eine Ausnahme mit verständlicher Meldung bei Problemen.</summary>
        public static void Build(string id, Transform root, StudioProp prop)
        {
            var file = FileOf(id);
            var key = id.Substring(Prefix.Length);
            if (!Cache.TryGetValue(key, out var bytes))
            {
                if (!File.Exists(file)) throw new GlbLoader.GlbException("Datei fehlt in diesem Browser: " + key);
                bytes = File.ReadAllBytes(file);
                Cache[key] = bytes;
            }
            if (IsImage(file)) BuildStandee(bytes, root, prop);
            else BuildModel(bytes, key, root, prop);
        }

        static void BuildModel(byte[] bytes, string name, Transform root, StudioProp prop)
        {
            var model = GlbLoader.Load(bytes, name);
            model.transform.SetParent(root, false);
            var rends = model.GetComponentsInChildren<Renderer>();
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float scale = 1f;
            if (b.size.y > 20f) scale = 0.01f;          // vermutlich Zentimeter
            else if (b.size.magnitude < 0.02f) scale = 100f;
            if (scale != 1f)
            {
                model.transform.localScale *= scale;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
            }
            model.transform.position += root.position - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var r in rends)
            {
                var mats = r.materials;
                prop.tintMaterials.AddRange(mats);
                r.materials = mats;
            }
        }

        static void BuildStandee(byte[] bytes, Transform root, StudioProp prop)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(bytes)) throw new GlbLoader.GlbException("Bild konnte nicht gelesen werden (PNG oder JPG verwenden).");
            tex.wrapMode = TextureWrapMode.Clamp;
            float aspect = (float)tex.width / Mathf.Max(1, tex.height);
            float h = aspect > 1f ? 2f / aspect : 1.8f, w = h * aspect;

            var mat = StudioMaterials.Unique("Aufsteller", Color.white, 0.15f);
            mat.mainTexture = tex;
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            prop.tintMaterials.Add(mat);

            Geo.Quad("Front", root, new Vector3(0, h / 2 + 0.03f, 0.004f), new Vector2(w, h), mat, new Vector3(0, 180, 0));
            Geo.Quad("Back", root, new Vector3(0, h / 2 + 0.03f, -0.004f), new Vector2(w, h), mat, Vector3.zero);
            Geo.Box("Foot", root, new Vector3(0, 0.015f, 0), new Vector3(Mathf.Min(w, 0.6f), 0.03f, 0.3f), StudioMaterials.MetalBlack);
        }
    }
}
