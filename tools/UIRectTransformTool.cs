using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// UI RectTransform设置工具 - 设置UI元素的位置、大小和锚点
/// </summary>
public class UIRectTransformTool : IMCPTool
{
    public string ToolName => "ui_rect_transform_set";
    
    public string Description => "设置UI元素的RectTransform属性（位置、大小、锚点等）";
    
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
            
            // 获取GameObject
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (gameObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 获取RectTransform组件
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return MCPResponse.Error($"GameObject '{gameObject.name}' 没有RectTransform组件，可能不是UI元素");
            }
            
            // 记录Undo操作
            Undo.RecordObject(rectTransform, "Set RectTransform Properties");
            
            // 设置锚点最小值
            if (parameters.ContainsKey("anchorMin"))
            {
                var anchorMinDict = parameters["anchorMin"] as Dictionary<string, object>;
                if (anchorMinDict != null)
                {
                    Vector2 anchorMin = new Vector2(
                        anchorMinDict.ContainsKey("x") ? System.Convert.ToSingle(anchorMinDict["x"]) : rectTransform.anchorMin.x,
                        anchorMinDict.ContainsKey("y") ? System.Convert.ToSingle(anchorMinDict["y"]) : rectTransform.anchorMin.y
                    );
                    rectTransform.anchorMin = anchorMin;
                    Debug.Log($"设置 '{gameObject.name}' 的 anchorMin: {anchorMin}");
                }
            }
            
            // 设置锚点最大值
            if (parameters.ContainsKey("anchorMax"))
            {
                var anchorMaxDict = parameters["anchorMax"] as Dictionary<string, object>;
                if (anchorMaxDict != null)
                {
                    Vector2 anchorMax = new Vector2(
                        anchorMaxDict.ContainsKey("x") ? System.Convert.ToSingle(anchorMaxDict["x"]) : rectTransform.anchorMax.x,
                        anchorMaxDict.ContainsKey("y") ? System.Convert.ToSingle(anchorMaxDict["y"]) : rectTransform.anchorMax.y
                    );
                    rectTransform.anchorMax = anchorMax;
                    Debug.Log($"设置 '{gameObject.name}' 的 anchorMax: {anchorMax}");
                }
            }
            
            // 设置轴心点
            if (parameters.ContainsKey("pivot"))
            {
                var pivotDict = parameters["pivot"] as Dictionary<string, object>;
                if (pivotDict != null)
                {
                    Vector2 pivot = new Vector2(
                        pivotDict.ContainsKey("x") ? System.Convert.ToSingle(pivotDict["x"]) : rectTransform.pivot.x,
                        pivotDict.ContainsKey("y") ? System.Convert.ToSingle(pivotDict["y"]) : rectTransform.pivot.y
                    );
                    rectTransform.pivot = pivot;
                    Debug.Log($"设置 '{gameObject.name}' 的 pivot: {pivot}");
                }
            }
            
            // 设置尺寸增量
            if (parameters.ContainsKey("sizeDelta"))
            {
                var sizeDeltaDict = parameters["sizeDelta"] as Dictionary<string, object>;
                if (sizeDeltaDict != null)
                {
                    Vector2 sizeDelta = new Vector2(
                        sizeDeltaDict.ContainsKey("x") ? System.Convert.ToSingle(sizeDeltaDict["x"]) : rectTransform.sizeDelta.x,
                        sizeDeltaDict.ContainsKey("y") ? System.Convert.ToSingle(sizeDeltaDict["y"]) : rectTransform.sizeDelta.y
                    );
                    rectTransform.sizeDelta = sizeDelta;
                    Debug.Log($"设置 '{gameObject.name}' 的 sizeDelta: {sizeDelta}");
                }
            }
            
            // 设置锚点位置
            if (parameters.ContainsKey("anchoredPosition"))
            {
                var anchoredPosDict = parameters["anchoredPosition"] as Dictionary<string, object>;
                if (anchoredPosDict != null)
                {
                    Vector2 anchoredPosition = new Vector2(
                        anchoredPosDict.ContainsKey("x") ? System.Convert.ToSingle(anchoredPosDict["x"]) : rectTransform.anchoredPosition.x,
                        anchoredPosDict.ContainsKey("y") ? System.Convert.ToSingle(anchoredPosDict["y"]) : rectTransform.anchoredPosition.y
                    );
                    rectTransform.anchoredPosition = anchoredPosition;
                    Debug.Log($"设置 '{gameObject.name}' 的 anchoredPosition: {anchoredPosition}");
                }
            }
            
            // 设置旋转
            if (parameters.ContainsKey("rotation"))
            {
                var rotDict = parameters["rotation"] as Dictionary<string, object>;
                if (rotDict != null)
                {
                    Vector3 eulerAngles = new Vector3(
                        rotDict.ContainsKey("x") ? System.Convert.ToSingle(rotDict["x"]) : rectTransform.eulerAngles.x,
                        rotDict.ContainsKey("y") ? System.Convert.ToSingle(rotDict["y"]) : rectTransform.eulerAngles.y,
                        rotDict.ContainsKey("z") ? System.Convert.ToSingle(rotDict["z"]) : rectTransform.eulerAngles.z
                    );
                    rectTransform.eulerAngles = eulerAngles;
                    Debug.Log($"设置 '{gameObject.name}' 的 rotation: {eulerAngles}");
                }
            }
            
            // 设置缩放
            if (parameters.ContainsKey("scale"))
            {
                var scaleDict = parameters["scale"] as Dictionary<string, object>;
                if (scaleDict != null)
                {
                    Vector3 scale = new Vector3(
                        scaleDict.ContainsKey("x") ? System.Convert.ToSingle(scaleDict["x"]) : rectTransform.localScale.x,
                        scaleDict.ContainsKey("y") ? System.Convert.ToSingle(scaleDict["y"]) : rectTransform.localScale.y,
                        scaleDict.ContainsKey("z") ? System.Convert.ToSingle(scaleDict["z"]) : rectTransform.localScale.z
                    );
                    rectTransform.localScale = scale;
                    Debug.Log($"设置 '{gameObject.name}' 的 scale: {scale}");
                }
            }
            
            // 返回设置后的RectTransform信息
            var result = new Dictionary<string, object>
            {
                ["name"] = gameObject.name,
                ["instanceId"] = instanceId,
                ["anchorMin"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.anchorMin.x,
                    ["y"] = rectTransform.anchorMin.y
                },
                ["anchorMax"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.anchorMax.x,
                    ["y"] = rectTransform.anchorMax.y
                },
                ["pivot"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.pivot.x,
                    ["y"] = rectTransform.pivot.y
                },
                ["sizeDelta"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.sizeDelta.x,
                    ["y"] = rectTransform.sizeDelta.y
                },
                ["anchoredPosition"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.anchoredPosition.x,
                    ["y"] = rectTransform.anchoredPosition.y
                },
                ["rotation"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.eulerAngles.x,
                    ["y"] = rectTransform.eulerAngles.y,
                    ["z"] = rectTransform.eulerAngles.z
                },
                ["scale"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.localScale.x,
                    ["y"] = rectTransform.localScale.y,
                    ["z"] = rectTransform.localScale.z
                }
            };
            
            Debug.Log($"成功设置UI元素 '{gameObject.name}' 的RectTransform属性");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置RectTransform属性时出错: {e.Message}");
            return MCPResponse.Error($"设置RectTransform属性失败: {e.Message}");
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