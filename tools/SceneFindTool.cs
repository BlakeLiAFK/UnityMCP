using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景对象查找工具 - 按条件查找场景中的GameObject
/// </summary>
public class SceneFindTool : IMCPTool
{
    public string ToolName => "scene_find_objects";
    
    public string Description => "按条件查找场景中的GameObject（名称、标签、组件等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取搜索参数
            string objectName = parameters.ContainsKey("name") ? parameters["name"].ToString() : "";
            string tag = parameters.ContainsKey("tag") ? parameters["tag"].ToString() : "";
            string componentType = parameters.ContainsKey("componentType") ? parameters["componentType"].ToString() : "";
            string layer = parameters.ContainsKey("layer") ? parameters["layer"].ToString() : "";
            bool activeOnly = parameters.ContainsKey("activeOnly") ? System.Convert.ToBoolean(parameters["activeOnly"]) : false;
            bool exactMatch = parameters.ContainsKey("exactMatch") ? System.Convert.ToBoolean(parameters["exactMatch"]) : false;
            int maxResults = parameters.ContainsKey("maxResults") ? System.Convert.ToInt32(parameters["maxResults"]) : 100;
            string scenePath = parameters.ContainsKey("scenePath") ? parameters["scenePath"].ToString() : "";
            
            var result = new Dictionary<string, object>
            {
                ["searchCriteria"] = new Dictionary<string, object>
                {
                    ["name"] = objectName,
                    ["tag"] = tag,
                    ["componentType"] = componentType,
                    ["layer"] = layer,
                    ["activeOnly"] = activeOnly,
                    ["exactMatch"] = exactMatch,
                    ["maxResults"] = maxResults,
                    ["scenePath"] = scenePath
                },
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 确定搜索范围
            List<GameObject> searchObjects;
            Scene targetScene;
            
            if (!string.IsNullOrEmpty(scenePath))
            {
                targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.IsValid())
                {
                    return MCPResponse.Error($"场景未加载或不存在: {scenePath}");
                }
                searchObjects = GetSceneObjects(targetScene);
                result["searchScope"] = $"指定场景: {targetScene.name}";
            }
            else
            {
                targetScene = SceneManager.GetActiveScene();
                searchObjects = GetAllSceneObjects();
                result["searchScope"] = "所有已加载的场景";
            }
            
            result["totalObjectsSearched"] = searchObjects.Count;
            
            // 执行搜索
            var foundObjects = new List<Dictionary<string, object>>();
            
            foreach (GameObject obj in searchObjects)
            {
                if (foundObjects.Count >= maxResults)
                    break;
                
                if (MatchesSearchCriteria(obj, objectName, tag, componentType, layer, activeOnly, exactMatch))
                {
                    foundObjects.Add(CreateObjectInfo(obj));
                }
            }
            
            result["foundObjects"] = foundObjects;
            result["foundCount"] = foundObjects.Count;
            result["limitReached"] = foundObjects.Count >= maxResults;
            
            // 搜索统计
            result["searchStatistics"] = CreateSearchStatistics(foundObjects);
            
