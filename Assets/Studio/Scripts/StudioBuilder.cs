using System.Collections.Generic;
using UnityEngine;
using M = Studio.StudioMaterials;

namespace Studio
{
    /// <summary>
    /// Строит копию павильона по 4 фотографиям. Размеры приблизительные (по глазу), в метрах.
    ///
    ///  Вид сверху (X — вправо/восток, Z — вниз/юг):
    ///
    ///            СЕВЕР: чёрные акустические панели, балкон-галерея на 3.8 м,
    ///                   двустворчатый проём, вентрешётка, красный силовой щит
    ///      +-------------------------------------------+
    ///      |                                           | ВОСТОК:
    ///  З   |                                           | белая стена (циклорама-плоскость),
    ///  А   |        [ширма]  кресло ○ кресло           | в углу СВ — проём с чёрной драпировкой
    ///  П   |                                           |
    ///  А   | камеры                       монитор      |
    ///  Д   |                                           |
    ///      +-------------------------------------------+
    ///            ЮГ: чёрный занавес, бежевая кулиса, проход на склад (EXIT)
    /// </summary>
    public class StudioBuilder : MonoBehaviour
    {
        public const float Width = 16f;   // X
        public const float Depth = 13f;   // Z
        public const float Height = 8.5f;
        public const float GridY = 7.3f;  // высота подвесной решётки

        public static float HalfW => Width / 2;
        public static float HalfD => Depth / 2;

        public Transform room, rig, set;
        public readonly List<Light> fixtureLights = new List<Light>();
        public readonly List<float> fixtureBaseIntensity = new List<float>();
        public Renderer whiteWall;
        public Material whiteWallMat, wallBodyMat;

        /// <summary>Точка, куда по умолчанию направлен свет (центр съёмочной зоны).</summary>
        public static readonly Vector3 SetCenter = new Vector3(1.0f, 0.9f, -0.8f);

        public void Build()
        {
            Clear();
            room = Geo.Group("Room", transform, Vector3.zero).transform;
            rig = Geo.Group("LightingRig", transform, Vector3.zero).transform;
            set = Geo.Group("Set", transform, Vector3.zero).transform;

            BuildShell();
            BuildNorthWall();
            BuildEastWall();
            BuildSouthWall();
            BuildWestWall();
            BuildGrid();
            BuildFixtures();
            BuildDefaultSet();
            SetupEnvironment();
        }

        public void Clear()
        {
            fixtureLights.Clear();
            fixtureBaseIntensity.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
        }

        // ---------------- Коробка ----------------

        void BuildShell()
        {
            var floor = Geo.Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(Width, 0.1f, Depth), M.Floor);
            floor.AddComponent<BoxCollider>(); // для лучей мыши
            floor.layer = 0;
            Geo.Box("Ceiling", room, new Vector3(0, Height, 0), new Vector3(Width, 0.1f, Depth), M.Ceiling);
        }

        void Wall(string name, Vector3 center, Vector3 size) => Geo.Box(name, room, center, size, M.WallBlack);

