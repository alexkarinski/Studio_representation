using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.EditorTools
{
    public static class StudioMenu
    {
        const string ScenePath = "Assets/Studio/Studio.unity";

        [MenuItem("Studio/Создать сцену студии")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AddAlwaysIncludedShaders();
            Debug.Log("Сцена создана: " + ScenePath + ". Нажмите Play — студия построится автоматически.");
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

        /// <summary>Материалы создаются из кода, поэтому для билда шейдеры нужно включить явно.</summary>
        [MenuItem("Studio/Подготовить проект к сборке")]
        public static void AddAlwaysIncludedShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) { Debug.LogWarning("GraphicsSettings.asset не найден"); return; }
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Standard", "Sprites/Default" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) found = true;
                if (found) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Шейдеры Standard и Sprites/Default добавлены в Always Included Shaders.");
        }
    }
}