            Debug.Log($"场景对象搜索完成，找到 {foundObjects.Count} 个匹配对象");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"查找场景对象时出错: {e.Message}");
            return MCPResponse.Error($"查找场景对象失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取指定场景的所有对象
    /// </summary>
    private List<GameObject> GetSceneObjects(Scene scene)
    {
        var objects = new List<GameObject>();
        
        if (!scene.isLoaded)
            return objects;
        
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject rootObj in rootObjects)
        {
            AddObjectAndChildrenRecursive(rootObj, objects);
        }
        
        return objects;
    }
    
    /// <summary>
    /// 获取所有已加载场景的对象
    /// </summary>
    private List<GameObject> GetAllSceneObjects()
    {
        var objects = new List<GameObject>();
        
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObj in rootObjects)
                {
                    AddObjectAndChildrenRecursive(rootObj, objects);
                }
            }
        }
        
        return objects;
    }
    
    /// <summary>
    /// 递归添加对象及其子对象
    /// </summary>
    private void AddObjectAndChildrenRecursive(GameObject obj, List<GameObject> list)
    {
        list.Add(obj);
        
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            AddObjectAndChildrenRecursive(obj.transform.GetChild(i).gameObject, list);
        }
    }
    
    /// <summary>
    /// 检查对象是否符合搜索条件
    /// </summary>
    private bool MatchesSearchCriteria(GameObject obj, string objectName, string tag, string componentType, 
        string layer, bool activeOnly, bool exactMatch)
    {
        // 检查激活状态
        if (activeOnly && !obj.activeInHierarchy)
            return false;
        
        // 检查名称
        if (!string.IsNullOrEmpty(objectName))
        {
            bool nameMatches = exactMatch ? 
                string.Equals(obj.name, objectName, System.StringComparison.OrdinalIgnoreCase) :
                obj.name.IndexOf(objectName, System.StringComparison.OrdinalIgnoreCase) >= 0;
            
            if (!nameMatches)
                return false;
        }
        
        // 检查标签
        if (!string.IsNullOrEmpty(tag))
        {
            if (!string.Equals(obj.tag, tag, System.StringComparison.OrdinalIgnoreCase))
                return false;
        }
        
        // 检查组件
        if (!string.IsNullOrEmpty(componentType))
        {
            bool hasComponent = false;
            Component[] components = obj.GetComponents<Component>();
            
            foreach (Component comp in components)
            {
                if (comp != null)
                {
                    string compTypeName = comp.GetType().Name;
                    string compFullTypeName = comp.GetType().FullName;
                    
                    if (string.Equals(compTypeName, componentType, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(compFullTypeName, componentType, System.StringComparison.OrdinalIgnoreCase) ||
                        compFullTypeName.EndsWith("." + componentType, System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasComponent = true;
                        break;
                    }
                }
            }
            
            if (!hasComponent)
                return false;
        }
        
        // 检查层级
        if (!string.IsNullOrEmpty(layer))
        {
            bool layerMatches = false;
            
            // 尝试按层级名称匹配
            string layerName = LayerMask.LayerToName(obj.layer);
            if (string.Equals(layerName, layer, System.StringComparison.OrdinalIgnoreCase))
            {
                layerMatches = true;
            }
            // 尝试按层级数字匹配
            else if (int.TryParse(layer, out int layerNumber) && obj.layer == layerNumber)
            {
                layerMatches = true;
            }
            
            if (!layerMatches)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 创建对象信息
    /// </summary>
    private Dictionary<string, object> CreateObjectInfo(GameObject obj)
    {
        var info = new Dictionary<string, object>
        {
            ["name"] = obj.name,
            ["instanceId"] = obj.GetInstanceID(),
            ["active"] = obj.activeInHierarchy,
            ["activeSelf"] = obj.activeSelf,
            ["tag"] = obj.tag,
            ["layer"] = obj.layer,
            ["layerName"] = LayerMask.LayerToName(obj.layer),
            ["sceneName"] = obj.scene.name,
            ["scenePath"] = obj.scene.path
        };
        
        // 位置信息
        Transform transform = obj.transform;
        info["transform"] = new Dictionary<string, object>
        {
            ["position"] = new Dictionary<string, float>
            {
                ["x"] = transform.position.x,
                ["y"] = transform.position.y,
                ["z"] = transform.position.z
            },
            ["rotation"] = new Dictionary<string, float>
            {
                ["x"] = transform.eulerAngles.x,
                ["y"] = transform.eulerAngles.y,
                ["z"] = transform.eulerAngles.z
            },
            ["scale"] = new Dictionary<string, float>
            {
                ["x"] = transform.localScale.x,
                ["y"] = transform.localScale.y,
                ["z"] = transform.localScale.z
            }
        };
        
        // 层级信息
        info["hierarchy"] = new Dictionary<string, object>
        {
            ["childCount"] = transform.childCount,
            ["siblingIndex"] = transform.GetSiblingIndex(),
            ["parentName"] = transform.parent?.name ?? "None",
            ["parentInstanceId"] = transform.parent?.GetInstanceID() ?? 0,
            ["depth"] = GetObjectDepth(transform)
        };
        
        // 组件信息
        Component[] components = obj.GetComponents<Component>();
        var componentList = new List<Dictionary<string, object>>();
        
        foreach (Component comp in components)
        {
            if (comp != null)
            {
                componentList.Add(new Dictionary<string, object>
                {
                    ["type"] = comp.GetType().Name,
                    ["fullType"] = comp.GetType().FullName,
                    ["instanceId"] = comp.GetInstanceID()
                });
            }
        }
        
        info["components"] = componentList;
        info["componentCount"] = componentList.Count;
        
        // 预制体信息
        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
            
            info["prefabInfo"] = new Dictionary<string, object>
            {
                ["isPrefabInstance"] = true,
                ["prefabAssetPath"] = prefabAsset != null ? AssetDatabase.GetAssetPath(prefabAsset) : "Unknown",
                ["prefabRootName"] = prefabRoot?.name ?? "Unknown",
                ["hasOverrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(prefabRoot, false)
            };
        }
        else
        {
            info["prefabInfo"] = new Dictionary<string, object>
            {
                ["isPrefabInstance"] = false
            };
        }
        
        return info;
    }
    
    /// <summary>
    /// 获取对象深度
    /// </summary>
    private int GetObjectDepth(Transform transform)
    {
        int depth = 0;
        Transform current = transform.parent;
        
        while (current != null)
        {
            depth++;
            current = current.parent;
        }
        
        return depth;
    }
    
    /// <summary>
    /// 创建搜索统计信息
    /// </summary>
    private Dictionary<string, object> CreateSearchStatistics(List<Dictionary<string, object>> foundObjects)
    {
        if (foundObjects.Count == 0)
        {
            return new Dictionary<string, object>
            {
                ["totalFound"] = 0
            };
        }
        
        var stats = new Dictionary<string, object>
        {
            ["totalFound"] = foundObjects.Count,
            ["activeObjects"] = foundObjects.Count(obj => (bool)obj["active"]),
            ["inactiveObjects"] = foundObjects.Count(obj => !(bool)obj["active"])
        };
        
        // 按标签统计
        var tagCounts = new Dictionary<string, int>();
        foreach (var obj in foundObjects)
        {
            string tag = obj["tag"].ToString();
            tagCounts[tag] = tagCounts.ContainsKey(tag) ? tagCounts[tag] + 1 : 1;
        }
        stats["tagDistribution"] = tagCounts;
        
        // 按层级统计
        var layerCounts = new Dictionary<string, int>();
        foreach (var obj in foundObjects)
        {
            string layerName = obj["layerName"].ToString();
            layerCounts[layerName] = layerCounts.ContainsKey(layerName) ? layerCounts[layerName] + 1 : 1;
        }
        stats["layerDistribution"] = layerCounts;
        
        // 按场景统计
        var sceneCounts = new Dictionary<string, int>();
        foreach (var obj in foundObjects)
        {
            string sceneName = obj["sceneName"].ToString();
            sceneCounts[sceneName] = sceneCounts.ContainsKey(sceneName) ? sceneCounts[sceneName] + 1 : 1;
        }
        stats["sceneDistribution"] = sceneCounts;
        
        // 预制体统计
        int prefabInstances = foundObjects.Count(obj => 
        {
            var prefabInfo = obj["prefabInfo"] as Dictionary<string, object>;
            return prefabInfo != null && (bool)prefabInfo["isPrefabInstance"];
        });
        stats["prefabInstances"] = prefabInstances;
        stats["nonPrefabObjects"] = foundObjects.Count - prefabInstances;
        
        return stats;
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 验证maxResults参数
        if (parameters.ContainsKey("maxResults"))
        {
            if (!int.TryParse(parameters["maxResults"].ToString(), out int maxResults) || maxResults <= 0)
            {
                return "maxResults必须是大于0的整数";
            }
            
            if (maxResults > 1000)
            {
                return "maxResults不能超过1000";
            }
        }
        
        // 验证场景路径（如果提供）
        if (parameters.ContainsKey("scenePath"))
        {
            string scenePath = parameters["scenePath"].ToString();
            if (!string.IsNullOrEmpty(scenePath) && !scenePath.EndsWith(".unity"))
            {
                return "scenePath必须以.unity结尾";
            }
        }
        
        // 至少需要一个搜索条件
        bool hasSearchCriteria = 
            parameters.ContainsKey("name") && !string.IsNullOrEmpty(parameters["name"].ToString()) ||
            parameters.ContainsKey("tag") && !string.IsNullOrEmpty(parameters["tag"].ToString()) ||
            parameters.ContainsKey("componentType") && !string.IsNullOrEmpty(parameters["componentType"].ToString()) ||
            parameters.ContainsKey("layer") && !string.IsNullOrEmpty(parameters["layer"].ToString());
        
        if (!hasSearchCriteria)
        {
            return "至少需要提供一个搜索条件: name, tag, componentType, 或 layer";
        }
        
        return null;
    }
}