using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 预制体创建工具 - 从场景对象创建预制体
/// </summary>
public class PrefabCreateTool : IMCPTool
{
    public string ToolName => "prefab_create";
    
    public string Description => "从场景对象创建预制体";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("instanceId"))
            {
                return MCPResponse.Error("缺少必需参数: instanceId");
            }
            
            if (!parameters.ContainsKey("prefabPath"))
            {
                return MCPResponse.Error("缺少必需参数: prefabPath");
            }
            
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            string prefabPath = parameters["prefabPath"].ToString();
            bool overwrite = parameters.ContainsKey("overwrite") ? System.Convert.ToBoolean(parameters["overwrite"]) : false;
            
            // 获取场景对象
            GameObject sceneObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (sceneObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 确保路径以.prefab结尾
            if (!prefabPath.EndsWith(".prefab"))
            {
                prefabPath += ".prefab";
            }
            
            // 检查文件是否已存在
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                return MCPResponse.Error($"预制体已存在: {prefabPath}。设置overwrite=true以覆盖。");
            }
            
            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
            
            // 创建预制体
            GameObject prefabAsset;
            if (overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                // 替换现有预制体
                prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(sceneObject, prefabPath, InteractionMode.UserAction);
            }
            else
            {
                // 创建新预制体
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(sceneObject, prefabPath);
            }
            
            if (prefabAsset == null)
            {
                return MCPResponse.Error($"创建预制体失败: {prefabPath}");
            }
            
            // 获取预制体信息
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            
            var result = new Dictionary<string, object>
            {
                ["prefabPath"] = prefabPath,
                ["prefabName"] = prefabAsset.name,
                ["prefabGuid"] = guid,
                ["sourceObjectName"] = sceneObject.name,
                ["sourceInstanceId"] = instanceId,
                ["overwritten"] = overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null,
                ["created"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 获取预制体的组件信息
            var components = new List<Dictionary<string, object>>();
            Component[] prefabComponents = prefabAsset.GetComponents<Component>();
            foreach (Component comp in prefabComponents)
            {
                if (comp != null)
                {
                    components.Add(new Dictionary<string, object>
                    {
                        ["type"] = comp.GetType().Name,
                        ["fullType"] = comp.GetType().FullName
                    });
                }
            }
            result["components"] = components;
            
            // 获取子对象信息
            Transform[] childTransforms = prefabAsset.GetComponentsInChildren<Transform>();
            result["childCount"] = childTransforms.Length - 1; // 减去根对象
            
            if (childTransforms.Length > 1)
            {
                var children = new List<string>();
                foreach (Transform child in childTransforms)
                {
                    if (child != prefabAsset.transform)
                    {
                        children.Add(child.name);
                    }
                }
                result["childNames"] = children;
            }
            
            Debug.Log($"成功创建预制体: {sceneObject.name} -> {prefabPath}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建预制体时出错: {e.Message}");
            return MCPResponse.Error($"创建预制体失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        if (!parameters.ContainsKey("prefabPath"))
        {
            return "缺少必需参数: prefabPath";
        }
        
        // 验证instanceId是否为有效数字
        if (!int.TryParse(parameters["instanceId"].ToString(), out _))
        {
            return "instanceId必须是有效的整数";
        }
        
        // 验证prefabPath
        string prefabPath = parameters["prefabPath"].ToString();
        if (string.IsNullOrEmpty(prefabPath))
        {
            return "prefabPath不能为空";
        }
        
        // 检查路径是否在Assets目录下
        if (!prefabPath.StartsWith("Assets/"))
        {
            return "prefabPath必须在Assets目录下";
        }
        
        return null;
    }
}