        void BuildNorthWall()
        {
            float z = -HalfD;
            var n = Geo.Group("NorthWall", room, Vector3.zero).transform;
            // проём двери: x от -3.6 до -1.6, высота 2.6
            const float doorL = -3.6f, doorR = -1.6f, doorH = 2.6f;
            Geo.Box("WallLeft", n, new Vector3((-HalfW + doorL) / 2, Height / 2, z - 0.1f), new Vector3(doorL + HalfW, Height, 0.2f), M.WallBlack);
            Geo.Box("WallRight", n, new Vector3((doorR + HalfW) / 2, Height / 2, z - 0.1f), new Vector3(HalfW - doorR, Height, 0.2f), M.WallBlack);
            Geo.Box("WallOverDoor", n, new Vector3((doorL + doorR) / 2, (Height + doorH) / 2, z - 0.1f), new Vector3(doorR - doorL, Height - doorH, 0.2f), M.WallBlack);
            // тёмный коридор за проёмом + открытая створка
            Geo.Box("Corridor", n, new Vector3((doorL + doorR) / 2, doorH / 2, z - 1.2f), new Vector3(doorR - doorL + 0.4f, doorH, 2f), M.DoorDark);
            Geo.Box("DoorLeaf", n, new Vector3(doorL + 0.05f, doorH / 2 - 0.05f, z + 0.45f), new Vector3(0.05f, doorH - 0.1f, 0.95f), M.Get("DoorLeaf", new Color(0.1f, 0.1f, 0.11f), 0.2f));
            Geo.Box("DoorFrame", n, new Vector3((doorL + doorR) / 2, doorH + 0.04f, z + 0.02f), new Vector3(doorR - doorL + 0.1f, 0.08f, 0.06f), M.MetalBlack);

            // балкон-галерея (технологический уступ) по северной и восточной стенам
            Geo.Box("Ledge", n, new Vector3(0, 3.85f, z + 0.18f), new Vector3(Width, 0.12f, 0.36f), M.MetalBlack);
            Geo.Box("LedgeLip", n, new Vector3(0, 3.95f, z + 0.35f), new Vector3(Width, 0.08f, 0.03f), M.MetalBlack);

            // вентиляционная решётка
            var vent = Geo.Group("Vent", n, new Vector3(3.2f, 3.0f, z + 0.02f)).transform;
            Geo.Box("VentFrame", vent, Vector3.zero, new Vector3(3.0f, 0.95f, 0.04f), M.MetalBlack);
            for (int i = 0; i < 9; i++)
                Geo.Box("Slat", vent, new Vector3(0, -0.4f + i * 0.1f, 0.03f), new Vector3(2.9f, 0.025f, 0.04f), M.Get("Slat", new Color(0.2f, 0.2f, 0.21f), 0.3f), new Vector3(30, 0, 0));

            // силовой щит (красные розетки) на уступе
            var box = Geo.Group("PowerBox", n, new Vector3(6.6f, 4.25f, z + 0.25f)).transform;
            Geo.Box("Case", box, Vector3.zero, new Vector3(0.45f, 0.55f, 0.18f), M.MetalBlack);
            for (int i = 0; i < 4; i++)
                Geo.Cyl("Socket", box, new Vector3((i % 2 == 0 ? -0.1f : 0.1f), (i < 2 ? 0.08f : -0.12f), 0.1f), 0.07f, 0.05f, M.RedBox, new Vector3(90, 0, 0));
            // красный кабель вдоль стены
            Geo.Rod("RedCable", n, new Vector3(3.0f, 4.0f, z + 0.05f), new Vector3(6.4f, 4.05f, z + 0.05f), 0.01f, M.RedBox);
            // вертикальная труба справа
            Geo.Rod("Pipe", n, new Vector3(7.2f, 0, z + 0.15f), new Vector3(7.2f, 4.5f, z + 0.15f), 0.04f, M.MetalBlack);
            // выключатель
            Geo.Box("Switch", n, new Vector3(1.5f, 4.6f, z + 0.02f), new Vector3(0.12f, 0.12f, 0.04f), M.PlasticWhite);
        }

