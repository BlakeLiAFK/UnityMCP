using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景获取工具 - 获取当前场景的层级结构数据
/// </summary>
public class SceneGetTool : IMCPTool
{
    public string ToolName => "scene_get";
    
    public string Description => "获取当前场景的层级结构数据";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            bool includeComponents = parameters.ContainsKey("includeComponents") ? (bool)parameters["includeComponents"] : false;
            bool includeTransform = parameters.ContainsKey("includeTransform") ? (bool)parameters["includeTransform"] : true;
            
            // 获取场景中的所有根对象
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            
            var sceneData = new Dictionary<string, object>
            {
                ["sceneName"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                ["scenePath"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                ["rootObjectCount"] = rootObjects.Length,
                ["gameObjects"] = new List<Dictionary<string, object>>()
            };
            
            var gameObjectsList = (List<Dictionary<string, object>>)sceneData["gameObjects"];
            
            // 遍历所有根对象
            foreach (var rootObj in rootObjects)
            {
                var objData = BuildGameObjectData(rootObj, includeComponents, includeTransform);
                gameObjectsList.Add(objData);
            }
            
            Debug.Log($"成功获取场景层级数据: {rootObjects.Length} 个根对象");
            
            return MCPResponse.Success(sceneData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取场景数据时出错: {e.Message}");
            return MCPResponse.Error($"获取场景数据失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 递归构建GameObject数据
    /// </summary>
    private Dictionary<string, object> BuildGameObjectData(GameObject obj, bool includeComponents, bool includeTransform)
    {
        var objData = new Dictionary<string, object>
        {
            ["name"] = obj.name,
            ["instanceId"] = obj.GetInstanceID(),
            ["active"] = obj.activeInHierarchy,
            ["activeSelf"] = obj.activeSelf,
            ["tag"] = obj.tag,
            ["layer"] = obj.layer,
            ["layerName"] = LayerMask.LayerToName(obj.layer)
        };
        
        // 添加Transform信息
        if (includeTransform && obj.transform != null)
        {
            objData["transform"] = new Dictionary<string, object>
            {
                ["position"] = new Dictionary<string, float>
                {
                    ["x"] = obj.transform.position.x,
                    ["y"] = obj.transform.position.y,
                    ["z"] = obj.transform.position.z
                },
                ["rotation"] = new Dictionary<string, float>
                {
                    ["x"] = obj.transform.rotation.x,
                    ["y"] = obj.transform.rotation.y,
                    ["z"] = obj.transform.rotation.z,
                    ["w"] = obj.transform.rotation.w
                },
                ["scale"] = new Dictionary<string, float>
                {
                    ["x"] = obj.transform.localScale.x,
                    ["y"] = obj.transform.localScale.y,
                    ["z"] = obj.transform.localScale.z
                }
            };
        }
        
        // 添加组件信息
        if (includeComponents)
        {
            var components = obj.GetComponents<Component>();
            var componentList = new List<Dictionary<string, object>>();
            
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    componentList.Add(new Dictionary<string, object>
                    {
                        ["type"] = comp.GetType().Name,
                        ["fullType"] = comp.GetType().FullName,
                        ["enabled"] = comp is Behaviour behaviour ? behaviour.enabled : true
                    });
                }
            }
            
            objData["components"] = componentList;
        }
        
        // 递归处理子对象
        if (obj.transform.childCount > 0)
        {
            var children = new List<Dictionary<string, object>>();
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                children.Add(BuildGameObjectData(child, includeComponents, includeTransform));
            }
            objData["children"] = children;
            objData["childCount"] = obj.transform.childCount;
        }
        else
        {
            objData["childCount"] = 0;
        }
        
        return objData;
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 此工具不需要必需参数，所有参数都是可选的
        return null;
    }
}
