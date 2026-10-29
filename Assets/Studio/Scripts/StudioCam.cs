using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Studiokamera (Kamerakopf auf Stativ/Pumpstativ): Schwenk, Neigung, Brennweite und Höhe.
    /// Durch das Auge (eye) kann man im Sucher-Modus schauen; dieselben Werte drehen das 3D-Modell.
    /// </summary>
    public class StudioCam : MonoBehaviour
    {
        // 2/3"-Broadcast-Sensor (B4-Objektive, z. B. Fujinon UA20x7.6)
        public const float SensorHeightMm = 5.39f;
        public const float MinFocal = 7.6f, MaxFocal = 152f;

        public int number = 1;
        public float pan, tilt = -4f, focal = 24f, height = 1.35f;
        public float minHeight = 0.9f, maxHeight = 1.8f;

        public Transform panPivot, tiltPivot, eye, column;
        public Material tallyMat;
        public Renderer[] numberTags = new Renderer[0];
        public bool live;

        /// <summary>Vertikaler Bildwinkel des 16:9-Bildes in Grad.</summary>
        public float VerticalFov => 2f * Mathf.Atan(SensorHeightMm * 0.5f / focal) * Mathf.Rad2Deg;

        public void Apply()
        {
            tilt = Mathf.Clamp(tilt, -45f, 40f);
            focal = Mathf.Clamp(focal, MinFocal, MaxFocal);
            height = Mathf.Clamp(height, minHeight, maxHeight);
            pan = Mathf.Repeat(pan + 180f, 360f) - 180f;
            if (panPivot != null)
            {
                panPivot.localPosition = new Vector3(0, height, 0);
                panPivot.localRotation = Quaternion.Euler(0, pan, 0);
            }
            if (tiltPivot != null) tiltPivot.localRotation = Quaternion.Euler(-tilt, 0, 0);
            if (column != null)
            {
                // Mittelsäule füllt den Abstand zwischen Stativschale/Säule und Schwenkkopf
                float h = Mathf.Max(0.01f, height - _columnBase);
                column.localScale = new Vector3(column.localScale.x, h * 0.5f, column.localScale.z);
                column.localPosition = new Vector3(0, _columnBase + h * 0.5f, 0);
            }
        }

        float _columnBase;
        public void SetColumn(Transform col, float baseY) { column = col; _columnBase = baseY; }

        public void SetNumber(int n)
        {
            number = n;
            foreach (var r in numberTags) if (r != null) r.sharedMaterial = StudioMaterials.NumberTag(n);
        }

        public void SetLive(bool on)
        {
            live = on;
            if (tallyMat != null) tallyMat.SetColor("_EmissionColor", on ? new Color(1f, 0.05f, 0.05f) * 4f : new Color(0.25f, 0.02f, 0.02f));
        }

        /// <summary>Kamera so drehen, dass sie auf einen Punkt in der Welt schaut.</summary>
        public void AimAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world) - new Vector3(0, height, 0);
            pan = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            tilt = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            Apply();
        }
    }
}
