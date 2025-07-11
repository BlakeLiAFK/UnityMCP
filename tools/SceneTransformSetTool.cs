using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景Transform设置工具 - 设置指定GameObject的Transform信息
/// </summary>
public class SceneTransformSetTool : IMCPTool
{
    public string ToolName => "scene_transform_set";
    
    public string Description => "设置场景中指定GameObject的Transform信息(position, rotation, scale)";
    
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
            
            // 记录Undo操作
            Undo.RecordObject(transform, "Set Transform");
            
            // 设置位置
            if (parameters.ContainsKey("position"))
            {
                var posDict = parameters["position"] as Dictionary<string, object>;
                if (posDict != null)
                {
                    Vector3 newPosition = new Vector3(
                        posDict.ContainsKey("x") ? System.Convert.ToSingle(posDict["x"]) : (worldSpace ? transform.position.x : transform.localPosition.x),
                        posDict.ContainsKey("y") ? System.Convert.ToSingle(posDict["y"]) : (worldSpace ? transform.position.y : transform.localPosition.y),
                        posDict.ContainsKey("z") ? System.Convert.ToSingle(posDict["z"]) : (worldSpace ? transform.position.z : transform.localPosition.z)
                    );
                    
                    if (worldSpace)
                    {
                        transform.position = newPosition;
                        Debug.Log($"设置 '{targetObject.name}' 世界位置: {newPosition}");
                    }
                    else
                    {
                        transform.localPosition = newPosition;
                        Debug.Log($"设置 '{targetObject.name}' 本地位置: {newPosition}");
                    }
                }
            }
            
            // 设置旋转
            if (parameters.ContainsKey("rotation"))
            {
                var rotDict = parameters["rotation"] as Dictionary<string, object>;
                if (rotDict != null)
                {
                    Quaternion newRotation;
                    
                    // 检查是否提供了四元数
                    if (rotDict.ContainsKey("quaternion"))
                    {
                        var quatDict = rotDict["quaternion"] as Dictionary<string, object>;
                        if (quatDict != null)
                        {
                            newRotation = new Quaternion(
                                quatDict.ContainsKey("x") ? System.Convert.ToSingle(quatDict["x"]) : 0f,
                                quatDict.ContainsKey("y") ? System.Convert.ToSingle(quatDict["y"]) : 0f,
                                quatDict.ContainsKey("z") ? System.Convert.ToSingle(quatDict["z"]) : 0f,
                                quatDict.ContainsKey("w") ? System.Convert.ToSingle(quatDict["w"]) : 1f
                            );
                        }
                        else
                        {
                            newRotation = worldSpace ? transform.rotation : transform.localRotation;
                        }
                    }
                    // 检查是否提供了欧拉角
                    else if (rotDict.ContainsKey("eulerAngles") || rotDict.ContainsKey("x") || rotDict.ContainsKey("y") || rotDict.ContainsKey("z"))
                    {
                        Vector3 currentEuler = worldSpace ? transform.rotation.eulerAngles : transform.localRotation.eulerAngles;
                        
                        Vector3 newEuler;
                        if (rotDict.ContainsKey("eulerAngles"))
                        {
                            var eulerDict = rotDict["eulerAngles"] as Dictionary<string, object>;
                            if (eulerDict != null)
                            {
                                newEuler = new Vector3(
                                    eulerDict.ContainsKey("x") ? System.Convert.ToSingle(eulerDict["x"]) : currentEuler.x,
                                    eulerDict.ContainsKey("y") ? System.Convert.ToSingle(eulerDict["y"]) : currentEuler.y,
                                    eulerDict.ContainsKey("z") ? System.Convert.ToSingle(eulerDict["z"]) : currentEuler.z
                                );
                            }
                            else
                            {
                                newEuler = currentEuler;
                            }
                        }
                        else
                        {
                            newEuler = new Vector3(
                                rotDict.ContainsKey("x") ? System.Convert.ToSingle(rotDict["x"]) : currentEuler.x,
                                rotDict.ContainsKey("y") ? System.Convert.ToSingle(rotDict["y"]) : currentEuler.y,
                                rotDict.ContainsKey("z") ? System.Convert.ToSingle(rotDict["z"]) : currentEuler.z
                            );
                        }
                        
                        newRotation = Quaternion.Euler(newEuler);
                    }
                    else
                    {
                        newRotation = worldSpace ? transform.rotation : transform.localRotation;
                    }
                    
                    if (worldSpace)
                    {
                        transform.rotation = newRotation;
                        Debug.Log($"设置 '{targetObject.name}' 世界旋转: {newRotation.eulerAngles}");
                    }
                    else
                    {
                        transform.localRotation = newRotation;
                        Debug.Log($"设置 '{targetObject.name}' 本地旋转: {newRotation.eulerAngles}");
                    }
                }
            }
            
            // 设置缩放
            if (parameters.ContainsKey("scale"))
            {
                var scaleDict = parameters["scale"] as Dictionary<string, object>;
                if (scaleDict != null)
                {
                    Vector3 currentScale = transform.localScale;
                    Vector3 newScale = new Vector3(
                        scaleDict.ContainsKey("x") ? System.Convert.ToSingle(scaleDict["x"]) : currentScale.x,
                        scaleDict.ContainsKey("y") ? System.Convert.ToSingle(scaleDict["y"]) : currentScale.y,
                        scaleDict.ContainsKey("z") ? System.Convert.ToSingle(scaleDict["z"]) : currentScale.z
                    );
                    
                    transform.localScale = newScale;
                    Debug.Log($"设置 '{targetObject.name}' 缩放: {newScale}");
                }
            }
            
            // 返回更新后的Transform信息
            var result = new Dictionary<string, object>
            {
                ["gameObjectName"] = targetObject.name,
                ["gameObjectInstanceId"] = targetObject.GetInstanceID(),
                ["worldSpace"] = worldSpace
            };
            
            if (worldSpace)
            {
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
            
            Debug.Log($"成功设置对象 '{targetObject.name}' 的Transform信息");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置Transform信息时出错: {e.Message}");
            return MCPResponse.Error($"设置Transform信息失败: {e.Message}");
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
        
        // 至少需要提供position、rotation或scale中的一个
        if (!parameters.ContainsKey("position") && !parameters.ContainsKey("rotation") && !parameters.ContainsKey("scale"))
        {
            return "至少需要提供position、rotation或scale中的一个参数";
        }
        
        return null;
    }
}
