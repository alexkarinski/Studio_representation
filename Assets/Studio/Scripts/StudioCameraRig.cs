using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Орбитальная камера: ПКМ/ЛКМ по пустому месту — вращение, СКМ или Shift+ЛКМ — панорама,
    /// колесо — зум, WASD — перемещение по полу. Пресеты повторяют ракурсы фотографий.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StudioCameraRig : MonoBehaviour
    {
        public struct Preset
        {
            public string name; public Vector3 eye, target; public float fov;
            public Preset(string n, Vector3 e, Vector3 t, float f = 60) { name = n; eye = e; target = t; fov = f; }
        }

        // Ракурсы подобраны под 4 фото (телефон ~ 24-26 мм, горизонтальный кадр)
        public static readonly Preset[] Presets =
        {
            new Preset("Фото 1 — на северную стену", new Vector3(5.0f, 1.75f, 4.6f),  new Vector3(-1.5f, 2.6f, -6.5f), 62),
            new Preset("Фото 2 — белая стена, занавес", new Vector3(-2.0f, 1.8f, -4.2f), new Vector3(5.0f, 2.2f, 6.5f), 62),
            new Preset("Фото 3 — угол СВ", new Vector3(-5.2f, 1.75f, 4.8f), new Vector3(5.5f, 2.8f, -5.5f), 62),
            new Preset("Фото 4 — угол СВ, ниже", new Vector3(-4.6f, 1.55f, 5.2f), new Vector3(5.5f, 3.1f, -5.0f), 64),
            new Preset("План сверху", new Vector3(0.01f, 17f, 0.6f), new Vector3(0, 0, 0), 55),
            new Preset("Эфирный кадр (общий)", new Vector3(-3.6f, 1.45f, 0.9f), new Vector3(1.0f, 1.0f, -0.9f), 45),
            new Preset("Эфирный кадр (крупный)", new Vector3(-1.4f, 1.25f, 0.2f), new Vector3(1.4f, 1.0f, -1.3f), 30),
        };

        public float orbitSpeed = 0.25f, panSpeed = 0.01f, zoomSpeed = 1.2f, moveSpeed = 4f;
        /// <summary>Устанавливается редактором, когда мышь над UI или тащит объект.</summary>
        public System.Func<bool> inputBlocked;

        Camera _cam;
        Vector3 _target;
        float _yaw, _pitch, _dist;
        Vector3 _lastMouse;
        bool _orbiting, _panning;

        // плавный перелёт к пресету
        Vector3 _flyEye, _flyTarget; float _flyFov; float _flyT = 1f;
        Vector3 _fromEye, _fromTarget; float _fromFov;

        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

        void Awake()
        {
            Cam.renderingPath = RenderingPath.DeferredShading; // много прожекторов
            Cam.allowHDR = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 200f;
            Apply(Presets[0], instant: true);
        }

        public void Apply(Preset p, bool instant = false)
        {
            if (instant)
            {
                SetLook(p.eye, p.target);
                Cam.fieldOfView = p.fov;
                _flyT = 1f;
                return;
            }
            _fromEye = transform.position; _fromTarget = _target; _fromFov = Cam.fieldOfView;
            _flyEye = p.eye; _flyTarget = p.target; _flyFov = p.fov; _flyT = 0f;
        }

        void SetLook(Vector3 eye, Vector3 target)
        {
            _target = target;
            var d = eye - target;
            _dist = d.magnitude;
            _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(d.y / Mathf.Max(_dist, 0.001f), -1f, 1f)) * Mathf.Rad2Deg;
            UpdateTransform();
        }

        void UpdateTransform()
        {
            _pitch = Mathf.Clamp(_pitch, -80f, 89f);
            _dist = Mathf.Clamp(_dist, 0.5f, 40f);
            var offset = Quaternion.Euler(-_pitch, _yaw, 0) * Vector3.forward * _dist;
            transform.position = _target + offset;
            transform.rotation = Quaternion.LookRotation(_target - transform.position, Vector3.up);
        }

        void LateUpdate()
        {
            if (_flyT < 1f)
            {
                _flyT = Mathf.Min(1f, _flyT + Time.unscaledDeltaTime * 1.4f);
                float k = Mathf.SmoothStep(0, 1, _flyT);
                SetLook(Vector3.Lerp(_fromEye, _flyEye, k), Vector3.Lerp(_fromTarget, _flyTarget, k));
                Cam.fieldOfView = Mathf.Lerp(_fromFov, _flyFov, k);
                return;
            }

            bool blocked = inputBlocked != null && inputBlocked();
            var mouse = Input.mousePosition;
            var delta = mouse - _lastMouse;
            _lastMouse = mouse;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!blocked)
            {
                if (Input.GetMouseButtonDown(1) || (Input.GetMouseButtonDown(0) && !shift)) _orbiting = true;
                if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(0) && shift)) { _panning = true; _orbiting = false; }
            }
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) _orbiting = false;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(2)) _panning = false;
            if (blocked && Input.GetMouseButton(0)) _orbiting = _panning = false;

            if (_orbiting)
            {
                // перетаскивание влево поворачивает камеру налево
                _yaw += delta.x * orbitSpeed;
                _pitch -= delta.y * orbitSpeed;
            }
            if (_panning)
            {
                var right = transform.right;
                var up = transform.up;
                _target -= (right * delta.x + up * delta.y) * panSpeed * Mathf.Max(1f, _dist * 0.3f);
            }
            if (!blocked)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _dist *= Mathf.Pow(0.88f, wheel * zoomSpeed);
            }

            // WASD — перемещение по полу (не при вводе текста в UI)
            if (GUIUtility.keyboardControl == 0)
            {
                var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                var rgt = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                var mv = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) mv += fwd;
                if (Input.GetKey(KeyCode.S)) mv -= fwd;
                if (Input.GetKey(KeyCode.D)) mv += rgt;
                if (Input.GetKey(KeyCode.A)) mv -= rgt;
                if (Input.GetKey(KeyCode.PageUp)) mv += Vector3.up;
                if (Input.GetKey(KeyCode.PageDown)) mv -= Vector3.up;
                _target += mv * moveSpeed * (shift ? 2.5f : 1f) * Time.unscaledDeltaTime;
            }

            UpdateTransform();
        }
    }
}
