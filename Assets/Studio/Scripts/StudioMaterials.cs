using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Материалы и процедурные текстуры студии (Built-in Render Pipeline, шейдер Standard).
    /// </summary>
    public static class StudioMaterials
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Shader _standard;
        static Shader Standard => _standard != null ? _standard : (_standard = Shader.Find("Standard"));

        public static Material Get(string key, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = key, color = color };
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        /// <summary>Уникальный (не кешированный) материал — для перекрашиваемых объектов.</summary>
        public static Material Unique(string name, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            var m = new Material(Standard) { name = name, color = color };
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material Emissive(string key, Color color, float intensity)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = key, color = color };
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * intensity);
            Cache[key] = m;
            return m;
        }

        public static Material Transparent(string key, Color color)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Sprites/Default")) { name = key, color = color };
            m.renderQueue = 3100;
            Cache[key] = m;
            return m;
        }

        public static Material Textured(string key, Texture2D tex, Color tint, float smoothness = 0.1f, Vector2? tiling = null)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Standard) { name = key, color = tint, mainTexture = tex };
            m.SetFloat("_Glossiness", smoothness);
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            Cache[key] = m;
            return m;
        }

        // ---- Палитра студии (сняты с фото) ----
        public static Material WallBlack => Textured("WallPanels", PanelTexture(), new Color(0.16f, 0.16f, 0.17f), 0.05f, new Vector2(8, 4));
        public static Material Curtain => Get("CurtainBlack", new Color(0.05f, 0.05f, 0.055f), 0.12f);
        public static Material CurtainBeige => Get("CurtainBeige", new Color(0.86f, 0.82f, 0.74f), 0.1f);
        public static Material Floor => Textured("Floor", NoiseTexture(256, 0.72f, 0.05f), new Color(0.78f, 0.78f, 0.77f), 0.35f, new Vector2(6, 6));
        public static Material Ceiling => Get("Ceiling", new Color(0.04f, 0.04f, 0.045f), 0.05f);
        public static Material MetalBlack => Get("MetalBlack", new Color(0.07f, 0.07f, 0.08f), 0.45f, 0.6f);
        public static Material MetalGrey => Get("MetalGrey", new Color(0.55f, 0.56f, 0.58f), 0.5f, 0.7f);
        public static Material PipeYellow => Get("PipeYellow", new Color(0.62f, 0.55f, 0.32f), 0.4f, 0.5f);
        public static Material PlasticBlue => Get("PlasticBlue", new Color(0.12f, 0.45f, 0.85f), 0.5f);
        public static Material PlasticTeal => Get("PlasticTeal", new Color(0.15f, 0.62f, 0.7f), 0.4f);
        public static Material PlasticWhite => Get("PlasticWhite", new Color(0.9f, 0.9f, 0.9f), 0.4f);
        public static Material Rubber => Get("Rubber", new Color(0.03f, 0.03f, 0.03f), 0.1f);
        public static Material Wood => Get("WoodOak", new Color(0.72f, 0.52f, 0.3f), 0.35f);
        public static Material RedBox => Get("RedBox", new Color(0.75f, 0.08f, 0.08f), 0.3f);
        public static Material Glass => Get("LensGlass", new Color(0.02f, 0.02f, 0.03f), 0.95f, 0.2f);
        public static Material DoorDark => Get("DoorDark", new Color(0.02f, 0.02f, 0.02f), 0.05f);
        public static Material WhiteWall => Get("WhiteWall", new Color(0.93f, 0.93f, 0.92f), 0.05f);
        public static Material ExitSign => Emissive("ExitSign", new Color(0.1f, 0.85f, 0.4f), 2f);
        public static Material Screen => Emissive("ScreenSet", new Color(0.45f, 0.75f, 0.85f), 0.8f);
        public static Material TagWhite => Get("TagWhite", new Color(0.95f, 0.95f, 0.95f), 0.2f);

        // ---- Процедурные текстуры ----
        static Texture2D _panel, _shoji, _noise;

        /// <summary>Акустические панели: тёмный шум + швы между плитами.</summary>
        public static Texture2D PanelTexture()
        {
            if (_panel != null) return _panel;
            const int s = 256;
            _panel = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "PanelTex" };
            var rnd = new System.Random(7);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float v = 0.85f + (float)rnd.NextDouble() * 0.25f;
                bool seam = x < 3 || y < 3;
                if (seam) v = 0.35f;
                px[y * s + x] = new Color(v, v, v, 1);
            }
            _panel.SetPixels(px);
            _panel.Apply(true);
            return _panel;
        }

        /// <summary>Рисовая бумага сёдзи с решёткой.</summary>
        public static Texture2D ShojiTexture()
        {
            if (_shoji != null) return _shoji;
            const int w = 128, h = 384;
            _shoji = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "ShojiTex" };
            var rnd = new System.Random(3);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = 0.96f + (float)rnd.NextDouble() * 0.04f;
                bool lattice = (x % 64) < 4 || (y % 64) < 4 || x > w - 4 || y > h - 4;
                px[y * w + x] = lattice ? new Color(0.55f, 0.55f, 0.55f, 1) : new Color(n, n, n * 0.98f, 1);
            }
            _shoji.SetPixels(px);
            _shoji.Apply(true);
            return _shoji;
        }

        static readonly Dictionary<string, Texture2D> RugTex = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Teppichmuster. Helle Muster (border/stripes/plain) werden per Farbe getönt;
        /// "persian" hat eigene Farben (Tönung Weiß = Original).
        /// </summary>
        public static Texture2D RugTexture(string kind)
        {
            if (RugTex.TryGetValue(kind, out var t) && t != null) return t;
            const int w = 512, h = 512;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Rug_" + kind, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var rnd = new System.Random(kind.GetHashCode());
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w, v = (float)y / h;
                float fiber = 0.92f + (float)rnd.NextDouble() * 0.08f; // Flor
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                Color c;
                switch (kind)
                {
                    case "border":
                        c = edge < 0.06f ? new Color(0.55f, 0.55f, 0.55f) : edge < 0.075f ? new Color(0.98f, 0.98f, 0.98f) : new Color(0.85f, 0.85f, 0.85f);
                        break;
                    case "stripes":
                        c = (Mathf.FloorToInt(u * 9) % 2 == 0) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.6f, 0.6f, 0.6f);
                        if (edge < 0.03f) c = new Color(0.45f, 0.45f, 0.45f);
                        break;
                    case "persian":
                    {
                        var red = new Color(0.55f, 0.1f, 0.1f); var navy = new Color(0.1f, 0.13f, 0.3f);
                        var cream = new Color(0.88f, 0.8f, 0.62f); var gold = new Color(0.75f, 0.55f, 0.2f);
                        float cx = u - 0.5f, cy = v - 0.5f;
                        float diamond = Mathf.Abs(cx) * 1.4f + Mathf.Abs(cy);
                        float orn = Mathf.Sin(u * 60f) * Mathf.Sin(v * 60f);
                        if (edge < 0.04f) c = navy;
                        else if (edge < 0.1f) c = (Mathf.Sin((u + v) * 90f) > 0.3f) ? gold : navy;
                        else if (edge < 0.115f) c = cream;
                        else if (diamond < 0.12f) c = navy;
                        else if (diamond < 0.16f) c = gold;
                        else if (diamond < 0.3f) c = orn > 0.4f ? cream : red;
                        else c = orn > 0.65f ? gold : red;
                        break;
                    }
                    default:
                        c = new Color(0.9f, 0.9f, 0.9f);
                        break;
                }
                px[y * w + x] = c * fiber;
            }
            t.SetPixels(px);
            t.Apply(true);
            RugTex[kind] = t;
            return t;
        }

        public static Texture2D NoiseTexture(int s, float baseV, float amp)
        {
            if (_noise != null) return _noise;
            _noise = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "NoiseTex" };
            var rnd = new System.Random(11);
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++)
            {
                float v = baseV + ((float)rnd.NextDouble() - 0.5f) * amp + 0.28f;
                px[i] = new Color(v, v, v, 1);
            }
            _noise.SetPixels(px);
            _noise.Apply(true);
            return _noise;
        }

        /// <summary>Текстура с цифрой для бирки прибора (номера как на фото: 11, 15, 16...).</summary>
        public static Material NumberTag(int number)
        {
            string key = "Tag" + number;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var tex = DigitTexture(number.ToString());
            m = new Material(Standard) { name = key, mainTexture = tex, color = Color.white };
            m.SetFloat("_Glossiness", 0.1f);
            Cache[key] = m;
            return m;
        }

        // 3x5 пиксельный шрифт цифр
        static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
        };

        static Texture2D DigitTexture(string text)
        {
            const int cell = 6, pad = 4;
            int w = pad * 2 + text.Length * 4 * cell, h = pad * 2 + 5 * cell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            for (int c = 0; c < text.Length; c++)
            {
                int d = text[c] - '0';
                if (d < 0 || d > 9) continue;
                string g = Digits[d];
                for (int gy = 0; gy < 5; gy++)
                for (int gx = 0; gx < 3; gx++)
                {
                    if (g[gy * 3 + gx] != '1') continue;
                    for (int yy = 0; yy < cell; yy++)
                    for (int xx = 0; xx < cell; xx++)
                    {
                        int px0 = pad + c * 4 * cell + gx * cell + xx;
                        int py0 = h - 1 - (pad + gy * cell + yy);
                        px[py0 * w + px0] = Color.black;
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
