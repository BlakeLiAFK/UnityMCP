using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 预制体信息工具 - 获取预制体详细信息
/// </summary>
public class PrefabInfoTool : IMCPTool
{
    public string ToolName => "prefab_get_info";
    
    public string Description => "获取预制体详细信息（组件、变体、实例等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取参数
            string prefabPath = parameters.ContainsKey("prefabPath") ? parameters["prefabPath"].ToString() : "";
            int instanceId = parameters.ContainsKey("instanceId") ? System.Convert.ToInt32(parameters["instanceId"]) : 0;
            bool includeInstances = parameters.ContainsKey("includeInstances") ? System.Convert.ToBoolean(parameters["includeInstances"]) : false;
            bool includeVariants = parameters.ContainsKey("includeVariants") ? System.Convert.ToBoolean(parameters["includeVariants"]) : false;
            
            GameObject prefabAsset = null;
            GameObject targetObject = null;
            
            // 获取预制体资源
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    return MCPResponse.Error($"无法加载预制体: {prefabPath}");
                }
                targetObject = prefabAsset;
            }
            else if (instanceId != 0)
            {
                targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (targetObject == null)
                {
                    return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
                }
                
                // 如果是场景中的预制体实例，获取其预制体资源
                prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
                if (prefabAsset == null)
                {
                    return MCPResponse.Error($"GameObject '{targetObject.name}' 不是预制体实例");
                }
            }
            else
            {
                return MCPResponse.Error("必须提供prefabPath或instanceId中的一个参数");
            }
            
            var result = new Dictionary<string, object>();
            
            // 基本信息
            if (prefabAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
                result["prefabPath"] = assetPath;
                result["prefabName"] = prefabAsset.name;
                result["prefabGuid"] = AssetDatabase.AssetPathToGUID(assetPath);
                
                // 预制体类型信息
                PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
                result["prefabAssetType"] = assetType.ToString();
                
                // 如果是变体，获取基础预制体
                if (assetType == PrefabAssetType.Variant)
                {
                    GameObject basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabAsset);
                    if (basePrefab != null)
                    {
                        result["basePrefab"] = new Dictionary<string, object>
                        {
                            ["path"] = AssetDatabase.GetAssetPath(basePrefab),
                            ["name"] = basePrefab.name,
                            ["guid"] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(basePrefab))
                        };
                    }
                }
            }
            
            // 如果是场景实例，添加实例信息
            if (instanceId != 0)
            {
                result["instanceInfo"] = GetInstanceInfo(targetObject);
            }
            
            // 组件信息
            result["components"] = GetComponentInfo(prefabAsset ?? targetObject);
            
            // 层级结构信息
            result["hierarchy"] = GetHierarchyInfo(prefabAsset ?? targetObject);
            
            // 预制体变体（如果请求）
            if (includeVariants && prefabAsset != null)
            {
                result["variants"] = GetPrefabVariants(prefabAsset);
            }
            
            // 场景中的实例（如果请求）
            if (includeInstances && prefabAsset != null)
            {
                result["sceneInstances"] = GetSceneInstances(prefabAsset);
            }
            
            // 依赖信息
            if (prefabAsset != null)
            {
                result["dependencies"] = GetPrefabDependencies(prefabAsset);
            }
            
            Debug.Log($"成功获取预制体信息: {(prefabAsset?.name ?? targetObject.name)}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取预制体信息时出错: {e.Message}");
            return MCPResponse.Error($"获取预制体信息失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取预制体实例信息
    /// </summary>
    private Dictionary<string, object> GetInstanceInfo(GameObject instance)
    {
        var info = new Dictionary<string, object>
        {
            ["name"] = instance.name,
            ["instanceId"] = instance.GetInstanceID(),
            ["position"] = new Dictionary<string, float>
            {
                ["x"] = instance.transform.position.x,
                ["y"] = instance.transform.position.y,
                ["z"] = instance.transform.position.z
            }
        };
        
        // 预制体连接状态
        PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(instance);
        info["prefabStatus"] = status.ToString();
        
        // 是否有覆盖
        bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
        info["hasOverrides"] = hasOverrides;
        
        if (hasOverrides)
        {
            var overrides = PrefabUtility.GetObjectOverrides(instance);
            info["overrideCount"] = overrides.Count;
        }
        
        return info;
    }
    
    /// <summary>
    /// 获取组件信息
    /// </summary>
    private List<Dictionary<string, object>> GetComponentInfo(GameObject gameObject)
    {
        var components = new List<Dictionary<string, object>>();
        Component[] comps = gameObject.GetComponents<Component>();
        
        foreach (Component comp in comps)
        {
            if (comp != null)
            {
                var compInfo = new Dictionary<string, object>
                {
                    ["type"] = comp.GetType().Name,
                    ["fullType"] = comp.GetType().FullName,
                    ["instanceId"] = comp.GetInstanceID()
                };
                
                // 特殊组件的额外信息
                if (comp is Renderer renderer)
                {
                    compInfo["materials"] = renderer.sharedMaterials?.Where(m => m != null)
                        .Select(m => new Dictionary<string, object>
                        {
                            ["name"] = m.name,
                            ["path"] = AssetDatabase.GetAssetPath(m)
                        }).ToArray();
                }
                else if (comp is MeshFilter meshFilter && meshFilter.sharedMesh != null)
                {
                    compInfo["mesh"] = new Dictionary<string, object>
                    {
                        ["name"] = meshFilter.sharedMesh.name,
                        ["path"] = AssetDatabase.GetAssetPath(meshFilter.sharedMesh),
                        ["vertexCount"] = meshFilter.sharedMesh.vertexCount,
                        ["triangleCount"] = meshFilter.sharedMesh.triangles.Length / 3
                    };
                }
                
                components.Add(compInfo);
            }
        }
        
        return components;
    }
    
    /// <summary>
    /// 获取层级结构信息
    /// </summary>
    private Dictionary<string, object> GetHierarchyInfo(GameObject gameObject)
    {
        var hierarchy = new Dictionary<string, object>
        {
            ["childCount"] = gameObject.transform.childCount
        };
        
        if (gameObject.transform.childCount > 0)
        {
            var children = new List<Dictionary<string, object>>();
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                children.Add(new Dictionary<string, object>
                {
                    ["name"] = child.name,
                    ["instanceId"] = child.GetInstanceID(),
                    ["childCount"] = child.childCount,
                    ["componentCount"] = child.GetComponents<Component>().Length
                });
            }
            hierarchy["children"] = children;
        }
        
        return hierarchy;
    }
    
    /// <summary>
    /// 获取预制体变体
    /// </summary>
    private List<Dictionary<string, object>> GetPrefabVariants(GameObject prefabAsset)
    {
        var variants = new List<Dictionary<string, object>>();
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        
        // 搜索所有预制体变体
        string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
            {
                GameObject basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                if (basePrefab == prefabAsset)
                {
                    variants.Add(new Dictionary<string, object>
                    {
                        ["name"] = prefab.name,
                        ["path"] = path,
                        ["guid"] = guid
                    });
                }
            }
        }
        
        return variants;
    }
    
    /// <summary>
    /// 获取场景中的实例
    /// </summary>
    private List<Dictionary<string, object>> GetSceneInstances(GameObject prefabAsset)
    {
        var instances = new List<Dictionary<string, object>>();
        
        // 搜索当前场景中的所有实例
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            if (source == prefabAsset)
            {
                instances.Add(new Dictionary<string, object>
                {
                    ["name"] = obj.name,
                    ["instanceId"] = obj.GetInstanceID(),
                    ["hasOverrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(obj, false),
                    ["status"] = PrefabUtility.GetPrefabInstanceStatus(obj).ToString()
                });
            }
        }
        
        return instances;
    }
    
    /// <summary>
    /// 获取预制体依赖
    /// </summary>
    private Dictionary<string, object> GetPrefabDependencies(GameObject prefabAsset)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, false);
        
        var depInfo = new Dictionary<string, object>
        {
            ["totalCount"] = dependencies.Length - 1 // 减去自身
        };
        
        var depList = new List<Dictionary<string, object>>();
        foreach (string dep in dependencies)
        {
            if (dep != prefabPath) // 排除自身
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
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 必须提供prefabPath或instanceId中的一个
        bool hasPrefabPath = parameters.ContainsKey("prefabPath") && !string.IsNullOrEmpty(parameters["prefabPath"].ToString());
        bool hasInstanceId = parameters.ContainsKey("instanceId") && int.TryParse(parameters["instanceId"].ToString(), out _);
        
        if (!hasPrefabPath && !hasInstanceId)
        {
            return "必须提供prefabPath或instanceId中的一个参数";
        }
        
        return null;
    }
}