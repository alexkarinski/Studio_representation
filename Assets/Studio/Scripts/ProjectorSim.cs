using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Beamer: wirft ein Bild auf die Fläche, auf die er zeigt (Wände, weiße Wand, Vorhänge, Boden, Objekte).
    /// Die Bildgröße folgt aus Entfernung und Projektionsverhältnis (Throw Ratio), wie bei einem echten Beamer.
    /// </summary>
    public class ProjectorSim : MonoBehaviour
    {
        public float pan, tilt = 5f, height = 1.1f, throwRatio = 1.4f, brightness = 1f;
        public float minHeight = 0.4f, maxHeight = 2.2f;
        public Transform panPivot, tiltPivot, lensPoint, column;
        public Material projectionMat;
        public Light spill;
        float _columnBase;

        Transform _screen;

        public void SetColumn(Transform col, float baseY) { column = col; _columnBase = baseY; }

        void OnDisable() { if (_screen != null) _screen.gameObject.SetActive(false); }

        public void Apply()
        {
            pan = Mathf.Repeat(pan + 180f, 360f) - 180f;
            tilt = Mathf.Clamp(tilt, -60f, 60f);
            height = Mathf.Clamp(height, minHeight, maxHeight);
            throwRatio = Mathf.Clamp(throwRatio, 0.4f, 3f);
            brightness = Mathf.Clamp(brightness, 0.1f, 1.5f);
            panPivot.localPosition = new Vector3(0, height, 0);
            panPivot.localRotation = Quaternion.Euler(0, pan, 0);
            tiltPivot.localRotation = Quaternion.Euler(-tilt, 0, 0);
            if (column != null)
            {
                float h = Mathf.Max(0.01f, height - _columnBase);
                column.localScale = new Vector3(column.localScale.x, h * 0.5f, column.localScale.z);
                column.localPosition = new Vector3(0, _columnBase + h * 0.5f, 0);
            }
            if (spill != null) spill.intensity = 0.6f * brightness;
        }

        Texture MainTex => projectionMat != null ? projectionMat.mainTexture : null;

        void LateUpdate()
        {
            if (projectionMat == null || lensPoint == null) return;
            if (_screen == null) CreateScreen();

            var origin = lensPoint.position;
            var dir = lensPoint.forward;
            if (!Cast(origin, dir, out var hitPoint, out var normal, out float dist) || dist < 0.3f)
            {
                _screen.gameObject.SetActive(false);
                return;
            }
            _screen.gameObject.SetActive(gameObject.activeInHierarchy);

            var tex = MainTex;
            float aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 16f / 9f;
            float w = dist / throwRatio, h = w / aspect;
            // Bild liegt auf der Fläche, „oben“ ist die projizierte Hochachse des Beamers
            var up = Vector3.ProjectOnPlane(lensPoint.up, normal);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            _screen.SetPositionAndRotation(hitPoint + normal * 0.012f, Quaternion.LookRotation(-normal, up.normalized));
            _screen.localScale = new Vector3(w, h, 1f);
            projectionMat.color = new Color(brightness, brightness, brightness, Mathf.Clamp01(0.55f + brightness * 0.4f));
        }

        void CreateScreen()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Projektionsbild";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, true);
            // Vorderseite des Quads zeigt nach -Z = in den Raum (siehe LookRotation(-normal))
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = projectionMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _screen = go.transform;
        }

        /// <summary>Nächster Treffer: Objekte (Collider) oder die Flächen des Studios.</summary>
        bool Cast(Vector3 origin, Vector3 dir, out Vector3 point, out Vector3 normal, out float dist)
        {
            dist = float.MaxValue; point = Vector3.zero; normal = Vector3.up;
            foreach (var hit in Physics.RaycastAll(origin, dir, 40f))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < dist) { dist = hit.distance; point = hit.point; normal = hit.normal; }
            }
            // Studioflächen analytisch: Boden, Decke, weiße Wand, Vorhänge, Wände
            float hw = StudioBuilder.HalfW, hd = StudioBuilder.HalfD;
            Plane(origin, dir, new Vector3(0, 1, 0), 0f, ref dist, ref point, ref normal);                    // Boden
            Plane(origin, dir, new Vector3(0, -1, 0), StudioBuilder.Height, ref dist, ref point, ref normal);  // Decke
            Plane(origin, dir, new Vector3(-1, 0, 0), hw - 0.18f, ref dist, ref point, ref normal);            // Ost (weiße Wand)
            Plane(origin, dir, new Vector3(1, 0, 0), hw - 0.5f, ref dist, ref point, ref normal);              // West (Vorhang)
            Plane(origin, dir, new Vector3(0, 0, -1), hd - 0.55f, ref dist, ref point, ref normal);            // Süd (Vorhang)
            Plane(origin, dir, new Vector3(0, 0, 1), hd, ref dist, ref point, ref normal);                     // Nord (Wand)
            return dist < 39f;
        }

        /// <summary>Ebene mit Normale n (zeigt in den Raum) im Abstand d vom Ursprung entlang -n.</summary>
        static void Plane(Vector3 o, Vector3 dir, Vector3 n, float d, ref float best, ref Vector3 point, ref Vector3 normal)
        {
            float denom = Vector3.Dot(dir, n);
            if (denom >= -1e-4f) return; // nur Flächen, die zum Beamer zeigen
            // Ebene: dot(n, x) = -d
            float t = (-d - Vector3.Dot(n, o)) / denom;
            if (t > 0 && t < best) { best = t; point = o + dir * t; normal = n; }
        }
    }
}
