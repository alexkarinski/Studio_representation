using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>Ein Keyframe einer Kamerafahrt: Position der Kamera im Studio und Einstellung des Kopfes.</summary>
    [Serializable]
    public class CamKey
    {
        public float time;
        public Vector3 pos;
        public float rotY, pan, tilt, focal, height;
    }

    /// <summary>
    /// Geplante Kamerafahrt (Dolly, Schwenk, Zoom, Pumpe) zwischen Keyframes.
    /// Position wird per Catmull-Rom-Spline interpoliert, Zoom logarithmisch, Winkel über den kürzesten Weg.
    /// </summary>
    [RequireComponent(typeof(StudioCam))]
    public class CameraMove : MonoBehaviour
    {
        public const float DefaultGap = 3f;

        public List<CamKey> keys = new List<CamKey>();
        public bool ease = true, loop;
        public bool playing;
        public float time;

        StudioCam _cam;
        StudioCam Cam => _cam != null ? _cam : (_cam = GetComponent<StudioCam>());

        public float Duration => keys.Count > 0 ? keys[keys.Count - 1].time : 0f;
        public bool CanPlay => keys.Count >= 2 && Duration > 0.01f;

        public CamKey Capture(float t) => new CamKey
        {
            time = t,
            pos = transform.localPosition,
            rotY = transform.localEulerAngles.y,
            pan = Cam.pan, tilt = Cam.tilt, focal = Cam.focal, height = Cam.height
        };

        /// <summary>Aktuelle Einstellung als neuen Keyframe ans Ende hängen.</summary>
        public void AddKey()
        {
            Stop(false);
            keys.Add(Capture(keys.Count == 0 ? 0f : Duration + DefaultGap));
            time = Duration;
        }

        public void UpdateKey(int i)
        {
            if (i < 0 || i >= keys.Count) return;
            keys[i] = Capture(keys[i].time);
        }

        public void RemoveKey(int i)
        {
            if (i < 0 || i >= keys.Count) return;
            keys.RemoveAt(i);
            if (keys.Count > 0)
            {
                // erster Keyframe beginnt immer bei 0
                float shift = keys[0].time;
                foreach (var k in keys) k.time -= shift;
            }
            time = Mathf.Min(time, Duration);
        }

        /// <summary>Dauer des Abschnitts vor Keyframe i ändern; alle folgenden verschieben sich mit.</summary>
        public void SetSegmentDuration(int i, float seconds)
        {
            if (i <= 0 || i >= keys.Count) return;
            seconds = Mathf.Clamp(seconds, 0.3f, 60f);
            float delta = seconds - (keys[i].time - keys[i - 1].time);
            for (int j = i; j < keys.Count; j++) keys[j].time += delta;
        }

        public void Play()
        {
            if (!CanPlay) return;
            if (time >= Duration - 0.01f) time = 0f;
            playing = true;
        }

        public void Pause() => playing = false;

        public void Stop(bool rewind = true)
        {
            playing = false;
            if (rewind && keys.Count > 0) { time = 0f; ApplyAt(0f); }
        }

        void Update()
        {
            if (!playing) return;
            time += Time.deltaTime;
            if (time >= Duration)
            {
                if (loop) time = 0f;
                else { time = Duration; playing = false; }
            }
            ApplyAt(time);
        }

        public void ApplyAt(float t)
        {
            if (keys.Count == 0) return;
            var k = Evaluate(t);
            transform.localPosition = k.pos;
            transform.localRotation = Quaternion.Euler(0, k.rotY, 0);
            var c = Cam;
            c.pan = k.pan; c.tilt = k.tilt; c.focal = k.focal; c.height = k.height;
            c.Apply();
        }

        public CamKey Evaluate(float t)
        {
            if (keys.Count == 1) return keys[0];
            float dur = Duration;
            t = Mathf.Clamp(t, 0f, dur);
            // weich anfahren und abbremsen über die ganze Fahrt
            if (ease && dur > 0) t = Mathf.SmoothStep(0f, dur, t / dur);

            int i = 0;
            while (i < keys.Count - 2 && t > keys[i + 1].time) i++;
            var a = keys[i]; var b = keys[i + 1];
            var p0 = keys[Mathf.Max(0, i - 1)].pos; var p3 = keys[Mathf.Min(keys.Count - 1, i + 2)].pos;
            float seg = Mathf.Max(0.0001f, b.time - a.time);
            float u = Mathf.Clamp01((t - a.time) / seg);
            float us = Mathf.SmoothStep(0, 1, u) * 0.35f + u * 0.65f; // Kopfbewegung leicht geglättet

            return new CamKey
            {
                time = t,
                pos = CatmullRom(p0, a.pos, b.pos, p3, u),
                rotY = Mathf.LerpAngle(a.rotY, b.rotY, us),
                pan = Mathf.LerpAngle(a.pan, b.pan, us),
                tilt = Mathf.Lerp(a.tilt, b.tilt, us),
                focal = Mathf.Exp(Mathf.Lerp(Mathf.Log(a.focal), Mathf.Log(b.focal), us)),
                height = Mathf.Lerp(a.height, b.height, us)
            };
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Punkte der Fahrt (Höhe des Kamerakopfes) für die Pfadanzeige, in Weltkoordinaten.</summary>
        public void SamplePath(List<Vector3> pts, int perSecond = 8)
        {
            pts.Clear();
            if (keys.Count < 2) return;
            var parent = transform.parent;
            bool e = ease; ease = false; // Pfad unabhängig vom Timing
            int n = Mathf.Clamp(Mathf.CeilToInt(Duration * perSecond), 8, 400);
            for (int i = 0; i <= n; i++)
            {
                var k = Evaluate(Duration * i / n);
                var local = k.pos + Vector3.up * k.height;
                pts.Add(parent != null ? parent.TransformPoint(local) : local);
            }
            ease = e;
        }
    }
}
