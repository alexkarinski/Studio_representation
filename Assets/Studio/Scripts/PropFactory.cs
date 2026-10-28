using System;
using System.Collections.Generic;
using UnityEngine;
using M = Studio.StudioMaterials;

namespace Studio
{
    /// <summary>Каталог объектов оформления и техники. Всё строится из примитивов, без внешних ассетов.</summary>
    public static class PropFactory
    {
        public class Entry
        {
            public string id, name, category;
            public Color defaultColor;
            public Func<Transform, StudioProp, Color, GameObject> build;
        }

        public static readonly List<Entry> Catalog = new List<Entry>
        {
            new Entry { id = "armchair",    name = "Кресло",               category = "Мебель",     defaultColor = new Color(0.62f, 0.62f, 0.60f), build = Armchair },
            new Entry { id = "sofa",        name = "Диван",                category = "Мебель",     defaultColor = new Color(0.25f, 0.32f, 0.45f), build = Sofa },
            new Entry { id = "table_round", name = "Столик круглый",       category = "Мебель",     defaultColor = new Color(0.72f, 0.52f, 0.30f), build = RoundTable },
            new Entry { id = "news_desk",   name = "Стол ведущего",        category = "Мебель",     defaultColor = new Color(0.92f, 0.92f, 0.92f), build = NewsDesk },
            new Entry { id = "stool",       name = "Барный стул",          category = "Мебель",     defaultColor = new Color(0.10f, 0.10f, 0.10f), build = Stool },
            new Entry { id = "bookshelf",   name = "Стеллаж",              category = "Мебель",     defaultColor = new Color(0.72f, 0.52f, 0.30f), build = Bookshelf },
            new Entry { id = "shoji",       name = "Ширма сёдзи",          category = "Декор",      defaultColor = new Color(0.80f, 0.45f, 0.15f), build = Shoji },
            new Entry { id = "plant",       name = "Растение",             category = "Декор",      defaultColor = new Color(0.20f, 0.45f, 0.20f), build = Plant },
            new Entry { id = "rug",         name = "Ковёр круглый",        category = "Декор",      defaultColor = new Color(0.55f, 0.35f, 0.30f), build = Rug },
            new Entry { id = "podium",      name = "Подиум",               category = "Декор",      defaultColor = new Color(0.12f, 0.12f, 0.13f), build = Podium },
            new Entry { id = "logo_panel",  name = "Панель с логотипом",   category = "Декор",      defaultColor = new Color(0.95f, 0.95f, 0.95f), build = LogoPanel },
            new Entry { id = "neon_ring",   name = "Неоновое кольцо",      category = "Декор",      defaultColor = new Color(1.00f, 0.25f, 0.60f), build = NeonRing },
            new Entry { id = "floor_lamp",  name = "Торшер",               category = "Декор",      defaultColor = new Color(1.00f, 0.80f, 0.55f), build = FloorLamp },
            new Entry { id = "led_wall",    name = "LED-экран 4×2.25 м",   category = "Техника",    defaultColor = new Color(0.30f, 0.55f, 0.90f), build = LedWall },
            new Entry { id = "tv_stand",    name = "Монитор на стойке",    category = "Техника",    defaultColor = new Color(0.45f, 0.75f, 0.85f), build = TvStand },
            new Entry { id = "studio_cam",  name = "Камера на штативе",    category = "Техника",    defaultColor = new Color(0.08f, 0.08f, 0.09f), build = TripodCamera },
            new Entry { id = "pedestal_cam",name = "Камера на пьедестале", category = "Техника",    defaultColor = new Color(0.75f, 0.76f, 0.78f), build = PedestalCamera },
        };

        public static Entry Find(string id) => Catalog.Find(e => e.id == id);

        public static StudioProp Create(string id, Transform parent, Vector3 pos, float rotY, Color? color = null)
        {
            var e = Find(id);
            if (e == null) { Debug.LogWarning("Неизвестный объект: " + id); return null; }
            var root = new GameObject(e.name);
            root.transform.SetParent(parent, false);
            var prop = root.AddComponent<StudioProp>();
            prop.typeId = id;
            prop.displayName = e.name;
            var c = color ?? e.defaultColor;
            e.build(root.transform, prop, c);
            prop.color = c;
            Geo.FitCollider(root);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            return prop;
        }

