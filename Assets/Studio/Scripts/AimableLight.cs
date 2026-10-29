using UnityEngine;

namespace Studio
{
    /// <summary>Ausrichtbarer Scheinwerfer (Stativ oder Bodenstrahler): Schwenk, Neigung, Helligkeit.</summary>
    public class AimableLight : MonoBehaviour
    {
        public float pan, tilt = -20f, intensity = 1.5f;
        public float minTilt = -80f, maxTilt = 30f;
        public Transform panPivot, tiltPivot;
        public Light lightSource;
        public Material lensMat;

        public void Apply()
        {
            pan = Mathf.Repeat(pan + 180f, 360f) - 180f;
            tilt = Mathf.Clamp(tilt, minTilt, maxTilt);
            intensity = Mathf.Clamp(intensity, 0f, 4f);
            if (panPivot != null) panPivot.localRotation = Quaternion.Euler(0, pan, 0);
            if (tiltPivot != null) tiltPivot.localRotation = Quaternion.Euler(-tilt, 0, 0);
            if (lightSource != null)
            {
                lightSource.intensity = intensity;
                lightSource.enabled = intensity > 0.01f;
            }
            if (lensMat != null) lensMat.SetColor("_EmissionColor", lensMat.color * (0.2f + intensity * 1.5f));
        }

        public void AimAt(Vector3 world)
        {
            var origin = tiltPivot != null ? tiltPivot.position : transform.position;
            var local = transform.InverseTransformDirection(world - origin);
            pan = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            tilt = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            Apply();
        }
    }

    /// <summary>Leichte „Lebendigkeit“ für Personen: Atmen und langsames Kopfdrehen.</summary>
    public class PersonIdle : MonoBehaviour
    {
        public Transform head, torso;
        float _seed;
        Vector3 _torsoScale;

        void Start()
        {
            _seed = Random.value * 10f;
            if (torso != null) _torsoScale = torso.localScale;
        }

        void Update()
        {
            float t = Time.time + _seed;
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.21f) * 4f, Mathf.Sin(t * 0.13f) * 18f, 0);
            if (torso != null) torso.localScale = _torsoScale * (1f + Mathf.Sin(t * 1.6f) * 0.008f);
        }
    }
}
