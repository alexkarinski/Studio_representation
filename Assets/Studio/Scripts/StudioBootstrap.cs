using UnityEngine;

namespace Studio
{
    /// <summary>
    /// Точка входа: при запуске любой сцены (если в ней ещё нет студии) строит павильон,
    /// настраивает камеру и редактор. Достаточно открыть проект и нажать Play.
    /// </summary>
    public static class StudioBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<StudioEditor>() != null) return;
            // превью из редактора (меню Studio) заменяем полноценной студией
            foreach (var b in Object.FindObjectsOfType<StudioBuilder>()) Object.Destroy(b.gameObject);
            Create();
        }

        public static StudioBuilder Create()
        {
            // уличный направленный свет из шаблонной сцены в павильоне не нужен
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);

            var root = new GameObject("Studio");
            var builder = root.AddComponent<StudioBuilder>();
            builder.Build();

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            var rig = cam.GetComponent<StudioCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<StudioCameraRig>();

            var editor = root.AddComponent<StudioEditor>();
            editor.builder = builder;
            editor.rig = rig;
            return builder;
        }
    }
}
