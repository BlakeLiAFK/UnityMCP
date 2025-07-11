using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景信息工具 - 获取场景详细信息
/// </summary>
public class SceneInfoTool : IMCPTool
{
    public string ToolName => "scene_get_info";
    
    public string Description => "获取场景详细信息（对象统计、组件分析等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            string scenePath = parameters.ContainsKey("scenePath") ? parameters["scenePath"].ToString() : "";
            bool includeObjects = parameters.ContainsKey("includeObjects") ? 
                System.Convert.ToBoolean(parameters["includeObjects"]) : false;
            bool includeComponents = parameters.ContainsKey("includeComponents") ? 
                System.Convert.ToBoolean(parameters["includeComponents"]) : false;
            bool analyzePerformance = parameters.ContainsKey("analyzePerformance") ? 
                System.Convert.ToBoolean(parameters["analyzePerformance"]) : false;
            
            Scene targetScene;
            
            // 确定目标场景
            if (!string.IsNullOrEmpty(scenePath))
            {
                // 检查指定场景是否已加载
                targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.IsValid())
                {
                    return MCPResponse.Error($"场景未加载或不存在: {scenePath}");
                }
            }
            else
            {
                // 使用当前活动场景
                targetScene = SceneManager.GetActiveScene();
                if (!targetScene.IsValid())
                {
                    return MCPResponse.Error("没有有效的活动场景");
                }
            }
            
            var result = new Dictionary<string, object>
            {
                ["includeObjects"] = includeObjects,
                ["includeComponents"] = includeComponents,
                ["analyzePerformance"] = analyzePerformance,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 基本场景信息
            result["sceneInfo"] = GetBasicSceneInfo(targetScene);
            
            // 对象统计
            result["objectStatistics"] = GetObjectStatistics(targetScene);
            
            // 组件分析
            if (includeComponents)
            {
                result["componentAnalysis"] = GetComponentAnalysis(targetScene);
            }
            
            // 对象列表
            if (includeObjects)
            {
                result["gameObjects"] = GetGameObjectList(targetScene);
            }
            
            // 性能分析
            if (analyzePerformance)
            {
                result["performanceAnalysis"] = GetPerformanceAnalysis(targetScene);
            }
            
            // 场景依赖
            result["dependencies"] = GetSceneDependencies(targetScene);
            
            // 构建设置信息
            result["buildSettings"] = GetBuildSettingsInfo(targetScene);
            
            Debug.Log($"成功获取场景信息: {targetScene.name}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取场景信息时出错: {e.Message}");
            return MCPResponse.Error($"获取场景信息失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取基本场景信息
    /// </summary>
    private Dictionary<string, object> GetBasicSceneInfo(Scene scene)
    {
        var info = new Dictionary<string, object>
        {
            ["name"] = scene.name,
            ["path"] = scene.path,
            ["isLoaded"] = scene.isLoaded,
            ["isDirty"] = scene.isDirty,
            ["isValid"] = scene.IsValid(),
            ["buildIndex"] = scene.buildIndex,
            ["handle"] = scene.handle
        };
        
        // 文件信息
        if (!string.IsNullOrEmpty(scene.path) && System.IO.File.Exists(scene.path))
        {
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(scene.path);
            info["fileSize"] = fileInfo.Length;
            info["fileSizeFormatted"] = FormatFileSize(fileInfo.Length);
            info["lastModified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            info["created"] = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        // GUID信息
        if (!string.IsNullOrEmpty(scene.path))
        {
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            info["guid"] = guid;
        }
        
        return info;
    }
    
    /// <summary>
    /// 获取对象统计信息
    /// </summary>
    private Dictionary<string, object> GetObjectStatistics(Scene scene)
    {
        if (!scene.isLoaded)
        {
            return new Dictionary<string, object> { ["error"] = "场景未加载" };
        }
        
        GameObject[] rootObjects = scene.GetRootGameObjects();
        int totalObjects = 0;
        int activeObjects = 0;
        int inactiveObjects = 0;
        
        // 统计所有对象
        foreach (GameObject rootObj in rootObjects)
        {
            CountObjectsRecursive(rootObj, ref totalObjects, ref activeObjects, ref inactiveObjects);
        }
        
        var stats = new Dictionary<string, object>
        {
            ["rootObjectCount"] = rootObjects.Length,
            ["totalObjectCount"] = totalObjects,
            ["activeObjectCount"] = activeObjects,
            ["inactiveObjectCount"] = inactiveObjects
        };
        
        // 根对象信息
        var rootObjectInfo = new List<Dictionary<string, object>>();
        foreach (GameObject rootObj in rootObjects)
        {
            int childCount = CountChildrenRecursive(rootObj.transform);
            rootObjectInfo.Add(new Dictionary<string, object>
            {
                ["name"] = rootObj.name,
                ["instanceId"] = rootObj.GetInstanceID(),
                ["active"] = rootObj.activeInHierarchy,
                ["childCount"] = childCount,
                ["componentCount"] = rootObj.GetComponents<Component>().Length,
                ["tag"] = rootObj.tag,
                ["layer"] = rootObj.layer,
                ["layerName"] = LayerMask.LayerToName(rootObj.layer)
            });
        }
        stats["rootObjects"] = rootObjectInfo;
        
        return stats;
    }
    
    /// <summary>
    /// 获取组件分析
    /// </summary>
    private Dictionary<string, object> GetComponentAnalysis(Scene scene)
    {
        if (!scene.isLoaded)
        {
            return new Dictionary<string, object> { ["error"] = "场景未加载" };
        }
        
        var componentCounts = new Dictionary<string, int>();
        var componentStats = new Dictionary<string, object>();
        
        GameObject[] allObjects = scene.GetRootGameObjects();
        foreach (GameObject rootObj in allObjects)
        {
            AnalyzeComponentsRecursive(rootObj, componentCounts);
        }
        
        componentStats["totalComponentTypes"] = componentCounts.Count;
        componentStats["componentTypes"] = componentCounts.OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        
        // 特殊组件统计
        componentStats["rendererCount"] = componentCounts.ContainsKey("Renderer") ? componentCounts["Renderer"] : 0;
        componentStats["colliderCount"] = GetColliderCount(componentCounts);
        componentStats["lightCount"] = componentCounts.ContainsKey("Light") ? componentCounts["Light"] : 0;
        componentStats["cameraCount"] = componentCounts.ContainsKey("Camera") ? componentCounts["Camera"] : 0;
        componentStats["uiElementCount"] = GetUIElementCount(componentCounts);
        
        return componentStats;
    }
    
    /// <summary>
    /// 获取GameObject列表
    /// </summary>
    private List<Dictionary<string, object>> GetGameObjectList(Scene scene)
    {
        var objectList = new List<Dictionary<string, object>>();
        
        if (!scene.isLoaded)
        {
            return objectList;
        }
        
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject rootObj in rootObjects)
        {
            AddGameObjectToListRecursive(rootObj, objectList, 0);
        }
        
        return objectList;
    }
    
    /// <summary>
    /// 获取性能分析
    /// </summary>
    private Dictionary<string, object> GetPerformanceAnalysis(Scene scene)
    {
        if (!scene.isLoaded)
        {
            return new Dictionary<string, object> { ["error"] = "场景未加载" };
        }
        
        var analysis = new Dictionary<string, object>();
        
        // 渲染统计
        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        int totalVertices = 0;
        int totalTriangles = 0;
        var materials = new HashSet<Material>();
        
        foreach (Renderer renderer in renderers)
        {
            if (renderer.gameObject.scene == scene)
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter && meshFilter.sharedMesh)
                {
                    totalVertices += meshFilter.sharedMesh.vertexCount;
                    totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                }
                
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                        materials.Add(mat);
                }
            }
        }
        
        analysis["renderStatistics"] = new Dictionary<string, object>
        {
            ["rendererCount"] = renderers.Length,
            ["totalVertices"] = totalVertices,
            ["totalTriangles"] = totalTriangles,
            ["uniqueMaterialCount"] = materials.Count
        };
        
        // 光照统计
        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        analysis["lightingStatistics"] = new Dictionary<string, object>
        {
            ["lightCount"] = lights.Length,
            ["realtimeLights"] = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Realtime),
            ["bakedLights"] = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Baked),
            ["mixedLights"] = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Mixed)
        };
        
        // 碰撞体统计
        Collider[] colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
        analysis["physicsStatistics"] = new Dictionary<string, object>
        {
            ["colliderCount"] = colliders.Length,
            ["triggerCount"] = colliders.Count(c => c.isTrigger),
            ["staticColliders"] = colliders.Count(c => !c.attachedRigidbody),
            ["dynamicColliders"] = colliders.Count(c => c.attachedRigidbody)
        };
        
        return analysis;
    }
    
    /// <summary>
    /// 获取场景依赖
    /// </summary>
    private Dictionary<string, object> GetSceneDependencies(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path))
        {
            return new Dictionary<string, object> { ["error"] = "场景路径为空" };
        }
        
        string[] dependencies = AssetDatabase.GetDependencies(scene.path, false);
        
        var depInfo = new Dictionary<string, object>
        {
            ["totalCount"] = dependencies.Length - 1 // 减去场景自身
        };
        
        var depList = new List<Dictionary<string, object>>();
        foreach (string dep in dependencies)
        {
            if (dep != scene.path) // 排除自身
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(dep);
                depList.Add(new Dictionary<string, object>
                {
                    ["path"] = dep,
                    ["name"] = asset?.name ?? System.IO.Path.GetFileNameWithoutExtension(dep),
                    ["type"] = asset?.GetType().Name ?? "Unknown"
                });
            }
        }
        
        depInfo["dependencies"] = depList;
        return depInfo;
    }
    
    /// <summary>
    /// 获取构建设置信息
    /// </summary>
    private Dictionary<string, object> GetBuildSettingsInfo(Scene scene)
    {
        var buildInfo = new Dictionary<string, object>
        {
            ["buildIndex"] = scene.buildIndex,
            ["isInBuildSettings"] = scene.buildIndex >= 0
        };
        
        if (scene.buildIndex >= 0)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scene.buildIndex < scenes.Length)
            {
                EditorBuildSettingsScene buildScene = scenes[scene.buildIndex];
                buildInfo["enabled"] = buildScene.enabled;
                buildInfo["guid"] = buildScene.guid.ToString();
            }
        }
        
        return buildInfo;
    }
    
    // 辅助方法
    private void CountObjectsRecursive(GameObject obj, ref int total, ref int active, ref int inactive)
    {
        total++;
        if (obj.activeInHierarchy)
            active++;
        else
            inactive++;
        
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            CountObjectsRecursive(obj.transform.GetChild(i).gameObject, ref total, ref active, ref inactive);
        }
    }
    
    private int CountChildrenRecursive(Transform parent)
    {
        int count = parent.childCount;
        for (int i = 0; i < parent.childCount; i++)
        {
            count += CountChildrenRecursive(parent.GetChild(i));
        }
        return count;
    }
    
    private void AnalyzeComponentsRecursive(GameObject obj, Dictionary<string, int> componentCounts)
    {
        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp != null)
            {
                string typeName = comp.GetType().Name;
                componentCounts[typeName] = componentCounts.ContainsKey(typeName) ? componentCounts[typeName] + 1 : 1;
            }
        }
        
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            AnalyzeComponentsRecursive(obj.transform.GetChild(i).gameObject, componentCounts);
        }
    }
    
    private void AddGameObjectToListRecursive(GameObject obj, List<Dictionary<string, object>> list, int depth)
    {
        var objInfo = new Dictionary<string, object>
        {
            ["name"] = obj.name,
            ["instanceId"] = obj.GetInstanceID(),
            ["active"] = obj.activeInHierarchy,
            ["depth"] = depth,
            ["tag"] = obj.tag,
            ["layer"] = obj.layer,
            ["componentCount"] = obj.GetComponents<Component>().Length
        };
        
        list.Add(objInfo);
        
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            AddGameObjectToListRecursive(obj.transform.GetChild(i).gameObject, list, depth + 1);
        }
    }
    
    private int GetColliderCount(Dictionary<string, int> componentCounts)
    {
        int count = 0;
        string[] colliderTypes = { "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider", "WheelCollider", "TerrainCollider" };
        
        foreach (string type in colliderTypes)
        {
            if (componentCounts.ContainsKey(type))
                count += componentCounts[type];
        }
        
        return count;
    }
    
    private int GetUIElementCount(Dictionary<string, int> componentCounts)
    {
        int count = 0;
        string[] uiTypes = { "Canvas", "Image", "Text", "Button", "ScrollRect", "Slider", "Toggle", "InputField" };
        
        foreach (string type in uiTypes)
        {
            if (componentCounts.ContainsKey(type))
                count += componentCounts[type];
        }
        
        return count;
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (System.Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return string.Format("{0:n1} {1}", number, suffixes[counter]);
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 验证场景路径（如果提供）
        if (parameters.ContainsKey("scenePath"))
        {
            string scenePath = parameters["scenePath"].ToString();
            if (!string.IsNullOrEmpty(scenePath) && !scenePath.EndsWith(".unity"))
            {
                return "scenePath必须以.unity结尾";
            }
        }
        
        return null;
    }
}