        void BuildEastWall()
        {
            float x = HalfW;
            var e = Geo.Group("EastWall", room, Vector3.zero).transform;
            Geo.Box("Wall", e, new Vector3(x + 0.1f, Height / 2, 0), new Vector3(0.2f, Height, Depth), M.WallBlack);
            Geo.Box("Ledge", e, new Vector3(x - 0.18f, 3.85f, 0), new Vector3(0.36f, 0.12f, Depth), M.MetalBlack);

            // белая стена (на фото — большой белый фон справа)
            whiteWallMat = M.Unique("WhiteWallPaint", new Color(0.93f, 0.93f, 0.92f), 0.05f);
            wallBodyMat = M.Unique("WhiteWallBody", whiteWallMat.color, 0.05f);
            Geo.Box("WhiteWall", e, new Vector3(x - 0.12f, 1.85f, 0.4f), new Vector3(0.12f, 3.7f, 8.0f), wallBodyMat);
            // отдельная лицевая плоскость, чтобы картинка клиента не растягивалась по торцам
            var face = Geo.Quad("WhiteWallFace", e, new Vector3(x - 0.181f, 1.85f, 0.4f), new Vector2(8.0f, 3.7f), whiteWallMat, new Vector3(0, 90, 0));
            whiteWall = face.GetComponent<Renderer>();

            // угол СВ: ниша с драпировкой и кабельной стойкой
            Geo.Curtain("CornerDrape", e, new Vector3(x - 0.4f, 0, -5.1f), -90, 2.6f, 3.6f, M.Curtain, 0.25f, 0.07f);
            Geo.Box("Lintel", e, new Vector3(x - 0.4f, 3.65f, -5.3f), new Vector3(0.1f, 0.12f, 2.4f), M.MetalBlack);
            var rack = Geo.Group("CableRack", e, new Vector3(x - 0.6f, 0, -4.4f)).transform;
            Geo.Box("Desk", rack, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1.0f, 1.2f), M.MetalBlack);
            for (int i = 0; i < 5; i++)
                Geo.Cyl("CableCoil", rack, new Vector3(0.25f, 1.4f + i * 0.08f, -0.3f + i * 0.15f), 0.16f, 0.06f, M.PlasticBlue, new Vector3(0, 0, 90));
        }

        void BuildSouthWall()
        {
            float z = HalfD;
            var s = Geo.Group("SouthWall", room, Vector3.zero).transform;
            Geo.Box("Wall", s, new Vector3(0, Height / 2, z + 0.1f), new Vector3(Width, Height, 0.2f), M.WallBlack);
            // проход на склад (стопки стульев, EXIT)
            Geo.Box("StorageOpening", s, new Vector3(6.4f, 1.3f, z + 0.05f), new Vector3(2.0f, 2.6f, 0.12f), M.DoorDark);
            Geo.Box("ExitSign", s, new Vector3(6.4f, 2.85f, z - 0.02f), new Vector3(0.35f, 0.15f, 0.04f), M.ExitSign);
            var storage = Geo.Group("ChairStack", s, new Vector3(6.4f, 0, z - 0.3f)).transform;
            for (int i = 0; i < 2; i++)
                Geo.Box("Stack", storage, new Vector3(-0.4f + i * 0.8f, 0.85f, 0), new Vector3(0.45f, 1.7f, 0.45f), M.Wood);
            // карниз и кулисы
            Geo.Box("CurtainTrack", s, new Vector3(0, 6.2f, z - 0.5f), new Vector3(Width, 0.06f, 0.08f), M.MetalGrey);
            Geo.Curtain("BeigeCurtain", s, new Vector3(4.6f, 0, z - 0.5f), 180, 1.4f, 6.2f, M.CurtainBeige, 0.22f, 0.06f);
            Geo.Curtain("BlackCurtainS", s, new Vector3(-2.15f, 0, z - 0.55f), 180, 11.7f, 6.2f, M.Curtain);
            Geo.Curtain("BlackDrapeDoor", s, new Vector3(7.6f, 0, z - 0.5f), 180, 0.8f, 6.2f, M.Curtain, 0.2f, 0.05f);
        }

        void BuildWestWall()
        {
            float x = -HalfW;
            var w = Geo.Group("WestWall", room, Vector3.zero).transform;
            Geo.Box("Wall", w, new Vector3(x - 0.1f, Height / 2, 0), new Vector3(0.2f, Height, Depth), M.WallBlack);
            Geo.Box("CurtainTrack", w, new Vector3(x + 0.5f, 6.2f, 0), new Vector3(0.08f, 0.06f, Depth), M.MetalGrey);
            Geo.Curtain("BlackCurtainW", w, new Vector3(x + 0.5f, 0, 0.2f), 90, Depth - 0.6f, 6.2f, M.Curtain);
        }

        // ---------------- Решётка и свет ----------------

        void BuildGrid()
        {
            var g = Geo.Group("Grid", rig, Vector3.zero).transform;
            for (float x = -HalfW + 1; x <= HalfW - 1; x += 1.5f)
                Geo.Box("RailZ", g, new Vector3(x, GridY + 0.25f, 0), new Vector3(0.08f, 0.08f, Depth - 0.4f), M.MetalBlack);
            for (float z = -HalfD + 1; z <= HalfD - 1; z += 2.2f)
                Geo.Box("RailX", g, new Vector3(0, GridY + 0.45f, z), new Vector3(Width - 0.4f, 0.1f, 0.1f), M.MetalBlack);
            // жёлтая труба по периметру (видна на фото)
            Geo.Box("YellowPipeN", g, new Vector3(0, GridY - 0.1f, -HalfD + 0.4f), new Vector3(Width - 0.6f, 0.05f, 0.05f), M.PipeYellow);
            Geo.Box("YellowPipeW", g, new Vector3(-HalfW + 0.4f, GridY - 0.1f, 0), new Vector3(0.05f, 0.05f, Depth - 0.6f), M.PipeYellow);
            Geo.Box("YellowPipeE", g, new Vector3(HalfW - 0.4f, GridY - 0.1f, 0), new Vector3(0.05f, 0.05f, Depth - 0.6f), M.PipeYellow);
            // технологический свет под потолком
            for (int i = 0; i < 4; i++)
                Geo.Box("WorkLight", g, new Vector3(-5 + i * 3.4f, GridY + 0.6f, -1), new Vector3(1.2f, 0.05f, 0.25f), M.Emissive("WorkLight", Color.white, 1.2f));
        }

        enum Fx { FresnelL, FresnelS, Soft, SoftTeal }

        struct FixtureDef
        {
            public Vector3 pos; public float drop; public Fx type; public int tag; public Vector3 aim; public float intensity;
            public FixtureDef(float x, float z, float drop, Fx type, int tag, Vector3 aim, float intensity)
            { pos = new Vector3(x, GridY, z); this.drop = drop; this.type = type; this.tag = tag; this.aim = aim; this.intensity = intensity; }
        }

        void BuildFixtures()
        {
            var c = SetCenter;
            var face = c + new Vector3(0, 0.4f, 0);
            var defs = new[]
            {
                // ключевые/заполняющие на сет
                new FixtureDef(-1.5f, 1.5f, 2.6f, Fx.FresnelL, 15, face, 2.2f),
                new FixtureDef( 3.5f, 1.8f, 2.4f, Fx.FresnelL, 16, face, 1.8f),
                new FixtureDef( 1.0f, 2.8f, 2.9f, Fx.Soft,     11, face, 1.4f),
                new FixtureDef(-3.0f,-0.5f, 2.2f, Fx.Soft,     17, face, 1.0f),
                new FixtureDef( 4.5f,-3.5f, 2.7f, Fx.FresnelS, 21, c,    1.4f),   // контровой
                new FixtureDef(-1.5f,-3.8f, 2.5f, Fx.FresnelS, 20, c,    1.3f),   // контровой
                new FixtureDef( 2.5f,-2.0f, 1.8f, Fx.SoftTeal, 24, c,    0.8f),
                // на белую стену
                new FixtureDef( 5.0f, 2.5f, 1.6f, Fx.FresnelS, 23, new Vector3(HalfW, 1.8f, 0.5f), 1.2f),
                new FixtureDef( 5.5f,-2.0f, 1.5f, Fx.Soft,     12, new Vector3(HalfW, 1.8f, -1.0f), 0.8f),
                // по периметру (подняты, слабее — дежурный свет)
                new FixtureDef(-6.0f,-4.5f, 1.2f, Fx.FresnelS, 1,  new Vector3(-6, 0, -2), 0.6f),
                new FixtureDef(-6.0f, 3.5f, 1.4f, Fx.FresnelS, 2,  new Vector3(-4, 0, 2), 0.6f),
                new FixtureDef(-3.5f, 4.5f, 1.0f, Fx.SoftTeal, 3,  new Vector3(-2, 0, 3), 0.5f),
                new FixtureDef( 6.5f,-5.0f, 0.9f, Fx.FresnelL, 4,  new Vector3(4, 0, -3), 0.6f),
                new FixtureDef( 0.0f,-5.2f, 1.0f, Fx.SoftTeal, 5,  new Vector3(0, 0, -3), 0.5f),
            };

            foreach (var d in defs)
            {
                var root = Geo.Group("Fixture_" + d.tag, rig, d.pos).transform;
                Pantograph(root, d.drop);
                var head = Geo.Group("Head", root, new Vector3(0, -d.drop - 0.25f, 0)).transform;
                head.rotation = Quaternion.LookRotation((d.aim - head.position).normalized, Vector3.up);
                Light l;
                switch (d.type)
                {
                    case Fx.FresnelL: l = PropFactory.Fresnel(head, true); break;
                    case Fx.FresnelS: l = PropFactory.Fresnel(head, false); break;
                    case Fx.SoftTeal: l = PropFactory.SoftPanel(head, true); break;
                    default: l = PropFactory.SoftPanel(head, false); break;
                }
                // бирка с номером
                Geo.Quad("Tag", root, new Vector3(0.18f, -d.drop + 0.15f, 0.02f), new Vector2(0.22f, 0.16f), M.NumberTag(d.tag), new Vector3(0, 180, 0));
                l.shadows = d.intensity >= 1.4f ? LightShadows.Soft : LightShadows.None;
                l.intensity = d.intensity;
                fixtureLights.Add(l);
                fixtureBaseIntensity.Add(d.intensity);
            }
        }

        /// <summary>Пантограф: «ножницы» от решётки вниз на drop метров.</summary>
        void Pantograph(Transform root, float drop)
        {
            Geo.Box("Trolley", root, new Vector3(0, 0.1f, 0), new Vector3(0.35f, 0.18f, 0.25f), M.MetalBlack);
            int n = Mathf.Max(2, Mathf.RoundToInt(drop / 0.22f));
            float seg = drop / n;
            const float half = 0.11f;
            for (int i = 0; i < n; i++)
            {
                float y0 = -i * seg, y1 = -(i + 1) * seg;
                Geo.Rod("X1", root, new Vector3(-half, y0, 0), new Vector3(half, y1, 0), 0.008f, M.MetalBlack);
                Geo.Rod("X2", root, new Vector3(half, y0, 0), new Vector3(-half, y1, 0), 0.008f, M.MetalBlack);
            }
            // кабель
            Geo.Rod("Cable", root, new Vector3(0.05f, 0, 0.05f), new Vector3(0.05f, -drop, 0.05f), 0.006f, M.Rubber);
            Geo.Box("Plate", root, new Vector3(0, -drop - 0.02f, 0), new Vector3(0.25f, 0.04f, 0.2f), M.MetalBlack);
            Geo.Box("Connector", root, new Vector3(0.1f, -drop + 0.1f, 0.08f), new Vector3(0.06f, 0.08f, 0.06f), M.PlasticBlue);
        }

        // ---------------- Текущая расстановка (как на фото) ----------------

        void BuildDefaultSet()
        {
            var grey = new Color(0.62f, 0.62f, 0.60f);
            PropFactory.Create("armchair", set, new Vector3(1.6f, 0, -1.6f), -150, grey);
            PropFactory.Create("armchair", set, new Vector3(0.6f, 0, -0.2f), 30, grey);
            PropFactory.Create("table_round", set, new Vector3(1.05f, 0, -0.9f), 0);
            PropFactory.Create("table_round", set, new Vector3(1.45f, 0, -0.55f), 0);
            PropFactory.Create("shoji", set, new Vector3(3.7f, 0, -2.6f), -60, new Color(0.95f, 0.95f, 0.95f));
            PropFactory.Create("shoji", set, new Vector3(1.2f, 0, -3.2f), 15, new Color(0.80f, 0.45f, 0.15f));
            PropFactory.Create("shoji", set, new Vector3(3.6f, 0, 1.0f), -100, new Color(0.45f, 0.42f, 0.40f));

            PropFactory.Create("tv_stand", set, new Vector3(5.2f, 0, 1.6f), -120);
            PropFactory.Create("pedestal_cam", set, new Vector3(-4.0f, 0, 1.0f), 100);
            PropFactory.Create("studio_cam", set, new Vector3(-4.6f, 0, -1.4f), 80);
            PropFactory.Create("studio_cam", set, new Vector3(-5.2f, 0, -4.4f), 60);
            PropFactory.Create("studio_cam", set, new Vector3(-1.0f, 0, 4.4f), 160);
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.17f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            if (!Application.isPlaying) return; // не трогаем настройки проекта из превью
            QualitySettings.pixelLightCount = 16;
            QualitySettings.shadowDistance = 30f;
        }

        // ---------------- Управление светом ----------------

        public void ApplyLighting(float master, float kelvin, bool[] enabledMask = null)
        {
            var col = Mathf.CorrelatedColorTemperatureToRGB(kelvin);
            for (int i = 0; i < fixtureLights.Count; i++)
            {
                var l = fixtureLights[i];
                if (l == null) continue;
                l.color = col;
                l.intensity = fixtureBaseIntensity[i] * master;
                if (enabledMask != null && i < enabledMask.Length) l.enabled = enabledMask[i];
            }
        }

        public void SetWhiteWall(Color c, Texture2D tex)
        {
            whiteWallMat.color = tex != null ? Color.white : c;
            whiteWallMat.mainTexture = tex;
            wallBodyMat.color = c;
        }
    }
}
