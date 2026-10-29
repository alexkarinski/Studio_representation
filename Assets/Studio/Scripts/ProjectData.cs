using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>Сохраняемый вариант оформления (JSON в Application.persistentDataPath/Projects).</summary>
    [Serializable]
    public class ProjectData
    {
        public string name = "Variante 1";
        public List<PropData> props = new List<PropData>();
        public Color wallColor = new Color(0.93f, 0.93f, 0.92f);
        public string wallImage = "";
        public Color floorColor = new Color(0.78f, 0.78f, 0.77f);
        public float lightMaster = 1f;
        public float lightKelvin = 4300f;
        public float ambient = 0.16f;
        public bool[] fixtures = new bool[0];
        public bool showEquipment = true;
        public int liveCamera = 2;
        public bool programOnMonitor = true;
        public bool viewfinderOverlay = true;
        public bool showPaths = true;
        public bool physicalFalloff = true;
        public int shadowQuality = 3;
        public List<FixtureData> fixtureStates = new List<FixtureData>();
        public bool pictureInPicture = true;
    }

    [Serializable]
    public class PropData
    {
        public string type;
        public Vector3 pos;
        public float rotY;
        public Color color;
        public string image = "";
        public bool castShadows = true;
        // nur für Studiokameras
        public int camNumber;
        public float camPan, camTilt, camFocal = 24f, camHeight = 1.3f;
        public List<CamKey> camKeys = new List<CamKey>();
        public bool camEase = true, camLoop;
        // nur für Stativscheinwerfer
        public float lightPan, lightTilt = -20f, lightIntensity = 1.5f;
        // höhenverstellbare Bildschirme / Fernseher
        public float mountHeight = -1f, screenDiagonal = -1f;
        // Beamer
        public float projPan, projTilt = 5f, projHeight = 1.1f, projThrow = 1.4f, projBrightness = 1f;
    }

    /// <summary>Zustand eines Deckenscheinwerfers.</summary>
    [Serializable]
    public class FixtureData
    {
        public int number;
        public bool on = true;
        public float x, z, drop, pan, tilt, dimmer = 1f, focus;
        public float[] doors = new float[4];
        public bool ownKelvin;
        public float kelvin = 4300f;
        public Color gel = Color.white;
        public string gelName = "";
        public bool castShadows = true;
        public int diffusion;
    }
}
