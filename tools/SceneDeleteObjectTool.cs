using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景删除对象工具 - 删除场景中的GameObject
/// </summary>
public class SceneDeleteObjectTool : IMCPTool
{
    public string ToolName => "scene_delete_object";
    
    public string Description => "删除场景中的GameObject";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("instanceId"))
            {
                return MCPResponse.Error("缺少必需参数: instanceId");
            }
            
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            bool deleteChildren = parameters.ContainsKey("deleteChildren") ? 
                System.Convert.ToBoolean(parameters["deleteChildren"]) : true;
            
            // 获取GameObject
            GameObject targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (targetObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 收集删除信息
            var result = new Dictionary<string, object>
            {
                ["deletedObject"] = new Dictionary<string, object>
                {
                    ["name"] = targetObject.name,
                    ["instanceId"] = targetObject.GetInstanceID(),
                    ["tag"] = targetObject.tag,
                    ["layer"] = targetObject.layer,
                    ["sceneName"] = targetObject.scene.name
                },
                ["deleteChildren"] = deleteChildren,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 统计子对象信息
            int childCount = 0;
            var childNames = new List<string>();
            
            if (deleteChildren)
            {
                CountChildrenRecursive(targetObject.transform, ref childCount, childNames);
                result["childrenDeleted"] = childCount;
                result["childNames"] = childNames;
            }
            else
            {
                // 如果不删除子对象，将子对象移动到父级
                Transform parent = targetObject.transform.parent;
                var movedChildren = new List<Dictionary<string, object>>();
                
                for (int i = targetObject.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = targetObject.transform.GetChild(i);
                    child.SetParent(parent);
                    
                    movedChildren.Add(new Dictionary<string, object>
                    {
                        ["name"] = child.name,
                        ["instanceId"] = child.GetInstanceID(),
                        ["newParent"] = parent?.name ?? "Root"
                    });
                }
                
                result["movedChildren"] = movedChildren;
                result["movedChildrenCount"] = movedChildren.Count;
            }
            
            // 获取父对象信息
            if (targetObject.transform.parent != null)
            {
                result["parentObject"] = new Dictionary<string, object>
                {
                    ["name"] = targetObject.transform.parent.name,
                    ["instanceId"] = targetObject.transform.parent.GetInstanceID()
                };
            }
            
            // 检查是否为预制体实例
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(targetObject);
            if (isPrefabInstance)
            {
                result["wasPrefabInstance"] = true;
                GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
                if (prefabAsset != null)
                {
                    result["prefabAssetPath"] = AssetDatabase.GetAssetPath(prefabAsset);
                }
            }
            else
            {
                result["wasPrefabInstance"] = false;
            }
            
            // 注册Undo操作
            Undo.DestroyObjectImmediate(targetObject);
            
            result["success"] = true;
            result["message"] = $"成功删除GameObject: {targetObject.name}" + 
                (deleteChildren && childCount > 0 ? $" 及其 {childCount} 个子对象" : "");
            
            Debug.Log($"成功删除GameObject: {targetObject.name} (InstanceID: {instanceId})" + 
                (deleteChildren && childCount > 0 ? $" 及其 {childCount} 个子对象" : ""));
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除GameObject时出错: {e.Message}");
            return MCPResponse.Error($"删除GameObject失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 递归计算子对象数量和名称
    /// </summary>
    private void CountChildrenRecursive(Transform parent, ref int count, List<string> names)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            count++;
            names.Add(child.name);
            
            // 递归计算子对象的子对象
            CountChildrenRecursive(child, ref count, names);
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        // 验证instanceId是否为有效数字
        if (!int.TryParse(parameters["instanceId"].ToString(), out _))
        {
            return "instanceId必须是有效的整数";
        }
        
        return null;
    }
}