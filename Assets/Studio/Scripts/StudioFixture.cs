using UnityEngine;
using M = Studio.StudioMaterials;

namespace Studio
{
    /// <summary>
    /// Torblende („Klappen“): vier Flügel (oben, unten, links, rechts), jeder stufenlos in Grad verstellbar
    /// (−30° = nach außen gespreizt, 0° = parallel zum Strahl, 90° = vor der Linse geschlossen).
    /// Die Beschneidung wird geometrisch berechnet (Strahl von der Linse zur Flügelkante) und als Cookie
    /// auf das Licht gelegt — das Licht wird also tatsächlich abgeschattet.
    /// </summary>
    public class BarnDoors : MonoBehaviour
    {
        public static readonly string[] SideNames = { "oben", "unten", "links", "rechts" };
        public const float MinAngle = -30f, MaxAngle = 90f, OpenAngle = -20f;

        public Light target;
        public Transform[] leaves = new Transform[4];
        /// <summary>Winkel jedes Flügels in Grad (0 = parallel zur Strahlachse).</summary>
        public float[] angles = { OpenAngle, OpenAngle, OpenAngle, OpenAngle };
        public float hingeOffset = 0.2f, leafLength = 0.2f;

        Texture2D _cookie;
        readonly float[] _appliedAngles = { 999, 999, 999, 999 };
        float _appliedSpot = -1, _appliedInner = -1;
        public bool FullyClosed { get; private set; }

        /// <summary>Kompatibilität: Stufe 0…1 (¼-Schritte) ↔ Winkel.</summary>
        public static float AngleForStep(float step01) => Mathf.Lerp(OpenAngle, MaxAngle, step01);
        public static float StepForAngle(float angle) => Mathf.InverseLerp(OpenAngle, MaxAngle, angle);

        public void Apply()
        {
            for (int i = 0; i < 4; i++)
            {
                angles[i] = Mathf.Clamp(angles[i], MinAngle, MaxAngle);
                if (leaves[i] == null) continue;
                float a = angles[i];
                // Drehrichtung je Seite so, dass positive Winkel zur Strahlmitte schließen
                leaves[i].localRotation = i == 0 ? Quaternion.Euler(a, 0, 0) : i == 1 ? Quaternion.Euler(-a, 0, 0)
                                       : i == 2 ? Quaternion.Euler(0, a, 0) : Quaternion.Euler(0, -a, 0);
            }
            if (target == null) return;
            bool dirty = Mathf.Abs(_appliedSpot - target.spotAngle) > 0.01f || Mathf.Abs(_appliedInner - target.innerSpotAngle) > 0.01f;
            for (int i = 0; i < 4; i++) if (Mathf.Abs(_appliedAngles[i] - angles[i]) > 0.01f) dirty = true;
            if (!dirty) return;
            BuildCookie();
            for (int i = 0; i < 4; i++) _appliedAngles[i] = angles[i];
            _appliedSpot = target.spotAngle; _appliedInner = target.innerSpotAngle;
        }

        /// <summary>Kante des Lichtfelds auf dieser Seite, in Cookie-Koordinaten ab Mitte (0.5 = Kegelrand).</summary>
        float EdgeFromCenter(float angleDeg, float tanHalf, out float soft)
        {
            float phi = angleDeg * Mathf.Deg2Rad;
            const float lensToHinge = 0.02f;
            // Flügelspitze relativ zur Linsenmitte: seitlich (zur Achse hin positiv) und nach vorn
            float side = hingeOffset - leafLength * Mathf.Sin(phi);
            float fwd = lensToHinge + leafLength * Mathf.Cos(phi);
            if (fwd < 1e-3f) fwd = 1e-3f;
            float tanAlpha = side / fwd;                      // Grenzstrahl
            // Halbschatten: Linse ist keine Punktquelle → Kantenbreite ~ Linsenradius / Abstand
            soft = Mathf.Clamp(0.5f * (hingeOffset * 0.8f / Mathf.Max(0.05f, fwd + 0.05f)) / tanHalf * 0.25f, 0.02f, 0.2f);
            return 0.5f * tanAlpha / tanHalf;
        }

