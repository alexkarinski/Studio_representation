using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.EditorTools
{
    public static class StudioMenu
    {
        [MenuItem("Studio/Открыть сцену студии")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = BuildScript.EnsureScene();
            EditorSceneManager.OpenScene(path);
            BuildScript.EnsureShaderKeepers();
            Debug.Log("Сцена: " + path + ". Нажмите Play — студия построится автоматически.");
        }

        /// <summary>Предпросмотр геометрии в редакторе без Play (объекты не сохраняются в сцену).</summary>
        [MenuItem("Studio/Построить превью в редакторе")]
        static void BuildPreview()
        {
            var existing = Object.FindObjectOfType<StudioBuilder>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject("Studio (preview)") { hideFlags = HideFlags.DontSave };
            root.AddComponent<StudioBuilder>().Build();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Studio/Убрать превью")]
        static void ClearPreview()
        {
            var existing = Object.FindObjectOfType<StudioBuilder>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
    }
}
