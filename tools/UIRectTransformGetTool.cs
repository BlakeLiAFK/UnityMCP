using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// UI RectTransform获取工具 - 获取UI元素的RectTransform信息
/// </summary>
public class UIRectTransformGetTool : IMCPTool
{
    public string ToolName => "ui_rect_transform_get";
    
    public string Description => "获取UI元素的RectTransform信息（位置、大小、锚点等）";
    
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
            bool includeWorldSpace = parameters.ContainsKey("includeWorldSpace") ? 
                System.Convert.ToBoolean(parameters["includeWorldSpace"]) : true;
            
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
            
            // 构建RectTransform信息
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
                ["localPosition"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.localPosition.x,
                    ["y"] = rectTransform.localPosition.y,
                    ["z"] = rectTransform.localPosition.z
                },
                ["localRotation"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.localEulerAngles.x,
                    ["y"] = rectTransform.localEulerAngles.y,
                    ["z"] = rectTransform.localEulerAngles.z
                },
                ["localScale"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.localScale.x,
                    ["y"] = rectTransform.localScale.y,
                    ["z"] = rectTransform.localScale.z
                },
                ["rect"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.rect.x,
                    ["y"] = rectTransform.rect.y,
                    ["width"] = rectTransform.rect.width,
                    ["height"] = rectTransform.rect.height
                }
            };
            
            // 如果需要包含世界空间信息
            if (includeWorldSpace)
            {
                // 获取世界空间的四个角点
                Vector3[] worldCorners = new Vector3[4];
                rectTransform.GetWorldCorners(worldCorners);
                
                result["worldPosition"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.position.x,
                    ["y"] = rectTransform.position.y,
                    ["z"] = rectTransform.position.z
                };
                
                result["worldRotation"] = new Dictionary<string, float>
                {
                    ["x"] = rectTransform.eulerAngles.x,
                    ["y"] = rectTransform.eulerAngles.y,
                    ["z"] = rectTransform.eulerAngles.z
                };
                
                result["worldCorners"] = new Dictionary<string, object>
                {
                    ["bottomLeft"] = new Dictionary<string, float>
                    {
                        ["x"] = worldCorners[0].x,
                        ["y"] = worldCorners[0].y,
                        ["z"] = worldCorners[0].z
                    },
                    ["topLeft"] = new Dictionary<string, float>
                    {
                        ["x"] = worldCorners[1].x,
                        ["y"] = worldCorners[1].y,
                        ["z"] = worldCorners[1].z
                    },
                    ["topRight"] = new Dictionary<string, float>
                    {
                        ["x"] = worldCorners[2].x,
                        ["y"] = worldCorners[2].y,
                        ["z"] = worldCorners[2].z
                    },
                    ["bottomRight"] = new Dictionary<string, float>
                    {
                        ["x"] = worldCorners[3].x,
                        ["y"] = worldCorners[3].y,
                        ["z"] = worldCorners[3].z
                    }
                };
            }
            
            // 获取父级Canvas信息
            Canvas parentCanvas = rectTransform.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                result["parentCanvas"] = new Dictionary<string, object>
                {
                    ["name"] = parentCanvas.name,
                    ["instanceId"] = parentCanvas.GetInstanceID(),
                    ["renderMode"] = parentCanvas.renderMode.ToString(),
                    ["sortingOrder"] = parentCanvas.sortingOrder
                };
            }
            
            // 获取父级RectTransform信息
            if (rectTransform.parent != null)
            {
                RectTransform parentRect = rectTransform.parent as RectTransform;
                if (parentRect != null)
                {
                    result["parentRectTransform"] = new Dictionary<string, object>
                    {
                        ["name"] = parentRect.name,
                        ["instanceId"] = parentRect.GetInstanceID()
                    };
                }
            }
            
            Debug.Log($"成功获取UI元素 '{gameObject.name}' 的RectTransform信息");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取RectTransform信息时出错: {e.Message}");
            return MCPResponse.Error($"获取RectTransform信息失败: {e.Message}");
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