        void BuildCookie()
        {
            const int n = 96;
            if (_cookie == null)
                _cookie = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Torblende", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float tanHalf = Mathf.Tan(target.spotAngle * 0.5f * Mathf.Deg2Rad);
            float inner = Mathf.Clamp01(Mathf.Tan(target.innerSpotAngle * 0.5f * Mathf.Deg2Rad) / tanHalf) * 0.5f;
            var edge = new float[4];
            var soft = new float[4];
            for (int i = 0; i < 4; i++) edge[i] = EdgeFromCenter(angles[i], tanHalf, out soft[i]);
            // oben: v ≤ 0.5+edge0, unten: v ≥ 0.5−edge1, links: u ≥ 0.5−edge2, rechts: u ≤ 0.5+edge3
            float top = 0.5f + edge[0], bottom = 0.5f - edge[1], left = 0.5f - edge[2], right = 0.5f + edge[3];
            FullyClosed = top <= bottom + 0.01f || right <= left + 0.01f;

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                // runder Lichtkegel mit weichem Rand (ersetzt Unitys Standard-Spot-Cookie)
                float r = new Vector2(u - 0.5f, v - 0.5f).magnitude;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 0.5f, r));
                a *= Mathf.Clamp01((top - v) / soft[0] + 0.5f) * Mathf.Clamp01((v - bottom) / soft[1] + 0.5f)
                   * Mathf.Clamp01((u - left) / soft[2] + 0.5f) * Mathf.Clamp01((right - u) / soft[3] + 0.5f);
                if (x == 0 || y == 0 || x == n - 1 || y == n - 1) a = 0;
                byte b = (byte)(Mathf.Clamp01(a) * 255);
                px[y * n + x] = new Color32(b, b, b, b);
            }
            _cookie.SetPixels32(px);
            _cookie.Apply();
            target.cookie = _cookie;
        }

        /// <summary>Baut vier Klappen vor einer runden Linse (Kopf schaut entlang +Z).</summary>
        public static BarnDoors Create(Transform head, Light light, float rad, float front)
        {
            var bd = head.gameObject.AddComponent<BarnDoors>();
            bd.target = light;
            float len = rad * 1.15f, d = rad + 0.02f;
            bd.hingeOffset = d; bd.leafLength = len;
            var hinges = new[] { new Vector3(0, d, front), new Vector3(0, -d, front), new Vector3(-d, 0, front), new Vector3(d, 0, front) };
            for (int i = 0; i < 4; i++)
            {
                var pivot = Geo.Group("Klappe_" + SideNames[i], head, hinges[i]).transform;
                var size = i < 2 ? new Vector3(rad * 2.1f, 0.006f, len) : new Vector3(0.006f, rad * 1.7f, len);
                Geo.Box("Leaf", pivot, new Vector3(0, 0, len / 2), size, M.MetalBlack);
                bd.leaves[i] = pivot;
            }
            bd.Apply();
            return bd;
        }
    }

    /// <summary>
    /// Scheinwerfer an der Decke: Laufkatze auf der Schiene (vor/zurück, seitlich), Pantograf (Höhe),
    /// Kopf (Schwenk/Neigung), Dimmer, Fokus (Spot ↔ Flood), Torblende.
    /// </summary>
    public class StudioFixture : MonoBehaviour
    {
        public enum Kind { FresnelLarge, FresnelSmall, Soft, SoftTeal }

        public int number;
        public Kind kind;
        public bool on = true;
        public float x, z, drop = 2f, pan, tilt, dimmer = 1f, focus = 30f, baseIntensity = 1f;
        public float minFocus = 15f, maxFocus = 60f;
        /// <summary>Eigene Farbtemperatur statt der globalen.</summary>
        public bool ownKelvin;
        public float kelvin = 4300f;
        /// <summary>Farbfilter (Gel) vor der Linse; Weiß = kein Filter.</summary>
        public Color gel = Color.white;
        public string gelName = "kein Filter";
        /// <summary>Wirft dieser Scheinwerfer Schatten?</summary>
        public bool castShadows = true;
        /// <summary>Diffusionsfilter: 0 = keiner, 1 = ¼ Frost, 2 = ½ Frost, 3 = Full Frost.</summary>
        public int diffusion;
        public static readonly string[] DiffusionNames = { "keiner", "¼ Frost", "½ Frost", "Full Frost" };

        public static readonly (string name, Color c)[] Gels =
        {
            ("kein Filter", Color.white),
            ("CTO ½ (wärmer)", new Color(1f, 0.82f, 0.62f)), ("CTO (Halogen→warm)", new Color(1f, 0.66f, 0.38f)),
            ("CTB ½ (kühler)", new Color(0.78f, 0.86f, 1f)), ("CTB (Tageslicht)", new Color(0.6f, 0.74f, 1f)),
            ("Rot", new Color(1f, 0.12f, 0.1f)), ("Orange", new Color(1f, 0.45f, 0.1f)), ("Gelb", new Color(1f, 0.9f, 0.25f)),
            ("Grün", new Color(0.25f, 1f, 0.3f)), ("Cyan", new Color(0.2f, 0.95f, 1f)), ("Blau", new Color(0.15f, 0.3f, 1f)),
            ("Lila", new Color(0.55f, 0.25f, 1f)), ("Magenta", new Color(1f, 0.2f, 0.85f)), ("Pink", new Color(1f, 0.55f, 0.7f)),
        };

        public Transform pantograph, plate, panPivot, tiltPivot;
        public Light lightSource;
        public BarnDoors doors;
        public Material lensMat;

        public bool IsFresnel => kind == Kind.FresnelLarge || kind == Kind.FresnelSmall;
        public string Title => "Scheinwerfer Nr. " + number + (IsFresnel ? " (Stufenlinse)" : " (Softlight)");

        /// <summary>Höhe der Kopfmitte über dem Boden.</summary>
        public float HeadHeight => StudioBuilder.GridY - drop - 0.3f;

        public const float MinDrop = 0.6f, MaxDrop = 5.0f;
        public static float MinX => -StudioBuilder.HalfW + 1f;
        public static float MaxX => StudioBuilder.HalfW - 1f;
        public static float MinZ => -StudioBuilder.HalfD + 1f;
        public static float MaxZ => StudioBuilder.HalfD - 1f;

        public void Apply(float master, float globalKelvin)
        {
            x = Mathf.Clamp(x, MinX, MaxX);
            z = Mathf.Clamp(z, MinZ, MaxZ);
            drop = Mathf.Clamp(drop, MinDrop, MaxDrop);
            tilt = Mathf.Clamp(tilt, -90f, 30f);
            pan = Mathf.Repeat(pan + 180f, 360f) - 180f;
            dimmer = Mathf.Clamp01(dimmer);
            focus = Mathf.Clamp(focus, minFocus, maxFocus);

            transform.localPosition = new Vector3(x, StudioBuilder.GridY, z);
            if (pantograph != null) pantograph.localScale = new Vector3(1, drop, 1);
            if (plate != null) plate.localPosition = new Vector3(0, -drop, 0);
            if (panPivot != null) panPivot.localRotation = Quaternion.Euler(0, pan, 0);
            if (tiltPivot != null) tiltPivot.localRotation = Quaternion.Euler(-tilt, 0, 0);
            if (doors != null) doors.Apply();

            if (lightSource == null) return;
            // Lichtstrom bleibt beim Fokussieren gleich: schmaler Kegel → hellere Mitte (≈ 1/Raumwinkel)
            float focusGain = 1f;
            if (IsFresnel)
            {
                lightSource.spotAngle = focus;
                lightSource.innerSpotAngle = focus * 0.45f;
                float ref_ = 30f;
                focusGain = Mathf.Clamp((1f - Mathf.Cos(ref_ * 0.5f * Mathf.Deg2Rad)) / (1f - Mathf.Cos(focus * 0.5f * Mathf.Deg2Rad)), 0.25f, 4f);
            }
            // Diffusor: streut das Licht → weichere, schwächere Schatten, etwas Lichtverlust, breiterer Rand
            diffusion = Mathf.Clamp(diffusion, 0, 3);
            float diffLoss = 1f - 0.12f * diffusion;
            if (diffusion > 0 && IsFresnel) lightSource.innerSpotAngle = lightSource.spotAngle * Mathf.Max(0.1f, 0.45f - 0.12f * diffusion);
            bool soft = !IsFresnel;
            lightSource.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            lightSource.shadowStrength = (soft ? 0.55f : 0.9f) * (1f - 0.22f * diffusion);

            // Dimmer: Helligkeit wie bei Glühlicht-Dimmerkurve leicht quadratisch
            float level = dimmer * dimmer;
            lightSource.intensity = baseIntensity * master * level * focusGain * diffLoss;
            lightSource.enabled = on && level > 0.001f && (doors == null || !doors.FullyClosed);
            float k = ownKelvin ? kelvin : globalKelvin;
            lightSource.useColorTemperature = true;
            lightSource.colorTemperature = k;
            // Gel wirkt wie ein Filter: Lichtfarbe = Farbtemperatur × Filterfarbe
            lightSource.color = gel;
            if (lensMat != null)
            {
                var c = Mathf.CorrelatedColorTemperatureToRGB(k) * gel;
                lensMat.SetColor("_EmissionColor", lightSource.enabled ? c * (0.1f + 3f * level) : Color.black);
            }
        }

        public void AimAt(Vector3 world)
        {
            if (panPivot == null) return;
            var origin = tiltPivot != null ? tiltPivot.position : panPivot.position;
            var local = transform.InverseTransformDirection(world - origin);
            pan = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            tilt = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        }

        // ---------------- Aufbau ----------------

        public static StudioFixture Create(Transform parent, int number, Kind kind, float x, float z, float drop, float intensity)
        {
            var root = new GameObject("Fixture_" + number);
            root.transform.SetParent(parent, false);
            var f = root.AddComponent<StudioFixture>();
            f.number = number; f.kind = kind; f.x = x; f.z = z; f.drop = drop; f.baseIntensity = intensity;

            Geo.Box("Trolley", root.transform, new Vector3(0, 0.1f, 0), new Vector3(0.35f, 0.18f, 0.25f), M.MetalBlack);
            Geo.Cyl("Wheel", root.transform, new Vector3(0, 0.2f, 0.1f), 0.04f, 0.04f, M.MetalGrey, new Vector3(0, 0, 90));
            Geo.Cyl("Wheel", root.transform, new Vector3(0, 0.2f, -0.1f), 0.04f, 0.04f, M.MetalGrey, new Vector3(0, 0, 90));

            // Pantograf für Länge 1 bauen, per Skalierung auf „drop“ strecken
            f.pantograph = Geo.Group("Pantograph", root.transform, Vector3.zero).transform;
            const int segs = 9;
            const float half = 0.11f;
            for (int i = 0; i < segs; i++)
            {
                float y0 = -(float)i / segs, y1 = -(float)(i + 1) / segs;
                Geo.Rod("X1", f.pantograph, new Vector3(-half, y0, 0), new Vector3(half, y1, 0), 0.008f, M.MetalBlack);
                Geo.Rod("X2", f.pantograph, new Vector3(half, y0, 0), new Vector3(-half, y1, 0), 0.008f, M.MetalBlack);
            }
            Geo.Rod("Cable", f.pantograph, new Vector3(0.05f, 0, 0.05f), new Vector3(0.05f, -1f, 0.05f), 0.006f, M.Rubber);

            f.plate = Geo.Group("Plate", root.transform, new Vector3(0, -drop, 0)).transform;
            Geo.Box("PlateBox", f.plate, new Vector3(0, -0.02f, 0), new Vector3(0.25f, 0.04f, 0.2f), M.MetalBlack);
            Geo.Box("Connector", f.plate, new Vector3(0.1f, 0.06f, 0.08f), new Vector3(0.06f, 0.08f, 0.06f), M.PlasticBlue);
            Geo.Quad("Tag", f.plate, new Vector3(0.18f, 0.12f, 0.02f), new Vector2(0.22f, 0.16f), M.NumberTag(number), new Vector3(0, 180, 0));

            f.panPivot = Geo.Group("PanPivot", f.plate, new Vector3(0, -0.04f, 0)).transform;
            bool fresnel = kind == Kind.FresnelLarge || kind == Kind.FresnelSmall;
            float rad = kind == Kind.FresnelLarge ? 0.17f : 0.12f, len = kind == Kind.FresnelLarge ? 0.45f : 0.32f;
            float yokeH = fresnel ? rad + 0.12f : 0.26f, yokeW = fresnel ? rad + 0.05f : 0.36f;
            Geo.Rod("YokeTop", f.panPivot, new Vector3(-yokeW, 0, 0), new Vector3(yokeW, 0, 0), 0.014f, M.MetalBlack);
            Geo.Rod("YokeL", f.panPivot, new Vector3(-yokeW, 0, 0), new Vector3(-yokeW, -yokeH, 0), 0.014f, M.MetalBlack);
            Geo.Rod("YokeR", f.panPivot, new Vector3(yokeW, 0, 0), new Vector3(yokeW, -yokeH, 0), 0.014f, M.MetalBlack);
            f.tiltPivot = Geo.Group("TiltPivot", f.panPivot, new Vector3(0, -yokeH, 0)).transform;

            if (fresnel)
            {
                Geo.Cyl("Housing", f.tiltPivot, Vector3.zero, rad, len, M.MetalBlack, new Vector3(90, 0, 0));
                f.lensMat = M.Unique("FresnelLens", new Color(1f, 0.92f, 0.8f), 0.8f);
                f.lensMat.EnableKeyword("_EMISSION");
                Geo.Cyl("Lens", f.tiltPivot, new Vector3(0, 0, len / 2 + 0.005f), rad * 0.85f, 0.01f, f.lensMat, new Vector3(90, 0, 0));
                Geo.Box("Fan", f.tiltPivot, new Vector3(0, 0, -len / 2 - 0.04f), new Vector3(rad * 1.6f, rad * 1.6f, 0.08f), M.MetalBlack);
                Geo.Cyl("FocusKnob", f.tiltPivot, new Vector3(0, -rad - 0.02f, -len / 4), 0.02f, 0.04f, M.MetalBlack);
                var l = MakeLight(f.tiltPivot, new Vector3(0, 0, len / 2 + 0.03f), 14f);
                l.shadows = LightShadows.Soft;
                f.lightSource = l;
                f.focus = kind == Kind.FresnelLarge ? 38f : 30f;
                f.minFocus = 12f; f.maxFocus = 62f;
                f.doors = BarnDoors.Create(f.tiltPivot, l, rad, len / 2 + 0.01f);
            }
            else
            {
                Geo.Box("Frame", f.tiltPivot, new Vector3(0, 0, -0.04f), new Vector3(0.66f, 0.38f, 0.07f), kind == Kind.SoftTeal ? M.PlasticTeal : M.MetalBlack);
                f.lensMat = M.Unique("SoftFace", new Color(1f, 0.97f, 0.92f), 0.6f);
                f.lensMat.EnableKeyword("_EMISSION");
                Geo.Box("Face", f.tiltPivot, Vector3.zero, new Vector3(0.6f, 0.32f, 0.01f), f.lensMat);
                var l = MakeLight(f.tiltPivot, new Vector3(0, 0, 0.05f), 12f);
                l.spotAngle = 110f; l.innerSpotAngle = 50f;
                // große Flächenleuchte: Schatten weich und schwach (Halbschatten der Fläche)
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.55f;
                f.lightSource = l;
                f.focus = 110f; f.minFocus = 110f; f.maxFocus = 110f;
            }
            Geo.FitCollider(root);
            return f;
        }

        static Light MakeLight(Transform parent, Vector3 pos, float range)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            l.type = LightType.Spot;
            l.range = range;
            l.shadowStrength = 0.9f;
            // kleine Biaswerte: Schatten „klebt“ an Füßen und Stuhlbeinen (Kontaktschatten)
            l.shadowBias = 0.01f;
            l.shadowNormalBias = 0.15f;
            l.shadowNearPlane = 0.1f;
            l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            return l;
        }

        /// <summary>Collider an die aktuelle Pantografenlänge anpassen (zum Anklicken).</summary>
        public void RefitCollider() => Geo.FitCollider(gameObject);
    }
}
