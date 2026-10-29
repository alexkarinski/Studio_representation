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

            _defaultFixtures = CaptureFixtures();
            _hands = FirstPersonHands.Create(rig.Cam);
            SetupProgramFeed();
            SetLive(Cameras().FirstOrDefault(c => c.number == 2) ?? Cameras().FirstOrDefault());
            StartCoroutine(RestoreLastSession());
        }

        // ================= Eingabe in 3D =================

        bool PointerOverUI()
        {
            if (_hideUI) return false;
            var m = Input.mousePosition;
            var gui = new Vector2(m.x, Screen.height - m.y) / UiScale;
            if (rig.InPov) return _povBarRect.Contains(gui) || GUIUtility.hotControl != 0;
            return _panelRect.Contains(gui) || HelpRect.Contains(gui) || _modeBarRect.Contains(gui) || _objBarRect.Contains(gui)
                || (_pipTarget != null && _pipRect.Contains(gui)) || GUIUtility.hotControl != 0;
        }

        float _lastMouseMove = -10f;
        Vector3 _prevMouse;

        void Update()
        {
            _clickConsumed = false;
            if ((Input.mousePosition - _prevMouse).sqrMagnitude > 4f || Input.anyKeyDown) _lastMouseMove = Time.unscaledTime;
            _prevMouse = Input.mousePosition;
            HandleUndoKeys();
            bool typing = GUIUtility.keyboardControl != 0;

            if (rig.InPov)
            {
                if (!typing)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) rig.ExitPov();
                    if (Input.GetKeyDown(KeyCode.C)) NextCamera(1);
                    if (Input.GetKeyDown(KeyCode.L)) SetLive(rig.Pov);
                    if (Input.GetKeyDown(KeyCode.Space)) ToggleRecording(rig.Pov);
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
                StudioFixture hitFixture = null;
                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    hitProp = hit.collider.GetComponentInParent<StudioProp>();
                    hitFixture = hit.collider.GetComponentInParent<StudioFixture>();
                }
                if (hitFixture != null)
                {
                    SelectFixture(hitFixture);
                    _clickConsumed = true;
                }
                else if (hitProp != null)
                {
                    // Doppelklick: Kamera → durchschauen, sonst Ansicht auf das Objekt
                    if (hitProp == _lastClickProp && Time.unscaledTime - _lastClickTime < 0.35f)
                    {
                        var sc = hitProp.GetComponent<StudioCam>();
                        Select(hitProp);
                        if (sc != null) rig.EnterPov(sc); else FocusSelected();
                        _lastClickProp = null;
                        _clickConsumed = true;
                        return;
                    }
                    _lastClickProp = hitProp; _lastClickTime = Time.unscaledTime;
                    PushUndo();
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
                if ((Input.mousePosition - _downMouse).sqrMagnitude < 16f) { Select(null); _fixture = null; }
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
                if (Input.GetKeyDown(KeyCode.F)) { rig.EnterWalk(); StudioCameraRig.LockMouse(true); }
                if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                if (Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
            }

            MoveFixtureByKeys();
            UpdateSelectionRing();
        }

        public const float DragSpeed = 1.5f;   // m/s beim Verschieben
        public const float RotateSpeed = 75f;  // °/s beim Drehen (Shift: langsamer)

        void RotateSelectedByKeys()
        {
            if (_selected == null) return;
            float speed = (IsShift() ? RotateSpeed / 3f : RotateSpeed) * Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)) PushUndo();
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
                if (Input.GetKeyDown(KeyCode.F)) { if (_carried != null) Drop(); rig.ExitWalk(); return; }
                // Esc gibt nur die Maus frei (der Browser macht das ohnehin); zweites Esc beendet den Laufmodus
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_wasLocked || StudioCameraRig.MouseLocked) StudioCameraRig.LockMouse(false);
                    else { if (_carried != null) Drop(); rig.ExitWalk(); return; }
                }
                if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                if (Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
                if (Input.GetKeyDown(KeyCode.C)) NextCamera(1);
                if (_selected != null && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))) { if (_carried == _selected) Drop(); DeleteSelected(); }
            }

            // an eine Kamera treten
            _nearCam = _carried == null ? NearestCamera(1.8f) : null;
            if (!typing && _nearCam != null && Input.GetKeyDown(KeyCode.E)) { rig.EnterPov(_nearCam); return; }

            bool lockedNow = StudioCameraRig.MouseLocked;
            if (!lockedNow && Input.GetMouseButtonDown(0) && !PointerOverUI())
            {
                // Klick in die Szene: Maus fangen (Umsehen wie in Spielen)
                StudioCameraRig.LockMouse(true);
                _wasLocked = true;
                return;
            }
            _wasLocked = lockedNow;
            if (lockedNow && Input.GetMouseButtonDown(0))
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

        StudioCam _nearCam;
        bool _wasLocked;

        StudioCam NearestCamera(float maxDist)
        {
            StudioCam best = null;
            float bd = maxDist;
            foreach (var c in Cameras())
            {
                var d = Vector3.ProjectOnPlane(c.transform.position - rig.WalkFeet, Vector3.up).magnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
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
            if (!_hideUI && _selected == null && _fixture != null && _fixture.lightSource != null)
            {
                // Ring auf dem Boden, wo die Strahlachse auftrifft
                var lt = _fixture.lightSource.transform;
                var hitPos = lt.forward.y < -0.05f ? lt.position + lt.forward * (lt.position.y / -lt.forward.y) : new Vector3(_fixture.transform.position.x, 0, _fixture.transform.position.z);
                _selRing.SetActive(true);
                _selRing.transform.position = new Vector3(hitPos.x, 0.012f, hitPos.z);
                _selRing.transform.localScale = new Vector3(0.8f, 0.005f, 0.8f);
                return;
            }
            if (_hideUI || _selected == null || !_selected.gameObject.activeInHierarchy) { _selRing.SetActive(false); return; }
            var col = _selected.GetComponent<BoxCollider>();
            float r = col != null ? Mathf.Max(col.size.x, col.size.z) * 0.6f + 0.1f : 0.6f;
            _selRing.SetActive(true);
            var c = col != null ? _selected.transform.TransformPoint(new Vector3(col.center.x, 0, col.center.z)) : _selected.transform.position;
            _selRing.transform.position = new Vector3(c.x, 0.012f, c.z);
            _selRing.transform.localScale = new Vector3(r * 2, 0.005f, r * 2);
        }

        StudioFixture _fixture;

        void SelectFixture(StudioFixture f)
        {
            Select(null);
            _fixture = f;
            if (f != null) _tab = Tab.Light;
        }

        /// <summary>Ausgewählten Deckenscheinwerfer mit Pfeiltasten verfahren (begrenzte Geschwindigkeit).</summary>
        void MoveFixtureByKeys()
        {
            if (_fixture == null || GUIUtility.keyboardControl != 0) return;
            float dt = Time.unscaledDeltaTime, v = IsShift() ? 1.2f : 0.5f;
            bool changed = false;
            var fwd = Vector3.ProjectOnPlane(rig.Cam.transform.forward, Vector3.up).normalized;
            var rgt = Vector3.Cross(Vector3.up, fwd);
            var mv = Vector3.zero;
            if (Input.GetKey(KeyCode.UpArrow)) mv += fwd;
            if (Input.GetKey(KeyCode.DownArrow)) mv -= fwd;
            if (Input.GetKey(KeyCode.RightArrow)) mv += rgt;
            if (Input.GetKey(KeyCode.LeftArrow)) mv -= rgt;
            if (mv.sqrMagnitude > 0) { _fixture.x += mv.normalized.x * v * dt; _fixture.z += mv.normalized.z * v * dt; changed = true; }
            if (Input.GetKey(KeyCode.Home)) { _fixture.drop -= 0.4f * dt; changed = true; }
            if (Input.GetKey(KeyCode.End)) { _fixture.drop += 0.4f * dt; changed = true; }
            if (changed) { ApplyLight(); _fixture.RefitCollider(); }
        }

        void Select(StudioProp p)
        {
            if (p != null) _fixture = null;
            _selected = p;
            _propImagePath = p != null ? p.imagePath : "";
            if (p != null) _tab = Tab.Objects;
        }

        // ---------- Rückgängig / Wiederholen ----------

        readonly List<string> _undo = new List<string>(), _redo = new List<string>();
        float _lastUndoPush = -10f;

        /// <summary>Zustand vor einer Änderung merken (mehrere Pushes innerhalb 0,3 s zählen als einer).</summary>
        void PushUndo()
        {
            if (Time.unscaledTime - _lastUndoPush < 0.3f) return;
            _lastUndoPush = Time.unscaledTime;
            var json = JsonUtility.ToJson(Capture());
            if (_undo.Count > 0 && _undo[_undo.Count - 1] == json) return;
            _undo.Add(json);
            if (_undo.Count > 40) _undo.RemoveAt(0);
            _redo.Clear();
        }

        void Undo()
        {
            if (_undo.Count == 0) { Status("Nichts zum Rückgängigmachen"); return; }
            _redo.Add(JsonUtility.ToJson(Capture()));
            var json = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            LoadJson(json, true);
            Status("Rückgängig");
        }

        void Redo()
        {
            if (_redo.Count == 0) { Status("Nichts zum Wiederholen"); return; }
            _undo.Add(JsonUtility.ToJson(Capture()));
            var json = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            LoadJson(json, true);
            Status("Wiederholt");
        }

        bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        void HandleUndoKeys()
        {
            if (GUIUtility.keyboardControl != 0 || !Ctrl) return;
            if (Input.GetKeyDown(KeyCode.Z)) { if (IsShift()) Redo(); else Undo(); }
            if (Input.GetKeyDown(KeyCode.Y)) Redo();
        }

        /// <summary>Ansicht auf ein Objekt zoomen.</summary>
        void FocusSelected()
        {
            if (_selected == null) return;
            var col = _selected.GetComponent<BoxCollider>();
            var b = col != null ? col.bounds : new Bounds(_selected.transform.position + Vector3.up * 0.5f, Vector3.one);
            rig.FocusOn(b.center, b.extents.magnitude * 3.2f + 1f);
        }

        float _lastClickTime;
        StudioProp _lastClickProp;

        // ---------- Modusleiste oben + Werkzeugleiste am Objekt ----------

        Rect _modeBarRect, _objBarRect;

        void DrawModeBar()
        {
            float w = 560, h = 36;
            _modeBarRect = new Rect(Mathf.Max(376f, (UiW - w) / 2f), 8, w, h); // nicht unter dem Seitenpanel
            GUILayout.BeginArea(_modeBarRect, _box);
            GUILayout.BeginHorizontal();
            var prev = GUI.backgroundColor;
            bool overview = !rig.InWalk && !rig.InPov;
            GUI.backgroundColor = overview ? new Color(0.5f, 0.8f, 1f) : prev;
            if (GUILayout.Button("Übersicht", GUILayout.Height(22))) { rig.ExitPov(); rig.ExitWalk(); }
            GUI.backgroundColor = rig.InWalk ? new Color(0.5f, 0.8f, 1f) : prev;
            if (GUILayout.Button("Laufen (F)", GUILayout.Height(22))) { if (!rig.InWalk) { rig.EnterWalk(); StudioCameraRig.LockMouse(true); } }
            GUI.backgroundColor = rig.InPov ? new Color(0.5f, 0.8f, 1f) : prev;
            if (GUILayout.Button("Kamera (C)", GUILayout.Height(22))) NextCamera(1);
            GUI.backgroundColor = prev;
            GUILayout.Space(10);
            GUI.enabled = _undo.Count > 0;
            if (GUILayout.Button(new GUIContent("↶", "Rückgängig (Strg+Z)"), GUILayout.Width(36), GUILayout.Height(22))) Undo();
            GUI.enabled = _redo.Count > 0;
            if (GUILayout.Button(new GUIContent("↷", "Wiederholen (Strg+Y)"), GUILayout.Width(36), GUILayout.Height(22))) Redo();
            GUI.enabled = true;
            if (GUILayout.Button(new GUIContent("Foto", "Screenshot (P)"), GUILayout.Width(48), GUILayout.Height(22))) StartCoroutine(Screenshot());
            if (GUILayout.Button(new GUIContent("Ausblenden", "Oberfläche ausblenden (H)"), GUILayout.Height(22))) _hideUI = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Kleine Werkzeugleiste direkt über dem ausgewählten Objekt.</summary>
        void DrawObjectBar()
        {
            _objBarRect = Rect.zero;
            if (_selected == null || rig.InPov || rig.InWalk || !_selected.gameObject.activeInHierarchy) return;
            var col = _selected.GetComponent<BoxCollider>();
            var top = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z) : _selected.transform.position + Vector3.up;
            var sp = rig.Cam.WorldToScreenPoint(top + Vector3.up * 0.15f);
            if (sp.z <= 0) return;
            var cam = _selected.GetComponent<StudioCam>();
            float w = cam != null ? 330 : 250, h = 30;
            var gx = sp.x / UiScale - w / 2f;
            var gy = (Screen.height - sp.y) / UiScale - h - 6;
            gx = Mathf.Clamp(gx, 376, UiW - w - 8);
            gy = Mathf.Clamp(gy, 52, UiH - h - 8);
            _objBarRect = new Rect(gx, gy, w, h);
            GUILayout.BeginArea(_objBarRect, _box);
            GUILayout.BeginHorizontal();
            if (GUILayout.RepeatButton(new GUIContent("⟲", "Links drehen (Q halten)"), GUILayout.Width(30))) { PushUndo(); _selected.transform.Rotate(0, -RotateSpeed * Time.unscaledDeltaTime, 0, Space.World); }
            if (GUILayout.RepeatButton(new GUIContent("⟳", "Rechts drehen (E halten)"), GUILayout.Width(30))) { PushUndo(); _selected.transform.Rotate(0, RotateSpeed * Time.unscaledDeltaTime, 0, Space.World); }
            if (GUILayout.Button(new GUIContent("Kopie", "Strg+D"))) Duplicate();
            if (GUILayout.Button(new GUIContent("Löschen", "Entf"))) DeleteSelected();
            if (GUILayout.Button(new GUIContent("Fokus", "Ansicht auf das Objekt (Doppelklick)"))) FocusSelected();
            if (cam != null && GUILayout.Button("Durchschauen")) rig.EnterPov(cam);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DeleteSelected()
        {
            if (_selected == null) return;
            PushUndo();
            if (_selected.GetComponent<StudioCam>() == _liveCam) _liveCam = null;
            Destroy(_selected.gameObject);
            Select(null);
        }

        void Duplicate()
        {
            if (_selected == null) return;
            PushUndo();
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
            PushUndo();
            // auf den Boden vor die Ansichtskamera stellen
            var cam = rig.Cam.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var pos = cam.position + fwd * (rig.InWalk ? 2f : 3f);
            pos.x = Mathf.Clamp(pos.x, -StudioBuilder.HalfW + 1, StudioBuilder.HalfW - 1);
            pos.z = Mathf.Clamp(pos.z, -StudioBuilder.HalfD + 1, StudioBuilder.HalfD - 1);
            pos.y = 0;
            float rotY = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg; // zur Kamera gedreht
            var p = PropFactory.Create(id, builder.set, pos, rotY);
            if (p == null) { if (PropFactory.LastError != null) Status(PropFactory.LastError); return; }
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

        void ToggleRecording(StudioCam c)
        {
            var mv = c != null ? c.GetComponent<CameraMove>() : null;
            if (mv == null) return;
            if (mv.recording)
            {
                mv.StopRecording();
                Status($"Aufnahme gespeichert: {mv.Duration:0.0} s — ▶ zum Ansehen (im Sucher oder in der Übersicht)");
            }
            else
            {
                foreach (var other in builder.set.GetComponentsInChildren<CameraMove>()) other.StopRecording();
                mv.StartRecording();
                Status("Aufnahme läuft — Kamera führen, Leertaste beendet");
            }
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

        FirstPersonHands _hands;
        Camera _vfCam;
        RenderTexture _vfRT;

        /// <summary>Bild der Studiokamera für den Suchermonitor (Monitor-Ansicht).</summary>
        void RenderViewfinder()
        {
            var c = rig.Pov;
            if (c == null || !StudioCameraRig.MonitorView) return;
            if (_vfCam == null)
            {
                _vfRT = new RenderTexture(1024, 576, 24) { name = "Sucher" };
                _vfCam = new GameObject("ViewfinderCamera").AddComponent<Camera>();
                _vfCam.transform.SetParent(transform, false);
                _vfCam.enabled = false;
                _vfCam.targetTexture = _vfRT;
                _vfCam.renderingPath = RenderingPath.DeferredShading;
                _vfCam.allowHDR = false;
                _vfCam.nearClipPlane = 0.05f;
                _vfCam.aspect = 16f / 9f;
                _vfCam.clearFlags = CameraClearFlags.SolidColor;
                _vfCam.backgroundColor = Color.black;
                _vfCam.cullingMask = ~(1 << IconRenderer.Layer);
            }
            _vfCam.transform.SetPositionAndRotation(c.eye.position, c.eye.rotation);
            _vfCam.fieldOfView = c.VerticalFov;
            _vfCam.Render();
        }

        void LateUpdate()
        {
            RenderViewfinder();
            if (_hands != null) _hands.Tick(rig.InWalk, _carried != null, rig.WalkFeet);
            if (_restored && Time.unscaledTime > _nextAutoSave) { _nextAutoSave = Time.unscaledTime + 30f; AutoSave(); }
            UpdatePaths();
            UpdatePip();
            if (_programCam == null) return;
            bool feed = _state.programOnMonitor && _liveCam != null && _liveCam.gameObject.activeInHierarchy;
            foreach (var p in AllProps())
            {
                if ((p.typeId != "tv_stand" && p.typeId != "tv75" && p.typeId != "tv_wall") || p.imageMaterial == null || !string.IsNullOrEmpty(p.imagePath)) continue;
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

        // ---------- Bild-im-Bild + Fahrtwege ----------

        Camera _pipCam;
        RenderTexture _pipRT;
        StudioCam _pipTarget;
        int _pipFrame;
        readonly Dictionary<CameraMove, LineRenderer> _paths = new Dictionary<CameraMove, LineRenderer>();
        readonly List<Vector3> _pathPts = new List<Vector3>();
        Material _pathMat;

        StudioCam PipTarget()
        {
            if (!_state.pictureInPicture || rig.InPov || _hideUI) return null;
            var sel = _selected != null ? _selected.GetComponent<StudioCam>() : null;
            if (sel != null && sel.gameObject.activeInHierarchy) return sel;
            return Cameras().FirstOrDefault(c => c.GetComponent<CameraMove>()?.playing == true);
        }

        void UpdatePip()
        {
            _pipTarget = PipTarget();
            if (_pipTarget == null) return;
            if (_pipCam == null)
            {
                _pipRT = new RenderTexture(640, 360, 24) { name = "BildImBild" };
                var go = new GameObject("PipCamera");
                go.transform.SetParent(transform, false);
                _pipCam = go.AddComponent<Camera>();
                _pipCam.enabled = false;
                _pipCam.targetTexture = _pipRT;
                _pipCam.renderingPath = RenderingPath.DeferredShading;
                _pipCam.allowHDR = false;
                _pipCam.nearClipPlane = 0.05f;
                _pipCam.aspect = 16f / 9f;
                _pipCam.clearFlags = CameraClearFlags.SolidColor;
                _pipCam.backgroundColor = Color.black;
            }
            if ((_pipFrame++ & 1) == 0) return; // abwechselnd mit dem Programmbild
            _pipTarget.Apply();
            _pipCam.transform.SetPositionAndRotation(_pipTarget.eye.position, _pipTarget.eye.rotation);
            _pipCam.fieldOfView = _pipTarget.VerticalFov;
            _pipCam.Render();
        }

        void DrawPip()
        {
            if (_pipTarget == null || _pipRT == null) return;
            float w = Mathf.Min(420f, UiW * 0.32f), h = w * 9f / 16f;
            var r = new Rect(UiW - w - 12, 52, w, h);
            Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 26), new Color(0, 0, 0, 0.75f));
            GUI.DrawTexture(r, _pipRT, ScaleMode.StretchToFill, false);
            if (_pipTarget == _liveCam)
            {
                var red = new Color(0.95f, 0.1f, 0.1f);
                Fill(new Rect(r.x, r.y, r.width, 3), red); Fill(new Rect(r.x, r.yMax - 3, r.width, 3), red);
                Fill(new Rect(r.x, r.y, 3, r.height), red); Fill(new Rect(r.xMax - 3, r.y, 3, r.height), red);
            }
            var mv = _pipTarget.GetComponent<CameraMove>();
            string t = "Kamera " + _pipTarget.number + " · " + _pipTarget.focal.ToString("0") + " mm" +
                       (mv != null && mv.keys.Count > 0 ? "  ·  Fahrt " + mv.time.ToString("0.0") + " / " + mv.Duration.ToString("0.0") + " s" : "");
            GUI.Label(new Rect(r.x + 4, r.yMax + 2, r.width, 20), t, _small);
            _pipRect = new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 26);
        }

        Rect _pipRect;

        void UpdatePaths()
        {
            bool show = _state.showPaths && !_hideUI && !rig.InPov;
            foreach (var mv in builder.set.GetComponentsInChildren<CameraMove>(false))
            {
                if (!_paths.TryGetValue(mv, out var lr) || lr == null)
                {
                    if (_pathMat == null) _pathMat = StudioMaterials.Transparent("PathLine", new Color(0.3f, 0.9f, 1f, 0.9f));
                    lr = new GameObject("Fahrtweg").AddComponent<LineRenderer>();
                    lr.transform.SetParent(transform, false);
                    lr.sharedMaterial = _pathMat;
                    lr.widthMultiplier = 0.025f;
                    lr.numCapVertices = 2;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _paths[mv] = lr;
                }
                if (!show || mv.keys.Count < 2) { lr.enabled = false; continue; }
                mv.SamplePath(_pathPts);
                lr.enabled = true;
                lr.positionCount = _pathPts.Count;
                lr.SetPositions(_pathPts.ToArray());
                var sel = _selected != null && _selected.gameObject == mv.gameObject;
                lr.startColor = lr.endColor = sel ? new Color(0.3f, 0.95f, 1f) : new Color(0.3f, 0.95f, 1f, 0.45f);
            }
            // Linien gelöschter Kameras entfernen
            foreach (var dead in _paths.Keys.Where(k => k == null || !k.gameObject.activeInHierarchy).ToList())
            {
                if (_paths[dead] != null) _paths[dead].enabled = false;
                if (dead == null) { Destroy(_paths[dead]?.gameObject); _paths.Remove(dead); }
            }
        }

        void PlayAll()
        {
            int n = 0;
            foreach (var mv in builder.set.GetComponentsInChildren<CameraMove>(false))
                if (mv.CanPlay) { mv.time = 0; mv.Play(); n++; }
            Status(n > 0 ? n + " Kamerafahrt(en) laufen" : "Noch keine Kamerafahrt geplant (mind. 2 Keyframes)");
        }

        void StopAll()
        {
            foreach (var mv in builder.set.GetComponentsInChildren<CameraMove>(false)) mv.Stop();
        }

        /// <summary>Planung einer Kamerafahrt: Keyframes setzen, abspielen, Zeitleiste.</summary>
        void DrawMoveControls(StudioCam cam, bool compact)
        {
            var mv = cam.GetComponent<CameraMove>();
            if (mv == null) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Keyframe", GUILayout.Width(compact ? 90 : 100))) { mv.AddKey(); Status("Keyframe " + mv.keys.Count + " gesetzt"); }
            GUI.enabled = mv.CanPlay;
            if (GUILayout.Button(mv.playing ? "❚❚ Pause" : "▶ Abspielen", GUILayout.Width(compact ? 90 : 100))) { if (mv.playing) mv.Pause(); else mv.Play(); }
            if (GUILayout.Button("■", GUILayout.Width(30))) mv.Stop();
            GUI.enabled = true;
            mv.loop = GUILayout.Toggle(mv.loop, " Schleife", GUILayout.Width(80));
            mv.ease = GUILayout.Toggle(mv.ease, " weich", GUILayout.Width(64));
            GUILayout.EndHorizontal();

            if (mv.keys.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(mv.time.ToString("0.0") + " / " + mv.Duration.ToString("0.0") + " s", _small, GUILayout.Width(80));
                float nt = GUILayout.HorizontalSlider(mv.time, 0f, Mathf.Max(0.01f, mv.Duration));
                if (Mathf.Abs(nt - mv.time) > 0.0001f) { mv.Pause(); mv.time = nt; mv.ApplyAt(nt); }
                GUILayout.EndHorizontal();
            }
            if (compact) return;

            if (mv.keys.Count == 0)
            {
                GUILayout.Label("So planst du eine Fahrt: Kamera einstellen (Position, Schwenk, Zoom) → „+ Keyframe“, dann Kamera verschieben/umstellen → wieder „+ Keyframe“ … → „Abspielen“.", _small);
                return;
            }
            for (int i = 0; i < mv.keys.Count; i++)
            {
                var k = mv.keys[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label("K" + (i + 1) + "  " + k.time.ToString("0.0") + " s · " + k.focal.ToString("0") + " mm", _small, GUILayout.Width(120));
                if (i > 0)
                {
                    float seg = k.time - mv.keys[i - 1].time;
                    if (GUILayout.Button("−", GUILayout.Width(22))) mv.SetSegmentDuration(i, seg - 0.5f);
                    if (GUILayout.Button("+", GUILayout.Width(22))) mv.SetSegmentDuration(i, seg + 0.5f);
                }
                else GUILayout.Space(48);
                if (GUILayout.Button("Gehe zu", GUILayout.Width(62))) { mv.Pause(); mv.time = k.time; mv.ApplyAt(k.time); }
                if (GUILayout.Button("Übernehmen", GUILayout.Width(84))) mv.UpdateKey(i);
                if (GUILayout.Button("×", GUILayout.Width(22))) { mv.RemoveKey(i); break; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("−/+ ändert die Dauer bis zu diesem Keyframe. „Übernehmen“ speichert die aktuelle Einstellung in den Keyframe.", _small);
        }

        // ================= Licht / Umgebung =================

        void ApplyLight()
        {
            StudioBuilder.SetPhysicalLighting(_state.physicalFalloff);
            StudioBuilder.SetShadowQuality(_state.shadowQuality);
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
            _state.fixtureStates = builder.fixtures.Where(f => f != null).Select(f => new FixtureData
            {
                number = f.number, on = f.on, x = f.x, z = f.z, drop = f.drop, pan = f.pan, tilt = f.tilt,
                dimmer = f.dimmer, focus = f.focus, doors = f.doors != null ? (float[])f.doors.angles.Clone() : new float[4],
                ownKelvin = f.ownKelvin, kelvin = f.kelvin, gel = f.gel, gelName = f.gelName,
                castShadows = f.castShadows, diffusion = f.diffusion
            }).ToList();
            _state.props = AllProps().Select(p =>
            {
                var d = new PropData
                {
                    type = p.typeId,
                    pos = p.transform.localPosition,
                    rotY = p.transform.localEulerAngles.y,
                    color = p.color,
                    image = p.imagePath ?? "",
                    castShadows = p.castShadows
                };
                var c = p.GetComponent<StudioCam>();
                if (c != null) { d.camNumber = c.number; d.camPan = c.pan; d.camTilt = c.tilt; d.camFocal = c.focal; d.camHeight = c.height; }
                var mv = p.GetComponent<CameraMove>();
                if (mv != null) { d.camKeys = mv.keys.Select(k => JsonUtility.FromJson<CamKey>(JsonUtility.ToJson(k))).ToList(); d.camEase = mv.ease; d.camLoop = mv.loop; }
                var al = p.GetComponent<AimableLight>();
                if (al != null) { d.lightPan = al.pan; d.lightTilt = al.tilt; d.lightIntensity = al.intensity; }
                var ha = p.GetComponent<HeightAdjust>();
                if (ha != null) { d.mountHeight = ha.height; d.screenDiagonal = ha.HasSize ? ha.diagonal : -1f; }
                var pj = p.GetComponent<ProjectorSim>();
                if (pj != null) { d.projPan = pj.pan; d.projTilt = pj.tilt; d.projHeight = pj.height; d.projThrow = pj.throwRatio; d.projBrightness = pj.brightness; }
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
            string json;
            try { json = File.ReadAllText(file); }
            catch { Status("Datei konnte nicht gelesen werden: " + file); return; }
            LoadJson(json);
        }

        /// <summary>Rückruf aus dem Browser: Inhalt einer hochgeladenen .json-Variante.</summary>
        public void OnWebProject(string json) => LoadJson(json);

        void LoadJson(string json) => LoadJson(json, false);

        void LoadJson(string json, bool undo)
        {
            ProjectData data = null;
            try { data = JsonUtility.FromJson<ProjectData>(json); } catch { }
            if (data == null || data.props == null) { Status("Keine gültige Studio-Variante"); return; }
            if (!undo) { rig.ExitWalk(); rig.ExitPov(); }
            if (_carried != null) Drop();
            foreach (var p in AllProps().ToList()) DestroyImmediate(p.gameObject);
            Select(null);
            foreach (var d in data.props)
            {
                var p = PropFactory.Create(d.type, builder.set, d.pos, d.rotY, d.color);
                if (p == null) continue;
                if (!string.IsNullOrEmpty(d.image)) p.SetImage(d.image);
                if (!d.castShadows) p.SetCastShadows(false);
                var c = p.GetComponent<StudioCam>();
                if (c != null && d.camNumber > 0)
                {
                    c.SetNumber(d.camNumber);
                    c.pan = d.camPan; c.tilt = d.camTilt; c.focal = d.camFocal; c.height = d.camHeight;
                    c.Apply();
                }
                var mv = p.GetComponent<CameraMove>();
                if (mv != null && d.camKeys != null) { mv.keys = d.camKeys; mv.ease = d.camEase; mv.loop = d.camLoop; }
                var al = p.GetComponent<AimableLight>();
                if (al != null) { al.pan = d.lightPan; al.tilt = d.lightTilt; al.intensity = d.lightIntensity; al.Apply(); }
                var ha = p.GetComponent<HeightAdjust>();
                if (ha != null)
                {
                    if (d.mountHeight >= 0) ha.height = d.mountHeight;
                    if (d.screenDiagonal > 0 && ha.HasSize) ha.diagonal = d.screenDiagonal;
                    ha.Apply();
                    Geo.FitCollider(p.gameObject);
                }
                var pj = p.GetComponent<ProjectorSim>();
                if (pj != null) { pj.pan = d.projPan; pj.tilt = d.projTilt; pj.height = d.projHeight; pj.throwRatio = d.projThrow; pj.brightness = d.projBrightness; pj.Apply(); }
            }
            _state = data;
            _projectName = data.name;
            if (data.fixtures != null && data.fixtures.Length == builder.fixtureLights.Count) _fixtureOn = data.fixtures;
            else _fixtureOn = Enumerable.Repeat(true, builder.fixtureLights.Count).ToArray();
            if (data.fixtureStates != null)
                foreach (var fd in data.fixtureStates)
                {
                    var f = builder.fixtures.FirstOrDefault(x => x != null && x.number == fd.number);
                    if (f == null) continue;
                    f.on = fd.on; f.x = fd.x; f.z = fd.z; f.drop = fd.drop; f.pan = fd.pan; f.tilt = fd.tilt;
                    f.dimmer = fd.dimmer; if (fd.focus > 0) f.focus = fd.focus;
                    if (f.doors != null && fd.doors != null && fd.doors.Length == 4) f.doors.angles = (float[])fd.doors.Clone();
                    f.ownKelvin = fd.ownKelvin; f.kelvin = fd.kelvin; f.gel = fd.gel; f.gelName = fd.gelName;
                    f.castShadows = fd.castShadows; f.diffusion = fd.diffusion;
                    f.RefitCollider();
                }
            _fixtureOn = builder.fixtures.Select(f => f == null || f.on).ToArray();
            SetFloorColor(data.floorColor);
            _wallImagePath = data.wallImage;
            SetWallImage(data.wallImage);
            SetWallColor(data.wallColor);
            SetEquipmentVisible(data.showEquipment);
            SetLive(Cameras().FirstOrDefault(c => c.number == data.liveCamera) ?? Cameras().FirstOrDefault());
            ApplyLight();
            if (!undo) Status("Geladen: " + data.name);
        }

        // ---------- automatisch speichern / letzte Sitzung ----------

        const string AutoSaveName = "_Letzte Sitzung";
        float _nextAutoSave = 30f;
        bool _restored;

        string AutoSaveFile => Path.Combine(ProjectsDir, AutoSaveName + ".json");

        void AutoSave()
        {
            try
            {
                Directory.CreateDirectory(ProjectsDir);
                File.WriteAllText(AutoSaveFile, JsonUtility.ToJson(Capture(), true));
                WebBridge.SyncFS();
            }
            catch (System.Exception e) { Debug.LogWarning("Autosave: " + e.Message); }
        }

        IEnumerator RestoreLastSession()
        {
            // im Browser wird IndexedDB erst kurz nach dem Start eingebunden
            yield return new WaitForSecondsRealtime(1f);
            _restored = true;
            if (File.Exists(AutoSaveFile))
            {
                Load(AutoSaveFile);
                Status("Letzte Sitzung wiederhergestellt (Projekt → „Zurücksetzen“ für den Fotostand)");
            }
        }

        void OnApplicationQuit() { if (_restored) AutoSave(); }

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
            _defaultFixtures = CaptureFixtures();
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
                if (rig.InPov && StudioCameraRig.MonitorView) DrawViewfinderHud(); // Monitorbild gehört zur Szene
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
                DrawViewfinderHud();
                GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1f));
                DrawPovBar();
                return;
            }

            if (rig.InWalk) DrawWalkOverlay();
            if (rig.InWalk && StudioCameraRig.MouseLocked)
            {
                // Spielansicht: keine Panels, nur Fadenkreuz und Hinweise
                _panelRect = _modeBarRect = _objBarRect = Rect.zero;
                return;
            }
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

            DrawPip();
            DrawObjectBar();
            DrawModeBar();
            DrawHelp();
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                var mp = Event.current.mousePosition;
                var tr = new Rect(mp.x + 14, mp.y + 18, 230, 22);
                Fill(tr, new Color(0, 0, 0, 0.85f));
                GUI.Label(new Rect(tr.x + 6, tr.y + 2, tr.width - 8, tr.height), GUI.tooltip, _small);
            }
            // Mausklick ins Panel = mögliche Änderung → für Rückgängig merken
            if (Event.current.type == EventType.MouseDown && _panelRect.Contains(Event.current.mousePosition)) PushUndo();
        }

        // ---------- Sucheransicht ----------

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        /// <summary>
        /// Sucher-Overlay wie am Kameramonitor (Blackmagic-Stil, vgl. Foto vom Set): Statuszeile mit
        /// RES/FPS/SPEED/IRIS/GAIN/WB und Akku, Drittelraster, Tally. Optional mit Monitorgehäuse.
        /// </summary>
        void DrawViewfinderHud()
        {
            var cam = rig.Pov;
            if (cam == null) return;
            GUI.matrix = Matrix4x4.identity;
            var f = StudioCameraRig.Frame169();
            float sw = Screen.width, sh = Screen.height, s = UiScale;
            bool live = cam == _liveCam;

            if (StudioCameraRig.MonitorView)
            {
                if (f.width < 2 || _vfRT == null) return;
                // Monitorbild: oben Statuszeile, darunter das 16:9-Kamerabild (wie am Set)
                Fill(f, Color.black);
                float imgH = f.width * 9f / 16f;
                var img = new Rect(f.x, f.yMax - imgH, f.width, imgH);
                GUI.DrawTexture(img, _vfRT, ScaleMode.StretchToFill, false);
                DrawViewfinderOverlay(cam, img, new Rect(f.x, f.y, f.width, f.height - imgH), live, s, true);
                return;
            }
            var dim = new Color(0, 0, 0, 0.85f);
            if (f.x > 0) { Fill(new Rect(0, 0, f.x, sh), dim); Fill(new Rect(f.xMax, 0, sw - f.xMax, sh), dim); }
            if (f.y > 0) { Fill(new Rect(0, 0, sw, f.y), dim); Fill(new Rect(0, f.yMax, sw, sh - f.yMax), dim); }
            DrawViewfinderOverlay(cam, f, new Rect(f.x, f.y, f.width, f.height * 0.085f), live, s, false);
        }

        /// <summary>Raster + Statuszeile (Bezeichnung klein über dem Wert) + REC/Tally, auf ein Bildrechteck.</summary>
        void DrawViewfinderOverlay(StudioCam cam, Rect f, Rect bar, bool live, float s, bool monitor)
        {
            if (!_state.viewfinderOverlay) return;
            // Drittelraster (wie im Kameramonitor)
            var line = new Color(1, 1, 1, 0.55f);
            for (int i = 1; i <= 2; i++)
            {
                Fill(new Rect(f.x + f.width * i / 3f, f.y, Mathf.Max(1, s), f.height), line);
                Fill(new Rect(f.x, f.y + f.height * i / 3f, f.width, Mathf.Max(1, s)), line);
            }

            // Statuszeile: kleine Bezeichnung über dem Wert
            float barH = bar.height;
            Fill(bar, new Color(0, 0, 0, monitor ? 1f : 0.6f));
            int fsVal = Mathf.Max(9, Mathf.RoundToInt(barH * 0.36f)), fsKey = Mathf.Max(7, Mathf.RoundToInt(barH * 0.22f));
            var keyStyle = new GUIStyle(_hud) { fontSize = fsKey, alignment = TextAnchor.LowerLeft, normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } };
            var valStyle = new GUIStyle(_hud) { fontSize = fsVal, alignment = TextAnchor.UpperLeft };
            var nameStyle = new GUIStyle(_hudBig) { fontSize = Mathf.RoundToInt(barH * 0.42f) };
            GUI.Label(new Rect(bar.x + barH * 0.4f, bar.y, bar.width * 0.16f, barH), "Camera " + cam.number, nameStyle);
            var keys = new[] { "RES", "FPS", "SPEED", "IRIS", "GAIN", "WB", "FOCAL" };
            var vals = new[] { "1080p", "50", "1/50", "f1.8", "+0dB", Mathf.RoundToInt(_state.lightKelvin / 100f) * 100 + "K", cam.focal.ToString("0") + "mm" };
            float x0 = bar.x + bar.width * 0.2f, colW = bar.width * 0.095f;
            for (int i = 0; i < keys.Length; i++)
            {
                GUI.Label(new Rect(x0 + i * colW, bar.y, colW, barH * 0.45f), keys[i], keyStyle);
                GUI.Label(new Rect(x0 + i * colW, bar.y + barH * 0.45f, colW, barH * 0.55f), vals[i], valStyle);
            }
            // Akku-Anzeige
            var bat = new Rect(bar.xMax - barH * 1.7f, bar.y + barH * 0.25f, barH * 1.3f, barH * 0.5f);
            Fill(bat, new Color(1, 1, 1, 0.85f));
            Fill(new Rect(bat.x + 1.5f * s, bat.y + 1.5f * s, bat.width - 3 * s, bat.height - 3 * s), new Color(0.05f, 0.05f, 0.05f, 1f));
            GUI.Label(bat, "14.5V", new GUIStyle(_hud) { fontSize = Mathf.Max(7, Mathf.RoundToInt(barH * 0.24f)) });

            var rec = cam.GetComponent<CameraMove>();
            if (rec != null && (rec.recording || rec.playing))
            {
                bool blink = rec.playing || Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
                var rr = monitor ? new Rect(f.xMax - f.width * 0.24f, f.y + 4 * s, f.width * 0.22f, Mathf.Max(14, f.height * 0.08f))
                                 : new Rect(f.xMax - 170 * s, f.y + barH + 8 * s, 156 * s, 24 * s);
                Fill(rr, new Color(0, 0, 0, 0.6f));
                var t = System.TimeSpan.FromSeconds(rec.time);
                GUI.Label(rr, (rec.recording ? (blink ? "● REC  " : "   REC  ") : "▶ PLAY  ") + $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}", new GUIStyle(_hudBig) { fontSize = Mathf.RoundToInt(15 * s) });
            }

            // Tally: rote Umrandung im Vollbild, wenn diese Kamera auf Sendung ist
            if (live && !monitor)
            {
                var red = new Color(0.95f, 0.1f, 0.1f, 0.9f);
                float t = 4 * s;
                Fill(new Rect(f.x, f.y, f.width, t), red);
                Fill(new Rect(f.x, f.yMax - t, f.width, t), red);
                Fill(new Rect(f.x, f.y, t, f.height), red);
                Fill(new Rect(f.xMax - t, f.y, t, f.height), red);
            }
        }

        void DrawPovBar()
        {
            var cam = rig.Pov;
            if (cam == null) return;
            float h = 182;
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
            _state.viewfinderOverlay = GUILayout.Toggle(_state.viewfinderOverlay, " Raster/Status", GUILayout.Width(110));
            StudioCameraRig.MonitorView = GUILayout.Toggle(StudioCameraRig.MonitorView, " Monitor-Ansicht", GUILayout.Width(130));
            if (GUILayout.Button("Oberfläche aus (H)", GUILayout.Width(140))) _hideUI = true;
            GUILayout.EndHorizontal();
            var mvRec = cam.GetComponent<CameraMove>();
            GUILayout.BeginHorizontal();
            var prevBg = GUI.backgroundColor;
            if (mvRec != null && mvRec.recording) GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button(mvRec != null && mvRec.recording ? "■ Aufnahme stoppen (Leertaste)" : "● Aufnahme: Kamerafahrt live aufzeichnen (Leertaste)", GUILayout.Height(26))) ToggleRecording(cam);
            GUI.backgroundColor = prevBg;
            GUILayout.EndHorizontal();
            DrawMoveControls(cam, true);
            GUILayout.Label("Ziehen/Pfeile: schwenken/neigen · Rad: zoomen · WASD: Kamera fahren · R/F: höher/tiefer · C: nächste Kamera", _small);
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
            if (_nearCam != null && _carried == null)
            {
                var r0 = new Rect(0, Screen.height / 2f + 30 * s, Screen.width, 30 * s);
                Fill(new Rect(c.x - 170 * s, r0.y, 340 * s, r0.height), new Color(0, 0, 0, 0.6f));
                GUI.Label(r0, "E: an Kamera " + _nearCam.number + " treten und führen", st);
            }
            string hint = !StudioCameraRig.MouseLocked
                ? "Maus frei — in die Szene klicken, um sich wieder umzusehen · F: Laufmodus beenden"
                : _carried != null
                ? "Trägt: " + _carried.displayName + "  ·  Q/E drehen  ·  Mausrad näher/weiter  ·  Klick: abstellen"
                : "Maus: umsehen · WASD gehen · Shift rennen · Klick auf Objekt: aufnehmen · Esc: Maus frei · F: beenden";
            var r = new Rect(0, Screen.height - 40 * s, Screen.width, 30 * s);
            Fill(new Rect(c.x - 380 * s, r.y, 760 * s, r.height), new Color(0, 0, 0, 0.5f));
            GUI.Label(r, hint, st);
        }

        // ---------- Tabs ----------

        void DrawViewTab()
        {
            if (GUILayout.Button(rig.InWalk ? "Laufmodus beenden (F)" : "Durchs Studio laufen (F)", GUILayout.Height(30)))
            {
                if (rig.InWalk) rig.ExitWalk(); else { rig.EnterWalk(); StudioCameraRig.LockMouse(true); }
            }
            GUILayout.Label("Mausempfindlichkeit im Laufmodus: " + StudioCameraRig.MouseSensitivity.ToString("0.0"), _small);
            StudioCameraRig.MouseSensitivity = GUILayout.HorizontalSlider(StudioCameraRig.MouseSensitivity, 0.3f, 3f);
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Alle Fahrten abspielen", GUILayout.Height(26))) PlayAll();
            if (GUILayout.Button("■ Stopp", GUILayout.Height(26), GUILayout.Width(70))) StopAll();
            GUILayout.EndHorizontal();
            _state.showPaths = GUILayout.Toggle(_state.showPaths, " Fahrtwege im Studio zeigen");
            _state.pictureInPicture = GUILayout.Toggle(_state.pictureInPicture, " Kamerabild einblenden (Bild im Bild)");
            bool feed = GUILayout.Toggle(_state.programOnMonitor, " Monitore/Fernseher ohne eigenes Bild zeigen das Live-Bild");
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

        // ---------- Lager (Katalog mit Vorschaubildern) ----------

        IconRenderer _iconRenderer;
        string _category = "Möbel";
        string _hoverItem;
        const string CustomCategory = "Eigene";
        string _customPath = "";

        IconRenderer Icons => _iconRenderer != null ? _iconRenderer : (_iconRenderer = gameObject.AddComponent<IconRenderer>());

        void DrawObjectsTab()
        {
            if (_selected != null) DrawSelected();
            else GUILayout.Label("Objekt in der Szene anklicken, um es auszuwählen. Ziehen verschiebt, Q/E halten dreht, Entf löscht, Strg+D kopiert.", _small);

            GUILayout.Space(10);
            GUILayout.Label("Lager", _h2);
            var cats = PropFactory.Catalog.Select(e => e.category).Distinct().ToList();
            cats.Add(CustomCategory);
            int ci = Mathf.Max(0, cats.IndexOf(_category));
            ci = GUILayout.SelectionGrid(ci, cats.ToArray(), 4);
            _category = cats[ci];
            GUILayout.Space(4);

            if (_category == CustomCategory) DrawCustomItems();
            else DrawTileGrid(PropFactory.Catalog.Where(e => e.category == _category).Select(e => (e.id, e.name)).ToList());

            GUILayout.Space(10);
            GUILayout.Label("Im Studio", _h2);
            foreach (var p in AllProps().ToList())
            {
                var cam = p.GetComponent<StudioCam>();
                var label = (p == _selected ? "▶ " : "") + p.displayName + (cam != null ? " " + cam.number : "") + (p.gameObject.activeSelf ? "" : " (ausgeblendet)");
                if (GUILayout.Button(label, GUI.skin.label)) Select(p);
            }
        }

        /// <summary>Kacheln mit Vorschaubild und Namen; Klick stellt das Objekt ins Studio.</summary>
        void DrawTileGrid(List<(string id, string name)> items, System.Action<string> onDelete = null)
        {
            const int perRow = 3;
            const float tile = 104f;
            string hover = null;
            string toDelete = null;
            for (int i = 0; i < items.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, items.Count); j++)
                {
                    var (id, name) = items[j];
                    GUILayout.BeginVertical(GUILayout.Width(tile));
                    var icon = Icons.Get(id);
                    var rect = GUILayoutUtility.GetRect(tile, tile, GUILayout.Width(tile), GUILayout.Height(tile));
                    bool selectedType = _selected != null && _selected.typeId == id;
                    if (selectedType) Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0.3f, 0.85f, 1f));
                    bool clicked = icon != null
                        ? GUI.Button(rect, new GUIContent(icon, name), GUIStyle.none)
                        : GUI.Button(rect, new GUIContent("Vorschau …", name), GUI.skin.box);
                    if (clicked) Spawn(id);
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        hover = name;
                        Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), new Color(0.95f, 0.85f, 0.6f));
                    }
                    GUILayout.Label(name, new GUIStyle(_small) { alignment = TextAnchor.UpperCenter, fontSize = 11 }, GUILayout.Width(tile), GUILayout.Height(30));
                    if (onDelete != null && GUILayout.Button("Löschen", GUILayout.Width(tile))) toDelete = id;
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            if (Event.current.type == EventType.Repaint) _hoverItem = hover;
            GUILayout.Label(_hoverItem != null ? "Klicken, um „" + _hoverItem + "“ ins Studio zu stellen" : "Kachel anklicken, um das Objekt ins Studio zu stellen", _small);
            if (toDelete != null) onDelete(toDelete);
        }

        void DrawCustomItems()
        {
            GUILayout.Label("Eigenes Objekt hinzufügen", _h2);
            GUILayout.Label("3D-Modell — " + GlbLoader.FormatHint, _small);
            GUILayout.Label(CustomItems.ImageHint, _small);
            if (WebBridge.IsWeb)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("3D-Modell (.glb) …", GUILayout.Height(26))) WebBridge.PickFile(gameObject.name, nameof(OnWebCustomFile), ".glb");
                if (GUILayout.Button("Bild als Aufsteller …", GUILayout.Height(26))) WebBridge.PickFile(gameObject.name, nameof(OnWebCustomFile), "image/png,image/jpeg");
                GUILayout.EndHorizontal();
                GUILayout.Label("Eigene Dateien bleiben in diesem Browser. Beim Weitergeben einer Variante die Datei mitschicken.", _small);
            }
            else
            {
                GUILayout.Label("Pfad zur Datei (.glb, .png, .jpg):", _small);
                _customPath = GUILayout.TextField(_customPath ?? "");
                GUILayout.BeginHorizontal();
#if UNITY_EDITOR
                if (GUILayout.Button("Auswählen…"))
                {
                    var p = UnityEditor.EditorUtility.OpenFilePanel("Eigenes Objekt", "", "glb,png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(p)) _customPath = p;
                }
#endif
                if (GUILayout.Button("Hinzufügen"))
                {
                    var path = _customPath.Trim().Trim('"');
                    if (File.Exists(path)) AddCustom(Path.GetFileName(path), File.ReadAllBytes(path));
                    else Status("Datei nicht gefunden: " + path);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            var custom = CustomItems.All();
            if (custom.Count == 0) GUILayout.Label("Noch keine eigenen Objekte.", _small);
            else DrawTileGrid(custom.Select(id => (id, CustomItems.DisplayName(id))).ToList(), id =>
            {
                foreach (var p in AllProps().Where(p => p.typeId == id).ToList()) Destroy(p.gameObject);
                CustomItems.Delete(id);
                Icons.Forget(id);
                Status("Eigenes Objekt gelöscht");
            });
        }

        void AddCustom(string fileName, byte[] bytes)
        {
            var id = CustomItems.Store(fileName, bytes);
            Icons.Forget(id);
            Spawn(id);
            if (PropFactory.LastError != null)
            {
                Status("Konnte nicht geladen werden: " + PropFactory.LastError);
                CustomItems.Delete(id);
            }
        }

        /// <summary>Rückruf aus dem Browser für eigene Objekte: "name|base64".</summary>
        public void OnWebCustomFile(string payload)
        {
            int bar = payload.IndexOf('|');
            if (bar < 0) return;
            byte[] bytes;
            try { bytes = System.Convert.FromBase64String(payload.Substring(bar + 1)); }
            catch { Status("Datei konnte nicht gelesen werden"); return; }
            AddCustom(payload.Substring(0, bar), bytes);
        }

        void DrawSelected()
        {
            var t = _selected.transform;
            var cam = _selected.GetComponent<StudioCam>();
            GUILayout.BeginHorizontal();
            var selIcon = Icons.Get(_selected.typeId);
            if (selIcon != null) GUILayout.Label(selIcon, GUILayout.Width(56), GUILayout.Height(56));
            GUILayout.Label("Ausgewählt: " + _selected.displayName + (cam != null ? " " + cam.number : ""), _h2);
            GUILayout.EndHorizontal();
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

                GUILayout.Space(6);
                GUILayout.Label("Kamerafahrt planen", _h2);
                DrawMoveControls(cam, false);
            }

            var ha = _selected.GetComponent<HeightAdjust>();
            if (ha != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Bildschirm", _h2);
                if (ha.HasSize)
                {
                    var m = ha.ScreenMeters;
                    GUILayout.Label($"Bildschirmgröße: {ha.diagonal:0}\"  ({m.x * 100:0} × {m.y * 100:0} cm Bildfläche)", _small);
                    float nd = Mathf.Round(GUILayout.HorizontalSlider(ha.diagonal, ha.minDiagonal, ha.maxDiagonal));
                    GUILayout.BeginHorizontal();
                    foreach (var inch in new[] { 43, 55, 65, 75, 85, 98 })
                        if (GUILayout.Button(inch + "\"")) nd = inch;
                    GUILayout.EndHorizontal();
                    if (Mathf.Abs(nd - ha.diagonal) > 0.1f) { ha.diagonal = nd; ha.Apply(); Geo.FitCollider(_selected.gameObject); }
                }
                GUILayout.Label($"{ha.label}: {ha.height:0.00} m (Bildmitte)", _small);
                float nh = GUILayout.HorizontalSlider(ha.height, ha.minHeight, ha.maxHeight);
                if (Mathf.Abs(nh - ha.height) > 0.001f) { ha.height = nh; ha.Apply(); Geo.FitCollider(_selected.gameObject); }
            }

            var pj = _selected.GetComponent<ProjectorSim>();
            if (pj != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Beamer", _h2);
                GUILayout.Label($"Schwenk: {pj.pan:0}°", _small);
                pj.pan = GUILayout.HorizontalSlider(pj.pan, -180f, 180f);
                GUILayout.Label($"Neigung: {pj.tilt:0}°", _small);
                pj.tilt = GUILayout.HorizontalSlider(pj.tilt, -60f, 60f);
                GUILayout.Label($"Höhe: {pj.height:0.00} m", _small);
                pj.height = GUILayout.HorizontalSlider(pj.height, pj.minHeight, pj.maxHeight);
                GUILayout.Label($"Bildgröße (Projektionsverhältnis {pj.throwRatio:0.00}:1 — kleiner = größeres Bild)", _small);
                pj.throwRatio = GUILayout.HorizontalSlider(pj.throwRatio, 0.4f, 3f);
                GUILayout.Label($"Helligkeit: {pj.brightness * 100:0} %", _small);
                pj.brightness = GUILayout.HorizontalSlider(pj.brightness, 0.1f, 1.5f);
                pj.Apply();
                GUILayout.Label("Das Bild landet auf der Fläche, auf die der Beamer zeigt (Wand, weiße Wand, Vorhang, Boden oder ein Objekt). Bild unten hochladen.", _small);
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
                if (spot.focus > 0)
                {
                    GUILayout.Label($"Fokus: {spot.focus:0}° — schmal = heller", _small);
                    spot.focus = GUILayout.HorizontalSlider(spot.focus, spot.minFocus, spot.maxFocus);
                }
                DrawDoors(spot.doors);
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
                GUILayout.Label(_selected.GetComponent<ProjectorSim>() != null ? "Projiziertes Bild (PNG/JPG, z. B. 1920×1080):" : "Bild auf dem Bildschirm (PNG/JPG, am besten 16:9, z. B. 1920×1080):", _small);
                var prop = _selected;
                DrawImagePicker(ref _propImagePath, path =>
                {
                    if (prop.SetImage(path)) Status(string.IsNullOrEmpty(path) ? "Bild entfernt" : "Bild: " + Path.GetFileName(path));
                    else Status("Datei nicht gefunden oder kein Bild: " + path);
                });
            }
            bool shadowsOn = GUILayout.Toggle(_selected.castShadows, " wirft Schatten");
            if (shadowsOn != _selected.castShadows) _selected.SetCastShadows(shadowsOn);
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
            GUILayout.Label("Grundlicht im Raum (hellt alle Schatten auf): " + _state.ambient.ToString("0.00"), _small);
            float a = GUILayout.HorizontalSlider(_state.ambient, 0f, 0.6f);
            if (m != _state.lightMaster || k != _state.lightKelvin || a != _state.ambient)
            {
                _state.lightMaster = m; _state.lightKelvin = k; _state.ambient = a;
                ApplyLight();
            }

            GUILayout.Label("Lichtstimmung", _h2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Schattenarm\n(Talkshow)", GUILayout.Height(40))) ApplyLightPreset(LightPreset.Shadowless);
            if (GUILayout.Button("3-Punkt\n(wie auf Foto)", GUILayout.Height(40))) ApplyLightPreset(LightPreset.Classic);
            if (GUILayout.Button("Dramatisch\n(1 Führung)", GUILayout.Height(40))) ApplyLightPreset(LightPreset.Dramatic);
            GUILayout.EndHorizontal();
            GUILayout.Label("Schatten verschwinden wie im echten Studio: weiches Licht (Softlights, Diffusor), von mehreren Seiten, hoch gehängt, plus Aufheller — jedes Licht hellt die Schatten der anderen auf.", _small);
            GUILayout.Space(6);
            bool phys = GUILayout.Toggle(_state.physicalFalloff, " Physikalischer Lichtabfall (1/r², Kelvin, Schatten)");
            if (phys != _state.physicalFalloff) { _state.physicalFalloff = phys; ApplyLight(); }
            GUILayout.Label("Schatten (Personen, Möbel, Technik) — bei langsamen Rechnern niedriger stellen:", _small);
            int sq = GUILayout.Toolbar(_state.shadowQuality, StudioBuilder.ShadowQualityNames);
            if (sq != _state.shadowQuality) { _state.shadowQuality = sq; StudioBuilder.SetShadowQuality(sq); }

            if (_fixture != null) DrawFixturePanel(_fixture);

            GUILayout.Space(8);
            GUILayout.Label("Scheinwerfer an Pantografen (anklicken zum Einstellen)", _h2);
            bool changed = false;
            for (int i = 0; i < builder.fixtures.Count; i++)
            {
                var f = builder.fixtures[i];
                if (f == null) continue;
                GUILayout.BeginHorizontal();
                bool on = GUILayout.Toggle(_fixtureOn[i], "", GUILayout.Width(20));
                if (on != _fixtureOn[i]) { _fixtureOn[i] = on; changed = true; }
                var label = (f == _fixture ? "▶ " : "") + f.Title + (f.dimmer < 0.999f ? "  " + (f.dimmer * 100).ToString("0") + " %" : "") + (f.gel != Color.white ? "  · " + f.gelName : "");
                if (GUILayout.Button(label, GUI.skin.label)) SelectFixture(f);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Alle an")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = true; changed = true; }
            if (GUILayout.Button("Alle auf Sitzgruppe")) { foreach (var f in builder.fixtures) f.AimAt(StudioBuilder.SetCenter + Vector3.up * 0.4f); changed = true; }
            if (GUILayout.Button("Alle aus")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = false; changed = true; }
            GUILayout.EndHorizontal();
            if (changed) ApplyLight();
            GUILayout.Label("Stativscheinwerfer und LED-Röhren findest du unter „Objekte → Licht“.", _small);
        }

        enum LightPreset { Shadowless, Classic, Dramatic }

        List<FixtureData> _defaultFixtures;

        List<FixtureData> CaptureFixtures() => builder.fixtures.Where(f => f != null).Select(f => new FixtureData
        {
            number = f.number, on = f.on, x = f.x, z = f.z, drop = f.drop, pan = f.pan, tilt = f.tilt,
            dimmer = f.dimmer, focus = f.focus, doors = f.doors != null ? (float[])f.doors.angles.Clone() : new float[4],
            ownKelvin = f.ownKelvin, kelvin = f.kelvin, gel = f.gel, gelName = f.gelName,
            castShadows = f.castShadows, diffusion = f.diffusion
        }).ToList();

        void RestoreFixtures(List<FixtureData> list)
        {
            foreach (var fd in list)
            {
                var f = builder.fixtures.FirstOrDefault(x => x != null && x.number == fd.number);
                if (f == null) continue;
                f.on = fd.on; f.x = fd.x; f.z = fd.z; f.drop = fd.drop; f.pan = fd.pan; f.tilt = fd.tilt;
                f.dimmer = fd.dimmer; f.focus = fd.focus;
                if (f.doors != null && fd.doors != null && fd.doors.Length == 4) f.doors.angles = (float[])fd.doors.Clone();
                f.ownKelvin = fd.ownKelvin; f.kelvin = fd.kelvin; f.gel = fd.gel; f.gelName = fd.gelName;
                f.castShadows = fd.castShadows; f.diffusion = fd.diffusion;
                f.RefitCollider();
            }
        }

        /// <summary>
        /// Fertige Lichtaufbauten. „Schattenarm“ verteilt die Scheinwerfer im Kreis um die Sitzgruppe,
        /// hängt sie höher, macht sie weich (Flood + Frost) und ergänzt Aufheller — wie man es im Studio
        /// für schattenarmes Talkshow-Licht macht.
        /// </summary>
        void ApplyLightPreset(LightPreset preset)
        {
            PushUndo();
            if (_defaultFixtures == null) return;
            RestoreFixtures(_defaultFixtures);
            var target = StudioBuilder.SetCenter + Vector3.up * 0.5f;
            var c = new Vector3(StudioBuilder.SetCenter.x, 0, StudioBuilder.SetCenter.z);
            var wall = new HashSet<int> { 23, 12 };   // bleiben auf der weißen Wand

            switch (preset)
            {
                case LightPreset.Shadowless:
                {
                    var setLights = builder.fixtures.Where(f => f != null && !wall.Contains(f.number)).ToList();
                    for (int i = 0; i < setLights.Count; i++)
                    {
                        var f = setLights[i];
                        float a = i * Mathf.PI * 2f / setLights.Count + 0.3f;
                        float radius = f.number <= 5 ? 4.2f : 3.2f; // Randscheinwerfer als zweiter Ring
                        f.x = c.x + Mathf.Cos(a) * radius;
                        f.z = c.z + Mathf.Sin(a) * radius;
                        f.drop = 1.6f;                               // hoch: kurze Schatten am Boden
                        f.on = true;
                        f.dimmer = f.IsFresnel ? 0.75f : 1f;
                        if (f.IsFresnel) { f.focus = f.maxFocus; f.diffusion = 2; }
                        else f.diffusion = 1;
                        for (int k = 0; k < 4 && f.doors != null; k++) f.doors.angles[k] = BarnDoors.OpenAngle;
                        f.AimAt(target);
                        f.RefitCollider();
                    }
                    _state.ambient = 0.28f;   // Reflexionen von Boden/weißer Wand grob nachgebildet
                    _state.lightMaster = 0.9f;
                    Status("Schattenarmes Licht: Scheinwerfer im Kreis, hoch, weich (Flood + Frost), Aufheller");
                    break;
                }
                case LightPreset.Classic:
                    _state.ambient = 0.16f;
                    _state.lightMaster = 1f;
                    Status("3-Punkt-Licht wie auf den Fotos");
                    break;
                case LightPreset.Dramatic:
                    foreach (var f in builder.fixtures)
                    {
                        if (f == null) continue;
                        f.on = f.number == 15 || f.number == 21;
                        if (f.number == 15) { f.focus = 22f; f.diffusion = 0; f.dimmer = 1f; }
                        f.AimAt(target);
                    }
                    _state.ambient = 0.04f;
                    _state.lightMaster = 1.2f;
                    Status("Dramatisch: eine harte Führung + Spitzlicht");
                    break;
            }
            _fixtureOn = builder.fixtures.Select(f => f == null || f.on).ToArray();
            ApplyLight();
        }

        static readonly string[] DoorSteps = { "offen", "¼", "½", "¾", "4/4" };

        /// <summary>Torblende: pro Seite 0 … 4/4 in Vierteln.</summary>
        bool DrawDoors(BarnDoors doors)
        {
            if (doors == null) return false;
            bool changed = false;
            GUILayout.Label("Klappen (Torblende) — Winkel je Seite: −30° gespreizt · 0° parallel · 90° zu", _small);
            for (int i = 0; i < 4; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(BarnDoors.SideNames[i] + $" {doors.angles[i]:0}°", _small, GUILayout.Width(78));
                float na = Mathf.Round(GUILayout.HorizontalSlider(doors.angles[i], BarnDoors.MinAngle, BarnDoors.MaxAngle, GUILayout.Width(120)));
                if (Mathf.Abs(na - doors.angles[i]) > 0.01f) { doors.angles[i] = na; changed = true; }
                // Schnellwahl in Vierteln
                float step = BarnDoors.StepForAngle(doors.angles[i]) * 4f;
                int cur = Mathf.Abs(step - Mathf.Round(step)) < 0.02f ? Mathf.RoundToInt(step) : -1;
                int sel = GUILayout.Toolbar(cur, DoorSteps);
                if (sel != cur && sel >= 0) { doors.angles[i] = BarnDoors.AngleForStep(sel / 4f); changed = true; }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Alle offen")) { for (int i = 0; i < 4; i++) doors.angles[i] = BarnDoors.OpenAngle; changed = true; }
            if (GUILayout.Button("Waagrechter Streifen")) { doors.angles[0] = doors.angles[1] = 70f; doors.angles[2] = doors.angles[3] = BarnDoors.OpenAngle; changed = true; }
            if (GUILayout.Button("Senkrechter Streifen")) { doors.angles[2] = doors.angles[3] = 70f; doors.angles[0] = doors.angles[1] = BarnDoors.OpenAngle; changed = true; }
            GUILayout.EndHorizontal();
            if (changed) doors.Apply();
            return changed;
        }

        bool DrawColorControls(ref bool ownKelvin, ref float kelvin, ref Color gel, ref string gelName)
        {
            bool changed = false;
            bool ok = GUILayout.Toggle(ownKelvin, " Eigene Farbtemperatur");
            if (ok != ownKelvin) { ownKelvin = ok; changed = true; }
            if (ownKelvin)
            {
                GUILayout.Label($"Farbtemperatur: {kelvin:0} K", _small);
                float nk = Mathf.Round(GUILayout.HorizontalSlider(kelvin, 2700f, 6500f) / 50f) * 50f;
                if (Mathf.Abs(nk - kelvin) > 1f) { kelvin = nk; changed = true; }
            }
            GUILayout.Label("Farbfilter (Gel): " + gelName, _small);
            var gels = StudioFixture.Gels;
            var prevBg = GUI.backgroundColor;
            for (int i = 0; i < gels.Length; i += 7)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 7, gels.Length); j++)
                {
                    GUI.backgroundColor = gels[j].c;
                    if (GUILayout.Button(new GUIContent(j == 0 ? "–" : "", gels[j].name), _swatch, GUILayout.Width(38), GUILayout.Height(22)))
                    { gel = gels[j].c; gelName = gels[j].name; changed = true; }
                }
                GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = prevBg;
            GUILayout.BeginHorizontal();
            float r = GUILayout.HorizontalSlider(gel.r, 0, 1), g = GUILayout.HorizontalSlider(gel.g, 0, 1), b = GUILayout.HorizontalSlider(gel.b, 0, 1);
            GUILayout.EndHorizontal();
            if (r != gel.r || g != gel.g || b != gel.b) { gel = new Color(r, g, b); gelName = "eigene Farbe"; changed = true; }
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label(GUI.tooltip, _small);
            return changed;
        }

        void DrawFixturePanel(StudioFixture f)
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(f.Title, _h2);
            if (GUILayout.Button("×", GUILayout.Width(24))) { _fixture = null; GUILayout.EndHorizontal(); return; }
            GUILayout.EndHorizontal();
            bool changed = false;
            int idx = builder.fixtures.IndexOf(f);
            bool on = GUILayout.Toggle(f.on, " eingeschaltet");
            if (on != f.on) { f.on = on; if (idx >= 0) _fixtureOn[idx] = on; changed = true; }

            GUILayout.Label($"Dimmer: {f.dimmer * 100:0} %", _small);
            float d = GUILayout.HorizontalSlider(f.dimmer, 0f, 1f);
            if (Mathf.Abs(d - f.dimmer) > 0.0005f) { f.dimmer = d; changed = true; }
            if (f.IsFresnel)
            {
                GUILayout.Label($"Fokus: {f.focus:0}° ({(f.focus < 25 ? "Spot" : f.focus > 45 ? "Flood" : "mittel")}) — schmal = heller", _small);
                float fo = GUILayout.HorizontalSlider(f.focus, f.minFocus, f.maxFocus);
                if (Mathf.Abs(fo - f.focus) > 0.01f) { f.focus = fo; changed = true; }
            }

            GUILayout.Label("Position an der Decke (Pfeiltasten, Pos1/Ende = Höhe)", _small);
            GUILayout.Label($"Vor/zurück: {f.z:0.00} m", _small);
            float z = GUILayout.HorizontalSlider(f.z, StudioFixture.MinZ, StudioFixture.MaxZ);
            GUILayout.Label($"Seitlich: {f.x:0.00} m", _small);
            float x = GUILayout.HorizontalSlider(f.x, StudioFixture.MinX, StudioFixture.MaxX);
            GUILayout.Label($"Höhe des Scheinwerfers: {f.HeadHeight:0.00} m über dem Boden", _small);
            float h = GUILayout.HorizontalSlider(f.HeadHeight, StudioBuilder.GridY - StudioFixture.MaxDrop - 0.3f, StudioBuilder.GridY - StudioFixture.MinDrop - 0.3f);
            if (Mathf.Abs(z - f.z) > 0.001f || Mathf.Abs(x - f.x) > 0.001f || Mathf.Abs(h - f.HeadHeight) > 0.001f)
            { f.z = z; f.x = x; f.drop = StudioBuilder.GridY - 0.3f - h; changed = true; }

            GUILayout.Label($"Schwenk: {f.pan:0}°   Neigung: {f.tilt:0}°", _small);
            float pn = GUILayout.HorizontalSlider(f.pan, -180f, 180f);
            float tl = GUILayout.HorizontalSlider(f.tilt, -90f, 30f);
            if (Mathf.Abs(pn - f.pan) > 0.01f || Mathf.Abs(tl - f.tilt) > 0.01f) { f.pan = pn; f.tilt = tl; changed = true; }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Auf Sitzgruppe")) { f.AimAt(StudioBuilder.SetCenter + Vector3.up * 0.4f); changed = true; }
            if (GUILayout.Button("Senkrecht nach unten")) { f.tilt = -90f; changed = true; }
            GUILayout.EndHorizontal();

            GUILayout.Label("Schatten", _small);
            bool cs = GUILayout.Toggle(f.castShadows, " wirft Schatten");
            if (cs != f.castShadows) { f.castShadows = cs; changed = true; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Diffusor:", _small, GUILayout.Width(60));
            int df = GUILayout.Toolbar(f.diffusion, StudioFixture.DiffusionNames);
            if (df != f.diffusion) { f.diffusion = df; changed = true; }
            GUILayout.EndHorizontal();
            GUILayout.Label("Tipp gegen harte Schatten: Diffusor davor, Scheinwerfer höher hängen oder ein Softlight als Aufheller von der anderen Seite einschalten.", _small);

            if (DrawDoors(f.doors)) changed = true;
            if (DrawColorControls(ref f.ownKelvin, ref f.kelvin, ref f.gel, ref f.gelName)) changed = true;

            if (changed) { ApplyLight(); f.RefitCollider(); }
        }

        void DrawProjectTab()
        {
            GUILayout.Label("Name der Variante", _h2);
            _projectName = GUILayout.TextField(_projectName);
            if (GUILayout.Button("Variante speichern", GUILayout.Height(26))) Save();
            if (WebBridge.IsWeb)
            {
                GUILayout.Label("Varianten werden in diesem Browser gespeichert. Die Datei kann heruntergeladen und weitergegeben werden.", _small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Herunterladen (.json)"))
                    WebBridge.Download(SafeName(_projectName) + ".json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(Capture(), true)), "application/json");
                if (GUILayout.Button("Variante öffnen (.json)…"))
                    WebBridge.PickText(gameObject.name, nameof(OnWebProject), ".json,application/json");
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Ordner: " + ProjectsDir, _small);

            GUILayout.Space(8);
            GUILayout.Label("Gespeicherte Varianten", _h2);
            GUILayout.Label("Alles wird zusätzlich alle 30 s als „Letzte Sitzung“ gesichert und beim nächsten Öffnen wiederhergestellt.", _small);
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
