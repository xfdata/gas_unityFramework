using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEditor.InitializeOnLoad]
public class SceneRootEditor
{
    static SceneRootEditor()
    {
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!checkSceneValid(scene)) return;
        SceneRoot sceneRealObjects = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            sceneRealObjects = root.GetComponent<SceneRoot>();
            if (sceneRealObjects != null)
            {
                break;
            }
        }
        if (sceneRealObjects == null)
        {
            sceneRealObjects = new GameObject("sceneRootMgr").AddComponent<SceneRoot>();
            sceneRealObjects.InitializeRealObjects(rootObjects);
        }

        ClearEmptyActiveGameObjects(sceneRealObjects);
        SearchAndCheckLightObjects(sceneRealObjects, rootObjects);
        //if (sceneRealObjects.mainCamera.clearFlags == CameraClearFlags.SolidColor)
        //    sceneRealObjects.mainCamera.backgroundColor = Color.black;
    }

    private static void ClearEmptyActiveGameObjects(SceneRoot sceneRoot)
    {
        foreach (var activeGameObject in sceneRoot.activeGameObjects.ToList())
        {
            if (activeGameObject == null)
            {
                sceneRoot.activeGameObjects.Remove(activeGameObject);
            }
        }
    }

    public static void SearchAndCheckLightObjects(SceneRoot sceneRoot, GameObject[] rootObjects)
    {
        if (sceneRoot.mainCamera == null)
        {
            var cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObj != null)
            {
                sceneRoot.mainCamera = cameraObj.GetComponent<Camera>();
            }
            else
            {
                var camera = GameObject.FindObjectOfType<Camera>();
                if (camera != null)
                {
                    camera.tag = "MainCamera";
                    sceneRoot.mainCamera = camera;
                }
            }
        }
        // 使用HashSet提高查找性能
        HashSet<GameObject> specialObjects = new HashSet<GameObject>();

        foreach (var rootObject in rootObjects)
        {
            // 获取所有带Light组件的子对象
            var lights = rootObject.GetComponentsInChildren<Light>(true);
            foreach (var light in lights)
            {
                if (light.bakingOutput.isBaked) continue;
                specialObjects.Add(light.gameObject);
            }

            // 获取所有带Volume组件的子对象
            var volumes = rootObject.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true);
            foreach (var volume in volumes)
            {
                specialObjects.Add(volume.gameObject);
            }
        }

        // 将符合条件的对象添加到activeGameObjects列表
        // 使用HashSet进行快速查找
        HashSet<GameObject> activeGameObjectSet = new HashSet<GameObject>(sceneRoot.activeGameObjects);

        foreach (var specialObject in specialObjects)
        {
            // 检查该对象是否已在activeGameObjects列表中
            if (activeGameObjectSet.Contains(specialObject))
                continue;

            // 检查该对象是否是activeGameObjects列表中任意对象的子节点
            bool isChildOfActiveObject = false;
            foreach (var activeObj in sceneRoot.activeGameObjects)
            {
                if (activeObj != null && IsChildOf(specialObject.transform, activeObj.transform))
                {
                    isChildOfActiveObject = true;
                    break;
                }
            }

            // 只有当该对象不在列表中且不是列表中任何对象的子节点时，才添加它
            if (!isChildOfActiveObject)
            {
                sceneRoot.activeGameObjects.Add(specialObject);
                activeGameObjectSet.Add(specialObject); // 同步更新HashSet以保持一致性
            }
        }
    }

    // 辅助方法：检查childTransform是否是parentTransform的子节点
    private static bool IsChildOf(Transform childTransform, Transform parentTransform)
    {
        Transform current = childTransform.parent;
        while (current != null)
        {
            if (current == parentTransform)
                return true;
            current = current.parent;
        }
        return false;
    }

    public static bool checkSceneValid(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.name == "Main"
            || scene.name.Contains("Test")
            || scene.name.Contains("Tool")
            || scene.name.Contains("UIDesign")
            || scene.name.Contains("HotUpdate")
            || scene.name.Contains("test")) return false;
        if (scene.path.Contains("Examples")
            || (scene.path.Contains("Tutorial")
            || scene.path.Contains("ThirdParty")
            || scene.path.Contains("Plugins")
            || scene.path.Contains("Editor")
            || scene.path.Contains("Timeline")
            || scene.path.Contains("Demos")
            || scene.path.Contains("Tools")
            || scene.path.Contains("_Scene")
            || scene.path.Contains("ToolScene")
            || scene.path.Contains("Perosonal")
            || scene.path.Contains("Demo")
            || scene.path.Contains("Tests"))) return false;
        return true;
    }


    [MenuItem("Build/SceneAll/场景设置加载后显示对象")]
    public static void SceneAfterloadedShowSet()
    {
        SceneRoot sceneRealObjects = null;
        List<GameObject> _rootObject = new List<GameObject>();
        SceneRoot[] objGos = Resources.FindObjectsOfTypeAll<SceneRoot>();
        if (objGos != null && objGos.Length > 0)
        {
            sceneRealObjects = objGos[0];
        }
        if (sceneRealObjects == null)
            sceneRealObjects = new GameObject("sceneRootMgr").AddComponent<SceneRoot>();
        sceneRealObjects.SetupObjectAfterLoading(sceneRealObjects.gameObject.scene.GetRootGameObjects());
    }

    [MenuItem("Build/SceneAll/场景设置")]
    public static void AllSceneSetting()
    {
        if (EditorUtility.DisplayDialog("", "开启刷新全部场景操作会耗费大量时间，是否继续？", "确定"))
        {
            DirectoryInfo direction = new DirectoryInfo(Application.dataPath);//获取文件夹，exportPath是文件夹的路径
            FileInfo[] files = direction.GetFiles("*.unity", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string mp = files[i].FullName;
                mp = mp.Substring(mp.IndexOf("Assets"));
                mp = mp.Replace('\\', '/');
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mp);
                if (scene != null)
                {
                    if (!checkSceneValid(scene)) continue;
                    SceneRoot sceneRealObjects = null;
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        sceneRealObjects = root.GetComponent<SceneRoot>();
                        if (sceneRealObjects != null)
                        {
                            break;
                        }
                    }
                    if (sceneRealObjects == null)
                    {
                        sceneRealObjects = new GameObject("sceneRootMgr").AddComponent<SceneRoot>();
                        sceneRealObjects.InitializeRealObjects(rootObjects);
                    }
                    ClearEmptyActiveGameObjects(sceneRealObjects);
                    SearchAndCheckLightObjects(sceneRealObjects, rootObjects);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, mp);
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
        else { }
    }

    [MenuItem("Build/SceneAll/重置场景设置,需要自定义过滤器")]
    public static void ResetSceneSetting()
    {

        System.Func<UnityEngine.SceneManagement.Scene, bool> filter = (scene) =>
        {
            if (scene.name.Contains("CityBattle"))
                return true;
            return false;
        };

        DirectoryInfo direction = new DirectoryInfo(Application.dataPath);//获取文件夹，exportPath是文件夹的路径
        FileInfo[] files = direction.GetFiles("*.unity", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string mp = files[i].FullName;
            mp = mp.Substring(mp.IndexOf("Assets"));
            mp = mp.Replace('\\', '/');
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mp);
            if (scene != null)
            {
                if (!checkSceneValid(scene)) continue;
                if (!filter(scene)) continue;
                SceneRoot sceneRealObjects = null;
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    sceneRealObjects = root.GetComponent<SceneRoot>();
                    if (sceneRealObjects != null)
                    {
                        break;
                    }
                }
                if (sceneRealObjects == null)
                {
                    sceneRealObjects = new GameObject("sceneRootMgr").AddComponent<SceneRoot>();
                }
                sceneRealObjects.InitializeRealObjects(rootObjects);
                //if (sceneRealObjects.mainCamera.clearFlags == CameraClearFlags.SolidColor)
                //    sceneRealObjects.mainCamera.backgroundColor = Color.black;
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, mp);
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }
}