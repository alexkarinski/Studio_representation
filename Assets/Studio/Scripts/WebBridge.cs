using System.Runtime.InteropServices;
using UnityEngine;

namespace Studio
{
    /// <summary>Вызовы браузера для WebGL-сборки (Assets/Plugins/WebGL/StudioWeb.jslib).</summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void StudioPickImage(string goName, string method, string accept);
        [DllImport("__Internal")] static extern void StudioPickText(string goName, string method, string accept);
        [DllImport("__Internal")] static extern void StudioDownload(string name, byte[] data, int len, string mime);
        [DllImport("__Internal")] static extern void StudioSyncFS();
        public static bool IsWeb => true;
#else
        static void StudioPickImage(string goName, string method, string accept) { }
        static void StudioPickText(string goName, string method, string accept) { }
        static void StudioDownload(string name, byte[] data, int len, string mime) { }
        static void StudioSyncFS() { }
        public static bool IsWeb => false;
#endif

        /// <summary>Открывает выбор файла; результат придёт в go.method("имя|base64").</summary>
        public static void PickImage(string goName, string method) => StudioPickImage(goName, method, "image/png,image/jpeg");

        /// <summary>Beliebige Datei wählen (accept z. B. ".glb,.png"); Ergebnis wie bei PickImage: "name|base64".</summary>
        public static void PickFile(string goName, string method, string accept) => StudioPickImage(goName, method, accept);

        /// <summary>Textdatei wählen (z. B. .json); Inhalt kommt in go.method(text).</summary>
        public static void PickText(string goName, string method, string accept) => StudioPickText(goName, method, accept);

        public static void Download(string fileName, byte[] data, string mime) => StudioDownload(fileName, data, data.Length, mime);

        /// <summary>Сбрасывает persistentDataPath в IndexedDB, чтобы сохранения пережили перезагрузку страницы.</summary>
        public static void SyncFS() => StudioSyncFS();
    }
}
