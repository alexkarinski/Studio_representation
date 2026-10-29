using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Lädt eigene 3D-Modelle im Format glTF-Binary (.glb) zur Laufzeit — auch im Browser.
    /// Unterstützt: Meshes (Dreiecke), Knoten-Hierarchie, Basisfarbe + Textur, Metallic/Roughness-Faktoren,
    /// Emission, Alpha (MASK/BLEND). Nicht unterstützt: Draco/Meshopt-Kompression, Animationen, Skinning.
    /// </summary>
    public static class GlbLoader
    {
        public const string FormatHint =
            "Format: glTF-Binary (.glb), bis ca. 50 MB, Maßeinheit Meter, Y-Achse nach oben, ohne Draco-Kompression. " +
            "Exporte aus Blender (Datei → Export → glTF 2.0, Format „GLB“), SketchUp, Sketchfab-Downloads („glTF“) funktionieren.";

        public class GlbException : Exception { public GlbException(string m) : base(m) { } }

        class Ctx
        {
            public Dictionary<string, object> json;
            public byte[] bin;
            public Material[] materials;
            public Texture2D[] images;
        }

        public static GameObject Load(byte[] data, string name)
        {
            if (data == null || data.Length < 20 || BitConverter.ToUInt32(data, 0) != 0x46546C67) // "glTF"
                throw new GlbException("Keine .glb-Datei (glTF-Binary). Bitte als GLB exportieren.");
            int length = (int)BitConverter.ToUInt32(data, 8);
            int pos = 12;
            string jsonText = null;
            byte[] bin = null;
            while (pos + 8 <= Math.Min(length, data.Length))
            {
                int chunkLen = (int)BitConverter.ToUInt32(data, pos);
                uint type = BitConverter.ToUInt32(data, pos + 4);
                if (type == 0x4E4F534A) jsonText = Encoding.UTF8.GetString(data, pos + 8, chunkLen);
                else if (type == 0x004E4942) { bin = new byte[chunkLen]; Buffer.BlockCopy(data, pos + 8, bin, 0, chunkLen); }
                pos += 8 + chunkLen;
            }
            if (jsonText == null) throw new GlbException("GLB ohne JSON-Teil.");
            var ctx = new Ctx { json = MiniJson.Parse(jsonText) as Dictionary<string, object>, bin = bin };

            var required = MiniJson.A(ctx.json, "extensionsRequired");
            if (required != null)
                foreach (var r in required)
                    if (r is string ext && (ext.Contains("draco") || ext.Contains("meshopt")))
                        throw new GlbException("Komprimiertes Modell (" + ext + ") wird nicht unterstützt. Bitte ohne Kompression exportieren.");

            LoadImages(ctx);
            LoadMaterials(ctx);

            var root = new GameObject(name);
            var nodes = MiniJson.A(ctx.json, "nodes") ?? new List<object>();
            var scenes = MiniJson.A(ctx.json, "scenes");
            int sceneIndex = Math.Max(0, MiniJson.I(ctx.json, "scene", 0));
            List<object> rootNodes = null;
            if (scenes != null && sceneIndex < scenes.Count) rootNodes = MiniJson.A(scenes[sceneIndex], "nodes");
            if (rootNodes == null)
            {
                // ohne Szene: alle Knoten, die kein Kind sind
                var children = new HashSet<int>();
                foreach (var n in nodes) { var ch = MiniJson.A(n, "children"); if (ch != null) foreach (var c in ch) children.Add((int)(double)c); }
                rootNodes = new List<object>();
                for (int i = 0; i < nodes.Count; i++) if (!children.Contains(i)) rootNodes.Add((double)i);
            }
            foreach (var n in rootNodes) BuildNode(ctx, nodes, (int)(double)n, root.transform, 0);
            if (root.GetComponentsInChildren<MeshFilter>().Length == 0)
            {
                UnityEngine.Object.Destroy(root);
                throw new GlbException("Im Modell wurden keine Dreiecks-Meshes gefunden.");
            }
            return root;
        }

        static void BuildNode(Ctx ctx, List<object> nodes, int index, Transform parent, int depth)
        {
            if (index < 0 || index >= nodes.Count || depth > 64) return;
            var n = nodes[index];
            var go = new GameObject(MiniJson.S(n, "name") ?? "Node" + index);
            go.transform.SetParent(parent, false);

            var m = MiniJson.Floats(n, "matrix");
            if (m != null && m.Length == 16)
            {
                var mat = new Matrix4x4();
                for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) mat[r, c] = m[c * 4 + r];
                var flip = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                mat = flip * mat * flip;
                go.transform.localPosition = mat.GetColumn(3);
                var fwd = (Vector3)mat.GetColumn(2); var up = (Vector3)mat.GetColumn(1);
                if (fwd.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f) go.transform.localRotation = Quaternion.LookRotation(fwd, up);
                go.transform.localScale = new Vector3(((Vector3)mat.GetColumn(0)).magnitude, up.magnitude, fwd.magnitude);
            }
            else
            {
                var t = MiniJson.Floats(n, "translation");
                var q = MiniJson.Floats(n, "rotation");
                var s = MiniJson.Floats(n, "scale");
                if (t != null && t.Length == 3) go.transform.localPosition = new Vector3(-t[0], t[1], t[2]);
                if (q != null && q.Length == 4) go.transform.localRotation = new Quaternion(q[0], -q[1], -q[2], q[3]);
                if (s != null && s.Length == 3) go.transform.localScale = new Vector3(s[0], s[1], s[2]);
            }

            int meshIndex = MiniJson.I(n, "mesh");
            if (meshIndex >= 0) BuildMesh(ctx, meshIndex, go.transform);

            var children = MiniJson.A(n, "children");
            if (children != null) foreach (var c in children) BuildNode(ctx, nodes, (int)(double)c, go.transform, depth + 1);
        }

        static void BuildMesh(Ctx ctx, int meshIndex, Transform parent)
        {
            var meshes = MiniJson.A(ctx.json, "meshes");
            if (meshes == null || meshIndex >= meshes.Count) return;
            var prims = MiniJson.A(meshes[meshIndex], "primitives");
            if (prims == null) return;
            int pi = 0;
            foreach (var p in prims)
            {
                int mode = MiniJson.I(p, "mode", 4);
                if (mode != 4) continue; // nur Dreiecke
                var attrs = MiniJson.O(p, "attributes");
                int posAcc = MiniJson.I(attrs, "POSITION");
                if (posAcc < 0) continue;

                var posF = ReadFloats(ctx, posAcc, out int posComps);
                int vcount = posF.Length / posComps;
                var verts = new Vector3[vcount];
                for (int i = 0; i < vcount; i++) verts[i] = new Vector3(-posF[i * 3], posF[i * 3 + 1], posF[i * 3 + 2]);

                Vector3[] normals = null;
                int nAcc = MiniJson.I(attrs, "NORMAL");
                if (nAcc >= 0)
                {
                    var nf = ReadFloats(ctx, nAcc, out _);
                    if (nf.Length == vcount * 3)
                    {
                        normals = new Vector3[vcount];
                        for (int i = 0; i < vcount; i++) normals[i] = new Vector3(-nf[i * 3], nf[i * 3 + 1], nf[i * 3 + 2]);
                    }
                }
                Vector2[] uvs = null;
                int uvAcc = MiniJson.I(attrs, "TEXCOORD_0");
                if (uvAcc >= 0)
                {
                    var uf = ReadFloats(ctx, uvAcc, out _);
                    if (uf.Length == vcount * 2)
                    {
                        uvs = new Vector2[vcount];
                        for (int i = 0; i < vcount; i++) uvs[i] = new Vector2(uf[i * 2], 1f - uf[i * 2 + 1]);
                    }
                }
                Color[] colors = null;
                int colAcc = MiniJson.I(attrs, "COLOR_0");
                if (colAcc >= 0)
                {
                    var cf = ReadFloats(ctx, colAcc, out int cc);
                    if (cf.Length == vcount * cc && cc >= 3)
                    {
                        colors = new Color[vcount];
                        for (int i = 0; i < vcount; i++) colors[i] = new Color(cf[i * cc], cf[i * cc + 1], cf[i * cc + 2], cc == 4 ? cf[i * cc + 3] : 1f);
                    }
                }

                int[] idx;
                int iAcc = MiniJson.I(p, "indices");
                if (iAcc >= 0) idx = ReadInts(ctx, iAcc);
                else { idx = new int[vcount]; for (int i = 0; i < vcount; i++) idx[i] = i; }
                // Händigkeit gewechselt → Wicklung umdrehen
                for (int i = 0; i + 2 < idx.Length; i += 3) { int t = idx[i + 1]; idx[i + 1] = idx[i + 2]; idx[i + 2] = t; }

                var mesh = new Mesh { name = parent.name + "_" + pi };
                if (vcount > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = verts;
                if (uvs != null) mesh.uv = uvs;
                if (colors != null) mesh.colors = colors;
                mesh.triangles = idx;
                if (normals != null) mesh.normals = normals; else mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject("Primitive" + pi);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                int matIndex = MiniJson.I(p, "material");
                go.AddComponent<MeshRenderer>().sharedMaterial = matIndex >= 0 && matIndex < ctx.materials.Length ? ctx.materials[matIndex] : DefaultMaterial();
                pi++;
            }
        }

        static Material _default;
        static Material DefaultMaterial() => _default != null ? _default : (_default = StudioMaterials.Unique("GlbDefault", new Color(0.8f, 0.8f, 0.8f), 0.3f));

        static void LoadImages(Ctx ctx)
        {
            var images = MiniJson.A(ctx.json, "images");
            ctx.images = new Texture2D[images?.Count ?? 0];
            if (images == null) return;
            for (int i = 0; i < images.Count; i++)
            {
                byte[] bytes = null;
                int bv = MiniJson.I(images[i], "bufferView");
                if (bv >= 0) bytes = BufferViewBytes(ctx, bv);
                else
                {
                    var uri = MiniJson.S(images[i], "uri");
                    if (uri != null && uri.StartsWith("data:"))
                    {
                        int comma = uri.IndexOf(',');
                        if (comma > 0) bytes = Convert.FromBase64String(uri.Substring(comma + 1));
                    }
                }
                if (bytes == null) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(bytes)) { tex.wrapMode = TextureWrapMode.Repeat; tex.anisoLevel = 4; ctx.images[i] = tex; }
            }
        }

        static Texture2D TextureFor(Ctx ctx, object texInfo)
        {
            int ti = MiniJson.I(texInfo, "index");
            var textures = MiniJson.A(ctx.json, "textures");
            if (ti < 0 || textures == null || ti >= textures.Count) return null;
            int src = MiniJson.I(textures[ti], "source");
            return src >= 0 && src < ctx.images.Length ? ctx.images[src] : null;
        }

        static void LoadMaterials(Ctx ctx)
        {
            var mats = MiniJson.A(ctx.json, "materials");
            ctx.materials = new Material[mats?.Count ?? 0];
            if (mats == null) return;
            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                var pbr = MiniJson.O(m, "pbrMetallicRoughness");
                var bc = MiniJson.Floats(pbr, "baseColorFactor");
                var color = bc != null && bc.Length == 4 ? new Color(bc[0], bc[1], bc[2], bc[3]) : Color.white;
                // glTF-Farben sind linear
                color = new Color(Mathf.LinearToGammaSpace(color.r), Mathf.LinearToGammaSpace(color.g), Mathf.LinearToGammaSpace(color.b), color.a);
                float metallic = MiniJson.F(pbr, "metallicFactor", 1f);
                float rough = MiniJson.F(pbr, "roughnessFactor", 1f);
                var mat = StudioMaterials.Unique(MiniJson.S(m, "name") ?? "GlbMat" + i, color, 1f - rough, metallic);
                var tex = TextureFor(ctx, MiniJson.O(pbr, "baseColorTexture"));
                if (tex != null) mat.mainTexture = tex;
                // ohne Metallic/Roughness-Textur wirken Faktor 1 = voll metallisch oft falsch dunkel
                if (MiniJson.O(pbr, "metallicRoughnessTexture") != null) mat.SetFloat("_Metallic", Mathf.Min(metallic, 0.5f));

                var em = MiniJson.Floats(m, "emissiveFactor");
                if (em != null && em.Length == 3 && (em[0] + em[1] + em[2]) > 0.01f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(em[0], em[1], em[2]));
                    var et = TextureFor(ctx, MiniJson.O(m, "emissiveTexture"));
                    if (et != null) mat.SetTexture("_EmissionMap", et);
                }

                var alphaMode = MiniJson.S(m, "alphaMode");
                if (alphaMode == "MASK")
                {
                    mat.SetFloat("_Mode", 1f);
                    mat.SetFloat("_Cutoff", MiniJson.F(m, "alphaCutoff", 0.5f));
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = 2450;
                }
                else if (alphaMode == "BLEND")
                {
                    mat.SetFloat("_Mode", 2f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                }
                ctx.materials[i] = mat;
            }
        }

        // ---------- Accessors ----------

        static byte[] BufferViewBytes(Ctx ctx, int bvIndex)
        {
            var bvs = MiniJson.A(ctx.json, "bufferViews");
            if (bvs == null || bvIndex >= bvs.Count || ctx.bin == null) return null;
            var bv = bvs[bvIndex];
            int off = Math.Max(0, MiniJson.I(bv, "byteOffset", 0)), len = MiniJson.I(bv, "byteLength", 0);
            if (off + len > ctx.bin.Length) return null;
            var r = new byte[len];
            Buffer.BlockCopy(ctx.bin, off, r, 0, len);
            return r;
        }

        static int Comps(string type)
        {
            switch (type) { case "SCALAR": return 1; case "VEC2": return 2; case "VEC3": return 3; case "VEC4": return 4; case "MAT4": return 16; default: return 1; }
        }

        static int CompSize(int ct) => ct == 5126 || ct == 5125 ? 4 : ct == 5123 || ct == 5122 ? 2 : 1;

        static float ReadComp(byte[] b, int o, int ct, bool norm)
        {
            switch (ct)
            {
                case 5126: return BitConverter.ToSingle(b, o);
                case 5125: return BitConverter.ToUInt32(b, o);
                case 5123: { float v = BitConverter.ToUInt16(b, o); return norm ? v / 65535f : v; }
                case 5122: { float v = BitConverter.ToInt16(b, o); return norm ? Mathf.Max(v / 32767f, -1f) : v; }
                case 5121: { float v = b[o]; return norm ? v / 255f : v; }
                case 5120: { float v = (sbyte)b[o]; return norm ? Mathf.Max(v / 127f, -1f) : v; }
                default: return 0f;
            }
        }

        static float[] ReadFloats(Ctx ctx, int accIndex, out int comps)
        {
            var accs = MiniJson.A(ctx.json, "accessors");
            var acc = accs[accIndex];
            comps = Comps(MiniJson.S(acc, "type"));
            int count = MiniJson.I(acc, "count", 0), ct = MiniJson.I(acc, "componentType", 5126);
            // ganzzahlige UVs/Farben sind in der Praxis normalisiert
            bool norm = ct != 5126 && ct != 5125;
            var result = new float[count * comps];
            int bvIndex = MiniJson.I(acc, "bufferView");
            if (bvIndex < 0 || ctx.bin == null) return result;
            var bv = MiniJson.A(ctx.json, "bufferViews")[bvIndex];
            int baseOff = Math.Max(0, MiniJson.I(bv, "byteOffset", 0)) + Math.Max(0, MiniJson.I(acc, "byteOffset", 0));
            int cs = CompSize(ct);
            int stride = MiniJson.I(bv, "byteStride", 0);
            if (stride <= 0) stride = cs * comps;
            for (int i = 0; i < count; i++)
                for (int c = 0; c < comps; c++)
                {
                    int o = baseOff + i * stride + c * cs;
                    if (o + cs > ctx.bin.Length) return result;
                    result[i * comps + c] = ReadComp(ctx.bin, o, ct, norm);
                }
            return result;
        }

        static int[] ReadInts(Ctx ctx, int accIndex)
        {
            var accs = MiniJson.A(ctx.json, "accessors");
            var acc = accs[accIndex];
            int count = MiniJson.I(acc, "count", 0), ct = MiniJson.I(acc, "componentType", 5125);
            var result = new int[count];
            int bvIndex = MiniJson.I(acc, "bufferView");
            if (bvIndex < 0 || ctx.bin == null) return result;
            var bv = MiniJson.A(ctx.json, "bufferViews")[bvIndex];
            int baseOff = Math.Max(0, MiniJson.I(bv, "byteOffset", 0)) + Math.Max(0, MiniJson.I(acc, "byteOffset", 0));
            int cs = CompSize(ct);
            for (int i = 0; i < count; i++)
            {
                int o = baseOff + i * cs;
                if (o + cs > ctx.bin.Length) break;
                result[i] = ct == 5125 ? (int)BitConverter.ToUInt32(ctx.bin, o) : ct == 5123 ? BitConverter.ToUInt16(ctx.bin, o) : ctx.bin[o];
            }
            return result;
        }
    }
}
