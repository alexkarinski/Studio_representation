using UnityEngine;
using M = Studio.StudioMaterials;

namespace Studio
{
    /// <summary>
    /// Hände in der Ich-Perspektive (Laufmodus): wippen beim Gehen, greifen nach vorn, wenn ein Objekt getragen wird.
    /// </summary>
    public class FirstPersonHands : MonoBehaviour
    {
        Transform _left, _right;
        float _phase, _carry, _speed;
        Vector3 _lastFeet;
        bool _init;

        public static FirstPersonHands Create(Camera cam)
        {
            var root = new GameObject("FirstPersonHands");
            root.transform.SetParent(cam.transform, false);
            var h = root.AddComponent<FirstPersonHands>();
            var sleeve = M.Get("FpSleeve", new Color(0.12f, 0.14f, 0.2f), 0.15f);
            var skin = M.Get("Skin", new Color(0.80f, 0.64f, 0.54f), 0.35f);
            h._left = Arm(root.transform, -1, sleeve, skin);
            h._right = Arm(root.transform, 1, sleeve, skin);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            root.SetActive(false);
            return h;
        }

        static Transform Arm(Transform parent, int side, Material sleeve, Material skin)
        {
            var arm = new GameObject(side < 0 ? "ArmL" : "ArmR").transform;
            arm.SetParent(parent, false);
            // Unterarm von unten-außen nach vorne-innen, Hand am Ende
            Geo.Limb("Sleeve", arm, new Vector3(0.05f * side, -0.12f, -0.12f), new Vector3(0.0f, 0f, 0.12f), 0.045f, sleeve);
            Geo.Limb("Wrist", arm, new Vector3(0.0f, 0f, 0.12f), new Vector3(-0.01f * side, 0.005f, 0.17f), 0.03f, skin);
            var hand = Geo.Box("Hand", arm, new Vector3(-0.015f * side, 0.01f, 0.22f), new Vector3(0.075f, 0.03f, 0.1f), skin);
            hand.transform.localRotation = Quaternion.Euler(10, -8 * side, 12 * side);
            for (int i = 0; i < 4; i++)
                Geo.Limb("Finger", arm, new Vector3((-0.045f + i * 0.022f) * side * -1f - 0.015f * side, 0.008f, 0.27f),
                    new Vector3((-0.045f + i * 0.024f) * side * -1f - 0.015f * side, -0.01f, 0.31f), 0.009f, skin);
            Geo.Limb("Thumb", arm, new Vector3(-0.045f * side, 0.012f, 0.21f), new Vector3(-0.07f * side, 0.02f, 0.26f), 0.011f, skin);
            arm.localPosition = new Vector3(0.24f * side, -0.3f, 0.2f);
            arm.localRotation = Quaternion.Euler(-8, -12 * side, 0);
            return arm;
        }

        /// <summary>Jedes Frame aus dem Editor: sichtbar im Laufmodus, Tragen, Fußposition für das Wippen.</summary>
        public void Tick(bool visible, bool carrying, Vector3 feet)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) { _init = false; return; }
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            if (!_init) { _lastFeet = feet; _init = true; }
            float v = Vector3.ProjectOnPlane(feet - _lastFeet, Vector3.up).magnitude / dt;
            _lastFeet = feet;
            _speed = Mathf.Lerp(_speed, v, dt * 8f);
            _phase += dt * (2f + _speed * 3.2f);
            _carry = Mathf.MoveTowards(_carry, carrying ? 1f : 0f, dt * 3f);

            float bob = Mathf.Sin(_phase * 2f) * 0.012f * Mathf.Clamp01(_speed / 1.5f) + Mathf.Sin(_phase * 0.5f) * 0.003f;
            float sway = Mathf.Sin(_phase) * 0.01f * Mathf.Clamp01(_speed / 1.5f);
            for (int i = 0; i < 2; i++)
            {
                var arm = i == 0 ? _left : _right;
                int side = i == 0 ? -1 : 1;
                // ruhig: Hände unten seitlich · tragend: nach vorn, etwas höher und enger (greift das Objekt)
                var idle = new Vector3(0.24f * side + sway, -0.32f + bob * (i == 0 ? 1 : -1), 0.18f);
                var hold = new Vector3(0.17f * side, -0.2f + bob * 0.4f, 0.32f);
                arm.localPosition = Vector3.Lerp(idle, hold, _carry);
                arm.localRotation = Quaternion.Slerp(Quaternion.Euler(-8, -12 * side, 0), Quaternion.Euler(-18, -26 * side, -30 * side), _carry);
            }
        }
    }
}
