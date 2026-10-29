using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Erzeugt Vorschaubilder für das Lager (Katalog): jedes Objekt wird einmal unsichtbar
    /// auf einer Bühne unter dem Studio aufgebaut, fotografiert und wieder entfernt — ein Objekt pro Frame.
    /// </summary>
    public class IconRenderer : MonoBehaviour
    {
        public const int Layer = 31;
        const int Size = 160;
        static readonly Vector3 Stage = new Vector3(0, -200, 0);

        readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        readonly List<string> _queue = new List<string>();
        Camera _cam;
        RenderTexture _rt;

        public Texture2D Get(string id)
        {
            if (_icons.TryGetValue(id, out var t)) return t;
            if (!_queue.Contains(id)) _queue.Add(id);
            return null;
        }

        public void Forget(string id) { _icons.Remove(id); }

        void Setup()
        {
            _rt = new RenderTexture(Size, Size, 24) { name = "Icons" };
            _cam = new GameObject("IconCamera").AddComponent<Camera>();
            _cam.transform.SetParent(transform, false);
            _cam.enabled = false;
            _cam.targetTexture = _rt;
            _cam.cullingMask = 1 << Layer;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
            _cam.renderingPath = RenderingPath.Forward;
            _cam.allowHDR = false;
            _cam.fieldOfView = 28f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 60f;
            AddLight("IconKey", new Vector3(35, -35, 0), 1.1f);
            AddLight("IconFill", new Vector3(20, 150, 0), 0.45f);
        }

        void AddLight(string name, Vector3 euler, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.rotation = Quaternion.Euler(euler);
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.cullingMask = 1 << Layer; // beleuchtet nur die Vorschau-Bühne
            l.shadows = LightShadows.None;
        }

        void LateUpdate()
        {
            if (_queue.Count == 0) return;
            if (_cam == null) Setup();
            var id = _queue[0];
            _queue.RemoveAt(0);
            _icons[id] = RenderIcon(id);
        }

        Texture2D RenderIcon(string id)
        {
            var stage = new GameObject("IconStage");
            stage.transform.position = Stage;
            try
            {
                var prop = PropFactory.Create(id, stage.transform, Vector3.zero, -28f);
                if (prop == null) return null;
                foreach (var t in prop.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var l in prop.GetComponentsInChildren<Light>(true)) l.enabled = false;

                var rends = prop.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return null;
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float radius = Mathf.Max(0.15f, b.extents.magnitude);
                float dist = radius / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                var dir = new Vector3(0f, 0.45f, 1f).normalized; // leicht von oben, von vorn
                _cam.transform.position = b.center + dir * dist;
                _cam.transform.rotation = Quaternion.LookRotation(b.center - _cam.transform.position, Vector3.up);
                _cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false) { name = "Icon_" + id };
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Vorschau für " + id + ": " + e.Message);
                return null;
            }
            finally
            {
                DestroyImmediate(stage);
            }
        }
    }
}