        static Material Tint(StudioProp p, string name, Color c, float smooth = 0.2f)
        {
            var m = M.Unique(name, c, smooth);
            p.tintMaterials.Add(m);
            return m;
        }

        static Material ScreenMat(StudioProp p, Color c)
        {
            var m = M.Unique("Screen", c, 0.8f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.8f);
            m.mainTexture = DefaultScreenTexture();
            m.SetTexture("_EmissionMap", m.mainTexture);
            p.imageMaterial = m;
            return m;
        }

        static Texture2D _screenTex;
        static Texture2D DefaultScreenTexture()
        {
            if (_screenTex != null) return _screenTex;
            const int w = 256, h = 144;
            _screenTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "ScreenDefault", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = (float)y / h;
                var c = Color.Lerp(new Color(0.55f, 0.85f, 0.95f), new Color(0.2f, 0.45f, 0.7f), t);
                // мягкие "окна" ширм как на эфирной картинке с фото
                if ((x > 30 && x < 60 && y > 30 && y < 110) || (x > 196 && x < 226 && y > 30 && y < 110)) c *= 1.15f;
                px[y * w + x] = c;
            }
            _screenTex.SetPixels(px);
            _screenTex.Apply(true);
            return _screenTex;
        }

        // ================= Мебель =================

        static GameObject Armchair(Transform r, StudioProp p, Color c)
        {
            var fab = Tint(p, "Upholstery", c, 0.05f);
            Geo.Box("Seat", r, new Vector3(0, 0.45f, 0.02f), new Vector3(0.62f, 0.12f, 0.56f), fab);
            Geo.Box("Back", r, new Vector3(0, 0.72f, -0.24f), new Vector3(0.64f, 0.46f, 0.1f), fab, new Vector3(-10, 0, 0));
            Geo.Box("ArmL", r, new Vector3(-0.31f, 0.6f, -0.08f), new Vector3(0.08f, 0.22f, 0.36f), fab, new Vector3(0, 12, 0));
            Geo.Box("ArmR", r, new Vector3(0.31f, 0.6f, -0.08f), new Vector3(0.08f, 0.22f, 0.36f), fab, new Vector3(0, -12, 0));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1 : 1, sz = (i & 2) == 0 ? -1 : 1;
                Geo.Cyl("Leg", r, new Vector3(sx * 0.25f, 0.2f, sz * 0.21f), 0.018f, 0.42f, M.Wood, new Vector3(sz * 10, 0, -sx * 10));
            }
            return r.gameObject;
        }

        static GameObject Sofa(Transform r, StudioProp p, Color c)
        {
            var fab = Tint(p, "Upholstery", c, 0.05f);
            Geo.Box("Base", r, new Vector3(0, 0.25f, 0), new Vector3(1.9f, 0.3f, 0.85f), fab);
            Geo.Box("Cushion", r, new Vector3(0, 0.44f, 0.05f), new Vector3(1.6f, 0.1f, 0.7f), fab);
            Geo.Box("Back", r, new Vector3(0, 0.62f, -0.34f), new Vector3(1.9f, 0.5f, 0.18f), fab);
            Geo.Box("ArmL", r, new Vector3(-0.88f, 0.5f, 0), new Vector3(0.16f, 0.32f, 0.85f), fab);
            Geo.Box("ArmR", r, new Vector3(0.88f, 0.5f, 0), new Vector3(0.16f, 0.32f, 0.85f), fab);
            for (int i = 0; i < 4; i++)
                Geo.Cyl("Leg", r, new Vector3(((i & 1) == 0 ? -0.85f : 0.85f), 0.05f, ((i & 2) == 0 ? -0.35f : 0.35f)), 0.02f, 0.1f, M.MetalBlack);
            return r.gameObject;
        }

        static GameObject RoundTable(Transform r, StudioProp p, Color c)
        {
            var top = Tint(p, "TableTop", c, 0.35f);
            Geo.Cyl("Top", r, new Vector3(0, 0.5f, 0), 0.3f, 0.035f, top);
            Geo.Cyl("Shelf", r, new Vector3(0, 0.22f, 0), 0.26f, 0.025f, top);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                var off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.22f;
                Geo.Rod("Leg", r, off + Vector3.up * 0.0f, off * 0.9f + Vector3.up * 0.49f, 0.012f, M.MetalBlack);
            }
            return r.gameObject;
        }

        static GameObject NewsDesk(Transform r, StudioProp p, Color c)
        {
            var body = Tint(p, "DeskBody", c, 0.5f);
            Geo.Box("Top", r, new Vector3(0, 0.76f, 0), new Vector3(2.4f, 0.05f, 0.8f), body);
            Geo.Box("Front", r, new Vector3(0, 0.38f, 0.32f), new Vector3(2.3f, 0.74f, 0.08f), body);
            Geo.Box("Strip", r, new Vector3(0, 0.12f, 0.365f), new Vector3(2.2f, 0.03f, 0.01f), M.Emissive("DeskStrip", new Color(0.4f, 0.8f, 1f), 2f));
            Geo.Box("SideL", r, new Vector3(-1.13f, 0.38f, 0), new Vector3(0.06f, 0.74f, 0.7f), body);
            Geo.Box("SideR", r, new Vector3(1.13f, 0.38f, 0), new Vector3(0.06f, 0.74f, 0.7f), body);
            return r.gameObject;
        }

        static GameObject Stool(Transform r, StudioProp p, Color c)
        {
            var seat = Tint(p, "StoolSeat", c, 0.4f);
            Geo.Cyl("Seat", r, new Vector3(0, 0.75f, 0), 0.19f, 0.06f, seat);
            Geo.Cyl("Post", r, new Vector3(0, 0.38f, 0), 0.025f, 0.72f, M.MetalGrey);
            Geo.Cyl("Ring", r, new Vector3(0, 0.3f, 0), 0.17f, 0.015f, M.MetalGrey);
            Geo.Cyl("Base", r, new Vector3(0, 0.01f, 0), 0.22f, 0.02f, M.MetalGrey);
            return r.gameObject;
        }

        static GameObject Bookshelf(Transform r, StudioProp p, Color c)
        {
            var wood = Tint(p, "ShelfWood", c, 0.3f);
            const float w = 1.2f, h = 1.9f, d = 0.35f;
            Geo.Box("SideL", r, new Vector3(-w / 2, h / 2, 0), new Vector3(0.03f, h, d), wood);
            Geo.Box("SideR", r, new Vector3(w / 2, h / 2, 0), new Vector3(0.03f, h, d), wood);
            var rnd = new System.Random(5);
            for (int i = 0; i < 5; i++)
            {
                float y = 0.02f + i * (h - 0.04f) / 4;
                Geo.Box("Shelf", r, new Vector3(0, y, 0), new Vector3(w, 0.03f, d), wood);
                if (i == 4) break;
                float x = -w / 2 + 0.08f;
                while (x < w / 2 - 0.15f)
                {
                    float bw = 0.03f + (float)rnd.NextDouble() * 0.04f, bh = 0.2f + (float)rnd.NextDouble() * 0.12f;
                    var bc = Color.HSVToRGB((float)rnd.NextDouble(), 0.35f, 0.35f + (float)rnd.NextDouble() * 0.4f);
                    Geo.Box("Book", r, new Vector3(x, y + 0.015f + bh / 2, 0), new Vector3(bw, bh, 0.24f), M.Get("Book" + bc.GetHashCode(), bc, 0.2f));
                    x += bw + 0.005f;
                    if (rnd.NextDouble() < 0.12) x += 0.15f;
                }
            }
            return r.gameObject;
        }

        // ================= Декор =================

        /// <summary>Складная ширма из 3 створок «зигзагом», как на фото студии.</summary>
        static GameObject Shoji(Transform r, StudioProp p, Color c)
        {
            var frame = Tint(p, "ShojiFrame", c, 0.3f);
            var paper = M.Textured("ShojiPaper", M.ShojiTexture(), Color.white, 0.05f);
            const float pw = 0.5f, ph = 1.8f, ang = 22f;
            float step = pw * Mathf.Cos(ang * Mathf.Deg2Rad);
            for (int i = 0; i < 3; i++)
            {
                var g = Geo.Group("Panel" + i, r, new Vector3((i - 1) * step, 0, 0), i % 2 == 0 ? ang : -ang);
                var t = g.transform;
                Geo.Box("Paper", t, new Vector3(0, ph / 2 + 0.02f, 0), new Vector3(pw - 0.04f, ph - 0.06f, 0.008f), paper);
                Geo.Box("StileL", t, new Vector3(-pw / 2 + 0.02f, ph / 2 + 0.02f, 0), new Vector3(0.035f, ph, 0.03f), frame);
                Geo.Box("StileR", t, new Vector3(pw / 2 - 0.02f, ph / 2 + 0.02f, 0), new Vector3(0.035f, ph, 0.03f), frame);
                Geo.Box("RailT", t, new Vector3(0, ph + 0.01f, 0), new Vector3(pw, 0.035f, 0.03f), frame);
                Geo.Box("RailB", t, new Vector3(0, 0.04f, 0), new Vector3(pw, 0.035f, 0.03f), frame);
            }
            return r.gameObject;
        }

        static GameObject Plant(Transform r, StudioProp p, Color c)
        {
            var leaf = Tint(p, "Leaves", c, 0.3f);
            Geo.Cyl("Pot", r, new Vector3(0, 0.22f, 0), 0.22f, 0.44f, M.Get("Pot", new Color(0.85f, 0.83f, 0.8f), 0.3f));
            Geo.Cyl("Stem", r, new Vector3(0, 0.75f, 0), 0.02f, 0.7f, M.Wood);
            var rnd = new System.Random(9);
            for (int i = 0; i < 9; i++)
            {
                var pos = new Vector3((float)rnd.NextDouble() - 0.5f, 0, (float)rnd.NextDouble() - 0.5f) * 0.55f;
                pos.y = 0.9f + (float)rnd.NextDouble() * 0.7f;
                var s = Geo.Sphere("Leaf", r, pos, 0.18f + (float)rnd.NextDouble() * 0.1f, leaf);
                s.transform.localScale = new Vector3(s.transform.localScale.x, s.transform.localScale.y * 0.7f, s.transform.localScale.z);
            }
            return r.gameObject;
        }

        static GameObject Rug(Transform r, StudioProp p, Color c)
        {
            var m = Tint(p, "Rug", c, 0.02f);
            Geo.Cyl("Rug", r, new Vector3(0, 0.006f, 0), 1.3f, 0.012f, m);
            return r.gameObject;
        }

        static GameObject Podium(Transform r, StudioProp p, Color c)
        {
            var m = Tint(p, "Podium", c, 0.25f);
            Geo.Cyl("Podium", r, new Vector3(0, 0.1f, 0), 2.0f, 0.2f, m);
            Geo.Cyl("Glow", r, new Vector3(0, 0.03f, 0), 2.02f, 0.02f, M.Emissive("PodiumGlow", new Color(0.9f, 0.95f, 1f), 1.5f));
            return r.gameObject;
        }

        static GameObject LogoPanel(Transform r, StudioProp p, Color c)
        {
            var frame = Tint(p, "PanelFrame", c, 0.3f);
            Geo.Box("Panel", r, new Vector3(0, 1.3f, 0), new Vector3(2.0f, 2.6f, 0.06f), frame);
            var face = M.Unique("LogoFace", Color.white, 0.2f);
            face.mainTexture = M.ShojiTexture();
            p.imageMaterial = face;
            Geo.Quad("Face", r, new Vector3(0, 1.3f, 0.032f), new Vector2(1.8f, 2.4f), face, new Vector3(0, 180, 0));
            Geo.Box("Foot", r, new Vector3(0, 0.02f, 0), new Vector3(2.0f, 0.04f, 0.5f), M.MetalBlack);
            return r.gameObject;
        }

        static GameObject NeonRing(Transform r, StudioProp p, Color c)
        {
            var neon = M.Unique("Neon", c, 0.5f);
            neon.EnableKeyword("_EMISSION");
            neon.SetColor("_EmissionColor", c * 2.5f);
            p.tintMaterials.Add(neon);
            p.tintIsEmission = true;
            const int n = 28;
            const float rad = 0.9f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                Geo.Rod("Seg", r, new Vector3(Mathf.Cos(a0) * rad, 1.5f + Mathf.Sin(a0) * rad, 0), new Vector3(Mathf.Cos(a1) * rad, 1.5f + Mathf.Sin(a1) * rad, 0), 0.025f, neon);
            }
            Geo.Rod("Stand", r, new Vector3(0, 0, 0), new Vector3(0, 0.6f, 0), 0.02f, M.MetalBlack);
            Geo.Cyl("Base", r, new Vector3(0, 0.01f, 0), 0.25f, 0.02f, M.MetalBlack);
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(r, false);
            l.transform.localPosition = new Vector3(0, 1.5f, 0.3f);
            l.type = LightType.Point;
            l.range = 3.5f;
            l.intensity = 1.5f;
            l.color = c;
            return r.gameObject;
        }

        static GameObject FloorLamp(Transform r, StudioProp p, Color c)
        {
            var shade = M.Unique("LampShade", c, 0.2f);
            shade.EnableKeyword("_EMISSION");
            shade.SetColor("_EmissionColor", c * 1.2f);
            p.tintMaterials.Add(shade);
            p.tintIsEmission = true;
            Geo.Cyl("Base", r, new Vector3(0, 0.015f, 0), 0.18f, 0.03f, M.MetalBlack);
            Geo.Cyl("Pole", r, new Vector3(0, 0.8f, 0), 0.012f, 1.6f, M.MetalBlack);
            Geo.Cyl("Shade", r, new Vector3(0, 1.62f, 0), 0.2f, 0.3f, shade);
            var l = new GameObject("Bulb").AddComponent<Light>();
            l.transform.SetParent(r, false);
            l.transform.localPosition = new Vector3(0, 1.55f, 0);
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 1.2f;
            l.color = c;
            return r.gameObject;
        }

        // ================= Техника =================

        static GameObject LedWall(Transform r, StudioProp p, Color c)
        {
            const float w = 4f, h = 2.25f, y0 = 0.6f;
            Geo.Box("Cabinet", r, new Vector3(0, y0 + h / 2, -0.06f), new Vector3(w + 0.08f, h + 0.08f, 0.1f), M.MetalBlack);
            Geo.Quad("Screen", r, new Vector3(0, y0 + h / 2, 0.0f), new Vector2(w, h), ScreenMat(p, c), new Vector3(0, 180, 0));
            Geo.Box("LegL", r, new Vector3(-w / 2 + 0.3f, y0 / 2, -0.06f), new Vector3(0.08f, y0, 0.08f), M.MetalBlack);
            Geo.Box("LegR", r, new Vector3(w / 2 - 0.3f, y0 / 2, -0.06f), new Vector3(0.08f, y0, 0.08f), M.MetalBlack);
            Geo.Box("FootL", r, new Vector3(-w / 2 + 0.3f, 0.02f, -0.06f), new Vector3(0.1f, 0.04f, 0.8f), M.MetalBlack);
            Geo.Box("FootR", r, new Vector3(w / 2 - 0.3f, 0.02f, -0.06f), new Vector3(0.1f, 0.04f, 0.8f), M.MetalBlack);
            return r.gameObject;
        }

        /// <summary>Мобильная стойка с монитором (на фото — у белой стены, показывает эфирную картинку).</summary>
        static GameObject TvStand(Transform r, StudioProp p, Color c)
        {
            Geo.Box("Foot", r, new Vector3(0, 0.06f, 0), new Vector3(0.9f, 0.04f, 0.5f), M.MetalBlack);
            for (int i = 0; i < 4; i++)
                Geo.Cyl("Wheel", r, new Vector3(((i & 1) == 0 ? -0.4f : 0.4f), 0.035f, ((i & 2) == 0 ? -0.22f : 0.22f)), 0.035f, 0.03f, M.Rubber, new Vector3(0, 0, 90));
            Geo.Box("Column", r, new Vector3(0, 0.75f, -0.03f), new Vector3(0.22f, 1.4f, 0.08f), M.MetalBlack);
            Geo.Box("Bezel", r, new Vector3(0, 1.55f, 0), new Vector3(1.25f, 0.74f, 0.06f), M.MetalBlack);
            Geo.Quad("Screen", r, new Vector3(0, 1.55f, 0.031f), new Vector2(1.2f, 0.68f), ScreenMat(p, c), new Vector3(0, 180, 0));
            return r.gameObject;
        }

        static void CameraHead(Transform r, float y, StudioProp p)
        {
            var head = Geo.Group("Head", r, new Vector3(0, y, 0)).transform;
            Geo.Box("PanHead", head, new Vector3(0, 0.06f, 0), new Vector3(0.2f, 0.12f, 0.2f), M.MetalBlack);
            Geo.Box("Body", head, new Vector3(0, 0.24f, -0.05f), new Vector3(0.18f, 0.22f, 0.42f), M.MetalBlack);
            Geo.Cyl("Lens", head, new Vector3(0, 0.24f, 0.3f), 0.065f, 0.34f, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("LensFront", head, new Vector3(0, 0.24f, 0.475f), 0.055f, 0.01f, M.Glass, new Vector3(90, 0, 0));
            Geo.Box("Viewfinder", head, new Vector3(0, 0.42f, -0.12f), new Vector3(0.22f, 0.15f, 0.12f), M.MetalBlack);
            Geo.Box("Tally", head, new Vector3(0, 0.51f, -0.1f), new Vector3(0.04f, 0.02f, 0.02f), M.Emissive("Tally", Color.red, 3f));
            Geo.Rod("PanBarL", head, new Vector3(-0.1f, 0.08f, -0.1f), new Vector3(-0.3f, -0.05f, -0.6f), 0.012f, M.MetalBlack);
            Geo.Rod("PanBarR", head, new Vector3(0.1f, 0.08f, -0.1f), new Vector3(0.3f, -0.05f, -0.6f), 0.012f, M.MetalBlack);
        }

        static GameObject TripodCamera(Transform r, StudioProp p, Color c)
        {
            var legs = Tint(p, "TripodLegs", c, 0.4f);
            const float top = 1.25f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3 + Mathf.PI / 2;
                var foot = new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * 0.55f;
                foot.y = 0.12f;
                Geo.Rod("Leg", r, new Vector3(0, top, 0), foot, 0.016f, legs);
                Geo.Rod("Spreader", r, foot, Vector3.up * 0.12f, 0.01f, M.MetalBlack);
                Geo.Cyl("Wheel", r, foot - Vector3.up * 0.07f, 0.05f, 0.03f, M.Rubber, new Vector3(0, 0, 90));
            }
            CameraHead(r, top, p);
            return r.gameObject;
        }

        static GameObject PedestalCamera(Transform r, StudioProp p, Color c)
        {
            var body = Tint(p, "Pedestal", c, 0.5f);
            Geo.Cyl("Base", r, new Vector3(0, 0.12f, 0), 0.38f, 0.12f, body);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                Geo.Cyl("Wheel", r, new Vector3(Mathf.Cos(a) * 0.38f, 0.05f, Mathf.Sin(a) * 0.38f), 0.05f, 0.04f, M.Rubber, new Vector3(0, 0, 90));
            }
            Geo.Cyl("Column1", r, new Vector3(0, 0.5f, 0), 0.16f, 0.65f, body);
            Geo.Cyl("Column2", r, new Vector3(0, 1.0f, 0), 0.12f, 0.45f, body);
            Geo.Cyl("SteeringRing", r, new Vector3(0, 1.1f, 0), 0.3f, 0.025f, M.MetalGrey);
            CameraHead(r, 1.22f, p);
            return r.gameObject;
        }

        // ================= Световые приборы (часть рига, не каталог) =================

        /// <summary>Линза Френеля с шторками. Возвращает Light, смотрящий вдоль локальной +Z головы.</summary>
        public static Light Fresnel(Transform head, bool large)
        {
            float rad = large ? 0.17f : 0.12f, len = large ? 0.45f : 0.32f;
            Geo.Cyl("Housing", head, Vector3.zero, rad, len, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("Lens", head, new Vector3(0, 0, len / 2 + 0.005f), rad * 0.85f, 0.01f, M.Emissive("FresnelLens", new Color(1f, 0.9f, 0.75f), 3f), new Vector3(90, 0, 0));
            Geo.Box("Fan", head, new Vector3(0, 0, -len / 2 - 0.04f), new Vector3(rad * 1.6f, rad * 1.6f, 0.08f), M.MetalBlack);
            // шторки (barn doors)
            float d = rad + 0.02f;
            Geo.Box("DoorT", head, new Vector3(0, d, len / 2 + 0.1f), new Vector3(rad * 2, 0.005f, 0.2f), M.MetalBlack, new Vector3(-25, 0, 0));
            Geo.Box("DoorB", head, new Vector3(0, -d, len / 2 + 0.1f), new Vector3(rad * 2, 0.005f, 0.2f), M.MetalBlack, new Vector3(25, 0, 0));
            Geo.Box("DoorL", head, new Vector3(-d, 0, len / 2 + 0.1f), new Vector3(0.005f, rad * 2, 0.2f), M.MetalBlack, new Vector3(0, 25, 0));
            Geo.Box("DoorR", head, new Vector3(d, 0, len / 2 + 0.1f), new Vector3(0.005f, rad * 2, 0.2f), M.MetalBlack, new Vector3(0, -25, 0));
            // разъёмы — синие/жёлтые как на фото
            Geo.Box("Plug", head, new Vector3(0, rad + 0.04f, -len / 2), new Vector3(0.06f, 0.06f, 0.06f), M.PlasticBlue);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(head, false);
            l.transform.localPosition = new Vector3(0, 0, len / 2 + 0.05f);
            l.type = LightType.Spot;
            l.spotAngle = large ? 38 : 30;
            l.innerSpotAngle = l.spotAngle * 0.5f;
            l.range = 14f;
            return l;
        }

        /// <summary>LED-панель (софт) — светящийся прямоугольник в рамке.</summary>
        public static Light SoftPanel(Transform head, bool teal)
        {
            Geo.Box("Frame", head, new Vector3(0, 0, -0.04f), new Vector3(0.66f, 0.38f, 0.07f), teal ? M.PlasticTeal : M.MetalBlack);
            Geo.Box("Face", head, new Vector3(0, 0, 0.0f), new Vector3(0.6f, 0.32f, 0.01f), M.Emissive("SoftFace", new Color(1f, 0.97f, 0.92f), 1.6f));
            Geo.Rod("YokeL", head, new Vector3(-0.34f, 0, -0.04f), new Vector3(-0.34f, 0.3f, -0.04f), 0.012f, M.MetalBlack);
            Geo.Rod("YokeR", head, new Vector3(0.34f, 0, -0.04f), new Vector3(0.34f, 0.3f, -0.04f), 0.012f, M.MetalBlack);
            Geo.Rod("YokeT", head, new Vector3(-0.34f, 0.3f, -0.04f), new Vector3(0.34f, 0.3f, -0.04f), 0.012f, M.MetalBlack);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(head, false);
            l.transform.localPosition = new Vector3(0, 0, 0.05f);
            l.type = LightType.Spot;
            l.spotAngle = 100;
            l.innerSpotAngle = 40;
            l.range = 12f;
            return l;
        }
    }
}
