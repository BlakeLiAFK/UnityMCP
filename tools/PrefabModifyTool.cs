using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 预制体修改工具 - 应用或恢复预制体实例的修改
/// </summary>
public class PrefabModifyTool : IMCPTool
{
    public string ToolName => "prefab_modify";
    
    public string Description => "管理预制体实例的修改（应用到预制体或恢复修改）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("instanceId"))
            {
                return MCPResponse.Error("缺少必需参数: instanceId");
            }
            
            if (!parameters.ContainsKey("operation"))
            {
                return MCPResponse.Error("缺少必需参数: operation");
            }
            
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            string operation = parameters["operation"].ToString().ToLower();
            
            // 获取预制体实例
            GameObject instance = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (instance == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 检查是否为预制体实例
            PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(instance);
            if (status == PrefabInstanceStatus.NotAPrefab)
            {
                return MCPResponse.Error($"GameObject '{instance.name}' 不是预制体实例");
            }
            
            var result = new Dictionary<string, object>
            {
                ["instanceName"] = instance.name,
                ["instanceId"] = instanceId,
                ["operation"] = operation,
                ["originalStatus"] = status.ToString()
            };
            
            switch (operation)
            {
                case "apply":
                case "apply_all":
                    return ApplyOverrides(instance, result);
                    
                case "revert":
                case "revert_all":
                    return RevertOverrides(instance, result);
                    
                case "unpack":
                    return UnpackPrefab(instance, result);
                    
                case "disconnect":
                    return DisconnectPrefab(instance, result);
                    
                case "check_overrides":
                    return CheckOverrides(instance, result);
                    
                default:
                    return MCPResponse.Error($"不支持的操作: {operation}。支持的操作: apply, revert, unpack, disconnect, check_overrides");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"修改预制体时出错: {e.Message}");
            return MCPResponse.Error($"修改预制体失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 应用覆盖到预制体
    /// </summary>
    private MCPResponse ApplyOverrides(GameObject instance, Dictionary<string, object> result)
    {
        try
        {
            // 检查是否有覆盖
            bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
            if (!hasOverrides)
            {
                result["message"] = "没有覆盖需要应用";
                result["appliedOverrides"] = 0;
                return MCPResponse.Success(result);
            }
            
            // 获取预制体资源路径
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            
            // 获取覆盖列表
            var overrides = PrefabUtility.GetObjectOverrides(instance);
            var propertyOverrides = PrefabUtility.GetPropertyModifications(instance);
            
            result["prefabPath"] = prefabPath;
            result["overrideCount"] = overrides.Count;
            result["propertyOverrideCount"] = propertyOverrides?.Length ?? 0;
            
            // 应用所有覆盖
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);
            
            result["success"] = true;
            result["message"] = $"成功应用 {overrides.Count} 个覆盖到预制体";
            
            Debug.Log($"成功将 '{instance.name}' 的覆盖应用到预制体: {prefabPath}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            result["error"] = e.Message;
            return MCPResponse.Error($"应用覆盖失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 恢复覆盖
    /// </summary>
    private MCPResponse RevertOverrides(GameObject instance, Dictionary<string, object> result)
    {
        try
        {
            // 检查是否有覆盖
            bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
            if (!hasOverrides)
            {
                result["message"] = "没有覆盖需要恢复";
                result["revertedOverrides"] = 0;
                return MCPResponse.Success(result);
            }
            
            // 获取覆盖信息
            var overrides = PrefabUtility.GetObjectOverrides(instance);
            var propertyOverrides = PrefabUtility.GetPropertyModifications(instance);
            
            result["overrideCount"] = overrides.Count;
            result["propertyOverrideCount"] = propertyOverrides?.Length ?? 0;
            
            // 恢复所有覆盖
            PrefabUtility.RevertPrefabInstance(instance, InteractionMode.UserAction);
            
            result["success"] = true;
            result["message"] = $"成功恢复 {overrides.Count} 个覆盖";
            
            Debug.Log($"成功恢复 '{instance.name}' 的所有覆盖");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            result["error"] = e.Message;
            return MCPResponse.Error($"恢复覆盖失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 解包预制体
    /// </summary>
    private MCPResponse UnpackPrefab(GameObject instance, Dictionary<string, object> result)
    {
        try
        {
            // 解包预制体（完全断开连接）
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            
            result["success"] = true;
            result["unpackedInstanceId"] = instance.GetInstanceID();
            result["message"] = "成功解包预制体，已完全断开与预制体的连接";
            
            Debug.Log($"成功解包预制体: '{instance.name}'");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            result["error"] = e.Message;
            return MCPResponse.Error($"解包预制体失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 断开预制体连接
    /// </summary>
    private MCPResponse DisconnectPrefab(GameObject instance, Dictionary<string, object> result)
    {
        try
        {
            // 断开预制体连接但保持层级结构
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            
            result["success"] = true;
            result["message"] = "成功断开预制体连接，保持了层级结构";
            
            Debug.Log($"成功断开预制体连接: '{instance.name}'");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            result["error"] = e.Message;
            return MCPResponse.Error($"断开预制体连接失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 检查覆盖信息
    /// </summary>
    private MCPResponse CheckOverrides(GameObject instance, Dictionary<string, object> result)
    {
        try
        {
            bool hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false);
            result["hasOverrides"] = hasOverrides;
            
            if (hasOverrides)
            {
                // 获取对象覆盖
                var objectOverrides = PrefabUtility.GetObjectOverrides(instance);
                var objectOverrideList = new List<Dictionary<string, object>>();
                
                foreach (var objOverride in objectOverrides)
                {
                    objectOverrideList.Add(new Dictionary<string, object>
                    {
                        ["instanceObject"] = objOverride.instanceObject?.name ?? "Unknown"
                    });
                }
                
                result["objectOverrides"] = objectOverrideList;
                result["objectOverrideCount"] = objectOverrides.Count;
                
                // 获取属性覆盖
                var propertyOverrides = PrefabUtility.GetPropertyModifications(instance);
                if (propertyOverrides != null)
                {
                    var propertyOverrideList = new List<Dictionary<string, object>>();
                    
                    foreach (var propOverride in propertyOverrides)
                    {
                        propertyOverrideList.Add(new Dictionary<string, object>
                        {
                            ["propertyPath"] = propOverride.propertyPath,
                            ["value"] = propOverride.value ?? "null",
                            ["objectReference"] = propOverride.objectReference?.name ?? "None",
                            ["target"] = propOverride.target?.name ?? "Unknown"
                        });
                    }
                    
                    result["propertyOverrides"] = propertyOverrideList;
                    result["propertyOverrideCount"] = propertyOverrides.Length;
                }
                else
                {
                    result["propertyOverrideCount"] = 0;
                }
            }
            else
            {
                result["objectOverrideCount"] = 0;
                result["propertyOverrideCount"] = 0;
                result["message"] = "没有检测到覆盖";
            }
            
            // 获取预制体信息
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (prefabAsset != null)
            {
                result["prefabInfo"] = new Dictionary<string, object>
                {
                    ["name"] = prefabAsset.name,
                    ["path"] = AssetDatabase.GetAssetPath(prefabAsset),
                    ["guid"] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabAsset))
                };
            }
            
            result["success"] = true;
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            result["error"] = e.Message;
            return MCPResponse.Error($"检查覆盖失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        if (!parameters.ContainsKey("operation"))
        {
            return "缺少必需参数: operation";
        }
        
        // 验证instanceId是否为有效数字
        if (!int.TryParse(parameters["instanceId"].ToString(), out _))
        {
            return "instanceId必须是有效的整数";
        }
        
        // 验证operation
        string operation = parameters["operation"].ToString().ToLower();
        string[] validOperations = { "apply", "apply_all", "revert", "revert_all", "unpack", "disconnect", "check_overrides" };
        
        bool isValidOperation = false;
        foreach (string validOp in validOperations)
        {
            if (operation == validOp)
            {
                isValidOperation = true;
                break;
            }
        }
        
        if (!isValidOperation)
        {
            return $"不支持的操作: {operation}。支持的操作: {string.Join(", ", validOperations)}";
        }
        
        return null;
    }
}