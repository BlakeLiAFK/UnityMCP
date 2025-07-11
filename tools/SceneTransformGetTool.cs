using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景Transform获取工具 - 获取指定GameObject的Transform信息
/// </summary>
public class SceneTransformGetTool : IMCPTool
{
    public string ToolName => "scene_transform_get";
    
    public string Description => "获取场景中指定GameObject的Transform信息(position, rotation, scale)";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            bool worldSpace = parameters.ContainsKey("worldSpace") ? (bool)parameters["worldSpace"] : true;
            
            // 通过InstanceID查找GameObject
            GameObject targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (targetObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            Transform transform = targetObject.transform;
            
            var result = new Dictionary<string, object>
            {
                ["gameObjectName"] = targetObject.name,
                ["gameObjectInstanceId"] = targetObject.GetInstanceID(),
                ["worldSpace"] = worldSpace
            };
            
            if (worldSpace)
            {
                // 世界坐标系
                result["position"] = new Dictionary<string, float>
                {
                    ["x"] = transform.position.x,
                    ["y"] = transform.position.y,
                    ["z"] = transform.position.z
                };
                
                result["rotation"] = new Dictionary<string, object>
                {
                    ["quaternion"] = new Dictionary<string, float>
                    {
                        ["x"] = transform.rotation.x,
                        ["y"] = transform.rotation.y,
                        ["z"] = transform.rotation.z,
                        ["w"] = transform.rotation.w
                    },
                    ["eulerAngles"] = new Dictionary<string, float>
                    {
                        ["x"] = transform.rotation.eulerAngles.x,
                        ["y"] = transform.rotation.eulerAngles.y,
                        ["z"] = transform.rotation.eulerAngles.z
                    }
                };
                
                result["scale"] = new Dictionary<string, float>
                {
                    ["x"] = transform.lossyScale.x,
                    ["y"] = transform.lossyScale.y,
                    ["z"] = transform.lossyScale.z
                };
            }
            else
            {
                // 本地坐标系
                result["position"] = new Dictionary<string, float>
                {
                    ["x"] = transform.localPosition.x,
                    ["y"] = transform.localPosition.y,
                    ["z"] = transform.localPosition.z
                };
                
                result["rotation"] = new Dictionary<string, object>
                {
                    ["quaternion"] = new Dictionary<string, float>
                    {
                        ["x"] = transform.localRotation.x,
                        ["y"] = transform.localRotation.y,
                        ["z"] = transform.localRotation.z,
                        ["w"] = transform.localRotation.w
                    },
                    ["eulerAngles"] = new Dictionary<string, float>
                    {
                        ["x"] = transform.localRotation.eulerAngles.x,
                        ["y"] = transform.localRotation.eulerAngles.y,
                        ["z"] = transform.localRotation.eulerAngles.z
                    }
                };
                
                result["scale"] = new Dictionary<string, float>
                {
                    ["x"] = transform.localScale.x,
                    ["y"] = transform.localScale.y,
                    ["z"] = transform.localScale.z
                };
            }
            
            // 添加层级信息
            result["parent"] = transform.parent != null ? new Dictionary<string, object>
            {
                ["name"] = transform.parent.name,
                ["instanceId"] = transform.parent.gameObject.GetInstanceID()
            } : null;
            
            result["childCount"] = transform.childCount;
            
            if (transform.childCount > 0)
            {
                var children = new List<Dictionary<string, object>>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    children.Add(new Dictionary<string, object>
                    {
                        ["name"] = child.name,
                        ["instanceId"] = child.gameObject.GetInstanceID()
                    });
                }
                result["children"] = children;
            }
            
            Debug.Log($"成功获取对象 '{targetObject.name}' 的Transform信息 ({(worldSpace ? "世界" : "本地")}坐标系)");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取Transform信息时出错: {e.Message}");
            return MCPResponse.Error($"获取Transform信息失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            return "参数不能为空";
        }
        
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        try
        {
            System.Convert.ToInt32(parameters["instanceId"]);
        }
        catch
        {
            return "instanceId必须是有效的整数";
        }
        
        return null;
    }
}
