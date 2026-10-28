using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Рантайм-редактор оформления для показа клиенту: расстановка мебели и декора,
    /// цвета, картинка на белую стену/экраны, свет, ракурсы, скриншоты, сохранение вариантов.
    /// Интерфейс на IMGUI — без зависимостей от пакетов.
    /// </summary>
    public class StudioEditor : MonoBehaviour
    {
        public StudioBuilder builder;
        public StudioCameraRig rig;

        StudioProp _selected;
        bool _dragging, _pendingDeselect, _hideUI, _showHelp = true;
        Vector3 _dragOffset, _downMouse;
        GameObject _selRing;
        Material _selRingMat;

        enum Tab { View, Objects, Light, Project }
        Tab _tab = Tab.View;
        Vector2 _scroll;
        Rect _panelRect;
        string _status = "";
        float _statusTime;

        // состояние сцены
        ProjectData _state = new ProjectData();
        string _wallImagePath = "", _propImagePath = "", _projectName = "Вариант 1";
        bool[] _fixtureOn;

        static readonly (string name, Color c)[] Palette =
        {
            ("Белый", new Color(0.95f, 0.95f, 0.94f)), ("Светло-серый", new Color(0.75f, 0.75f, 0.74f)),
            ("Серый", new Color(0.5f, 0.5f, 0.5f)), ("Графит", new Color(0.2f, 0.21f, 0.22f)),
            ("Чёрный", new Color(0.04f, 0.04f, 0.045f)), ("Бежевый", new Color(0.86f, 0.80f, 0.70f)),
            ("Песок", new Color(0.80f, 0.68f, 0.50f)), ("Дуб", new Color(0.72f, 0.52f, 0.30f)),
            ("Орех", new Color(0.42f, 0.28f, 0.17f)), ("Терракота", new Color(0.72f, 0.36f, 0.25f)),
            ("Оранжевый", new Color(0.95f, 0.50f, 0.12f)), ("Горчица", new Color(0.85f, 0.67f, 0.18f)),
            ("Оливка", new Color(0.45f, 0.48f, 0.25f)), ("Зелёный", new Color(0.20f, 0.50f, 0.30f)),
            ("Бирюза", new Color(0.12f, 0.60f, 0.62f)), ("Синий", new Color(0.15f, 0.35f, 0.75f)),
            ("Тёмно-синий", new Color(0.08f, 0.12f, 0.28f)), ("Фиолетовый", new Color(0.42f, 0.25f, 0.62f)),
            ("Розовый", new Color(0.95f, 0.55f, 0.65f)), ("Красный", new Color(0.78f, 0.10f, 0.12f)),
            ("Хромакей зелёный", new Color(0.0f, 0.69f, 0.25f)), ("Хромакей синий", new Color(0.0f, 0.28f, 0.73f)),
        };

        static readonly HashSet<string> EquipmentTypes = new HashSet<string> { "studio_cam", "pedestal_cam", "tv_stand" };

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
        }

        // ================= Ввод в 3D =================

        bool _clickConsumed;

        bool PointerOverUI()
        {
            if (_hideUI) return false;
            var m = Input.mousePosition;
            var gui = new Vector2(m.x, Screen.height - m.y);
            return _panelRect.Contains(gui) || HelpRect.Contains(gui) || GUIUtility.hotControl != 0;
        }

        void Update()
        {
            _clickConsumed = false;
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
                        p.x = Mathf.Clamp(p.x, -StudioBuilder.HalfW + 0.3f, StudioBuilder.HalfW - 0.3f);
                        p.z = Mathf.Clamp(p.z, -StudioBuilder.HalfD + 0.3f, StudioBuilder.HalfD - 0.3f);
                        p.y = 0;
                        _selected.transform.position = p;
                    }
                }
                else _dragging = false;
            }

            if (Input.GetMouseButtonUp(0) && _pendingDeselect)
            {
                _pendingDeselect = false;
                if ((Input.mousePosition - _downMouse).sqrMagnitude < 16f) Select(null);
            }

            if (_selected != null && GUIUtility.keyboardControl == 0)
            {
                float step = IsShift() ? 5f : 15f;
                if (Input.GetKeyDown(KeyCode.Q)) _selected.transform.Rotate(0, -step, 0, Space.World);
                if (Input.GetKeyDown(KeyCode.E)) _selected.transform.Rotate(0, step, 0, Space.World);
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) DeleteSelected();
                if (Input.GetKeyDown(KeyCode.Escape)) Select(null);
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand)) && Input.GetKeyDown(KeyCode.D)) Duplicate();
            }
            if (GUIUtility.keyboardControl == 0)
            {
                for (int i = 0; i < StudioCameraRig.Presets.Length && i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) rig.Apply(StudioCameraRig.Presets[i]);
                if (Input.GetKeyDown(KeyCode.H)) _hideUI = !_hideUI;
                if (Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.P)) StartCoroutine(Screenshot());
            }

            UpdateSelectionRing();
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
            if (_selected == null || !_selected.gameObject.activeInHierarchy) { _selRing.SetActive(false); return; }
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
            Destroy(_selected.gameObject);
            Select(null);
        }

        void Duplicate()
        {
            if (_selected == null) return;
            var s = _selected;
            var np = PropFactory.Create(s.typeId, builder.set, s.transform.localPosition + new Vector3(0.6f, 0, 0.6f), s.transform.localEulerAngles.y, s.color);
            if (np != null && !string.IsNullOrEmpty(s.imagePath)) np.SetImage(s.imagePath);
            Select(np);
        }

        void Spawn(string id)
        {
            // ставим на пол перед камерой
            var cam = rig.Cam.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var pos = cam.position + fwd * 4f;
            pos.y = 0;
            pos.x = Mathf.Clamp(pos.x, -StudioBuilder.HalfW + 1, StudioBuilder.HalfW - 1);
            pos.z = Mathf.Clamp(pos.z, -StudioBuilder.HalfD + 1, StudioBuilder.HalfD - 1);
            float rotY = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg; // лицом к камере
            var p = PropFactory.Create(id, builder.set, pos, rotY);
            if (p != null && EquipmentTypes.Contains(id)) p.gameObject.SetActive(_state.showEquipment);
            Select(p);
            Status("Добавлено: " + p?.displayName);
        }

        IEnumerable<StudioProp> AllProps() => builder.set.GetComponentsInChildren<StudioProp>(true);

        // ================= Свет / окружение =================

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
            if (tex == null) { Status("Файл не найден или не картинка: " + path); return; }
            _state.wallImage = path;
            builder.SetWhiteWall(_state.wallColor, tex);
            Status("Картинка на белой стене: " + Path.GetFileName(path));
        }

        void SetEquipmentVisible(bool v)
        {
            _state.showEquipment = v;
            foreach (var p in AllProps())
                if (EquipmentTypes.Contains(p.typeId)) p.gameObject.SetActive(v);
            if (!v && _selected != null && EquipmentTypes.Contains(_selected.typeId)) Select(null);
        }

        // ================= Проекты =================

        ProjectData Capture()
        {
            _state.name = _projectName;
            _state.fixtures = _fixtureOn;
            _state.props = AllProps().Select(p => new PropData
            {
                type = p.typeId,
                pos = p.transform.localPosition,
                rotY = p.transform.localEulerAngles.y,
                color = p.color,
                image = p.imagePath ?? ""
            }).ToList();
            return _state;
        }

        void Save()
        {
            Directory.CreateDirectory(ProjectsDir);
            var file = Path.Combine(ProjectsDir, SafeName(_projectName) + ".json");
            File.WriteAllText(file, JsonUtility.ToJson(Capture(), true));
            Status("Сохранено: " + file);
        }

        void Load(string file)
        {
            var data = JsonUtility.FromJson<ProjectData>(File.ReadAllText(file));
            if (data == null) { Status("Не удалось прочитать " + file); return; }
            foreach (var p in AllProps().ToList()) DestroyImmediate(p.gameObject);
            Select(null);
            foreach (var d in data.props)
            {
                var p = PropFactory.Create(d.type, builder.set, d.pos, d.rotY, d.color);
                if (p != null && !string.IsNullOrEmpty(d.image)) p.SetImage(d.image);
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
            ApplyLight();
            Status("Загружено: " + data.name);
        }

        void ResetToPhotos()
        {
            builder.Build();
            _state = new ProjectData { name = _projectName };
            _fixtureOn = Enumerable.Repeat(true, builder.fixtureLights.Count).ToArray();
            _wallImagePath = "";
            Select(null);
            ApplyLight();
            Status("Студия восстановлена как на фото");
        }

        static string SafeName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "project" : s;
        }

        IEnumerator Screenshot()
        {
            bool prev = _hideUI;
            _hideUI = true;
            _selRing.SetActive(false);
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(ShotsDir);
            var file = Path.Combine(ShotsDir, SafeName(_projectName) + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            _hideUI = prev;
            Status("Скриншот: " + file);
        }

        void Status(string s) { _status = s; _statusTime = Time.unscaledTime; Debug.Log(s); }

        // ================= UI =================

        GUIStyle _box, _h1, _h2, _small, _swatch;
        Texture2D _bgTex, _white;

        void InitStyles()
        {
            if (_box != null) return;
            _bgTex = new Texture2D(1, 1); _bgTex.SetPixel(0, 0, new Color(0.08f, 0.085f, 0.095f, 0.92f)); _bgTex.Apply();
            _white = Texture2D.whiteTexture;
            _box = new GUIStyle(GUI.skin.box) { normal = { background = _bgTex }, padding = new RectOffset(10, 10, 10, 10) };
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.85f, 0.6f) } };
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.85f, 0.95f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.75f, 0.75f, 0.78f) } };
            _swatch = new GUIStyle(GUI.skin.button) { margin = new RectOffset(2, 2, 2, 2) };
        }

        Rect HelpRect => _showHelp ? new Rect(Screen.width - 300, Screen.height - 190, 290, 180) : new Rect(Screen.width - 120, Screen.height - 34, 110, 26);

        void OnGUI()
        {
            if (_hideUI) { _panelRect = Rect.zero; return; }
            InitStyles();

            float w = 340;
            _panelRect = new Rect(8, 8, w, Screen.height - 16);
            GUILayout.BeginArea(_panelRect, _box);
            GUILayout.Label("Студия — визуализация оформления", _h1);
            GUILayout.Space(4);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Вид", "Объекты", "Свет", "Проект" });
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

        void DrawViewTab()
        {
            GUILayout.Label("Ракурсы (клавиши 1–7)", _h2);
            for (int i = 0; i < StudioCameraRig.Presets.Length; i++)
                if (GUILayout.Button((i + 1) + ". " + StudioCameraRig.Presets[i].name)) rig.Apply(StudioCameraRig.Presets[i]);
            GUILayout.Label("Угол обзора: " + rig.Cam.fieldOfView.ToString("0") + "°", _small);
            rig.Cam.fieldOfView = GUILayout.HorizontalSlider(rig.Cam.fieldOfView, 20f, 90f);
            GUILayout.Space(8);
            if (GUILayout.Button("Скриншот для клиента (P)", GUILayout.Height(28))) StartCoroutine(Screenshot());
            GUILayout.Label("Сохраняется в " + ShotsDir, _small);

            GUILayout.Space(10);
            GUILayout.Label("Белая стена (фон)", _h2);
            DrawPalette(SetWallColor);
            GUILayout.Label("Картинка/брендинг на стену — путь к PNG/JPG:", _small);
            DrawImagePicker(ref _wallImagePath, SetWallImage);

            GUILayout.Space(10);
            GUILayout.Label("Пол", _h2);
            DrawPalette(SetFloorColor);

            GUILayout.Space(10);
            bool eq = GUILayout.Toggle(_state.showEquipment, " Показывать технику (камеры, монитор)");
            if (eq != _state.showEquipment) SetEquipmentVisible(eq);
            bool rigOn = builder.rig.gameObject.activeSelf;
            bool r2 = GUILayout.Toggle(rigOn, " Показывать подвесной риг");
            if (r2 != rigOn) builder.rig.gameObject.SetActive(r2);
        }

        void DrawObjectsTab()
        {
            if (_selected != null) DrawSelected();
            else GUILayout.Label("Кликните объект в сцене, чтобы выбрать. Перетаскивайте мышью, Q/E — поворот, Del — удалить, Ctrl+D — копия, Ctrl при перетаскивании — шаг 25 см.", _small);

            GUILayout.Space(10);
            GUILayout.Label("Добавить", _h2);
            foreach (var group in PropFactory.Catalog.GroupBy(e => e.category))
            {
                GUILayout.Label(group.Key, _small);
                var items = group.ToList();
                for (int i = 0; i < items.Count; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 2, items.Count); j++)
                        if (GUILayout.Button(items[j].name, GUILayout.Width(150))) Spawn(items[j].id);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("В сцене", _h2);
            foreach (var p in AllProps().ToList())
            {
                GUILayout.BeginHorizontal();
                var label = (p == _selected ? "▶ " : "") + p.displayName + (p.gameObject.activeSelf ? "" : " (скрыто)");
                if (GUILayout.Button(label, GUI.skin.label)) Select(p);
                GUILayout.EndHorizontal();
            }
        }

        void DrawSelected()
        {
            var t = _selected.transform;
            GUILayout.Label("Выбрано: " + _selected.displayName, _h2);
            var pos = t.localPosition;
            GUILayout.Label($"X: {pos.x:0.00} м", _small);
            pos.x = GUILayout.HorizontalSlider(pos.x, -StudioBuilder.HalfW + 0.3f, StudioBuilder.HalfW - 0.3f);
            GUILayout.Label($"Z: {pos.z:0.00} м", _small);
            pos.z = GUILayout.HorizontalSlider(pos.z, -StudioBuilder.HalfD + 0.3f, StudioBuilder.HalfD - 0.3f);
            t.localPosition = pos;
            float rot = t.localEulerAngles.y;
            GUILayout.Label($"Поворот: {rot:0}°", _small);
            float nr = GUILayout.HorizontalSlider(rot, 0f, 360f);
            if (Mathf.Abs(nr - rot) > 0.01f) t.localRotation = Quaternion.Euler(0, nr, 0);

            if (_selected.HasColor)
            {
                GUILayout.Label("Цвет", _small);
                DrawPalette(c => _selected.SetColor(c));
                var c0 = _selected.color;
                GUILayout.BeginHorizontal();
                float r = GUILayout.HorizontalSlider(c0.r, 0, 1), g = GUILayout.HorizontalSlider(c0.g, 0, 1), b = GUILayout.HorizontalSlider(c0.b, 0, 1);
                GUILayout.EndHorizontal();
                if (r != c0.r || g != c0.g || b != c0.b) _selected.SetColor(new Color(r, g, b));
            }
            if (_selected.HasImage)
            {
                GUILayout.Label("Картинка на экран/панель — путь к PNG/JPG:", _small);
                var prop = _selected;
                DrawImagePicker(ref _propImagePath, path =>
                {
                    if (prop.SetImage(path)) Status(string.IsNullOrEmpty(path) ? "Картинка снята" : "Картинка: " + Path.GetFileName(path));
                    else Status("Файл не найден или не картинка: " + path);
                });
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копия")) Duplicate();
            if (GUILayout.Button("Удалить")) DeleteSelected();
            if (GUILayout.Button("Снять выбор")) Select(null);
            GUILayout.EndHorizontal();
        }

        void DrawLightTab()
        {
            GUILayout.Label("Общая яркость приборов: " + _state.lightMaster.ToString("0.00"), _small);
            float m = GUILayout.HorizontalSlider(_state.lightMaster, 0f, 2.5f);
            GUILayout.Label("Цветовая температура: " + _state.lightKelvin.ToString("0") + " K", _small);
            float k = GUILayout.HorizontalSlider(_state.lightKelvin, 2700f, 6500f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3200 K (галоген)")) k = 3200;
            if (GUILayout.Button("4300 K")) k = 4300;
            if (GUILayout.Button("5600 K (день)")) k = 5600;
            GUILayout.EndHorizontal();
            GUILayout.Label("Заполняющий (общий) свет зала: " + _state.ambient.ToString("0.00"), _small);
            float a = GUILayout.HorizontalSlider(_state.ambient, 0f, 0.6f);
            if (m != _state.lightMaster || k != _state.lightKelvin || a != _state.ambient)
            {
                _state.lightMaster = m; _state.lightKelvin = k; _state.ambient = a;
                ApplyLight();
            }

            GUILayout.Space(8);
            GUILayout.Label("Приборы на пантографах", _h2);
            bool changed = false;
            for (int i = 0; i < builder.fixtureLights.Count; i++)
            {
                var l = builder.fixtureLights[i];
                if (l == null) continue;
                string fixtureName = l.transform.parent.parent.name.Replace("Fixture_", "Прибор №") + (l.spotAngle > 60 ? " (софт)" : " (френель)");
                bool on = GUILayout.Toggle(_fixtureOn[i], " " + fixtureName);
                if (on != _fixtureOn[i]) { _fixtureOn[i] = on; changed = true; }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Все вкл")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = true; changed = true; }
            if (GUILayout.Button("Все выкл")) { for (int i = 0; i < _fixtureOn.Length; i++) _fixtureOn[i] = false; changed = true; }
            GUILayout.EndHorizontal();
            if (changed) ApplyLight();
        }

        void DrawProjectTab()
        {
            GUILayout.Label("Название варианта", _h2);
            _projectName = GUILayout.TextField(_projectName);
            if (GUILayout.Button("Сохранить вариант", GUILayout.Height(26))) Save();
            GUILayout.Label("Папка: " + ProjectsDir, _small);

            GUILayout.Space(8);
            GUILayout.Label("Сохранённые варианты", _h2);
            if (Directory.Exists(ProjectsDir))
                foreach (var f in Directory.GetFiles(ProjectsDir, "*.json").OrderBy(f => f))
                    if (GUILayout.Button("Открыть: " + Path.GetFileNameWithoutExtension(f))) Load(f);

            GUILayout.Space(12);
            if (GUILayout.Button("Сбросить к расстановке с фото")) ResetToPhotos();
            if (GUILayout.Button("Убрать весь декор и мебель"))
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
                    if (GUILayout.Button(content, _swatch, GUILayout.Width(32), GUILayout.Height(22))) onPick(Palette[j].c);
                }
                GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = prev;
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label(GUI.tooltip, _small);
        }

        void DrawImagePicker(ref string path, System.Action<string> apply)
        {
            path = GUILayout.TextField(path ?? "");
            GUILayout.BeginHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button("Выбрать…"))
            {
                var p = UnityEditor.EditorUtility.OpenFilePanel("Картинка", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(p)) { path = p; apply(p); }
            }
#endif
            if (GUILayout.Button("Применить")) apply(path.Trim().Trim('"'));
            if (GUILayout.Button("Убрать")) { path = ""; apply(""); }
            GUILayout.EndHorizontal();
        }

        void DrawHelp()
        {
            var r = HelpRect;
            if (!_showHelp)
            {
                if (GUI.Button(r, "Управление")) _showHelp = true;
                return;
            }
            GUILayout.BeginArea(r, _box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Управление", _h2);
            if (GUILayout.Button("×", GUILayout.Width(22))) _showHelp = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "ЛКМ/ПКМ по пустому — вращать камеру\n" +
                "Shift+ЛКМ / СКМ — сдвиг, колесо — зум\n" +
                "WASD — ходить, PgUp/PgDn — выше/ниже\n" +
                "ЛКМ по объекту — выбрать и тащить\n" +
                "Q/E — поворот, Del — удалить, Ctrl+D — копия\n" +
                "1–7 — ракурсы, P — скриншот, H — скрыть UI", _small);
            GUILayout.EndArea();
        }
    }
}
