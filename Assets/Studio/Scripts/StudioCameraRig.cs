using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Ansichtskamera mit zwei Modi:
    ///  • Übersicht — Orbit um einen Zielpunkt (LMB/RMB drehen, Shift+LMB/MMB verschieben, Rad zoomen, WASD gehen).
    ///    Die Kamera bleibt immer innerhalb des Studios.
    ///  • Sucher — Blick durch eine Studiokamera (StudioCam): Ziehen schwenkt/neigt, Rad zoomt (Brennweite).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StudioCameraRig : MonoBehaviour
    {
        public struct Preset
        {
            public string name; public Vector3 eye, target; public float fov;
            public Preset(string n, Vector3 e, Vector3 t, float f = 60) { name = n; eye = e; target = t; fov = f; }
        }

        // Blickwinkel der 4 Fotos (Handy ~ 24–26 mm, Querformat)
        public static readonly Preset[] Presets =
        {
            new Preset("Foto 1 – Nordwand", new Vector3(5.0f, 1.75f, 4.6f),  new Vector3(-1.5f, 2.6f, -6.5f), 62),
            new Preset("Foto 2 – weiße Wand, Vorhang", new Vector3(-2.0f, 1.8f, -4.2f), new Vector3(5.0f, 2.2f, 6.5f), 62),
            new Preset("Foto 3 – Nordostecke", new Vector3(-5.2f, 1.75f, 4.8f), new Vector3(5.5f, 2.8f, -5.5f), 62),
            new Preset("Foto 4 – Nordostecke, tief", new Vector3(-4.6f, 1.55f, 5.2f), new Vector3(5.5f, 3.1f, -5.0f), 64),
            new Preset("Draufsicht", new Vector3(0.01f, 6.9f, 0.3f), new Vector3(0, 0, 0), 95),
            new Preset("Totale", new Vector3(-3.6f, 1.45f, 0.9f), new Vector3(1.0f, 1.0f, -0.9f), 45),
            new Preset("Nahe", new Vector3(-1.4f, 1.25f, 0.2f), new Vector3(1.4f, 1.0f, -1.3f), 30),
        };

        // Innenraum, in dem sich die Ansichtskamera bewegen darf (vor Vorhängen/Wänden, unter dem Rig)
        public static readonly Vector3 BoundsMin = new Vector3(-StudioBuilder.HalfW + 0.8f, 0.25f, -StudioBuilder.HalfD + 0.45f);
        public static readonly Vector3 BoundsMax = new Vector3(StudioBuilder.HalfW - 0.4f, 7.0f, StudioBuilder.HalfD - 0.8f);

        public float orbitSpeed = 0.25f, panSpeed = 0.01f, zoomSpeed = 1.2f, moveSpeed = 4f;
        /// <summary>Vom Editor gesetzt: Maus über UI oder Objekt wird gezogen.</summary>
        public System.Func<bool> inputBlocked;

        Camera _cam;
        Vector3 _target;
        float _yaw, _pitch, _dist, _overviewFov = 62f;
        Vector3 _lastMouse;
        bool _orbiting, _panning, _dragPov;

        // weicher Flug zum Preset
        Vector3 _flyEye, _flyTarget; float _flyFov; float _flyT = 1f;
        Vector3 _fromEye, _fromTarget; float _fromFov;

        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

        /// <summary>Aktive Studiokamera im Sucher-Modus (null = Übersicht).</summary>
        public StudioCam Pov { get; private set; }
        public bool InPov => Pov != null;

        // ---- Laufmodus (Ich-Perspektive) ----
        public bool InWalk { get; private set; }
        public const float EyeHeight = 1.65f, WalkSpeed = 1.6f, RunSpeed = 3.6f, BodyRadius = 0.28f;
        Vector3 _feet;
        float _walkYaw, _walkPitch;
        /// <summary>Collider, die beim Laufen ignoriert werden (z. B. das getragene Objekt).</summary>
        public Collider ignoreCollider;

        public void EnterWalk()
        {
            ExitPov();
            InWalk = true;
            _flyT = 1f;
            var p = transform.position;
            _feet = Clamp(new Vector3(p.x, 0, p.z));
            _feet.y = 0;
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            _walkYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            _walkPitch = 0f;
            _overviewFov = Cam.fieldOfView;
            Cam.fieldOfView = 70f;
        }

        public void ExitWalk()
        {
            if (!InWalk) return;
            InWalk = false;
            // Übersicht hinter der aktuellen Position fortsetzen
            var fwd = Quaternion.Euler(0, _walkYaw, 0) * Vector3.forward;
            SetLook(_feet + Vector3.up * EyeHeight - fwd * 2.5f + Vector3.up * 0.8f, _feet + fwd * 1.5f + Vector3.up * 1f);
            Cam.fieldOfView = _overviewFov;
        }

        public Vector3 WalkForward => Quaternion.Euler(0, _walkYaw, 0) * Vector3.forward;
        public Vector3 WalkFeet => _feet;

        void Awake()
        {
            Cam.renderingPath = RenderingPath.DeferredShading; // viele Scheinwerfer
            Cam.allowHDR = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 200f;
            Cam.cullingMask &= ~(1 << IconRenderer.Layer); // Vorschau-Bühne nie zeigen
            Apply(Presets[0], instant: true);
        }

        public void Apply(Preset p, bool instant = false)
        {
            _povFromWalk = false;
            ExitPov();
            InWalk = false;
            if (instant)
            {
                SetLook(p.eye, p.target);
                Cam.fieldOfView = _overviewFov = p.fov;
                _flyT = 1f;
                return;
            }
            _fromEye = transform.position; _fromTarget = _target; _fromFov = Cam.fieldOfView;
            _flyEye = p.eye; _flyTarget = p.target; _flyFov = p.fov; _flyT = 0f;
        }

        bool _povFromWalk;

        public void EnterPov(StudioCam cam)
        {
            if (cam == null) return;
            if (InWalk) { _povFromWalk = true; InWalk = false; }
            else if (!InPov) _povFromWalk = false;
            if (!InPov) _overviewFov = Cam.fieldOfView;
            Pov = cam;
            _flyT = 1f;
        }

        public void ExitPov()
        {
            if (!InPov) return;
            var cam = Pov;
            Pov = null;
            if (_povFromWalk && cam != null)
            {
                // zurück in den Laufmodus: hinter der Kamera stehen, wo der Kameramann steht
                _povFromWalk = false;
                var fwd = Vector3.ProjectOnPlane(cam.eye.forward, Vector3.up).normalized;
                _feet = Clamp(cam.transform.position - fwd * 0.9f);
                _feet.y = 0;
                _walkYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                _walkPitch = -5f;
                InWalk = true;
                Cam.fieldOfView = 70f;
                return;
            }
            Cam.fieldOfView = _overviewFov;
            UpdateTransform();
        }

        /// <summary>16:9-Bildausschnitt in Bildschirmpixeln (GUI-Koordinaten, y nach unten).</summary>
        public static Rect Frame169()
        {
            float w = Screen.width, h = Screen.height;
            if (w / h > 16f / 9f) { float fw = h * 16f / 9f; return new Rect((w - fw) / 2, 0, fw, h); }
            float fh = w * 9f / 16f;
            return new Rect(0, (h - fh) / 2, w, fh);
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
            _dist = Mathf.Clamp(_dist, 0.3f, 25f);
            _target = Clamp(_target);
            var dir = Quaternion.Euler(-_pitch, _yaw, 0) * Vector3.forward;
            // nicht durch Wände/Decke: Abstand bis zum Rand des Innenraums begrenzen
            float maxDist = DistanceToBounds(_target, dir);
            if (_dist > maxDist) _dist = Mathf.Max(0.3f, maxDist);
            transform.position = Clamp(_target + dir * _dist);
            transform.rotation = Quaternion.LookRotation(_target - transform.position, Vector3.up);
        }

        static Vector3 Clamp(Vector3 p) => Vector3.Max(BoundsMin, Vector3.Min(BoundsMax, p));

        static float DistanceToBounds(Vector3 from, Vector3 dir)
        {
            float t = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(dir[i]) < 1e-5f) continue;
                float bound = dir[i] > 0 ? BoundsMax[i] : BoundsMin[i];
                t = Mathf.Min(t, (bound - from[i]) / dir[i]);
            }
            return Mathf.Max(0f, t);
        }

        void LateUpdate()
        {
            var mouse = Input.mousePosition;
            var delta = mouse - _lastMouse;
            _lastMouse = mouse;
            bool blocked = inputBlocked != null && inputBlocked();

            if (InPov) { UpdatePov(delta, blocked); return; }
            if (InWalk) { UpdateWalk(delta, blocked); return; }

            if (_flyT < 1f)
            {
                _flyT = Mathf.Min(1f, _flyT + Time.unscaledDeltaTime * 1.4f);
                float k = Mathf.SmoothStep(0, 1, _flyT);
                SetLook(Vector3.Lerp(_fromEye, _flyEye, k), Vector3.Lerp(_fromTarget, _flyTarget, k));
                Cam.fieldOfView = _overviewFov = Mathf.Lerp(_fromFov, _flyFov, k);
                return;
            }

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
                _yaw += delta.x * orbitSpeed;
                _pitch -= delta.y * orbitSpeed;
            }
            if (_panning)
                _target -= (transform.right * delta.x + transform.up * delta.y) * panSpeed * Mathf.Max(1f, _dist * 0.3f);
            if (!blocked)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _dist *= Mathf.Pow(0.88f, wheel * zoomSpeed);
            }

            // WASD — gehen (nicht während Texteingabe)
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

            Cam.fieldOfView = _overviewFov;
            UpdateTransform();
        }

        bool _looking;

        void UpdateWalk(Vector3 delta, bool blocked)
        {
            // Umsehen: rechte Maustaste halten (oder Pfeiltasten)
            if (!blocked && Input.GetMouseButtonDown(1)) _looking = true;
            if (!Input.GetMouseButton(1)) _looking = false;
            if (_looking)
            {
                _walkYaw += delta.x * 0.2f;
                _walkPitch = Mathf.Clamp(_walkPitch + delta.y * 0.2f, -70f, 70f);
            }
            float dt = Time.unscaledDeltaTime;
            bool typing = GUIUtility.keyboardControl != 0;
            if (!typing)
            {
                if (Input.GetKey(KeyCode.LeftArrow)) _walkYaw -= 90f * dt;
                if (Input.GetKey(KeyCode.RightArrow)) _walkYaw += 90f * dt;
                if (Input.GetKey(KeyCode.UpArrow)) _walkPitch = Mathf.Min(70f, _walkPitch + 60f * dt);
                if (Input.GetKey(KeyCode.DownArrow)) _walkPitch = Mathf.Max(-70f, _walkPitch - 60f * dt);

                var fwd = Quaternion.Euler(0, _walkYaw, 0) * Vector3.forward;
                var rgt = Quaternion.Euler(0, _walkYaw, 0) * Vector3.right;
                var mv = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) mv += fwd;
                if (Input.GetKey(KeyCode.S)) mv -= fwd;
                if (Input.GetKey(KeyCode.D)) mv += rgt;
                if (Input.GetKey(KeyCode.A)) mv -= rgt;
                if (mv.sqrMagnitude > 0)
                {
                    bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    var step = mv.normalized * (run ? RunSpeed : WalkSpeed) * dt;
                    // achsenweise bewegen → an Möbeln entlanggleiten
                    TryMove(new Vector3(step.x, 0, 0));
                    TryMove(new Vector3(0, 0, step.z));
                }
            }
            transform.position = _feet + Vector3.up * EyeHeight;
            transform.rotation = Quaternion.Euler(-_walkPitch, _walkYaw, 0);
        }

        void TryMove(Vector3 step)
        {
            var next = _feet + step;
            next.x = Mathf.Clamp(next.x, BoundsMin.x + 0.1f, BoundsMax.x - 0.1f);
            next.z = Mathf.Clamp(next.z, BoundsMin.z + 0.1f, BoundsMax.z - 0.1f);
            if (!Blocked(next) || Blocked(_feet)) _feet = next; // falls man in einem Objekt steht: rauslaufen erlauben
        }

        bool Blocked(Vector3 feet)
        {
            // Körper als Kapsel ab Kniehöhe — Teppiche und flache Objekte werden überschritten
            var hits = Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.6f, BodyRadius);
            foreach (var h in hits)
            {
                if (h == ignoreCollider || h.isTrigger) continue;
                if (h.GetComponentInParent<StudioProp>() == null) continue; // Boden etc.
                return true;
            }
            return false;
        }

        void UpdatePov(Vector3 delta, bool blocked)
        {
            var c = Pov;
            if (c == null || !c.gameObject.activeInHierarchy) { ExitPov(); return; }

            if (!blocked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) _dragPov = true;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) _dragPov = false;
            if (_dragPov)
            {
                // wie am Schwenkarm: Empfindlichkeit proportional zum Bildwinkel
                float k = c.VerticalFov / 60f * 0.2f;
                c.pan += delta.x * k;
                c.tilt += delta.y * k;
            }
            if (!blocked)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) c.focal *= Mathf.Pow(1.12f, wheel);
            }
            if (GUIUtility.keyboardControl == 0)
            {
                float dt = Time.unscaledDeltaTime;
                // Pfeiltasten: Schwenk/Neigung · WASD: Kamera fahren (Dolly/Truck) · R/F: Höhe · +/-: Zoom
                if (Input.GetKey(KeyCode.UpArrow)) c.tilt += 20f * dt;
                if (Input.GetKey(KeyCode.DownArrow)) c.tilt -= 20f * dt;
                if (Input.GetKey(KeyCode.RightArrow)) c.pan += 25f * dt;
                if (Input.GetKey(KeyCode.LeftArrow)) c.pan -= 25f * dt;
                if (Input.GetKey(KeyCode.R)) c.height += 0.3f * dt;
                if (Input.GetKey(KeyCode.F)) c.height -= 0.3f * dt;
                if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals)) c.focal *= 1f + dt;
                if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus)) c.focal /= 1f + dt;

                var fwd = Vector3.ProjectOnPlane(c.eye.forward, Vector3.up).normalized;
                var rgt = Vector3.Cross(Vector3.up, fwd);
                var mv = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) mv += fwd;
                if (Input.GetKey(KeyCode.S)) mv -= fwd;
                if (Input.GetKey(KeyCode.D)) mv += rgt;
                if (Input.GetKey(KeyCode.A)) mv -= rgt;
                if (mv.sqrMagnitude > 0)
                {
                    bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    var p = c.transform.position + mv.normalized * (fast ? 1.2f : 0.5f) * dt; // Dolly-Tempo
                    p.x = Mathf.Clamp(p.x, BoundsMin.x, BoundsMax.x);
                    p.z = Mathf.Clamp(p.z, BoundsMin.z, BoundsMax.z);
                    c.transform.position = p;
                }
            }
            c.Apply();

            transform.SetPositionAndRotation(c.eye.position, c.eye.rotation);
            // der 16:9-Ausschnitt entspricht dem Bild des 2/3"-Sensors
            var f = Frame169();
            float half = Mathf.Tan(c.VerticalFov * 0.5f * Mathf.Deg2Rad) * Screen.height / Mathf.Max(1f, f.height);
            Cam.fieldOfView = 2f * Mathf.Atan(half) * Mathf.Rad2Deg;
        }
    }
}
