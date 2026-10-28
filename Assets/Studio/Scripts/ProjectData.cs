using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio
{
    /// <summary>Сохраняемый вариант оформления (JSON в Application.persistentDataPath/Projects).</summary>
    [Serializable]
    public class ProjectData
    {
        public string name = "Вариант 1";
        public List<PropData> props = new List<PropData>();
        public Color wallColor = new Color(0.93f, 0.93f, 0.92f);
        public string wallImage = "";
        public Color floorColor = new Color(0.78f, 0.78f, 0.77f);
        public float lightMaster = 1f;
        public float lightKelvin = 4300f;
        public float ambient = 0.16f;
        public bool[] fixtures = new bool[0];
        public bool showEquipment = true;
    }

    [Serializable]
    public class PropData
    {
        public string type;
        public Vector3 pos;
        public float rotY;
        public Color color;
        public string image = "";
    }
}
