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
            /// <summary>Имя 3D-модели в Resources/Models (null — процедурный объект).</summary>
            public string model;
            public float modelYaw;
            public Color defaultColor;
            public Func<Transform, StudioProp, Color, GameObject> build;
        }

        static Entry Model(string id, string model, string name, string category, Func<Transform, StudioProp, Color, GameObject> fallback = null, float yaw = 0)
            => new Entry { id = id, model = model, modelYaw = yaw, name = name, category = category, defaultColor = Color.white, build = fallback };

        public static readonly List<Entry> Catalog = new List<Entry>
        {
            // 3D-модели Poly Haven (CC0). Цвет для них — тонировка поверх текстуры (белый = оригинал).
            Model("armchair_classic", "ArmChair_01",              "Sessel grau (wie auf Foto)", "Möbel", Armchair),
            Model("armchair_modern",  "modern_arm_chair_01",      "Ledersessel",             "Möbel", Armchair),
            Model("lounge_chair",     "mid_century_lounge_chair", "Loungesessel",               "Möbel", Armchair),
            Model("sofa_light",       "Sofa_01",                  "Sofa hell",              "Möbel", Sofa),
            Model("sofa_chester",     "sofa_02",                  "Chesterfield-Sofa",           "Möbel", Sofa),
            Model("ottoman",          "Ottoman_01",               "Hocker gepolstert",                        "Möbel"),
            Model("coffee_round",     "coffee_table_round_01",    "Marmortisch rund",           "Möbel", RoundTable),
            Model("coffee_modern",    "modern_coffee_table_01",   "Couchtisch",          "Möbel", RoundTable),
            Model("side_table",       "side_table_01",            "Beistelltisch",           "Möbel"),
            Model("side_table_tall",  "side_table_tall_01",       "Stehtischchen",             "Möbel"),
            Model("bar_chair",        "bar_chair_round_01",       "Barhocker Holz",     "Möbel", Stool),
            Model("metal_stool",      "metal_stool_02",           "Hocker Loft",               "Möbel", Stool),
            Model("cabinet",          "modern_wooden_cabinet",    "Sideboard",               "Möbel"),
            Model("shelves_steel",    "steel_frame_shelves_03",   "Regal Loft",               "Möbel", Bookshelf),
            Model("shelves_wood",     "wooden_display_shelves_01","Regal Holz",         "Möbel", Bookshelf),
            Model("plant_big",        "potted_plant_02",          "Pflanze groß",           "Deko", Plant),
            Model("plant_tree",       "potted_plant_01",          "Bäumchen im Topf",           "Deko", Plant),
            Model("plant_succulent",  "potted_plant_04",          "Sukkulente",                  "Deko", Plant),
            Model("vase",             "ceramic_vase_01",          "Vase",                       "Deko"),
            Model("pillows",          "throw_pillows_01",         "Kissen",                    "Deko"),
            Model("picture",          "standing_picture_frame_01","Bild im Rahmen",             "Deko"),

            // процедурные (форма как на фото / простая геометрия)
            new Entry { id = "table_round", name = "Beistelltisch (wie auf Foto)",   category = "Möbel",  defaultColor = new Color(0.72f, 0.52f, 0.30f), build = RoundTable },
            new Entry { id = "news_desk",   name = "Moderationstisch",        category = "Möbel",  defaultColor = new Color(0.92f, 0.92f, 0.92f), build = NewsDesk },
            new Entry { id = "shoji",       name = "Shoji-Paravent",          category = "Deko",   defaultColor = new Color(0.80f, 0.45f, 0.15f), build = Shoji },
            new Entry { id = "rug",         name = "Teppich rund, uni",    category = "Teppiche", defaultColor = new Color(0.55f, 0.35f, 0.30f), build = Rug },
            new Entry { id = "rug_border",  name = "Teppich 3×2 m, Bordüre", category = "Teppiche", defaultColor = new Color(0.75f, 0.70f, 0.62f), build = RugBuilder(3f, 2f, "border") },
            new Entry { id = "rug_stripes", name = "Teppich 2,5×1,6 m, gestreift", category = "Teppiche", defaultColor = new Color(0.45f, 0.5f, 0.6f), build = RugBuilder(2.5f, 1.6f, "stripes") },
            new Entry { id = "rug_persian", name = "Perserteppich 3×2 m",   category = "Teppiche", defaultColor = Color.white, build = RugBuilder(3f, 2f, "persian") },
            new Entry { id = "rug_runner",  name = "Läufer 3×0,8 m",        category = "Teppiche", defaultColor = new Color(0.3f, 0.3f, 0.32f), build = RugBuilder(3f, 0.8f, "border") },

            new Entry { id = "person_stand",   name = "Person stehend",       category = "Personen", defaultColor = new Color(0.25f, 0.27f, 0.3f), build = PersonBuilder(Pose.Stand) },
            new Entry { id = "person_walk",    name = "Person gehend",        category = "Personen", defaultColor = new Color(0.55f, 0.6f, 0.7f), build = PersonBuilder(Pose.Walk) },
            new Entry { id = "person_present", name = "Moderator:in mit Mikro", category = "Personen", defaultColor = new Color(0.7f, 0.2f, 0.2f), build = PersonBuilder(Pose.Present) },
            new Entry { id = "person_sit",     name = "Person sitzend",       category = "Personen", defaultColor = new Color(0.3f, 0.3f, 0.32f), build = PersonBuilder(Pose.Sit) },
            new Entry { id = "person_sitcross",name = "Gast, Beine überschlagen", category = "Personen", defaultColor = new Color(0.65f, 0.68f, 0.72f), build = PersonBuilder(Pose.SitCross) },

            new Entry { id = "spot_stand",  name = "Scheinwerfer auf Stativ", category = "Licht", defaultColor = new Color(1f, 0.92f, 0.8f), build = SpotOnStand },
            new Entry { id = "uplight",     name = "Bodenstrahler",         category = "Licht", defaultColor = new Color(0.3f, 0.4f, 1f), build = FloorUplight },
            new Entry { id = "led_tube",    name = "LED-Röhre stehend",     category = "Licht", defaultColor = new Color(1f, 0.85f, 0.7f), build = LedTubeStand },
            new Entry { id = "led_tube_floor", name = "LED-Röhre am Boden",  category = "Licht", defaultColor = new Color(0.4f, 0.5f, 1f), build = LedTubeFloor },
            new Entry { id = "podium",      name = "Podest",               category = "Deko",   defaultColor = new Color(0.12f, 0.12f, 0.13f), build = Podium },
            new Entry { id = "logo_panel",  name = "Logowand",   category = "Deko",   defaultColor = new Color(0.95f, 0.95f, 0.95f), build = LogoPanel },
            new Entry { id = "neon_ring",   name = "Neonring",      category = "Deko",   defaultColor = new Color(1.00f, 0.25f, 0.60f), build = NeonRing },
            new Entry { id = "floor_lamp",  name = "Stehlampe",               category = "Licht",   defaultColor = new Color(1.00f, 0.80f, 0.55f), build = FloorLamp },
            new Entry { id = "led_wall",    name = "LED-Wand 4×2,25 m",   category = "Technik", defaultColor = new Color(0.30f, 0.55f, 0.90f), build = LedWall },
            new Entry { id = "tv_stand",    name = "Monitor auf Rollständer",    category = "Technik", defaultColor = new Color(0.45f, 0.75f, 0.85f), build = TvStand },
            new Entry { id = "studio_cam",  name = "Kamera auf Stativ",    category = "Technik", defaultColor = new Color(0.08f, 0.08f, 0.09f), build = TripodCamera },
            new Entry { id = "pedestal_cam",name = "Kamera auf Pumpstativ", category = "Technik", defaultColor = new Color(0.75f, 0.76f, 0.78f), build = PedestalCamera },
        };

        /// <summary>Старые id из сохранённых проектов → новые.</summary>
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "armchair", "armchair_classic" }, { "sofa", "sofa_light" }, { "stool", "bar_chair" },
            { "bookshelf", "shelves_wood" }, { "plant", "plant_big" },
        };

        public static Entry Find(string id)
        {
            if (id != null && Aliases.TryGetValue(id, out var alias)) id = alias;
            return Catalog.Find(e => e.id == id);
        }

        public static StudioProp Create(string id, Transform parent, Vector3 pos, float rotY, Color? color = null)
        {
            var e = Find(id);
            if (e == null) { Debug.LogWarning("Unbekanntes Objekt: " + id); return null; }
            var root = new GameObject(e.name);
            root.transform.SetParent(parent, false);
            var prop = root.AddComponent<StudioProp>();
            prop.typeId = e.id;
            prop.displayName = e.name;
            var c = color ?? e.defaultColor;
            bool built = e.model != null && ModelLibrary.Build(e.model, root.transform, prop, e.modelYaw);
            if (!built)
            {
                // модели нет (не импортирована) — рисуем примитивами
                if (e.model != null && e.build == null) Geo.Box("Placeholder", root.transform, new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.6f), M.Get("Placeholder", Color.grey));
                else e.build(root.transform, prop, e.model != null ? new Color(0.62f, 0.62f, 0.6f) : c);
            }
            if (built && c != Color.white) prop.SetColor(c);
            prop.color = c;
            var cam = root.GetComponent<StudioCam>();
            if (cam != null) cam.SetNumber(parent.GetComponentsInChildren<StudioCam>(true).Length);
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

        // ================= Personen (stilisierte Figuren) =================

        enum Pose { Stand, Walk, Present, Sit, SitCross }

        static Material Skin => M.Get("Skin", new Color(0.80f, 0.64f, 0.54f), 0.35f);
        static Material Hair => M.Get("Hair", new Color(0.2f, 0.15f, 0.11f), 0.25f);
        static Material Pants => M.Get("Pants", new Color(0.13f, 0.16f, 0.24f), 0.15f);
        static Material Shoes => M.Get("Shoes", new Color(0.88f, 0.88f, 0.86f), 0.3f);

        static Func<Transform, StudioProp, Color, GameObject> PersonBuilder(Pose pose) => (r, p, c) => Person(r, p, c, pose);

        static GameObject Person(Transform r, StudioProp p, Color c, Pose pose)
        {
            var shirt = Tint(p, "Shirt", c, 0.1f);
            // Gelenke; Blickrichtung +Z, rechte Seite = +X
            Vector3 pelvis = new Vector3(0, 0.95f, 0), neck = new Vector3(0, 1.48f, 0), head = new Vector3(0, 1.63f, 0.01f);
            Vector3 hipL = new Vector3(-0.1f, 0.93f, 0), hipR = new Vector3(0.1f, 0.93f, 0);
            Vector3 kneeL = new Vector3(-0.1f, 0.5f, 0.02f), kneeR = new Vector3(0.1f, 0.5f, 0.02f);
            Vector3 ankleL = new Vector3(-0.1f, 0.08f, 0), ankleR = new Vector3(0.1f, 0.08f, 0);
            Vector3 shL = new Vector3(-0.19f, 1.44f, 0), shR = new Vector3(0.19f, 1.44f, 0);
            Vector3 elL = new Vector3(-0.24f, 1.16f, 0), elR = new Vector3(0.24f, 1.16f, 0);
            Vector3 wrL = new Vector3(-0.25f, 0.9f, 0.04f), wrR = new Vector3(0.25f, 0.9f, 0.04f);
            bool mic = false;

            switch (pose)
            {
                case Pose.Walk:
                    kneeL = new Vector3(-0.1f, 0.5f, 0.13f); ankleL = new Vector3(-0.1f, 0.08f, 0.26f);
                    kneeR = new Vector3(0.1f, 0.5f, -0.06f); ankleR = new Vector3(0.1f, 0.12f, -0.24f);
                    elL = new Vector3(-0.24f, 1.17f, -0.08f); wrL = new Vector3(-0.25f, 0.93f, -0.15f);
                    elR = new Vector3(0.24f, 1.17f, 0.08f); wrR = new Vector3(0.25f, 0.93f, 0.17f);
                    break;
                case Pose.Present:
                    elR = new Vector3(0.22f, 1.18f, 0.12f); wrR = new Vector3(0.12f, 1.36f, 0.22f);
                    elL = new Vector3(-0.26f, 1.15f, 0.06f); wrL = new Vector3(-0.3f, 1.06f, 0.3f);
                    mic = true;
                    break;
                case Pose.Sit:
                case Pose.SitCross:
                    pelvis = new Vector3(0, 0.52f, -0.05f); neck = new Vector3(0, 1.03f, -0.13f); head = new Vector3(0, 1.18f, -0.11f);
                    hipL = new Vector3(-0.1f, 0.5f, -0.04f); hipR = new Vector3(0.1f, 0.5f, -0.04f);
                    kneeL = new Vector3(-0.12f, 0.52f, 0.38f); kneeR = new Vector3(0.12f, 0.52f, 0.38f);
                    ankleL = new Vector3(-0.12f, 0.08f, 0.42f); ankleR = new Vector3(0.12f, 0.08f, 0.42f);
                    shL = new Vector3(-0.19f, 0.99f, -0.12f); shR = new Vector3(0.19f, 0.99f, -0.12f);
                    elL = new Vector3(-0.22f, 0.74f, 0.0f); elR = new Vector3(0.22f, 0.74f, 0.0f);
                    wrL = new Vector3(-0.14f, 0.6f, 0.28f); wrR = new Vector3(0.14f, 0.6f, 0.28f);
                    if (pose == Pose.SitCross)
                    {
                        // wie der Gast auf dem Sucherfoto: Bein überschlagen, Hände hinter dem Kopf
                        kneeR = new Vector3(0.0f, 0.66f, 0.36f); ankleR = new Vector3(-0.22f, 0.5f, 0.62f);
                        elL = new Vector3(-0.3f, 1.16f, -0.08f); wrL = new Vector3(-0.07f, 1.22f, -0.2f);
                        elR = new Vector3(0.3f, 1.16f, -0.08f); wrR = new Vector3(0.07f, 1.22f, -0.2f);
                    }
                    break;
            }

            var torsoG = Geo.Group("Torso", r, pelvis).transform;
            var torso = Geo.Limb("Chest", torsoG, new Vector3(0, 0.1f, 0), neck - pelvis - new Vector3(0, 0.06f, 0), 0.14f, shirt);
            torso.transform.localScale = Vector3.Scale(torso.transform.localScale, new Vector3(1.35f, 1f, 0.8f));
            Geo.Limb("Hips", r, hipL + new Vector3(0, 0.03f, 0), hipR + new Vector3(0, 0.03f, 0), 0.11f, Pants);
            Geo.Limb("Neck", r, neck - new Vector3(0, 0.06f, 0), head - new Vector3(0, 0.08f, 0), 0.05f, Skin);

            Geo.Limb("ThighL", r, hipL, kneeL, 0.075f, Pants); Geo.Limb("ShinL", r, kneeL, ankleL, 0.06f, Pants);
            Geo.Limb("ThighR", r, hipR, kneeR, 0.075f, Pants); Geo.Limb("ShinR", r, kneeR, ankleR, 0.06f, Pants);
            Foot(r, ankleL, kneeL); Foot(r, ankleR, kneeR);

            Geo.Limb("UpperArmL", r, shL, elL, 0.055f, shirt); Geo.Limb("ForearmL", r, elL, wrL, 0.043f, Skin);
            Geo.Limb("UpperArmR", r, shR, elR, 0.055f, shirt); Geo.Limb("ForearmR", r, elR, wrR, 0.043f, Skin);
            Geo.Sphere("HandL", r, wrL + (wrL - elL).normalized * 0.05f, 0.045f, Skin);
            Geo.Sphere("HandR", r, wrR + (wrR - elR).normalized * 0.05f, 0.045f, Skin);
            if (mic)
            {
                var top = wrR + (wrR - elR).normalized * 0.06f;
                Geo.Limb("Mic", r, top - new Vector3(0, 0.08f, 0), top + new Vector3(0, 0.08f, 0.02f), 0.018f, M.MetalBlack);
                Geo.Sphere("MicHead", r, top + new Vector3(0, 0.11f, 0.02f), 0.028f, M.MetalGrey);
            }

            var headG = Geo.Group("Head", r, head).transform;
            var skull = Geo.Sphere("Skull", headG, Vector3.zero, 0.105f, Skin);
            skull.transform.localScale = new Vector3(0.2f, 0.24f, 0.22f);
            var hair = Geo.Sphere("Hair", headG, new Vector3(0, 0.035f, -0.015f), 0.11f, Hair);
            hair.transform.localScale = new Vector3(0.215f, 0.19f, 0.23f);
            Geo.Box("Nose", headG, new Vector3(0, -0.01f, 0.105f), new Vector3(0.025f, 0.04f, 0.03f), Skin);

            var idle = r.gameObject.AddComponent<PersonIdle>();
            idle.head = headG;
            idle.torso = torsoG;
            return r.gameObject;
        }

        static void Foot(Transform r, Vector3 ankle, Vector3 knee)
        {
            var fwd = Vector3.ProjectOnPlane(ankle - knee, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f || Vector3.Dot(fwd, Vector3.forward) < 0.2f) fwd = Vector3.forward;
            fwd.Normalize();
            var foot = Geo.Box("Shoe", r, ankle + fwd * 0.06f - new Vector3(0, 0.035f, 0), new Vector3(0.09f, 0.07f, 0.24f), Shoes);
            foot.transform.localRotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // ================= Teppiche =================

        static Func<Transform, StudioProp, Color, GameObject> RugBuilder(float w, float d, string kind)
            => (r, p, c) =>
            {
                var m = Tint(p, "Rug", c, 0.02f);
                m.mainTexture = M.RugTexture(kind);
                Geo.Box("Rug", r, new Vector3(0, 0.006f, 0), new Vector3(w, 0.012f, d), m);
                return r.gameObject;
            };

        // ================= Licht am Boden =================

        static Material Emitter(StudioProp p, string name, Color c, float k)
        {
            var m = M.Unique(name, c, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * k);
            p.tintMaterials.Add(m);
            p.tintIsEmission = true;
            return m;
        }

        static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = c;
            return l;
        }

        /// <summary>Stehende LED-Röhre (z. B. Astera-Titan-Stil) auf Bodenplatte.</summary>
        static GameObject LedTubeStand(Transform r, StudioProp p, Color c)
        {
            var tube = Emitter(p, "LedTube", c, 2.5f);
            Geo.Cyl("Base", r, new Vector3(0, 0.012f, 0), 0.13f, 0.024f, M.MetalBlack);
            Geo.Cyl("Holder", r, new Vector3(0, 0.08f, 0), 0.03f, 0.12f, M.MetalBlack);
            Geo.Cyl("Tube", r, new Vector3(0, 0.79f, 0), 0.025f, 1.3f, tube);
            Geo.Cyl("CapTop", r, new Vector3(0, 1.45f, 0), 0.028f, 0.03f, M.MetalBlack);
            PointLight(r, new Vector3(0, 0.6f, 0.08f), c, 3.5f, 1.0f);
            PointLight(r, new Vector3(0, 1.2f, 0.08f), c, 3.5f, 1.0f);
            return r.gameObject;
        }

        /// <summary>Liegende LED-Röhre am Boden (Akzentlicht für Wand/Vorhang).</summary>
        static GameObject LedTubeFloor(Transform r, StudioProp p, Color c)
        {
            var tube = Emitter(p, "LedTubeFloor", c, 2.5f);
            Geo.Cyl("Tube", r, new Vector3(0, 0.06f, 0), 0.025f, 1.6f, tube, new Vector3(0, 0, 90));
            Geo.Box("FootL", r, new Vector3(-0.6f, 0.02f, 0), new Vector3(0.04f, 0.04f, 0.12f), M.MetalBlack);
            Geo.Box("FootR", r, new Vector3(0.6f, 0.02f, 0), new Vector3(0.04f, 0.04f, 0.12f), M.MetalBlack);
            PointLight(r, new Vector3(-0.4f, 0.2f, 0.1f), c, 3f, 1.0f);
            PointLight(r, new Vector3(0.4f, 0.2f, 0.1f), c, 3f, 1.0f);
            return r.gameObject;
        }

        /// <summary>Stufenlinsen-Scheinwerfer mit Torblende — Kopf entlang +Z.</summary>
        static Light SpotHead(Transform head, Material lens, float rad, float len)
        {
            Geo.Cyl("Housing", head, Vector3.zero, rad, len, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("Lens", head, new Vector3(0, 0, len / 2 + 0.005f), rad * 0.85f, 0.01f, lens, new Vector3(90, 0, 0));
            Geo.Cyl("Back", head, new Vector3(0, 0, -len / 2 - 0.02f), rad * 0.9f, 0.04f, M.MetalBlack, new Vector3(90, 0, 0));
            float d = rad + 0.02f, l = rad * 1.3f;
            Geo.Box("DoorT", head, new Vector3(0, d + 0.03f, len / 2 + l * 0.45f), new Vector3(rad * 2, 0.005f, l), M.MetalBlack, new Vector3(-30, 0, 0));
            Geo.Box("DoorB", head, new Vector3(0, -d - 0.03f, len / 2 + l * 0.45f), new Vector3(rad * 2, 0.005f, l), M.MetalBlack, new Vector3(30, 0, 0));
            Geo.Box("DoorL", head, new Vector3(-d - 0.03f, 0, len / 2 + l * 0.45f), new Vector3(0.005f, rad * 1.6f, l), M.MetalBlack, new Vector3(0, 30, 0));
            Geo.Box("DoorR", head, new Vector3(d + 0.03f, 0, len / 2 + l * 0.45f), new Vector3(0.005f, rad * 1.6f, l), M.MetalBlack, new Vector3(0, -30, 0));
            Geo.Cyl("FocusKnob", head, new Vector3(0, -rad - 0.02f, -len / 4), 0.02f, 0.04f, M.MetalBlack);
            var l2 = new GameObject("Light").AddComponent<Light>();
            l2.transform.SetParent(head, false);
            l2.transform.localPosition = new Vector3(0, 0, len / 2 + 0.03f);
            l2.type = LightType.Spot;
            l2.spotAngle = 34;
            l2.innerSpotAngle = 14;
            l2.range = 15f;
            l2.shadows = LightShadows.Soft;
            return l2;
        }

        /// <summary>Scheinwerfer auf Lampenstativ (wie auf dem Referenzbild: schwarzer Fresnel mit Torblende).</summary>
        static GameObject SpotOnStand(Transform r, StudioProp p, Color c)
        {
            var lens = Emitter(p, "SpotLens", c, 2f);
            const float top = 1.85f;
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI * 2 / 3;
                var foot = new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.5f;
                foot.y = 0.01f;
                Geo.Rod("Leg", r, new Vector3(0, 0.75f, 0), foot, 0.012f, M.MetalBlack);
                Geo.Rod("Brace", r, new Vector3(0, 0.45f, 0), foot * 0.55f + Vector3.up * 0.3f, 0.007f, M.MetalBlack);
            }
            Geo.Rod("Pole", r, new Vector3(0, 0.45f, 0), new Vector3(0, top, 0), 0.016f, M.MetalBlack);
            Geo.Cyl("Collar", r, new Vector3(0, 1.2f, 0), 0.025f, 0.05f, M.MetalBlack);

            var al = r.gameObject.AddComponent<AimableLight>();
            al.panPivot = Geo.Group("PanPivot", r, new Vector3(0, top, 0)).transform;
            Geo.Rod("YokeL", al.panPivot, new Vector3(-0.17f, 0, 0), new Vector3(-0.17f, 0.22f, 0), 0.012f, M.MetalBlack);
            Geo.Rod("YokeR", al.panPivot, new Vector3(0.17f, 0, 0), new Vector3(0.17f, 0.22f, 0), 0.012f, M.MetalBlack);
            Geo.Rod("YokeB", al.panPivot, new Vector3(-0.17f, 0, 0), new Vector3(0.17f, 0, 0), 0.012f, M.MetalBlack);
            Geo.Cyl("YokeKnob", al.panPivot, new Vector3(0.19f, 0.22f, 0), 0.025f, 0.03f, M.MetalBlack, new Vector3(0, 0, 90));
            al.tiltPivot = Geo.Group("TiltPivot", al.panPivot, new Vector3(0, 0.22f, 0)).transform;
            al.lightSource = SpotHead(al.tiltPivot, lens, 0.15f, 0.3f);
            al.lightSource.color = c;
            al.lensMat = lens;
            al.tilt = -15f; al.intensity = 1.8f;
            al.Apply();
            return r.gameObject;
        }

        /// <summary>Bodenstrahler (PAR) auf Bodenplatte — für Wand- und Vorhangakzente.</summary>
        static GameObject FloorUplight(Transform r, StudioProp p, Color c)
        {
            var lens = Emitter(p, "ParLens", c, 2f);
            Geo.Box("Plate", r, new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.3f), M.MetalBlack);
            var al = r.gameObject.AddComponent<AimableLight>();
            al.minTilt = -10f; al.maxTilt = 90f;
            al.panPivot = Geo.Group("PanPivot", r, new Vector3(0, 0.02f, 0)).transform;
            Geo.Rod("YokeL", al.panPivot, new Vector3(-0.13f, 0, 0), new Vector3(-0.13f, 0.16f, 0), 0.01f, M.MetalBlack);
            Geo.Rod("YokeR", al.panPivot, new Vector3(0.13f, 0, 0), new Vector3(0.13f, 0.16f, 0), 0.01f, M.MetalBlack);
            al.tiltPivot = Geo.Group("TiltPivot", al.panPivot, new Vector3(0, 0.16f, 0)).transform;
            Geo.Cyl("Can", al.tiltPivot, Vector3.zero, 0.11f, 0.24f, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("Lens", al.tiltPivot, new Vector3(0, 0, 0.122f), 0.095f, 0.01f, lens, new Vector3(90, 0, 0));
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(al.tiltPivot, false);
            l.transform.localPosition = new Vector3(0, 0, 0.14f);
            l.type = LightType.Spot;
            l.spotAngle = 40; l.innerSpotAngle = 15; l.range = 8f;
            l.color = c;
            al.lightSource = l;
            al.lensMat = lens;
            al.tilt = 55f; al.intensity = 1.5f;
            al.Apply();
            return r.gameObject;
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

        // ===== Studiokamera wie im Studio: Blackmagic URSA Broadcast + Fujinon-Zoom, Manfrotto-Stativ mit Spinne =====

        static Material CableBlue => M.Get("CableBlue", new Color(0.1f, 0.35f, 0.85f), 0.5f);
        static Material MonitorBlue => M.Get("MonitorBlue", new Color(0.12f, 0.16f, 0.32f), 0.4f, 0.2f);
        static Material DarkGrey => M.Get("CamDarkGrey", new Color(0.13f, 0.13f, 0.14f), 0.35f, 0.3f);

        /// <summary>Schwenkkopf + Kamera + Objektiv + Sucher-Monitor. Gibt StudioCam zurück.</summary>
        static StudioCam CameraHead(Transform r, StudioProp p, float height, float minH, float maxH)
        {
            var cam = r.gameObject.AddComponent<StudioCam>();
            cam.height = height; cam.minHeight = minH; cam.maxHeight = maxH;

            var pan = Geo.Group("PanPivot", r, new Vector3(0, height, 0)).transform;
            cam.panPivot = pan;
            // Fluidkopf (Manfrotto) mit rotem Logo
            Geo.Cyl("HeadBase", pan, new Vector3(0, 0.02f, 0), 0.06f, 0.04f, M.MetalBlack);
            Geo.Box("FluidHead", pan, new Vector3(0, 0.08f, 0), new Vector3(0.13f, 0.1f, 0.15f), M.MetalBlack);
            Geo.Cyl("Logo", pan, new Vector3(0.066f, 0.085f, 0), 0.015f, 0.004f, M.RedBox, new Vector3(0, 0, 90));

            var tilt = Geo.Group("TiltPivot", pan, new Vector3(0, 0.14f, 0)).transform;
            cam.tiltPivot = tilt;
            Geo.Box("Plate", tilt, new Vector3(0, 0.005f, 0.02f), new Vector3(0.14f, 0.015f, 0.3f), DarkGrey);

            // Kamerakörper
            Geo.Box("Body", tilt, new Vector3(0, 0.115f, 0.0f), new Vector3(0.15f, 0.19f, 0.3f), M.MetalBlack);
            Geo.Box("Battery", tilt, new Vector3(0, 0.115f, -0.175f), new Vector3(0.13f, 0.17f, 0.05f), DarkGrey);
            Geo.Box("SidePanel", tilt, new Vector3(-0.077f, 0.12f, -0.03f), new Vector3(0.006f, 0.09f, 0.12f), DarkGrey);
            Geo.Box("SideButtons", tilt, new Vector3(0.077f, 0.08f, 0.05f), new Vector3(0.006f, 0.05f, 0.1f), DarkGrey);
            for (int i = 0; i < 4; i++)
                Geo.Cyl("SDI", tilt, new Vector3(-0.08f, 0.06f + i * 0.03f, -0.12f), 0.008f, 0.02f, M.MetalGrey, new Vector3(0, 0, 90));

            // Fujinon-Broadcastzoom mit Servogriff
            Geo.Cyl("LensMount", tilt, new Vector3(0, 0.1f, 0.165f), 0.05f, 0.03f, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("LensBarrel", tilt, new Vector3(0, 0.1f, 0.27f), 0.046f, 0.2f, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("ZoomRing", tilt, new Vector3(0, 0.1f, 0.25f), 0.049f, 0.04f, DarkGrey, new Vector3(90, 0, 0));
            Geo.Cyl("LensFront", tilt, new Vector3(0, 0.1f, 0.4f), 0.062f, 0.08f, M.MetalBlack, new Vector3(90, 0, 0));
            Geo.Cyl("FrontGlass", tilt, new Vector3(0, 0.1f, 0.441f), 0.052f, 0.004f, M.Glass, new Vector3(90, 0, 0));
            Geo.Box("ServoGrip", tilt, new Vector3(0.075f, 0.075f, 0.27f), new Vector3(0.055f, 0.11f, 0.15f), M.MetalBlack);
            Geo.Box("FujinonPlate", tilt, new Vector3(0.103f, 0.09f, 0.27f), new Vector3(0.004f, 0.025f, 0.08f), M.MetalGrey);
            Geo.Box("ZoomRocker", tilt, new Vector3(0.1f, 0.12f, 0.24f), new Vector3(0.012f, 0.02f, 0.04f), DarkGrey);

            // Tragegriff + Tally
            Geo.Box("HandleRail", tilt, new Vector3(0, 0.26f, -0.01f), new Vector3(0.03f, 0.025f, 0.26f), M.MetalBlack);
            Geo.Box("HandlePostF", tilt, new Vector3(0, 0.225f, 0.1f), new Vector3(0.025f, 0.05f, 0.025f), M.MetalBlack);
            Geo.Box("HandlePostB", tilt, new Vector3(0, 0.225f, -0.1f), new Vector3(0.025f, 0.05f, 0.025f), M.MetalBlack);
            cam.tallyMat = M.Unique("Tally", new Color(0.8f, 0.05f, 0.05f), 0.5f);
            cam.tallyMat.EnableKeyword("_EMISSION");
            Geo.Box("Tally", tilt, new Vector3(0, 0.278f, 0.1f), new Vector3(0.05f, 0.012f, 0.02f), cam.tallyMat);
            cam.SetLive(false);

            // Sucher-Monitor (7") am Arm, links oben, mit Sonnenblende
            Geo.Rod("MonArm1", tilt, new Vector3(-0.02f, 0.27f, 0.02f), new Vector3(-0.13f, 0.36f, 0.0f), 0.012f, M.MetalBlack);
            Geo.Rod("MonArm2", tilt, new Vector3(-0.13f, 0.36f, 0.0f), new Vector3(-0.2f, 0.38f, -0.03f), 0.012f, M.Get("ArmGreen", new Color(0.15f, 0.45f, 0.3f), 0.4f, 0.4f));
            var mon = Geo.Group("Viewfinder", tilt, new Vector3(-0.22f, 0.4f, -0.05f), 0).transform;
            mon.localRotation = Quaternion.Euler(-8, -15, 0);
            Geo.Box("MonBody", mon, Vector3.zero, new Vector3(0.2f, 0.14f, 0.045f), MonitorBlue);
            Geo.Quad("MonScreen", mon, new Vector3(0, 0, -0.024f), new Vector2(0.17f, 0.11f), M.Emissive("VfScreen", new Color(0.35f, 0.4f, 0.6f), 0.8f), Vector3.zero);
            Geo.Box("HoodT", mon, new Vector3(0, 0.075f, -0.06f), new Vector3(0.2f, 0.006f, 0.08f), M.MetalBlack);
            Geo.Box("HoodL", mon, new Vector3(-0.1f, 0, -0.06f), new Vector3(0.006f, 0.14f, 0.08f), M.MetalBlack);
            Geo.Box("HoodR", mon, new Vector3(0.1f, 0, -0.06f), new Vector3(0.006f, 0.14f, 0.08f), M.MetalBlack);
            for (int i = 0; i < 4; i++)
                Geo.Cyl("Knob", mon, new Vector3(0.105f, -0.045f + i * 0.03f, 0.0f), 0.008f, 0.012f, DarkGrey, new Vector3(0, 0, 90));

            // Schwenkarme: links mit Zoom-Fernbedienung, rechts mit Fokus
            Geo.Rod("PanBarL", tilt, new Vector3(-0.06f, 0.02f, -0.1f), new Vector3(-0.24f, -0.06f, -0.62f), 0.013f, M.MetalBlack);
            Geo.Cyl("ZoomDemand", tilt, new Vector3(-0.25f, -0.065f, -0.66f), 0.025f, 0.13f, M.Rubber, new Vector3(80, -20, 0));
            Geo.Box("ZoomDemandBox", tilt, new Vector3(-0.22f, -0.04f, -0.56f), new Vector3(0.05f, 0.06f, 0.09f), M.MetalBlack);
            Geo.Rod("PanBarR", tilt, new Vector3(0.06f, 0.02f, -0.1f), new Vector3(0.22f, -0.06f, -0.58f), 0.013f, M.MetalBlack);
            Geo.Cyl("FocusDemand", tilt, new Vector3(0.23f, -0.06f, -0.6f), 0.03f, 0.06f, M.Rubber, new Vector3(0, 0, 90));

            // blaue SDI-Kabel hinten herunter
            Geo.Rod("Cable1", tilt, new Vector3(-0.08f, 0.08f, -0.13f), new Vector3(-0.1f, -0.15f, -0.2f), 0.007f, CableBlue);
            Geo.Rod("Cable2", tilt, new Vector3(-0.08f, 0.11f, -0.13f), new Vector3(-0.08f, -0.15f, -0.18f), 0.007f, CableBlue);
            Geo.Rod("Cable3", tilt, new Vector3(0.03f, 0.27f, 0.02f), new Vector3(-0.2f, 0.38f, -0.05f), 0.005f, M.Rubber);

            // Kameranummer an beiden Seiten
            var tags = new System.Collections.Generic.List<Renderer>();
            tags.Add(Geo.Quad("NumberL", tilt, new Vector3(-0.0761f, 0.17f, 0.06f), new Vector2(0.07f, 0.05f), M.NumberTag(1), new Vector3(0, 90, 0)).GetComponent<Renderer>());
            tags.Add(Geo.Quad("NumberR", tilt, new Vector3(0.0761f, 0.17f, 0.06f), new Vector2(0.07f, 0.05f), M.NumberTag(1), new Vector3(0, -90, 0)).GetComponent<Renderer>());
            cam.numberTags = tags.ToArray();

            var eye = Geo.Group("Eye", tilt, new Vector3(0, 0.1f, 0.46f)).transform;
            cam.eye = eye;
            return cam;
        }

        static GameObject TripodCamera(Transform r, StudioProp p, Color c)
        {
            var legs = Tint(p, "TripodLegs", c, 0.4f);
            const float bowlY = 1.02f, footR = 0.5f, armY = 0.14f;
            // Bodenspinne mit Rollen
            Geo.Cyl("DollyHub", r, new Vector3(0, armY, 0), 0.07f, 0.05f, M.MetalBlack);
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI * 2 / 3; // ein Bein nach vorn (+Z)
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var foot = dir * footR + Vector3.up * armY;
                Geo.Rod("DollyArm", r, dir * 0.06f + Vector3.up * armY, foot + dir * 0.06f, 0.018f, M.MetalBlack);
                Geo.Box("CasterFork", r, foot + dir * 0.06f - Vector3.up * 0.05f, new Vector3(0.03f, 0.08f, 0.03f), M.MetalGrey);
                Geo.Cyl("Caster", r, foot + dir * 0.06f - Vector3.up * 0.09f, 0.045f, 0.025f, M.Rubber, new Vector3(0, 0, 90));
                // Doppelrohr-Bein mit Mittelklemme
                var top = dir * 0.07f + Vector3.up * bowlY;
                var side = Vector3.Cross(Vector3.up, dir).normalized * 0.022f;
                Geo.Rod("LegA", r, top + side, foot + side * 0.6f + Vector3.up * 0.03f, 0.011f, legs);
                Geo.Rod("LegB", r, top - side, foot - side * 0.6f + Vector3.up * 0.03f, 0.011f, legs);
                var mid = Vector3.Lerp(top, foot, 0.45f);
                Geo.Box("LegClamp", r, mid, new Vector3(0.07f, 0.04f, 0.07f), M.MetalBlack);
                var low = Vector3.Lerp(top, foot, 0.75f);
                Geo.Box("LegLock", r, low, new Vector3(0.06f, 0.05f, 0.06f), M.MetalBlack);
            }
            Geo.Cyl("Bowl", r, new Vector3(0, bowlY, 0), 0.085f, 0.07f, M.MetalBlack);
            var cam = CameraHead(r, p, 1.25f, bowlY + 0.03f, 1.6f);
            cam.SetColumn(Geo.Cyl("Column", r, new Vector3(0, bowlY + 0.1f, 0), 0.03f, 0.2f, M.MetalGrey).transform, bowlY);
            cam.Apply();
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
            Geo.Cyl("SteeringRing", r, new Vector3(0, 0.85f, 0), 0.3f, 0.025f, M.MetalGrey);
            var cam = CameraHead(r, p, 1.35f, 1.0f, 1.75f);
            cam.SetColumn(Geo.Cyl("Column2", r, new Vector3(0, 1.0f, 0), 0.11f, 0.3f, body).transform, 0.82f);
            cam.Apply();
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
