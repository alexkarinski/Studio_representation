using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Laufzeit-Editor für die Kundenpräsentation: Möbel/Deko/Personen platzieren, Farben,
    /// Bild auf weiße Wand und Bildschirme, Licht, Ansichten, Studiokameras mit Sucheransicht,
    /// Live-Bild auf dem Monitor, Screenshots, Varianten speichern. Oberfläche: IMGUI.
    /// </summary>
    public class StudioEditor : MonoBehaviour
    {
        public StudioBuilder builder;
        public StudioCameraRig rig;

        StudioProp _selected;
        bool _dragging, _pendingDeselect, _hideUI, _showHelp = true, _clickConsumed;
        Vector3 _dragOffset, _downMouse;
        GameObject _selRing;
        Material _selRingMat;

        enum Tab { View, Objects, Light, Project }
        Tab _tab = Tab.View;
        Vector2 _scroll;
        Rect _panelRect, _povBarRect;
        string _status = "";
        float _statusTime;

        ProjectData _state = new ProjectData();
        string _wallImagePath = "", _propImagePath = "", _projectName = "Variante 1";
        bool[] _fixtureOn;

        // Live-Bild (Programm) für den Monitor auf dem Rollständer
        StudioCam _liveCam;
        Camera _programCam;
        RenderTexture _programRT;
        int _programFrame;

        static readonly (string name, Color c)[] Palette =
        {
            ("Weiß", new Color(0.95f, 0.95f, 0.94f)), ("Hellgrau", new Color(0.75f, 0.75f, 0.74f)),
            ("Grau", new Color(0.5f, 0.5f, 0.5f)), ("Graphit", new Color(0.2f, 0.21f, 0.22f)),
            ("Schwarz", new Color(0.04f, 0.04f, 0.045f)), ("Beige", new Color(0.86f, 0.80f, 0.70f)),
            ("Sand", new Color(0.80f, 0.68f, 0.50f)), ("Eiche", new Color(0.72f, 0.52f, 0.30f)),
            ("Nussbaum", new Color(0.42f, 0.28f, 0.17f)), ("Terrakotta", new Color(0.72f, 0.36f, 0.25f)),
            ("Orange", new Color(0.95f, 0.50f, 0.12f)), ("Senf", new Color(0.85f, 0.67f, 0.18f)),
            ("Oliv", new Color(0.45f, 0.48f, 0.25f)), ("Grün", new Color(0.20f, 0.50f, 0.30f)),
            ("Petrol", new Color(0.12f, 0.60f, 0.62f)), ("Blau", new Color(0.15f, 0.35f, 0.75f)),
            ("Dunkelblau", new Color(0.08f, 0.12f, 0.28f)), ("Violett", new Color(0.42f, 0.25f, 0.62f)),
            ("Rosa", new Color(0.95f, 0.55f, 0.65f)), ("Rot", new Color(0.78f, 0.10f, 0.12f)),
            ("Greenscreen", new Color(0.0f, 0.69f, 0.25f)), ("Bluescreen", new Color(0.0f, 0.28f, 0.73f)),
        };

        static readonly HashSet<string> EquipmentTypes = new HashSet<string> { "studio_cam", "pedestal_cam", "tv_stand", "spot_stand" };

        string ProjectsDir => Path.Combine(Application.persistentDataPath, "Projects");
        string ShotsDir => Path.Combine(Application.persistentDataPath, "Screenshots");

        void Start()
        {
            rig.inputBlocked = () => PointerOverUI() || _dragging || _clickConsumed;
            _fixtureOn = Enumerable.Repeat(true, builder.fixtureLights.Count).ToArray();
            _state.fixtures = _fixtureOn;
            ApplyLight();

            _selRingMat = StudioMaterials.Transparent("SelRing", new Color(0.2f, 0.85f, 1f, 0.35f));
            _selRing = Geo.Cyl("SelectionRing", transform, Vector3.zero, 0.5f, 0.01f, _selRingMat);
            _selRing.SetActive(false);

            SetupProgramFeed();
            SetLive(Cameras().FirstOrDefault(c => c.number == 2) ?? Cameras().FirstOrDefault());
        }

        // ================= Eingabe in 3D =================

        bool PointerOverUI()
        {
            if (_hideUI) return false;
            var m = Input.mousePosition;
            var gui = new Vector2(m.x, Screen.height - m.y) / UiScale;
            if (rig.InPov) return _povBarRect.Contains(gui) || GUIUtility.hotControl != 0;
            return _panelRect.Contains(gui) || HelpRect.Contains(gui) || GUIUtility.hotControl != 0;
        }

        float _lastMouseMove = -10f;
        Vector3 _prevMouse;

        void Update()
        {
            _clickConsumed = false;
            if ((Input.mousePosition - _prevMouse).sqrMagnitude > 4f || Input.anyKeyDown) _lastMouseMove = Time.unscaledTime;
            _prevMouse = Input.mousePosition;
            bool typing = GUIUtility.keyboardControl != 0;

            if (rig.InPov)
            {
                if (!typing)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) rig.ExitPov();
                    if (Input.GetKeyDown(KeyCode.C)) NextCamera(1);
                    if (Input.GetKeyDown(KeyCode.L)) SetLive(rig.Pov);
                    if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                    if (Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
                    for (int i = 0; i < StudioCameraRig.Presets.Length && i < 9; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i)) rig.Apply(StudioCameraRig.Presets[i]);
                }
                _selRing.SetActive(false);
                return;
            }

            if (rig.InWalk) { UpdateWalk(); return; }
            if (_carried != null) Drop();

            var cam = rig.Cam;
            bool overUI = PointerOverUI();

            if (Input.GetMouseButtonDown(0) && !overUI && !IsShift())
            {
                _downMouse = Input.mousePosition;
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                StudioProp hitProp = null;
                if (Physics.Raycast(ray, out var hit, 100f))
                    hitProp = hit.collider.GetComponentInParent<StudioProp>();
                if (hitProp != null)
                {
                    Select(hitProp);
                    if (FloorPoint(ray, out var fp)) _dragOffset = hitProp.transform.position - fp;
                    _dragging = true;
                    _clickConsumed = true;
                }
                else _pendingDeselect = true;
            }

            if (_dragging)
            {
                if (Input.GetMouseButton(0) && _selected != null)
                {
                    if (FloorPoint(cam.ScreenPointToRay(Input.mousePosition), out var fp))
                    {
                        var p = fp + _dragOffset;
                        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                        {
                            p.x = Mathf.Round(p.x * 4) / 4;
                            p.z = Mathf.Round(p.z * 4) / 4;
                        }
                        // begrenzte Geschwindigkeit: Objekt folgt der Maus, springt aber nicht
                        _selected.transform.position = Vector3.MoveTowards(_selected.transform.position, ClampToRoom(p), DragSpeed * Time.unscaledDeltaTime);
                    }
                }
                else _dragging = false;
            }

            if (Input.GetMouseButtonUp(0) && _pendingDeselect)
            {
                _pendingDeselect = false;
                if ((Input.mousePosition - _downMouse).sqrMagnitude < 16f) Select(null);
            }

            if (_selected != null && !typing)
            {
                RotateSelectedByKeys();
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) DeleteSelected();
                if (Input.GetKeyDown(KeyCode.Escape)) Select(null);
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand)) && Input.GetKeyDown(KeyCode.D)) Duplicate();
                if (Input.GetKeyDown(KeyCode.V) && _selected.GetComponent<StudioCam>() != null) rig.EnterPov(_selected.GetComponent<StudioCam>());
            }
            if (!typing)
            {
                for (int i = 0; i < StudioCameraRig.Presets.Length && i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) rig.Apply(StudioCameraRig.Presets[i]);
                if (Input.GetKeyDown(KeyCode.C)) NextCamera(1);
                if (Input.GetKeyDown(KeyCode.F)) rig.EnterWalk();
                if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                if (Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
            }

            UpdateSelectionRing();
        }

        public const float DragSpeed = 1.5f;   // m/s beim Verschieben
        public const float RotateSpeed = 75f;  // °/s beim Drehen (Shift: langsamer)

        void RotateSelectedByKeys()
        {
            if (_selected == null) return;
            float speed = (IsShift() ? RotateSpeed / 3f : RotateSpeed) * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.Q)) _selected.transform.Rotate(0, -speed, 0, Space.World);
            if (Input.GetKey(KeyCode.E)) _selected.transform.Rotate(0, speed, 0, Space.World);
        }

        // ---------- Laufmodus: Objekte tragen ----------

        StudioProp _carried;
        float _carryDist;

        void UpdateWalk()
        {
            bool typing = GUIUtility.keyboardControl != 0;
            if (!typing)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F)) { if (_carried != null) Drop(); else rig.ExitWalk(); }
                if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                if (Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
                if (Input.GetKeyDown(KeyCode.C)) NextCamera(1);
                if (_selected != null && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))) { if (_carried == _selected) Drop(); DeleteSelected(); }
            }

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
            {
                if (_carried != null) Drop();
                else
                {
                    var ray = rig.Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                    if (Physics.Raycast(ray, out var hit, 4f))
                    {
                        var prop = hit.collider.GetComponentInParent<StudioProp>();
                        if (prop != null) PickUp(prop);
                    }
                }
            }

            if (_carried != null)
            {
                if (!typing) RotateSelectedByKeys();
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _carryDist = Mathf.Clamp(_carryDist + wheel * 0.25f, 1.0f, 3.5f);
                var target = ClampToRoom(rig.WalkFeet + rig.WalkForward * _carryDist);
                _carried.transform.position = Vector3.MoveTowards(_carried.transform.position, target, DragSpeed * Time.unscaledDeltaTime);
            }
            UpdateSelectionRing();
        }

        void PickUp(StudioProp p)
        {
            Select(p);
            _carried = p;
            var flat = Vector3.ProjectOnPlane(p.transform.position - rig.WalkFeet, Vector3.up);
            _carryDist = Mathf.Clamp(flat.magnitude, 1.2f, 3f);
            rig.ignoreCollider = p.GetComponent<Collider>();
            Status("Getragen: " + p.displayName + " — Q/E drehen, Mausrad näher/weiter, Klick abstellen");
        }

        void Drop()
        {
            if (_carried != null) Status("Abgestellt: " + _carried.displayName);
            _carried = null;
            rig.ignoreCollider = null;
        }

        static Vector3 ClampToRoom(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -StudioBuilder.HalfW + 0.3f, StudioBuilder.HalfW - 0.3f);
            p.z = Mathf.Clamp(p.z, -StudioBuilder.HalfD + 0.3f, StudioBuilder.HalfD - 0.3f);
            p.y = 0;
            return p;
        }

        static bool IsShift() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        static bool FloorPoint(Ray ray, out Vector3 p)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { p = ray.GetPoint(d); return true; }
            p = Vector3.zero;
            return false;
        }

        void UpdateSelectionRing()
        {
            if (_hideUI || _selected == null || !_selected.gameObject.activeInHierarchy) { _selRing.SetActive(false); return; }
            var col = _selected.GetComponent<BoxCollider>();
            float r = col != null ? Mathf.Max(col.size.x, col.size.z) * 0.6f + 0.1f : 0.6f;
            _selRing.SetActive(true);
            var c = col != null ? _selected.transform.TransformPoint(new Vector3(col.center.x, 0, col.center.z)) : _selected.transform.position;
            _selRing.transform.position = new Vector3(c.x, 0.012f, c.z);
            _selRing.transform.localScale = new Vector3(r * 2, 0.005f, r * 2);
        }

        void Select(StudioProp p)
        {
            _selected = p;
            _propImagePath = p != null ? p.imagePath : "";
            if (p != null) _tab = Tab.Objects;
        }

        void DeleteSelected()
        {
            if (_selected == null) return;
            if (_selected.GetComponent<StudioCam>() == _liveCam) _liveCam = null;
            Destroy(_selected.gameObject);
            Select(null);
        }

        void Duplicate()
        {
            if (_selected == null) return;
            var s = _selected;
            var np = PropFactory.Create(s.typeId, builder.set, ClampToRoom(s.transform.position + new Vector3(0.6f, 0, 0.6f)), s.transform.localEulerAngles.y, s.color);
            if (np != null && !string.IsNullOrEmpty(s.imagePath)) np.SetImage(s.imagePath);
            var src = s.GetComponent<StudioCam>();
            var dst = np != null ? np.GetComponent<StudioCam>() : null;
            if (src != null && dst != null) { dst.pan = src.pan; dst.tilt = src.tilt; dst.focal = src.focal; dst.height = src.height; dst.Apply(); }
            Select(np);
        }

        void Spawn(string id)
        {
            // auf den Boden vor die Ansichtskamera stellen
            var cam = rig.Cam.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var pos = cam.position + fwd * (rig.InWalk ? 2f : 3f);
            pos.x = Mathf.Clamp(pos.x, -StudioBuilder.HalfW + 1, StudioBuilder.HalfW - 1);
            pos.z = Mathf.Clamp(pos.z, -StudioBuilder.HalfD + 1, StudioBuilder.HalfD - 1);
            pos.y = 0;
            float rotY = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg; // zur Kamera gedreht
            var p = PropFactory.Create(id, builder.set, pos, rotY);
            if (p == null) return;
            var sc = p.GetComponent<StudioCam>();
            if (sc != null) sc.AimAt(StudioBuilder.SetCenter);
            if (EquipmentTypes.Contains(id)) p.gameObject.SetActive(_state.showEquipment);
            Select(p);
            Status("Hinzugefügt: " + p.displayName);
        }

        IEnumerable<StudioProp> AllProps() => builder.set.GetComponentsInChildren<StudioProp>(true);
        List<StudioCam> Cameras() => builder.set.GetComponentsInChildren<StudioCam>(false).OrderBy(c => c.number).ToList();

        // ================= Studiokameras =================

        void NextCamera(int dir)
        {
            var cams = Cameras();
            if (cams.Count == 0) { Status("Keine Studiokamera im Studio"); return; }
            int i = rig.InPov ? cams.IndexOf(rig.Pov) : -1;
            i = (i + dir + cams.Count) % cams.Count;
            rig.EnterPov(cams[i]);
        }

        void SetLive(StudioCam c)
        {
            foreach (var cam in Cameras()) cam.SetLive(cam == c);
            _liveCam = c;
            _state.liveCamera = c != null ? c.number : 0;
        }

        void SetupProgramFeed()
        {
            _programRT = new RenderTexture(768, 432, 24) { name = "Programm" };
            var go = new GameObject("ProgramCamera");
            go.transform.SetParent(transform, false);
            _programCam = go.AddComponent<Camera>();
            _programCam.enabled = false;
            _programCam.targetTexture = _programRT;
            _programCam.renderingPath = RenderingPath.DeferredShading;
            _programCam.allowHDR = false;
            _programCam.nearClipPlane = 0.05f;
            _programCam.aspect = 16f / 9f;
            _programCam.clearFlags = CameraClearFlags.SolidColor;
            _programCam.backgroundColor = Color.black;
        }

        void LateUpdate()
        {
            if (_programCam == null) return;
            bool feed = _state.programOnMonitor && _liveCam != null && _liveCam.gameObject.activeInHierarchy;
            foreach (var p in AllProps())
            {
                if (p.typeId != "tv_stand" || p.imageMaterial == null || !string.IsNullOrEmpty(p.imagePath)) continue;
                var m = p.imageMaterial;
                if (feed && m.mainTexture != _programRT)
                {
                    m.mainTexture = _programRT;
                    m.SetTexture("_EmissionMap", _programRT);
                    m.color = Color.white;
                    m.SetColor("_EmissionColor", Color.white * 0.9f);
                }
                else if (!feed && m.mainTexture == _programRT) p.SetImage("");
            }
            // jedes zweite Bild rendern — spart Leistung im Browser
            if (!feed || (_programFrame++ & 1) == 1) return;
            _liveCam.Apply();
            _programCam.transform.SetPositionAndRotation(_liveCam.eye.position, _liveCam.eye.rotation);
            _programCam.fieldOfView = _liveCam.VerticalFov;
            _programCam.Render();
        }

        // ================= Licht / Umgebung =================

        void ApplyLight()
        {
            builder.ApplyLighting(_state.lightMaster, _state.lightKelvin, _fixtureOn);
            RenderSettings.ambientLight = new Color(_state.ambient, _state.ambient, _state.ambient * 1.05f);
        }

        void SetFloorColor(Color c)
        {
            _state.floorColor = c;
            StudioMaterials.Floor.color = c;
        }

        void SetWallColor(Color c)
        {
            _state.wallColor = c;
            builder.SetWhiteWall(c, string.IsNullOrEmpty(_state.wallImage) ? null : builder.whiteWallMat.mainTexture as Texture2D);
        }

        void SetWallImage(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _state.wallImage = "";
                builder.SetWhiteWall(_state.wallColor, null);
                return;
            }
            var tex = StudioProp.LoadTexture(path);
            if (tex == null) { Status("Datei nicht gefunden oder kein Bild: " + path); return; }
            _state.wallImage = path;
            builder.SetWhiteWall(_state.wallColor, tex);
            Status("Bild auf der weißen Wand: " + Path.GetFileName(path));
        }

        void SetEquipmentVisible(bool v)
        {
            _state.showEquipment = v;
            foreach (var p in AllProps())
                if (EquipmentTypes.Contains(p.typeId)) p.gameObject.SetActive(v);
            if (!v && _selected != null && EquipmentTypes.Contains(_selected.typeId)) Select(null);
            if (!v) rig.ExitPov();
        }

        // ================= Varianten =================

        ProjectData Capture()
        {
            _state.name = _projectName;
            _state.fixtures = _fixtureOn;
            _state.props = AllProps().Select(p =>
            {
                var d = new PropData
                {
                    type = p.typeId,
                    pos = p.transform.localPosition,
                    rotY = p.transform.localEulerAngles.y,
                    color = p.color,
                    image = p.imagePath ?? ""
                };
                var c = p.GetComponent<StudioCam>();
                if (c != null) { d.camNumber = c.number; d.camPan = c.pan; d.camTilt = c.tilt; d.camFocal = c.focal; d.camHeight = c.height; }
                return d;
            }).ToList();
            return _state;
        }

        void Save()
        {
            Directory.CreateDirectory(ProjectsDir);
            var file = Path.Combine(ProjectsDir, SafeName(_projectName) + ".json");
            File.WriteAllText(file, JsonUtility.ToJson(Capture(), true));
            WebBridge.SyncFS();
            Status(WebBridge.IsWeb ? "Im Browser gespeichert: " + _projectName : "Gespeichert: " + file);
        }

        void Load(string file)
        {
            var data = JsonUtility.FromJson<ProjectData>(File.ReadAllText(file));
            if (data == null) { Status("Datei konnte nicht gelesen werden: " + file); return; }
            rig.ExitPov();
            foreach (var p in AllProps().ToList()) DestroyImmediate(p.gameObject);
            Select(null);
            foreach (var d in data.props)
            {
                var p = PropFactory.Create(d.type, builder.set, d.pos, d.rotY, d.color);
                if (p == null) continue;
                if (!string.IsNullOrEmpty(d.image)) p.SetImage(d.image);
                var c = p.GetComponent<StudioCam>();
                if (c != null && d.camNumber > 0)
                {
                    c.SetNumber(d.camNumber);
                    c.pan = d.camPan; c.tilt = d.camTilt; c.focal = d.camFocal; c.height = d.camHeight;
                    c.Apply();
                }
            }
            _state = data;
            _projectName = data.name;
            if (data.fixtures != null && data.fixtures.Length == builder.fixtureLights.Count) _fixtureOn = data.fixtures;
            else _fixtureOn = Enumerable.Repeat(true, builder.fixtureLights.Count).ToArray();
            SetFloorColor(data.floorColor);
            _wallImagePath = data.wallImage;
            SetWallImage(data.wallImage);
            SetWallColor(data.wallColor);
            SetEquipmentVisible(data.showEquipment);
            SetLive(Cameras().FirstOrDefault(c => c.number == data.liveCamera) ?? Cameras().FirstOrDefault());
            ApplyLight();
            Status("Geladen: " + data.name);
        }

        void ResetToPhotos()
        {
            rig.ExitPov();
            builder.Build();
            _state = new ProjectData { name = _projectName };
            _fixtureOn = Enumerable.Repeat(true, builder.fixtureLights.Count).ToArray();
            _wallImagePath = "";
            Select(null);
            ApplyLight();
            StartCoroutine(SetLiveNextFrame());
            Status("Studio wie auf den Fotos wiederhergestellt");
        }

        IEnumerator SetLiveNextFrame()
        {
            yield return null; // alte Objekte sind erst jetzt zerstört
            SetLive(Cameras().FirstOrDefault(c => c.number == 2) ?? Cameras().FirstOrDefault());
        }

        static string SafeName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "projekt" : s;
        }

        IEnumerator Screenshot()
        {
            bool prev = _hideUI;
            _hideUI = true;
            _selRing.SetActive(false);
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var shotName = SafeName(_projectName) + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            var png = tex.EncodeToPNG();
            Destroy(tex);
            string file;
            if (WebBridge.IsWeb)
            {
                WebBridge.Download(shotName, png, "image/png");
                file = shotName + " (vom Browser heruntergeladen)";
            }
            else
            {
                Directory.CreateDirectory(ShotsDir);
                file = Path.Combine(ShotsDir, shotName);
                File.WriteAllBytes(file, png);
            }
            _hideUI = prev;
            Status("Screenshot: " + file);
        }

        void Status(string s) { _status = s; _statusTime = Time.unscaledTime; Debug.Log(s); }

        // ================= Oberfläche =================

        GUIStyle _box, _h1, _h2, _small, _swatch, _hud, _hudBig;
        Texture2D _bgTex, _white;
        Font _font, _fontBold;

        /// <summary>UI-Skalierung: auf Retina/4K ist das Canvas im Browser 2–3× dichter.</summary>
        static float UiScale => Mathf.Clamp(Mathf.Min(Screen.dpi > 0 ? Screen.dpi / 96f : 1f, Screen.height / 620f), 1f, 3f);
        static float UiW => Screen.width / UiScale;
        static float UiH => Screen.height / UiScale;

        void InitStyles()
        {
            if (_box != null) return;
            // der eingebaute WebGL-Font hat keine Umlaute/Sonderzeichen in allen Größen — PT Sans (OFL) aus Resources
            _font = Resources.Load<Font>("Fonts/PTSans-Regular");
            _fontBold = Resources.Load<Font>("Fonts/PTSans-Bold");
            if (_font != null)
            {
                GUI.skin.font = _font;
                foreach (var st in new[] { GUI.skin.label, GUI.skin.button, GUI.skin.toggle, GUI.skin.textField, GUI.skin.box, GUI.skin.window })
                {
                    st.font = _font;
                    st.fontSize = 13;
                }
            }
            _bgTex = new Texture2D(1, 1); _bgTex.SetPixel(0, 0, new Color(0.08f, 0.085f, 0.095f, 0.92f)); _bgTex.Apply();
            _white = Texture2D.whiteTexture;
            _box = new GUIStyle(GUI.skin.box) { normal = { background = _bgTex }, padding = new RectOffset(10, 10, 10, 10) };
            _h1 = new GUIStyle(GUI.skin.label) { font = _fontBold ?? _font, fontSize = 17, normal = { textColor = new Color(0.95f, 0.85f, 0.6f) } };
            _h2 = new GUIStyle(GUI.skin.label) { font = _fontBold ?? _font, fontSize = 14, normal = { textColor = new Color(0.75f, 0.85f, 0.95f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.75f, 0.75f, 0.78f) } };
            _swatch = new GUIStyle(GUI.skin.button) { margin = new RectOffset(2, 2, 2, 2) };
            _hud = new GUIStyle(GUI.skin.label) { font = _fontBold ?? _font, fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.95f, 0.95f) } };
            _hudBig = new GUIStyle(_hud) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }

        Rect HelpRect => _showHelp ? new Rect(UiW - 340, UiH - 240, 330, 230) : new Rect(UiW - 130, UiH - 34, 120, 26);

        void OnGUI()
        {
            InitStyles();
            if (_hideUI)
            {
                // saubere Ansicht: nur ein kleiner Knopf, solange die Maus bewegt wird
                _panelRect = _povBarRect = Rect.zero;
                float idle = Time.unscaledTime - _lastMouseMove;
                if (idle < 2.5f)
                {
                    GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f));
                    var prev = GUI.color;
                    GUI.color = new Color(1, 1, 1, Mathf.Clamp01(2.5f - idle) * 0.8f);
                    var r = new Rect(8, 8, 190, 26);
                    _panelRect = r;
                    if (GUI.Button(r, "Oberfläche einblenden (H)")) _hideUI = false;
                    GUI.color = prev;
                }
                return;
            }

            if (rig.InPov)
            {
                if (_state.viewfinderOverlay) DrawViewfinderHud();
                GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f));
                DrawPovBar();
                return;
            }

            if (rig.InWalk) DrawWalkOverlay();
            GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f));
            float w = 360;
            _panelRect = new Rect(8, 8, w, UiH - 16);
            GUILayout.BeginArea(_panelRect, _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Studio – Gestaltung visualisieren", _h1);
            if (GUILayout.Button("Ausblenden (H)", GUILayout.Width(110))) _hideUI = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Ansicht", "Objekte", "Licht", "Projekt" });
            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.View: DrawViewTab(); break;
                case Tab.Objects: DrawObjectsTab(); break;
                case Tab.Light: DrawLightTab(); break;
                case Tab.Project: DrawProjectTab(); break;
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(_status) && Time.unscaledTime - _statusTime < 6f)
                GUILayout.Label(_status, _small);
            GUILayout.EndArea();

            DrawHelp();
        }

        // ---------- Sucheransicht ----------

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        /// <summary>Sucher-Overlay wie am Kameramonitor: 16:9-Ausschnitt, Drittellinien, Statuszeile, Tally.</summary>
        void DrawViewfinderHud()
        {
            var cam = rig.Pov;
            if (cam == null) return;
            GUI.matrix = Matrix4x4.identity;
            var f = StudioCameraRig.Frame169();
            float sw = Screen.width, sh = Screen.height, s = UiScale;

            // außerhalb des 16:9-Bildes abdunkeln
            var dim = new Color(0, 0, 0, 0.85f);
            if (f.x > 0) { Fill(new Rect(0, 0, f.x, sh), dim); Fill(new Rect(f.xMax, 0, sw - f.xMax, sh), dim); }
            if (f.y > 0) { Fill(new Rect(0, 0, sw, f.y), dim); Fill(new Rect(0, f.yMax, sw, sh - f.yMax), dim); }

            // Drittellinien
            var line = new Color(1, 1, 1, 0.35f);
            for (int i = 1; i <= 2; i++)
            {
                Fill(new Rect(f.x + f.width * i / 3f, f.y, 1, f.height), line);
                Fill(new Rect(f.x, f.y + f.height * i / 3f, f.width, 1), line);
            }
            // Mittelkreuz
            var cx = f.center;
            Fill(new Rect(cx.x - 10 * s, cx.y, 20 * s, 1), line);
            Fill(new Rect(cx.x, cx.y - 10 * s, 1, 20 * s), line);

            // Statuszeile oben
            float barH = 28 * s;
            Fill(new Rect(f.x, f.y, f.width, barH), new Color(0, 0, 0, 0.55f));
            var hud = new GUIStyle(_hud) { fontSize = Mathf.RoundToInt(12 * s) };
            var hudBig = new GUIStyle(_hudBig) { fontSize = Mathf.RoundToInt(15 * s) };
            GUI.Label(new Rect(f.x + 14 * s, f.y, 140 * s, barH), "Kamera " + cam.number, hudBig);
            string[] cols =
            {
                "RES  1080p", "FPS  50", "SHUTTER  1/50", "IRIS  f1.8", "GAIN  +0dB",
                "WB  " + Mathf.RoundToInt(_state.lightKelvin / 100f) * 100 + "K", "BRENNW.  " + cam.focal.ToString("0") + " mm"
            };
            float x0 = f.x + 150 * s, colW = (f.width - 160 * s) / cols.Length;
            for (int i = 0; i < cols.Length; i++)
                GUI.Label(new Rect(x0 + i * colW, f.y, colW, barH), cols[i], hud);

            // Tally: rote Umrandung, wenn diese Kamera auf Sendung ist
            if (cam == _liveCam)
            {
                var red = new Color(0.95f, 0.1f, 0.1f, 0.9f);
                float t = 4 * s;
                Fill(new Rect(f.x, f.y, f.width, t), red);
                Fill(new Rect(f.x, f.yMax - t, f.width, t), red);
                Fill(new Rect(f.x, f.y, t, f.height), red);
                Fill(new Rect(f.xMax - t, f.y, t, f.height), red);
                Fill(new Rect(f.x + f.width / 2 - 34 * s, f.y + barH + 8 * s, 68 * s, 22 * s), red);
                GUI.Label(new Rect(f.x + f.width / 2 - 34 * s, f.y + barH + 8 * s, 68 * s, 22 * s), "LIVE", hud);
            }
        }

        void DrawPovBar()
        {
            var cam = rig.Pov;
            if (cam == null) return;
            float h = 112;
            _povBarRect = new Rect(8, UiH - h - 8, Mathf.Min(UiW - 16, 980), h);
            GUILayout.BeginArea(_povBarRect, _box);
            GUILayout.BeginHorizontal();
            foreach (var c in Cameras())
            {
                var label = "Kamera " + c.number + (c == _liveCam ? " ●" : "");
                var prev = GUI.backgroundColor;
                if (c == cam) GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button(label, GUILayout.Height(26))) rig.EnterPov(c);
                GUI.backgroundColor = prev;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(cam == _liveCam ? "Auf Sendung" : "Live schalten (L)", GUILayout.Height(26), GUILayout.Width(150))) SetLive(cam);
            if (GUILayout.Button("Zur Übersicht (Esc)", GUILayout.Height(26), GUILayout.Width(160))) rig.ExitPov();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Brennweite " + cam.focal.ToString("0") + " mm", _small, GUILayout.Width(130));
            // logarithmischer Regler wie ein Zoomhebel
            float lf = GUILayout.HorizontalSlider(Mathf.Log(cam.focal), Mathf.Log(StudioCam.MinFocal), Mathf.Log(StudioCam.MaxFocal), GUILayout.Width(200));
            cam.focal = Mathf.Exp(lf);
            GUILayout.Space(16);
            GUILayout.Label("Höhe " + cam.height.ToString("0.00") + " m", _small, GUILayout.Width(90));
            cam.height = GUILayout.HorizontalSlider(cam.height, cam.minHeight, cam.maxHeight, GUILayout.Width(140));
            GUILayout.Space(16);
            if (GUILayout.Button("Screenshot (P)", GUILayout.Width(120))) StartCoroutine(Screenshot());
            _state.viewfinderOverlay = GUILayout.Toggle(_state.viewfinderOverlay, " Sucher-Overlay", GUILayout.Width(120));
            if (GUILayout.Button("Oberfläche aus (H)", GUILayout.Width(140))) _hideUI = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Ziehen: schwenken/neigen · Mausrad: zoomen · C: nächste Kamera · Kamera verschieben: in der Übersicht anklicken und ziehen", _small);
            GUILayout.EndArea();
            cam.Apply();
        }

        void DrawWalkOverlay()
        {
            GUI.matrix = Matrix4x4.identity;
            float s = UiScale;
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var col = _carried != null ? new Color(0.4f, 0.9f, 1f, 0.9f) : new Color(1, 1, 1, 0.8f);
            Fill(new Rect(c.x - 8 * s, c.y - 1, 16 * s, 2), col);
            Fill(new Rect(c.x - 1, c.y - 8 * s, 2, 16 * s), col);
            var st = new GUIStyle(_hud) { fontSize = Mathf.RoundToInt(13 * s) };
            string hint = _carried != null
                ? "Trägt: " + _carried.displayName + "  ·  Q/E drehen  ·  Mausrad näher/weiter  ·  Klick: abstellen"
                : "WASD gehen · Shift rennen · rechte Maustaste: umsehen · Klick auf Objekt: aufnehmen · F/Esc: Laufmodus beenden";
            var r = new Rect(0, Screen.height - 40 * s, Screen.width, 30 * s);
            Fill(new Rect(c.x - 380 * s, r.y, 760 * s, r.height), new Color(0, 0, 0, 0.5f));
            GUI.Label(r, hint, st);
        }

        // ---------- Tabs ----------

        void DrawViewTab()
        {
            if (GUILayout.Button(rig.InWalk ? "Laufmodus beenden (F)" : "Durchs Studio laufen (F)", GUILayout.Height(30)))
            {
                if (rig.InWalk) rig.ExitWalk(); else rig.EnterWalk();
            }
            GUILayout.Space(6);
            GUILayout.Label("Ansichten (Tasten 1–7)", _h2);
            for (int i = 0; i < StudioCameraRig.Presets.Length; i++)
                if (GUILayout.Button((i + 1) + ". " + StudioCameraRig.Presets[i].name)) rig.Apply(StudioCameraRig.Presets[i]);
            GUILayout.Label("Bildwinkel: " + rig.Cam.fieldOfView.ToString("0") + "°", _small);
            rig.Cam.fieldOfView = GUILayout.HorizontalSlider(rig.Cam.fieldOfView, 20f, 100f);

            GUILayout.Space(10);
            GUILayout.Label("Studiokameras (C)", _h2);
            var cams = Cameras();
            if (cams.Count == 0) GUILayout.Label("Keine Kamera im Studio. Unter „Objekte“ hinzufügen.", _small);
            foreach (var c in cams)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kamera " + c.number + (c == _liveCam ? "  ● LIVE" : "") + "  ·  " + c.focal.ToString("0") + " mm", GUILayout.Width(190));
                if (GUILayout.Button("Durchschauen")) rig.EnterPov(c);
                if (GUILayout.Button("Live", GUILayout.Width(44))) SetLive(c);
                GUILayout.EndHorizontal();
            }
            bool feed = GUILayout.Toggle(_state.programOnMonitor, " Monitor zeigt das Live-Bild");
            if (feed != _state.programOnMonitor) _state.programOnMonitor = feed;

            GUILayout.Space(8);
            if (GUILayout.Button("Screenshot für den Kunden (P)", GUILayout.Height(28))) StartCoroutine(Screenshot());
            GUILayout.Label(WebBridge.IsWeb ? "PNG wird vom Browser heruntergeladen" : "Gespeichert in " + ShotsDir, _small);

            GUILayout.Space(10);
            GUILayout.Label("Weiße Wand (Hintergrund)", _h2);
            DrawPalette(SetWallColor);
            GUILayout.Label("Bild/Branding auf der Wand:", _small);
            DrawImagePicker(ref _wallImagePath, SetWallImage);

            GUILayout.Space(10);
            GUILayout.Label("Boden", _h2);
            DrawPalette(SetFloorColor);

            GUILayout.Space(10);
            bool eq = GUILayout.Toggle(_state.showEquipment, " Technik zeigen (Kameras, Monitor, Stativleuchten)");
            if (eq != _state.showEquipment) SetEquipmentVisible(eq);
            _state.viewfinderOverlay = GUILayout.Toggle(_state.viewfinderOverlay, " Sucher-Overlay in der Kameraansicht (Raster, Statuszeile)");
            _showHelp = GUILayout.Toggle(_showHelp, " Steuerungshilfe zeigen");
            bool rigOn = builder.rig.gameObject.activeSelf;
            bool r2 = GUILayout.Toggle(rigOn, " Hängendes Lichtrig zeigen");
            if (r2 != rigOn) builder.rig.gameObject.SetActive(r2);
        }

        void DrawObjectsTab()
        {
            if (_selected != null) DrawSelected();
            else GUILayout.Label("Objekt in der Szene anklicken, um es auszuwählen. Mit der Maus ziehen, Q/E drehen, Entf löschen, Strg+D kopieren, Strg beim Ziehen = 25-cm-Raster.", _small);

            GUILayout.Space(10);
            GUILayout.Label("Hinzufügen", _h2);
            foreach (var group in PropFactory.Catalog.GroupBy(e => e.category))
            {
                GUILayout.Label(group.Key, _small);
                var items = group.ToList();
                for (int i = 0; i < items.Count; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, items.Count); j++)
                        if (GUILayout.Button(items[j].name, GUILayout.Width(160))) Spawn(items[j].id);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Im Studio", _h2);
            foreach (var p in AllProps().ToList())
            {
                var cam = p.GetComponent<StudioCam>();
                var label = (p == _selected ? "▶ " : "") + p.displayName + (cam != null ? " " + cam.number : "") + (p.gameObject.activeSelf ? "" : " (ausgeblendet)");
                if (GUILayout.Button(label, GUI.skin.label)) Select(p);
            }
        }

        void DrawSelected()
        {
            var t = _selected.transform;
            var cam = _selected.GetComponent<StudioCam>();
            GUILayout.Label("Ausgewählt: " + _selected.displayName + (cam != null ? " " + cam.number : ""), _h2);
            var pos = t.localPosition;
            GUILayout.Label($"X: {pos.x:0.00} m", _small);
            pos.x = GUILayout.HorizontalSlider(pos.x, -StudioBuilder.HalfW + 0.3f, StudioBuilder.HalfW - 0.3f);
            GUILayout.Label($"Z: {pos.z:0.00} m", _small);
            pos.z = GUILayout.HorizontalSlider(pos.z, -StudioBuilder.HalfD + 0.3f, StudioBuilder.HalfD - 0.3f);
            t.localPosition = pos;
            float rot = t.localEulerAngles.y;
            GUILayout.Label($"Drehung: {rot:0}°", _small);
            float nr = GUILayout.HorizontalSlider(rot, 0f, 360f);
            if (Mathf.Abs(nr - rot) > 0.01f) t.localRotation = Quaternion.Euler(0, nr, 0);

            if (cam != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Kamerakopf", _h2);
                GUILayout.Label($"Schwenk: {cam.pan:0}°", _small);
                cam.pan = GUILayout.HorizontalSlider(cam.pan, -180f, 180f);
                GUILayout.Label($"Neigung: {cam.tilt:0}°", _small);
                cam.tilt = GUILayout.HorizontalSlider(cam.tilt, -45f, 40f);
                GUILayout.Label($"Brennweite: {cam.focal:0} mm", _small);
                cam.focal = Mathf.Exp(GUILayout.HorizontalSlider(Mathf.Log(cam.focal), Mathf.Log(StudioCam.MinFocal), Mathf.Log(StudioCam.MaxFocal)));
                GUILayout.Label($"Höhe: {cam.height:0.00} m", _small);
                cam.height = GUILayout.HorizontalSlider(cam.height, cam.minHeight, cam.maxHeight);
                cam.Apply();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Durch die Kamera schauen (V)", GUILayout.Height(26))) rig.EnterPov(cam);
                if (GUILayout.Button(cam == _liveCam ? "● Live" : "Live schalten", GUILayout.Height(26))) SetLive(cam);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Auf die Sitzgruppe ausrichten")) cam.AimAt(StudioBuilder.SetCenter);
            }

            var spot = _selected.GetComponent<AimableLight>();
            if (spot != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Scheinwerfer", _h2);
                GUILayout.Label($"Schwenk: {spot.pan:0}°", _small);
                spot.pan = GUILayout.HorizontalSlider(spot.pan, -180f, 180f);
                GUILayout.Label($"Neigung: {spot.tilt:0}°", _small);
                spot.tilt = GUILayout.HorizontalSlider(spot.tilt, -80f, 30f);
                GUILayout.Label($"Helligkeit: {spot.intensity:0.0}", _small);
                spot.intensity = GUILayout.HorizontalSlider(spot.intensity, 0f, 4f);
                spot.Apply();
                if (GUILayout.Button("Auf die Sitzgruppe ausrichten")) spot.AimAt(StudioBuilder.SetCenter);
            }

            if (_selected.HasColor)
            {
                GUILayout.Label(_selected.tintIsEmission ? "Lichtfarbe" : "Farbe", _small);
                DrawPalette(c => _selected.SetColor(c));
                var c0 = _selected.color;
                GUILayout.BeginHorizontal();
                float r = GUILayout.HorizontalSlider(c0.r, 0, 1), g = GUILayout.HorizontalSlider(c0.g, 0, 1), b = GUILayout.HorizontalSlider(c0.b, 0, 1);
                GUILayout.EndHorizontal();
                if (r != c0.r || g != c0.g || b != c0.b) _selected.SetColor(new Color(r, g, b));
            }
            if (_selected.HasImage)
            {
                GUILayout.Label("Bild auf Bildschirm/Wand/Rahmen:", _small);
                var prop = _selected;
                DrawImagePicker(ref _propImagePath, path =>
                {
                    if (prop.SetImage(path)) Status(string.IsNullOrEmpty(path) ? "Bild entfernt" : "Bild: " + Path.GetFileName(path));
                    else Status("Datei nicht gefunden oder kein Bild: " + path);
                });
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kopieren")) Duplicate();
            if (GUILayout.Button("Löschen")) DeleteSelected();
            if (GUILayout.Button("Auswahl aufheben")) Select(null);
            GUILayout.EndHorizontal();
        }

        void DrawLightTab()
        {
            GUILayout.Label("Helligkeit aller Scheinwerfer: " + _state.lightMaster.ToString("0.00"), _small);
            float m = GUILayout.HorizontalSlider(_state.lightMaster, 0f, 2.5f);
            GUILayout.Label("Farbtemperatur: " + _state.lightKelvin.ToString("0") + " K", _small);
            float k = GUILayout.HorizontalSlider(_state.lightKelvin, 2700f, 6500f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3200 K (Halogen)")) k = 3200;
            if (GUILayout.Button("4300 K")) k = 4300;
            if (GUILayout.Button("5600 K (Tageslicht)")) k = 5600;
            GUILayout.EndHorizontal();
            GUILayout.Label("Grundlicht im Raum: " + _state.ambient.ToString("0.00"), _small);
            float a = GUILayout.HorizontalSlider(_state.ambient, 0f, 0.6f);
            if (m != _state.lightMaster || k != _state.lightKelvin || a != _state.ambient)
            {
                _state.lightMaster = m; _state.lightKelvin = k; _state.ambient = a;
                ApplyLight();
            }

            GUILayout.Space(8);
            GUILayout.Label("Scheinwerfer an Pantografen", _h2);
            bool changed = false;
            for (int i = 0; i < builder.fixtureLights.Count; i++)
            {
                var l = builder.fixtureLights[i];
                if (l == null) continue;
                string fixtureName = l.transform.parent.parent.name.Replace("Fixture_", "Scheinwerfer Nr. ") + (l.spotAngle > 60 ? " (Softlight)" : " (Stufenlinse)");
                bool on = GUILayout.Toggle(_fixtureOn[i], " " + fixtureName);
                if (on != _fixtureOn[i]) { _fixtureOn[i] = on; changed = true; }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Alle an")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = true; changed = true; }
            if (GUILayout.Button("Alle aus")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = false; changed = true; }
            GUILayout.EndHorizontal();
            if (changed) ApplyLight();
            GUILayout.Label("Stativscheinwerfer und LED-Röhren findest du unter „Objekte → Licht“.", _small);
        }

        void DrawProjectTab()
        {
            GUILayout.Label("Name der Variante", _h2);
            _projectName = GUILayout.TextField(_projectName);
            if (GUILayout.Button("Variante speichern", GUILayout.Height(26))) Save();
            if (WebBridge.IsWeb)
            {
                GUILayout.Label("Varianten werden in diesem Browser gespeichert. Die Datei kann heruntergeladen und weitergegeben werden.", _small);
                if (GUILayout.Button("Variante herunterladen (.json)"))
                    WebBridge.Download(SafeName(_projectName) + ".json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(Capture(), true)), "application/json");
            }
            else GUILayout.Label("Ordner: " + ProjectsDir, _small);

            GUILayout.Space(8);
            GUILayout.Label("Gespeicherte Varianten", _h2);
            if (Directory.Exists(ProjectsDir))
                foreach (var f in Directory.GetFiles(ProjectsDir, "*.json").OrderBy(f => f))
                    if (GUILayout.Button("Öffnen: " + Path.GetFileNameWithoutExtension(f))) Load(f);

            GUILayout.Space(12);
            if (GUILayout.Button("Zurücksetzen wie auf den Fotos")) ResetToPhotos();
            if (GUILayout.Button("Alle Möbel, Deko und Personen entfernen"))
            {
                foreach (var p in AllProps().ToList())
                    if (!EquipmentTypes.Contains(p.typeId)) Destroy(p.gameObject);
                Select(null);
            }
        }

        void DrawPalette(System.Action<Color> onPick)
        {
            const int perRow = 8;
            var prev = GUI.backgroundColor;
            for (int i = 0; i < Palette.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, Palette.Length); j++)
                {
                    GUI.backgroundColor = Palette[j].c;
                    var content = new GUIContent(_white, Palette[j].name);
                    if (GUILayout.Button(content, _swatch, GUILayout.Width(34), GUILayout.Height(22))) onPick(Palette[j].c);
                }
                GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = prev;
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label(GUI.tooltip, _small);
        }

        System.Action<string> _webImageTarget;

        void DrawImagePicker(ref string path, System.Action<string> apply)
        {
            if (WebBridge.IsWeb)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Bild hochladen…"))
                {
                    _webImageTarget = apply;
                    WebBridge.PickImage(gameObject.name, nameof(OnWebImage));
                }
                if (GUILayout.Button("Entfernen")) { path = ""; apply(""); }
                GUILayout.EndHorizontal();
                return;
            }
            path = GUILayout.TextField(path ?? "");
            GUILayout.BeginHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button("Auswählen…"))
            {
                var p = UnityEditor.EditorUtility.OpenFilePanel("Bild", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(p)) { path = p; apply(p); }
            }
#endif
            if (GUILayout.Button("Übernehmen")) apply(path.Trim().Trim('"'));
            if (GUILayout.Button("Entfernen")) { path = ""; apply(""); }
            GUILayout.EndHorizontal();
        }

        /// <summary>Rückruf aus dem Browser: "name|base64". Das Bild wird in persistentDataPath abgelegt, damit es in der Variante gespeichert werden kann.</summary>
        public void OnWebImage(string payload)
        {
            int bar = payload.IndexOf('|');
            if (bar < 0 || _webImageTarget == null) return;
            var name = SafeName(payload.Substring(0, bar));
            byte[] bytes;
            try { bytes = System.Convert.FromBase64String(payload.Substring(bar + 1)); }
            catch { Status("Bild konnte nicht gelesen werden"); return; }
            var dir = Path.Combine(Application.persistentDataPath, "Images");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, name);
            File.WriteAllBytes(file, bytes);
            WebBridge.SyncFS();
            _webImageTarget(file);
        }

        void DrawHelp()
        {
            var r = HelpRect;
            if (!_showHelp)
            {
                if (GUI.Button(r, "Steuerung")) _showHelp = true;
                return;
            }
            GUILayout.BeginArea(r, _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Steuerung", _h2);
            if (GUILayout.Button("×", GUILayout.Width(22))) _showHelp = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Linke/rechte Maustaste ins Leere – Ansicht drehen\n" +
                "Shift+Linksklick / Mitteltaste – verschieben, Rad – zoomen\n" +
                "WASD – gehen, Bild↑/Bild↓ – höher/tiefer\n" +
                "Objekt anklicken – auswählen und ziehen\n" +
                "Q/E halten – drehen, Entf löschen, Strg+D kopieren\n" +
                "F – durchs Studio laufen (Objekte aufnehmen/tragen)\n" +
                "C – durch Studiokameras schauen, Esc – zurück\n" +
                "1–7 Ansichten, P Screenshot, H Oberfläche aus", _small);
            GUILayout.EndArea();
        }
    }
}
